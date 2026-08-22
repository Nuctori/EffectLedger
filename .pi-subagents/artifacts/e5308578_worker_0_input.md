# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代15 审计」。只审计测试代码，不写测试。

**只读文件（违反即作废）：** `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/CrossLayerTests.cs` + 被测 src（Generator/Analyzer/ApiMapping/Objects/Algebra/Deviation）。对照 PDR §7/§8.1/§3.x + LANDING_PLAN §1（L2/L3 依赖 L1 经 ProjectReference）。
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件。

**审计目标（用户铁律：跨层引用一致性真由反射断言、非假绿）：** 核对反射断言是否真锁签名/符号存在、可证伪（漂移必红）。

逐条（回指测试行号 + 被测行号 + 结论）：
1. **GodotApiWhitelist.All**：反射断言属性存在 + 类型 `ImmutableArray<ApiMapping>` + Length>0？回退（改名/改类型）是否必红？
2. **ReleaseClass.IsRelease + Names**：反射断言方法签名 `(string)->bool` + Names 含 7 项？改名是否必红？
3. **Claim 五字段**：反射断言 Kind/Resource/Mode/Scope/Size 均存在（含 required）？删字段是否必红？
4. **Compatible/NetTable 签名**：反射断言方法存在且签名匹配？漂移是否必红？
5. **L2 Generator 类型**：反射断言 `EffectAlgebraGenerator : IIncrementalGenerator`？删接口实现是否必红？
6. **L3 Analyzer 类型**：反射断言 `[DiagnosticAnalyzer]` + `: DiagnosticAnalyzer` + `SupportedDiagnostics` 含 `EAA0901`？改名/删诊断是否必红？
7. **ApiMapping 字段 + Claims 元素合法**：反射断言 GodotApi/Claims 字段 + 每条 Claim 五字段非空？
8. **假绿扫描**：有无反射 `GetProperty` 用 `BindingFlags` 漏掉 static/instance 致「找不到也当存在」？有无 `Assert.True(true)`？有无「永远通过」弱断言？
9. **出处注释**：每项带 §x.y？

**产出 `D:/Godot/Cosmos/audit/iter-code15.md`（严格）：**
```
# 迭代15 审计（跨层引用一致性）
## 摘要
- 测试：79 通过 0 失败（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐条核对（回指行号 + 被测 + 真锁? + 假绿? + 结论）
| 项 | 行号 | 被测 | 真锁? | 假绿? | 结论 |
...
## open 项清单（若有）
## 结论
```
独立判断。若 8 项反射断言真锁跨层符号/签名、可证伪、无假绿 ⇒ 可终止。

完成后回复：iter-code15.md 已写入；open 项 X；终止判定=？（一行）。

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