# Iter42 审计 — §8 推导层默认规则（ED-001..ED-008）的 soundness：默认 Claim 缺 scope 字段、裸 `unknown` 非合法 ResourceId、max-effect 自相矛盾（独立审计 #42，hy3 单独进程）

- **审计视角**：§8 推导层「默认规则」在 §3.1 代数上的良定义性与可判定性（独立 pass #42，全新上下文）
- **范围**：§8.1 自动推导规则（L511-516，白名单 + `未映射 API 默认 { read(unknown, use), write(unknown, use) }`）、§8.2 ED-001..ED-008（L518-528）、§3.1.1 Claim（L78-86）、§3.1.2 ResourceId（L88-99）、§3.1.3 ScopeId（L103-113）、§3.1.4 Signature=ImmutableHashSet<Claim>（L94）、§7 映射（L421-510）；邻接 Iter10 I10-01（默认规则漏报 occupy/release）、Iter15 I15-02/03（global/shell 非合法 ScopeId）、Iter21（Unknown 处理）、Iter26（size 四值口径）、Iter32（size 缺省≡1 / Claim 相等）
- **结论摘要**：§8.1 默认规则 `未映射 API 默认 { read(unknown, use), write(unknown, use) }` 在 §3.1 代数上**不是良定义的 Claim**：(a) 缺强制的 `scope` 字段 ⇒ 不满足 §3.1.1 的 5 元组构造子 ⇒ 非法 Claim；(b) 裸 `unknown` 不是 §3.1.2 的合法 ResourceId（仅 `Tree(Unknown)` 合法）⇒ 需归一但未定义；(c) 无 `scope` 则必隐含 global/shell/async 之类，而它们全非 §3.1.3 枚举（Iter15）⇒ 依赖 Iter34 归一未立；(d) size 缺省=1 与 §3.1.1 一致但与该默认规则自身在 ED-006(60fps)、ED-004(∞) 的口径冲突；(e) **ED-001 称「其他默认最大效应」自相矛盾**——默认 `{read,write use}` 完全不含 `occupy`/`create`/`release` ⇒ 对 DO-9 泄漏检测是**假阴性（欠近似）**而非「最大效应（过近似）」，与 Iter10 I10-01 同根。(f) 大量 effect 由默认规则「推断」为 malformed Claim ⇒ 整个代数（∪/Compatible/net/peak）长在 malformed 值上，ED-001..008 的「已收敛」不实。

---

## V1. 命题：默认 Claim 缺强制 `scope` 字段 ⇒ 不满足 §3.1.1 构造子

**命题**（§3.1.1 L78-86）：`Claim := (kind, resource, mode, scope, size?)`，其中 `scope ∈ ScopeId` 为**必填**（size 才是 `?` 可选）。

**命题**（§8.1 L512-516）：默认规则产出 `read(unknown, use)` / `write(unknown, use)`——仅含 (kind, resource, mode) 三项，**无 scope 字段**。

**后果**：按 §3.1.1 的字面构造子，缺 `scope` 的元组不是合法 `Claim` ⇒ 默认推导产物根本不落在 `Signature := ImmutableHashSet<Claim>`（L94）的载体中 ⇒ §3.2/§3.3 所有以 `Claim` 为操作对象的代数运算（∪、Compatible 的 `c.resource`、net/peak 的 `c.scope`）对默认 Claim **无定义**（输入类型不符）。

**数学性质 / 证明状态**：
- **(PO-I42-a) 默认 Claim 缺 scope ⇒ 非法 Claim（open，高）**：默认规则未给出 scope 的默认来源（既无「scope=global」也无「缺省归一」规则），故默认产物不满足 §3.1.1 构造子 ⇒ 整个推导层的「未映射」产物类型非法。状态 = open（高），交叉 Iter15 I15-02、Iter34（scope 归一草案未采纳）。
- 文档行号：§3.1.1（L78-86）、§8.1（L512-516）、§3.1.4（L94）。

---

## V2. 命题：裸 `unknown` 不是 §3.1.2 的合法 ResourceId

**命题**（§3.1.2 L88-99）：`ResourceId` 10 构造子，`Unknown` 仅作为 `Tree(path: NodePath | Unknown)` 的**判别式子字段**出现，不存在独立的 `unknown` 裸资源。

**命题**（§8.1 L512-516）：默认规则写 `read(unknown, use)`——`unknown` 是裸标识符，非任何 ResourceId 构造子调用。

**后果**：即便补了 scope，`read(unknown, use, scope)` 的 resource 仍是裸 `unknown`，不是合法 `ResourceId` ⇒ 仍非法 Claim。需「裸 `unknown` ⇒ `Tree(Unknown)`」的归一规则，但文档未定义（§3.4 MA-010 把变量 path 归 `Unknown`，但那是 `Tree(Unknown)` 语境，且 MA-010 自身未形式化裸串归一，Iter21、Iter31 PO-I31-b）。

**数学性质 / 证明状态**：
- **(PO-I42-b) 裸 unknown⇒ResourceId 归一未定义（open，高）**：默认规则的 resource 载体非法，需 `unknown → Tree(Unknown)` 或 `Custom("unknown")` 归一，文档未立 ⇒ 默认产物 resource 维度悬空。状态 = open（高），交叉 Iter21（Unknown 处理）、Iter31（裸串 vs 构造子）、§3.4 MA-010（L189）。
- 文档行号：§3.1.2（L88-99）、§8.1（L512-516）、§3.4（L189）。

---

## V3. 命题：默认 scope 的来源未定义 + global/shell/async 全非合法 ScopeId

**命题**（§3.1.3 L103-113）：`ScopeId` 7 构造子 = `Method/Type/Scene/Global/Loop/Conditional/Async`，**无** `shell_scope`/`global_scope`/`async_scope`/`unknown`。

**命题**（§8.1/§8.2 + §7）：默认规则不赋 scope；若按直觉补，候选是 `shell_scope`/`global_scope`/`async_scope`（§7 满屏、ED-007 提 `async_scope`），但三者全不在 §3.1.3 枚举。

**后果**：默认 Claim 的 scope 字段无论「缺失」还是「隐式 global/shell/async」都非法。唯一收口路径是 Iter34 的归一（global_scope→Global、shell_scope→Scene(name)、async_scope→Async(id)），但该草案未被采纳 ⇒ 默认产物 scope 维度持续悬空。

**数学性质 / 证明状态**：
- **(PO-I42-c) 默认 scope 归一缺失（open，高）**：scope 来源无定义且所有直觉候选皆非法 ScopeId ⇒ 默认 Claim 的 scope 维度无法满足 §3.1.1。状态 = open（高），交叉 Iter15 I15-02/03、Iter34（PO-I34-b 未采纳）。
- 文档行号：§3.1.3（L103-113）、§8.1（L512-516）、§7（L421-510）、Iter34（O2）。

---

## V4. 命题：默认 size=1 与 §3.1.1 一致，但与 ED-006/ED-004 口径冲突

**命题**（§3.1.1 L82）：`size ∈ Nat? (可选，默认值为 1)`。

**命题**（§8.1 默认规则）：`read(unknown,use)` / `write(unknown,use)` 未写 size ⇒ 按 §3.1.1 缺省=1。

**一致性**：在「缺省 size ⟺ 显式 1」归一下（Iter32 PO-I32-c 草案），默认规则的 size 口径与 §3.1.1 一致 ⇒ size 维度是五维中**唯一不悬空**的。

**冲突**：但同一推导层内 ED-006（L520「静态保守假设 60fps」）与 ED-004（L519「动态 Instantiate 变量场景标记为 ∞」）引入了**不同**的 size 载体（60fps 频次、∞ 上界），而默认规则对「未映射 API」一律 size=1。即：被默认规则覆盖的 API（绝大多数）size 恒 1，而被 ED-004/ED-006 特判的少数 API size 为 ∞/60fps——**同一推导层存在三套 size 语义并存且无切换规则**，net/peak 的 Σ 混合三套口径 ⇒ 聚合数值不确定（Iter26 四值口径母题）。

**数学性质 / 证明状态**：
- **(PO-I42-d) size 三口径并存无切换规则（open，中）**：默认=1、ED-006=60fps、ED-004=∞ 在 §8 推导层共存，但无「何时用哪套」的判定 ⇒ peak/net 的 size 加和跨口径不可判定。状态 = open（中），交叉 Iter26（size 四值）、Iter32（size_eq）、Iter18（∞ 兜底）。
- 文档行号：§3.1.1（L78-86）、§8.1-8.2（L512-528）。

---

## V5. 命题：ED-001「默认最大效应」自相矛盾——默认规则对 DO-9 是欠近似

**命题**（§8.2 ED-001 L518）：「其他默认最大效应，允许 [EffectOverride] 修正」——声称未映射 API 取**最大（最保守/过近似）**效应。

**命题**（§8.1 L512-516）：实际默认规则 = `{ read(unknown, use), write(unknown, use) }`——**仅 read/write 且 mode 仅 `use`，完全不含 `occupy`/`create`/`release`**。

**后果**：
- 对**峰值/并发**（peak，count/write）而言，`use` 的 read/write 是「保守存在」⇒ 偏安全（过近似），与「最大效应」自洽。
- 对**泄漏检测 DO-9**（Iter27/Iter37：需 `occupy(create)` 与 `occupy(release)` 配对）：默认规则**完全不产 `occupy` claim** ⇒ 未映射 API 的占用/释放行为**不可见** ⇒ 泄漏**假阴性（欠近似）**。这与「最大效应（过近似）」的宣称**直接矛盾**。
- 换言之，ED-001 的「默认最大效应」只在 read/write 维度成立，在 occupy/create/release 维度是**最小化（甚至零）** ⇒ ED-001 的收敛声明依赖错误前提。

**数学性质 / 证明状态**：
- **(PO-I42-e) ED-001 max-effect 宣称与默认规则内容矛盾（open，高）**：默认 `{read,write use}` 对 DO-9 是欠近似（漏 occupy）→「最大效应」不实 → ED-001 未真正收敛。状态 = open（高），交叉 Iter10 I10-01（默认规则漏报 occupy/release 同根）、Iter27（QueueFree 漏算）、Iter37（net(scope)）。
- 文档行号：§8.2 ED-001（L518）、§8.1（L512-516）、§1 DO-9（L21）。

---

## V6. 命题：默认规则与 §7 显式映射的冲突解决 + 覆盖度缺口

**命题**（§8.1 L512-516）：「未映射 API 默认…」——显式映射优先，默认仅兜底未映射者。

**冲突解决表面成立**，但有两处真缺口：
1. **§7 映射仅为样本，非权威全集**。§8 称白名单「约 100 个方法」（L512），但 §7 表格只列 ~50 个（L421-510）；那 100 个白名单的**完整映射不在 PDR 内** ⇒ 哪些 API「已映射（用 §7 样本）」、哪些「未映射（走默认）」在文档内**不可判定** ⇒ 默认规则的实际触发范围不可复现。
2. **默认规则对「零效应 API」的过触发**：默认 `{read,write use}` 施加于**任何**未映射 API，含纯计算/无副作用方法 ⇒ 大量假阳性 Claim 进入 Signature ⇒ peak 被系统性高估、net 被污染（见 V5 的泄漏盲区 + 此处峰值过估，方向相反的两类失真并存）。

**数学性质 / 证明状态**：
- **(PO-I42-f) 默认触发域不可判定 + 零效应 API 过触发（open，高）**：100 白名单权威映射缺失 ⇒ 默认规则边界不可复现；且默认对无副作用 API 仍产 Claim ⇒ 失真。状态 = open（高）。
- 文档行号：§8.1（L512-516）、§7（L421-510）。

---

## V7. 命题：可证性边界——多数 effect 被「推断」为 malformed Claim ⇒ 代数长在非法值上

**命题**：推导层对未映射 API 产 `{read(unknown,use), write(unknown,use)}`（malformed：缺 scope、裸 unknown，V1/V2/V3）。§7 映射虽字段较全，但其 scope 用 `shell_scope`/`global_scope`（非法，Iter15），resource 用 `command_buffer`/`signal_bus`/`"subscribers_"+signal` 等裸串（Iter31），亦非完全合法。

**命题**：§3/§4/§5 的代数性质（∪ 幂等、Compatible、net、peak、Shell 函子）的**证明义务（PO）默认建于「Claim 为合法 §3.1.1 值」之上**。但真实 Signature 由推导层填充，而推导层产物多为 malformed ⇒ 这些 PO 的**前提（well-formedness）未被满足** ⇒ 「已收敛」的代数结论在真实输入上**不成立**。

**数学性质 / 证明状态**：
- **(PO-I42-g) 代数 PO 的 well-formedness 前提被推导层打破（open，高）**：若推导层产 malformed Claim，则 §3.2 的 ∪/Compatible/§3.3 的 net/peak 的「对 Claim 成立」证明对真实 Signature 失效 ⇒ 可证性边界破裂。状态 = open（高），交叉 Iter32（Claim 相等未定义）、Iter19（量化：对象层代数律全缺）、Iter15/Iter31/Iter34。
- 文档行号：§3.1.1-3.1.4（L78-113）、§3.2-3.3（L107-167）、§8（L511-528）。

---

## V8. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 (1) 定义裸 `unknown → Tree(Unknown)` 归一（V2），(2) 默认 scope 取 `Global`（或 Iter34 归一）（V3），(3) 默认 size 同 §3.1.1=1 且峰值口径统一（V4），则默认规则产合法 Claim。证明：补全三字段 ⇒ 满足 §3.1.1 构造子。前提 PO-I42-a/b/c/d 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若默认规则改为**含 `occupy` 维度**（如未知 API 默认也产 `occupy(unknown?, create/release)` 配对或保守 `occupy(unknown, use)`），则 DO-9 泄漏检测不再结构性失明（V5）。证明：补 occupy ⇒ 配对可见。前提 PO-I42-e 未立 ⇒ 条件。
- **P3（discharged，条件）**：若 PDR 补「100 白名单完整映射表」+「零效应 API 豁免规则」，则默认触发域可判定、过触发消除（V6）。证明：映射全集 ⇒ 边界确定。前提 PO-I42-f 未立 ⇒ 条件。
- **P4（discharged）**：在「所有 API 均显式声明于 §7 且 scope/resource 合法」理想假设下，默认规则永不触发 ⇒ V1-V7 全不活化。证明：假设排除默认。但 §7 仅为样本、真实 100 白名单不全 ⇒ 假设弱（V6）。

---

## Proof Obligation 账本（Iter42）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I42-a | 默认 Claim 缺 scope ⇒ 非法 Claim | open(高) | 默认 scope 来源/归一 | L78-86, L512-516, L94 |
| PO-I42-b | 裸 unknown 非合法 ResourceId | open(高) | unknown→Tree(Unknown) 归一 | L88-99, L512-516, L189 |
| PO-I42-c | 默认 scope 归一缺失(global/shell/async 非法) | open(高) | Iter34 归一采纳 | L103-113, L512-516, Iter34 |
| PO-I42-d | size 三口径(1/60fps/∞)并存无切换 | open(中) | size 单一口径规则 | L78-86, L518-520 |
| PO-I42-e | ED-001 max-effect 宣称与默认内容矛盾(对 DO-9 欠近似) | open(高) | 默认补 occupy 维度 | L518, L512-516, L21 |
| PO-I42-f | 默认触发域不可判定+零效应过触发 | open(高) | 100 白名单全集+豁免 | L512-516, L421-510 |
| PO-I42-g | 代数 PO 的 well-formedness 被推导层打破 | open(高) | 推导层产合法 Claim(V1-V3) | L78-113, L107-167 |

## 本轮新发现未消解缺口（I42- 前缀，全局唯一）
- **I42-01（高）**：§8.1 默认规则 `{read(unknown,use), write(unknown,use)}` 缺强制 `scope` 字段 ⇒ 不满足 §3.1.1 5 元组构造子 ⇒ 默认产物非法 Claim，§3.2/§3.3 运算无定义（交叉 Iter15 I15-02、Iter34）。
- **I42-02（高）**：裸 `unknown` 非 §3.1.2 合法 ResourceId（仅 `Tree(Unknown)` 合法）⇒ 默认资源维度悬空，需归一未定义（交叉 Iter21、Iter31 PO-I31-b、§3.4 MA-010）。
- **I42-03（高）**：默认 scope 来源未定义，且 global/shell/async_scope 全非 §3.1.3 枚举 ⇒ 依赖 Iter34 归一未立（交叉 Iter15、Iter34）。
- **I42-04（中）**：size 默认=1 与 §3.1.1 一致，但与同层 ED-006(60fps)、ED-004(∞) 三口径并存无切换规则 ⇒ peak/net 跨口径不可判定（交叉 Iter26、Iter32）。
- **I42-05（高）**：ED-001 称「默认最大效应」，但默认规则不含 `occupy`/`create`/`release` ⇒ 对 DO-9 泄漏检测是假阴性（欠近似），宣称自相矛盾 ⇒ ED-001 未真收敛（交叉 Iter10 I10-01、Iter27、Iter37）。
- **I42-06（高）**：§7 仅 ~50 样本映射、100 白名单权威映射不在 PDR ⇒ 默认规则触发域不可复现；且默认对零效应 API 过触发 ⇒ 峰值过估与泄漏漏检并存（交叉 §7、Iter19）。
- **I42-07（高）**：推导层产 malformed Claim 为主 ⇒ §3/§4/§5 代数 PO 的 well-formedness 前提被打破 ⇒ 「已收敛」结论在真实输入上失效（交叉 Iter32、Iter19、Iter15/31/34）。

---

一句话摘要：§8.1 默认规则 `{read(unknown,use), write(unknown,use)}` 在 §3.1 代数上**非良定义**——缺强制 scope（I42-01，高）、裸 unknown 非合法 ResourceId（I42-02，高）、scope 来源与 global/shell/async 全非法（I42-03，高，交叉 Iter15/34），size 三口径并存（I42-04，中，交叉 Iter26/32），且 ED-001「默认最大效应」自相矛盾——默认规则不含 occupy/create/release 致 DO-9 泄漏检测假阴性（I42-05，高，交叉 Iter10），§7 样本化使默认触发域不可复现且对零效应 API 过触发（I42-06，高），最终推导层主要产 malformed Claim ⇒ 整个代数 PO 的 well-formedness 前提被打破（I42-07，高，交叉 Iter32/19）——ED-001..008 的「已收敛」在真实输入上不实，需 PDR 侧补 scope 来源+unknown 归一+occupy 维度+100 白名单全集+size 单一口径。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter42.md，未读/改其它 audit 文件，聚焦 §8 推导层默认规则 soundness，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #42（hy3 单独进程）」、V1-V8 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I42- 缺口列表；交叉引用真实行号(L78-86/L88-99/L103-113/L512-516/L518-528/L421-510) 并经 read 确认 §3.1/§8/§7 真实文本"}
  ],
  "changedFiles": ["audit/iter42.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 78, 40)", "result": "passed", "summary": "读取 §3.1.1-3.1.3 Claim/ResourceId/ScopeId 构造子，确认默认 Claim 缺 scope、裸 unknown 非法"},
    {"command": "read PDR (offset 511, 35)", "result": "passed", "summary": "读取 §8.1-8.2 默认规则与 ED-001..008 确认 {read,write use} 缺 occupy 与 max-effect 矛盾"},
    {"command": "read PDR (offset 421, 90)", "result": "passed", "summary": "读取 §7 映射确认样本化(约50)与 shell/global_scope 非法、裸串资源(Iter31)"},
    {"command": "write D:/Godot/Cosmos/audit/iter42.md", "result": "passed", "summary": "覆盖写入独立审计 #42"}
  ],
  "validationOutput": ["header 含「独立审计 #42（hy3 单独进程）」", "共 V1-V8 八节 + Proof Obligation 账本(7 项) + 7 条 I42- 缺口", "交叉引用 §3.1/§8/§7/§3.4 MA-010/Iter10/Iter15/Iter21/Iter26/Iter31/Iter32/Iter34/Iter37 真实行号"],
  "residualRisks": ["未运行 Analyzer 验证默认规则实际生成的 Claim 字段（仅基于 §8.1 文本推导）", "unknown→Tree(Unknown) 归一与 Iter21/Iter31 交叉，未本轮重证"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter42.md，独立审计 §8 推导层默认规则 soundness（ED-001..008）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现默认规则产 malformed Claim（缺 scope/裸 unknown）、ED-001 max-effect 自相矛盾、推导层破 well-formedness，需 PDR 侧补 scope 来源+unknown 归一+occupy 维度+100 白名单全集"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
