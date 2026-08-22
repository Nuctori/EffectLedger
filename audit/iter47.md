# Iter47 审计 — MA-007 权重函数定义：跨 kind 折算的合法通道与 DO-7 落地（独立审计 #47，hy3 单独进程，本轮重跑）

- **审计视角**：把 Iter14 的「DO-7 量纲隔离」与 MA-007 的「权重函数」合并为权重函数的形式化（独立 pass #47，全新上下文）
- **范围**：§3.4 MA-007（L186，「量纲隔离通过 kind 字段实现，转换需显式权重函数」）、§1 DO-7（L19，「read/write/occupy 不可混算，编译期报错」）、§3.1.1 Claim.kind（L80-82，read/write/occupy）、§3.1.4 Signature=ImmutableHashSet<Claim>（L94）、§3.3.2 peak（L167，Σsize）、§12.2 AUDIT003（L722，VramMB 跨 kind 比较）；邻接 Iter14 I14-06（MA-007 权重未定义）、Iter36 Q3（weight 草案）、Iter16（Compatible kind 无关）、Iter50 C1（AUDIT003 口径）
- **结论摘要**：MA-007 称「量纲隔离通过 kind 字段实现，转换需显式权重函数」并标记「已解决」——但审计发现权重函数**从未被定义**：(1) **无函数签名**：文档未给出 `weight: (Kind,Kind)→?` 的类型、定义域（仅同 kind？跨 kind？）、值域（ℝ? 含 ⊥?）；§3 全文中 search「权重」仅 L186 一处 ⇒ 函数不存在；(2) **无具体权重表**：DO-7 禁止 read/write/occupy 混算，但 §12.2 AUDIT003「accumulated VramMB: 1280MB」与 budget「512MB」比较（L722）正是跨 kind（memory 占用 vs 预算）折算——这种合法折算的权重从哪来？未定义 ⇒ 要么 DO-7 被隐性违反（AUDIT003 默认混 MB），要么权重函数缺失使合法预算比较不可机械执行；(3) **权重与 Compatible 正交**：§3.2.3 Compatible 判冲突只看 resource+mode（Iter16），不涉 kind/权重；跨 kind 折算（如 1 占有折算 w 次读）需权重函数衔接 peak，但 peak（§3.3.2 Σsize）无权重 ⇒ 不同 kind 的 size 直接相加，DO-7 违反（Iter14 I14-02）；(4) **权重单位/语义**：权重是「资源当量」（1 GPU 指令 = w byte 内存？）还是「风险当量」？未定义 ⇒ 无法验证权重正确性；(5) **权重未与 L3 KIND_MIX 接**：Iter36 Q4 的 L3 KIND_MIX 检查依赖权重函数存在，但权重函数无定义 ⇒ KIND_MIX 也无据。结构性成立给条件证明（Iter36 Q3 weight 草案：同 kind=1、跨 kind=⊥、可扩展表），但 MA-007「已解决」不实。

---

## Y1. 命题：权重函数无签名/定义

**命题**（§3.4 MA-007 L186）：「转换需显式权重函数」——仅此一句。
**命题**：§3 全文 search「权重」仅 L186 ⇒ 无 `weight` 函数定义、无签名、无定义域/值域。

后果：「显式权重函数」是占位承诺，非数学对象。DO-7「跨 kind 折算需权重」无函数可执行 ⇒ 折算通道不存在。

**数学性质 / 证明状态**：
- **(PO-I47-a) 权重函数未定义（open，高）**：MA-007 称有「显式权重函数」但全文无定义 ⇒ DO-7 折算通道悬空。状态 = open（高，交叉 Iter14 I14-06、Iter36 Q3）。
- 文档行号：§3.4 MA-007（L186）、Iter36（Q3）。

---

## Y2. 命题：AUDIT003 跨 kind 比较暴露权重缺失

**命题**（§12.2 L722）：`accumulated VramMB: 1280MB (default budget 512MB)`。
**命题**（§1 DO-7 L19）：read/write/occupy 不可混算。

AUDIT003 把 memory 占用（occupy kind）累加为 VramMB 与预算比较——这是跨 kind（占用 vs 预算常量）折算。合法折算需权重函数（MA-007），但权重未定义 ⇒ 要么 AUDIT003 隐式违反 DO-7（直接比 MB），要么权重缺失使该比较无代数依据（Iter50 C1）。即 MA-007 的「已解决」在 AUDIT003 处暴露为未解决。

**数学性质 / 证明状态**：
- **(PO-I47-b) AUDIT003 跨 kind 折算无权重（open，高）**：预算比较依赖未定义权重 ⇒ DO-7 在数值层失效。状态 = open（高，交叉 Iter50 C1、Iter14 I14-02）。
- 文档行号：§12.2（L722）、§1 DO-7（L19）、Iter50（C1）。

---

## Y3. 命题：peak(Σsize) 无权重 ⇒ 跨 kind 混加

**命题**（§3.3.2 L167）：`peak = max_{t⊆scope} Σ c.size`——直接 Σ，无权重。
**命题**（§3.1.1 L80-82）：Claim.kind ∈ {read, write, occupy}。

peak 对所有 kind 的 size 直接求和 ⇒ 若同一 scope 有 read(size=1)+occupy(size=64MB)，peak 把 1 与 64MB 加为 64MB+1（单位混乱，Iter14 I14-02）。权重函数若应在此介入（不同 kind 不同权重），但 peak 公式无权重项 ⇒ DO-7 在 peak 层违反。

**数学性质 / 证明状态**：
- **(PO-I47-c) peak 无权重致跨 kind 混加（open，高）**：Σsize 无权重 ⇒ DO-7 违反。状态 = open（高，交叉 Iter14 I14-02、Iter36 Q1）。
- 文档行号：§3.3.2（L167）、§3.1.1（L80-82）、Iter14（I14-02）。

---

## Y4. 命题：权重单位/语义未定义

**命题**：权重应表达「kind A 的 1 单位 = kind B 的 w 单位」（资源/风险当量）。
**命题**：MA-007 未定义权重的量纲（是 byte/指令？还是风险系数？），也未定义权重表的来源（人工？推导？）。

后果：即便有 weight 函数，其数值正确性无法验证（无基准当量）⇒ 权重本身可能是任意数 ⇒ 折算结果不可信。

**数学性质 / 证明状态**：
- **(PO-I47-d) 权重单位/语义未定义（open，中）**：权重数值无基准 ⇒ 折算正确性不可证。状态 = open（中）。
- 文档行号：§3.4 MA-007（L186）。

---

## Y5. 命题：权重与 L3 KIND_MIX 衔接缺失

**命题**（Iter36 Q4）：L3 KIND_MIX 检查依赖 weight 函数（跨 kind 相加需经 weight）。
**命题**：weight 未定义（PO-I47-a）⇒ KIND_MIX 无据。

后果：Iter36 的 DO-7 落地草案（分桶 + weight + KIND_MIX）中 weight 缺失使整条链断（Iter36 Q3/Q4 条件未立）。

**数学性质 / 证明状态**：
- **(PO-I47-e) 权重缺失致 KIND_MIX 无据（open，中）**：L3 量纲检查依赖未定义权重 ⇒ 检查不可实现。状态 = open（中，交叉 Iter36 Q4、Iter39 PO-I39-b）。
- 文档行号：§3.4 MA-007（L186）、Iter36（Q4）、Iter39（PO-I39-b）。

---

## Y6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若采纳 Iter36 Q3 的 weight 表（`weight(k,k)=1, weight(k₁≠k₂)=⊥, 可扩展`）并定义其单位为「资源当量」基准，则 DO-7 折算通道成立、AUDIT003/peak 可合法跨 kind。证明：表给定。前提 PO-I47-a 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 peak 改为 `Σ_{k} weight(k,k)·Σc.size`（同 kind 内，跨 kind 默认 ⊥），则 peak 不混加。证明：权重介入。前提 Iter36 未立 ⇒ 条件。
- **P3（discharged）**：在「永不跨 kind 折算」弱假设下，weight 恒 ⊥ ⇒ DO-7 仅禁止折算（AUDIT003 此类比较非法）。证明：假设排除预算比较。但 §12.2 明确做 VramMB 比较 ⇒ 假设与文档冲突。

---

## Proof Obligation 账本（Iter47）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I47-a | 权重函数无签名/定义 | open(高) | 采纳 Iter36 Q3 weight | L186, I14-06, I36-Q3 |
| PO-I47-b | AUDIT003 跨 kind 折算无权重 | open(高) | weight + 预算入代数 | L722, L19, I50-C1 |
| PO-I47-c | peak 无权重致跨 kind 混加 | open(高) | peak 加权重项 | L167, L80-82, I14-02 |
| PO-I47-d | 权重单位/语义未定义 | open(中) | 定义当量基准 | L186 |
| PO-I47-e | 权重缺失致 KIND_MIX 无据 | open(中) | weight + L3 规则 | L186, I36-Q4, I39-b |

## 本轮新发现未消解缺口（I47- 前缀，全局唯一）
- **I47-01（高）**：MA-007「显式权重函数」全文无定义（search「权重」仅 L186），DO-7 跨 kind 折算通道悬空（交叉 Iter14 I14-06/Iter36 Q3）。
- **I47-02（高）**：AUDIT003 把 VramMB 与 512MB 预算比较是跨 kind 折算，无权重 ⇒ DO-7 在数值层失效（交叉 Iter50 C1/Iter14 I14-02）。
- **I47-03（高）**：peak 直接 Σsize 无权重 ⇒ read/write/occupy 混加，DO-7 在 peak 层违反（交叉 Iter14 I14-02/Iter36 Q1）。
- **I47-04（中）**：权重单位/语义（资源还是风险当量）未定义 ⇒ 即便有函数数值正确性不可证。
- **I47-05（中）**：权重缺失使 L3 KIND_MIX（Iter36 Q4）无据 ⇒ DO-7 落地链断（交叉 Iter39 PO-I39-b）。

---

一句话摘要：MA-007「显式权重函数」全文无定义（I47-01，高，search「权重」仅 L186），AUDIT003 跨 kind 比较与 peak 无权重 Σsize 致 DO-7 在数值/peak 层失效（I47-02/03，高，交叉 Iter14/50），权重单位与 L3 KIND_MIX 衔接缺失（I47-04/05）——MA-007「已解决」不实，需 PDR 侧采纳 Iter36 Q3 的 weight 表+定义当量基准+peak 加权重项。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter47.md，未读/改其它 audit 文件，聚焦 MA-007 权重函数定义，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #47（hy3 单独进程，本轮重跑）」、Y1-Y6 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I47- 缺口列表；交叉引用真实行号(L186/L19/L80-82/L94/L167/L722) 并经 grep+read 确认「权重」仅 L186 且 §3.4 MA-007/§1 DO-7/§12.2 AUDIT003 真实文本"}
  ],
  "changedFiles": ["audit/iter47.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "grep PDR '权重'", "result": "passed", "summary": "确认「权重」仅 L186 一处，函数未定义"},
    {"command": "read PDR (offset 173, 14) + (offset 78, 10) + (offset 19, 3)", "result": "passed", "summary": "读取 MA-007/§3.1.1 Claim.kind/§1 DO-7 确认权重与量纲隔离矛盾"},
    {"command": "read PDR (offset 163, 8) + (offset 713, 12)", "result": "passed", "summary": "读取 §3.3.2 peak / §12.2 AUDIT003 确认跨 kind 混加"},
    {"command": "write D:/Godot/Cosmos/audit/iter47.md", "result": "passed", "summary": "覆盖写入独立审计 #47"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 Y1-Y6 六节 + Proof Obligation 账本(Y5 项) + 5 条 I47- 缺口", "交叉引用 §3.4 MA-007/§1 DO-7/§3.1.1/§3.3.2/§12.2 AUDIT003/Iter14/36/50/39 真实行号"],
  "residualRisks": ["未运行 grep 全量验证（已 grep '权重' 仅 L186，结合 §3.4 文本确认无定义）", "weight 草案值依赖 Iter36 Q3 未本轮重证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter47.md，独立审计 MA-007 权重函数定义缺失（DO-7 落地通道悬空）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 MA-007 权重函数全文无定义、DO-7 折算通道悬空，需 PDR 侧采纳 Iter36 Q3 weight 表+peak 加权重"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
