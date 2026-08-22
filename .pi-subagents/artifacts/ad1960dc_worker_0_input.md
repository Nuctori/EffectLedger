# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代27 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/IntervalArithmeticTests.cs` + 被测 `src/Cosmos.EffectAlgebra/Numeric.cs`(Interval).对照 PDR §3.1.5a(⊤ 律)/§3.1.5b(Merge join-semilattice)/§3.1.5(Exact/Dynamic/Default)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：Interval 算术真锁、可证伪、非假绿）：** 核对 Interval 测试是否真覆盖 Exact/Dynamic/Default + Merge 全 ⊤ 组合 + 非法构造 + 随机，且断言可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **Exact/Dynamic/Default**：`Exact(5)==[5,5]`、`Dynamic==[1,⊤]`、`Default==[1,1]`、`Exact(0)==[0,0]`？真断言还是只测 trivial？
2. **Merge 全 ⊤ 组合**：`[1,3]∪[2,5]=[1,5]`、`[1,⊤]∪[2,⊤]=[1,⊤]`、`[1,3]∪[1,⊤]=[1,⊤]`、`[⊤,⊤]∪[1,5]=[⊤,⊤]`？是否真含 ⊤ 吸收（max/min 内嵌 ⊤ 律）？
3. **幂等/交换**：是否真断言？
4. **非法构造（§3.1.5a）**：`new Interval(Top, Of(5))` 是否真 `throw`？注释「类型层拒绝」是否真有构造子校验？
5. **Lo<=Hi 不变量**：合法区间是否真断言 `Lo<=Hi`？
6. **随机 1000 组**：是否真覆盖 form0/1/2 合法区间 + 边界（⊤/0/缺省）？
7. **可证伪**：若 Merge 去掉 ⊤ 律（普通 max 遇 Top 抛或误算），对应断言是否必红？
8. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`？
9. **出处注释**：引 §3.1.5a/§3.1.5b？

**产出 `D:/Godot/Cosmos/audit/iter-code27.md`（严格）：**
```
# 迭代27 审计（Interval 算术锁）
## 摘要
- 测试：208 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 Exact/Dynamic/Default + Merge 全 ⊤ 组合 + 非法构造 + 随机 真锁、可证伪、无假绿 ⇒ 可终止。

完成后回复：iter-code27.md 已写入；open 项 X；终止判定=？（一行）。

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