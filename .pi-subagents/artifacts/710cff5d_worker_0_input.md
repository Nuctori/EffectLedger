# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§3.1.3b ScopeId ⊆* 偏序完整性锁**（迭代23），`dotnet test` 绿——覆盖全部 7 种标签构造子（Method/Type/Scene/Global/Loop/Conditional/Async/Shell）的偏序关系，证明 ⊆* 是良构偏序且 Global 为唯一最大元。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Objects.cs`(`ScopeId` 判别联合：Method/Type/Scene/Global/Loop/Conditional/Async/Shell 构造子 + `IncludedIn` 实现 + `Equals`)。

**任务（新文件 `ScopeOrderTests.cs`，带 §3.1.3b 注释）：** 用 `Random(23)` 随机生成全部标签实例（同名/异名），断言偏序三定律 + Global 最大元 + 跨标签不可比：
1. **自反**：任意 s，`s.IncludedIn(s)` == true（7 标签 × N 随机名）。
2. **反对称**：若 `a.IncludedIn(b) && b.IncludedIn(a)` 则 `a.Equals(b)`（随机对；跨标签/异名必不满足 ⇒ 验证不等于）。
3. **传递**：随机 a,b,c，若 `a.IncludedIn(b) && b.IncludedIn(c)` 则 `a.IncludedIn(c)`（用 a=b 自反 + b⊆Global 最大元构造真链）。
4. **Global 最大元**：任意 s（7 标签），`s.IncludedIn(Global())` == true；且 `Global().IncludedIn(s)` == false 当 s 非 Global（唯一最大元）。
5. **跨标签不可比**：任意异标签对（如 Method("m") vs Scene("s")、Type("t") vs Loop("l")、Async("a") vs Shell()、Conditional("c") vs Global() 反向等），两者互不包含（`!a.IncludedIn(b) && !b.IncludedIn(a)`）；同标签异名亦然（Method("m1") vs Method("m2")）。逐对枚举 7 标签交叉 + 异名。
6. **Shell 标签**：`Shell()` 与任意非 Shell 标签不可比；自身自反。
- 断言用 `[Theory]`/`[Fact]` 多组；随机 500 组 + 显式交叉枚举（7×7 标签对全覆盖）。
- 注释明言「⊆* 经 Equals + Global 最大元实现，非 NamedScopes 多标签（iter45 收口）」。

**约束（用户铁律）：** 断言可证伪（若 IncludedIn 退化全 true/false 必红）；不引魔法数；覆盖全部 7 标签；注释引 §3.1.3b。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若 `IncludedIn` 实现与偏序定律冲突（实现 bug，如漏 Global 最大元或跨标签误包含），用 write 修 Objects.cs 的 `IncludedIn`，保持构建绿。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 ScopeOrderTests.cs（自反/反对称/传递/Global 最大元/跨标签不可比 + 7 标签全覆盖，§3.1.3b）。

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