# Iter14 独立审计

**范围**：跨章一致性专项——DO-7「read/write/occupy 不可混算，编译期报错」（L22）与 §3.1 单一 `Set<Claim>` 混合 kind 做 ∪ 组合（L63、L166、L254–262）是否矛盾；§3.3.2 peak 跨 kind 求和是否混算；MA-007 权重函数是否真正定义（L357 vs L331–338）；DO-7 有无编译期执行机制（L22、L199–200、L1032）。

## 结论摘要

1. **「单一 Set<Claim> 混合 kind 做 ∪」与 DO-7 在数学层不构成矛盾**（discharged）：∪ 是集合层组合，不执行跨 kind 算术；DO-7 禁止的是**派生度量聚合层**的跨 kind 加法。由于 kind 是 Claim 五元组的分量（L87），按 kind 分桶的分解存在且唯一，可机械执行——§3.1.4b（L194–200）正是该分区。两者分层后相容。
2. **但 DO-7 的落地链条存在三处真实缺口**：
   - §3.3.2 Peak 的两个公式（L330、L337）均**无 kind 过滤**，字面上对混合 kind 的 claim 集合做 size 求和——这正是 DO-7 禁止的混算点，与 §3.1.4b「peak 仅在同桶内聚合」（L199）直接冲突；
   - L337 的「加权聚合公式」中 `weight(c.kind,c.kind)` 只在对角线上求值（恒 =1），**⊥ 分支在该公式中不可达**——公式与 L338 注释「跨 kind ⇒ ×⊥」自相矛盾，加权公式是空洞的（等价于不加权）；
   - KIND_MIX 的**编译期触发面未定义**：文档从未定义用户可见的何种语法构造/API 调用构成「混算」，A3 判据（L1032）与测试 KIND_MIX_CrossKind_Blocked（L1050）因此无可检验的规范对象。
3. MA-007 的 weight 函数本体**已定义**（3×3 全函数表，L331–335，discharged）；但其「已解决」声明中的**强制执行部分仅为 asserted**——依赖 §14.3 A3 工具实现，而文档自认工具层留口（L1081、rA6 历史 L1105）。
4. 新发现：§8.1 默认规则 emit 的 `Unknown(unknown, Unknown, Unknown, scope)`（L698）第二槽位为 kind=Unknown ∉ {read,write,occupy}（L88），**破坏 §3.1.4b 分桶的全性**（三分桶不含 Unknown 桶）。

---

## 逐命题小节

### 命题 14-1：单一 Set<Claim> 混合 kind 做 ∪ 与 DO-7 相容

| 项 | 内容 |
| --- | --- |
| 数学性质 | 设 Sig ⊆ Claim 为任意混合 kind 的签名。定义分区 π(Sig) := (Sig∩Sig_read, Sig∩Sig_write, Sig∩Sig_occupy)，其中 Sig_k := {c ∈ Sig \| c.kind=k}。因 kind 是 Claim 的确定性分量（L87），π 是 Sig 的唯一划分：π(Sig) 两两不相交、并为 Sig。∪ 组合（L254、L260）作用于集合层，不改任何 Claim 的 kind 分量，故 π(S₁∪S₂) 各桶 = 对应桶之并 |
| 状态 | **discharged**（条件证明） |
| 论证 | DO-7 的「不可混算」约束的是聚合算子（Σ）的定义域，不是集合的存储结构。§3.1.4b 明确「单桶 ImmutableHashSet\<Claim\> 仍是底层存储，分桶为聚合时的类型层约束」（L200），即存储层允许混合、聚合层禁止跨桶——分层消解了表面矛盾。前提：所有聚合算子必须显式带 kind 过滤（见命题 14-3，此前提当前对 Peak 不成立） |
| 行号 | L22、L63、L87、L166、L194–200、L254、L260 |

### 命题 14-2：§3.1.4b 分桶的全性被默认 Unknown 规则破坏

| 项 | 内容 |
| --- | --- |
| 数学性质 | 分桶规则要求 ∀c∈Signature：c.kind ∈ {read,write,occupy} 且落入恰一桶。但 §8.1 默认规则 emit `{ Unknown(unknown, Unknown, Unknown, scope) }`（L698），其第二个槽位按 Claim 五元组顺序为 kind=Unknown，而 L88 定义 kind ∈ {read, write, occupy}——kind=Unknown 不属于 Kind 域，也不属于任何一桶 |
| 状态 | **open** |
| 论证 | §3.2.3 P4 只收口了 mode=Unknown（L279），未收口 kind=Unknown。若默认规则的 Unknown claim 要进入 Signature 并参与聚合，则三分桶划分不是全函数：此类 claim 无桶可归，KIND_MIX 判定对其无定义。另注意 L698 的构造子 `Unknown(unknown, Unknown, Unknown, scope)` 只有 4 个参数，与 3.1.1 的五元组（含 size?）形状不符，本身即非良构项 |
| 最小补充 | 二选一：(a) 将 Kind 扩为 {read, write, occupy, unknown} 并新增 Sig_unknown 第四桶（聚合遇 unknown 桶一律返回「需人工确认」，不参与数值 Σ）；(b) 规定默认规则产物在入 Signature 前归一为 kind=read ∧ kind=write 双 claim（保守上界），保持三桶全性。同时修正 L698 构造子的参数形状 |
| 行号 | L88、L199、L698 |

### 命题 14-3：§3.3.2 Peak 字面上执行跨 kind 求和（违反 DO-7）

| 项 | 内容 |
| --- | --- |
| 数学性质 | 反例：设 S 含 c₁ = read(tree, "root", use, Method(m), [1,1]) 与 c₂ = occupy(memory, uid, create, Method(m), [64,64])（均可由 §7.1 GetTree / §7.4 Load 映射产出，scope 同为 Method(m)、mode≠release）。代入 L330 公式：Peak(S, Method(m)) = max_i ([1,1] + [64,64]) = [65,65]——节点计数维度与 MB 维度相加。这是量纲混算 |
| 状态 | **open**（文档内部矛盾：L330/L337 vs L199 + L22） |
| 论证 | 对照同章其余度量：net 显式过滤 c.kind=occupy（L313–319）、read/write 显式过滤 c.kind=read/write（L343–344）——三者均为 kind-纯。唯独 Peak 的两个公式（L330 主定义、L337 加权形式）过滤条件只有 scope⊆scope ∧ mode≠release，**缺 kind 过滤**。故 §3.1.4b「peak 仅在同桶内聚合」（L199）对 §3.3.2 自身的字面公式不成立：数学层内部自相矛盾，不能仅靠「实现时记得分桶」消解 |
| 最小补充 | 将 Peak 定义为族：∀k∈{read,write,occupy}，Peak_k(S,scope) := max_{i∈1..ω} Σ_{c∈copy_i(S), c.scope⊆scope, c.kind=k, c.mode≠release} c.size；需要总峰值时显式声明取哪一桶或经 weight 折算。或至少加 c.kind=occupy 过滤（语义上「并发占用 size 之和」本就只应对 occupy 有意义——read/write 的 size 是操作次数不是驻留量） |
| 行号 | L22、L199、L313–319、L330、L337、L343–344 |

### 命题 14-4：L337 加权 Peak 公式空洞且与其注释矛盾

| 项 | 内容 |
| --- | --- |
| 数学性质 | weight : Kind×Kind → ℝ∪{⊥} 中 weight(k,k)=1 对一切 k 成立（L333）。L337 公式每项只计算 weight(c.kind, c.kind)——两个参数恒相同 ⇒ 恒落在对角线 ⇒ 恒 =1 ⇒ 该公式恒等于 L330 的无权公式。⊥ 仅在 k₁≠k₂ 时取值（L334），而公式中不存在以不同 kind 实参调用 weight 的位置 |
| 状态 | **open**（文档内部矛盾：公式 L337 vs 注释 L338「跨 kind ⇒ ×⊥」） |
| 论证 | L338 声称「跨 kind ⇒ ×⊥ ⇒ 编译期 KIND_MIX 报错」，但该行为不是公式的数学后果，而是注释外加的操作性断言。作为规范，此公式无法推导出任何 KIND_MIX；它给出的语义反而**允许**跨 kind 求和（每项 ×1 后照加），即恰好把 DO-7 禁止的计算形式化了 |
| 最小补充 | 删除 L337「聚合公式（含 weight）」块，或将 weight 的强制点改为类型层：聚合 API 的域限定为单桶 Sig_k（命题 14-3 的 Peak_k 族），weight 仅在显式跨桶折算表达式 weight(k₁,k₂)·Peak_{k₂}(...) 中出现，k₁≠k₂ 时该表达式非类型良构 ⇒ 编译期报错 |
| 行号 | L333–338 |

### 命题 14-5：MA-007 权重函数本体已定义

| 项 | 内容 |
| --- | --- |
| 数学性质 | weight 为 3×3 全函数表：对角线（read,read)/(write,write)/(occupy,occupy)=1，非对角 6 对 =⊥；值域 ℝ∪{⊥}；扩展政策显式（新折算对须查表登记，L336）。表完全、无歧义、可机械求值 |
| 状态 | **discharged**（本体定义）；注：weight 不是度量、不要求乘性/三角不等式，仅作查表禁用函数，无需更多代数律 |
| 论证 | MA-007 行（L357）称「已解决」，就「函数缺失」这一原始缺口而言属实。遗留两点边界：① weight(k,k)·c.size 中「标量 × SizeVal 区间」运算未显式定义（§3.1.5a 只给出 ⊤ 的 +/×/max/min 律，未给一般标量乘区间；1·[a,b]=[a,b] 平凡成立但未写明）；② 函数定义 ≠ 强制执行（见命题 14-6） |
| 行号 | L331–336、L357 |

### 命题 14-6：DO-7 的「编译期报错」有无执行机制

| 项 | 内容 |
| --- | --- |
| 数学性质 | 文档给出的机制链：§3.1.4b 分桶（L199，「类型层约束」）→ §3.3.2 weight=⊥（L334）→ §14.3 A3 Analyzer 判据「跨 kind 聚合（weight=⊥）⇒ KIND_MIX 编译错误」（L1032）→ 测试 KIND_MIX_CrossKind_Blocked（L1050） |
| 状态 | **asserted**（机制被声明但关键环节无规范内容） |
| 论证 | 三处断链：① 「分桶为类型层约束」（L200）但全文未定义承载桶的 C#/类型表示（如幻影类型参数 Signature<K>、或 Sig_read/Sig_write/Sig_occupy 三个独立类型）——没有类型表示就没有「编译期」可言，只有运行期/分析器检查；② A3 的触发模式未定义：派生度量 net/read/write/Peak 均由工具自身实现且（除 Peak 外）结构性 kind-纯，用户代码中何构造构成「跨 kind 聚合」无一处定义——KIND_MIX 因此可能**在任何真实用户代码路径上永不可触发**；③ §8.3.1 只规定 EffectOverride 不得豁免 DO-7（L734）、§12.2 示例只用同 kind 比较（L958），均为消极约束，不提供触发面。结论：DO-7 验收标准「read/write/occupy 不可混算，编译期报错」目前既无可触发的输入、也无承诺的报错载体类型 |
| 最小补充 | 定义公开聚合 API 的类型签名（如 `Peak(Sig_occupy, ScopeId)`、`Read(Sig_read)`），使跨桶调用成为 C# 类型错误（L1 层即可报错，早于 Analyzer）；并在 §14.3 A3 写明其检测的具体语法模式（对哪个 API/表达式的哪类实参报警），补一条「跨桶调用被拒」的反例测试 |
| 行号 | L22、L199–200、L334、L734、L958、L1032、L1050 |

### 命题 14-7：L199 悬空引用「见 3.3.2b」

| 项 | 内容 |
| --- | --- |
| 数学性质 | —（文档结构缺陷） |
| 状态 | **open** |
| 论证 | §3.1.4b 写「跨桶相加需显式 weight（见 3.3.2b）」（L199），但全文不存在编号 3.3.2b 的小节；weight 定义在「定义 3.3.2」内部的注释块中（L331–338）。引用悬空使读者无法从 §3.1.4b 机械定位强制点 |
| 最小补充 | 将 L199 引用改为「见 §3.3.2 weight 块」，或把 weight 提升为独立编号「定义 3.3.2b（量纲权重）」 |
| 行号 | L199、L331–338 |

---

## Proof Obligation 账本

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ------ | ------ |
| PO-14-1 | 单一 Set<Claim> 混 kind 存储 + ∪ 组合与 DO-7 相容（经 kind 分区） | discharged | 无（证明见命题 14-1；前提是所有聚合算子 kind-纯，由 PO-14-3 承接） | L63, L166, L194–200 |
| PO-14-2 | 默认 Unknown claim 可归入三分桶之一（分桶全性） | open | Kind 扩 unknown 第四桶，或默认规则归一为 read+write 双 claim；修正 L698 构造子参数形状 | L88, L199, L698 |
| PO-14-3 | Peak 公式满足 DO-7（kind-纯聚合） | open | Peak 改为分桶族 Peak_k 或加 c.kind=occupy 过滤 | L330, L337 |
| PO-14-4 | 加权 Peak 公式能实际产生 ⊥/KIND_MIX（非空洞） | open | 删除 L337 公式，或把 weight 强制点上移到聚合 API 类型签名 | L337–338 |
| PO-14-5 | weight 函数为全函数表 | discharged | 无（标量×SizeVal 运算建议随 PO-14-4 一句补记） | L331–336 |
| PO-14-6 | KIND_MIX 存在用户可达的编译期触发面与报错载体 | asserted | 定义分桶的类型表示 + 公开聚合 API 类型签名 + A3 具体语法模式 + 反例测试 | L199–200, L1032, L1050 |
| PO-14-7 | net/read/write 的 kind 纯度 | discharged | 无（显式 kind 过滤已在公式内） | L313–319, L343–344 |
| PO-14-8 | §3.1.4b→§3.3.2 交叉引用可达 | open | 改引「§3.3.2 weight 块」或新增小节编号 3.3.2b | L199 |
| PO-14-9 | MA-007「已解决」声明的完整成立 | asserted | 本体已证（PO-14-5）；强制执行部分待 PO-14-3/14-6 闭合后方可升级 discharged | L357, L1081 |

**统计**：discharged 3 · asserted 2 · open 4。

---

## 本轮新发现缺口清单

1. **【高】Peak 跨 kind 求和（PO-14-3）**：§3.3.2 两个 Peak 公式均无 kind 过滤，与 §3.1.4b「peak 仅在同桶内聚合」及 DO-7 直接矛盾。反例：GetTree 的 read(tree,[1,1]) 与 Load 的 occupy(memory,[64,64]) 同 scope 相加得 [65,65]。这是 DO-7 收口声明（L194「计算性落地」）下的一个未闭合混算点。
2. **【高】加权公式空洞（PO-14-4）**：L337 中 weight 仅在对角线求值恒为 1，⊥ 分支不可达；公式语义反而形式化地**允许**了 DO-7 禁止的跨 kind 加法，与 L338 注释自相矛盾。
3. **【中】KIND_MIX 无用户可达触发面（PO-14-6）**：「编译期报错」（L22）缺少触发它的用户侧语法构造定义与报错载体（类型/诊断 ID 之外的绑定）；现有文本下 KIND_MIX 可能只在工具自身实现的假想调用中出现。
4. **【中】默认 Unknown claim 破坏分桶全性（PO-14-2）**：L698 的 kind=Unknown 不属于 L88 的 Kind 域，亦不属于任何桶；且该构造子仅 4 参，与五元组形状不符。§3.2.3 P4 只收口 mode=Unknown，未收口 kind=Unknown。
5. **【低】悬空引用 3.3.2b（PO-14-8）**：L199 引用不存在的小节编号。
6. **【低】标量×SizeVal 未定义**：weight(k,k)·c.size 用到的「标量乘区间」运算未在 §3.1.5a 律表中列出（平凡情形，随 PO-14-4 补一句即可）。

DONE_ITER_14
