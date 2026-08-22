# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代29-补 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`、`tests/Cosmos.EffectAlgebra.Tests/AnalyzerCompletenessTests.cs`(或 ToolingTests.cs 新增项)、`ApiMapping.cs`。对照 PDR §14.3 A3(KIND_MIX)/A4(Compat 冲突)、§3.1.4b(DO-7)/§3.2.3(Compatible)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件（含 iter-code29）。

**审计目标：确认 iter29 OPEN-1/OPEN-2 已闭。** 逐条：
1. **A3 KIND_MIX（EAA0303）**：是否真实现「同归一资源跨 kind 调用 + 无 [EffectOverride] ⇒ Warning」？是否真用 `GodotApiWhitelist.All` 取 Claims + `ResourceId.Normalize(c.Resource)` 按资源分桶 + 判 `Kind` 集合>1？测试是否真驱动（同资源 read+occupy 无 override ⇒ ≥1 EAA0303）？
2. **A4 Compat 冲突（EAA0304）**：是否真实现「同归一资源 mode 对 `!Compatible.IsCompatible` + 无 [EffectOverride] ⇒ Warning」？是否真用 §3.2.3 `Compatible.IsCompatible`？测试是否真驱动（create×2 同资源 ⇒ ≥1 EAA0304）？
3. **豁免**：两诊断在方法标 `[EffectOverride]` 时是否真不报？
4. **诚实性**：是否注释「L1 已结构性隔离/保护，此诊断仅提示意图，运行期权威在 L1」？control-flow 近似漏报是否仍透明（iter09 OPEN-2 同口径）？
5. **无死描述符/无魔法数**：EAA0303/EAA0304 是否都加入 `SupportedDiagnostics` 且真被 `ReportDiagnostic` 调用？判定数据是否取 §7（无硬编码）？
6. **全解构建**：0e/0w、212 测试绿（已核实）。

**产出 `D:/Godot/Cosmos/audit/iter-code29b.md`（严格）：**
```
# 迭代29-补 审计（A3/A4 诊断闭环）
## 摘要
- 全解构建：0 错误 0 警告；测试 212 通过 0 失败
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 真落实? + 结论）
| 项 | 行号 | PDR § | 真落实? | 结论 |
...
## 结论
```
独立判断。若 A3/A4 真由 §7/§3.2.3 数据驱动、测试真驱动、豁免正确、诚实注释 ⇒ 可终止（OPEN-1/OPEN-2 已闭）。

完成后回复：iter-code29b.md 已写入；open 项 X；终止判定=？（一行）。

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