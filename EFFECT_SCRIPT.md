# EFFECT_SCRIPT — 视觉效应代数剧本（L1 增量设计）

> 状态：设计冻结（待 3 轮独立数学家对抗性审计）
> 范围：纯 L1 增量（`Cosmos.EffectAlgebra/`，**零 Godot 依赖**）。**不动** §7 白名单 / L2 Generator / L3 Analyzer。
> 目标：把「AI 看参考图/视频 → 写视觉效果」这一步，落地为一层**可静态验证、可符号探索、不跑游戏**的代数剧本类型。
> 出处：所有类型严格复用 PDR §3.1–§3.3 已建 L1。新增类型不引入任何未证明的代数结构。

---

## 1. 动机与边界（为什么需要这一层）

用户远景：给 AI 一张图/一段演示视频 → AI 分析元素 → 写需求剧本 → 直接实现「视频效果」。

关键判断：**AI 不应直接产出 Godot 代码**（黑箱、不可审计、跑了才知道崩）。AI 应产出一层次级：**代数剧本（Effect Script）**——纯数据，描述「某时间窗内屏幕上有哪些视觉元素、各占用/释放什么资源」。剧本喂给已建的 L1 代数，在**没渲染、没跑游戏**之前即可验证：资源不泄露、显存峰值不超预算、作用域不冲突。

本设计**只做类型系统那半**：让「视觉代数剧本」在数学上成立、可验证。AI 的视觉理解（图/视频→元素）与真实 Godot 播放，不在本层（见 §7 诚实边界）。

### 1.1 与既有成果的关系（零新增代数）

一个视觉效果 = 时间域上，每时刻屏幕是一组**图层**的合成。每个图层（视觉元素）天然可被既有 L1 建模：

| 视觉元素属性 | 既有 L1 载体 | 出处 |
| --- | --- | --- |
| 存在时间窗 | `Interval`（§3.1.5 / Exact / Dynamic / Default） | Numeric.cs |
| 占用 / 释放的资源 | `Claim` + `ResourceId`（`Gpu`/`CommandBuffer`/`Memory`/`Occupancy`…） | Objects.cs |
| 作用域（图层 / CanvasLayer 层级） | `ScopeId` 偏序（`IncludedIn` ⊆*） | Objects.cs |
| 每帧代价（size） | `Claim.Size : Interval` | Objects.cs |
| 同作用域兄弟是否兼容 | `Compatible.IsCompatible`（§3.2.3 全函数） | Algebra.cs |
| 该时刻总占用 | `Signature.Union`（§3.2.1 半格并） | Objects.cs |
| 资源守恒 / 泄漏 | `NetTable.IsConserved`（§3.3.1） | Algebra.cs |
| 显存 / 命令缓冲峰值 | `Peak.Compute`（§3.3.2） | Algebra.cs |
| 循环 / 粒子 | `LoopCount`（ω∈ℕ∪{⊤}, §3.2.5）+ `Combination.Loop` | DerivedMetrics.cs |

→ **一个视觉剧本 = 时间 → Signature 的函数** `Script : Time → Signature`。而 `Signature.Union`（iter19 已证结合/交换/幂等）天然表达「某时刻所有存活图层叠加后的总占用」。

**结论：无需新造代数结构。剧本层只把 `Claim`「抬到时间轴」——新增 2 个组合值类型 + 1 个预算壳。**

> **语义注记（OPEN-N2，来自 iter-effect01 审计）：** `LoopCount` 在 PDR §3.2.5 定义为「循环重复次数 ω」。本设计在剧本层将其**重新解释为「同一时刻的并发副本数」**——即 ω 份**瞬时并发**的相同元素（如粒子系统同时存在的粒子数），而非时间上的重复次数。二者在代数缩放上一致（都是复制 ω 份：`size×ω`，ω=⊤⇒上界开放），但概念含义不同：元素的时间长度由 `Lifetime` 承载，ω 只表达「某一刻有多少份同时存在」。建模约定：**时长→`Lifetime`，并发密度→`LoopCount`**。后续维护者须避免将二者混淆。

---

## 2. 核心类型（最小集合）

### 2.1 `EffectEvent` — 一个视觉元素的生命周期

```csharp
// §视觉剧本核心单位：一个视觉元素 = 一段存在时间 + 一份资源签名。
// 视觉上 sprite/图层是原子的 ⇒ 不拆成 N 个 TimedClaim，而是把 Claim 分组进 Event（最小性的关键选择）。
public readonly record struct EffectEvent
{
    public Interval Lifetime { get; }     // §3.1.5 — [lo,hi]；hi=⊤ ⇒ 上界开放（常驻层）
    public Signature Footprint { get; }   // §3.1.4b — 该元素存活期内的资源签名（三桶）
    public LoopCount Loop { get; }        // §3.2.5 — ω；默认 LoopCount.Of(1)（非循环）
}
```

语义：`Lifetime ∋ t` ⇔ `Lifetime.Lo ≤ t ∧ t ≤ Lifetime.Hi`（`NatStar.CompareToFinite`，hi=⊤ 视为无上界）。
`Loop`：ω 有限时 Footprint 经 `Combination.Loop(Footprint, ω, scope)` 缩放（§3.2.5）；ω=⊤ 时上界开放 ⇒ 该元素持续占用 ⇒ 正确标为非守恒（除非本就是常驻背景层）。

> **边界注记（OPEN-B4，来自 iter-effect03_14 审计）：** `Lifetime.Lo` 必须有限。`Interval` 构造子允许退化 `[⊤,⊤]`（Lo=⊤ 且 Hi=⊤），此时 `Lo.CompareToFinite(t)>0` 恒真 ⇒ 该 Event 在任意有限 t 均「不存活」，完全脱离 `At(t)` 与审计（fail-open：未知生命周期诚实不审计）。用户须给出有限起点 `Lo`；`[⊤,⊤]` 视为非法输入而非错误项。

> 类型强制（用户铁律）：5 字段位置记录 ⇒ 构造即全必填，不存在漏字段的 Event。`Lifetime`/`Footprint`/`Loop` 的数学边界由既有类型承载。

### 2.2 `EffectScript` — 剧本 = 有限个 Event 的集合

```csharp
public readonly record struct EffectScript
{
    public ImmutableArray<EffectEvent> Events { get; }

    // §核心算子 — 某时刻 t 的屏幕总签名 = 所有 lifetime∋t 的 Footprint 做 Union（§3.2.1 半格并）。
    public Signature At(NatStar t);

    // §验证 — 端点采样审计（见 §3，完备且精确，非近似）。
    public AuditResult Audit(Budget cap);

    // 确定性：At(t) 是纯函数（Signature 不可变值类型，Union 交换/结合/幂等）⇒ 同剧本同 t ⇒ 同签名。
}
```

`At(t)` 实现：遍历 `Events`，对满足 `e.Lifetime ∋ t` 者取 `Combination.Loop(e.Footprint, e.Loop, loopScope)` 后 `Signature.Union` 累积。ω=⊤ 的 Event 在任意 t 都「存活」（若其 `Lo ≤ t`）。

### 2.3 `Budget` — 每资源上限壳（fail-closed 默认 ⊤）

```csharp
public readonly record struct Budget
{
    // 缺省 = 该资源无上限（⊤）。仅填关心的资源（显存 / 命令缓冲 / 通道数…）。
    public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }
}
```

> 设计意图：`Budget` 是「软约束壳」——不写进代数内核，因为上限是工程策略不是数学性质。审计时 `Peak.At(t) ≤ cap`（经 `NatStar.CompareToFinite`）超限即报。

---

## 3. 验证正确性论证（为什么是 correct 而非近似）

**端点采样定理**：`At(t)` 是分段时间常数函数——它仅在各 `Lifetime` 的端点（lo / hi）改变取值，区间内部恒常。`Union` 对区间内部单调不变。因此：

> **完整验证 = 收集所有 Event 的 distinct `Lo`/`Hi` 端点（有限集），逐个采样 `At(t)`，对每个采样点检查 3 类不变量。**

这是**完备且精确**的，不是 Kimi 讨论中担心的「近似」——因为 Union 是逐点分段常数，端点采样 = 全量检查。正确性完全落在已证的代数上（iter19 `Union` 律 / iter27 区间算术 / iter16 溢出⇒⊤ / iter04-05 `SignedNet` 有符号 net），**未引入任何未经证明的新代数**。

### 3.1 采样点不变量

对每采样时刻 `t`：

| 不变量 | 算式 | 出处 | 失败含义 |
| --- | --- | --- | --- |
| **资源守恒**（无泄漏） | `∀r ∈ script.Resources : Net(At(t), scope).IsConserved(r)` | §3.3.1 / DO-9 | 某资源 create 与 release 不配对（GPU/纹理/回调泄漏） |
| **峰值预算** | `Peak(At(t), scope) ≤ Budget.Caps[r]`（`NatStar.CompareToFinite`） | §3.3.2 | 显存 / 命令缓冲 / 并发占用超预算（overdraw 爆炸） |
| **作用域兼容** | 对 `At(t)` 内同 scope 兄弟 Claim，`Compatible.IsCompatible` 全通过 | §3.2.3 全函数 | 同资源两 create 冲突 / Z 层级冲突（CONFLICT 集） |

> `Net`/`Peak`/`Compatible` 的 ⊤ 律（`IsTop` ⇒ fail-closed 返回「不守恒 / 超限 / 不兼容」）天然贯穿，无需额外处理。`⊤` 一律交人工确认，不静默漏报。

### 3.2 审计产物

```csharp
public readonly record struct AuditResult
{
    public bool Passed { get; }
    // 反例：首个违例的 (时间 t, 资源 r, 类型, 当前值 vs 上限)。供 AI 直接回修剧本。
    public ImmutableArray<Violation> Violations { get; }
}
```

---

## 4. AI 契约（数据形态，不经 §7 白名单）

AI **不写 Godot 代码**，只产出 `EffectScript` 数据（JSON），直接喂 L1 验证。**不经 Godot API、不经 §7 白名单**——是独立数据契约：

```json
{
  "events": [
    { "lifetime": [0, 120], "loop": 1,
      "footprint": [
        { "kind": "occupy", "resource": {"gpu": {"bufferId":"mesh1"}}, "mode": "create",
          "scope": {"scene":"Battle"}, "size": [1,1] },
        { "kind": "occupy", "resource": {"commandBuffer":"gpu"}, "mode": "create",
          "scope": {"scene":"Battle"}, "size": [1,1] }
      ] },
    { "lifetime": [60, 180], "loop": 0,
      "footprint": [
        { "kind": "occupy", "resource": {"commandBuffer":"gpu"}, "mode": "release",
          "scope": {"scene":"Battle"}, "size": [1,1] }
      ] }
  ],
  "budget": { "commandBuffer:gpu": 64 }
}
```

验证失败 → `AuditResult.Violations` 返回反例（哪个时刻、哪个资源、超什么界）→ AI 改 JSON 重投。**闭环无需运行游戏。**

---

## 5. 确定性 / 符号探索（AI 自动玩的地基）

`At(t)` 是纯函数（`Signature` 不可变值类型，`Union` 交换/结合/幂等）。因此：

- 同剧本 + 同 t ⇒ 同签名（确定性，免费获得）。
- AI 可在「类型合法的时刻 + 合法图层组合」空间内符号探索，反例 100% 可复现（与 LANDING_PLAN §5 性质测试同构）。
- 这正是「不跑游戏也能审计 + AI 自动找不合理」的数学地基。

---

## 6. 与 Godot API 包装的关系（澄清范围）

- **不需要包装 Godot API。** 现有 L2/L3 已是**非侵入式**：按方法名规范化匹配 §7 白名单，游戏侧照常写 `DrawMesh`/`Instantiate`/`QueueFree`，不引进任何门面类。
- 本设计**完全在 L1**（零 Godot）。视觉剧本是 **L1 数据契约**，与 Godot 运行期解耦。
- 仅当「把剧本生成 Godot 代码」或「分析真实 Godot 代码」时才动 L2/L3——那是**后续**扩展（§7.2），本轮**不实现**，只在文档标注。

---

## 7. 诚实边界（哪些不在本层）

| 范围 | 状态 | 处理 |
| --- | --- | --- |
| 资源守恒 / 显存峰值 / overdraw 预算 | ✅ 本层代数覆盖 | `IsConserved` + `Peak` + `Budget` |
| Z-index 冲突 / 同作用域不兼容 | ✅ `Compatible` + `ScopeId` | 已建，采样点检查 |
| 时间循环 / 确定性 | ✅ `LoopCount` + 纯函数 | 已建 |
| **像素级布局越界（rect 几何）** | ⚠️ 后续小扩展 | 需新增 `LayoutClaim`（独立小补丁，**非核心**，见 §7.1） |
| **美感 / 手感 / 光照氛围** | ❌ 无形式化定义 | 必须人工或视觉判别模型，与 §10/§12 同口径 out-of-scope |
| **真实 Godot 播放** | ❌ 需 Godot SDK | 与 §10 同口径 out-of-scope |
| **AI 视觉理解（图/视频→元素）** | ❌ 外部 ML | 不属于类型系统，本设计只接其产物 |

### 7.1 后续小扩展（非核心，明确排期外）

- **`LayoutClaim`**：为像素级布局越界（rect/anchor/z 数值）补一个最小 kind，落 `EffectEvent` 的附加字段；由 L3 Analyzer 查 `EAA0401 LayoutOverflow`。不引入新代数，只是给 `Claim` 加一个几何承载。
- **L2/L3 剧本生成**：把 `EffectScript` 反向经 `GodotApiWhitelist` 生成 Godot 调用；或把真实 Godot 代码分析成脚本做对比。扩展既有的 L2 Generator / L3 Analyzer 模式，不重写。

---

## 8. 实现清单（落到代码时的精确接口）

新增文件 `src/Cosmos.EffectAlgebra/EffectScript.cs`，仅含：

1. `EffectEvent` record struct（§2.1）
2. `EffectScript` record struct + `At(NatStar)` + `Audit(Budget)`（§2.2/§3）
3. `Budget` record struct（§2.3）
4. `AuditResult` + `Violation`（§3.2）
5. 端点采样私法：收集 distinct Lo/Hi → 逐点 `At` → 三类不变量检查（§3.1）

**复用调用为零新增代数**：`Interval` / `Signature.Union` / `NetTable.Compute` / `Derived.IsConserved` / `Peak.Compute` / `Compatible.IsCompatible` / `Combination.Loop` / `LoopCount` / `ResourceId.Normalize` / `ScopeId.IncludedIn`。

测试（下一轮迭代补）：endpoint 采样完备性、守恒反例、峰值超限、循环 ω=⊤ 非守恒、确定性。

---

## 9. 一句话总结

> **视觉代数剧本 = 把已建 L1 的 `Claim` 抬到时间轴：一个 `EffectEvent`（lifetime + footprint + loop）是原子的视觉元素，一个 `EffectScript` 是有限 Event 集，`At(t)` 用已证的 `Union` 合成某时刻总签名。验证靠「端点采样」——因 Union 分段常数，采样即全量、精确非近似。整套零新增代数、零 Godot 依赖，AI 只产出剧本数据，不写代码。**

---

## 10. 审计轨迹与性能收口（Jeff Dean 视角）

### 10.1 30 轮迭代闭环（#116–#148）

以「对抗性数学审计 + 实现 + 测试 + 修复」循环推进，每轮独立 subagent 审计，证据落盘 `audit/iter-effect0N.md` / `audit/iter-effectNN.md`。关键进展：

| 轮次 | 主题 | 关键修复 | 审计落盘 |
| --- | --- | --- | --- |
| Iter1 (#119) | 骨架 + OPEN-1 | `EffectEvent` 自带 `Scope`（消除自由变量 `loopScope`） | `audit/iter-effect01.md` |
| Iter2 (#120) | 守恒累积 | 居民层 ω=⊤ 豁免，双趟 `hasFinitePos`/`hasTopPos` 去序相关 | `audit/iter-effect01.md` |
| Iter3–14 (#121–132) | 循环 ω / 兼容分组 / 预算 ⊤ / 代数定律 / 随机性质 / 溢出→⊤ / 非线性 / 确定性 / 类型硬化 / 居民豁免 | 端点采样完备性 + 反例测试 | `audit/iter-effect03_14.md` |
| Iter11 (#129) | JSON 契约（AI 数据格式） | `EffectScriptContract.Parse/ToJson` 快速失败 `FormatException` | `audit/iter-effect03_14.md` |
| Iter21/22/23 (#139–141) | 全 Compatible 矩阵 / 1000 fuzz / 端点采样==密集扫描 | 穷举 + HashSet 确定性比对 | `audit/iter-effect26.md` |
| Iter24 (#142) | §7 形状一致（不发明新 ResourceId kind） | `ResourceKinds_MatchKnownSet` 集合比对 | `audit/iter-effect26.md` |
| Iter25/26 (#143–146) | **性能**：Jeff Dean 审计 O(S·E²)→O(E·K·log E) 扫换线重写 | 见 §10.2 | `audit/iter-effect26.md` + `audit/iter-effect28-review.md` |

### 10.2 性能审计结论（Jeff Dean 视角，iter-effect26.md）

**诊断**：原 `Audit` 每采样点全量重算 `At`/`CumulativeNet`，且 gate(3) 两两配对 ⇒ 最坏 **O(S·E²)**（S 采样点，E 事件）。对「数千粒子同屏同资源」的 AI 视觉脚本，尾延迟与内存随事件数平方恶化——与「不跑游戏也能审计」目标冲突。

**修复**：扫换线（sweep-line）。端点排序一次 O(E·log E)，沿时间轴增量维护①运行中累积 net ②每资源运行中峰值（含 ⊤ 计数）③每 `(resource,scope,mode)` 活跃事件集合；每个事件仅进入/退出各处理一次 ⇒ **O(E·K·log E)**、内存 **O(E·K)**，消除 S 乘子与 gate(3) 平方。

**等价性证明**：`audit/iter-effect26.md` 给出不变量证明 + 独立暴力参考实现 `ReferenceAudit`（用 public API 重写旧 O(S·E²) 语义）与扫换线 `Audit` 在 100 随机 + 4 对抗形状上**逐条 Violation 集合相等**（测试 `Iter26_SweepLine_EqualsBruteForce_*`）。

**规模实测（阈值测试通过）**：5000 同资源同 scope 重叠粒子审计 <2s（朴素 O(S·E²) 在此输入下会卡死/分钟级）；1000 事件 <5s。

**独立审查（fresh-context reviewer, `audit/iter-effect28-review.md`）**：确认四相位扫换线精确等价于 `alive ⇔ Lo≤t≤Hi`，三道 gate 与旧语义逐条对齐，**PASS**；等价性另有 `ReferenceAudit` 集相等断言背书。

### 10.3 最终状态

- `dotnet build` 0 error / 0 warning（TreatWarningsAsErrors 开启）。
- `dotnet test` **279 passed / 0 failed**（EffectScript 子系统 ~63 项 + 基座 212 项）。
- 类型即边界：所有载体 `readonly record struct`，构造即全必填；注释承载 ω 语义、端点采样定理与 § 出处。
- 零 Godot 依赖；AI 产出 `EffectScript` JSON（§4），`Audit()` 返回可回修反例，闭环无需运行游戏。
