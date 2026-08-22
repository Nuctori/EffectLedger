# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #18（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的跨章主题：两个 ∞ 语义（S×ω 循环展开、动态 Instantiate 占用 ∞）在数学上是否良定义，ω=∞ 时 Peak 是否发散。

步骤：
1. 用 read 读取该 PDR，重点 §3.2.4 S×ω 循环组合（L143-147）、§3.2.5 Peak 含 max_{i∈1..ω}（L154）、§7.4 Instantiate 标 ∞（L458）、§3.4 MA-002（∞ 代数性质，L180-181）、§3.4 MA-005（size 单调性，L184）；邻接 Iter02(I2-05 S×ω)、Iter04(PO-I4-a)、Iter09(I9-06 new_id Unknown)、Iter17。
2. 审计并给数学性质+证明状态：
   - S×ω：ω 是什么？自然数上界？集合上的「重复 ω 次」？document 未定义 ω 的载体（open）。若 ω=∞（循环/无限重复），则 S×ω 是无限多 Claim 副本，Peak 的 max_{i∈1..ω} 遍历无限集 ⇒ 若任一 S 含非 release Claim，Peak→∞（发散，open 高）；若用「无界但有限」近似，ω 取值未定（open）。
   - 动态 Instantiate 标 size=∞（L458 注释：「动态实例化，保守估计为 ∞」）——但 §3.1.1 size∈Nat?（有限自然数），∞∉Nat ⇒ 类型冲突（open）；且 MA-002「∞ 代数性质已定义」但全文无 ∞ 的 +/* 规则（如 ∞+c、∞×ω 是否仍 ∞？）⇒ ∞ 非代数对象（open）。
   - Peak 用 |·| 计数，遇 S×ω 展开 + size=∞ 时两处发散源（open 高）。
   - MA-002「已收敛」在 ∞ 无代数定义下不实（open）。
   - `;`（顺序组合）若允许循环（while 展开），则 ; 也隐含 ω（open）。
3. 结构性成立的给条件证明（若 ω 定义为自然数上界、size 引入 extended-Nat 含 ⊤ 且 ⊤ 闭包规则定义）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter18.md（非空中文 Markdown）：顶部「独立审计 #18（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I18- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter18.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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