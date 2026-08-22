# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代04 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Deviation.cs`
- 被测：`D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Numeric.cs`(NatStar/Interval/DeviationVal)、`Algebra.cs`(NetTable)、`Objects.cs`(ResourceId.Normalize/Signature)
- 对照：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §9.1（搜 `### 9.1`）+ §3.1.5c（搜 `定义 3.1.5c`）+ §3.1.5a
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件（含 iter-code01-03）。

**审计目标（用户铁律：类型约束数学边界，不靠注释吹）：** 核对 Deviation 实现是否真落实 §9.1 公式且 ⊤ 不崩溃。

逐条（回指代码行 + PDR § + 结论）：
1. **公式正确性**：`Deviation = Σᵢ |actualᵢ_mid − expectedᵢ_mid| / max(expectedᵢ_range, ε)` 是否被真实现？mid=(lo+hi)/2、range=hi−lo 是否真算？
2. **分母下界 ε=1（iter33 收口）**：单值区间 range=0 时是否用 `max(range, 1.0)` 避免除零？有无 `0/0` 或 `x/0` 路径？
3. **⊤ 跳过 / 整体 ⊤（§3.1.5c/MA-002）**：任一端 size 为 ⊤ 时该项是否计为 `DeviationVal.Top`，且整体返回 Top（不 NaN 不 ∞）？是否真靠 `DeviationVal.IsTop` 类型强制，而非运行时 if 漏判？
4. **资源对齐**：expected 与 actual 是否按 `ResourceId.Normalize` 后的资源键对齐（同资源比较）？缺一方资源时如何处理（缺省 [1,1] 还是跳过）？
5. **`ExceedsThreshold`**：是否复用 `DeviationVal.ExceedsThreshold`（先判 IsTop 再比数值），返回 ⊤ 时不触发 0.2 报警？
6. **类型边界**：`NatStar` 的 `+/−`（mid/range 运算）若遇 ⊤ 是否被捕获（如 range 用 `Hi.IsTop?...` 判定）？有无任何 `double` 运算会 NaN/∞ 的路径？
7. **注释/出处**：每个方法是否带 §9.1/§3.1.5c/§3.1.5a 出处？是否明言 ε=1 与 ⊤ 整体跳过？

**产出 `D:/Godot/Cosmos/audit/iter-code04.md`（严格）：**
```
# 迭代04 审计（§9.1 Deviation 公式）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 结论 OK/OPEN）
| 检查 | 代码行 | PDR § | 类型真约束? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若公式真落实、⊤ 不崩溃靠类型强制、ε=1 防除零、资源对齐 ⇒ 可终止。

完成后回复：iter-code04.md 已写入；open 项 X；终止判定=？（一行）。

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