# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§3.1.4a ResourceId 归一等价类全锁**（迭代25），`dotnet test` 绿——覆盖 §3.1.4a 全部缩写→规范形映射的等价性（DO-8 单点真相完整性）。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Objects.cs`(`ResourceId` 判别联合 + `Normalize` 静态方法 + 各构造子 `Tree`/`Self`/`SignalBus`/`Signal`/`CommandBuffer`/`Gpu`/`Memory`/`Occupancy`/`Callback`/`AudioMixer`/`Input`/`Network`)。确认 `Normalize(ResourceId)` 的返回与各构造子归一规则（如 `Self("signal_x")`⇒`SignalBus("x")`、`Signal("signal_x")`⇒`SignalBus("x")`、`CommandBuffer("gpu")` 与 `Gpu(...)` 关系等）。

**任务（新文件 `ResourceNormalizationTests.cs`，带 §3.1.4a 注释）：** 逐条锁定 §3.1.4a 等价类（用 `Assert.Equal(Normalize(a), Normalize(b))`）：
1. `Self("signal_x") == SignalBus("x")`（ST-02/iter13 收口）
2. `Signal("signal_x") == SignalBus("x")`（signal_ 前缀归一）
3. `Self("x") == SignalBus("x")`（ST-03：`Self` 裸名当 signal）
4. `SignalBus("signal_x") == SignalBus("x")`（SignalBus 内部也剥 signal_ 前缀，自洽）
5. `Gpu(new Rid("mesh"))` 与 `CommandBuffer("mesh")` 关系：按 §3.1.4a，gpu/command_buffer 是否归一为同一规范形？若实现中 `Gpu` 与 `CommandBuffer` 是**不同**构造子（不等价），则改为断言「二者各自 Normalize 幂等」而非相等。先 read 实现确认，按真实语义写（不要硬套假设）。
6. `Memory("mem")` 幂等：`Normalize(Memory("mem")) == Memory("mem")`。
7. `Occupancy("audio") == Occupancy("audio")`（audio_channel 裸名）。
8. `Callback("cb") == Callback("cb")`。
9. `AudioMixer("1") == AudioMixer("1")`。
10. `Input("action") == Input("action")`。
11. `Network(0,"m") == Network(0,"m")`。
12. **幂等性全集**：对以上每个样本 `r`，`Normalize(Normalize(r)) == Normalize(r)`（一次性循环断言 12+ 样本）。
13. **跨类不等价**：`Tree("x") != SignalBus("x")`、`Memory("m") != Gpu(...)` 等（断言不同类归一后互不相等，证明归一是**规范形**而非全塌缩）。
- 用 `[Theory]`/`[MemberData]` 或 `[Fact]` 多组；注释引 §3.1.4a + 各 ST 收口号（如有）。
- 若某等价（如 Gpu vs CommandBuffer）实现未归一，按真实语义断言（幂等/不等价），避免假绿；注释明言「§3.1.4a 中 gpu/command_buffer 在本实现为独立构造子，未强制同形（残差：运行时别名由 §7 白名单维度处理）」。

**约束（用户铁律）：** 断言可证伪（实现破归一必红）；不引魔法数；注释引 §3.1.4a（DO-8）。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若 `Normalize` 实际未落实某等价（实现 bug），用 write 修 Objects.cs 的 `Normalize`，保持构建绿。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 ResourceNormalizationTests.cs（§3.1.4a 全部等价类 + 幂等 + 跨类不等价，DO-8 单点真相完整锁）。

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