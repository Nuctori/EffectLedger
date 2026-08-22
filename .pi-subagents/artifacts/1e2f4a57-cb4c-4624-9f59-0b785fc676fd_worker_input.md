# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #45】（全新上下文，单独进程，不得复用任何记忆）。

审计主题（聚焦，不 widening）：MA-002「∞ 代数闭包规则」+ 收口「World 寿命 2^64 事实性声明」。
具体：(a) §3.4 MA-002（约 L180-182）声称 size/次数可达 ∞ 或 2^64，审计其「∞ 代数」是否定义了 extended-Nat/extended-Real 闭包（⊤ 的 +、×、max、compare 规则），是否使 Peak/net/⊔ 在 ∞ 输入下良定义（回指 Iter18 两个 ∞ 语义、Iter35 ω 载体、Iter33 Deviation range）；(b) 文档若在某处（§12 或 §3）声明「World 寿命/帧上限/对象上限 = 2^64」之类事实性论断，审计其真伪：2^64 是否真实 Godot/引擎事实，还是凭空断言；该事实若用于「∞ 退化为有限上界」的论证，论证是否成立；(c) 给出 ∞ 闭包规则草案（extended 载体 + 运算表），说明使 Peak/net 在 ∞ 收敛（返回 ⊤ 而非 NaN/∞ 崩溃）所需最小补充。

严格步骤：
1. 用 read 真实读取 PDR：§3.4 MA-002（L178-190）、§3.2.4 ⊔（L143-147）、§3.2.5 Peak（L154）、§3.3.1/3.3.2 net/peak（L163-167）、§9.1 Deviation（L549-561）、§12 中若提及 2^64/World 寿命（grep 全文确认行号）。必须真实 read，不得编造。
2. 只写 D:/Godot/Cosmos/audit/iter45.md（覆盖写）。【禁止】读/改其它 audit/*.md。
3. 文件结构（中文）：第1行 `# Iter45 审计 — <一句话主题>（独立审计 #45，hy3 单独进程）`；审计视角+范围(真实行号)+邻接 iter 编号(Iter18/Iter35/Iter33)；结论摘要；若干节(命题+数学性质/证明状态+论证+行号)；Proof Obligation 账本(ID PO-I45-X)；新缺口(I45-XX 全局唯一,标高/中/弱)；一句话摘要末行。
4. 行号必须真实；未定义写 open；诚实，不谎称已消解。
完成后用普通文本回报：覆盖 MA-002 与 World 2^64 哪几行、发现几个高优先 open。不用结构化输出工具。

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