# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #21（hy3 单独进程）。极小聚焦审计：D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.2.3 第1析取 `(use∧use)⇒兼容` 与 §3.4 MA-010「Unknown 资源与任何资源冲突」的自相矛盾。

步骤：
1. read 读取 PDR：§3.2.3（L131-138，重点第1析取 L133）、§3.1.2 ResourceId.Unknown（L88-99）、§3.4 MA-010（L189）、§7 中实际标 Unknown 的 Claim（如 Load 变量 path L456、网络 Rpc L498）。
2. 论证命题：当两 Claim 的 resource=Unknown（保守，如 Load 变量 path、网络 self.id+"/"+method），依第1析取二者 use∧use⇒Compatible=true⇒不报冲突；但 MA-010 要求 Unknown 与任何资源（含另一 Unknown）冲突⇒应保守报冲突。两规则直接矛盾：按 Compatible 规则 Unknown∧Unknown 放行，按 MA-010 应保守冲突。指出哪一为准未定、保守性失效漏报。
3. 给数学性质+证明状态（open，高），交叉引用 Iter16 I16-06 / Iter01 I1-03。
4. write 覆盖写入 D:/Godot/Cosmos/audit/iter21.md（非空中文，顶部「独立审计 #21（hy3 单独进程，本轮重跑）」；节：命题/数学性质/状态/论证/行号；Proof Obligation 账本(ID|命题|状态|最小补充|行号)；未消解缺口列表(I21- 前缀)）。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter21.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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