# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：完善 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 的 **§3.3 派生度量**（迭代05），构建绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**必读 PDR（先 read）：** `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.2.5（L293 循环组合 ω∈ℕ∪{⊤}、S×ω 算子、Peak cardinality 形式已废弃标注）+ §3.3（L286 起 net(S,scope)/Peak/peak 统一）。已知现有 `Algebra.cs` 已有 `NetTable.Compute`(§3.3.1)、`NetTable.IsConserved`(§3.3.1 DO-9)、`Peak.Compute`(§3.3.2 size 求和 + ⊤)。

**落地内容（新文件 `DerivedMetrics.cs`，或扩展现有，用 write 保证不漂移）：**
1. `readonly record struct LoopCount`：`Count`(NatStar)，构造子 `Of(ulong)` / `Top()`；表示 §3.2.5 `ω∈ℕ∪{⊤}`。注释 §3.2.5。
2. `static class Combination`：
   - `public static Signature Loop(Signature body, LoopCount ω, ScopeId loopScope)`：实现 §3.2.5 `(S × ω) = Σ copy_i(S)`，i=1..ω。当 `ω.Count.IsTop` ⇒ 返回「上界开放重复副本集合」——实现对 body 的 Claim 做 ω=⊤ 标记（每个 Claim 的 size 上限拉到 ⊤，或复制 N 次副本；简单可行：把 body 中每个 Claim 的 Interval 上限设为 ⊤ 表示上界开放，scope 标注 loopScope）。注释 §3.2.5：ω=⊤ 时 Peak/net 以 ⊤ 兜底。
   - `public static Signature Sequence(Signature a, Signature b) => Signature.Union(a,b);`（§3.2.1）
   - `public static Signature Parallel(Signature a, Signature b) => Signature.Union(a,b);`（§3.2.2，并行组合已含 Compatible 检查由 Analyzer 补）
3. `static class Derived`：
   - `public static NatStar Peak(Signature s, ScopeId scope) => Peak.Compute(s, scope);`（§3.3.2 便利封装，size 求和，⊤⇒⊤）
   - `public static NetTable Net(Signature s, ScopeId scope) => NetTable.Compute(s, scope);`（§3.3.1 便利封装）
   - `public static bool IsConserved(Signature s, ResourceId r, ScopeId scope) => Net(s, scope).IsConserved(r);`（§3.3.1 DO-9 便利封装）

**约束（用户铁律）：** 类型字段即边界；ω=⊤ 经 `LoopCount.Count.IsTop` 类型强制；Peak size 求和（非 cardinality）与 §3.3.2 一致；旧 cardinality 形式已在 §3.2.5 注释标注废弃。每条带 §x.y 出处。

**验证（必须）：** 写 `DerivedMetrics.cs` 后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** BUILD_OK 错误数=0，已写 DerivedMetrics.cs（LoopCount + Combination + Derived 便利封装）。

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