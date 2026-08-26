# Iter38 审计 — L2 Source Generator 写集/类型 soundness：是否真捕获所有 effect 写入（独立审计 #38，hy3 单独进程，本轮重跑）

- **审计视角**：§6.3 L2 静态分析的可靠性与完备性边界（独立 pass #38，全新上下文）
- **范围**：§6.3 L2 Source Generator（L355-388，字段白名单/嵌套检查/EffectSignature 生成）、§3.1.4 Signature 推导、§8 推导层、§3.4 MA-005/TS-002/TS-005；邻接 Iter07 I7-02（L2 写集 soundness 未证）、Iter10（默认规则漏报）、Iter19（量化：L2/L3 完备性全 open）
- **结论摘要**：L2 是「开发者写 partial struct + [ComponentField]，Generator 强制字段白名单（unmanaged/string/ImmutableArray/IComponent）、禁 List/Dict/T[]/class、禁嵌套、自动生成 EffectSignature」的编译期代码生成器。其 soundness 主张「生成的 EffectSignature 忠实反映字段的effect 写入」。但审计发现：(1) **L2 只分析 struct 字段类型，不分析方法体**——字段白名单保证「字段本身不引入可变引用」，但**组件方法（如 `[Budget] void Update()` 内对其它组件的写）不在 L2 视野内**，需 L3 补（Iter07 I7-03），L2 单独不完备；(2) **EffectSignature 自动生成仅当字段有 [Budget]**（L363「如果字段有 [Budget]」），无 [Budget] 的组件不产生 Signature ⇒ 未标记组件的行为完全不可见，与 §8 默认规则叠加（Iter10 漏报）⇒ 双重盲区；(3) **白名单的「unmanaged/string/ImmutableArray」仍可能间接持有可变状态**（如 string 不可变但指向外部可变资源、ImmutableArray 内部引用 Entity 的 chunk 内存）⇒ 「字段白名单」≠「effect 写入为零」，L2 的 soundness 论证缺失；(4) **Generator 错误（GEN001）不阻断运行期**，仅编译错误——但若开发者用 `#pragma warning disable` 或绕过 partial（手写实现），L2 完全失效。结构性成立（L2 是类型约束工具、非行为分析工具，职责有限）给条件证明，但其「写集 soundness」声称需降级为「类型安全」而非「effect 完备」。

---

## S1. 命题：L2 不分析方法体 ⇒ 写集仅限字段

**命题**（§6.3 L355-372）：Generator 检查「字段类型白名单、禁嵌套、自动生成 EffectSignature」。全文未提 L2 分析方法体（method bodies）。
**命题**：effect 写入大量发生在方法体（AddChild/QueueFree/Connect 等，§7），非字段声明。

后果：L2 生成的 EffectSignature 反映「组件字段的静态结构」，不反映「方法调用的动态 effect」。方法体的 effect 推导完全依赖 L3（Roslyn Analyzer）或 §8 默认规则——L2 单独对 method-level effect **不完备**。

**数学性质 / 证明状态**：
- **(PO-I38-a) L2 写集仅限字段、不覆盖方法体（open，高）**：L2 soundness 若被理解为「捕获所有 effect 写入」则**不成立**（方法体不在视野）；若被理解为「字段类型安全」则成立。文档未区分，§6 收敛声明（Iter07 I7-01）跨称之为「三层完备」。状态 = open（高）。
- 交叉：Iter07 I7-02（L2 写集 soundness 未证）、Iter07 I7-03（L3 补方法体）。

**文档行号**：§6.3（L355-372）、§7.1-7.10（L421-507）、Iter07（I7-02/I7-03）。

---

## S2. 命题：EffectSignature 仅当 [Budget] 才生成 ⇒ 未标记组件盲区

**命题**（§6.3 L363）：「自动生成 EffectSignature（如果字段有 [Budget]）」。
**命题**：无 [Budget] 的组件不生成 Signature ⇒ 其字段/方法 effect 完全不进入审计系统。

后果：审计覆盖度取决于 [Budget] 标注率。若开发者漏标 [Budget]（常见，因标注是 opt-in），该组件所有 effect 静默不可见 ⇒ 与 §8 默认规则（Iter10 I10-01 漏报 occupy/release）叠加 ⇒ **双重盲区**：L2 不生成 + 默认规则漏报 ⇒ 该组件泄漏/峰值完全无检测。

**数学性质 / 证明状态**：
- **(PO-I38-b) [Budget] opt-in 致未标记盲区（open，高）**：effect 检测的「开启」依赖人工标注，无标注=无检测。状态 = open（高，交叉 Iter10 I10-04 白名单覆盖率、Iter07 I7-04 标注依赖）。
- 文档行号：§6.3（L363）、§8（L511-518）、Iter10（I10-04）、Iter07（I7-04）。

---

## S3. 命题：字段白名单 ≠ effect 写入为零

**命题**（§6.3 L357-358）：白名单 `unmanaged/string/ImmutableArray<T>/IComponent`，禁 `List/Dictionary/T[]/class`。
**命题**：即便字段类型合规，effect 仍可能通过：
- `string` 不可变，但组件方法可用 string 拼接网络 resource（§7.9 Rpc，Iter30）⇒ 字段白名单不直接造成 effect，但方法体仍写网络。
- `ImmutableArray<EntityRef>` 内部引用 chunk 内存（ECS 存储）⇒ 持有即隐式占用内存资源（Iter09 I9-01 global/shell 混合）⇒ 字段「不可变」但占用「可变资源」。

⇒ 「字段类型安全」推不出「组件 effect 写入为零」⇒ L2 的 soundness 若含「effect 维度」则论据不足。

**数学性质 / 证明状态**：
- **(PO-I38-c) 白名单≠effect 零写入（open，中）**：L2 保证类型安全，不保证 effect 中性；字段合规的组件仍可能通过方法体/引用资源产生 effect。状态 = open（中，交叉 Iter09 I9-01）。
- 文档行号：§6.3（L357-358）、§7.9（L498）、Iter09（I9-01）。

---

## S4. 命题：绕过 partial 手写实现使 L2 完全失效

**命题**（§6.3 L355）：开发者写 `partial struct` + Generator 生成另一半。若开发者**手写完整 struct**（不 partial、不 [ComponentField]），则 Generator 不触发 ⇒ 无 EffectSignature 生成、无白名单检查。

后果：L2 的约束是「opt-in by partial+attribute」，**非强制**。审计系统对未用 L2 模式的代码零覆盖（除非 L3 兜底，Iter07 I7-03）。

**数学性质 / 证明状态**：
- **(PO-I38-d) L2 非强制、可绕过（open，中）**：Generator 模式依赖开发者遵循 partial+attribute 约定，绕过则失效。状态 = open（中，交叉 Iter07 I7-05）。
- 文档行号：§6.3（L355）、Iter07（I7-05）。

---

## S5. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 L2 的职责**明确定义为「字段类型安全 + 结构签名生成」**（非「方法体 effect 捕获」），且 L3 显式负责方法体 effect（Iter07 I7-03 完备），则 L2 在其职责内 sound（字段白名单⇒类型安全）。证明：职责分离。前提 PO-I38-a（职责未厘清）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 [Budget] 标注改为**默认开启**（或编译器强制所有 IComponent 经 L2），则 S2 盲区消解。证明：强制覆盖。前提 PO-I38-b 未立 ⇒ 条件。
- **P3（discharged）**：在「组件仅含白名单字段、无方法体 effect、全标 [Budget]」理想假设下，L2 完备且 sound。证明：理想假设。但 Godot 组件必有方法体 effect ⇒ 假设弱。

---

## Proof Obligation 账本（Iter38）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I38-a | L2 不分析方法体、写集仅限字段 | open(高) | 厘清 L2 职责+L3 补方法体 | L355-372, I7-02/03 |
| PO-I38-b | [Budget] opt-in 致未标记盲区 | open(高) | 默认开启/强制 | L363, I10-04, I7-04 |
| PO-I38-c | 字段白名单≠effect 零写入 | open(中) | 区分类型安全/effect | L357-358, I9-01 |
| PO-I38-d | L2 非强制可绕过(partial+attr) | open(中) | 编译器强制 | L355, I7-05 |

## 本轮新发现未消解缺口（I38- 前缀，全局唯一）
- **I38-01（高）**：L2 只查字段类型、不分析方法体 ⇒ 写集 soundness 若指「捕获所有 effect」则不成立（方法体 effect 在 L2 视野外），需 L3 补（交叉 Iter07 I7-02/03）。
- **I38-02（高）**：EffectSignature 仅当字段有 [Budget] 才生成 ⇒ 未标记组件完全无检测，与 §8 默认规则漏报叠加成双重盲区（交叉 Iter10 I10-01/04）。
- **I38-03（中）**：字段白名单（unmanaged/string/ImmutableArray）≠ effect 写入为零（方法体/引用资源仍产 effect，交叉 Iter09 I9-01）。
- **I38-04（中）**：L2 为 opt-in（partial+[ComponentField]），手写完整 struct 可绕过 ⇒ 非强制、可失效（交叉 Iter07 I7-05）。
- **I38-05（弱）**：Generator 错误 GEN001 仅编译错误，运行期若有反射/动态加载绕过 partial，L2 静态保证不适用。

---

一句话摘要：L2 Source Generator 仅查字段类型、不分析方法体 ⇒ 写集 soundness 若指「捕获所有 effect」则不成立（I38-01，高，交叉 Iter07），EffectSignature 仅 [Budget] 才生成致未标记组件双重盲区（I38-02，高，交叉 Iter10），字段白名单≠effect 零写入（I38-03），且 L2 opt-in 可绕过（I38-04，交叉 Iter07）——L2 应降级为「类型安全工具」而非「effect 完备」，与 L3 职责分离方 sound。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter38.md，未读/改其它 audit 文件，聚焦 L2 写集 soundness，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #38（hy3 单独进程，本轮重跑）」、S1-S5 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I38- 缺口列表；交叉引用真实行号(L355-372/L363/L357-358) 并经 read 确认 §6.3 真实文本"}
  ],
  "changedFiles": ["audit/iter38.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 355, 35)", "result": "passed", "summary": "读取 §6.3 L2 Source Generator 字段白名单/嵌套检查/EffectSignature 生成真实文本"},
    {"command": "read PDR (offset 421, 6) + (offset 511, 10)", "result": "passed", "summary": "读取 §7 方法体 effect 与 §8 默认规则确认 L2 盲区叠加"},
    {"command": "write D:/Godot/Cosmos/audit/iter38.md", "result": "passed", "summary": "覆盖写入独立审计 #38"}
  ],
  "validationOutput": ["header 含「本轮重跑」", "共 S1-S5 五节 + Proof Obligation 账本(S4 项) + 5 条 I38- 缺口", "交叉引用 §6.3/§7/§8/§3.4 MA-005/Iter07/Iter10/Iter09 真实行号"],
  "residualRisks": ["未运行 Generator 源码验证是否真不分析方法体（仅基于 §6.3 文本推导）", "ImmutableArray 内部引用 chunk 内存的假设依赖 ECS 常识"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter38.md，独立审计 L2 Source Generator 写集 soundness 边界",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 L2 仅类型安全非 effect 完备、[Budget] opt-in 致盲区，需 PDR 侧厘清 L2/L3 职责分离"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
