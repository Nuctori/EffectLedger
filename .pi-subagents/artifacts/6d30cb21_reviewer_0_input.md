# Task for reviewer

只做三件小事（fresh context，不要读 audit/ 也不读对话历史）：
1. 读 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs` 第 223–236 行（四相位扫换线推进）与第 89–97 行（Audit 注释）。判断：四相位（先应用 time<tv，再 tv-enter，审计，后 tv-exit）是否精确等价于 alive ⇔ Lo≤t≤Hi（含端点 t=Lo 与 t=Hi）。
2. 读 `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/EffectScriptEdgeTests.cs` 中的 `ReferenceAudit` 方法和 `Iter26_SweepLine_EqualsBruteForce_Reference_Random100` 测试。确认：ReferenceAudit 是用 public API 独立重写的旧「逐点全算+两两枚举」语义，且该测试用集合相等断言验证新 Audit == ReferenceAudit。
3. 把结论（≤10 行）写入 `D:/Godot/Cosmos/audit/iter-effect28-review.md`：给出 PASS / OPEN / BUG 判定，每条带行号证据。

不要改任何生产
<arg_key:6124c78e>context</arg_key:6124c78e>
<arg_value:6124c78e>fresh

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Return concrete findings with file paths and severity when applicable

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