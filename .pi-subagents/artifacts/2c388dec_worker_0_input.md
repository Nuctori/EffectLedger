# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代20 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`、对应测试 `tests/Cosmos.EffectAlgebra.Tests/GeneratorEmitTests.cs`(或 ToolingTests.cs 新增项)、被引用 `src/Cosmos.EffectAlgebra/ApiMapping.cs`/`Objects.cs`(Signature)。对照 PDR §14 L2 + LANDING_PLAN §1（L2=翻译层）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：生成器真委托 L1、非桩、残差诚实；数学在 L1）：** 核对 L2 升级后是否真为「Godot 语法→L1 Signature」的翻译层且生成代码可运行消费。

逐条（回指代码行 + 被测行号 + PDR/LANDING § + 结论）：
1. **真委托 L1（非桩）**：生成代码是否真含 `GodotApiWhitelist.All` 引用 + `Signature.Union`/`Of` 调用 + `Compute_<method>` 方法体（非「return baseSig; // TODO」桩）？
2. **生成代码可运行消费**：测试是否真把生成代码编译进编译 + 反射调用 `Compute_AddChild` ⇒ 返回 Signature 含白名单 Claims（如 `Kind.Occupy`）？还是只 grep 文本？
3. **属性匹配（长短名）**：生成器识别 `[EffectOverride]` 与 `[EffectOverrideAttribute]` 两种形式是否都匹配（subagent 修了一处只匹配长名的 bug）？有无仍漏短名？普通方法是否不被误判？
4. **每方法独立**：不同标注方法是否生成独立 `Compute_<name>` 且互不串（互不覆盖）？
5. **数学在 L1**：生成代码是否不重算 net/Peak/Compatible（仅组合 Signature）？
6. **残差诚实**：是否注释「§7 数据运行期实例化为 Claims；L1 数学运行期权威」而非假绿？
7. **约束（用户铁律）**：类型边界由 L1 强制；生成代码用 L1 真实类型构造 Signature（不 new 裸对象）？
8. **零 Godot 依赖 / 零魔法数**：生成器有无 `using Godot`？API 名是否取 §7 白名单而非硬编码？

**产出 `D:/Godot/Cosmos/audit/iter-code20.md`（严格）：**
```
# 迭代20 审计（L2 生成真实签名）
## 摘要
- 全解构建：0 错误 0 警告；测试 141 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR/LANDING § + 结论）
| 检查 | 行号 | 被测 | PDR/LANDING § | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若生成器真委托 L1、生成代码可运行消费、属性长短名都匹配、数学在 L1、残差诚实 ⇒ 可终止。

完成后回复：iter-code20.md 已写入；open 项 X；终止判定=？（一行）。

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