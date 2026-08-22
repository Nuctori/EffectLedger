# Iter17 审计 — §3.4 MA-004「net 与 peak 概念混淆」收敛真伪：net/peak 是否在数学层清晰分离且各自良定义（独立审计 #17，hy3 单独进程，本轮重跑）

- **审计视角**：派生度量语义独立性 / 各自良定义性 / 「分清」是命名层还是数学层（独立 pass #17，全新上下文）
- **范围**：§3.4 MA-004（L182-183）、§3.3.1 net（L163-165）、§3.3.2 peak（L167）、§3.2.5 Peak（L154）、§1 DO-8/DO-9（L20-21）、§12.2 报告（L656-672）；邻接 Iter15（I15-01 ⊆ 悬空 / I15-04 两 Peak 矛盾 / I15-06 net 无 scope）、Iter16（Compatible 偏定义）
- **结论摘要**：MA-004 声称「net 占用净值与 peak 瞬时峰值已分清（已解决）」，收敛方案为「采用 Set<Claim>，net 和 peak 是派生度量，非原语」。但审计表明：(1) **net 与 peak 都未各自良定义**——net 无 scope 参数（Iter15 I15-06）、peak 依赖未定义的 ⊆ 偏序（Iter15 I15-01），二者数学层均悬空，所谓「分清」只是**命名层**的标识符不同，非语义分离（open，高）；(2) **文中存在三个被混用的「峰值/净值」对象**——§3.2.5 的 `Peak`（count `|·|` 遍历循环索引 i∈1..ω）、§3.3.2 的 `peak`（Σsize 遍历 scope 窗口 t∈scope）、§3.3.1 的 `net`（Σcreate−Σrelease 净值），三者量纲/遍历域/依赖均不同，却跨节跳跃使用，无统一度量对象（open）；(3) **MA-004「已收敛」未证明 net 与 peak 语义域不相交**（peak 可 > net？net<0 含义？），且 DO-8/DO-9 分别依赖 peak/net，二者定义都未立 ⇒ 收敛不实（open）；(4) **net<0（释放多于创建）语义未定义**——是泄漏正向报警？还是允许的正常回收？与 DO-9「Instantiate 无对应释放路径才报警」的报警方向冲突（open）。结构性成立（在各自良定义且语义域分离后「分清」成立）给条件证明。

---

## Q1. MA-004「已解决」仅停留在命名层，net/peak 各自都未良定义（核心，高）

**命题** §3.4 MA-004（L182-183）：「occupy 峰值与净变化混淆 — 已解决 — 采用 Set<Claim>，net 和 peak 是派生度量，非原语」。§3.3.1（L163-165）`net` 与 §3.3.2（L167）`peak` 分列两定义。

**数学性质 / 证明状态**：
- **(PO-I17-a) 「分清」是标识符区分，非数学语义分离（open，高）**：MA-004 的收敛论据是「net 和 peak 是派生度量，非原语」——这仅说明二者**不是基本类型**，并未说明二者**数学语义已厘清**。要证明「混淆已解决」，需证明 `net` 与 `peak` 各自作为函数良定义且语义域不相交。但：
  - `net(S)`（L163-165）不含 scope 参数（Iter15 I15-06），是**全局标量**；
  - `peak(S, scope)`（L167）依赖 `c.scope ⊆ t`，而 ⊆ 偏序在 §3.1.3 从未定义（Iter15 I15-01）。
  - 二者都未良定义 ⇒ 谈不上「分清」或「混淆已解决」。文档把「重命名为两个派生函数」等同于「已解决概念混淆」，是**命名层混淆解决**冒充**数学层混淆解决**。状态 = open（高，MA-004 收敛断言不实）。
- 交叉：Iter15 I15-01 / I15-06 已分别证 peak/net 悬空；本项将二者合并为「MA-004 是否真正收敛」的判定——结论：未收敛。

**文档行号**：§3.4 MA-004（L182-183）、§3.3.1（L163-165）、§3.3.2（L167）、§3.1.3（L103-113，Iter15 I15-01）。

---

## Q2. 三个混用对象：Peak(count,i) / peak(Σsize,t) / net(净值) 无统一度量（open）

**命题** 文档给出三个相关但不同的度量：
- `Peak(S, scope) = max_{i∈1..ω} |{ c ∈ S×i | c.scope ⊆ scope ∧ c.mode ≠ release }|`（§3.2.5，L154）——遍历**循环索引 i**，取**集合计数** `|·|`；
- `peak(S, scope) = max_{t∈scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size`（§3.3.2，L167）——遍历**scope 窗口 t**，取 **Σsize**；
- `net(S) = Σ create/move − Σ release`（§3.3.1，L163-165）——全局**净值**。

**数学性质 / 证明状态**：
- **(PO-I17-b) 三对象量纲/遍历域均不同，跨节跳跃无基准（open）**：
  - `Peak`（L154）量纲 = count（Claim 个数），遍历 = 循环展开 `S×ω` 的迭代 i；
  - `peak`（L167）量纲 = Σsize（字节/计数混加，交叉 Iter14 DO-7），遍历 = scope 窗口 t；
  - `net`（L163-165）量纲 = Σsize 净值，遍历 = 全集，无 scope。
  - 三者既不等价也未被声明为「不同抽象层」，`peak`(小写) 与 `Peak`(大写) 在文中似同一概念的两种表述（交叉 Iter15 I15-04），但数学上 `Peak` 不计 size、`peak` 不展开循环 ⇒ 读者/工具无法判断「峰值」到底指哪个。§12.2 报告（L656-672）用「VramMB: 1280MB」是 Σsize 语义（peak 方向），而 §3.2.5 的 Peak 永不产生「MB」⇒ 报告与 §3.2.5 不一致。状态 = open（文档内部度量对象不统一）。
- 附加：若 `peak` 应覆盖循环展开，则 `peak(S, scope)` 需先对 `S` 做 `S×ω` 展开（§3.2.5），但 §3.3.2 公式直接写 `Σ_{c∈S}`，未含 `×ω` ⇒ 循环峰值与 peak 公式脱节（交叉 Iter04 PO-I4-a：ω=∞ 时 Peak 未定义）。

**文档行号**：§3.2.5（L154）、§3.3.1（L163-165）、§3.3.2（L167）、§12.2（L656-672）、Iter15 I15-04。

---

## Q3. MA-004 未证明 net 与 peak 语义域不相交（open，收敛不实）

**命题** MA-004 收敛方案称「net 和 peak 是派生度量」，隐含二者已厘清。但全文**无任何一处**论证 net 与 peak 的语义关系：peak 与 net 可否同时超限？net 为负时 peak 为何？二者报警阈值是否独立？

**数学性质 / 证明状态**：
- **(PO-I17-c) 语义域不相交性未证（open）**：「分清 net 与 peak」的最低充分条件是证二者度量**不同物理对象**（net=累积占用净值，peak=瞬时并发上限），且 DO-8 依赖 peak、DO-9 依赖 net（L20-21）。但：
  - peak 的「瞬时」定义依赖时间窗口 t，而 net 无时间窗口（全局求和）→ 二者的「时间维度」未对齐：peak 是某窗口最大值，net 是全程净值，二者数学域不同（前者是 `Max over windows`，后者是 `Σ over all`）。
  - 文档未定义「net 与 peak 何者为预算比较基准」——§12.2 用 512MB 预算比较 `accumulated VramMB: 1280MB`（L667），这是峰值（peak）还是累计（net）？措辞「accumulated」偏 net 但数值 1280MB 偏 peak（Σsize 窗口）→ 概念漂移。状态 = open（MA-004「已解决」无证据支撑语义分离）。
- 交叉：Iter15 I15-06（net 无 scope）使 net 的「窗口」维度彻底缺失，更无从与 peak 对齐。

**文档行号**：§3.4 MA-004（L182-183）、§1 DO-8/DO-9（L20-21）、§12.2（L667）、Iter15 I15-06。

---

## Q4. net<0 语义未定义，与 DO-9 报警方向冲突（open）

**命题** §3.3.1（L163-165）：`net(S) = Σ_{create/move} − Σ_{release}`。当某作用域内 release 项 size 之和大于 create/move 项之和时，`net < 0`。

**数学性质 / 证明状态**：
- **(PO-I17-d) net<0 未定义语义（open）**：
  - `net<0` 物理含义是「释放多于占用」——本应**不是泄漏**（泄漏是占用未释放，即 net>0 且无对应 release）。但 DO-9（L21）定义「Instantiate 无对应释放路径静态报警」，其数学判据通常是「存在 occupy(create) 未配 release」⇒ 这对应 `net` 的 create/release **配对缺失**，而非简单的 net 符号。
  - 文档未规定：`net<0` 是「正常回收过量」（允许）、还是「重复释放/误报」（应警告）、还是「绝对值即潜在 double-free 风险」？三种解读对 DO-9 报警方向截然相反。状态 = open。
  - 交叉：Iter15 I15-06（net 无 scope）使「某作用域内 net<0」不可计算 ⇒ DO-9 即使想用 net<0 报警也无数学对象。
- 交叉：Iter08 I8-01（QueueFree mode=move 致 release 项缺失）使 net 的 release 侧系统性漏算 ⇒ net 偏正，net<0 几乎不出现，但这是**计量错误**而非「无泄漏」，进一步说明 net 符号与泄漏语义脱钩。

**文档行号**：§3.3.1（L163-165）、§1 DO-9（L21）、Iter08 I8-01、Iter15 I15-06。

---

## Q5. MA-004 收敛依赖的「Set<Claim 化」未消除 peak 的内部矛盾（open）

**命题** MA-004 收敛方案「采用 Set<Claim>」——即把 Claims 放入集合、net/peak 作为集合上的派生函数。但 Set<Claim> 化只解决「原语 vs 派生」的层次问题，未解决：
- peak 依赖的 ⊆（Iter15 I15-01）在 Set<Claim> 层仍缺失；
- 两个 Peak/peak 定义（Q2）在 Set<Claim> 层仍冲突；
- DO-7 量纲（Iter14）使 Σsize 的 peak 含混加，Set<Claim> 不自动隔离 kind。

**数学性质 / 证明状态**：
- **(PO-I17-e) Set<Claim> 化 ≠ 混淆已解决（open）**：将 net/peak 表达为 `f: Set<Claim> → Number` 仅是函数签名，不保证 f 良定义（Q1）或 f 之间语义分离（Q3）。MA-004 把「表示层重构」当作「语义层收敛」，与 MA-006/MA-009 同属「已收敛」过度声称母题（交叉 Iter04 I4-03 / Iter07 I7-01 / Iter16 I16-07）。状态 = open。

**文档行号**：§3.4 MA-004（L182-183）、§3.1.4（L94，Set<Claim>）、Iter15 I15-01、Iter14 DO-7。

---

## Q6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (a) 显式定义 ScopeId 上的 ⊆ 偏序（Iter15 PO-I15-a），(b) 统一两峰值定义为单一 `peak(S, scope)=max_{t⊆scope} Σ_{c∈S,c.scope⊆t,..} c.size` 并令 S 已含 `S×ω` 展开（Iter15 PO-I15-d/e），(c) 补 net 的 scope 分组 `net(S, scope)`（Iter15 PO-I15-f），则 `peak` 与 `net` 各自良定义且度量不同物理对象（peak=窗口最大并发 size，net=作用域累积占用净值）⇒ MA-004「分清」在**数学层**成立。证明：各自定义域明确 ⇒ 混淆消除。前提 PO-I17-a/b/c 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若补充「net<0 ⇔ 释放多于创建，标记为潜在 double-free 警告（非泄漏）；net>0 且作用域内无 release 配对 ⇔ 泄漏（DO-9）」的语义规则，则 net 符号语义闭合、与 DO-9 报警方向对齐。证明：符号规则消除歧义。前提 PO-I17-d 未立 ⇒ 条件。
- **P3（discharged）**：在「仅命名层」弱解释下，§3.3.1/§3.3.2 作为两个不同标识符已「分开写」⇒ 满足 MA-004「采用 Set<Claim> 派生度量」的字面描述。证明：字面闭合。但此非数学层收敛（P1 才是），仅证明文档未违反自身字面值。

---

## Proof Obligation 账本（Iter17）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I17-a | net/peak 各自未良定义，「分清」仅命名层 | open(高) | 见 Q1 / Iter15 I15-01,I15-06 | L182-183, L163-165, L167 |
| PO-I17-b | Peak(count,i)/peak(Σsize,t)/net 三对象混用 | open | 统一度量对象+注明抽象层 | L154, L167, L163-165, L667 |
| PO-I17-c | net 与 peak 语义域不相交性未证 | open | 证二者物理对象分离+阈值基准 | L182-183, L20-21 |
| PO-I17-d | net<0 语义未定义，与 DO-9 冲突 | open | 定义 net 符号语义规则 | L163-165, L21, Iter08 |
| PO-I17-e | Set<Claim> 化 ≠ 混淆已解决 | open | 见 P1 数学层条件 | L182-183, L94 |

## 本轮新发现未消解缺口（I17- 前缀，全局唯一）
- **I17-01（高）**：MA-004 声称「net/peak 混淆已解决」，但 net 无 scope（Iter15 I15-06）、peak 依赖未定义 ⊆（Iter15 I15-01），二者数学层均悬空；「分清」仅是标识符命名层，非语义分离，收敛不实。
- **I17-02**：文中三个度量对象 `Peak`(count,遍历 i∈1..ω, L154)、`peak`(Σsize,遍历 t∈scope, L167)、`net`(Σcreate−Σrelease, L163-165) 量纲/遍历域/依赖均不同，跨节跳跃无统一基准，§12.2 报告 1280MB 数值与 L154 的 Peak 矛盾。
- **I17-03**：MA-004 未证 net 与 peak 语义域不相交（peak=窗口最大、net=全程净值的时间维度未对齐），DO-8/DO-9 分别依赖二者但二者定义都未立。
- **I17-04**：`net<0`（释放多于创建）语义未定义，与 DO-9「无释放路径才报警」的报警方向冲突；且 net 无 scope 使「作用域内 net<0」不可算。
- **I17-05**：MA-004「采用 Set<Claim>」仅重构表示层，未消除 peak 内部矛盾（⊆ 缺失、两 Peak 冲突、DO-7 量纲混加），属「已收敛」过度声称（交叉 Iter04/Iter07/Iter16 同母题）。
- **I17-06（弱）**：§12.2 用「accumulated VramMB」措辞介于 net/peak 之间，概念漂移，凸显 MA-004 未澄清二者在报告层的呈现边界。

---

一句话摘要：MA-004「net 与 peak 混淆已解决」仅停留在**命名层**——net 无 scope（Iter15 I15-06）、peak 依赖未定义 ⊆（Iter15 I15-01），二者数学层都悬空，所谓「分清」不实（I17-01，高）；文中 Peak(count,i)/peak(Σsize,t)/net 三对象量纲与遍历域均不同、跨节混用无基准（I17-02）；net 与 peak 语义域不相交性未证（I17-03）、net<0 语义未定义与 DO-9 冲突（I17-04）；「Set<Claim> 化」仅重构表示层、未消除 peak 内部矛盾，属「已收敛」过度声称（I17-05）——MA-004 实际未收敛。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter17.md，未读/改其它 audit 文件，聚焦 §3.4 MA-004 net/peak 混淆收敛真伪，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #17（hy3 单独进程，本轮重跑）」、Q1-Q6 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I17- 缺口列表；交叉引用真实行号(L154/L163-165/L167/L182-183/L20-21/L667 等)并经 read 确认 §3.2.5/§3.3/§3.4 真实文本"}
  ],
  "changedFiles": ["audit/iter17.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 149, 45) + (offset 176, 20)", "result": "passed", "summary": "读取 §3.2.5 Peak、§3.3.1 net、§3.3.2 peak、§3.4 MA-004 真实文本"},
    {"command": "read PDR (offset 1, 25) + (offset 655, 25)", "result": "passed", "summary": "读取 §1 DO-8/DO-9、§12.2 报告数值真实文本确认 net/peak 概念漂移"},
    {"command": "write D:/Godot/Cosmos/audit/iter17.md", "result": "passed", "summary": "覆盖写入独立审计 #17"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 Q1-Q6 六节 + Proof Obligation 账本 + 6 条 I17- 缺口", "交叉引用 §3.2.5/§3.3.1/§3.3.2/§3.4 MA-004/§1 DO-8,DO-9/§12.2/Iter15/Iter16 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 net/peak 实际生成代码语义（仅基于文档 §3.3 文本推导）", "ω=∞ 时 Peak 未定义依赖 Iter04 PO-I4-a 交叉引用，未本轮重读 §3.2.4"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter17.md，独立审计 §3.4 MA-004 net/peak 混淆收敛真伪",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 MA-004「已解决」仅命名层、net/peak 数学层均悬空、三度量对象混用，需 PDR 侧修正"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
