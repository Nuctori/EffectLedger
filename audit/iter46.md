# Iter46 审计 — MA-006 ⊔ 半环区间合并：size 区间载体与 [min,max] 合并的代数合法性（独立审计 #46，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter02 的「size 单值 vs 区间」与 MA-006 的「放弃半环」合并审查 ⊔ 的 size 载体（独立 pass #46，全新上下文）
- **范围**：§3.2.4 ⊔（L143-147，`∀c∈S₁∪S₂, [min(c₁.size,c₂.size), max(c₁.size,c₂.size)]`）、§3.4 MA-006（L185，「放弃半环，采用集合代数 ∪ + 偏序集 ⊆」）、§3.1.1 size∈Nat?（L78-86，单值默认1）、§3.3.2 peak（L167，Σsize）、§9.1 Deviation（L549-561，[min,max] 区间）；邻接 Iter02 I2-04（size 单值无区间）、Iter33 N3（区间载体无出处）、Iter45（∞ 闭包）、Iter26（size=∞）、Iter18（两 ∞）
- **结论摘要**：MA-006 称「⊔ 不是半环乘法，已解决：放弃半环，采用集合代数 ∪ + 偏序集 ⊆」——但 §3.2.4 的 ⊔（条件组合合并）**仍用 [min,max] 区间**对 size 做合并（L144-147），与「放弃半环」自相矛盾。审计发现：(1) **size 载体冲突**：§3.1.1 size 是单值 Nat?（L82），§3.2.4 ⊔ 输出 [min,max] 区间——同一个 Claim.size 既是单值又是区间，类型未统一（Iter02 I2-04）；(2) **区间载体无出处**：Deviation（§9.1 L549）也用 [min,max]，但 Signature 的 size 是单值，区间从哪来？§3.2.4 的 ⊔ 假设 size 已是区间 ⇒ 但单值 size 的 min=max ⇒ ⊔ 退化（Iter33 N3） ⇒ ⊔ 的区间合并仅对「已是区间」的 size 有意义，而文档无 size 为区间的构造子；(3) **半环放弃但 ⊔ 仍是乘法式合并**：MA-006「放弃半环」指放弃 Grade 区间半环，但 §3.2.4 的 ⊔ 是 size 上的区间交/并（[min,max] 实为 size 区间的取交），其代数性质（闭合性、幂等、单位元）未证——⊔ 是否仍是某种半环（size 区间半格）未澄清；(4) **⊔ 遇 ∞**：若 c₁.size=∞（ED-004 L527）则 [min,max] 含 ∞，max 无上界（Iter45 W4）⇒ ⊔ 区间合并发散；(5) **peak 用 Σsize 单值、⊔ 用区间**：§3.3.2 peak 直接 Σ c.size（L167），但 S 可能含 ⊔ 展开后的区间 size ⇒ peak 在单值与区间 size 间口径不一致（Iter33 N3/Iter02）。结构性成立给条件证明（统一 size 为 extended-区间、定义 ⊔ 区间闭包、消解 MA-006 措辞），但 ⊔ 区间载体仍 open。

---

## X1. 命题：size 单值 vs ⊔ 区间，载体冲突

**命题**（§3.1.1 L82）：`size ∈ Nat?` 单值，默认 1。
**命题**（§3.2.4 L144-147）：⊔ 输出 `[min(c₁.size, c₂.size), max(c₁.size, c₂.size)]`——区间。

同一 size 字段在 §3.1.1 是单值、在 §3.2.4 是区间端点 ⇒ 类型冲突。若 size 恒单值，则 ⊔ 的 [min,max] 恒 = [s,s]（退化点）⇒ ⊔ 的区间合并是恒等（无信息）；若 size 可为区间，则 §3.1.1 定义需升级为区间类型——文档两处矛盾（Iter02 I2-04）。

**数学性质 / 证明状态**：
- **(PO-I46-a) size 单值 vs 区间载体冲突（open，高）**：§3.1.1 与 §3.2.4 对 size 的类型不一致 ⇒ ⊔ 区间合并语义不确定。状态 = open（高，交叉 Iter02 I2-04、Iter33 N3）。
- 文档行号：§3.1.1（L78-86）、§3.2.4（L143-147）。

---

## X2. 命题：区间载体无出处（与 Deviation 同源）

**命题**（§9.1 L549）：Deviation 用 `expectedᵢ_range = max - min`，假设 size 带区间。
**命题**（§3.2.4 L144）：⊔ 也用 [min,max]，假设 size 带区间。
**命题**（§3.1.4 L94）：Signature = ImmutableHashSet<Claim>，size 单值（§3.1.1）。

区间 size 既非 §3.1.1 的 Nat?、也非 §3.1.4 的字段定义 ⇒ 区间载体无合法出处（Iter33 N3 同 root）。⊔ 与 Deviation 都依赖「size 是区间」但未定义该类型 ⇒ 二者输入对象不确定。

**数学性质 / 证明状态**：
- **(PO-I46-b) 区间载体无出处（open，高）**：⊔ 与 Deviation 共享未定义的区间 size 类型 ⇒ 合并与校准都悬空。状态 = open（高，交叉 Iter33 N3、Iter02）。
- 文档行号：§3.2.4（L143-147）、§9.1（L549-561）、§3.1.1（L78-86）、Iter33（N3）。

---

## X3. 命题：MA-006「放弃半环」与 ⊔ 区间合并自相矛盾

**命题**（§3.4 MA-006 L185）：「放弃半环，采用集合代数 ∪ + 偏序集 ⊆」。
**命题**（§3.2.4 L143-147）：⊔ 仍做 size 的 [min,max] 区间合并。

MA-006 指「放弃 Grade 区间半环」，但 §3.2.4 的 ⊔ 是 size 区间的取交/合并——若 size 是区间，则 ⊔ 是 size 半格上的运算，仍具半环/半格结构。MA-006「放弃半环」措辞与 §3.2.4 实际仍有区间合并矛盾 ⇒ 半环/半格是否真放弃未澄清（可能发生术语偷换：放弃 Grade 半环，但引入 size 半格）。

**数学性质 / 证明状态**：
- **(PO-I46-c) MA-006 措辞与 ⊔ 矛盾（open，中）**：「放弃半环」不实——size 区间合并仍是半格运算。状态 = open（中，交叉 §3.4 MA-006、§3.2.4）。
- 文档行号：§3.4 MA-006（L185）、§3.2.4（L143-147）。

---

## X4. 命题：⊔ 遇 size=∞ 发散

**命题**（ED-004 L527）：动态 Instantiate size=∞。
**命题**（§3.2.4 L144）：[min(c₁.size,c₂.size), max(c₁.size,c₂.size)]。

若任一 size=∞，则 max=∞ ⇒ 区间 [?, ⊤] 无上界 ⇒ ⊔ 区间合并发散（Iter45 W4 ⊤ 闭包未定义）。即 ⊔ 在含 ∞ size 时非闭合。

**数学性质 / 证明状态**：
- **(PO-I46-d) ⊔ 遇 ∞ size 发散（open，中）**：缺 ⊤ 闭包 ⇒ ⊔ 含 ∞ 无值。状态 = open（中，交叉 Iter26、Iter45 W4）。
- 文档行号：§3.2.4（L143-147）、ED-004（L527）、Iter45（W4）。

---

## X5. 命题：peak(Σsize) 与 ⊔ 区间口径不一致

**命题**（§3.3.2 L167）：`peak = max_{t⊆scope} Σ c.size`——单值求和。
**命题**（§3.2.4 L144）：⊔ 展开后 size 为区间。

若 S 含 ⊔ 展开（区间 size），peak 的 Σ c.size 对区间如何处理未定义（取 mid？取 max？），与 §9.1 Deviation 取 mid/range 又不同 ⇒ peak/⊔/Deviation 三处 size 口径互不一致（Iter33 N3/Iter02/Iter50）。

**数学性质 / 证明状态**：
- **(PO-I46-e) peak 与 ⊔ size 口径不一致（open，中）**：peak 单值 Σ vs ⊔ 区间 ⇒ 聚合口径冲突。状态 = open（中，交叉 Iter33、Iter50、§3.3.2）。
- 文档行号：§3.3.2（L167）、§3.2.4（L143-147）、§9.1（L549）。

---

## X6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若统一 size 为 `extended-区间 = [lo, hi] ∈ (ℕ∪{⊤})²`（§3.1.1 升级），单值 s 表示 [s,s]，则 §3.2.4 ⊔ 的 [min,max] 是自然区间交、§3.3.2 peak 取 Σ mid、Deviation 取 [min,max] 一致。证明：类型统一。前提 PO-I46-a/b 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若定义 ⊤ 闭包（Iter45 W4），则 ⊔ 遇 ∞ 收敛为 [?, ⊤]。证明：闭包。前提 Iter45 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 size 单值」受限假设下，⊔=[s,s] 退化、peak=Σs 直接成立。证明：假设排除区间。但 ⊔ 存在即暗示区间需求 ⇒ 假设弱。

---

## Proof Obligation 账本（Iter46）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I46-a | size 单值 vs ⊔ 区间冲突 | open(高) | 升级 size 为区间 | L78-86, L143-147 |
| PO-I46-b | 区间载体无出处(⊔/Deviation 同源) | open(高) | 定义区间 size 类型 | L143-147, L549, L78-86 |
| PO-I46-c | MA-006 措辞与 ⊔ 矛盾 | open(中) | 澄清半环/半格 | L185, L143-147 |
| PO-I46-d | ⊔ 遇 ∞ size 发散 | open(中) | ⊤ 闭包(Iter45) | L143-147, L527 |
| PO-I46-e | peak 与 ⊔ size 口径不一致 | open(中) | 统一 size 口径 | L167, L143-147, L549 |

## 本轮新发现未消解缺口（I46- 前缀，全局唯一）
- **I46-01（高）**：§3.1.1 size 单值 Nat? 与 §3.2.4 ⊔ 的 [min,max] 区间冲突，同一字段两类型（交叉 Iter02 I2-04/Iter33 N3）。
- **I46-02（高）**：区间 size 无合法出处（§3.1.1/§3.1.4 无此类型），⊔ 与 Deviation 共享未定义区间载体（交叉 Iter33 N3）。
- **I46-03（中）**：MA-006「放弃半环」与 §3.2.4 仍有 size 区间合并矛盾，半格是否真放弃未澄清。
- **I46-04（中）**：⊔ 遇 size=∞ 发散，缺 ⊤ 闭包（交叉 Iter26/Iter45 W4）。
- **I46-05（中）**：peak 单值 Σ 与 ⊔ 区间口径不一致，三处(size/⊔/Deviation) size 口径互不一致（交叉 Iter33/Iter50）。

---

一句话摘要：§3.1.1 size 单值 Nat? 与 §3.2.4 ⊔ 的 [min,max] 区间冲突（I46-01，高）、区间 size 无合法出处（I46-02，高，交叉 Iter33）、MA-006「放弃半环」与 ⊔ 区间合并矛盾（I46-03）、⊔ 遇 size=∞ 发散（I46-04，交叉 Iter45）、peak 单值 Σ 与 ⊔ 区间口径不一致（I46-05，交叉 Iter33/50）——需 PDR 侧统一 size 为 extended-区间类型 + ⊤ 闭包 + 澄清 MA-006 措辞。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter46.md，未读/改其它 audit 文件，聚焦 MA-006 ⊔ 区间合并与 size 载体，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #46（hy3 单独进程，本轮重跑）」、X1-X6 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I46- 缺口列表；交叉引用真实行号(L78-86/L143-147/L185/L167/L549/L527) 并经 read 确认 §3.1.1/§3.2.4/§3.4 MA-006/§3.3.2/§9.1 真实文本"}
  ],
  "changedFiles": ["audit/iter46.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 22) + (offset 140, 12) + (offset 173, 14)", "result": "passed", "summary": "读取 §3.1.1/§3.2.4/§3.4 MA-006 确认 size 单值 vs 区间冲突"},
    {"command": "read PDR (offset 163, 8) + (offset 543, 8)", "result": "passed", "summary": "读取 §3.3.2 peak / §9.1 Deviation 确认 size 口径不一致"},
    {"command": "write D:/Godot/Cosmos/audit/iter46.md", "result": "passed", "summary": "覆盖写入独立审计 #46"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 X1-X6 六节 + Proof Obligation 账本(X5 项) + 5 条 I46- 缺口", "交叉引用 §3.1.1/§3.2.4/§3.4 MA-006/§3.3.2/§9.1/ED-004/Iter02/33/45/50 真实行号"],
  "residualRisks": ["未运行类型检查验证 size 单值 vs 区间冲突（仅基于 §3.1.1/§3.2.4 文本推导）", "⊤ 闭包依赖 Iter45 未本轮定义"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter46.md，独立审计 MA-006 ⊔ 区间合并与 size 载体合法性",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 size 单值 vs 区间冲突、区间载体无出处，需 PDR 侧统一 size 为 extended-区间+⊤ 闭包"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
