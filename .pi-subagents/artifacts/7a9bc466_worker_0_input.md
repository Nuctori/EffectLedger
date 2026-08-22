# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #5（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §4 Entity-as-Data 公理与证明义务。

步骤：
1. 用 read 读取该 PDR，重点 §4.1.1-4.1.7 与 §4.2 EA-001..007。
2. 审计并给数学性质+证明状态(discharged/asserted/open)：
   - EntityId 单调性：U64 计数器在私有构造+中央分配器前提下 discharged；不可伪造在 public struct 下不成立(open 弱)；EA-001 分布式 v2 仅声明。
   - Component 不可变性：readonly record struct 表层 discharged，但 ImmutableArray<T> 递归不可变未证(open)；EA-002「已收敛」建立在未形式化 L2/L3 兜底(asserted)。
   - World.version 单调 discharged；但 §4.1.5 L235「U64 在 60fps 下可用 9.7 亿年」数量级错误（复核 2^64/60/s ≈ 309 年，非 9.7 亿年）——指出事实错误。
   - System 纯函数性/确定性：依赖未形式化 L2/L3 工具(open，仅契约声明)；EA-005 同理。
   - Command Signature 与 §7 Godot API 实际 Claim 无一致性校验(open)；EA-006「已收敛」为机制存在非正确。
   - EA-003/004 收敛真伪。
3. 结构性成立的给形式证明(写前提)；需补充定义的标 open 并给最小补充。
4. 用 write 工具覆盖写入 D:/Godot/Cosmos/audit/iter05.md（非空中文 Markdown）：顶部写「独立审计 #5（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证或反例/行号)；EA-001..007 审计表；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I5- 前缀)。
5. 自由文本一句话摘要即可（不要调用 structured_output 类工具）。

铁律：只写 audit/iter05.md，绝对不要读/写/改其它任何 audit 文件。验收：D:/Godot/Cosmos/audit/iter05.md 非空且 header 含「本轮重跑」。

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