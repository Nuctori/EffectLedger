# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #11（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §9 运行时层（Deviation 公式良定义与零开销证明）。

步骤：
1. 用 read 读取该 PDR，重点 §9.1（L537-561 Deviation）、§9.2（L564-575 Release 零开销）、§9.3（L577-589 校准）、§9.4 RT-001..006（L593-600）；邻接 §3.1.4 Signature、§3.4 RT-002。
2. 审计并给数学性质+证明状态：
   - Deviation = Σ|actual-mid|/range，mid=(min+max)/2，range=max-min。当某 Claim size 区间退化为单点(min=max)时 range=0 ⇒ 除零/NaN（高，open，RT-002「已收敛」不实）。
   - 公式假设 Claim 带 [min,max] 区间，但 §3.1.1 size∈Nat? 单值 ⇒ 载体冲突（交叉 Iter02 I2-04）。
   - Σ 遍历 expected/actual 的索引对齐（按 Claim 相等？resource？）未定义（open）。
   - Release 零开销：#if DEBUG 剥离成立（discharged）；但 §9.1 EffectValidator 标 [Conditional("DEBUG")]，方法体仍入 RELEASE IL，与 §9.2 #if DEBUG（不入 IL）矛盾 ⇒ Mono.Cecil 会检出残留，RT-005「剥离验证已收敛」不实（open）。
   - RT-006「不长期存储」与 §9.3 SQLite+Chrome Trace 导出落盘矛盾。
   - 20% 阈值无依据；[AcceptDeviation] 逐处接受致全局偏差无上界（安全 open）。
3. 结构性成立的给条件证明。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter11.md（非空中文 Markdown）：顶部「独立审计 #11（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；RT-001..006 审计表；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I11- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter11.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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