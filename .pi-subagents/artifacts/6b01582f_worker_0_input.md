# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是独立审计 subagent，负责「迭代03 审计」。只审计代码，不写代码。

**只读文件（违反即作废）：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/ApiMapping.cs`
- 对照：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §7（L623 起 §7.1–§7.10 逐表）+ §8.1 release-class 清单（搜 `release-class = {`）
**禁止**读 `D:/Godot/Cosmos/audit/` 任何文件（含 iter-code01/02）。

**审计目标（用户铁律：类型约束数学边界 + 注释承载；白名单须逐条可机械对齐 PDR）：**
1. **白名单覆盖率**：§7.1–§7.10 每个 API 是否都在 `GodotApiWhitelist.All` 中有一条 `ApiMapping`？有无 §7 列出但代码遗漏的 API（逐表核对，回指 PDR 行号 + 代码行号）？
2. **Claim 编码正确性**：每条映射的 `Claims` 是否与 PDR §7 表的「Claim 集合」列逐字一致？重点核：
   - §7.1 QueueFree ⇒ `mode=release`（PDR §7.1 已收口 rA：QueueFree mode=release）；
   - §7.5 Connect `write(self,"signal_"+s,create)` + `occupy(callback,...,create)`；Disconnect 对应 release；EmitSignal `write(signal_bus,...,create)` + `read(tree,"subscribers_"+s,use)`；
   - §7.6 Draw* `write(gpu,command_buffer,create)`；
   - §7.7 Play `write(audio_mixer,self.channel_id,create)` + `read(memory,stream.buffer_id,use)` + `occupy(audio_channel,1,create)`；
   - §7.8 `read(input,action,use)`；§7.9 `write(network,...)` + `read(memory,...)`；§7.10 `occupy(animation_state,1,create)`。
3. **resource 编码一致性**：§7 裸名（gpu/memory/command_buffer/signal_bus/audio_channel/animation_state/callback/audio_mixer/input/self/tree/network）是否用 PDR §3.1.4a 的 ResourceId 构造子（Gpu/CommandBuffer/SignalBus/Occupancy/AudioMixer/Input/Self/Tree/Network 等）？有无用错构造子或漏字段？
4. **scope 编码**：§7 的 shell_scope 是否统一用 `new ScopeId.Shell()`（§3.1.3 Shell 构造子，ST-04 收口）？
5. **release-class 一致性**：`ReleaseClass.Names` 是否 = `{ queue_free, free, remove_child, disconnect, remove_from_group, cancel_free, free_children_in_group }`（与 PDR §8.1 严格一致，7 个，无增删）？`IsRelease` 是否真判（大小写）？
6. **注释/出处**：每条 ApiMapping 是否带 §7.x 出处注释？release-class 是否引 §8.1 + 源码事实？
7. **L1 零 Godot 依赖**：ApiMapping.cs 是否无 `using Godot`、Rid/StringName 为内部原语？

**产出 `D:/Godot/Cosmos/audit/iter-code03.md`（严格）：**
```
# 迭代03 审计（§7 白名单 + release-class 数据层）
## 摘要
- 构建：0 错误 0 警告（已核实）
- open 项总数：X（可闭 K / 设计 out-of-scope M）
- 终止判定：可终止 / 需继续(N)
## 逐 API 核对（回指 PDR 行 + 代码行 + 结论 OK/OPEN）
| PDR § | API | 代码行 | Claim 是否一致 | 结论 |
...
## release-class 核对
## open 项清单（若有）
## 结论
```
独立判断。若白名单逐条对齐 PDR、release-class 7 个一致、注释完整、零 Godot 依赖 ⇒ 可终止。

完成后回复：iter-code03.md 已写入；open 项 X；终止判定=？（一行）。

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