# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际运行全解构建+全测试并产出交付证明。任务：迭代30 收尾——全解构建 + 全测试 + 写交付证明文档（不写新源码，仅验证 + 文档）。

环境：所有命令前置 `$env:MSBUILD_EXE_PATH = $null`（PowerShell）或 `MSBUILD_EXE_PATH=`（bash）。
`cd D:/Godot/Cosmos`

**步骤（逐步运行并记录真实输出）：**
1. `dotnet build Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly` → 记录 错误/警告数（须 0/0）。
2. `dotnet test Cosmos.EffectAlgebra.slnx -clp:ErrorsOnly --nologo` → 记录 通过/失败/跳过/总计（须 0 失败，约 212）。
3. 列出已交付文件树（src 三工程 + tests 工程的 .cs 文件清单，用 ls）：
   - `src/Cosmos.EffectAlgebra/*.cs`（8 个核心文件）
   - `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs`
   - `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`
   - `tests/Cosmos.EffectAlgebra.Tests/*.cs`（测试文件清单）
4. 确认每个 src .cs 顶部模块文档头存在（grep `LANDING_PLAN §` 应 ≥8 命中）。

**产出 `D:/Godot/Cosmos/DELIVERABLE.md`（一次性写，严格）：**
```
# Cosmos.EffectAlgebra 交付证明（v3.0-FINAL-rA6 实现）
## 构建状态
- 全解 `dotnet build`：0 错误 0 警告（<时间>，附命令输出）
- 全测 `dotnet test`：通过 N / 失败 0 / 跳过 0（附命令输出）
## 交付内容（文件树）
- L1 纯代数核心（零 Godot 依赖）：Numeric/Objects/Algebra/SignedNet/Deviation/ApiMapping/DerivedMetrics/EffectAttributes
- L2 Source Generator：EffectAlgebraGenerator（IIncrementalGenerator，真实每方法 Signature 委托 L1）
- L3 Roslyn Analyzer：EffectAlgebraAnalyzer（EAA0901 DO-9 近似 / EAA0303 KIND_MIX / EAA0304 Compat 冲突 / 控制流近似诚实）
- Tests：N 个测试文件，覆盖 §3.1–§3.3/§7/§8/§9.1/§11/§14 不变量 + 端到端 + 规模 + 跨层
## PDR 对照覆盖
- §3.1–§3.3 代数核心：✅ 全
- §7 白名单 38 条 + §8.1 release-class 7 项：✅
- §8.3 属性（reason/ε 构造子强制）：✅
- §9.1 Deviation（ε=1 + ⊤ 跳过）：✅
- §11 DO-1..DO-10 回归守护：✅
- §14 验证矩阵可自动化项：✅（A3/A4 已补）
## out-of-scope（诚实标注，无静默缺口）
- §10 运行期采样（Godot 引擎钩子，依赖 D1）
- §12 Godot 工程改造接入（Audit-only，D1）
- §13 L3 在真实 Godot 工程接入（需 Godot.NET.Sdk，本机未装，D1）
## 类型-vs-注释纪律落实
- 类型强制：NatStar ⊤ 闭包/溢出⇒⊤、Interval 构造不变量、Claim required 五元组、EffectAttributes 构造子边界、ResourceId 判别联合单点真相、ScopeId 偏序、Signature 三桶、Compatible 全函数
- 注释承载：控制流近似、运行期权威、残差、逐符号 §x.y 出处（8 文件模块头 + 符号级注释）
## 质量门禁
- TreatWarningsAsErrors（src 三工程）：✅ 0 警告即 0 缺陷
- 全解构建门禁 + 212 测试绿
```
**验证（必须）：** 构建与测试命令真实运行且 0 失败（不得凭记忆写「绿」）；文件树真实列出。

**完成后最后一行回复：** DELIVER_OK 构建 0e/0w 测试 N 通过 0 失败；DELIVERABLE.md 已写。

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