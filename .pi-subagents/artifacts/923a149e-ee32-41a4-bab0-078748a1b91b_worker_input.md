# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #41】（全新上下文，单独进程，不得复用任何记忆）。

审计主题（聚焦于一个未充分审计的维度，不要 widening 到其它主题）：
§10 缓解措施 R-1..R-12 的「自身正确性」——即每条缓解措施（如 R-8 Delta Sync 正确性、R-3 误报抑制、R-11 标注覆盖率检查、等）所依赖的机制是否已在文档内被证明（discharged），还是依赖某个 open 公设/asserted 命题。若缓解依赖 open 公设，则该缓解本身失效（缓解建立在未证机制上）。逐条给「缓解 → 依赖机制 → 该机制状态(discharged/open/asserted) → 缓解是否有效」。

严格步骤：
1. 用 read 工具读取 PDR 相关行（§10 R-1..R-12 约 L600-630；邻接 §6 工具、§5 Delta、§9 Deviation、§8 推导、§3 代数）。必须真实 read 文本，不得凭记忆编造。
2. 只写入文件 D:/Godot/Cosmos/audit/iter41.md（覆盖写）。【禁止】读或改其它 audit/*.md 文件。
3. 文件结构（中文）：
   - 第 1 行标题：`# Iter41 审计 — <一句话主题>（独立审计 #41，hy3 单独进程）`
   - 审计视角 + 范围（列出真实行号，如 L600-630）+ 邻接前期 iter（可引用 iter01-40 的编号作为交叉引用，但不得读那些文件）
   - 结论摘要（2-4 行）
   - 若干节（每节一个 R-* 缓解）：命题（文档真实表述+行号）、数学性质/证明状态（明确 discharged/open/asserted 及依赖）、论证、行号回指
   - Proof Obligation 账本表（ID 用 PO-I41-X，列：命题/状态/消解所需最小补充/行号）
   - 本轮新发现未消解缺口（I41-XX 前缀，全局唯一，标高/中/弱）
   - 一句话摘要（末行）
4. 每行号必须真实对应 PDR 文本；若某机制未定义，明确写「未定义（open）」而非含糊。
5. 诚实：若无法判定，标 open 并说明原因；不得谎称已消解。

完成后用普通文本回报：写了哪几行号、覆盖哪些 R-*、发现几个高优先 open。不要使用任何结构化输出工具。

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