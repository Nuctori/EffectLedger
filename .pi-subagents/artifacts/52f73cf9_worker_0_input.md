# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：扫描 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 全部 .cs，确保**每个 public 类型/方法/属性**都带 PDR §x.y 出处注释（迭代13），并补齐缺失的出处（用户铁律：类型系统约束不了的语义写注释上，含 §x.y 出处）。构建仍绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**先 read 全部 src .cs：** Numeric.cs / Objects.cs / Algebra.cs / SignedNet.cs / Deviation.cs / ApiMapping.cs / DerivedMetrics.cs / EffectAttributes.cs。

**任务：** 用 grep/读，逐文件逐 public 符号（class/struct/enum/method/property/ctor/static），检查是否带 `§x.y` 字样（XML 注释 `///` 或行尾 `// §x.y`）。对每个**缺失出处**的 public 符号，按其语义补一行 `§x.y` 注释——对照 PDR 定位：
- NatStar/Interval/DeviationVal → §3.1.5a/§3.1.5b/§3.1.5c
- ResourceId/Claim/Kind/Mode/ScopeId → §3.1.4a/§3.1.1/§3.1.2/§3.1.3b
- Signature/NetTable/Peak/Compatible → §3.2.1/§3.3.1/§3.3.2/§3.2.3
- LoopCount/Combination/Derived → §3.2.5/§3.3
- Calculate(Deviation) → §9.1
- ApiMapping/GodotApiWhitelist/ReleaseClass → §7/§8.1
- EffectOverride/AcceptDeviation → §8.3.1/§8.3.2

**约束（用户铁律）：** 注释写「语义/约束不了的边界 + §x.y 出处」，不写废话；不改变任何逻辑/签名（只加注释）。用 write 整体重写缺失出处的文件（保证其余不变）。

**验证（必须）：** 补完后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告；`grep -rn "public.*(" *.cs` 抽查若干 public 符号确带来源。

**完成后最后一行回复：** FIX_OK 错误数=0，已补 X 个 public 符号的 §x.y 出处注释（列出哪些文件改了）。

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope

Required evidence: changed-files, tests-added, commands-run, residual-risks, no-staged-files

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