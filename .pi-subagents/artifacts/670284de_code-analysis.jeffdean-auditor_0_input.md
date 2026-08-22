# Task for code-analysis.jeffdean-auditor

独立审查（fresh context，只读代码，不要读 audit/ 下任何 prior 审计文件，也不要读之前的对话记录）`D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs` 的 `EffectScript.Audit` 扫换线（sweep-line）重写，对照其引用的 PDR 数学（`D:/Godot/Cosmos/EFFECT_SCRIPT.md` §2/§3，以及 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.2.3/§3.3.2/§3.3.1）。

审查目标（Jeff Dean 工程务实视角）：
1. 算法复杂度：新实现是否真的从朴素 O(S·E²) 降到 O(E·K·log E)？扫换线四相位（<tv / tv-enter / 审计 / tv-exit）是否精确等价于 `alive ⇔ Lo≤t≤Hi`？
2. 数学等价性：gate(1) 累积 net、gate(2) 峰值、gate(3) 兼容冲突判定，是否与「逐采样点全量重算 + 两两枚举」的旧语义逐条 Violation 集合一致？特别检查：
   - gate(1)：release 负向贡献是否只在其 Lo 处计入（新实现仅在 enter 累加 net，exit 不改 net）——是否正确？
   - gate(2)：ω=⊤ 或 hi=⊤ 的 claim 用 topCount 标记 ⊤，退出时是否正确归零？cap key 归一化匹配（忽略 scope）是否与旧 PeakForResource 一致？
   - gate(3)：单事件内 ω 份并发副本是否不会自冲突（旧版用 EventIdx 相同跳过；新版用 HashSet 单键不重复）？
   - Lo=⊤ 的事件在两类实现中是否都永不存活？
3. 规模/尾延迟：5000 同资源同 scope 重叠粒子场景下，增量维护是否避免平方爆炸？
4. 正确性风险点：是否有任何采样点或任何资源在扫换线推进中被遗漏/重复/顺序错误？

要求：只读代码 + 文档。给出结构化判定（PASS / 有具体 OPEN 项 / 有具体 BUG 项），每条结论带文件:行号证据。如果发现实际 BUG（不是风格），直接指出复现路径。不要修改任何文件。

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Return concrete findings with file paths and severity when applicable

Required evidence: changed-files, tests-added, commands-run, residual-risks, no-staged-files

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