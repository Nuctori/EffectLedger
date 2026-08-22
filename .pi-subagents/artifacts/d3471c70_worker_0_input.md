# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #8（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §7.1-7.3 场景树/属性/物理 API 的 Claim 映射数学性质。

步骤：
1. 用 read 读取该 PDR，重点 §7.1（场景树 L421-450）、§7.2（属性 L432-440）、§7.3（物理 L442-450）；邻接 §3.2.3 Compatible、§3.1.1 Claim 字段、§3.4 MA-002。
2. 为每条 Godot API 建单步 Claim 集合并核查组合性质与内部一致性。重点：
   - QueueFree（§7.1 L429）用 mode=move 标注释放动作，与 §3.2.3 Compatible 注释「move+move 不兼容」冲突；且 §3.3.1 net 公式按 mode=release 计负项，此处 mode=move 将不被 net 当释放 ⇒ 泄漏检测(DO-9)失效（高，open）。
   - self 资源在映射中裸称 vs §3.1.2 Self(component) 构造子不一致（跨 API 去重/冲突精度，open）。
   - 重复 AddChild 被 create+create 不兼容误报（Godot 允许多同名子节点，open 精度）。
   - AddChild(create)+RemoveChild(release) 跨 mode 兼容规则缺失（§3.2.3 未列 create∧release，open）。
   - Position/Rotation/Scale 共享 "transform" 资源，粒度粗（open 精度）。
   - 属性 setter 标 write(..,use)——kind=write 与 mode=use 正交耦合规则未定义（open）。
   - 物理 physics(body_id:RID) 编译期非常量 RID 退化为 Unknown（MA-010，open 精度）。
3. 结构性成立的给形式证明(写前提)。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter08.md（非空中文 Markdown）：顶部「独立审计 #8（hy3 单独进程，本轮重跑）」；每 API 表或分组(命题/数学性质/状态/一致性/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I8- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter08.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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