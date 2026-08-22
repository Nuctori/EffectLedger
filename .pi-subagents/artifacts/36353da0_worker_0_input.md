# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建。任务：在 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 落地 **§7 Godot API 白名单 + release-class 数据层**（迭代03），构建绿。

环境（必读）：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**必读 PDR 章节（先 read，再写，对照编码）：**
- `D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md` §7（L623 起的 §7.1–§7.10 映射表）与 §8.1 release-class 清单（搜 `release-class = {` 那行，约 L694）。
- 已落地的类型在 `Objects.cs`/`Algebra.cs`：`ResourceId` 判别联合、`Claim(Kind,ResourceId,Mode,ScopeId,Interval)`、`Kind`/`Mode` enum、`ScopeId`（含 `Shell` 构造子，§7 用 shell_scope ⇒ Shell）。

**落地内容（新文件 `ApiMapping.cs`）：**
1. `readonly record struct ApiMapping`：`GodotApi`(string) + `Claims`(ImmutableArray<Claim>)。构造函数/工厂。
2. `static class GodotApiWhitelist`：
   - `public static ImmutableArray<ApiMapping> All { get; }` = 把 §7 映射表每条编码为一个 `ApiMapping`。**逐条对应 PDR §7.1–§7.10**（但 L1 零 Godot 依赖，API 名用字符串、resource 用 ResourceId 构造子、scope 用 `new ScopeId.Shell()` 表示 shell_scope）。
   - 编码规则（与 §7 表逐格一致）：
     - §7.1 Spawn: `Instantiate<T>`/`AddChild`/`QueueFree` 等按表；
     - §7.2 Tree: `GetNode`/`GetTree`/`RemoveChild`；
     - §7.3 Physics: `AddChild`-ish、`CreateRigidBody`/`AddCollisionShape`；
     - §7.4 Memory: `new Texture2D`/`Load`/`Preload`（显式 size 用 `Interval.Exact(ulong)` 或按 § 给值）；
     - §7.5 Signal: `EmitSignal`/`Connect`/`Disconnect`/`IsConnected`（signal_bus ⇒ `new ResourceId.SignalBus(new StringName(s))`，callback ⇒ `new ResourceId.Callback("cb")`）；
     - §7.6 Render: `DrawMesh`/`DrawRect`/`SetMaterialOverride`（gpu ⇒ `new ResourceId.Gpu(new Rid("mesh"))`、command_buffer ⇒ `new ResourceId.CommandBuffer("gpu")`）；
     - §7.7 Audio: `Play`/`Stop`/`SetVolumeDb`（audio_mixer ⇒ `AudioMixer(channelId)`、audio_channel ⇒ `Occupancy("audio")`）；
     - §7.8 Input: `IsActionPressed`/`IsActionJustPressed`/`GetMousePosition`（input ⇒ `Input(action)`）；
     - §7.9 Network: `Rpc`/`RpcId`（network ⇒ `Network(peerId,method)`）；
     - §7.10 Animation: `Play`/`Stop`/`Seek`（animation_state ⇒ `Occupancy("animation")`）。
   - **注意**：§7 表是 GPU 等资源的裸名（如 `gpu`/`memory`），按 §3.1.4a 归一映射：裸 `gpu`⇒`Gpu(Rid)`、`memory`⇒`Memory(uid="mem")`、`command_buffer`⇒`CommandBuffer("gpu")`、`signal_bus`⇒`SignalBus(_)`、`audio_channel`⇒`Occupancy("audio")`、`animation_state`⇒`Occupancy("animation")`、`callback`⇒`Callback("cb")`、`audio_mixer`⇒`AudioMixer(c)`、`input`⇒`Input(a)`、`self`⇒`Self(c)`、`tree`⇒`Tree(p)`、`network`⇒`Network(...)`。
   - 每条 `ApiMapping` 带 XML 注释 `§7.x` 出处。
3. `static class ReleaseClass`：
   - `public static ImmutableHashSet<string> Names { get; }` = `{ "queue_free", "free", "remove_child", "disconnect", "remove_from_group", "cancel_free", "free_children_in_group" }`（与 PDR §8.1 严格一致，源码核实清单）。
   - `public static bool IsRelease(string api)` 判断 api（小写）是否在 Names 中。
   - `public static ImmutableArray<string> All => Names.ToImmutableArray();`
   - 注释引 §8.1 + Godot node.cpp 源码事实（queue_free/free 递归释放 children）。

**约束（用户铁律）：** 每个映射条带来源注释；类型字段即边界；裸名映射靠 ResourceId.Normalize 在运行时归一（注释说明，不重复写死）。

**验证（必须）：** 写 `ApiMapping.cs` 后 `dotnet build -clp:ErrorsOnly` 0 错误 0 警告。若 §7 表某项编码后类型不匹配（如 Rid 需 `new Rid("x")`），用 write 修直到绿。

**完成后最后一行回复：** BUILD_OK 错误数=0，已写 ApiMapping.cs（白名单 N 条 + release-class 7 个）。

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