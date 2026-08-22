# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：修复 `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/AlgebraLawsTests.cs` 中 ScopeId ⊆* 的测试（迭代02 OPEN-1），使测试**真**验证 PDR §3.1.3b 的传递性与反对称性，然后 `dotnet test` 绿。

环境（必读）：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**改动要求（用 write 整体重写 AlgebraLawsTests.cs，不要增量 edit 漂移；保持其他测试不变）：**
1. 把现有 `ScopeId_Transitive`（L120-125，现仅断言 `a.IncludedIn(Global)` 与 GlobalMaximal 重复）**改为真实传递性测试**：
   - 构造多组 (a,b,c) 满足 `a.IncludedIn(b) && b.IncludedIn(c)`，断言 `a.IncludedIn(c)`。
   - 样例组（全部应成立）：`(Method("m"), Method("m"), Global())`、`(Method("m"), Global(), Global())`、`(Scene("s"), Scene("s"), Global())`、`(Type("t"), Type("t"), Global())`。
   - 注释明确：§3.1.3b 传递性 a⊆b ∧ b⊆c ⇒ a⊆c。
   - 关键：要真有「两步」链，例如 `Method("m").IncludedIn(Global())` 是单步，不能算传递；必须用 `a.IncludedIn(b) && b.IncludedIn(c)` 三个不同变量组合（a⊆b 由 Equals 自反给、b⊆c 由 Global 最大元给，则 a⊆c）。可写为：
     ```
     var a = new ScopeId.Method("m");
     var b = a;            // a⊆b 自反
     var c = new ScopeId.Global();  // b⊆c（Global 最大元）
     Assert.True(a.IncludedIn(c));   // 传递：a⊆b∧b⊆c⇒a⊆c
     ```
     再加一组跨标签链验证（如 `Type("t")⊆Type("t")⊆Global()`）。
2. **新增 `ScopeId_Antisymmetric` 测试**：`a.IncludedIn(b) && b.IncludedIn(a) ⇒ a.Equals(b)`。样例：`Method("m")⊆Method("m")`（自反双向）⇒ Equals；`Method("m")` 与 `Scene("s")` 互不包含 ⇒ 不适用（但可断言 `!a.IncludedIn(b) || !b.IncludedIn(a)` 时不等于）；重点测 `a.IncludedIn(b)&&b.IncludedIn(a)` 为真时 `a.Equals(b)`。
   - 注释：§3.1.3b 反对称（⊑ 反对称 ⇒ ⊆* 偏序）。

**注意**：`ScopeId` 构造子在 `Cosmos.EffectAlgebra` 命名空间，测试需 `using Cosmos.EffectAlgebra;`。`Global()` 写法：`new ScopeId.Global()`（record 无字段）。当前测试文件已正确引用。

**验证（必须）：** 重写后 `dotnet test -clp:ErrorsOnly` 0 失败（绿）。测试总数应仍合理（原 28，补 1 条 antisymmetry 可能变 29，属正常）。

**完成后最后一行回复：** FIX_OK 失败数=0，已修 ScopeId_Transitive（真传递性）+ 新增 ScopeId_Antisymmetric。

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