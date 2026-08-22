# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #6（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §5 Shell 同态映射的函子律与 Delta Sync 正确性。

步骤：
1. 用 read 读取该 PDR，重点 §5.1.1-5.1.3（Shell 函子、薄层约束、同步点/Delta）与 §5.2 SH-001..005。
2. 审计并给数学性质+证明状态(discharged/asserted/open)：
   - §5.1.1「Shell: Domain→Godot 函子」是比喻非范畴论函子：范畴结构未定义(open)、函子律(identity/composition 守恒)未证(open)、映射非全且仅单向(open)。
   - §5.1.2 薄层五约束(无决策/无状态/无循环/确定性/效应边界)均为禁止型契约，依赖未形式化 L2/L3(open/asserted)。
   - §5.1.3 Delta Sync 仅定义数据结构(Spawned/Modified/Destroyed)，无一致性判据/前条件/证明 ⇒ 增量正确性 open(核心，阻断 DO-10)；三集合不相交性/幂等性未证。
   - SH-001/003/004「已收敛」依赖未形式化工具(open/asserted)；SH-002 区分成立(discharged)；SH-005 声明自洽(discharged)。
3. 结构性成立的给形式证明(写前提)。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter06.md（非空中文 Markdown）：顶部「独立审计 #6（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；SH-001..005 审计表；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I6- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter06.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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