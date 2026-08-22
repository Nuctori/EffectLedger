# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #20（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md：全局综合账本——把 Iter01-19 的全部证明义务(PO-I*)与未消解缺口(I*-)汇总回收，判定 §14「21 开放问题全收敛、0 阻塞」是否真实，并给出 PDR 级结论错误清单与修复优先级。

铁律：你只能写 D:/Godot/Cosmos/audit/iter20.md。但你**可以参考** audit/iter01.md … iter19.md 的 I- 编号（只读已有 audit 文件来汇总，这是允许的——汇总环节需要读其它审计结论；真正的"独立"指你本轮的归纳视角独立、且你只写 iter20.md 不修改任何其它文件）。

步骤：
1. 用 read 读取该 PDR 的 §14（L767-775）和 §3.4（L176-190）至少一遍；以及 audit/iter01.md..iter19.md 的「未消解缺口」与「Proof Obligation 账本」节（只读，用于汇总）。
2. 构建全局账本：
   - 汇总全部 PO-I*（按状态分 discharged含条件 / asserted / open），给出计数。
   - 汇总全部 I*- 阻塞级缺口（高/高），列出 8-10 条最关键（直接使 DO-7/DO-8/DO-9 数学未良定义者）。
   - 明确判定 §14「0 阻塞」为假：列出 §14 与文档自身证据冲突的具体点（§3.4 MA-002/006/009 open、全文 ≥8 阻塞、Deviation 除零、ScopeId⊆ 缺失、Compatible 偏函数、QueueFree mode=move、DO-7 未落地、S×ω/∞ 发散）。
3. 给 PDR 级结论错误清单（编号 L1-LN，每条含：错误陈述 | 反证行号 | 严重程度）与修复优先级（P0 阻塞数学定义 / P1 数值口径 / P2 声明降级）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter20.md（非空中文 Markdown）：顶部「独立审计 #20（hy3 单独进程，本轮重跑）」；全局账本 + 阻塞级缺口表 + §14 矛盾判定 + PDR 结论错误清单 + 修复优先级。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

验收：header 含「本轮重跑」；只写 audit/iter20.md（汇总时读其它 iter 文件是允许的，但绝不要改它们）。

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