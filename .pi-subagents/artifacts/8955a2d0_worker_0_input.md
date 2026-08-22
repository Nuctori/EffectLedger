# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代29 PDR↔代码对账」（核对 PDR 各章是否已被交付代码覆盖或诚实标记为 out-of-scope）。只审计，不写代码。

**只读：**
- `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（通读，重点 §7/§8/§9/§10/§11/§12/§13/§14 是否存在及内容）
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/*.cs`、`Generator/*.cs`、`Analyzer/*.cs`、`tests/.../*.cs`
- `D:/Godot/Cosmos/LANDING_PLAN.md`（架构 + 验证策略）
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：不留技术债＝不静默留缺口）：** 逐 PDR 章节核对交付状态：
1. **§3 代数核心（§3.1–§3.3）**：是否已由 L1 全实现（Numeric/Objects/Algebra/SignedNet/DerivedMetrics）？逐小节（§3.1.1–§3.3.2）回指代码文件。
2. **§7 白名单 / §8.1 release-class**：是否已由 ApiMapping.cs 编码？逐 §7.1–§7.10 是否覆盖？
3. **§8.2 DO-7 / §8.3 属性**：DO-7 量纲隔离是否在 L1 运行期真落实（NetTable 仅 occupy）？§8.3 属性是否已由 EffectAttributes.cs 实现 + 构造子强制？
4. **§9.1 Deviation**：是否已由 Deviation.cs 实现（ε=1、⊤ 跳过）？
5. **§10（若有，集成/运行期）**：PDR §10 讲什么？运行期采样（如每帧 Σnet）是否在 L1 有载体（SignatureDeviation.Calculate 取两 Signature）还是依赖 Godot 运行期（out-of-scope，诚实注释）？
6. **§11 稳定性法则**：是否已由 StabilityAuditTests.cs（DO-1..DO-10）锁为回归守护？
7. **§12（若有，采纳/迁移）**：是否涉及 Godot 工程改造（out-of-scope，本交付是纯 C# 代数库 + L2/L3 工具，不改造 Godot 工程）？
8. **§13（若有，CI/门禁）**：是否由 Tests 工程 + TreatWarningsAsErrors + 全解构建门禁覆盖？L3 Analyzer 是否在 Godot 工程接入（out-of-scope：需 Godot.NET.Sdk，本机未装，诚实注释）？
9. **§14 验证矩阵**：是否已由 VerificationMatrixTests + 全测试套件覆盖可自动化项？

**产出 `D:/Godot/Cosmos/audit/iter-code29.md`（严格）：**
```
# 迭代29 审计（PDR↔代码对账）
## 摘要
- 全解构建：0 错误 0 警告；测试 208 通过 0 失败
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐 PDR 章节对账（章节 | 状态 | 代码/测试证据 | 结论）
| PDR 章节 | 状态 | 证据 | 结论 |
...
## out-of-scope 诚实清单（必须显式，不得静默缺口）
## open 项清单（若有，仅限「应实现但未实现」的真缺口，非 out-of-scope）
## 结论
```
独立判断。若已交付章节均真覆盖、out-of-scope 章节显式诚实标注（无静默缺口）⇒ 可终止。若发现「应实现却未实现」的真缺口 ⇒ 列出（但据前序迭代，这类应已闭环）。

完成后回复：iter-code29.md 已写入；open 项 X（真缺口 Y / out-of-scope Z）；终止判定=？（一行）。

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