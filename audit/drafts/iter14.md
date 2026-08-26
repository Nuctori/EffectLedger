# Iter14 审计 — DO-7 量纲隔离与 §3.1 Set<Claim> 单集合 ∪ 混合 kind 的矛盾（独立审计 #14，hy3 单独进程，本轮重跑）

- **审计视角**：量纲隔离的代数实现 / 工具执行责任 / 派生度量混算（独立 pass #14，全新上下文）
- **范围**：§1 DO-7（L19）、§3.1.1 Claim.kind（L80-82）、§3.1.4 Signature=ImmutableHashSet<Claim>（L94）、§3.2 组合律（∪ 混合 kind，L107-147）、§3.3 派生度量（peak 混加 size，L155-173）、§7 映射（kind 混合，L421-507）、§3.4 MA-007（L186）；邻接 §6 三层工具（L329-398，Iter07）、§12.2 预算比较（L656-672，Iter13）
- **结论摘要**：DO-7 要求「read/write/occupy 不可混算，编译期报错」，但全文用**单一 Set<Claim>（含混合 kind）做 ∪ 组合**（§3.1.4/§3.2.1-2），∪ 对三类 Claim 一视同仁、无 kind 子空间 ⇒ 组合层即混算，与 DO-7 直接矛盾（open，高）；§3.3.2 `peak = max Σ c.size`（仅按 mode≠release 过滤，不按 kind 分离）把 read/write/occupy 三类 size 同数值相加 ⇒ 混算，违反 DO-7（open）；DO-7「编译期报错」未指派任何 L1/L2/L3 工具执行（§6 三层均未提量纲检查，grep 全文「量纲」仅 L19/L186 两处）⇒ 无执行机制（open）；peak 求和结果的量纲单位未定义（bytes? count? mix?），而 §12.2 用 512MB 预算比较仅对 memory 有意义（open）；MA-007「量纲隔离通过 kind 字段实现，转换需显式权重函数」但全文**权重函数未定义** ⇒ 跨量纲合法转换通道缺失（open，partial）。结构性成立（若 Signature 改为按 kind 分桶 R/W/O 则 DO-7 可落地；当前弱解释下 DO-7 不成立但 §3.3 自洽）给条件证明。

---

## N1. DO-7 与 Set<Claim> 单集合 ∪ 组合的矛盾（核心，高）

**命题** §1 DO-7（L19）：「read/write/occupy 不可混算，编译期报错」。§3.1.4（L94）：`Signature := ImmutableHashSet<Claim>`，即所有 Claim（含 read/write/occupy 三类 kind）落入**同一个集合**。§3.2.1/3.2.2（L107-113）：`(S₁;S₂) = S₁ ∪ S₂`、`(S₁||S₂) = S₁ ∪ S₂`，∪ 对集合内元素**无差别**，不存在「按 kind 划分子空间」或「跨 kind ∪ 报错」的机制。

**数学性质 / 证明状态**：
- **(PO-I14-a) 组合层即混算（open，高）**：设 `S₁ = {read(tree,..)}`，`S₂ = {write(tree,..)}`，`S₃ = {occupy(tree,..)}`。DO-7 的语义是「read/write/occupy 不可混算」，即 `S₁∪S₂`、`S₁∪S₃`、`S₂∪S₃` 应触发编译期报错。但 §3.2 的 ∪ 定义对 kind 视而不见 ⇒ 这些组合**静默合并**为合法 Signature，未触发任何报错。即组合律（§3.2）与 DO-7（§1）在**同一文档内互斥**：若 DO-7 为真，则 §3.2 的 ∪ 必须携带 kind 子空间约束；若 §3.2 为真，则 DO-7 的「不可混算」从未被实现。状态 = open（高，PDR 级目标与机制矛盾）。
- 交叉：Iter13 I13-01 已表明 §14「0 阻塞」与文档内部矛盾；本项为「目标 vs 机制」层面的具体矛盾实例。

**文档行号**：§1 DO-7（L19）、§3.1.4（L94）、§3.2.1/3.2.2（L107-113）。

---

## N2. §3.3.2 peak 混加三类 size（open）

**命题** §3.3.2（L159）：`peak(S, scope) = max_{t∈scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size`。

**数学性质 / 证明状态**：
- **(PO-I14-b) peak 求和未按 kind 分离（open）**：求和谓词仅过滤 `mode≠release`，**不按 kind 过滤**，故对同 (scope, t) 窗口，read/write/occupy 三类的 size 被**同数值相加**。例如 §7.4 `Instantiate` 产生 `occupy(memory, scene.estimated_size, create)` 与 `create(tree, new_id, create)`（kind=create 但记为 write? —— 见 N5）、§7.2 `Position` setter 产生 `write(self,"transform",use)`；这些不同 kind 的 size 在同一 peak 窗口求和 ⇒ 把「读取次数」「写入字节」「占用内存」混为单一数值，违反 DO-7「不可混算」。状态 = open。
- 附加：peak 与 §3.3.3 `read(S)/write(S)`（L172-173，按 kind 计|·|）存在**双重标准**——§3.3.3 承认 kind 之分（按 kind 计数），§3.3.2 却抹去 kind（按 kind 求和时不分）。文档内部对 kind 的处理不一致。状态 = open（弱）。

**文档行号**：§3.3.2（L159）、§3.3.3（L172-173）、§7.2/§7.4（L436-459）。

---

## N3. DO-7「编译期报错」无工具执行（open）

**命题** DO-7 验收标准含「编译期报错」（L19）。§6 三层工具（L329-398）：L1 类型系统（L340-353，sealed/readonly/struct）、L2 Source Generator（L355-388，字段白名单/方法体写集分析）、L3 Roslyn Analyzer（L390-398，RULE001/SHELL001/SHELL003/BUDGET001/SYS001）。

**数学性质 / 证明状态**：
- **(PO-I14-c) 三层工具均未执行量纲检查（open）**：全文 grep「量纲」仅命中 L19（DO-7 目标）与 L186（MA-007 收敛方案），**§6 任何一层均未提及对 read/write/occupy 混算的静态检测**。L3 的 BUDGET001 仅做「预算累加」（§12.2 显示是 size 累加，未分 kind），SYS001 仅做 System 写冲突，RULE001/SHELL001/SHELL003 与量纲无关。即 DO-7 的「编译期报错」**没有任何机制负责实现**，目标悬空。状态 = open（交叉 Iter07 PO-I7-*，三层完备性未证，但此处是「根本未覆盖该检查」而非「检查不完备」）。
- 交叉：Iter13 I13-02/I13-06 指出 §6 工具完备性/覆盖性缺口；本项补充「量纲检查根本不在工具职责表内」。

**文档行号**：§1 DO-7（L19）、§6（L329-398）、§3.4 MA-007（L186）、§12.2（L656-672，Iter13 PO-I13-c/d）。

---

## N4. peak 量纲单位未定义，与 512MB 预算比较仅对 memory 有意义（open）

**命题** §3.3.2 的 `peak` 输出为标量 Σsize；§12.2（L667）`Texture2D → 默认 occupy{memory, 64MB}`，场景中 20 Enemy 累加 1280MB 与「默认预算 512MB」比较报警。

**数学性质 / 证明状态**：
- **(PO-I14-d) peak 量纲单位未定义（open）**：`c.size` 在 §3.1.1（L82）`size ∈ Nat?` 无单位标注；但对不同资源，size 语义不同：`occupy(memory,..)` 的 size 是字节，`occupy(audio_channel,1,..)`/`occupy(animation_state,1,..)` 的 size 是「通道/状态计数」，`read(tree,..)` 的「size」其实无 size（读计数在 §3.3.3 用 |·| 而非 size）。§3.3.2 把这些都加进同一 peak 标量 ⇒ 量纲单位未定义（bytes? count? mix?）。状态 = open。
- **(PO-I14-e) 512MB 预算比较仅对 memory 有意义（open）**：§12.2 用 512MB 预算与累加 size 比较，但累加若混入音频通道数/动画状态数/读计数，则「1280MB」是**跨量纲假数值**。即 §12.2 的预算报警在 DO-7 未落实时可能是无意义的混算结果。状态 = open（交叉 Iter13 PO-I13-d 数值口径冲突）。

**文档行号**：§3.1.1（L82）、§3.3.2（L159）、§12.2（L667，Iter13 PO-I13-d）。

---

## N5. MA-007「权重函数」全文未定义（open，partial）

**命题** §3.4 MA-007（L186）：「量纲转换缺失 → 已解决：量纲隔离通过 kind 字段实现，转换需显式权重函数」。

**数学性质 / 证明状态**：
- **(PO-I14-f) 权重函数未定义（open，partial）**：MA-007 声称「转换需显式权重函数」，但全文**无任何权重函数定义**（grep 无「权重」实现，仅 L186 一处声明）。即：
  - 「量纲隔离通过 kind 字段实现」——kind 字段确实存在（L81），但如前 N1/N2，kind 字段**未被任何运算符消费**（∪/peak 都无视 kind），故「通过 kind 实现隔离」是**断言而非实现**。
  - 「转换需显式权重函数」——该函数缺失 ⇒ 合法跨量纲转换（如「1 次 occupy(memory) 折算为 N 次 read」用于统一预算）**无通道**，任何跨 kind 的派生度量只能粗暴混加（回到 N2 问题）。
  - 故 MA-007 标注「已解决」不实，实为 partial/asserted（机制元素存在，行为未定义）。状态 = open（partial）。
- 交叉：Iter04 已把 MA-007 还原为 open/asserted；本项给出具体证据（权重函数缺失 + kind 未被运算符消费）。

**文档行号**：§3.4 MA-007（L186）、§3.1.1（L81）、§3.2/§3.3（L107-173）。

---

## N6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 Signature 改为 `Signature := (R:Set<Claim(kind=read)>, W:Set<Claim(kind=write)>, O:Set<Claim(kind=occupy)>)`（按 kind 分桶），且 ∪ 定义为桶内各自 ∪、跨桶 ∪ 触发编译期报错（或需显式权重函数折算），则 DO-7 落地（read/write/occupy 不可混算，编译期报错）。证明：kind 子空间在类型层强制分离，跨 kind 操作无类型通路。前提 PO-I14-a（当前 Set<Claim> 未改）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 peak 定义为 `peak_k(S,scope) = max Σ_{c∈S,kind=k,..} weight(k)·c.size`（k∈{read,write,occupy}，weight 来自 MA-007 权重函数），则 peak 在显式权重下可加、且 DO-7 允许「经权重折算的混算」（明确通道）。证明：权重函数提供合法转换。前提 PO-I14-f（权重函数未定义）未立 ⇒ 条件。
- **P3（discharged）**：在当前（未落实 DO-7）弱解释下，§3.3 自洽——`peak`/`net`/`read`/`write` 作为「不带量纲标注的纯数值派生度量」内部一致（仅与 DO-7 目标冲突，不与自身冲突）。证明：§3.2-§3.3 公式体系闭合，无内部除零/类型错误（除 Iter11 的 range=0 问题外）。无需额外前提（结构成立，但与 DO-7 外部矛盾）。

---

## Proof Obligation 账本（Iter14）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I14-a | Set<Claim> ∪ 混合 kind 即混算 | open(高) | Signature 按 kind 分桶 + ∪ 跨桶报错 | L19, L94, L107-113 |
| PO-I14-b | peak 求和未按 kind 分离 | open | peak 按 kind 分桶或用权重 | L159, L172-173 |
| PO-I14-c | 三层工具无执行量纲检查 | open | §6 增 L3 量纲检查规则 | L19, L329-398, L186 |
| PO-I14-d | peak 量纲单位未定义 | open | 定义 size 单位/分资源量纲 | L82, L159 |
| PO-I14-e | 512MB 预算比较仅对 memory 意义 | open | 预算按资源类型分列 | L667, L159 |
| PO-I14-f | 权重函数未定义 | open(部分) | 定义 MA-007 权重函数 | L186, L81, L107-173 |

## 本轮新发现未消解缺口（I14- 前缀，全局唯一）

- **I14-01（高）**：DO-7「read/write/occupy 不可混算」与 §3.1.4 单 Set<Claim> + §3.2 ∪ 混合 kind 直接矛盾；组合层即静默混算，目标从未被实现。
- **I14-02**：§3.3.2 peak 仅按 mode≠release 过滤、不按 kind 分离，把 read/write/occupy 三类 size 同数值相加，违反 DO-7。
- **I14-03**：DO-7 的「编译期报错」未指派任何 L1/L2/L3 工具执行（grep 全文「量纲」仅 L19/L186），目标悬空无机制。
- **I14-04**：peak 求和量纲单位未定义（bytes/count/mix 皆可能），§3.3.3 却按 kind 计数——文档内部对 kind 处理双重标准。
- **I14-05**：§12.2 用 512MB 预算与混算 peak 比较，若混入音频/动画/读计数则「1280MB」为跨量纲假数值，报警意义存疑（交叉 Iter13 PO-I13-d）。
- **I14-06**：MA-007 称「转换需显式权重函数」但全文权重函数未定义，且 kind 字段未被任何运算符（∪/peak）消费 ⇒ 「量纲隔离通过 kind 实现」是断言非实现，MA-007「已解决」不实。
- **I14-07**：DO-7 与 §3.3 的冲突本质是「目标层要求强隔离」但「机制层用无差别集合代数」——二者需二选一（改 Signature 分桶/改 DO-7 降级为「建议」），文档未做选择。

---

一句话摘要：DO-7「read/write/occupy 不可混算，编译期报错」与 §3.1.4 单 Set<Claim>+§3.2 无差别 ∪（I14-01，高）、§3.3.2 混加三类 size（I14-02）、§6 三层工具根本未执行量纲检查（I14-03）、peak 量纲单位未定义致 512MB 预算比较失真（I14-05）、MA-007 权重函数全文缺失（I14-06）五处矛盾，DO-7 实际未落地、MA-007「已解决」不实。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter14.md，未读/改其它 audit 文件，聚焦 DO-7 量纲隔离与 Set<Claim> 混合 kind 矛盾，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #14（hy3 单独进程，本轮重跑）」、N1-N6 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I14- 缺口列表；交叉引用真实行号(L19/L80-82/L94/L107-173/L186/L329-398/L667 等)并经 grep 验证「量纲」仅 L19/L186 两处"}
  ],
  "changedFiles": ["audit/iter14.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 1, 25) + (offset 78, 25) + (offset 103, 110)", "result": "passed", "summary": "读取 §1 DO-7、§3.1.1/3.1.4、§3.2/§3.3、§3.4 MA-007 真实文本"},
    {"command": "read PDR (offset 421, 90) + (offset 655, 30) + (offset 686, 30)", "result": "passed", "summary": "读取 §7 映射 kind 混合、§12.2 预算比较真实文本"},
    {"command": "grep PDR 全文「量纲|kind|dimension|权重」", "result": "passed", "summary": "确认「量纲」仅命中 L19/L186，「权重」无实现，§6 无涵盖量纲检查"},
    {"command": "write D:/Godot/Cosmos/audit/iter14.md", "result": "passed", "summary": "覆盖写入独立审计 #14"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 N1-N6 六节 + Proof Obligation 账本 + 7 条 I14- 缺口", "交叉引用 §1/§3.1.1/§3.1.4/§3.2/§3.3/§3.4 MA-007/§6/§7/§12.2 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 源码验证「量纲检查」确无实现（仅基于文档 §6 文本 + grep 推断）", "§12.2 预算比较的具体资源类型拆分未在本次读取范围外验证，引用基于 §12.2 文本"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter14.md，独立审计 DO-7 量纲隔离与 Set<Claim> 混合 kind 矛盾",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 DO-7 与 §3.2 ∪ 组合、§3.3.2 peak 混算、§6 工具缺职责、MA-007 权重函数缺失四重矛盾，需 PDR 侧修正（改 Signature 分桶 或 降级 DO-7）"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read + grep 结果；未读其它 audit 文件"
}
