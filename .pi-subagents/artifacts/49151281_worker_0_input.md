# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：删除 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs` 中未使用的 `ReasonGuaranteedByCtor` 描述符（迭代08 OPEN-1），构建仍绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**改动：** 约 L36-42 有 `private static readonly DiagnosticDescriptor ReasonGuaranteedByCtor = new(...)` 声明后从未被 `ReportDiagnostic` 调用（死代码）。删除该字段及其整段注释块。保留「reason 非空由 L1 EffectOverrideAttribute 构造子强制」的事实——把它写进 `GetAnnotatedMethod` 方法或类的注释（一行即可），不保留描述符对象。

**注意：** 仅删除死代码，不改生成器逻辑。用 write 整体重写该文件（保证其余不变），或确认无其他引用后删除该段。

**验证：** `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** FIX_OK 错误数=0，已删 ReasonGuaranteedByCtor 死描述符（OPEN-1）。

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