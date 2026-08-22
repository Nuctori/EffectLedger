# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
独立审计员 #19（hy3 单独进程）。审计 D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md：为全文每条 Godot API（§7.1-7.10）与每个组合子（§3.2）与每个对象（§3.1）构建「显式数学性质总表」，并核查总表能否落地——即每个效应/对象/组合子是否真有可核验的数学性质，还是多数仍是未定义/asserted。

步骤：
1. 用 read 读取该 PDR 全文，重点 §3.1（Claim/ResourceId/ScopeId/Signature，L78-104）、§3.2 组合子（; || ⊔ S×ω Compatible Peak peak，L107-154）、§3.3 派生度量（L155-173）、§7.1-7.10 逐条 API 映射（L421-507）；邻接全文各 Iter 审计。
2. 构建一张**主表**（在文件内），行 = 每个效应/对象/组合子，列 = 名称 | 显式数学性质 | 证明状态 | 性质来源(定义/asserted/缺失) | 行号 | 关联 open 缺口。覆盖：
   - 对象：Claim(kind/mode/resource/scope/size)、ResourceId(10 构造子)、ScopeId(7 构造子)、Signature(Set<Claim>)
   - 组合子：`;`/`||`/`⊔`/`S×ω`/Compatible/Peak/peak/net/read/write
   - 效应（§7 至少 30 个 API：GetNode/GetTree/AddChild/RemoveChild/QueueFree/MoveChild/Position/GlobalPosition/Rotation/Scale/MoveAndSlide/ApplyForce/ApplyImpulse/GetSlideCollisionCount/GetSlideCollision/Load/LoadInteractive/Instantiate/Preload/EmitSignal/Connect/Disconnect/IsConnected/DrawMesh/DrawRect/SetMaterialOverride/Play/Stop/SetVolumeDb/IsActionPressed/JustPressed/GetMousePosition/Rpc/RpcId/动画Play/Stop/Seek）
3. 核查：总表中有多少行性质是「真定义」、多少是「asserted/缺失」；给出量化结论（如「70 行中仅 X 行性质完整良定义，Y 行依赖未证机制」）。
4. 用 write 覆盖写入 D:/Godot/Cosmos/audit/iter19.md（非空中文 Markdown）：顶部「独立审计 #19（hy3 单独进程，本轮重跑）」；主表 + 量化结论 + 每类小结 + Proof Obligation 账本(ID|命题|状态|最小补充|行号) + 未消解缺口列表(I19- 前缀)。
5. 自由文本一句话摘要（不要调用 structured_output 类工具）。

铁律：只写 audit/iter19.md，绝对不要读/写/改其它 audit 文件。验收：header 含「本轮重跑」。

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