# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§11 稳定性法则的自动回归守护**（迭代12），`dotnet test` 绿。这是「稳定性审计」的机械化：把 §11 的 10 条根因（DO-1..DO-10）映射为永不可回归的断言。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** PDR §11（搜 `## 11` 或 `### 11` 稳定性法则）与 §3.1.5/§3.1.4a/§3.2.3/§3.3.1/§9.1。并 read `ApiMapping.cs`、`Deviation.cs`、`Objects.cs`、`Numeric.cs`、`Algebra.cs`、`SignedNet.cs`。

**任务（新文件 `StabilityAuditTests.cs`）：** 把 §11 的每条「稳定性根因」映射成一个**回归守护测试**（若实现回退，此测试必红），逐项回指 §11 + 对应 §x.y：
- **DO-1（单值默认）**：`Interval.Default` 必为 `[1,1]`（单值上界保守）；`Interval.Exact(0)` 仍 [0,0] 不崩。
- **DO-2（类型即约束）**：`Interval` 构造 `lo > hi` ⇒ 抛；`lo=⊤ 且 hi 有限` ⇒ 抛（§3.1.5a 上界未知则整体未知）。
- **DO-3（⊤ 不报警）**：`DeviationVal.Top` 经 `ExceedsThreshold(任意)` 永 false（§3.1.5c）。
- **DO-4（可终止）**：`LoopCount.Top` 经 `Combination.Loop` 必产「上界开放」Signature（不抛、不无限循环）；断言结果非 null 且含 ⊤ 标记（或用 `Peak.Compute` 对该结果返回 ⊤）。
- **DO-5（偏序自洽）**：`ScopeId.IncludedIn` 自反 + 传递 + 反对称（§3.1.3b）；`Global` 最大元。
- **DO-6（栈堆分离）**：`NetTable.Compute` 对 `Tree` vs `Memory` 两资源 net 互不串（独立键）。
- **DO-7（量纲隔离）**：`NetTable.Compute` 仅 occupy 桶参与（§3.3.1）；read/write 不进 net（断言 net 字典不含 read/write 资源的「占用」贡献）。
- **DO-8（单点真相）**：`ResourceId.Normalize(Self("signal_x")) == ResourceId.Normalize(SignalBus("x"))`（§3.1.4a 唯一规范形）。
- **DO-9（守恒）**：同资源 create+release ⇒ `IsConserved` true；缺 release ⇒ false（§3.3.1）。
- **DO-10（自洽）**：`Compatible` 全函数（25 组合不抛）+ 对称。

每项测试名带 `DO-n_` 前缀 + XML 注释引 §11 + 对应 §x.y。注释明言「此测试守护 §11 根因不回归」。

**约束（用户铁律）：** 每个断言可证伪（回退必红）；不引魔法数；出处完整。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 StabilityAuditTests.cs（DO-1..DO-10 回归守护，10 项 + 出处）。

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