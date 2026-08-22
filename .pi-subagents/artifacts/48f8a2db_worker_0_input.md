# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：实现 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer/` 的 **L3 Roslyn Analyzer 骨架**（迭代09），构建绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Analyzer; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`
注意：**不要**引用 Godot.NET.Sdk（未装）。L3 是 Roslyn DiagnosticAnalyzer，依赖 `Microsoft.CodeAnalysis.CSharp` + `Cosmos.EffectAlgebra`（L1）。

**先 read：** `Analyzer/*.csproj`、`Analyzer/Class1.cs`（删除）、`src/Cosmos.EffectAlgebra/Objects.cs`(Kind/Mode/Claim/Compatible/Signature)、`ApiMapping.cs`(白名单)。

**落地内容（新文件 `EffectAlgebraAnalyzer.cs`）：** 一个 `DiagnosticAnalyzer`，对标注了 `[EffectOverride]`/`[AcceptDeviation]` 或调用 §7 白名单 API 的代码做**静态检查**（编译期，不重算运行期 net）。骨架期聚焦可静态判定的根因报警（这些由 §7 + L1 类型可机械判定，不需运行期数据）：
1. **DO-7 量纲混算（§8.2）**：静态检测「同一 EffectOverride 的 reason 为空」——但 reason 由 L1 构造子强制非空，故此项在 L3 层面为「属性存在性提示」即可，重点放**可静态判定的真实缺陷**（见下）。
2. **[EffectOverride] 覆盖 kind 的静态拒绝（§8.3.1）**：L1 类型层已禁止 OverrideKind，故 L3 只需确认「用户未在属性里写 kind 覆盖」——可静态解析 `EffectOverride` 命名参数，若无 `kind`/`OverrideKind` 参数即合规（类型层已保证）。此项为信息性。
3. **真实可静态判定项（重点）**：当检测到方法体内**只调用了 §7 白名单中的 create/occupy（如 `new`/Instantiate/AddChild/Connect）但无对应 release-class（§8.1）调用**（queue_free/free/remove_child/disconnect/...）且该方法**未标 `[EffectOverride]`**，则报 **DO-9 疑似泄漏** 诊断（`DiagnosticSeverity.Warning`），指向方法声明。这是 §3.3.1 DO-9 + §8.1 release-class 的可静态判定近似（控制流近似，注释明言「近似、运行期 net 为权威」）。
4. **DiagnosticDescriptor 注册**：`SupportedDiagnostics` 含至少 `MissingReleaseForAcquire`（DO-9 近似）、`EffectOverrideKindForbidden`（§8.3.1 信息性）。每个 descriptor 带 §x.y 出处 id（如 EAA0901/ EAA0801）。
5. `Initialize` 用 `analysisContext.RegisterSyntaxNodeActionsInCodeBlock` 或 `RegisterSymbolAction` 遍历 `MethodDeclarationSyntax`/调用表达式，按 §7 白名单 API 名（来自 `GodotApiWhitelist.All` 的 `GodotApi` 字符串）匹配调用名。
6. 注释：每条检查带 §x.y 出处 + 「控制流近似、运行期 Σnet 为权威」不变式 + L3 角色（「静态根因报警，数学在 L1」）。

**.csproj（write 重写）：** 同 Generator：`EnforceExtendedAnalyzerRules`/`IncludeBuildOutput=false`/`SuppressDependenciesWhenPacking` + Roslyn 引用 + `ProjectReference` L1；删 Class1.cs。

**约束（用户铁律）：** L3 只发根因诊断，不重算代数；类型边界由 L1 强制；控制流近似用注释明言（不假绿）；诊断 id 引 §x.y。

**验证（必须）：** 删 Class1.cs、写 EffectAlgebraAnalyzer.cs、改 .csproj 后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** BUILD_OK 错误数=0，已写 EffectAlgebraAnalyzer.cs（DiagnosticAnalyzer 骨架：DO-9 近似泄漏诊断 + §8.3.1 kind 信息性）+ 删 Class1.cs。

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