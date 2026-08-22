# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并运行测试。任务：升级 **L2 Source Generator 从桩→真实生成每方法 Signature**（迭代20），`dotnet test` 绿——生成代码真调用 L1 `GodotApiWhitelist` 组合出标注方法的 `Signature`，可被运行期/Analyzer 消费。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra.Generator; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly` 与 `cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`（现有桩）、`src/Cosmos.EffectAlgebra/ApiMapping.cs`(`GodotApiWhitelist.All`：`ApiMapping{GodotApi, Claims: Claim[]}`)、`Objects.cs`(`Signature`：`Add(Claim)`/`Union`/`AllClaims`/`static`？)、`EffectAttributes.cs`(`EffectOverrideAttribute`/`AcceptDeviationAttribute` 构造子)。确认 `Signature` 如何构造（有 `new Signature()` + `Add`？还是 `ImmutableArray<Claim>` 字段 + `static Signature Union`？先 read 确认 API）。

**升级内容（重写 `EffectAlgebraGenerator.cs`）：** 让生成器对每个带 `[EffectOverride]` 或 `[AcceptDeviation]` 的方法，生成：
```
namespace <方法所在 namespace>  // 或全局
{
    [global::System.CodeDom.Compiler.GeneratedCode("Cosmos.EffectAlgebra.Generator", "1.0")]
    public static partial class EffectAlgebraGenerated
    {
        /// <summary>§14 L2：方法 <methodName> 的效应签名，由 §7 白名单 Claims 组合（L1 数学在运行期 Σnet 权威）。</summary>
        public static global::Cosmos.EffectAlgebra.Signature Compute_<methodName>(global::Cosmos.EffectAlgebra.Signature baseSig)
            => global::Cosmos.EffectAlgebra.Signature.Union(baseSig, <methodName>_Claims());
        private static global::Cosmos.EffectAlgebra.Signature <methodName>_Claims()
        {
            var s = new global::Cosmos.EffectAlgebra.Signature();
            // 从 GodotApiWhitelist.All 取该方法名对应的 ApiMapping.Claims 并入
            foreach (var m in global::Cosmos.EffectAlgebra.GodotApiWhitelist.All)
            {
                if (m.GodotApi == "<methodName>")   // 精确名匹配（规范化后）
                    foreach (var c in m.Claims) s = global::Cosmos.EffectAlgebra.Signature.Union(s, new global::Cosmos.EffectAlgebra.Signature(new[]{ c }));
            }
            return s;
        }
    }
}
```
- 若 `Signature` 无 `Add` 实例方法但有 `static Union(Signature,Signature)` 与 `new Signature(Claim[])`，按实际 API 写（先 read 确认；若需，给 `Signature` 加一个 `public Signature(params Claim[] claims)` 构造子或 `static Signature Of(params Claim[])`——用 write 改 Objects.cs 加该便捷构造子，保持构建绿）。
- 生成器必须**真引用 L1**（GodotApiWhitelist + Signature + Claim），不再发「TODO 注释桩」。注释仍明言「§7 数据在运行期实例化为 Claims；L1 数学（net/Peak/Compatible）在运行期权威」（诚实，非假绿）。
- 保留 `IIncrementalGenerator` 结构 + `[Generator]`。
- 注释带 §14 L2 / §7 / §3.3.1 出处。

**测试（改 `ToolingTests.cs` 或新增 `GeneratorEmitTests.cs`）：** 现有 `Generator_EmitsRegistryForAnnotatedMethod` 已断言生成含 `EffectAlgebraGenerated`——升级后断言生成文本**同时含** `Compute_<method>` 与 `GodotApiWhitelist.All` 与 `Signature.Union`（证明真引用 L1 非桩）。若旧测试断言过弱，用 write 加强（如断言生成文本含 `GodotApiWhitelist.All`）。
- 另加一条：生成代码若被编译进一个带 `[EffectOverride("r")] void AddChild(){}` 的测试 source，其生成方法 `Compute_AddChild` 返回的 Signature 应包含 `GodotApiWhitelist.All` 中 `AddChild` 对应的 Claims（用 Roslyn 跑 generator → 更新编译 → 反射调用 `EffectAlgebraGenerated.Compute_AddChild` → 断言结果 `AllClaims()` 非空且含 `Kind.Occupy`）。此条验证「生成代码真可运行消费」。

**约束（用户铁律）：** 生成器是翻译层，数学在 L1；生成代码真委托 L1（非桩）；残差诚实注释。

**验证（必须）：** `dotnet build Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly` 0 错误 0 警告；`dotnet test` 0 失败（绿）。若 `Signature` 缺便捷构造子，用 write 补，保持绿。

**完成后最后一行回复：** BUILD_OK 测试=0 失败，已升级 L2 生成器为真每方法 Signature（引用 GodotApiWhitelist+Signature，非桩）+ 生成消费测试。

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