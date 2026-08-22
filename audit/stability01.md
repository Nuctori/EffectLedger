# 稳定点审计 — 修订版 v3.0-FINAL-rA 收口复核（独立审计 stability01，hy3 单独进程）

- **审计视角**：修订版 PDR（v3.0-FINAL-rA，含 §3.1 顶部「良性定义修订 A/B/C」注释、§3.4 MA 状态栏「已解决（修订 A）」、§14 历史 v3.0-FINAL-rA 行）是否真稳定——10 根因是否内部自洽、有无新引入矛盾（独立 pass，全新上下文）
- **范围**：§3.1（L79-223）、§3.2（L224-348 修订块）、§3.3（L280-348 + L356-382 残留块）、§7.1 QueueFree（L646）、§7.5（L682-683）、§8.1/§8.3（L730-768）、§9.1（L788-816）、§12.2（L960-984）、§14（L1034）；邻接 iter01/14/15/16/21/23/25/27/31/32/33/34/35/36/37/44/45/46/47/49/50
- **结论摘要**：**不稳定**。修订块（L224-348）内 10 根因的良性定义本身**自洽且可机械执行**，但编辑过程引入一处**结构性重复**——修订块之后残留了一份**未修订的旧版 §3.2.4–§3.3**（约 L349-382），其中 `Compatible` 仍是原非对称版、`⊔` 仍是 malformed 公式、`read/write` 仍用 `|{...}|` 计数（非 SizeVal 求和）、并含一段 stale `peak` 注释。该重复段与上方修订块直接矛盾，且位于 `### 3.4` 之前，构成文档级严重不一致（ST-01，高）。另发现两处未完全闭合的残差（ST-02 合成命名空间未覆盖 `Connect` 的 `Self("signal_"+s)`；ST-03 Deviation 返回类型 `double` 无法表达 `⊤`）。其余 10 根因在修订块内判定为 resolved（open→resolved），但 ST-01 使整份文档作为规范**不可直接引用**（读者取到旧定义）。

---

## 逐根因判定（修订块 L224-348 内）

**根1 — Claim 相等/归一（§3.1.4a, L165-181）**：resolved。五元组逐字段相等 + resource 按 3.1.2b 归一 + Unknown 处理（Unknown=Unknown, Unknown≢已知）完整定义，∪ 幂等/resource 去重/Deviation 对齐/net(scope) 分组均良定义。无新矛盾。**三态：open→resolved**。

**根2 — ScopeId⊆ 偏序（§3.1.3b, L137-157）**：resolved。`⊑` 基础偏序 + `⊑_any` 包含层次 + 嵌套闭包 `⊆*`，Global 最大元，机械可判定。无新矛盾。**三态：open→resolved**。

**根3 — ω 载体 + S×ω（§3.2.5, L272-277）**：resolved。`ω∈ℕ∪{⊤}`、`(S×ω)` 副本求和、`ω=⊤` 返回 ⊤ 兜底。无新矛盾。**三态：open→resolved**。

**根4 — QueueFree mode=release（§7.1, L646）**：resolved。`release(tree, self.id, release)`/`release(memory, self.size, release)` 已改 mode=release，net 计入 −size，DO-9 生效。**三态：open→resolved**。

**根5 — Compatible 全函数+对称（§3.2.3, L247-264）**：resolved（在修订块内）。16 对全函数、CONFLICT 集、`use` 最弱兼容、create+release 配对良性、mode=Unknown 按 use。P1-P4 性质成立。**三态：open→resolved — 但见 ST-01：L349-382 残留旧版非对称 Compatible 仍共存，文档级矛盾**。

**根6 — net(S,scope) 分组（§3.3.1, L285-296）**：resolved。基础 net + `net(S,scope)` 作用域过滤 + Unknown fail-closed 上界。依赖根1/根2 已立。**三态：open→resolved**。

**根7 — DO-7 kind 分桶（§3.1.4b, L183-190 + §3.3.2 weight, L307-316）**：resolved。Sig_read/write/occupy 分桶 + `weight:Kind×Kind→ℝ∪{⊥}` 同 kind=1/跨 kind=⊥，KIND_MIX 编译期报错。无新矛盾。**三态：open→resolved**。

**根8 — L2/L3 完备（§14, L1034）**：仍 open（诚实标注）。修订仅给数学定义，工具层（Roslyn Analyzer / Source Generator）实现完备性未证，§14 已显式留口「工具层完备性仍依赖实现」。**三态：仍 open（预期内，非回归）**。

**根9 — Deviation range=0（§9.1, L808-816）**：resolved。分母 `max(range, ε)` 下界 ε=1 防除零，⊤ 项跳过不 NaN。**三态：open→resolved — 但见 ST-03：返回类型 `double` 与「返回 ⊤」注释冲突**。

**根10 — size 多口径（§3.1.5 + §12.2, L192-204 / L960-984）**：resolved。SizeVal 单一载体统一 (a)(b)(c)(d)；AUDIT002「+10」废除改由 net(S,scope) size 区间机械给出；AUDIT003 64MB/512MB 归一到 SizeVal 区间 [64,64]/[512,512]，与 net/peak 同源。**三态：open→resolved**。

---

## 新发现缺口（ST- 前缀，全局唯一）

- **ST-01（高，新引入结构性重复）**：修订块（L224-348）之后、紧接 `### 3.4`（L383）之前，存在一份**未修订的旧版 §3.2.4–§3.3 残留块**（约 L349-382）：含旧 `⊔ = 保守合并：∀c∈S₁∪S₂, [min(c₁.size,c₂.size), max(...)]`（malformed，c₁/c₂ 未绑定）、旧非对称 `Compatible`（`(m₁=use∧m₂=use)∨...`）、旧 `read(S)=|{c∈S|c.kind=read}|`（集合计数，非 SizeVal 求和）、以及 stale `peak` 注释「ω=1 特例已由 §3.3.2 覆盖，此处不再独立定义」。该块与上方修订块（L224-348）对同一算子给出**两套矛盾定义**，且位于 §3.4 之前，读者/工具解析时取到旧定义 ⇒ 整份 PDR 作为规范不可直接引用。这是编辑过程未整段替换旧 §3.2/§3.3 所致（原文本一份存活）。**必须删除 L349-382 残留块方使文档稳定**。

- **ST-02（中，合成命名空间未闭合 Connect）**：§3.1.2b/§3.1.4a 归一规则（L174-175）声明 `signal_bus ≡ SignalBus(_)` 与 `"signal_"+s ≡ SignalBus(s)`，但 §7.5 `Connect(...)`（L683）实际写 `write(self, "signal_"+signal, create, ...)` ——其 resource 是 `Self(component="signal_"+signal)`（Self 构造子），**非** `SignalBus`。而同节 `EmitSignal`（L682）写 `write(signal_bus, signal, ...)`（即 `SignalBus`）。二者 resource 构造子不同（Self vs SignalBus），归一规则未覆盖 `Self("signal_"+s)≡SignalBus(s)`，故 `Connect` 与 `EmitSignal` 对信号资源的写入**仍不被判定为同一资源** ⇒ 原 iter31 意图（信号资源唯一标识）在 Connect 路径未真闭合。**三态：根1 部分回退（Connect 用例）**。

- **ST-03（弱，Deviation 返回类型冲突）**：§9.1 `CalculateDeviation` 签名 `private static double CalculateDeviation(...)`（L808），但修订注释要求「任一端为 ⊤ ⇒ 该项 Deviation 计为 ⊤ ⇒ 整体标记不可校准」（L814-815）。`double` 无法表达 `⊤` 上界标记 ⇒ 返回类型与语义注释不一致；需改为 `double?` 或独立 `DeviationVal`（含 `⊤`）。不阻塞数学层（注释已声明语义），但实现落地时会编译错误。**三态：新发现（弱）**。

- **ST-04（弱，scope 字面量未入 ScopeId 枚举）**：§7 映射表大量使用 `global_scope`/`shell_scope` 字面（如 L646 `shell_scope`、L682 `shell_scope`、§7.4 `global_scope`），但 §3.1.3 ScopeId 枚举仅含 `Global`/`Method`/`Type`/...，无 `shell_scope`/`global_scope` 构造子。`global_scope` 可解读为 `Global`，但 `shell_scope` 无对应项 ⇒ §7 映射的 scope 字段有部分未落入 §3.1.3 定义域（预存问题，修订未触及）。**三态：新发现（弱，预存）**。

---

## 一句话摘要

修订块（L224-348）内 10 根因的良性定义**自洽、可机械执行、判定 open→resolved**（仅根8 L2/L3 工具完备仍 open 为预期留口），但编辑过程在 `### 3.4`（L383）前残留了一份**未修订旧版 §3.2.4–§3.3**（约 L349-382，含非对称 Compatible / malformed ⊔ / `|{...}|` 计数 / stale peak 注释），与修订块直接矛盾 ⇒ **文档级不稳定（ST-01 高，须删 L349-382 方稳定）**；另 ST-02（Connect 的 `Self("signal_"+s)` 未被合成命名空间归一）、ST-03（Deviation 返回 `double` 无法表达 `⊤`）、ST-04（§7 `shell_scope` 未入 ScopeId 枚举）三处残差。整份 PDR 作为规范**不可直接引用**，须先消 ST-01。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅新建 audit/stability01.md，未读/改其它 audit/*.md，聚焦修订版收口稳定性，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 stability01（hy3 单独进程）」、逐根因 10 节(命题/修订后定义/三态/行号)、ST-01~04 新发现缺口；交叉引用真实行号(L79-223/L224-348/L349-382/L383/L646/L682-683/L730-768/L788-816/L960-984/L1034) 并经 read 确认 §3.1/§3.2/§3.3/§7.1/§7.5/§8.1/§8.3/§9.1/§12.2/§14 修订版真实文本"}
  ],
  "changedFiles": ["audit/stability01.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 79, 145) + (offset 224, 160)", "result": "passed", "summary": "读取 §3.1 修订块与 §3.2/§3.3 修订块，确认 10 根因定义"},
    {"command": "grep PDR '### 3.2 组合律|### 3.3 派生度量|### 3.4'", "result": "passed", "summary": "发现两个 '### 3.3 派生度量' 头（L280/L361）与 §3.4 在 L383，坐实 L349-382 残留旧块"},
    {"command": "read PDR (offset 349, 34) + (offset 356, 28)", "result": "passed", "summary": "读取残留旧块 L349-382，确认含非对称 Compatible/malformed ⊔/|{...}| 计数/stale peak 注释"},
    {"command": "read PDR (offset 646,1)+(offset 682,2)+(offset 730,38)+(offset 788,30)+(offset 960,28)+(offset 1034,1)", "result": "passed", "summary": "读取 §7.1 QueueFree/§7.5/§8.1/§8.3/§9.1/§12.2/§14 确认修订内容与 ST-02/03/04"},
    {"command": "grep PDR 'signal_bus|command_buffer|Connect(|global_scope|shell_scope'", "result": "passed", "summary": "确认 Connect 用 Self(\"signal_\"+s) 未被 SignalBus 归一（ST-02）与 shell_scope 未入枚举（ST-04）"}
  ],
  "validationOutput": [
    "header 含「独立审计 stability01（hy3 单独进程）」",
    "10 根因三态：9 项 open→resolved（根1/2/3/4/5/6/7/9/10），1 项仍 open（根8 L2/L3 工具完备，预期留口）",
    "ST-01（高）：L349-382 残留未修订旧版 §3.2.4–§3.3，与修订块矛盾，文档级不稳定",
    "ST-02（中）：Connect 的 Self(\"signal_\"+s) 未被 SignalBus 归一",
    "ST-03（弱）：Deviation 返回 double 无法表达 ⊤",
    "ST-04（弱）：§7 shell_scope 未入 ScopeId 枚举"
  ],
  "residualRisks": ["未运行解析器实测双 §3.3 头对工具的影响（仅基于 grep+read 文本证据）", "ST-02/03/04 修复需回写 PDR 正文，本审计未改"],
  "noStagedFiles": true,
  "diffSummary": "新建 audit/stability01.md，独立审计修订版 v3.0-FINAL-rA 收口稳定性：10 根因在修订块内 resolved，但发现 ST-01 结构性重复（须删 L349-382）、ST-02/03/04 残差",
  "reviewFindings": ["blocker: ST-01 高 — PDR 在 L349-382 含未修订旧版 §3.2.4–§3.3，与 L224-348 修订块矛盾，整份文档不可直接引用，须删除残留块后方稳定"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件；诚实结论：修订数学定义自洽，但文档结构因双份 §3.2/§3.3 而未稳定"
}
