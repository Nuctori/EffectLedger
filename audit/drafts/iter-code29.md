# 迭代29 审计（PDR↔代码对账）

## 摘要
- 全解构建（D:/Godot/Cosmos/Cosmos.EffectAlgebra.slnx）：**0 错误 0 警告**（TreatWarningsAsErrors=true，4 工程）
- 测试：**208 通过 0 失败**（tests/Cosmos.EffectAlgebra.Tests）
- open 项总数：**2**（可闭 0 / 真缺口 2 / 设计 out-of-scope 见下方显式清单）
- 终止判定：**需继续(2)** —— 见 open 项（§14.3 A3/A4 两条 L3 诊断未实现）

---

## 逐 PDR 章节对账

| PDR 章节 | 状态 | 证据（文件:行/类型） | 结论 |
|---|---|---|---|
| **§3.1.1 Claim** | 已覆盖 | `Objects.cs` `Claim` readonly record struct(kind,resource,mode,scope,size)；`Normalize()` | 五元组结构相等 + Normalize 键；类型强制全字段必填 |
| **§3.1.2 / §3.1.2b ResourceId** | 已覆盖 | `Objects.cs` 抽象 record + 14 个 sealed record 构造子（含 Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Occupancy/Callback/Network/Input/Custom/CommandBuffer/SignalBus） | 判别联合标签完整性由 record 保证 |
| **§3.1.3 / §3.1.3b ScopeId ⊆\*** | 已覆盖 | `Objects.cs` `ScopeId` 8 构造子 + `IncludedIn`（自反/Global 最大元/跨标签不可比） | 偏序逻辑内嵌查表 |
| **§3.1.4 / §3.1.4a Claim= + Normalize** | 已覆盖 | `Objects.cs` `ResourceId.Normalize`（signal_/SignalBus/CommandBuffer 归一 + Unknown 处理）；`Claim.Normalize` | 单点真相 |
| **§3.1.4b 分桶量纲隔离** | 已覆盖 | `Objects.cs` `Signature._read/_write/_occupy` 三桶；`Algebra.cs` `NetTable.Compute` `if (c.Kind != Kind.Occupy) continue;` | DO-7 计算性落地 |
| **§3.1.5a ℕ\* ⊤ 闭包** | 已覆盖 | `Numeric.cs` `NatStar`（+/×/Max/Min/CompareToFinite 内嵌 ⊤ 律，溢出→⊤ 保守） | MA-002 闭包 |
| **§3.1.5b Interval Merge** | 已覆盖 | `Numeric.cs` `Interval`（构造子 lo≤hi/lo=⊤ 校验、Default[1,1]、Dynamic[1,⊤]、Merge join-semilattice） | |
| **§3.1.5c DeviationVal** | 已覆盖 | `Numeric.cs` `DeviationVal`（IsTop 先判再比、ExceedsThreshold） | |
| **§3.2.1/§3.2.2 ∪** | 已覆盖 | `Objects.cs` `Signature.Union`/`Join`（Normalize 去重） | |
| **§3.2.3 Compatible 全函数+对称** | 已覆盖 | `Algebra.cs` `Compatible.IsCompatible`（16 对、CONFLICT 集、Unknown→Use） | P1-P4 落实 |
| **§3.2.4 ⊔ join-semilattice** | 已覆盖 | `Objects.cs` `Signature.Join`（=Union） | 半环措辞已修正 |
| **§3.2.5 循环组合 ω∈ℕ∪{⊤}** | 已覆盖 | `DerivedMetrics.cs` `LoopCount`/`Combination.Loop`（ω=⊤→[lo,⊤] 开放上界，×律内嵌） | |
| **§3.3.1 net(S,scope) 有符号** | 已覆盖 | `Algebra.cs` `NetTable`（ZStar 有符号、Negate、IsConserved 含0/⊤ fail-closed）+ `SignedNet.cs` | 旧无符号 bug 已修（iter05） |
| **§3.3.2 Peak** | 已覆盖 | `Algebra.cs` `Peak.Compute`（c.mode≠release 过滤、ω=⊤→⊤）+ `DerivedMetrics.cs` `Derived.Peak` | 旧 cardinality 形式已废弃 |
| **§3.3.3 read/write 量** | 已覆盖（数据态） | 由 `Signature.ReadClaims/WriteClaims` + `Peak`/求和可派生；无独立方法但语义可表达，未单列函数 | 无缺，非缺口 |
| **§7.1–§7.10 白名单** | 已覆盖 | `ApiMapping.cs` `GodotApiWhitelist.All`：38 条映射，每条带 `§7.x` 注释（GetNode/GetTree/AddChild/RemoveChild/QueueFree/MoveChild/Position/GlobalPosition/Rotation/Scale/MoveAndSlide/ApplyForce/ApplyImpulse/GetSlideCollision*/Load/LoadInteractive/Instantiate/Preload/EmitSignal/Connect/Disconnect/IsConnected/DrawMesh/DrawRect/SetMaterialOverride/Audio.*/IsAction*/GetMousePosition/Rpc/RpcId/Anim.*） | 逐 §7.x 逐一对应 |
| **§8.1 release-class** | 已覆盖 | `ApiMapping.cs` `ReleaseClass.Names` = {queue_free,free,remove_child,disconnect,remove_from_group,cancel_free,free_children_in_group}（7 项，与 PDR 严格一致）+ `IsRelease` | 来源注释引 node.cpp 核实 |
| **§8.2 DO-7** | 已覆盖 | `Algebra.cs` `NetTable.Compute` 仅 occupy 桶；`Objects.cs` `Signature` 三桶分离 | 量纲隔离真落实 |
| **§8.3.1 [EffectOverride]** | 已覆盖 | `EffectAttributes.cs` `EffectOverrideAttribute`（reason 非空构造子强制、无 OverrideKind 属性⇒类型禁止覆盖 kind、注释明言不得豁免 DO-9/DO-7） | |
| **§8.3.2 [AcceptDeviation]** | 已覆盖 | `EffectAttributes.cs` `AcceptDeviationAttribute`（ε∈[0,0.5] 构造子强制越界抛） | |
| **§9.1 Deviation** | 已覆盖 | `Deviation.cs` `SignatureDeviation.Calculate`（ε=1 分母下界、⊤ 整体跳过、资源按 Normalize 对齐、仅 occupy 桶 net）；`ExceedsThreshold` 复用类型安全方法 | |
| **§10 运行时采样（EffectValidator）** | **out-of-scope（诚实标注）** | 数学载体 `SignatureDeviation.Calculate(Signature expected, Signature actual)` 已在 L1 实现（取两 Signature）；但 `Engine.GetProcessFrames` 采样钩子/SQLite/Chrome Trace 是 Godot 运行期，依赖 D1。LANDING_PLAN §7/D1 显式列为 out-of-scope | 非静默缺口 |
| **§11 稳定性法则 DO-1..DO-10** | 已覆盖 | `StabilityAuditTests.cs`（DO1–DO10 各 `__` 守护）+ `VerificationMatrixTests.cs`（DO1/3/6/7/8/9/10 复验） | 10 条根因均锁为回归 |
| **§12 社区发布/采纳（Godot 工程改造）** | **out-of-scope（诚实标注）** | 本交付是纯 C# 代数库 + L2/L3 工具，不改造 Godot 工程；Audit-only 接入在 D1 | 非静默缺口 |
| **§13 CI/门禁** | **部分覆盖 + 部分 out-of-scope** | 已覆盖：Tests 工程 + 4 工程 `TreatWarningsAsErrors` + 全解 0e/0w 构建门禁 + ToolingTests 实触发 L2/L3。out-of-scope：L3 Analyzer 在真实 Godot 工程接入（需 Godot.NET.Sdk，本机未装，LANDING_PLAN §7/D1 显式标注） | 非静默缺口 |
| **§14 完备性规范 L2/L3** | **部分覆盖（见 open 项）** | 已覆盖：S1–S3（DO-1..DO-10 L1 可验证项）、A1（零 Godot）、A2（类型即约束）、A3（白名单完整）、A4（无魔法数）、A5（可复现）；L2 `Generator_EmitsRealSignatureDelegatingToL1` 证 S1 真实委托；L3 `Analyzer_ReportsMissingRelease`/`NoDiagnosticWhenReleased`/`NoDiagnosticWhenOverrideAttr` 证 A1 泄漏完整+无假阳+逃逸通道。**未覆盖**：A3(KIND_MIX)、A4(Compat 冲突) 两条 L3 诊断未实现（见 open） | 见 open 项 |

---

## out-of-scope 诚实清单（显式，非静默缺口）
1. **§10 运行时 Deviation 采样**：`Engine.GetProcessFrames` 抽样钩子、SQLite 内存库、Chrome Trace 导出——依赖 Godot 运行期，属 D1（LANDING_PLAN §7/D1）。L1 已提供计算载体 `SignatureDeviation.Calculate`。
2. **§12 Godot 工程改造 / Audit-only 落地**：纯 C# 交付不改造现有 Godot 工程；NuGet 发布、阶段式采纳为后续。
3. **§13 L3 Analyzer 接入真实 Godot 工程**：需 `Godot.NET.Sdk`（本机未装，L2/L3 csproj 显式排除并注释），CI 集成单列 D1。
4. **§14.4 AUDIT003 预算超支（场景 GlobalBudget 累加）**：预算累加属 Godot 场景级分析，依赖 D1；数学层 `Interval` SizeVal 同源可对账，但无 budget 累加器在 L1/L3。
5. **§14.4 NoFalsePositive_Unknown_Maps_ToManualReview**：未知 API 落默认 Unknown 规则——当前 L3 仅识别白名单 acquire/release，未映射 API 对分析器不可见（构造上无假阳），但无显式"落 Unknown→人工确认"诊断；由 L1 `Mode.Unknown` 数据语义承载，L3 未专门报。属近似边界，已在 Analyzer 类注释声明。

---

## open 项清单（真缺口，非 out-of-scope）

| # | 位置 | 问题 | 严重度 | 建议 |
|---|---|---|---|---|
| OPEN-1 | `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs` `SupportedDiagnostics` | **§14.3 A3（KIND_MIX）未实现**：L3 仅注册 `EAA0901`(DO-9)，未实现"跨 kind 聚合 ⇒ KIND_MIX 编译错误"诊断。PDR §14.3 A3 明确要求 L3 产出该诊断（基于 §3.1.4b 分桶 + §3.3.2b weight=⊥）。注：当前 L1 `Signature` 结构性按 kind 分桶、永不产生跨 kind 混合桶，故数学上已防；但 §14.3 把 KIND_MIX 列为 L3 completeness 判据，交付未含。 | 中 | 在 L3 Analyzer 增加 KIND_MIX descriptor：当分析单元内出现跨 kind 同一资源/同一聚合表达式（如 `Peak`/`Net` 跨 read+occupy 调用）时报告；或显式在 LANDING_PLAN §4 声明"KIND_MIX 由 L1 类型结构性阻止，L3 不重复发"（须文档化，不得静默）。 |
| OPEN-2 | `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs` `SupportedDiagnostics` | **§14.3 A4（Compat 冲突）未实现**：L3 未实现"同资源冲突 mode 对（CONFLICT 集 create+create/move+move/release+release）⇒ 冲突诊断"。PDR §14.3 A4 明确要求。当前仅 DO-9 泄漏近似。 | 中 | 在 L3 Analyzer 增加 Compat-conflict descriptor：方法内/跨调用点同一资源出现 CONFLICT mode 对时报告（需资源流分析，可作保守近似并注释边界，同 DO-9 风格）。 |

> 说明：OPEN-1/OPEN-2 均为 **L3 Analyzer 工具层 completeness 判据未实现**，属本次交付工具层应含范畴（§14.3 显名），非 out-of-scope；且底层数学已被 L1 类型保护（KIND_MIX 被分桶结构性阻止、Compat 全函数由 `Compatible` 真值表承载）。二者为"应实现但本交付未含"的真缺口，**不静默**（LANDING_PLAN §4 已声明 L3 仅做 DO-9 近似 + 近似边界注释，但未显式声明"KIND_MIX/Compat 诊断不实现"）。

---

## 结论
- 数学核心（§3.1–§3.3）、数据层（§7 全 38 条 + §8.1 release-class 7 项）、属性（§8.3 构造子强制）、偏差（§9.1 ε=1+⊤ 跳过）、稳定性（§11 DO-1..DO-10 全锁）、§14 可自动化部分（S1–S3 + A1/A2/A3/A4/A5 + L2 真实委托 + L3 DO-9 完整/无假阳/逃逸通道）**均已真覆盖**，全解 0e/0w、208 测试绿。
- 已交付章节无假绿、无注释吹；类型承载构造/⊤-闭包/偏序/分桶，注释承载归一前置/控制流近似/fail-closed/§ 出处。
- out-of-scope 章节（§10/§12/§13-Godot工程接入/§14.4-AUDIT003预算/§14.4-Unknown人工确认）均**显式诚实标注**，无静默缺口。
- **残留 2 真缺口（OPEN-1/OPEN-2）**：§14.3 A3(KIND_MIX)、A4(Compat 冲突) 两条 L3 诊断未实现。二者数学已被 L1 类型保护，但作为 §14 completeness 判据应在 L3 显式落地或显式声明豁免。
- 终止判定：**需继续(2)** —— 闭 OPEN-1/OPEN-2 后 §14 完备性方"已证"，达成可终止。
