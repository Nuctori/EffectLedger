# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计员 subagent #3（hy3，单独进程）。审计设计文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §3.3 派生度量（net/peak/read/write）。

执行：
1. 用 read 读取 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md，重点 §3.1-3.3（§3.3.1-3.3.3、§3.4 MA-003/MA-004）。
2. 审计并给数学性质+证明状态(discharged/asserted/open)：
   - net(S) 对 ∪ 非线性（非同态）——与 DO-3「统一组合律」不能推广到 net；net 守恒仅当按资源配对（关联 DO-9 净零不变量）。
   - §3.3.2 peak(S,scope) 依赖 ScopeId⊆（§3.1.3 未定义，阻塞）；peak 跨 kind 混合 size 求和（与 DO-7 量纲隔离冲突，见 Iter14）；与 §3.2.5 的 Peak 定义矛盾。
   - read(S)/write(S) 计数可加性依赖 Claim 去重（Iter01 I1-02）；read 不按 size 加权（精度缺口）；MA-003「并行 read 累加」是精度策略非证明。
   - MA-004「occupy 峰值与净变化混淆已解决」：结构层收敛但使用层混淆未消（仅重构载体重构，无防误用机制）。
3. 结构性成立的给形式证明（写前提）；需补充定义的标 open 并给最小补充。
4. 用 write 写入 D:/Godot/Cosmos/audit/iter03.md（非空中文 Markdown）：顶部范围/视角/结论摘要；每点一节(命题/数学性质/状态/论证或反例/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I3- 前缀，全局唯一)。
5. 最终回复一句话摘要+确认文件已写入。

铁律：只写 audit/iter03.md，绝对不要读/写/改其它任何 audit 文件。验收：D:/Godot/Cosmos/audit/iter03.md 非空存在。

## Acceptance Contract
Acceptance level: verified
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope

Required evidence: changed-files, tests-added, commands-run, validation-output, residual-risks, no-staged-files

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