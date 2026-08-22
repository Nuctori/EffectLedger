# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是 fresh-context 独立终检 subagent。只读代码/测试/PDR，**不读** `audit/` 目录任何文件（含 iter-code*），独立核验交付声称是否真实成立。不写代码（除非发现真缺口且可立即闭）。

**只读：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/*.cs`（Numeric/Objects/Algebra/SignedNet/Deviation/ApiMapping/DerivedMetrics/EffectAttributes）
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`
- `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/*.cs`
- `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（§3.x/§7/§8/§9.1/§11/§14）
- `D:/Godot/Cosmos/DELIVERABLE.md`、`LANDING_PLAN.md`

**核验清单（逐项回指文件:行 + 结论）：**
1. 全解能 `dotnet build` 0e/0w、`dotnet test` 全绿（你跑一次确认；本机需 `$env:MSBUILD_EXE_PATH=$null`）
2. L1 每个 public 符号是否带 §x.y 出处（抽查 ≥12）
3. §3.3.1 net 有符号：create+release 同资源 ⇒ IsConserved true；仅 create ⇒ false（读代码+测试确认）
4. §3.1.5a ⊤ 闭包 + 溢出⇒⊤（NatStar +/*）
5. §7 白名单 38 条 + §8.1 release-class 7 项存在
6. §8.3 属性 reason 非空 / ε∈[0,0.5] 由构造子强制（代码+测试）
7. §9.1 Deviation ε=1 + ⊤ 跳过
8. L2 生成器真委托 L1（非桩）
9. L3 三条诊断 EAA0901/EAA0303/EAA0304 存在且逻辑由 §7/§3.2.3 数据驱动
10. 测试是否真触发（非假绿）：抽查 ToolingTests/EndToEndTests/CrossTableTests/ScaleGuardTests 各一条，确认断言可证伪
11. DELIVERABLE.md 声称与实际文件树一致

**产出 `D:/Godot/Cosmos/audit/final-review.md`（严格）：**
```
# Fresh-Context 终检
## 构建/测试实测：<输出>
## 核验逐项（# 声称 | 文件:行 | 结论 OK/缺口）
## 真缺口（若有，附严重度+最小修复）
## 结论：交付是否达到「每符号数学边界+语义明确、类型约束+注释承载、零技术债」
```
独立判断。若全部成立、无真缺口 ⇒ 结论=通过。若发现真缺口 ⇒ 列最小修复并直接改（保持 0e/0w/测试绿）后复测。

完成后回复：final-review.md 已写入；结论=？（一行）。

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