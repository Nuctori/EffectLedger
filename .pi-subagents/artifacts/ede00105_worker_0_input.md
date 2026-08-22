# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #7（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §6 三层类型系统 L1/L2/L3 可证性与 TS-001..012 收敛真伪。

步骤：
1. 用 read 读取该 PDR，重点 §6.1-6.5（L1 类型系统、L2 Source Generator、L3 Roslyn Analyzer、TS-001..012）。
2. 审计并给数学性质+证明状态：
   - L1（sealed/readonly/struct 泛型约束）是语言保证，discharged（唯一真可证层）。
   - L2 Source Generator：字段白名单递归完备未证(open)；方法体写集分析 soundness 未证(open，漏间接写/反射/别名 ⇒ EA-005/SYS001 可能漏冲突)；无决策 AST 检查仅覆盖 6 种语句漏表达式级控制流(open)。
   - L3 Roslyn Analyzer：RULE001 包引用检测漏间接依赖(open)；SHELL001 复杂度「复杂条件」未定义(open)；SHELL003 反射 new 隐藏方法无法静态阻止(open)。
   - TS-001/003/005/007/008/010/011 实为 asserted（依赖未证工具）；TS-002/004/006/009/012 部分 discharged；TS-009 反射 new 隐藏为 open。
3. 结构性成立的给形式证明(写前提)。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter07.md（非空中文 Markdown）：顶部「独立审计 #7（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；TS-001..012 审计表；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I7- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter07.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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