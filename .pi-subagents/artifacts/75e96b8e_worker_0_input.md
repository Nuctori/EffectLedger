# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #10（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §8 效应推导层（白名单/默认规则/override 的完备性与可靠性）。

步骤：
1. 用 read 读取该 PDR，重点 §8.1（L511-518 白名单/默认规则）、§8.2 ED-001..008（L520-531）；邻接 §7 映射、§3.2、Iter09。
2. 审计并给数学性质+证明状态：
   - §8.1 默认规则「未映射 API 默认 {read(unknown,use), write(unknown,use)}」是双向误差源：① 漏报 occupy/release（未映射 Instantiate/QueueFree 等释放效应被静默漏报 ⇒ DO-8/DO-9 失效，高 open）；② 误报 write 于纯读 API（DO-7 违背，open）；③ 所有未映射 API 共享 unknown 资源 ⇒ 大规模保守冲突（MA-010 Unknown⊤，open 精度）。
   - 白名单「约 100 个」与 §7 实际列 ~50 不符；Godot 效应 API 数千 ⇒ 覆盖率极低，默认规则主导（open）。
   - [EffectOverride] 无校验 ⇒ 开发者可关闭审计（信任边界，安全 open）。
   - ED-001..008 全部「已收敛」但靠保守估计/override/静态假设声明，无 soundness 证明（asserted）。
3. 结构性成立的给条件证明（白名单完备+准确 ⇒ 默认规则不触发 ⇒ sound）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter10.md（非空中文 Markdown）：顶部「独立审计 #10（hy3 单独进程，本轮重跑）」；每点一节(命题/数学性质/状态/论证/行号)；ED-001..008 审计表；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I10- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter10.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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