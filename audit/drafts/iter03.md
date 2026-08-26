# Iter03 审计 — §3.3 派生度量（net / peak / read / write）数学性质

- **审计视角**：派生度量代数（独立审计 pass #3/20，hy3，单独进程）
- **范围**：§3.3.1-3.3.3（net/peak/read/write）；邻接 §3.1.1（Claim 字段）、§3.2.1-3.2.5（∪/S×ω/Peak）、§3.4 MA-003/MA-004、§1 DO-3/DO-7/DO-9；交叉 Iter01（I1-02 Claim 去重）、Iter02（I2-05 S×ω、I2-07 双 Peak 矛盾）、Iter14（DO-7 量纲隔离）
- **结论摘要**：net/peak/read/write 的**语法定义**可形式化，但其数学性质（可加性、单调性、scope 单调性、量纲隔离）几乎全部挂在 Iter01/Iter02 的开放约束上（Claim 相等、ScopeId⊆、S×ω 语义）。关键发现：`net` 对 ∪ **非同态**——这与 §2.2「统一组合律」、DO-3 的「基于集合并 ∪ 的统一组合律」不能直接推广到派生度量 net（仅集合层成立）；net 守恒仅在「按资源配对 create/release 同 size」时成立，而文档未要求配对 ⇒ 泄漏（net≠0）是**允许状态**，与 DO-9 泄漏检测依赖的「净零」不变量未显式关联。§3.3.2 `peak` 依赖未定义的 ScopeId⊆（阻塞）且跨 kind 混合 size 求和（与 DO-7 冲突）；与 §3.2.5 的 `Peak` 是两函数同名（Iter02 I2-07）。read/write 仅计数、可加性依赖 Claim 去重、不按 size 加权（精度缺口）；MA-003「并行 read 累加」是精度策略非证明。MA-004「occupy 峰值与净变化混淆已解决」仅为**结构层收敛**，使用层混淆（调用方仍可用 peak 当 budget、用单点 net 断言无泄漏）未消。

---

## C1. 净变化 Net（§3.3.1）

**命题** `net(S) = Σ_{c∈S, c.kind=occupy, c.mode∈{create,move}} c.size − Σ_{c∈S, c.kind=occupy, c.mode=release} c.size`

**数学性质 / 证明状态**：
- **(PO-I3-a) net 对 ∪ 非线性（open，与 DO-3 冲突）**：`net(S₁∪S₂)` 一般 **≠** `net(S₁)+net(S₂)`。因 net 按 `mode∈{create,move}` 与 `mode=release` **分桶求和**，而非按资源配对：`S₁` 含 `occupy(res,r,create,size=a)`，`S₂` 含 `occupy(res,r,release,size=a)`，`S₁∪S₂` 后净量抵消，但 `net(S₁)=+a`、`net(S₂)=-a`。即 net 对 ∪ **无同态**。
  - 证据：net 是 Signature 上的**非线性**函数（分桶求和 ≠ 线性叠加）。§2.2 L60「统一组合律，无分配律问题」仅指 `∪` 这层集合运算；net 是 ∪ 的**派生量**，其非同态性说明 DO-3「统一组合律」不能推广到 net。状态 = **open（需与 DO-3 调和声明）**。
- **(PO-I3-b) net 守恒仅当按资源配对（open，关联 DO-9）**：令 per-resource 净 `net_r(S)=Σ_{同(res,scope)} create·size − Σ release·size`。仅当所有 occupy 按**资源+scope 配对**（create 后必 release 同 size）时 `Σ_r net_r(S)=0`。文档未要求配对 ⇒ 泄漏（net≠0）是允许状态。DO-9「Instantiate 无对应释放路径静态报警」依赖「净零」不变量，但该不变量**未被文档声明为强制**，故泄漏检测的数学前提缺失。状态 = **open（关联 DO-9）**。
- 形式证明（结构层）：**P1（discharged，条件）**：若 Claim 相等良定义（Iter01 PO-I1-a/b）且所有 occupy 按 (resource,scope) 配对 create/release 同 size，则 `Σ_r net_r(S∪S')=Σ_r net_r(S)+Σ_r net_r(S')`（在配对集一致时）。证明：分桶求和对配对集相加可分配。前提 PO-I3-b 未立 ⇒ 条件。

**文档行号**：§3.3.1（L159-163）、§2.2 L60-61、§1 DO-3（L15）、DO-9（L21）、Iter01 PO-I1-a/b、Iter02 I2-07。

---

## C2. 峰值 Peak（§3.3.2）与 §3.2.5 的 Peak 矛盾

**命题** `peak(S,scope) = max_{t∈scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size`

**数学性质 / 证明状态**：
- **(PO-I3-c) 依赖 ScopeId⊆（open，阻塞）**：`c.scope⊆t` 的 ⊆ 在 §3.1.3 仅枚举 7 种 ScopeId 构造子，**从未定义包含序**（Iter01 I1-04 / Iter15）。无 ⊆ 则 `peak` 谓词无确定真值 ⇒ peak 数学未良定义。状态 = **open（阻塞）**。
- **(PO-I3-d) 跨 kind 混合 size 求和（open，与 DO-7 冲突）**：求和 `Σ c.size` 对 `c.scope⊆t ∧ c.mode≠release` 的**全部 Claim** 累加，不按 kind 分离。即 read/write/occupy 三类 size 同数值相加（如 `GetNode` read size + `AddChild` occupy size 同加）。DO-7「read/write/occupy 不可混算，编译期报错」要求跨 kind 算术被禁止——peak 在此**混算**，与 DO-7 冲突（Iter14）。状态 = **open**。
- **(PO-I3-e) 与 §3.2.5 Peak 矛盾（open）**：Iter02 I2-07 已立。补充：§3.3.2 `peak` 对**单签名 S 静态**求 max over `t∈scope`，不含循环展开 `S×ω`；§3.2.5 `Peak` 量化 `i∈1..ω`（轮次）。对 `while` 体 S，`peak(S,scope)` 给常量上界，`Peak(S,scope)` 在 ω=∞ 下为 ∞（若多重集语义，Iter02 I2-05）。同一输入两函数结论相反 ⇒ 选错即错报警/漏报。状态 = **open**。
- 形式证明（结构层）：**P2（discharged，条件）**：在「Claim 相等良定义 + ScopeId⊆ 定义 + 单签名静态」前提下，`peak(S,scope)` 是 well-defined 函数（有限 scope 上有限和的最大值）。证明：前提成立即良定义；size≥0 ⇒ 求和单调。前提 PO-I3-c/d 未立 ⇒ 条件。

**文档行号**：§3.3.2（L165-168）、§3.2.5 L154、§3.1.3 L103-113、§1 DO-7（L19）、Iter01 I1-04、Iter02 I2-05/I2-07、Iter14。

---

## C3. read / write 计数（§3.3.3）

**命题** `read(S)=|{c∈S | c.kind=read}|`，`write(S)=|{c∈S | c.kind=write}|`

**数学性质 / 证明状态**：
- **(PO-I3-f) 可加性依赖 Claim 去重（open）**：`read(S₁∪S₂)` 若不去重（size 归一化未立，Iter01 I1-02/I1-b），重复 read 同一资源被计 2 次。`read` 对 ∪ 是**基数度量**，非 additivity-preserving 除非去重。状态 = **open（根因 Iter01）**。
- **(PO-I3-g) read 不按 size 加权（open，精度）**：`read(S)` 计数而非量化；但 §7 的 read 资源有 size 语义（如 `Load` 占 memory size，§7.4 L456）。read 计数不反映实际读量 ⇒ 「读多但计数少」漏评估。状态 = **open（精度）**。
- **(PO-I3-h) MA-003「并行 read 去重，接受保守累加」审计（asserted）**：§3.4 L182「保守估计：并行 read 累加，不尝试去重」——这是**精度策略声明，非证明结论**。状态 = **asserted（接受保守，未证上界紧度）**。
- 形式证明（结构层）：**P3（discharged，条件）**：在 Claim 去重（Iter01 PO-I1-b 立）下，`read(S₁∪S₂) ≤ read(S₁)+read(S₂)`（次可加，等号当无重复）。证明：集合基数次可加。前提未立 ⇒ 条件。

**文档行号**：§3.3.3（L170-174）、§3.4 MA-003（L182）、§7.4 L456-459、Iter01 I1-02/I1-b。

---

## C4. MA-004「occupy 峰值与净变化混淆已解决」真伪

**命题** §3.4 MA-004（L183）：`occupy 峰值与净变化混淆，高，已解决`，收敛「采用 Set<Claim>，net 和 peak 是派生度量，非原语」。

**数学性质 / 证明状态**：
- **(PO-I3-i) 结构层收敛 ≠ 使用层收敛（open，partial）**：Set<Claim> 使 net/peak 成为派生量，**消解了「把二元组(net,peak)当原语导致的不一致」这一结构问题**（原 v0.1 用区间 Grade 原语，v1→v3 改为派生量）。但文档**未提供任何机制**阻止调用方在**使用**时：① 用 `peak` 当 budget（峰值误当预算，高估然后误报或反之）；② 用单点 `net` 断言「无泄漏」（net≠0 但允许，PO-I3-b）。即混淆**根因（开发者误用派生量）未被消除**，仅载体重构。
- 状态判定：**MA-004 部分 discharged（结构层）+ 部分 open（使用层）**。文档以「已解决」单一结论 closure，过度声称。状态 = **open（使用层）**。

**文档行号**：§3.4 MA-004（L183）、§2.2 L61、§14 L771（v0.1 区间 Grade）。

---

## C5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：net 对配对集一致时满足 `Σ_r net_r(S∪S')=Σ_r net_r(S)+Σ_r net_r(S')`。前提 PO-I3-b（配对要求）未立。
- **P2（discharged，条件）**：peak 在 ⊆+去重+静态签名下 well-defined 且 size 单调。前提 PO-I3-c/d 未立。
- **P3（discharged，条件）**：read/write 次可加。前提 PO-I3-f（去重）未立。

---

## Proof Obligation 账本（Iter03）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I3-a | net 对 ∪ 非线性（无同态） | open | 与 DO-3 调和：组合律仅限集合层 | L159-163, L60 |
| PO-I3-b | net 守恒需按资源配对 | open | 显式关联 DO-9 净零不变量 | L159-163, DO-9 |
| PO-I3-c | peak 依赖 ScopeId⊆ | open（阻塞） | 见 Iter15 | L167, L103-113 |
| PO-I3-d | peak 混合 size 维度 | open | 见 Iter14 量纲隔离 | L167 |
| PO-I3-e | 两 Peak 定义矛盾 | open | 见 Iter02 I2-07 | L154, L167 |
| PO-I3-f | read 可加性依赖去重 | open | 见 Iter01 I1-02 | L170-174 |
| PO-I3-g | read 不按 size 加权 | open（精度） | 引入加权 read | L170-174 |
| PO-I3-h | MA-003 接受保守未证紧 | asserted | 给精度上界 | L182 |
| PO-I3-i | MA-004 使用层混淆未消 | open（partial） | 见 Iter17 | L183 |

## 本轮新发现未消解缺口（I3- 前缀，全局唯一）
- **I3-01**：net 对 ∪ 非同态 ⇒ DO-3「统一组合律」不能推广到派生度量 net（仅集合层成立，需限定声明）。
- **I3-02**：net 不变量（净零=无泄漏）与 DO-9 泄漏检测未显式关联（缺按 (resource,scope) 配对要求）。
- **I3-03**：peak 单签名定义不含循环展开，与安全峰值 Peak（§3.2.5）语义割裂（选错函数即错报警/漏报）。
- **I3-04**：read/write 仅计数不量化 size，读量评估漏算（MA-003 接受保守但未证紧度）。
- **I3-05**：MA-004「已解决」为过度声称，结构层收敛但使用层混淆未消（交叉 Iter17）。
- **I3-06**：§3.2.3 注释「use+write(同资源)不兼容」中「write」非 mode 枚举词（mode={use,create,release,move}）→ 注释与 §3.1.1 定义冲突（repeat of Iter02 I2-08）。
