# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代06 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectAttributes.cs`，对照 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §8.3（搜 `### 8.3`）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：类型约束数学边界，约束不了的写注释）：** 核对 §8.3 两属性是否真落实「可覆盖域受限 + 边界靠类型/构造子强制」。

逐条（回指代码行 + PDR § + 结论）：
1. **EffectOverride reason 非空强制（§8.3.1）**：构造子是否真 `throw` 当 reason 空？还是只注释？
2. **kind 禁止覆盖（§8.3.1）**：类型是否真**不提供** OverrideKind 属性（类型层禁止 read/write/occupy 互转）？有无任何途径设 kind？
3. **可覆盖域（§8.3.1）**：是否仅暴露 mode/size/scope 覆盖（OverrideMode/OverrideSize/Scope）？DO-9 根因不压制、DO-7 不豁免是否注释明言？
4. **AcceptDeviation epsilon 上界（§8.3.2）**：构造子是否真 `throw ArgumentOutOfRangeException` 当 epsilon∉[0,0.5]？还是只注释？
5. **AcceptDeviation 语义（§8.3.2）**：注释是否明言「仅放宽运行时报警、不豁免编译期 DO、作用域 scope」？
6. **L1 零 Godot 依赖 / 是 Attribute 可挂方法或类**：两个类是否 `: Attribute`、可用 `[EffectOverride(...)]` 标注？
7. **注释/出处**：每条是否带 §8.3.1/§8.3.2 出处 + 不变式？

**产出 `D:/Godot/Cosmos/audit/iter-code06.md`（严格）：**
```
# 迭代06 审计（§8.3 属性）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 类型真约束? + 结论）
| 检查 | 代码行 | PDR § | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 reason/epsilon 边界靠构造子强制、kind 不可覆盖靠类型、注释完整 ⇒ 可终止。

完成后回复：iter-code06.md 已写入；open 项 X；终止判定=？（一行）。

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