# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§8.3 属性构造子边界锁**（迭代22），`dotnet test` 绿——证明 reason 非空 / epsilon∈[0,0.5] 由构造子真强制（类型层边界，呼应用户铁律「类型系统约束不了的用注释，约束了的也要测」）。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/EffectAttributes.cs`(`EffectOverrideAttribute` 构造子 reason 非空抛、`AcceptDeviationAttribute` 构造子 epsilon∈[0,0.5] 抛)。

**任务（新文件 `AttributeBoundaryTests.cs`，带 §8.3.1/§8.3.2 注释）：**
1. **EffectOverride reason 非空（§8.3.1）**：
   - `Assert.Throws<ArgumentException>(() => new EffectOverrideAttribute(""));`（空串）
   - `Assert.Throws<ArgumentException>(() => new EffectOverrideAttribute(null));`（null）
   - `Assert.Throws<ArgumentException>(() => new EffectOverrideAttribute("   "));`（空白）
   - `new EffectOverrideAttribute("真理由")`（合法，不抛）+ 断言 `.Reason == "真理由"`。
2. **AcceptDeviation epsilon 上界（§8.3.2）**：
   - `Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptDeviationAttribute(0.6));`（超上界）
   - `Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptDeviationAttribute(-0.1));`（超下界）
   - `Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptDeviationAttribute(1.0));`（=1 超 0.5）
   - `new AcceptDeviationAttribute(0.0)` / `new AcceptDeviationAttribute(0.5)` / `new AcceptDeviationAttribute(0.2)`（合法边界+内点）+ 断言 `.Epsilon` 值正确。
3. **kind 不可覆盖（§8.3.1，类型层）**：`typeof(EffectOverrideAttribute)` 反射断言**无** `OverrideKind` 属性/字段（确认类型层禁止覆盖 kind：`GetProperty("OverrideKind")` 返回 null）。这是「类型即约束」的直接证据。
4. **注释明言**：每项测试注释「边界由 L1 构造子强制，非运行期 if 漏判」。

**约束（用户铁律）：** 断言可证伪（若实现去掉抛异常则测试必红）；不引魔法数；注释引 §8.3.1/§8.3.2。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若构造子实际未抛（实现 bug），用 write 修 EffectAttributes.cs 加抛异常，保持构建绿。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 AttributeBoundaryTests.cs（reason 非空 3 例 + epsilon 上界 3 例 + kind 不可覆盖反射 1 例，全部 §8.3 出处）。

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