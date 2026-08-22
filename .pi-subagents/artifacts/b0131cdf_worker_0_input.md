# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代26 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/BucketIsolationTests.cs` + 被测 `src/Cosmos.EffectAlgebra/Objects.cs`(Signature 三桶)/`Algebra.cs`(NetTable).对照 PDR §3.1.4b(三桶)/§3.3.1(net 仅 occupy)/§8.2(DO-7 量纲隔离)。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：维度隔离真锁、可证伪、非假绿）：** 核对三桶隔离 + net 仅 occupy 测试是否真落实 §3.1.4b/§3.3.1 且可证伪。

逐条（回指测试行号 + 被测行号 + PDR § + 结论）：
1. **三桶独立（§3.1.4b）**：同资源 Read/Write/Occupy 三 claim ⇒ `ReadClaims`/`WriteClaims`/`OccupyClaims` 各仅含对应一条、互不串？断言可证伪（混桶必红）？
2. **net 仅 occupy（§3.3.1/DO-7）**：同资源 Read+Write+Occupy 三 claim（occupy size=[5,5]）⇒ net 区间 == [5,5]（read/write 不进 net）？真断言还是只测单一 occupy？
3. **跨资源不串（DO-6）**：Tree("x") 与 Memory("m") 各 occupy ⇒ net 字典两键独立、各自守恒判据对应？
4. **Union 幂等/交换（§3.2.1）**：`Union(s,s)` 三桶 SetEquals；`Union(a,b)==Union(b,a)`？
5. **守恒判据（§3.3.1 DO-9）**：create+release⇒true、仅 create⇒false？
6. **可证伪**：若 NetTable 误纳入 read/write（去掉 `continue`），对应断言是否必红？
7. **假绿扫描**：有无 `[Fact]` 无断言/`Assert.True(true)`/只测 trivial？
8. **出处注释**：引 §3.1.4b/§3.3.1/§8.2？

**产出 `D:/Godot/Cosmos/audit/iter-code26.md`（严格）：**
```
# 迭代26 审计（Signature 维度隔离）
## 摘要
- 测试：194 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若三桶隔离+net 仅 occupy+跨资源不串 真锁、可证伪、无假绿 ⇒ 可终止。

完成后回复：iter-code26.md 已写入；open 项 X；终止判定=？（一行）。

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