# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #44】（全新上下文，单独进程，不得复用任何记忆）。

审计主题（聚焦，不 widening）：§8 推导层默认规则对 occupy/release 的漏报，及补救方案。
具体：(a) §8 默认规则（ED-001..007，约 L511-540）默认把未知 API 推断为 create/use，是否系统性漏报 release（如 QueueFree/RemoveChild/Disconnect 的释放语义被默认规则忽略，回指 Iter27 QueueFree mode=move 漏算、Iter10 默认规则漏报）；(b) 默认规则下「创建-释放」配对守恒（net 守恒，§3.3.1/DO-9）是否可证；(c) 给出可消解的 proof obligation 履行尝试：推荐「默认规则必须显式标注 mode，未知一律归入 Unknown 而非默认 create」或「推导层对 release 类 API 设白名单」等补救草案，说明该草案使 net 守恒/DO-9 在推导层可判定所需最小补充。

严格步骤：
1. 用 read 真实读取 PDR：§8（L511-540）、§3.3.1 net（L163-165）、§7 释放类 API（QueueFree L429 / RemoveChild L427 / Disconnect L466）、§1 DO-9（L21）。必须真实 read，不得编造。
2. 只写 D:/Godot/Cosmos/audit/iter44.md（覆盖写）。【禁止】读/改其它 audit/*.md。
3. 文件结构（中文）：第1行 `# Iter44 审计 — <一句话主题>（独立审计 #44，hy3 单独进程）`；审计视角+范围(真实行号)+邻接 iter 编号(Iter10/Iter27/Iter37)；结论摘要；若干节(命题+数学性质/证明状态 discharged/open/asserted+论证+行号)；Proof Obligation 账本(ID PO-I44-X)；新缺口(I44-XX 全局唯一,标高/中/弱)；一句话摘要末行。
4. 行号必须真实；未定义写 open；诚实，不谎称已消解。
完成后用普通文本回报：覆盖哪些 ED-*、发现几个高优先 open。不用结构化输出工具。

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