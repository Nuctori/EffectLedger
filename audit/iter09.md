# Iter09 独立审计

- **范围**：§7.4–§7.10（资源加载 / 信号 / 渲染 / 音频 / 输入 / 网络 / 动画）的 Claim 映射，交叉核对 §3.1.2/3.1.2b/3.1.3/3.1.3b/3.1.4a/3.2.2/3.2.3/3.3.1。
- **证据边界**：仅依据 `PDR_Effect_Cost_Algebra_v3_FINAL.md` 磁盘内容（v3.0-FINAL-rA6），行号以该文件为准。
- **结论摘要**：
  1. §7 全部裸资源名**可机械归一**到 §3.1.2 构造子（L186–189 映射表闭合），枚举覆盖性 **discharged**；
  2. 但 §3.1.3b 偏序定义存在**内部矛盾**（L154–155 双向包含规则摧毁反对称性与传递性），使 `net(S,scope)` 的作用域过滤退化为恒真，**所有依赖 scope 过滤的守恒命题均无法按现文证明**；
  3. 归一化的固定字段折叠（`Memory(uid="mem")`、`Callback("cb")`、`Occupancy("audio")`、`Occupancy("animation")`，L186–188）与 §7 各表隐含的 per-instance 语义冲突：并行组合下产生系统性 (create,create)∈CONFLICT 误报，且 net 守恒出现**跨对象虚假抵消**；
  4. Connect/Disconnect、Play/Stop 配对在「同 scope + 同 size」前提下可条件证 Global 级守恒；跨方法配对（_Ready 连接 / _ExitTree 断开）在现文下必然误报泄漏——文档无 scope 提升规则，**open**。

---

## 逐命题小节

### P1 裸资源名归一完备性

- **命题**：§7.4–§7.10 出现的每个裸资源名（disk/memory/tree/self/physics/gpu/command_buffer/audio_mixer/audio_channel/callback/network/input/signal_bus/signal_*）均可经 L177–189 的归一规则映射到 §3.1.2/3.1.2b 枚举中的唯一构造子。
- **数学性质**：全函数性：∀name ∈ Names(§7.4–7.10), ∃!constructor c: name ↦ c(fields)。
- **状态**：discharged
- **论证**：逐名核验——disk⇒Disk(L186)、memory⇒Memory(L186)、gpu/command_buffer⇒CommandBuffer("gpu")(L181/L186)、audio_mixer⇒AudioMixer(L187)、audio_channel⇒Occupancy("audio")(L187)、callback⇒Callback("cb")(L188)、animation_state⇒Occupancy("animation")(L188)、network⇒Network(L189)、input⇒Input(L189)、signal_bus⇒SignalBus(L178)、Self("signal_"+s)≡SignalBus(s)(L180)。§7.4–7.10 无表外裸名。合成资源 CommandBuffer/SignalBus 均在 ResourceId 枚举内（L120–121），Callback/Occupancy/Input 在 L107–110。枚举封闭。
- **行号**：L96–113, L115–126, L177–189, L637–688

### P2 ScopeId 偏序 ⊑ 良定义性（§3.1.3b）

- **命题**：⊑ 是自反、反对称、传递的偏序，Global 为最大元（L159 自述）。
- **数学性质**：偏序三律 + 最大元唯一性。
- **状态**：open（文档内部矛盾）
- **论证（反例）**：L154 给出 `Global ⊑_any X`，L155 给出 `X ⊑_any Global`。取 X=Method(m)：两条规则同时成立 ⇒ 由反对称应有 Global = Method(m)，而二者是不同构造子，矛盾。由传递性：Loop(id) ⊑ Global ⊑ Method(m)，与 L156 「其余跨标签不可比较」直接冲突。后果：`c.scope ⊆ scope := (c.scope ⊑ scope) ∨ (scope=Global)`（L158）在 ⊑ 全通下退化为恒真谓词，net(S,scope)（L316–318）与 Peak(S,scope)（L338）的 scope 过滤失效，一切 claim 进入一切聚合。**这是本次范围内最严重的形式缺陷**；§7 的 shell_scope/global_scope 区分（ST-04，L138）因此失去判别力。
- **行号**：L141–159, L316–318, L338

### P3 occupy 的 scope 一致性（global_scope vs shell_scope）

- **命题**：§7.4 中 Load/Preload 的 occupy{memory} 标 global_scope（L637/L640），Instantiate 的 occupy{memory} 标 shell_scope（L639），且 QueueFree 的 release{memory} 标 shell_scope（L610），构成一致的生命周期模型。
- **数学性质**：配对可行性：∀create-claim ∃release-claim 使 resource 相等且 create.scope ⊆* release 所在聚合 scope。
- **状态**：open（部分矛盾）
- **论证**：(i) Instantiate↔QueueFree：resource 分别为 Memory("mem") 折叠后相等（见 P5 保留意见），scope 均 Shell ⊑* Global，方向可通——但 size 符号不同：create 侧 `scene.estimated_size`（L639）、release 侧 `self.size`（L610）。文档未给 `scene.estimated_size = self.size` 的任何等式或约束，net(S,Global)=Σ+−Σ 仅在逐对象 size 相等时为零；现文不可证。(ii) Load/Preload 的 occupy{memory, estimatedSize(T), create, global} 在全文（含 §8.1 release-class 清单 L694 起：queue_free/free/remove_child/disconnect/remove_from_group/cancel_free/free_children_in_group）中**不存在任何对应 release**。若按 DO-9（net>0 且无配对即报警，L319）机械执行，则每次 Load 必报泄漏——要么是误报风暴（说明 scope=global + 无释放路径的建模不自洽），要么需要补充「资源卸载/refcount 归零」的 release 映射。二者的选择文档均未做出。
- **行号**：L610, L637–640, L319, L694–709

### P4 合成 resource 名是否在 ResourceId 枚举内

- **命题**：command_buffer、signal_bus（及 callback/audio_channel/animation_state/input/network）均在 ResourceId 枚举内。
- **状态**：discharged
- **论证**：CommandBuffer/SignalBus 由 §3.1.2b 显式入枚举（L120–121）；Occupancy/Callback/Network/Input 由 rA4 扩展入枚举（L107–110）；等价规则 L124–125 与 L178–181 把 §7 用法绑定到这些构造子。无悬空名。
- **行号**：L107–121, L178–181

### P5 固定字段折叠与并行兼容性（Memory("mem")/Callback("cb")/Occupancy 折叠）

- **命题**（隐含于 L186–188）：将裸名 memory/callback/audio_channel/animation_state 归一为固定字段实例不破坏 §3.2.2 并行兼容判定与 net 守恒。
- **数学性质**：Compatible 判定的可靠性：语义上独立的资源实例不应被判 CONFLICT。
- **状态**：open（反例成立）
- **论证（反例组）**：
  - **音频**：两个节点在不同 channel 上并发 Play，各自 emit occupy(Occupancy("audio"), [1,1], create)（L663 经 L187 归一）。resource 相等 ⇒ 按 §3.2.2 需 Compatible(create,create)=false（CONFLICT，L273）⇒ 必报冲突。但引擎语义上不同通道并发播放合法 ⇒ 结构性误报。
  - **动画**：两个实体并发 Play 各自动画，occupy(Occupancy("animation"),1,create)×2（L686 经 L188）同理误报。
  - **回调**：Connect 到信号 A 与 Connect 到信号 B 并发，均折叠为 occupy(Callback("cb"), callable.sizeᵢ, create)（L647 经 L188）⇒ 误报冲突；更糟的是 Disconnect(B) 的 release(Callback("cb"), size_B)（L648）会与 Connect(A) 的 create 在 net 中**跨信号虚假抵消**，使泄漏判定失真。
  - **渲染**：同一帧内 DrawMesh 与 DrawRect 并行（两个 System || 组合），write(CommandBuffer("gpu"), ·, create)×2（L655–656 经 L181）⇒ (create,create) 冲突误报。命令缓冲追加语义应为 use 或专用 append mode；mode=create 的选型无任何论证。
  - **信号**：EmitSignal 写 write(SignalBus(_), signal, create)（L646 经 L178），两次 EmitSignal 并行即冲突误报；且 SignalBus(_) 中 `_` 不是 §3.1.4a 定义的值（字段相等要求逐位相等，L177），故 EmitSignal 的 create 与 Disconnect 的 release(SignalBus(s))（L180 归一）**永不配对**——发射侧 create 无任何 release，多订阅者信号模型与 CONFLICT 集根本不相容（Godot 允许同一信号多连接）。
  - 结论：折叠归一牺牲了 resource 的实例区分度，§3.2.2 在 §7.4–7.10 上的可靠性和 net 的守恒精度均被破坏。
- **行号**：L186–188, L258–262, L273–274, L646–648, L655–656, L663, L686

### P6 size 估算值来源可追溯性

- **命题**：§7.4–7.10 中所有非常量 size 符号均有 §3.1.5 定义的取值来源。
- **数学性质**：∀size 符号 e ∈ {estimatedSize(T), scene.estimated_size, callable.size, args.size, self.size}, ∃d∈{(a)..(d)}(L131–135): size(e) 按 d 良定义。
- **状态**：open
- **论证**：(a) `estimatedSize(T)`（L637）/`estimatedSize`（L640）：无来源定义。§12.2 的 AUDIT002/003 启发式仅覆盖 Texture2D 字段（[64,64] MB，L906）与 .tscn 场景（[s,s]/[1,⊤]），未覆盖任意 T 的 Load。(b) `callable.size`（L647–648）：全文零定义，连量纲（字节？槽位？）都未指明。(c) `args.size`（L679–680）：运行期值；MA-005（L355）称运行时值经 Command 编码，但未给出 RPC 参数的静态区间化规则。(d) `self.size`（L610）与 `scene.estimated_size`（L639）之间无等式约束（见 P3）。常量项 occupy(audio_channel,1)/occupy(animation_state,1)（L663–664, L686–687）按缺省 [1,1] 可 discharged，但整体命题因 (a)–(c) 为 open。
- **行号**：L129–140, L355, L610, L637–640, L647–648, L679–680

### P7 Connect/Disconnect 配对的 net 守恒

- **命题**：Connect 后必有 Disconnect 时，net 不因信号连接产生正漂移。
- **数学性质**：设 C={write(Self(sig_s),create), occupy(Callback(k), m, create)}（scope σ₁），D={write(Self(sig_s),release), occupy(Callback(k'), m', release)}（scope σ₂）。net(S∪{C,D}) 的 callback 分量 = merge(m) − merge(m')，需 m=m' 且两 claim 落入同一聚合。
- **状态**：discharged（带前提的条件证明）
- **前提**：(i) 两侧 callable.size 符号解析为相同 SizeVal；(ii) 两 claim 经 P2 修复后的 ⊆* 落入同一查询 scope（如 Type/Scene 级聚合；Method 级分别聚合时必失配——_Ready 连接/_ExitTree 断开的常见模式在 Method scope 下 net(Method(_Ready))>0，DO-9 必误报，文档无 scope 提升规则）；(iii) Callback 折叠问题（P5）已修复为 per-(signal,callable) 实例，否则跨信号抵消使命题空洞成立（假守恒）。
- **在上述前提下**：resource 归一后两侧相等（L188 + L180），kind=occupy、mode create/release 对称，size 相等 ⇒ 净和为 0（ℕ* 上减法按 §3.3.1 逐项符号和）。∎（条件的）
- **行号**：L312–320, L647–648

### P8 Play/Stop（音频与动画）配对守恒

- **命题**：Play 之后 Stop 时 occupy(audio_channel/animation_state) 守恒。
- **状态**：discharged（条件弱于 P7 的情形更少，但仍受 P2/P5 制约）
- **论证**：size 双方均为字面量 1（L663–664, L686–687），无 P6 问题；resource 归一后相等。前提：(i) 同一实例的 Play/Stop 落入同一聚合 scope（同 P7-ii）；(ii) Occupancy 折叠（P5）修复，否则 A 实体 Play、B 实体 Stop 会假性抵消；(iii) 音频自然播完（Finished 无 Stop 调用）路径无 release 映射——release-class 清单（L700–708）不含音频/动画停止类 API，此类路径下 DO-9 误报，属已知保守方向（fail-closed），须记录而非静默。
- **行号**：L663–665, L686–688, L700–708

### P9 网络 Rpc/RpcId 的 resource 良构性

- **命题**：write(network, self.id+"/"+method, create)（L679）可归一到 Network(peerId:int, method:String)（L109）。
- **状态**：open
- **论证**：L189 的缩写 `network ⇒ Network(peerId, method)` 只适用于裸名；L679–680 实际使用字符串拼接键 `id+"/"+method`，其到二元组 (peerId:int, method:String) 的拆解规则未定义（self.id 是 EntityId/U64，非 peerId:int；Rpc 广播场景甚至无目标 peer）。Claim 相等（L177 要求「构造子标签+字段逐位相等」）对该拼接键不可机械执行 ⇒ §3.2.2 对两个 Rpc 的兼容判定无定义。另：Rpc 为一次性发送，mode=create 且永无 release，两个 Rpc 并行即 (create,create) 冲突误报（同 P5 模式）；read(memory, args.size) 见 P6(c)。
- **行号**：L109, L177, L189, L679–680

### P10 输入映射（对照基准）

- **命题**：§7.8 三条 read(input, action/mouse, use, Shell) 映射自洽。
- **状态**：discharged
- **论证**：Input(action) 构造子存在（L110），action 字符串即构造子字段，逐位相等良定义（L177）；全部 kind=read、mode=use ⇒ Compatible(use,·)=true 恒成立（L274），无 net/Peak 占用贡献。无缺口。作为 §7.4–7.10 中唯一无瑕疵小节。
- **行号**：L110, L274, L671–673

### P11 渲染 SetMaterialOverride 的读写对称性

- **命题**：write(self,"material",use) + read(gpu,shader_id,use)（L657）自洽。
- **状态**：discharged
- **论证**：mode 全 use ⇒ 无兼容/net 问题；Gpu(bufferId) 构造子覆盖 material.shader_id 键（L103）。备注：写自身材质标 use 而非 create 与 §7.2 Position setter 的 use 一致（属性写入统一 use），风格一致，非矛盾。
- **行号**：L103, L617–621, L657

### P12 文档内部一致性：ScopeId 术语表 vs §3.1.3

- **命题**：术语表 ScopeId 条目与 §3.1.3 一致。
- **状态**：asserted（发现轻微不一致，非阻塞）
- **论证**：术语表 L838 列「method, type, scene, global, loop, conditional, async」，遗漏 §3.1.3 的 `Shell` 构造子（L138）。不影响机械判定（权威在 §3.1.3），记为编辑级不一致。
- **行号**：L138, L836–838

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ------ | ------ |
| PO-09-01 | ⊑ 为偏序（自反/反对称/传递），Global 最大元 | open | 重写 L154–155：拆分「claim-scope→query-scope」单向可见关系与偏序；删除双向 ⊑_any 或改为单条 `X ⊑ Global`；重证三律 | L154–159 |
| PO-09-02 | Load/Preload occupy{memory,global} 有 release 配对（DO-9 不误报） | open | 补资源卸载/refcount-release 映射（如 `Resource.unref ⇒ occupy(memory,·,release,global)`）或在 §8.1 明示豁免理由并改 fail-open 依据 | L637–640, L700–708 |
| PO-09-03 | scene.estimated_size = self.size（Instantiate↔QueueFree size 对齐） | open | 补一条符号等式/约束：QueueFree 的 release size ≡ 该实体 Instantiate 时的 create size（或经 [Budget] 绑定） | L610, L639 |
| PO-09-04 | 折叠归一（Memory("mem")/Callback("cb")/Occupancy("audio"/"animation")）不破坏 §3.2.2 可靠性 | open | 将固定字段改为参数化实例（Memory(uid)、Callback(signal,callable)、Occupancy(node,channel)），或在 §3.2.2 引入「同类多实例」容量语义 | L186–188, L273 |
| PO-09-05 | 多订阅者信号（Connect×n、EmitSignal×m 并行）不被 CONFLICT 误判 | open | 为 signal/callback 类 claim 定义计数型 mode 或把 SignalBus 写操作改为 use+计数；显式处理 SignalBus(_) 通配字段的相等规则 | L178, L273, L646–649 |
| PO-09-06 | estimatedSize(T) 取值来源 ∈ §3.1.5 (a)–(d) | open | 补 T→SizeVal 的来源表（类型默认区间 / [Budget] 标注 / 动态 [1,⊤]） | L129–135, L637, L640 |
| PO-09-07 | callable.size 良定义（载体+量纲） | open | 补 SizeVal 来源（如每连接 [1,1] 槽位或字节区间），并保证 Connect/Disconnect 双方可计算相等 | L647–648 |
| PO-09-08 | args.size（RPC）静态区间化 | open | 补规则：可序列化 DTO ⇒ 按字段静态求区间；否则 [0,⊤] | L355, L679–680 |
| PO-09-09 | Rpc 字符串键 → Network(peerId,method) 机械拆解 | open | 改 §7.9 为结构化 Network(self.id, method)/Network(peerId, method)，废除拼接键 | L109, L679–680 |
| PO-09-10 | 跨方法 Connect/Disconnect、Play/Stop 配对在细粒度 scope 下不误报 DO-9 | open | 补 scope 提升（promotion）规则：生命周期配对的 claims 聚合至最近公共 enclosing Type/Scene scope | L316–320, L647–648, L663–664 |
| PO-09-11 | CommandBuffer 追加语义与 mode=create 兼容 | open | Draw*/SetMaterial 命令缓冲写入改 mode=use（或引入 append mode），并补一帧结束的缓冲清空建模 | L655–656 |

---

## 新发现缺口清单

1. **【高】§3.1.3b 偏序自毁**（PO-09-01）：L154–155 双向包含使全部 ScopeId 等价，⊆* 恒真，net(S,scope)/Peak(S,scope) 的 scope 参数失效；rA2 的 ST-04 收口（shell_scope⇒Shell）因此空转。此为 iter09 范围内唯一动摇既有收口结论的新根因。
2. **【高】折叠式归一的系统性误报/假守恒**（PO-09-04/05）：`Memory("mem")`、`Callback("cb")`、`Occupancy("audio"/"animation")` 的固定字段把引擎中互异的实例压成单一 resource，令 §3.2.3 CONFLICT 在 §7.5/7.6/7.7/7.10 上必然触发误报，同时令 net 出现跨对象抵消式的「假守恒」。rA4 的 iter51 #3 收口只解决了「枚举覆盖」，未解决「实例区分度」。
3. **【高】Load/Preload 无释放路径**（PO-09-02）：§7.4 的 global_scope occupy 与 §8.1 release-class 清单不相交，DO-9 机械执行 ⇒ 所有资源加载皆报泄漏；文档未在「补映射」与「声明豁免」间做选择。
4. **【中】SignalBus(_) 通配字段**（PO-09-05）：L178 使用非值占位 `_`，与 L177「字段逐位相等」冲突，导致 EmitSignal 与 Disconnect 的资源永不相等。
5. **【中】网络映射形参错位**（PO-09-09）：§7.9 字符串拼接键无法落入 Network(int,String)，Claim 相等对该表不可计算。
6. **【中】size 符号来源缺失**（PO-09-06/07/08）：estimatedSize(T)、callable.size、args.size 三个符号游离于 §3.1.5 四条来源之外，违背 §12.2「禁止游离字面量」的自设纪律。
7. **【低】跨方法生命周期配对无 scope 提升**（PO-09-10）：_Ready/_ExitTree 等惯用模式在 Method 粒度聚合下必然触发 DO-9。
8. **【低】负向净变化无检测**：Disconnect/Stop 无配对 Connect/Play 时 net<0，DO-9 仅判 net>0（L319），虚假释放静默通过（不对称性未声明为有意设计）。
9. **【编辑级】术语表 ScopeId 缺 `Shell`**（P12，L838 vs L138）。
10. **【低】音频/动画自然终止路径无 release 映射**：Finished 信号驱动的停止不在白名单，fail-closed 方向正确但应在 §8.1 显式记录为已知保守点。

DONE_ITER_09