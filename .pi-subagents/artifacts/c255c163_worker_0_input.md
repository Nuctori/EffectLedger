# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际写文件并验证 YAML 合法。任务：为 `D:/Godot/Cosmos/Cosmos.EffectAlgebra.slnx` 实现 **CI 集成编译期审计 gate**（迭代28 #113），让构建+测试+分析器在 CI 中作为门禁失败即红。

环境：`cd D:/Godot/Cosmos`

**必读：** `Cosmos.EffectAlgebra.slnx` 存在；`src/Cosmos.EffectAlgebra/*.csproj`（确认 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 已在 3 个 src 工程）、`tests/Cosmos.EffectAlgebra.Tests/Cosmos.EffectAlgebra.Tests.csproj`（确认引用 Generator/Analyzer 两 ProjectReference + xUnit，ToolingTests/EndToEndTests 已实跑 L2/L3）。

**落地内容：**
1. **GitHub Actions 工作流 `D:/Godot/Cosmos/.github/workflows/ci.yml`**（CI 编译期审计 gate）：
   ```yaml
   name: CI - Effect Cost Algebra Audit Gate
   on:
     push:
       branches: [ main, master ]
     pull_request:
   jobs:
     audit-gate:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
         - name: Setup .NET 10
           uses: actions/setup-dotnet@v4
           with:
             dotnet-version: '10.0.x'
         - name: Restore
           run: dotnet restore Cosmos.EffectAlgebra.slnx
         - name: Build (编译期门禁：TreatWarningsAsErrors 使警告=错误)
           run: dotnet build Cosmos.EffectAlgebra.slnx -c Release --no-restore -warnaserror
         - name: Test (审计 + 代数 + L2/L3 端到端；任何失败=红)
           run: dotnet test Cosmos.EffectAlgebra.slnx -c Release --no-build --verbosity normal
   ```
   - 注释：`-warnaserror` 与 src 工程 `TreatWarningsAsErrors` 双保险，编译期任何警告即失败；`dotnet test` 含 212 项（代数定律/随机性质/§7 白名单/§8 属性/§11 稳定性/§14 矩阵/端到端/L2 生成+L3 分析器实跑），任一红=CI 红。
   - 注意：本机该 slnx 的 VS nuget restore 坏，但 GitHub runner 用 dotnet SDK 还原正常（与本机 SDK `dotnet build` 同机制）。无需在该 yml 处理 MSBUILD_EXE_PATH（那是本机 VS 坏环境特例，CI runner 干净）。
   - 若 `.github/workflows` 目录不存在，用 write 创建（自动建目录）。
2. **本地 CI 等价脚本 `D:/Godot/Cosmos/ci.sh`**（bash，便于本地复现，且清掉本机坏 MSBUILD_EXE_PATH）：
   ```bash
   #!/usr/bin/env bash
   # 编译期审计 gate 本地等价（迭代28 #113）
   set -euo pipefail
   cd "$(dirname "$0")"
   unset MSBUILD_EXE_PATH   # 本机 VS 坏 MSBuild 特例，CI runner 不需要
   dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror
   dotnet test Cosmos.EffectAlgebra.slnx -c Release --no-build
   echo "CI audit gate: PASS (0 errors, 0 warnings, 0 test failures)"
   ```
   - 同时写 `D:/Godot/Cosmos/ci.ps1`（PowerShell 等价，`$env:MSBUILD_EXE_PATH = $null`）。

**验证（必须）：**
- YAML 合法性：用 `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml'))" `（python 3.10 本机有）确认不抛；若没有 pyyaml，用 `python -c "import ast"` 不适用——改用一个简单校验：`Get-Content` 读文件确认缩进合理，或 `python -c "import yaml"` 测试可用性后决定。若 yaml 模块缺失，写一个小 python 片段做基础括号/缩进检查即可，不阻塞。
- 本地 gate 真跑（若快）：`bash ci.sh` 或 `pwsh ci.ps1` 确认 0e/0w/0 失败（本机约 1-2s，因已编译）。若时间顾虑，至少跑 `dotnet build ... -warnaserror` 确认无警告变错误的新增问题。

**完成后最后一行回复：** CI_OK 已写 .github/workflows/ci.yml + ci.sh + ci.ps1；YAML 合法；本地 build -warnaserror 0e/0w。

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