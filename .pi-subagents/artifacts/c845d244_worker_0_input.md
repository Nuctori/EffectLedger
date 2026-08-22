# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：为 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 每个 .cs 加**模块级文档头**（迭代17），声明该文件实现 PDR 的哪一节 + LANDING_PLAN 架构角色，构建仍绿。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**先 read 全部 src .cs：** Numeric.cs / Objects.cs / Algebra.cs / SignedNet.cs / Deviation.cs / ApiMapping.cs / DerivedMetrics.cs / EffectAttributes.cs。

**任务（用户铁律：类型约束数学边界 + 注释承载语义与 § 出处；模块级也须声明「本文件实现 PDR §x + LANDING_PLAN 角色」）：** 每个文件顶部（namespace 之前或文件级 `///`/注释）补一个模块文档头（单行或多行），格式：
```
// <file> — PDR §x.y 实现：<一句话语义>。LANDING_PLAN §n：<角色：纯代数核心 / 白名单数据 / 派生度量 / 属性 / 类型边界>。
```
逐文件映射：
- `Numeric.cs` → `// Numeric.cs — PDR §3.1.5a/§3.1.5b/§3.1.5c 实现：ℕ*/区间/DeviationVal 的 ⊤-闭环代数载体。LANDING_PLAN §3.1：L1 纯代数核心（零 Godot 依赖）。`
- `Objects.cs` → `// Objects.cs — PDR §3.1.1/§3.1.2/§3.1.3b/§3.1.4a/§3.1.4b 实现：Claim 五元组、ResourceId 单点真相、ScopeId 偏序、Signature 三桶量纲隔离。LANDING_PLAN §3.1：L1 纯代数核心。`
- `Algebra.cs` → `// Algebra.cs — PDR §3.2.1/§3.2.3/§3.3.1/§3.3.2 实现：组合、Compatible 全函数、net 净占用、Peak 峰值。LANDING_PLAN §3.2：L1 代数运算。`
- `SignedNet.cs` → `// SignedNet.cs — PDR §3.3.1 实现：有符号网值 ZStar/SignedInterval（create 正、release 负、区间含 0 即守恒）。LANDING_PLAN §3.3：类型边界载体。`
- `Deviation.cs` → `// Deviation.cs — PDR §9.1 实现：开发期 Deviation 公式（分母 ε=1 防除零、⊤ 整体跳过）。LANDING_PLAN §3.3：L1 派生度量。`
- `ApiMapping.cs` → `// ApiMapping.cs — PDR §7（§7.1–§7.10）实现：Godot API → Claim 白名单；§8.1 release-class。LANDING_PLAN §3：L1 数据层（零 Godot）。`
- `DerivedMetrics.cs` → `// DerivedMetrics.cs — PDR §3.2.5/§3.3 实现：循环组合 ω∈ℕ∪{⊤}、便利封装 Derived.Peak/Net/IsConserved。LANDING_PLAN §3.2：L1 派生度量。`
- `EffectAttributes.cs` → `// EffectAttributes.cs — PDR §8.3.1/§8.3.2 实现：[EffectOverride]（禁覆盖 kind、reason 强制）/[AcceptDeviation]（ε∈[0,0.5] 构造子强制）。LANDING_PLAN §3：L1 属性层（零 Godot）。`

**约束：** 只加模块头注释，不改任何代码/签名；保持 build 0e/0w（注释不影响编译）。

**验证：** `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。

**完成后最后一行回复：** FIX_OK 错误数=0，已补 8 个文件的模块级文档头（各引 PDR §x + LANDING_PLAN 角色）。

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