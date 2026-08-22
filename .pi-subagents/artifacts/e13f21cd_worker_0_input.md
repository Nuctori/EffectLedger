# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代18 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/CrossTableTests.cs` + 被测 `src/Cosmos.EffectAlgebra/ApiMapping.cs`(GodotApiWhitelist/ReleaseClass)。对照 PDR §7.1(QueueFree)/§8.1(release-class 7 项)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：跨表一致性真由数据驱动断言、非假绿）：** 核对 §7↔§8.1 守护是否真锁「释放语义对应」且可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **release-class ⇒ 白名单含 Release（§8.1⇒§7）**：是否真遍历 `ReleaseClass.Names` 7 项、对白名单里有条目的断言其 Claims 含 `Mode.Release`？找不到条目的非红软约束是否合理（注释「Analyzer 按名匹配」）？
2. **白名单 Release ⇒ release-class 收录（§7⇒§8.1）**：是否真遍历白名单 `Mode==Release` 的 claim、断言其 API 名在 `ReleaseClass.Names` 或「§7 释放语义已知名」集合？孤儿数断言为 0 是否真（无漏网孤儿）？
3. **QueueFree 收口（§7.1/iter27）**：是否真断言 `GodotApi=="QueueFree"` 的 Claims 含 `Mode.Release`？若实现误标 Create 是否必红？
4. **无魔法数**：release-class 集合/释放语义集合是否从 `ReleaseClass.Names` 真实读取（非手写重复 7 字符串）？
5. **假绿扫描**：有无「永远通过」的弱断言（如只遍历空集合）？有无 `[Fact]` 无断言/`Assert.True(true)` 充数（除明确注释的软约束外）？
6. **出处注释**：每项带 §7/§8.1？

**产出 `D:/Godot/Cosmos/audit/iter-code18.md`（严格）：**
```
# 迭代18 审计（§7↔§8.1 跨表一致性）
## 摘要
- 测试：88 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若跨表守护真锁释放语义对应、QueueFree 收口、无魔法数、无假绿 ⇒ 可终止。

完成后回复：iter-code18.md 已写入；open 项 X；终止判定=？（一行）。

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