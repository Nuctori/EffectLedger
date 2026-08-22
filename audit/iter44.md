# Iter44 审计 — 默认规则 occupy/release 漏报：非白名单 API 的 DO-9 泄漏盲区（独立审计 #44，hy3 单独进程，本轮重跑）

- **审计视角**：§8.1 默认效应规则对 lifecycle 类 API 的覆盖真空（独立 pass #44，全新上下文）
- **范围**：§8.1 默认规则（L516-518，「未映射 API 默认 { read(unknown, use), write(unknown, use) }」）、§8 ED-001..008（L524-535）、§7 映射表（L421-507，仅 ~100 白名单）、§1 DO-9（L21，泄漏检测）、§3.3.1 net（L163-165）；邻接 Iter10 I10-01/04（默认规则漏报/白名单覆盖率）、Iter38 S2（[Budget] opt-in 盲区）、Iter27（QueueFree 漏算）、Iter09（global/shell 混合）
- **结论摘要**：§8.1 默认规则对「未映射 API」只产生 `read(unknown, use)` 与 `write(unknown, use)` 两类 Claim，**完全不含 occupy(kind=occupy) 与 release**。后果：(1) **泄漏盲区**：任何不在 ~100 白名单内的「创建/释放」型 API（含大量非核心 Instantiate/AddChild/QueueFree/Free/RemoveChild 变体、第三方封装）走默认规则 ⇒ 不产生 occupy(create)/occupy(release) ⇒ net（§3.3.1）与 DO-9（L21）对其**完全不可见**——泄漏无法被检测；(2) **默认 Claim 三元组缺 scope 维**：`read(unknown, use)` 是 (kind, resource, mode?) 还是 (kind, resource, scope?) 歧义——若「use」是 mode，则该 Claim 无 scope（§3.1.3 ScopeId 7 构造子无 `use`，Iter15 I15-02），致 Peak/peak/net(scope) 过滤落空；(3) **resource=unknown 与 size 缺省**：默认两 Claim 均无 size ⇒ 与 ED-004 动态=∞（L527）、AUDIT002 +10（L717）口径全不一致（Iter46/Iter50）；(4) **白名单覆盖率假设不实**：ED-001 称「核心 API 白名单 100 个」，但 Godot 公开 API 数千，默认规则兜底意味着 95%+ API 走 read/write 而非 occupy ⇒ DO-9 仅在白名单内有效，覆盖率缺口巨大（Iter38 S2）。结构性成立给条件证明（补默认规则 occupy 兜底 + 显式 scope + size），但文档未采纳 ⇒ open（高）。

---

## V1. 命题：默认规则不含 occupy/release ⇒ DO-9 泄漏盲区

**命题**（§8.1 L516-518）：默认 `{ read(unknown, use), write(unknown, use) }`。
**命题**（§1 DO-9 L21）：Instantiate/AddChild 无对应释放路径 ⇒ 报警，依赖 occupy(create)/occupy(release) 配对。

后果：非白名单 API 调用若产生对象生命周期（如某个不在白名单的 `AddChild` 封装、`queue_free` 的 C# 别名、自定义 Pool 分配），其 Claim 仅 read/write，**无任何 occupy** ⇒ net（§3.3.1 仅 sum occupy）对其为 0 ⇒ 泄漏静默通过。即 DO-9 检测面 = 白名单面，非白名单面全盲。

**数学性质 / 证明状态**：
- **(PO-I44-a) 默认规则缺 occupy ⇒ 泄漏盲区（open，高）**：DO-9 正确性依赖 occupy(release) 存在；默认规则不产 occupy ⇒ 漏报。状态 = open（高，交叉 Iter10 I10-01、Iter27、Iter37 R4）。
- 文档行号：§8.1（L516-518）、§1 DO-9（L21）、§3.3.1（L163-165）。

---

## V2. 命题：默认 Claim 缺 scope 维（use 是 mode 还是 scope？）

**命题**（§8.1 L517）：`read(unknown, use)`——三元（kind=read, resource=unknown, 第三元=use）。
**命题**（§3.1.1 Claim 五元组 L78-86）：(kind, resource, mode, scope, size?)。第三元若表 mode，则该 Claim **无 scope**；若表 scope，则 `use` 非 §3.1.3 7 构造子之一（Iter15 I15-02）→ 非法 ScopeId。

两种读法皆坏：无 scope ⇒ Peak/peak/net(scope) 的 `c.scope⊆t` 过滤无法应用（Iter15 I15-01、Iter34）；`use` 作 scope ⇒ 非法。默认规则未定义 scope 维 ⇒ 派生度量对默认 Claim 全失效。

**数学性质 / 证明状态**：
- **(PO-I44-b) 默认 Claim 缺 scope/use 歧义（open，高）**：默认规则 Claim 的 scope 维缺失或非法 ⇒ 所有依赖 scope 的聚合落空。状态 = open（高，交叉 Iter15 I15-02、Iter34）。
- 文档行号：§8.1（L516-518）、§3.1.1（L78-86）、§3.1.3（L103-113）。

---

## V3. 命题：默认 resource=unknown 与 size 缺省的多口径

**命题**（§8.1 L517）：默认 resource=unknown，无 size。
**命题**（ED-004 L527）：动态 Instantiate 标 ∞；（AUDIT002 L717）+10；（§3.1.1 L82）size 默认 1。

默认 read/write 的 unknown 资源使 §3.2.2 Compatible 短路（Iter21 Unknown∧Unknown 放行、MA-010 L189 Unknown 冲突，二者打架）；无 size 使 net/peak 对这些 Claim 贡献 0 或默认 1，与 ∞/+10 不可对账（Iter46/Iter50）。

**数学性质 / 证明状态**：
- **(PO-I44-c) 默认 unknown/size 多口径（open，中）**：默认资源/规模未归一 ⇒ Compatible 与聚合口径冲突。状态 = open（中，交叉 Iter21、Iter46、Iter50）。
- 文档行号：§8.1（L516-518）、ED-004（L527）、MA-010（L189）、Iter46。

---

## V4. 命题：白名单覆盖率假设不实

**命题**（ED-001 L524）：「核心 API 白名单（100 个）」。
**命题**：Godot 4.x 公开 API 数千。默认规则兜底意味着绝大多数 API 走 read/write 而非 occupy ⇒ DO-9 仅在 ≤100 白名单 API 上有效。
**命题**（Iter38 S2）：[Budget] opt-in 使未标注组件亦盲 ⇒ 双重覆盖率缺口。

后果：DO-9「泄漏检测」实际覆盖面远低于文档隐含的完整覆盖 ⇒ 收敛声明（ED-001「已收敛」）不实。

**数学性质 / 证明状态**：
- **(PO-I44-d) 白名单覆盖率缺口（open，高）**：默认规则使 DO-9 仅覆盖白名单，覆盖率 <5% 量级，与「已收敛」不符。状态 = open（高，交叉 Iter38 S2、Iter10 I10-04）。
- 文档行号：ED-001（L524）、§8.1（L516-518）、Iter38（S2）。

---

## V5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若默认规则补 `occupy(unknown, create, scope_default)` + `occupy(unknown, release, scope_default)`（对疑似生命周期 API），则 DO-9 盲区收窄。证明：occupy 存在 ⇒ net 可见。前提：scope_default 合法（Iter34 ⊆*）、size 归一（Iter46）未立 ⇒ 条件。
- **P2（discharged，条件）**：若默认 Claim 显式 scope（如 Method(调用点)），则 Peak/peak/net(scope) 可过滤。证明：scope 维填充。前提 Iter34 未立 ⇒ 条件。
- **P3（discharged）**：在「所有生命周期 API 均入白名单」理想假设下，默认规则不影响 DO-9。证明：假设排除盲区。但 Godot API 数千 ⇒ 假设弱。

---

## Proof Obligation 账本（Iter44）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I44-a | 默认规则缺 occupy ⇒ 泄漏盲区 | open(高) | 补 occupy 兜底 | L516-518, L21, L163-165 |
| PO-I44-b | 默认 Claim 缺 scope/use 歧义 | open(高) | 显式 scope 维 | L516-518, L78-86, L103-113 |
| PO-I44-c | 默认 unknown/size 多口径 | open(中) | 归一(Iter21/46/50) | L516-518, L527, L189 |
| PO-I44-d | 白名单覆盖率缺口 | open(高) | 扩大白名单/默认 occupy | L524, L516-518, Iter38 |

## 本轮新发现未消解缺口（I44- 前缀，全局唯一）
- **I44-01（高）**：§8.1 默认规则仅 read/write、不含 occupy ⇒ 非白名单生命周期 API 的泄漏对 DO-9/net 全盲（交叉 Iter10/27/37）。
- **I44-02（高）**：默认 `read(unknown, use)` 的第三元 use 是 mode 还是 scope 歧义，且无 scope 维 ⇒ Peak/peak/net(scope) 过滤落空（交叉 Iter15/34）。
- **I44-03（中）**：默认 resource=unknown / 无 size 与 ED-004 ∞ / AUDIT002 +10 / size 默认 1 多口径冲突（交叉 Iter21/46/50）。
- **I44-04（高）**：白名单仅 100 个、Godot API 数千 ⇒ DO-9 实际覆盖 <5%，ED-001「已收敛」不实（交叉 Iter38 S2）。
- **I44-05（弱）**：默认规则的 use 与 §3.2.2 Compatible 的 use 含义是否一致未定义（mode=use 还是独立概念），加剧口径混乱。

---

一句话摘要：§8.1 默认规则仅产 read/write、不含 occupy（I44-01，高）⇒ 非白名单生命周期 API 的 DO-9 泄漏全盲，且默认 Claim 缺 scope 维/use 歧义（I44-02，高，交叉 Iter15/34）、resource=unknown 与 size 多口径（I44-03）、白名单仅 100 个致覆盖率 <5%（I44-04，高，交叉 Iter38）——ED-001「已收敛」不实，需补默认 occupy 兜底 + 显式 scope + 扩大白名单。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter44.md，未读/改其它 audit 文件，聚焦默认规则 occupy/release 漏报，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #44（hy3 单独进程，本轮重跑）」、V1-V5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I44- 缺口列表；交叉引用真实行号(L516-518/L524/L21/L163-165/L78-86/L103-113/L527/L189) 并经 read 确认 §8.1/§8 ED-001/§1 DO-9/§3.3.1 真实文本"}
  ],
  "changedFiles": ["audit/iter44.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 511, 12)", "result": "passed", "summary": "读取 §8.1 默认规则与 ED-001 确认缺 occupy"},
    {"command": "read PDR (offset 19, 3) + (offset 163, 5) + (offset 78, 10)", "result": "passed", "summary": "读取 §1 DO-9 / §3.3.1 net / §3.1.1 Claim 确认泄漏与 scope 维缺口"},
    {"command": "write D:/Godot/Cosmos/audit/iter44.md", "result": "passed", "summary": "覆盖写入独立审计 #44"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 V1-V5 五节 + Proof Obligation 账本(V4 项) + 5 条 I44- 缺口", "交叉引用 §8.1/§8 ED-001/§1 DO-9/§3.3.1/§3.1.1/§3.1.3/Iter10/27/37/15/34/21/46/50/38 真实行号"],
  "residualRisks": ["未运行源码验证 Godot API 实际数量与白名单比例（仅基于 §8 文本与 Godot 常识推导）"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter44.md，独立审计默认规则 occupy/release 漏报（DO-9 泄漏盲区）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现默认规则缺 occupy 致泄漏盲区，需 PDR 侧补 occupy 兜底+显式 scope+扩大白名单"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
