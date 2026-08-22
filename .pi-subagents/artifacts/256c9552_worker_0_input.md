# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，任务：实现 L1+L2+L3 端到端集成测试（迭代21），`dotnet test` 绿——证明「白名单→生成器→L1 数学→分析器」全链路闭环。

环境：`cd D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests; $env:MSBUILD_EXE_PATH = $null; dotnet test -clp:ErrorsOnly`

**先 read：** `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`（生成 `EffectAlgebraGenerated.Compute_<method>(Signature)`）、`src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`（DO-9 近似：acquire 无 release 且无 `[EffectOverride]` ⇒ EAA0901）、`ApiMapping.cs`（§7 含 `AddChild`(occupy create) 与 `QueueFree`(occupy release)、`Disconnect` 等）、`Objects.cs`(Signature/NetTable)。

**任务（新文件 `EndToEndTests.cs`）：** 构造一个真实感 sample 源（用 stub 类型占位 Godot API，无需真 Godot 类型），跑 generator + analyzer + L1 数学，断言全链路：
1. **balanced 方法（acquire+release）⇒ analyzer 不报 + L1 守恒**：
   源：
   ```csharp
   using Cosmos.EffectAlgebra;
   public class Enemy {
       public object child;
       [EffectOverride("spawn/despawn 配对")]
       public void SpawnAndDespawn() {
           AddChild(child);    // §7 acquire (occupy create)
           QueueFree();        // §8.1 release (occupy release)
       }
   }
   ```
   - 跑 generator ⇒ 生成含 `Compute_SpawnAndDespawn`。
   - 跑 analyzer ⇒ 0 个 EAA0901（acquire 有 release 配对，不误报）。
   - 把生成代码编译进 compilation ⇒ 反射 `EffectAlgebraGenerated.Compute_SpawnAndDespawn(new Signature())` ⇒ 结果 `NetTable.Compute(sig, Global()).IsConserved(...)` 对 `Tree` 资源为 true（create+release 抵消）。**此条证明生成签名经 L1 数学确守恒**。
2. **unbalanced 方法（仅 acquire）⇒ analyzer 报 EAA0901**：
   源：
   ```csharp
   public class Leaker {
       public object child;
       public void Leak() { AddChild(child); }  // 仅 acquire，无 release、无 [EffectOverride]
   }
   ```
   ⇒ analyzer 返回 ≥1 个 EAA0901。
3. **override 豁免（仅 acquire 但 [EffectOverride]）⇒ analyzer 不报**：
   源：
   ```csharp
   public class Intended {
       public object child;
       [EffectOverride("帧内临时占用，已知泄漏")]
       public void Temp() { AddChild(child); }
   }
   ```
   ⇒ 0 个 EAA0901。
4. **生成代码真委托 L1（非桩）**：断言 generator 对 `SpawnAndDespawn` 生成的 `Compute_SpawnAndDespawn` 文本含 `GodotApiWhitelist.All` 且 `Signature.Union`（与 iter20 一致）；且反射调用返回非空 Signature。
- 辅助：复用 iter10 的 `MakeCompilation`/`RunAnalyzer`/`RunGenerator` 模式；stub 类型 `AddChild`/`QueueFree` 定义为实例方法（如 `public void AddChild(object o){}` `public void QueueFree(){}`）使源可编译。
- 注释每项引 §7/§8.1/§3.3.1/§14 L2/L3。

**约束（用户铁律）：** 端到端真跑 generator+analyzer+L1，断言可证伪（不平衡必报、平衡必过、生成签名真守恒）；不假绿；stub 类型仅为编译占位（注释声明非真 Godot）。

**验证（必须）：** `dotnet test -clp:ErrorsOnly` 0 失败（绿）。若 analyzer 因 stub 类型名归一未命中白名单（如 `AddChild` vs 白名单 `AddChild` 大小写），确认归一一致；必要时在测试源用与白名单逐字一致的 API 名。

**完成后最后一行回复：** TEST_OK 失败数=0，已写 EndToEndTests.cs（balanced⇒过+守恒 / unbalanced⇒EAA0901 / override⇒豁免 / 生成真委托 L1 四项端到端）。

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