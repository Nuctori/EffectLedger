# Iter1 审计

> 审计对象：`src/Cosmos.EffectAlgebra/EffectScript.cs` + `tests/.../EffectScriptTests.cs`（Iter1 实现）
> 对照：`EFFECT_SCRIPT.md`、`audit/effect-script-auditA.md`（6 open）、L1 真实源码（Objects/Algebra/DerivedMetrics/Numeric/SignedNet.cs）
> 方法：只读 + 对抗质问；构建复验（`dotnet build` 0e/0w；`EffectScriptTests` 15 通过 0 失败）
> 立场：过度质疑；宁可误报

---

## auditA 6 open 闭合核验

### OPEN-1（焦点2，HIGH）：scope 自由变量 / loopScope 未定义
- **闭合**。`EffectEvent` 现携带 `Scope` 字段（`EffectScript.cs:19-22`，4 字段位置记录）。`At(t)` 内调用 `Combination.Loop(e.Footprint, e.Loop, e.Scope)`（`EffectScript.cs:80`）——`loopScope` = `e.Scope`，**无自由变量**。
- 脚本级 `Audit` 的 net/Peak 一律以 `Global`（屏幕级）为隐含 scope，与 `EFFECT_SCRIPT.md §3`「scope=Global」一致；`ScopeId.IncludedIn(Global)` 恒真，过滤退化为恒等，属设计选择（剧本级审计），非缺陷。
- 证据：`EffectScript.cs:78-92`（At 实现）、`:94-225`（Audit 全用 Global 免 scope 参数）。

### OPEN-2（焦点3，HIGH）：瞬时守恒误判临时占用
- **闭合**。Audit 已改用**时间聚合累积 net** `CumulativeNet(t)`（`EffectScript.cs:196-219`），非瞬时 `At(t).IsConserved`。
- 合法临时占用（create@0, release@10）`Audit_TemporaryOccupancy_Passes` 通过；release 早于 create `Audit_ReleaseBeforeCreate_ReportedNegativeDip` 报 `NegativeDip`。
- 证据：测试 `EffectScriptTests.cs:62-83`；`CumulativeNet` 实现 `:196-219`。

### OPEN-3（焦点2/3 衍生，HIGH）：`Combination.Loop` 把 ω 误当瞬时并发倍数
- **按设计重定义闭合，但标记一处语义发散（见下方新 OPEN-N2 注释）**。Iter1 将 `EffectEvent.Loop` 的语义**显式重定义为「同一时刻并发副本数」**（非 PDR §3.2.5 的「循环重复次数」），`EffectScript.cs:34-39` 注释明确。
- 在此语义下，`At(t)` 用 `Combination.Loop` 缩放 size（ω 份并发）= 瞬时峰值用 ω 份，正确；生命周期长度由 `Lifetime` 承载，与 ω 解耦。若用户正确建模（时长→Lifetime，并发→ω），Peak 不失真。
- 与 `DerivedMetrics.cs:56-61` `Scale` 一致：`w.IsTop ? [s.Lo,⊤] : [s.Lo*w, s.Hi*w]`，与 `EffectScript.ScaleSize`（`:221-225`）逐字对应。
- **结论**：代码无 bug，但 `LoopCount`（PDR §3.2.5 定义为循环次数）被 EffectScript 层**复用为并发副本数**，属语义发散——应在 `EFFECT_SCRIPT.md` §2.1 显著标注「LoopCount 语义在剧本层被重新解释为并发副本数，与 §3.2.5 原式一致（复制 ω 份），但概念含义不同」，以免后续维护者误用。列为**文档注记（非代码 open）**。

### OPEN-4（焦点4，MEDIUM）：Budget ⊤ 与常驻 ω=⊤ 交互
- **闭合**。`residentExempt`（`:174-185`）对「仅 ω=⊤ 正向贡献」的资源豁免守恒；`Mode.Use` 常驻层 `Audit_ResidentLayer_NotFlaggedLeak` 通过；仍受 Peak 约束 `Audit_ResidentLayer_PeakExceededStillReported`（cap=1 报 `PeakExceeded`）。资源不在 `Caps` ⇒ 不检查 `Audit_ResourceAbsentFromCap_Passes`。
- 见下方新 OPEN-N1：该豁免计算存在**顺序依赖**缺陷（仍可触发 false-negative）。

### OPEN-5（焦点5，MEDIUM）：Compatible 分组配对
- **闭合**。gate(3)（`:183-211`）按 `(归一化 ResourceId, ScopeId)` 分组，展开每事件 ω 副本后组内两两 `Compatible.IsCompatible`；冲突报 `CompatibleConflict`。
- `Audit_SameResourceTwoCreates_ReportedConflict` 通过；`Audit_CrossResource_NoConflict`、`Audit_CreateReleaseSameInstant_NoConflict` 通过。
- 证据：`EffectScript.cs:183-211`。

### OPEN-6（焦点6，LOW）：JSON 示例瑕疵
- **本轮不涉及**（JSON 契约在 Iter11 实现）。`EFFECT_SCRIPT.md §4` 示例 `loop`/`budget` 键瑕疵保留至 Iter11 修复，非 Iter1 open。

---

## 对抗质问结论

- **hi=⊤ 在 At(t) 处理**：`Alive`（`:227-229`）`lt.Hi.IsTop || t≤Hi` ⇒ 上界开放即无上界，正确。居民层 ∞ 寿命 + ω=⊤：At(t) 对有限 t 仍取 `Combination.Loop`（ω=⊤ ⇒ size [lo,⊤]），正确。✓
- **端点采样覆盖 ∞ 寿命事件**：端点集合包含所有有限 `Lo`（含开放尾事件的有限 Lo，`Audit:155-166`），`maxFinite` ≥ 任意有限 Lo ⇒ `closureT=maxFinite` 时 `CumulativeNet(closureT)` 涵盖全部有限 Lo 事件。`Lo=⊤` 仅当 `Hi=⊤` 同真（`Interval` 构造子禁止 `Lo=⊤` 且有限 `Hi`），即退化 `[⊤,⊤]`，非常规用例。⇒ 采样完备，无漏点。✓
- **多资源同事件**：`CumulativeNet` 遍历 footprint 全部 occupy claim，按归一化资源独立分组；`PeakForResource` 按资源独立求和。独立正确。✓
- **scaleSize 与 Derived 一致性**：逐字一致（见 OPEN-3）。✓
- **确定性（焦点6）**：`At(t)` 基于 `ImmutableHashSet<Claim>` 无序并集 ⇒ 同脚本同 t 同内容签名（`At_Deterministic_AcrossEnumerationOrder` 通过）。但 `Audit` 结果因下方 OPEN-N1 受**输入事件顺序**影响，违反 `EFFECT_SCRIPT.md §5`「审计确定性」声称——见 OPEN-N1。

---

## 开放项

### OPEN-N1（MEDIUM-HIGH）：`residentExempt` 顺序依赖 ⇒ 漏报漏（false-negative）
- **文件:行**：`EffectScript.cs:174-185`（`residentExempt` 计算循环）。
- **数学描述**：豁免定义为「资源 r 仅由 ω=⊤ 正向贡献」。当前实现：遍历事件，遇有限正向 `Remove(r)`、遇 ω=⊤ 正向且 r 不在集 `Add(r)`。由于是**单遍按输入顺序**增删，若 ω=⊤ 事件排在有限事件之后，r 最终留在豁免集；反之则移除。⇒ 同一剧本仅因事件排列顺序不同，泄漏检测结果不同——违反数学审计的顺序无关性，且与 `EFFECT_SCRIPT.md §5` 确定性声明冲突。
- **反例**：资源 `Gpu("x")` 同时有 Event A（ω=1 create，无 release）+ Event B（ω=⊤ create，无 release）。正确应报 `Leak(Gpu x)`（A 的有限 create 永不释放）。若 B 列于 A 之后 ⇒ `Remove` 先（no-op）后 `Add` ⇒ r 被豁免 ⇒ **漏报**。若 B 列于 A 之前 ⇒ `Add` 后 `Remove` ⇒ 正确报。结果取决于输入顺序。
- **最小修正**（保持 0e/0w/测试绿）：两遍法。第一遍收集 `hasFinitePos[r]`（存在有限正向贡献）；第二遍收集 `hasTopPos[r]`（存在 ω=⊤ 正向贡献）。豁免 ⇔ `hasTopPos[r] && !hasFinitePos[r]`。此定义顺序无关。
  ```csharp
  var hasFinitePos = new HashSet<ResourceId>(), hasTopPos = new HashSet<ResourceId>();
  foreach (var e in Events) foreach (var c in e.Footprint.OccupyClaims)
      if (c.Mode is Mode.Create or Mode.Move or Mode.Use) {
          var r = ResourceId.Normalize(c.Resource);
          if (!e.Loop.Count.IsTop) hasFinitePos.Add(r); else hasTopPos.Add(r);
      }
  var residentExempt = new HashSet<ResourceId>(hasTopPos.Where(r => !hasFinitePos.Contains(r)));
  ```
- **建议**：此修正应在 Iter2 落地（连同 OPEN-2 已闭合的守恒逻辑一并复测），不在此轮静改，以保持审计只读。

### OPEN-N2（LOW，文档注记，非代码 open）
- `EFFECT_SCRIPT.md §2.1` 需显著标注：`LoopCount` 在剧本层被**重新解释为并发副本数**（与 PDR §3.2.5 复制语义一致，但概念含义由「时间循环次数」变为「瞬时并发数」）。避免后续维护者将其与循环时长混淆。

---

## 总评：需修订 1 项（OPEN-N1，顺序依赖漏报）

auditA 的 6 项 open **全部闭合**（OPEN-3 以语义重定义闭合 + 文档注记）。但对抗审计新发现 **OPEN-N1**：`residentExempt` 单遍顺序依赖计算可致泄漏 false-negative，违反审计确定性。属真实正确性缺陷，建议 Iter2 以两遍法最小修正并复测。其余 API 定义良性、可验证、测试绿。
