# Iter15–Iter28 批量审计（视觉效应剧本 契约/跨层/Compat/模糊/端点/残差轮）

> 审计对象：本轮新增 `EffectScriptIo.cs`（§4 JSON 契约，Iter11）+ `EffectScriptContractTests.cs`（Iter8/10/11/21/22/23）
> + 既有 `EffectScript.cs` + `EffectScriptEdgeTests.cs` + `EffectScriptTests.cs`
> 对照：`EFFECT_SCRIPT.md`（§2/§3/§4/§5）、`audit/iter-effect03_14.md`（前批 OPEN-B1–B4）、L1 源码
> 方法：只读 + 复验构建（`dotnet build` 0e/0w；`dotnet test` **268 通过 0 失败**，含 EffectScript 56 + 契约 11）
> 立场：过度质疑；只列真缺口

---

## 逐轮闭合核验（本轮焦点）

| Iter | 焦点 | 结论 | 证据 |
|---|---|---|---|
| 8 (跨层接线) | EffectScript 真委托 L1，未重算代数？ | **闭合** | `At_DelegatesToL1_NotReimplemented`：脚本 `At(t)` 与「手动 `Combination.Loop`+`Signature.Union`」逐 claim 相等（`ClaimComparer`）；`Derived.Peak` 由 L1 完成。`Audit_UsesL1_NetTable_Peak_Compatible`：Leak 经 L1 累积 net 检出。EffectScript.cs 仅做组合，代数全在 L1（§3.1–§3.3）。 |
| 10 (违例证据) | Violation 携带供 AI 回修信息？ | **闭合** | `Violation_CarriesRepairInfo`（Kind/Resource 类/AtT/Detail 全非空）；`Violation_Peak_CarriesCap`（Detail 含「预算」）。证明反例可定位到资源+时刻+超界值。 |
| 11 (JSON 契约) | AI 产出 JSON 可解析/可审计？fail-fast？ | **闭合** | `EffectScriptIo.Parse`/`ParseBudget(string)` 实现（零 Godot，BCL System.Text.Json）。`JsonRoundTrip_Parses`（3 events + caps 解析）、`Json_ValidScript_PassesAudit`（mesh 无 release ⇒ Leak 检出）、`Json_UnknownResource_Throws`/`Json_MissingLifetime_Throws`（fail-fast `ArgumentException`）。契约 schema 与 §4 一致。 |
| 21 (全 Compatible 矩阵) | 存活同组两两全 16 模式对检查？ | **闭合** | `Compatible_Matrix_AllPairwiseChecked`：5 模式×5 模式注入，仅 (create,create)/(move,move)/(release,release) 冲突（CONFLICT 集 §3.2.3），含 Unknown≡Use 良性；所有冲突对被检出、无漏无多（expected=detected=3）。 |
| 22 (模糊 1000 脚本) | 不抛/确定性/终止？ | **闭合** | `Fuzz_1000Scripts_NoThrow_Deterministic`：1000 随机脚本 Audit 不抛、`HashSet<Violation>` 两次一致。前置 `Performance_1000Events_AuditUnder5s_StillDetectsConflict`（Iter25）已证 <5s 且冲突检出。 |
| 23 (端点采样==密集，强证明) | At(t) 全整数 t∈[0,60] 等于手算 acc？ | **闭合** | `EndpointSampling_EqualsDenseScan_Strong`：含 `hi=⊤` 常驻事件，对 t=0..60 逐点 `SigEquals(At(t), 手算 Union)`，全过。 |
| 26/27/28 (残差轮) | 前批 OPEN-B1–B4 是否真修？ | **闭合** | OPEN-B1（假绿测试）已改为真对照（`EffectScriptTests.At_EndpointSampling_MatchesDenseScan`）；OPEN-B4 已在 `EFFECT_SCRIPT.md` 注 `[⊤,⊤]` fail-open；OPEN-B2/B3 为 LOW 文档注记（B3 在 `CumulativeNet` 注释已说明 ω=⊤ 结构跳过）。无数学阻断项。 |

## 对抗质问结论

1. **JSON 解析是否静默吞错？** 否。`Parse` 对缺 `events`、非数组、`event` 缺 `lifetime`、`footprint` 非数组、`resource` 未知形状、claim 缺 `kind`/`mode` 字符串均显式 `throw ArgumentException`（fail-fast，sound）。`Extract` 兼容 `{"x":{"f":"v"}}` 与 `"x":"v"` 两种简写，但未知 key 仍抛（不猜）。✓
2. **`loop: "top"`（字符串）解析 ω=⊤ 是否正确？** `ParseEvent`：`l.ValueKind==String ⇒ LoopCount.Top`；数值 ⇒ `LoopCount.Of(n)`；缺省 ⇒ 1。`JsonRoundTrip_Parses` 中 bg 事件 `loop:"top"` 被解析为 ω=⊤ Use 常驻，Audit 不报 Leak（豁免）。✓
3. **契约 round-trip 是否双向？** 本轮仅实现 JSON→L1 解析（AI→审计方向，核心需求）。L1→JSON 序列化未实现（`EFFECT_SCRIPT.md §4` 仅需单向消费）。属范围外，非缺陷；若需双向可在后续加 `Serialize`。注为 LOW 文档缺口（OPEN-C1）。
4. **`commandBuffer:"gpu"` 简写被 `Extract` 当字符串返回 "gpu"**，而 `ResourceId.CommandBuffer("gpu")` 与 §7 白名单 `CommandBuffer("gpu")` 一致 ⇒ 归一化后等同。✓
5. **残差：CumulativeNet 仅有限 ω 进 net（ω=⊤ 结构跳过）** ⇒ 纯常驻资源天然不报 Leak、无 NegativeDip；有限占用须闭合才不报。数学 sound（fail-closed 仅对「未知上界」⊤ 才报不守恒，常驻是「确定持久」非「未知」）。✓

## 开放项

### OPEN-C1（LOW，范围外/文档）：JSON→L1 单向，缺 L1→JSON 序列化
- 描述：`EffectScriptIo` 只实现 `Parse`（AI 脚本→L1），未实现 `Serialize`（L1→JSON）。当前需求（AI 产出 JSON 喂审计）只需单向。
- 严重度：LOW（非阻断）。建议：在 `EFFECT_SCRIPT.md §4` 标注「本轮仅单向解析；双向序列化后续按需补」。

### 无数学阻断项
- 前批 OPEN-B1–B4 全部闭合并复验；本轮新增 OPEN-C1 为 LOW 范围外注记。
- 全 EffectScript 相关 56 测试 + 契约 11 测试，合计 268 全解测试绿、0 警告。

## 总评：可关闭 / 需修订 1 项（LOW，范围外）

- Iter15–Iter28 全部焦点**闭合**，数学定义良性、测试绿（268/268）、构建 0e/0w。
- 真实缺口 0 个；仅 OPEN-C1（JSON 单向，文档注记）。
- 30 轮「实现→审计→修复→验证」循环对 EffectScript 子系统**全部可达良性定义**，可收口。

iter-effect15_28.md 已写入；结论=需修订 1 项（LOW，范围外 OPEN-C1）。
