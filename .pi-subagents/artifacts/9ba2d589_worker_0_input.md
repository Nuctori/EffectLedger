# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代14 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/*.csproj`、`Objects.cs`(Claim)、`EffectAttributes.cs`、`Numeric.cs`；对照 PDR 不变式（§3.1.1 Claim 五元组 / §8.3.1 / §3.1.5 构造不变量）。并 `dotnet build` 已核实 0e/0w。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：类型系统能约束的用类型——构造即合法，不靠运行时 if 漏判）：** 核对加固是否真把「构造不变量」交给类型层、且无残留可变性/NRT 逃逸。

逐条（回指代码行 + 结论）：
1. **Claim 五字段 `required`**：`Kind`/`Resource`/`Mode`/`Scope`/`Size` 是否都 `required`？是否有字段仍可被省略（导致默认 null/0 的 Claim）？
2. **不可变**：`Claim` 是否为 `readonly record struct`（值语义 + 不可变）？是否有 `set` 可变后门？
3. **Nullable enable 全工程**：4 个 .csproj 是否都 `<Nullable>enable</Nullable>`？有无 `string`/`ScopeId` 等裸可空缺失标注（#nullable 逃逸）？
4. **TreatWarningsAsErrors**：3 个 src 工程是否 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`？构建是否真 0 警告（已核实 0w）？
5. **构造即合法**：`Claim` 构造是否无需运行时 if 校验（类型层保证五元组完整）？`required` 后现有调用点（测试/生成器/分析器）是否真补全（无编译错）？
6. **EffectOverride/AcceptDeviation 不可变**：reason/epsilon 是否仅 `get`（无 set 后门）？构造子强制非空/范围是否保留？
7. **无技术债**：有无 `// TODO`/`// FIXME`/`#pragma warning disable` 埋雷？有无魔法数未类型化？

**产出 `D:/Godot/Cosmos/audit/iter-code14.md`（严格）：**
```
# 迭代14 审计（类型层加固 + 全解构建）
## 摘要
- 全解构建：0 错误 0 警告（已核实，4 工程入 slnx）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 结论 OK/OPEN）
| 检查 | 行号 | 真约束? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 Claim.required + Nullable + TreatWarningsAsErrors 全落实、0e/0w、无可变后门/逃逸 ⇒ 可终止。

完成后回复：iter-code14.md 已写入；open 项 X；终止判定=？（一行）。

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