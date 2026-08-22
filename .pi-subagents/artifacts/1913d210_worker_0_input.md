# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent（Iter1 视觉效应剧本）。只读审查刚实现的 `EffectScript.cs`，对照设计 + 既有 L1 + PDR，找数学/实现缺口。不改代码（除非发现真缺口且可立即闭；保持 0e/0w/测试绿）。

**只读：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs`（新实现）
- `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/EffectScriptTests.cs`（新测试）
- `D:/Godot/Cosmos/EFFECT_SCRIPT.md`（设计，含 auditA 的 6 open）
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/{Objects,Algebra,DerivedMetrics,Numeric,SignedNet}.cs`（L1 真实 API）
- `D:/Godot/Cosmos/audit/effect-script-auditA.md`（上一轮 6 open）
- `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.1–§3.3

**审计焦点（逐项核验 auditA 的 6 open 是否真闭合）：**
1. OPEN-1 scope 自由变量：`EffectEvent` 是否携带 `ScopeId`？`At(t)` 内 `Combination.Loop(Footprint, Loop, ?)` 的 loopScope 是否取自 `e.Scope`（非自由变量）？`Net/Peak` 的 scope 是否一致？
2. OPEN-2 瞬时守恒：Audit 是否用**累积净效应 C(t)**（非瞬时 `At(t)` 的 `IsConserved`）？create@t1 release@t2（t1<t2）临时占用是否通过？负陷（release 早于 create）是否报 `NegativeDip`？
3. OPEN-3/5 Compatible：`At(t)` 内是否按 (归一化 ResourceId, ScopeId) **分组**后两两 `IsCompatible`？冲突（同资源同 scope 两 create）是否报 `CompatibleConflict`？
4. OPEN-4 居民层：`Loop.Top` 常驻层是否在守恒检查**豁免**（免 Leak）但仍受 Peak/Budget 约束？
5. OPEN-4b Budget：资源不在 `Caps` ⇒ 不检查 Peak；在 Caps 且超限 ⇒ `PeakExceeded`（带 current vs cap）。`⊤` 交互是否正确？
6. 确定性：`At(t)` 同脚本同 t 同签名（ImmutableHashSet 无序）——读代码确认 `Signature` 真的基于 `ImmutableHashSet`。

**额外质问（对抗）：**
- `At(t)` 的 hi=⊤ 处理：`Lifetime.Hi.IsTop` 时是否视为 t 任意大仍存活？居民层 ∞ 寿命 + ω=⊤ 组合会不会让 `At(t)` 对有限 t 仍正确？
- 端点采样：若某 Event hi=⊤，代表右端取 `max finite lo + 1`——会不会漏掉「∞ 寿命事件对有限区间内累积 net 的影响」？累积 C(t) 是否覆盖所有 finite 采样点（含被 ∞ 事件影响的点）？
- 多资源同事件：Footprint 含 gpu+commandbuffer+memory，三者守恒/Peak 是否独立正确？
- `scaleSize`：ω 有限时 size×ω（§3.2.5 `Combination.Loop` 的 Scale）；ω=⊤ 时 hi=⊤。是否与 `Derived`/组合算子一致（读 DerivedMetrics.cs 核对，不要重算）？

**产出 `D:/Godot/Cosmos/audit/iter-effect01.md`（严格）：**
```
# Iter1 审计
## auditA 6 open 闭合核验（# open | 闭合/缺口 | 文件:行 + 论证/反例）
## 对抗质问结论
## 开放项（OPEN-# 数学描述+反例+严重度+最小修正；0 项则写「无 open」）
## 总评：可关闭 / 需修订 N 项
```
独立判断，宁可过度质疑。若发现真缺口 ⇒ 列最小修正（保持 0e/0w/测试绿，必要时直接改并复测）。完成后回复一行：iter-effect01.md 已写入；结论=？（闭合/需修订 N 项 open）。

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