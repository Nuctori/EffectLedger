# Task for code-analysis.jeffdean-auditor

独立审查（fresh context，只读代码，不要读 audit/ 目录任何 prior 审计文件，也不要读对话历史）`D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs` 的 `EffectScript.Audit` 扫换线重写，对照 `D:/Godot/Cosmos/EFFECT_SCRIPT.md` §2/§3 与 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.2.3/§3.3.2/§3.3.1。

你的最终交付：把结构化审查报告写入 `D:/Godot/Cosmos/audit/iter-effect28-review.md`，并在最后返回一行总结。报告必须含：
- 复杂度判定（是否 O(E·K·log E)，四相位是否等价 alive⇔Lo≤t≤Hi）
- gate(1)(2)(3) 与「逐点全算+两两枚举」旧语义的逐条等价性核对（重点：net 仅 enter 累加、topCount ⊤ 归零、cap key 归一化忽略 scope、单事件 ω 副本不自冲突、Lo=⊤ 永不存活）
- 规模风险（5000 同资源重叠粒子是否避免平方爆炸）
- 结论：PASS / 有 OPEN 项 / 有 BUG 项（每条带 文件:行号 证据）

不要改任何生产代码。如果确属 BUG，给出复现路径即可。

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