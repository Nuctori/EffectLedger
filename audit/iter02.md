# Iter02 审计 — §3.2 组合律（顺序 / 并行 / 条件 / 循环 / Compatible / Peak）

- **审计视角**：代数语义 / 组合律（独立审计 pass #2/20，hy3，单独进程）
- **范围**：§3.2.1-3.2.5（顺序`;`/并行`||`/条件`⊔`/循环`S×ω`/Compatible/Peak）；邻接 §3.1.1（Claim 定义、size∈Nat?）、§3.3.2（peak 第二定义）、§3.4 MA-002/MA-006
- **结论摘要**：四种组合子全部归约为 `∪`（符合 DO-3「统一组合律」），但各自的安全语义都挂在**未被形式定义的约束**上：顺序组合 `;` 因定义为 ∪ 而意外可交换（丢失执行序）；并行组合在 Compatible 不满足时无错误语义；`Compatible` 本身非对称，破坏 `||` 交换性；条件合并 `⊔` 产出「区间 size」却与 §3.1.1「size∈Nat?」的 Claim 载体类型冲突；循环 `S×ω` 的语义（多重集/集合）未定义，直接决定 ω=∞ 时 Peak 有限还是无限；且 §3.2.5 与 §3.3.2 给出两个互相矛盾的 Peak 定义。MA-002「∞ 已解决」、MA-006「⊔ 已解决」均被证伪为 open。

---

## B1. 顺序组合 `;` 定义为 ∪ 丢失执行序

**命题** `(S₁;S₂) = S₁∪S₂`（§3.2.1，L122-125）。

**数学性质 / 证明状态**：
- 设 `;` 为真顺序组合，则应**非交换**（`S₁;S₂ ≠ S₂;S₁` 在运行时序意义上）。但 `;` 被定义为 ∪，而 ∪ 交换 ⇒ `S₁;S₂ = S₂;S₁` 恒成立。
- **(PO-I2-a) 序无关性未论证（open）**：文档用 scope（而非序）承载「释放前使用 vs 使用后释放」的安全区分（§3.3.2 的 `c.mode≠release`），但同一 scope 内 `create` 后 `release` vs `release` 后 `create` 的顺序信息被 ∪ 完全抹除。状态 = **open**。
- 关联/交换/中性元同 Iter01（在 Claim 相等良定义前提下 discharged）。

**文档行号**：§3.2.1（L122-125）、§3.3.2（L167）、§2.2 L60。

---

## B2. 并行组合 `||` 的前置条件无错误语义

**命题** `(S₁||S₂) = S₁∪S₂`，需 `∀c₁∈S₁,c₂∈S₂, c₁.resource=c₂.resource ⇒ Compatible(c₁.mode,c₂.mode)`（§3.2.2，L127-131）。

**数学性质 / 证明状态**：
- **(PO-I2-b) 约束违反时无定义/降级语义缺失（open）**：文档说「需满足」但未规定违反时结果（编译错误？取保守并集？置 ∞？）。使 `||` 在 Compatible 失败时是**部分函数**——这一 partiality 未形式化。状态 = **open**。
- 在「全部 pairwise Compatible」前提下 `(S₁||S₂)||S₃ = S₁||(S₂||S₃)`：discharged（此时 ∪ 结合 + 约束域闭包）。

**文档行号**：§3.2.2（L127-131）。

---

## B3. `Compatible` 非对称破坏 `||` 交换性

**命题** `Compatible(m₁,m₂)`（§3.2.3，L133-141）列出 4 个析取，其中 3 个为 `_ ∧ use`：
`use∧use | create∧use | release∧use | move∧use`。

**数学性质 / 证明状态**：
- `Compatible` 对有序参数**非对称**：`Compatible(create,use)=true`（m₁=create,m₂=use 命中第2析取），但 `Compatible(use,create)=false`（m₁=use,m₂=create：4 析取无一命中）。
- **(PO-I2-c) `||` 交换性被破坏（open）**：并行组合 `(S₁||S₂)` 的约束是固定的 `Compatible(c₁∈S₁, c₂∈S₂)`，顺序由语法固定。若 `Compatible(c₁,c₂)=true` 但 `Compatible(c₂,c₁)=false`，则 `S₁||S₂` 良定义而 `S₂||S₁` 不定义，与并行语义的对称性预期矛盾。状态 = **open**（亦呼应 Iter01 I1-05，MA-009 完备性无形式证明）。
- 注：若希望并行对称，`Compatible` 必须改为对称闭包（即 `Compatible(create,use) ⇔ Compatible(use,create)`），但这意味着 `use+create` 组合被允许——与 §3.2.3 注释「不兼容：create+create, move+move, use+write(同资源)」中的 `use+write` 描述又冲突（§3.2.3 注释用「write」而非 mode 枚举中的词，见 PO-I2-i）。

**文档行号**：§3.2.3（L133-141）、§3.4 MA-009（L188）。

---

## B4. 条件合并 `⊔` 与 Claim 载体类型冲突

**命题** `(if b then S₁ else S₂) = Signature(b) ∪ (S₁ ⊔ S₂)`，其中 `⊔`：`∀c∈S₁∪S₂, [min(c₁.size,c₂.size), max(c₁.size,c₂.size)]`（§3.2.4，L143-147）。

**数学性质 / 证明状态**：
- **(PO-I2-d) 类型不一致（open）**：`⊔` 产出**区间** `[lo,hi]` 作为 size，但 §3.1.1 定义 `size ∈ Nat?`（单值/缺省1），**不是区间**。故 `S₁⊔S₂` 的结果不是 `Set<Claim>` 的合法元素 ⇒ 组合子输出脱离代数载体。与 MA-006「放弃半环，采用集合代数」声明矛盾（实际产出了区间代数对象）。
- **(PO-I2-e) 单侧出现的 claim 区间未定义（open）**：`∀c∈S₁∪S₂` 若 c 仅属 S₁（不在 S₂），其 size 区间应为 `[c.size,c.size]`，但文档未陈述，造成 `⊔` 对差集元素未定义。
- 在「将 size 区间化」假设下，`⊔` 是区间格的 join（凸包）：关联/交换/幂等。discharged（给定载体修正）。

**文档行号**：§3.2.4（L143-147）、§3.1.1 L85。

---

## B5. 循环 `S×ω` 语义未定义 ⇒ ω=∞ 时 Peak 有限/无限不定

**命题** `(while b do S) = Signature(b) ∪ (S×ω)`，`ω`=循环次数，静态未知为 ∞；`Peak(S,scope)=max_{i∈1..ω}|{c∈S×i | c.scope⊆scope ∧ c.mode≠release}|`（§3.2.5，L149-155）。

**数学性质 / 证明状态**：
- **(PO-I2-f) `S×ω` 是多重集还是集合未定义（open，阻塞级）**：
  - 若 `S×ω` 为**多重集**（重复累加）：每轮 `create`（非 release）claim 计数增长 ⇒ `Peak = ω·|S| = ∞`（ω=∞）。此时 Peak 无限 ⇒ 任何带 occupy-create 的循环恒报警（sound 但极度不精确）。
  - 若 `S×ω` 为**集合**（同 Claim 合并）：`|{c∈S×i}| = |S|` 常量 ⇒ `Peak=|S|` 有限，即便 ω=∞。
  - 两者结论相反，文档未指定 ⇒ Peak 在 ω=∞ 时**数学未良定义**。
- **(PO-I2-g) `Signature(b)` 未定义（open）**：条件/循环中的 `b`（循环守卫）效应 `Signature(b)` 从未定义（是否读变量？读哪些资源？）。
- **MA-002「∞ 的代数性质已解决」被证伪**：收敛依据「∞ 作为 ScopeId 的循环标记，Peak 计算时处理」——但 PO-I2-f 显示 Peak 在 ω=∞ 时因 S×ω 语义未定义而不确定，故 MA-002 = **open**（非已解决）。

**文档行号**：§3.2.5（L149-155）、§3.4 MA-002（L181）。

---

## B6. 两个 Peak 定义互相矛盾

**命题**：
- §3.2.5：`Peak(S,scope)=max_{i∈1..ω}|{c∈S×i | c.scope⊆scope ∧ c.mode≠release}|`（计数，跨循环轮次）
- §3.3.2：`peak(S,scope)=max_{t∈scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size`（求和 size，跨 t∈scope）

**数学性质 / 证明状态**：
- **(PO-I2-h) 两定义不一致（open）**：① 一者取**基数**`|·|`、一者取**size 求和**`Σc.size`；② 一者量化 `i∈1..ω`（循环轮次）、一者量化 `t∈scope`（scope 层次）；③ §3.3.2 无 `S×ω`/ω，完全未提循环。两者不是同一函数的两种写法，而是两个不同函数被同名 `Peak/peak` 称呼。状态 = **open**（与 Iter03/Iter15/Iter18 交叉）。

**文档行号**：§3.2.5 L154、§3.3.2 L167。

---

## B7. 可消解的 proof obligation（履行尝试）

- **P1（discharged）**：在「Size 区间化 + ⊔=区间 join」假设下，`⊔` 是交换幂等结合算子（join-semilattice）。证明：区间集合上的 convex-hull join 满足半格公理（标准格论）。前提：需先消解 PO-I2-d 的类型不一致。
- **P2（discharged）**：在「全部 pairwise Compatible」子域上，`||` 满足结合律且结果与分组无关。证明：∪ 结合 + 约束为 pairwise 全局成立。

---

## Proof Obligation 账本（Iter02）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I2-a | 顺序组合序无关性论证 | open | 证明执行序可被 scope 完全编码，或显式承认序丢失 | L122-125, L167 |
| PO-I2-b | 并行组合约束违反的语义 | open | 定义 Compatible 失败时的错误/降级语义 | L127-131 |
| PO-I2-c | Compatible 非对称破坏 ∥ 交换 | open | 将 Compatible 改为对称，或否定 ∥ 交换性 | L133-141 |
| PO-I2-d | ⊔ 区间 size 与 Claim 载体冲突 | open | 将 Claim.size 扩展为区间 / 重写 ⊔ 载体 | L143-147, L85 |
| PO-I2-e | ⊔ 单侧 claim 区间未定义 | open | 规定单侧元素区间=[size,size] | L143-147 |
| PO-I2-f | S×ω 多重集/集合语义 | open（阻塞） | 显式定义 S×ω 为多重集或集合 | L149-155 |
| PO-I2-g | Signature(b) 守卫效应 | open | 定义条件/循环守卫的效应签名 | L143-147, L149-155 |
| PO-I2-h | 两个 Peak 定义矛盾 | open | 统一为单一 Peak 定义 | L154, L167 |

## 本轮新发现未消解缺口（I2- 前缀）
- **I2-01**：`;` 定义为 ∪ 抹除执行序，与程序顺序语义不符，序无关性未论证。
- **I2-02**：并行组合 `||` 在 Compatible 失败时无错误语义（部分函数未形式化）。
- **I2-03**：`Compatible` 非对称 ⇔ `||` 非交换，与并行语义预期矛盾。
- **I2-04**：`⊔` 输出区间 size，与 §3.1.1 `size∈Nat?` 载体冲突（MA-006「已解决」不实）。
- **I2-05**：`S×ω` 多重集/集合语义未定 ⇒ ω=∞ 时 Peak 有限/无限不定（MA-002「已解决」不实，阻塞）。
- **I2-06**：`Signature(b)`（守卫效应）从未定义。
- **I2-07**：§3.2.5 与 §3.3.2 的两个 `Peak/peak` 定义互不一致。
- **I2-08**：§3.2.3 注释用「write」一词，但 mode 枚举为 {use,create,release,move}，无 write 模式 ⇒ 注释与定义自相矛盾（同 Iter03/Iter16）。
