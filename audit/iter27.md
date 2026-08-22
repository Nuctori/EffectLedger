# Iter27 审计 — `QueueFree()` 深审：mode=move 致 net 公式漏算释放，DO-9 泄漏检测失效（独立审计 #27，hy3 单独进程，本轮重跑）

- **审计视角**：释放动作的代数标注与 net 守恒的精确关系（独立 pass #27，全新上下文）
- **范围**：§7.1 QueueFree（L429）、§7.1 AddChild（L427）、§3.3.1 net（L163-165）、§3.2.3 Compatible（L131-138）、§1 DO-9（L21）；邻接 Iter08 I8-01（QueueFree mode=move）、Iter24 I24-02（move 在 net 计正项）、Iter15 I15-06（net 无 scope）、Iter23 I23-03（释放 mode 分裂）
- **结论摘要**：QueueFree 是 Godot 中节点释放的**标准路径**（与 AddChild 配对），但 §7.1（L429）将其释放动作标 `mode=move`（非 release）：`{ release(tree, self.id, move, shell_scope), release(memory, self.size, move, shell_scope) }`。§3.3.1 的 `net(S)` 只对 `mode∈{release}` 计负项，move 与 create 同置正侧（Iter24 D2）。故 QueueFree 的释放**不计入 net 的负项**，AddChild(+size) 后 QueueFree(+size，因 move 正侧) ⇒ net = +2·size 而非回零 ⇒ 节点占用被双重累计、释放静默漏算 ⇒ DO-9「Instantiate/AddChild 无对应释放路径才报警」**数学失效**（审计系统永远显示占用在涨，无法识别「有 AddChild 无 QueueFree」的真泄漏 vs 「有 AddChild 有 QueueFree」的假泄漏）（open，高，安全）。这是 Iter01-26 中**安全最关键**的一条——泄漏检测是系统的头号存在理由。结构性成立（把 QueueFree 改 `mode=release`，或把 move 归约为 release+create）给条件证明。

---

## G1. 命题：QueueFree mode=move，net 公式不识别释放

**命题**（§7.1 L429）：`QueueFree() = { release(tree, self.id, move, shell_scope), release(memory, self.size, move, shell_scope) }`。
**命题**（§3.3.1 L163-165）：`net(S) = Σ_{occupy, mode∈{create,move}} size − Σ_{occupy, mode=release} size`。

求值（节点经 AddChild 占用 `occupy(tree,id,create)` 与 `occupy(memory,size,create)`，后 QueueFree）：
- AddChild：`+size(tree)` `+size(memory)`（create 正侧）。
- QueueFree：`+size(tree)` `+size(memory)`（move 正侧，非 release 负侧）。
- `net = (+size) + (+size) + (+size) + (+size) = +4·size`（两种资源各被双计）。

正确期望：AddChild(+2) 后 QueueFree 应 −2 ⇒ net 回 0。当前 net 恒偏正 ⇒ **QueueFree 释放被完全漏算**。

**数学性质 / 证明状态**：
- **(PO-I27-a) QueueFree move 致 net 漏算释放（open，高，安全）**：net 公式的 release 负侧仅认 `mode=release`，QueueFree 用 move ⇒ 释放不抵正项。状态 = open（高，安全——DO-9 失效）。
- 交叉：Iter08 I8-01、Iter24 I24-02（同 root，本项给精确数值反例）。

**文档行号**：§7.1（L429）、§3.3.1（L163-165）、§1 DO-9（L21）。

---

## G2. 命题：泄漏检测失去判别力

**命题**（§1 DO-9 L21）：「`Instantiate`/`AddChild` 无对应释放路径 ⇒ 静态报警（泄漏）」。

因 net 恒偏正（G1），审计系统无法区分：
- **真泄漏**：AddChild 后**无** QueueFree ⇒ net = +2·size（占用未释放）。
- **假泄漏**：AddChild 后**有** QueueFree ⇒ net = +4·size（释放被当占用）。

二者 net 均 > 0 ⇒ 系统对两种情况都报「占用在涨」，但无法区分「真未释放」与「已释放但被漏算」。即 DO-9 的**判别基准（net 应回零表示已释放）被破坏** ⇒ 报警要么全误报（把已释放当泄漏）、要么阈值需调到 +4·size 量级（使真泄漏也被淹没）。

**数学性质 / 证明状态**：
- **(PO-I27-b) DO-9 判别基准破坏（open，高，安全）**：DO-9 依赖「占用 create 未配 release ⇒ net>0」判泄漏，但 move 使「已配释放」也 net>0 ⇒ 判别失效。状态 = open（高，安全）。
- 交叉：Iter15 I15-06（net 无 scope 使局部失配被全局掩盖，叠加 G1 更糟）、Iter17 I17-04（net<0 未定义，本项反向：net 永远不回零故 net<0 永不出现，但这是计量错误非真无泄漏）。

**文档行号**：§1 DO-9（L21）、§3.3.1（L163-165）、Iter15（I15-06）、Iter17（I17-04）。

---

## G3. 命题：释放 mode 标注分裂（QueueFree move vs RemoveChild release）

**命题**（§7.1 L428）：`RemoveChild(node) = { ..., occupy(tree, node.id, release, shell_scope) }` —— 用 `release`。
**命题**（§7.1 L429）：`QueueFree() = { ..., release(..., move, ...) }` —— 用 `move`。

同一语义「释放节点」在两个 API 用两个 mode。RemoveChild 的 release 能被 net 正确计负（若 resource 同），QueueFree 的 move 不能 ⇒ **同是释放、代数命运不同**。

**数学性质 / 证明状态**：
- **(PO-I27-c) 释放语义 mode 分裂（open，高）**：文档对「释放」无统一 mode，net 公式只认 release ⇒ QueueFree 路径泄漏检测系统性失效，RemoveChild 路径正常。状态 = open（高，交叉 Iter23 I23-03）。
- 附注：Godot 语义上 QueueFree 与 RemoveChild 都释放节点，标注不一致属设计缺陷。

**文档行号**：§7.1（L427-429）、§3.3.1（L163-165）。

---

## G4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若把 QueueFree 的释放 Claim 改 `mode=release`（即 `release(tree, self.id, release, shell_scope)` 等），则 net 公式 `mode=release` 计负 ⇒ AddChild(+2)+QueueFree(−2)=0，G1 漏算消解、DO-9 判别恢复。证明：标注对齐公式。前提 PO-I27-a/c（标注未改）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若保留 move 但定义 `move ≜ release(旧)+create(新)`（Iter24 D3），且 net 对归约后 Claim 求和（release 负、create 正，回收器临时持有后释放最终归零），则 QueueFree 占用守恒。证明：归约使 move 无独立正侧。前提 Iter24 PO-I24-a 未立 ⇒ 条件。
- **P3（discharged，条件）**：若 `net` 改为 `net(S, scope)`（Iter37）并按 per-node 分组（resource 细化到 node.id），则「AddChild(id)+QueueFree(id)」同 id 配对可见，即便全局 net 偏正，局部 id 粒度也能暴露缺 QueueFree 的真泄漏。证明：粒度细化。前提 Iter37 PO-I37 未立 ⇒ 条件。
- **P4（discharged）**：在「QueueFree 不计入审计（仅 AddChild 算占用、靠运行期采样回零）」的规避方案下，DO-9 退化为运行期检测（Iter11 Deviation）。证明：职责转移运行期。但静态审计层仍失效（与 DO-9「静态报警」目标冲突）。

---

## Proof Obligation 账本（Iter27）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I27-a | QueueFree move 致 net 漏算释放 | open(高,安全) | 改 mode=release 或归约 move | L429, L163-165 |
| PO-I27-b | DO-9 判别基准破坏（真/假泄漏不可分） | open(高,安全) | 见 P1/P3 | L21, L163-165 |
| PO-I27-c | 释放 mode 分裂(QueueFree move vs RemoveChild release) | open(高) | 统一释放 mode | L427-429 |

## 本轮新发现未消解缺口（I27- 前缀，全局唯一）
- **I27-01（高，安全）**：QueueFree mode=move 使 net 公式（release 计负）不识别释放 ⇒ AddChild(+size)+QueueFree(+size，move 正侧)=+2size 而非回零 ⇒ 释放静默漏算（交叉 Iter08 I8-01 / Iter24 I24-02）。
- **I27-02（高，安全）**：DO-9 依赖「create 未配 release⇒net>0」判泄漏，但 move 使「已释放」也 net>0 ⇒ 真泄漏(无 QueueFree) 与假泄漏(有 QueueFree) 不可分 ⇒ 报警全误报或阈值失效。
- **I27-03（高）**：QueueFree(move) 与 RemoveChild(release) 同语义「释放」两 mode 标注分裂 ⇒ 同是释放代数命运不同，泄漏检测系统性偏袒 RemoveChild 路径（交叉 Iter23 I23-03）。
- **I27-04（弱）**：若 net 加 per-node scope 分组(Iter37)，即便全局 net 偏正，局部 id 粒度仍能暴露缺 QueueFree 真泄漏——但需 net(scope) 先立（交叉 Iter15 I15-06）。

---

一句话摘要：QueueFree mode=move 致 net 公式（仅 release 计负）不识别释放、AddChild(+size)+QueueFree(+size，move 正侧) 双计使释放漏算（I27-01，高安全，交叉 Iter08/Iter24），DO-9 判别基准破坏、真/假泄漏不可分（I27-02，高安全），且 QueueFree(move) 与 RemoveChild(release) 释放语义两标注分裂（I27-03，高）——**泄漏检测是系统头号存在理由，此缺口为安全最关键**，需 PDR 侧改 QueueFree mode=release 或归约 move 并补 net(scope) 分组（Iter15/Iter37）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter27.md，未读/改其它 audit 文件，聚焦 QueueFree net 漏算，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #27（hy3 单独进程，本轮重跑）」、G1-G4 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I27- 缺口列表；交叉引用真实行号(L429/L427/L163-165/L21) 并经 read 确认 §7.1/§3.3.1/§1 真实文本"}
  ],
  "changedFiles": ["audit/iter27.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 425, 6)", "result": "passed", "summary": "读取 §7.1 QueueFree/AddChild/RemoveChild 真实 mode 标注"},
    {"command": "read PDR (offset 163, 5) + (offset 19, 3)", "result": "passed", "summary": "读取 §3.3.1 net 公式与 §1 DO-9 泄漏检测目标"},
    {"command": "write D:/Godot/Cosmos/audit/iter27.md", "result": "passed", "summary": "覆盖写入独立审计 #27"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 G1-G4 四节 + Proof Obligation 账本(G3 项) + 4 条 I27- 缺口", "交叉引用 §7.1/§3.3.1/§1 DO-9/Iter08/Iter24/Iter15/Iter37/Iter23 真实行号"],
  "residualRisks": ["未运行 Analyzer 验证 net 公式实际代码对 move 的正负侧处理（仅基于 §3.3.1 文本推导）", "回收器临时持有是否最终归零依赖 §9 运行时未本轮读取"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter27.md，独立审计 QueueFree mode=move 致 net 漏算释放、DO-9 失效",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 QueueFree 释放漏算致泄漏检测失效(安全最关键缺口)，需 PDR 侧改 mode=release 或归约 move+net(scope)"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
