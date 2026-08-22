# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #17（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.4 MA-004「net 与 peak 概念混淆」收敛真伪：net(占用净值) 与 peak(瞬时峰值) 二者是否在数学层被清晰分离且各自良定义。

步骤：
1. 用 read 读取该 PDR，重点 §3.4 MA-004（L182-183）、§3.3.1 net（L163-165）、§3.3.2 peak（L167）、§3.2.5 Peak（L154）、Iter15(I15-01 ⊆ 悬空 / I15-04 两 Peak 矛盾)、Iter16(Compatible)；邻接 §12.2 报告。
2. 审计并给数学性质+证明状态：
   - 文档声称「net 占用净值与 peak 瞬时峰值已分清」。但：net 不含 scope 参数（Iter15 I15-06），peak 依赖未定义 ⊆（Iter15 I15-01），二者各自都未良定义 ⇒ 「分清」只是**命名层**，数学层两者都悬空（open 高）。
   - §3.2.5 的 Peak（count|·| 遍历 i∈1..ω）与 §3.3.2 的 peak（Σsize 遍历 t∈scope）是**两个不同函数**却都叫「峰值」，且 net 是 Σcreate−Σrelease 净值——三者在文中混用/跳跃，无统一对象（open）。
   - MA-004「已收敛」但没有任何一处证明 net 与 peak 的语义不相交（例如 peak 可 > net？net<0 含义？），且 DO-8/DO-9 分别依赖二者，二者定义都未立 ⇒ 收敛不实（open）。
   - net<0（释放多于创建）的语义未定义（泄漏？还是允许？），与 DO-9 报警方向冲突（open）。
3. 结构性成立的给条件证明（在 net/peak 各自良定义且语义域分离后「分清」成立）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter17.md（非空中文 Markdown）：顶部「独立审计 #17（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I17- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter17.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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