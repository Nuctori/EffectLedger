# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代02 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：**
- `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/AlgebraLawsTests.cs`
- 被测实现：`D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Numeric.cs`、`Objects.cs`、`Algebra.cs`
- 对照：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.1.5a(L211)/§3.1.5b(L226)/§3.1.3b(L141)/§3.2.3(L265)/§3.1.4a(L169)/§3.2.1(L252)/§3.1.4b(L188)
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件（含 iter-code01）。

**审计目标（用户铁律：类型约束数学边界，测试证明它真成立，不靠吹）：** 核对每个测试是否**真正穷举/覆盖**了它声称的 PDR 定律，而非假绿（例如：只测一个 trivial 值、定律在代码里没真实现却过了、断言永远为真）。

逐条核对（回指测试行号 + 实现行号 + PDR § + 结论）：
1. NatStar ⊤ 闭包测试：是否覆盖 +/*/Max/Min 的全部 ⊤ 情形（尤其 0×⊤=⊤）？`CompareToFinite` 三态？
2. NatStar 结合/交换：是否真取多值验证（不是只测一对）？
3. Interval.Merge 幂等/交换/结合 + ⊤ 律：是否真含 [1,⊤] 情形？
4. ScopeId ⊆*：自反/Global最大元/跨标签false 是否都有？传递性是否测？
5. Compatible：是否真遍历 5×5=25 全对验证全函数不抛 + 对称？CONFLICT(C,C)/(M,M)/(R,R) 是否断言 false？use 放行、Unknown⇒Use、create+release 配对是否各断言？
6. Normalize 幂等 + Self("signal_x")≡SignalBus("x") 一致性：是否真测结构相等？
7. Claim.Normalize：跨资源归一后相等 + 缺省 size⇒Default？
8. Signature.Union 幂等：同 Claim 合并一次（不是测 `Union(s,s)==s` 这种恒真）？跨桶不混？

**额外检查：**
- 测试方法是否带 [Fact]/[Theory] 且确实被 xUnit 发现（28 个总数是否真对应 28 个断言点）？
- 有无「断言恒真」测试（如 `Assert.True(true)` 或断言与定律无关）？
- 测试是否引 PDR § 出处注释？

**产出 `D:/Godot/Cosmos/audit/iter-code02.md`（严格）：**
```
# 迭代02 审计（L1 代数定律单测）
## 摘要
- 测试结果：28 通过 0 失败（已核实 dotnet test）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐测试核对（回指行号 + PDR § + 是否真覆盖 + 结论）
| 测试 | 行号 | PDR § | 真覆盖定律? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断，不信任历史审计。若测试真覆盖全部定律、无假绿、无遗漏重要定律未测 ⇒ 可终止。

完成后回复：iter-code02.md 已写入；open 项 X；终止判定=？（一行）。

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