# Iter01 独立审计

- **审计对象**：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（磁盘内容，v3.0-FINAL-rA6 系列）
- **范围**：仅 §3.1 基本对象——Claim / ResourceId / ScopeId / Signature 定义；Set&lt;Claim&gt; 上 ∪ 的结合律/交换律/幂等律/中性元；Claim 相等规则（含 size 缺省归一化）；ResourceId 跨构造子等价与 Unknown⊤ 策略；ScopeId 上 ⊆ 的定义。除该文档外未读任何项目文件。
- **结论摘要**：
  1. ∪ 的三律**可在前提下条件消解**（前提：Claim= 为等价关系），但该前提本身**当前不成立**（归一规则含通配符与双重匹配，见 PO-01）；
  2. 发现一处**硬性内部矛盾**：§3.1.3b 同时声明「⊑ 是偏序（反对称）」并给出 `Global ⊑ X` 与 `X ⊑ Global` 双向规则，二者不相容，偏序声明被反例证伪（PO-04）；
  3. 发现两处**域矛盾**：`kind`/`mode` 枚举封闭（L88/L90）但 §8.1 默认规则与 §3.2.3-P4 使用 `kind=unknown`/`mode=Unknown`（PO-02）；
  4. ResourceId 文法**缺少顶层 `Unknown` 构造子声明**，而 §3.1.4a/§3.3.1 均依赖之（PO-03）；
  5. `Shell` 作用域缺席自反表，使「无未定义项」声明为假（PO-05）。
  其余为可修的中低severity 缺口（哈希契约、区间加减法未定义、中性元未显式声明等）。

---

## 逐命题审计

### P-01 Claim 五元组的良定义性

| 项 | 内容 |
| --- | --- |
| 命题 | Claim := (kind, resource, mode, scope, size?) 为良定义的五元组，kind ∈ {read,write,occupy}、mode ∈ {use,create,release,move} 封闭 |
| 数学性质 | 类型的构造合法性（inhabitability）：每个字段域非空且声明完整 |
| 状态 | **asserted（部分矛盾）** |
| 论证 | L84–93 给出五元组与封闭枚举。但 (i) §8.1 默认规则 emit `{ Unknown(unknown, Unknown, Unknown, scope) }`（L698）使用 `kind=unknown`；(ii) §3.2.3 性质 P4「mode=Unknown 按 use 处理」（L279）及 L701–702「改为 mode=Unknown」均使用 `mode=Unknown`。二者皆不在 L88/L90 的封闭枚举内。**内部矛盾**：要么扩域（kind/mode 各加 ⊥型 Unknown 元素并修订 3.1.1），要么默认规则非法。另 `size?` 为可选字段但 3.1.4a(L175) 以「缺省视为 [1,1]」做相等——归一时点未定义，见 PO-06 |
| 行号 | L84–93、L175、L279、L698–702 |

### P-02 Claim 相等是等价关系（自反/对称/传递）

| 项 | 内容 |
| --- | --- |
| 命题 | 3.1.4a 定义的 Claim₁ = Claim₂（五元组逐字段相等，resource 经归一化）构成等价关系 |
| 数学性质 | ∀a,b,c: a=a；a=b ⇒ b=a；a=b ∧ b=c ⇒ a=c |
| 状态 | **open（传递性不可证，存在反例风险）** |
| 论证 | 字段 (1)(2)(3)：枚举相等、ScopeId 结构相等、区间相等（3.1.5b）各自显然为等价关系，可消解。字段 (4)：resource「按如下归一化后相等」要求存在归一函数 ρ: ResourceId → ResourceId（全函数、幂等 ρ∘ρ=ρ、汇聚），则 resource 相等 ⇔ ρ(x)=ρ(y) 必为等价关系。但现行规则表使 ρ **不是函数**：<br>① L178 `signal_bus ≡ SignalBus(_)`——`_` 为通配符，非合法字段值，裸名 `signal_bus` 无法确定 SignalBus 的 name 参数；<br>② L185 `gpu ⇒ Gpu(bufferId)`（bufferId 未给）与 L181 `gpu/command_buffer ≡ CommandBuffer("gpu")` 对裸名 `gpu` 双重匹配，归一目标不唯一 ⇒ 非汇聚；<br>③ L185 `memory ⇒ Memory(uid="mem")` 固定 uid，而 §7 表自身以带参形式使用（如 `read(memory, stream.buffer_id, …)` L664、Instantiate 的 `memory(scene.uid)` L658），裸名归一为 uid="mem" 后将与这些带参形式判**不等**，违反「必须按 3.1.2b 归一为同一资源」的意图。<br>故 Claim= 的传递性（进而对称性对归一类的良定义）**未被消解** |
| 行号 | L172–191（尤其 L178、L181、L185–189）、L124–125 |

### P-03 size 缺省归一化

| 项 | 内容 |
| --- | --- |
| 命题 | 缺省 size ≡ 显式 [1,1]，且 Claim 相等在该归一下良定义 |
| 数学性质 | 存在规范嵌入 ι: Claim₅(可选size) → Claim₅([1,1])，使 = 为 ι 后的结构相等 |
| 状态 | **asserted（语义可证，实现层留口）** |
| 论证 | 若在**构造点**归一（size?: Option⟹[1,1]），则载体无 Option，「缺省 ≡ 显式 1」（L175）平凡成立且与 3.1.5b 区间相等兼容——可证。但文档未指明归一时点；若仅在比较时归一而哈希不随之归一，则 `ImmutableHashSet<Claim>`（L166）所需的 `Equals ⇔ GetHashCode` 契约被破坏，∪ 幂等在实现层失效。数学层 discharged（以构造点归一为前提），实现层转 PO-06 |
| 行号 | L166、L175、L209 |

### P-04 Set&lt;Claim&gt; 上 ∪ 的结合律 / 交换律 / 幂等律 / 中性元

| 项 | 内容 |
| --- | --- |
| 命题 | (S₁∪S₂)∪S₃ = S₁∪(S₂∪S₃)；S₁∪S₂ = S₂∪S₁；S∪S = S；∃∅: S∪∅ = S |
| 数学性质 | (Signature, ∪, ∅) 为交换幂等幺半群（semilattice + 单位元） |
| 状态 | **discharged（条件证明）** |
| 论证 | **前提 A**：Claim=（P-02）为等价关系；**前提 B**：Signature 的载体为外延集合（ImmutableHashSet，L166，集合语义由 L166 注释明示）。在 A∧B 下，∪ 即集合论并，三律由外延集合公理直接继承：幂等律额外依赖 Claim= 良定（相同 Claim 只保留一份，L192 注释亦如此主张）；结合/交换与元素结构无关，无条件成立。**缺口**：中性元 ∅: Signature 从未被显式声明（全文无 `∅`/空 Signature 定义行，L166 仅给载体），属 assertable 但未 assert；且前提 A 当前 open（P-02），故本消解为**条件的** |
| 行号 | L166、L192–193、L252–255（3.2.1 引用） |

### P-05 Signature 按 kind 分桶的不相交性（3.1.4b）

| 项 | 内容 |
| --- | --- |
| 命题 | Sig_read ∪ Sig_write ∪ Sig_occupy 三桶两两不相交，构成 Signature 的分区 |
| 数学性质 | 桶划分 = 按 Claim→kind 投影的原像分区：i≠j ⇒ Sigᵢ ∩ Sigⱼ = ∅ |
| 状态 | **discharged** |
| 论证 | kind 是 Claim 相等的分量之一（L173 第(1)条：逐字段相等），故同一 Claim 的 kind 唯一，不可能同时属于两桶；分区性由投影原像的一般事实给出。注意：该消解以 kind 域**不含 Unknown** 为前提——若依 PO-02 扩域，需增加 Sig_unknown 第四桶，否则分桶不完备（默认规则的 Claim 无桶可落） |
| 行号 | L173、L196–200、L698 |

### P-06 ResourceId 跨构造子等价（3.1.2b 合成命名空间）

| 项 | 内容 |
| --- | --- |
| 命题 | `signal_bus ≡ SignalBus(s)`、`"signal_"+s ≡ SignalBus(s)`、`Self("signal_"+s) ≡ SignalBus(s)`、`gpu/command_buffer ≡ CommandBuffer("gpu")` 构成一致的归一系统 |
| 数学性质 | 重写系统 → 汇聚（confluence）+ 终止 ⇒ 规范形唯一 ⇒ ≡ 可判定 |
| 状态 | **open** |
| 论证 | 终止性可信（每规则降低语法复杂度）。汇聚性：`Self("signal_"+s)` 与其参数串 `"signal_"+s` 两条规则重叠，但二者的规范形同为 SignalBus(s)，局部汇聚成立；真正的破坏点是 P-02 所列通配符/双匹配问题：`signal_bus ≡ SignalBus(_)`（L178）目标不定、裸 `gpu` 被 L181 与 L186 双规则竞争。**结论**：意图正确（示例 L124–125 一致），形式化未闭合；修复方式＝把每条 ≡ 改写为**参数完整的函数方程**（如 `signal_bus ↦ λs. SignalBus(s)` 并规定裸名的 s 取值来源，或规定裸名仅允许出现在 size 无关上下文——后者会破坏 Claim= 的语法导向，不建议） |
| 行号 | L115–126、L177–183 |

### P-07 Unknown⊤ 策略（resource 层）

| 项 | 内容 |
| --- | --- |
| 命题 | resource=Unknown ⟺ 静态不可判定；Unknown ≢ 任意已知资源；Unknown = Unknown；net 中 Unknown 计为 ⊤ 上界 |
| 数学性质 | Unknown 为 ResourceId 的吸收顶元（top element）：∀r≠⊥Unknown: r ⊓ Unknown = ⊥（无交），Unknown 与自身相等 |
| 状态 | **asserted（策略自洽）＋open（文法缺口与一处下游不一致）** |
| 论证 | (a) **文法缺口**：ResourceId 文法（L95–113）**未声明顶层 `Unknown` 构造子**；唯一的 Unknown 出现在 `Tree(path: NodePath | Unknown)`（L99）的字段级。而 L182「resource 字段为 Unknown」、L320「未知映射…计为 ⊤」均以顶层 Unknown 为前提 ⇒ 文法不完备（PO-03）。<br>(b) **Tree(Unknown) 与顶层 Unknown 的相等性未定义**：前者是已知构造子 Tree 带未知 path，后者整体未知；按「构造子标签+字段逐位相等」（L179）二者不等，但直觉上均为「树资源不可判定」，需显式裁决。<br>(c) **全体 Unknown 坍缩为单点** ⇒ 两个互不相关的未知资源判同。在 §3.2.2 并行约束下，若二者 mode∈CONFLICT，将触发冲突报警——即**冤枉**（false positive）。这与 §14.3-A5「未知保守 SOUND：…不冤枉」（L1034）存在张力：A5 只对「落默认规则后走人工确认路径」的 soundness 成立，对进入并行组合的 Unknown claims 不成立。需在 §14.3 A5 加限定或在 3.2.2 对 Unknown 资源豁免冲突判定（转人工确认） |
| 行号 | L99、L182–183、L320、L698、L1034 |

### P-08 ScopeId 上的偏序 ⊆（3.1.3b）

| 项 | 内容 |
| --- | --- |
| 命题 | ⊑ 自反、反对称、传递 ⇒ ⊆* 为偏序；Global 为最大元；判定表完备（「无未定义项」） |
| 数学性质 | 偏序公理：(R1) ∀a: a⊑a；(R2) a⊑b ∧ b⊑a ⇒ a=b；(R3) a⊑b ∧ b⊑c ⇒ a⊑c |
| 状态 | **refuted（反例）＋内部矛盾** |
| 论证 | **反例 1（反对称）**：L154 `Global ⊑_any X` 与 L155 `X ⊑_any Global` 联立，取 X=Method(m)：得 Method(m) ⊑ Global ∧ Global ⊑ Method(m)，由 R2 ⇒ Method(m) = Global。同理一切具体作用域坍缩为单点，与 L142「宽松包含」意图和 L156「具体作用域互不比较除非同名」直接矛盾。L159 声称的性质在本规则集下**为假**。<br>**反例 2（传递性 vs 不可比声明）**：Method(m) ⊑ Global（L155）与 Global ⊑ Loop(l)（L154）经 R3 ⇒ Method(m) ⊑ Loop(l)，与 L156「其余跨标签不可比较」矛盾。<br>**反例 3（判定完备性）**：`Shell` 构造子（L138）缺席 L145–152 自反表 ⇒ (Shell, Shell) 查表无规则适用，L160「无未定义项」为假。<br>**根因诊断**：文档混淆了两个不同关系——(i) 聚合**过滤谓词** filter(a,b) =「scope=a 的 claim 是否计入 scope=b 的聚合」，其自然定义 filter(a,b) ⟺ (a=b) ∨ (b=Global)（此时 Global 为最大元，R1/R2/R3 全部可验证成立，且恰好表达「全局聚合含全部」）；以及 (ii) L154 注释想要的另一方向「全局 claim 出现在所有 scope 聚合」即 filter′(a,b) ⟺ (a=b) ∨ (a=Global)。filter 与 filter′ 的并列使 Global 同时成为最大元与最小元，任何非平凡偏序不容许。**最小修复**：删除双向包含层次，令 `c.scope ⊆ scope := (c.scope = scope) ∨ (scope = Global)`（即 filter），并把 L154 的「全局资源出现在所有 scope 聚合」改为独立声明或并入 net/Peak 的第二个谓词；同时在自反表补 `Shell ⊑ Shell`。L158 的第二析取支 `(scope = Global)` 在此修复下冗余但无害 |
| 行号 | L141–160（关键：L154、L155、L156、L158、L159、L160）、L138 |

### P-09 SizeVal / merge_I 半格律（支撑 Claim 相等的第 (3) 分量）

| 项 | 内容 |
| --- | --- |
| 命题 | Interval := [lo,hi], lo,hi ∈ ℕ*, lo≤hi 良定义；merge_I 为 join-semilattice（幂等/交换/结合） |
| 数学性质 | (Interval, merge_I) 为半格；ℕ* 上 min/max 关于线序 ≤（compare 律 L227 补全 x<⊤）构成对偶半格 |
| 状态 | **discharged（条件证明）** |
| 论证 | 前提：3.1.5a 的 ⊤ 律（L223–227）使 (ℕ*, max, min) 成为以 ⊤ 为最大元的分配格之载体——min/max 的交换/结合/幂等/吸收在 ℕ 上标准成立，⊤ 律与之相容（max(x,⊤)=⊤ 即 ⊤ 为最大元；min(x,⊤)=x 即吸收）。merge_I([a,b],[c,d])=[min lo, max hi] 逐分量继承半格律。区间良序 lo≤hi 在含 ⊤ 时由 compare 律（∀x: x≤⊤）保证。**残留缺口**：merge_I 是 join（取宽），而 net/Peak 所需的是区间**加法/减法**——二者均未在任何小节定义（见新缺口 G-03）；且 L225 `0 × ⊤ = ⊤` 使 × 丧失零元律（0×x=0 对 x=⊤ 失效），凡引用「乘零消去」的推导全部失效，属声明的保守选择但须记录代数代价 |
| 行号 | L203–213、L221–227、L232–236 |

### P-10 术语表与正文的一致性

| 项 | 内容 |
| --- | --- |
| 命题 | 术语表的 ResourceId/ScopeId 清单与 §3.1.2/3.1.2b/3.1.3 一致 |
| 数学性质 | n/a（文档一致性检查） |
| 状态 | **asserted（不一致，低危）** |
| 论证 | L981 术语表 ResourceId 清单「tree, self, physics, memory, disk, signal, gpu, audio, network, custom, CommandBuffer, SignalBus」遗漏 rA4 新增的 **Occupancy / Callback / Input** 及（隐含的）**Unknown**；L982 ScopeId 清单遗漏 rA2 新增的 **Shell**。不影响数学内容，但违背「机械归一」的可查表性 |
| 行号 | L981–982 对照 L107–111、L118–121、L138 |

---

## Proof Obligation 账本

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ---------------- | ------ |
| PO-01 | Claim=（resource 归一分量）是等价关系 | open | 把 3.1.4a 全部 ≡/⇒ 规则改写为参数完整的全函数方程 ρ；消除 `SignalBus(_)` 通配符；裁决裸名 `gpu`/`memory` 与带参形式的归一目标（建议：裸名仅是「缺省参数」糖，ρ 展开糖后再比对） | L177–191 |
| PO-02 | kind/mode 域与默认规则类型一致 | open | 在 3.1.1 将 kind/mode 域扩为 {…} ∪ {Unknown}，或将 §8.1 默认规则改为专用 `UnknownClaim(scope)` 构造子；同步修订 3.1.4b 分桶（增第四桶或规定 UnknownClaim 单独桶） | L88, L90, L196–198, L698 |
| PO-03 | ResourceId 文法含顶层 Unknown | open | 在 3.1.2 增加 `\| Unknown` 构造子；显式裁决 Tree(Unknown) =? Unknown（建议判不等，因构造子标签不同，符合 L179 规则） | L95–113, L182 |
| PO-04 | ⊑/⊆* 是偏序且判定完备 | open（已证伪） | 按本报告 P-08 最小修复：⊆ := (a=b) ∨ (b=Global)，删除双向包含层次，补 Shell ⊑ Shell；或承认 ⊑ 是预序/过滤关系并撤回「偏序/反对称」声明 | L141–160 |
| PO-05 | ∪ 幂等的实现层前提 | open | 规定 size 归一发生在**构造点**（Claim 智能构造器），并要求生成的 Equals/GetHashCode 同源于归一后的五元组（否则 ImmutableHashSet 幂等失真） | L166, L175 |
| PO-06 | (Signature, ∪, ∅) 的单位元 | open（轻） | 增加一行显式定义 `∅ :⇔ ImmutableHashSet.Empty`，并注明其为 3.2.1/3.2.2 的单位元（顺序/并行与空组合恒等） | L163–167 |
| PO-07 | 区间加法/减法闭包 | open | 在 §3.1.5 系补 `[a,b]+[c,d]=[a+c,b+d]`（含 ⊤ 律传播）；为 net 的 − 定义 monus 或规定「任一端含 ⊤ ⇒ 结果 ⊤」；记录 0×⊤=⊤ 对零元律的破坏及其影响面 | L203–236, L305–312 |
| PO-08 | Unknown 单点坍缩 vs §14.3-A5 soundness | open | 在 3.2.2 增补：c₁.resource=c₂.resource=Unknown 时不触发 CONFLICT 报警，改走「人工确认」通道；或在 A5 声明中加限定 | L182–183, L265–263 区域(L258–263), L1034 |

---

## 新发现缺口清单（本次 iter01 首报）

| ID | 缺口 | Severity | 说明 / 行号 |
| ---- | ------ | -------- | ------------ |
| G-01 | §7 记法与 Claim 五元组字段序冲突 | 中 | Claim := (kind, resource, mode, scope, size?)（L86–92，size 最后）。但 §7 全表采用五槽记法 `(kind, x, y, mode, scope)`，如 `occupy(audio_channel, 1, create, shell_scope)`（L674 附近，audio 小节）第三槽为数值 size；而 `read(memory, stream.buffer_id, use, shell_scope)` 第三槽又像 resource 参数。两种读法不能同时成立，且直接影响 PO-01「机械归一」的可执行性（L184 明言要使裸名可机械归一）。需统一：要么 `(kind, resource, size?, mode, scope)` 写入规范，要么把第三槽并入 ResourceId 的参数（如 Memory(stream.buffer_id)） |
| G-02 | Unknown 单点坍缩引发并行冲突误报 | 中 | 见 P-07(c)/PO-08：Unknown=Unknown 使无关未知资源判同，与 A5「不冤枉」矛盾 |
| G-03 | 区间算术（+/−）未定义即被 §3.3 使用 | 中 | net/Peak/read/write 的求和求差依赖未定义的 Interval 加减法；⊤ 传播律只给了标量 +/×/max/min |
| G-04 | `0 × ⊤ = ⊤` 破坏乘法零元律 | 低（有意保守，须记录） | 任何「占用为零 × 未知循环次数 ⇒ 应为 0」的直觉推导失效，net/Peak 一律升 ⊤；应在 3.1.5a 注明这是刻意的 fail-closed 而非代数疏漏 |
| G-05 | 3.1.2 与 3.1.2b 重复开启同一非终结符定义 | 低 | `ResourceId :=` 出现两次（L96、L117），形式上应合并为一个文法块或在 3.1.2b 标注「文法增量扩展」；当前写法易被机械解析器当作重定义冲突 |
| G-06 | 术语表资源/作用域清单滞后于 rA2/rA4 构造子扩充 | 低 | 见 P-10，L981–982 |
| G-07 | `Tree(path: NodePath \| Unknown)` 与 3.1.4a「构造子标签+字段逐位相等」的组合语义未说明 Unknown 字段的哈希/相等实现 | 低 | 字段级 Unknown 与顶层 Unknown（PO-03）需分别给出相等规则，避免实现层混同 |

---

*审计员：独立隔离进程 iter01。证据仅基于本文档磁盘内容；所有行号可直接复核。*
