# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：修正 `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/CrossTableTests.cs` 的 `KnownReleaseSemanticsCanon`（迭代18 OPEN-1），`dotnet test` 绿。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**改动（用 write 整体重写该文件，保持其余测试不变）：**
- 删除 `KnownReleaseSemanticsCanon` 中手写的 7 串字面量数组（与 `ReleaseClass.Names` 逐字重复）。
- 改为从权威源派生：`private static readonly ImmutableHashSet<string> KnownReleaseSemanticsCanon = ReleaseClass.Names.Select(Canonical).ToImmutableHashSet().Union(new[] { "Audio.Stop", "Anim.Stop" }.Select(Canonical));`（若 Canonical 是局部函数，保留；若不在作用域，把 Canonical 定义为文件级 `static string Canonical(string s) => s.Replace(".", "").Replace("_", "").ToLowerInvariant();`）。
- 保留 `ReleaseClassCanon`（已从 `ReleaseClass.Names` 派生）不变。
- 语义集合仍为 9 个（7 release-class + Audio.Stop + Anim.Stop），但消除重复、与 `ReleaseClass.Names` 同源，与 `NoHardcodedSevenStrings` 测试名/注释一致。
- 不动其他测试（§8.1⇒§7、§7⇒§8.1 孤儿、QueueFree 收口）。

**约束：** 仅改数据派生方式，不动断言语义；保持 build/test 绿。

**验证：** `dotnet test -clp:ErrorsOnly` 0 失败（绿），测试数不变（88）。

**完成后最后一行回复：** FIX_OK 失败数=0，已改 KnownReleaseSemanticsCanon 从 ReleaseClass.Names 派生（删 7 串硬编码，OPEN-1）。

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