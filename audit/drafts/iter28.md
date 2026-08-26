# Iter28 审计 — `Load`/`Preload` 内存占用标 `global_scope` 与 `Instantiate` 标 `shell_scope` 跨 scope 不一致（独立审计 #28，hy3 单独进程，本轮重跑）

- **审计视角**：同一物理资源（内存）在不同 API 的 scope 标注不一致 ⇒ 跨 scope 峰值/净值得不可并（独立 pass #28，全新上下文）
- **范围**：§7.4 Load/Preload（L456/L459，occupy memory global_scope）、§7.4 Instantiate（L458，occupy memory shell_scope）、§3.1.3 ScopeId（L103-113，7 构造子无 global_scope/shell_scope）、§3.3.2 peak（L167，c.scope⊆t 过滤）、§3.3.1 net（L163-165，无 scope）；邻接 Iter09 I9-01（scope 不统一）、Iter15 I15-01/02/03（ScopeId⊆ 未定义 + shell_scope 非合法）、Iter27（net 漏算）
- **结论摘要**：§7.4 三个内存占用 API 给出三种 scope：Load/Preload 标 `occupy(memory, estSize, create, global_scope)`、Instantiate 标 `occupy(memory, est_size, create, shell_scope)`。三者物理上都占「内存」资源，但 scope 标注分裂为 global/shell。叠加 §3.1.3 的 ScopeId **根本无 `global_scope`/`shell_scope` 构造子**（只有 Method/Type/Scene/Global/Loop/Conditional/Async），这些标注值本身就不合法（Iter15 I15-02）。在即便把标注「归一」为合法构造子后，问题仍存：若 global 与 shell 是不同 scope 层级，则 Peak/peak 按 `c.scope⊆t` 过滤时，标 global 的内存占用**不并入**标 shell 的内存占用 ⇒ 同一物理内存被两套 scope 各自计数，**峰值被低估、net 跨 scope 不守恒**（open，高）。这是 §7 映射层最普遍的 scope 混乱源之一（几乎每个 API 都标 shell_scope，仅 Load/Preload 标 global_scope）。

---

## H1. 命题：内存占用 scope 标注分裂 global vs shell

**命题**（§7.4 L456/L459/L458）：
- `Load<T>(path)`：`occupy(memory, estimatedSize(T), create, global_scope)`
- `Preload(path)`：`occupy(memory, estimatedSize, create, global_scope)`
- `Instantiate(scene)`：`occupy(memory, scene.estimated_size, create, shell_scope)`

三者 resource 均为 `memory`（同物理资源），mode 均 create，但 scope 字段：Load/Preload=`global_scope`、Instantiate=`shell_scope`。

**数学性质 / 证明状态**：
- **(PO-I28-a) 同资源不同 scope 不可并（open，高）**：peak(S, scope)=`max_{t∈scope} Σ_{c: c.scope⊆t,..} size`（§3.3.2 L167）。若 `global_scope` 与 `shell_scope` 是**不同 scope 层级**（如 Global ⊋ Scene/shell），则标 global 的内存占用只落在 `t=Global` 窗口、标 shell 的只落在 `t=shell` 窗口（或按 ⊆ 各自归属）。二者**不在同一窗口求和** ⇒ 物理上同一内存池的占用被拆成两个独立计数 ⇒ 峰值低估（真实并发占用 = 二者之和，但各窗口只看到一部分）。
- 状态 = open（高）。
- 交叉：Iter09 I9-01（同 root）、Iter15 I15-01（⊆ 未定义使过滤更悬空）。

**文档行号**：§7.4（L456、L458、L459）、§3.3.2（L167）、Iter09（I9-01）、Iter15（I15-01）。

---

## H2. 命题：global_scope / shell_scope 本身非合法 ScopeId

**命题**（§3.1.3 L103-113）：`ScopeId := Method(name) | Type(name) | Scene(name) | Global | Loop(id) | Conditional(branch) | Async(id)`。
**命题**（§7.4 L456-459）：scope 字段统一写 `global_scope` / `shell_scope`。

`global_scope` ≠ `Global`（命名不一致，Iter15 I15-03）；`shell_scope` **不在 7 构造子内**（Iter15 I15-02）⇒ 三个内存占用的 scope 字段**类型非法**，连「归一为合法构造子」都需先发明 `shell_scope` 对应哪个（Scene? Type?）。

**数学性质 / 证明状态**：
- **(PO-I28-b) 标注值非法 + 命名不一致（open，高）**：在 ScopeId 类型约束下，Load/Preload/Instantiate 的 scope 字段全部非法 ⇒ §7 映射在类型层不可编译/不可判定。状态 = open（高，交叉 Iter15 I15-02/I15-03）。
- 附注：即便修正命名（`global_scope`→`Global`），`shell_scope` 仍无归宿（Godot 的 Entity-as-Data 中 shell 是「场景外壳」，或对应 Scene 构造子，但文档未定义）。

**文档行号**：§3.1.3（L103-113）、§7.4（L456-459）、Iter15（I15-02/I15-03）。

---

## H3. 命题：net 无 scope 使跨 scope 失配被全局掩盖

**命题**（§3.3.1 L163-165）：`net(S)` 全局求和，无 scope 参数（Iter15 I15-06）。
**命题**：Load 占内存(global) + Instantiate 占内存(shell)，若前者释放标 release(global)、后者释放标 release(shell)（或 move，Iter27）。

即便 scope 标注一致问题不解决，net 全局求和 ⇒ 标 global 的占用与标 shell 的释放**可能匹配也可能不匹配**（取决于 resource 相等性，而 resource 都是 `memory` 字面量 ⇒ 匹配）。但 scope 维度被 net 完全忽略 ⇒ 「某 scope 内内存泄漏」无法暴露（Iter15 I15-06）。即 scope 不一致在 peak 层（低估）与 net 层（掩盖局部失配）**双重失效**。

**数学性质 / 证明状态**：
- **(PO-I28-c) 跨 scope 失配 net 层不可见（open，高）**：net 无 scope ⇒ 即便 peak 因 scope 分裂低估，net 也因无粒度无法定位「哪个 scope 泄漏」。状态 = open（高，交叉 Iter15 I15-06、Iter27 I27-02）。
- 文档行号：§3.3.1（L163-165）、§3.3.2（L167）、Iter15（I15-06）。

---

## H4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (a) 把 `global_scope` 归一为 `Global`、`shell_scope` 归一为 `Scene`（或统一为单一内存 scope `Global`），则内存占用 scope 一致 ⇒ peak 按 `c.scope⊆t` 同窗口求和，H1 低估消解。证明：统一 scope ⇒ 同窗口。前提 PO-I28-a/b（标注未归一）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 §3.1.3 显式定义 `shell_scope` 对应构造子（如 `Scene(name)`）并证明 `Scene ⊆ Global`（Iter34 PO-I34），则 global/shell 在 ⊆ 下可并（shell 窗口的占用计入 global 上界）⇒ H1 的「不可并」变为「可并但需 ⊆ 定义」。证明：⊆ 定义使跨层可并。前提 Iter34 PO-I34（ScopeId⊆ 未定义）未立 ⇒ 条件。
- **P3（discharged，条件）**：若 net 加 scope 分组 `net(S, scope)`（Iter37），则跨 scope 失配在粒度可见。证明：粒度细化。前提 Iter37 未立 ⇒ 条件。
- **P4（discharged）**：在「所有内存占用统一标 Global」弱修正下，peak/net 同 scope 一致，H1/H2/H3 全消解。证明：统一即闭合。但此修正是 PDR 该做（非文档现状），故仍 open。

---

## Proof Obligation 账本（Iter28）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I28-a | 内存占用 global/shell scope 不可并，峰值低估 | open(高) | 统一 scope 标注 | L456/L458/L459, L167, I9-01 |
| PO-I28-b | global_scope/shell_scope 非合法 ScopeId | open(高) | 归一为构造子 | L103-113, L456-459, I15-02/03 |
| PO-I28-c | 跨 scope 失配 net 层不可见 | open(高) | net(scope) 分组 | L163-165, I15-06 |

## 本轮新发现未消解缺口（I28- 前缀，全局唯一）
- **I28-01（高）**：Load/Preload 内存占用标 global_scope、Instantiate 标 shell_scope，同物理内存跨 scope 不可并 ⇒ peak 低估、net 跨 scope 不守恒（交叉 Iter09 I9-01）。
- **I28-02（高）**：global_scope（≠Global 命名）/shell_scope（非 7 构造子）标注值本身非法，连归一都需先发明 shell_scope 归宿（交叉 Iter15 I15-02/I15-03）。
- **I28-03（高）**：net 无 scope ⇒ 跨 scope 失配在全局求和下被掩盖，局部泄漏不可定位（交叉 Iter15 I15-06 / Iter27 I27-02）。
- **I28-04（弱）**：§7 几乎全部 API 标 shell_scope、仅 Load/Preload 标 global_scope ⇒ scope 不一致非偶发，是映射层系统性问题（交叉 Iter15 I15-07 二值退化）。
- **I28-05（弱）**：若 shell_scope 归 Scene、Global 为顶层，定义 Scene⊆Global 后跨层可并——但 ⊆ 未定义故当前仍悬空（交叉 Iter34）。

---

一句话摘要：Load/Preload 内存占用标 global_scope、Instantiate 标 shell_scope，同物理内存跨 scope 不可并致 peak 低估、net 跨 scope 不守恒（I28-01，高，交叉 Iter09），且 global_scope(≠Global)/shell_scope(非构造子) 标注本身非法（I28-02，高，交叉 Iter15），net 无 scope 使失配被掩盖（I28-03，高）——scope 不一致是 §7 映射层系统性问题（I28-04），需 PDR 侧统一 scope 标注并补 ScopeId⊆（Iter34）+net(scope)（Iter37）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter28.md，未读/改其它 audit 文件，聚焦 Load/Preload vs Instantiate scope 不一致，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #28（hy3 单独进程，本轮重跑）」、H1-H4 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I28- 缺口列表；交叉引用真实行号(L456/L458/L459/L103-113/L167/L163-165) 并经 read 确认 §7.4/§3.1.3/§3.3 真实文本"}
  ],
  "changedFiles": ["audit/iter28.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 452, 10)", "result": "passed", "summary": "读取 §7.4 Load/Preload/Instantiate 真实 scope 标注(global_scope vs shell_scope)"},
    {"command": "read PDR (offset 103, 12) + (offset 155, 15)", "result": "passed", "summary": "读取 §3.1.3 ScopeId 构造子与 §3.3.1/§3.3.2 net/peak 过滤确认 scope 不可并"},
    {"command": "write D:/Godot/Cosmos/audit/iter28.md", "result": "passed", "summary": "覆盖写入独立审计 #28"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 H1-H4 四节 + Proof Obligation 账本(H3 项) + 5 条 I28- 缺口", "交叉引用 §7.4/§3.1.3/§3.3.1/§3.3.2/Iter09/Iter15/Iter27/Iter34/Iter37 真实行号"],
  "residualRisks": ["未运行 Analyzer 验证 scope 字段实际生成值（仅基于 §7.4 文本比对）", "shell_scope 归属 Scene 的假设依赖 Iter34 ScopeId⊆ 未本轮读取"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter28.md，独立审计 Load/Preload vs Instantiate 内存占用 scope 不一致",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 scope 分裂致 peak 低估+net 掩盖，需 PDR 侧统一 scope + ScopeId⊆ + net(scope)"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
