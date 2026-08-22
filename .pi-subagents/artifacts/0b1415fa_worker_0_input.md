# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：为 L3 Analyzer 补 §14.3 A3(KIND_MIX) 与 A4(Compat 冲突) 两条诊断（迭代29 OPEN-1/OPEN-2），`dotnet test` 绿。数学已由 L1 类型保护，但 §14.3 将其列为 L3 完备性判据，需显式落地或标注豁免（用户铁律：不留静默缺口）。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly` 与 `cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`（现有 EAA0901 DO-9 近似 + SupportedDiagnostics 结构）、`src/Cosmos.EffectAlgebra/ApiMapping.cs`（`GodotApiWhitelist.All`：每条 `ApiMapping{GodotApi, Claims: Claim[]}`，`Claim` 含 `Kind`(Read/Write/Occupy)/`Resource`(ResourceId)/`Mode`(Use/Create/Release/Move/Unknown)）、`Objects.cs`(`ResourceId.Normalize`/`Compatible.IsCompatible`/`Mode`/`Kind`)、`Algebra.cs`(`Compatible`)。

**任务（用 write 整体重写 EffectAlgebraAnalyzer.cs，保持现有 EAA0901 不变）：** 新增两条诊断，从方法体调用映射回 §7 白名单 Claims，按归一资源分桶检测：
1. **A3 KIND_MIX（EAA0303，§14.3 A3 / §3.1.4b DO-7）**：若一个方法体内对同一**归一资源**出现了**跨 kind** 的调用（如既 read 又 write、或 write 又 occupy），且未标 `[EffectOverride]`，报 `Warning`「同资源混用多类效应（read/write/occupy），量纲隔离由 L1 运行期保证，但建议显式 [EffectOverride] 标注意图」。实现：对某方法，收集其调用对应的 Claim，按 `ResourceId.Normalize(c.Resource)` 分组，若任一组的 `Kind` 集合大小 >1 且方法无 `[EffectOverride]` ⇒ 报。注释明言「L1 已结构性隔离三桶（NetTable 仅 occupy），此诊断仅提示意图清晰度，非数学缺」。
2. **A4 Compat 冲突（EAA0304，§14.3 A4 / §3.2.3）**：若一个方法体内对同一归一资源出现**两 claim 的 mode 对 Compatible==false**（CONFLICT 集：(Create,Create)/(Move,Move)/(Release,Release)），且未标 `[EffectOverride]`，报 `Warning`「同资源并发冲突模式（如重复 Create 无配对 Release），运行期可能泄漏/竞态」。实现：按归一资源分组，枚举组内 mode 对，若 `!Compatible.IsCompatible(a.Mode, b.Mode)` 且方法无 `[EffectOverride]` ⇒ 报。注释明言「冲突语义由 L1 Compatible 全函数定义（§3.2.3）」。
- 两条 descriptor 加入 `SupportedDiagnostics`（EAA0303/EAA0304），各带 § 出处 id。
- 在 `Initialize` 的分析 action 内复用现有遍历方法体的调用提取，扩展为「按归一资源聚合 (Kind,Mode)」。
- 注释：控制流近似（同 iter09 OPEN-2 诚实声明 receiver-前缀漏报）；运行期权威在 L1。
- 不动 EAA0901 逻辑。

**测试（改 ToolingTests.cs 或新增 `AnalyzerCompletenessTests.cs`）：** 各加一条：
- A3：源方法同时 `GetNode(...)`（read tree）+ `AddChild(...)`（occupy tree）同资源 + 无 `[EffectOverride]` ⇒ ≥1 个 EAA0303。
- A4：源方法两次 `AddChild(...)`（create×2 同资源）无 release 无 `[EffectOverride]` ⇒ ≥1 个 EAA0304（也触发 EAA0901，但 A4 单独断言 EAA0304 存在）。
- A3/A4 豁免：方法标 `[EffectOverride("...")]` 则不报对应诊断。
- 注释 §14.3 A3/A4。

**约束（用户铁律）：** 诊断真由 §7/§3.2.3/§3.1.4b 数据驱动；数学已由 L1 保护，注释诚实；残差透明；每条带 § 出处。

**验证（必须）：** `dotnet build Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly` 0 错误 0 警告；`dotnet test` 0 失败（绿）。

**完成后最后一行回复：** BUILD_OK 测试=0 失败，已补 A3(KIND_MIX, EAA0303) + A4(Compat 冲突, EAA0304) 两条 L3 诊断 + 测试。

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