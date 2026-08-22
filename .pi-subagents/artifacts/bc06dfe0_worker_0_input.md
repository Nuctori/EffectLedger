# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代17 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/*.cs`（仅看文件头 1-5 行模块文档头）。对照 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` 各 § 编号 + `D:/Godot/Cosmos/LANDING_PLAN.md` §3（架构角色）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：每文件声明「实现 PDR §x + LANDING_PLAN 角色」）：** 核对 8 个 .cs 的模块文档头是否真实、准确（§ 号存在且语义匹配角色）。

逐条（回指文件 + 头注释行 + 实际 PDR § 标题 + 结论）：
1. Numeric.cs → §3.1.5a/§3.1.5b/§3.1.5c（ℕ*/区间/DeviationVal）？角色 L1 纯代数核心？
2. Objects.cs → §3.1.1/§3.1.2/§3.1.3b/§3.1.4a/§3.1.4b（Claim/ResourceId/ScopeId/Signature）？角色 L1 核心？
3. Algebra.cs → §3.2.1/§3.2.3/§3.3.1/§3.3.2（组合/Compatible/net/Peak）？角色 L1 运算？
4. SignedNet.cs → §3.3.1（有符号网值）？角色类型边界载体？
5. Deviation.cs → §9.1（Deviation 公式）？角色 L1 派生度量？
6. ApiMapping.cs → §7（白名单）/§8.1（release-class）？角色 L1 数据层？
7. DerivedMetrics.cs → §3.2.5/§3.3（循环组合/便利封装）？角色 L1 派生度量？
8. EffectAttributes.cs → §8.3.1/§8.3.2（两属性）？角色 L1 属性层？
9. **准确性**：抽查 § 号是否真指向 PDR 对应章节（标题匹配）？有无错挂？
10. **无错挂/无漏**：8 文件是否都带头？有无 § 号指向不存在章节？

**产出 `D:/Godot/Cosmos/audit/iter-code17.md`（严格）：**
```
# 迭代17 审计（模块级文档头）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐文件头注释核对（回指文件 + 行 + 实际 § + 准确? + 结论）
| 文件 | 行 | 所引 § | PDR 标题 | 准确? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 8 文件头注释真实、§ 号准确、无错挂/遗漏 ⇒ 可终止。

完成后回复：iter-code17.md 已写入；open 项 X；终止判定=？（一行）。

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