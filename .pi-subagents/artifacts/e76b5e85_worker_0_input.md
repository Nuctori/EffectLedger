# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建整个 solution。任务：L1 类型层加固 + 全解构建（迭代14），0 错误 0 警告。

环境：`cd D:/Godot/Cosmos; $env:MSBUILD_EXE_PATH = $null; dotnet build Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Objects.cs`(Claim/ResourceId/ScopeId/Signature)、`Numeric.cs`、`ApiMapping.cs`、`EffectAttributes.cs`。并 read 各 .csproj 的 `LangVersion`/`Nullable`/`TreatWarningsAsErrors` 设置。

**任务（用户铁律：类型系统能约束的用类型——用 .NET 10 的 `required`/non-nullable/record 不可变强制构造不变量，让「构造即合法」）：**
1. **`Claim` 用 `required` 成员**：`Kind`/`Resource`/`Mode`/`Scope`/`Size` 标 `required`（构造时必填，类型层防漏字段）。`Claim` 现有 record，把字段声明加 `required`。
2. **`EffectOverrideAttribute`/`AcceptDeviationAttribute` 不可变**：确认 reason/epsilon 经构造子强制后属性为 `get;`（init 也行）；无 set 可变后门。
3. **`Nullable` 开启**：每个 .csproj 设 `<Nullable>enable</Nullable>`（若未开）；把 `string?`/`ScopeId?` 等可空标注补全，消除全部 nullable 警告（构建 0 警告）。`ResourceId` 内部 `Rid`/`StringName` 值类型非空（record struct 天然）。
4. **`TreatWarningsAsErrors`**：每个 .csproj 设 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`（让「残留警告=缺陷」由编译器强制；契合用户「不留技术债」）。
5. **全解构建**：`dotnet build Cosmos.EffectAlgebra.slnx` 对 4 工程（src ×3 + tests）统一构建，0 错误 0 警告（tests 工程可不开 TreatWarningsAsErrors，但 src 三工程必须 0w）。

**约束（用户铁律）：** 类型强制构造不变量；不靠运行时 if 漏判；每个成员边界由类型/required/nullable 承载；§x.y 注释保留。

**验证（必须）：** `dotnet build Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly` 0 错误 0 警告。若 `required` 导致现有构造点报错，用 write 修正调用点（如测试/生成器/分析器里的 `new Claim(...)` 补全 required 字段或加 `with`）。

**完成后最后一行回复：** BUILD_OK 错误数=0 警告数=0，已加固 Claim.required + Nullable enable + TreatWarningsAsErrors（src 三工程）+ 全解构建。

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