# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：修复 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs` 两处（迭代09 OPEN-1/OPEN-2），构建仍绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**改动（用 write 整体重写该文件，保持 DO-9 近似逻辑不变）：**
1. **OPEN-1 删冗余 EAA0801**：`EffectOverrideKindForbidden` 描述符 + 其检测逻辑（约 L34-48 描述符 + L96-104 检测 `kind`/`OverrideKind` 命名参数）整段删除——因为 L1 `EffectOverrideAttribute` 类型层已无 `OverrideKind` 属性，良性代码写不出该命名参数（编译器先报 CS0117）；L3 重检属冗余护栏（技术债）。`SupportedDiagnostics` 改为仅 `ImmutableArray.Create(MissingReleaseForAcquire)`。类注释中保留「kind 覆盖由 L1 EffectOverrideAttribute 类型层禁止，L3 不重检」一句即可。
2. **OPEN-2 诚实注释 receiver-前缀漏报**：在 `MissingReleaseForAcquire` 检测逻辑附近（或类注释）补一句：明确「本近似**仅匹配带接收者前缀的调用**（如 `node.QueueFree()`、`GetTree().Free()`）；无接收者（裸 `QueueFree()`）或 `this.` 前缀、跨方法/跨对象释放的配对**不在本静态近似覆盖内**，可能静默漏报；真实闭合以运行期 `NetTable.IsConserved` 为权威」。让近似的边界透明（非假绿）。

**约束：** 只删冗余 + 补诚实注释，不改 DO-9 检测核心逻辑、不改 L1。

**验证：** `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。确认 `EffectOverrideKindForbidden` 字面已无残留（grep）。

**完成后最后一行回复：** FIX_OK 错误数=0，已删 OPEN-1(EAA0801 冗余护栏)+补 OPEN-2(receiver 前缀漏报诚实注释)。

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