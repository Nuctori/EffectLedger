# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：在 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 落地 **§9.1 开发模式验证 / Deviation 公式**（迭代04），构建绿。

环境（必读）：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**必读 PDR（先 read 再写）：** `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §9.1（搜 `### 9.1 开发模式验证`）与 §3.1.5c（搜 `定义 3.1.5c`）。重点：
- §9.1 原代码：`Deviation = Σᵢ |actualᵢ - expectedᵢ_mid| / max(expectedᵢ_range, ε)`；其中 `expectedᵢ_mid=(lo+hi)/2`，`expectedᵢ_range=hi-lo`，`ε=1`（分母下界，单值区间 range=0 ⇒ 用 ε 避免除零）；任一端为 ⊤ ⇒ 该项 Deviation 计为 ⊤ ⇒ 整体 ⊤（不可校准，跳过）。
- §3.1.5c：`DeviationVal := double ∪ {⊤}`；返回 ⊤ 时调用方视为需人工界定，不触发普通 0.2f 报警。
- §3.1.5a：⊤ 表示上界未知。

**落地内容（新文件 `Deviation.cs`）：**
1. `static class SignatureDeviation`：
   - `public static DeviationVal Calculate(NetTable expected, NetTable actual)` 或 `Calculate(Signature expected, Signature actual)`：按资源对齐比较两个签名。
   - 实现：遍历 expected 与 actual 的全部归一化资源（两集合并的键），对每个资源 i：
     - `expected_mid = (expected[i].Lo + expected[i].Hi)/2`（当两者均有限；若任一 IsTop ⇒ 该项 ⊤）；
     - `expected_range = expected[i].Hi - expected[i].Lo`（有限；IsTop ⇒ ⊤）；
     - `actual_mid = (actual[i].Lo + actual[i].Hi)/2`（IsTop ⇒ 该项 ⊤）；
     - 若 expected 或 actual 的 Lo/Hi 任一边界 IsTop ⇒ 该项返回 ⊤（整体 ⊤）；
     - 否则 `term = |actual_mid - expected_mid| / max(expected_range, 1.0)`（ε=1 下界）。
     - 累加 term；若任一 ⊤ 项 ⇒ 整体返回 `DeviationVal.Top`，否则 `DeviationVal.Of(Σterms)`。
   - 辅助：`private static bool TryMid(Interval x, out double mid, out double range)` —— 当 x.Lo/Hi 均有限时算 mid/range 返回 true，否则 false（调用方据此判 ⊤）。
   - `public static bool ExceedsThreshold(DeviationVal d, double threshold) => d.ExceedsThreshold(threshold);`（复用 DeviationVal.ExceedsThreshold）。
2. 注释：每个方法带 §9.1 / §3.1.5c / §3.1.5a 出处；明言「分母下界 ε=1 防除零（iter33 收口）」「任一 ⊤ ⇒ 整体 ⊤（不 NaN 不 ∞，MA-002）」。

**约束（用户铁律）：** 类型字段即边界；⊤ 不崩溃靠 `DeviationVal`/`NatStar` 类型强制，不靠运行时 if 漏判；归一化资源对齐靠 `ResourceId.Normalize`。

**验证（必须）：** 写 `Deviation.cs` 后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。若 `NetTable` 公开访问不够（如需要 `NetTable` 公开 `Keys` 或按资源取 Interval 的方法），可在 `NetTable` 加 `public IEnumerable<ResourceId> Resources` 与 `public Interval Get(ResourceId)` —— 用 `write` 重写 Algebra.cs 相应段（不要增量 edit 漂移），保持其他逻辑不变，构建仍绿。

**完成后最后一行回复：** BUILD_OK 错误数=0，已写 Deviation.cs（Calculate + TryMid + ExceedsThreshold）。

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