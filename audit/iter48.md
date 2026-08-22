# Iter48 审计 — MA-001/MA-003/MA-008/MA-010 四条「收敛/asserted」声明真伪（独立审计 #48，hy3 单独进程）

- **审计视角**：§3.4 中 MA-001（L180）、MA-003（L182）、MA-008（L187）、MA-010（L189）四条状态标注（已解决/接受/已收敛）是否真·discharged，还是仅 asserted / 依赖 open 公设（独立 pass #48，全新上下文，未读任何其它 audit 文件）
- **范围**：§3.4 MA-001/003/008/010（L180/L182/L187/L189）、§3.1.1 Claim（L78-86，size∈Nat? 缺省 1）、§3.1.2 ResourceId（L88-99，含 `Tree(path: NodePath | Unknown)`）、§3.1.3 ScopeId（L103-113，7 构造子无 ⊆）、§3.2.3 Compatible（L131-138）、§3.2.5 循环组合与 Peak（L143-154）；邻接 Iter15（I15-01 ⊆ 未定义）、Iter34（O1-O3 ⊆* 草案）、Iter18（I18-01/02 ω 载体/Peak 发散）、Iter35（P1-P3 ω/S×ω 草案）、Iter25（C* Compatible 全函数）、Iter21（I21-01 Unknown∧Unknown 放行）、Iter32（PO-I32-a Claim= 未定义 / PO-I32-c size 缺省归一）、Iter26（I26-01 Instantiate size=∞）、Iter14（I14-02 peak 混加）、Iter36（Q1-Q4 DO-7 分桶）、Iter46（I46-03 ⊔ 区间载体冲突）
- **结论摘要**：逐条审计四条 MA 的状态标注真伪——(1) **MA-001（已解决）实为 asserted**：其收敛方案「新架构使用 Set<Claim>，无需 IndexExpr」并未真正消解 IndexExpr 的 Min/Scale 成本缩放问题，只是把问题转移到 Set<Claim> 上；而 Set<Claim> 的良定义依赖 Claim= 相等（Iter32 未定义）与 ScopeId⊆（Iter15/34 未定义）两个 open 公设，故 MA-001 的「已解决」建立在未证载体上，状态应降为 asserted/open（open，中）。(2) **MA-003（接受）为合法保守选择，但基底座开放**：「并行 read 累加、不尝试去重」是 conscious 过近似（read∧read 相容，L132），作为「接受」标注本身诚实；但 peak(L167) 跨 kind 直接 Σsize（Iter14 I14-02 / Iter36）、且 read 是否真「无成本缩放」依赖 §3.3.2 的 size 载体口径（Iter26/46/47 open），故「接受」的底层良定义仍依赖 open 项，状态=接受但受 open 牵连（open，弱）。(3) **MA-008（已收敛）窄命题真收敛、生态交互开放**：「size 可选、默认=1」与 §3.1.1 L82 字面一致（狭义 discharged）；但 size 缺省≡1 与显式 1 是否相等未定义（Iter32 PO-I32-c）、动态 size=∞（Iter26）、⊔ 区间 [min,max] 与单值 Nat? 载体冲突（Iter46 I46-03）、peak 跨单位 Σ（Iter47 weight 未定义）均 open ⇒ 该 MA 的「size 代数一致性」整体未收敛（open，中）。(4) **MA-010（已收敛）实为 asserted/矛盾**：「变量 path 保守为 Unknown，与任何资源冲突」依赖 (a) ResourceId 相等未形式化（Iter32 PO-I32-b），(b) Unknown 在 Compatible 层的处理——Iter21 已证 Unknown∧Unknown 在 Compatible 层放行、而 MA-010 在 resource 相等层把 Unknown 判为「与任何资源冲突」，两层语义直接矛盾，且 §3.2.2 约束 `c₁.resource=c₂.resource` 对 Unknown 取等/冲突未定义 ⇒ MA-010「已收敛」不实（open，高）。结构性结论：四条 MA 无一条是真正 rigorous discharged；MA-001、MA-010 为明文 asserted/open，MA-003 为诚实「接受」但受 open 牵连，MA-008 为狭义收敛但生态开放。诚实标注：未消解。

---

## W1. 命题：MA-001「已解决」实为 asserted，依赖 Set<Claim> 良定义（open 公设）

**命题**（§3.4 L180）：`MA-001 | IndexExpr 缺少 Min/Scale | 中 | 已解决 | 新架构使用 Set<Claim>，无需 IndexExpr`。

**论证**：原问题（MA-001 的发现）是「IndexExpr 缺少 Min/Scale」——即带索引表达式的成本缩放（如 `arr[i]` 的效应随 i 的边界缩放）在旧架构下无 Min/Scale 建模。收敛方案是「新架构用 Set<Claim>，无需 IndexExpr」——即把成本建模从「索引表达式」改为「Claims 集合」。

但此方案**并未证明索引缩放问题消失**，只是换了载体：
- Set<Claim> 的元素 Claim 的良定义依赖 **Claim= 相等律**（Iter32 PO-I32-a，全文未定义）⇒ HashSet 去重/∪ 幂等悬空；
- Set<Claim> 的聚合（net/peak，L163-167）依赖 **ScopeId⊆ 偏序**（Iter15 I15-01 / Iter34 O1，未定义）⇒ `c.scope⊆scope` 过滤不可判定；
- 循环组合的 Peak(L154) 依赖 **ω 载体**（Iter18 I18-01/02 / Iter35，未定义）⇒ 计数发散。

即：MA-001 用「新架构」规避了 IndexExpr，但新架构自身的代数地基（Claim=、⊆、ω）三项全 open ⇒ 「无需 IndexExpr」不等于「问题已解决」，只是把未证项从 IndexExpr 转移到 Set<Claim> 的 open 公设上。

**数学性质 / 证明状态**：
- **(PO-I48-a) MA-001「已解决」依赖 Set<Claim> 良定义（open，中）**：收敛方案成立 ⟺ Set<Claim> 良定义，而 Set<Claim> 依赖 Claim=(Iter32)/⊆(Iter15/34)/ω(Iter18/35) 三项 open ⇒ 实际未收敛。状态 = open（中，交叉 Iter32/Iter15/Iter18）。
- 文档行号：§3.4（L180）、§3.1.1（L78-86）、§3.1.3（L103-113）、§3.2.5（L154）、Iter32/Iter15/Iter18。

---

## W2. 命题：MA-003「接受」为合法保守选择，但基底座开放（open 牵连）

**命题**（§3.4 L182）：`MA-003 | 并行 read 去重 | 低 | 接受 | 保守估计：并行 read 累加，不尝试去重`。

**论证**：MA-003 的状态是「接受」而非「已解决」——这是**诚实的设计抉择**：并行 read 不尝试去重、直接累加（过近似），因为 read∧read 在 Compatible 层相容（§3.2.3 L132 `use∧use` 兼容），累加 read 不会漏报冲突（read 不互斥），属保守安全方向。

但此「接受」的底层良定义仍牵连 open 项：
- peak(L167) 的 `Σ c.size` **跨 kind 直接求和**（read/write/occupy 混加，Iter14 I14-02 / Iter36 Q1 未分桶）⇒ 「read 累加」的 size 单位与 write/occupy 混在一体，DO-7「不可混算」未落地（Iter36 草案未采纳）；
- read 的 size 载体口径依赖 §3.1.1 缺省=1（L82，但缺省≡显式 1 未定义，Iter32 PO-I32-c）、动态 size=∞（Iter26 I26-01）、以及是否参与 ⊔ 区间 [min,max]（Iter46 I46-03 载体冲突）⇒ read 累加的**数值本身**未完全定义。

⇒ MA-003 的「接受」标注对自身（过近似选择）诚实，**但过近似所累加对象的 size 代数未闭合** ⇒ 状态应记为「接受（合法），但受 open 牵连」。

**数学性质 / 证明状态**：
- **(PO-I48-b) MA-003「接受」受 size 代数 open 牵连（open，弱）**：过近似方向安全，但累加量的 size 载体（缺省/∞/区间/跨 kind）open ⇒ 数值未闭合。状态 = open（弱，交叉 Iter14/Iter36/Iter26/Iter32/Iter46）。
- 文档行号：§3.4（L182）、§3.2.3（L132）、§3.3.2（L167）、§3.1.1（L82）、Iter14/Iter36/Iter26/Iter32/Iter46。

---

## W3. 命题：MA-008「已收敛」窄命题真收敛、生态交互开放

**命题**（§3.4 L187）：`MA-008 | Claim 的 size 为可选 | 中 | 已收敛 | 默认 size = 1（单位资源），显存/内存等精确资源显式标注 size`。

**论证**：MA-008 的窄命题「size 可选、默认=1」与 §3.1.1 L82 字面一致（`size ∈ Nat? (可选，默认值为 1)`）——**此狭义谓词在文档内可核对、已定义** ⇒ 窄层 discharged。

但「size 代数一致性」整体开放：
- **缺省≡显式 1 未归一**：`occupy(tree,id,create)`（缺省 size）与 `occupy(tree,id,create,1)`（显式 1）是否相等未定义（Iter32 PO-I32-c）⇒ 聚合去重不确定；
- **动态 size=∞**：ED-004（L527）把动态 Instantiate 标 size=∞（Iter26 I26-01），而 §3.1.1 size∈Nat? 不含 ∞ ⇒ 单值载体与 ∞ 冲突，net(∞−∞)=NaN（Iter45 PO-I45-c）；
- **⊔ 区间载体冲突**：§3.2.4 ⊔ 输出 `[min,max]` 区间，但 §3.1.1 size 单值 ⇒ 合并结果无法落入 Signature（Iter46 I46-03）；
- **跨 kind Σsize 混算**：peak(L167) 不分离 kind/scope，与 DO-7 矛盾（Iter36/Iter14），且 weight 函数未定义（Iter47）⇒ 单位不可比。

⇒ MA-008 在「size 字段可选、默认 1」的**语法层**已收敛，但在 size 的**代数生态**（缺省归一/∞/区间/跨 kind）层全 open ⇒ 标注「已收敛」对窄命题成立、对生态不实。

**数学性质 / 证明状态**：
- **(PO-I48-c) MA-008 窄收敛但生态 open（open，中）**：语法层与 §3.1.1 一致（discharged），但 size 的缺省归一/∞/区间/跨 kind 四层交互全 open ⇒ 整体未收敛。状态 = open（中，交叉 Iter32 PO-I32-c/Iter26/Iter46/Iter14/Iter36/Iter47）。
- 文档行号：§3.4（L187）、§3.1.1（L82）、§3.2.4（L143-147）、§3.2.5（L154）、§3.3.2（L167）、Iter32/Iter26/Iter46/Iter14/Iter36/Iter47。

---

## W4. 命题：MA-010「已收敛」实为 asserted，且与 Iter21 的 Unknown 处理矛盾

**命题**（§3.4 L189）：`MA-010 | ResourceId 的相等性 | 中 | 已收敛 | 编译期常量 path 精确分析，变量 path 保守为 Unknown，与任何资源冲突`。

**论证**：MA-010 主张两件事：(a) 编译期常量 path 精确相等分析；(b) 变量 path 保守归 Unknown、**与任何资源冲突**。

(b) 依赖两个未定义/矛盾项：
- **ResourceId 相等未形式化**（Iter32 PO-I32-b）：§3.1.2（L88-99）给出 10 构造子但未写「两 ResourceId 何时相等」规则 ⇒ `Tree(p1)=Tree(p2) ⇔ p1=p2` 仅是直觉，文档未证；`Unknown` 是否为 ResourceId 构造子字段（L89 `Tree(path: NodePath | Unknown)`）也未明确其相等行为。
- **Unknown 双层语义矛盾**：MA-010 在 **resource 相等层**把 Unknown 判为「与任何资源冲突」（即任何含 Unknown 的两 Claim 资源相等 ⇒ 触发冲突/聚合）；但 **Iter21（I21-01）已证 Compatible 层 Unknown∧Unknown 直接放行**（保守相容）。即：MA-010 说 Unknown 资源「冲突」，Iter21 说 Unknown 模式「放行」——同一 Unknown 在两个并发判定层（resource 相等 vs mode Compatible）给出相反结论，文档未定义二者优先级/组合律 ⇒ 矛盾。
- **§3.2.2 约束悬空**：约束 `c₁.resource = c₂.resource`（L126-130）对 `Unknown = Unknown` 是取「相等（冲突）」还是「未知（跳过）」未定义 ⇒ MA-010 的「与任何资源冲突」缺乏执行规则。

⇒ MA-010 的「已收敛」实为 asserted：其收敛方案依赖未形式化的 ResourceId 相等 + 与 Iter21 矛盾的两层 Unknown 处理，状态应降为 open。

**数学性质 / 证明状态**：
- **(PO-I48-d) MA-010「已收敛」依赖未定义相等 + 与 Iter21 矛盾（open，高）**：resource 相等未定义（Iter32）+ Unknown 双层（MA-010 冲突 vs Iter21 放行）矛盾 ⇒ 收敛方案不成立。状态 = open（高，交叉 Iter32 PO-I32-b / Iter21 I21-01 / §3.2.2 L126-130）。
- 文档行号：§3.4（L189）、§3.1.2（L88-99）、§3.2.2（L126-130）、§3.2.3（L131-138）、Iter21（I21-01）、Iter32（PO-I32-b）。

---

## W5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (a) 定义 `Claim=` 相等律（Iter32 M4 草案）使 Set<Claim> 良定义（去重/∪ 幂等），且 (b) 定义 `ScopeId⊆*` 偏序（Iter34 O1），且 (c) 定义 `ω∈ℕ∪{⊤}` 载体（Iter35 P1），则 MA-001 所依赖的 Set<Claim> 真正闭合 ⇒ MA-001「已解决」可证。证明：载体齐 ⇒ 索引缩放问题在 Set<Claim> 上可建模。前提 PO-I48-a 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 (a) size 缺省≡显式 1 归一（Iter32 PO-I32-c），且 (b) size 提升为 extended 区间 + ⊔ 重写为 join-semilattice（Iter46），且 (c) DO-7 kind 分桶 + weight（Iter36/Iter47），则 MA-008 的 size 生态闭合 ⇒ MA-008 真收敛。证明：载体统一。前提 Iter32/Iter46/Iter36/Iter47 未立 ⇒ 条件。
- **P3（discharged，条件）**：若 (a) 形式化 ResourceId 相等（Iter32 PO-I32-b：同构造子+字段等，Unknown 处理明确），且 (b) 统一 Unknown 在 resource 层与 Compatible 层的处理（收回 Iter21 的 Unknown∧Unknown 放行短路、或显式定义两层优先级），则 MA-010「变量 path→Unknown 与任何资源冲突」可证且自洽。证明：相等律 + 双层一致性。前提 PO-I48-d 未立 ⇒ 条件。
- **P4（discharged）**：在「所有 path 为编译期常量、无变量 path、无循环、所有 size 显式」理想假设下，MA-001/003/008/010 全部 trivially 成立（IndexExpr 不存在、read 不缩放、size 无缺省/∞、path 无 Unknown）。证明：假设排除所有开放项。但 §7 满屏动态/变量 path（Iter26/Iter21）、§3.2.5 含 while（Iter18）⇒ 假设与文档冲突，不现实。

---

## Proof Obligation 账本（Iter48）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I48-a | MA-001「已解决」依赖 Set<Claim> 良定义(Claim=/⊆/ω 全 open) | open(中) | 采纳 Iter32/34/35 | L180, L78-86, L103-113, L154 |
| PO-I48-b | MA-003「接受」受 size 代数 open 牵连 | open(弱) | 闭合 size 生态(Iter26/32/46/36) | L182, L132, L167, L82 |
| PO-I48-c | MA-008 窄收敛但 size 生态 open(缺省/∞/区间/跨kind) | open(中) | 归一+区间+分桶(Iter32/26/46/36) | L187, L82, L143-147, L167 |
| PO-I48-d | MA-010「已收敛」依赖未定义相等+与 Iter21 Unknown 矛盾 | open(高) | 形式化相等+统一 Unknown 层 | L189, L88-99, L126-130, I21-01 |

## 本轮新发现未消解缺口（I48- 前缀，全局唯一）
- **I48-01（中）**：MA-001（L180）标「已解决」，但收敛方案「用 Set<Claim> 规避 IndexExpr」未证索引缩放消失，且 Set<Claim> 依赖 Claim=/⊆/ω 三项 open 公设 ⇒ 实为 asserted（交叉 Iter32/Iter15/Iter18）。
- **I48-02（弱）**：MA-003（L182）「接受」标注自身诚实，但 read 累加量的 size 载体（缺省/∞/区间/跨 kind）open ⇒ 过近似所累加数值未闭合（交叉 Iter14/Iter36/Iter26/Iter32/Iter46）。
- **I48-03（中）**：MA-008（L187）窄命题「size 可选默认 1」与 §3.1.1 L82 一致（狭义 discharged），但 size 的缺省归一/∞/区间/跨 kind 四层交互全 open ⇒ 整体未收敛（交叉 Iter32/Iter26/Iter46/Iter14/Iter36/Iter47）。
- **I48-04（高）**：MA-010（L189）标「已收敛」，但其「变量 path→Unknown 与任何资源冲突」依赖 ResourceId 相等未形式化（Iter32 PO-I32-b）且与 Iter21 已证的「Unknown∧Unknown 在 Compatible 层放行」矛盾 ⇒ 两层 Unknown 语义冲突、§3.2.2 约束对 Unknown 未定义 ⇒ 实为 asserted/open。
- **I48-05（弱）**：四条 MA 的状态标注（已解决/接受/已收敛）整体反映 §3.4「收敛表」的乐观主义——凡依赖 Set<Claim>/size/ResourceId 相等/Unknown 的条目，其「收敛」均建立在 Iter32（Claim=）、Iter15/34（⊆）、Iter18/35（ω）、Iter21（Unknown）等 open 公设上，需统一回写为「条件收敛/asserted」。

---

一句话摘要：四条 MA 无一条真正 rigorous discharged——MA-001（L180「已解决」）把 IndexExpr 问题转移到 Set<Claim>，却依赖 Claim=/⊆/ω 三项 open 公设（I48-01，中，交叉 Iter32/15/18）；MA-003（L182「接受」）自身诚实但 read 累加的 size 生态 open（I48-02，弱）；MA-008（L187「已收敛」）窄命题与 §3.1.1 L82 一致（狭义 discharged）但 size 的缺省归一/∞/区间/跨 kind 四层全 open（I48-03，中，交叉 Iter26/32/46/36/47）；MA-010（L189「已收敛」）依赖 ResourceId 相等未形式化且与 Iter21 的 Unknown∧Unknown 放行矛盾（I48-04，高）——结论：MA-001、MA-010 为明文 asserted/open，MA-003 为诚实「接受」但受 open 牵连，MA-008 为狭义收敛但生态开放，四条均需回写为「条件收敛/asserted」而非全量收敛。
