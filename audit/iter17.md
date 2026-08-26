# Iter17 独立审计

## 范围 / 结论摘要

**范围**：跨章审计 MA-004「occupy 峰值与净变化混淆已解决」（L354）。论证分两层：
- **结构层**：Set<Claim> 作为唯一载体、net/Peak 为派生函数，是否在定义层面消解「峰值 vs 净变化」的混淆；
- **使用层**：派生函数相对真实资源轨迹（执行多重集）是否 sound + complete；集合幂等性与多重计数的关系及其对 net/Peak 的冲击；§7 QueueFree / Instantiate 的资源标识配对能否支撑 DO-9 泄漏检测链（L319 → L1030 → L1042-1062）。

**结论摘要**：
1. **结构层 partially discharged**：net/Peak 成为 Signature 上的良定义函数（L309-338），「两个独立原语可能不一致」的混淆在定义层面确已消除。
2. **使用层 open，且发现一处硬性内部矛盾**：∪ 幂等（L166/L190/L255）与 §12.2/§14.4 的累加语义直接冲突——20 个相同 Texture 字段的 identical claims 在集合语义下坍缩为 1 条，AUDIT003 测试期望的 [1280,1280]（L936/L947/L956/L1047）在 §3 代数下不可达。MA-004「已解决」在此点上被本文档自身证伪。
3. **Peak 对有限循环失真**：(S × ω) 中所有 copy_i 的 scope 均标注为同一 Loop(id)（L296），copy_1(c)=copy_2(c)（五元组相等），集合坍缩使 ω 从 Peak 中完全消失；DO-8（L23）对有限 ω 未 discharge。
4. **net 的 ⊤ 兜底算术不封闭**：「未知计为 ⊤ 上界」（L320）在带符号减法下未定义（ℕ* 无负数，§3.1.5a L143-150 只定义 +/×/max/min）。
5. **QueueFree↔Instantiate 配对链断裂**：`new_id`（L639）为运行期新鲜标识，静态分析无配对规则；`release(memory, self.size)` 与 `occupy(memory, scene.estimated_size)` 的 size 表达式不同源；递归释放子节点（L705）无对应 release claims。

---

## 逐命题小节

### P17-1 结构层：Set<Claim> 使 net/Peak 良定义为派生函数

| 项 | 内容 |
| --- | --- |
| **命题** | MA-004：采用 Set<Claim> 后，net 和 peak 是派生度量而非原语，occupy 峰值与净变化混淆已解决（L354）。 |
| **数学性质** | net, Peak : Signature × ScopeId → Measure 为全函数；Signature 为 join-semilattice 元素（∪ 幂等/交换/结合，由 L166 + L190 保证）。 |
| **状态** | **partially discharged（仅结构层）** |
| **论证** | 定义层面成立：net(S)/net(S,scope)（L310-318）、Peak(S,scope)（L327/L337）均为 Signature 上显式给定的 Σ/max 表达式，输入唯一（Signature）、输出唯一，不存在「同一系统维护两份 occupy 状态导致 net 与 peak 不一致」的结构可能。「混淆」的原含义（v1.0 时代二元组 (net,peak) 需手动同步）确实被单载体消除。**但**「派生度量」只保证 well-definedness，不保证 derived value ≈ 真实运行的净变化/峰值。后者属使用层，见 P17-2 ~ P17-6。文档将两层混在一个「已解决」里，是状态标注过强。 |
| **行号** | L166, L190, L252-256, L293-301, L309-338, L354 |

### P17-2 幂等 vs 多重计数：net 的等式条件定理

| 项 | 内容 |
| --- | --- |
| **命题** | 集合幂等不破坏 net 的前提是「执行轨迹到 Claim 的映射在 net 正贡献项上单射」。 |
| **数学性质** | 设真实执行产生 Claim 多重集 Mult(S)，doc 计算基于像集合 Img(S)=Mult(S)/=。则：discrepancy ≝ net_doc − net_true = Σ_{c∈Img} (m_c − 1)·s_c·size(c)，其中 m_c = Mult 中 c 的重数，s_c = +1（create/move）或 −1（release）（同 Claim 必同符号，因五元组含 mode）。**推论**：net_doc = net_true ⟺ ∀c: m_c = 1 ∨ size(c) = 0。 |
| **状态** | **discharged（作为带前提的定理）；前提在实际负载中普遍不成立 ⇒ 使用层 open** |
| **论证** | 同 Claim 同号故无抵消通道。反例：同一方法内两次 `Instantiate(Explosion)` 且 scope/size 标注相同 ⇒ Mult 有 2 条 occupy(memory,[s,s],create)，Img 坍缩为 1 ⇒ net_doc = s，net_true = 2s，泄漏量被低估一半；若其中一次配对 QueueFree 则 true net = s 而 doc net = 0 ⇒ DO-9 漏报（false negative）。这不是实现 bug，而是 L255 明文选择的幂等语义的必然后果。 |
| **行号** | L166, L190, L255, L310-318, L319-320 |

### P17-3 幂等 vs 多重计数：AUDIT003 内部矛盾（硬矛盾）

| 项 | 内容 |
| --- | --- |
| **命题** | §14.4 AUDIT003_Budget_Exceeded 的期望值在 §3 代数下不可推导。 |
| **数学性质** | 20 个相同字段产生 20 条 identical claim c = occupy(memory,[64,64],create,…)。由 L255，S₁;…;S₂₀ = {c}（单元素）。故 net(S) = [64,64] ≠ [1280,1280]。 |
| **状态** | **文档内部矛盾，明确指出** |
| **论证** | L936/L956/L1047 三处断言累加结果 ×20 = [1280,1280] 并与 GlobalBudget [512,512] 比较；而 L252-256 的顺序组合 ∪ 幂等 + L190「相同 Claim 合并一次」机械地禁止该累加。两章不可能同时为真。修复方向（最小补充）：Signature 底层改为 Multiset<Claim> 或 Map<Claim,Count>（以分配点/allocation-site 为键保幂等去重的良性部分），或要求 size 携带乘子维度 count∈ℕ。注意 §12.2 手写「×20」说明作者直觉上需要多重性，与 §3 的集合语义自相矛盾。 |
| **行号** | L190, L252-256, L936, L947, L956, L1046-1048 |

### P17-4 Peak 在有限循环下的 ω 消失（欠近似）

| 项 | 内容 |
| --- | --- |
| **命题** | 对有限 ω ≥ 2，§3.2.5/§3.3.2 给出的 Peak 不随循环次数放大。 |
| **数学性质** | copy_i 将 S 中每条 claim 的 scope 改标为 Loop(id)（L296，id 无迭代索引）。故 ∀i,j: copy_i(c) = copy_j(c)（五元组逐字段相等，L169-191），(S×ω) 作为 Signature 坍缩为 re-scoped S。于是 max_{i∈1..ω} Σ_{c∈copy_i(S),…} c.size 对所有 i 取相同值，Peak(S×ω, Loop(id)) = Peak(S₁次, Loop(id))，与 ω 无关。 |
| **状态** | **open（证明缺口）** |
| **论证** | 反例：`for i in 10: var e = Scene.Instantiate(); AddChild(e)`，每次 occupy(memory,[8,8],create,Loop("l"))。真实峰值 ≈ 80；文档代数给 8。仅当 ω=⊤ 时走 L297 的 ⊤ 兜底，A2 判据（L1031）也只覆盖 ω=⊤。⇒ DO-8（L23「循环内资源分配静态报警」）对可数有限循环未 discharge。与 P17-2/P17-3 同根：**多重性在集合载体上无处安放**——这正是 MA-004 声称已解决的混淆在新形式下的残留。 |
| **行号** | L23, L293-301, L327, L337, L1031 |

### P17-5 Peak 缺少生命周期区间（过近似方向）

| 项 | 内容 |
| --- | --- |
| **命题** | Peak 公式 Σ over c.mode≠release 不含存活区间信息，实际计算的是「作用域内全部非 release 占用之和」，即 lifetime-blind 上界，而非峰值。 |
| **数学性质** | 真实 peak = max_t Σ{size(c) : c 已 create 未 release 于时刻 t}；文档 peak = Σ 全部非 release size。恒有 doc ≥ true（保守安全，无漏报），但当非重叠生存期的分配共存于同一 scope 时产生任意大的误报。例：scope 内先 create A、release A、再 create B、release B ⇒ doc = |A|+|B|，true = max(|A|,|B|)。 |
| **状态** | **asserted（保守方向，安全性成立但「峰值」名不符实）；与 P17-4 的欠近似方向相反，两类误差不抵消** |
| **论证** | 文档从未声明 Peak 是上界近似；L327 注释称「并发占用 size 之和的最大值」，但集合载体无法表达时序并发，「并发」一词在 Signature 上不可定义。这是命名与语义的偏差：应改注「Peak 为 lifetime-blind 保守上界」，或引入区间标注（claim 携带 [t_enter,t_exit] 抽象区间）。 |
| **行号** | L323-330 |

### P17-6 net 的 ⊤ 兜底算术不封闭

| 项 | 内容 |
| --- | --- |
| **命题** | 「Unknown 映射在 net 中计为 ⊤ 上界」（L320）在现有载体上未定义。 |
| **数学性质** | net 是带符号和：Σ(+size) − Σ(−size)。ℕ* = ℕ∪{⊤} 无负元；§3.1.5a（L143-151）仅定义 +/×/max/min/compare，未定义减法，更未定义 ⊤ 参与减法（x − ⊤ = ?；⊤ − x = ?）。若把「上界」解释为 sup{net}，需要先定义 net 的值域排序扩展（如 ℤ ∪ {−∞,⊤} 或区间载体 [lo,hi]⊂ℤ*），文档没有做。 |
| **状态** | **open（证明缺口）** |
| **论证** | fail-closed 意图正确，但机械执行时会卡在「如何计算」：含一条 Unknown occupy 的 Signature，其 net_doc 是什么值、如何与 0 比较（L319 判定 net>0）均无答案。最小补充：net 返回类型改为 IntervalVal ⊂ ℤ*∪{⊤}，Unknown 项贡献 [0,⊤]，泄漏判定改为 lo>0 或 hi 触发人工确认。 |
| **行号** | L143-151, L310-320 |

### P17-7 QueueFree ↔ Instantiate 资源标识配对检查（DO-9 检测链）

| 项 | 内容 |
| --- | --- |
| **命题** | §7.1/§7.4 的 Claim 映射足以支撑 A1 泄漏检测（L1030「基于 §3.3.1 net(S,scope)>0 判定」）。 |
| **数学性质** | 配对关系 Pair(create-claim, release-claim) 应为 resource 相等 + mode 互补 + 存活性包含；文档未给出任何一条的形式化。 |
| **状态** | **open（三处断裂 + 一处判据不一致）** |
| **论证** | 断裂如下：<br>(a) **新鲜标识不可静态化**：Instantiate emit `create(tree, new_id, …)`（L639），QueueFree emit `release(tree, self.id, …)`（L610）。new_id 是运行期 EntityId/Node id，静态分析期无值；self.id 是另一语法表达式。二者按 3.1.4a 归一后是否相等取决于 new_id 的静态指派规则——文档完全没有该规则（如 allocation-site uid）。无此规则则 tree 分量的配对率恒 0，net(tree)>0 恒真 ⇒ AUDIT002 全量误报或被迫走路径分析。<br>(b) **memory 分量 size 不同源**：create 侧 `scene.estimated_size`，release 侧 `self.size`（L639 vs L610）。resource 归一后同为 Memory(uid="mem")（L180 缩写表）可配对，但 size 区间不等（动态场景 create 侧 [1,⊤]）时 net 的 ± 项不相消，残留正值 ⇒ 需区间相消规则（[a,b]−[c,d] 的载体现不存在，见 P17-6 同根）。<br>(c) **递归释放未建模**：L705 实证 queue_free 递归释放全部 children，但 §7.1 QueueFree 仅 emit self 的两条 release；children 经 AddChild 占用的 `occupy(tree, node.id, create)`（L608）永无 release 对应 ⇒ 凡「父节点统一释放子节点」的正常模式必报假泄漏，或迫使 analyzer 绕开 net 判定。<br>(d) **判据内部不一致**：AUDIT002 文本为控制流路径判据「代码路径无 QueueFree → 报警」（L929、L949），A1 声称基于 net(S,scope)>0（L1030），测试注释又写「no release pair」（L1044）。三种判据（路径可达性 / net 数值 / 配对存在性）不等价：路径存在 QueueFree 但位于永不执行分支时，net 判据报警而路径判据可能不报。文档须择一为准并证明其余一致。 |
| **行号** | L319-320, L471, L608, L610, L639, L703-716, L929, L949, L1030, L1042-1044, L1060-1062 |

### P17-8 附带发现：mode=move 在 net 正项中的地位悬空

| 项 | 内容 |
| --- | --- |
| **命题** | net 把 move 与 create 同记 +size（L312/L317），但 iter27 修订后 §7 全表已无任何 API emit move（原 move 生产者 QueueFree 已改 release，L610）。move 成为只有消费者语义、无生产者、且单向膨胀 net 的死模式。 |
| **数学性质** | 若未来某 API emit move，全局 net 将 +size 而资源并未新增（移动≠获取），系统性高估 net ⇒ 假泄漏。move 的正确记账应是 scope 转移（−旧 scope，+新 scope，全局 0）。 |
| **状态** | **asserted（潜在不一致，当前无实例触发）** |
| **论证** | Compatible 仍为 (create,move)/(move,release) 开绿灯（L276-283），说明 move 语义仍被期待使用；但 net 的记账法与 move 语义矛盾。二选一：从 net 正项剔除 move 并定义转移规则，或在 §7 明示 move 的预期生产者并给出 −size 对侧。 |
| **行号** | L276-283, L310-318, L610 |

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| --- | --- | --- | --- | --- |
| PO-17.1 | net_doc = net_true（使用层 soundness of net） | open | 引入 Multiplicity 载体（Map<Claim,Count> 以分配点为键），并在 §3.3.1 重述 Σ 为加权求和；给出 P17-2 定理作为退化情形 | L166, L255, L310-318 |
| PO-17.2 | AUDIT003 累加 [1280,1280] 可从 §3 推导 | open（内部矛盾） | 解决 PO-17.1 后修订 §14.4 测试期望的推导链；或声明 §12.2 累加发生在 Analyzer 层而非 Signature 层并给出两层接口 | L936, L956, L1046-1048 |
| PO-17.3 | Peak 随有限 ω 放大（DO-8 完备性） | open | copy_i 的 scope 携带迭代索引 Loop(id,i)，或 Peak 公式显式乘 ω（配合 PO-17.1 的 Count 维度）；补 §14.4 有限循环正例测试 | L23, L293-301, L1031 |
| PO-17.4 | Peak 是真实峰值的保守上界 | asserted→需重述 | 在 §3.3.2 注明 lifetime-blind 上界语义；可选：Claim 增加 liveness 区间字段收紧 | L323-330 |
| PO-17.5 | 含 Unknown 项的 net 可计算且 fail-closed | open | net 返回类型改为区间/ℤ* 载体，定义 ⊤ 参与 ± 的规则，泄漏判定改为 lo>0 ∨ hi=⊤ | L143-151, L310-320 |
| PO-17.6 | Instantiate↔QueueFree 静态配对规则 | open | 定义 new_id 的静态指派规则（allocation-site uid ∈ ResourceId），写入 §3.1.2/§7.4；定义 Pair 判定的三条充要条件 | L608-610, L639 |
| PO-17.7 | 递归释放的 claims 完整性 | open | QueueFree 映射扩展为 self+descendants 的闭包 release 集，或声明 Analyzer 对 parent-free 模式的豁免规则并证明无漏报 | L608, L610, L705 |
| PO-17.8 | AUDIT002 三种判据（路径/net/pairing）一致性 | open | 择一为权威判定（建议 pairing-based net），其余降为实现提示；补一致性命题与证明 | L929, L1030, L1044 |
| PO-17.9 | move 在 net 中的记账语义 | asserted | §3.3.1 剔除 move 或定义转移规则；同步 §7 说明 move 的合法生产者 | L276-283, L310-318 |

## 新发现缺口清单

1. **G1（最高优先，硬矛盾）**：集合幂等 vs 累加语义的章节级冲突（P17-3）。MA-004 的核心机制（Set + ∪）与预算累加用例互斥，必须引入多重性载体才能同时满足 DO-3 与 §12.2/§14.4。
2. **G2**：有限循环的峰值放大缺失（P17-4），ω 在集合载体上被幂等吞噬；DO-8 仅对 ω=⊤ 成立。
3. **G3**：net 的带符号算术在 ℕ* 上不封闭，「⊤ 上界兜底」不可机械执行（P17-6）。
4. **G4**：new_id 静态指派规则缺失，tree 分量配对率为零，A1 的 net 判据实际不可操作（P17-7a）。
5. **G5**：递归释放未建模 ⇒ parent-free 正常模式系统性假阳性（P17-7c）。
6. **G6**：AUDIT002 的路径判据与 A1 的 net 判据并存且未经一致性证明（P17-7d）。
7. **G7**：Peak 的「并发」语义在无时序信息的集合载体上不可定义，现值为 lifetime-blind 上界（P17-5）。
8. **G8**：move 成死模式且记账语义与其生命周期直觉相反（P17-8）。

**总评**：MA-004 的「已解决」应降级为「结构层已解决（well-definedness），使用层 9 项 PO open」。混淆未被消灭，而是从「双原语不一致」转化为「幂等载体丢失多重性与时序」的新形式；PO-17.1/17.3/17.6 为阻塞级（分别影响 DO-9/AUDIT003、DO-8、A1 的可证性）。
