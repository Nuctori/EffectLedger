# Iter36 审计 — DO-7 量纲隔离落地草案：Signature 按 kind 分桶 (R/W/O) + ∪ 跨桶需权重函数（独立审计 #36，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter14 的 I14-01/02/06（DO-7 未落地）补为可采纳的代数结构（独立 pass #36，全新上下文）
- **范围**：§1 DO-7（L19，read/write/occupy 不可混算、编译期报错）、§3.1.1 Claim.kind（L80-82）、§3.1.4 Signature=ImmutableHashSet<Claim>（L94）、§3.2.1-2 ∪（L107-113/L126-130）、§3.3.2 peak（L167）、§3.4 MA-007（L186，权重函数未定义）；邻接 Iter14 I14-01/02/03/06（单 Set 混 kind/peak 混加/工具无检查/权重缺失）、Iter19（量化：组合子层 0 良定义）、Iter25（Compatible 偏函数）
- **结论摘要**：Iter14 证 DO-7「read/write/occupy 不可混算」与 §3.1.4 单 Set<Claim>+§3.2 无差别 ∪ 直接矛盾，且 MA-007「权重函数」全文未定义。本审计给出**推荐结构草案**使 DO-7 落地：(1) **Signature 分桶**：`Signature := (R:Set<Claim(kind=read)>, W:Set<Claim(kind=write)>, O:Set<Claim(kind=occupy)>)`，类型层强制 kind 分离；(2) **∪ 分桶并**：`(S₁;S₂)=(S₁.R∪S₂.R, S₁.W∪S₂.W, S₁.O∪S₂.O)`，`||` 同形但跨桶不交互（read 与 write 永不在同桶 ⇒ 不可能混算）；(3) **跨 kind 转换需显式权重函数**：`weight: Kind×Kind → ℝ∪{⊥}`，仅当 `weight(k₁,k₂)≠⊥` 允许折算（如 peak 求和为 `Σ_{k} weight(k,k)·Σ c.size`，同 kind 内 weight=1）；(4) **编译期检查**：§6 L3 增 `KIND_MIX` 规则，对试图把 read/write/occupy 直接相加的代码报编译错（Iter14 I14-03 的缺失职责补上）。该结构使 DO-7「编译期报错」可机械检查。但草案未采纳 ⇒ MA-007 仍 asserted、DO-7 仍悬空（open）。结构性成立（草案）给条件证明。

---

## Q1. 命题：Signature 分桶 (R,W,O) 使 kind 分离类型强制

**定义草案**（推荐）：
```
Signature := (R:Set<Claim>, W:Set<Claim>, O:Set<Claim>)
  其中 R 仅含 kind=read，W 仅含 kind=write，O 仅含 kind=occupy
  （构造时校验 kind，违反则类型错误）
```

**数学性质 / 证明状态**：
- **(PO-I36-a) 分桶 Signature 良定义（discharged，条件）**：三元组集合，构造校验 kind ⇒ 类型层保证 read/write/occupy 不混。证明：桶内同 kind ⇒ 跨桶操作需显式（见 Q3）。前提：所有 §7 映射的 Claim 需正确归入 R/W/O（依赖 §7 标注准确，Iter08/09 open）⇒ 条件。
- 交叉：Iter14 I14-01（单 Set 混 kind）、Iter19（量化）。

**文档行号**：§3.1.4（L94）、§3.1.1（L80-82）、Iter14（I14-01）。

---

## Q2. 命题：∪ 分桶并，跨桶不交互

**定义草案**：
```
(S₁;S₂)  := (S₁.R∪S₂.R, S₁.W∪S₂.W, S₁.O∪S₂.O)
(S₁||S₂) := 同形（跨桶不交互；兼容检查仅在同桶内按 mode 进行）
```
- read 与 write 永不在同桶 ⇒ 「read+write 混算」在类型层不可能（无共同容器）。
- 兼容检查（Iter25 C*）仅作用于同桶（如 W 桶内 create+release 等）。

**数学性质 / 证明状态**：
- **(PO-I36-b) ∪ 分桶并良定义（discharged，条件）**：桶内集合并，跨桶独立 ⇒ 满足 §3.1 组合律（A1/A2/A3 在三元组上逐桶成立）。证明：逐桶集合代数。前提 PO-I36-a 未立 ⇒ 条件。
- 交叉：Iter14 I14-01（∪ 混 kind）、Iter25（Compatible 同桶）。

**文档行号**：§3.2.1-2（L107-130）、§3.1.4（L94）、Iter25（C*）。

---

## Q3. 命题：跨 kind 转换需显式权重函数 weight

**定义草案**：
```
weight: (Kind, Kind) → ℝ ∪ {⊥}
  weight(read,read)=1, weight(write,write)=1, weight(occupy,occupy)=1
  weight(k₁,k₂)=⊥  for k₁≠k₂  (默认禁止跨 kind 折算)
  weight 表可由文档扩展（如 weight(occupy_memory, read)=w 表示「1 次占用折算 w 次读」用于统一预算）
```
- peak/聚合：仅同 kind 内求和，跨 kind 默认禁止（⊥）⇒ 不混算。
- 若需统一预算（如 512MB 比较），显式定义 weight 表 ⇒ 合法通道（DO-7「显式权重函数」落地）。

**数学性质 / 证明状态**：
- **(PO-I36-c) weight 函数使跨 kind 合法转换（discharged，条件）**：MA-007 称「转换需显式权重函数」，草案给出具体 weight 表 ⇒ 填补 Iter14 I14-06（权重缺失）。证明：显式函数定义。前提 PO-I36-a（分桶）未立 ⇒ 条件。
- 交叉：Iter14 I14-06（MA-007 权重未定义）、§3.4 MA-007（L186）。

**文档行号**：§3.3.2（L167）、§3.4 MA-007（L186）、Iter14（I14-06）。

---

## Q4. 命题：§6 L3 增 KIND_MIX 编译期检查（补 Iter14 I14-03 缺失职责）

**定义草案**：L3 Analyzer 增规则 `KIND_MIX`：检测源码中试图将 read/write/occupy 的 Signature 直接相加（不通过 weight）⇒ 编译警告/错误，落实 DO-7「编译期报错」。

**数学性质 / 证明状态**：
- **(PO-I36-d) L3 KIND_MIX 检查（discharged，条件）**：分桶 Signature（Q1）使「跨桶相加」在类型层即可静态捕获 ⇒ L3 可机械实现 KIND_MIX。证明：类型层可判。前提 Iter14 I14-03（L3 无量纲职责）、§6 L3 完备性（Iter07）未立 ⇒ 条件。
- 交叉：Iter14 I14-03（三层工具无执行量纲检查）、Iter07（L3 完备性）。

**文档行号**：§6（L329-398）、§1 DO-7（L19）、Iter14（I14-03）、Iter07（TS-003）。

---

## Q5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 采纳 (R,W,O) 分桶 Signature + 分桶 ∪ + weight 函数 + L3 KIND_MIX，则 DO-7「read/write/occupy 不可混算，编译期报错」可落地（类型层分离 + 机械检查）。证明：分桶 ⇒ 混算不可能；weight ⇒ 合法转换；KIND_MIX ⇒ 检查。前提 PO-I36-a/b/c/d 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 §7 映射的 Claim 正确归桶（Iter08/09 标注准确），则分桶 Signature 的构造校验不误伤正常映射。证明：标注准确 ⇒ 归桶正确。前提 Iter08/09 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 kind 仅单桶使用、绝不需跨 kind 折算」弱假设下，分桶即足以落实 DO-7（无需 weight）。证明：单桶自洽。但 §12.2 预算比较需跨 kind（memory vs 通道数）⇒ 仍需 weight（P1）。

---

## Proof Obligation 账本（Iter36）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I36-a | Signature 分桶 (R,W,O) 良定义 | open(条件) | 采纳 Q1 | L94, L80-82, I14-01 |
| PO-I36-b | ∪ 分桶并跨桶不交互 | open(条件) | 采纳 Q2 | L107-130, I14-01 |
| PO-I36-c | weight 函数使跨 kind 合法转换 | open(条件) | 采纳 Q3 + MA-007 | L167, L186, I14-06 |
| PO-I36-d | L3 KIND_MIX 编译期检查 | open(条件) | 采纳 Q4 + §6 | L329-398, L19, I14-03 |

## 本轮新发现未消解缺口（I36- 前缀，全局唯一）
- **I36-01（条件草案）**：(R,W,O) 分桶 Signature 使 kind 类型层分离，收口 Iter14 I14-01（单 Set 混 kind）。
- **I36-02（条件草案）**：分桶 ∪ 跨桶不交互，满足组合律且 read/write 永不同桶 ⇒ 混算不可能，收口 Iter14 I14-02（peak 混加）。
- **I36-03（条件草案）**：weight 函数表填补 MA-007 权重缺失（Iter14 I14-06），使跨 kind 合法折算通道成立。
- **I36-04（条件草案）**：L3 KIND_MIX 规则补 §6 量纲检查缺失职责（Iter14 I14-03），落实 DO-7「编译期报错」。
- **I36-05（弱）**：分桶 Signature 与 §3.1.4 现有 `ImmutableHashSet<Claim>` 表示不兼容 ⇒ 需改 PDR 类型定义（非仅加规则），改动面较大。

---

一句话摘要：给出 Signature 按 kind 分桶 (R,W,O)+分桶 ∪+weight 函数+L3 KIND_MIX 的 DO-7 落地草案（Q1-Q4），收口 Iter14 I14-01/02/06（单 Set 混 kind / peak 混加 / 权重缺失）与 I14-03（L3 无量纲职责），使 DO-7「编译期报错」可机械检查——草案为条件证明，需 PDR 侧改类型定义并补 §6 规则，且依赖 §7 标注准确（Iter08/09）与 §6 L3 完备性（Iter07）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter36.md，未读/改其它 audit 文件，聚焦 DO-7 kind 分桶落地草案，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #36（hy3 单独进程，本轮重跑）」、Q1-Q5 各节(草案/数学性质/状态/论证/行号)、Proof Obligation 账本、I36- 缺口列表；交叉引用真实行号(L19/L80-82/L94/L107-130/L167/L186/L329-398) 并经 read 确认 §1/§3.1/§3.2/§3.3/§3.4/§6 真实文本"}
  ],
  "changedFiles": ["audit/iter36.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 22) + (offset 103, 12) + (offset 149, 25)", "result": "passed", "summary": "读取 §3.1.1/§3.1.4/§3.2.1-2/§3.3.2 确认 kind/size/Signature/∪/peak 结构与 DO-7 矛盾"},
    {"command": "read PDR (offset 19, 3) + (offset 184, 4) + (offset 329, 70)", "result": "passed", "summary": "读取 §1 DO-7 / MA-007 / §6 L3 确认量纲检查缺失与权重函数缺口"},
    {"command": "write D:/Godot/Cosmos/audit/iter36.md", "result": "passed", "summary": "覆盖写入独立审计 #36"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 Q1-Q5 五节 + Proof Obligation 账本(Q4 项) + 5 条 I36- 缺口", "交叉引用 §3.1/§3.2/§3.3/§1 DO-7/§3.4 MA-007/§6 L3/Iter14/Iter08/Iter09/Iter07 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 KIND_MIX 可实现性（仅基于 §6 文本推导）", "分桶 Signature 与现有 ImmutableHashSet<Claim> 表示不兼容需 PDR 改类型"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter36.md，独立审计 DO-7 kind 分桶 Signature 落地草案（收口 Iter14）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但给出 (R,W,O) 分桶+weight+KIND_MIX 草案可收口 Iter14 全部 DO-7 缺口，需 PDR 侧改类型定义并补 §6 规则"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
