# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代24 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/LoopCombinationTests.cs` + 被测 `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`(LoopCount/Combination.Loop/Sequence/Parallel)、`Algebra.cs`(Peak.Compute)。对照 PDR §3.2.5(ω∈ℕ∪{⊤}, S×ω)/§3.2.1(Sequence=∪)/§3.2.2(Parallel=∪)/§3.3.2(Peak size 求和)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：Loop ω 缩放真锁、ω=⊤⇒⊤ 不崩、Sequence/Parallel=Union、非假绿）：** 核对循环组合测试是否真锁 §3.2.5 语义且可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **Loop 有限 ω 缩放（§3.2.5）**：`Loop(body, Of(5))` ⇒ `Peak.Compute` == 10×5=50（ω×Σsize）？断言是否真驱动（实现若不改 size 必红）？
2. **Loop ω=⊤⇒⊤（§3.2.5/§3.1.5a）**：`Loop(body, Top)` ⇒ Peak 该资源 `IsTop==true`（不崩、不有限误判）？
3. **Sequence=Union（§3.2.1）**：`Combination.Sequence(a,b)` 结构等于 `Signature.Union(a,b)`？
4. **Parallel=Union（§3.2.2）**：`Combination.Parallel(a,b)` 同 `Signature.Union(a,b)`？
5. **嵌套（若测）**：`Loop(Loop(body,ω1),ω2)` 与 `Loop(body,ω1×ω2)` Peak 一致？若实现不支持嵌套，是否注释「非当前范围」而非假绿？
6. **可证伪**：若 `Loop` 实现偏离 §3.2.5（如不做 ω 缩放），对应断言是否必红？
7. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`/只测 trivial（ω=1）？
8. **出处注释**：引 §3.2.5/§3.2.1/§3.2.2/§3.3.2？

**产出 `D:/Godot/Cosmos/audit/iter-code24.md`（严格）：**
```
# 迭代24 审计（循环组合锁）
## 摘要
- 测试：169 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 Loop ω 缩放/ω=⊤⇒⊤/Sequence=Parallel=Union 真锁、可证伪、无假绿 ⇒ 可终止。

完成后回复：iter-code24.md 已写入；open 项 X；终止判定=？（一行）。

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