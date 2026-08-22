# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #47】（全新上下文，单独进程，不得复用任何记忆）。

审计主题（聚焦，不 widening）：MA-007「权重函数」的明确定义。
具体：(a) §3.4 MA-007（约 L186-188）与 §3.3.2 peak（L167）声称跨 kind 折算需「显式权重函数」但全文未定义。审计：给出权重函数 `weight: Kind×Kind → ℝ∪{⊥}` 的推荐定义草案（同 kind=1、跨 kind=⊥ 或文档指定值），说明其使 DO-7 量纲隔离（§1 DO-7 L19）落地、peak 跨 kind 求和不混算所需最小补充；(b) 权重函数与 §12.2 预算比较（512MB 等）的关系：权重是否用于统一预算口径；(c) 回指 Iter14 I14-06（权重缺失）、Iter36 Q3（分桶+weight 草案）。

严格步骤：
1. 用 read 真实读取 PDR：§3.4 MA-007（L186-188）、§3.3.2 peak（L167）、§1 DO-7（L19）、§12.2 预算（L660-670）、§3.1.1 kind（L80-82）。必须真实 read，不得编造。
2. 只写 D:/Godot/Cosmos/audit/iter47.md（覆盖写）。【禁止】读/改其它 audit/*.md。
3. 文件结构（中文）：第1行 `# Iter47 审计 — <一句话主题>（独立审计 #47，hy3 单独进程）`；审计视角+范围(真实行号)+邻接 iter 编号(Iter14/Iter36)；结论摘要；若干节(命题+数学性质/证明状态+论证+行号)；Proof Obligation 账本(ID PO-I47-X)；新缺口(I47-XX 全局唯一,标高/中/弱)；一句话摘要末行。
4. 行号必须真实；未定义写 open；诚实，不谎称已消解。
完成后用普通文本回报：覆盖 MA-007 与权重函数哪几行、发现几个高优先 open。不用结构化输出工具。

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