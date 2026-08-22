# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：为 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 加 **性质测试（随机/FsCheck 风格）**（迭代07），`dotnet test` 绿。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`
该 Tests 工程已为 xUnit。无 FsCheck，故用 `System.Random` 驱动穷举/随机生成（不引外部包，避免 nuget 环境故障）。

**测试内容（新文件 `PropertyTests.cs`）：**
用确定性随机种子（`new Random(42)`）生成大量随机输入，验证代数定律对所有输入成立（数学边界穷举证明）：
1. **NatStar 随机 ⊤ 闭包**：随机取 a,b ∈ {Of(rand 0..1000), Top}，验证 `a+b==(b+a)`（交换）、`(a+b)+c==a+(b+c)`（结合，c 同理随机）、`a*Top==Top`、`Top*a==Top`、`a.Max(Top)==Top`、`a.Min(Top)==a`（当 !a.IsTop）。运行 1000 组。
2. **NatStar CompareToFinite 全序**：随机 a,b，验证 `a.CompareToFinite(b)` 与 `b.CompareToFinite(a)` 符号相反或 0（除 ⊤ 情形，需特殊处理：两 Top⇒0；一 Top⇒±1）。
3. **Interval.Merge 随机 幂等/交换/结合**：随机 lo,hi（保证 lo≤hi，或一侧 Top），验证 `m.Merge(m)==m`、`a.Merge(b)==b.Merge(a)`、`(a.Merge(b)).Merge(c)==a.Merge(b.Merge(c))`。运行 1000 组。注意构造 `Interval` 时若 lo=Top 且 hi 有限需抛异常——生成器避免该非法组合（仅生成 [x,y]、[x,⊤]、[⊤,⊤] 合法形式）。
4. **ScopeId ⊆* 随机 偏序**：随机选构造子（Method/Type/Scene/Global/Loop/Conditional/Async 带随机名），验证 `IncludedIn` 自反（x⊆x）、Global 最大元（任意 x⊆Global）、跨标签不同名不可比较。运行 1000 组。
5. **Compatible 随机 全函数/对称/CONFLICT**：随机 a,b ∈ {Use,Create,Release,Move,Unknown}，验证 `IsCompatible(a,b)==IsCompatible(b,a)`、`IsCompatible` 不抛（全函数）、`(Create,Create)/(Move,Move)/(Release,Release)` ⇒ false、`Use` 与任意 ⇒ true、`Unknown` 与任意 ⇒ 同 `Use`。运行 25×25 全组合 + 1000 随机。
6. **Normalize 幂等随机**：随机生成 ResourceId（Tree/Self/SigBus/Signal("signal_x")/CommandBuffer/Gpu...），验证 `Normalize(Normalize(r))` 结构等于 `Normalize(r)`。运行 1000 组。
7. **Claim.Normalize 一致随机**：随机构造两个 Claim（resource 取 Self("signal_x") 与 SignalBus(x) 对照），验证归一后相等；size 缺省 ⇒ Default。运行 500 组。

**约束（用户铁律）：** 每个测试方法带 §x.y 出处注释；生成器覆盖边界（⊤、0、缺省、跨桶）。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若某定律在边界真失败，说明实现有 bug——用 write 修 src 文件直到绿（不得增量 edit 漂移）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 PropertyTests.cs（N 组随机性质测试）。

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