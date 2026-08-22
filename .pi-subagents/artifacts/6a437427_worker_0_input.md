# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #22（hy3 单独进程）。极小聚焦审计：§3.2.3 第2-4析取的非对称性如何破坏并行组合 `||` 的交换律（即 §3.1 隐含 A2 交换律，但 Compatible 非对称致同组合顺序不同结论相反）。

步骤：
1. read 读取 PDR：§3.2.3（L131-138，4 条析取）、§3.2.2 并行组合 `||`（L126-130，约束用有序对 `Compatible(c₁.mode,c₂.mode)`）、§3.1 组合律（A2 交换律隐含）。
2. 论证命题：`Compatible(create,use)=true` 但 `Compatible(use,create)=false`（因第2析取仅 `(create,use)` 形）；`(release,use)=true` vs `(use,release)=false`；`(move,use)=true` vs `(use,move)=false`。而 `S₁||S₂=S₁∪S₂=S₂∪S₁`（交换律），约束却用有序对 ⇒ 把同一对操作写成 `S₁||S₂` 检查 `(mode₁,mode₂)`、写成 `S₂||S₁` 检查 `(mode₂,mode₁)`，一真一假 ⇒ 并行组合是否通过兼容性检查取决于人为编号顺序，与 `||` 交换性矛盾。举例：AddChild(write,create) 与 MoveChild(write,use) 同 tree 节点——`(create,use)=true` ⇒ AddChild||MoveChild 通过；`(use,create)=false` ⇒ MoveChild||AddChild 失败。
3. 给数学性质+证明状态（open，高），交叉引用 Iter16 I16-01/02、Iter19 量化。
4. write 覆盖写入 D:/Godot/Cosmos/audit/iter22.md（非空中文，顶部「独立审计 #22（hy3 单独进程，本轮重跑）」；节：命题/数学性质/状态/论证/行号；Proof Obligation 账本(ID|命题|状态|最小补充|行号)；未消解缺口列表(I22- 前缀)）。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter22.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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