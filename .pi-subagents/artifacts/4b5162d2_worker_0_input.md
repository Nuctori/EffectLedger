# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：实现 **§3.2.3 Compatible 全表穷举锁**（迭代19），`dotnet test` 绿——把 PDR §3.2.3 的 16 对兼容矩阵逐字编码为期望表，断言 `Compatible.IsCompatible` 全 25 组合与表一致，零漂移。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra/Objects.cs`(`Mode` enum：Use/Create/Release/Move/Unknown)、`Algebra.cs` 或 `Objects.cs` 中的 `Compatible` 静态方法（§3.2.3：全函数 + 对称）。定位 `IsCompatible(Mode,Mode):bool`。

**任务（新文件 `CompatibleMatrixTests.cs`）：** 按 PDR §3.2.3 的兼容矩阵逐字编码期望表（带 §3.2.3 注释），逐项断言：
PDR §3.2.3 兼容矩阵（mode 对 a,b）：
- **CONFLICT（false）**：(Create,Create)、(Move,Move)、(Release,Release) —— 同类自冲突（同资源/同类不可并发）。
- **良性配对（true）**：(Create,Release)、(Release,Create)、(Create,Move)、(Move,Create)、(Release,Move)、(Move,Release) —— 生命周期互补。
- **use 放行（true）**：(Use,Use)/(Use,Create)/(Use,Release)/(Use,Move)/(Create,Use)/(Release,Use)/(Move,Use) —— use 与任何兼容（只读不冲突）。
- **Unknown 视为 Use（true）**：(Unknown,*) 与 (*,Unknown) 同 (Use,*)（P4，运行期具体化前 Unknown 按 Use 处理）。
- 其余已覆盖（全 25 组合 = 5×5）。

编码方式（数据驱动，可证伪）：
```
// §3.2.3 兼容矩阵：false = CONFLICT，true = 兼容
static readonly (Mode a, Mode b, bool expected)[] Matrix =
{
  (Create,Create,false),(Move,Move,false),(Release,Release,false),
  (Create,Release,true),(Release,Create,true),(Create,Move,true),(Move,Create,true),(Release,Move,true),(Move,Release,true),
  (Use,Use,true),(Use,Create,true),(Use,Release,true),(Use,Move,true),(Create,Use,true),(Release,Use,true),(Move,Use,true),
  (Unknown,Use,true),(Unknown,Create,true),(Unknown,Release,true),(Unknown,Move,true),(Use,Unknown,true),(Create,Unknown,true),(Release,Unknown,true),(Move,Unknown,true),(Unknown,Unknown,true),
};
[Theory][MemberData(nameof(Matrix))]  // 或 [Fact] 遍历
void Compatible_Matrix_AllPairs(Mode a, Mode b, bool expected) =>
  Assert.Equal(expected, Compatible.IsCompatible(a,b)); // 方法名按实际定位
```
- 若 `Compatible` 是静态类 `Compatible.IsCompatible`；若是 `SignatureExtensions` 或其他名，按实际写（先 read 确认）。
- 额外断言 **对称性**：`IsCompatible(a,b)==IsCompatible(b,a)` 对全 25 组合（矩阵已含双向，但补一条显式对称断言）。
- 额外断言 **全函数**：25 组合均不抛（已隐含）。

**约束（用户铁律）：** 期望表逐字引 §3.2.3；断言可证伪（实现偏离矩阵必红）；无魔法数（Mode 枚举）。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若 `IsCompatible` 实际返回与 §3.2.3 表不符（实现 bug），用 write 修源代码 Compatible 逻辑，保持构建绿。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 CompatibleMatrixTests.cs（§3.2.3 全 25 组合期望表穷举锁 + 对称）。

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