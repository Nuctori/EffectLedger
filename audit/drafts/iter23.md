# Iter23 审计 — AddChild/RemoveChild「create+release」正常生命周期被 Compatible 误判冲突（独立审计 #23，hy3 单独进程，本轮重跑）

- **审计视角**：并发安全判定的良性配对识别（独立 pass #23，全新上下文）
- **范围**：§7.1 AddChild（L427）、RemoveChild（L428）、QueueFree（L429）；§3.2.3 Compatible（L131-138）第1-4析取 + 注释「create+create, move+move 不兼容」；§3.2.2 并行组合约束（L126-130）；§1 DO-9（L21）；邻接 Iter16 I16-03（create+release 误判）、Iter08 I8-01（QueueFree mode=move）
- **结论摘要**：AddChild 产生 `create` 型 Claim、RemoveChild 产生 `release` 型 Claim，二者是 Godot 节点管理最基础的**生命周期配对**（先加后移除，或先创建后复用）。但 §3.2.3 的 4 条析取与 1 行注释**均未将 `(create,release)` / `(release,create)` 列为兼容**——它们既不匹配任何 `(·,use)` 析取，也不在「create+create/move+move」不兼容列举中（注释仅列两项，未含 create+release）。严格按定义，`Compatible(create,release)=false` ⇒ AddChild 与 RemoveChild 同资源并行被兼容性检查判为**冲突**。这是**语义错误**（把良性生命周期当危险），非单纯「未定义」——即便以「else=false 宽恕读」也成立。后果：§7.1 最核心的 add/remove 配对在 `||` 或顺序组合下被反复误报冲突，DO-9 的「泄漏/冲突」信号被正常节点管理噪声淹没；若把 false 当作「阻止」则正常 RemoveChild 被过度约束（open，高）。

---

## C1. 命题：create+release 不在任何兼容析取，也不在不兼容列举 → 判冲突

**命题**（§3.2.3 L131-138）：
- 4 条析取全为 `(·, use)` 形：`(use,use)`、`(create,use)`、`(release,use)`、`(move,use)`。
- 注释仅列「不兼容：create+create, move+move, use+write(同资源)」。

**命题**（§7.1 L427/L428）：
- `AddChild(node) = { write(tree, node.id, create, shell_scope), occupy(tree, node.id, create, shell_scope) }`
- `RemoveChild(node) = { write(tree, node.id, release, shell_scope), occupy(tree, node.id, release, shell_scope) }`

求值 `AddChild || RemoveChild`（同 `tree` 同 `node.id`，触发约束）：
- `Compatible(create, release)`：m₂=release≠use ⇒ 不在 4 析取 ⇒ **false**；且注释「不兼容」未列 `create+release` ⇒ 不触发任何「已知不兼容」例外，但默认 false。
- ⇒ AddChild 与 RemoveChild 作用于同一节点被兼容性检查判为**冲突**。

**数学性质 / 证明状态**：
- **(PO-I23-a) create+release 良性配对被判冲突（open，高）**：创建后释放恰是节点正常生命周期（先 AddChild 再 RemoveChild，或不同帧对同一节点先建后拆），逻辑上应**兼容/无冲突**。文档既未将其列入兼容析取，也未在注释中列为「良性」，使非对称单向 `(·,use)` 规则默认将其归为冲突 ⇒ 语义方向错误（把安全当危险）。状态 = open（高）。
- 交叉：Iter16 I16-03 已指该误判；本项给出**完整配对链**（AddChild→RemoveChild 两个 API 的精确 Claim 集合）并论证「即便 else=false 仍误判」。

**文档行号**：§3.2.3（L131-138）、§7.1（L427、L428）、§3.2.2（L126-130）。

---

## C2. 命题：release+create（复用模式）同样误判

**命题**：真实工程中除「先加后移」外，还有「先释放旧节点、再占用同槽位」的复用模式（如对象池 `RemoveChild(old) ; AddChild(new)`，或同帧 `RemoveChild(x) || AddChild(y)` 不同节点但同 tree 资源）。

求值 `RemoveChild || AddChild`（同 tree）：
- `Compatible(release, create)`：m₁=release, m₂=create，均≠use ⇒ 不在 4 析取 ⇒ **false**。
- ⇒ 释放后立即复用的良性模式也被判冲突。

**数学性质 / 证明状态**：
- **(PO-I23-b) release+create 复用模式误判（open，高）**：对象池/槽位复用是常见良性用法，release 旧、create 新在同资源不同 id 常并发，按当前规则全部报冲突 ⇒ 误报面扩大。状态 = open（高）。
- 附注：若 RemoveChild 与 AddChild 的 `node.id` 不同（不同节点同 tree），§3.2.2 约束仅当 `c₁.resource=c₂.resource`（此处 resource 均为 `tree` 字面量）触发 ⇒ 仍触发 ⇒ 即便不同节点也因共享 `tree` 资源被判冲突（resource 粒度粗，交叉 Iter19 量化：resource 多为 `tree`/`self` 粗粒度 ⇒ 大量良性并发被粗粒度 resource 拉入冲突）。

**文档行号**：§7.1（L427、L428）、§3.2.2（L126-130）。

---

## C3. 命题：与 QueueFree mode=move 叠加，释放语义双失守

**命题**（§7.1 L429）：`QueueFree() = { release(tree, self.id, move, shell_scope), release(memory, self.size, move, shell_scope) }` —— 释放动作标 `mode=move`（非 release）。

交叉后果：
- 设计者用 `move` 表示「所有权转移/释放」（Iter08 I8-01），但 §3.2.3 的释放语义只识别 `release` 模式。`RemoveChild` 用 `release` 却被 create+release 误判冲突，`QueueFree` 用 `move` 则 `Compatible(move, release)` 也不在析取（第4析取仅 `(move,use)`）⇒ `move` 与 `release` 的配对同样 undefined/false。
- ⇒ 文档对「释放」这一语义动作给出**两种 mode 标注**（release 与 move），且二者在 Compatible 层都未被正确识别为「释放配对」⇒ 释放类操作的并发/守恒判定双失守（交叉 Iter16 I16-04、Iter08 I8-01）。

**数学性质 / 证明状态**：
- **(PO-I23-c) 释放 mode 标注分裂（release vs move）致配对规则双失守（open，高）**：同一语义「释放资源」在 RemoveChild 标 release、在 QueueFree 标 move，Compatible 对 `(create,release)` 与 `(create,move)`/`(release,move)` 均未定义兼容 ⇒ 无论哪种释放标注，与创建/释放的配对都判冲突或悬空。状态 = open（高）。
- 交叉：Iter08 I8-01（QueueFree mode=move 致 net 漏 release）、Iter16 I16-04（move 5 个配对 undefined）。

**文档行号**：§7.1（L427-429）、§3.2.3（L131-138）。

---

## C4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 Compatible 扩展为显式**良性对集合** `B := {(use,use),(use,create),(create,use),(use,release),(release,use),(create,release),(release,create),(move,use),(use,move),(move,release),(release,move)}`，仅 `(create,create)/(release,release)/(move,move)` 为假（冲突），则 create+release 与 release+create 均为良性通过。证明：枚举覆盖。前提 PO-I23-a/b/c（原 4 析取未含）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若定义 `move ≡ 释放旧所有者 + 转移给新所有者`（对旧资源 release、对新资源 create，Iter24 主题），则 `move` 归约为 release/create 组合，QueueFree 的 `move` 配对自动落入 P1 良性对 ⇒ C3 双失守消解。证明：语义归约。前提 PO-I23-c（move 语义未定义）未立 ⇒ 条件。
- **P3（discharged）**：在「仅比较 mode 忽略 resource 粒度」弱解释下，语法闭合；矛盾仅源于 Compatible 定义质量，非语法层。证明：语法完整。但语义错误（误判良性）仍存（P1 才是修复）。
- **P4（discharged，条件）**：若 resource 粒度细化到 `node.id`（而非粗 `tree`），则「不同节点并发」不触发约束，`create+release 同节点」才检查 ⇒ 误报面缩小但 `create+release 同节点` 仍误判（需 P1）。证明：resource 细化缩减触发集。前提 PO-I23-b（resource 粒度粗）未立 ⇒ 条件。

---

## Proof Obligation 账本（Iter23）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I23-a | create+release（AddChild→RemoveChild）判冲突（语义错误） | open(高) | 补生命周期良性对 | L427-428, L131-138 |
| PO-I23-b | release+create 复用模式误判 | open(高) | 补良性对+细 resource | L427-428, L126-130 |
| PO-I23-c | 释放 mode 分裂(release/move)致配对双失守 | open(高) | 统一释放 mode / 扩 move 规则 | L429, L131-138 |
| PO-I23-d | 粗 resource(tree)放大误报面 | open(中) | 细化 resource 粒度 | L427-428, §3.2.2 |

## 本轮新发现未消解缺口（I23- 前缀，全局唯一）
- **I23-01（高）**：AddChild(write/create)+RemoveChild(write/release) 同节点被 Compatible 判冲突，但二者是 Godot 最基本良性生命周期配对 ⇒ 语义错误（safe 当 unsafe），即便 else=false 仍成立。
- **I23-02（高）**：release+create 复用/对象池模式同样误判，良性并发（释放旧+占用新）全报冲突。
- **I23-03（高）**：释放语义在 RemoveChild 标 release、QueueFree 标 move，两种 mode 在 Compatible 层均未被识别为释放配对 ⇒ 释放类判定双失守（交叉 Iter08 I8-01 / Iter16 I16-04）。
- **I23-04（中）**：resource 多为粗粒度 `tree`/`self` ⇒ 不同节点并发也被拉入冲突检查，误报面被 resource 粒度放大（交叉 Iter19 量化）。
- **I23-05（弱）**：注释「create+create, move+move 不兼容」漏列 `create+release`/`release+create`，说明设计者写作时未意识到生命周期配对应良性，属设计盲区而非遗漏边角。

---

一句话摘要：AddChild(write/create)+RemoveChild(write/release) 同节点被 Compatible 判冲突，但二者是 Godot 最基础良性生命周期配对（I23-01，高，语义错误非未定义），release+create 复用/对象池亦误判（I23-02），叠加 QueueFree mode=move 使释放 mode 分裂、配对规则双失守（I23-03，交叉 Iter08 I8-01/Iter16 I16-04），粗 resource(tree) 放大误报（I23-04）——DO-9 冲突信号被正常节点管理噪声淹没，需 PDR 侧显式补生命周期良性对并统一释放 mode。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter23.md，未读/改其它 audit 文件，聚焦 create+release 良性生命周期误判，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #23（hy3 单独进程，本轮重跑）」、C1-C4 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I23- 缺口列表；交叉引用真实行号(L427-429/L131-138/L126-130/L21) 并经 read 确认 §7.1/§3.2.3 真实文本"}
  ],
  "changedFiles": ["audit/iter23.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 425, 12)", "result": "passed", "summary": "读取 §7.1 AddChild(L427)/RemoveChild(L428)/QueueFree(L429) 真实 Claim 集合"},
    {"command": "read PDR (offset 124, 15)", "result": "passed", "summary": "读取 §3.2.2 并行约束 + §3.2.3 Compatible 4 析取与注释"},
    {"command": "write D:/Godot/Cosmos/audit/iter23.md", "result": "passed", "summary": "覆盖写入独立审计 #23"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 C1-C4 四节 + Proof Obligation 账本(C4 项) + 5 条 I23- 缺口", "交叉引用 §7.1/§3.2.3/§3.2.2/DO-9/Iter16/Iter08 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 Compatible 实际实现是否把 create+release 当冲突（仅基于文档 §3.2.3 文本推导）", "node.id 同异对 resource 触发的影响依赖 §3.2.2 约束字面，未运行源码"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter23.md，独立审计 create+release 良性生命周期被 Compatible 误判冲突",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 AddChild/RemoveChild 配对误判、释放 mode 双失守，需 PDR 侧补良性对并统一 mode"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
