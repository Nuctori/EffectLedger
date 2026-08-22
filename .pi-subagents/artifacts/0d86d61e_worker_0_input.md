# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #15（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的跨章主题：ScopeId⊆ 偏序未定义导致 Peak/peak/net 的 scope 过滤悬空。

步骤：
1. 用 read 读取该 PDR，重点 §3.1.3 ScopeId（L87-93）、§3.3.2 peak/maxOverlap 的 `c.scope⊆t`（L158-163）、§3.3.3 net 的 scope 聚合（L164-167）、§5 Shell 作用域、§7 映射 scope 标注（global/shell/method/type/loop 等）。
2. 审计并给数学性质+证明状态：
   - §3.1.3 定义 ScopeId 为 7 构造子，但**未定义 ⊆ 偏序**；而 §3.3.2 peak 依赖 `c.scope⊆t` 判定「Claim 是否落入窗口 t」，⊆ 未定义 ⇒ Peak 过滤对象不定（open 高，DO-7/DO-8 数学悬空）。
   - 实际标注混乱：§7 映射里 QueueFree 标 scope=shell，Connect 标 method，Load 标 global，AddChild 标 shell，EmitSignal 标 shell，无统一层级 ⇒ ⊆ 即使定义也无法给出一致嵌套（open）。
   - `t` 的遍历域（哪些 scope 是合法窗口？global? 每 method? 每 loop?）未列出 ⇒ max(Σ) 的 max 范围未定（open）。
   - `net` 按 scope 分组求和，但 group by 用 ScopeId 相等；Parallel 作用域（Prototype 两 caller 同 Shell 标 shell）与 Sequential 不同 ⇒ 分组依赖 ⊆ 定义（open）。
   - 若 ScopeId⊆ 定义为「同 Shell 下的 method/loop 均 ≤ Shell，Shell ≤ Global」，则 Peak 跨函数并（DO-8）；但此定义需文档给出（当前缺）。
3. 结构性成立的给条件证明（在明确 ⊆ 定义下 Peak/net 良定义）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter15.md（非空中文 Markdown）：顶部「独立审计 #15（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I15- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter15.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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