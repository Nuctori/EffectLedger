# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代13 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/*.cs`（Numeric/Objects/Algebra/SignedNet/Deviation/ApiMapping/DerivedMetrics/EffectAttributes）。对照 PDR `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` 各 § 编号（§3.1.5a/§3.1.5b/§3.1.5c/§3.1.2/§3.1.4a/§3.1.4b/§3.1.3b/§3.2.3/§3.3.1/§3.3.2/§3.2.5/§9.1/§7/§8.1/§8.3.1/§8.3.2）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：类型约束数学边界 + 注释承载语义与 §x.y 出处；每 public 符号必有真出处、且出处准确）：** 核对每个 public 符号的 § 出处是否**真实存在且与语义匹配**（不是乱写 § 号充数）。

逐条（回指代码行 + 实际 PDR § 标题 + 结论）：
1. **覆盖率**：逐 public 类型/方法/属性/ctor，是否都带 `§x.y`？有无遗漏（抽查所有文件，给出遗漏清单，没有则写「无」）。
2. **准确性**：抽查 ≥10 个符号的 § 号，对照 PDR 实际章节标题是否匹配该符号语义。例如：`NatStar` 引 §3.1.5a 是否真是「闭环 ℕ*」？`Interval.Merge` 引 §3.1.5b 是否真是「上下界合并」？`NetTable` 引 §3.3.1 是否真是「net 净占用」？`Calculate` 引 §9.1 是否真是 Deviance 公式？`GodotApiWhitelist` 引 §7？`ReleaseClass` 引 §8.1？`EffectOverride` 引 §8.3.1？`AcceptDeviation` 引 §8.3.2？`LoopCount` 引 §3.2.5？`Peak` 引 §3.3.2？`DeviationVal` 引 §3.1.5c？
3. **无错挂**：有无 § 号指向不存在章节或语义不符（如把「组合律」挂到 §9.1）？
4. **注释质量**：出处注释是否同时承载「类型约束不了的语义」（fail-closed/控制流近似/量纲隔离），而非只剩废话？

**产出 `D:/Godot/Cosmos/audit/iter-code13.md`（严格）：**
```
# 迭代13 审计（PDR 出处注释完整性）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐文件 public 符号出处核对（回指行号 + 实际 § + 准确? + 结论）
| 符号 | 行号 | 所引 § | PDR 标题 | 准确? | 结论 |
...
## 遗漏清单（若有）
## open 项清单（若有）
## 结论
```
独立判断。若覆盖率 100% 且抽查 § 号均准确、无错挂 ⇒ 可终止。

完成后回复：iter-code13.md 已写入；open 项 X；终止判定=？（一行）。

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