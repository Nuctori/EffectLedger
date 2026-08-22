# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **跨层引用一致性测试**（迭代15），`dotnet test` 绿——证明 L2/L3 调用 L1 的 API 签名都真实存在、无漂移。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`、`src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`、`src/Cosmos.EffectAlgebra/ApiMapping.cs`(GodotApiWhitelist/ReleaseClass/Claim)、`Objects.cs`(Claim/Signature/ResourceId/Mode/Kind/ScopeId)、`Algebra.cs`(NetTable/Peak/Compatible)、`Deviation.cs`(SignatureDeviation)。

**任务（新文件 `CrossLayerTests.cs`）：** 用反射/类型检查在运行期断言 L2/L3 用到的 L1 符号确实存在且签名匹配（机械化「无漂移」），逐项带 §x.y：
- **A_L1_GodotApiWhitelist存在**：`typeof(GodotApiWhitelist).GetProperty("All")` 存在且类型 `ImmutableArray<ApiMapping>`；`GodotApiWhitelist.All.Length > 0`。
- **A_L1_ReleaseClass存在**：`typeof(ReleaseClass).GetMethod("IsRelease")` 存在且签名 `(string)->bool`；`ReleaseClass.Names` 含 `"queue_free"` 等 7 个。
- **A_L1_Claim字段**：`typeof(Claim)` 五字段 `Kind`/`Resource`/`Mode`/`Scope`/`Size` 均存在（反射取属性/字段名集合，含 required 修饰）。
- **A_L1_Compatible**：`typeof(NetTable)` 或静态 `Compatible.IsCompatible(Mode,Mode)` 存在且签名 `(Mode,Mode)->bool`（若 Compatible 是静态类/枚举扩展，按实际写）。
- **A_L1_NetTable**：`NetTable.Compute(Signature,ScopeId)` 与 `NetTable.IsConserved(ResourceId)` 方法存在（反射）。
- **A_L2_Generator类型**：`typeof(EffectAlgebraGenerator)` 存在且实现 `IIncrementalGenerator`（反射 `typeof(IIncrementalGenerator).IsAssignableFrom`）。
- **A_L3_Analyzer类型**：`typeof(EffectAlgebraAnalyzer)` 存在且 `[DiagnosticAnalyzer]` + `: DiagnosticAnalyzer`；`SupportedDiagnostics` 含 id `EAA0901`。
- **A_whitelist_ApiMapping字段**：`ApiMapping.GodotApi`/`Claims` 字段存在；每条 `Claims` 元素为 `Claim` 且 `Resource/Kind/Mode/Scope/Size` 均非空（构造即合法，呼应 iter14 required）。
- **每个测试用反射断言 + §x.y 注释**。

**约束（用户铁律）：** 反射断言可证伪（签名漂移⇒红）；不引魔法数；注释引 §7/§8.1/§3.x。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 CrossLayerTests.cs（L1↔L2/L3 符号/签名一致性反射断言，N 项 + 出处）。

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