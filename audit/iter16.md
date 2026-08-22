# Iter16 审计 — §3.2.3 冲突判定 Compatible 组合子：完备性 / 对称性 / mode 语义覆盖（独立审计 #16，hy3 单独进程，本轮重跑）

- **审计视角**：冲突判定函数的数学定义 / 与并行组合交换律的相容性 / mode 语义覆盖（独立 pass #16，全新上下文）
- **范围**：§3.2.3 Compatible（L131-138）、§3.1.1 Claim 字段 mode（L79-86）、§3.2.2 并行组合（L126-130）、§7 映射各 mode 标注（L425-507，重点 QueueFree mode=move L429）、§3.4 MA-009（L189）；邻接 Iter08（I8-01 QueueFree mode=move）、Iter15（ScopeId⊆ 不影响本函数，但共享「判定谓词悬空」母题）、Iter01（I1-03 Unknown⊤/MA-010）、Iter14（DO-7 kind 与 mode 混淆）
- **结论摘要**：§3.2.3 的 `Compatible(m₁,m₂)` 仅以 4 条析取（全部形如 `m₂=use`）显式定义「兼容」情形，其余 12 个有序 mode 对（mode∈{use,create,release,move}，共 16 对）既未列入析取、也**未声明 else-分支=false**，故该函数作为数学定义在严格意义下是**偏函数**——未覆盖对返回 undefined，冲突判定悬空（open，高，DO-9/并发安全数学基础缺失）。更严重的是：(1) **非对称**——`Compatible(create,use)=true` 而 `Compatible(use,create)=false`，但并行组合 `||` 是交换的（S₁||S₂=S₂||S₁），其「需满足」约束却用有序对 `Compatible(c₁.mode,c₂.mode)`，致同一并行组合的依赖顺序不同可能一会通过一会失败，矛盾（open，高）；(2) **误伤正常生命周期**——`create+release`（AddChild 后 RemoveChild，本应良性）与 `release+create`（复用）不在 4 析取内 ⇒ 被判定为冲突 ⇒ 误报，且恰好打中 §7.1 最核心的 add/remove 配对（open，高）；(3) **move 模式语义覆盖残缺**——QueueFree 用 mode=move（Iter08 I8-01），但 move 仅出现在第 4 析取 `(move,use)` 与注释 `(move,move)`，`(move,create)/(move,release)/(release,move)/(create,move)` 全未定义 ⇒ 含 move 的任意非 use 配对冲突判定 undefined（open，高）；(4) **注释引用不存在的「write 模式」**——注释「use+write(同资源)」混用 kind(read/write/occupy) 与 mode(use/create/release/move) 术语，表明设计未厘清 kind×mode 二维（交叉 Iter14 DO-7）；(5) **第1析取 use∧use 与 MA-010 Unknown⊤ 冲突**——若 resource=Unknown（保守），两 use 同「unknown」资源按规则兼容（不报冲突），但 MA-010 要求 Unknown 与任何资源冲突 ⇒ 静默放行本应保守冲突的 Unknown 对（open）；(6) **MA-009「枚举 16 种组合、完备性已收敛」不实**——文档正文从未给出 16 组合表，且上述 create+release 漏判、非对称、move 残缺均未覆盖，实为 asserted（open）。结构性成立（补全 16 对语义 + 强制对称 + 显式 move 规则）给条件证明。

---

## P1. 函数偏定义：12/16 有序对未覆盖（核心，高）

**命题** §3.2.3（L131-138）：`Compatible(m₁,m₂) := (m₁=use∧m₂=use) ∨ (m₁=create∧m₂=use) ∨ (m₁=release∧m₂=use) ∨ (m₁=move∧m₂=use)`；注释仅列「不兼容：create+create, move+move, use+write(同资源)」。

**数学性质 / 证明状态**：
- **(PO-I16-a) 偏函数 ⇒ 冲突判定悬空（open，高）**：mode 域为 4 元 `{use,create,release,move}`，有序对共 16 个。4 条析取仅覆盖 `(_,use)` 的 4 个（use/create/release/move 各作 m₁）。剩余 12 个（m₂∈{create,release,move} 的全部、及 m₁=use 且 m₂≠use 的 3 个）既不匹配析取、注释也未穷尽。严格数学定义下：不匹配析取 ⇒ 表达式无值 ⇒ `Compatible` 是**偏函数**，对未覆盖对返回 undefined。§3.2.2 的「需满足 `Compatible(c₁.mode,c₂.mode)`」在 c₁.mode/c₂.mode 落入未覆盖对时**谓词无定义** ⇒ 并行组合 `||` 的良定义性悬空 ⇒ DO-9 并发安全判定无数学基础。状态 = open（高）。
  - 即使「宽恕读」为「注释即 else=false」，该 else 分支**从未被显式写出**，属隐含约定，非形式化定义；且即便视为 false，下述 P2/P3 的语义错误仍成立。
- **(PO-I16-b) 注释「use+write(同资源)」引用不存在的模式（open）**：mode 域无 `write`（write 是 kind，见 §3.1.1 L79-86）。注释把 kind「write」当 mode 写，说明 Compatible 的设计残留旧版 kind=mode 设想，kind×mode 二维未厘清（交叉 Iter14 DO-7、N1）。状态 = open（文档自洽缺陷）。

**文档行号**：§3.2.3（L131-138）、§3.1.1（L79-86）、§3.2.2（L126-130）、DO-9（L21）。

---

## P2. 非对称性 vs 并行组合交换律矛盾（高）

**命题** §3.2.2（L126-130）：`(S₁ || S₂) = S₁ ∪ S₂`，约束用有序对 `Compatible(c₁.mode, c₂.mode)`（c₁∈S₁, c₂∈S₂）。§3.1/§3.2 隐含 `||` 为可交换算子（同名集合并，满足 A2 交换律）。

**数学性质 / 证明状态**：
- **(PO-I16-c) `||` 交换律与 Compatible 非对称矛盾（open，高）**：由 4 析取，`Compatible` 不对称：
  - `Compatible(create, use) = true`（第2析取），但 `Compatible(use, create) = false`（m₂=create≠use，未覆盖）。
  - `Compatible(release, use) = true`，但 `Compatible(use, release) = false`。
  - `Compatible(move, use) = true`，但 `Compatible(use, move) = false`。
  - 即 `Compatible(A,B) ⇏ Compatible(B,A)`。然而 `S₁ || S₂ = S₁ ∪ S₂ = S₂ ∪ S₁ = S₂ || S₁`，并行组合的数学对象与书写顺序无关；但「需满足」约束 `∀c₁∈S₁∀c₂∈S₂ Compatible(c₁.mode,c₂.mode)` **依赖顺序**：把同一对操作写成 `S₁||S₂` 时检查 `(mode₁,mode₂)`，写成 `S₂||S₁` 时检查 `(mode₂,mode₁)`——二者可能一真一假。故「并行组合是否通过兼容性检查」取决于**人为给操作编号的顺序**，与 `||` 的交换性矛盾 ⇒ 冲突判定本身不自洽。状态 = open（高，PDR 级算法规格矛盾）。
  - 例：AddChild `(write,create)` 与 MoveChild `(write,use)` 同 tree 节点。`(create,use)=true` ⇒ `AddChild||MoveChild` 通过；但 `(use,create)=false` ⇒ `MoveChild||AddChild` 失败。同一物理并行组合两写法结论相反。

**文档行号**：§3.2.2（L126-130）、§3.2.3（L131-138）、Iter14（DO-7 量纲/类型层未强制对称）。

---

## P3. 误伤正常生命周期：create+release / release+create 被误判冲突（高）

**命题** §7.1（L426-428）：`AddChild(node) = {write(tree,node.id,create,...), occupy(tree,node.id,create,...)}`；`RemoveChild(node) = {write(tree,node.id,release,...), occupy(tree,node.id,release,...)}`。即 AddChild(创建) 与 RemoveChild(释放) 是**标准生命周期配对**。

**数学性质 / 证明状态**：
- **(PO-I16-d) create+release 良性却被判冲突（open，高）**：`Compatible(create, release)`：m₂=release≠use ⇒ 不在 4 析取 ⇒ undefined/false。即 AddChild 后接 RemoveChild（同一节点）被兼容性检查判为**冲突**。但创建后释放恰是良性用法（节点新增后移除），应**兼容**。同理 `Compatible(release, create)`（先释放再复用）也 false ⇒ 复用模式误报。
  - 后果：§7.1 最核心的 add/remove 配对在 `||` 或顺序组合下被反复误判冲突 ⇒ DO-9 的「冲突/泄漏」报警噪声极大，正常节点管理会污染信号；且若把「Compatible=false」当作「危险」而阻止，则正常 remove 被阻断（过度约束）。状态 = open（高）。
  - 注释仅列 `create+create`、`move+move` 为假，却**未列 `create+release`/`release+create` 为假**——说明设计者未意识到生命周期配对应良性；即便按「else=false」宽恕读，这也是**语义错误**（把良性当冲突），而非单纯「未定义」。

**文档行号**：§7.1（L426-428）、§3.2.3（L131-138）、DO-9（L21）、Iter08（I8-01 QueueFree mode=move 同属释放语义错配）。

---

## P4. move 模式语义覆盖残缺（高，交叉 Iter08 I8-01）

**命题** §7.1（L429）：`QueueFree() = {release(tree,self.id,move,...), release(memory,self.size,move,...)}`，即 QueueFree 用 **mode=move** 表示所有权转移/释放。§3.2.3 的 4 析取仅含 `(move,use)`，注释仅含 `(move,move)`。

**数学性质 / 证明状态**：
- **(PO-I16-e) 含 move 的非 use 配对全部 undefined（open，高）**：涉及 move 的 7 个有序对（move×{create,release}、{create,release}×move、move×move）中，仅 `(move,use)` 与 `(move,move)` 有定义（后者仅在注释），其余 5 个 `(move,create)/(move,release)/(create,move)/(release,move)` 均未定义 ⇒ QueueFree(move) 与 AddChild(create)/RemoveChild(release) 等配对时冲突判定 undefined。
  - 更深层：`move` 的代数语义（所有权转移 vs 释放）在 §3.1.1 仅列为 4 mode 之一，无任何「move 与 create/release 的关系」说明。QueueFree 标 move 而非 release（Iter08 I8-01 已指其导致 net 漏算释放项），此处又暴露 move 在冲突层无规则 ⇒ 释放类操作的「冲突/守恒」判定双失守。状态 = open（高，交叉 Iter08）。

**文档行号**：§7.1（L429）、§3.2.3（L131-138）、Iter08（I8-01 / PO-I8-a）。

---

## P5. 第1析取 use∧use 与 MA-010 Unknown⊤ 冲突（open）

**命题** §3.2.3 第1析取 `(m₁=use ∧ m₂=use)` ⇒ 兼容；§3.4 MA-010（L189）：变量 path 保守为 `Unknown`，与任何资源冲突。

**数学性质 / 证明状态**：
- **(PO-I16-f) use∧use 兼容 vs Unknown⊤ 冲突（open）**：当两 Claim 的 resource=Unknown（保守，如 `Load` 变量 path、网络 `self.id+"/"+method`，见 Iter09 I9-05 / Iter01 I1-03），依第1析取二者 `use∧use` ⇒ `Compatible=true` ⇒ 不报冲突。但 MA-010 要求 Unknown 与**任何**资源（含另一 Unknown）冲突 ⇒ 应报冲突。两规则直接矛盾：按 Compatible 规则 Unknown∧Unknown 放行，按 MA-010 应保守冲突。未知哪一为准 ⇒ 保守性失效（漏报风险）。状态 = open（交叉 Iter01 I1-03、Iter09 I9-05）。

**文档行号**：§3.2.3（L131-138）、§3.4 MA-010（L189）、§3.1.2（L88-99 ResourceId.Unknown）。

---

## P6. MA-009「枚举 16 种组合、完备性已收敛」不实（open）

**命题** §3.4 MA-009（L189）：「Compatible 的完备性 — 已收敛 — 枚举定义 16 种组合，单元测试覆盖，不追求形式化证明」。

**数学性质 / 证明状态**：
- **(PO-I16-g) MA-009 实为 asserted（open）**：文档正文（§3.2.3 L131-138）**从未给出 16 组合表**，仅 4 条析取 + 1 行注释；且上述 P1(偏定义)/P2(非对称)/P3(create+release 误判)/P4(move 残缺)/P5(Unknown 矛盾) 表明这「枚举」既不全（12/16 未覆盖）也不对（良性生命周期误判、非对称破坏交换律）。即「完备性已收敛」建立在未写出的隐含约定与错误语义上 ⇒ 实为 asserted，非真正收敛。状态 = open（交叉 Iter04 I4-03、Iter07 I7-01 的「已收敛」过度声称母题）。

**文档行号**：§3.4 MA-009（L189）、§3.2.3（L131-138）。

---

## P7. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若将 `Compatible` 重定义为**对称全函数**并补全生命周期语义：
  `C(m₁,m₂) :=` 对称闭包 + 良性对 `{ (use,use),(use,create),(create,use),(use,release),(release,use),(create,release),(release,create),(move,use),(use,move),(move,release),(release,move) }` 为真，仅 `(create,create),(release,release),(move,move)` 为假（冲突），则 `Compatible` 成为全函数且对称，`||` 交换律相容，add/remove 配对良性通过。证明：对称闭包 ⇒ `C(A,B)=C(B,A)`；枚举覆盖全部 16 对 ⇒ 全函数。前提 PO-I16-a/c/d 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若显式定义 `move` 的代数语义（`move ≡ 释放旧所有者 + 转移给新所有者`，等价于对旧资源 `release`、对新资源 `create`），则 move 与 create/release 的配对可归约为 P1 的良性对，冲突判定闭合。证明：语义归约。前提 PO-I16-e（move 语义未定义）未立 ⇒ 条件。
- **P3（discharged，条件）**：若 MA-010 的 Unknown 处理改为「Unknown 资源的 use∧use 亦保守冲突」并显式写入 Compatible 第1析取的例外条款，则与 Unknown⊤ 一致。证明：例外条款对齐 MA-010。前提 PO-I16-f 未立 ⇒ 条件。
- **P4（discharged）**：在「仅比较 mode、忽略 kind」的当前设计下，§3.2.2 的约束语法自洽（只要把 Compatible 视为给定谓词）；矛盾仅源于该谓词的**定义质量**，非语法层。证明：语法闭合。但语义正确性仍依赖 P1-P3。

---

## Proof Obligation 账本（Iter16）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I16-a | Compatible 偏定义（12/16 对 undefined） | open(高) | 显式穷举 16 对或声明 else=false | L131-138, L126-130 |
| PO-I16-b | 注释引用不存在的「write 模式」 | open | 厘清 kind×mode，删/改注释 | L138, L79-86 |
| PO-I16-c | 非对称破坏 \|\| 交换律 | open(高) | 强制 Compatible 对称 | L126-130, L131-138 |
| PO-I16-d | create+release 良性被判冲突 | open(高) | 补生命周期良性对 | L426-428, L131-138 |
| PO-I16-e | move 模式语义覆盖残缺 | open(高) | 定义 move 代数+配对规则 | L429, L131-138 |
| PO-I16-f | use∧use 兼容 vs Unknown⊤ 冲突 | open | 补 Unknown 例外条款 | L131-138, L189 |
| PO-I16-g | MA-009 完备性不实(asserted) | open | 重写 16 组合表+对称+move | L189, L131-138 |

## 本轮新发现未消解缺口（I16- 前缀，全局唯一）
- **I16-01（高）**：`Compatible(m₁,m₂)` 仅 4 条 `(·,use)` 析取覆盖 16 有序对中的 4 个，其余 12 个未声明 else 值 ⇒ 偏函数，冲突判定语义悬空，DO-9 并发安全数学基础缺失。
- **I16-02（高）**：`Compatible` 非对称（`(create,use)=true` 但 `(use,create)=false`），而 `||` 交换律要求对称 ⇒ 同一并行组合因书写顺序不同结论相反，算法规格自相矛盾。
- **I16-03（高）**：`create+release`（AddChild→RemoveChild 正常生命周期）不在兼容析取内 ⇒ 被误判冲突，§7.1 核心 add/remove 配对噪声极大，且可能过度约束正常 remove。
- **I16-04（高）**：QueueFree 的 `mode=move`（Iter08 I8-01）在冲突层仅 `(move,use)/(move,move)` 有定义，余 5 个含 move 配对 undefined ⇒ 释放类操作冲突/守恒判定双失守。
- **I16-05**：注释「use+write(同资源)」引用不存在的 write 模式，暴露 kind(mode) 与 mode 二维未厘清（交叉 Iter14 DO-7）。
- **I16-06**：`use∧use 兼容` 与 MA-010 Unknown⊤「与任何资源冲突」矛盾 ⇒ Unknown∧Unknown 保守性失效、漏报。
- **I16-07**：MA-009「枚举 16 种组合、完备性已收敛」不实——正文无 16 组合表，且 P1-P5 证明枚举不全不对，实为 asserted。

---

一句话摘要：§3.2.3 `Compatible` 仅 4 条 `(·,use)` 析取、余 12 对未声明 else 值 ⇒ 偏函数冲突判定悬空（I16-01，高）；非对称（`(create,use)≠(use,create)`）破坏 `||` 交换律致同组合顺序不同结论相反（I16-02，高）；正常生命周期 `create+release` 误判冲突（I16-03，高）；QueueFree 的 move 模式 5 个配对 undefined（I16-04，高）；use∧use 与 MA-010 Unknown⊤ 矛盾（I16-06）；MA-009「16 组合完备」不实（I16-07）——DO-9 并发安全判定无自洽数学基础。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter16.md，未读/改其它 audit 文件，聚焦 §3.2.3 Compatible 组合子的完备性/对称性/mode 覆盖，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #16（hy3 单独进程，本轮重跑）」、P1-P7 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I16- 缺口列表；交叉引用真实行号(L79-86/L126-138/L189/L426-429 等) 并经 read 确认 §3.2.3 / §7.1 / §3.4 真实文本"}
  ],
  "changedFiles": ["audit/iter16.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 75) + (offset 124, 45) + (offset 160, 35)", "result": "passed", "summary": "读取 §3.1.1 Claim mode、§3.2.2-3 Compatible、§3.3 派生度量、§3.4 MA 表真实文本"},
    {"command": "read PDR (offset 421, 90)", "result": "passed", "summary": "读取 §7.1-7.10 映射确认 QueueFree mode=move、AddChild/RemoveChild create/release 标注"},
    {"command": "write D:/Godot/Cosmos/audit/iter16.md", "result": "passed", "summary": "覆盖写入独立审计 #16"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 P1-P7 七节 + Proof Obligation 账本 + 7 条 I16- 缺口", "交叉引用 §3.1.1/§3.2.2/§3.2.3/§3.4 MA-009/MA-010/§7.1/DO-9/Iter08/Iter01/Iter14 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 Compatible 实际代码是否对称（仅基于文档 §3.2.3 文本推导）", "move 的「真实」代数语义未在 PDR 正文找到，归约为假设条件证明"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter16.md，独立审计 §3.2.3 Compatible 组合子完备性/对称性/move 覆盖缺口",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 Compatible 偏定义/非对称破坏 || 交换律/生命周期误判/MA-009 不实，需 PDR 侧修正"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
