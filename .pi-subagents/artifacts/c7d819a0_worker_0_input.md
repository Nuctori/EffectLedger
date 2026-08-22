# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代09 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`、`.csproj`、被引用 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/ApiMapping.cs`(§7 + ReleaseClass)。对照 PDR §8.2(DO-7)/§8.3.1(kind 禁止)/§3.3.1(DO-9)/§8.1(release-class)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：L3 只发根因诊断、数学在 L1、控制流近似必须诚实注释、不留死代码）：** 核对 L3 Analyzer 骨架是否真为 L1 之上的静态根因报警层且无非诚实近似/死代码。

逐条（回指代码行 + PDR § + 结论）：
1. **是 DiagnosticAnalyzer**：`[DiagnosticAnalyzer(LanguageNames.CSharp)]` + `: DiagnosticAnalyzer` + `SupportedDiagnostics`？
2. **依赖 L1 / 零 Godot**：是否 `ProjectReference` L1、未引 Godot.NET.Sdk？诊断判定是否用 `GodotApiWhitelist.All`/`ReleaseClass.Names`（§7/§8.1 数据）而非硬编码？
3. **DO-9 近似泄漏（§3.3.1/§8.1）**：是否真检测「方法体只调 acquire/create/occupy（§7）但无 release-class（§8.1）且无 `[EffectOverride]`」⇒ 报 Warning？是否**近似**（控制流非全，运行期 Σnet 权威）且注释明言？误报/漏报是否在注释中诚实交代（非假绿）？
4. **§8.3.1 kind 禁止**：是否真靠 L1 `EffectOverrideAttribute` 无 OverrideKind 属性（类型层强制），L3 仅信息性确认「无 kind 参数」？有无重复实现 type 已保证的约束？
5. **DO-7 量纲混算（§8.2）**：是否**不**在 L3 重算（量纲隔离是 L1 `NetTable.Compute` 仅 occupy 桶的运行时行为）？L3 若涉及 DO-7 是否仅信息性且注释「权威在 L1 运行期」？
6. **诊断 id/出处**：每个 DiagnosticDescriptor 是否带 §x.y 出处 id（EAAxxxx）？`ReportDiagnostic` 是否真被调用（无死描述符）？
7. **死代码/魔法数**：有无未使用 descriptor（对比 iter-code08 OPEN-1 同类）？有无硬编码 Claim（应取 §7）？有无 `using Godot`？
8. **残差诚实**：控制流近似、运行期权威，是否两处注释明言（非 TODO 埋雷）？

**产出 `D:/Godot/Cosmos/audit/iter-code09.md`（严格）：**
```
# 迭代09 审计（L3 Roslyn Analyzer 骨架）
## 摘要
- 构建：0 错误 0 警告（已核实，Analyzer 工程）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 结论）
| 检查 | 代码行 | PDR § | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 Analyzer 真为 L1 之上的静态根因报警层、依赖经 L1、控制流近似诚实、无死描述符、零 Godot ⇒ 可终止。

完成后回复：iter-code09.md 已写入；open 项 X；终止判定=？（一行）。

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