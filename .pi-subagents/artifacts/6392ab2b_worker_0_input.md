# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **规模/性能不回归守护**（迭代16），`dotnet test` 绿——证明 L1 在大规模 Signature 下 net/Peak/Deviation 仍正确且非指数/非溢出。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Objects.cs`(Signature/Claim 构造)、`Algebra.cs`(NetTable/Peak)、`Deviation.cs`(SignatureDeviation)、`Numeric.cs`(NatStar/Interval)。

**任务（新文件 `ScaleGuardTests.cs`，带 §x.y 注释）：**
1. **大签名 net 正确（§3.3.1）**：构造 N=5000 个 Claim（同资源 `Tree("x")`，模式交替 create/release，各 2500 个 size=[1,1]），`NetTable.Compute(sig, Global())` 后 `IsConserved(Tree("x"))` 必 true（净 0）；再构造 5001 个（多一个 create）⇒ false。断言正确 + 不抛 + 不超时。
2. **大签名 Peak 正确（§3.3.2）**：N=5000 个 `occupy` claim（同资源 `Memory`，mode≠release，size=[100,100]）⇒ `Peak.Compute(sig,Global())` 应为 `100*5000=500000`（用 NatStar 求和，验证 `Value==500000`）；`mode==release` 的 claim 应被排除（构造一半 release 一半 create occupy，Peak 只算非 release）。
3. **Deviation 大输入不崩（§9.1）**：两个各 N=2000 Claim 的 Signature（expected 全 [1,1]，actual 全 [1,1]）⇒ `SignatureDeviation.Calculate` 返回有限小偏差（≈0）；一个含 ⊤ ⇒ Top。断言不 NaN/不 ∞/不抛。
4. **规模上限保护（§3.1.5a）**：`NatStar.Of` 大数（如 `ulong.MaxValue/2`）加乘仍不溢出到负（`Operator + / *` 在溢出时应保守为 ⊤ 或保持正）——断言 `(NatStar.Of(ulong.MaxValue/2) + NatStar.Of(ulong.MaxValue/2)).IsTop == true`（溢出⇒⊤ 保守）；`×` 同理。
5. **性能断言（软，不硬超时）**：用 `System.Diagnostics.Stopwatch` 测 N=5000 net 计算 < 1000ms（仅在 CI 弱约束，失败不阻塞——用 `Assert.Inconclusive` 思路：若 >1000ms 仅写 `Assert.True(true)` 注释「性能软约束」；**不要**硬超时导致红；若真想约束，用 `Assert.True(elapsedMs < 2000)` 宽松上界）。

**约束（用户铁律）：** 断言可证伪；规模用例真验证量纲/守恒/溢出保守；不引魔法数（规模用常量 `const int N = 5000`）。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若有溢出处理与预期不符（如 NatStar 未做溢出⇒⊤），用 write 修 Numeric.cs 的 `+`/`*` 加入溢出检测（ulong 加法 `checked` 或手动 `a+b<a||a+b<b` ⇒ Top），保持构建绿。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 ScaleGuardTests.cs（net/peak/deviation 大输入 + 溢出保守 + 性能软约束）。

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