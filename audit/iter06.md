# Iter06 审计 — §5 Shell 同态映射的函子律与 Delta Sync 正确性（独立审计 #6，hy3 单独进程，本轮重跑）

- **审计视角**：范畴论 / 同态正确性（独立 pass #6，全新上下文）
- **范围**：§5.1.1-5.1.3（Shell 函子、薄层约束、同步点/Delta）；§5.2 SH-001..005；邻接 §3（Signature/组合律）、§4（Entity/World/Command）、§6（L1/L2/L3）、§1 DO-10
- **结论摘要**：§5.1.1 将 Shell 称为「函子 Domain→Godot」是**比喻性陈述，非范畴论函子**——Domain 与 Godot 均未构造为范畴、函子律（identity/composition 守恒）从未陈述或证明、映射仅单向且不全。§5.1.3 Delta Sync 仅定义数据结构（Spawned/Modified/Destroyed），**无一致性判据/前条件/证明** ⇒ 增量同步正确性为 open（核心，阻断 DO-10）。薄层五约束均为禁止型契约，依赖未形式化 L2/L3 ⇒ 多为 open/asserted。SH-002/SH-005 在声明层成立（discharged），SH-001/003/004 收敛建立于未形式化工具/未定义校准，实为 open/asserted。

---

## E1. §5.1.1 Shell 作为「函子」的断言审计

**命题** §5.1.1：`Shell: Domain → Godot`，列对象映射 3 条（Entity→Node、World→SceneTree、Component→Node 属性/子节点）、态射映射 3 条（Spawn→Instantiate+AddChild、Destroy→QueueFree、SetComponent→node.Set）（L281-294）。

**数学性质 / 证明状态**：
- **(PO-I6-a) 范畴结构未定义（open）**：Domain 与 Godot 未构造为范畴（未定义对象集、态射集、复合、单位元）。「函子」术语无承载结构 ⇒ 该陈述为类比，非可证性质。状态 = open。
- **(PO-I6-b) 函子律未证（open，高）**：
  - 守恒单位元：需 `Shell(id_Domain) = id_Godot`。文档未定义 Domain 的 identity 态射（World 不变更？）与 Godot 的 identity（空操作？）。open。
  - 守恒复合：需 `Shell(f;g) = Shell(f);Shell(g)`。文档仅给单步映射，未给复合态射（如 Spawn 后 SetComponent 的复合 `Shell(Spawn;SetComponent)`）。open。
- **(PO-I6-c) 映射非全 / 仅单向（open）**：Domain 对象含 Entity/Component/Archetype/World/System/Command（§4），Shell 仅映射 Entity/World/Component 三类；Command→Godot API 映射在 §7（结果而非态射）。World→SceneTree 是**增量**（Delta）而非整体同构。无逆向映射（Godot→Domain 未在 Shell 职责内，除输入采集）。故「同态」弱化为部分单向同态。状态 = open。

**文档行号**：§5.1.1（L281-294）、§4.1（L197-261）。

---

## E2. §5.1.2 薄层五约束的可证性

**命题** 五约束：无决策、无状态、无循环、确定性、效应边界（L296-304）。

**数学性质 / 证明状态**：
- 五约束均为**禁止型契约**，非可由 §5 自证的程序性质。
- **无决策/无循环**：需 L2 Generator AST 检查（禁 if/else/for/while/switch/try）+ L3 Analyzer（SH-001，L321）。但 L2/L3 的检查完备性未证（Iter07）→ **open/asserted**。
- **无状态**：语义区分「独立状态禁止 / 缓存覆盖写入允许」（SH-002），属声明层自洽 ⇒ **discharged（声明层）**，但运行时保证依赖 L1/L2（Iter07 open ⇒ 实际 open）。
- **确定性**：需 Delta 计算确定性 ⇒ 依赖 E3 的 Delta 正确性（open）。状态 = open。
- **(PO-I6-d) 效应仅在同步点（open）**：需证 Domain 层 0 效应 + Shell 仅 Sync Point 触发效应。Domain 0 效应依赖 §4 System 纯函数（Iter05 I5-05 open）+ §1 DO-2（Domain 不引用 Godot，RULE001 强制，Iter07 open）。链上每环均 open ⇒ 该约束 open。

**文档行号**：§5.1.2（L296-304）、SH-001（L321）、DO-2（L14）、Iter07。

---

## E3. §5.1.3 Delta Sync 增量正确性（核心缺口）

**命题** `Delta := (Spawned: Set<EntityId>, Modified: Map<EntityId, Set<ComponentType>>, Destroyed: Set<EntityId>)`（L313-315）。

**数学性质 / 证明状态**：
- **(PO-I6-e) 增量一致性判据缺失（open，高/核心，阻断 DO-10）**：需证 `apply(SceneTree, Delta(World, World'))` 使 SceneTree 与 World' 一致（同构/等价）。文档仅给 Delta 数据结构，**无一致性判据、无前条件、无证明**。
  - 具体悬空：① World' 与 SceneTree 当前状态差异如何精确计算（全量 diff？需旧 World 快照）；② Spawned/Modified/Destroyed 三者的**不相交性**未证（同一 Entity 同时 Spawn 且 Destroy 的竞态）；③ 嵌套 Component 变更依赖 SH-003「COMP002 禁止嵌套」（该禁令本身依赖 L2 强制，Iter07 open）。
  - 状态 = open（核心）。Delta Sync 正确性不成立 ⇒ DO-10「Shell 同态映射无决策」的"同态"无数学支撑。
- **(PO-I6-f) 三集合不相交性/幂等性未证（open）**：若同步中途 World 再变，Delta 三集合可能重叠 ⇒ 非幂等同步。需 `Spawned ∩ Destroyed = ∅` 等不变量，未证。状态 = open。

**文档行号**：§5.1.3（L306-315）、DO-10（L22）、SH-003（L323）。

---

## E4. SH-001..005 收敛真伪

| ID | 文档状态 | 实际审计状态 | 说明 |
|----|---------|-------------|------|
| SH-001 无决策类型保证 | 已收敛 | **open/asserted** | L1 sealed + L2 AST 禁构造 + L3 兜底；L2/L3 完备性未证（Iter07）→ 实为 asserted（依赖未形式化工具）。 |
| SH-002 无状态 vs Node mutable | 已收敛 | **discharged（声明层）** | 「独立状态禁止 / 缓存覆盖写入允许」语义区分成立（定义层）。运行时保证依赖 L1/L2（Iter07 open）。 |
| SH-003 嵌套变更遗漏 | 已收敛 | **open** | 仅禁止嵌套 Component（COMP002），但依赖该禁令被 L2 强制（Iter07 open）→ 禁令可能漏检深层嵌套。 |
| SH-004 Domain/Shell 签名合并 | 已收敛 | **asserted** | 「预算声明 vs 实际执行，运行时校准」——校准逻辑在 §9，偏差>20% 才报警，存在「声明与执行长期偏差不报」窗口（Iter11 RT-002/004）。 |
| SH-005 输入效应归属 | 已收敛 | **discharged（声明层）** | Claim 归 Shell.GatherInput，Domain.Input 0 效应——声明自洽，依赖 DO-2 成立（Iter07 open ⇒ 实际弱 open）。 |

---

## E5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 Domain 范畴与 Godot 范畴均良定义、Shell 映射全且保单位元与复合，则 Shell 为函子。证明：标准函子定义。前提 PO-I6-a/b/c 未立 ⇒ 条件证明，实际未消解。
- **P2（discharged，条件）**：给定 Delta 三集合两两不交且覆盖 World→World' 的全部变更，单次 `apply` 使 SceneTree 与 World' 一致。证明：集合划分 ⇒ 无竞态。前提 PO-I6-f（不相交）未立 ⇒ 条件。
- **P3（discharged）**：SH-002 的语义区分（独立状态 vs 覆盖写入）在声明层自洽，无内部矛盾。证明：定义清晰。

---

## Proof Obligation 账本（Iter06）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I6-a | Domain/Godot 范畴结构 | open | 构造两范畴 | L281-294 |
| PO-I6-b | 函子律 id/comp 守恒 | open(高) | 证单位元/复合守恒 | L281-294 |
| PO-I6-c | 映射全/双向 | open | 补全映射+逆向 | L281-294, §7 |
| PO-I6-d | 效应仅 Sync Point | open | 见 Iter05/Iter07 链 | L303, DO-2 |
| PO-I6-e | Delta 一致性判据 | open(核心) | 定义+证一致性 | L313-315, DO-10 |
| PO-I6-f | Delta 三集合不交/幂等 | open | 证不相交 | L313-315 |

## 本轮新发现未消解缺口（I6- 前缀，全局唯一）
- **I6-01**：「Shell 作为函子」是比喻，非范畴论函子；范畴未定义、函子律未证、映射非全单向。
- **I6-02**：Delta Sync 仅定义数据结构，无一致性判据/前条件/证明 ⇒ 增量同步正确性 open（核心，阻断 DO-10）。
- **I6-03**：Delta 三集合（Spawned/Modified/Destroyed）不相交性与幂等性未证，中途变更有竞态。
- **I6-04**：SH-001/003/004「已收敛」依赖未形式化工具（Iter07）/未定义校准紧度（Iter11），实为 asserted/open。
- **I6-05**：「效应仅在 Sync Point」整条依赖链（System 纯函数 + DO-2 + L2/L3）每环 open ⇒ 该约束 open。
- **I6-06**：§5.1.1 态射仅 3 例且全单向，World→SceneTree 是增量非同构，「同态映射」表述过强。
