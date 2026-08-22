# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #16（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.2.3 冲突判定 Compatible 组合子：完备性、对称性、mode 语义覆盖。

步骤：
1. 用 read 读取该 PDR，重点 §3.2.3 Compatible（L124-141）、§3.1.1 Claim 字段(move 模式)、§7 映射各 mode 标注、§3.4 MA-009；邻接 Iter08(I8-01 QueueFree mode=move)、Iter15(ScopeId⊆)、Iter01(I1-02 相等)。
2. 审计并给数学性质+证明状态：
   - §3.2.3 列 4 行（use∧use / read∧write / write∧write / 不同resource），但**漏列 create∧release、create∧create、不同 mode 跨 resource** 等 ⇒ 函数未全定义，返回 undefined（open，高，DO-9/并发安全数学悬空）。
   - 规则非对称：write∧write 冲突，但 write∧read 兼容，而 read∧write 顺序给出「兼容」（断言对称但未证）；实际代码 `Compatible(A,B)` 与 `Compatible(B,A)` 是否同结果未定（open）。
   - `move` 模式（QueueFree 用）未出现在 Compatible 任何析取支 ⇒ 含 move 的 Claim 冲突判定 undefined（交叉 Iter08 I8-01）。
   - 第1析取「同 resource 的 use∧use 兼容」依赖§3.1.2 是否把 Unknown 当合法 resource（MA-010，Iter01 I1-03）；若 Unknown⊤ 则 use∧use 也冲突（矛盾）。
   - 第2析取「read∧write 兼容」与「DO-7 不可混算」是否冲突需澄清（open）。
   - MA-009「兼容判定完备性已证明」在规则未全定义下不实（open）。
3. 结构性成立的给条件证明（补全集后 Compatible 成全函数）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter16.md（非空中文 Markdown）：顶部「独立审计 #16（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I16- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter16.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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