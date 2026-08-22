# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立的正式逻辑审计员。审计设计文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.1 基本对象（Claim / ResourceId / ScopeId / Signature / Set<Claim> 组合律）。

执行：
1. 用 read 工具读取 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md，重点 §1、§3.1-3.3、§3.4 的 MA-001..010。
2. 审计并给出数学性质+证明状态(discharged/asserted/open)：
   - Set<Claim> 上 ∪：结合律、交换律、幂等(S∪S=S)、中性元(∅)、Claim 相等性未定义(缺口)。
   - ResourceId 相等性 + MA-010「Unknown 与任何资源冲突」是否因 ∪ 吞并同 kind Claim(dominate)破坏可组合性。
   - ScopeId 上 ⊆ 在 §3.1 未定义(Peak/net 依赖)→ open，给最小补充定义。
   - Signature 不可变性：Claim 未实现值相等则语义相同 Claim 当不同元素→open。
   - MA-009「不追求形式化证明」实为 open；MA-010 的 Unknown 保守策略带来 dominate 问题。
3. 结构性成立的给形式证明(引用集合论标准结果，写前提)；需补充定义的标 open 并给最小补充。
4. 用 write 工具写入 D:/Godot/Cosmos/audit/iter01.md（非空中文 Markdown）：顶部范围/视角/结论摘要；每审计点一节(命题/数学性质/状态/论证或反例/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I1- 前缀，全局唯一)。
5. 最终回复一句话摘要+确认文件已写入。

重要：只写 iter01.md，不要读、不要改其它 audit 文件。验收：D:/Godot/Cosmos/audit/iter01.md 非空存在。

## Acceptance Contract
Acceptance level: verified
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope

Required evidence: changed-files, tests-added, commands-run, validation-output, residual-risks, no-staged-files

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