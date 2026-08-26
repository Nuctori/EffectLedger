# Iter26 审计 — `Instantiate(scene)` 深审：new_id 动态⇒Unknown 与 size 口径分裂（独立审计 #26，hy3 单独进程，本轮重跑）

- **审计视角**：动态实例化映射的可判定性 / size 语义一致性（独立 pass #26，全新上下文）
- **范围**：§7.4 Instantiate（L458）、§8 ED-004 动态 Instantiate 标 ∞（L527）、§3.1.1 size∈Nat?（L78-86，默认 1）、§3.4 MA-008（L185，size 默认 1）、§3.1.2 ResourceId（L88-99，动态 uid⇒Unknown）、§12.2 AUDIT002（L662-664，+10 per death）；邻接 Iter09 I9-06（new_id Unknown）、Iter18 I18-03（size=∞ 违 Nat?）、Iter08 I8-01/Iter27（泄漏链）
- **结论摘要**：`Instantiate` 产生 3 个 Claim：读 `memory(scene.uid)`、创建 `tree(new_id)`、占用 `memory(scene.estimated_size)`。两处动态性引爆未定义：(1) **new_id 是运行时生成的节点 id** ⇒ 编译期 `ResourceId` 未知（§3.1.2 动态 uid 归 Unknown，Iter01/Iter09）⇒ `create(tree, new_id, create)` 的 resource 为 Unknown ⇒ 与任何 tree 操作保守冲突（MA-010）、且其占用是否配 QueueFree 释放无法按 resource 配对（泄漏链断裂）；(2) **size 口径分裂**——§7.4 写 `scene.estimated_size`（有限估计值），但 §8 ED-004 把「动态实例化变量场景」另标为 size=∞（L527），而 §3.1.1 `size∈Nat?` 拒绝 ∞（Iter18 I18-03）、§3.4 MA-008 明定默认 1 ⇒ 同一 API 在三处给出三种 size 语义（estimated_size / ∞ / 1），且 §12.2 AUDIT002 报告又写死 +10（Iter13 I13-03）。即 Instantiate 的「占用数值」在 PDR 内无统一出处（open，高）。

---

## F1. 命题：new_id 动态 ⇒ ResourceId Unknown ⇒ 冲突与配对悬空

**命题**（§7.4 L458）：`Instantiate(scene) = { ..., create(tree, new_id, create, shell_scope), ... }`，`new_id` 为运行时由引擎分配的节点 id。
**命题**（§3.1.2 L88-99）：ResourceId 构造子含 `Tree(path)`，但 `path` 为静态字面量；动态生成的 id 不满足任何构造子 ⇒ 保守归 `Unknown`（MA-010 同机制）。

后果：
- `create(tree, new_id, create)` 的 resource = `Tree(Unknown)` ⇒ 与同场景的 `AddChild`/`RemoveChild`/`QueueFree` 的 `tree` 资源（哪怕真实是同一树）按 MA-010 保守判**冲突**（任意 Instantiate 与任意 tree 操作冲突，精度 R-3）。
- 泄漏检测（DO-9）：Instantiate 的占用 `occupy(memory, scene.estimated_size, create)` 期望配 QueueFree 的 `occupy(memory, self.size, move)`（Iter08）；但二者 resource 为 `memory` 字面量（一致），而 `create(tree,new_id)` 这边 new_id 的 Unknown 使「该节点是否被正确释放」无法按 id 追踪（net 粒度是全局 memory，非 per-node，Iter15 I15-06）⇒ 泄漏链在 create(tree) 端已断裂。

**数学性质 / 证明状态**：
- **(PO-I26-a) Instantiate new_id Unknown 致冲突+配对悬空（open，高）**：动态 id 使 resource 编译期不可判定 ⇒ MA-010 保守冲突 + per-node 释放追踪失效。状态 = open（高）。
- 交叉：Iter09 I9-06、Iter01 I1-03（Unknown⊤）、Iter15 I15-06（net 无 per-scope）、Iter16 I16-06（Unknown 冲突）。

**文档行号**：§7.4（L458）、§3.1.2（L88-99）、§3.1.3（L103-113）、§3.4 MA-010（L189）。

---

## F2. 命题：size 三处口径分裂（estimated_size / ∞ / 1 / +10）

**命题**：同一 `Instantiate` 的占用 size 在文档不同位置给出不同值：
- §7.4（L458）：`occupy(memory, scene.estimated_size, create, shell_scope)` —— 有限估计值 `scene.estimated_size`。
- §8 ED-004（L527）：「动态 `Instantiate` 变量场景标记为 ∞」 —— size=∞。
- §3.4 MA-008（L185）：「默认 size = 1（单位资源）」 —— 若未显式标注则 1。
- §12.2 AUDIT002（L662-664）：`occupy{memory} +10 per death` —— 报告写死 10。

四值并存：`estimated_size` / `∞` / `1` / `10`。
- `∞` 违反 §3.1.1 `size∈Nat?`（Iter18 I18-03，类型冲突）。
- `estimated_size`（真实估计）与 `1`（默认）与 `10`（报告示例）无任何换算关系 ⇒ 占用数值在 PDR 内**无单一权威出处**。

**数学性质 / 证明状态**：
- **(PO-I26-b) Instantiate size 口径分裂（open，高）**：派生度量 peak/net 依赖 size 数值，但同一 API 的 size 在四处在四种语义间游移 ⇒ peak/net 在 Instantiate 上无确定值，DO-8 峰值检测失准。状态 = open（高）。
- 交叉：Iter18 I18-03（∞ 违 Nat?）、Iter13 I13-03（AUDIT002 +10 无出处）、Iter13 I13-04（AUDIT003 64MB 冲突）。

**文档行号**：§7.4（L458）、§8 ED-004（L527）、§3.4 MA-008（L185）、§12.2（L662-664）、§3.1.1（L78-86）。

---

## F3. 命题：ED-004「动态实例化标 ∞」的归属错误

**命题**（§8 ED-004 L527）：把「动态 Instantiate 变量场景」标 ∞ 放在**推导层**，但 §7.4 映射层写的是有限 `estimated_size`。即同一 API 的 size 在映射层（有限）与推导层（∞）口径分离。

**数学性质 / 证明状态**：
- **(PO-I26-c) ∞ 标注层错位（open，中）**：若动态性需保守为 ∞，应统一在映射层（§7.4）标注 ∞，而非推导层临时补丁；当前分离使「映射层有限、推导层 ∞」两套并行，读者不知以何为准。状态 = open（中，口径一致性）。
- 交叉：Iter18 R3（∞ 在 ED-004 非 §7.4）、Iter18 I18-03。

**文档行号**：§8 ED-004（L527）、§7.4（L458）。

---

## F4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (a) 显式建立「运行时 id 映射表」`runtime_id(scene) → Tree(Unknown)` 并定义 `Unknown` 在 Compatible 中的处理（Iter21 P1 前置短路），(b) size 统一为 `scene.estimated_size` 并禁止 ∞（改 §8 ED-004 为「动态性保守通过 Unknown 冲突表达，而非 size=∞」，即 Iter18 P2 的 extended-Nat 仅用于上界标记而非 size 值），则 Instantiate 的 resource 与 size 均良定义。证明：统一口径 + Unknown 处理。前提 PO-I26-a/b/c 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 size 提升为 extended-Nat（Iter18 P2），`estimated_size` 为有限、`∞` 改写为 `⊤`（上界标记，非实际 size），则 §7.4 与 ED-004 口径统一（有限值 + 上界标记）。证明：extended-Nat 含 ⊤。前提 Iter18 PO-I18-c 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 scene 为编译期已知、new_id 可静态推断」的受限工程假设下，resource 不落 Unknown、size 取 estimated_size 一致 ⇒ Instantiate 良定义。证明：无动态性。但此假设与 Godot 动态实例化现实矛盾（ED-004 正是为动态性设）。

---

## Proof Obligation 账本（Iter26）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I26-a | new_id Unknown ⇒ 冲突+per-node 泄漏链断 | open(高) | 建 runtime_id 映射 + Unknown 处理 | L458, L88-99, I1-03 |
| PO-I26-b | size 口径分裂(estimated/∞/1/+10) | open(高) | 统一 size 出处 | L458, L527, L185, L662-664 |
| PO-I26-c | ∞ 标注层错位(映射层有限 vs 推导层∞) | open(中) | 统一标注层 | L527, L458 |

## 本轮新发现未消解缺口（I26- 前缀，全局唯一）
- **I26-01（高）**：Instantiate 的 `new_id` 运行时生成 ⇒ ResourceId 未知 ⇒ 与任何 tree 操作保守冲突（MA-010）且 per-node 释放追踪断裂（net 全局无 per-scope，Iter15 I15-06）。
- **I26-02（高）**：同一 Instantiate 占用 size 在 §7.4(estimated_size)/§8 ED-004(∞)/MA-008(1)/§12.2(+10) 四值并存 ⇒ peak/net 在 Instantiate 上无确定值（DO-8 失准）。
- **I26-03（中）**：ED-004 把 ∞ 放在推导层、§7.4 写有限 estimated_size ⇒ 口径分离，读者不知以何为准（交叉 Iter18 R3）。
- **I26-04（弱）**：Instantiate 是泄漏检测核心载体（DO-9 例常举 Instantiate 无 QueueFree），但其 new_id Unknown 使「该实例是否释放」无法按 resource 配对 ⇒ DO-9 在动态实例上数学失效（交叉 Iter08/Iter27）。

---

一句话摘要：Instantiate 的 new_id 运行时生成⇒ResourceId 未知（MA-010 保守冲突+per-node 释放追踪断裂，I26-01，高，交叉 Iter09/Iter15），且同一占用 size 在 §7.4(estimated_size)/§8 ED-004(∞)/MA-008(1)/§12.2(+10) 四值并存（I26-02，高，DO-8 失准，交叉 Iter18/Iter13），∞ 标注层错位（I26-03）——动态实例化的可判定性与数值口径在 PDR 内均无统一基础，DO-9 在其核心载体上数学失效（I26-04）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter26.md，未读/改其它 audit 文件，聚焦 Instantiate 动态性，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #26（hy3 单独进程，本轮重跑）」、F1-F4 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I26- 缺口列表；交叉引用真实行号(L458/L527/L185/L662-664/L88-99/L78-86) 并经 read 确认 §7.4/§8 ED-004/§3.1.1/MA-008/§12.2 真实文本"}
  ],
  "changedFiles": ["audit/iter26.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 452, 10)", "result": "passed", "summary": "读取 §7.4 Instantiate/Load/Preload 真实 Claim 集合与 size 标注"},
    {"command": "read PDR (offset 78, 10) + (offset 184, 4) + (offset 520, 10) + (offset 656, 12)", "result": "passed", "summary": "读取 §3.1.1 size∈Nat? / MA-008 / §8 ED-004 / §12.2 AUDIT002 确认四值口径"},
    {"command": "write D:/Godot/Cosmos/audit/iter26.md", "result": "passed", "summary": "覆盖写入独立审计 #26"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 F1-F4 四节 + Proof Obligation 账本(F3 项) + 4 条 I26- 缺口", "交叉引用 §7.4/§8 ED-004/§3.1.1/§3.4 MA-008/MA-010/§12.2/§3.1.2/Iter09/Iter15/Iter18/Iter13 真实行号"],
  "residualRisks": ["未运行 Godot 源码验证 new_id 是否真运行时生成（仅基于文档 §7.4 文本）", "size 四值口径为文档比对，未运行 Analyzer 实测 Instantiate 输出"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter26.md，独立审计 Instantiate 动态性(new_id Unknown + size 四值口径分裂)",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 Instantiate 动态 id 与 size 口径双缺口，需 PDR 侧建 runtime_id 映射 + 统一 size 出处"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
