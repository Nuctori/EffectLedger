# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #9（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md 的 §7.4-7.10 资源/信号/渲染/音频/输入/网络/动画 API 的 Claim 映射数学性质。

步骤：
1. 用 read 读取该 PDR，重点 §7.4（L452-459 资源加载）、§7.5（L461-468 信号）、§7.6（L470-475 渲染）、§7.7（L477-484 音频）、§7.8（L486-491 输入）、§7.9（L493-499 网络）、§7.10（L501-507 动画）。
2. 为每条 API 建单步 Claim 集合并核查性质。重点：
   - size 估算的 scope 不统一：Load/Preload 标 occupy(memory,..,create,**global**)，Instantiate 标 occupy(memory,..,create,**shell**) ⇒ 同一内存占用两 scope，Peak/net 跨 scope 不并（高，open）。
   - QueueFree 的 occupy(memory,..,move) 与 Connect/Play/Stop/动画的 release 释放 mode 不一致（前者 move、后者 release）⇒ 同是释放动作 mode 不统一（交叉 Iter08 I8-01，open）。
   - 合成 resource 命名空间未封闭：command_buffer / signal_bus / "subscribers_"+signal / "material" 等非 §3.1.2 构造子（open）。
   - 网络 Rpc resource=self.id+"/"+method 字符串拼接 ⇒ 编译期 Unknown（MA-010，open 精度）。
   - Instantiate 的 new_id 动态 ⇒ Unknown（ED-004，open）。
   - 音频/动画 occupy(X,1,..) 单位 size 与 GPU 命令数在 peak 求和混算量纲（DO-7，交叉 Iter14，open）。
   - 纯读 API（输入/GetNode）幂等 clean；Connect/Disconnect、Play/Stop、动画 Play/Stop 的 occupy create/release 配对（给定 QueueFree 也用 release 的前提下 net 守恒，条件证明）。
3. 结构性成立的给条件证明。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter09.md（非空中文 Markdown）：顶部「独立审计 #9（hy3 单独进程，本轮重跑）」；分组(命题/数学性质/状态/行号)；Proof Obligation 账本表(ID|命题|状态|最小补充|行号)；未消解缺口列表(I9- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter09.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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