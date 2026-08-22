# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际调用 write 工具修文件，不得只计划。任务：修复 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 下 L1 代码库 3 个与 PDR 不符的点（来自独立审计 iter-code01.md），然后 `dotnet build` 绿。

环境（必读）：构建须在 PowerShell 执行且先 `$env:MSBUILD_EXE_PATH = $null`：
  cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly

**三处修复（严格对应 PDR，用 write 整体重写涉及的文件，避免增量漂移）：**

1. **OPEN-3 / Numeric.cs `Interval` 构造子**：当前 `lo≤hi` 校验仅双侧有限时执行，允许 `[⊤,x]`。PDR §3.1.5 要求下界有限、上界可 ⊤。改为：构造子内加 `if (lo.IsTop) throw new ArgumentException("Interval lower bound must be finite; lo=⊤ invalid (§3.1.5)");`（上界 Hi 仍可 IsTop，合法）。保持 `Dynamic=[1,⊤]` 仍合法（Lo=Of(1) 有限）。

2. **OPEN-1 / Algebra.cs `NetTable.Compute`**：当前遍历 `sig.AllClaims()` 全量，未过滤 kind。PDR §3.3.1 定义 `net` 仅 `c.kind == Occupy` 的 create/move 与 release 抵消（read/write 不进 net 守恒）。在 foreach 内 `sig.AllClaims()` 循环体首行加 `if (c.Kind != Kind.Occupy) continue;`。同步更新 `Compute` 与 `IsConserved` 方法上方注释，明言「net 仅含 occupy 桶（§3.3.1）」。

3. **OPEN-2 / Algebra.cs `NetTable.IsConserved`**：当前 `v.Lo.IsTop || v.Hi.IsTop` 时 `loLeZero`/`hiGeZero` 均 true ⇒ 返回 true（静默宣称守恒），但 PDR §3.3.1 DO-9 fail-closed 要求 ⊤ 视为「需人工界定」⇒ 不守恒。改为：`IsConserved` 开头加 `if (v.Lo.IsTop || v.Hi.IsTop) return false;`（未知 ⇒ 不守恒 ⇒ 触发人工确认/DO-9 报警），保留后续 `loLeZero && hiGeZero` 判定有限情形。注释保留 fail-closed 说明。

**验证（必须）：** 用 write 重写 Numeric.cs（Interval 构造子段）与 Algebra.cs（NetTable.Compute + IsConserved 段）后，运行 build 命令确认 0 错误 0 警告。注意：`Interval` 构造子若加 `if (lo.IsTop) throw`，要确保 `Dynamic`/`Exact`/`Default` 静态初始化仍合法（它们 Lo 均有限），且任何其他构造 `[⊤,...]` 处不存在。

**完成后最后一行回复：** FIX_OK 错误数=0，已修 OPEN-1/OPEN-2/OPEN-3。

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