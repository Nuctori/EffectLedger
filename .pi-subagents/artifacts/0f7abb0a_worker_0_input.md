# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：为 L2/L3 工具层加**单元/集成测试**（迭代10），`dotnet test` 绿。用 Roslyn 自带 API（`Microsoft.CodeAnalysis.CSharp` 已可传递引用，无需新增 nuget 测试包），避免本机 VS nuget restore 故障。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`、`D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`、`D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectAttributes.cs`（属性）。

**Tests 工程改造：** 在 `tests/Cosmos.EffectAlgebra.Tests.csproj` 加两个 `ProjectReference`：到 `..\..\src\Cosmos.EffectAlgebra.Generator\Cosmos.EffectAlgebra.Generator.csproj` 与 `..\..\src\Cosmos.EffectAlgebra.Analyzer\Cosmos.EffectAlgebra.Analyzer.csproj`。确认已引用 L1（`Cosmos.EffectAlgebra`）与 xUnit。无需新增 package（Microsoft.CodeAnalysis 由 Microsoft.CodeAnalysis.CSharp 传递提供）。

**新文件 `ToolingTests.cs`（用 Roslyn 构造最小编译）：**
辅助方法：
- `static CSharpCompilation MakeCompilation(string source, params MetadataReference[] extra)`，用 `CSharpCompilation.Create("T", new[]{CSharpSyntaxTree.ParseText(source)}, refs)`；refs 含 `MetadataReference.CreateFromFile(typeof(object).Assembly.Location)`、`typeof(Claim).Assembly.Location`（L1 dll）、`typeof(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree).Assembly.Location`（Roslyn）等常见。
- `static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)`：建 Compilation → `var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new EffectAlgebraAnalyzer())); return await withAnalyzers.GetAnalyzerDiagnosticsAsync();`
- `static string RunGenerator(string source)`：建 Compilation → `GeneratorDriver(driver).RunGeneratorsAndUpdateCompilation(compilation, out var output, out _); return output.SyntaxTrees.Select(t=>t.ToString()).Aggregate((a,b)=>a+"\n"+b);`（driver 用 `CSharpGeneratorDriver.Create(new EffectAlgebraGenerator())`）。

**测试用例（带 §x.y 注释）：**
1. `Generator_EmitsRegistryForAnnotatedMethod`（§14 L2）：source 含 `using Cosmos.EffectAlgebra; class C { [EffectOverride("r")] void M(){} }` ⇒ 断言生成的 `output` 文本含 `"EffectAlgebraGenerated"`（生成桩类名）。
2. `Analyzer_ReportsMissingRelease`（§3.3.1 DO-9 近似）：source 含一个类，方法 `void Acquire(){ var n = new object(); n.ToString(); /* 模拟 acquire：AddChild 在白名单 */ }` — 直接构造调用语法更可靠：用 `GodotApiWhitelist.All` 中任一 create API 名（如 `AddChild`）做 `void Acquire(){ node.AddChild(x); }`（node 为某个 object 字段）⇒ 断言 `RunAnalyzer` 返回的诊断含 `EAA0901` 至少一个（或 `Id=="EAA0901"`）。
3. `Analyzer_NoDiagnosticWhenReleased`（§3.3.1）：source 方法同时含 acquire(`AddChild`) 与 release-class（`QueueFree`）⇒ 断言 0 个 EAA0901 诊断。
4. `Analyzer_NoDiagnosticWhenOverrideAttr`（§8.3.1）：方法含 acquire 但标 `[EffectOverride("r")]` ⇒ 断言 0 个 EAA0901。

**关键**：测试构造的 source 要能编译（或至少 Roslyn 能解析语法——`GetAnalyzerDiagnosticsAsync` 在编译有错时仍返回 analyzer 诊断，但更稳妥是让 source 可编译，用 stub 类型占位 `AddChild`/`QueueFree`/`AddChild` 为实例方法即可，不必真 Godot 类型）。若 analyzer 依赖 `GodotApiWhitelist` 的实际 API 名匹配，测试 source 的调用名必须与白名单一致（如 `AddChild`/`QueueFree` 确在 §7/§8.1）。

**约束（用户铁律）：** 测试真触发工具层（非假绿）；不新增 nuget 测试包；每个方法带 § 出处。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若 analyzer/generator 因测试编译环境差异不触发，用 write 调整测试 source 或断言，直到绿（不得删测试蒙混）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 ToolingTests.cs（Generator 生成 + Analyzer DO-9 近似 4 项）+ 加两 ProjectReference。

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope
- criterion-2: Return evidence sufficient for an independent acceptance review

Required evidence: changed-files, tests-added, commands-run, residual-risks, no-staged-files

Review gate: required by reviewer.

Finish with a fenced JSON block tagged `acceptance-report` in this shape:
Use empty arrays when no items apply; array fields contain strings unless object entries are shown.
`criteriaSatisfied[].status` must be exactly one of: satisfied, not-satisfied, not-applicable.
`commandsRun[].result` must be exactly one of: passed, failed, not-run.
`manualNotes` and `notes` are optional strings; an empty string means no note and does not satisfy `manual-notes` evidence.
```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "specific proof"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "specific proof"
    }
  ],
  "changedFiles": [
    "src/file.ts"
  ],
  "testsAddedOrUpdated": [
    "test/file.test.ts"
  ],
  "commandsRun": [
    {
      "command": "command",
      "result": "passed",
      "summary": "short result"
    }
  ],
  "validationOutput": [
    "validation output or concise summary"
  ],
  "residualRisks": [
    "none"
  ],
  "noStagedFiles": true,
  "diffSummary": "short description of the diff",
  "reviewFindings": [
    "blocker: file.ts:12 - issue found, or no blockers"
  ],
  "manualNotes": "anything else the parent should know"
}
```