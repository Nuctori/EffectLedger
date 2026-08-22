# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代10 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/ToolingTests.cs` + 被测 `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`、`src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`、`ApiMapping.cs`。对照 PDR §14（L2/L3 规范）+ LANDING_PLAN.md §5（验证策略）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：测试真触发工具层、非假绿、不留死测试）：** 核对 L2/L3 测试是否真驱动生成器/分析器且断言有意义。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **Generator 测试真触发**：是否真 `CSharpGeneratorDriver` 跑 `EffectAlgebraGenerator` 并断言生成文本含 `EffectAlgebraGenerated`？还是只 `new Generator()` 没跑？有无「构造了没运行」假绿？
2. **Analyzer DO-9 近似测试真触发**：`RunAnalyzer` 是否真 `WithAnalyzers` + `GetAnalyzerDiagnosticsAsync`？source 调用名是否与 §7 白名单 `AddChild`/`QueueFree` 真匹配（大小写归一）？断言是否真查 `Id=="EAA0901"`？
3. **反例测试**（released / override ⇒ 0 诊断）：是否真构造 acquire+release / acquire+override 且断言 0 个 EAA0901？该反例是否真覆盖「不误报」？
4. **编译环境**：测试 source 是否真可编译（stub 类型占位），或依赖「诊断可在编译错误下返回」？若 source 编译错，analyzer 是否仍真跑（不静默依赖调试模式）？有无测试因 source 无法编译而实际没驱动 analyzer（假绿）？
5. **不新增 nuget 测试包**：csproj 是否仅加 ProjectReference（未引新测试包）？避免本机 restore 故障？
6. **死测试/假绿**：有无 `[Fact]` 无断言、`Assert.True(true)`、或永远通过的断言？4 项测试是否各对应可证伪行为？
7. **出处注释**：是否带 §x.y？

**产出 `D:/Godot/Cosmos/audit/iter-code10.md`（严格）：**
```
# 迭代10 审计（L2/L3 工具层单测）
## 摘要
- 测试：45 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 真触发? + 假绿? + 结论）
| 测试 | 行号 | 被测 | PDR § | 真触发? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 4 项测试真驱动生成器/分析器且断言可证伪行为、无假绿、无死测试、未新增 nuget 包 ⇒ 可终止。

完成后回复：iter-code10.md 已写入；open 项 X；终止判定=？（一行）。

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