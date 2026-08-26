# Iter09 审计 — §7.4-7.10 资源/信号/渲染/音频/输入/网络/动画 API 映射性质（独立审计 #9，hy3 单独进程，本轮重跑）

- **审计视角**：API→Claim 映射代数性质（资源/信号/渲染/音频/输入/网络/动画）（独立 pass #9，全新上下文，本轮重跑）
- **范围**：§7.4 资源加载（L452-459）、§7.5 信号（L461-468）、§7.6 渲染（L470-475）、§7.7 音频（L477-484）、§7.8 输入（L486-491）、§7.9 网络（L493-499）、§7.10 动画（L501-507）；邻接 §3.1.1 size、§3.3、§8 ED-004、Iter08 I8-01（QueueFree mode=move）
- **结论摘要**：为 §7.4-7.10 每条 API 建单步 Claim 集合并核性质。核心一致性发现：(1) **size 估算的 scope 不统一**——`Load`/`Preload` 标 `occupy(memory,estSize(T),create,global)`，而 `Instantiate` 标 `occupy(memory,scene.est_size,create,shell)`，同是内存占用但 scope 不同 ⇒ 全局 vs 局部占用混算，Peak/net 跨 scope 不并（高，open）；(2) **Instantiate 的泄漏检测数学关系**：Instantiate 产生 `occupy(memory,..,create)` 须配 QueueFree 的 `occupy(memory,..,move)`（Iter08 I8-01 指其 mode=move 致 net 漏算）⇒ 泄漏检测链在 §7.4/7.1 两端 mode 不一致；(3) **Connect/Disconnect 的 occupy(callback,size,create/release)** 与 QueueFree 同病——Disconnect 标 release mode（L467），但 Connect 的回调 occupy create 与 Disconnect release 配对时 net 可抵，而 QueueFree 路径的 occupy 释放却标 move ⇒ 不一致；(4) **网络 `Rpc` 写 `network,self.id+"/"+method`**（L498）用字符串拼接造 resource id，编译期为 Unknown（MA-010），保守冲突；(5) **音频/动画 occupy(X,1,create/release)** 单位资源 size=1，但 peak 求和混音频通道数与 GPU 命令数（量纲隔离 DO-7，Iter14）。结构性成立（纯读幂等 clean、Connect/Disconnect/Play/Stop/动画 Play/Stop 配对）给条件证明。

---

## H1. §7.4 资源加载映射性质

| API | Claim 集合（L456-459） | 性质/一致性 |
|-----|-----------|------------|
| `Load<T>(path)` | `read(disk,path,use,shell) ∪ occupy(memory,estSize(T),create,global)` | 读盘+占内存(global scope)。**size 为 `estimatedSize(T)` 估计值**（MA-005 未定常量性，Iter04 I4-04）。scope=global ⇒ 与 Instantiate 的 shell-scope 内存占用不并（Iter15 ⊆ 未定义放大）。 |
| `LoadInteractive(path)` | `read(disk,path,use,shell)` | 仅读盘，不占内存（延迟实例化）。合理。discharged（结构）。 |
| `Instantiate(scene)` | `read(memory,scene.uid,use,shell) ∪ create(tree,new_id,create,shell) ∪ occupy(memory,scene.est_size,create,shell)` | 三动作。**泄漏检测核心**：occupy(memory,create) 须配 QueueFree 的 occupy(memory,..,move) 释放（Iter08 I8-01）。**new_id 为动态 id ⇒ 编译期 Unknown**（ED-004），冲突保守。 |
| `Preload(path)` | `read(disk,path,use,shell) ∪ occupy(memory,estSize,create,global)` | 同 Load。 |

- **构成性**：`Load` 后 `Instantiate`（用 loaded scene）→ `read(memory,scene.uid,use)` 与 Load 的 `occupy(memory,estSize,create,global)`：read use + occupy create 同 memory 资源不同 mode ⇒ `Compatible(use,create)=true`（§3.2.3 第2析取）⇒ 兼容。discharged（条件：Claim 相等）。

## H2. §7.5 信号映射性质

| API | Claim 集合（L465-468） | 性质/一致性 |
|-----|-----------|------------|
| `EmitSignal(...)` | `write(signal_bus,signal,create,shell) ∪ read(tree,"subscribers_"+signal,use,shell)` | 写信号总线+读订阅者。`create` 模式写信号——信号发射为何 create？语义指「本次发射事件」创建于总线，合理但 mode 用 create 使其与 Disconnect 的 release 不成对（发射非绑定）。 |
| `Connect(...)` | `write(self,"signal_"+signal,create,shell) ∪ occupy(callback,callable.size,create,shell)` | 绑定：写自身信号槽 + 占回调。**泄漏**：occupy(callback,create) 须配 Disconnect 的 occupy(callback,release)。 |
| `Disconnect(...)` | `write(self,"signal_"+signal,release,shell) ∪ occupy(callback,callable.size,release,shell)` | 释放。**mode=release（正确）**——与 QueueFree 的 move 不一致（对照 Iter08 I8-01）。 |
| `IsConnected(...)` | `read(self,"signal_"+signal,use,shell)` | 单读。 |

- **Connect/Disconnect 配对**：occupy(callback,create)+occupy(callback,release) 同资源 ⇒ net 抵消（若 size 同）。`Compatible(create,release)` 未定义（Iter08 PO-I8-d / Iter16）→ 并行安全不明。

## H3. §7.6 渲染映射性质

| API | Claim 集合（L474-475） | 性质/一致性 |
|-----|-----------|------------|
| `DrawMesh(mesh,mat,transform)` | `read(gpu,mesh.buffer_id,use) ∪ write(gpu,command_buffer,create) ∪ read(gpu,material.shader_id,use)` | 读两 GPU 资源+写命令缓冲。**命令缓冲 `command_buffer` 是合成资源**（非 §3.1.2 ResourceId 显式构造子）⇒ resource 命名空间扩展未定义（Iter01 I1-02 跨构造子）。 |
| `DrawRect(rect,color)` | `write(gpu,command_buffer,create)` | 单写命令。 |
| `SetMaterialOverride(material)` | `write(self,"material",use) ∪ read(gpu,material.shader_id,use)` | 写自身+读 GPU 材质。 |

## H4. §7.7 音频映射性质

| API | Claim 集合（L482-484） | 性质/一致性 |
|-----|-----------|------------|
| `Play(stream)` | `write(audio_mixer,self.channel_id,create) ∪ read(memory,stream.buffer_id,use) ∪ occupy(audio_channel,1,create)` | 占音频通道(size=1)+写混音器。**泄漏**：occupy(audio_channel,1,create) 须配 Stop 的 release。 |
| `Stop()` | `write(audio_mixer,self.channel_id,release) ∪ occupy(audio_channel,1,release)` | 释放（mode=release 正确）。 |
| `SetVolumeDb(vol)` | `write(audio_mixer,self.channel_id,use)` | 单写 use。 |

- **Play/Stop 配对**：occupy(audio_channel,1,create)+release 抵消 ⇒ net 守恒当每 Play 配 Stop。泄漏检测依赖 Stop 路径存在（SH-004/ED-004）。

## H5. §7.8 输入 / §7.9 网络 / §7.10 动画

- **输入** `IsActionPressed/JustPressed` `{read(input,action,use)}`、`GetMousePosition` `{read(input,"mouse",use)}`：纯读，幂等，无占用。性质 clean。discharged（结构）。
- **网络** `Rpc(method,args)` `{write(network,self.id+"/"+method,create) ∪ read(memory,args.size,use)}`、`RpcId(peerId,..)` 同形。`self.id+"/"+method` 字符串拼接造 resource id ⇒ 编译期 Unknown（MA-010）⇒ 任意两 Rpc 保守冲突（精度，R-3）。**网络无 release/occupy 配对**（调用不占持久资源，合理）。
- **动画** `Play` `{write(self,"animation",create) ∪ read(memory,animName,use) ∪ occupy(animation_state,1,create)}`、`Stop` `{write(self,"animation",release) ∪ occupy(animation_state,1,release)}`。Play/Stop 配对该 release，合理（mode=release 正确，对照 QueueFree 的 move 不一致）。
- **Seek** `write(self,"animation",use)` 单写 use。

## H6. 跨 §7.4-7.10 的系统性缺口

**(PO-I9-a) occupy 的 scope 不统一（open，高）**：memory 占用在 Load/Preload 标 `global`，在 Instantiate 标 `shell`，在 Connect/Play/动画标 `shell`。同一物理内存被两个 scope 标注 ⇒ Peak/net 在 global vs shell 下不并（Iter15 ⊆ 未定义 ⇒ 不并更糟）。泄漏检测（DO-9）的 net 跨 scope 无法守恒。

**(PO-I9-b) size 估算精度（open）**：`estimatedSize(T)`/`scene.est_size`/`callable.size`/`stream.buffer_id` size 全为估计/运行时值（MA-005/MA-008）→ peak 求和为估计值，与预算（§12.2 AUDIT003 默认 512MB）比较的偏差无界证明（Iter11 RT-002）。

**(PO-I9-c) 合成 resource 命名空间（open）**：`command_buffer`/`signal_bus`/`"subscribers_"+signal`/`"material"` 等非 §3.1.2 构造子 ⇒ resource 命名空间未封闭，未知资源可能与之冲突或遗漏（Iter01 I1-02）。

**(PO-I9-d) 网络 resource 编译期 Unknown（open，精度）**：`self.id+"/"+method` ⇒ 任意两 Rpc 保守冲突（MA-010，R-3）。

## H7. 可消解 proof obligation（履行尝试）

- **P1（discharged，条件）**：给定 Connect/Disconnect、Play/Stop、动画 Play/Stop 的 occupy create/release 配对且 size 相同，则 net 守恒（泄漏检测可行）。证明：net 公式线性，配对抵消。前提：QueueFree 路径也用 release（Iter08 PO-I8-a 未立 ⇒ 条件）。
- **P2（discharged）**：纯读 API（输入、GetNode、LoadInteractive、IsConnected）的 Claim 幂等且对 ∪ 不增冲突。证明：单 read use + Compatible(use,use)=true（§3.2.3 第1析取）。

---

## Proof Obligation 账本（Iter09）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I9-a | occupy scope 不统一(global/shell) | open(高) | 统一内存占用 scope | L456,459,L458 |
| PO-I9-b | size 估算精度无界 | open | 见 MA-005/RT-002 | L456-459 |
| PO-I9-c | 合成 resource 命名空间 | open | 封闭 resource 枚举 | L474,L465 |
| PO-I9-d | 网络 resource 编译期 Unknown | open(精度) | 见 MA-010 | L498-499 |
| PO-I9-e | Instantiate new_id Unknown | open(精度) | 见 ED-004 | L458 |

## 本轮新发现未消解缺口（I9- 前缀，全局唯一）
- **I9-01（高）**：memory 占用 scope 在 Load/Preload(global) 与 Instantiate/Connect/Play(shell) 不一致 ⇒ net/Peak 跨 scope 不并，泄漏检测可能失准。
- **I9-02**：QueueFree 的 occupy(memory,..,move) 与 Connect/Play/Stop/动画的 release 释放 mode 不一致（前者 move、后者 release）⇒ 同是释放动作，mode 不统一，net 与 Compatible 判定分裂（交叉 Iter08 I8-01）。
- **I9-03**：`estimatedSize(T)`/`scene.est_size` 等 size 为估计值，peak 与预算比较偏差无界（交叉 Iter11）。
- **I9-04**：`command_buffer`/`signal_bus`/`"subscribers_"+signal` 等合成 resource 不在 §3.1.2 枚举，命名空间未封闭。
- **I9-05**：网络 `Rpc` resource=`self.id+"/"+method` 编译期 Unknown ⇒ 任意两 Rpc 保守冲突（精度，R-3）。
- **I9-06**：Instantiate 的 `new_id` 动态 ⇒ Unknown（ED-004），泄漏/冲突保守。
- **I9-07**：音频/动画 `occupy(X,1,...)` 单位 size 与 GPU 命令数在 peak 求和混算量纲（DO-7，Iter14）。

一句话摘要：§7.4-7.10 映射结构合法，但 occupy 的 global/shell scope 不统一（I9-01，高，阻断 net/Peak 跨 scope 守恒）、QueueFree 与 Connect/Play 释放 mode 分裂（I9-02）、合成 resource 未封闭（I9-04）、网络 resource 编译期 Unknown（I9-05）四处为 open。
