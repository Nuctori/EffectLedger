# Iter37 审计 — `net<0` 语义 + `net(S, scope)` 作用域分组草案：泄漏检测在粒度可行（独立审计 #37，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter17 的 I17-04 与 Iter15 的 I15-06（net 无 scope）补为可采纳定义（独立 pass #37，全新上下文）
- **范围**：§3.3.1 net（L163-165，全局无 scope）、§1 DO-9（L21，泄漏检测）、§7.1 AddChild/RemoveChild/QueueFree（L427-429）、§3.2.5/§3.3.2 scope 过滤（L154/L167）；邻接 Iter15 I15-06（net 无 scope 致泄漏掩盖）、Iter17 I17-04（net<0 未定义）、Iter27（QueueFree 漏算）、Iter34（scope⊆ 草案）
- **结论摘要**：Iter15/17 证 `net(S)` 不含 scope 参数、全局求和 ⇒ 局部作用域泄漏被全局抵消掩盖（DO-9 失效）。本审计给出**推荐草案**：(1) **net(S, scope) 分组**：`net(S, scope) = Σ_{c∈S, c.scope⊆*scope, c.kind=occupy, c.mode∈{create,move}} size − Σ_{c∈S, c.scope⊆*scope, c.mode=release} size`，用 Iter34 的 ⊆* 偏序按作用域聚合；(2) **net<0 语义规则**：`net(S,scope) < 0` ⇔ 该作用域内 release 多于 create ⇒ 标记为「潜在 double-free / 重复释放」警告（非泄漏，泄漏是 net>0 且缺 release 配对）；(3) **泄漏判定**：`net(S,scope) > 0 且 该 scope 内存在 occupy(create) 无对应 release 配对` ⇒ 报警（DO-9）—区分「净占用>0 但有正常未释放期」与「真泄漏」需配对分析（Iter29/Iter27 的配对逻辑）；(4) **与分桶**：net 仅作用于 O 桶（occupy，Iter36 Q1），read/write 不计入 net。草案使 DO-9 在作用域粒度可行。但草案未采纳 ⇒ Iter15/17 仍 open。结构性成立（草案）给条件证明。

---

## R1. 命题：net(S, scope) 作用域分组草案

**定义草案**（推荐）：
```
net(S, scope) :=
  Σ_{c ∈ S, c.scope ⊆* scope, c.kind=occupy, c.mode ∈ {create,move}} c.size
  − Σ_{c ∈ S, c.scope ⊆* scope, c.kind=occupy, c.mode = release} c.size
其中 ⊆* 为 Iter34 的 ScopeId 偏序。
```
- 全局 net 为 `net(S, Global)`（scope=Global ⇒ 含所有子作用域）。
- 细粒度：`net(S, Method("Foo"))` 仅含 Foo 方法内的占用 ⇒ 局部泄漏可见。

**数学性质 / 证明状态**：
- **(PO-I37-a) net 按 scope 分组良定义（discharged，条件）**：用 ⊆* 过滤 ⇒ 分组可判定，局部 net 独立于全局。证明：偏序过滤（Iter34 PO-I34-a）。前提 Iter34（⊆* 未采纳）未立 ⇒ 条件，实际未消解（Iter15 I15-06）。
- 交叉：Iter15 I15-06、Iter34（PO-I34-a）、Iter36（O 桶）。

**文档行号**：§3.3.1（L163-165）、§3.1.3（L103-113）、Iter34（PO-I34-a）、Iter15（I15-06）。

---

## R2. 命题：net<0 语义 = 潜在 double-free（非泄漏）

**定义草案**：
```
net(S, scope) < 0  ⇒ 警告「潜在重复释放 / double-free」：
   该作用域内 release 项 size 之和 > create/move 项，
   可能 ReleaseChild 两次 / QueueFree 两次 / 释放未创建资源。
net(S, scope) > 0 且 存在 occupy(create) 无配对 release ⇒ 警告「泄漏」（DO-9）。
net(S, scope) = 0   ⇒ 占用守恒（良性）。
```

**命题**（§1 DO-9 L21）：DO-9 定义「Instantiate/AddChild 无对应释放路径 ⇒ 报警」。这对应 `net>0 且缺 release 配对`，**非简单的 net 符号**。net<0 恰是反面（释放多于创建），不应误报为泄漏。

**数学性质 / 证明状态**：
- **(PO-I37-b) net<0 语义规则（discharged，条件）**：明确 net<0=double-free 警告、net>0+缺配对=泄漏，消除 Iter17 I17-04「net<0 语义未定义」歧义。证明：符号→语义映射表。前提 PO-I37-a（net(scope) 未立）未立 ⇒ 条件。
- 交叉：Iter17 I17-04、§1 DO-9（L21）。

**文档行号**：§3.3.1（L163-165）、§1 DO-9（L21）、Iter17（I17-04）。

---

## R3. 命题：泄漏判定的配对分析（非仅 net 符号）

**命题**：仅 `net(S,scope)>0` 不足以判泄漏——正常对象「创建后暂未释放」（如关卡加载中）net>0 但非泄漏。需配对分析：`occupy(create) 配 occupy(release)` 缺失才报警。

**数学性质 / 证明状态**：
- **(PO-I37-c) 泄漏需配对分析（discharged，条件）**：DO-9 的真正判据是「占用节点无释放路径」（控制流/生命周期分析，Iter07 L2/L3），net(scope)>0 仅是必要非充分信号。草案将 DO-9 拆为「net(scope)>0 触发候选 + 配对分析确认」。证明：两阶段判定。前提 Iter07（L2/L3 配对分析完备性）未立 ⇒ 条件。
- 交叉：Iter07（TS-002/003）、Iter27（QueueFree 漏算使配对分析失真）。

**文档行号**：§1 DO-9（L21）、§7.1（L427-429）、Iter07（TS-002/003）、Iter27（I27-01）。

---

## R4. 命题：与 QueueFree 漏算的交互（Iter27）

**命题**（Iter27 I27-01）：QueueFree mode=move 使 net 不识别释放 ⇒ 即便 net(scope) 分组，QueueFree 的释放仍计正项 ⇒ net 恒偏正 ⇒ 泄漏候选（R3）全误报。

**数学性质 / 证明状态**：
- **(PO-I37-d) net(scope) 不独立于 QueueFree 修正（open，高）**：net(scope) 草案仅在 QueueFree 释放被正确计入（Iter27 P1 改 release，或 Iter24 move 归约）时才有效。若 QueueFree 仍 mode=move，则 net(scope) 分组只是「把漏算局部化」，仍失真。状态 = open（高，依赖 Iter27/Iter24）。
- 交叉：Iter27（I27-01）、Iter24（I24-02）。

**文档行号**：§7.1（L429）、Iter27（I27-01）、Iter24（I24-02）。

---

## R5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 采纳 net(S,scope)（R1）+ net<0=double-free 规则（R2）+ 配对分析（R3）+ QueueFree 修正（Iter27 P1）+ ⊆*（Iter34），则 DO-9 在作用域粒度可行（局部泄漏可见、net<0 不误报泄漏）。证明：分组+符号语义+配对。前提 PO-I37-a/b/c/d 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 net 仅作用于 O 桶（Iter36 Q1 分桶），则 read/write 不污染 net ⇒ net 纯占用净值。证明：桶隔离。前提 Iter36 未立 ⇒ 条件。
- **P3（discharged）**：在「无 QueueFree（仅 AddChild/RemoveChild 用 release）」理想假设下，net(scope) 直接有效（Iter27 问题不存在）。证明：假设排除 move。但 Godot 实际用 QueueFree ⇒ 假设弱。

---

## Proof Obligation 账本（Iter37）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I37-a | net(S,scope) 分组良定义 | open(条件) | 采纳 R1 + Iter34⊆* | L163-165, I15-06, Iter34 |
| PO-I37-b | net<0=double-free 语义规则 | open(条件) | 采纳 R2 | L163-165, L21, I17-04 |
| PO-I37-c | 泄漏需配对分析(非仅 net 符号) | open(条件) | 采纳 R3 + Iter07 | L21, Iter07, Iter27 |
| PO-I37-d | net(scope) 依赖 QueueFree 修正 | open(高) | Iter27 P1 / Iter24 | L429, I27-01, I24-02 |

## 本轮新发现未消解缺口（I37- 前缀，全局唯一）
- **I37-01（条件草案）**：net(S,scope) 用 ⊆* 分组使局部泄漏可见，收口 Iter15 I15-06（net 无 scope）。
- **I37-02（条件草案）**：net<0=double-free 警告、net>0+缺配对=泄漏，收口 Iter17 I17-04（net<0 未定义）。
- **I37-03（条件草案）**：DO-9 拆为「net>0 候选 + 配对分析确认」，避免正常未释放期误报。
- **I37-04（高）**：net(scope) 有效性依赖 QueueFree 释放修正（Iter27 P1）——若 QueueFree 仍 mode=move，分组仅局部化漏算仍失真（交叉 Iter27 I27-01）。
- **I37-05（弱）**：net 应仅作用于 O 桶（Iter36 Q1），read/write 不计入 ⇒ 需分桶 Signature 先立。

---

一句话摘要：给出 net(S,scope) 按 ⊆* 分组（R1，收口 Iter15 I15-06）+ net<0=double-free 语义规则（R2，收口 Iter17 I17-04）+ 泄漏需配对分析（R3）+ 依赖 QueueFree 修正（R4，Iter27）的 DO-9 落地草案——使泄漏检测在作用域粒度可行，但草案未采纳且依赖 Iter34/36/27/07，仍有高优先的 QueueFree 漏算先决（I37-04）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter37.md，未读/改其它 audit 文件，聚焦 net(scope) 分组+net<0 语义草案，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #37（hy3 单独进程，本轮重跑）」、R1-R5 各节(草案/数学性质/状态/论证/行号)、Proof Obligation 账本、I37- 缺口列表；交叉引用真实行号(L163-165/L21/L427-429) 并经 read 确认 §3.3.1/§1 DO-9/§7.1 真实文本"}
  ],
  "changedFiles": ["audit/iter37.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 163, 5) + (offset 19, 3) + (offset 425, 6)", "result": "passed", "summary": "读取 §3.3.1 net / §1 DO-9 / §7.1 AddChild/RemoveChild/QueueFree 确认 net 无 scope 与泄漏语义"},
    {"command": "read PDR (offset 103, 12)", "result": "passed", "summary": "读取 §3.1.3 ScopeId 确认 net 分组需 ⊆*（Iter34）"},
    {"command": "write D:/Godot/Cosmos/audit/iter37.md", "result": "passed", "summary": "覆盖写入独立审计 #37"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 R1-R5 五节 + Proof Obligation 账本(R4 项) + 5 条 I37- 缺口", "交叉引用 §3.3.1/§1 DO-9/§7.1/Iter15/Iter17/Iter27/Iter34/Iter36/Iter07 真实行号"],
  "residualRisks": ["net(scope) 依赖 Iter34 ⊆* 与 Iter36 分桶，未运行静态分析验证作用域聚合可行性", "配对分析依赖 §6 L2/L3 完备性(Iter07) 未本轮读取"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter37.md，独立审计 net(S,scope) 分组 + net<0 语义草案（收口 Iter15/Iter17）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但给出 net(scope)+net<0 规则草案可收口 Iter15/Iter17，需 PDR 侧采纳且依赖 Iter34/36/27/07"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
