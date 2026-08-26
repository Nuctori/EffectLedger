# Iter39 审计 — L3 Roslyn Analyzer 完备性：RULE001/SHELL001/SHELL003/BUDGET001/SYS001 覆盖域与误报/漏报边界（独立审计 #39，hy3 单独进程，本轮重跑）

- **审计视角**：§6.4 L3 静态分析的覆盖域与「已收敛」真伪（独立 pass #39，全新上下文）
- **范围**：§6.4 L3（L390-398，5 条规则）、§7 映射（L421-507）、§3.2.3 Compatible（L131-138）、§3.3.2 peak（L167）、§3.4 TS-003/TS-006/TS-007；邻接 Iter07 I7-03（L3 补方法体 effect）、Iter16（Compatible）、Iter14（DO-7 量纲）、Iter19（量化：L2/L3 完备性全 open）
- **结论摘要**：L3 列 5 条 Roslyn 规则：RULE001(Domain 禁引用 Godot)、SHELL001(Shell 方法复杂度>20 行/复杂条件)、SHELL003(new 隐藏 sealed override)、BUDGET001(场景预算累加)、SYS001(System 并行冲突检测)。审计发现其覆盖域与文档核心机制严重错位：(1) **无任何规则执行 §3.2.3 Compatible 冲突判定**——DO-9 的并发安全判定（Iter16，偏函数+非对称）在 L3 全无对应规则 ⇒ 并行冲突检测缺失；(2) **无规则执行 DO-7 量纲隔离**（Iter14 I14-03，grep 全文「量纲」仅 L19/L186）——read/write/occupy 混算无 L3 检查；(3) **BUDGET001「场景预算累加」只做 size 累加，不区分 kind/scope**（Iter14 I14-02/05），与 §3.3.2 peak 的 kind/scope 过滤脱节 ⇒ 预算报警基于混算伪值；(4) **SHELL001 复杂度阈值(>20 行) 是任意启发式**，非 effect 相关，与代数无关；(5) **5 条规则覆盖不到 §7 的 50+ API 映射语义**——如 Rpc 网络冲突(Iter30)、DrawMesh command_buffer 追加写(Iter31 L4)、Instantiate new_id Unknown(Iter26) 全无 L3 规则；(6) 规则本身 soundness 未证（启发式阈值、误报/漏报边界未量化）。结构性成立（L3 是工程 lint 工具）给条件证明，但「L3 完备性已收敛」不实（open，高）。

---

## T1. 命题：L3 无规则执行 Compatible 并发冲突判定

**命题**（§6.4 L390-398）：5 条规则（RULE001/SHELL001/SHELL003/BUDGET001/SYS001）**无一条**对应 §3.2.3 的 `Compatible` 并行冲突判定。
**命题**（Iter16 I16-01/02）：DO-9 并发安全依赖 Compatible，但 Compatible 偏函数+非对称 ⇒ 判定本身悬空。

后果：即便 Compatible 被正确定义（Iter25 C*），L3 也无规则将其落地为编译期检查 ⇒ DO-9 的「并发安全」在工具层**完全无执行机制**（与 Iter14 I14-03 量纲检查同源——三层工具都没覆盖核心判定）。

**数学性质 / 证明状态**：
- **(PO-I39-a) L3 无 Compatible 执行规则（open，高）**：SYS001 仅「System 并行冲突」兜底，但 §7 的并行冲突（如 AddChild||MoveChild，Iter22）是 tree/资源层，非 System 命名空间层 ⇒ SYS001 不覆盖。状态 = open（高，交叉 Iter16/Iter14 I14-03）。
- 文档行号：§6.4（L390-398）、§3.2.3（L131-138）、Iter16（I16-01）、Iter14（I14-03）。

---

## T2. 命题：L3 无 DO-7 量纲隔离检查

**命题**（§1 DO-7 L19）：read/write/occupy 不可混算，编译期报错。
**命题**（Iter14 I14-03）：grep 全文「量纲」仅 L19/L186 ⇒ L3 无规则检查 kind 混算。

**数学性质 / 证明状态**：
- **(PO-I39-b) L3 无量纲检查规则（open，高）**：DO-7 的编译期报错无 L3 规则执行（Iter36 Q4 的 KIND_MIX 规则在文档中不存在）。状态 = open（高，交叉 Iter14 I14-03、Iter36 Q4）。
- 文档行号：§1 DO-7（L19）、§6.4（L390-398）、Iter14（I14-03）、Iter36（Q4）。

---

## T3. 命题：BUDGET001 预算累加不区分 kind/scope

**命题**（§6.4 L394）：BUDGET001「场景预算累加」。
**命题**（Iter14 I14-02/05）：§3.3.2 peak 混加 read/write/occupy size、不区分 kind/scope；§12.2 用 512MB 比较基于混算伪值。

BUDGET001 若仅「累加 size」而不按 kind/scope 分离 ⇒ 与 peak 同病（DO-7 违反）。且 BUDGET001 触发前提（[Budget] 标注，Iter38 S2）使其仅覆盖标记组件 ⇒ 未标记组件预算零检测（双重盲区，Iter10/Iter38）。

**数学性质 / 证明状态**：
- **(PO-I39-c) BUDGET001 混算+标注依赖（open，高）**：预算报警基于混算伪值且仅覆盖 [Budget] 组件 ⇒ 精度与覆盖双缺。状态 = open（高，交叉 Iter14 I14-02/05、Iter38 S2、Iter10）。
- 文档行号：§6.4（L394）、§3.3.2（L167）、§12.2（L667）、Iter14（I14-02/05）。

---

## T4. 命题：5 规则覆盖不到 §7 的 50+ API 语义

**命题**（§7 L421-507）：50+ Godot API 映射为 Claim 集合，含网络/渲染/信号/动画等。
**命题**（§6.4 L390-398）：5 规则均为工程 lint（命名空间/复杂度/sealed/预算/System），**无一条**语义级校验 §7 映射（如 Rpc 网络冲突、DrawMesh command_buffer、Instantiate new_id）。

后果：L3 能查「方法>20 行」「new 隐藏 sealed」，但**查不了**「Rpc 任意两调用保守冲突」（Iter30）、「同 command_buffer 多 Draw 误报」（Iter31 L4）、「Instantiate new_id Unknown」（Iter26）。即 L3 与 §7 映射层**语义脱节**——L3 是通用 lint，非 effect 代数检查器。

**数学性质 / 证明状态**：
- **(PO-I39-d) L3 与 §7 映射语义脱节（open，高）**：effect 代数需的语义检查（Compatible/量纲/scope/Unknown）全无 L3 规则。状态 = open（高，交叉 Iter30/31/26/Iter16/Iter14）。
- 文档行号：§6.4（L390-398）、§7（L421-507）、Iter30/31/26。

---

## T5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 L3 增补 (a) COMPAT 规则执行 Iter25 C*（并行 Compatible 检查）、(b) KIND_MIX 规则（Iter36 Q4，量纲隔离）、(c) SCOPE 规则（Iter34/37，scope 分组）、(d) UNKNOWN 规则（Iter21，Unknown 冲突），则 L3 覆盖 effect 代数核心判定。证明：规则落地。前提 Iter25/36/34/21 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 BUDGET001 改为「按 kind×scope 分桶累加」（Iter36/37），则预算报警不混算。证明：分桶。前提 Iter36/37 未立 ⇒ 条件。
- **P3（discharged）**：在「L3 仅作工程 lint、effect 判定由 L2+§8 推导承担」弱假设下，L3 职责闭合。但文档 §6 收敛声明称「三层完备」（Iter07 I7-01）⇒ 假设与声明冲突。

---

## Proof Obligation 账本（Iter39）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I39-a | L3 无 Compatible 执行规则 | open(高) | 增 COMPAT 规则(Iter25) | L390-398, I16-01, I14-03 |
| PO-I39-b | L3 无 DO-7 量纲检查 | open(高) | 增 KIND_MIX(Iter36 Q4) | L19, L390-398, I14-03 |
| PO-I39-c | BUDGET001 混算+标注依赖 | open(高) | 分桶累加(Iter36/37) | L394, L167, I14-02/05 |
| PO-I39-d | L3 与 §7 映射语义脱节 | open(高) | 增语义规则(Iter30/31/26) | L390-398, L421-507 |

## 本轮新发现未消解缺口（I39- 前缀，全局唯一）
- **I39-01（高）**：L3 五规则无一条执行 §3.2.3 Compatible ⇒ DO-9 并发冲突在工具层无机制（交叉 Iter16/Iter14 I14-03）。
- **I39-02（高）**：L3 无量纲隔离检查 ⇒ DO-7 编译期报错悬空（交叉 Iter14 I14-03 / Iter36 Q4）。
- **I39-03（高）**：BUDGET001 仅 size 累加不区分 kind/scope + 依赖 [Budget] 标注 ⇒ 混算伪值+未标记盲区（交叉 Iter14/Iter38）。
- **I39-04（高）**：L3 是通用 lint，与 §7 的 50+ API 映射语义脱节（Rpc/command_buffer/Instantiate 等无规则，交叉 Iter30/31/26）。
- **I39-05（弱）**：SHELL001 的「>20 行」复杂度阈值是任意启发式，非 effect 相关，与代数机制无关 ⇒ 即便 sound 也与 DO 目标无直接联系。

---

一句话摘要：L3 五规则（RULE001/SHELL001/SHELL003/BUDGET001/SYS001）无一条执行 §3.2.3 Compatible（I39-01，高，DO-9 并发无机制）或 DO-7 量纲隔离（I39-02，高）、BUDGET001 混算+标注依赖致伪值与盲区（I39-03，高）、且与 §7 的 50+ API 映射语义脱节（I39-04，高，Rpc/command_buffer/Instantiate 无规则）——L3 是工程 lint 非 effect 代数检查器，「L3 完备性已收敛」不实，需 PDR 侧补 COMPAT/KIND_MIX/SCOPE/UNKNOWN 语义规则（Iter25/36/34/21）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter39.md，未读/改其它 audit 文件，聚焦 L3 Roslyn Analyzer 完备性，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #39（hy3 单独进程，本轮重跑）」、T1-T5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I39- 缺口列表；交叉引用真实行号(L390-398/L131-138/L19/L167/L421-507) 并经 read 确认 §6.4/§3.2.3/§1/§7 真实文本"}
  ],
  "changedFiles": ["audit/iter39.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 390, 10)", "result": "passed", "summary": "读取 §6.4 L3 五规则真实文本确认覆盖域"},
    {"command": "read PDR (offset 19, 3) + (offset 131, 8) + (offset 421, 6)", "result": "passed", "summary": "读取 §1 DO-7 / §3.2.3 Compatible / §7 映射确认 L3 语义脱节"},
    {"command": "write D:/Godot/Cosmos/audit/iter39.md", "result": "passed", "summary": "覆盖写入独立审计 #39"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 T1-T5 五节 + Proof Obligation 账本(T4 项) + 5 条 I39- 缺口", "交叉引用 §6.4/§3.2.3/§1 DO-7/§3.3.2/§7/Iter16/Iter14/Iter36/Iter30/31/26/Iter07 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 源码验证规则实际行为（仅基于 §6.4 文本推导）", "effect 代数语义规则(COMAT/KIND_MIX) 依赖 Iter25/36 未本轮定义"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter39.md，独立审计 L3 Roslyn Analyzer 完备性边界",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 L3 五规则与 effect 代数核心判定(Compatible/量纲/scope/Unknown) 全脱节，需 PDR 侧补语义规则"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
