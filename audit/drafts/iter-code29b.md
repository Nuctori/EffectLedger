# 迭代29-补 审计（A3/A4 诊断闭环）

## 摘要
- 全解构建：`dotnet build Cosmos.EffectAlgebra.slnx` ⇒ 0 错误 0 警告；`dotnet test` ⇒ 212 通过 0 失败 0 跳过（bash 重跑实证，非转述）。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**（iter-code29 OPEN-1/OPEN-2 已闭）。

## 逐条核对（回指行号 + PDR § + 真落实? + 结论）

| 项 | 行号 | PDR § | 真落实? | 结论 |
|---|---|---|---|---|
| 1 A3 KIND_MIX 实现 | EffectAlgebraAnalyzer.cs `AnalyzeKindMixAndCompat` L~210-270 | §14.3 A3 / §3.1.4b DO-7 | 真 | 按 `ResourceId.Normalize(c.Resource)` 分桶；仅当 `kv.Value.Count >= 2`（跨调用）且 `allKinds.Count > 1`（Read/Write/Occupy 混用）才 `ReportDiagnostic(KindMixOnSameResource)`；单 API 内部多态由 `siteClaims.Distinct()` + 跨调用门槛排除（护栏注释 L~38-44 一致） |
| 1a A3 真用 §7 数据 | `BuildAcquireNames`/`BuildReleaseNames` + `FindWhitelistEntry` | §7 | 真 | canonical 名查 `GodotApiWhitelist.All` 取 Claims，无硬编码 Claim；判定数据零 Godot 依赖 |
| 1b A3 测试真驱动 | AnalyzerCompletenessTests.cs `Analyzer_ReportsKindMixOnSameResource` L~62-82 | §14.3 A3 | 真 | `Connect`(§7 Wr Self("signal_x")→SignalBus + Oc Cb) + `IsConnected`(§7 Rd Self("signal_x")→SignalBus) 同方法两次调用 ⇒ 归一 `SignalBus("signal_x")`，2 站点 kind={Write,Read} ⇒ `Assert.Contains(d => d.Id=="EAA0303")` 必过；若未实现则无 EAA0303 ⇒ 必红 |
| 2 A4 Compat 冲突实现 | `AnalyzeKindMixAndCompat` L~250-270 | §14.3 A4 / §3.2.3 | 真 | 枚举不同调用间 mode 对，`if (!Compatible.IsCompatible(a, b))` 命中 CONFLICT 集 {(Create,Create),(Move,Move),(Release,Release)}（Algebra.cs L9/L27）⇒ `ReportDiagnostic(CompatConflictOnSameResource)`；跨调用门槛排除单 API 内重复 |
| 2a A4 真用 §3.2.3 | `Compatible.IsCompatible` | §3.2.3 | 真 | 直接调用 L1 `Compatible.IsCompatible(Mode,Mode)`，非重写；对称/全函数由 L1 保证 |
| 2b A4 测试真驱动 | `Analyzer_ReportsCompatConflictOnSameResource` L~85-105 | §14.3 A4 | 真 | 两次 `AddChild`（§7 Wr+Occupy Tree("node.id") Create）⇒ 2 站点 (Create,Create) 冲突 ⇒ `Assert.Contains(d => d.Id=="EAA0304")` 必过 |
| 3 逃逸通道 | `AnalyzeMethod` L~150 `hasEscape` | §8.3 | 真 | 标 `[EffectOverride]`/`[AcceptDeviation]` ⇒ 早返回，跳 DO-9/A3/A4；`Analyzer_NoKindMixOrConflictWhenOverrideAttr` 断言无 EAA0303/0304/0901 |
| 4 诚实性（数学已保护） | 类注释 L~14-18 / L~38-44 | §3.1.4b / §3.2.3 | 真 | 明言「A3/A4 仅提示意图清晰度，数学由 L1 三桶隔离/Compatible 全函数保护；EAA0303/EAA0304 非数学缺」；并透明声明控制流近似（跨方法/跨对象配对静默漏报，iter09 OPEN-2 同口径） |
| 5 无死描述符/无魔法数 | `SupportedDiagnostics` L~95 + 三处 `ReportDiagnostic` | — | 真 | 三描述符 `MissingReleaseForAcquire`/`KindMixOnSameResource`/`CompatConflictOnSameResource` 均进 `SupportedDiagnostics` 且均被 `ReportDiagnostic` 调用；acquire/release 名由 `GodotApiWhitelist.All`/`ReleaseClass.Names` 派生（Builder 循环），无硬编码魔法数 |
| 6 单 API 内部误报护栏 | `Analyzer_NoFalsePositiveForSingleApiInternalClaims` L~108-128 | §3.1.4b / §3.2.3 | 真 | `AddChild`(Wr+Occupy) 单调用 + `Load`(两次 Occupy Create) 单调用 ⇒ 各自跨调用数<2 ⇒ 不报 A3/A4；断言 `DoesNotContain(EAA0303/EAA0304)` 必过（避免 PDR §7 有意单条 API 多态被误报） |
| 7 全解构建/测试 | bash 重跑 | — | 真 | 0e/0w；212 通过 0 失败（见 validationOutput） |

## 结论
- §14.3 A3(KIND_MIX)/A4(Compat 冲突) 两条 L3 完备性判据**真落地于类型/代码**：数据全部来自 §7 `GodotApiWhitelist` + §3.2.3 `Compatible`（零 Godot），跨调用门槛排除单 API 内部 PDR 有意多态（无假阳），`[EffectOverride]` 逃逸通道生效，控制流近似与「数学已由 L1 保护」均诚实注释（iter09 OPEN-2 口径延续）。
- 三处 `DiagnosticDescriptor` 均注册且均被调用，无死描述符、无硬编码 Claim/魔法数。
- iter-code29 OPEN-1（A3 未实现）、OPEN-2（A4 未实现）**均已闭**：实现 + `AnalyzerCompletenessTests` 4 个用例真驱动闭环。
- 终止判定：**可终止**（0 open）。

## validationOutput
```
$ dotnet build Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly
  0 个错误；0 警告
$ dotnet test tests/Cosmos.EffectAlgebra.Tests -clp:ErrorsOnly
  通过: 212，失败: 0，跳过: 0
```
