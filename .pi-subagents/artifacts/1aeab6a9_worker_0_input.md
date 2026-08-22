# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：补全 `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/PropertyTests.cs` 两处测试覆盖缺口（迭代07 OPEN-1/OPEN-2），`dotnet test` 绿。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**改动（用 write 整体重写 PropertyTests.cs，保持其他测试不变）：**
1. **OPEN-1 / Compatible 穷举循环（约 L107-122）**：在现有 `if ((a,b) is (Mode.Create,Mode.Create)...)` 冲突断言之后，补 §3.2.3 P3 良性生命周期配对结果断言：
   - `(Create,Release)` 或 `(Release,Create)` ⇒ `Assert.True(r);`
   - `(Create,Move)` 或 `(Move,Create)` ⇒ `Assert.True(r);`
   - `(Release,Move)` 或 `(Move,Release)` ⇒ `Assert.True(r);`
   即遍历 25 组合时，对这三对断言 `IsCompatible` 为 true（r 为当前循环断言的布尔结果）。
2. **OPEN-2 / NatStar min ⊤ 律（约 L30）**：现有 `Assert.Equal(a, a.Min(NatStar.Top));`（receiver 有限）。补反向：`Assert.Equal(a, NatStar.Top.Min(a));`（receiver=Top 时 `Min` 返回 o=a；a=Top 时也成立 `Top.Min(Top)==Top`）。放在该 min 断言附近。

**约束：** 只加断言，不改实现；保持 `Random(42)` 种子与现有测试结构。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿），测试总数应略增（约 +1 方法或 +3 断言，依写法）。

**完成后最后一行回复：** FIX_OK 失败数=0，已补 OPEN-1(P3 良性配对结果断言)+OPEN-2(min(⊤,x)=x 反向)。

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