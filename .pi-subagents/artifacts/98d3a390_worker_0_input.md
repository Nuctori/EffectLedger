# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #14（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的跨章主题：DO-7 量纲隔离（read/write/occupy 不可混算）与 §3.1 Set<Claim> 单集合 ∪ 混合 kind 的矛盾。

步骤：
1. 用 read 读取该 PDR，重点 §1 DO-7（L19）、§3.1.1 Claim.kind（L80-82）、§3.2 组合律（∪ 混合 kind）、§3.3 派生度量（peak 混加 size）、§7 映射（kind 混合）、§3.4 MA-007。
2. 审计并给数学性质+证明状态：
   - DO-7「read/write/occupy 不可混算，编译期报错」与 §3.1.1 用单一 Set<Claim>（含混合 kind）做 ∪ 组合直接矛盾：∪ 对三类 Claim 一视同仁，无 kind 子空间（open 高）。
   - §3.3.2 peak = max Σ c.size（仅按 mode≠release 过滤，不按 kind 分离）把 read/write/occupy 三类 size 同数值相加 ⇒ 混算，违反 DO-7（open）。
   - DO-7「编译期报错」未指派任何 L1/L2/L3 工具执行（§6 三层均未提量纲检查）⇒ 无执行机制（open）。
   - peak 求和结果的量纲单位未定义（bytes? count? mix?），与 §12.2 512MB 预算比较仅对 memory 有意义（open）。
   - MA-007「量纲隔离通过 kind 字段实现，转换需显式权重函数」——权重函数全文未定义 ⇒ 跨量纲合法转换通道缺失（open，partial）。
3. 结构性成立的给条件证明（若 Signature 改为按 kind 分桶 R/W/O 则 DO-7 落地；当前弱解释下 DO-7 不成立但 §3.3 自洽）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter14.md（非空中文 Markdown）：顶部「独立审计 #14（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I14- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter14.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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