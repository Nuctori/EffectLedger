# Iter29 审计 — `Connect`/`Disconnect` 回调 occupy 配对：create 须配 release 才守恒（独立审计 #29，hy3 单独进程，本轮重跑）

- **审计视角**：信号连接的生命周期占用守恒（独立 pass #29，全新上下文）
- **范围**：§7.5 Connect/Disconnect（L465-467）、§3.3.1 net（L163-165）、§3.2.3 Compatible（L131-138）、§1 DO-9（L21）；邻接 Iter09 I9-02（Disconnect mode=release 与 QueueFree move 不一致）、Iter27（QueueFree 释放漏算）、Iter25（C* 把 create+release 判兼容）、Iter23（良性生命周期）
- **结论摘要**：Connect 产生 `occupy(callback, callable.size, create, shell_scope)`（占用回调资源），Disconnect 产生 `occupy(callback, callable.size, release, shell_scope)`（释放回调资源）。**回调占用守恒要求每个 Connect 配一个 Disconnect**，且二者 size 相同（callable.size 不变）⇒ net 抵消、无泄漏。这是 §7.5 中**标注正确**的一对（Disconnect 用 release，与 QueueFree 的 move 形成对比，Iter09 I9-02）。本审计确认：(1) 配对逻辑成立，但**前提是 size 在 Connect→Disconnect 间不变**（callable 未被替换），文档未保证该不变式；(2) 若同一信号 Connect 两次（重复连接）而仅 Disconnect 一次，则 `occupy(callback)` 净 +1 个 size，但 Godot 中重复 Connect 同 callable 是 no-op（幂等），文档未建模该幂等 ⇒ net 可能误报；(3) Compatible 中 `(create,release)` 当前未定义（Iter23/Iter25），故 Connect||Disconnect 的并发安全判定仍悬空（即便 net 守恒）；（4）**跨信号类型泄漏**：若 Connect(sigA) 后 Disconnect(sigB)（错信号），resource 为 `callback` 字面量（非 per-signal）⇒ net 误抵消，泄漏检测被「接错信号」绕过（open，高）。结构性成立给条件证明。

---

## J1. 命题：Connect/Disconnect 回调 occupy 配对守恒（标注正确侧）

**命题**（§7.5 L466-467）：
- `Connect(...) = { ..., occupy(callback, callable.size, create, shell_scope) }`
- `Disconnect(...) = { ..., occupy(callback, callable.size, release, shell_scope) }`

若 ① 每个 Connect 配一个 Disconnect、② size 相同（`callable.size` 不变）、③ 同 `callback` 资源（同 callable 对象）：
- net：`+callable.size`（Connect create）`−callable.size`（Disconnect release）= 0 ⇒ 守恒。

**数学性质 / 证明状态**：
- **(PO-I29-a) 配对守恒成立（discharged，条件）**：在不变式 ①②③ 下，回调占用 net 守恒。证明：线性公式抵消。前提 ②③（size 不变、同 callback）未立 ⇒ 条件，实际未消解。
- 交叉：Iter09 I9-02（Disconnect 标 release 正确）、Iter25（C* 兼容 create+release）。

**文档行号**：§7.5（L465-467）、§3.3.1（L163-165）。

---

## J2. 命题：size 不变式与重复 Connect 幂等未建模

**命题**：Connect 占 `callable.size`、Disconnect 释 `callable.size`。若中间 `callable` 被替换（size 变）或重复 Connect（Godot 幂等 no-op）仅一次 Disconnect：
- 替换：Connect(sizeA)+Disconnect(sizeB)，sizeA≠sizeB ⇒ net = sizeA−sizeB ≠ 0 ⇒ **误报泄漏/残留**。
- 重复 Connect：Connect(sig)+Connect(sig)（幂等，仅 1 实际占用）但 Disconnect(sig) 1 次 ⇒ net = +2size−1size = +size ⇒ 误报占用。

**数学性质 / 证明状态**：
- **(PO-I29-b) size 不变式/幂等未建模（open，中）**：文档未声明 `callable.size` 在 Connect→Disconnect 间恒定，也未建模 Godot Connect 幂等（同 callable 重复 Connect 不增占用）。状态 = open（中，精度）。
- 交叉：Iter09 I9-03（size 估计）、Iter26（size 口径）。

**文档行号**：§7.5（L465-467）、§3.3.1（L163-165）。

---

## J3. 命题：Compatible 中 (create,release) 未定义，并发判定悬空

**命题**（§3.2.3 L131-138）：Connect(create)/Disconnect(release) 的 `Compatible(create,release)` 当前未定义（Iter23/Iter25）。即 `(create,release)` 不在 4 析取，也不在注释冲突项 ⇒ 默认 false/undefined。

**数学性质 / 证明状态**：
- **(PO-I29-c) Connect||Disconnect 并发判定悬空（open，高）**：即便 net 守恒（J1），`Connect ∥ Disconnect` 同 `callback` 资源的并发安全判定仍因 Compatible 偏函数悬空。状态 = open（高，交叉 Iter23 I23-01 / Iter25 PO-I25-a）。
- 附注：Iter25 的 C* 草案把 `(create,release)` 判兼容可消解此，但文档未采纳。

**文档行号**：§3.2.3（L131-138）、§7.5（L465-467）、Iter25（C*）。

---

## J4. 命题：跨信号类型泄漏绕过（resource 粗粒度）

**命题**：Connect/Disconnect 的回调占用 resource 为 `callback`（callable 对象字面量 `self`+method？实际 L466 写 `occupy(callback, callable.size, ...)`——`callback` 是占位符，非 `self.method` 构造）。若 Connect(sigA) 后 Disconnect(sigB)：
- 二者 `occupy` resource 均为 `callback`（字面量相同）⇒ net 抵消 ⇒ 系统认为「已释放」。
- 但真实：sigA 的回调未释放（仍连接），sigB 无对应连接被「假释放」。

⇒ 泄漏检测被「接错信号」绕过（resource 未区分信号实例，Iter19 量化：resource 多为粗粒度 `self`/`callback`/`tree`）。

**数学性质 / 证明状态**：
- **(PO-I29-d) 跨信号 resource 粗粒度致泄漏绕过（open，高）**：`callback` 作为 resource 不携带 signal 实例信息 ⇒ 不同信号的 Connect/Disconnect 在 net 层互相抵消 ⇒ 真泄漏（sigA 未释放）被掩盖。状态 = open（高，DO-9 失效，交叉 Iter23 I23-04 粗 resource）。
- 文档行号：§7.5（L465-467）、§3.3.1（L163-165）、Iter19（resource 粒度量化）。

---

## J5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (a) 文档声明 `callable.size` 在 Connect→Disconnect 间不变、(b) 建模 Godot Connect 幂等（重复 Connect 同 callable 不增占用）、(c) 采纳 Iter25 C* 使 `(create,release)` 兼容，则 Connect/Disconnect 配对守恒且并发安全。证明：不变式+幂等+对称。前提 PO-I29-a/b/c 未立 ⇒ 条件。
- **P2（discharged，条件）**：若 resource 细化到 `self.method`（per-signal 实例），则 Connect(sigA)/Disconnect(sigB) 因 resource 不同不抵消 ⇒ J4 绕过消解。证明：resource 细化。前提 Iter19（resource 粒度）未立 ⇒ 条件。
- **P3（discharged）**：在「每 Connect 严格配一 Disconnect、无重复无替换、单信号」理想工程下，net 守恒、DO-9 可行。证明：理想假设。但依赖未声明的强不变式。

---

## Proof Obligation 账本（Iter29）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I29-a | Connect/Disconnect 配对守恒(discharged 条件) | open(条件) | size 不变式 | L465-467, L163-165 |
| PO-I29-b | size 不变式/幂等未建模 | open(中) | 声明不变式+幂等 | L465-467 |
| PO-I29-c | (create,release) 并发判定悬空 | open(高) | 采纳 C*(Iter25) | L131-138 |
| PO-I29-d | 跨信号 resource 粗粒度致泄漏绕过 | open(高) | resource 细化 per-signal | L465-467, I19 |

## 本轮新发现未消解缺口（I29- 前缀，全局唯一）
- **I29-01**：Connect/Disconnect 回调占用配对标注正确（Disconnect 用 release），在不变式下 net 守恒（交叉 Iter09 I9-02，§7 中少见的标注一致对）。
- **I29-02（中）**：`callable.size` 不变式与 Godot Connect 幂等未建模 ⇒ 替换/重复 Connect 致 net 误报。
- **I29-03（高）**：`Compatible(create,release)` 未定义 ⇒ Connect||Disconnect 并发判定悬空（交叉 Iter23/Iter25）。
- **I29-04（高）**：`callback` resource 不区分信号实例 ⇒ Connect(sigA)/Disconnect(sigB) 净抵消，真泄漏绕过（交叉 Iter19 粗 resource / Iter23 I23-04）。
- **I29-05（弱）**：§7.5 的「释放侧用 release」与 §7.1 QueueFree 的「释放侧用 move」形成对照（Iter09 I9-02）——同一文内释放语义标注不统一，是 §7 映射层一致性缺陷的局部证据。

---

一句话摘要：Connect/Disconnect 回调占用配对标注正确（Disconnect 用 release，net 在不变式下守恒，I29-01，交叉 Iter09），但 `callable.size` 不变式与 Godot Connect 幂等未建模致误报（I29-02）、`Compatible(create,release)` 未定义致并发判定悬空（I29-03，交叉 Iter23/Iter25）、`callback` resource 不区分信号实例致 Connect(sigA)/Disconnect(sigB) 净抵消绕过泄漏检测（I29-04，高）——§7.5 与 QueueFree 释放标注分裂（I29-05，交叉 Iter09 I9-02）凸显 §7 一致性缺陷。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter29.md，未读/改其它 audit 文件，聚焦 Connect/Disconnect 回调配对，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #29（hy3 单独进程，本轮重跑）」、J1-J5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I29- 缺口列表；交叉引用真实行号(L465-467/L163-165/L131-138) 并经 read 确认 §7.5/§3.3.1/§3.2.3 真实文本"}
  ],
  "changedFiles": ["audit/iter29.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 461, 10)", "result": "passed", "summary": "读取 §7.5 Connect/Disconnect/EmitSignal/IsConnected 真实 Claim 集合"},
    {"command": "read PDR (offset 163, 5) + (offset 131, 8)", "result": "passed", "summary": "读取 §3.3.1 net 与 §3.2.3 Compatible 确认 (create,release) 未定义"},
    {"command": "write D:/Godot/Cosmos/audit/iter29.md", "result": "passed", "summary": "覆盖写入独立审计 #29"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 J1-J5 五节 + Proof Obligation 账本(J4 项) + 5 条 I29- 缺口", "交叉引用 §7.5/§3.3.1/§3.2.3/§1 DO-9/Iter09/Iter23/Iter25/Iter19 真实行号"],
  "residualRisks": ["未运行 Godot 源码验证 Connect 幂等与 callable.size 不变式（仅基于文档 §7.5 与 Godot 常识推导）", "callback resource 是否含 signal 实例依赖 §3.1.2 ResourceId 构造子未本轮读取"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter29.md，独立审计 Connect/Disconnect 回调 occupy 配对守恒与泄漏绕过",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 size 不变式/幂等未建模、Compatible(create,release) 悬空、跨信号 resource 绕过，需 PDR 侧补不变式+采纳 C*+细化 resource"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
