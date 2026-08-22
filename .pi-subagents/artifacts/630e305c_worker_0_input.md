# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§3.1.5b Interval 算术完整性锁**（迭代27），`dotnet test` 绿——覆盖 Merge 全 ⊤ 组合 + Exact/Dynamic/Default 边界不变量，呼应 §3.1.5a/§3.1.5b。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Numeric.cs`(`Interval`：`Exact`/`Dynamic`/`Default` 工厂 + 构造子 + `Merge` + `Lo`/`Hi` + `IsTop`)。

**任务（新文件 `IntervalArithmeticTests.cs`，带 §3.1.5a/§3.1.5b 注释）：**
1. **Exact/Dynamic/Default 边界（§3.1.5）**：
   - `Interval.Exact(5)` ⇒ `[5,5]`（单值）。
   - `Interval.Dynamic` ⇒ `[1,⊤]`（动态上界未知）。
   - `Interval.Default` ⇒ `[1,1]`（缺省保守）。
   - `Interval.Exact(0)` ⇒ `[0,0]`（不崩、非负允许 0）。
2. **Merge 全 ⊤ 组合（§3.1.5b join-semilattice，max/min 内嵌 ⊤ 律）**：穷举合法区间对并断言 Merge 结果：
   - `[1,3].Merge([2,5]) == [1,5]`（常规 join，min/max）。
   - `[1,⊤].Merge([2,⊤]) == [1,⊤]`（⊤ 律：max 吸收 ⊤）。
   - `[1,3].Merge([1,⊤]) == [1,⊤]`。
   - `[⊤,⊤].Merge([1,5]) == [⊤,⊤]`（⊤ 吸收，构造子 [⊤,⊤] 合法）。
   - 幂等 `[a,a].Merge([a,a])==[a,a]`；交换 `[a,b].Merge([c,d])==[c,d].Merge([a,b])`。
3. **Merge 非法构造（§3.1.5a）**：`new Interval(NatStar.Top, NatStar.Of(5))`（lo=⊤ 且 hi 有限）应 `throw ArgumentException`（类型层拒绝「上界未知却有限下界」）。
4. **Lo/Hi 不变量（§3.1.5）**：合法区间恒 `Lo <= Hi`（有限时）；`IsTop` 当 Lo 或 Hi 为 ⊤。
5. **随机互补（复用 Random(27)）**：随机生成合法区间（form0 [x,⊤]/form1 [⊤,⊤]/form2 [x,y]，x<=y 且 x,y 有限），断言 Merge 幂等+交换+`Lo<=Hi` 保持，1000 组。
- 注释：Merge 是 join-semilattice（min(Lo)/max(Hi)），⊤ 经 NatStar 内嵌律吸收；构造子 lo=⊤ 非法由类型/构造子强制（§3.1.5a）。
- 若 `Interval` 工厂名/构造子签名与假设不符（先 read 确认），按真实 API 写；若 `Dynamic`/`Default` 语义实现不同，按真实断言（不假绿），但 `[1,⊤]`/`[1,1]` 是 §3.1.5 既定语义，若实现偏离用 write 修 Numeric.cs，保持构建绿。

**约束（用户铁律）：** 断言可证伪（Merge ⊤ 律错必红）；不引魔法数；注释引 §3.1.5a/§3.1.5b。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 IntervalArithmeticTests.cs（Exact/Dynamic/Default + Merge 全 ⊤ 组合 + 非法构造 + 随机 1000 组，§3.1.5a/§3.1.5b）。

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