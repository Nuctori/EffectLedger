# Iter01 审计 — §3.1 基本对象的代数结构（Claim / ResourceId / ScopeId / Signature / Set<Claim>）

- **审计视角**：正式逻辑 / 代数结构（独立审计 pass 1/20）
- **范围**：§3.1 基本对象；邻接 §1（DO-3 组合律）、§3.2-3.3（Peak/net 引用）、§3.4（MA-009/MA-010）
- **结论摘要**：在「Claim 相等性良定义」前提下，Set<Claim> 上的 ∪ 是一个交换幂等幺半群（即 join-半格），∅ 为中性元/bottom。但文档**从未定义 Claim 相等规则**（特别是可选 size 的归一化与跨 ResourceId 构造子等价），导致 ∪ 的幂等性、集合去重、Signature 值相等与 Peak 依赖的 ScopeId⊆ 全部悬空。MA-009 的 Compatible 完备性仅有测试无形式证明（open）；MA-010 的 Unknown 策略是合理保守上近似，但精度损失未消解。

---

## A1. Set<Claim> 上 ∪ 的代数性质

**命题** `(Set<Claim>, ∪, ∅)` 构成交换幂等幺半群（commutative idempotent monoid），等价地给出 join-半格，⊥=∅。

**数学性质 / 证明状态**：

- 结合律 `S₁∪(S₂∪S₃) = (S₁∪S₂)∪S₃`：discharged（标准集合论，依赖元素相等良定义）。
- 交换律 `S₁∪S₂ = S₂∪S₁`：discharged（同上）。
- 幂等 `S∪S = S`：discharged **仅当** Claim 相等满足，否则两语义相同但字段不全同的 Claim 被视为不同元素 ⇒ 幂等失败。
- 中性元 `S∪∅ = S`，∅ 为 bottom/⊥：discharged。
- 与 §3.4 MA-006「放弃半环，采用集合代数（∪）+ 偏序集（⊆）」一致：⊆ 指 Signature 集合包含，∪ 即 join。discharged-consistent。

**关键缺口（前置 proof obligation）**：

- **(PO-I1-a) Claim 相等规则未定义**。§3.1.1 定义 Claim 为 5 元组但从不说明何时两个 Claim 相等。集合并的「去重」与 Signature 的 `ImmutableHashSet<Claim>` 相等都依赖它。
- **(PO-I1-b) 可选 size 的归一化未定义**。§3.1.1：`size ∈ Nat?，默认值为 1`。则 `Claim(kind,res,mode,scope)`（size 缺省）与 `Claim(kind,res,mode,scope,1)`（size 显式=1）在「字段全等」语义下是**不同元素**，导致同一资源声明在 ∪ 下被复制、`S∪S≠S` 在语义层被破坏。需规定「size 缺省 ≡ size=1」的归一化。

**文档行号依据**：§3.1.1（L78-86）、§3.1.4 Signature := ImmutableHashSet<Claim>（L115-118）、§2.2（L60「Set<Claim>+集合并∪」）、§3.4 MA-006（L185）。

---

## A2. ResourceId 相等性与 MA-010 的 Unknown 策略

**命题** ResourceId 是 10 构造子的 tagged union；其相等性 = 同构造子且字段相等。

**数学性质 / 证明状态**：

- 构造子内相等（同构造子、字段相等 ⇒ 相等）：discharged（按定义）。
- **(PO-I1-c) 跨构造子等价未定义**：不同构造子是否可能语义指向同一资源（如 `Tree(path)` 与 `Self(component)` 指向同一节点）？文档未定义跨构造子等价关系 ⇒ 同一资源可能以两个 ResourceId 出现，使 ∪ 无法去重、冲突检测漏判。open。
- **MA-010 收敛真伪**：「编译期常量 path 精确分析，变量 path 保守为 Unknown，与任何资源冲突」（L189）。
  - 数学性质：令冲突关系 `confl(r₁,r₂)`，MA-010 给出 `confl(Unknown, r)` 对**任意** r 成立 ⇒ Unknown 是 resource 冲突格上的 **top（⊤）**，构成 sound over-approximation。
  - **修正 A1 中的「dominate」误述**：∪ 不吞并元素（并集保留 Unknown Claim 为独立元素）。Unknown 的影响是——在 §3.2.2 并行组合 `c₁.resource=c₂.resource ⇒ Compatible` 检测中，任何与 Unknown 资源的并行组合都触发保守冲突（不兼容），从而阻止合并或报警。这是**精度**问题而非 ∪ 的语义问题。
  - **(PO-I1-d) 精度损失未消解**：Unknown⊤ 导致大量保守冲突（误报根源，呼应 §10 R-3 高误报率），文档以「保守估计」声明 closure，但无精度界证明。status = **asserted（仅声明，未证精度界）**。

**文档行号依据**：§3.1.2（L88-101）、§3.4 MA-010（L189）。

---

## A3. ScopeId 上的 ⊆ 关系未定义（阻塞级 proof obligation）

**命题** §3.2.5 与 §3.3.2 的 Peak 均依赖 `c.scope ⊆ scope`，但 §3.1.3 仅枚举 7 种 ScopeId 构造子，**从未定义 ScopeId 上的包含序 ⊆**。

**数学性质 / 证明状态**：

- **(PO-I1-e) ScopeId ⊆ 未定义 ⇒ Peak 未良定义**。具体悬空问题：
  - `Method(m) ⊆ Type(T)?` `Method ⊆ Global?` `Loop ⊆ Conditional?` `Async ⊆ Global?` —— 全部未定义。
  - 若 Method/Type 间的 ⊆ 不传递或不定义，则 `peak(S, scope)` 的 `c.scope ⊆ t` 谓词无确定真值，Peak 退化为未定义函数。
- 该 ⊆ 必须是**预序（自反+传递）**且理想情况下为偏序，Peak 的「scope 单调性」才成立（见 Iter03）。
- status = **open（阻塞级）**：Peak/net 是整套派生度量的地基（DO-8 峰值检测），未定义 ⊆ 使 §3.2.5、§3.3.2 数学上悬空。

**文档行号依据**：§3.1.3（L103-113）、§3.2.5 Peak（L154）、§3.3.2 peak（L167）。

---

## A4. Signature 不可变性 vs Claim 值相等

**命题** Signature 运行时不可变（ImmutableHashSet）；但两个 Signature 相等/相同以 Claim 值相等为前提。

**数学性质 / 证明状态**：

- 运行时不可变：discharged（C# ImmutableHashSet 保证）。
- **(PO-I1-f) Claim 值相等缺失导致幂等/去重失败**：若 Claim 仅实现引用相等（未实现值相等），`S∪S` 可能产生重复元素、Signature 相等比较失真。与 PO-I1-a/b 同根。open。
- 注：文档 §4.1.2、§6.3 用 `readonly record struct` 提供「自动相等性」——但那是 **Component** 的值相等，Claim 在 §3.1.1 是裸元组定义，**未声明为 record struct**，故其相等性无任何机制保证。这是文档内部事实缺口（Component 有、Claim 无）。

**文档行号依据**：§3.1.4（L115-118）、§4.1.2 Component 自动相等性（L204-216）、§6.3（L364-373）。

---

## A5. MA-009 / MA-010 收敛真伪

| 发现 | 文档状态 | 实际审计状态 | 说明 |
| ------ | --------- | ------------- | ------ |
| MA-009 Compatible 完备性 | 已收敛 | **open**（I1-05） | 「枚举 16 种组合，单元测试覆盖，**不追求形式化证明**」。mode 4 个 ⇒ 有序对 4×4=16；§3.2.3 仅列 4 个析取分支（均含 `use`），其余 12 种隐含不兼容。完备性 = 列出者恰为全部安全组合，仅测试覆盖、无形式证明。 |
| MA-010 Unknown 策略 | 已收敛 | **asserted**（PO-I1-d） | 收敛的是「采用保守 Unknown」，但精度损失（误报）未消；跨构造子等价（PO-I1-c）未消。 |

**文档行号依据**：§3.4 MA-009（L188）、MA-010（L189）、§3.2.3 Compatible（L133-141）。

---

## A6. 可消解的 proof obligation（履行尝试）

- **P1（discharged）**：在假设「Claim 相等 = 全字段（含归一化 size 缺省≡1）相等」下，证明 `(Set<Claim>, ∪, ∅)` 为交换幂等幺半群，且 ∪ 是上确界 ⇒ join-半格（⊆ 为集合包含，⊥=∅）。证明：标准集合论结果，前提成立即成立。与 MA-006 一致。
- **P2（discharged）**：MA-010 下 Unknown 是 resource 冲突格的 ⊤：`∀r. confl(Unknown, r)`，给出保守上近似的正确性（soundness）说明——任何漏报风险被排除（sound over-approximation），但 precision 不保证。

---

## Proof Obligation 账本（Iter01）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ---------------- | ------ |
| PO-I1-a | Claim 相等规则 | open | 显式定义 Claim 相等的字段规则 | L78-86, L115-118 |
| PO-I1-b | size 缺省≡1 归一化 | open | 规定 size 缺失与 size=1 同一元素 | L85 |
| PO-I1-c | 跨 ResourceId 构造子等价 | open | 定义跨构造子资源等价关系（或声明不等价） | L88-101 |
| PO-I1-d | Unknown⊤ 精度界 | asserted | 给出保守冲突导致的误报上界或接受声明 | L189, §10 R-3 |
| PO-I1-e | ScopeId ⊆ 定义 | open（阻塞） | 定义 7 构造子上的包含预序/偏序 | L103-113, L154, L167 |
| PO-I1-f | Claim 值相等机制 | open | 将 Claim 声明为 record struct 或显式相等 | L78-86 vs L204-216 |

## 本轮新发现未消解缺口（I1- 前缀，全局唯一）

- **I1-01**：Claim 相等规则完全缺失，是 ∪ 幂等/Sig 去重/Peak 的共性根因。
- **I1-02**：跨 ResourceId 构造子的语义等价未定义（Tree vs Self 可能同节点）。
- **I1-03**：MA-010 的 Unknown⊤ 必然引发保守冲突 => §10 R-3 高误报率的结构性来源，无精度界。
- **I1-04**：ScopeId⊆ 未定义 => Peak（DO-8 峰值检测）数学上未良定义（阻塞）。
- **I1-05**：MA-009 Compatible 完备性无形式证明，仅单元测试覆盖。
- **I1-06**：Claim 未声明 record struct，值相等无机制保障（与 Component 形成内部不一致）。
