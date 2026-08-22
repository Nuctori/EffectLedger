# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§3.1.4b Signature 三桶维度隔离 + NetTable 仅 occupy（DO-7）锁**（迭代26），`dotnet test` 绿——证明 read/write/occupy 三桶独立、net 仅 occupy 参与（量纲隔离），呼应 §3.3.1/§8.2。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Objects.cs`(`Signature`：`Add`/`Union`/`AllClaims`/`ReadClaims`/`WriteClaims`/`OccupyClaims` 或等价分桶访问器；`Claim` 含 `Kind`(Read/Write/Occupy))、`Algebra.cs`(`NetTable.Compute` 仅 occupy 桶、`IsConserved`)、`Numeric.cs`(NatStar/Interval)。

**任务（新文件 `BucketIsolationTests.cs`，带 §3.1.4b/§3.3.1/§8.2 注释）：**
1. **三桶独立（§3.1.4b）**：构造 `sig = new Signature()` + 3 个同资源 `Tree("x")` 但不同 Kind 的 claim（Read/Write/Occupy，各 mode=Use，size=[1,1]）；`Union` 后断言 `sig.ReadClaims` 仅含 Read 那条、`WriteClaims` 仅 Write、`OccupyClaims` 仅 Occupy（各 `Single`/Count==1）；三桶互不串（DO-7 量纲隔离在 Signature 层）。
2. **NetTable 仅 occupy（§3.3.1/DO-7）**：同资源 Tree("x") 加 Read+Write+Occupy 三 claim（occupy mode=Create size=[5,5]），`NetTable.Compute(sig, Global())[Tree("x")]` 只反映 occupy 的 [5,5]（read/write 不进 net）；断言 net 区间 == [5,5]（而非累加 read/write）。
3. **跨资源不串（DO-6）**：Tree("x") 与 Memory("m") 各 occupy claim，net 字典两键独立；断言 `IsConserved(Tree("x"))` 与 `IsConserved(Memory("m"))` 各自对应其 size。
4. **Union 幂等/可交换（§3.2.1/§3.1.4b）**：`Union(s,s)` 三桶各 `SetEquals(s)`；`Union(a,b)` 与 `Union(b,a)` 结构相等。
5. **net 守恒判据（§3.3.1 DO-9）**：同资源 occupy create[5,5] + release[5,5] ⇒ `IsConserved`==true；仅 create ⇒ false（已由 iter05/iter12 锁，此处补维度隔离上下文）。
6. **注释**：量纲隔离由 `NetTable.Compute` 的 `if (c.Kind != Kind.Occupy) continue;` + Signature 三桶分存双重保障（§3.1.4b/§8.2）。
- 若 `Signature` 无 `ReadClaims/WriteClaims/OccupyClaims` 公开访问器（iter01 修复要求加过），用 write 补到 Objects.cs（公开 `ImmutableHashSet<Claim>` 三桶），保持构建绿。
- 断言可证伪（若 Union 混桶必红）。

**约束（用户铁律）：** 维度隔离由类型/分桶访问器强制；断言可证伪；注释引 §3.1.4b/§3.3.1/§8.2。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 BucketIsolationTests.cs（三桶独立 / net 仅 occupy / 跨资源不串 / Union 幂等交换 / 守恒判据，§3.1.4b/§3.3.1/§8.2）。

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