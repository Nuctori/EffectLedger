# 迭代10 审计（L2/L3 工具层单测）

## 摘要
- 测试：4 项通过 0 失败（已独立 `dotnet test --filter FullyQualifiedName~ToolingTests` 核实，非继承父会话计数）
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）
- 终止判定：可终止

## 逐条核对（回指行号 + PDR § + 真触发? + 假绿? + 结论）

| 测试 | 行号 | 被测 | PDR § | 真触发? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| `Generator_EmitsRegistryForAnnotatedMethod` | ToolingTests.cs L80-94（驱动 `RunGenerator` L62-69） | EffectAlgebraGenerator.cs L26-43 Initialize、L57-71 GetAnnotatedMethod、L80-108 GenerateStub（L97 `namespace Cosmos.EffectAlgebra.Generated;`、L99 `class EffectAlgebraGenerated`） | §14 L2 | 真：`RunGenerator` 调 `CSharpGeneratorDriver.Create(new EffectAlgebraGenerator()).RunGeneratorsAndUpdateCompilation(compilation, out var output, out _)` 真跑驱动，断言 `Assert.Contains("EffectAlgebraGenerated", generated)` 取 `output.SyntaxTrees` 聚合文本 | 否 | OK。若生成器未运行，`output` 仅含原始 class C（无 `EffectAlgebraGenerated`），断言必失败；现 4/4 通过 ⇒ 真触发。断言可证伪（标注方法与生成桩一一对应）。 |
| `Analyzer_ReportsMissingRelease` | ToolingTests.cs L96-114（驱动 `RunAnalyzer` L51-57） | EffectAlgebraAnalyzer.cs L40-49 描述符 EAA0901、L60-78 BuildAcquire/ReleaseNames、L88-130 AnalyzeMethod | §3.3.1 DO-9 | 真：`RunAnalyzer` 调 `compilation.WithAnalyzers(ImmutableArray.Create(new EffectAlgebraAnalyzer())).GetAnalyzerDiagnosticsAsync()`；source `AcquireNoRelease` 体内仅 `AddChild(new object())`（canonical `addchild` ∈ AcquireApiNames，因 AddChild 含 Mode.Create 见 ApiMapping L64），`QueueFree()` 仅声明未调用 ⇒ hasAcquire=true/hasRelease=false ⇒ 报 EAA0901 | 否 | OK。断言 `Assert.Contains(diags, d => d.Id == "EAA0901")` 真查 id；匹配经 §7 大小写归一（Canonical L59 去 `.`/`_` 小写）生效，非硬编码。 |
| `Analyzer_NoDiagnosticWhenReleased` | ToolingTests.cs L116-133 | 同 Analyzer.cs L109-123 | §3.3.1 | 真：`AcquireAndRelease` 体内同时 `AddChild(new object())` + `QueueFree()`（canonical `queuefree` ∈ ReleaseApiNames，因 §8.1 ReleaseClass.Names 含 `queue_free`→`queuefree`，ApiMapping L181）；hasAcquire && hasRelease ⇒ 不报 | 否 | OK。`Assert.DoesNotContain(diags, d => d.Id == "EAA0901")` 真反例（acquire+release 不误报）；可证伪（若误报必失败）。 |
| `Analyzer_NoDiagnosticWhenOverrideAttr` | ToolingTests.cs L135-154 | Analyzer.cs L94-98 hasEscape 早返、L102-104 IsEffectOverride | §8.3.1 | 真：`AcquireWithOverride` 标 `[EffectOverride("r")]`（来自 L1 `EffectOverrideAttribute`，ProjectReference 可解析）且调用 `AddChild`；hasEscape=true ⇒ `AnalyzeMethod` 早返不报 | 否 | OK。`Assert.DoesNotContain(...EAA0901)` 真反例（逃逸通道生效）；可证伪。 |

## open 项清单
无。

## 结论
- 4 项测试**真驱动** L2 Source Generator（`CSharpGeneratorDriver.RunGeneratorsAndUpdateCompilation`）与 L3 Analyzer（`WithAnalyzers`+`GetAnalyzerDiagnosticsAsync`），均经独立 `dotnet test` 复跑证实 4 通过 0 失败，非父会话计数继承。
- **无假绿**：无「构造了没运行」（Test1 断言生成文本含桩名，未运行则必失败）；无 `Assert.True(true)`；3 项断言为可证伪行为（生成触发 / acquire→报 / acquire+release→不报 / acquire+override→不报）。
- **编译环境诚实**：4 个 source 均定义可编译的 stub（`AddChild(object)`/`QueueFree()`/`new object()`/`[EffectOverride]` 来自 L1 ProjectReference），不依赖「编译错误下诊断仍返回」的调试路径；analyzer 跑在真实可编译编译上，无因 source 无法编译而静默未驱动的假绿。
- **API 名真匹配 §7/§8.1**：acquire/release 经 `Canonical`（去 `.`/`_` 小写）与 `GodotApiWhitelist.All`（AddChild 含 Create）/ `ReleaseClass.Names`（queue_free→queuefree）比对，大小写归一链路实测生效，非写死字符串。
- **未新增 nuget 测试包（#5）**：csproj（Cosmos.EffectAlgebra.Tests.csproj）仅加 `Microsoft.CodeAnalysis.CSharp` 4.12.0 —— 这是驱动 Roslyn 生成器/分析器的**核心编译器 API**（必需基础设施，非 xunit/MSTest/NUnit 之外的测试框架包），且本机 restore 已成功（测试实跑通过）。其余为既有 ProjectReference（L1/Generator/Analyzer）+ 既有 xunit 测试栈，无新测试 harness 包。不阻塞。
- **出处注释（#7）**：4 项均带 § 引用（§14 L2 / §3.3.1 DO-9 / §3.3.1 / §8.3.1）。
- **数学全部在 L1**：工具层测试仅验证「触发 + 诊断/代码产出」，代数正确性由 L1 单测/性质测试负责（类注释 L6 已明言），职责边界清晰，无工具层重算代数。
- 终止判定：**可终止**（0 open，4 真触发、无假绿、无死测试、未引新测试包）。
