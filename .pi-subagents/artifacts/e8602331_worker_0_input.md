# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代22 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/AttributeBoundaryTests.cs` + 被测 `src/Cosmos.EffectAlgebra/EffectAttributes.cs`。对照 PDR §8.3.1(reason 非空)/§8.3.2(epsilon∈[0,0.5])/§8.3.1(kind 不可覆盖)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：构造子强制的边界要有测试真锁、非假绿）：** 核对属性边界测试是否真锁 reason/epsilon/kind 三类约束且可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **reason 非空（§8.3.1）**：空/ null/ 空白是否都 `Throws<ArgumentException>`？合法理由是否不抛且 `.Reason` 正确？
2. **epsilon 上界（§8.3.2）**：0.6 / -0.1 / 1.0 是否都 `Throws<ArgumentOutOfRangeException`？0.0 / 0.5 / 0.2 是否合法且 `.Epsilon` 正确？
3. **kind 不可覆盖（§8.3.1）**：反射 `GetProperty("OverrideKind")` 是否真返回 null（类型层禁止）？若实现偷偷加了该属性是否必红？
4. **可证伪**：若实现去掉抛异常/加 OverrideKind 属性，对应测试是否必红？
5. **假绿扫描**：有无 `Assert.True(true)`/`[Fact]` 无断言/只构造不断言异常？
6. **出处注释**：带 §8.3.1/§8.3.2？

**产出 `D:/Godot/Cosmos/audit/iter-code22.md`（严格）：**
```
# 迭代22 审计（属性边界锁）
## 摘要
- 测试：157 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 reason/epsilon/kind 三类边界真锁、可证伪、无假绿 ⇒ 可终止。

完成后回复：iter-code22.md 已写入；open 项 X；终止判定=？（一行）。

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