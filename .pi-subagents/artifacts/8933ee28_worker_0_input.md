# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：修正 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Deviation.cs` 一行注释（迭代04 OPEN-1），构建仍绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**改动：** Deviation.cs 约 L31 处有一句注释误述为「scope 取 Global（§3.1.3b 最大元），**包含全部 Claim**（与 §9.1 开发期全量对账一致）」。但 `Calculate` 实际经 `NetTable.Compute`，而 `NetTable.Compute` 仅含 occupy 桶（§3.3.1 net 仅 occupy 参与）。请把该注释改为准确描述：
「scope 取 Global（§3.1.3b 最大元），仅含 occupy 桶净效应（net，§3.3.1）——create/move 与 release 抵消后的占用对账；read/write 不进 net（§3.3.1 量纲隔离），故本 Deviation 只比对占用净效应，非全量 Claim。这与 §9.1 开发期占用对账一致。」

**注意：** 只改注释文字，不改任何代码逻辑。先用 read 确认 L31 附近实际文字，再整体重写该文件（write）或仅改该注释行（若用 write 重写请保证其余内容不变）。

**验证：** `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** FIX_OK 错误数=0，已修正 Deviation.cs L31 注释（occupy 桶净效应对账）。

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