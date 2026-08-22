# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代25 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/ResourceNormalizationTests.cs` + 被测 `src/Cosmos.EffectAlgebra/Objects.cs`(ResourceId.Normalize)。对照 PDR §3.1.4a（signal_+s ≡ SignalBus(s)、Self/signal 归一、各类规范形）+ DO-8 单点真相。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：归一等价类真锁、可证伪、非假绿）：** 核对 §3.1.4a 等价类测试是否真覆盖且断言可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **Self("signal_x")≡SignalBus("x")**：断言是否真 `Equal(Normalize(a),Normalize(b))`？
2. **Signal("signal_x")≡SignalBus("x")**：是否真（subagent 修了一处 SignalBus 未剥 signal_ 前缀的 bug，回指 Objects.cs Normalize 代码确认已修）？
3. **Self("x")≡SignalBus("x")**：是否真？
4. **SignalBus 自洽**：`SignalBus("signal_x")==SignalBus("x")`？
5. **Gpu/CommandBuffer 真实语义**：测试是否按实现真实语义断言（独立构造子则断言幂等/不等价，而非硬套相等假绿）？
6. **Memory/Occupancy/Callback/AudioMixer/Input/Network 幂等**：各样本 `Normalize(Normalize(r))==Normalize(r)`？
7. **幂等性全集**：是否真循环断言全部样本归一幂等？
8. **跨类不等价**：不同类归一后互不相等（防全塌缩）是否真断言？
9. **可证伪**：若实现破归一/去幂等，对应断言是否必红？
10. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`？
11. **出处注释**：引 §3.1.4a/DO-8？

**产出 `D:/Godot/Cosmos/audit/iter-code25.md`（严格）：**
```
# 迭代25 审计（ResourceId 归一锁）
## 摘要
- 全解构建：0 错误 0 警告；测试 188 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 §3.1.4a 等价类真锁、幂等+跨类不等价、可证伪、无假绿 ⇒ 可终止。

完成后回复：iter-code25.md 已写入；open 项 X；终止判定=？（一行）。

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