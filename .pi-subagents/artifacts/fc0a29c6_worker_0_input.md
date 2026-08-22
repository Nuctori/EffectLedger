# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代08 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`、`.csproj`、被引用的 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/ApiMapping.cs`（§7 数据）。对照 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §14（L2/L3 规范）+ LANDING_PLAN.md §1（架构：L2=翻译层）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：生成器只翻译，数学在 L1；残差诚实写注释，不假绿）：** 核对 L2 Generator 骨架是否真为「Godot 语法→L1 Claim 翻译层」且依赖经 L1、无独立代数逻辑。

逐条（回指代码行 + PDR §/LANDING_PLAN § + 结论）：
1. **是 IIncrementalGenerator**：类是否 `: IIncrementalGenerator` + `[Generator]`？`Initialize` 是否用 `SyntaxProvider.Create` 收集带特性的方法？
2. **依赖 L1**：是否 `ProjectReference` 到 Cosmos.EffectAlgebra、**不**引用 Godot.NET.Sdk（本机未装）？生成代码是否用 L1 的 `Claim`/`Signature`/`GodotApiWhitelist` 类型？
3. **只翻译不重算**：生成器是否**不**在生成代码里重算 net/Peak/Compatible（数学全在 L1）？生成桩是否真委托 L1（如 `Signature.Union`/`GodotApiWhitelist.All`）？
4. **诚实残差**：是否对「编译期拿不到 §7 运行期数据」用注释明言（非假绿、非 TODO 埋雷）？LANDING_PLAN §1 说 L2 是薄翻译层——是否一致？
5. **Attribute 识别**：是否真用 `Attribute.IsOrHasName` 过滤 `EffectOverride`/`AcceptDeviation`？有无误判普通方法？
6. **Diagnostics / reason 强制**：`EffectOverride` reason 非空是否由 L1 构造子保证（生成器不重发）？注释是否明言？
7. **csproj**：`OutputItemType`/analyzer 元数据是否合理（`EnforceExtendedAnalyzerRules`、`IncludeBuildOutput=false`、Microsoft.CodeAnalysis.CSharp 引用）？无 Class1.cs 遗留？
8. **零 Godot 依赖 / 零魔法数**：生成器有无 `using Godot`？有无硬编码 Claim（应取 §7 白名单）？

**产出 `D:/Godot/Cosmos/audit/iter-code08.md`（严格）：**
```
# 迭代08 审计（L2 Source Generator 骨架）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 结论）
| 检查 | 代码行 | PDR/LANDING § | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 Generator 真为 L1 之上的薄翻译层、依赖经 L1、残差诚实、零 Godot 依赖 ⇒ 可终止。

完成后回复：iter-code08.md 已写入；open 项 X；终止判定=？（一行）。

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