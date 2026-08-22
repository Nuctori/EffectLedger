# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代05 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/DerivedMetrics.cs`，及被测 `Algebra.cs`(NetTable/Peak)、`Objects.cs`(Signature/ScopeId/ResourceId)、`Numeric.cs`。对照 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.2.5（L293）+ §3.3（L286 起）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：类型约束数学边界）：** 核对 §3.3 派生度量与 §3.2.5 循环组合是否真落实且自洽。

逐条（回指代码行 + PDR § + 结论）：
1. **LoopCount（§3.2.5 ω∈ℕ∪{⊤}）**：是否 `readonly record struct`、含 `Of(ulong)`/`Top`、`Count:NatStar`？ω=⊤ 是否真经 `NatStar.IsTop` 类型强制（无魔法数）？
2. **Combination.Loop（S×ω）**：ω=⊤ 时是否返回「上界开放」表示（如 Claim size 上限拉 ⊤ 或复制副本并标 loopScope）？是否真用 `ω.Count.IsTop` 判定？ω 有限时是否等价为 ω 次 body 合并？
3. **Combination.Sequence/Parallel**：是否真 = `Signature.Union`（§3.2.1/§3.2.2）？
4. **Derived.Peak**：是否真委托 `Peak.Compute`（§3.3.2 size 求和、⊤⇒⊤），而非 §3.2.5 已废弃的 cardinality 形式？
5. **Derived.Net / IsConserved**：是否真委托 `NetTable.Compute`/`IsConserved`（§3.3.1，仅 occupy 桶、⊤⇒不守恒）？
6. **一致性**：`Peak.Compute`（Algebra.cs）与 §3.3.2 是否一致（size 求和、任一 ⊤⇒⊤）？`NetTable.Compute` 是否仅 occupy 桶（§3.3.1 量纲隔离）？有无 cardinality 形式残留未标注废弃？
7. **注释/出处**：每条方法是否带 §3.2.5/§3.3.1/§3.3.2 出处？旧 cardinality 形式是否明确标注废弃？

**产出 `D:/Godot/Cosmos/audit/iter-code05.md`（严格）：**
```
# 迭代05 审计（§3.3 派生度量）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 结论）
| 检查 | 代码行 | PDR § | 类型真约束? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 LoopCount/Combination/Derived 均真落实 §3.2.5/§3.3 且自洽、无 cardinality 残留、注释完整 ⇒ 可终止。

完成后回复：iter-code05.md 已写入；open 项 X；终止判定=？（一行）。

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope

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