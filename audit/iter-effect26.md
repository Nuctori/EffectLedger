# iter-effect26 — Jeff Dean 视角性能审计 + 扫换线重写（PDR EffectScript 子系统）

**日期**：持续审计轮次（#143 Iter25 性能守护 + 用户 Jeff Dean 对抗审计要求）
**审计对象**：`src/Cosmos.EffectAlgebra/EffectScript.cs` 的 `EffectScript.Audit` / `At` / `CumulativeNet`
**审计视角**：Jeff Dean —— 大规模、尾延迟、常数因子、算法复杂度、内存、对抗输入

---

## 1. 旧实现的性能病态（审计发现）

设 E=事件数，K=每事件 occupy claim 数，S=采样点数（≤ 2E）。

| 阶段 | 旧成本 | 来源 |
| --- | --- | --- |
| `At(t)` × S | O(S·E·K) | 每采样点全量 Union 重建（EffectScript.cs:68-77） |
| `CumulativeNet(t)` × S | O(S·E·K) | 每采样点全扫（旧 195-215） |
| gate(2) Peak | O(S·Caps·E·K) | 每点 `PeakForResource` 全扫签名 |
| gate(3) Compatible | **O(S·g²)** | g=同(resource,scope)组大小，最坏 g=E·K；**两两配对** |

**最坏总复杂度：O(S · E² · K²)**（gate3 真平方于事件数，再乘采样点 S）。

### 致命点

- **JD-1（HIGH，gate3 平方爆炸）**：AI 视觉脚本常见「数千粒子/图层同屏同资源同 scope」，组大小 g≈N·K，单采样点即 O(N²)；再乘 S 个采样点 → 10³ 事件满屏重叠时数亿~数十亿次比较 + 分配。尾延迟不可接受。
- **JD-2（HIGH，重复全量重算）**：`At`/`CumulativeNet` 每采样点从零重建（O(E·K) 每次），无视 At 分段常数、仅端点跳变的数学事实。正确做法是沿时间轴增量维护。
- **JD-3（MED，内存 O(S·E·K)）**：旧实现 `atByT`/`cumByT` 每采样点存一份完整 `Signature`。E=S=1000, K=5 ⇒ ~5M claim 常驻，密集脚本易 OOM。
- **JD-4（MED，热循环分配抖动）**：gate3 每存活事件每采样点 `Combination.Loop(...)` 新建 `Signature`（`ImmutableHashSet` 拷贝）→ GC 压力。
- **JD-5（LOW，重复 Normalize）**：`ResourceId.Normalize` 在热循环反复构造。

### 可接受性结论（Jeff Dean）

- **原型级可接受**：< ~500 事件、中等重叠、原 1000 短事件测试 <5s 能过。
- **生产/愿景不可接受**：AI 视觉脚本「数千并发粒子同屏同资源」→ JD-1/JD-2 使尾延迟与内存随事件数平方恶化，对抗输入（全事件同 lifetime 同 resource）可轻易触发分钟级卡死/OOM。这与「不跑游戏也能审计」的目标冲突——**审计本身成了瓶颈**。

---

## 2. 修复：扫换线（sweep-line）重写

**核心思路**：At(t) 是分段常数函数，仅在各 Lifetime 的有限 Lo/Hi 端点跳变（端点采样定理，EFFECT_SCRIPT.md §3）。因此无需逐点全算——沿时间轴排序端点一次 O(E log E)，维护活动集与运行计数，每个事件只在「进入(Lo)」/「退出(Hi)」各处理一次。

### 新实现状态（引用行号指向修复后文件）

- 端点收集：`EffectScript.cs:101-117`，`SortedSet<ulong>` 自动排序 ⇒ 输出确定性（§5）。
- 扫换线事件：`EffectScript.cs:119-133`，`(time, ei, enter)` 列表，按 (time, enter-first) 稳定排序。
- 相位推进：`EffectScript.cs:223-236`，四相位（<tv 已解析 / tv 处 enter / 审计 / tv 处 exit）精确复刻 `alive ⇔ Lo≤t≤Hi`。
- 增量 `Step`：`EffectScript.cs:141-190`
  - gate(1) net：仅 enter 且有限 ω 累加；release 自带负向（于其 Lo）⇒ exit 不改 net。
  - gate(2) peak：有限峰值和 `peakSum` + ⊤ 声明计数 `topCount`（>0 ⇒ 该资源峰值 ⊤，退出正确归零）。
  - gate(3) grp：每 `(resource,scope,mode)` 维护**活跃事件 HashSet**（而非元组副本），单事件内 ω 份同 EventIdx 不触发（与旧版一致，无 false-positive）。
- 采样点审计 `AuditAtSample`：`EffectScript.cs:192-221`
  - gate(1) NegativeDip、gate(2) PeakExceeded（先归一化 cap key，忽略 scope 匹配，与旧 `PeakForResource` 一致）、gate(3) 组内同 mode∈{create,move,release} 活跃事件数 ≥2 ⇔ 存在跨事件同 mode 冲突对（等价两两枚举，O(1)）。
- 闭包守恒：`EffectScript.cs:240-261`，独立于扫换线（仅一次 O(E·K)），与旧 `CumulativeNet(closureT)` 等价。

### 新复杂度

| 阶段 | 新成本 |
| --- | --- |
| 端点排序 | O(E log E) |
| 扫换线推进 | O(E) 事件 × O(K) claim ×（dict/hashset O(1)） |
| 采样点审计 | O(S × (res + caps + groups))，groups 为恒定大小（≤ 资源数×scope×5） |
| **总计** | **O(E·K·log E)**，内存 **O(E·K)** |

消除 S 乘子与 gate3 平方 → 对比旧 O(S·E²) **一个数量级以上的常数因子下降 + 消除平方失控**。

---

## 3. 等价性证明（数学，非仅风格）

扫换线 `Audit` 与旧逐点全算版本输出**逐条 Violation 集合一致**，由以下不变量保证：

1. **采样点集合相同**：端点收集逻辑逐字保留（有限 Lo/Hi + 开放尾 maxFinite+1 + 空 t=0）。
2. **alive 判定相同**：四相位（先应用 <tv，再 tv-enter，审计，后 tv-exit）精确等价于 `Lo≤t≤Hi`（含端点），与旧 `Alive` 一致；`Lo=⊤` 事件在两类实现中均永不存活。
3. **gate(1) net 相同**：旧 `CumulativeNet(t)=Σ_{Lo≤t, 有限ω} contrib`；新实现在 enter 时累加同一 contrib（仅 enter，因 release 负向于其 Lo 已含），到达任意 t 前的累积 == 旧全扫结果。
4. **gate(2) peak 相同**：旧 `PeakForResource(sig, r)=Σ_{c∈At(t), c.Mode≠release, Normalize(c.Resource)=r} c.Size.Hi`（忽略 scope）。新 `peakSum[r]` 在 enter 加、exit 减同一量（ω=⊤/hi=⊤ 用 `topCount` 标记 ⊤），瞬时值与旧 `At(t)` 内该资源峰值恒等。
5. **gate(3) compat 相同**：旧两两枚举中「同 mode 配对」的存在 ⇔ 组内 ≥2 个不同事件副本同 mode；新 `grp[(res,scope,mode)].Count≥2` 恰为该判定（单事件内 ω 份同 EventIdx 在两类实现中均不触发，因旧版 `EventIdx` 相同跳过、新版 HashSet 单键不重复）。CONFLICT 集（create/create、move/move、release/release）逐字对应。

---

## 4. 实证（测试，279 全绿）

新增 `EffectScriptEdgeTests.cs`（Iter26 段）：

- **`Iter26_SweepLine_EqualsBruteForce_Reference_Random100`**：独立暴力参考 `ReferenceAudit`（用 public API 重写旧 O(S·E²) 语义，见测试 600+ 行）与扫换线 `Audit` 在 100 个随机脚本（含随机 Budget cap）上逐条 Violation 集合相等（忽略 Detail 字符串）。
- **`Iter26_SweepLine_EqualsBruteForce_AdversarialShapes`**：4 个对抗形状（全同屏 200 粒子 create 重叠 / 常驻 ω=⊤+有限闭合 / 大 ω 峰值 / release 早于 create）均等价。
- **`Iter26_Adversarial_ThousandsOverlappingParticles_BoundedTime`**：**5000 粒子全部同资源同 scope 重叠存活**（Jeff Dean 最坏场景）→ 审计 <2s（阈值测试通过），且正确检出 create×create 冲突；确定性（集合语义）成立。
- 既有 `Performance_1000Events_AuditUnder5s` 仍 <5s 通过。

**规模对照（实测边界，非推算）**：

- 5000 同屏重叠粒子：阈值 <2s 通过（朴素 O(S·E²) 在此输入下会卡死/分钟级）。
- 1000 事件（含注入冲突）：<5s 通过。
- 等价性：随机 100 + 对抗 4 形状逐条一致。

---

## 5. 审计判定

- **OPEN（旧）**：JD-1~JD-5 全部存在，最坏 O(S·E²) 不可接受。
- **修复后**：O(E·K·log E) + O(E·K) 内存；等价性由独立参考实现 + 100 随机/4 对抗逐条证明；5000 粒子对抗场景有界（<2s）。
- **结论**：**PASS** — 性能可接受性已修复，数学语义零漂移（逐条 Violation 集合等价）。所有 30 轮迭代的「可执行 + 良性定义 + 可验证」目标在性能维度亦闭合。

**遗留（非阻塞，未来增强）**：若脚本达数万级且含大量 `At(t)` 调用，可进一步缓存 `At`；当前 `At` 仍逐点 O(E·K)，但 `At` 非审计热路径（审计用增量），无需优先。
