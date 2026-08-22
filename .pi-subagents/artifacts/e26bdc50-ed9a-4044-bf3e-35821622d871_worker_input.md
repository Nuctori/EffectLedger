# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #49】（全新上下文，单独进程，不得复用任何记忆）。

审计主题（聚焦，不 widening）：§3.2.5 的 Peak（count 并发）与 §3.3.2 的 peak（Σ size 峰值）两个定义统一。
具体：文档存在两个「峰值」定义——§3.2.5 Peak(S,scope)=max_{i∈1..ω}|{c: c.scope⊆scope, mode≠release}|（计数并发 Claim 数，L154）与 §3.3.2 peak(S,scope)=max_{t⊆scope} Σ_{c: c.scope⊆t} c.size（size 求和，L167）。审计：(a) 二者量纲不同（计数 vs size 字节），是否测量同一概念；(b) 是否都依赖 ScopeId⊆（Iter15/Iter34）、ω 载体（Iter18/Iter35）；(c) 给出统一草案：peak 统一为「Σ size」口径、Peak 退化为 peak 在 size≡1 特例，或反之；说明统一后 S×ω 展开（Iter35）、scope 过滤（Iter34）、分桶（Iter36）如何一致。

严格步骤：
1. 用 read 真实读取 PDR：§3.2.5 Peak（L143-154）、§3.3.2 peak（L163-167）、§3.1.1 size（L78-86）、§3.1.3 ScopeId（L103-113）。必须真实 read，不得编造。
2. 只写 D:/Godot/Cosmos/audit/iter49.md（覆盖写）。【禁止】读/改其它 audit/*.md。
3. 文件结构（中文）：第1行 `# Iter49 审计 — <一句话主题>（独立审计 #49，hy3 单独进程）`；审计视角+范围(真实行号)+邻接 iter 编号(Iter15/Iter34/Iter18/Iter35/Iter36)；结论摘要；若干节(命题+数学性质/证明状态+论证+行号)；Proof Obligation 账本(ID PO-I49-X)；新缺口(I49-XX 全局唯一,标高/中/弱)；一句话摘要末行。
4. 行号必须真实；未定义写 open；诚实，不谎称已消解。
完成后用普通文本回报：两 Peak 统一草案要点、发现几个高优先 open。不用结构化输出工具。

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