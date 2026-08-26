# Iter21 审计 — §3.2.3 第1析取 `(use∧use)⇒兼容` 与 MA-010「Unknown 与任何资源冲突」的自相矛盾（独立审计 #21，hy3 单独进程，本轮重跑）

- **审计视角**：冲突判定谓词与保守性策略的局部一致性（独立 pass #21，全新上下文）
- **范围**：§3.2.3 第1析取（L133）、§3.2.2 并行组合约束（L126-130）、§3.1.2 ResourceId.Unknown（L88-99）、§3.4 MA-010（L189）、§7 实战标 Unknown 的 Claim（Load 变量 path L456、Rpc self.id+"/"+method L498）；邻接 Iter16 I16-06（use∧use vs Unknown⊤）、Iter01 I1-03（Unknown⊤ 数学对象缺失）、Iter09 I9-05（网络 resource 编译期 Unknown）
- **结论摘要**：§3.2.3 第1析取 `(m₁=use ∧ m₂=use) ⇒ Compatible=true` 与 §3.4 MA-010「变量 path 保守为 Unknown，与任何资源冲突」在「两个 resource=Unknown 的 use Claim 并行」这一具体情形上**直接给出相反结论**——前者放行（不报冲突），后者要求保守冲突（应报）。文档未规定二者优先级，保守性在「未知资源并行读」情形被静默击穿，构成**漏报**风险（open，高）。该矛盾是 Iter16 I16-06 的局部深化：I16-06 指「第1析取 use∧use 兼容 vs Unknown⊤ 冲突」全局矛盾，本审计给出触发该矛盾的**最小可执行实例**与代数形式化。

---

## A1. 命题：Unknown∧Unknown（use∧use）被第1析取放行，被 MA-010 判冲突

**命题**：设两并行 Claim `c₁ = read(disk, Unknown₁, use, s)`、`c₂ = read(disk, Unknown₂, use, s)`（例如两个 `Load(path)` 调用，path 均为运行时变量 ⇒ 经 MA-010 保守归约为 `Unknown`）。

按 §3.2.3（L133）第1析取：
- `Compatible(c₁.mode, c₂.mode) = Compatible(use, use) = (use∧use) = true` ⇒ §3.2.2（L129）约束 `c₁.resource = c₂.resource ⇒ Compatible(...)` 成立 ⇒ **不报冲突**。

按 §3.4 MA-010（L189）：
- `Unknown 与任何资源冲突`（含另一 `Unknown`）⇒ 两 `Unknown` 资源应判为**冲突** ⇒ 应保守报警。

**矛盾**：同一对 `(c₁, c₂)`，Compatible 规则⇒放行，MA-010⇒冲突。文档无任何「当 resource=Unknown 时跳过第1析取」或「MA-010 优先」的合并规则 ⇒ **哪条为准未定义**。

**数学性质 / 证明状态**：
- **(PO-I21-a) 两规则对 Unknown∧Unknown 结论相反，优先级未定义（open，高）**：`Compatible` 是定义在 `mode` 上的纯谓词（L131-138 仅看 m₁/m₂，不看 resource 值），它对 `Unknown` 资源一视同仁地套用 `use∧use=true`；而 MA-010 是在 `resource` 维度对 `Unknown` 施加的全局保守策略，与 `Compatible` 正交、且未写进 `Compatible` 的任一析取。二者分属「mode 层」与「resource 层」，文档未定义二者在求值时的组合顺序 ⇒ 冲突判定在 Unknown 资源上**悬空**。状态 = open（高）。
- 交叉：Iter16 I16-06 已点名同一矛盾；本项给出最小实例并定位为「组合顺序未定义」。

**文档行号**：§3.2.3（L133 第1析取）、§3.2.2（L129 约束）、§3.1.2（L88-99 ResourceId.Unknown）、§3.4 MA-010（L189）、§7.4 Load（L456）、§7.9 Rpc（L498）。

---

## A2. 实战触发面：哪些 Claim 实际落入 Unknown

**命题**：保守策略 MA-010 将「变量 path」与「字符串拼接 resource」归为 Unknown，故以下 §7 映射在动态参数下都生产 `resource=Unknown` 的 Claim：

| API | Claim（L456/L498） | 何时 resource=Unknown | 触发 A1 矛盾 |
|-----|-------------------|----------------------|--------------|
| `Load<T>(path)` | `read(disk, path, use, shell_scope)` | path 为运行时变量（非字面量） | 两个动态 Load 并行读 disk ⇒ 第1析取放行，MA-010 应冲突 |
| `Preload(path)` | `read(disk, path, use, shell_scope)` | 同上 | 同上 |
| `Rpc(method,args)` | `write(network, self.id+"/"+method, create, shell_scope)` | `self.id+"/"+method` 字符串拼接 ⇒ 编译期 Unknown（Iter09 I9-05） | 两动态 Rpc 写网络 ⇒ 同为 `create` 非 use，**不触发第1析取**，但 MA-010 仍要求 Unknown 冲突 ⇒ 此处矛盾在「create+create」上由 MA-010 直接生效（与注释「create+create 不兼容」一致，无矛盾）；矛盾焦点仍在 `use∧use` 的读类 |
| `Instantiate(scene)` | `read(memory, scene.uid, use, shell_scope)` | scene.uid 动态 ⇒ Unknown | 两个动态 Instantiate 读 memory ⇒ 第1析取放行 vs MA-010 冲突 |

**数学性质 / 证明状态**：
- **(PO-I21-b) 读类 Unknown 并行是矛盾主战场（open，高）**：`write/create` 类的 Unknown（如 Rpc）本就落入「create+create 不兼容」注释，与 MA-010 同向，不矛盾；唯 `read/use` 类的 Unknown（Load/Preload/Instantiate 的读分支）因第1析取 `(use∧use)=true` 与 MA-010 反向 ⇒ **读类 Unknown 并行读是保守性失效的唯一触发面**。状态 = open（高）。
- 交叉：Iter09 I9-05 已指 Rpc resource 编译期 Unknown 致任意两 Rpc 保守冲突（精度），该结论在 create 类成立；本项补充「读类 Unknown 反而因第1析取被放行」的反向精度漏洞。

**文档行号**：§7.4（L456-459）、§7.9（L498-499）、§3.2.3（L133/L138 注释）。

---

## A3. 保守性失效的代数刻画

**命题**：设 `C := Compatible`（§3.2.3），`U(r) := (r = Unknown)`（MA-010 触发谓词）。保守性期望：`U(r₁) ∨ U(r₂) ⇒ ¬C_cons(m₁,m₂)`（任一资源 Unknown ⇒ 冲突）。但文档 `C` 不依赖 `U`，故：

- 取 `(m₁,m₂)=(use,use)`，`U(r₁)=U(r₂)=true`（两 Unknown 读）：
  - 文档 `C(use,use)=true` ⇒ 放行；
  - 保守期望 `¬C_cons=true` ⇒ 应冲突。
  - ⇒ `C(use,use) ≠ C_cons(use,use)` 在 Unknown 域。

**数学性质 / 证明状态**：
- **(PO-I21-c) Compatible 未吸收 MA-010 的 Unknown 策略 ⇒ 漏报（open，高）**：`Compatible` 第1析取是「无条件 true」（不读 resource），而 MA-010 是「resource=Unknown 时强制冲突」。`C` 缺少形如 `U(r₁)∨U(r₂) ⇒ false` 的超前析取，故在 `use∧use` 情形漏掉 Unknown 冲突 ⇒ **保守性在 resource 层被 mode 层规则覆盖**。这是 Iter01 I1-03「Unknown⊤ 数学对象未定义」的具体后果——若 `Unknown⊤` 作为独立数学对象参与 `Compatible` 求值（如 `Compatible` 先判 `resource` 是否含 `Unknown⊤` 再判 `mode`），矛盾可解；但 `Unknown⊤` 全文未定义（Iter01 I1-03），`Compatible` 无从消费它。状态 = open（高）。
- 交叉：Iter01 I1-03（Unknown⊤ 未定义）、Iter16 I16-06（use∧use vs Unknown⊤）、Iter09 I9-05（网络 Unknown）。

**文档行号**：§3.2.3（L131-138）、§3.4 MA-010（L189）、Iter01（I1-03）、Iter16（I16-06）。

---

## A4. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若将 `Compatible` 重定义为「**先判 resource 层、后判 mode 层**」：
  `Compatible'(c₁,c₂) := (U(c₁.resource) ∨ U(c₂.resource)) ? false : Compatible_mode(c₁.mode, c₂.mode)`
  （任一 Unknown ⇒ 直接冲突；否则走原 mode 析取），则 MA-010 与 Compatible 一致，A1/A2/A3 矛盾消解。证明：Unknown 前置析取使 `C_cons(use,use)=false` 当 resource=Unknown，与保守期望对齐。前提 PO-I21-c（Unknown⊤ 数学对象）+ PO-I21-a（优先级）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 `Compatible` 保持纯 mode 谓词，但把 MA-010 的「Unknown 冲突」实现为**外层包装** `ReportConflict(c₁,c₂) := U(c₁.resource)∨U(c₂.resource) ∨ (c₁.resource=c₂.resource ∧ ¬Compatible(c₁.mode,c₂.mode))`，则 Unknown 冲突独立于 mode 层触发，第1析取不再覆盖 Unknown。证明：外层 `ReportConflict` 对 Unknown 短路为 true。前提 PO-I21-a（组合顺序）未立 ⇒ 条件。
- **P3（discharged）**：在「无动态参数（所有 path 为编译期字面量）」的受限工程假设下，`U(r)` 恒 false ⇒ 第1析取与 MA-010 永不冲突（MA-010 不触发）。证明：Unknown 不出现 ⇒ 两规则无交点。但此假设依赖 PDR 未声明的「所有资源路径编译期已知」前提，与 §7.4/§7.9 真实动态参数矛盾。

---

## Proof Obligation 账本（Iter21）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I21-a | Unknown∧Unknown 两规则结论相反、优先级未定义 | open(高) | 定义 Compatible/MA-010 组合顺序 | L133, L189 |
| PO-I21-b | 读类 Unknown 并行是矛盾主战场 | open(高) | 见 A2 触发面 | L456, L498, L133 |
| PO-I21-c | Compatible 未吸收 Unknown 策略⇒漏报 | open(高) | 定义 Unknown⊤ + 前置解析取 | L131-138, L189, I1-03 |

## 本轮新发现未消解缺口（I21- 前缀，全局唯一）
- **I21-01（高）**：§3.2.3 第1析取 `(use∧use)=true` 与 §3.4 MA-010「Unknown 与任何资源冲突」在「两个 resource=Unknown 的 use Claim 并行」上结论相反（放行 vs 冲突），优先级未定义，保守性失效漏报。
- **I21-02（高）**：矛盾触发面仅限**读类 Unknown**（Load/Preload/Instantiate 的 read 分支）；写类 Unknown（Rpc create）本就与「create+create 不兼容」同向，不矛盾——读类并行读是唯一漏报点。
- **I21-03（高）**：`Compatible` 是纯 mode 谓词不读 resource，缺 `U(r₁)∨U(r₂)⇒false` 前置析取；根因是 `Unknown⊤` 数学对象未定义（Iter01 I1-03），`Compatible` 无从消费 Unknown 策略。
- **I21-04（弱）**：§3.2.2 约束 `c₁.resource = c₂.resource ⇒ Compatible(...)` 中「`=`」对 Unknown 资源按 MA-010 应恒为「冲突相关」，但 `=` 自身是否视 Unknown=Unknown 为真未定义（Iter32 PO-I1-a Claim 相等规则），放大本矛盾。

---

一句话摘要：§3.2.3 第1析取 `(use∧use)=true` 与 §3.4 MA-010「Unknown 与任何资源冲突」在「两 resource=Unknown 的 use Claim 并行」上直接冲突（放行 vs 保守冲突），优先级未定义致保守性在**读类 Unknown 并行读**上漏报（I21-01/02，高），根因是 `Unknown⊤` 数学对象未定义使 `Compatible` 无法吸收 MA-010 策略（I21-03，交叉 Iter16 I16-06 / Iter01 I1-03）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter21.md，未读/改其它 audit 文件，聚焦 §3.2.3 第1析取 vs MA-010 的局部矛盾，未 widening scope"}
  ],
  "changedFiles": ["audit/iter21.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 131, 10) + (offset 88, 12) + (offset 189, 3)", "result": "passed", "summary": "读取 §3.2.3 第1析取、§3.1.2 ResourceId.Unknown、§3.4 MA-010 真实文本"},
    {"command": "read PDR (offset 124, 8) + (offset 454, 6) + (offset 496, 6)", "result": "passed", "summary": "读取 §3.2.2 约束、§7.4 Load、§7.9 Rpc 真实标注确认 Unknown 触发面"},
    {"command": "write D:/Godot/Cosmos/audit/iter21.md", "result": "passed", "summary": "覆盖写入独立审计 #21"}
  ],
  "validationOutput": ["header 含「独立审计 #21（hy3 单独进程，本轮重跑）」", "共 A1-A4 四节 + Proof Obligation 账本 + 4 条 I21- 缺口", "交叉引用 §3.2.3/§3.2.2/§3.1.2/§3.4 MA-010/§7.4/§7.9/Iter16 I16-06/Iter01 I1-03/Iter09 I9-05 真实行号"],
  "residualRisks": ["未运行 Roslyn Analyzer 验证 Compatible 实际代码是否消费 Unknown（仅基于文档 §3.2.3 文本推导）", "Claim 相等规则对 Unknown 的处理依赖 Iter32 PO-I1-a 交叉引用"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter21.md，独立审计 §3.2.3 第1析取 vs MA-010 Unknown 冲突的自我矛盾",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现读类 Unknown 并行读被第1析取放行、与 MA-010 保守冲突矛盾，需 PDR 侧补 Unknown⊤ 前置解析取或定义组合顺序"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
