# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是「严格审计员」。对文档 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 做【独立审计 #50】（全新上下文，单独进程，不得复用任何记忆）。这是 50 轮审计的最后一环：细粒度全局回收 + 总账更新 + 收口三项遗留。

审计主题（聚焦，不 widening）：
(A) 把 iter21–50 这 30 轮细粒度审计发现的「未消解缺口」做全局回收：归纳出仍 open 的高优先公设清单（如 ScopeId⊆、Compatible 全函数、ω 载体、QueueFree mode、net(scope)、Claim 相等、Deviation range、DO-7 分桶、L2/L3 完备性 等），按「阻塞依赖链」排序（哪些是高优先根因）；
(B) 更新/汇总「总账」：文档声称「21 问题收敛、0 阻塞」（§14）与实测不符，给出 quantified 总账（约多少 PO/缺口 open、多少高优先）；
(C) 收口两项原 #61/#62 遗留：§12.2 的 AUDIT002/AUDIT003 数值口径是否统一（与 peak/net 的 size 口径、Deviation 单位一致？），以及 §7/§9 中 [EffectOverride]/[AcceptDeviation] 属性是否定义了校验规则（覆盖哪些冲突、如何降级/豁免、未定义则 open）。

严格步骤：
1. 用 read 真实读取 PDR：§14（L700-775 附近，收敛声明）、§12.2 AUDIT002/003（L660-670）、§7 中 [EffectOverride]/[AcceptDeviation]（grep 确认行号）、§9（L540-590）。必须真实 read，不得编造。注意：iter21-49 的文件你【禁止】读取，只能凭本任务给的编号作交叉引用；本审计自身必须基于 PDR 文本。
2. 只写 D:/Godot/Cosmos/audit/iter50.md（覆盖写）。【禁止】读/改其它 audit/*.md。
3. 文件结构（中文）：第1行 `# Iter50 审计 — 细粒度全局回收+总账+收口 AUDIT002/003 与 EffectOverride（独立审计 #50，hy3 单独进程）`；审计视角+范围(真实行号)；结论摘要；节A(阻塞依赖链排序)；节B(总账 quantified：open 高优先清单)；节C(AUDIT002/003 口径统一 + EffectOverride/AcceptDeviation 校验)；Proof Obligation 账本(ID PO-I50-X)；新缺口(I50-XX)；一句话摘要末行。
4. 行号必须真实；未定义写 open；诚实，不谎称已消解。
完成后用普通文本回报：列出高优先根因公设、总账 open 数、AUDIT/EffectOverride 状态。不用结构化输出工具。

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