# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：在 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 落地 **§8.3 [EffectOverride]/[AcceptDeviation] 属性**（迭代06），构建绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**必读 PDR（先 read）：** `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §8.3（搜 `### 8.3`）。重点：
- §8.3.1 `[EffectOverride(target: Claim | resource, reason: string, scope?: ScopeId)]`：仅可覆盖单条 Claim 的 mode/size/scope 三类属性；**禁止覆盖 kind**（read/write/occupy 不可经 override 互转）；禁止压制 DO-9 根因（override 必须给真实 release 路径证明）；禁止静默豁免 DO-7 量纲混算。reason 必须非空，CI 需人工 approve。
- §8.3.2 `[AcceptDeviation(epsilon: double)]`：`epsilon ∈ [0.0, 0.5]`，超出 ⇒ 编译错误（上界约束）；仅放宽运行时 Deviation>阈值 的局部报警，不豁免编译期 DO 报警；作用域标注对象 scope。

**落地内容（新文件 `EffectAttributes.cs`）：**
1. `sealed class EffectOverrideAttribute : Attribute`：
   - 构造子 `EffectOverrideAttribute(string reason)`（reason 非空强制——构造子内 `if(string.IsNullOrWhiteSpace(reason)) throw`）。
   - 属性 `public ScopeId? Scope { get; set; }`（可选 scope 覆盖）。
   - 属性 `public Mode? OverrideMode { get; set; }`（覆盖 mode，仅 Use/Create/Release/Move，不含 Unknown）。
   - 属性 `public double? OverrideSize { get; set; }`（覆盖 size 数值；或 `Interval?` 更贴合，用 `double?` 简化 + 注释 §3.1.5）。
   - **kind 禁止覆盖**：不提供 OverrideKind 属性（类型层即禁止，注释明言 §8.3.1 禁覆盖 kind）。
   - 注释：可覆盖域受限、reason 非空强制、CI 人工 approve 注释（不含审批逻辑，仅注释约定）、DO-9 根因不压制/DO-7 不豁免的不变式。
2. `sealed class AcceptDeviationAttribute : Attribute`：
   - 构造子 `AcceptDeviationAttribute(double epsilon)`：`if (epsilon < 0.0 || epsilon > 0.5) throw new ArgumentOutOfRangeException(nameof(epsilon), "§8.3.2: epsilon ∈ [0,0.5]");`（上界约束，编译期/构造期即拦）。
   - 属性 `public double Epsilon { get; }`。
   - 注释：仅放宽运行时报警、不豁免编译期 DO、作用域 scope（注释）。

**约束（用户铁律）：** 类型字段即边界；kind 不可覆盖靠「无该属性」类型强制；epsilon 上界靠构造子抛异常强制；reason 非空靠构造子抛异常强制。每条带 §8.3.1/§8.3.2 出处 + 一句不变式。

**验证（必须）：** 写 `EffectAttributes.cs` 后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** BUILD_OK 错误数=0，已写 EffectAttributes.cs（EffectOverrideAttribute + AcceptDeviationAttribute，reason/epsilon 边界靠构造子强制，kind 不可覆盖靠类型）。

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