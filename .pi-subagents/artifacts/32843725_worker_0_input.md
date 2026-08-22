# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代28 全代码残差审计」（用户铁律：每一个符号都要有明确的数学边界定义+明确语义；类型系统能约束的用类型，类型系统约束不了的写在注释上。不留技术债）。只审计，不写代码。

**只读全部：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/*.cs`（Numeric/Objects/Algebra/SignedNet/Deviation/ApiMapping/DerivedMetrics/EffectAttributes）
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`
- `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/*.cs`（仅确认覆盖广度，不逐行）
- `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（§3.x/§7/§8.x/§9.1/§14）
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标：** 以用户铁律为准，扫全代码库找**残差技术债**：
1. **符号数学边界**：每个 public 类型/方法/字段/operator 是否都有明确数学边界（构造不变量、⊤ 处理、量纲、偏序）？有无「类型未约束 + 注释也未承载」的裸符号？
2. **类型 vs 注释分工**：类型能约束的（非空/required/enum/readonly record/构造子抛）是否已由类型强制，还是靠运行时 if 漏判？类型约束不了的（控制流近似/运行期权威/残差）是否写在注释且带 § 出处？
3. **注释真实性**：有无注释声称「类型保证 X」但类型实际未保证（注释吹）？有无 `// TODO`/`// FIXME`/`#pragma warning disable` 埋雷？
4. **魔法数**：有无未类型化/未引 § 的硬编码常量（如 0/1/ε=1 未注明 §9.1）？
5. **L2/L3 诚实性**：Generator/Analyzer 是否真委托 L1、残差（编译期拿不到运行期 §7 数据）是否诚实注释、控制流近似边界是否明言？
6. **跨层一致性**：L2/L3 调用的 L1 API 是否都真实存在（无漂移）？
7. **测试覆盖广度**：208 测试是否覆盖全部 8 个 src 文件 + §7/§8/§3.x 主要不变量？有无明显未覆盖的核心符号？

**产出 `D:/Godot/Cosmos/audit/iter-code28.md`（严格）：**
```
# 迭代28 审计（全代码残差技术债）
## 摘要
- 全解构建：0 错误 0 警告（已核实 dotnet build）
- 测试：208 通过 0 失败
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐符号/逐文件残差核对（回指行号 + 结论）
| 符号/文件 | 行号 | 数学边界? | 类型强制? | 注释真实? | 结论 |
...
## open 项清单（若有，附严重度+建议）
## 结论
```
独立判断，按用户铁律严苛标准。若有真残差（类型未约束且无注释、注释吹、魔法数、埋雷）⇒ 列出可闭项；若全库已达「类型约束+注释承载+零技术债」⇒ 可终止。

完成后回复：iter-code28.md 已写入；open 项 X；终止判定=？（一行）。

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