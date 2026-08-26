# Iter49 审计 — 两个 Peak 定义统一：§3.2.5 count 峰值 与 §3.3.2 Σsize 峰值 的矛盾收口（独立审计 #49，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter17 的「两个 Peak 矛盾」补为可采纳的统一定义（独立 pass #49，全新上下文）
- **范围**：§3.2.5 Peak（L154，`max_{i∈1..ω} |{ c∈S×i : c.scope⊆scope ∧ c.mode≠release }|` 计数）、§3.3.2 peak（L167，`max_{t⊆scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size` 求和）、§3.4 MA-004（L183，「occupy 峰值与净变化混淆，已解决：net 和 peak 是派生度量」）、Iter17 I17-02（两 Peak 矛盾）；邻接 Iter15/34（scope⊆）、Iter35（ω/S×ω）、Iter46（size 区间）、Iter18（发散）
- **结论摘要**：同一文档有两个 `Peak/peak` 定义：(A) §3.2.5 L154 用 **count**（|{非 release Claim 数}|，不含 size）；(B) §3.3.2 L167 用 **Σsize**（求和 size）。二者对同一 S 给出不同数值——A 是「并发 Claim 条数」，B 是「并发资源规模」。审计发现：(1) **两个 Peak 是不同量纲**：A 无量纲计数、B 是资源单位总和；DO-7（Iter14/36/47）禁止 read/write/occupy 混算，但 B 把不同 kind 的 size 直接 Σ（Iter14 I14-02）⇒ B 违反 DO-7；(2) **scope 过滤域不同**：A 用 `c.scope⊆scope`（单次 scope 过滤），B 用 `c.scope⊆t, t∈scope`（遍历 t⊆scope 取 max，Iter34 O3 窗口枚举）——B 的峰值上界 ≥ A（B 取所有子作用域 max），二者论域不同；(3) **mode≠release 口径**：A/B 都用 `mode≠release`，但 net（§3.3.1 L163）用 `mode∈{create,move}` 减 release——peak 与 net 的「非 release」定义不一致（A/B 含 use/read/write，net 仅 occupy）⇒ 三者口径交错；(4) **ω 发散**：A 的 `max_{i∈1..ω}` 遇 ω=∞ 发散（Iter18/35/45），B 无 ω 但 S 含 S×ω 展开（Iter35）时也发散；(5) **MA-004「已解决」不实**：MA-004 称「net 和 peak 是派生度量，非原语」就解决了峰值/净变化混淆，但两个 peak 自身（count vs Σsize）仍矛盾、且 peak vs net 口径仍交错 ⇒ 混淆未真正消除。结构性成立给条件证明（统一为单一定义：peak_count 与 peak_size 双派生 + 统一 scope 窗口 + 统一 mode 过滤 + 接 Iter35/46），但两 Peak 矛盾仍 open。

---

## AA1. 命题：两 Peak 量纲不同（count vs Σsize）

**命题**（§3.2.5 L154）：Peak = |{非 release Claim}|（计数）。
**命题**（§3.3.2 L167）：peak = Σ size（资源单位）。

同 S 下：若有 3 个 occupy(size=64MB)，A=3（条）、B=192MB（单位）。两值不可比、用途不同（A=并发度、B=资源占用）。但文档用同一名 Peak/peak 指两物 ⇒ 读者混淆，且 DO-9/预算报警到底用哪个未定义（Iter47 AUDIT003 用 Σsize 向，Iter14 用条数向）。

**数学性质 / 证明状态**：
- **(PO-I49-a) 两 Peak 量纲不同（open，高）**：同一符号两定义 ⇒ 报警/比较口径不定。状态 = open（高，交叉 Iter14 I14-02、Iter47、Iter17 I17-02）。
- 文档行号：§3.2.5（L154）、§3.3.2（L167）、Iter17（I17-02）。

---

## AA2. 命题：scope 过滤域不同（单 scope vs 遍历 t⊆scope）

**命题**（§3.2.5 L154）：`c.scope⊆scope`——固定 scope。
**命题**（§3.3.2 L167）：`c.scope⊆t, t∈scope`，`max_{t⊆scope}`——遍历所有子作用域取 max（Iter34 O3）。

B 的峰值 = 「scope 下所有子作用域中最大者」≥ A 的「固定 scope 值」。二者论域不同 ⇒ 即便量纲统一，数值也不同（B 是更松的上界）。需统一为「固定 scope」或「遍历子作用域 max」之一。

**数学性质 / 证明状态**：
- **(PO-I49-b) scope 过滤域不同（open，中）**：A 固定 / B 遍历 max ⇒ 数值论域不同。状态 = open（中，交叉 Iter34 O3、Iter15 I15-05）。
- 文档行号：§3.2.5（L154）、§3.3.2（L167）、Iter34（O3）。

---

## AA3. 命题：mode 过滤口径交错（≠release vs ∈{create,move}）

**命题**（§3.2.5/§3.3.2）：peak 用 `mode≠release`。
**命题**（§3.3.1 L163）：net 用 `mode∈{create,move}` 减 release。

peak 把 use/read/write（非 release）全计入，net 仅 occupy(create/move) 减 release。即「峰值」含所有 kind 非 release、「净变化」仅 occupy ⇒ 两者口径交错：(a) 一个 read 计入 peak 但不计入 net；(b) 一个 move 计入 net（作正）但不计入 peak（非 release 也计入，move 是 create 类）——move 在 peak 与 net 都算正，但语义（move=转移非新占）未区分（Iter24）。需统一 mode 过滤定义。

**数学性质 / 证明状态**：
- **(PO-I49-c) peak/net mode 口径交错（open，中）**：peak≠release 含 read/write、net 仅 occupy ⇒ 口径不一致。状态 = open（中，交叉 Iter24、§3.3.1）。
- 文档行号：§3.2.5（L154）、§3.3.2（L167）、§3.3.1（L163）、Iter24。

---

## AA4. 命题：ω=∞ 发散（A 的 max_{i∈1..ω}）

**命题**（§3.2.5 L154）：`max_{i∈1..ω}`，ω 静态未知=∞（L152）。
**命题**（Iter35 PO-I35-c）：ω=∞ ⇒ max 论域无限 ⇒ 发散。

A 的 Peak 直接遇 ω=∞ 发散（无 ⊤ 兜底，Iter45 W4）；B 无 ω 但若 S 已含 S×ω 展开（Iter35 P4）则 B 的 Σ 也含 ∞ 副本 ⇒ 同样发散。两 Peak 收敛都依赖 Iter35/45 的 ω/⊤ 闭包。

**数学性质 / 证明状态**：
- **(PO-I49-d) 两 Peak 遇 ω=∞ 发散（open，高）**：缺 ⊤ 饱和 ⇒ 两 Peak 在未知循环发散。状态 = open（高，交叉 Iter35 PO-I35-c、Iter45 W4、Iter18）。
- 文档行号：§3.2.5（L152-154）、Iter35（P4）、Iter45（W4）。

---

## AA5. 命题：MA-004「已解决」不实

**命题**（§3.4 MA-004 L183）：「occupy 峰值与净变化混淆，已解决：net 和 peak 是派生度量，非原语」。
**命题**：两 peak（count/Σsize）仍矛盾、peak/net mode 口径仍交错（AA1/AA3）⇒ 峰值与净变化的混淆未消除，只是从「原语混淆」转为「派生度量间口径不一致」。

MA-004 用「派生度量」回避了真正矛盾（两峰值定义 + 三度量口径），收敛标记不实（Iter19 量化 asserted）。

**数学性质 / 证明状态**：
- **(PO-I49-e) MA-004 收敛标记不实（open，中）**：两 Peak 矛盾 + 三度量口径交错仍在 ⇒ 混淆未消除。状态 = open（中，交叉 Iter17 I17-02、Iter19）。
- 文档行号：§3.4 MA-004（L183）、Iter17（I17-02）、Iter19。

---

## AA6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若统一为两个显式派生度量 `peak_count(S,scope)=max_{t⊆*scope} |{c∈S: c.scope⊆t, c.mode≠release}|` 与 `peak_size(S,scope)=max_{t⊆*scope} Σ c.size`（同 scope 窗口、同 mode 过滤），则两 Peak 命名歧义消解。证明：双定义显式。前提 Iter34（⊆*）、Iter46（size 口径）未立 ⇒ 条件。
- **P2（discharged，条件）**：若 ω=∞ 时两 Peak 返回 ⊤（Iter35/45 闭包），则收敛。证明：⊤ 兜底。前提 Iter35/45 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 scope=Global、ω 有限、size 单值、仅 occupy」理想假设下，A≈B（条数≈规模当每 occupy size=1）⇒ 矛盾弱化。证明：假设排除差异。但 §7 多 kind/多 scope/动态 ⇒ 假设弱。

---

## Proof Obligation 账本（Iter49）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I49-a | 两 Peak 量纲不同(count vs Σsize) | open(高) | 双派生度量命名 | L154, L167, I17-02 |
| PO-I49-b | scope 过滤域不同(固定 vs 遍历max) | open(中) | 统一 scope 窗口(Iter34) | L154, L167, I34-O3 |
| PO-I49-c | peak/net mode 口径交错 | open(中) | 统一 mode 过滤 | L154, L167, L163, I24 |
| PO-I49-d | 两 Peak 遇 ω=∞ 发散 | open(高) | ⊤ 饱和(Iter35/45) | L152-154, I35/45 |
| PO-I49-e | MA-004 收敛标记不实 | open(中) | 双度量统一 | L183, I17-02, I19 |

## 本轮新发现未消解缺口（I49- 前缀，全局唯一）
- **I49-01（高）**：§3.2.5 用 count、§3.3.2 用 Σsize，同符号两 Peak 量纲不同，报警/比较口径不定（交叉 Iter14/47/17）。
- **I49-02（中）**：A 固定 scope、B 遍历 t⊆scope 取 max，论域不同致数值不同（交叉 Iter34/15）。
- **I49-03（中）**：peak 用 mode≠release（含 read/write）、net 用 occupy(create/move) 减 release，三度量口径交错（交叉 Iter24/§3.3.1）。
- **I49-04（高）**：两 Peak 遇 ω=∞ 发散，缺 ⊤ 饱和（交叉 Iter35/45/18）。
- **I49-05（中）**：MA-004「已解决」仅用「派生度量」回避，两 Peak 矛盾+三度量口径仍在，收敛不实（交叉 Iter17/19）。

---

一句话摘要：§3.2.5 Peak 用 count、§3.3.2 peak 用 Σsize，同符号两 Peak 量纲不同（I49-01，高）、scope 过滤域不同（I49-02）、peak/net mode 口径交错（I49-03，交叉 Iter24）、遇 ω=∞ 均发散（I49-04，高，交叉 Iter35/45）、MA-004「已解决」仅回避（I49-05，交叉 Iter17/19）——需 PDR 侧统一为 peak_count/peak_size 双派生+同 scope 窗口+同 mode 过滤+⊤ 饱和。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter49.md，未读/改其它 audit 文件，聚焦两 Peak 定义统一，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #49（hy3 单独进程，本轮重跑）」、AA1-AA6 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I49- 缺口列表；交叉引用真实行号(L154/L167/L163/L152-154/L183) 并经 read 确认 §3.2.5/§3.3.1-2/§3.4 MA-004 真实文本"}
  ],
  "changedFiles": ["audit/iter49.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 143, 25) + (offset 163, 8) + (offset 173, 14)", "result": "passed", "summary": "读取 §3.2.5/§3.3.1-2/§3.4 MA-004 确认两 Peak 与 net 口径"},
    {"command": "read PDR (offset 103, 12) + (offset 152, 3)", "result": "passed", "summary": "读取 §3.1.3 ScopeId 与 §3.2.5 ω=∞ 确认 scope 窗口与发散"},
    {"command": "write D:/Godot/Cosmos/audit/iter49.md", "result": "passed", "summary": "覆盖写入独立审计 #49"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 AA1-AA6 六节 + Proof Obligation 账本(AA5 项) + 5 条 I49- 缺口", "交叉引用 §3.2.5/§3.3.1-2/§3.4 MA-004/Iter17/34/35/45/18/24/14/47/19 真实行号"],
  "residualRisks": ["未运行数值验证 count vs Σsize 差异（仅基于 L154/L167 文本推导）", "⊤ 饱和依赖 Iter35/45 未本轮定义"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter49.md，独立审计两个 Peak 定义统一（收口 Iter17）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现两 Peak 量纲/scope/mode 口径矛盾、ω=∞ 发散，需 PDR 侧统一 peak_count/peak_size+⊤ 饱和"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
