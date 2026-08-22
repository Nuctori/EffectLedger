# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #13（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §11-13（工作量/术语/社区发布/§14 一致性）。

步骤：
1. 用 read 读取该 PDR，重点 §11 工作量（L623-635）、§12 社区发布（L639-738）、§13 术语表（L742-763）、§14 文档历史（L767-775）；邻接全文（Iter04 §14 声明矛盾、Iter08/09 映射、Iter11 Deviation、Iter12 风险依赖）。
2. 审计并给数学性质+证明状态：
   - §14 v3.0「21 个开放问题全部收敛，0 个阻塞」与 §3.4（MA-002/006/009 open）、§7/§9 冲突直接矛盾 ⇒ PDR 级结论错误（高，open）。
   - 术语表 L748 把 ResourceId 平铺为 9 类别，丢失 §3.1.2 的 10 构造子 tagged union 结构（open）。
   - §12.2 AUDIT002「occupy{memory}+10 per death」的 +10 来源不明（§7.4 Instantiate size 为估计值非 10）；AUDIT003 默认 64MB 与 MA-008「默认 size=1」矛盾（open）。
   - §11 工作量 12-19 周未计入本文已暴露 open 问题（Δ正确性、L2/L3 完备、Deviation 修复等）的消解成本 ⇒ 低估（open）。
   - §12 阶段1采用吸引力依赖 R-3 误报消解（Iter12 I12-03），阶段间负反馈未建模（open）。
3. 结构性成立的给条件证明（声明修订前提）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter13.md（非空中文 Markdown）：顶部「独立审计 #13（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I13- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter13.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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