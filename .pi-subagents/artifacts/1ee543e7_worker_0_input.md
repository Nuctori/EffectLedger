# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代21 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs` + 被测 `EffectAlgebraGenerator.cs`/`EffectAlgebraAnalyzer.cs`/`ApiMapping.cs`。对照 PDR §7/§8.1/§3.3.1/§14 L2/L3。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：端到端真跑 generator+analyzer+L1、断言可证伪、非假绿）：** 核对四项端到端是否真闭环且非假绿。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **balanced⇒过+守恒**：源 `AddChild`+`QueueFree` 标 `[EffectOverride]` ⇒ analyzer 0 个 EAA0901？反射 `Compute_SpawnAndDespawn` ⇒ L1 `NetTable.IsConserved` 对 Tree 资源 true？是否真跑 generator 编译生成代码（非仅 grep）？
2. **unbalanced⇒EAA0901**：源仅 `AddChild` ⇒ analyzer ≥1 个 EAA0901？是否真触发（非 trivial）？
3. **override 豁免**：仅 acquire + `[EffectOverride]` ⇒ 0 个 EAA0901？
4. **生成真委托 L1**：生成文本含 `GodotApiWhitelist.All`+`Signature.Union`？反射返回非空 Signature？
5. **可证伪**：若 generator 退化为桩/analyzer 不报，对应断言是否必红？balanced 的守恒断言（L1）是否真锁（create+release 抵消）？
6. **假绿扫描**：有无 stub 类型名与白名单不一致致 analyzer 静默不报（假绿）？有无 `[Fact]` 无断言/`Assert.True(true)`？
7. **出处注释**：引 §7/§8.1/§3.3.1/§14？

**产出 `D:/Godot/Cosmos/audit/iter-code21.md`（严格）：**
```
# 迭代21 审计（端到端集成）
## 摘要
- 测试：146 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真闭环? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真闭环? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若四项端到端真闭环、balanced 真守恒、unbalanced 真报、override 真豁免、无假绿 ⇒ 可终止。

完成后回复：iter-code21.md 已写入；open 项 X；终止判定=？（一行）。

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