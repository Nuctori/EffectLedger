# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
只读审查（不写代码）。核对视觉效应剧本的边界/性质测试是否真闭合各轮焦点，产出一份审计报告。

读这些文件：
- D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/EffectScriptEdgeTests.cs
- D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/EffectScriptTests.cs
- D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs
- D:/Godot/Cosmos/EFFECT_SCRIPT.md
- D:/Godot/Cosmos/audit/iter-effect01.md

逐轮核验（只列仍 open 的项，没有就写无）：
Iter3 居民层 ω=⊤ create 无 release 豁免 Leak 且受 Peak 约束；与有限 create 共存时有限侧仍报 Leak（顺序无关）。
Iter5 Budget peak 精确等于 cap⇒Passed；peak=⊤ vs 有限 cap⇒PeakExceeded。
Iter6 At 空脚本恒 Empty；脚本拼接 At(t)==分别At后Union（完整 OccupyClaims 集合相等）；端点采样==密集整数扫描（完整签名相等）。
Iter7 300 随机脚本 Audit 不抛/确定性/终止。
Iter9 ω 巨大溢出⇒Peak=⊤ 不崩溃不负数。
Iter12 事件顺序不同集合相同⇒Audit 结果相同（Violations 内容顺序无关）。
Iter14 混合资源 X(有限create+释放 + ω=⊤无释放)不豁免X；Y 仅ω=⊤ Use 豁免。
Iter15 空 Passed+AllEmpty；∞寿命事件任意大t存活；无有限hi闭包点取0正确。
Iter16 重叠区间At含两者；Scene⊆Global不误判。
Iter17 跨多资源各自NegativeDip。
Iter18 单事件三资源独立，删其一release仅该资源Leak。
Iter19 ω=3 size=Exact(2)⇒peak=6、累积net×3（对照Combination.Loop）。
Iter20 反射确认每个public API XML注释含§。
Iter24 Footprint只用既有ResourceId子类未发明新kind。
Iter25 1000事件Audit<5s且冲突仍检出。

额外：是否存在只断言Passed而无反例对照的假绿测试？端点采样对hi=⊤开放事件是否遗漏采样点？

产出 D:/Godot/Cosmos/audit/iter-effect03_14.md，严格格式：
# Iter3–Iter14 批量审计
## 逐轮闭合核验（Iter# | 闭合/缺口 | 文件:行）
## 对抗质问结论
## 开放项（OPEN-# 描述+严重度+最小修正；0则写无）
## 总评：可关闭 / 需修订 N 项
完成后回复一行：iter-effect03_14.md 已写入；结论=？（闭合/需修订 N 项 open）。

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