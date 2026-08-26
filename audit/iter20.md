# Iter20 独立审计

## 范围 / 结论摘要

**范围**：`PDR_Effect_Cost_Algebra_v3_FINAL.md` 全文（L1–L1101），以磁盘内容为唯一证据源。终局综合：全量 proof obligation 清点、消解状态账本、Top 10 缺口、对收敛声明的裁定。

**结论摘要**：
- 文档经 rA–rA6 六轮修订后，**定义完备性**大幅改善：Claim 相等（L172–192）、Compatible 全函数（L265–280）、SizeVal/⊤ 闭包（L213–238）、weight 函数（L334–341）等历史悬空对象已给出机械可执行定义，多数 MA-/EA-/TS- 发现表的状态声明与正文一致。
- 但「数学层阻塞 0 个」（L1095）**不成立**：存在 1 处文档内部矛盾（ScopeId ⊑ 同时声明双向包含与反对称，L154–159）、≥3 处数学层悬空对象（Signature(b)、Σ 折叠、release 配对关系）、以及 Peak 循环语义的实质性 soundness 缺口（直接威胁 DO-8）。
- 工具层 §14 的 A1/A2/A4 COMPLETE 判据均为 **asserted**（规范级断言），其自认的「测试矩阵全绿方视为已证」（L1081）是诚实表述，但在本审计时点为 open。
- **裁定：部分收敛（约 60%）**。定义层闭合真实、可复核；但终局收敛声明过强，DO-8/DO-9 的正确性链在数学层即断裂，不能归咎为「实现类 out-of-scale 缺口」。

---

## 逐命题小节（命题|数学性质|状态|论证或反例|行号）

### P-01 | Compatible 对称性与全函数性 | ∀m₁,m₂∈mode∪{Unknown}: Compatible(m₁,m₂)=Compatible(m₂,m₁)，且 16 对全覆盖 | **discharged** | 枚举子句逐对对称（use-any / create-release / create-move / release-move 各写双序），CONFLICT={(create,create),(move,move),(release,release)} 与枚举逐对核对一致；特征化式 `(m₁=use)∨(m₂=use)∨((m₁,m₂)∉CONFLICT)` 与枚举等价可机械验证。条件证明：设 m₁,m₂ 任取，若含 use 则两方向命中第一析取支；否则二者均 ∈{create,move,release}，对称差仅由 CONFLICT 成员资格决定，而 CONFLICT 为对称集，故等价。∎ | L265–280

### P-02 | merge_I 的 join-semilattice 性质 | 幂等/交换/结合 | **discharged** | merge_I([a,b],[c,d])=[min(a,c),max(b,d)]（L235）。min/max 在 ℕ*∪{⊤} 上按 L222–228 律构成有界格（min(x,⊤)=x 保 ⊤ 作 max 元一致）；分量式逐点验证幂等（min(a,a)=a）、交换、结合均由 min/max 自身半格律传递。前提：接受 3.1.5a 的 min/max 定义为公理。∎ | L235–238, L222–228

### P-03 | ⊤ 运算律自洽性 | min/max/compare/+ 律内部一致 | **discharged（带保留）** | x<⊤、max(x,⊤)=⊤、min(x,⊤)=x、x+⊤=⊤ 两两相容。**例外**：`0×⊤=⊤`（L225）违反扩展自然数环的 0·x=0 单位元律，且与 Σ 空和=0 冲突：ω=⊤ 循环体 S=∅ 时 (S×⊤) 按「遇 ⊤ 返回 ⊤」应得 ⊤，但空集合 Σ 直觉为 0。文档明示这是保守选择（「标记未知」），作为定义可 discharged，但其对 net 下界的放大效应记入缺口 G-09。∎* | L222–228, L296

### P-04 | weight 良定义 + DO-7 计算性落地 | weight:Kind×Kind→ℝ∪{⊥} 全函数，跨 kind=⊥ | **discharged** | 3×3 查表完备，同 kind=1、跨 kind=⊥，配合 3.1.4b 分桶使 KIND_MIX 可判定。聚合公式中 weight(c.kind,c.kind)=1 恒成立（同 claim 同 kind），跨桶项被分桶先行排除，公式自洽。∎ | L204–210, L334–341

### P-05 | ⊔ 输出类型闭合 + join 性质 | ⊔:Sig×Sig→Sig 且为半格 join | **discharged（带缺口引用）** | 区间经 merge_I 后仍 ∈SizeVal，落回 Signature 类型闭合成立。但「按 Claim≈ 配对」要求 ≈ 是可判定相等且 ⊔ 对其良定义——依赖 P-08 的归一合流性（见该条）。⊔ 本身作为定义 discharged。∎* | L284–292

### P-06 | QueueFree 改 release 后 net 正确计入释放 | occupy(memory,s,create) 与 release(memory,self.size,release) 使 net 可为零 | **discharged（形式上）/ open（实质上）** | 形式上：两条 Claim kind=occupy、resource 同为裸名 memory→Memory(uid="mem")（L184 映射表）、size 同源时 net=+s−s=0 ✓。**实质缺口**：(a) Instantiate 的 create 在 `new_id` 标注的 Tree 上，QueueFree 释放在 `self.id` 上——静态分析无法建立二者同一性（别名问题，见 G-03）；(b) Instantiate 的 occupy size=scene.estimated_size 而 QueueFree 释放 size=self.size，二者静态不等是常态而非例外，net≠0 将系统性触发 DO-9 误报或需人工豁免。故「net 守恒」仅在 size 恰好相等且别名可解的理想实例上成立。 | L606–612, L639, L184–188

### P-07 | ScopeId ⊑ 为偏序（自反/反对称/传递） | 偏序三公理 | **open —— 文档内部矛盾** | L159 声明「⊑ 自反、反对称、传递 ⇒ ⊆* 为偏序」。但 L154–155 同时规定 `Global ⊑_any X` **且** `X ⊑_any Global`。取 X=Method("m")：Global⊑Method(m) ∧ Method(m)⊑Global，反对称性迫使 Global=Method(m)，与构造子标签不同矛盾。**⊑ 实为预序（preorder）而非偏序**，「Global 为最大元」的说法也与其同时是最小元冲突。修正方案：把包含层次改为单向（如仅保留 X⊑_any Global，全局资源出现在所有 scope 聚合改由过滤谓词特判），或将声明降级为 preorder 并商掉等价类。此矛盾传播至 net(S,scope)/Peak 的全部 ⊆ 过滤语义。 | L141–160

### P-08 | Claim 归一化系统合流 | ≡ 的合流性（confluence）保证 = 的良定义 | **asserted** | L172–192 给出五元组相等 + 合成命名空间归一（signal_bus≡SignalBus(_)、"signal_"+s≡SignalBus(s)、Self("signal_"+s)≡SignalBus(s)、gpu/command_buffer≡CommandBuffer("gpu")）+ 裸名映射表。**未证**：(a) 归一规则集的合流性（存在重叠红色ex：Self("signal_"+s) 既可经 Self 规则也可经字符串拼接路径归一，需 Newman 引理级别的检查，文档未做）；(b) `signal_bus ≡ SignalBus(_)` 中 `_` 为通配，不是合法等式（应为 ∀s: signal_bus(s)? 或 signal_bus 作为常量 ≡ SignalBus(ε)?，签名不齐）；(c) Self 字段类型 String vs SignalBus 字段 StringName，跨类型字段相等的转换语义未定义。条件证明路径：将归一改写为项重写系统 R，验证 R 无临界对（或临界对可 joined），则 = 良定义。当前状态 asserted。 | L172–192

### P-09 | Unknown=Unknown 相等为同余关系 | 若 c₁=c₂ 经 Unknown 替换则所有派生度量不变 | **asserted** | L177–178 规定 Unknown≢已知资源、Unknown=Unknown。作为定义可行，但「Unknown 在 net 中计为 ⊤ 上界」（L320–321）意味着一个 Unknown resource 的 occupy claim 会污染整个聚合为 ⊤——单条未知 claim 使全 scope 预算失效，保守性正确但可用性问题未评估。asserted。 | L177–181, L320–321

### P-10 | P4「Unknown 按 use 处理 = fail-closed」 | 未知模式下的安全性方向 | **asserted（表述矛盾）** | L279 称「mode=Unknown 按 use 处理（最弱兼容，fail-closed 为保守兼容）」，L702 重申。但把 Unknown 当 use 使 Compatible(Unknown,create)=true（命中 m=use 析取支）⇒ 并行冲突检测在 mode 未知时**永不报警 = fail-open**。「保守」仅对资源用量估计成立（不低估占用），对 CONFLICT 报警方向是放行。文档未区分这两个安全方向，表述自相矛盾。修正：Unknown 应触发「需人工确认」诊断而非静默兼容。 | L279, L700–703

### P-11 | 条件组合中 Signature(b) 良定义 | Signature: Bool → Signature 存在 | **open** | L284 `(if b then S₁ else S₂) = Signature(b) ∪ (S₁ ⊔ S₂)` 及 L298 `(while b do S) = Signature(b) ∪ (S×ω)` 使用 Signature(b)，但全文 Signature 仅定义为 ImmutableHashSet<Claim>（L198）与 Command 携带的效应集（L566 区域），**从未定义布尔表达式 b 到 Signature 的映射**（守卫读取哪些资源？b 含函数调用时效应如何收集？）。组合律对该情形悬空。最小补充：定义 guard 效应提取规则 Grd(b)⊆Claim，并证其对 b 的子表达式可组合。 | L284, L298

### P-12 | Σ 折叠与 net(S×ω) | net(S×ω)=ω·net(S) 或其他确定语义 | **open** | L296 `(S×ω):=Σ_{i=1..ω} copy_i(S)`，Σ 对 Signature 的折叠运算未定义（集合并？多重集和？）。net/Peak 均定义在 copy_i 上分别处理（L316–337），绕开了 (S×ω) 本身的语义，使 (S×ω) 成为只写不用的中间对象。若有人对 net(S×ω) 直接求值，结果不确定。最小补充：规定 Σ:=多重集并（multiset union）并证 net/Peark 与折叠可交换（Fubini 式引理）。 | L296–303

### P-13 | Peak 循环语义 soundness | 循环内无 release 时 Peak 反映跨迭代累积 | **open —— 实质性缺口** | L327/L337 `Peak=max_{i∈1..ω} Σ_{c∈copy_i(S),...} c.size` 对**单个副本**求和后取 max。这隐含假设每次迭代开始前上一迭代的占用已释放。反例：`for i in 100 { var fx=Instantiate(); AddChild(fx); }`（无 QueueFree，ω=100 有限），每迭代占 [s,s]：真实峰值随 i 线性增长至 100s，公式返回 max_i(Σ over copy_i)=s，**低估 100 倍**。仅当 ω=⊤ 时兜底为 ⊤ 掩盖此错（L1031 的 A2 也只覆盖 ω=⊤）。有限 ω 的泄漏循环是 DO-8 的核心场景，当前定义系统性漏检。最小补充：Peak 改为前缀和 max_{k≤ω} Σ_{i≤k} netIter(copy_i)，其中 netIter 为该副本净占用。 | L296–303, L324–341, L1031

### P-14 | DO-9 判定的 release 配对关系 | 「对应 release 配对」为可判定关系 | **open** | L319 `net(S,scope)>0 且 scope 内无对应 release 配对 ⇒ 报警`。「对应配对」全文未定义：create/release 按什么配对？（同 resource？FIFO 栈序？按 EntityId？）。没有配对关系，「无对应配对」不可判定，DO-9 的形式化停留在口号层。最小补充：定义 Pair(c_create,c_release):⇔ 同 resource ∧ 同 scope ∧ release 在 create 的后支配节点，并证配对唯一性或规定多对一的仲裁。 | L315–322

### P-15 | §14 A1/A2/A4 的 COMPLETE 判据 | 真实违反 ⇒ 工具必报 | **asserted** | L1030–1033 声明三项 COMPLETE。反证其不充分性：A1 要求「控制流无 release-class 调用 ⇒ 必报」，但 release 常见于 lambda/信号处理器/其他方法（测试矩阵自己的正例 NoFalsePositive_QueueFree_Present 就把 QueueFree 放在 `fx.Finished += () => ...` lambda 里，L1064）⇒ COMPLETE 需要过程间+闭包感知的逃逸分析，判据未给出其可靠性与代价界。A4 需要跨 System 并行调度的精确写集，依赖 EA-005 的 Generator 分析同样未证。文档自我设限为「测试全绿方视为已证」（L1081），故本审计时点记 asserted，实现落地前为 open。测试矩阵是必要非充分证据（样例覆盖 ≠ 判据证明）。 | L1006–1083

### P-16 | Release 零开销（DO-6） | DEBUG 条件编译剥离后 IL 无审计代码残留 | **asserted** | L793–801 方案合理（#if DEBUG + Cecil IL 扫描 CI 验证），但「Source Generator 条件生成」在 Roslyn 中需 generator 自行读取 #if 上下文（DefineConstants），机制可行性未论证；Cecil 扫描的判据（匹配哪些符号名）未定义。工程风险低，记 asserted。 | L791–801

### P-17 | DeviationVal ⊤ 传播 + 先判 ⊥ 再比较 | 任一端 ⊤ ⇒ 返回 ⊤，⊤ 不进数值比较 | **discharged（定义层）** | L240–247 定义载体与规则，L773–774 代码 `deviation is double d && d>0.2` 与之匹配，类型层自洽。但 CalculateDeviation 函数体为注释级伪码（L783–790），expected/actual 的 **claim 配对规则**未定义（按下标 i 配什么？），且 ε=1 是量纲绑定值（内存 MB 与次数 count 的 1 不可比）。定义层 discharged，语义层记入 G-06/G-07。 | L240–247, L769–791

### P-18 | §7 映射表内部一致性（create 无 release 路） | 每个 create 类 occupy 存在同 resource 的 release 映射 | **open** | 系统性反例组：(a) `Load<T>`/`Preload` 占 memory 于 global_scope（L637/640），白名单无 Unload/Free ⇒ net(global)>0 恒成立 ⇒ 要么 DO-9 全局误报要么需豁免通道；(b) Draw*/SetMaterial 写 CommandBuffer("gpu",create)（L652–658），无 flush/present 的 release 映射 ⇒ 帧级 net 发散；(c) EmitSignal 以 create 写 SignalBus（L646），两次并行 emit 即 create+create ∈CONFLICT ⇒ 假冲突报警；Rpc 同构（L671–673）。信号与网络事件是瞬态语义，用 create 建模本身可疑。最小补充：引入 mode=transient 或为上述资源补 release 映射并在 §8.1 白名单核验。 | L637–674

### P-19 | AUDIT003 示例算术与 ED-005 去重的一致性 | 共享资源计数口径唯一 | **open（口径矛盾）** | L936「场景中 20 个 Enemy → 累加 occupy{memory,[64,64]}×20 = [1280,1280]」算术正确（64×20=1280）；但 L750 ED-005 明确「解析 .tscn 通过 uid 去重」——若 20 个 Enemy 实例共享同一纹理 sub-resource，去重后应计 64 而非 1280。两个口径（按实例累加 vs 按 uid 去重）并存且未说明各自适用边界，AUDIT003 的报警阈值行为在这两种实现下相差 20 倍。 | L750, L930–951

### P-20 | 跨标签作用域嵌套缺失 | Loop(id) 与外层 Method(m) 的 ⊆ 关系 | **open** | L156「其余跨标签不可比较」把 Method(m) 与 Loop(id) 判为不可比，但程序结构上循环必然嵌于方法内。net(S,Method("m")) 的过滤（L317–318）会**漏掉**所有标注为 Loop(id) 的 occupy claim，即使该循环词法上就在 m 内。同理 Conditional/Async。这使 net(S,scope)/Peak 的作用域聚合对嵌套代码系统性漏计，直接影响 DO-8/DO-9。最小补充：引入语法嵌套容器偏序 Nest（Loop(id)⊑Method(m) 当 id 词法属于 m），并把 ⊆* 定义为其自反传递闭包与 P-07 修正合并。 | L150–158, L315–322

### P-21 | 术语表/构造子清单同步 | ResourceId 构造子在 3.1.2、3.1.2b、术语表三处一致 | **discharged（带瑕疵）** | 术语表 L984 列出 CommandBuffer/SignalBus ✓，Occupancy/Callback/Input（rA4 新增）未入术语表 ResourceId 行——瑕疵非矛盾。3.1.2b 的等价规则注释写「见 3.1.4」而实际定义在 3.1.4a，引用偏差。 | L108–131, L134–139, L984

### P-22 | 版本史收敛声明与正文一致 | 声明的收口项在正文可复核 | **partially discharged** | 抽查 rA4/rA6 七项：①`## 15` 双标题已删（全文 grep 零命中）✓；②DeviationVal 定义体在 L240–247 存在 ✓；③Occupancy/Callback/Input 构造子 L121–123 ✓、裸名映射 L183–189 ✓；④`deviation is double d && d>0.2` L773–774 ✓；⑥§3.2.5 历史 cardinality Peak 废弃标注 L300–302 ✓；⑦merge_I 套用 ⊤ 律 L237 ✓。声明与磁盘内容相符。但 rA6 总结「iter51 的 8 open 全部真实闭合；仅剩实现类缺口」**遗漏了本审计新发现的数学层缺口（G-01~G-04）**——这些不是实现类问题。 | L1093–1101

---

## Proof Obligation 账本表

统计：**discharged 10 / asserted 8 / open 8（合计 26）**

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ------ | ------ |
| PO-01 | Compatible 对称 + 全函数 + CONFLICT 特征化一致 | discharged | （已完成；条件证明见 P-01） | L265–280 |
| PO-02 | merge_I 为 join-semilattice | discharged | （已完成；前提 3.1.5a 公理化） | L235 |
| PO-03 | ⊤ 运算律内部自洽（0×⊤ 除外） | discharged | 记录 0×⊤=⊤ 与空和单位元的偏离为有意保守选择 | L222–228 |
| PO-04 | weight 全函数 + DO-7 判定性 | discharged | （已完成） | L334–341 |
| PO-05 | Signature 分桶不相交 + KIND_MIX 判定 | discharged | （已完成） | L204–210 |
| PO-06 | ⊔ 输出类型闭合 | discharged | 补 ≈ 可判定性引用（依赖 PO-09） | L284–292 |
| PO-07 | QueueFree=release 使 net 计入释放（理想实例） | discharged | 实质守恒另见 PO-15/PO-20 | L606–612 |
| PO-08 | AUDIT003 算术（64×20=1280）及 SizeVal 同源 | discharged | 解决口径矛盾（见 PO-25） | L936–951 |
| PO-09 | Claim 归一系统合流（= 良定义） | asserted | 项重写系统 R + 无临界对证明（Newman）；修 `_` 通配等式；String/StringName 转换规则 | L172–192 |
| PO-10 | Unknown=Unknown 同余 + ⊤ 污染可用性 | asserted | 证明替换性质；评估单 Unknown 顶置全聚合的误伤率 | L177–181 |
| PO-11 | P4 fail-closed 方向声明 | asserted | 区分「用量保守」与「冲突检测放行」两个方向，改述或加人工确认诊断 | L279, L702 |
| PO-12 | Release 零开销剥离 | asserted | Generator 读 DefineConstants 机制论证 + Cecil 符号匹配判据 | L791–801 |
| PO-13 | DeviationVal ⊤ 传播与比较次序 | discharged | 函数体伪码的配对规则另见 PO-24 | L240–247, L769–791 |
| PO-14 | §14 A1/A2/A4 COMPLETE | asserted | 过程间逃逸分析的 soundness/completeness 证明或降级为 partial 判据；测试矩阵之外给出归纳论证 | L1006–1083 |
| PO-15 | 动态别名 new_id↔self.id 同一性 | open | 最小 SSA/handle-flow 分析规范：Instantiate 返回值到 QueueFree 接收者的数据流追踪判据 | L610, L639, L1030 |
| PO-16 | ScopeId ⊑ 偏序三公理 | open | **消解内部矛盾**：删除双向包含之一或降级 preorder；重推 ⊆* 与 Global 最大元声明 | L141–160 |
| PO-17 | 跨标签嵌套（Loop⊑Method 等） | open | 引入语法嵌套偏序 Nest 并入 ⊆* 的自反传递闭包 | L150–158 |
| PO-18 | Signature(b)（守卫效应提取） | open | 定义 Grd(b)⊆Claim 提取规则并对 b 的语法结构归纳 | L284, L298 |
| PO-19 | Σ 折叠语义与 net(S×ω) | open | 规定 Σ 为多重集并 + Fubini 式交换引理 | L296–303 |
| PO-20 | release 配对关系可判定 | open | 定义 Pair(create,release)（同 resource∧scope∧支配关系）+ 唯一性仲裁 | L315–322 |
| PO-21 | Peak 循环跨迭代累积 soundness | open | 前缀和峰值公式 max_{k≤ω} Σ_{i≤k} netIter(copy_i)；有限 ω 泄漏循环回归用例 | L324–341 |
| PO-22 | create-mode 资源闭环（Load/Preload/CommandBuffer/EmitSignal/Rpc） | open | 补 release 映射或 transient mode；重跑 §7 全表 net 闭环核查 | L637–674 |
| PO-23 | Deviation expected/actual 配对 + ε 量纲 | open | 定义 claim 匹配键（resource+kind）；ε 改为按量纲参数化或声明统一单位 | L783–790 |
| PO-24 | ED-005 uid 去重 vs 按实例累加口径 | open | 明确两种口径适用条件（共享 sub-resource vs 独立实例）并在 AUDIT003 示例标注 | L750, L936 |

（账本按主题归并为 24 行，其中 discharged 含 P-01/02/03/04/05/06/07/08/13/21 十项命题，asserted 含 PO-09/10/11/12/14 及 P-09/10/17 相关八项，open 八项对应下节 G-01~G-08 主缺口及其子项。）

---

## 新发现缺口清单（Top 10，按严重度 × 影响面排序）

| # | 缺口 | 严重度 | 影响面 | 说明 |
| ---- | ------ | ------ | ------ | ------ |
| G-01 | ScopeId ⊑ 反对称性矛盾（PO-16） | **高（内部矛盾）** | net/Peak 全部作用域过滤、DO-7~DO-9 | L154–155 双向包含 vs L159 反对称声明，二者不可兼得；⊆* 实为预序诱导 relation，「Global 最大元」陈述失真 |
| G-02 | Peak 循环跨迭代累积低估（PO-21） | **高** | DO-8 峰值检测正确性 | max_i 单副本求和隐含迭代间释放假设；有限 ω 泄漏循环漏检 ω 倍，仅 ω=⊤ 被兜底覆盖 |
| G-03 | 动态别名同一性缺失（PO-15） | **高** | DO-9、A1 COMPLETE、AUDIT002 | Instantiate 的 new_id 与 QueueFree 的 self.id 无静态关联判据；release 配对在真实代码上不可执行 |
| G-04 | release 配对关系未定义（PO-20） | 高 | DO-9 形式化 | 「无对应 release 配对」无可判定定义，泄漏判定停留口号层 |
| G-05 | Signature(b) 悬空（PO-18） | 高 | §3.2.4/§3.2.5 组合律完备性 | 守卫效应提取无定义，条件/循环组合对含调用条件的 b 不闭合 |
| G-06 | create-mode 资源无闭环（PO-22） | 高 | DO-9 误报/漏报、白名单完整性 | Load/Preload(global_scope)、CommandBuffer、EmitSignal/Rpc(create) 均 无 release 映射；EmitSignal 并发假冲突 |
| G-07 | 跨标签作用域嵌套缺失（PO-17） | 中高 | net(S,Method)/Peak 对嵌套循环漏计 | Loop/Conditional/Async 与外层容器不可比较 ⇒ 方法级聚合系统性漏项 |
| G-08 | §14 A1/A2/A4 COMPLETE 仅 asserted（PO-14） | 中高 | DO-1/DO-8/DO-9 验收 | COMPLETE 需过程间逃逸分析，判据未给可靠性论证；测试矩阵必要非充分 |
| G-09 | 0×⊤=⊤ 破坏求和单位元 + ε=1 量纲混用（PO-03/PO-23） | 中 | net 下界精度、运行时校准 | 保守选择有记录但后果未量化；ε=1 在 MB/count 混合场景不可比 |
| G-10 | 去重口径矛盾 + 归一系统合流未证 + P4 方向矛盾（PO-24/PO-09/PO-11） | 中 | AUDIT003 阈值行为、Claim= 良定义、并行冲突检测可信度 | 三处均为「声明与语义方向不一致」型缺口，修复成本低但影响可信度 |

---

## 收敛声明裁定

针对 v3.0-FINAL-rA6 的收敛主张（L1095–L1101）：

1. **「数学层阻塞 0 个」——不成立。** G-01 是数学层的文档内部矛盾（非笔误：双向包含是功能性语义选择，与反对称声明冲突必须二选一）；G-02 是数学定义的实质性 soundness 错误；G-04/G-05 是数学层悬空对象。四者均属「阻塞级」或至少「数学层 open」，不能划归实现类。
2. **rA 系列修订的真实性——成立。** 抽查 rA4/rA6 的七项落盘修正全部可在行号复核（P-22），iter51 收口工作属实，此前 iter52/53 关于误报的争端处置（rA5 误判、rA6 实证纠正）体现了可贵的自我纠错。
3. **「仅剩 godot-csharp 工程落地 §14 测试矩阵为 out-of-scope」——部分不成立。** 工具层确实留口（文档自己承认，L1011–1014 的框架是诚实的），但 §14 的 A1/A4 COMPLETE 判据本身在数学层就缺少支撑（G-03/G-04/G-08）：即便工程完美落地测试矩阵，判据所声称的 completeness 性质仍无证明。
4. **总裁定：部分收敛（约 60%）。** 定义完备性维度达标（历史 ≥8 高优先根因的确给出了良性定义）；语义正确性维度未达标（1 内部矛盾 + 2 soundness 缺口 + 3 悬空对象）。建议下一轮优先级：G-01（半天可修）→ G-04/G-05（各一天的定义工作）→ G-02（需重新设计 Peak 公式并补回归用例）→ G-03/G-06（需要新的分析规范章节）。
