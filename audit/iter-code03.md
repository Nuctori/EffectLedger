# 迭代03 审计（§7 白名单 + release-class 数据层）

## 摘要
- 构建：**0 错误 0 警告**（已独立核实 `dotnet build -clp:ErrorsOnly`：0 个警告 / 0 个错误）。
- 白名单条目数：`ApiMapping.cs` 含 **38** 条 `M("...")` 映射；PDR §7.1–§7.10 全表共 **38** 个 API 行（§7.1×6 / §7.2×5 / §7.3×5 / §7.4×4 / §7.5×4 / §7.6×3 / §7.7×3 / §7.8×3 / §7.9×2 / §7.10×3）。**数量逐表一致，无遗漏、无多余。**
- open 项总数：**0（代码层可闭 0 / 设计 out-of-scope 0）**；附 **2 条 PDR 文档侧 kind-label 笔误**（非代码缺陷，L1 无对应 kind，代码采用与全文一致的数学正确编码）。
- 终止判定：**可终止**——白名单逐条对齐 §7、release-class 7 个与 §8.1 严格一致、注释带 § 出处、L1 零 Godot 依赖、类型字段即边界。

## 逐 API 核对（回指 PDR 行 + 代码行 + 结论）

| PDR § | API | 代码行 | Claim 是否一致 | 结论 |
|---|---|---|---|---|
| §7.1 | `GetNode` | L50 | `read(tree,path,use,shell)` → `Rd(Tree("path"),Use,Shell())` | OK |
| §7.1 | `GetTree` | L51 | `read(tree,"root",use,shell)` → `Rd(Tree("root"),Use,Shell())` | OK |
| §7.1 | `AddChild` | L53-55 | `write(tree,node.id,create)+occupy(tree,node.id,create)` → `Wr/Oc(Tree("node.id"),Create,Exact(1))` | OK |
| §7.1 | `RemoveChild` | L57-59 | `write(tree,node.id,release)+occupy(tree,node.id,release)` → `Wr/Oc(Tree("node.id"),Release,Exact(1))` | OK |
| §7.1 | `QueueFree` | L61-63 | `mode=release`（iter27 收口）满足；代码 `Oc(Tree("self.id"),Release,Exact(1)) + Oc(Mem(),Release,Dynamic)`。PDR 行写 `release(tree,...)` 作 kind，但 L1 无 Release kind；代码用 Occupy+Release，与 §7.5 Disconnect 同构、且 feed §3.3.1 net 的 −size 规则（见下方 PDR-笔误-1） | OK（代码数学正确；PDR 行 kind 标签为笔误） |
| §7.1 | `MoveChild` | L65 | `write(tree,node.id,use)` → `Wr(Tree("node.id"),Use,Shell())` | OK |
| §7.2 | `Position.get` | L68 | `Rd(Self("transform"),Use,Shell())` | OK |
| §7.2 | `Position.set` | L69 | `Wr(Self("transform"),Use,Shell())` | OK |
| §7.2 | `GlobalPosition.get` | L71-73 | `Rd(Self)+Rd(Tree("parent_path"))` | OK |
| §7.2 | `Rotation.getset` | L75-77 | `Rd+Wr(Self("transform"))` | OK |
| §7.2 | `Scale.getset` | L79-81 | `Rd+Wr(Self("transform"))` | OK |
| §7.3 | `MoveAndSlide` | L84-86 | `Rd+Wr(Phys)+Rd(Tree("collision_shapes"))` | OK |
| §7.3 | `ApplyForce` | L87 | `Wr(Phys("self.body_id"),Use)` | OK |
| §7.3 | `ApplyImpulse` | L88 | `Wr(Phys("self.body_id"),Use)` | OK |
| §7.3 | `GetSlideCollisionCount` | L89 | `Rd(Phys("self.body_id"),Use)` | OK |
| §7.3 | `GetSlideCollision` | L90 | `Rd(Phys("self.body_id"),Use)` | OK |
| §7.4 | `Load` | L93-95 | `Rd(Disk)+Oc(Mem,Create,Global,Dynamic)`；global_scope→`Global()` ✓ | OK |
| §7.4 | `LoadInteractive` | L96 | `Rd(Disk("path"),Use)` | OK |
| §7.4 | `Instantiate` | L98-101 | `Rd(Mem,Use)+Wr(Tree("new_id"),Create)+Oc(Mem,Create,Dynamic)`。PDR 行写 `create(tree,new_id,create)` 作 kind，但 L1 无 Create kind；代码用 Write+Create，与 §7.1 AddChild 同构（见 PDR-笔误-2） | OK（代码数学正确；PDR 行 kind 标签为笔误） |
| §7.4 | `Preload` | L103-105 | `Rd(Disk)+Oc(Mem,Create,Global,Dynamic)`；global_scope→`Global()` ✓ | OK |
| §7.5 | `EmitSignal` | L108-110 | `Wr(SigBus("signal"),Create)+Rd(Tree("subscribers_signal"),Use)` | OK |
| §7.5 | `Connect` | L112-114 | `Wr(Self("signal_x"),Create)+Oc(Cb(),Create,Dynamic)`（任务点 2 要求） | OK |
| §7.5 | `Disconnect` | L116-118 | `Wr(Self("signal_x"),Release)+Oc(Cb(),Release,Dynamic)`（任务点 2 要求） | OK |
| §7.5 | `IsConnected` | L120 | `Rd(Self("signal_x"),Use)` | OK |
| §7.6 | `DrawMesh` | L123-125 | `Rd(Gpu("mesh"))+Wr(CmdBuf(),Create)+Rd(Gpu("material"))` | OK |
| §7.6 | `DrawRect` | L126 | `Wr(CmdBuf(),Create)` | OK |
| §7.6 | `SetMaterialOverride` | L128-130 | `Wr(Self("material"),Use)+Rd(Gpu("material"),Use)` | OK |
| §7.7 | `Audio.Play` | L133-135 | `Wr(AudioMx(),Create)+Rd(Mem(),Use)+Oc(Occ("audio"),Create,Exact(1))`（任务点 2 要求） | OK |
| §7.7 | `Audio.Stop` | L137-139 | `Wr(AudioMx(),Release)+Oc(Occ("audio"),Release,Exact(1))` | OK |
| §7.7 | `Audio.SetVolumeDb` | L141 | `Wr(AudioMx(),Use)` | OK |
| §7.8 | `IsActionPressed` | L144 | `Rd(Inp("action"),Use)` | OK |
| §7.8 | `IsActionJustPressed` | L145 | `Rd(Inp("action"),Use)` | OK |
| §7.8 | `GetMousePosition` | L146 | `Rd(Inp("mouse"),Use)` | OK |
| §7.9 | `Rpc` | L149-151 | `Wr(Net(0,"method"),Create)+Rd(Mem(),Use)` | OK |
| §7.9 | `RpcId` | L153-155 | `Wr(Net(0,"method"),Create)+Rd(Mem(),Use)` | OK |
| §7.10 | `Anim.Play` | L158-160 | `Wr(Self("animation"),Create)+Rd(Mem(),Use)+Oc(Occ("animation"),Create,Exact(1))`（任务点 2 要求） | OK |
| §7.10 | `Anim.Stop` | L162-164 | `Wr(Self("animation"),Release)+Oc(Occ("animation"),Release,Exact(1))` | OK |
| §7.10 | `Anim.Seek` | L166 | `Wr(Self("animation"),Use)` | OK |

## resource 编码一致性（审计点 3）
- `gpu→Gpu(Rid)` / `memory→Mem(Memory(0) 哨兵)` / `command_buffer→CmdBuf(CommandBuffer("gpu"))` / `signal_bus→SigBus(SignalBus)` / `audio_channel→Occ("audio")(Occupancy)` / `animation_state→Occ("animation")(Occupancy)` / `callback→Cb(Callback)` / `audio_mixer→AudioMx(AudioMixer)` / `input→Inp(Input)` / `self→Self` / `tree→Tree` / `network→Net` / `disk→Disk` / `physics→Phys(Physics(Rid))`。
- 全部走 §3.1.4a 构造子，无错构造子、无漏字段。`Mem()/AudioMx()/CmdBuf()/Net(0,…)` 用 0 / "gpu" / peer=0 哨兵，注释明言「真实 UID 由映射层运行时填入（§3.1.4a）」——属 L1 数据层合理占位，非错误。

## scope 编码（审计点 4）
- `shell_scope → new ScopeId.Shell()`（ST-04 收口）；`global_scope → new ScopeId.Global()`（§7.4 Load/Preload 的 memory 占用）。全部统一，无裸字符串 scope。

## release-class 核对（审计点 5）
- `ReleaseClass.Names`（L181）= `{ queue_free, free, remove_child, disconnect, remove_from_group, cancel_free, free_children_in_group }` —— **7 个，与 PDR §8.1 L713 逐字一致，无增删。**
- `IsRelease` 对 `api.ToLowerInvariant()` 查 `Names`（大小写不敏感，真判）。
- `ReleaseClass` 注释引 §8.1 + Godot 源码事实（node.cpp 递归释放 / remove_child / disconnect / remove_from_group 等），与 PDR §8.1 来源注记一致。

## 注释 / 出处（审计点 6）
- 每个 `M("Api", …)` 带 `// §7.x` 出处注释；`ApiMapping` 类级 XML 引 §7；`GodotApiWhitelist` 引 §7.1–§7.10；`ReleaseClass` 引 §8.1。类型字段即边界（record struct + ImmutableArray），注释承载数学语义。满足。

## L1 零 Godot 依赖（审计点 7）
- `ApiMapping.cs` 无 `using Godot`；`Rid`/`StringName` 为 `Cosmos.EffectAlgebra` 命名空间内 `readonly record struct` 原语别名（定义于 Objects.cs，本文件引用）。注释明言映射层（§7，L2）负责运行时转换。满足。

## open 项清单
**无代码层 open 项。**

### 附：PDR 文档侧 kind-label 笔误（非代码缺陷，out-of-scope，供父层可选清理）
- **PDR-笔误-1**（§7.1 QueueFree 行 L610）：写 `release(tree, self.id, release, …)` / `release(memory, self.size, release, …)`，把 `release` 用作 kind。L1 kind 枚举仅 {Read,Write,Occupy}，无 Release kind；释放语义的正确编码是 **Occupy+Release**（代码如此，且 §7.5 Disconnect `occupy(callback,…,release)` 同构、§3.3.1 net 规则依赖 occupy+release 计 −size）。建议 PDR 将 QueueFree 行的 kind 标签由 `release` 改为 `occupy`。
- **PDR-笔误-2**（§7.4 Instantiate 行 L607）：写 `create(tree, new_id, create, …)`，把 `create` 用作 kind。L1 无 Create kind；正确编码是 **Write+Create**（代码如此，且 §7.1 AddChild `write(tree,node.id,create)` 同构）。建议 PDR 将 Instantiate 行的 kind 标签由 `create` 改为 `write`。

## 结论
- 白名单 **38/38** 逐条对齐 §7（kind/mode/resource/scope 四元组一致），release-class **7/7** 与 §8.1 严格一致，注释带 § 出处，L1 零 Godot 依赖，类型字段即边界——**全部满足迭代03 终止条件**。
- 仅余 2 处 PDR 文档级 kind-label 笔误（QueueFree / Instantiate），属 PDR 文本不一致、非代码错误；代码采用与全文一致的数学正确编码（Occupy+Release / Write+Create），无需修改代码即可终止本轮。
- 终止判定：**可终止**。
