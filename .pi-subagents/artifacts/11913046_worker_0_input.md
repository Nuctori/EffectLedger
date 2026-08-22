# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代28 #113 审计」（CI 集成编译期审计 gate）。只审计，不写代码。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/.github/workflows/ci.yml`、`D:/Godot/Cosmos/ci.sh`、`D:/Godot/Cosmos/ci.ps1`、`src/Cosmos.EffectAlgebra/*.csproj`（确认 TreatWarningsAsErrors）、`tests/.../Cosmos.EffectAlgebra.Tests.csproj`（确认引用 Generator/Analyzer + xUnit 测试真跑 L2/L3）、`ToolingTests.cs`/`EndToEndTests.cs`（确认 L2/L3 被实跑）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件（含 iter-code28/iter-code29）。

**审计目标（用户铁律：编译期审计 gate 必须真作为门禁、失败即红、非装饰文件）：** 核对 CI gate 是否真落地且能拦缺陷。

逐条（回指文件行号 + 结论）：
1. **CI 工作流存在且真跑门禁**：`ci.yml` 是否 `dotnet build ... -warnaserror` + `dotnet test`？`warnaserror` 是否与 src 工程 `TreatWarningsAsErrors` 双保险（警告=错误）？
2. **test 含 L2/L3 实跑**：`dotnet test` 是否真含 ToolingTests/EndToEndTests（Generator 真正生成 + Analyzer 真正报 EAA0901）？还是只测 L1？回指 Tests.csproj ProjectReference + 测试名。
3. **失败即红**：CI 步骤是否 `set -euo`/步骤默认失败即 job 红？无 `continue-on-error: true` 掩盖？
4. **本地等价**：`ci.sh`/`ci.ps1` 是否真清本机坏 `MSBUILD_EXE_PATH` 并跑 build+test？实测 `bash ci.sh` 绿（已核实 212 通过 0 失败）。
5. **YAML 合法**：yaml 解析无误（已核实 YAML_OK）。
6. **触发范围**：on push/PR 主干分支，合理。
7. **无装饰/假绿**：有无空步骤、有无 `echo "pass"` 充数、有无绕过 test？

**产出 `D:/Godot/Cosmos/audit/iter-code28b.md`（严格）：**
```
# 迭代28 #113 审计（CI 编译期审计 gate）
## 摘要
- 本地 gate 实测：212 通过 0 失败 0e/0w（已核实）
- YAML 合法（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 结论 OK/OPEN）
| 检查 | 行号 | 真门禁? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 CI gate 真跑 build(-warnaserror)+test(含 L2/L3)、失败即红、YAML 合法、本地等价可复现 ⇒ 可终止（#113 闭）。

完成后回复：iter-code28b.md 已写入；open 项 X；终止判定=？（一行）。

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