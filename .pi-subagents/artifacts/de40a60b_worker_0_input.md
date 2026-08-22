# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试，不得只计划。任务：为 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 的 L1 类型写 **代数定律 xUnit 单测**（迭代02），然后 `dotnet test` 绿。

环境（必读）：构建/测试须在 PowerShell 且先 `$env:MSBUILD_EXE_PATH = $null`：
  cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly
（该 Tests 工程已引用 Cosmos.EffectAlgebra，且为 xUnit 模板。删掉自带的 UnitTest1.cs，改写你的测试文件。）

测试工程需引用 L1 工程：确认 `tests/Cosmos.EffectAlgebra.Tests/Cosmos.EffectAlgebra.Tests.csproj` 含 `<PackageReference Include="Microsoft.NET.Test.Sdk"/>`、`<PackageReference Include="xunit"/>`、`<PackageReference Include="xunit.runner.visualstudio"/>` 与 `<ProjectReference Include="..\..\src\Cosmos.EffectAlgebra\Cosmos.EffectAlgebra.csproj"/>`。若缺则补（`write` 重写该 csproj，保持 net10.0 + Nullable enable）。

**测试内容（每个断言对应 PDR 定律，用 [Fact] 或 [Theory]；注释标 §x.y）：**
文件 `tests/Cosmos.EffectAlgebra.Tests/AlgebraLawsTests.cs`：
1. **NatStar ⊤ 闭包（§3.1.5a）**：`Of(3)+Top==Top`、`Top+Top==Top`、`Of(2)*Top==Top`、`Top*Of(0)==Top`（0×⊤=⊤ 保守）、`Top.Max(Of(5))==Top`、`Of(5).Min(Top)==Of(5)`、`Top.Min(Of(5))==Of(5)`、`CompareToFinite`：两 Top⇒0、Top vs Of(1)⇒1、Of(1) vs Top⇒-1。
2. **NatStar 结合/交换**：`(Of(a)+Of(b))+Of(c) == Of(a)+(Of(b)+Of(c))`、`Of(a)+Of(b)==Of(b)+Of(a)`（取若干值）。
3. **Interval.Merge（§3.1.5b）**：幂等 `m.Merge(m)==m`、交换 `a.Merge(b)==b.Merge(a)`、结合 `(a.Merge(b)).Merge(c)==a.Merge(b.Merge(c))`、[1,⊤].Merge([2,⊤])==[1,⊤]（⊤ 律）。
4. **ScopeId ⊆*（§3.1.3b）**：自反 `Method("m").IncludedIn(Method("m"))`；Global 最大元 `Method("m").IncludedIn(new Global())`；跨标签不可比较 `!Method("m").IncludedIn(Scene("s"))` 当 m≠s；反对称/传递由结构保证（可选一条传递测试）。
5. **Compatible（§3.2.3）**：对称 `IsCompatible(a,b)==IsCompatible(b,a)`（遍历 5 mode 全对）；全函数无未覆盖（遍历 25 组合均返回 bool 不抛）；CONFLICT 排除 `(C,C)/(M,M)/(R,R)` 返回 false；use 放行 `IsCompatible(Use,任何)` 均 true；`Unknown` 按 Use：`IsCompatible(Unknown,Release)==true`；create+release 配对 `IsCompatible(Create,Release)==true`。
6. **Normalize 幂等（§3.1.4a）**：`ResourceId.Normalize(Normalize(x))` 结构相等 `Normalize(x)`；`Self("signal_x")` 与 `SignalBus("x")` 归一后相等；`Signal("signal_y")` 与 `SignalBus("y")` 归一后相等。
7. **Claim.Normalize 一致（§3.1.1）**：两 resource 不同但归一后同（如 Self("signal_z") vs SignalBus("z")）的 Claim 经 Normalize 后相等；size 缺省 ⇒ Default。
8. **Signature.Union 幂等（§3.2.1）**：`Union(s,s)==s`（同 Claim 合并一次）；跨桶不混（read/write/occupy 各独立桶）。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 在 Tests 目录返回 0 失败（绿）。若失败，修测试或代码直到绿（代码若真有 bug，用 write 修 src 文件，不得增量 edit 漂移）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 AlgebraLawsTests.cs（N 个测试）。

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