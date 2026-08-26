# Iter08 审计 — §7.1-7.3 场景树/属性/物理 API 的 Claim 映射数学性质（独立审计 #8，hy3 单独进程，本轮重跑）

- **审计视角**：API→Claim 映射的代数性质与内部一致性（独立 pass #8，全新上下文）
- **范围**：§7.1 场景树（L421-450 内 GetNode/GetTree/AddChild/RemoveChild/QueueFree/MoveChild，L425-430）、§7.2 属性访问（L432-440，Position/GlobalPosition/Rotation/Scale 的 getter/setter，L436-440）、§7.3 物理操作（L442-450，MoveAndSlide/ApplyForce/ApplyImpulse/GetSlideCollisionCount/GetSlideCollision，L446-450）；邻接 §3.1.1 Claim 字段（L78-86）、§3.2.3 Compatible（L133-141）、§3.3.1 net（L159-163）、§3.4 MA-002（L181）、§1 DO-9（L21）
- **结论摘要**：为每条 Godot API 建立「单步 Claim 集合」并核查组合性质。核心一致性发现：(1) **QueueFree 用 mode=move 标注释放动作**（L429），与 §3.2.3 `Compatible` 注释「move+move 不兼容」冲突，且 §3.3.1 net 公式按 `mode=release` 计负项——此处 mode=move 使 net **不把该释放计入负项**，⇒ 泄漏检测（DO-9）失效（高，open）；(2) **self 资源裸称 vs §3.1.2 `Self(component)` 构造子不一致**（跨 API 去重/冲突精度，open）；(3) 重复 AddChild 被 `create+create 不兼容` 误报（Godot 允许多同名子节点，open 精度）；(4) AddChild(create)+RemoveChild(release) 的跨 mode 兼容规则缺失（§3.2.3 未列 create∧release，open）；(5) Position/Rotation/Scale 共享 `"transform"` 资源，粒度粗（open 精度）；(6) 属性 setter 标 `write(self,..,use)`——kind=write 与 mode=use 的正交耦合规则未定义（open）；(7) 物理 `physics(body_id:RID)` 编译期非常量 RID 退化为 Unknown（MA-010，open 精度）。另：§3.2.3 注释用「use+write(同资源)」中「write」非 mode 枚举词（mode={use,create,release,move}），与 §3.1.1 定义冲突（交叉 Iter02 I2-08 / Iter16）。结构性成立的部分（单步 Claim 集合为合法 Signature 元素、MoveAndSlide 同 body_id 的 read+write 经 Compatible(use,use) 兼容）给出条件证明。

---

## G1. §7.1 场景树操作映射性质

| Godot API | Claim 集合（摘要，L425-430） | 代数性质 / 一致性检查 |
|-----------|------------------|---------------------|
| `GetNode(path)` | `{ read(tree, path, use, shell) }` | 单 read，幂等（重复 GetNode ∪ 去重后不变，依赖 Claim 相等归一化，Iter01 I1-02）。性质：read 中性、可加（计数）。discharged（在 Claim 相等良定义下）。 |
| `GetTree()` | `{ read(tree, "root", use, shell) }` | 同 GetNode 特例（path="root"）。discharged。 |
| `AddChild(node)` | `{ write(tree, node.id, create, shell), occupy(tree, node.id, create, shell) }` | **写+占同时 create**：`write(tree,..,create)` + `occupy(tree,..,create)`。对同 (tree,node.id) 再 AddChild（重复子节点，Godot 允许）→ `create+create`，§3.2.3 称不兼容 ⇒ 报冲突。**但重复 AddChild 在 Godot 是合法**（同名多子），映射保守误报（R-3 来源）。状态 = open（精度）。 |
| `RemoveChild(node)` | `{ write(tree, node.id, release, shell), occupy(tree, node.id, release, shell) }` | 写+占同时 release。与 AddChild 的 create 配对时 `create(tree)+release(tree)` 经 Compatible？§3.2.3 仅列 `release∧use` 兼容，未列 `create∧release` ⇒ **不明**（open，见 G4 / Iter16）。 |
| `QueueFree()` | `{ release(tree, self.id, move, shell), release(memory, self.size, move, shell) }` | **mode=move 而非 release**（关键不一致，见 G3 / I8-01）。 |
| `MoveChild(node, index)` | `{ write(tree, node.id, use, shell) }` | 仅 write use；移动不改占用量，合理。AddChild(create) 后 MoveChild(use) 同节点：`Compatible(create,use)=true`（§3.2.3 第2析取）⇒ 兼容。discharged（条件）。 |

- **构成性（组合）示例**：
  - `AddChild`(create) `; MoveChild`(use) 同节点：create∧use 兼容 ⇒ 安全。✅
  - `AddChild`(create) `; AddChild`(create) 同节点：create∧create 不兼容 ⇒ 报警。但 Godot 合法 ⇒ **误报**（I8-03）。
  - `QueueFree`(move) `; GetNode`(use) 同 tree：move∧use 兼容（§3.2.3 第4析取）⇒ 判「转移后使用安全」。但 QueueFree 是**释放**，释放后再使用已 freed 对象应**不安全** ⇒ **错误兼容**（I8-01，安全相关）。

## G2. §7.2 属性访问映射性质

| API | Claim 集合（L436-440） | 性质/一致性 |
|-----|------------------|------------|
| `Position` getter | `{ read(self, "transform", use, shell) }` | read use，幂等。discharged（结构）。 |
| `Position` setter | `{ write(self, "transform", use, shell) }` | **kind=write 但 mode=use**（非 create/release/move）。语义上 use=「使用已有」、write=「改值」——两套维度（kind=读写类型，mode=占用动作）正交但未定义耦合（见 G5 / I8-06）。结构合法（Claim 字段取值合法），但组合语义未定义。 |
| `GlobalPosition` getter | `read(self,..,use) ∪ read(tree, parent_path, use)` | 跨 self+tree 两资源读取。组合时与 GetNode(parent) 的 read(tree,parent) 可能重复（归一化问题，Iter01 I1-02）。 |
| `Rotation`/`Rotation` getter/setter | `read/write(self, "transform", use)` | 同 Position。Rotation/Scale 与 Position **共享 `"transform"` 资源** ⇒ 读 Position 后写 Scale 对同资源不同组件：`Compatible(use,use)=true`（§3.2.3 第1析取）⇒ 兼容。合理但暗示「transform 资源」粒度粗（I8-05）。 |
| `Scale` getter/setter | `read/write(self, "transform", use)` | 同上。 |

## G3. §7.3 物理操作映射性质

| API | Claim 集合（L446-450） | 性质/一致性 |
|-----|------------------|------------|
| `MoveAndSlide()` | `read(physics, body_id, use) ∪ write(physics, body_id, use) ∪ read(tree, "collision_shapes", use)` | 同 body 既 read 又 write，mode 均 use ⇒ `Compatible(use,use)=true`（§3.2.3 第1析取）⇒ 兼容。✅ discharged（条件：Claim 相等）。 |
| `ApplyForce(force)` / `ApplyImpulse(impulse)` | `write(physics, body_id, use)` | 单写。与 GetSlideCollision(read use) 同 body：`Compatible(use,use)=true` ⇒ 兼容。✅ |
| `GetSlideCollisionCount()` / `GetSlideCollision(index)` | `read(physics, body_id, use)` | 单读。 |
| 物理资源粒度 | `physics(body_id: RID)` | RID 是 Godot 运行时标识，编译期静态分析需常量 RID ⇒ 非常量时 MA-010 Unknown 保守（Iter01 I1-03）。**精度 open**。 |

---

## G4. QueueFree 的 mode=move 不一致（核心，高）

**命题** §7.1 L429：`QueueFree() = { release(tree, self.id, move, shell), release(memory, self.size, move, shell) }`。

**数学性质 / 证明状态**：
- 语义上 QueueFree 是**释放**（free 节点、回收内存），但 mode 标为 **move**（§3.1.1 move=「转移」）。这与 §3.2.3 注释「move+move 不兼容」直接冲突：连续两次 QueueFree（同一对象被多处释放，典型泄漏/误用）→ `move(tree)+move(tree)` ⇒ 不兼容 ⇒ 报警。
  - 但 §3.3.1 net 公式把 release 计入负项（`-Σ release.size`），而此处释放动作的 mode 却是 move ⇒ **net 计算是否识别该 release？** 若 net 按 `mode∈{release}` 归属负项，则 QueueFree 的 `release(..,move)` 因 mode≠release 而**不被 net 当作释放** ⇒ net 漏算释放量 ⇒ 泄漏检测（DO-9）失效。
  - **(PO-I8-a) QueueFree 的 release 动作 mode=move 与 net/release 语义冲突（open，高）**：使 DO-9 泄漏检测的 net 公式错过该释放。交叉 Iter17 / Iter03。状态 = **open（高，阻断 DO-9 数学良定义）**。
- 同理 `Disconnect`（§7.5，后续审计）用 release mode，而 QueueFree 用 move——**同类动作（释放）mode 不统一** ⇒ Compatible/兼容性判定依赖 mode 一致性，混合标注致误判。状态 = open（交叉 Iter09 I9-02）。

**文档行号**：§7.1 L429、§3.2.3 L133-141、§3.3.1 L159-163、§1 DO-9 L21。

## G5. self 资源归一化（交叉缺口）

**命题** 多处用 `resource=self`（QueueFree、Position getter/setter、Connect、Draw*、Play 等），但 §3.1.2 ResourceId 无 `Self` 的统一标识——`Self(component: String)`（L92）是带 component 字符串的构造子，而映射中 `self` 常省略 component（如 `self.id`/`self.size`/`self,"transform"`）。**(PO-I8-b) self 的 ResourceId 表示不统一（open）**：`self` 裸称 vs `Self(component)` 构造子，跨 API 的 self 是否同一资源未定义 ⇒ ∪ 去重/冲突检测精度缺（Iter01 I1-02 同根）。状态 = open。

## G6. 可消解 proof obligation（履行尝试）

- **P1（discharged，条件）**：给定 mode 标注正确（QueueFree 修正为 release mode），则 `AddChild`(create) 与 `RemoveChild`(release) 对同一 tree 节点的配对在 net 上抵消、在 Compatible 上需补 `create∧release` 规则（Iter16）。证明：net 公式线性，配对时 `Σcreate·size=Σrelease·size ⇒ net=0`。前提 PO-I8-a 未立 ⇒ 条件。
- **P2（discharged，条件）**：单步 API 的 Claim 集合在正确 mode 下构成合法 Signature（`Set<Claim>` ∪ 元素）。证明：字段取值合法（kind/mode/scope 均属枚举）。前提：Claim 相等/归一化立（Iter01）。⇒ 条件。
- **P3（discharged）**：`MoveAndSlide` 同 body_id 的 read+write 因 `mode=use` 均满足 `Compatible(use,use)`，组合兼容。证明：§3.2.3 第1析取。无需额外前提（结构成立）。

---

## Proof Obligation 账本（Iter08）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I8-a | QueueFree release 标 move 冲突 net | open(高) | 修正 mode=release 或扩 net 识别 | L429, L159-163 |
| PO-I8-b | self 资源表示统一 | open | 定义 self↔Self(component) | L429,436-440, L92 |
| PO-I8-c | 重复 AddChild 误报 | open(精度) | Compatible 允许多子 | L427, L136-141 |
| PO-I8-d | AddChild+RemoveChild 跨 mode 兼容 | open | 补 create∧release 规则 | L427-428, L133-141 |
| PO-I8-e | transform 资源粒度粗 | open(精度) | 细化资源粒度 | L436-440 |
| PO-I8-f | write kind + use mode 耦合规则 | open | 定义 kind×mode 耦合 | L437, L80-82 |
| PO-I8-g | 物理 RID 非常量→Unknown | open(精度) | 见 MA-010 | L446-450, L189 |

## 本轮新发现未消解缺口（I8- 前缀，全局唯一）
- **I8-01（高）**：QueueFree 用 mode=move 标注释放动作，与 §3.2.3「move+move 不兼容」冲突，且使 §3.3.1 net 公式漏算该释放 ⇒ DO-9 泄漏检测可能失效（安全相关）。
- **I8-02**：self 资源在映射中裸称与 §3.1.2 `Self(component)` 构造子不一致，跨 API 去重/冲突精度缺。
- **I8-03**：重复 AddChild 被 `create+create 不兼容` 误报（Godot 允许多同名子节点）。
- **I8-04**：AddChild(create)+RemoveChild(release) 的跨 mode 兼容规则缺失（§3.2.3 未列 create∧release）。
- **I8-05**：Position/Rotation/Scale 共享 `"transform"` 资源，资源粒度粗，独立修改被当同资源冲突/兼容混淆。
- **I8-06**：属性 setter 标 `write(..,use)`——kind=write 与 mode=use 的正交耦合规则未定义（交叉 Iter16）。
- **I8-07**：物理 `physics(body_id:RID)` 编译期非常量 RID 退化为 Unknown ⇒ MA-010 保守（Iter01 I1-03）。
- **I8-08**：§3.2.3 注释「use+write(同资源)不兼容」中「write」非 mode 枚举词（mode={use,create,release,move}）→ 注释与 §3.1.1 定义冲突（交叉 Iter02 I2-08 / Iter16）。

---

一句话摘要：§7.1-7.3 的 API→Claim 映射在结构层合法，但 QueueFree 的 mode=move 标注（I8-01）使净变化公式漏算释放、威胁 DO-9 泄漏检测，且 self 资源表示/重复 AddChild/跨 mode 兼容/transform 粒度/kind×mode 耦合/RID→Unknown 共 7 处一致性缺口均为 open。
