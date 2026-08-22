# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代23 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/ScopeOrderTests.cs` + 被测 `src/Cosmos.EffectAlgebra/Objects.cs`(ScopeId.IncludedIn)。对照 PDR §3.1.3b（⊆* 偏序：自反/反对称/传递/Global 最大元/跨标签不可比）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：偏序三定律 + Global 最大元 + 跨标签不可比 真锁、非假绿）：** 核对 ScopeId 偏序测试是否真覆盖全部 7 标签且断言可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **自反**：任意 s（7 标签随机名）`s.IncludedIn(s)`==true？断言是否真驱动（退化 false 必红）？
2. **反对称**：`a⊆b ∧ b⊆a ⇒ a==b`？跨标签/异名是否真被证伪（不包含⇒不等于）？
3. **传递**：`a⊆b ∧ b⊆c ⇒ a⊆c`？是否用真链（a=b 自反 + b⊆Global）构造？
4. **Global 最大元**：任意 s `s⊆Global`==true 且 `Global⊆s`==false（s 非 Global）？唯一最大元？
5. **跨标签不可比**：7×7 标签交叉枚举是否真断言两相互不包含（含同标签异名）？有无漏对（如 Async×Shell、Conditional×Loop）？
6. **覆盖完整性**：7 标签（Method/Type/Scene/Global/Loop/Conditional/Async/Shell）是否都出现在测试中？
7. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`/随机只测安全值？
8. **出处注释**：引 §3.1.3b？

**产出 `D:/Godot/Cosmos/audit/iter-code23.md`（严格）：**
```
# 迭代23 审计（ScopeId 偏序锁）
## 摘要
- 测试：164 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若偏序三定律+Global 最大元+跨标签不可比 真锁、7 标签全覆盖、无假绿 ⇒ 可终止。

完成后回复：iter-code23.md 已写入；open 项 X；终止判定=？（一行）。

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