# Iter32 审计 — `Claim` 相等/归一化规则构造：∪ 幂等、资源去重、size 缺省≡1 的根因（独立审计 #32，hy3 单独进程，本轮重跑）

- **审计视角**：集合代数的相等律缺失——∪ 幂等性、resource 去重、size 缺省归一（独立 pass #32，全新上下文）
- **范围**：§3.1.1 Claim（L78-86）、§3.1.2 ResourceId 10 构造子（L88-99）、§3.1.4 Signature=ImmutableHashSet<Claim>（L94）、§3.2.1 `;`/`||`=∪（L107-113/L126-130）；邻接 Iter01 I1-02（Claim 相等/归一化未定义）、Iter21（Unknown=Unknown 未定义）、Iter30/Iter31（拼接式/裸串 vs 构造子不等价）、Iter19（量化：对象层代数律全缺）
- **结论摘要**：§3.1 给出 Claim 五元组、ResourceId 10 构造子、Signature 为 `ImmutableHashSet<Claim>`，但**从未定义 Claim 的相等律**（两 Claim 何时相等）。这导致 (1) **∪ 幂等性无定义**：`S∪S` 是否 = `S` 取决于 Claim 相等规则——若相等未定义，HashSet 的去重语义（依赖相等）悬空；(2) **resource 去重不可判定**：§3.2.2 约束 `c₁.resource=c₂.resource`、§3.3 聚合按 resource，但 `Tree("a")` 与 `Tree("a")` 是否相等、`Tree(Unknown)` 与 `Tree(Unknown)` 是否相等（Iter21）、拼接式 `self.id+"/"+m` 与构造子 `Network(self.id,m)` 是否相等（Iter30/31）全无规则；(3) **size 缺省≡1 归一**：§3.1.1 `size∈Nat?` 默认 1，但「缺省」与「显式 1」是否相等（同值即等，还是缺省标记≠显式）未定义；(4) **跨 kind 相等**：`read(tree,a,use)` 与 `write(tree,a,use)` 不同 kind 是否「同资源冲突」取决于 Compatible 而非相等——但 resource 相等是 Compatible 前置（§3.2.2），故 resource 相等规则是冲突判定的地基。结构性成立给条件证明（补 `Claim=` 五元组相等 + `ResourceId=` 按判别字段 + size 缺省归一 + Unknown 处理）。

---

## M1. 命题：Claim 相等律缺失 ⇒ ∪ 幂等性/HashSet 去重悬空

**命题**（§3.1.4 L94）：`Signature := ImmutableHashSet<Claim>`。HashSet 依赖元素相等（去重）。
**命题**：§3.1 全文**未定义** `Claim₁ = Claim₂` 的判定（既无 `=` 运算符、也无「两 Claim 相等的充要条件」段落）。

后果：`S₁ ∪ S₂`（§3.2.1/§3.2.2）的结果大小依赖去重——若相等未定义，则 `S∪S` 可能 ≠ `S`（重复元素保留）⇒ 并集不满足幂等 ⇒ §3.1 隐含的集合代数律（A1 幂等、A2 交换、A3 结合）**无定义基础**。

**数学性质 / 证明状态**：
- **(PO-I32-a) Claim 相等未定义致 ∪ 幂等悬空（open，高）**：所有组合律（;` `||`）以 ∪ 为核，∪ 去重依赖相等，相等缺失 ⇒ 组合律数学悬空。状态 = open（高，交叉 Iter01 I1-02、Iter19 对象层代数律全缺）。
- 文档行号：§3.1.4（L94）、§3.2.1（L107-113）、§3.2.2（L126-130）、Iter01（I1-02）。

---

## M2. 命题：ResourceId 相等按判别字段，但 Unknown 与拼接式未覆盖

**命题**（§3.1.2 L88-99）：ResourceId 10 构造子带判别字段：`Tree(path)`、`Self(component)`、`Physics(bodyId)`、`Memory(uid)`、`Disk(path)`、`Signal(name)`、`Gpu(bufferId)`、`AudioMixer(channelId)`、`Network(peerId,method)`、`Custom(name)`。

相等规则**应**为「同构造子且判别字段相等」（如 `Tree(p1)=Tree(p2) ⇔ p1=p2`）。但文档未写出此规则，且两处例外未定义：
- **Unknown**：§3.4 MA-010 把变量 path 归 `Unknown`，但 `Unknown` 是否是一个 ResourceId 构造子？若是，`Unknown=Unknown` 是否（保守）冲突？Iter21 已证 Unknown∧Unknown 在 Compatible 层放行、MA-010 层冲突 ⇒ 相等与冲突策略打架。
- **拼接式/裸串**（Iter30/31）：`self.id+"/"+method` 与 `Network(self.id, method)`、`command_buffer` 裸串与 `Custom("command_buffer")` 是否相等无规则。

**数学性质 / 证明状态**：
- **(PO-I32-b) ResourceId 相等未写+Unknown/拼接式未覆盖（open，高）**：即便「同构造子且字段等」直觉成立，文档未形式化，且 Unknown/拼接式两例外使真实映射（§7 满屏 Unknown 与拼接式）的相等全悬空。状态 = open（高，交叉 Iter01 I1-03、Iter21、Iter30、Iter31）。
- 文档行号：§3.1.2（L88-99）、§3.4 MA-010（L189）、Iter21（I21-01）、Iter30（I30-02）、Iter31（I31-02）。

---

## M3. 命题：size 缺省≡1 归一未定义

**命题**（§3.1.1 L82）：`size ∈ Nat? (可选，默认值为 1)`。
**命题**：`size` 缺省与显式 `size=1` 是否产生「相等 Claim」？即 `occupy(tree,id,create)` 与 `occupy(tree,id,create,1)` 是否相等？若「相等需五元组全等」则不等（前者 size=None、后者 size=1）；若「缺省归一为 1」则等。

这影响 §3.3 聚合：`net`/`peak` 对同一资源不同 size 标注的 Claim 是否合并。文档未定义归一 ⇒ 聚合结果依赖未定义规则。

**数学性质 / 证明状态**：
- **(PO-I32-c) size 缺省归一未定义（open，中）**：缺省 size 与显式 1 的相等性未定 ⇒ 同资源占用聚合数不确定。状态 = open（中，交叉 Iter26 size 四值口径）。
- 文档行号：§3.1.1（L78-86）、§3.3.1（L163-165）、§3.3.2（L167）。

---

## M4. 命题：推荐 Claim 相等规则草案

依集合代数与 §3.1 结构，给出推荐 `Claim=`：
```
Claim₁ = Claim₂ ⇔ kind₁=kind₂ ∧ resource_eq(r₁,r₂) ∧ mode₁=mode₂ ∧ scope_eq(s₁,s₂) ∧ size_eq(sz₁,sz₂)
其中：
  resource_eq(r₁,r₂) := (r₁,r₂ 同构造子) ∧ (判别字段相等) 
                       ∨ (r₁=r₂=Unknown)  [保守：见下]
                       ∨ (拼接式 ⇔ 对应构造子，Iter30/31 归一)
  size_eq(sz₁,sz₂) := (sz₁, sz₂ 均缺省或均=1 ⇒ 等) ∧ (否则数值等)
  scope_eq := (同 ScopeId 构造子且字段等，Iter34 PO-I34)
  Unknown 处理：resource_eq(Unknown,Unknown) 默认 false（保守冲突，Iter21 P1 短路）
```

**数学性质 / 证明状态**：
- **(PO-I32-d) Claim= 草案待采纳（open，草案）**：该规则使 ∪ 幂等、resource 去重、跨 API 配对良定义。但文档未采纳 ⇒ 仍 open。状态 = open（草案，交叉 Iter01 I1-02）。
- 附注：size_eq 的「缺省≡1」需与 §3.4 MA-008 默认 1 一致（Iter26）。

**文档行号**：§3.1.1（L78-86）、§3.1.2（L88-99）、§3.1.4（L94）、Iter21/30/31/34。

---

## M5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 采纳 M4 的 `Claim=` + `ResourceId=` + `size_eq(缺省≡1)` + Unknown 处理，则 ∪ 幂等、S×ω 展开去重、resource 聚合全部良定义。证明：相等律给出 ⇒ 集合操作闭合。前提 PO-I32-a/b/c/d 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 `resource_eq` 吸收 Iter30 拼接式⇔构造子、Iter31 裸串⇔Custom、Iter21 Unknown 短路，则 §7 所有动态/合成 resource 的相等可判定。证明：归一规则覆盖。前提 Iter30/31/21 未立 ⇒ 条件。
- **P3（discharged）**：在「所有 resource 为编译期字面量、无动态/合成」受限假设下，M4 退化为「同构造子+字段等」⇒ 闭合。但 §7 真实满是动态/合成 ⇒ 假设弱。

---

## Proof Obligation 账本（Iter32）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I32-a | Claim 相等未定义致 ∪ 幂等悬空 | open(高) | 采纳 M4 Claim= | L94, L107-113 |
| PO-I32-b | ResourceId 相等未写+Unknown/拼接式未覆盖 | open(高) | 补字段相等+归一 | L88-99, I1-03/I21/I30/I31 |
| PO-I32-c | size 缺省≡1 归一未定义 | open(中) | size_eq 规则 | L78-86, L163-165 |
| PO-I32-d | Claim= 草案待采纳 | open(草案) | PDR 写入 | L78-99, I21/30/31/34 |

## 本轮新发现未消解缺口（I32- 前缀，全局唯一）
- **I32-01（高）**：§3.1 未定义 Claim 相等律 ⇒ ∪ 幂等/HashSet 去重悬空，组合律 A1/A2/A3 无定义基础（交叉 Iter01 I1-02 / Iter19 对象层律全缺）。
- **I32-02（高）**：ResourceId 相等「同构造子+字段等」直觉成立但未形式化，且 Unknown（Iter21）/拼接式（Iter30）/裸串（Iter31）三例外使 §7 真实映射相等全悬空。
- **I32-03（中）**：size 缺省与显式 1 是否相等未定 ⇒ 同资源占用聚合数不确定（交叉 Iter26 四值口径）。
- **I32-04（弱）**：Claim= 是 §3.2.2 约束 `c₁.resource=c₂.resource` 与 §3.3 聚合的地基，缺失则冲突判定与聚合双悬空（交叉 Iter16/Iter23/Iter28）。
- **I32-05（弱）**：scope_eq 依赖 ScopeId⊆ 与合法构造子（Iter34），当前 shell_scope/global_scope 非合法（Iter15 I15-02）⇒ Claim= 的 scope 维度也悬空。

---

一句话摘要：§3.1 未定义 Claim 相等律 ⇒ ∪ 幂等/HashSet 去重悬空、组合律基础缺失（I32-01，高，交叉 Iter01/Iter19），ResourceId 相等未形式化且 Unknown/拼接式/裸串三例外使 §7 真实映射相等全悬空（I32-02，高，交叉 Iter21/Iter30/Iter31），size 缺省≡1 归一未定（I32-03，交叉 Iter26），Claim= 为 §3.2.2 约束与 §3.3 聚合的地基（I32-04）——需 PDR 侧采纳 M4 草案（五元组相等+字段相等+size 归一+Unknown 短路+拼接/裸串归一）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter32.md，未读/改其它 audit 文件，聚焦 Claim 相等/归一化，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #32（hy3 单独进程，本轮重跑）」、M1-M5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I32- 缺口列表；交叉引用真实行号(L78-99/L94/L107-113/L126-130/L163-165) 并经 read 确认 §3.1/§3.2 真实文本"}
  ],
  "changedFiles": ["audit/iter32.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 22)", "result": "passed", "summary": "读取 §3.1.1 Claim / §3.1.2 ResourceId / §3.1.4 Signature 确认相等律缺失"},
    {"command": "read PDR (offset 107, 25)", "result": "passed", "summary": "读取 §3.2.1-3.2.2 ∪ 组合与约束确认去重依赖相等"},
    {"command": "write D:/Godot/Cosmos/audit/iter32.md", "result": "passed", "summary": "覆盖写入独立审计 #32"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 M1-M5 五节 + Proof Obligation 账本(M4 项) + 5 条 I32- 缺口", "交叉引用 §3.1/§3.2/§3.4 MA-010/Iter01/Iter19/Iter21/Iter30/Iter31/Iter34 真实行号"],
  "residualRisks": ["未运行 HashSet 源码验证去重行为（仅基于 §3.1.4 文本推导）", "Unknown/拼接式/裸串 归一依赖 Iter21/30/31 未本轮重证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter32.md，独立审计 Claim 相等/归一化规则构造（∪ 幂等根因）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 Claim 相等律缺失致组合律悬空，需 PDR 侧采纳 M4 草案"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
