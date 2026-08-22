# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 PDR §14 验证矩阵的**可自动化部分**（迭代11），`dotnet test` 绿。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §14（搜 `## 14` 或 `### 14`），重点 S1–S3（稳定性法则表）与 A1–A5（架构不变性表）。并 read `src/Cosmos.EffectAlgebra/Objects.cs`(ScopeId/Self/Shell)、`Algebra.cs`(NetTable/Peak)、`ApiMapping.cs`(白名单)、`Deviation.cs`。

**任务：** §14 矩阵列出了稳定性/架构验证项。把**能在纯 L1（零 Godot）内机械验证**的项编码为测试（新文件 `VerificationMatrixTests.cs`），逐项回指 §14 出处：
- **S1–S3（稳定性法则）**：原 §11 的 10 条根因（DO-1..DO-10）在 L1 层的可验证项。逐项编码能静态/运行时断言的：
  - DO-1 单值区间归 default（§3.1.5）：`Interval.Exact` 缺省 ⇒ `Interval.Default`；已有，补显式断言 1 条回指 §3.1.5。
  - DO-3 ⊤ 不触发 0.2 报警（§3.1.5c/§9.1）：`DeviationVal.Top.ExceedsThreshold(0.2)` ⇒ false；另取一 `DeviationVal.Of(0.5).ExceedsThreshold(0.2)` ⇒ true。
  - DO-6 栈/堆分离：资源 type 区分（`Tree` vs `Memory` vs `Gpu`）——构造 `Claim` 用不同 ResourceId，断言 `NetTable.Compute` 不会把 `Memory` 的 net 误记到 `Tree`（量纲隔离）。
  - DO-7 量纲隔离（§3.3.1）：`NetTable.Compute` 仅 occupy 桶（read/write 不进 net）；构造含 read/write/occupy 同资源的 Signature，断言 net 仅含 occupy。
  - DO-8 单点真相（§3.1.4a）：`ResourceId.Normalize` 幂等 + `Self("signal_x")≡SignalBus("x")` 唯一规范形；已有，补回指 §3.1.4a。
  - DO-9 守恒（§3.3.1）：create+release 同资源 ⇒ `IsConserved` true；仅 create ⇒ false（fail-closed）。已有，补回指 §3.3.1/DO-9。
  - DO-10 自洽（§3.2.3）：`Compatible` 全函数 + 对称；已有，补回指 §3.2.3。
- **A1–A5（架构不变性）**：L1 作为「可独立验证的数学核心」的不变性：
  - A1 零 Godot 依赖：测试工程能编译即证明（注释明言，不写断言，但可 `Assert.True(typeof(Claim).Assembly.GetName().Name == "Cosmos.EffectAlgebra")`）。
  - A2 类型即约束：构造 `Interval` 非法（`lo=⊤` 且 hi 有限）应抛 `ArgumentException`；`Claim` 空 resource 应抛。
  - A3 白名单完整：断言 `GodotApiWhitelist.All.Length > 0` 且每条 `Claims` 非空。
  - A4 注释承载残差：本测试文件本身不带魔法数（注释引 §14）。
  - A5 测试可复现：`Random(14)` 跑一遍矩阵关联断言（可选，不强制）。

**约束（用户铁律）：** 每项测试带 §14/§3.x/§9.1 出处；能机械验证的才编码，运行期不可达的（如真实 Godot 行为）用注释声明「out-of-scope，权威在 L1 定义」而非硬编。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 VerificationMatrixTests.cs（§14 S1-S3/A1-A5 可自动化项 + 出处注释）。

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