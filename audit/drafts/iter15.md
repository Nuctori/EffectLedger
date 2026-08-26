# Iter15 审计 — ScopeId⊆ 偏序未定义导致 Peak/peak/net 的 scope 过滤悬空（独立审计 #15，hy3 单独进程，本轮重跑）

- **审计视角**：scope 偏序的数学定义 / 派生度量过滤良定义 / §7 标注与 ScopeId 文法一致性（独立 pass #15，全新上下文）
- **范围**：§3.1.3 ScopeId（L103-113）、§3.2.5 Peak（L154）、§3.3.1 net（L163-165）、§3.3.2 peak（L167）、§3.3.3 read/write（L172-173）；邻接 §7 映射 scope 标注（L425-507，shell_scope/global_scope）、§5 Shell 作用域、§14「0 阻塞」（Iter13）、DO-7/DO-8（Iter14/Iter08）
- **结论摘要**：ScopeId 在 §3.1.3 仅定义为 7 个构造子，**从未定义 ⊆ 偏序**；而 §3.2.5 的 `Peak` 与 §3.3.2 的 `peak` 都依赖 `c.scope⊆t`（或 `c.scope ⊆ scope`）做窗口过滤 ⇒ 过滤对象不确定，Peak/peak 数学悬空（open，高）。额外发现：(1) §7 映射全部标 `shell_scope`/`global_scope`，而 `shell_scope` **根本不在 7 构造子内**、`global_scope` 命名与 `Global` 不一致 ⇒ 标注与 ScopeId 文法脱节（open，高）；(2) 文档存在**两个相互矛盾的 Peak 定义**（L154 用 count `|·|` 遍历循环索引 i；L167 用 `Σsize` 遍历 scope 窗口 t），二者既不一致又都依赖未定义的 ⊆（open）；(3) `peak` 的 `max_{t∈scope}` 中 `t` 的遍历域（哪些 scope 是合法窗口？）未列出 ⇒ max 范围未定（open）；(4) `net` 公式（L163-165）实际**不含 scope 参数**，无法按 scope 聚合，与任务描述「net 按 scope 分组」不符 ⇒ 跨作用域泄漏检测数学缺失（open）；(5) 即便补 ⊆ 定义，§7 标注的 scope 层级混乱（QueueFree/Connect/Load/AddChild 各标不同 scope）也无法给出一致嵌套。结构性成立（在显式补 ⊆ 偏序 + 统一 scope 标注文法后 Peak/peak 良定义）给条件证明。

---

## O1. §3.1.3 定义 ScopeId 但缺失 ⊆ 偏序（核心，高）

**命题** §3.1.3（L103-113）：`ScopeId := Method(name) | Type(name) | Scene(name) | Global | Loop(id) | Conditional(branch) | Async(id)`。全文（grep「⊆」「subset」「偏序」「partial order」）无任何位置定义该类型上的二元关系 `⊆`。

**数学性质 / 证明状态**：
- **(PO-I15-a) ⊆ 偏序未定义 ⇒ Peak/peak 过滤悬空（open，高）**：§3.2.5（L154）`Peak(S, scope) = max_{i∈1..ω} |{ c ∈ S×i | c.scope ⊆ scope ∧ c.mode ≠ release }|` 与 §3.3.2（L167）`peak(S, scope) = max_{t∈scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size` 都以 `c.scope ⊆ scope`（或 `c.scope⊆t`）作为「Claim 是否落入窗口」的判定谓词。该谓词依赖 ScopeId 上的偏序 ⊆，但：
  - 没有自反性/反对称性/传递性证明；
  - `Method("Foo") ⊆ Type("Enemy")`？`Loop("i") ⊆ Method("Foo")`？`Conditional("b") ⊆ Loop("i")`？全部未定义 ⇒ 对任意 Signature，`{ c | c.scope ⊆ scope }` 的集合成员资格**不可判定**。
  - 后果：`Peak`/`peak` 的求和域为空/全集/部分集合均可能，值不确定 ⇒ DO-8（峰值检测，L20）所依赖的峰值数字**无数学定义**，属 PDR 级悬空。状态 = open（高）。
- 交叉：Iter09 I9-01 已指 `occupy` 的 scope 标注在 Load/Preload 用 `global`、在 Instantiate 用 `shell`，但彼处讨论的是 global vs shell 的**值不统一**；本项上升为「连 ⊆ 比较本身都未定义」的更根本缺口。

**文档行号**：§3.1.3（L103-113）、§3.2.5（L154）、§3.3.2（L167）、DO-8（L20）。

---

## O2. §7 标注使用 `shell_scope`/`global_scope`，与 7 构造子文法脱节（高）

**命题** §7 全部映射表（L425-507）的 scope 字段统一写为 `shell_scope` 或 `global_scope`（如 `read(tree, path, use, shell_scope)`、`occupy(memory, estimatedSize(T), create, global_scope)`），而 §3.1.3 的 ScopeId 构造子是 `Method/Type/Scene/Global/Loop/Conditional/Async`。

**数学性质 / 证明状态**：
- **(PO-I15-b) `shell_scope` 非合法 ScopeId（open，高）**：`shell_scope` 在 7 构造子中**完全不存在**——既非 `Scene(name)` 也非 `Global` 也非其他。即 §7 给每个 Claim 标注的 scope 值**不属于 ScopeId 类型** ⇒ 与 §3.1.1 `scope ∈ ScopeId`（L84）的类型约束直接冲突，所有 §7 映射在类型层非法。状态 = open（高，文法断裂）。
- **(PO-I15-c) `global_scope` 与 `Global` 命名不一致（open）**：`global_scope` 应对应 `Global` 构造子，但命名不统一（`global_scope` vs `Global`），且无「`global_scope` ⇒ `Global`」的归一化规则（交叉 Iter01 I1-02 Claim 相等/归一化未定义）。状态 = open（弱，但放大 O1 的 ⊆ 判定——连 scope 值的**名字**都未对齐，更无从比较 ⊆）。
- 附加：`type`/`method`/`loop`/`conditional`/`async` 构造子在 §7 映射中**从未出现**（L425-507 全部是 shell_scope/global_scope），即 §3.1.3 设计的 5 个细粒度作用域在真实映射中无一处使用 ⇒ ScopeId 文法与实际标注严重脱节（open，弱）。

**文档行号**：§7.1-7.10（L425-507）、§3.1.1（L80-84）、§3.1.3（L103-113）。

---

## O3. 文档存在两个相互矛盾的 Peak 定义（open）

**命题** §3.2.5（L154）与 §3.3.2（L167）各给一个「峰值」公式：
- L154：`Peak(S, scope) = max_{i∈1..ω} |{ c ∈ S×i | c.scope ⊆ scope ∧ c.mode ≠ release }|`（遍历**循环索引 i**，取**集合计数 |·|**）
- L167：`peak(S, scope) = max_{t∈scope} Σ_{c∈S, c.scope⊆t, c.mode≠release} c.size`（遍历**scope 窗口 t**，取 **Σ size**）

**数学性质 / 证明状态**：
- **(PO-I15-d) 两公式语义不一致（open）**：前者度量「某次循环迭代中非 release Claim 的**个数**」，后者度量「某 scope 窗口内非 release Claim 的 **size 之和**」。二者量纲不同（count vs bytes）、遍历域不同（i∈1..ω vs t∈scope），且都叫「峰值」却不等价 ⇒ 读者/工具不知以哪个为准。若以 L154 为准，则 `size` 字段（DO-7 量纲，Iter14）从未进入峰值；若以 L167 为准，则循环索引 ω 完全不参与（与 §3.2.5 `S×ω` 的循环展开语义脱节，交叉 Iter04 PO-I4-a：`S×ω` 中 ω=∞ 时 Peak 未定义）。状态 = open（文档内部定义冲突）。
- 附加：L154 的 `Peak` 首字母大写、L167 的 `peak` 小写，二者在文中似被视为同一概念的不同表述，但数学上不等价 ⇒ 命名混淆掩盖了语义冲突。

**文档行号**：§3.2.5（L154）、§3.3.2（L167）、§3.2.4（S×ω，L143-147）、Iter04 PO-I4-a。

---

## O4. `peak` 的 `max_{t∈scope}` 遍历域 t 未定义（open）

**命题** §3.3.2（L167）：`peak(S, scope) = max_{t∈scope} ...`，其中 `t` 被描述为「scope 窗口」但**未列出哪些 ScopeId 是合法窗口**。

**数学性质 / 证明状态**：
- **(PO-I15-e) max 遍历域未定（open）**：`t∈scope` 的含义含糊：
  - 若 `scope` 是单个 ScopeId（如 `Global`），`t∈scope` 是「t 取 scope 自身」还是「t 取所有 ⊆ scope 的子作用域」？前者则 max 退化为单值（无意义），后者则依赖 O1 的 ⊆ 才能枚举子作用域 ⇒ 仍悬空。
  - 合法窗口集合（global? 每 method? 每 loop?）未给出 ⇒ `max` 的论域空集/全集不定，峰值上界不唯一。状态 = open。
- 交叉：此缺口与 O1 同源——即便 ⊆ 定义，仍需显式规定「peak 的窗口枚举规则（如所有 Method/Type/Loop ⊆ 给定 scope）」。

**文档行号**：§3.3.2（L167）、§3.1.3（L103-113）。

---

## O5. `net` 公式无 scope 参数，跨作用域泄漏检测数学缺失（open）

**命题** §3.3.1（L163-165）：`net(S) = Σ_{c∈S, c.kind=occupy, c.mode∈{create,move}} c.size − Σ_{c∈S, c.kind=occupy, c.mode=release} c.size`。该式**不含 scope 参数**（任务描述称「net 按 scope 分组求和」，但公式无 scope 绑定）。

**数学性质 / 证明状态**：
- **(PO-I15-f) net 全局化、无 scope 聚合（open）**：`net(S)` 对所有 Claim 一视同仁求和，不区分 scope。后果：
  - 泄漏检测（DO-9，L21）需「某作用域内 occupy(create) 未配 release」才报警；但 net 全局求和 ⇒ 全局 net=0 时，局部作用域内的泄漏（如 QueueFree 标 `move` 而非 `release`，Iter08 PO-I8-a，致 release 项缺失）被其它作用域的 create/release 抵消而**不可见** ⇒ DO-9 数学失效。
  - 任务期望的「net 按 ScopeId 分组」在公式中未实现；若改为 `net(S, scope)` 则又依赖 O1 的 ⊆ 做分组判据（group by = 聚合同 scope 或 ⊆ scope 的 Claim）。状态 = open。
- 交叉：Iter08 I8-01（QueueFree mode=move 致 release 项缺失）+ 本项（net 无 scope 分组）⇒ 泄漏检测两侧同时失守，DO-9 实际不可证。

**文档行号**：§3.3.1（L163-165）、DO-9（L21）、Iter08 PO-I8-a。

---

## O6. 即便补 ⊆，§7 标注层级混乱无法给出一致嵌套（open）

**命题** 即便文档补出 ⊆ 偏序，§7 实际标注也无法形成一致嵌套：QueueFree/Connect/EmitSignal 标 `shell_scope`，Load/Preload 标 `global_scope`，AddChild/MoveChild 标 `shell_scope`，无任何 `Method/Type/Loop` 细粒度标注（O2 已证）。

**数学性质 / 证明状态**：
- **(PO-I15-g) 标注无层级结构 ⇒ ⊆ 即使定义也退化（open）**：若所有 Claim 的 scope 非 `shell_scope` 即 `global_scope`（二值），则合理 ⊆ 只能是 `shell_scope ⊆ global_scope`（或反之）。但 `shell_scope` 非合法构造子（O2），且「shell」与「global」的嵌套方向（shell 是 global 的子集？还是并列？）未定义 ⇒ 即便补 ⊆，也只能得到平凡二值偏序，无法区分「方法内 vs 方法间 vs 循环内」的峰值（DO-8 的细粒度目标落空）。状态 = open（弱，依赖 O1/O2 先立）。

**文档行号**：§7.1-7.10（L425-507）、§3.1.3（L103-113）。

---

## O7. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若显式定义 ScopeId 上的偏序 ⊆（如 `Method/Type/Loop/Conditional/Async/Scene 均 ⊆ Global`，且同层按 name 相等才可比，异层 `Method(m) ⊆ Type(t) ⇔ m 属于 t` 等），并把 §7 标注统一为合法构造子（消除 `shell_scope`，将 shell 归为 `Scene` 或 `Type`），则 `Peak`/`peak` 的过滤谓词良定义（成员资格可判定）。证明：偏序给出 ⇒ `{ c | c.scope ⊆ scope }` 为可判定集合 ⇒ 求和/计数域确定。前提 PO-I15-a/b/c 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若统一两 Peak 定义为「`peak(S, scope)=max_{t: t⊆scope} Σ c.size`」（取 Σsize 语义、遍历 ⊆scope 的窗口），并明确 `t` 的枚举规则（所有 Method/Type/Loop ⊆ scope），则峰值唯一且覆盖循环展开（令 `S` 已含 `S×ω` 展开，删除 L154 的 `max_{i∈1..ω}` 形式）。证明：单一定义消除 O3/O4 冲突。前提 PO-I15-d/e 未立 ⇒ 条件。
- **P3（discharged，条件）**：若 `net` 改为 `net(S, scope)=Σ_{c∈S, c.scope⊆scope, ...} create.size − Σ_{c∈S, c.scope⊆scope, c.mode=release} c.size`（按 ⊆scope 分组），则泄漏检测可在作用域粒度进行，配合 QueueFree mode=release 修正（Iter08）⇒ DO-9 可证。证明：分组 net 暴露局部失配。前提 PO-I15-f + Iter08 PO-I8-a 未立 ⇒ 条件。

---

## Proof Obligation 账本（Iter15）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I15-a | ScopeId ⊆ 偏序未定义 ⇒ Peak/peak 过滤悬空 | open(高) | 定义 ScopeId 上的偏序 ⊆ | L103-113, L154, L167 |
| PO-I15-b | §7 标 `shell_scope` 非合法 ScopeId | open(高) | 统一 scope 标注为 7 构造子 | L425-507, L84, L103-113 |
| PO-I15-c | `global_scope` 与 `Global` 命名不一致 | open(弱) | 归一化命名规则 | L456, L459, L103-113 |
| PO-I15-d | 两个 Peak 定义矛盾(count vs Σsize) | open | 合并为单一定义 | L154, L167 |
| PO-I15-e | peak 的 `max_{t∈scope}` 遍历域 t 未定 | open | 列出合法窗口枚举规则 | L167 |
| PO-I15-f | net 无 scope 参数，跨域泄漏检测缺失 | open | net 加 scope 分组 | L163-165, L21 |
| PO-I15-g | 标注二值化致 ⊆ 退化 | open(弱) | 引入细粒度 scope 标注 | L425-507 |

## 本轮新发现未消解缺口（I15- 前缀，全局唯一）
- **I15-01（高）**：ScopeId（§3.1.3）仅列 7 构造子，全文未定义 ⊆ 偏序；§3.2.5/§3.3.2 的 Peak/peak 都依赖 `c.scope⊆scope` 过滤 ⇒ 过滤集合成员资格不可判定，峰值数字数学悬空（DO-8 失效）。
- **I15-02（高）**：§7 全部映射标 `shell_scope`，而 `shell_scope` 不在 7 构造子内 ⇒ 所有 §7 Claim 的 scope 字段类型非法，与 §3.1.1 `scope ∈ ScopeId` 冲突。
- **I15-03**：`global_scope` 命名与 `Global` 构造子不一致，且无归一化规则 ⇒ 连 scope 值名字都未对齐，⊆ 比较更无从谈起。
- **I15-04**：文档存在两个矛盾 Peak 定义——L154 用 count `|·|` 遍历循环索引 i，L167 用 `Σsize` 遍历 scope 窗口 t；量纲与遍历域均不同，读者/工具无基准。
- **I15-05**：`peak` 的 `max_{t∈scope}` 中合法窗口集合（global? 每 method? 每 loop?）未列出 ⇒ max 论域不定，峰值上界不唯一。
- **I15-06**：`net`（§3.3.1）不含 scope 参数，全局求和；泄漏检测（DO-9）需作用域粒度 net 才能暴露局部失配，当前公式下局部泄漏被全局抵消掩盖（交叉 Iter08 QueueFree mode=move）。
- **I15-07（弱）**：§7 标注退化为 shell_scope/global_scope 二值，即便补 ⊆ 也只能得平凡偏序，无法支撑 DO-8 的细粒度峰值目标。
- **I15-08（弱）**：`Method/Type/Loop/Conditional/Async` 五细粒度构造子在 §7 真实映射中零使用 ⇒ ScopeId 文法与实际脱节。

---

一句话摘要：ScopeId 在 §3.1.3 仅定义 7 构造子、**从未定义 ⊆ 偏序**，而 §3.2.5/§3.3.2 的 Peak/peak 都以 `c.scope⊆scope` 做窗口过滤 ⇒ 峰值数字数学悬空（I15-01，高）；叠加 §7 标 `shell_scope`（非合法 ScopeId，I15-02，高）、两 Peak 定义矛盾（I15-04）、peak 窗口 t 遍历域未定（I15-05）、net 无 scope 参数致泄漏检测失效（I15-06）、标注二值退化（I15-07），DO-8/DO-9 在数学层均无定义基础。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter15.md，未读/改其它 audit 文件，聚焦 ScopeId⊆ 偏序缺失导致 Peak/peak/net 悬空的跨章主题，未 widening scope"}
  ],
  "changedFiles": ["audit/iter15.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 20) + (offset 98, 20)", "result": "passed", "summary": "读取 §3.1.1 Claim、§3.1.2 ResourceId、§3.1.3 ScopeId、§3.1.4 Signature 真实文本"},
    {"command": "read PDR (offset 149, 22) + (offset 155, 20)", "result": "passed", "summary": "读取 §3.2.5 Peak(L154)、§3.3.1 net(L163-165)、§3.3.2 peak(L167)、§3.3.3 read/write(L172-173)"},
    {"command": "grep PDR 「scope|shell|global|method|loop|async」全文", "result": "passed", "summary": "确认 §7 映射标 shell_scope/global_scope、ScopeId 构造子、两 Peak 定义行号"},
    {"command": "write D:/Godot/Cosmos/audit/iter15.md", "result": "passed", "summary": "覆盖写入独立审计 #15"}
  ],
  "validationOutput": ["header 含「独立审计 #15（hy3 单独进程，本轮重跑）」", "共 O1-O7 七节 + Proof Obligation 账本 + 8 条 I15- 缺口", "交叉引用 §3.1.1/§3.1.3/§3.2.5/§3.3.1/§3.3.2/§7(L425-507)/DO-8/DO-9/Iter08/Iter14 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 §7 标注是否实际生成 shell_scope 字符串（仅基于文档 §7 文本比对）", "ScopeId ⊆ 的「合理定义」仅为条件证明假设，未由文档给出"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter15.md，独立审计 ScopeId⊆ 偏序未定义致 Peak/peak/net 悬空",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 §3.1.3 缺 ⊆ 偏序、§7 标 shell_scope 非合法 ScopeId、两 Peak 定义矛盾，需 PDR 侧修正"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read + grep；未读其它 audit 文件"
}
