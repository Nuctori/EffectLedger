# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代16 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/ScaleGuardTests.cs` + 被测 `src/Cosmos.EffectAlgebra/Numeric.cs`(NatStar +/* 溢出)、`Algebra.cs`(NetTable/Peak)、`Deviation.cs`。对照 PDR §3.1.5a(⊤ 保守)/§3.3.1/§3.3.2/§9.1。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：规模测试真验证量纲/守恒/溢出保守、非假绿）：** 核对规模守护是否真锁大规模下正确性 + 溢出⇒⊤ 保守。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **大签名守恒（§3.3.1）**：N=5000 create+release 交错 ⇒ IsConserved true；N=5001 ⇒ false？断言是否真触发（非 trivial）？
2. **大 Peak（§3.3.2）**：5000 occupy size=[100,100] ⇒ Value==500000？release 是否被排除？
3. **Deviation 大输入（§9.1）**：2000×2 [1,1] ⇒ 有限≈0；含 ⊤ ⇒ Top？不 NaN/∞
4. **溢出保守（§3.1.5a）**：`NatStar.Of(ulong.MaxValue/2)+同值` 是否真 ⇒ IsTop==true（溢出⇒⊤）？该断言是否真驱动了 Numeric.cs 的溢出检测（回指 Numeric.cs 加法代码确认有溢出分支）？
5. **性能软约束**：是否用宽松上界（如 <2000ms）而非硬超时？是否不因偶发慢而红？
6. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`/规模不足（如只用 N=3 冒充大输入）？
7. **出处注释**：每项带 §x.y？

**产出 `D:/Godot/Cosmos/audit/iter-code16.md`（严格）：**
```
# 迭代16 审计（规模/性能不回归）
## 摘要
- 全解构建：0 错误 0 警告；测试 84 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真验证? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真验证? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若规模测试真锁量纲/守恒/溢出保守、Numeric.cs 确有溢出分支、无假绿 ⇒ 可终止。

完成后回复：iter-code16.md 已写入；open 项 X；终止判定=？（一行）。

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