# Iter22 审计 — §3.2.3 第2-4析取非对称如何破坏并行组合 `||` 交换律（独立审计 #22，hy3 单独进程，本轮重跑）

- **审计视角**：冲突判定谓词对称性与组合算子代数律的局部一致性（独立 pass #22，全新上下文）
- **范围**：§3.2.3（L131-138，第1-4析取）、§3.2.2 并行组合 `||`（L126-130，约束用有序对 `Compatible(c₁.mode,c₂.mode)`）、§3.1 组合律隐含 A2 交换律；邻接 Iter16 I16-01/02（Compatible 偏函数+非对称）、Iter19（量化：组合子层 7/7 无端到端良定义）
- **结论摘要**：§3.2.3 第2-4析取仅形如 `(X, use)`（X∈{create,release,move}），即**只对「第一元为某 mode、第二元为 use」有序**给出 true；其对称有序对 `(use, X)` 全部未覆盖 ⇒ Compatible 非对称。但 §3.2.2 的 `||` 定义为 `S₁∪S₂`、且 §3.1 隐含并行组合满足 A2 交换律（同集合并，结果与书写顺序无关）。约束却用**有序对** `Compatible(c₁.mode, c₂.mode)`（c₁∈S₁, c₂∈S₂），使「并行组合是否通过兼容性检查」取决于把操作编号为 S₁ 还是 S₂——同物理并行组合两种写法结论相反 ⇒ 与 `||` 交换律矛盾（open，高）。

---

## B1. 命题：第2-4析取非对称致 Compatible(A,B)≠Compatible(B,A)

**命题**（§3.2.3 L132-134）：
- 第2析取：`(m₁=create ∧ m₂=use) ⇒ true`
- 第3析取：`(m₁=release ∧ m₂=use) ⇒ true`
- 第4析取：`(m₁=move ∧ m₂=use) ⇒ true`

这三析取都是「**第一元 = X，第二元 = use**」的单方向形式。其对称对：
- `(use, create)`：m₁=use≠create（不在第2析取），m₂=create≠use（不在任何析取，因所有析取第二元固定为 use）⇒ **false**
- `(use, release)`：同理 ⇒ **false**
- `(use, move)`：同理 ⇒ **false**

即：
```
Compatible(create, use) = true    但   Compatible(use, create) = false
Compatible(release, use) = true   但   Compatible(use, release) = false
Compatible(move, use) = true      但   Compatible(use, move) = false
```
⇒ `Compatible(A,B) ⇏ Compatible(B,A)`，谓词**非对称**。

**数学性质 / 证明状态**：
- **(PO-I22-a) Compatible 非对称（open，高）**：由 §3.2.3 字面定义直接推出，不需额外假设。第1析取 `(use,use)` 对称，第2-4析取全为非对称单向。状态 = open（高，纯定义缺陷）。
- 交叉：Iter16 I16-02 已指同一非对称；本项聚焦「第2-4析取」形式如何具体导致，并下接 B2 的交换律矛盾。

**文档行号**：§3.2.3（L131-138）、§3.2.2（L126-130）。

---

## B2. 命题：`||` 交换律要求 Compatible 对称，否则同组合顺序不同结论相反

**命题**（§3.2.2 L126-130）：`(S₁ || S₂) = S₁ ∪ S₂`，约束 `∀c₁∈S₁, c₂∈S₂, c₁.resource=c₂.resource ⇒ Compatible(c₁.mode, c₂.mode)`。
**命题**（§3.1 组合律隐含 A2 交换律）：`S₁ || S₂ = S₂ || S₁`（集合并天然交换，算子结果顺序无关）。

矛盾链：
1. 由 `||` 定义：`S₁ || S₂` 的约束检查 `(mode of c₁ in S₁, mode of c₂ in S₂)`。
2. `S₂ || S₁` 的约束检查 `(mode of c₂ in S₂, mode of c₁ in S₁)` —— 即 `(mode₂, mode₁)` 而非 `(mode₁, mode₂)`。
3. 若 `Compatible(mode₁, mode₂) ≠ Compatible(mode₂, mode₁)`（B1 已证对 create/release/move 成立），则 `S₁ || S₂` 通过、`S₂ || S₁` 失败，二者**同一物理并行组合、两种等价写法结论相反**。
4. 但 `S₁ || S₂ = S₂ || S₁`（交换律）⇒ 算子结果应是同一 Signature，不该因「先写谁」而合法性不同。

⇒ **冲突判定的合法性取决于人为编号顺序，与 `||` 交换律矛盾**（open，高）。

**数学性质 / 证明状态**：
- **(PO-I22-b) `||` 交换律与 Compatible 非对称矛盾（open，高）**：`||` 的代数对象（`S₁∪S₂`）交换，但其「准入约束」用有序对破坏交换 ⇒ 算子不是良定义的交换律算子（约束随书写顺序浮动）。这是 §3.4 MA-009「完备性已收敛」的反例之一（Iter16 I16-07）。状态 = open（高）。
- 交叉：Iter16 I16-02、Iter19（组合子层 0 端到端良定义）。

**文档行号**：§3.2.2（L126-130）、§3.1 组合律（A2 交换律隐含）、§3.4 MA-009（L189）。

---

## B3. 最小可执行实例：AddChild || MoveChild vs MoveChild || AddChild

**命题**（§7.1 L427/L430）：
- `AddChild(node) = { write(tree, node.id, create, shell_scope), occupy(tree, node.id, create, shell_scope) }` ⇒ mode 含 `create`
- `MoveChild(node, index) = { write(tree, node.id, use, shell_scope) }` ⇒ mode 为 `use`
- 二者 resource 均为 `tree` 同节点 ⇒ 触发 §3.2.2 约束（`c₁.resource = c₂.resource` 成立）。

求值：
- `AddChild || MoveChild`：c₁=AddChild(create), c₂=MoveChild(use) ⇒ `Compatible(create, use) = true`（第2析取）⇒ **通过**。
- `MoveChild || AddChild`：c₁=MoveChild(use), c₂=AddChild(create) ⇒ `Compatible(use, create) = false`（B1）⇒ **失败**。

⇒ 同一对「AddChild 与 MoveChild 作用于同树节点」并行，仅因书写顺序，一通过一失败。

**数学性质 / 证明状态**：
- **(PO-I22-c) 顺序敏感实例证明矛盾具体可触发（open，高）**：该实例的所有输入（mode、resource）均取自 §7.1 真实映射，非构造反例 ⇒ 矛盾在真实文档中可触发，非边界病态。状态 = open（高）。
- 附注：即便「宽恕读」为「注释即 else=false」，结论不变——`Compatible(use,create)` 仍 false，顺序敏感性依旧。

**文档行号**：§7.1（L427、L430）、§3.2.2（L126-130）、§3.2.3（L131-138）。

---

## B4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 `Compatible` 重定义为**对称闭包**——即对任意 `(A,B)` 令 `Compatible_sym(A,B) := Compatible_raw(A,B) ∨ Compatible_raw(B,A)`，并把第2-4析取扩展为双向（如 `(create,use)∨(use,create)`）——则 `Compatible_sym(A,B)=Compatible_sym(B,A)` 恒成立，`||` 约束顺序无关 ⇒ 交换律矛盾消解。证明：对称闭包使谓词对称 ⇒ `S₁||S₂` 与 `S₂||S₁` 约束同真值。前提 PO-I22-a/b（原 Compatible 非对称）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若把 `||` 约束改为**无序对** `∀{c₁,c₂} ⊆ (S₁∪S₂), c₁.resource=c₂.resource ⇒ Compatible_sym(c₁.mode,c₂.mode)`（先并后两两检查，消除 S₁/S₂ 编号），则顺序敏感性消失。证明：集合并后两两检查与书写顺序无关。前提 PO-I22-b 未立 ⇒ 条件。
- **P3（discharged）**：在「所有并行组合显式按 (create,use)/(release,use)/(move,use) 单向书写」的工程约定下，当前非对称 `Compatible` 不产生误判（因开发者总把「被使用方」放第二元）。证明：约定消除顺序歧义。但此约定非文档规定，且无法阻止 `MoveChild || AddChild` 合法写法 ⇒ 仅是规避而非解决（交叉 Iter19「组合子层 0 端到端良定义」）。

---

## Proof Obligation 账本（Iter22）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I22-a | 第2-4析取非对称：Compatible(create,use)=true 但 (use,create)=false | open(高) | 扩为对称双向析取 | L132-134 |
| PO-I22-b | `||` 交换律与 Compatible 非对称矛盾（顺序敏感） | open(高) | 强制 Compatible 对称或约束改无序对 | L126-130, §3.1 A2 |
| PO-I22-c | AddChild||MoveChild 通过 vs MoveChild||AddChild 失败（实例） | open(高) | 见 P1/P2 对称化 | L427, L430, L126-130 |
| PO-I22-d | MA-009「完备性已收敛」未含此矛盾 | open(高) | 重写 16 组合表+对称 | L189, I16-07 |

## 本轮新发现未消解缺口（I22- 前缀，全局唯一）
- **I22-01（高）**：§3.2.3 第2-4析取仅 `(X,use)` 单向形式，致 `Compatible(create,use)=true` 而 `Compatible(use,create)=false` 等三类非对称，谓词非对称（open）。
- **I22-02（高）**：`||` 定义为 `S₁∪S₂` 且 §3.1 隐含交换律，但准入约束用有序对 `Compatible(c₁.mode,c₂.mode)`，使同组合两种等价写法合法性相反，与交换律矛盾。
- **I22-03（高）**：最小实例 `AddChild(write,create) || MoveChild(write,use)` 通过，而 `MoveChild || AddChild` 失败（L427/L430），矛盾在 §7.1 真实映射中可触发。
- **I22-04（弱）**：该顺序敏感性是 §3.4 MA-009「16 组合完备性已收敛」的反例之一，进一步证 MA-009 实为 asserted（交叉 Iter16 I16-07）。
- **I22-05（弱）**：即便把第1析取 `(use,use)` 也纳入，仅 create/release/move 三类的对称对缺失即足以破坏全局对称，非「个别边角」。

---

一句话摘要：§3.2.3 第2-4析取仅 `(X,use)` 单向形式致 Compatible 非对称（`(create,use)=true` 但 `(use,create)=false`，I22-01，高），而 `||` 定义为 `S₁∪S₂` 且 §3.1 隐含交换律、其准入约束却用有序对 ⇒ 同组合两种写法合法性相反、与交换律矛盾（I22-02，高），最小实例 `AddChild||MoveChild` 通过而 `MoveChild||AddChild` 失败（I22-03，高，L427/L430 真实映射可触发），MA-009「完备性收敛」未含此反例（I22-04）——并行组合在数学层非良定义交换律算子。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter22.md，未读/改其它 audit 文件，聚焦 §3.2.3 第2-4析取非对称与 || 交换律矛盾，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #22（hy3 单独进程，本轮重跑）」、B1-B4 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I22- 缺口列表；交叉引用真实行号(L126-130/L131-138/L427/L430/§3.1 A2/Iter16/Iter19) 并经 read 确认 §3.2.2/§3.2.3/§7.1 真实文本"}
  ],
  "changedFiles": ["audit/iter22.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 124, 18)", "result": "passed", "summary": "读取 §3.2.1-3.2.3 并行组合与 Compatible 真实文本确认第2-4析取单向形式"},
    {"command": "grep PDR 「交换律|A2|AddChild|MoveChild」", "result": "passed", "summary": "定位 §7.1 AddChild(L427)/MoveChild(L430) 真实 mode 标注与并行组合约束"},
    {"command": "write D:/Godot/Cosmos/audit/iter22.md", "result": "passed", "summary": "覆盖写入独立审计 #22"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 B1-B4 四节 + Proof Obligation 账本(B4 项) + 5 条 I22- 缺口", "交叉引用 §3.2.2/§3.2.3/§7.1/§3.1 A2/§3.4 MA-009/Iter16/Iter19 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 Compatible 实际代码对称性（仅基于文档 §3.2.3 文本推导）", "§3.1 A2 交换律为隐含约定，文档未显式列「交换律」三字符，依赖组合律并的代数常识"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter22.md，独立审计 §3.2.3 第2-4析取非对称破坏 || 交换律的矛盾",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 Compatible 非对称与 || 交换律矛盾、AddChild||MoveChild 顺序敏感反例，需 PDR 侧对称化 Compatible 或约束改无序对"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read/grep；未读其它 audit 文件"
}
