# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计员 subagent #4（hy3，单独进程，全新上下文）。审计设计文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.4 数学层审计发现 MA-001..010 收敛真伪。

执行：
1. 用 read 工具读取 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md，重点 §3.4（L176-190）MA-001..010，交叉 §3.1-3.3 与 §14。
2. 对 10 条逐条还原「文档状态 vs 实际可证状态」：仅 MA-001（结构性已消）与 MA-007（低危声明）可判 discharged/低危；MA-002/MA-005/MA-006/MA-009 的「已解决/已收敛」被证伪为 open；MA-003/MA-008/MA-010 为 asserted（精度/接受类，未证紧度或精度界）；MA-004 为 partial（结构层 discharged、使用层 open）。
3. 重点再确认：MA-002「∞ 作 ScopeId 标记」与公式实际用 ω 两套表示未对接（§3.1.3 Loop(id:String) 非 ∞，公式用 ω）；MA-006「放弃半环」与 §3.2.4 ⊔ 区间 size 实际矛盾；§14 v3.0「21 问题全收敛/0 阻塞」与 §3.4 自身 MA-002/006/009 open 直接冲突。
4. 用 write 工具覆盖写入 D:/Godot/Cosmos/audit/iter04.md（非空中文 Markdown）：顶部写明「独立审计 #4（hy3 单独进程，本轮重跑）」；逐项审计表(状态 discharged/asserted/open/partial + 依据行号)；关键论证；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I4- 前缀，全局唯一)。
5. 自由文本回复一句话摘要即可（不要调用 structured_output 类工具）。

铁律：只写 audit/iter04.md，绝对不要读/写/改其它任何 audit 文件。验收：D:/Godot/Cosmos/audit/iter04.md 非空且为本次重跑内容。

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