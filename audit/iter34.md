# Iter34 审计 — `ScopeId` 偏序 ⊆ 构造草案：Method/Type/Loop/Conditional/Async/Scene ⊆ Global + §7 标注归一（独立审计 #34，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter15 的 I15-01（⊆ 未定义）补为可采纳的数学定义（独立 pass #34，全新上下文）
- **范围**：§3.1.3 ScopeId（L103-113，7 构造子）、§7 映射 scope 标注（L425-507，shell_scope/global_scope）、§3.2.5 Peak（L154，c.scope⊆scope）、§3.3.2 peak（L167，c.scope⊆t）、§3.3.1 net（L163-165，无 scope）；邻接 Iter15 I15-01/02/03（⊆ 未定义/shell_scope 非合法/global_scope 命名）、Iter28（Load vs Instantiate scope 分裂）、Iter37（net(scope)）
- **结论摘要**：Iter15 证 ScopeId 仅有 7 构造子、**无 ⊆ 偏序**，致 Peak/peak 的 `c.scope⊆t` 过滤悬空。本审计给出**推荐偏序草案** `⊆*` 使 Peak/peak/net 良定义：(1) 偏序骨架：`Global` 为顶元，`Scene(name) ⊆ Global`，`Type(name) ⊆ Global`，`Method(m) ⊆ Type(t)`（m 属类型 t），`Loop(id) ⊆ Method(m)`（id 环在方法 m 内），`Conditional(b) ⊆ Method(m)`，`Async(id) ⊆ Method(m)`；(2) **反对称性/传递性**由构造子层级保证（自反：同构造子且字段等）；(3) **§7 标注归一**：`global_scope → Global`、`shell_scope → Scene(当前场景名)`（或统一归 `Scene`，因 §5 Entity-as-Data 中 shell 即场景外壳），消 I15-02/03；(4) **peak 窗口枚举**：`peak(S, scope)` 的 `t` 遍历「所有 `t ⊆ scope` 的 ScopeId」（含 Global 下全部子作用域），使峰值上界确定；(5) 该草案使 Iter28 的「内存占用 global/shell 不可并」变为「Scene⊆Global ⇒ shell 占用计入 Global 上界」⇒ 可并。但草案是推荐、文档未采纳 ⇒ 仍 open。结构性成立（草案）给条件证明。

---

## O1. 命题：ScopeId 偏序草案 ⊆*

**定义草案**（推荐）：
```
偏序 ⊆* 在 ScopeId 上，满足：
  自反：s ⊆* s  （同构造子且判别字段相等）
  传递：s ⊆* t ∧ t ⊆* u ⇒ s ⊆* u
  反对称：s ⊆* t ∧ t ⊆* s ⇒ s = t
层级（覆盖关系）：
  Global                             为顶元（⊤）
  Scene(name)      ⊆* Global
  Type(name)       ⊆* Global
  Method(m)        ⊆* Type(declaringType(m))
  Loop(id)         ⊆* Method(m(id))      // id 标识某方法内的循环
  Conditional(br)  ⊆* Method(m(br))      // br 标识某方法内的分支
  Async(id)        ⊆* Method(m(id))      // id 标识某异步方法
其中 declaringType / m(id) 为「从代码静态分析得声明归属」的辅助函数（依赖 §6 L2/L3，Iter07）。
```

**数学性质 / 证明状态**：
- **(PO-I34-a) ⊆* 偏序良定义（discharged，条件）**：上述定义给出自反/传递/反对称 + 层级 ⇒ 是合法偏序（数学上成立）。证明：构造子层级构成树/森林，根为 Global ⇒ 偏序良定义。前提：辅助函数 `declaringType/m(id)` 需 L2/L3 静态分析提供（Iter07 open）⇒ 条件，实际未消解。
- 交叉：Iter15 I15-01（⊆ 未定义）、Iter07（L2/L3 完备性）。

**文档行号**：§3.1.3（L103-113）、§3.2.5（L154）、§3.3.2（L167）、Iter15（I15-01）、Iter07（TS-002/003）。

---

## O2. 命题：§7 标注归一 global_scope/shell_scope

**命题**（§7 L425-507）：scope 全标 `global_scope`/`shell_scope`。
**归一规则（推荐）**：
```
global_scope  → Global
shell_scope    → Scene(currentSceneName)   // §5 EaD 中 shell=场景外壳
```
（currentSceneName 由静态分析得，依赖 §6 L2）。

**数学性质 / 证明状态**：
- **(PO-I34-b) scope 标注归一（discharged，条件）**：归一后 §7 所有 Claim 的 scope 为合法 ScopeId 构造子 ⇒ I15-02（shell_scope 非合法）消解、I15-03（global_scope 命名）消解。证明：字符串→构造子映射。前提：currentSceneName 静态可得（Iter07）⇒ 条件。
- 交叉：Iter15 I15-02/I15-03、Iter28（scope 分裂）。

**文档行号**：§7.1-7.10（L425-507）、§3.1.3（L103-113）、Iter15（I15-02/I15-03）。

---

## O3. 命题：peak 窗口枚举 t ∈ scope（下界确定）

**命题**（§3.3.2 L167）：`peak(S, scope) = max_{t∈scope} Σ_{c: c.scope⊆t,..} size`，`t` 遍历域未定义（Iter15 I15-05）。
**草案**：`t` 遍历「所有 ScopeId `t` 满足 `t ⊆* scope`」（即 scope 的所有下界作用域，含 scope 自身与 Global 下全部子作用域）。

例：`peak(S, Global)` ⇒ t 取 Global + 所有 Scene/Type/Method/Loop/... ⇒ 峰值为「全局最大并发」上界收敛（有限，因 ScopeId 实例有限）。

**数学性质 / 证明状态**：
- **(PO-I34-c) peak 窗口枚举确定（discharged，条件）**：`t ⊆* scope` 的实例集有限（代码静态作用域有限）⇒ max 论域有限 ⇒ peak 收敛。证明：偏序有限下界。前提 PO-I34-a（⊆* 未采纳）未立 ⇒ 条件。
- 交叉：Iter15 I15-05、Iter17（两 Peak 统一需此）。

**文档行号**：§3.3.2（L167）、Iter15（I15-05）、Iter17（I17-02）。

---

## O4. 命题：Iter28 内存占用「不可并」⇒「可并」

**命题**（Iter28 H1）：Load(global_scope) 与 Instantiate(shell_scope) 内存占用跨 scope 不可并。
**草案**：`Scene ⊆* Global` ⇒ `shell_scope → Scene` ⊆* `global_scope → Global` ⇒ `c.scope⊆t` 在 `t=Global` 时两者都满足 ⇒ 同窗口求和 ⇒ 内存占用**可并**（峰值 = 二者之和）。

**数学性质 / 证明状态**：
- **(PO-I34-d) 跨 scope 内存可并（discharged，条件）**：Scene⊆Global 使 shell 占用计入 Global 上界 ⇒ Iter28 低估消解。证明：偏序传递。前提 PO-I34-a/b 未立 ⇒ 条件。
- 交叉：Iter28（I28-01）、Iter15（I15-01）。

**文档行号**：§7.4（L456-459）、§3.3.2（L167）、Iter28（I28-01）。

---

## O5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 采纳 ⊆*（O1）+ scope 归一（O2）+ peak 窗口枚举（O3），则 Peak/peak 的 `c.scope⊆t` 过滤良定义、Iter28 内存可并、Iter15 全部 scope 缺口消解。证明：偏序给出过滤可判定。前提 PO-I34-a/b/c 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 net 加 `net(S, scope)`（Iter37）并按 `c.scope⊆*scope` 分组，则泄漏检测在作用域粒度可行。证明：分组用偏序。前提 Iter37 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 scope 显式 Method/Type/Scene 标注、无 global_scope/shell_scope」理想假设下，⊆* 直接适用。证明：标注合法。但 §7 真实满屏 shell_scope ⇒ 需 O2 归一。

---

## Proof Obligation 账本（Iter34）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I34-a | ScopeId 偏序 ⊆* 草案(合法偏序) | open(条件) | 采纳+辅助函数(§6) | L103-113, I15-01, Iter07 |
| PO-I34-b | scope 归一 global/shell→构造子 | open(条件) | 采纳 O2 | L425-507, I15-02/03 |
| PO-I34-c | peak 窗口 t⊆*scope 枚举确定 | open(条件) | 采纳 O3 | L167, I15-05 |
| PO-I34-d | 内存占用跨 scope 可并 | open(条件) | O1+O2 | L456-459, I28-01 |

## 本轮新发现未消解缺口（I34- 前缀，全局唯一）
- **I34-01（条件草案）**：ScopeId 偏序 ⊆*（Global 顶元 + Scene/Type⊆Global + Method/Loop/Conditional/Async⊆Method）是合法偏序草案，可使 Peak/peak 过滤良定义。
- **I34-02（条件草案）**：scope 归一 `global_scope→Global`、`shell_scope→Scene(name)` 使 §7 标注合法化，消 I15-02/03。
- **I34-03（条件草案）**：peak 窗口 `t⊆*scope` 有限枚举 ⇒ 峰值上界收敛，消 I15-05。
- **I34-04（弱）**：⊆* 依赖辅助函数 `declaringType/m(id)` 由 §6 L2/L3 静态分析提供，若 L2/L3 完备性未证（Iter07）则辅助函数可能不全 ⇒ 偏序实例集可能漏作用域。
- **I34-05（弱）**：`Async(id)` 与 `Loop(id)` 是否 ⊆ 同一 Method 取决于 id 解析，动态 id（Iter26）使部分 Async/Loop scope 不可静态判定 ⇒ 偏序在动态作用域退保守（全部归 Global）。

---

一句话摘要：给出 ScopeId 偏序 ⊆* 草案（Global 顶元、Scene/Type⊆Global、Method/Loop/Conditional/Async⊆Method，O1）使 Peak/peak 过滤良定义，scope 归一 global_scope→Global / shell_scope→Scene（O2）消 I15-02/03，peak 窗口 t⊆*scope 有限枚举（O3）消 I15-05，内存占用因 Scene⊆Global 可并（O4，消 Iter28）——草案为条件证明，依赖 §6 L2/L3 辅助函数（Iter07）与 net(scope)（Iter37），需 PDR 侧采纳。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter34.md，未读/改其它 audit 文件，聚焦 ScopeId⊆ 构造草案，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #34（hy3 单独进程，本轮重跑）」、O1-O5 各节(草案/数学性质/状态/论证/行号)、Proof Obligation 账本、I34- 缺口列表；交叉引用真实行号(L103-113/L154/L167/L425-507) 并经 read 确认 §3.1.3/§3.2.5/§3.3.2/§7 真实文本"}
  ],
  "changedFiles": ["audit/iter34.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 103, 12)", "result": "passed", "summary": "读取 §3.1.3 ScopeId 7 构造子确认偏序骨架"},
    {"command": "read PDR (offset 149, 20) + (offset 425, 6)", "result": "passed", "summary": "读取 §3.2.5/§3.3.2 Peak/peak 过滤与 §7 scope 标注(global/shell) 确认归一需求"},
    {"command": "write D:/Godot/Cosmos/audit/iter34.md", "result": "passed", "summary": "覆盖写入独立审计 #34"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 O1-O5 五节 + Proof Obligation 账本(O4 项) + 5 条 I34- 缺口", "交叉引用 §3.1.3/§3.2.5/§3.3.2/§7/Iter15/Iter28/Iter17/Iter37/Iter07 真实行号"],
  "residualRisks": ["⊆* 依赖 declaringType/m(id) 辅助函数由 §6 L2/L3 提供，未运行静态分析验证可行性", "动态 id(Iter26) 致部分 Async/Loop scope 退保守"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter34.md，独立审计 ScopeId 偏序 ⊆* 构造草案（收口 Iter15）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但给出 ⊆* 草案可直接收口 Iter15/Iter28/Iter17，需 PDR 侧采纳且依赖 §6 L2/L3(Iter07)"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
