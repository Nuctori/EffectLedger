# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #43】（全新上下文，单独进程，不得复用任何记忆）。

审计主题（聚焦于一个未充分审计的维度，不要 widening 到其它主题）：
§11/§12 验证层（L1/L2/L3 单元测试、集成测试、R-12 测试策略）的「覆盖域与有效性」：这些测试能否证立/证伪 PDR 的核心不变量（如 ∪ 幂等、net 守恒、Peak 收敛、Compatible 一致性）？是否因为核心公设本身 open（如 ScopeId⊆ 未定义、Compatible 偏函数、ω 载体未定义）而导致「测试只能验证实现、不能验证代数正确性」？即：测试覆盖的是代码还是规范？哪些是真·规范测试、哪些是伪测试（只测已实现但规范悬空的部分）。

严格步骤：
1. 用 read 工具读取 PDR 相关行（§11/§12 验证与测试，约 L630-700；§3 代数核心不变量；§6 工具；§9 Deviation 测试）。必须真实 read 文本，不得凭记忆编造。
2. 只写入文件 D:/Godot/Cosmos/audit/iter43.md（覆盖写）。【禁止】读或改其它 audit/*.md 文件。
3. 文件结构（中文）：
   - 第 1 行标题：`# Iter43 审计 — <一句话主题>（独立审计 #43，hy3 单独进程）`
   - 审计视角 + 范围（真实行号）+ 邻接前期 iter 编号（如 Iter07 L2/L3 完备性、Iter33 Deviation 测试、Iter19 量化）
   - 结论摘要（2-4 行）
   - 若干节：命题（文档真实表述+行号）、数学性质/证明状态（discharged/open/asserted）、论证、行号回指
   - Proof Obligation 账本表（ID 用 PO-I43-X）
   - 本轮新发现未消解缺口（I43-XX 前缀，全局唯一，标高/中/弱）
   - 一句话摘要（末行）
4. 每行号必须真实；若未定义写 open，不得含糊。
5. 诚实：无法判定标 open 并说明；不得谎称已消解。

完成后用普通文本回报：写了哪几行号、覆盖哪些测试条目、发现几个高优先 open。不要使用任何结构化输出工具。

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