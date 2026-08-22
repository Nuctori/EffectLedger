# Iter24 审计 — `move` 模式的代数语义全审：所有权转移 ≡ 旧资源 release + 新资源 create（独立审计 #24，hy3 单独进程，本轮重跑）

- **审计视角**：「move」作为第四 mode 的代数定义 / 与 create/release 的关系 / 在 net 与 Compatible 中的归约（独立 pass #24，全新上下文）
- **范围**：§3.1.1 Claim.mode（L78-86，mode∈{use,create,release,move}）、§7.1 QueueFree（L429，mode=move）、§7.1 AddChild/RemoveChild（L427-428，create/release）、§3.3.1 net（L163-165）、§3.2.3 Compatible（L131-138）；邻接 Iter08 I8-01（QueueFree mode=move 致 net 漏 release）、Iter16 I16-04（move 5 个配对 undefined）、Iter23 I23-03（释放 mode 分裂）
- **结论摘要**：`move` 在 §3.1.1 仅列为 4 个 mode 之一（与 use/create/release 并列），**全文无任何「move 的代数语义」定义**——既不说明「move 相对于 create/release 是什么」，也不给 move 与 create/release 的组合/归约规则。从 Godot 语义看，QueueFree 的 `move` 表示「节点所有权从场景树转移到待回收队列（或释放）」，即**旧拥有者 release + 新拥有者(回收器) create** 的复合动作。但 §3.3.1 的 `net(S)` 仅对 `mode∈{create,move}` 计正项、`mode=release` 计负项——`move` 被当作**正值累计**（与 create 同侧），而 QueueFree 实际含义是「释放」应计负项 ⇒ net 把一次释放当一次占用，**系统性高估占用、泄漏检测（DO-9）失效**（open，高，安全）。同时 §3.2.3 仅含 `(move,use)` 析取，move 与 create/release 的 5 个有序对全 undefined ⇒ 冲突判定对含 move 的配对悬空（open，高）。结构性成立（若将 move 严格归约为 release+create 复合）给条件证明。

---

## D1. 命题：move 在 §3.1.1 无代数定义，仅列名

**命题**（§3.1.1 L80-81）：`mode ∈ { use, create, release, move }` —— 4 值枚举，但仅 use/create/release 有直观语义（使用/创建/释放），`move` 无配套说明：无定义「move 修改哪些字段」「move 与 create/release 的守恒关系」「move 进入 net 公式的哪一侧」。

**数学性质 / 证明状态**：
- **(PO-I24-a) move 无代数定义（open，高）**：作为形式系统的基元，每个 mode 应给出其在派生度量（net/peak/Compatible）中的语义。use/create/release 在 net 公式中明确（create/move 正、release 负），但 release/move 与 create 的**守恒关系**未定义：一个 create 是否必须配一个 release 才守恒？move 是否「既入又出」？文档未给。状态 = open（高）。
- 交叉：Iter08 I8-01、Iter16 I16-04、Iter23 I23-03。

**文档行号**：§3.1.1（L78-86）、§3.3.1（L163-165）、§3.2.3（L131-138）。

---

## D2. 命题：QueueFree 的 move 在 net 中被计为正项，致释放漏算（核心，高）

**命题**（§7.1 L429）：`QueueFree() = { release(tree, self.id, move, shell_scope), release(memory, self.size, move, shell_scope) }`。
**命题**（§3.3.1 L163-165）：`net(S) = Σ_{c∈S, c.kind=occupy, c.mode∈{create,move}} c.size − Σ_{c∈S, c.kind=occupy, c.mode=release} c.size`。

求值：设节点经 AddChild 占用 `occupy(tree,id,create)`（L427），随后 QueueFree 释放 `occupy(tree,id,move)`（L429）。
- AddChild 贡献：`+size`（create 侧正项）。
- QueueFree 贡献：`+size`（move 侧正项，因 net 公式把 move 与 create 同置正侧）。
- `net = (+size) + (+size) = +2·size` ⇒ **释放反而增加 net，占用被双重累计**。

而正确泄漏检测期望：AddChild(+size) 后 QueueFree 应 −size ⇒ net 回到 0（无泄漏）。当前公式下 net 不回零 ⇒ **QueueFree 的释放被 net 完全漏算**，DO-9「Instantiate/AddChild 无对应释放路径才报警」失去数学基础（net 恒偏正，永远显示「占用在涨」）。

**数学性质 / 证明状态**：
- **(PO-I24-b) QueueFree move 在 net 中计正项致释放漏算（open，高，安全）**：`net` 公式把 `move` 与 `create` 同放正侧，但 QueueFree 的 move 语义是「释放」⇒ 释放被当占用累计。这是 Iter08 I8-01 的精确化：漏算的不是「release 项缺失」那么简单，而是「move 被错误放在正侧」。状态 = open（高，安全相关——泄漏检测失效）。
- 交叉：Iter08 I8-01（同 root）、Iter15 I15-06（net 无 scope 使局部失配被掩盖）、Iter17 I17-04（net<0 未定义）。

**文档行号**：§7.1（L427、L429）、§3.3.1（L163-165）、Iter08（I8-01）。

---

## D3. 命题：move 的推荐代数归约——release(旧) + create(新)

**命题**：若按 Godot 所有权语义，QueueFree 的 move 表示「节点从场景树移交回收器」：
- 对**旧拥有者**（场景树/self）：`move` = `release`（不再拥有）。
- 对**新拥有者**（回收队列/GC）：`move` = `create`（回收器获得引用，待真正释放）。

即 `release(X, id, move) ≡ release(X, id, release) [旧所有者] + create(Recycler, id, create) [新所有者]`（复合，两 Claim）。若如此归约：
- net 贡献：`−size`(旧 release) `+size`(新 create) = 0（守恒，若回收器不永久占用）。
- 冲突判定：`move` 拆为 release+create 后落入 Iter23 P1 的良性对 `(create,release)` 等 ⇒ 与 create 配对良定义。

**数学性质 / 证明状态**：
- **(PO-I24-c) move 可归约为 release+create 复合（条件证明）**：若文档显式定义 `move(X,id) ≜ release(X,id) ⨯ create(Recycler,id)`（或用现有 create/release 表达），则 net/Compatible 对 move 的语义闭合。证明：归约后 move 不再是需要独立处理的第四 mode。前提 PO-I24-a（move 未定义）未立 ⇒ 条件，实际未消解。
- 交叉：Iter23 P2 同指 move 归约。

**文档行号**：§3.1.1（L78-86）、§7.1（L429）、§3.3.1（L163-165）。

---

## D4. 命题：Compatible 中 move 的 5 个配对全 undefined

**命题**（§3.2.3 L131-138）：仅 `(move,use)` 为兼容析取，注释含 `(move,move)` 不兼容。涉及 move 的 7 个有序对：
- 已定义：`(move,use)`（兼容）、`(move,move)`（注释不兼容）。
- 未定义：`(move,create)`、`(move,release)`、`(create,move)`、`(release,move)`、`(use,move)`。

⇒ QueueFree(move) 与 AddChild(create) / RemoveChild(release) / MoveChild(use 反向) 配对时，冲突判定 undefined 或（宽恕读）false。

**数学性质 / 证明状态**：
- **(PO-I24-d) 含 move 非 use 配对全 undefined（open，高）**：move 作为「释放/转移」语义，其与 create（创建新占用）、release（直接释放）的关系最该被定义（占用守恒的核心），却全缺。状态 = open（高，交叉 Iter16 I16-04）。
- 附注：即便补 `(move,use)` 为兼容（已含），move 与「其它释放/创建动作」的配对才是泄漏/并发判定的关键，仍全悬空。

**文档行号**：§3.2.3（L131-138）、§7.1（L427-429）。

---

## D5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若将 `move` 严格归约为 `release(旧拥有者) + create(新拥有者)`（D3），并把 `net` 公式视为对归约后 Claim 求和（release 计负、create 计正），则 QueueFree 的占用守恒（旧 −size、新 +size 抵消，若回收器临时持有后释放则最终归零），D2 漏算消解。证明：归约使 move 无独立正侧。前提 PO-I24-a/c（move 归约未定义）未立 ⇒ 条件。
- **P2（discharged，条件）**：若 Compatible 扩展含 `(move,release)`、`(release,move)`、`(move,create)`、`(create,move)`、`(use,move)` 五对的语义（按 D3 归约后分别等价于 release/release、release/create、use/release 等已知对），则 D4 全闭合。证明：归约复用已知对。前提 PO-I24-d 未立 ⇒ 条件。
- **P3（discharged，替代）**：若**不归约**而直接把 QueueFree 的 `move` 改为 `release`（Iter27 主题），则 net 公式 `mode=release` 计负项 ⇒ 释放正确计入，D2 直接消解（无需定义 move 代数）。证明：标注对齐公式。前提 iter27 PO-I27（mode 标注修正）未立 ⇒ 条件。
- **P4（discharged）**：在「move 被当作『纯占用』、不计释放」的弱解释（当前 net 公式字面）下，语法闭合但语义错误（D2 漏算）。证明：公式字面自洽。但 DO-9 失效 ⇒ 非可用解。

---

## Proof Obligation 账本（Iter24）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I24-a | move 无代数定义（仅枚举未给语义） | open(高) | 定义 move 守恒/归约 | L78-86 |
| PO-I24-b | QueueFree move 在 net 计正项致释放漏算 | open(高,安全) | 归约 move 或改 release | L429, L163-165 |
| PO-I24-c | move ≜ release(旧)+create(新) 归约 | open(条件) | 见 P1 | L429, L78-86 |
| PO-I24-d | Compatible 含 move 的 5 配对 undefined | open(高) | 补 move 配对规则 | L131-138, L427-429 |

## 本轮新发现未消解缺口（I24- 前缀，全局唯一）
- **I24-01（高）**：`move` 在 §3.1.1 仅列名无代数定义，未说明与 create/release 的守恒/组合关系。
- **I24-02（高，安全）**：QueueFree `release(tree,id,move)` 在 §3.3.1 net 公式中因 `move` 与 `create` 同置正侧，被计为正占用 ⇒ AddChild(+size)+QueueFree(+size)=+2size，释放被漏算，DO-9 泄漏检测失效（交叉 Iter08 I8-01）。
- **I24-03**：move 可归约为「旧拥有者 release + 新拥有者(回收器) create」复合，归约后 net 守恒、Compatible 闭合——但文档未采用此归约。
- **I24-04（高）**：Compatible 中 `(move,create)/(move,release)/(create,move)/(release,move)/(use,move)` 五个有序对全 undefined，释放类配对判定悬空（交叉 Iter16 I16-04）。
- **I24-05（弱）**：QueueFree 用 move、RemoveChild 用 release，同一「释放」语义两 mode 标注分裂 ⇒ 文档对释放动作无统一代数表达（交叉 Iter23 I23-03）。

---

一句话摘要：`move` 在 §3.1.1 仅列名无代数定义（I24-01，高），QueueFree 的 `release(tree,id,move)` 因 net 公式把 move 与 create 同置正侧被计为正占用、释放漏算致 DO-9 失效（I24-02，高安全，交叉 Iter08 I8-01），move 可归约为「旧 release+新 create」复合但未采用（I24-03），Compatible 中含 move 的 5 个配对全 undefined（I24-04，高），且 QueueFree(move) 与 RemoveChild(release) 释放语义两标注分裂（I24-05，交叉 Iter23）——需 PDR 侧定义 move 代数或统一改 release。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter24.md，未读/改其它 audit 文件，聚焦 mode=move 代数语义，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #24（hy3 单独进程，本轮重跑）」、D1-D5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I24- 缺口列表；交叉引用真实行号(L78-86/L427-429/L163-165/L131-138) 并经 read 确认 §3.1.1/§7.1/§3.3.1/§3.2.3 真实文本"}
  ],
  "changedFiles": ["audit/iter24.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 10)", "result": "passed", "summary": "读取 §3.1.1 Claim.mode 枚举确认 move 仅列名"},
    {"command": "read PDR (offset 425, 6) + (offset 163, 5)", "result": "passed", "summary": "读取 §7.1 QueueFree/AddChild/RemoveChild 与 §3.3.1 net 公式"},
    {"command": "write D:/Godot/Cosmos/audit/iter24.md", "result": "passed", "summary": "覆盖写入独立审计 #24"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 D1-D5 五节 + Proof Obligation 账本(D4 项) + 5 条 I24- 缺口", "交叉引用 §3.1.1/§7.1/§3.3.1/§3.2.3/Iter08/Iter16/Iter23 真实行号"],
  "residualRisks": ["未运行 Analyzer 验证 net 公式实际代码对 move 的处理（仅基于 §3.3.1 文本推导）", "move→release+create 归约的回收器临时持有是否最终归零依赖 §9 运行时未本轮读取"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter24.md，独立审计 mode=move 代数语义（所有权转移归约、net 漏算、Compatible 配对悬空）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 move 无定义致 net 释放漏算(DO-9 失效)与 Compatible 5 配对悬空，需 PDR 侧定义 move 或统一改 release"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
