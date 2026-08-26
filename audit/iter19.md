# Iter19 独立审计

## 范围 / 结论摘要

- **审计对象**：`PDR_Effect_Cost_Algebra_v3_FINAL.md`（磁盘内容，v3.0-FINAL-rA6 状态），仅基于本文档，未读取任何其他项目文件。
- **范围**：跨章综合。对 `kind ∈ {read, write, occupy}` × `mode ∈ {use, create, release, move}` 共 12 个组合，逐一给出幂等性 / 抵消性 / 单调性 / 可组合性四个数学性质的显式主表；每格标注 discharged / asserted / open 及前提依赖；用 §7 真实 API 映射实例化每格。
- **结论摘要**：
  1. 集合层面的 ∪ 幂等性对全部 12 组合 discharged（依赖 L169–L192 Claim=）；但**语义层幂等缺口 open**：∪ 去重抹除重复执行次数，与 AUDIT001 的「60/sec→1」频率语义（L944–L946）直接矛盾。
  2. 抵消性仅在 occupy 的 create↔release 对上有定义（L309–L321 net）；但 `net(S)`/`net(S,scope)` **不按 resource 分组聚合**（对照 L191 Deviation Σ 明确按资源对齐），跨资源虚假抵消反例可构造 ⇒ 条件 discharged / 根因 open。
  3. 单调性在 read(S)/write(S)/Peak 上可条件证 discharged；net 对 release 维是反单调（by design），但 move 计为 +size 且无源端借记 ⇒ move 存在时 net 高估、泄漏误报，open。
  4. 可组合性由 §3.2.3 全函数 Compatible discharged；但 Compatible 仅作用于 `||`，顺序组合 `;` 无任何配对检查 ⇒ 双重 release（double-free）不可检，open。
  5. **发现 4 处文档内部矛盾**（详见 §C）：Unknown kind 与分桶/weight 体系冲突并使 A5「不冤枉」自相矛盾（最重）；Unknown-mode 按 use 处理与 fail-closed 学说冲突；MoveChild 映射 write/use 与 AddChild write/create 处理不一致且致 move 模式在 §7 全表零实例化。
  6. 12 组合中仅 6 个有 §7 实例；read×{create,release,move}、write×move、occupy×use、occupy×move 共 6 格无实例且无良构性约束禁止它们 ⇒ well-formedness 谓词缺失，open。

---

## A. (kind × mode) 数学性质主表

记号：Claim := (kind, resource, mode, scope, size?)（L84–L92）；Signature 为 ImmutableHashSet<Claim>（L166–L167）；Claim= 按 L169–L192；Compatible 按 L265–L280；net 按 L309–L321；Peak 按 L323–L341。状态标记：**D** = discharged（给出论证或前提已闭合）、**A** = asserted（文档断言但无论证）、**O** = open。

| # | kind×mode | §7 实例 | 幂等性 | 抵消性 | 单调性 | 可组合性 |
|---|-----------|---------|--------|--------|--------|----------|
| 1 | read×use | GetNode(L606)、IsActionPressed(L671)、Position getter(L616 区) | **D**（前提 L169 五元组相等） | **A/N.A.**（read 不入 net 公式，无抵消语义；文档未明示 N.A. 属断言） | **D**（premise: size ∈ ℕ* 非负，L205–L207；则 S⊆T ⇒ read(S)≤read(T)，L347–L349 求和逐项） | **D**（use 最弱权限，Compatible 恒真，L271 第一析取支） |
| 2 | read×create | **无实例** | D（形式上同为集合元素去重） | O（无定义） | O（无良构谓词判定该组合是否合法） | O（同左） |
| 3 | read×release | **无实例** | D（同上） | O | O | O |
| 4 | read×move | **无实例** | D（同上） | O | O | O |
| 5 | write×use | Position setter、SetVolumeDb、anim Seek、**MoveChild(L611)** | **D**（L169） | N.A./A（write 不入 net） | **D**（同 #1 论证，write(S) L351–L353） | **D**（use 最弱）⚠ 但见 §C-3：MoveChild 结构变异标 use 使并行 MoveChild 同节点误判兼容 |
| 6 | write×create | AddChild(L608)、EmitSignal(L646)、DrawRect、Play(L663)、Rpc | **D**（L169） | **O→A**（树/音频/信号生命周期在 write 维**无**抵消度量：net 只看 occupy，L310–L312；DO-9 完全寄生于 occupy 维——文档未证 write 维泄漏不影响 DO 目标） | **D**（write(S) 递增） | **D**（create+create ∈ CONFLICT，L272；并行双 AddChild 同 node.id 必报） |
| 7 | write×release | RemoveChild、Disconnect、Stop、anim Stop | **D**（L169） | A/N.A.（不入 net） | **D**（write(S) 递增；release 不减 write 量） | **D**（release+release ∈ CONFLICT，L272 ⇒ 并行双释放报警；但顺序双释放漏检，见 PO-A5） |
| 8 | write×move | **无实例**（§7 全表 mode=move 零出现） | D（形式） | O | O | D（Compatible 有 move 对，L273–L274，形式全函数）但语义空转 |
| 9 | occupy×use | **无实例** | D（形式） | O（"使用期占用"无净变化语义定义） | O | O |
| 10 | occupy×create | AddChild occupy(tree)·create(L608)、Load memory·create(L637)、Instantiate(L639)、Connect callback(L647)、Play channel(L663) | **D**（L169；注意跨循环副本 scope 不同 ⇒ 不同 Claim 不合并，这是 Peak 得以计数的机制，L293–L301） | **条件 D**：同一 (resource,scope,size) 的 create+release 在 net 中 +size−size=0；**全局 net 未按 resource 分组**（L312/L317 对照 L191 仅 Deviation 对齐资源）⇒ 跨资源虚假抵消反例成立（§B-2）⇒ 配对抵消条件 discharged、聚合抵消 **open** | **D**：net 贡献 +size（L312）；入 Peak（mode≠release 过滤，L325/L339）；size 含 ⊤ 或 ω=⊤ 时单调退化为 ⊤（L224–L231 律） | **D**（create+create 冲突；create+release/move 配对兼容 L272–L273） |
| 11 | occupy×release | RemoveChild、QueueFree memory(L610)、Disconnect、Stop、anim Stop | **D**（L169） | **条件 D**（同 #10，配对抵消的负半边）；独立 release（超量释放）在资源分组缺失下可抵消他资源泄漏 ⇒ 同 **open** | **D**（反单调 by design：net 贡献 −size；被 Peak 的 mode≠release 过滤排除，L325——排除本身 discharged） | **D**（release+release ∈ CONFLICT） |
| 12 | occupy×move | **无实例** | D（形式） | **O**：move 计入 net 正项（L312 `mode∈{create,move}`）但代数中**不存在 move 的源端借记算子**（无 move-out claim 类型），转移语义下 net 必然高估 ⇒ 泄漏误报方向系统性偏差 | **O**（同因） | D（Compatible 形式覆盖 move 对，L273–L274） |

**表级前提汇总**：(i) 全表幂等格共享前提「Claim 五元组字段相等可判定」（L169–L192，含区间相等 L232–L237 与 Unknown 相等 L183–L185）；(ii) 所有单调格共享前提「size ∈ ℕ* 非负 + ⊤ 运算律」（L203–L231）；(iii) 可组合格共享前提「Compatible 为全函数且对称」（L265–L280，P1/P2 可机械验证）。三个前提自身在本版均已 discharged。

---

## B. 逐命题小节（命题 | 数学性质 | 状态 | 论证或反例 | 行号）

### B-1 集合幂等性
- **命题**：∀S, S∪S = S（Signature 层幂等）。
- **性质**：幂等性。
- **状态**：**discharged**（全部 12 组合）。
- **论证**：Signature 是 ImmutableHashSet（L166），元素去重由 Claim= 保证；五元组逐字段相等可判定（枚举相等 / ScopeId 同构造子同字段 L146–L163 / size 区间相等 L232–L233 / resource 归一 L173–L190）。机械完成。
- **行号**：L166–L167, L169–L192, L232–L237。

### B-2 net 的跨资源虚假抵消
- **命题**：net(S,scope)>0 ∧ 无 release 配对 ⇒ 泄漏报警（DO-9，L319–L320）是可靠的。
- **性质**：抵消性。
- **状态**：**open**（配对情形条件 discharged；聚合可靠性未证）。
- **论证（反例）**：net 定义为全体 occupy claim 的带符号求和（L312/L317），**无 resource 分组项**。取 S = {occupy(A,10,create), occupy(B,10,release)}（A≠B，同 scope）：net(S)=+10−10=0 ⇒ 无泄漏报警；实际 A 泄漏 10、B 超额释放 10。对照 L191，Deviation Σ 明确「按资源对齐」，net 却没有同等条款——修订 C 收口 iter37 只加了 scope 分组（L317），resource 分组缺失。**最小补充**：把 net 改写为按 resource 分组的逐资源和（或逐资源和再聚合）。
- **行号**：L309–L321, L191。

### B-3 语义幂等 / 多重性丢失
- **命题**：同一方法内两次相同调用（如两次 `GetNode("../Player")`）的效应被计数两次。
- **性质**：幂等性（语义层）。
- **状态**：**open**（且构成矛盾，§C-4）。
- **论证**：两次调用产生完全相同的 Claim（同 kind/resource/mode/scope，缺省 size 均 [1,1]）⇒ 被 ∪ 合并为一个元素；read(S) 因此只计一次。而 AUDIT001 文案宣称「reduce read{tree} from 60/sec to 1」（L946），ED-006 以 60fps 累加频率效应（L751）——两者都预设多重性进入度量，但基础代数（L123–L125, L169）在结构上抹除之。循环情形靠 copy_i 的 Loop(id) scope 标注区分（L295–L296），非循环重复调用无任何区分机制。
- **行号**：L84–L92, L123–L125, L169–L192, L293–L301, L751, L944–L946。

### B-4 Peak 单调性与 ⊤ 兜底
- **命题**：S⊆T ⇒ Peak(S,scope) ≤ Peak(T,scope)；ω=⊤ 或任一 size=⊤ ⇒ Peak=⊤。
- **性质**：单调性。
- **状态**：**条件 discharged**（前提：区间加法为分量式）。
- **论证**：max 与 Σ 对集合包含均单调（size 非负）；⊤ 律保证 max(x,⊤)=⊤、x+⊤=⊤（L224–L228），ω=⊤ 分支 L298 显式兜底。**残余缺口**：Σ c.size 作用在 Interval 载体上，但 §3.1.5a 只定义了 ℕ* 标量的 +/×/max/min（L222–L231），§3.1.5b 只定义了 merge_I（L232–L237）——**区间加法 [a,b]+[c,d]=[a+c,b+d] 与区间序 ≤ 从未显式定义**，Peak/read/write/net 四个派生度量全部踩在此未定义运算上。
- **行号**：L203–L237, L293–L301, L323–L341, L347–L353。

### B-5 Compatible 全函数性与对称性
- **命题**：Compatible 覆盖 16 有序对且对称。
- **性质**：可组合性。
- **状态**：**discharged**。
- **论证**：CONFLICT={(create,create),(move,move),(release,release)} 三对无序对 ⇒ 16 有序对划分完备（4×4 矩阵，3 冲突对 ×2 对称 + 其余兼容）；对称性由「∉CONFLICT」谓词的对合性立得。机械可验。
- **行号**：L265–L280。

### B-6 顺序组合无配对检查
- **命题**：`(S₁;S₂)` 下同资源双重 release 可被检测。
- **性质**：可组合性。
- **状态**：**open**（命题为假）。
- **论证**：`;` 定义为纯集合并（L120–L125），Compatible 仅作为 `||` 的前置条件（L128–L134）。release;release（如 free 后再 QueueFree，或 Disconnect 两次）在顺序组合下静默通过，CONFLICT 集永不触发。文档未提供「为何只有并行需要兼容检查」的论证；而 double-free 恰是真实 Godot 缺陷类。
- **行号**：L120–L134, L265–L280。

### B-7 Unknown kind 与分桶/weight 体系
- **命题**：未映射 API 的默认 Signature `{ Unknown(unknown, Unknown, Unknown, scope) }` 能无害进入派生度量（A5「不冤枉」，L1034）。
- **性质**：可组合性 / 良构性。
- **状态**：**open**（且构成矛盾，§C-1）。
- **论证**：默认规则 emit 的 kind 字面为 `Unknown`（L698），但 kind 枚举定义为 {read,write,occupy}（L85–L86），分桶只认三桶（L194–L201），weight 定义域为 Kind×Kind 且跨 kind=⊥（L332–L336）。kind=Unknown 的 claim 要么类型非法，要么落入所有桶外 ⇒ 聚合时 weight(Unknown,Unknown) 无定义或=⊥ ⇒ 每个未映射 API 触发 KIND_MIX 编译错误，与 A5「NO 误报」测试（L1073–L1075）直接冲突。二者不可同时成立。
- **行号**：L85–L92, L194–L201, L332–L341, L698–L703, L1034, L1073–L1075。

### B-8 move 的守恒性
- **命题**：move 表示占用的转移，net 对其计 +size 是正确的。
- **性质**：抵消性 / 单调性。
- **状态**：**open**。
- **论证**：net 把 move 计入正项（L312），但 Claim 代数中没有「move-out」负项构造子；转移语义要求源端 −size、目标端 +size。当前定义下任何含 move 的程序 net 严格高估。缓解因素：§7 全表零个 mode=move 实例（QueueFree 已于 iter27 改 release，L610），故缺陷潜伏不触发——但这同时意味着 move 是**死代码模式**（见 §C-3）。
- **行号**：L309–L321, L605–L671（§7 全表无 move）。

### B-9 条件组合 ⊔ 的半格性质
- **命题**：⊔ 是 join-semilattice 的 join（幂等/交换/结合）。
- **性质**：幂等性 + 可组合性。
- **状态**：**discharged**。
- **论证**：⊔ 按 Claim= 配对后逐 Claim 取 merge_I（L285–L290）；merge_I([a,b],[c,d])=[min(a,c),max(b,d)]（L235）是区间上的 join（min/max 幂等交换结合，⊤ 分量经 L236 接入 L224–L231 律）；未配对 claim 直通。逐点继承半格三律。输出闭于 SizeVal（L289）。
- **行号**：L232–L237, L282–L291。

### B-10 循环副本的作用域区分
- **命题**：S×ω 中各副本互不合并（Peak 可计数的前提）。
- **性质**：幂等性（否定面）/单调性。
- **状态**：**discharged**。
- **论证**：copy_i 的 scope 标注为 Loop(id)（L295–L296），不同副本若标注可区分（Loop(id) ⊑ Loop(id) 仅同名可比，L148–L156）⇒ Claim 不等 ⇒ ∪ 不去重。**边界缺口（asserted 级）**：嵌套同名 Loop(id) 或同一 Loop 内多次分配同资源仍会合并——id 的唯一性生成规则未定义。
- **行号**：L146–L163, L293–L306。

---

## C. 文档内部矛盾清单

| # | 矛盾 | 位置 | 严重度 |
|---|------|------|--------|
| C-1 | 默认规则 emit `kind=Unknown`（L698），而 kind 枚举限 {read,write,occupy}（L85）、分桶（L194）与 weight（L332）均无 Unknown 情形 ⇒ 要么类型非法要么 KIND_MIX 洪泛，后者使 §14 A5「不冤枉」（L1034）与其反例测试（L1073）不可能同时为真 | L85, L194–L201, L332, L698, L1034, L1073 | **高** |
| C-2 | P4「mode=Unknown 按 use 处理…fail-closed 为保守兼容」（L279）：Unknown-as-use 使 create∥Unknown 判兼容 ⇒ 冲突检测 fail-**open**；将其称为「保守/fail-closed」与 §8.1/L702/L1034 的 fail-closed 学说语义相反 | L279, L700–L703, L1034 | 中 |
| C-3 | MoveChild 映射 `write(tree,node.id,**use**)`（L611）与同类结构变更 AddChild 的 `write(...,**create**)`（L608）处理不一致；且副作用是 §7 全表 mode=move 零实例，使 §3.2.3 的 move 配对律（L273–L274）与 net 的 move 正项（L312）成为无实例死代码 | L273–L274, L312, L608, L611 | 中 |
| C-4 | ∪ 幂等抹除重复调用次数（L123–L125, L169），而 AUDIT001 文案以「60/sec→1」报告频率收益（L946）、ED-006 以 60fps 累加（L751）：度量口径与代数载体不匹配 | L123–L125, L169, L751, L946 | 中 |

---

## D. Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|------------------|------|
| PO-A1 | 每 kind 存在合法 mode 子集（well-formedness） | open | 增加 WF ⊆ Kind×Mode 关系（建议 WF=read×{use}, write×{use,create,release}, occupy×{create,release}±{move}），并在 Claim 构造处强制 | L84–L92 |
| PO-A2 | net 聚合的抵消可靠性 | open | net(S,scope) 改为按 resource 分组求和后再比较（复用 L169 Claim= 的 resource 归一即可机械化） | L309–L321, L191 |
| PO-A3 | 区间算术封闭性 | open | 显式定义分量式 [a,b]+[c,d]、Interval 上的 max/≤ 及其与 ⊤ 律（L222–L231）的相容性引理 | L203–L237, L323–L341 |
| PO-A4 | 多重性载体 | open | 引入 multiset<Claim> 或 frame-count 标注，或证明 AUDIT001/ED-006 的频率语义可在现集合代数内表达 | L123–L125, L751, L946 |
| PO-A5 | 顺序组合的 double-release 检测 | open | 为 `;` 增加逐资源 release-after-release / create-after-create 序检查，或显式声明 out-of-scope 并说明理由 | L120–L134, L265–L280 |
| PO-A6 | Unknown kind 的类型地位 | open | 二选一：①默认规则改 emit 具体 kind（保守取 occupy+mode=Unknown）并入桶；②扩展 Kind 加 Unknown 构造子并定义 weight(Unknown,·)=⊥ 外的专用通道，同步修 A5 | L85, L194, L332, L698, L1034 |
| PO-A7 | move 的守恒语义 | open | 定义 move-out 负项或在 net 中将 move 移出正项；否则删除 move 模式消除死代码 | L273–L274, L312, L611 |
| PO-A8 | Loop(id) 标识唯一性 | open | 定义 id 生成规则（编译期唯一路径哈希），否则嵌套同名 Loop 下 B-10 的副本区分失效 | L146–L163, L293–L306 |
| PO-A9 | 6 个无实例组合的处置 | open | 对 read×{create,release,move}、write×move、occupy×use、occupy×move 给出语义定义或经 WF（PO-A1）显式禁止 | L84–L92, L605–L671 |

---

## E. 新发现缺口清单

1. **net 无 resource 分组**（PO-A2/B-2）：修订 C 收口了 scope 分组却遗漏 resource 分组；Deviation 已按资源对齐（L191）而 net 没有，属收口不对称。这是本轮最重要的可证缺陷——DO-9 泄漏检测存在构造性反例。
2. **区间算术未定义**（PO-A3/B-4）：四个派生度量的求和运算踩在未定义的 Interval 加法/序上；merge_I 不能替代加法。
3. **Unknown kind 类型逃逸**（PO-A6/C-1）：默认规则产出的对象在 §3 类型体系中无处安放，并使 §14 的 A5 判据内部不自洽。
4. **顺序组合零检查**（PO-A5/B-6）：Compatible 只护住 `||` 一条路；`;` 上的 double-free/double-create 完全静默。
5. **多重性丢失 vs 频率型诊断**（PO-A4/C-4）：集合代数与 AUDIT001/ED-006 的频率语义互相矛盾。
6. **良构性谓词缺失 + move 死代码**（PO-A1/A7/A9/C-3）：12 组合中 6 个无实例无约束；MoveChild 的 use 标注既削弱并行冲突检测又使 move 全家成为死代码。
7. **Loop(id) 唯一性未规约**（PO-A8/B-10）：循环副本机制的正确性隐含依赖 id 唯一，但生成规则缺失。
