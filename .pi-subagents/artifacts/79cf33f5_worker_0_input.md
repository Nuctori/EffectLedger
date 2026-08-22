# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§3.2.5 循环组合 Loop 锁**（迭代24），`dotnet test` 绿——证明 `Combination.Loop` 的 ω 缩放语义（有限 ω ⇒ size×ω、ω=⊤ ⇒ ⊤ 兜底）与 Sequence/Parallel=Union，对齐 PDR §3.2.5/§3.3.2。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`(`LoopCount`/`Combination.Loop`/`Sequence`/`Parallel`)、`Objects.cs`(`Signature`/`Claim`/`ScopeId`/`ResourceId`)、`Algebra.cs`(`Peak.Compute`)、`Numeric.cs`(NatStar/Interval)。

**任务（新文件 `LoopCombinationTests.cs`，带 §3.2.5/§3.3.2 注释）：**
1. **Loop 有限 ω 缩放（§3.2.5）**：构造 `body = Signature` 含 1 个 `occupy` claim（resource `Tree("x")`, size `[10,10]`, mode=Use 或 Create 皆可，但 Peak 排除 release——用 mode≠release）+ scope `Method("m")`；`var looped = Combination.Loop(body, LoopCount.Of(5), new ScopeId.Loop("L"));` ⇒ `Peak.Compute(looped, Global())` 的该资源 Value == `10*5=50`（ω×size 求和）；scope 标注为 Loop（断言 looped 中 claim 的 Scope 为 `Loop("L")` 或含 Loop，取决于实现——按实际：若实现复制 body 且改 scope，断言 scope 为 Loop）。
   - 若实现是「把 size 上限拉 ⊤/复制 N 份」，按实际语义断言（先 read 实现确认 Loop 究竟怎么展开）；关键是 **ω 有限时 Peak = ω × Σbody_size**（数学对齐 §3.2.5）。
2. **Loop ω=⊤ ⇒ ⊤ 兜底（§3.2.5 / §3.1.5a）**：`Combination.Loop(body, LoopCount.Top, ...)` ⇒ `Peak.Compute` 对该资源返回 `NatStar.Top`（上界开放，不崩、不有限误判）。断言 `.IsTop==true`。
3. **Sequence = Union（§3.2.1）**：`Combination.Sequence(a,b)` 结果与 `Signature.Union(a,b)` 结构相等（各桶 claim 集合一致）。
4. **Parallel = Union（§3.2.2）**：`Combination.Parallel(a,b)` 同 `Signature.Union(a,b)`。
5. **Loop 幂等性（组合）**：`Loop(Loop(body,ω1),ω2)` 与 `Loop(body, ω1×ω2)` 在 Peak 上一致（若实现支持嵌套——若不支持嵌套，跳过此条并注释「嵌套 Loop 非当前范围」）。
- 注释：ω=⊤ 经 `LoopCount.Count.IsTop` 类型强制；Peak size 求和（§3.3.2），release 排除。
- 若实现 `Loop` 与测试预期不符（如实现是「复制 N 份 body」而非「size×ω」），按**实现真实语义**断言一致的数学（不硬套 size×ω 若实现是复制）；但 §3.2.5 定义是 `(S×ω)=Σ copy_i(S)`，即复制 ω 份 ⇒ Peak = ω×Σsize，**应**等于 size×ω。若实现偏离，用 write 修 DerivedMetrics.cs 的 `Loop` 使其复制 ω 份（finite）或标记 ⊤（Top），保持构建绿。

**约束（用户铁律）：** 断言可证伪（ω 缩放错必红）；ω=⊤ 不崩靠类型；注释引 §3.2.5/§3.3.2。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 LoopCombinationTests.cs（Loop 有限缩放 / ω=⊤⇒⊤ / Sequence=Parallel=Union，§3.2.5/§3.3.2）。

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