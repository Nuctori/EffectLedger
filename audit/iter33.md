# Iter33 审计 — Deviation 公式 `range=max-min` 作分母遇 size 单点退化 ⇒ 除零/NaN 与校准失效（独立审计 #33，hy3 单独进程，本轮重跑）

- **审计视角**：§9.1 校准公式的良定义边界（独立 pass #33，全新上下文）
- **范围**：§9.1 CalculateDeviation（L549-561，`expectedᵢ_range = max - min` 作分母）、§3.1.1 size∈Nat?（L78-86，默认 1 单值）、§3.2.4 ⊔ 区间 size（L143-147，[min,max]）、§9.3 校准循环（L577-589）、RT-002（L595）；邻接 Iter11 I11-01/02/03（除零/NaN/区间载体/Σ 对齐）、Iter02 I2-04（size 单值无区间）、Iter18（size=∞ 发散）
- **结论摘要**：Deviation 公式 `Deviation = Σᵢ |actualᵢ - expectedᵢ_mid| / expectedᵢ_range`，`expectedᵢ_range = max - min`。当某 Claim 的 size 区间退化单点（min=max，常见于 §3.1.1 `size` 缺省=1 或编译期常量，或 ⊔ 合并后单点），`expectedᵢ_range = 0` ⇒ 该项 `|·|/0`。C# 浮点除法 `x/0` 得 `+∞` 或 `NaN`（非抛异常）⇒ 该 Claim 的 deviation 项为 ∞/NaN，参与 Σ 使整体 Deviation 为 ∞ 或 NaN。而 §9.1 报警条件 `if (deviation > 0.2f)` 对 `NaN` 恒假（IEEE 754：NaN 与任何值比较为假）、对 `+∞` 恒真（+∞>0.2）⇒ **NaN 情形报警永不触发、+∞ 情形永远报警**——校准循环（§9.3）对该对象完全失效（open，高，运行期）。叠加：(1) 公式载体假设 Claim 带 [min,max] 区间，但 §3.1.1 `size` 是单值、§3.1.4 Signature 未声明区间 ⇒ 区间语义无出处（Iter02 I2-04）；(2) Σ 的 `i` 对齐谓词（expected/actual 如何配对）未定义（Iter11 I11-03）。结构性成立给条件证明（补 range 下界/区间良定义/Σ 对齐）。

---

## N1. 命题：range=0 致除零/NaN，Deviation 未定义

**命题**（§9.1 L549-551）：`expectedᵢ_range = max - min`，分母。
**命题**（§3.1.1 L82）：`size ∈ Nat?` 单值，默认 1。

退化情形：某 Claim `size` 为单点（缺省 1 或常量）⇒ 其 `min=max=size` ⇒ `expectedᵢ_range = 0`。
- `|actualᵢ - expectedᵢ_mid| / 0`：C# `double` 除法 `非零/0` ⇒ `+∞`；`0/0` ⇒ `NaN`。
- Σ 含该项 ⇒ 整体 `Deviation = +∞` 或 `NaN`。

**数学性质 / 证明状态**：
- **(PO-I33-a) range=0 致 Deviation 非有限（open，高，运行期）**：Deviation 定义为「偏差百分比」，应 ∈ [0,∞) 且有限；range=0 使其越界至 ∞/NaN ⇒ 公式在该 Claim 上未定义。状态 = open（高）。
- 交叉：Iter11 I11-01（同 root）、Iter02 I2-04（size 单值）。

**文档行号**：§9.1（L549-551）、§3.1.1（L78-86）、Iter11（I11-01）、Iter02（I2-04）。

---

## N2. 命题：NaN 使报警恒假、+∞ 使报警恒真 ⇒ 校准闭环破

**命题**（§9.1 L547）：`if (deviation > 0.2f) GD.PushWarning(...)`。
**命题**（IEEE 754）：`NaN > 0.2f` 为 false；`+∞ > 0.2f` 为 true。

后果：
- 若 Deviation=NaN：报警条件恒假 ⇒ 该对象**永不报警** ⇒ 校准循环（§9.3）对其完全失效（即使 actual 严重偏离 expected，NaN 掩盖）。
- 若 Deviation=+∞：报警条件恒真 ⇒ 该对象**每 60 帧报警** ⇒ 噪声淹没真实偏差信号（R-3 误报加剧）。

两种情形都使 §9.1 的「偏差>20% 才报警」语义崩溃——不是「阈值不精确」，而是**公式本身对非区间 size 无意义**。

**数学性质 / 证明状态**：
- **(PO-I33-b) NaN/∞ 破坏报警语义（open，高，运行期）**：`deviation > 0.2f` 对 NaN/∞ 行为极端（恒假/恒真）⇒ 校准闭环双失效。状态 = open（高）。
- 交叉：Iter11 I11-01（RT-002 不实）、§9.3（L577-589 校准循环）。

**文档行号**：§9.1（L547-551）、§9.3（L577-589）、Iter11（I11-01）。

---

## N3. 命题：区间载体无出处（size 单值 vs [min,max]）

**命题**（§3.1.1 L78-86）：`size ∈ Nat?` 单值（非区间）。
**命题**（§3.2.4 L143-147）：`⊔` 输出「区间 [min,max]」size——但 §3.1.1 的 size 是单值，二者冲突（Iter02 I2-04、Iter46）。
**命题**（§9.1 L549）：公式直接用 `min/max`——假设 Claim 带区间 size。

冲突：Deviation 公式的 `min/max` 从哪来？若来自 ⊔（L143-147），则与 §3.1.1 单值载体冲突；若来自 §3.1.1 单值，则 min=max ⇒ 必退化（N1）。即公式的**区间载体无合法出处** ⇒ Deviation 的输入对象不确定。

**数学性质 / 证明状态**：
- **(PO-I33-c) 区间载体无出处（open，高）**：Deviation 假设 claim 带 [min,max]，但 Signature 的 size 是单值 ⇒ 公式对象不确定。状态 = open（高，交叉 Iter02 I2-04、Iter46）。
- 文档行号：§9.1（L549-551）、§3.1.1（L78-86）、§3.2.4（L143-147）、Iter46（PO-I46）。

---

## N4. 命题：Σ 对齐谓词缺失

**命题**（§9.1 L549）：`Σᵢ |actualᵢ - expectedᵢ_mid|`——expected/actual 均为 Signature（Set<Claim>），无序。
**命题**：Σ 的索引 `i` 按何配对 expected 与 actual 的 Claim？按 Claim 相等（Iter32 PO-I32-a，未定义）？按 resource？按 resource+mode？

若 expected 多/actual 少（或反之）的项如何处理未定义（忽略？计为无限偏差？）。配对歧义使 Deviation 数值不确定。

**数学性质 / 证明状态**：
- **(PO-I33-d) Σ 对齐未定义（open，高）**：集合求和无序 ⇒ 需配对谓词，文档未给 ⇒ Deviation 求和对象不确定。状态 = open（高，交叉 Iter11 I11-03）。
- 文档行号：§9.1（L549-551）、§3.1.4（L94）、Iter32（PO-I32-a）。

---

## N5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (a) 定义 `expectedᵢ_range` 下界 `range_guard = max(range, ε)`（ε>0，退化时分母=ε 而非 0），(b) 区间载体统一（Iter46 把 size 提升为 extended-区间或 §3.1.1 显式 [min,max]），(c) Σ 对齐用 Claim 相等（Iter32），则 Deviation 公式对所有 Claim 良定义（有限实数）。证明：分母下界+载体确定+对齐。前提 PO-I33-a/c/d 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 Deviation 结果显式处理 `NaN/∞`（`if (!double.IsFinite(deviation))` 走特殊分支而非 `>0.2f`），则报警语义不崩溃。证明：有限性检查。前提 PO-I33-b 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 size 为严格区间 min<max 且 expected/actual 按 Claim 相等对齐」理想假设下，Deviation 良定义。证明：假设排除退化。但 §3.1.1 默认 size=1 单点 ⇒ 假设不成立（N3）。

---

## Proof Obligation 账本（Iter33）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I33-a | range=0 致 Deviation 非有限(除零/NaN) | open(高) | range 下界 ε | L549-551, L78-86 |
| PO-I33-b | NaN/∞ 破坏报警语义(校准闭环破) | open(高) | IsFinite 分支 | L547, L549-551 |
| PO-I33-c | 区间载体无出处(size 单值 vs [min,max]) | open(高) | 见 Iter46 size 提升 | L549, L78-86, L143-147 |
| PO-I33-d | Σ 对齐谓词缺失 | open(高) | Claim 相等(Iter32) | L549-551, L94 |

## 本轮新发现未消解缺口（I33- 前缀，全局唯一）
- **I33-01（高）**：Deviation 公式 range=max-min 作分母，size 单点退化(range=0)⇒除零/NaN，Deviation 越界非有限（交叉 Iter11 I11-01）。
- **I33-02（高，运行期）**：`deviation>0.2f` 对 NaN 恒假（永不报警）、对 ∞ 恒真（每帧报警）⇒ 校准闭环双失效（交叉 Iter11 RT-002 不实）。
- **I33-03（高）**：公式假设 [min,max] 区间，但 §3.1.1 size 单值、§3.3.1/§3.3.2 用单值 ⇒ 区间载体无出处（交叉 Iter02 I2-04 / Iter46）。
- **I33-04（高）**：Σ 索引对齐（expected/actual 配对）未定义 ⇒ Deviation 数值不确定（交叉 Iter11 I11-03 / Iter32）。
- **I33-05（弱）**：size=∞（Iter18）使 range=[·,⊤] 亦退化（max=⊤ 无上界）⇒ 即便补区间载体，∞ 仍使 range 非有限，Deviation 需 Iter18 的 ⊤ 闭包兜底。

---

一句话摘要：Deviation 公式 range=max-min 作分母遇 size 单点退化(range=0)⇒除零/NaN、Deviation 越界非有限（I33-01，高，交叉 Iter11），NaN 使报警恒假/∞ 使恒真致校准闭环双失效（I33-02，高运行期，交叉 RT-002），区间载体无出处(size 单值 vs [min,max]，I33-03，交叉 Iter02/Iter46)，Σ 对齐未定义（I33-04，交叉 Iter11/Iter32）——需 PDR 侧补 range 下界+IsFinite 分支+size 区间载体+Claim 相等对齐。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter33.md，未读/改其它 audit 文件，聚焦 Deviation 除零，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #33（hy3 单独进程，本轮重跑）」、N1-N5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I33- 缺口列表；交叉引用真实行号(L547-561/L78-86/L143-147/L577-589) 并经 read 确认 §9.1/§9.3/§3.1.1 真实文本"}
  ],
  "changedFiles": ["audit/iter33.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 537, 25)", "result": "passed", "summary": "读取 §9.1 EffectValidator/CalculateDeviation 真实公式与报警条件"},
    {"command": "read PDR (offset 78, 10) + (offset 143, 6)", "result": "passed", "summary": "读取 §3.1.1 size 单值 与 §3.2.4 ⊔ 区间 size 确认载体冲突"},
    {"command": "write D:/Godot/Cosmos/audit/iter33.md", "result": "passed", "summary": "覆盖写入独立审计 #33"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 N1-N5 五节 + Proof Obligation 账本(N4 项) + 5 条 I33- 缺口", "交叉引用 §9.1/§9.3/§3.1.1/§3.2.4/Iter11/Iter02/Iter46/Iter18/RT-002 真实行号"],
  "residualRisks": ["未运行 C# 源码验证 x/0 行为（仅基于 IEEE 754 与 C# double 语义推导）", "size 区间载体依赖 Iter46 未本轮重证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter33.md，独立审计 Deviation range=0 除零/NaN 与校准失效",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 Deviation 公式在非区间 size 下未定义、报警语义崩溃，需 PDR 侧补 range 下界+IsFinite+区间载体"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
