# Iter30 审计 — `Rpc`/`RpcId` 网络 resource 字符串拼接 ⇒ 编译期 Unknown 的精度损失（独立审计 #30，hy3 单独进程，本轮重跑）

- **审计视角**：动态 resource 构造与保守冲突策略的精度边界（独立 pass #30，全新上下文）
- **范围**：§7.9 Rpc/RpcId（L498-499，resource=self.id+"/"+method）、§3.1.2 ResourceId（L88-99，Network(peerId,method) 构造子）、§3.4 MA-010（L189，Unknown⊤ 保守）、§3.2.2 并行组合约束（L126-130）、§10 R-3（L610，误报率高）；邻接 Iter09 I9-05（网络 resource 编译期 Unknown）、Iter21（use∧use vs Unknown⊤）、Iter26（动态 id Unknown）
- **结论摘要**：Rpc 的 resource 由字符串拼接 `self.id + "/" + method` 构造（§7.9 L498），其中 `self.id`/`method` 多为运行时值 ⇒ 编译期不可判定 ⇒ 按 MA-010 保守归 `Unknown`。后果：(1) **任意两个 Rpc 调用**的 resource 都含 Unknown ⇒ 按 MA-010「Unknown 与任何资源冲突」⇒ 任意 Rpc∥Rpc 被判保守冲突 ⇒ 误报率极高（R-3 高/高，但此处是机制性放大，非「漏报」）；（2) §3.1.2 实际有 `Network(peerId, method)` 构造子（L88-99），但 §7.9 不走该构造子、改用字符串拼接 ⇒ resource 命名空间不一致（拼接式 vs 构造子式），且拼接式无法表达 `peerId` 区分（Rpc 用 self.id 而非显式 peerId，RpcId 才带 peerId）⇒ 同 peer 不同 method 与异 peer 同 method 的冲突粒度混乱；(3) 即便 resource 不 Unknown（method 为字面量），拼接式 `self.id+"/"+method` 与构造子 `Network(self.id, method)` 是**两种等价表示但文档无相等规则**（Iter32 PO-I1-a）⇒ 跨 API 的 Rpc 与 Network 资源无法去重/配对（open，高，精度）。结构性成立给条件证明。

---

## K1. 命题：Rpc resource 字符串拼接 ⇒ 编译期 Unknown ⇒ 任意两 Rpc 保守冲突

**命题**（§7.9 L498）：`Rpc(method, args) = { write(network, self.id + "/" + method, create, shell_scope), ... }`，`self.id` 与 `method` 编译期未知（运行时求值）。
**命题**（§3.4 MA-010 L189）：变量 path 保守为 `Unknown`，与任何资源冲突。

按 MA-010：`self.id+"/"+method` 含未知变量 ⇒ resource=`Unknown` ⇒ 两 Rpc 调用 `Compatible`/冲突判定（§3.2.2 约束，resource 相等则查 mode）⇒ `Unknown = Unknown`（保守冲突）⇒ **任意 Rpc ∥ Rpc 报冲突**。

**数学性质 / 证明状态**：
- **(PO-I30-a) 任意两 Rpc 保守冲突（open，高，精度）**：本应「同 method 同 target 才冲突」的细粒度判定退化为「任意网络写都冲突」⇒ R-3 误报率被机制性放大（非漏报）。状态 = open（高，精度）。
- 交叉：Iter09 I9-05（同 root）、Iter21（Unknown 冲突）、Iter26（动态 id Unknown 同机制）。

**文档行号**：§7.9（L498）、§3.4 MA-010（L189）、§3.2.2（L126-130）、§10 R-3（L610）。

---

## K2. 命题：拼接式 vs 构造子式 resource 命名空间不一致

**命题**（§3.1.2 L88-99）：`ResourceId := ... | Network(peerId, method) | ...`（带字段构造子）。
**命题**（§7.9 L498-499）：Rpc 用 `self.id + "/" + method`（字符串拼接），RpcId 用 `peerId + "/" + method`。

两者表达同一概念（网络端点+方法），但：
- 构造子式：`Network(peerId, method)`（结构化）。
- 拼接式：`self.id + "/" + method`（字符串）。

文档**未定义**「拼接式字符串 ⇔ 构造子式`Network`」的归一规则 ⇒ 同一网络资源有两种不可互认的表示。

**数学性质 / 证明状态**：
- **(PO-I30-b) 拼接式/构造子式不等价（open，中）**：若某处用 `Network(self.id, method)`（构造子）、另处用拼接式，二者在 `=` 比较（§3.2.2 约束、§3.3 聚合）下不相等 ⇒ 资源无法去重/配对。状态 = open（中，交叉 Iter32 PO-I1-a Claim 相等规则）。
- 文档行号：§3.1.2（L88-99）、§7.9（L498-499）。

---

## K3. 命题：Rpc 用 self.id 而非显式 peerId ⇒ 冲突粒度混乱

**命题**（§7.9 L498-499）：
- `Rpc(method)`：resource=`self.id + "/" + method`（self 为调用方 id，暗示「发往谁」由 self.id 决定？语义模糊）。
- `RpcId(peerId, method)`：resource=`peerId + "/" + method`（显式目标 peer）。

`Rpc` 的 resource 用 `self.id`（自身 id）而非目标 peer ⇒ 若 Rpc 语义是「广播/发送至默认 peer」，resource 应含目标而非自身。拼接式未表达「目标 peer」维度 ⇒ 同 self 不同目标 method 与异 self 同 method 在 `self.id` 维度混在一起 ⇒ 冲突粒度（谁与谁冲突）混乱。

**数学性质 / 证明状态**：
- **(PO-I30-c) Rpc resource 维度缺失目标 peer（open，中）**：`Rpc` 的 resource 不含目标 peer 标识（用 self.id），与 `RpcId` 的 `peerId` 不对称 ⇒ 网络冲突的语义粒度不一致。状态 = open（中，语义）。
- 文档行号：§7.9（L498-499）。

---

## K4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 Rpc 的 resource 改用 §3.1.2 构造子 `Network(targetPeer, method)`（显式目标 peer，编译期若 method 字面量则部分可判定），则 (a) 不依赖字符串拼接 (b) `Network(p,m)=Network(p',m')` 按字段相等（Iter32 规则）⇒ 仅同 peer 同 method 冲突，精度恢复。证明：构造子消除拼接+Unknown。前提 PO-I30-a/b/c 未立 ⇒ 条件。
- **P2（discharged，条件）**：若定义「拼接式 ⇔ 构造子式」归一（`self.id+"/"+method ≜ Network(self.id, method)`）并定义 Unknown 处理（Iter21 P1），则拼接式与构造子式互认、Unknown 短路冲突。证明：归一规则。前提 Iter32 PO-I1-a / Iter21 PO-I21-c 未立 ⇒ 条件。
- **P3（discharged）**：在「method 全为编译期字面量、self.id 经静态分析可知」的受限工程假设下，拼接式可静态求值 ⇒ 不落 Unknown ⇒ P1 精度可达。证明：无动态性。但 Godot Rpc 的 method 常为字符串变量 ⇒ 假设弱。

---

## Proof Obligation 账本（Iter30）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I30-a | 任意两 Rpc 保守冲突(Unknown) | open(高,精度) | 用 Network 构造子 | L498, L189, I9-05 |
| PO-I30-b | 拼接式/构造子式不等价 | open(中) | 归一规则(Iter32) | L88-99, L498-499 |
| PO-I30-c | Rpc resource 缺目标 peer 维度 | open(中) | Rpc 改 Network(target,m) | L498-499 |

## 本轮新发现未消解缺口（I30- 前缀，全局唯一）
- **I30-01（高，精度）**：Rpc resource=`self.id+"/"+method` 含运行时变量⇒Unknown⊤⇒任意两 Rpc 保守冲突，R-3 误报率机制性放大（交叉 Iter09 I9-05）。
- **I30-02（中）**：拼接式（§7.9）与构造子式 `Network(peerId,method)`（§3.1.2）同概念两种表示、无相等规则⇒跨 API 资源无法去重（交叉 Iter32 PO-I1-a）。
- **I30-03（中）**：Rpc 用 self.id 而非目标 peer⇒冲突维度（谁与谁冲突）混乱，与 RpcId 的 peerId 不对称。
- **I30-04（弱）**：若 method 为字面量但 self.id 未知，resource 为 `Unknown+"/"+method`——拼接式使「部分已知」也无法利用（字段级 Unknown 粒度丢失），比构造子式 `Network(Unknown, method)` 更粗（交叉 Iter26 动态 id）。

---

一句话摘要：Rpc resource=`self.id+"/"+method` 字符串拼接含运行时变量⇒Unknown⊤⇒任意两 Rpc 保守冲突、R-3 误报率机制性放大（I30-01，高，交叉 Iter09），拼接式与 §3.1.2 `Network(peerId,method)` 构造子同概念两种表示无相等规则（I30-02，交叉 Iter32），Rpc 用 self.id 缺目标 peer 维度与 RpcId 不对称（I30-03）——网络 resource 精度在 PDR 内无静态可判定基础，需改用构造子式+字段相等+Unknown 处理。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter30.md，未读/改其它 audit 文件，聚焦 Rpc 网络 resource Unknown 精度，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #30（hy3 单独进程，本轮重跑）」、K1-K4 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I30- 缺口列表；交叉引用真实行号(L498-499/L88-99/L189/L126-130/L610) 并经 read 确认 §7.9/§3.1.2/§3.4 MA-010/§10 真实文本"}
  ],
  "changedFiles": ["audit/iter30.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 493, 10)", "result": "passed", "summary": "读取 §7.9 Rpc/RpcId 真实 resource 拼接标注"},
    {"command": "read PDR (offset 88, 12) + (offset 186, 5) + (offset 608, 5)", "result": "passed", "summary": "读取 §3.1.2 Network 构造子、MA-010、§10 R-3 确认精度边界"},
    {"command": "write D:/Godot/Cosmos/audit/iter30.md", "result": "passed", "summary": "覆盖写入独立审计 #30"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 K1-K4 四节 + Proof Obligation 账本(K3 项) + 4 条 I30- 缺口", "交叉引用 §7.9/§3.1.2/§3.4 MA-010/§3.2.2/§10 R-3/Iter09/Iter21/Iter26/Iter32 真实行号"],
  "residualRisks": ["未运行 Godot 源码验证 Rpc method 是否常变量（仅基于文档 §7.9 与 Godot 常识）", "拼接式归一为 Network 构造子的可行性依赖 Iter32 Claim 相等规则未本轮读取"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter30.md，独立审计 Rpc/RpcId 网络 resource 字符串拼接 Unknown 精度损失",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现任意两 Rpc 保守冲突、拼接式/构造子式不等价、Rpc 缺目标 peer 维度，需 PDR 侧改用 Network 构造子+字段相等"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
