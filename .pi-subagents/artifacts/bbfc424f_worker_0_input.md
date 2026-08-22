# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：修正 `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/StabilityAuditTests.cs` 两处名/注释（迭代12 OPEN-1/OPEN-2），`dotnet test` 绿。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**改动（用 write 整体重写该文件，保持断言逻辑不变）：**
1. **OPEN-1（DO5 传递/反对称名实不符，约 L87-104）**：
   - 把方法 `DO5_ScopePartialOrderIsConsistent` 内「传递性」断言（实际为 `m⊆m ∧ m⊆g ⇒ m⊆g`）与「反对称」断言（实际为 `m⊆m ∧ m⊆m ⇒ m==m`）的注释改为准确描述：本模型偏序仅含 `Equals` 与 `Global` 两种可比关系，故传递/反对称可归约为「自反 + Global 最大元 + 跨标签不可比」；下方 `m.IncludedIn(s)==false`/`g.IncludedIn(m)==false` 已拦截「全返回 true」的退化实现。
   - 可在方法注释加一句：「注：ScopeId 偏序仅 `Equals` 与 `Global` 可比，故 a⊆b∧b⊆c⇒a⊆c 在 b=Global 时塌缩为 a⊆Global（已测）；非平凡三互异 scope 的传递因模型限制不变量退化，由 false 断言保证不退化。」
   - 不要求改名（改名可选）；重点是注释准确，不误导审阅者以为测了三互异 scope 传递。
2. **OPEN-2（DO3 注释根因错挂，约 L57-58）**：`DO3_TopDeviationNeverExceeds` 的 XML 注释把根因写为「§11 DO-3（统一组合律依赖 ⊤ 不崩溃）」——改为精准：「§11 DO-3 / §9.1 — ⊤ 不触发 0.2 报警（DeviationVal 类型边界不崩溃）；统一组合律的 ⊤ 闭合归 DO-4」。

**约束：** 只改注释/标签，不改任何断言与实现；保持 `Random`/测试名可识别。

**验证：** `dotnet test -clp:ErrorsOnly` 0 失败（绿），测试总数不变（71）。

**完成后最后一行回复：** FIX_OK 失败数=0，已修 OPEN-1(DO5 注释归约说明)+OPEN-2(DO3 根因精准挂 §9.1 不报警)。

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