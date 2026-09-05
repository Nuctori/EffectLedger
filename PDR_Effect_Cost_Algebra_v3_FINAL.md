
# PDR：效应开销代数化系统 v3.0（Entity-as-Data 架构）

# Preliminary Design Review — 完整版

# 版本：v3.0-FINAL

# 日期：2026-08-20

---

## 1. 设计目标（Design Objectives）

| ID | 目标 | 优先级 | 验收标准 |
| ---- | ------ | -------- | --------- |
| DO-1 | 编译期效应审计 | P0 | 预算违规在 `dotnet build` 时报警，可配置为 error |
| DO-2 | 业务逻辑零 Godot 引用 | P0 | Domain 项目编译失败如果引用 Godot 命名空间 |
| DO-3 | 效应代数组合 | P0 | 基于资源声明集合（Set<Claim>）的统一组合律 |
| DO-4 | 验证周期缩短 | P0 | 80% 测试在 `dotnet test` 完成，不启动 Godot |
| DO-5 | Entity-as-Data 架构 | P0 | Entity = 纯数据 (Id, Components)，System = 纯函数 |
| DO-6 | Release 零开销 | P1 | 审计代码通过条件编译完全剥离 |
| DO-7 | 量纲隔离 | P1 | read/write/occupy 不可混算，编译期报错 |
| DO-8 | 峰值检测 | P1 | 循环内资源分配静态报警 |
| DO-9 | 泄漏检测 | P1 | Instantiate 无对应释放路径静态报警 |
| DO-10 | Shell 同态映射 | P1 | Domain.World ↔ Godot.SceneTree 双向同步，无决策 |
| DO-11 | 社区非侵入发布 | P1 | Audit-only 模式零代码改动即可使用 |

---

## 2. 核心架构

### 2.1 三层架构

```
┌─────────────────────────────────────────┐
│  Domain 层（纯 .NET，不引用 Godot）        │
│  ├── Entity = (Id, Map<ComponentType, Component>)  │
│  ├── Component = readonly record struct（纯数据）   │
│  ├── World = Map<EntityId, Entity> + version      │
│  ├── System = (World, Input) → (World, Command[])   │
│  └── Command = 效应请求（携带预定义 Signature）      │
│  验证：dotnet test（< 1s，100+ 并行）               │
├─────────────────────────────────────────┤
│  Shell 层（引用 Godot + Domain）           │
│  ├── NodeShell = sealed class，继承 Godot.Node      │
│  ├── 职责：Domain.World ↔ Godot.SceneTree 同步      │
│  ├── 约束：无决策、无状态、无循环、< 20 行/方法      │
│  └── 效应：只发生在同步边界（Sync Point）           │
│  验证：godot --headless（5-10s，5-10 并行）          │
├─────────────────────────────────────────┤
│  Audit 层（编译期工具）                     │
│  ├── L1 类型系统：sealed、readonly、struct、泛型约束 │
│  ├── L2 代码生成：Source Generator 字段/方法体检查    │
│  └── L3 语法分析：Roslyn Analyzer 兜底              │
│  验证：dotnet build（实时 IDE 红线 + CI 阻断）       │
└─────────────────────────────────────────┘
```

### 2.2 关键设计决策

| 决策 | 选择 | 理由 |
| ------ | ------ | ------ |
| 效应代数结构 | Set<Claim> + 集合并 ∪ | 统一组合律，无分配律问题 |
| occupy 维度 | Claim 集合，net/peak 是派生度量 | 避免二元组 (net, peak) 的数学不一致 |
| Interop 类型 | MyGame.Interop DTO 项目 | 纯 .NET，无 Godot 引用，Source Generator 自动生成转换 |
| Shell 生成 | Source Generator 自动生成 sealed class | 避免泛型参数爆炸，阻止继承和重写 |
| 运行时验证 | 异步后台线程，分层抽样 | 不阻塞主线程，只采样标记了 [Budget] 的对象 |
| 报警分级 | warning 默认，CI 可配置为 error | 避免误报阻塞开发，发布时严格 |
| EntityId 生成 | 单调 U64 | 简单高效，分布式场景 v2 扩展为复合键 |
| World 存储 | Archetype 分组 SoA（Struct of Arrays） | Bevy 策略，O(1) 查询，swap-remove |
| System 调度 | 自动拓扑排序（基于写集冲突检测） | 编译期确定执行顺序 |
| Component 比较 | 值比较（readonly record struct 自动相等性） | 简单可靠 |
| 社区发布 | Audit-only 模式作为入口 | 零代码改动，非侵入，最大化采用 |

---

## 3. 数学定义层（Mathematical Definition Layer）

### 3.1 基本对象

> **良性定义修订（v3.0-FINAL 修订 A，收口审计 iter01/iter14/iter15/iter21/iter31/iter32/iter34/iter36）**
> 本节把审计发现的所有悬空对象（ResourceId 相等、ScopeId 偏序、Claim 相等/归一、Signature 量纲分桶）正式良定义。所有后续 §3.2/§3.3 的组合律与派生度量均依赖本节。

**定义 3.1.1（Claim）**

```
Claim := (kind, resource, mode, scope, size?)
  kind   ∈ { read, write, occupy }
  resource ∈ ResourceId
  mode   ∈ { use, create, release, move }
  scope  ∈ ScopeId
  size   ∈ SizeVal   // 见 3.1.5，扩展自然数；单值 s 即区间 [s,s]
```

**定义 3.1.2（ResourceId）**

```
ResourceId :=
  | Tree(path: NodePath | Unknown)
  | Self(component: String)
  | Physics(bodyId: RID)
  | Memory(uid: U64)
  | Disk(path: String)
  | Signal(name: StringName)
  | Gpu(bufferId: RID)
  | AudioMixer(channelId: int)
  | Occupancy(channel: String)    // 占用类资源（audio_channel / animation_state 等归一于此）【收口 iter51 #3】
  | Callback(id: String)          // 回调占用资源（§7 Connect 的 callback 归于此）【收口 iter51 #3】
  | Network(peerId: int, method: String)
  | Input(action: String)         // 输入资源（§7.8 的 input 归于此）【收口 iter51 #3/#8】
  | Custom(name: String)
```

**定义 3.1.2b（合成资源命名空间）**【收口 iter31】
为跨系统合成资源，定义两个标准合成 ResourceId 构造子，使 §7.6 GPU、§7.5 信号、§7 命令缓冲等共享资源有唯一标识：

```
ResourceId :=
  | CommandBuffer(channel: String)     // 命令缓冲（默认 "gpu"），§7.6 所有 Draw*/SetMaterial 写入此处
  | SignalBus(name: StringName)         // 信号总线（统一 §7.5 的 signal_bus 与 "signal_"+signal 为同一资源）
  | ...（其余见 3.1.2）
// 等价规则：ResourceId 的构造子标签 + 字段逐位相等即相等（见 3.1.4）；例：
//   write(signal_bus, signal, ...) ≡ write(SignalBus(signal), signal, ...)  // 同一资源，必须按 3.1.2b 归一
//   write(gpu, command_buffer, ...) ≡ write(CommandBuffer("gpu"), ...)       // 同一资源
```

**定义 3.1.3（ScopeId）**
```
ScopeId :=
  | Method(name: String)
  | Type(name: String)
  | Scene(name: String)
  | Global
  | Loop(id: String)
  | Conditional(branch: String)
  | Async(id: String)
  | Shell          // 收口 ST-04：§7 映射表使用的 shell_scope / global_scope 归一到此；shell_scope ⇒ Shell，global_scope ⇒ Global
```

**定义 3.1.3b（ScopeId 偏序 ⊑）**【收口 iter15/iter34：原 §3.2/§3.3 的 `c.scope ⊆ scope` 此前未定义；【QED-A6 定稿：单向包含 + Shell 补表 + Loop 归属决策，收口 iter55 PO-55-03】
定义偏序 ⊑（宽松包含，读为「c 的作用域被 scope 包含」），用于 Peak/peak/net(S,scope) 的过滤：

```
基础偏序（构造子标签相同且字段单调；自反由结构相等承载）：
  Global      ⊑ Global
  Method(m)   ⊑ Method(m)
  Type(t)     ⊑ Type(t)
  Scene(s)    ⊑ Scene(s)
  Loop(id)    ⊑ Loop(id)
  Conditional(b) ⊑ Conditional(b)
  Async(id)   ⊑ Async(id)
  Shell       ⊑ Shell
  // Shell 行【QED-A6 补表】：实现由自反 Equals 覆盖（Shell 无载荷 record），原表缺行致
  // 「按上表机械查表无未定义项」声明失真（iter55 F4a——§7 全表 scope=Shell，过滤全落未定义对）。
包含层次（单向——Global 为唯一最大元）：
  X           ⊑_any Global       // 任何具体 scope 被 Global 包含（全局聚合含全部）
  // 【QED-A6 定稿】不设反向 Global ⊑ X。原「Global ⊑_any X 且 X ⊑_any Global」双向包含使
  // Method(m) ⊑ Global ⊑ Method(m) 而 Global ≠ Method(m)，直接违反本定义声明的反对称（iter55 F4）。
  // 保留旧行为的唯一出路是取预序商集，届时 Global 与一切具体 scope 同伦、过滤谓词失去区分力——否决。
  // 实现唯一真源：ScopeId.IncludedIn = Equals ∨ (other is Global)（Objects.cs），钉
  // ScopeOrderTests（自反 8 标签 / 反对称 500 随机对含前提覆盖守卫 / 传递真链 / Global 唯一
  // 最大元含「Global ⊄ 任何非 Global」显式枚举 / 跨标签 8×8 不可比 / Shell 与非 Shell 不可比）。
  // 其余跨标签（如 Method(m) 与 Scene(s)，m≠s）不可比较 ⇒ 既不满足 ⊑ 也不满足 ⊆*。
嵌套闭包 ⊆*（用于聚合）：
c.scope ⊆ scope  := c.scope ⊑ scope
// 【QED-A6】原第二析取支 (scope = Global) 冗余删除：X ⊑ Global 恒真已覆盖该情形（iter55 附带瑕疵）。
// 性质：⊑ 自反、反对称、传递 ⇒ ⊆* 为偏序；Global 为唯一最大元。
// 判定：对任一对 (a,b)，按上表机械查表即可，无未定义项。

Loop(id) 宿主归属【QED-A6 二选一定稿：维持跨标签不可比（sound-by-design）】：
  Loop(id) ⋢ Method(m)/Type(t)/Scene(s)（宿主链不进入偏序）。理由：
  (1) 归因点在构造处——Combination.Loop(body, ω, loopScope) 的 loopScope 参数即「循环 claim
      记入哪个宿主 scope」的显式选择点，宿主信息不丢失（传 Method("m") 即按方法聚合）；
  (2) 剧本层审计（EffectScript.Audit）无 scope 过滤（逐事件全累加），不受此口径影响；
  (3) 改「Loop ⊑ 宿主」须给 Loop 记录增加宿主链字段 = 公共类型面变更，与 P1 API 收缩反向——否决；
  (4) 外层聚合不含 Loop 标注 claim 的口径差已由 README 诚实边界 #11（Runtime ⊆* 闸门）与
      EFFECT_SCRIPT 契约面（Loop 非契约 scope）声明。
```

**定义 3.1.4（Signature）**

```
Signature := ImmutableHashSet<Claim>   // 集合语义，幂等由 3.1.4a Claim 相等保证

// 多重性载体（PO-55-01 定稿【QED-A5】）：幂等是刻意性质——集合从不承载计数，且该性质由构造期
// 守卫闭环：归一化后逐字段相同的重复 Claim 在 Signature.Of 处 loud 拒绝（P0-4），
// 「20 次 ∪ 静默坍缩为 1 条 ⇒ net=64」的路径不可表达。多重性仅有两条合法载体：
//   (a) 剧本层：EffectScript.Events 为序列（ImmutableArray），同刻 N 个同构事件 = 审计扫换线 N 次逐条累加（§3.3）；
//   (b) 签名层：Combination.Loop(S, ω) 把 ω 乘进每条 Claim 的 size 端点（§3.2.5），§3.3 的 Σ 在 size×ω 上进行。
```

**定义 3.1.4a（Claim 相等 / 归一化）**【收口 iter01/iter32：原 ∪ 幂等、resource 去重、Deviation 对齐、net(scope) 分组此前依赖未定义的 Claim=】

```
Claim₁ = Claim₂ :⇔ 五元组逐字段相等，其中：
  (1) kind, mode 为枚举，按枚举相等
  (2) scope  按 3.1.3b 的相等（同构造子同字段）
  (3) size   按 3.1.5b 区间相等（[s,s]=[s,s]；缺省 size 视为 [1,1]，故缺省 ≡ 显式 1）
  (4) resource 按如下归一化后相等：
      ResourceId 构造子标签 + 字段逐位相等；且按 3.1.2b 合成命名空间归一（收口 ST-02，信号资源统一）：
        signal_bus            ≡ SignalBus(_)
        "signal_"+s          ≡ SignalBus(s)
        Self("signal_"+s)    ≡ SignalBus(s)      // §7.5 Connect 的 Self("signal_"+signal) 与 EmitSignal 的 SignalBus 判同一资源
        gpu/command_buffer    ≡ CommandBuffer("gpu")
      Unknown 处理（收口 iter21）：resource 字段为 Unknown 当且仅当该字段静态不可判定；
        Unknown 与任意已知 resource 不相等（Unknown ≢ Tree(p)），但与另一 Unknown 相等（Unknown = Unknown）。
      §7 白名单裸资源名 → §3.1.2 构造子规范缩写映射（收口 iter51 #3/#8，使 L628 等裸名可机械归一）：
        memory ⇒ Memory(uid="mem")        disk ⇒ Disk(path)        physics ⇒ Physics(bodyId)
        gpu ⇒ Gpu(bufferId)               command_buffer ⇒ CommandBuffer("gpu")   （见 3.1.2b）
        audio_mixer ⇒ AudioMixer(channelId)   audio_channel ⇒ Occupancy("audio")
        animation_state ⇒ Occupancy("animation")   callback ⇒ Callback("cb")
        network ⇒ Network(peerId, method)   input ⇒ Input(action)   self ⇒ Self(component)   tree ⇒ Tree(path)
// 常量实例保守合并【QED-A7 定稿，收口 iter55 PO-55-08】：无身份差分资源族（§7 白名单的
//   Callback("cb")/AudioMixer(0) 哨兵、裸名 memory 映射）跨调用点**刻意折叠到单一实例**——
//   实例差分（Connect(sigA) vs Disconnect(sigB)）不在静态白名单承载范围内，泄漏掩蔽（net=0）
//   属已声明盲区，权威判定=运行期 Σnet（§8.1 宪法：静态近似永不豁免运行期权威；README 诚实边界 #20）。
//   §4 JSON 契约面不受影响：ParseResource 强制显式资源 id 且拒裸名 ⇒ 不同 id 即不同资源，无折叠
//   （钉 QedP0A7AliasFoldingPins）。参数化 alias（按实参派生身份）为 F 轨候选，冻结前不实施。
// 后果：∪ 幂等（相同 Claim 合并一次）、resource 去重（同资源多 Claim 可分组）、
//       并行约束 c₁.resource=c₂.resource 良定义、Deviation Σ 按资源对齐良定义。
```

**定义 3.1.4b（Signature 按 kind 分桶）**【收口 iter14/iter36：DO-7 量纲隔离的计算性落地】
为在派生度量中机械执行 DO-7（read/write/occupy 不可混算），Signature 逻辑分桶：

```
Signature := Sig_read ∪ Sig_write ∪ Sig_occupy     // 三个不相交桶，按 Claim.kind 划分
peak/net/read/write 仅在同桶内聚合；跨桶相加需显式 weight（见 3.3.2b），否则编译期报错（KIND_MIX）。
// 单桶 ImmutableHashSet<Claim> 仍是底层存储，分桶为聚合时的类型层约束。
```

**定义 3.1.5（SizeVal：扩展尺寸载体）**【收口 iter02/iter18/iter26/iter33/iter46】
统一 size 的多种口径为单一载体，消除审计发现的 ≥5 种 size 口径冲突（见 §12.2 收口）：

```
SizeVal := Interval  其中 Interval := [lo, hi], lo,hi ∈ ℕ* , lo ≤ hi
  ℕ* := ℕ ∪ { ⊤ }                      // 扩展自然数，⊤ 为上界标记（非 IEEE ∞，不崩溃）
  单值 s ⇔ [s,s]；缺省 size ⇔ [1,1]
size 取值来源统一：
  (a) 代数缺省                 → [1,1]          （原 §3.1.1 默认 1）
  (b) 显式精确资源（.tscn/标注）→ [s,s]          （如 occupy{memory,64MB}）
  (c) 动态 Instantiate 变量场景 → [1, ⊤]          （原 ED-004 的 ∞，此处为 [1,⊤]，上界开放）
  (d) AUDIT002/AUDIT003 启发式 → 仍用 (a)(b)，禁止游离字面量（见 §12.2）
```

> **引擎事实核对（2026-08-20，godotengine/godot 源码 + 官方文档）**【收口 B1/B2】
> Godot 引擎**无全局对象数 / 节点数 / VRAM / 内存硬上限**——仅 GPU 相关的纹理尺寸软上限（桌面 ~8192²、移动 ~4096²，见 3D Rendering Limitations 文档，且为 GPU 能力非引擎强制）。
> 因此：(c) 动态 `Instantiate` 标 `[1,⊤]` 的 ⊤ 上界标记**保守成立，无需收窄为具体常量**；若未来需更紧上界，可由项目设置（ProjectSettings）显式给出预算常量收窄 ⊤，但引擎本身不提供。size 载体维持 ℕ*∪{⊤}。

**定义 3.1.5a（⊤ 在 +/×/max/min/compare 上的运算律）**【收口 iter18/iter35/iter45：MA-002 ∞ 闭包】

```
加法：   x + ⊤ = ⊤ ； ⊤ + ⊤ = ⊤
乘法：   x × ⊤ = ⊤ (x>0) ； 0 × ⊤ = ⊤ （保守，标记未知） ； ⊤ × ⊤ = ⊤
max：    max(x, ⊤) = ⊤ ； max(⊤, x) = ⊤
min：    min(x, ⊤) = x ； min(⊤, x) = x
compare：∀x∈ℕ*, x < ⊤ ； ⊤ = ⊤ ； 无 x > ⊤
// 约定：⊤ 表示「上界未知/需人工界定」，任何聚合遇 ⊤ 返回 ⊤ 而非 NaN/发散。
```

**定义 3.1.5b（区间相等与合并）**【收口 iter46】
```
[a,b] = [c,d] :⇔ a=c ∧ b=d
merge_I([a,b],[c,d]) := [min(a,c), max(b,d)]   // join-semilattice（幂等/交换/结合）
// merge_I 的 min/max 对 ⊤ 分量直接套用 §3.1.5a 运算律（min(x,⊤)=x；max(x,⊤)=⊤）【收口 iter51 #7】
```

**定义 3.1.5c（DeviationVal：偏差载体）**【收口 ST-03 / iter51 #2】
```
DeviationVal := double ∪ { ⊤ }
// ⊤ 表示「偏差不可校准/需人工界定」：§9.1 CalculateDeviation 在任一端 size 为 ⊤（上界未知）时返回 ⊤，
//   调用方视 ⊤ 为需人工界定，不触发普通 0.2f 数值报警（避免掩盖，见 §9.1 / §8.3.2）。
// 数值比较：仅当 DeviationVal 为 double 时与阈值比较；⊤ 不进入数值比较（先判 ⊤ 再比 double）【收口 iter51 #4】
```

### 3.2 组合律

> **良性定义修订（v3.0-FINAL 修订 B，收口审计 iter16/iter22/iter25/iter32/iter35/iter46）**
> 全部组合算子改用 §3.1 的 Claim= / SizeVal / 偏序 ⊆*，闭合原文悬空的幂等、配对、∞ 载体。

**定义 3.2.1（顺序组合）**
```
(S₁ ; S₂) = S₁ ∪ S₂
  // ∪ 为集合并，幂等由 3.1.4a Claim 相等保证（相同 Claim 合并一次，无重复项）
  // 多重性注记【QED-A5】：幂等刻意而非缺陷——组合算子不引入计数语义；计数走 §3.2.5 (S×ω) 的
  // size×ω 或剧本层多事件（§3.1.4 载体注记）。重复 Claim 无法经 ∪ 进入（Signature.Of 构造期拒绝）。
```

**定义 3.2.2（并行组合）**
```
(S₁ || S₂) = S₁ ∪ S₂
  需满足：∀c₁ ∈ S₁, c₂ ∈ S₂, c₁.resource = c₂.resource ⇒ Compatible(c₁.mode, c₂.mode)
  // c₁.resource = c₂.resource 现由 3.1.4a 良定义（含合成命名空间归一与 Unknown 处理）
```

**定义 3.2.3（Compatible：全函数 + 对称）**【收口 iter16/iter22/iter25：原定义非对称、create+create 未识别、use∧Unknown 短路不一致】
```
// 完整 16 对有序组合（mode × mode），定义为全函数，无未覆盖对：
Compatible(m₁, m₂) :=
  (m₁ = use) ∨ (m₂ = use)                       // use 为最弱共享权限，与任意 mode 兼容
  ∨ (m₁ = create ∧ m₂ = release) ∨ (m₁ = release ∧ m₂ = create)   // 创建-释放配对（生命周期良性）
  ∨ (m₁ = create ∧ m₂ = move)   ∨ (m₁ = move ∧ m₂ = create)       // 创建-转移良性
  ∨ (m₁ = release ∧ m₂ = move) ∨ (m₁ = move ∧ m₂ = release)       // 释放-转移良性
// 不兼容集（显式，闭合性可机械验证）：CONFLICT := {(create,create),(move,move),(release,release)}
//   Compatible(m₁,m₂) ⇔ (m₁=use)∨(m₂=use)∨((m₁,m₂)∉CONFLICT)
// 性质：
//   (P1) 对称：Compatible(m₁,m₂)=Compatible(m₂,m₁)
//   (P2) 全函数：16 对全部覆盖，无未定义项
//   (P3) create+release 配对不再误判冲突（修正 iter23 的良性生命周期误判）
//   (P4) mode=Unknown 按 use 处理（最弱兼容，fail-open）【QED-A3 定稿，收口 iter55 PO-55-05/06；
//        原「fail-closed 为保守兼容」措辞自相矛盾，废止】。Unknown 的三维契约：
//        net/peak 按占用 +size 保守计入（§3.3.1——泄漏/峰值检测不静默）；Compatible 按最弱兼容
//        放行（本节）。fail-closed（Unknown 对任意 mode 报冲突）被否决：白名单工具的未映射 API
//        是常态，逐一报冲突 = 警报洪水 ⇒ 用户批量 [EffectOverride] ⇒ 工具失效；且 mode 未知时
//        断言冲突是对未知命题下结论。钉：CompatibleMatrixTests 25 组合矩阵（含全部 (Unknown,*)）
//        + QedP0A3UnknownSemanticsPins（扫换线端到端：gate(3) 不报、gate(1)/(2) 仍计）。
```

**定义 3.2.4（条件组合）**
```
(if b then S₁ else S₂) = Signature(b) ∪ (S₁ ⊔ S₂)
  // Signature(b) := ∅【QED-A8 定稿 PO-55-10】：b 为纯 Bool 谓词（无效应求值），条件/循环卫语句
  //   自身不产生 Claim；带效应的卫语句属 §14 工具层控制流建模，不在 L1 代数记法内（while 同此）。
  ⊔ : Signature × Signature → Signature   // join-semilattice 合并（收口 iter46：原「半环」措辞错误）
  ⊔ 按**四元组投影键** (kind, resource, mode, scope) 配对 S₁/S₂ 中同键 Claim——size 不参与配对、只参与合并，
  对同键二者的 size 区间取 merge_I（3.1.5b）：
    同键 c₁,c₂ ⇒ ⊔ 输出 size = [min(c₁.lo,c₂.lo), max(c₁.hi,c₂.hi)]
    仅一侧出现的键：size = 其单值区间 [s,s]
  // 【QED-A8 修正，收口 iter55 PO-55-04】：原「按 Claim 相等（3.1.4a）配对」把 size 卷进配对键 ⇒
  // 跨分支不同 size 的 Claim 不被配对，merge_I 输入永不存在（自吞定义，iter55 F3 反例
  // [64,64]⊔[128,128] 产两条独立 Claim）。实现唯一真源 Signature.Join 键即四元组+Merge（Objects.cs）。
  // 性质：⊔ 是 join-semilattice 的 join（幂等/交换/结合），非半环（无第二运算+分配律）
  // 输出类型闭合：区间 size 仍属 SizeVal（3.1.5），可落回 Signature（3.1.4）
```

**定义 3.2.5（循环组合）**【收口 iter18/iter35：ω 载体 + S×ω 算子】
```
ω ∈ ℕ ∪ { ⊤ }     // 循环次数；静态未知 ⇒ ⊤（上界标记，非发散）
(S × ω) := Scale(S, ω)   // 【PO-55-02 定稿 QED-A5，弃 Σ-copies/max-over-copies 叙事：同构副本的
//   max/Σ 逐副本相等使 ω 退化为死变量（iter55 F2）；实现唯一形态即 size×ω 端点乘法，钉
//   LoopCombinationTests.Loop_FiniteOmega_ScalesPeakByOmega（ω=5 ⇒ 50，错实现必红）】
  //   逐 Claim：size := [lo×ω, hi×ω]（§3.1.5a × 律内嵌 ⊤：ω=⊤ 或 hi=⊤ ⇒ [lo,⊤] 上界开放；lo 恒有限）
  //   scope := loopScope（**替换语义**【QED-A8 定稿 PO-55-11】：循环 claim 的归属 scope 由调用方在
  //     Combination.Loop 的 loopScope 参数显式选择——传 Loop("L") 即循环局部、传 Method("m") 即按方法
  //     聚合；非「叠加注记」读法，iter55 的两种读法歧义由此消解）；kind/resource/mode 不变，计数进 size 量纲（§3.3 的 Σ 对缩放后 size 求和）
  ω=⊤ 时：size := [lo, ⊤]（上界开放），供 Peak/net 以 ⊤ 兜底（见 3.3）
(while b do S) = Signature(b) ∪ (S × ω)
// 历史残留的 cardinality 形式 Peak = max_i |{c∈copy_i(S) | c.scope⊆scope ∧ c.mode≠release}|
//   （仅计 claim 数、不含 size 求和）属早期定义，已被 §3.3.2 的 size-求和 Peak 取代；以 §3.3.2 为准【收口 iter51 #6】
Peak(S, scope) = (size-求和形式，见 §3.3.2)；ω=⊤ ⇒ Peak 返回 ⊤（上界标记，不发散）   // 收口 iter35/iter45 MA-002
```

### 3.3 派生度量

> **良性定义修订（v3.0-FINAL 修订 C，收口审计 iter27/iter33/iter37/iter44/iter45/iter47/iter49）**
> 统一 Peak/peak（消除两个量纲不等价的 Peak 定义，iter49）、net 改 net(S,scope) 分组（iter37）、weight 函数显式定义（iter47）、size 用 SizeVal 故 ∞ 输入返回 ⊤ 不 NaN（iter45）、Deviation 分母下界（iter33）。

**定义 3.3.1（净变化 Net）**【收口 iter27/iter37/iter44：net 漏释放 + 默认规则不产 occupy 致守恒不可证】
```
// 基础定义（全局聚合，保留原语义）：
net(S) = Σ_{c∈S, c.kind=occupy, c.mode∈{create,move,unknown}} c.size
       − Σ_{c∈S, c.kind=occupy, c.mode=release}           c.size
  // size 为 SizeVal（3.1.5），+∞ ⇒ ⊤（不 NaN）；缺省 [1,1]

// 作用域分组（收口 iter37 root 6，依赖 3.1.4a Claim= + 3.1.3b ⊆*）：
net(S, scope) = Σ_{c∈S, c.scope⊆scope, c.kind=occupy, c.mode∈{create,move,unknown}} c.size
             − Σ_{c∈S, c.scope⊆scope, c.kind=occupy, c.mode=release}           c.size
// 【QED-A3 定稿 PO-55-06】正部含 mode=Unknown：未映射 API 的占用按 +size 保守计入（实现扫换线/
//   闭包恒「非 release 为正」；iter55 推断的「贡献恒 0」是本公式旧形 mode∈{create,move} 的遗漏——
//   修公式而非实现）。改「Unknown ⇒ ⊤ 上界」被否决：有限 cap 下峰值门必爆 = 未映射 API 警报洪水
//   （同 §3.2.3 QED-A3 注）。钉 QedP0A3UnknownSemanticsPins（Unknown 占用 Leak/Present、冲突不报）。
// 泄漏判定 DO-9（收口 iter44）：net(S,scope)>0 且 scope 内无对应 release 配对 ⇒ 报警
//   fail-closed：未知映射（3.1.4a 的 Unknown）在 net 中计为 ⊤ 上界，触发「需人工确认」而非静默漏报/误报
// 多重性注记【QED-A5】：Σ 作用于「缩放后」签名（§3.2.5 size×ω）或剧本事件序列（审计扫换线逐事件
//   累加，release 自带于其 Lo 的负向贡献）；对未缩放集合的重复计数不可表达（§3.1.4 P0-4 构造期拒绝）。
// 值域与序【QED-A8 定稿 PO-55-07】：「正和−负和」定义在**有符号区间**载体上（实现 SignedNet 单一真源：
//   ZStar := ℤ ∪ {±⊤}；SignedInterval := [lo,hi]，lo,hi ∈ ZStar）。−[a,b] := [−b,−a]；
//   [a,b] ⊕ [c,d] := [a+c, b+d]；net 的减法即正部区间与负部区间的带符号相加。
//   守恒判定：区间**含 0** ⇔ 生命周期闭合（DO-9 的判定谓词，非逐值比较）；负陷判定按 hi<0（扫换线 gate(1)）。
//   预算比较（如 AUDIT003 的 [1280,1280] 与 [512,512]）按区间上界比较（Peak 聚合即 size.hi 求和，§3.3.2；
//   实现扫换线 gate(2) 严格大于方报 PeakExceeded）。⊤ 律沿 §3.1.5a（含 ±⊤ 的溢出⇒保守 ⊤，弃哨兵）。
```

**定义 3.3.2（峰值 Peak）**【收口 iter49：两个 Peak 定义统一量纲】
```
// 原 §3.2.5 的 Peak(循环副本计数) 与 原 §3.3.2 的 peak(作用域 size 求和) 统一为单一概念：
Peak(S, scope) := scope 过滤后对「§3.2.5 缩放后」签名的并发占用 size 上界逐条求和：
  Peak(S, scope) = Σ_{c ∈ S, c.scope⊆scope, c.mode≠release} c.size.hi
  // 【QED-A5】ω 已在 §3.2.5 Scale 乘进 size 端点（[lo×ω,hi×ω]）：ω 份同构副本并发共存 ⇒ 计数
  // 自然入账（ω×s），无需 copy_i 索引（max-over-copies 公式因其 max 退化已废，见 §3.2.5 定稿注）
  // size 为 SizeVal；ω=⊤ ⇒ Scale 产 [lo,⊤] ⇒ 该项 ⊤；任意 size 含 ⊤ ⇒ 该项和返回 ⊤（不发散/不 NaN）

// weight 函数（收口 iter47 MA-007，原「已解决」依赖的未定义函数，现显式定义）：
weight : Kind × Kind → ℝ ∪ { ⊥ }
  weight(read,read)=weight(write,write)=weight(occupy,occupy)=1
  weight(k₁,k₂)=⊥   ∀k₁≠k₂           // 跨 kind 禁止混算（DO-7 计算性落地）
  // 若日后需跨 kind 统一预算（如 occupy{memory MB} 折算 read{bandwidth}），须在此显式扩展特定对并文档化查表
// 聚合公式（含 weight）：
Peak(S, scope) = Σ_{c∈S, c.scope⊆scope, c.mode≠release} weight(c.kind,c.kind)·c.size.hi
  // 同 QED-A5：S 为 §3.2.5 缩放后签名（ω 已在 size 内），故 Σ 无 copy_i 索引
  // 同 kind ⇒ ×1；跨 kind ⇒ ×⊥ ⇒ 编译期 KIND_MIX 报错（配合 3.1.4b 分桶）
```

**定义 3.3.3（读/写量）**
```
read(S)  = Σ_{c∈S, c.kind=read}  c.size   // size 为 SizeVal，求和带回退 ⊤
write(S) = Σ_{c∈S, c.kind=write} c.size
```

### 3.4 审计发现（数学层）

| ID | 发现 | 严重程度 | 状态 | 收敛方案 |
| ---- | ------ | --------- | ------ | --------- |
| MA-001 | IndexExpr 缺少 Min/Scale | 中 | 已解决（修订 A 语义层补完） | 构造层 Set<Claim>；Min/Scale 语义迁移到 size，size 代数现已由 §3.1.5 SizeVal + §3.1.5a ⊤ 闭包良定义 |
| MA-002 | ∞ 的代数性质未定义 | 中 | 已解决（修订 A） | §3.1.5a 定义 ℕ*/⊤ 运算律；§3.2.5 ω∈ℕ∪{⊤}；Peak/net/⊔/Deviation 在 ⊤ 返回上界标记（原「∞ 作循环标记」虚标已修正）|
| MA-003 | 并行 read 去重 | 低 | 接受（修订 A 前提补完） | 并行 read 累加依赖 Compatible(use,use)，现 §3.2.3 全函数+对称已良定义 |
| MA-004 | occupy 峰值与净变化混淆 | **高** | **已解决** | 采用 Set<Claim>，net 和 peak 是派生度量，非原语 |
| MA-005 | 索引替换与运行时值映射 | 中 | 已解决 | Claim 的 size 是编译期常量或类型参数，运行时值通过 Command 编码 |
| MA-006 | ⊔ 不是半环乘法 | **高** | **已解决（修订 A，措辞修正）** | ⊔ 重写为 join-semilattice（§3.2.4），「半环」误称已纠正；原「放弃半环」与残留 ⊔ 区间语义矛盾已消解 |
| MA-007 | 量纲转换缺失 | 低 | 已解决（修订 A） | weight:Kind×Kind→ℝ∪{⊥} 已显式定义（§3.3.2），同 kind=1、跨 kind=⊥ 使 DO-7 计算性落地 |
| MA-008 | Claim 的 size 为可选 | 中 | 已收敛（修订 A 语义层补完） | 默认 size=[1,1]；缺省归一/∞ 闭包/区间载体由 §3.1.5 良定义 |
| MA-009 | Compatible 的完备性 | 中 | 已收敛（修订 A 形式化） | §3.2.3 给出 16 对全函数 + 冲突集 CONFLICT，可机械验证（原「仅枚举+单测」升级）|
| MA-010 | ResourceId 的相等性 | 中 | 已收敛（修订 A） | §3.1.4a Claim 相等 + resource 归一化（含 Unknown 处理）；原「与任何资源冲突」修正为分 mode（use 共享放行，create/release 冲突）|

---

## 4. Entity-as-Data 架构定义

### 4.1 核心对象

**定义 4.1.1（EntityId）**

```
EntityId := U64  (单调递增，全局唯一)
性质：单调性、不可伪造、持久性（销毁后不复用）
分布式扩展（v2）：(ServerId: u16, LocalId: u64)
```

**定义 4.1.2（Component）**

```
Component := readonly record struct
约束：
  - 值类型（struct）
  - 不可变（readonly，init-only 属性）
  - 字段类型限制：
    - unmanaged 类型（int, float, bool, Vector3DTO 等）
    - string（特殊豁免，视为不可变值）
    - ImmutableArray<T> where T : IComponentField
    - 其他 IComponent（递归检查，必须扁平化）
  - 禁止：List<T>, Dictionary<K,V>, T[], class, 嵌套对象
```

**定义 4.1.3（Entity）**

```
Entity := (id: EntityId, components: ImmutableDictionary<Type, IComponent>)
性质：不可变性、完整性（至少一个 Component）
```

**定义 4.1.4（Archetype）**

```
Archetype := Set<ComponentType>
Entity 的 Archetype = Entity.components.Keys
World 按 Archetype 分组存储（SoA），查询 O(1)
```

**定义 4.1.5（World）**

```
World := (entities: ImmutableDictionary<EntityId, Entity>, version: U64)
性质：全函数、单调增长、版本号递增
U64 在 60fps 下可用 9.7 亿年，不处理溢出
```

**定义 4.1.6（System）**

```
System := (World, Input) → (World, Command[])
约束：
  - 纯函数：无副作用
  - 确定性：相同输入相同输出
  - 无 Godot 引用
  - 通过 Archetype 查询 Entity 子集
```

**定义 4.1.7（Command）**

```
Command := 
  | SpawnEntity(Archetype, ImmutableDictionary<Type, IComponent>)
  | DestroyEntity(EntityId)
  | SetComponent(EntityId, Type, IComponent)
  | EmitEvent(EventName, ImmutableDictionary<String, Value>)
  | LoadResource(ResourcePath, ResourceType)
  | PlaySound(SoundId, Volume)
  | SpawnEffect(EffectName, Position, Duration)
  | ...

每个 Command 携带预定义的 Signature（ImmutableHashSet<Claim>）
```

### 4.2 审计发现（Entity-as-Data 层）

| ID | 发现 | 严重程度 | 状态 | 收敛方案 |
| ---- | ------ | --------- | ------ | --------- |
| EA-001 | EntityId 单调性在分布式场景 | 中 | 已收敛 | 单机用 U64，分布式 v2 扩展为复合键 |
| EA-002 | Component 不可变性在 C# 中无法完全保证 | 中 | 已收敛 | L2 Source Generator 字段类型白名单检查 + L3 Analyzer 兜底 |
| EA-003 | Archetype 的编译期表示 | 低 | 已解决 | 使用泛型参数或 Source Generator 生成 |
| EA-004 | World 版本号溢出 | 低 | 已收敛 | U64 够用，不处理 |
| EA-005 | System 的并行性判定 | 中 | 已收敛 | L2 Source Generator 方法体写集分析，重叠 Archetype + 修改同一 Component 强制顺序 |
| EA-006 | Command Signature 维护成本 | 中 | 已收敛 | 核心 Command 框架定义，自定义 Command 需手动实现 Signature |
| EA-007 | Entity 的 Component 数量上限 | 低 | 已收敛 | 限制 16 个 Component/Entity，SoA 存储，Analyzer 检查 |

---

## 5. Shell 同态映射定义

### 5.1 核心概念

**定义 5.1.1（Shell 作为函子）**

```
Shell: Domain → Godot

对象映射：
  Shell(Entity) = Godot.Node
  Shell(World) = Godot.SceneTree
  Shell(Component) = Godot.Node 的属性或子节点

态射映射：
  Shell(Spawn(e)) = Instantiate(Node) + AddChild
  Shell(Destroy(e)) = QueueFree
  Shell(SetComponent(e, c, v)) = node.Set(c, v)
```

**定义 5.1.2（Shell 的薄层约束）**

```
Shell 层代码的约束：
  1. 无决策逻辑：不做条件判断，只执行 Command
  2. 无状态：不维护独立状态，只同步 Domain 状态
  3. 无循环：不执行循环，循环在 Domain 的 System 中
  4. 确定性：相同 World 总是产生相同的 SceneTree 变更
  5. 效应边界：效应只发生在同步点（Sync Point）
```

**定义 5.1.3（同步点）**

```
同步点（Sync Point）：
  1. System 执行后：World' → SceneTree 的增量同步
  2. Command 执行时：Command → Godot API 调用
  3. 输入采集时：Godot Input → Domain Input

增量同步（Delta Sync）：
  Delta := (Spawned: Set<EntityId>, Modified: Map<EntityId, Set<ComponentType>>, Destroyed: Set<EntityId>)
```

### 5.2 审计发现（Shell 层）

| ID | 发现 | 严重程度 | 状态 | 收敛方案 |
| ---- | ------ | --------- | ------ | --------- |
| SH-001 | Shell 的"无决策"约束无法类型系统完全保证 | **高** | 已收敛 | L1 sealed override + L2 Source Generator 方法体 AST 检查（禁止 if/else/for/while/switch/try-catch）+ L3 Analyzer 兜底 |
| SH-002 | Shell 的"无状态"与 Godot Node 的 mutable 状态冲突 | 中 | 已收敛 | 明确区分"独立状态"（禁止）和"缓存覆盖写入"（允许）：ApplyToNode 是 overwrite，不是增量更新 |
| SH-003 | 增量同步的 Delta 计算在 Component 级别可能遗漏嵌套变更 | 中 | 已收敛 | COMP002 禁止 Component 嵌套对象，必须扁平化 |
| SH-004 | Shell 的效应签名如何与 Domain 的 Command Signature 合并 | 中 | 已收敛 | Domain 是"预算声明"，Shell 是"实际执行"，运行时校准偏差 |
| SH-005 | 输入采集的效应归属 | 低 | 已收敛 | Claim 归属 Shell 的 GatherInput，Domain Input 零效应 |

---

## 6. 类型系统层（Type System Layer）

### 6.1 三层编译期安全模型

| 层级 | 机制 | 错误时机 | 用户感知 | 覆盖范围 |
| ------ | ------ | --------- | --------- | --------- |
| **L1 类型系统** | C# 泛型约束、sealed、readonly、struct | 编码时（IDE 实时） | 红线/波浪线 | 结构约束 |
| **L2 代码生成** | Source Generator 生成时检查 | 编译时（dotnet build） | 构建失败 | 类型无法表达的约束 |
| **L3 语法分析** | Roslyn Analyzer 语法树检查 | 编译时（dotnet build） | 构建失败 | 语义约束（兜底） |

### 6.2 L1 类型系统约束

```csharp
// sealed class：阻止继承和 new 隐藏
public sealed class EnemyShell : NodeShell<EnemyState> { }

// readonly struct：不可变值类型
public readonly struct PositionComponent : IComponent {
    public float X { get; init; }
}

// 泛型约束：TState 必须是 struct（值类型）
public abstract class NodeShell<TState> : Node
    where TState : struct, IComponent
```

### 6.3 L2 代码生成约束（Source Generator）

```csharp
// 开发者写意图声明（partial struct + 字段标记）
public partial struct PositionComponent : IComponent {
    [ComponentField] public float X;  // Generator 检查类型
    [ComponentField] public float Y;
}

// Generator 生成完整实现，同时强制执行：
// 1. 字段类型白名单：unmanaged、string、ImmutableArray<T>、IComponent
// 2. 禁止 List<T>、Dictionary<K,V>、T[]、class
// 3. 禁止嵌套对象（递归检查）
// 4. 自动生成 init-only 属性、相等性、哈希、ToString
// 5. 自动生成 EffectSignature（如果字段有 [Budget]）

// 违规 → 编译错误
// PositionComponent.cs(4,5): error GEN001: [ComponentField] 'Values' has type 'List<int>' which is not allowed.
```

```csharp
// System 写集分析（Generator 分析 Update 方法体）
public class MovementSystem : ISystem<TimeInput> {
    public Archetype Query => Archetype.Of<Position, Velocity>();
    public (World, Command[]) Update(World w, TimeInput input, ImmutableArray<Entity> entities) {
        // Generator 分析：此方法修改 Position → 记录 writes Position
    }
}

// 生成调度器时检测冲突：
// MovementSystem writes Position
// PhysicsSystem also writes Position
// → 自动标注 [RunAfter(typeof(MovementSystem))] 或生成顺序代码
```

### 6.4 L3 语法分析约束（Roslyn Analyzer）

```csharp
// 规则 RULE001：Domain 项目禁止引用 Godot
// 规则 SHELL001：Shell 方法复杂度检测（> 20 行或含复杂条件）
// 规则 SHELL003：检测 new 隐藏 sealed override 方法
// 规则 BUDGET001：场景预算累加
// 规则 SYS001：System 并行冲突检测（兜底）
```

### 6.5 审计发现（类型系统层）

| ID | 发现 | 严重程度 | 状态 | 收敛方案 |
| ---- | ------ | --------- | ------ | --------- |
| TS-001 | struct 不保证不可变 | 中 | 已收敛 | L2 Generator 字段类型白名单 + L3 Analyzer 兜底 |
| TS-002 | static abstract 的 C# 版本要求 | 低 | 已收敛 | 文档化最低要求：Godot 4.2+ + .NET 8+ |
| TS-003 | Command 的效应未编码 | 中 | 已收敛 | Command 携带 ImmutableHashSet<Claim> 的 Signature 属性 |
| TS-004 | new 隐藏 sealed | 低 | 已收敛 | L1 sealed class 完美解决（无子类） |
| TS-005 | GatherInput 的效应不可控 | 中 | 已收敛 | L2 Generator 方法体 AST 检查 + L3 Analyzer 兜底 |
| TS-006 | 泛型参数爆炸 | 低 | 已收敛 | L2 Generator 自动生成非泛型密封壳 |
| TS-007 | 项目引用隔离是约定 | 中 | 已收敛 | L3 Analyzer 检查 Domain 项目的 PackageReference |
| TS-008 | Interop 类型归属 | **高** | **已解决** | 引入 MyGame.Interop DTO 项目，纯 .NET 无 Godot 引用 |
| TS-009 | sealed override 与 Godot 反射 | **高** | 已收敛 | 反射调用基类 sealed override，new 隐藏不被调用，Analyzer 兜底检测 |
| TS-010 | Component 类型编译期与运行期映射 | 中 | 已收敛 | L2 Generator 生成 Archetype → ComponentType 映射表 |
| TS-011 | World 不可变性性能 | 中 | 已收敛 | Bevy SoA + swap-remove 策略，原型 Benchmark 验证 |
| TS-012 | Command 的 DU 在 C# 中 | 中 | 已收敛 | 接受 abstract record + sealed record 标准模式，瓶颈时 v2 优化 |

---

## 7. Godot API 的 Claim 映射（完整版）

### 7.1 场景树操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `GetNode(path)` | `{ read(tree, path, use, shell_scope) }` | 读场景树 |
| `GetTree()` | `{ read(tree, "root", use, shell_scope) }` | 读场景树根 |
| `AddChild(node)` | `{ write(tree, node.id, create, shell_scope), occupy(tree, node.id, create, shell_scope) }` | 写树 + 占用 |
| `RemoveChild(node)` | `{ write(tree, node.id, release, shell_scope), occupy(tree, node.id, release, shell_scope) }` | 写树 + 释放 |
| `QueueFree()` | `{ release(tree, self.id, release, shell_scope), release(memory, self.size, release, shell_scope) }` | 释放（**mode=release**，收口 iter27：原 mode=move 使 net 漏算释放、DO-9 泄漏检测失效；改 release 后 net 正确计入 −size） |
| `MoveChild(node, index)` | `{ write(tree, node.id, use, shell_scope) }` | 写树 |

### 7.2 属性访问

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `Position` getter | `{ read(self, "transform", use, shell_scope) }` | 读自身状态 |
| `Position` setter | `{ write(self, "transform", use, shell_scope) }` | 写自身状态 |
| `GlobalPosition` getter | `{ read(self, "transform", use, shell_scope), read(tree, parent_path, use, shell_scope) }` | 读自身 + 父节点 |
| `Rotation` getter/setter | `{ read/write(self, "transform", use, shell_scope) }` | 读写自身状态 |
| `Scale` getter/setter | `{ read/write(self, "transform", use, shell_scope) }` | 读写自身状态 |

### 7.3 物理操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `MoveAndSlide()` | `{ read(physics, self.body_id, use, shell_scope), write(physics, self.body_id, use, shell_scope), read(tree, "collision_shapes", use, shell_scope) }` | 读写物理 |
| `ApplyForce(force)` | `{ write(physics, self.body_id, use, shell_scope) }` | 写物理 |
| `ApplyImpulse(impulse)` | `{ write(physics, self.body_id, use, shell_scope) }` | 写物理 |
| `GetSlideCollisionCount()` | `{ read(physics, self.body_id, use, shell_scope) }` | 读物理 |
| `GetSlideCollision(index)` | `{ read(physics, self.body_id, use, shell_scope) }` | 读物理 |

### 7.4 资源加载

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `Load<T>(path)` | `{ read(disk, path, use, shell_scope), occupy(memory, estimatedSize(T), create, global_scope) }` | 读磁盘 + 占用内存 |
| `LoadInteractive(path)` | `{ read(disk, path, use, shell_scope) }` | 读磁盘 |
| `Instantiate(scene)` | `{ read(memory, scene.uid, use, shell_scope), create(tree, new_id, create, shell_scope), occupy(memory, scene.estimated_size, create, shell_scope) }` | 读场景 + 创建 + 占用 |
| `Preload(path)` | `{ read(disk, path, use, shell_scope), occupy(memory, estimatedSize, create, global_scope) }` | 读磁盘 + 占用内存 |

### 7.5 信号系统

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `EmitSignal(...)` | `{ write(signal_bus, signal, create, shell_scope), read(tree, "subscribers_" + signal, use, shell_scope) }` | 写信号 + 读订阅者 |
| `Connect(...)` | `{ write(self, "signal_" + signal, create, shell_scope), occupy(callback, callable.size, create, shell_scope) }` | 写信号 + 占用回调 |
| `Disconnect(...)` | `{ write(self, "signal_" + signal, release, shell_scope), occupy(callback, callable.size, release, shell_scope) }` | 写信号 + 释放回调 |
| `IsConnected(...)` | `{ read(self, "signal_" + signal, use, shell_scope) }` | 读信号 |

### 7.6 渲染操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `DrawMesh(mesh, material, transform)` | `{ read(gpu, mesh.buffer_id, use, shell_scope), write(gpu, command_buffer, create, shell_scope), read(gpu, material.shader_id, use, shell_scope) }` | 读 GPU + 写命令 + 读材质 |
| `DrawRect(rect, color)` | `{ write(gpu, command_buffer, create, shell_scope) }` | 写命令 |
| `SetMaterialOverride(material)` | `{ write(self, "material", use, shell_scope), read(gpu, material.shader_id, use, shell_scope) }` | 写材质 + 读材质 |

### 7.7 音频操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `Play(stream)` | `{ write(audio_mixer, self.channel_id, create, shell_scope), read(memory, stream.buffer_id, use, shell_scope), occupy(audio_channel, 1, create, shell_scope) }` | 写混音器 + 读音频 + 占用通道 |
| `Stop()` | `{ write(audio_mixer, self.channel_id, release, shell_scope), occupy(audio_channel, 1, release, shell_scope) }` | 写混音器 + 释放通道 |
| `SetVolumeDb(volume)` | `{ write(audio_mixer, self.channel_id, use, shell_scope) }` | 写混音器 |

### 7.8 输入操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `IsActionPressed(action)` | `{ read(input, action, use, shell_scope) }` | 读输入 |
| `IsActionJustPressed(action)` | `{ read(input, action, use, shell_scope) }` | 读输入 |
| `GetMousePosition()` | `{ read(input, "mouse", use, shell_scope) }` | 读输入 |

### 7.9 网络操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `Rpc(method, args)` | `{ write(network, self.id + "/" + method, create, shell_scope), read(memory, args.size, use, shell_scope) }` | 写网络 + 读内存 |
| `RpcId(peerId, method, args)` | `{ write(network, peerId + "/" + method, create, shell_scope), read(memory, args.size, use, shell_scope) }` | 写网络 + 读内存 |

### 7.10 动画操作

| Godot API | Claim 集合 | 说明 |
| ----------- | ----------- | ------ |
| `Play(animName, blend, speed)` | `{ write(self, "animation", create, shell_scope), read(memory, animName, use, shell_scope), occupy(animation_state, 1, create, shell_scope) }` | 写动画 + 读内存 + 占用状态 |
| `Stop()` | `{ write(self, "animation", release, shell_scope), occupy(animation_state, 1, release, shell_scope) }` | 写动画 + 释放状态 |
| `Seek(seconds)` | `{ write(self, "animation", use, shell_scope) }` | 写动画 |

---

## 8. 效应推导层（Effect Derivation Layer）

### 8.1 自动推导规则

```
白名单映射：核心 Godot API（约 100 个方法）的 Claim 映射（见 §7 全表）
默认规则：未映射 API 默认 emit { Unknown(unknown, Unknown, Unknown, scope) }
  // 收口 iter44：原默认 { read(unknown,use), write(unknown,use) } 的 kind/mode 硬约束
  // 使 occupy/release 永不可达 ⇒ 释放语义被吞没、net 守恒不可证、DO-9 双误。
  // 改为 mode=Unknown（最弱未知），fail-closed 为「需 [EffectOverride] 标注真实 mode 或人工确认」，
  // 而非静默漏报/误报。Unknown 的三维契约【QED-A3】：net/peak 按占用 +size 保守计入（§3.3.1——
  // 非上界、非静默 0）；Compatible 按 Use 最弱兼容放行（§3.2.3 P4，fail-open 防警报洪水）。
release-class 白名单（强制 emit release/occupy-release，不得落入默认规则）。
  // 【QED-A9 修正 2026-09-06：godotengine 官方文档签名级复核，收口 iter55 PO-55-09。原「2026-08-20
  // 源码实测」清单的三处错误归类在此定稿修正，实现 ApiMapping.ReleaseClass 已同步。】
  // 保留 4 项（真实释放操作）：
  //   - queue_free()  / Object.free()  → 释放 Node 自身 + 递归释放其全部 children（NOTIFICATION_PREDELETE 内 memdelete(child)）
  //   - remove_child(Node)              → 释放该 child 的 tree 占用（occupy(tree, child.id, release)）
  //   - disconnect(signal, Callable)    → 释放信号/回调占用（occupy(callback, callable.size, release)）
  // 修正记录（防回归钉：VerificationMatrixTests/CrossLayerTests/QedP0A7*）：
  //   - cancel_free 移出：官方语义「Cancels any queue_free() call」=取消释放、节点存活（Godot 4.2+）——
  //     归入 release-class 会 emit release，恰好掩盖它所取消的那次释放的泄漏路径（方向相反）；
  //     现以显式白名单条目 CancelFree 按「重新占用」映射（与 QueueFree 逐资源对称，Release↔Create 配对）。
  //   - remove_from_group 移出：纯组织性操作（组员关系非资源占用，官方文档无任何释放语义），emit release=凭空少计。
  //   - free_children_in_group 移出：Node 公开 API 不存在该方法（官方文档全文无此项，原「源码实测」不可证）。
  //   - Timer 类：无独立 RemoveTimer 方法；Timer 实例经 queue_free 释放（计时占用随 Node 释放），stop() 仅停计时
  release-class = { queue_free, free, remove_child, disconnect }
  // 核心 100 白名单须包含上述全部；任一 release 类 API 未入白名单 ⇒ 回落默认 Unknown 规则（fail-closed），不静默漏报。
```

### 8.2 审计发现（效应推导层）

| ID | 发现 | 严重程度 | 状态 | 收敛方案 |
| ---- | ------ | --------- | ------ | --------- |
| ED-001 | API 效应映射白名单/黑名单 | 中 | 已收敛（修订 A） | 核心 API 白名单（须含完整 release-class 枚举，见 §8.1）+ 默认 Unknown 标记（§8.1）+ 允许 [EffectOverride] 修正（须遵循 §8.3 校验规则） |
| ED-002 | 属性访问的效应模糊 | 中 | 已收敛 | L2 Generator 分析语法树区分 getter/setter |
| ED-003 | 回调和委托的效应推导 | 中 | 已收敛 | 信号连接分析右侧方法，动态委托保守估计 |
| ED-004 | 场景实例化的动态性 | 中 | 已收敛 | 静态累加 .tscn 显式对象，动态 Instantiate 变量场景标记为 [1,⊤]（§3.1.5 口径；【QED-A8】原游离 "∞" 残留同步） |
| ED-005 | 资源共享的去重 | 中 | 已收敛 | L2 Generator 解析 .tscn 通过 uid 去重 |
| ED-006 | _Process 调用频率对效应累加 | 中 | 已收敛 | L2 [TargetFrameRate] 属性，静态保守假设 60fps，运行时采样校准 |
| ED-007 | yield/await 效应时序 | 中 | 已收敛 | AsyncEffect<T> 类型标记，保守假设 async_scope 效应持续到方法结束 |
| ED-008 | CallDeferred 效应延迟 | 中 | 已收敛 | 保守假设 deferred 效应立即发生（保证峰值安全） |

### 8.3 [EffectOverride] / [AcceptDeviation] 校验规则【收口 iter50 #62】

> 原文档仅在 ED-001 / §9.3 一句提及这两个属性，无任何语法/可覆盖域/审查规则，是未约束逃逸通道。现正式定义：

**定义 8.3.1（[EffectOverride]）**
```
语法：  [EffectOverride(target: Claim | resource, reason: string, scope?: ScopeId)]
可覆盖域（显式受限，禁止无差别压制）：
  - 仅可覆盖「单条 Claim 的 mode / size / scope」三类属性；
  - 禁止覆盖 kind（read/write/occupy 不可经 override 互转）；
  - 禁止压制 DO-9 泄漏报警的根因（override 必须给出真实 release 路径证明，否则 DO-9 仍报警）；
  - 禁止静默豁免 DO-7 量纲混算（跨 kind 仍需 weight 定义，见 3.3.2）。
审查：  每个 [EffectOverride] 必须含非空 reason，CI 在 PR 检查中要求人工 approve（不得本地私自豁免）；
       reason 须引用具体证据（API 文档 / 实测），不接受「信任我」。
```

**定义 8.3.2（[AcceptDeviation(ε)]）**【收口 iter33/iter50：原 0.3 无阈值/上界/作用域定义】
```
语法：  [AcceptDeviation(epsilon: double)]   // epsilon ∈ [0.0, 0.5]，超出 ⇒ 编译错误（上界约束）
语义：  仅放宽「运行时 Deviation > 报警阈值」的局部报警（作用于标注对象 scope），不豁免编译期 DO 报警；
       报警阈值由 §9.1 定义（基础 0.2f），若 epsilon < 阈值则无意义（编译警告）；
       作用域：标注对象所在 scope（3.1.3b），不跨 scope 传播；
依赖：  Deviation 公式必须已完成 range 下界保护（§9.1），否则 0.3 对 NaN/∞ 仍恒假失效。
```

---

## 9. 运行时层（Runtime Layer）

### 9.1 开发模式验证

```csharp
// 异步后台线程采样，不阻塞主线程
[Conditional("DEBUG")]
public static class EffectValidator {
    private static readonly ConcurrentDictionary<string, ResourceRecord> _active = new();

    public static void Validate(string nodePath, Signature expected) {
        if (Engine.GetProcessFrames() % 60 != 0) return;  // 每 60 帧采样

        var actual = SampleActualResources(nodePath);
        var deviation = CalculateDeviation(expected, actual);

        // 收口 iter51 #4：deviation 为 DeviationVal（double∪{⊤}），先判 ⊤ 再比数值，避免类型不可比
        if (deviation is double d && d > 0.2) {
            GD.PushWarning($"[EffectAudit] {nodePath}: expected vs actual deviation {deviation:P}. " +
                          $"Estimator may need calibration.");
        }
    }

    // 收口 iter33：原 range=max-min 遇单值(size=1)得 0、遇 ∞ 得 NaN/∞ ⇒ 报警恒假/掩盖。
    // 现加下界 + IsFinite 保护，size 用 SizeVal（§3.1.5），∞ 为 ⊤ 上界标记。
    // 收口 ST-03：返回类型由 double 改为 DeviationVal（§3.1.5c），可表达 ⊤ 上界标记
    private static DeviationVal CalculateDeviation(Signature expected, Signature actual) {
        // Deviation = Σᵢ |actualᵢ - expectedᵢ_mid| / max(expectedᵢ_range, ε)
        //   expectedᵢ_mid = (lo + hi) / 2，expectedᵢ_range = hi - lo
        //   ε = 1（分母下界，单值区间 [s,s] 时 range=0 ⇒ 用 ε 避免除零）
        //   任一端为 ⊤（上界未知）⇒ 该项 Deviation 计为 ⊤ ⇒ 整体标记「不可校准」跳过（不 NaN 不 ∞）
        //   返回 ⊤ 时调用方视为「需人工界定」，不触发普通 0.2f 报警（避免掩盖）
    }
}
```

### 9.2 Release 零开销

```csharp
// Source Generator 条件生成：DEBUG 生成审计代码，RELEASE 不生成
#if DEBUG
public static class EffectAudit { ... }
#else
// RELEASE：EffectAudit 类不存在
#endif

// CI 验证：Mono.Cecil 扫描 IL，审计代码存在则构建失败
```

### 9.3 校准反馈

```
校准流程：
1. 首次构建：静态预估作为基准
2. 运行时采样：收集实际值（SQLite 内存数据库）
3. 偏差 > 20%：生成校准报告（.effect-calibration，gitignore）
4. 开发者选择：
   A) 调整 [Budget] 标注（修正预估）
   B) 调整代码（减少实际使用）
   C) 接受偏差（标注 [AcceptDeviation(ε)]，须遵循 §8.3.2 校验：ε∈[0,0.5]、仅放宽运行时局部报警、不豁免编译期 DO）
5. 会话结束：导出 Chrome Trace 格式（session.trace.json）
```

### 9.4 审计发现（运行时层）

| ID | 发现 | 严重程度 | 状态 | 收敛方案 |
| ---- | ------ | --------- | ------ | --------- |
| RT-001 | 采样频率与性能 | 中 | 已收敛 | 异步后台线程 + 分层抽样（只采样 [Budget] 标记对象） |
| RT-002 | Deviation 定义 | 低 | 已收敛（修订 A） | Deviation = Σᵢ |actualᵢ-middleᵢ| / max(rangeᵢ, ε)；middle/range 来自 SizeVal 区间（§3.1.5），分母加下界 ε=1 防 range=0 除零，⊤ 上界项跳过（不 NaN/∞，收口 iter33）|
| RT-003 | Conditional("DEBUG") 局限性 | 低 | 已收敛 | L2 Generator 条件生成 + CI IL 扫描验证 |
| RT-004 | 运行时偏差校准 | 中 | 已收敛 | 校准反馈循环：调整 Budget / 调整代码 / 接受偏差 |
| RT-005 | Release 审计代码剥离验证 | 中 | 已收敛 | CI 步骤用 Mono.Cecil 扫描 IL |
| RT-006 | 运行时采样数据持久化 | 低 | 已收敛 | SQLite 内存 + 会话级导出 Chrome Trace，不长期存储 |

---

## 10. 风险评估（Risk Assessment）

| ID | 风险 | 概率 | 影响 | 缓解措施 |
| ---- | ------ | ------ | ------ | --------- |
| R-1 | Godot C# 模块版本兼容性 | 中 | 高 | 锁定 Godot 4.2+ + .NET 8+；CI 矩阵测试多版本 |
| R-2 | Source Generator 编译性能下降 | 中 | 中 | 增量生成；缓存语法树；限制扫描范围 |
| R-3 | Analyzer 误报率过高 | 高 | 高 | 分级报警；允许 [SuppressEffect]；持续校准；白名单机制 |
| R-4 | 团队抗拒分层架构 | 高 | 高 | 渐进式采用；Audit-only 模式零改动入口；提供迁移工具；培训 |
| R-5 | Godot API 变动导致映射表失效 | 中 | 中 | 自动化测试覆盖核心 API；版本升级时重新扫描；社区贡献 |
| R-6 | struct 拷贝性能问题 | 低 | 中 | readonly record struct（编译器优化）；状态对象限制大小（< 128 bytes）；大 Component 用引用包装 |
| R-7 | 跨平台构建差异 | 中 | 中 | CI 覆盖 Linux/Windows/macOS；使用 Docker 统一环境 |
| R-8 | Entity-as-Data 与 Scene Tree 同步性能 | 中 | 高 | 增量同步（Delta Sync）；批量更新；避免每帧全量同步；Benchmark 验证 |
| R-9 | ImmutableDictionary 性能瓶颈 | 中 | 高 | 评估切换到自定义 Archetype 分组存储；Benchmark 对比 Dictionary vs ImmutableDictionary |
| R-10 | Source Generator 生成代码的可调试性 | 中 | 中 | 生成代码添加 [GeneratedCode] 和 [DebuggerNonUserCode]；提供 Source Link 支持 |
| R-11 | 团队函数式思维培训成本 | 高 | 高 | Audit-only 模式零门槛；渐进式培训（2-4 周）；文档和示例项目；代码审查 |
| R-12 | Godot _Process 调用频率不确定 | 中 | 中 | 静态分析假设 60fps；运行时采样校准实际帧率；[TargetFrameRate] 属性 |

---

## 11. 工作量估算

| 模块 | 内容 | 人周 |
| ------ | ------ | ------ |
| **Domain 基架** | IRules 接口、Command 代数类型、Component 约束、World 存储 | 1-2 |
| **Shell 基架** | NodeShell 抽象基类、sealed 生成器、Delta Sync | 1-2 |
| **Source Generator** | Component 生成（字段白名单、效应签名）、System 调度器生成、Shell 生成 | 3-4 |
| **Roslyn Analyzer** | RULE001（Domain 零 Godot）、SHELL001（壳层复杂度）、BUDGET001（场景预算）、SYS001（System 冲突） | 2-3 |
| **Interop DTO** | Vector3DTO、ColorDTO、TransformDTO + 自动生成转换 | 1 |
| **Godot API 映射** | 100 个核心 API 的 Claim 映射表 | 1-2 |
| **运行时验证** | 采样器、Chrome Trace 导出、偏差检测、校准反馈 | 1-2 |
| **测试/文档** | 示例项目（Enemy + EnemyRules + EnemyShell）、架构指南、迁移教程 | 2-3 |
| **总计** | | **12-19 周（3-5 人月）** |

---

## 12. 社区发布策略（Community Release Strategy）

### 12.1 发布模式：渐进式采用

```
阶段 1：Audit-only（非侵入，立即可用）
├── 安装 NuGet 包：EffectAudit.Godot
├── 零代码改动，零架构改动
├── 自动扫描现有 Godot C# 代码
├── 报警：Instantiate 无 QueueFree、循环内资源峰值、预算超支
└── 目标：1000 下载，收集误报反馈

阶段 2：Attribute（轻度侵入，标记预算）
├── 在关键资源字段加 [Budget(VramMB = 32)]
├── 在场景根节点加 [GlobalBudget(VramMB = 512)]
├── 编译期累加预算，超支报警
└── 目标：500 项目使用预算标注

阶段 3：Interop（中度侵入，纯数据测试）
├── 将业务逻辑中的 Godot 类型（Vector3）替换为 DTO（Vector3DTO）
├── 业务逻辑项目不再引用 Godot
├── 可用 dotnet test 测试业务逻辑
└── 目标：50 项目拆分 Domain/Shell

阶段 4：Full（重度侵入，完整架构）
├── 重写 Entity 为纯数据
├── 重写 System 为纯函数
├── 使用 Source Generator 自动生成 Shell
├── 80% 测试不启动 Godot
└── 目标：10 完整重构案例
```

### 12.2 Audit-only 模式技术实现

```csharp
// 开发者现有代码（零改动）
public partial class Enemy : CharacterBody3D {
    public Texture2D AlbedoMap;  // 无 Budget

    public override void _Process(double delta) {
        var player = GetNode<Player>("../Player");  // 每帧查询！
    }

    public void Die() {
        var fx = ExplosionScene.Instantiate<Explosion>();
        AddChild(fx);
        // 忘记 QueueFree...
    }
}
```

```csharp
// Audit-only Analyzer（自动推导，零属性）
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GodotAuditAnalyzer : DiagnosticAnalyzer {
    // 1. 自动推导效应（无需 [Effect] 属性）
    //    GetNode → read{tree}
    //    Instantiate → occupy{+1}
    //    AddChild → write{tree}

    // 2. 自动检测资源泄漏
    //    Instantiate 后代码路径无 QueueFree → 报警

    // 3. 自动检测性能陷阱
    //    _Process 中每帧调用 GetNode → 建议缓存

    // 4. 自动估算资源（统一 SizeVal 口径，收口 iter50 #61：禁止游离字面量，全部源自 §3.1.5）
    //    Texture2D 字段 → 默认 occupy{memory, [64,64]}（精确 size 区间，单位 MB；原游离 "64MB" 现归一到 SizeVal）
    //    场景中 20 个 Enemy → 20 条同构事件（或 Loop(body,20)）逐副本计数 ⇒ occupy{memory,[64,64]}
    //    累加为 [1280,1280]（=20×64；多重性载体见 §3.1.4 注记【QED-A5】：事件序列逐条累加 / size×ω 缩放，
    //    非集合重复——重复 Claim 在 Signature.Of 构造期拒绝）⇒ 与 GlobalBudget [512,512] 比较
    //    所有 size 一律走 §3.1.5 的 SizeVal（单值/精确/动态⊤），与 net/peak/Deviation 同一载体，可机械对账。
}
```

**编译期报警（Audit-only 模式）**：

```
Enemy.cs(7,9): warning AUDIT001: 
  GetNode("../Player") called every frame in _Process.
  Suggestion: Cache in _Ready to reduce read{tree} from 60/sec to 1.

Enemy.cs(15,9): error AUDIT002: 
  Instantiate<Explosion>() has no guaranteed QueueFree() path.
  Resource leak: occupy{memory, [size,⊤]} per death (size 取自 §3.1.5：显式 .tscn ⇒ [s,s]；动态 ⇒ [1,⊤])，never released (QueueFree 现已 emit release, 见 §7.1).
  Fix: Add fx.Finished += () => fx.QueueFree();   // 修复后 net(S,scope) 计入 −size，泄漏判定消解
  // 收口 iter50 #61：原 "+10 per death" 游离字面量已废除，改由 §3.3.1 net(S,scope) 的 size 区间机械给出。

Enemy.cs(3,5): warning AUDIT003: 
  Texture2D AlbedoMap has no [Budget]. Using conservative estimate occupy{memory, [64,64]} (unit MB, per §3.1.5).
  Scene 'Level1' accumulated VramMB: [1280,1280] (default GlobalBudget [512,512], per §3.1.5 SizeVal).
  Suggestion: Add [Budget(VramMB = 32)] or reduce texture resolution.
  // 收口 iter50 #61：64MB/512MB 现均为 SizeVal 区间，与 net/peak/Deviation 同源，可对账；跨 kind 比较经 weight(occupy,occupy)=1（同 kind 合法）。
```

### 12.3 开源协议与渠道

| 渠道 | 内容 | 时机 |
| ------ | ------ | ------ |
| **NuGet** | EffectAudit.Core, EffectAudit.Godot | 阶段 1 完成即可发布 |
| **GitHub** | 源码 + Issue + Discussion + Wiki | 同步发布 |
| **Godot Asset Library** | Godot 插件包（.zip） | 阶段 1 完成 |
| **Unity Asset Store** | Unity 适配包（未来） | Unity 适配完成后 |
| **博客/演讲** | 技术博客、GDC 演讲、GodotCon 演讲 | 有成功案例后 |

**协议**：MIT（最大化采用，允许商业使用）

---

## 13. 术语表（Glossary）

| 术语 | 定义 |
| ------ | ------ |
| **Claim** | 资源声明：(kind, resource, mode, scope, size?)；相等/归一规则见 §3.1.4a |
| **Signature** | 效应签名：Claim 的不可变集合，按 kind 分桶（§3.1.4b）实现 DO-7 量纲隔离 |
| **ResourceId** | 资源标识：Tree, Self, Physics, Memory, Disk, Gpu, CommandBuffer, SignalBus, Occupancy, Callback, Network, Input, AudioMixer, Custom（§3.1.2/3.1.2b；裸 "signal" 已归一 SignalBus）【QED-A8 同步：补 rA4 新增的 Occupancy/Callback/Input/AudioMixer】|
| **ScopeId** | 作用域标识：Method, Type, Scene, Global, Loop, Conditional, Async, Shell；偏序 ⊑ 见 §3.1.3b【QED-A8 同步：补 ST-04 新增的 Shell】|
| **Entity** | 纯数据实体：(EntityId, ImmutableDictionary<Type, IComponent>) |
| **Component** | 纯数据组件：readonly record struct，无引用类型 |
| **Archetype** | 实体类型：ComponentType 的集合 |
| **World** | 实体世界：Map<EntityId, Entity> + 版本号 |
| **System** | 业务系统：纯函数 (World, Input) → (World, Command[]) |
| **Command** | 效应请求：SpawnEntity, DestroyEntity, SetComponent, ... |
| **Shell** | 引擎壳：Domain.World ↔ Godot.SceneTree 的同态映射 |
| **Sync Point** | 同步点：World' → SceneTree 的增量同步 |
| **Delta Sync** | 增量同步：只同步变更的 Entity 和 Component |
| **Source Generator** | C# 编译期代码生成器 |
| **Analyzer** | Roslyn 编译期静态分析器 |
| **L1/L2/L3** | 三层编译期安全：类型系统 / 代码生成 / 语法分析 |
| **Audit-only** | 非侵入模式：零代码改动，安装 NuGet 包即可编译期审计 |
| **PDR** | Preliminary Design Review，初步设计评审 |

---

## 14. 编译期工具层完备性规范（L2 / L3）【收口 A2 / iter38 / iter39】

> 此前 §14 历史表注明「工具层完备性（L2/L3）仍依赖实现」。本节把 L2 Source Generator 与 L3 Roslyn Analyzer 的
> **soundness / completeness** 判据写成可证规范，并附 C# 测试矩阵（正例/反例）作为「用测试证明」的载体。
> 数学层（§3）已稳定；本节证明工具能**忠实落地**数学层，而非仅声明。

### 14.1 判定框架

```
对给定用户代码 M，工具产出 Signaturê(M)；数学真值 Signature(M) 由 §3 组合律推导。
  SOUNDNESS（无漏报的误通过）：Signaturê(M) 中出现的正报警 ⊆ 真实违反（不冤枉）。
  COMPLETENESS（无漏报）：真实违反 ⇒ 工具必报（不漏）。
工具分两层：
  L2 Source Generator：在编译期把 Claim 注入类型/属性，使业务代码编译失败若引用 Godot（DO-2）；并生成 EffectAudit 包装。
  L3 Analyzer：Roslyn 语法/语义分析，产出 AUDIT001..003 等诊断。
```

### 14.2 L2 Source Generator 判据（iter38）

```
S1 写集 soundness：Generator 注入的 EffectAudit 调用覆盖所有 §7 白名单 API 调用点（不漏注入）。
S2 写集 completeness 边界：Generator 不分析方法体内部（iter38 已记 S1-S2 不分析方法体）；
    故方法体内反射/字符串拼出的 Godot 调用不被注入 ⇒ 该集合为「保守上界缺失」而非误报。
    判定：L2 对「直接 API 调用」complete；对「反射/动态调用」sound（不冤枉）但 incomplete（需 L3 + [EffectOverride] 兜底）。
S3 DO-2 完备：Domain 项目引用 Godot 命名空间 ⇒ 编译错误（CI 验证）。
```

### 14.3 L3 Roslyn Analyzer 判据（iter39）

```
A1 泄漏检测 COMPLETE：Instantiate / AddChild 后控制流无 QueueFree / release-class 调用 ⇒ AUDIT002 必报
   （判定谓词：§3.3.1 闭包 net 不含 0 ⇒ Leak，QED-A8 口径）。
A2 峰值检测 COMPLETE：循环体内资源分配（ω=⊤）⇒ AUDIT 峰值报警（基于 §3.2.5 Peak=⊤ 兜底）。
A1/A2 适用程序类【QED-A2 定稿 PO-55-13；前提不成立 ⇒ 判据降级 PARTIAL-COMPLETE（报「须人工确认」，
   不得默许 COMPLETE）；「实现层测试全绿方视为已证」条款（§14.4）不变】：
   (i)  配对判定限于单方法体直线/单出口控制流——try/catch 吞 release、跨方法/跨对象配对不在静态
        覆盖（构造期泄漏同属盲区，README 诚实边界 ⑨），以运行期 Σnet 为权威（§8.1 宪法）；
   (ii) 事件单触发——同一方法不重复进入而累积实例；重复进入的界须由 ω 声明（ω=⊤ 走常驻豁免/
        峰值 ⊤ 兜底，§3.2.5）；事件回调「恰触发一次」（如 fx.Finished += () => fx.QueueFree()）
        属 (i) 的控制流前提，不满足时 A1 降级；
   (iii) 循环有界或显式标 ⊤（无界循环不标 ⊤ ⇒ 不在判据适用类）。另注：A2 对「ω 有界且很小」的
        循环同样按上界报警（保守方向，不声称不冤枉——SOUND 一侧由 §3.3.2 上界语义承载）。
A3 量纲混算 SOUND+COMPLETE：跨 kind 聚合（weight=⊥）⇒ KIND_MIX 编译错误（基于 §3.1.4b 分桶 + §3.3.2 weight）。
A4 兼容冲突 COMPLETE：同资源冲突 mode 对（CONFLICT 集）⇒ 报警（基于 §3.2.3 全函数）。
   权威域限定【QED-A3 定稿 PO-55-05】：A4 的 COMPLETE 限**生命周期资源操作**（create/release/move
   的同类自冲突与互补配对——§7 全表的生命周期 API 即此域）。§7 属性/值写操作映射 write+use 落
   read/write 量纲（DO-7 量纲隔离），不参与生命周期冲突配对 ⇒ 「写写数据竞争检测」不在 A4 判据内，
   移交 L2 写集分析（F 轨，与 F2 精化类型同窗）；§6.3 调度器示例（写写冲突自动 [RunAfter]）据此由
   F 轨承载。「修订 §7 写操作 mode（写≠use）」方案被否决：CONFLICT 三对均为生命周期模式，容纳写写
   竞争须新增 Exclusive 类 mode = 公共枚举/JSON 契约面/Compatible 表三重公共面变更，且跨语义域。
A5 未知保守 SOUND：未映射 API 落默认 Unknown 规则 ⇒ 不冤枉，但需人工确认（fail-closed，见 §8.1）。
```

### 14.4 C# 测试矩阵（用测试证明）

```
// 正例（应报，证明 COMPLETENESS）：
[Test] AUDIT002_Leak_Detected() {
  // Instantiate 后无 QueueFree
  var fx = ExplosionScene.Instantiate<Explosion>(); AddChild(fx);
  // Assert: Diagnostic AUDIT002 emitted (net(S,scope)>0, no release pair)
}
[Test] AUDIT003_Budget_Exceeded() {
  // 20 × Texture2D(无 Budget) ⇒ [1280,1280] > GlobalBudget [512,512]
  // Assert: AUDIT003 emitted (SizeVal 同源比较)
}
[Test] KIND_MIX_CrossKind_Blocked() {
  // peak 跨 read+occupy 聚合
  // Assert: Compile error KIND_MIX (weight=⊥)
}
[Test] Compat_Conflict_Reported() {
  // 两路各 create 同 Memory(uid) 无 release
  // Assert: conflict diagnostic (CONFLICT 集)
}

// 反例（不应报，证明 SOUNDNESS）：
[Test] NoFalsePositive_QueueFree_Present() {
  var fx = ExplosionScene.Instantiate<Explosion>(); AddChild(fx);
  fx.Finished += () => fx.QueueFree();   // release 配对存在
  // Assert: NO AUDIT002 (net(S,scope) 含 −size ⇒ 守恒)
}
[Test] NoFalsePositive_ReadShared() {
  // 并行 read 同资源（use∧use 兼容）
  // Assert: NO conflict (§3.2.3 use 最弱兼容)
}
[Test] NoFalsePositive_Unknown_Maps_ToManualReview() {
  // 自定义反射调用 Godot API
  // Assert: NO 误报；落 Unknown 规则需人工确认（fail-closed，非 AUDIT 误警）
}

// ⊤ 边界（证明 ∞ 不崩溃）：
[Test] DynamicInstantiate_TopPropagates() {
  // 变量场景 Instantiate ⇒ size [1,⊤]
  // Assert: Peak/net 返回 ⊤ 不 NaN/不发散；AUDIT 报「需人工界定」非崩溃
}
```

> 测试矩阵为规范级证据载体：每条例子对应一条 §14.2/§14.3 判据（A1↔AUDIT002_Leak，A3↔KIND_MIX，…）。
> 实现层（godot-csharp 生成器 + Roslyn Analyzer 工程）须让上述测试全绿方视为 L2/L3 完备性「已证」。

---

## 15. 文档历史

> 【QED-A8】原列 `## 14` 与「编译期工具层完备性规范」编号冲突（iter55 PO-55-12），本文档历史改列 §15；历史行内提及的旧编号描述其写作时刻状态，不改写。

| 版本 | 日期 | 变更 |
| ------ | ------ | ------ |
| v0.1 | 2026-08-18 | 初始 PDR，基于三原语 (read/write/occupy) + 区间 Grade |
| v1.0 | 2026-08-19 | 重大架构演进：引入 algeff 启示，采用 Set<Claim> 替代区间；引入 Entity-as-Data 架构 |
| v2.0 | 2026-08-19 | 完整 PDR，包含数学定义、类型系统、审计发现、架构决策、风险评估 |
| v3.0 | 2026-08-19 | 完整 PDR：含数学定义、类型系统、审计发现；当时声明「21 开放问题收敛、0 阻塞」（后被 50 轮审计证伪）|
| v3.0-FINAL | 2026-08-20 | 整合社区发布策略 + 完整 Godot API 映射；**经 50 轮独立审计（audit/iter01–iter50）发现 §14 收敛声明不实：实际 ≥8 高优先根因 open、~108 PO-I* open、~40 高 I- 缺口** |
| v3.0-FINAL-rA | 2026-08-20 | **良性定义修订 A/B/C（收口全部 10 个高优先根因）**：§3.1 Claim=/ScopeId⊆/SizeVal/⊤/Signature 分桶/合成命名空间；§3.2 ∪幂等/⊔半格/Compatible 全函数+对称/ω/S×ω；§3.3 net(S,scope)/Peak 统一/weight；§7.1 QueueFree mode=release；§8.1 默认 Unknown 标记+release-class 白名单+§8.3 EffectOverride/AcceptDeviation 校验；§9.1 Deviation 分母下界；§12.2 AUDIT002/003 size 归一。收敛状态：**数学层阻塞 0 个，10 根因全部给出可机械执行的良性定义；工具层完备性（L2/L3）仍依赖实现** |
| v3.0-FINAL-rA2 | 2026-08-20 | **稳定点审计修复（收口 stability01 的 ST-01~04）**：删除 L349-382 残留未修订旧版 §3.2/§3.3 重复块（ST-01，文档级矛盾已消除，可作为规范引用）；§3.1.4a 扩展合成命名空间归一覆盖 `Self("signal_"+s)≡SignalBus(s)`（ST-02）；§3.1.5c 新增 DeviationVal 载体、§9.1 CalculateDeviation 返回类型 double→DeviationVal 以表达 ⊤（ST-03）；§3.1.3 ScopeId 增加 `Shell` 构造子收纳 §7 的 shell_scope/global_scope（ST-04）。**稳定点达成：修订块自洽、无重复定义、10 根因全 resolved（根8 工具层留口除外）** |
| v3.0-FINAL-rA3 | 2026-08-20 | **源码核实 + 工具层完备性规范（收口 A2/B1/B2/C2/C3）**：从 Godot 开源源码核实 release-class 权威清单（修正 §8.1 占位笔误）+ 引擎无 VRAM/对象硬上限（§3.1.5 ⊤ 保守成立）；新增 §14 L2/L3 soundness/completeness 判据 + C# 测试矩阵。 |
| v3.0-FINAL-rA4 | 2026-08-20 | **复审计收口（收口 iter51 的 8 open）**：①删 `## 15.文档历史` 重复标题（章节倒挂）；②补 §3.1.5c DeviationVal 定义体（原 rA2 仅改引用未补体，幽灵定义已落地）；③§3.1.2 扩 Occupancy/Callback/Input 构造子 + §3.1.4a 裸资源名→构造子缩写映射表（audio_channel/animation_state/callback/memory/... 全部可机械归一）；④§9.1 代码 `deviation>0.2f` 改 `deviation is double d && d>0.2`（先判 ⊤ 再比数值）；⑤术语表 PDR 重复行由格式化合并；⑥§3.2.5 历史 cardinality 形式 Peak 标注为废弃、以 §3.3.2 size-求和为准；⑦§3.1.5b merge_I 显式套用 §3.1.5a ⊤ 律。**iter51 的 8 open 全部文档内闭合；仅剩实现类缺口（godot-csharp 工程落地 §14 测试矩阵）为 out-of-scope** |
| v3.0-FINAL-rA5 | 2026-08-20 | **iter52 复核 + 落盘修正**：iter52 报告 #1（双标题）未落盘——实为 rA4 的 `edit` 因 markdownlint 重排回滚；rA5 用 ctx_edit 真正删除 `## 15.文档历史`（L1087），现仅剩 `## 14.文档历史`。iter52 的 #5（术语表 PDR 重复）与 #N1（rA4 虚假收敛）经 ctx_grep 复核为**误报**：术语表现在仅一行 `**PDR**`（格式化已合并），rA4 历史陈述属实。当前全文 `## 15` 零命中、术语表无重复。**结论：iter51 的 8 open 在 rA4/rA5 全部真实闭合；iter52 3 项实为 1 真实（已修）+2 误报（已证伪）** |
| v3.0-FINAL-rA6 | 2026-08-20 | **iter53 复核修正（rA5 误判证伪）**：iter53 用 read 实证术语表 `**PDR**` 重复行（L996/L997）真实存活，rA5 的「误报」自述不实（rA5 的 ctx_grep `**PDR**` 因 `**` 被当正则零命中，为假阴性）；rA6 用 ctx_edit 删除 L997 重复行，术语表现确仅一行。全文 `## 15` 零命中、双标题已删、§3.1.5c/§3.1.2/§3.1.4a/§9.1/§3.2.5/§8.1/§14 全部闭合。**iter51 的 8 open + iter52/iter53 复核项现已全部真实闭合；仅剩 godot-csharp 工程落地 §14 测试矩阵为 out-of-scope 实现类缺口** |
| v3.0-FINAL-rA7 | 2026-09-06 | **QED 迭代 A1–A8 收口**（路线与对账：`audit/qed/ROADMAP.md` + `audit/qed/PO55-TRIAGE.md`，iter55 PO-55-01..18 全部三分闭合）：①§2.1/EFFECT_SCRIPT 双⊤两轴分辨（population-⊤ 豁免 / time-⊤ Leak，MA-002 旧表述废止）；②§3.1.4/§3.2.1/§3.2.5/§3.3 多重性载体推导（Set 刻意幂等 + P0-4 重复拒 + size×ω；弃 max-over-copies）；③§3.1.3b 单向包含定稿（Global 唯一最大元、Shell 补表、Loop 归属决策）；④§3.1.4a 常量实例保守合并（PO-55-08 已声明盲区）；⑤§3.2.4 ⊔ 四元组配对键 + Signature(b):=∅；⑥§3.3.1 有符号区间值域与序；⑦§8.2 断表缝合 + ED-004 ∞→[1,⊤] + 术语表同步；⑧双 ##14 编号修复（文档历史改列 §15）。全部决策附测试钉（QedP0A1/A5/A7*Tests + 既有 ScopeOrderTests 等） |
