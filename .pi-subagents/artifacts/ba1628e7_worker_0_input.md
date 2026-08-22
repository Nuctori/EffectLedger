# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代07 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/PropertyTests.cs` + 被测实现（Numeric/Objects/Algebra/SignedNet/Deviation）。对照 PDR §3.1.5a/§3.1.5b/§3.1.3b/§3.2.3/§3.1.4a/§3.1.1。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：测试证明数学边界真成立，不靠吹）：** 核对随机性质测试是否真覆盖边界且非假绿。

逐条（回指测试行号 + 实现行号 + PDR § + 结论）：
1. NatStar 随机 ⊤ 闭包：是否真含 `Top` 输入（0×⊤=⊤、a×Top=Top、Max/Min 与 Top）？结合/交换随机多组？
2. NatStar CompareToFinite：两 Top、一 Top 情形是否真测（符号语义正确）？
3. Interval.Merge 随机：是否含 `[x,⊤]`/`[⊤,⊤]` 合法形式（生成器避开 lo=⊤ 且 hi 有限的非法组合）？幂等/交换/结合随机多组？
4. ScopeId ⊆* 随机：跨标签不同名不可比较是否真测？Global 最大元随机？
5. Compatible 随机：25×25 全组合 + 随机；CONFLICT/CONFLICT/Unknown/Use 是否断言？
6. Normalize 随机幂等 + Claim.Normalize 一致：Self("signal_x")≡SignalBus(x) 对照是否真测？缺省 size⇒Default？
7. **边界覆盖**：生成器是否真触达 ⊤/0/缺省/跨桶？有无「只测安全值、定律在边界其实没验证」？
8. **假绿扫描**：有无断言恒真（`Assert.True(true)` 或随机生成保证必过）？随机种子是否固定可复现（Random(42)）？
9. **出处注释**：测试是否带 §x.y？

**产出 `D:/Godot/Cosmos/audit/iter-code07.md`（严格）：**
```
# 迭代07 审计（L1 性质测试）
## 摘要
- 测试：41 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + PDR § + 真覆盖? + 假绿? + 结论）
| 测试 | 行号 | PDR § | 真覆盖边界? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若随机测试真覆盖 ⊤/0/缺省/跨桶边界、无假绿、定律全验证 ⇒ 可终止。

完成后回复：iter-code07.md 已写入；open 项 X；终止判定=？（一行）。

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