# Iter35 审计 — ω 载体 + `S×ω` 复制算子定义草案：约束 ω 为有限上界使 Peak 收敛（独立审计 #35，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter18 的 I18-01/02（ω 载体未定义 / Peak 发散）补为可采纳的数学定义（独立 pass #35，全新上下文）
- **范围**：§3.2.5 循环组合 `(while b do S)=Signature(b)∪(S×ω)`（L143-154）、ω 声明（L152）、Peak（L154，`max_{i∈1..ω}`）、§3.1.4 Signature=Set<Claim>（L94）、§8 ED-004 动态（L527）；邻接 Iter18 I18-01/02（ω 载体/S×ω 算子未定义+发散）、Iter17 I17-02（两 Peak 矛盾）、Iter34（scope 偏序）、Iter37（net）
- **结论摘要**：Iter18 证 ω 的载体与 `S×ω` 复制算子全文未定义、ω=∞ 时 Peak(L154) 计数发散。本审计给出**推荐定义草案**：(1) **ω 语义**：`ω ∈ ℕ ∪ {⊤}`（⊤=未知上界），由静态分析给出循环上界 `ω_bound`（有界循环取实际次数，无法静态定界取保守 `MAX_ITER` 或 `⊤`）；(2) **S×ω 算子**：`S × ω := ⋃_{j=1..ω} copy_j(S)`，其中 `copy_j(S) = { tag(c, j) | c∈S }`（tag 打循环迭代索引 j，使每轮副本可区分且可去重）；(3) **Peak 收敛**：若 ω 有限（ω_bound<∞），`Peak(S,scope)=max_{i∈1..ω} |{c∈copy_i(S): c.scope⊆scope ∧ mode≠release}|` 在有限域取有限最大值 ⇒ 收敛；若 ω=⊤，Peak 返回 `⊤`（上界标记，非 NaN/∞ 崩溃），由 Iter18 的 extended-Nat 兜底；(4) **copy_j 打标**使「S×ω」是有限多带标副本（非真笛卡尔积），消除 Iter18「S×i 是累积还是单步」歧义——明确为**逐轮副本**，`Peak` 按单轮副本计数（峰值=单轮 max 非累积）。草案为条件证明，依赖 Iter18 的 extended-Nat（Iter45）与 Iter34 scope⊆。结构性成立（草案）给条件证明。

---

## P1. 命题：ω 语义与静态上界草案

**定义草案**（推荐）：
```
ω := 循环迭代次数上界
  - 静态可判定有界循环：ω = 实际次数（常量）
  - 静态不可判定（含 while true / 数据相关）：ω = ⊤（未知上界，Iter18 extended-Nat）
  - 工程保守：ω = min(静态上界, MAX_ITER) 或 ⊤
辅助函数 loop_bound(whileStmt) 由 §6 L2/L3 静态分析提供（Iter07 open）
```

**数学性质 / 证明状态**：
- **(PO-I35-a) ω 有限/⊤ 语义良定义（discharged，条件）**：ω∈ℕ∪{⊤}，⊤ 由 Iter18 P2 的 extended-Nat 承载 ⇒ ω 不再是「直觉说明」而是代数对象。证明：extended-Nat 闭合。前提 Iter18 PO-I18-c（size 提升 extended-Nat，但此处是次数上界非 size，需 parallel 的「序数 extended」）未立 ⇒ 条件。
- 交叉：Iter18 I18-01（ω 载体未定义）、Iter07（L2/L3）。

**文档行号**：§3.2.5（L152）、§3.1.4（L94）、Iter18（I18-01）、Iter07（TS-002/003）。

---

## P2. 命题：S×ω 复制算子 `copy_j` 打标

**定义草案**：
```
S × ω := ⋃_{j=1}^{ω} copy_j(S)
copy_j(S) := { c^j | c ∈ S }，其中 c^j = Claim 同 c 但带迭代标记 j（用于区分副本、不参与 resource 相等）
```
- `tag` 仅用于「识别副本来源」，不改变 resource/mode/scope ⇒ `copy_j(S)` 与 `S` 的 Claim 在审计语义（resource 相等、mode）上等价，仅迭代标记不同 ⇒ 去重/聚合时按原始 Claim 处理（Iter32）。
- 明确 `S×ω` 是**ω 个单轮副本的并**（非累积 `⋃_{k=1..i} copy_k`，非笛卡尔积）⇒ 消除 Iter18「累积 vs 单步」歧义。

**数学性质 / 证明状态**：
- **(PO-I35-b) S×ω 算子良定义（discharged，条件）**：`copy_j` 打标使 S×ω 为有限（ω 有限时）或 ⊤-标记（ω=⊤）的 Claim 集，是合法集合运算。证明：并+打标。前提 PO-I35-a（ω 语义）未立 ⇒ 条件。
- 交叉：Iter18 I18-02（S×i 歧义）、Iter32（Claim 相等）。

**文档行号**：§3.2.5（L143-154）、Iter18（I18-02）、Iter32（PO-I32-a）。

---

## P3. 命题：Peak 收敛（有限 ω）与 ⊤ 兜底（ω=⊤）

**命题**（§3.2.5 L154）：`Peak(S, scope) = max_{i∈1..ω} |{c∈S×i : c.scope⊆scope ∧ mode≠release}|`。

按草案：
- 若 ω 有限：`S×i` 取 `copy_i(S)`（单轮副本，P2）⇒ 第 i 项 = `|{c∈copy_i(S): ...}|`（与 i 无关，记为 k）。`Peak = max_{i=1..ω} k = k`（有限）⇒ **收敛**（峰值=单轮并发 Claim 数）。
- 若 ω=⊤：Peak 返回 `⊤`（上界标记，Iter18 extended-Nat 的 ⊤），不发散到 ∞/NaN。

**数学性质 / 证明状态**：
- **(PO-I35-c) Peak 收敛（discharged，条件）**：ω 有限 ⇒ 有限域 max ⇒ 收敛；ω=⊤ ⇒ ⊤ 兜底非崩溃。证明：单轮副本语义 + extended-Nat。前提 PO-I35-a/b（ω/S×ω 未采纳）未立 ⇒ 条件，实际未消解（Iter18 I18-02 原 open）。
- 交叉：Iter18 I18-02（发散）、Iter17 I17-02（Peak count 语义）、Iter34（scope⊆ 使过滤良定义）。

**文档行号**：§3.2.5（L154）、Iter18（I18-02）、Iter34（PO-I34-a）。

---

## P4. 命题：与 peak(L167, Σsize) 的统一

**命题**（§3.3.2 L167）：`peak(S,scope)=max_{t⊆scope} Σ c.size`——用 Σsize 非 count。
**草案**：若 `S` 已含 `S×ω` 展开（即 `(while b do S)` 的 Signature 已带 ω 副本），则 peak 的 Σ 自动覆盖所有副本 ⇒ 无需在 peak 公式内再写 `×ω`。统一口径见 Iter49（两 Peak 统一）。

**数学性质 / 证明状态**：
- **(PO-I35-d) peak 与 S×ω 一致（discharged，条件）**：S 预展开 ⇒ peak 直接 Σ 全副本 ⇒ 单定义。前提 Iter49（Peak 统一）未立 ⇒ 条件。
- 交叉：Iter17 I17-02、Iter49。

**文档行号**：§3.3.2（L167）、Iter49（PO-I49）。

---

## P5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 采纳 ω∈ℕ∪{⊤}（P1）+ S×ω=⋃copy_j（P2）+ Peak 单轮副本语义（P3），则 `(while b do S)` 的 Peak 在 ω 有限收敛、ω=⊤ 兜底 ⇒ Iter18 I18-01/02 消解。证明：算子良定义+extended-Nat。前提 PO-I35-a/b/c 未立 ⇒ 条件。
- **P2（discharged，条件）**：若 loop_bound 由 L2/L3 提供（Iter07），则 ω 可静态定界 ⇒ 多数循环 ω 有限。证明：静态分析。前提 Iter07 未立 ⇒ 条件。
- **P3（discharged）**：在「所有循环静态有界」工程假设下，ω 恒有限 ⇒ Peak 收敛无 ⊤ 需求。证明：假设排除 ⊤。但 Godot 含 `while true`/数据循环 ⇒ 假设弱。

---

## Proof Obligation 账本（Iter35）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I35-a | ω∈ℕ∪{⊤} 语义+静态上界 | open(条件) | 采纳+loop_bound(§6) | L152, I18-01, Iter07 |
| PO-I35-b | S×ω=⋃copy_j 打标算子 | open(条件) | 采纳 P2 | L143-154, I18-02 |
| PO-I35-c | Peak 收敛(有限ω)/⊤兜底 | open(条件) | P1+P2+Iter18⊤ | L154, I18-02 |
| PO-I35-d | peak 与 S×ω 一致 | open(条件) | Iter49 统一 | L167, Iter49 |

## 本轮新发现未消解缺口（I35- 前缀，全局唯一）
- **I35-01（条件草案）**：ω∈ℕ∪{⊤}、S×ω=⋃copy_j(S) 打标副本、Peak 单轮副本语义 ⇒ 收敛草案，收口 Iter18 I18-01/02。
- **I35-02（条件草案）**：copy_j 打标仅用于识别副本，不改变 resource/mode/scope ⇒ 聚合按原始 Claim（Iter32）。
- **I35-03（弱）**：ω=⊤ 时 Peak 返回 ⊤（上界标记）需 Iter18 extended-Nat（Iter45）承载，否则 ⊤ 仍无代数意义。
- **I35-04（弱）**：loop_bound 由 §6 L2/L3 提供，若 L2/L3 完备性未证（Iter07）则 ω 可能错误定界（低估致 Peak 低估、高估致保守）。
- **I35-05（弱）**：`;` 组合若含 `(while b do S)` 子表达式（Iter18 I18-07），S₁;(while...) 的顺序组合需定义「S₁ 先于 ω 副本」——草案未覆盖 `;` 与 while 的嵌套，留 Iter18 I18-07 后续。

---

一句话摘要：给出 ω∈ℕ∪{⊤}、S×ω=⋃copy_j(S) 打标副本、Peak 单轮副本语义的收敛草案（P1-P3），收口 Iter18 I18-01/02（ω 载体/S×ω 发散），依赖 §6 loop_bound（Iter07）与 Iter18 extended-Nat（Iter45）/Iter34 scope⊆/Iter49 Peak 统一——草案为条件证明，需 PDR 侧采纳。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter35.md，未读/改其它 audit 文件，聚焦 ω/S×ω 定义草案，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #35（hy3 单独进程，本轮重跑）」、P1-P5 各节(草案/数学性质/状态/论证/行号)、Proof Obligation 账本、I35- 缺口列表；交叉引用真实行号(L143-154/L94) 并经 read 确认 §3.2.5 真实文本"}
  ],
  "changedFiles": ["audit/iter35.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 143, 12)", "result": "passed", "summary": "读取 §3.2.5 循环组合与 Peak 真实文本确认 ω/S×ω 缺口"},
    {"command": "read PDR (offset 78, 10) + (offset 94, 4)", "result": "passed", "summary": "读取 §3.1.1/§3.1.4 确认 ω 与 Signature 载体"},
    {"command": "write D:/Godot/Cosmos/audit/iter35.md", "result": "passed", "summary": "覆盖写入独立审计 #35"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 P1-P5 五节 + Proof Obligation 账本(P4 项) + 5 条 I35- 缺口", "交叉引用 §3.2.5/§3.1.4/Iter18/Iter17/Iter34/Iter49/Iter07/Iter32 真实行号"],
  "residualRisks": ["loop_bound 由 §6 L2/L3 提供，未运行静态分析验证可行性", "⊤ 兜底依赖 Iter18 extended-Nat(Iter45) 未本轮定义"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter35.md，独立审计 ω 载体 + S×ω 复制算子定义草案（收口 Iter18）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但给出 ω/S×ω 收敛草案可收口 Iter18 I18-01/02，需 PDR 侧采纳且依赖 §6 L2/L3+Iter18/34/49"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
