# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代11 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/VerificationMatrixTests.cs` + 被测 src。对照 `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §14（S1-S3/A1-A5）+ §3.1.5/§3.3.1/§9.1/§3.1.4a/§3.2.3。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：矩阵可自动化项真验证、不可达项诚实注释、不硬编假绿）：** 核对 §14 验证矩阵编码是否真对应且非假绿。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **DO-1 单值⇒Default（§3.1.5）**：是否真断言 `Interval.Exact` 缺省⇒`Interval.Default`？
2. **DO-3 ⊤ 不触发 0.2（§3.1.5c/§9.1）**：是否真断言 `DeviationVal.Top.ExceedsThreshold(0.2)==false` + 有限 0.5⇒true？
3. **DO-6 栈/堆分离（§3.3.1/量纲）**：是否真构造 Tree/Memory/Gpu 不同资源、断言 net 不串桶？
4. **DO-7 量纲隔离（§3.3.1）**：是否真构造 read/write/occupy 同资源、断言 net 仅 occupy？
5. **DO-8 单点真相（§3.1.4a）**：是否真断言 Normalize 幂等 + Self("signal_x")≡SignalBus("x")？
6. **DO-9 守恒（§3.3.1）**：是否真 create+release⇒IsConserved true、仅 create⇒false？
7. **DO-10 自洽（§3.2.3）**：是否真 Compatible 全函数/对称？
8. **A1 零 Godot**：是否真断言被测程序集名为 Cosmos.EffectAlgebra（编译即证）？
9. **A2 类型即约束**：是否真断言 Interval 非法构造抛 + Claim 空 resource 抛？
10. **A3 白名单完整**：是否真断言 All.Length>0 + 每条 Claims 非空？
11. **A4/A5**：注释引 §14？随机数可复现？
12. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`/永远通过？
13. **出处注释**：每项带 §14/x.y？

**产出 `D:/Godot/Cosmos/audit/iter-code11.md`（严格）：**
```
# 迭代11 审计（§14 验证矩阵）
## 摘要
- 测试：57 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 真验证? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真验证? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 §14 可自动化项真验证、不可达项诚实、无假绿 ⇒ 可终止。

完成后回复：iter-code11.md 已写入；open 项 X；终止判定=？（一行）。

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