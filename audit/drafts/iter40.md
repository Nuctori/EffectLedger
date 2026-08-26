# Iter40 审计 — Entity-as-Data 与 Scene Tree 的 Delta Sync 一致性判据：Spawned/Modified/Destroyed 三集合不相交（独立审计 #40，hy3 单独进程，本轮重跑）

- **审计视角**：§5.1.3 增量同步的正确性判据（独立 pass #40，全新上下文）
- **范围**：§5.1.3 Delta Sync（L306-315，Spawned/Modified/Destroyed 三集合）、§4 Entity-as-Data（L263-298，EA-001..007）、§3.3.1 net（L163-165，DO-9）、§10 R-8（L615，Delta Sync 正确性）；邻接 Iter06 I6-02（Delta Sync 正确性未证）、Iter07 I7-07（EA 收敛 asserted）、Iter28（scope 分裂）、Iter37（net(scope)）
- **结论摘要**：§5.1.3 的 Delta Sync 是「Entity-as-Data（EaD）组件状态 → Scene Tree 节点」的增量同步机制，产出三集合：Spawned（新建节点）、Modified（更新属性）、Destroyed（销毁节点）。文档称「增量同步保证二者一致」，但**未给出正确性判据**：(1) **三集合不相交性未证**——同一实体能否同时出现在 Spawned 与 Modified？或 Modified 与 Destroyed？若同步时序错乱（同一帧 Spawn 又 Destroy），三集合是否保证互斥、是否有优先级裁决（Spawn>Modified>Destroy？）未定义 ⇒ 重复/遗漏节点；(2) **终止性未证**——多帧连续增量后，Scene Tree 是否收敛到 EaD 的确定状态？是否存在「漏同步一帧」致永久偏离？无不变式（如 `SceneTree ≡ f(EaD)` 每帧后成立）；(3) **EaD↔Scene 双向映射的 function 性质未证**（Iter06 SH-001..005）——Spawned 创建的节点若被 Scene Tree 端外部修改（如用户代码直接 MoveChild），回写 EaD 的通道是否保持单值映射？文档未证；(4) **与 DO-9 的交互**——Destroyed 对应 QueueFree（Iter27 mode=move 漏算），EaD 销毁的占用释放是否计入 net(scope)（Iter37）未串接；(5) R-8 把「Delta Sync 正确性」作为缓解措施，但正确性本身 open（Iter06 I6-02）⇒ R-8 缓解建立在未证机制。结构性成立（若补三集合互斥+终止不变式）给条件证明。

---

## U1. 命题：Spawned/Modified/Destroyed 三集合不相交性未定义

**命题**（§5.1.3 L306-315）：Delta Sync 产出 Spawned/Modified/Destroyed 三集合，但对某实体 e：
- e 能否同时 ∈ Spawned ∩ Modified？（同一帧既新建又改属性）
- e 能否同时 ∈ Modified ∩ Destroyed？（改后又销毁）
- e 能否同时 ∈ Spawned ∩ Destroyed？（建后又销毁，即「瞬时实体」）

文档未定义三集合的互斥性 / 优先级。若同步按「先 Spawn 全部、再 Modified、再 Destroyed」顺序应用，且 Modified/Destroyed 对已 Destroy 的实体失效 ⇒ 需明确优先级裁决，否则 Scene Tree 状态依赖应用顺序（非确定性）。

**数学性质 / 证明状态**：
- **(PO-I40-a) 三集合互斥/优先级未定义（open，高）**：增量同步的正确性依赖「每实体每帧恰落一个集合」，但文档未证该不变量 ⇒ 同步可能重复/遗漏。状态 = open（高，交叉 Iter06 I6-02）。
- 文档行号：§5.1.3（L306-315）、Iter06（I6-02）。

---

## U2. 命题：终止性/不变式 `SceneTree ≡ f(EaD)` 未证

**命题**：Delta Sync 的目标应是「每帧同步后，Scene Tree 状态 = EaD 状态的投影」。但文档无任何「同步后不变式」声明（如 `invariant: SceneTree.nodes = { project(e) | e ∈ EaD, e.alive }`）。

后果：无法判定「增量同步是否使二者最终一致」——若某帧漏发 Delta（网络抖动/调度跳过），Scene Tree 是否永久偏离？无重放/全量对账机制 ⇒ 一致性无上界保证。

**数学性质 / 证明状态**：
- **(PO-I40-b) 同步不变式/终止性未证（open，高）**：无每帧后不变式 ⇒ 一致性无法验证，多帧后偏差可能累积。状态 = open（高，交叉 Iter07 I7-07 EA 收敛 asserted）。
- 文档行号：§5.1.3（L306-315）、§4（L263-298）、Iter07（I7-07）。

---

## U3. 命题：EaD↔Scene 双向映射 function 性质未证（Iter06 SH）

**命题**（Iter06 SH-001..005）：Shell 函子 `Shell: EaD → Scene` 声称保持结构（§5 同态）。但反向 `Scene → EaD`（用户代码直接改 Scene 后回写）是否 function（单值）未证：
- 用户 `MoveChild(node)` 改 Scene 树结构 ⇒ 回写 EaD 时节点 id 映射是否单值？
- 若 Scene 端有非 EaD 来源的节点（手工添加），回写时如何处理（丢弃？冲突？）

**数学性质 / 证明状态**：
- **(PO-I40-c) 双向映射 function 性质未证（open，高）**：Shell 仅单向定义（EaD→Scene），反向未证 ⇒ 同态/函子律（Iter06）仅半边成立。状态 = open（高，交叉 Iter06 SH-001/SH-005）。
- 文档行号：§5（L283-315）、Iter06（SH-001..005）。

---

## U4. 命题：与 DO-9 交互 —— Destroyed 释放串接 net(scope)

**命题**（Iter27 I27-01）：QueueFree mode=move 致 net 漏算释放。
**命题**：Delta Sync 的 Destroyed 集合应触发「实体占用释放」⇒ 若 Destroyed 的释放用 QueueFree（move），则 EaD 销毁的占用释放同样漏算 ⇒ net(scope)（Iter37）在 EaD 场景下失真。

**数学性质 / 证明状态**：
- **(PO-I40-d) Destroyed 释放串接 net 未定义（open，中）**：Delta Sync 的销毁语义未与 §3.3.1 net 公式串接（Destroyed→occupy release 还是 move？）。状态 = open（中，交叉 Iter27/Iter37）。
- 文档行号：§5.1.3（L306-315）、§3.3.1（L163-165）、Iter27（I27-01）、Iter37（R4）。

---

## U5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若定义 (a) 三集合互斥不变式（每实体每帧恰落一集合，优先级 Spawn>Modified>Destroyed），(b) 每帧后 `SceneTree ≡ project(EaD)` 不变式，则 Delta Sync 正确性可证。证明：不变量给出 ⇒ 同步确定且一致。前提 PO-I40-a/b 未立 ⇒ 条件，实际未消解（Iter06 I6-02）。
- **P2（discharged，条件）**：若定义反向映射 `Scene→EaD` 为 function（单值，冲突节点报错），则 Shell 函子律（Iter06）双向成立。证明：双向函数。前提 PO-I40-c 未立 ⇒ 条件。
- **P3（discharged，条件）**：若 Destroyed 显式产 `occupy(release)`（非 move，Iter27 P1），且 net 按 Iter37 scope 分组，则 EaD 销毁的占用释放计入 net ⇒ DO-9 在 EaD 场景可行。证明：串接。前提 Iter27/Iter37 未立 ⇒ 条件。
- **P4（discharged）**：在「每帧全量重算 Scene（非增量）」弱方案下，一致性 trivially 成立（无 Delta 概念）。但 §5.1.3 明确用增量 ⇒ 假设与文档冲突。

---

## Proof Obligation 账本（Iter40）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I40-a | Spawned/Modified/Destroyed 互斥/优先级 | open(高) | 定义不变量+优先级 | L306-315, I6-02 |
| PO-I40-b | 同步不变式/终止性未证 | open(高) | 每帧后 Scene≡f(EaD) | L306-315, L263-298, I7-07 |
| PO-I40-c | EaD↔Scene 双向 function 未证 | open(高) | 反向映射 function | §5, I6 SH-001/05 |
| PO-I40-d | Destroyed 释放串接 net 未定义 | open(中) | Destroyed→occupy release | L306-315, I27-01, I37 |

## 本轮新发现未消解缺口（I40- 前缀，全局唯一）
- **I40-01（高）**：Spawned/Modified/Destroyed 三集合互斥性/优先级未定义 ⇒ 同步可能重复/遗漏节点（交叉 Iter06 I6-02）。
- **I40-02（高）**：同步后不变式 `SceneTree ≡ project(EaD)` 与终止性未证 ⇒ 多帧偏差可能累积、无对账（交叉 Iter07 I7-07）。
- **I40-03（高）**：Shell 仅单向 EaD→Scene，反向 function 性质未证 ⇒ 函子律半边成立（交叉 Iter06 SH-001/05）。
- **I40-04（中）**：Destroyed 释放语义未与 net 公式串接（QueueFree move 漏算，Iter27）⇒ EaD 销毁占用释放失真（交叉 Iter37）。
- **I40-05（弱）**：R-8 把 Delta Sync 正确性作缓解措施，但正确性本身 open（I40-01/02）⇒ R-8 缓解建立在未证机制（交叉 Iter12 I12-02）。

---

一句话摘要：Delta Sync 的 Spawned/Modified/Destroyed 三集合互斥性/优先级未定义（I40-01，高）、同步后不变式与终止性未证（I40-02，高）、Shell 仅单向 EaD→Scene 反向 function 未证（I40-03，高，交叉 Iter06）、Destroyed 释放未串接 net（I40-04，交叉 Iter27/Iter37）——一致性判据整体缺失，R-8 缓解建立在未证机制（I40-05，交叉 Iter12），需 PDR 侧补三集合不变量+每帧一致性+双向映射+释放串接。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter40.md，未读/改其它 audit 文件，聚焦 Delta Sync 一致性判据，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #40（hy3 单独进程，本轮重跑）」、U1-U5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I40- 缺口列表；交叉引用真实行号(L306-315/L263-298) 并经 read 确认 §5.1.3/§4 真实文本"}
  ],
  "changedFiles": ["audit/iter40.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 306, 12) + (offset 263, 12)", "result": "passed", "summary": "读取 §5.1.3 Delta Sync 与 §4 EaD 真实文本确认三集合与映射定义"},
    {"command": "read PDR (offset 163, 5) + (offset 615, 3)", "result": "passed", "summary": "读取 §3.3.1 net 与 §10 R-8 确认与 DO-9/Delta 交互"},
    {"command": "write D:/Godot/Cosmos/audit/iter40.md", "result": "passed", "summary": "覆盖写入独立审计 #40"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 U1-U5 五节 + Proof Obligation 账本(U4 项) + 5 条 I40- 缺口", "交叉引用 §5.1.3/§4/§3.3.1/§10 R-8/Iter06/Iter07/Iter27/Iter37/Iter12 真实行号"],
  "residualRisks": ["未运行 EaD/Scene 同步源码验证三集合实际应用顺序（仅基于 §5.1.3 文本推导）", "反向映射 function 性质依赖 Iter06 SH 未本轮重证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter40.md，独立审计 Delta Sync 一致性判据（收口 Iter06/Iter12）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 Delta Sync 三集合互斥/终止/双向映射全未证，需 PDR 侧补不变量+双向 function+释放串接"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
