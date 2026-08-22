# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：实现 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator/` 的 **L2 Source Generator 骨架**（迭代08），构建绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`
注意：此机 VS 的 nuget restore 坏，但 SDK `dotnet build` 可用；**不要**引用 Godot.NET.Sdk（未装、会还原失败）。L2 是 Roslyn source generator，仅依赖 `Microsoft.CodeAnalysis.CSharp` + 引用 `Cosmos.EffectAlgebra`（L1 纯代数）。

**先 read 现有文件：** `src/Cosmos.EffectAlgebra.Generator/*.csproj`、`src/Cosmos.EffectAlgebra.Generator/Class1.cs`（删除它）。并 read `src/Cosmos.EffectAlgebra/ApiMapping.cs`（§7 白名单数据，供生成器映射）。

**落地内容（新文件 `EffectAlgebraGenerator.cs`）：**
实现 `IIncrementalGenerator`，扫描用户代码，把标记了 `[EffectOverride(...)]` / `[AcceptDeviation(...)]` 的方法，对照 `GodotApiWhitelist.All`（§7 数据），生成一份「效应代数校验注册表」源（partial class `EffectAlgebraGenerated` + 静态方法返回该方法的 Claim 集）。骨架版聚焦：
1. **识别标注**：`context.SyntaxProvider.Create` 收集带 `EffectOverrideAttribute`/`AcceptDeviationAttribute` 的 `MethodDeclarationSyntax`（用 `Attribute.IsOrHasName` 过滤）；只处理带这些特性的方法。
2. **生成代码**：对每个标注方法，用 `builder.AddSource($"{methodName}.g.cs", source)` 生成一个 `partial class EffectAlgebraGenerated { public static ImmutableArray<Claim> GetClaimsFor_{methodName}() => ImmutableArray.Create(/* 从 GodotApiWhitelist.All 找该方法名对应的 ApiMapping.Claims */); }`——**简化**：因生成器在编译期拿不到运行时 §7 数据，改用「生成器在编译期从 §7 白名单常量重新编码」不现实；改为**生成调用桩**：生成 `partial class EffectAlgebraGenerated { public static Signature Compute{methodName}(Signature baseSig) => /* TODO 注释：L2 在编译期注入 baseSig + 该方法对应 ApiMapping.Claims 的 Union */ baseSig; }` 并带 §x.y 注释与「L2 残差：运行期数据未在编译期可用」注释（honest，不是假绿）。
3. **诊断（Diagnostics）**：当 `EffectOverride` 的 reason 为空时（属性构造子无参数，reason 必填，故编译期已强制，这里补一条 `DiagnosticDescriptor` 说明「reason 非空由构造子保证」注释即可，不必真发）。
4. **Generator 元数据**：`[Generator]` 特性、类 `public sealed class EffectAlgebraGenerator : IIncrementalGenerator`。
5. 注释：每条带 §x.y 出处 + L2 角色（「从 Godot 调用语法 → L1 代数 Claim 的翻译层」）。

**.csproj 改造（write 重写）：** `OutputItemType=Analyzer` 不必要；设为 `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>` + `<IncludeBuildOutput>false</IncludeBuildOutput>` + `<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>`；引用 `PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />`；`ProjectReference Include="..\Cosmos.EffectAlgebra\Cosmos.EffectAlgebra.csproj" />`。删除 `Class1.cs`。

**约束（用户铁律）：** 生成器是「翻译层」，数学在 L1；类型边界由 L1 强制；生成器只做语法→Claim 的诚实映射，不重算代数（残差用注释明言）。

**验证（必须）：** 删 Class1.cs、写 EffectAlgebraGenerator.cs、改 .csproj 后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** BUILD_OK 错误数=0，已写 EffectAlgebraGenerator.cs（IIncrementalGenerator 骨架）+ 删 Class1.cs + .csproj 改依赖。

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