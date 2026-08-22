# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计员 subagent #2（hy3，单独进程）。审计设计文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.2 组合律（顺序`;`/并行`||`/条件`⊔`/循环`S×ω`/Compatible/Peak）。

执行：
1. 用 read 工具读取 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md，重点 §3.1-3.3（尤其 §3.2.1-3.2.5、§3.4 MA-002/MA-006）。
2. 审计并给数学性质+证明状态(discharged/asserted/open)：
   - 顺序组合 `;` 定义为 ∪ 是否丢失执行序（序无关性未论证）。
   - 并行组合 `||` 在 Compatible 不满足时无错误语义（部分函数未形式化）。
   - `Compatible`（§3.2.3）非对称（create∧use 真但 use∧create 假）破坏 `||` 交换性。
   - 条件合并 `⊔`（§3.2.4）产出区间 size `[lo,hi]` 与 §3.1.1 `size∈Nat?` 单值载体冲突（MA-006「已解决」不实）；单侧出现 claim 区间未定义。
   - 循环 `S×ω` 是多重集还是集合未定义 ⇒ ω=∞ 时 Peak 有限/无限不定（MA-002「已解决」不实）；`Signature(b)` 守卫效应未定义。
   - §3.2.5 与 §3.3.2 两个 Peak/peak 定义互相矛盾（一者基数/轮次、一者 size 求和/scope）。
3. 结构性成立的给形式证明（写前提）；需补充定义的标 open 并给最小补充。
4. 用 write 工具写入 D:/Godot/Cosmos/audit/iter02.md（非空中文 Markdown）：顶部范围/视角/结论摘要；每点一节(命题/数学性质/状态/论证或反例/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I2- 前缀，全局唯一)。
5. 最终回复一句话摘要+确认文件已写入。

铁律：只写 audit/iter02.md，绝对不要读、不要写、不要改其它任何 audit 文件。验收：D:/Godot/Cosmos/audit/iter02.md 非空存在。

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