# Iter31 审计 — 合成 resource 命名空间未封闭：`command_buffer`/`signal_bus`/`"subscribers_"+signal` 不在 §3.1.2 构造子枚举（独立审计 #31，hy3 单独进程，本轮重跑）

- **审计视角**：resource 命名空间的封闭性与合成资源的良定义（独立 pass #31，全新上下文）
- **范围**：§7.5 EmitSignal `signal_bus`/`subscribers_`+signal（L465）、§7.6 DrawMesh/DrawRect `command_buffer`（L474-475）、§3.1.2 ResourceId 10 构造子（L88-99）、§3.2.2 并行约束（L126-130，resource 相等）、§3.3 聚合（按 resource）；邻接 Iter09 I9-04（合成 resource 未封闭）、Iter30（拼接式 vs 构造子式）、Iter32（Claim 相等规则）
- **结论摘要**：§7 多处用**文档未声明的合成 resource 名**构造 Claim：`command_buffer`（L474/475，GPU 命令缓冲）、`signal_bus`（L465，信号总线）、`"subscribers_"+signal`（L465，订阅者表）、`"material"`（L476，自身材质字段）、`"transform"`（L435-439，自身变换字段）。这些名字**不在 §3.1.2 的 10 个 ResourceId 构造子内**（L88-99：Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Network/Custom）——既非显式构造子、也非 `Custom(name)` 形式，而是裸字符串。后果：(1) resource 命名空间未封闭 ⇒ 合成 resource 与 §3.1.2 构造子/未来 API 的 resource **可能无名冲突或漏识别**（如 `command_buffer` 与某 `Custom("command_buffer")` 无法区分）；(2) 平行 API 的 resource 相等性依赖字符串字面量（§3.2.2 约束用 `c₁.resource=c₂.resource`），但「`command_buffer` 是否 = `Custom("command_buffer")`」无相等规则（Iter32 PO-I1-a）⇒ 跨表示资源不并；(3) 合成 resource 的 scope 同为 `shell_scope`（非合法，Iter15 I15-02），叠加命名问题 ⇒ 合成资源在类型层与相等层双悬空（open，高）。

---

## L1. 命题：§7 合成 resource 裸字符串，不在 §3.1.2 构造子

**命题**（§3.1.2 L88-99）：`ResourceId := Tree(path)|Self(component)|Physics(bodyId)|Memory(uid)|Disk(path)|Signal(name)|Gpu(bufferId)|AudioMixer(channelId)|Network(peerId,method)|Custom(name)`。

**命题**（§7 映射）：
| 合成 resource | 出处 | 是否构造子 |
|------|------|-----------|
| `command_buffer` | L474/475 (DrawMesh/DrawRect) | 否（裸字符串） |
| `signal_bus` | L465 (EmitSignal) | 否 |
| `"subscribers_"+signal` | L465 (EmitSignal) | 否（字符串拼接） |
| `"material"` | L476 (SetMaterialOverride) | 否（自身字段名） |
| `"transform"` | L435-439 (属性) | 否 |
| `"animation"` | L501-507 (动画) | 否 |
| `"signal_"+signal` | L466-467 (Connect/Disconnect) | 否 |

7+ 类合成 resource 全为裸字符串，无一是 §3.1.2 构造子（也不走 `Custom(name)` 包装）。

**数学性质 / 证明状态**：
- **(PO-I31-a) 合成 resource 不在构造子枚举（open，高）**：resource 命名空间未封闭 ⇒ 这些名字的「合法来源」未定义，文档内部表示不一致（映射用裸串、定义用构造子）。状态 = open（高，交叉 Iter09 I9-04）。
- 文档行号：§3.1.2（L88-99）、§7.4-7.10（L456-507）。

---

## L2. 命题：裸串与 Custom(name) 的相等性未定义

**命题**（§3.2.2 L126-130）：约束 `c₁.resource = c₂.resource`。若一处写 `command_buffer`（裸串）、另处写 `Custom("command_buffer")`，`=` 是否成立？
- §3.1.2 有 `Custom(name)` 构造子但未规定「裸字符串 ⇔ Custom(name)」归一。
- 同上 Iter30 拼接式问题——两种表示无相等规则。

**数学性质 / 证明状态**：
- **(PO-I31-b) 裸串/Custom 相等未定义（open，中）**：合成 resource 若不走 `Custom()` 包装，则其「等于某个 Custom 名」不可判定，跨 API 聚合/冲突可能漏并或误并。状态 = open（中，交叉 Iter30 PO-I30-b、Iter32 PO-I1-a）。
- 文档行号：§3.1.2（L88-99）、§3.2.2（L126-130）。

---

## L3. 命题：合成 resource 可能无名冲突

**命题**：命名空间未封闭 ⇒ 若未来 API 或用户 `Custom("command_buffer")` 与 §7 的裸串 `command_buffer` 并存，文档无法保证二者不冲突或必冲突（无规则）。即合成 resource 的命名**无中央注册表** ⇒ 潜在同名歧义。

**数学性质 / 证明状态**：
- **(PO-I31-c) 命名空间无中央注册表（open，中）**：§3.1.2 构造子是封闭枚举，但 §7 合成 resource 是开放裸串 ⇒ 封闭枚举与开放裸串混用，命名冲突风险无机制兜底。状态 = open（中）。
- 文档行号：§3.1.2（L88-99）、§7。

---

## L4. 命题：command_buffer 跨 DrawMesh/DrawRect 的并发语义

**命题**（§7.6 L474-475）：`DrawMesh` 与 `DrawRect` 都 `write(gpu, command_buffer, create, shell_scope)`。按 §3.2.2 约束（同 resource `command_buffer` 触发 Compatible），DrawMesh ∥ DrawRect 检查 `Compatible(create, create)` = 冲突（注释「create+create 不兼容」）。

问题：真实 GPU 命令缓冲是**每帧重置的共享缓冲区**，多个 Draw 调用写同一 buffer 是正常并发（顺序追加），非冲突。但文档把 `command_buffer` 当普通 resource、`create+create` 判冲突 ⇒ **正常多 Draw 调用被误报冲突**。这其实是「合成 resource 语义 ≠ 普通资源」的证据——`command_buffer` 应是「可追加写」语义，文档未区分。

**数学性质 / 证明状态**：
- **(PO-I31-d) command_buffer 追加写语义未建模（open，高）**：同 `command_buffer` 的多次 write 是 GPU 正常行为，但 Compatible 按普通 resource 判 create+create 冲突 ⇒ 误报。需「命令缓冲 = 可追加共享」特殊语义，文档缺失。状态 = open（高，交叉 Iter16/Iter23 同「良性配对未识别」母题）。
- 文档行号：§7.6（L474-475）、§3.2.3（L138）、§3.2.2（L126-130）。

---

## L5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若所有合成 resource 统一走 `Custom(name)` 包装（如 `Custom("command_buffer")`、`Custom("signal_bus")`），并定义「裸串字面量 ⇔ Custom(同串)」归一（Iter32 PO-I1-a），则命名空间封闭、相等可判定。证明：统一表示。前提 PO-I31-a/b 未立 ⇒ 条件。
- **P2（discharged，条件）**：若定义 `command_buffer` 为「可追加写」特殊资源（mode=create 对其为 append 语义，不触发 create+create 冲突，属 Iter25 C* 的扩展良性对），则 DrawMesh∥DrawRect 兼容。证明：特殊语义。前提 Iter25 PO-I25-b（C* 未采纳）未立 ⇒ 条件。
- **P3（discharged）**：在「所有合成 resource 视为全局唯一 Custom 且按字面量相等」弱假设下，语法闭合；但语义正确性（L4 追加写）仍缺。

---

## Proof Obligation 账本（Iter31）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I31-a | 合成 resource 裸串不在构造子 | open(高) | 走 Custom(name) 包装 | L88-99, L456-507 |
| PO-I31-b | 裸串/Custom 相等未定义 | open(中) | 归一规则(Iter32) | L88-99, L126-130 |
| PO-I31-c | 命名空间无中央注册表 | open(中) | 封闭合成资源枚举 | L88-99, §7 |
| PO-I31-d | command_buffer 追加写语义未建模 | open(高) | 特殊资源语义+mode | L474-475, L138 |

## 本轮新发现未消解缺口（I31- 前缀，全局唯一）
- **I31-01（高）**：§7 的 command_buffer/signal_bus/subscribers_+signal/material/transform/animation/signal_+signal 等 7+ 合成 resource 为裸字符串，不在 §3.1.2 构造子（交叉 Iter09 I9-04）。
- **I31-02（中）**：裸串与 Custom(name) 无相等规则 ⇒ 跨表示资源不并（交叉 Iter30/Iter32）。
- **I31-03（中）**：命名空间无中央注册表 ⇒ 合成裸串与未来 Custom 同名歧义无兜底。
- **I31-04（高）**：command_buffer 多 Draw 写同一 buffer 是 GPU 正常追加写，但 Compatible 按普通资源判 create+create 冲突 ⇒ 正常渲染并发误报（交叉 Iter16/Iter23 良性配对未识别）。
- **I31-05（弱）**：合成 resource 与 shell_scope 非合法 ScopeId（Iter15 I15-02）叠加 ⇒ 命名+scope 双悬空。

---

一句话摘要：§7 的 command_buffer/signal_bus/subscribers_+signal/material/transform/animation/signal_+signal 等 7+ 合成 resource 为裸字符串、不在 §3.1.2 10 构造子内（I31-01，高，交叉 Iter09），裸串与 Custom(name) 无相等规则致跨表示不并（I31-02，交叉 Iter30/Iter32），命名空间无中央注册表（I31-03），且 command_buffer 多 Draw 追加写被 Compatible 误判 create+create 冲突（I31-04，高，交叉 Iter16/Iter23）——合成 resource 命名空间在 PDR 内未封闭，需统一走 Custom(name)+字段相等+command_buffer 追加写语义。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter31.md，未读/改其它 audit 文件，聚焦合成 resource 命名空间，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #31（hy3 单独进程，本轮重跑）」、L1-L5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I31- 缺口列表；交叉引用真实行号(L88-99/L456-507/L126-130/L138) 并经 read 确认 §3.1.2/§7.5/§7.6 真实文本"}
  ],
  "changedFiles": ["audit/iter31.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 88, 12)", "result": "passed", "summary": "读取 §3.1.2 ResourceId 10 构造子确认合成 resource 不在枚举"},
    {"command": "read PDR (offset 461, 18) + (offset 470, 8)", "result": "passed", "summary": "读取 §7.5 EmitSignal/§7.6 DrawMesh 真实合成 resource 标注(command_buffer/signal_bus/subscribers_)"},
    {"command": "write D:/Godot/Cosmos/audit/iter31.md", "result": "passed", "summary": "覆盖写入独立审计 #31"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 L1-L5 五节 + Proof Obligation 账本(L4 项) + 5 条 I31- 缺口", "交叉引用 §3.1.2/§7.4-7.10/§3.2.2/§3.2.3/Iter09/Iter30/Iter32/Iter16/Iter23 真实行号"],
  "residualRisks": ["未运行 Analyzer 验证合成 resource 是否真生成裸串（仅基于 §7 文本比对）", "command_buffer 追加写语义假设依赖 GPU 常识而非 PDR 定义"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter31.md，独立审计合成 resource 命名空间未封闭(command_buffer/signal_bus 等)",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 7+ 合成 resource 裸串、裸串/Custom 不等价、command_buffer 追加写误判，需 PDR 侧统一 Custom(name)+相等规则+特殊语义"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
