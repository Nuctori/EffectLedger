# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代12 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/StabilityAuditTests.cs` + 被测 src（Numeric/Objects/Algebra/SignedNet/Deviation/ApiMapping/DerivedMetrics/EffectAttributes）。对照 PDR §11（DO-1..DO-10 稳定性根因）+ §3.1.5/§3.1.3b/§3.3.1/§3.1.4a/§3.2.3/§9.1/§3.2.5。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：每个 DO-n 测试必须可证伪「实现回退⇒此测试必红」、不假绿、出处完整）：** 核对 10 条 DO-n 回归守护是否真各自锁定一条 §11 根因。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **DO-1 单值默认**：是否真锁 `Interval.Default==[1,1]` + `Exact(0)` 不崩？回退风险（实现改 Default 为 [0,0]）是否必红？
2. **DO-2 类型约束**：是否真锁构造不变量（lo>hi 抛 / lo=⊤ 且 hi 有限 抛）？回退（放开校验）是否必红？
3. **DO-3 ⊤ 不报警**：是否真锁 `DeviationVal.Top.ExceedsThreshold(任意)==false`？回退（⊤ 参与比较）是否必红？
4. **DO-4 可终止**：是否真锁 `LoopCount.Top` 经 Loop 不抛/不无限循环 + 结果含 ⊤？回退（ω=⊤ 枚举）是否必红/超时？
5. **DO-5 偏序自洽**：是否真锁 自反/传递/反对称 + Global 最大元？
6. **DO-6 栈堆分离**：是否真锁 不同资源 net 不串桶？回退（共享键）是否必红？
7. **DO-7 量纲隔离**：是否真锁 net 仅 occupy 桶？回退（纳入 read/write）是否必红？
8. **DO-8 单点真相**：是否真锁 Normalize 唯一规范形（Self("signal_x")≡SignalBus("x")）？回退（去归一）是否必红？
9. **DO-9 守恒**：是否真锁 create+release⇒true、仅 create⇒false？回退（OPEN-1 旧 bug 复活）是否必红？
10. **DO-10 自洽**：是否真锁 Compatible 全函数/对称？
11. **可证伪性**：每条是否对应一个「若实现坏则必红」的不变量？有无「永远通过」的弱断言？
12. **出处注释**：每个 `DO-n_` 方法是否带 §11 + §x.y？

**产出 `D:/Godot/Cosmos/audit/iter-code12.md`（严格）：**
```
# 迭代12 审计（§11 稳定性回归守护）
## 摘要
- 测试：71 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 可证伪? + 假绿? + 结论）
| DO-n | 行号 | 被测 | PDR § | 可证伪? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 10 条 DO-n 各自可证伪锁定 §11 根因、无假绿、出处完整 ⇒ 可终止。

完成后回复：iter-code12.md 已写入；open 项 X；终止判定=？（一行）。

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