# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：修正 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 两处 § 编号错挂注释（迭代13 OPEN-1/OPEN-2），构建仍绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**改动（用 write 整体重写相关文件，保持其他不变）：**
1. **OPEN-1**：`Objects.cs` 约 L107 `Kind` enum 注释「§3.2.3 — 效应种类」改为「§3.1.1（Claim 的 kind ∈ {read,write,occupy} 定义）/ §3.1.4b（Signature 按 kind 分三桶量纲隔离，DO-7）」。enum 穷举由 C# 类型保证。
2. **OPEN-2**：`Numeric.cs` 约 L81 `Interval.Default` 注释「§3.1.5(d) 缺省 size ⇔ [1,1]」改为「§3.1.5(a) 代数缺省 → [1,1]（单值上界保守；AUDIT002/003 启发式仍落 (a)(b)，不另算）」。

**约束：** 只改注释中 § 编号，不改任何代码/签名；保持 build 0e/0w。

**验证：** `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** FIX_OK 错误数=0，已修 OPEN-1(Kind→§3.1.1/§3.1.4b)+OPEN-2(Default→§3.1.5(a))。

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