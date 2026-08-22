# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #48】（全新上下文，单独进程，不得复用任何记忆）。这是补写轮——上一轮写入未落盘，必须确保本轮回写成功。

审计主题（聚焦，不 widening）：MA-001/MA-003/MA-008/MA-010 四条「收敛/asserted」声明的真伪。
具体：逐条审计 §3.4 中 MA-001（约 L178）、MA-003（L182-184）、MA-008（L188-189，默认 size=1）、MA-010（L189-190，变量 path→Unknown）是否真·已收敛(discharged) 还是仅 asserted/依赖 open 公设：(a) MA-001 是否依赖 ScopeId⊆（Iter15/Iter34）、ω 载体（Iter18/Iter35）等 open 项；(b) MA-003 是否依赖 Compatible 全函数（Iter25）；(c) MA-008 默认 size=1 与 §3.1.1 size∈Nat? 缺省（Iter32/Iter26）一致性；(d) MA-010 变量→Unknown 与 Compatible 的 Unknown 处理（Iter21）是否自洽。给出每条「断言 → 依赖 → 状态」判定。

严格步骤：
1. 用 read 真实读取 PDR：§3.4 MA-001/003/008/010（L178-190）、§3.1.1 size（L78-86）、§3.2.3 Compatible（L131-138）、§3.1.3 ScopeId（L103-113）、§3.2.5（L143-154）。必须真实 read，不得编造。
2. 只写 D:/Godot/Cosmos/audit/iter48.md（覆盖写）。【禁止】读/改其它 audit/*.md。
3. 文件结构（中文）：第1行 `# Iter48 审计 — <一句话主题>（独立审计 #48，hy3 单独进程）`；审计视角+范围(真实行号)+邻接 iter 编号(Iter15/Iter18/Iter25/Iter21/Iter32/Iter26)；结论摘要；若干节(每条 MA 一节：命题+数学性质/证明状态+论证+行号)；Proof Obligation 账本(ID PO-I48-X)；新缺口(I48-XX 全局唯一,标高/中/弱)；一句话摘要末行。
4. 行号必须真实；未定义写 open；诚实，不谎称已消解。确保文件确实写入（写完后自查文件存在）。
完成后用普通文本回报：覆盖哪几条 MA、几条实为 asserted 非 discharged。不用结构化输出工具。

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