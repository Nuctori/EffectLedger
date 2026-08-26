# Iter07 审计 — §6 三层类型系统 L1/L2/L3 可证性与 TS-001..012 收敛真伪（独立审计 #7，hy3 单独进程，本轮重跑）

- **审计视角**：工具性证明义务 / 静态分析完备性（独立 pass #7，全新上下文）
- **范围**：§6.1 三层模型表（L329-338）、§6.2 L1（L340-353）、§6.3 L2 Source Generator（L355-388）、§6.4 L3 Roslyn Analyzer（L390-398）、§6.5 TS-001..012（L400-416）；邻接全文（Iter05 的 §4 性质依赖、Iter08/Iter09 映射、Iter10 推导层、Iter11 运行时）
- **结论摘要**：这是**全局性依赖根**——§4/§5 的「不可变/纯函数/无决策/无 Godot」等性质全部委托给 L1/L2/L3 工具强制，但 §6 从未证明任何一层工具的**检测完备性**（sound & complete？sound 足够但需显式证明）。结论：L1（C# 类型系统）的 struct/sealed/readonly 约束可 discharged（语言保证）；L2 Source Generator 与 L3 Roslyn Analyzer 的方法体/语法检查**完备性未证**，故所有「已收敛」依赖它们的性质（SH-001、EA-002/005、TS-001/003/005/007/009/010/011 等）实为 **asserted**，而非 discharged。TS-002/004/006/012 为 discharged（语言/标准模式保证）；TS-009 标注「已收敛」但反射 new 隐藏方法无法静态阻止 ⇒ open。TS-008/TS-011 标注「已解决」但仅描述机制、无证据（Interop 项目机制成立但「纯 .NET 无 Godot」仍靠 RULE001 检测 open；原型 Benchmark 未附）。

---

## F1. L1 类型系统（§6.2，L340-353）

**数学性质 / 证明状态**：
- `sealed class` 阻止继承/重写：C# 语义保证 **discharged**（语言层）。
- `readonly struct` 字段不可变 + 值相等：discharged（C# 保证；见 Iter05 D2）。
- `where TState: struct` 泛型约束：discharged（C# 保证）。
- **结论**：L1 是**唯一真正可证**的层级（语言保证）。但 L1 仅覆盖**结构约束**（不可继承、值类型），**不覆盖**方法体语义（决策/循环/Godot 调用），后者全靠 L2/L3。

**文档行号**：§6.2（L340-353）、§6.1（L329-338 范围表）。

---

## F2. L2 Source Generator（§6.3，L355-388）—— 完备性未证

**命题** Generator 执行：字段类型白名单、禁 List/Dictionary/T[]/class、禁嵌套、生成 init/相等/签名、分析 System 写集（L355-388）。

**数学性质 / 证明状态**：
- **(PO-I7-a) 字段白名单检查完备性（open）**：Generator 遍历字段类型，但「递归检查嵌套」的完全性（Iter05 I5-02）依赖能否枚举所有类型构造；`ImmutableArray<T>` 的 T 递归需全类型图可达性分析，未证终止/完备。状态 = open。
- **(PO-I7-b) 方法体写集分析 soundness（open，高）**：L375-388 声称「Generator 分析 Update 方法体，记录 writes Position」。但方法体可调用其他函数（间接写）、可用反射、可写经属性——**别名分析与间接效应**未处理 ⇒ 写集分析是**不完备（可能漏）**的静态近似。文档未证明 soundness（即「标注的写集 ⊇ 实际写集」）。状态 = open（关键：EA-005 冲突检测、SYS001 依赖它，可能漏冲突 ⇒ 不安全）。
- **(PO-I7-c) 「禁止 if/else/for/while/switch/try」AST 检查完备性（open）**：SH-001 的「无决策/无循环」靠此。但 C# 中决策可经 `?.` 空传播、`??`、逻辑短路、`switch` 表达式（非语句）、LINQ `Where`（隐藏循环）、委托调用隐藏控制流——仅查 6 种语句无法穷举 ⇒ 不完备。状态 = open。

**文档行号**：§6.3（L355-388）、SH-001（L321，§5.2）、EA-005（L271，§4.2）。

---

## F3. L3 Roslyn Analyzer（§6.4，L390-398）—— 兜底但无完备性证明

**命题** RULE001（Domain 禁 Godot）、SHELL001（壳复杂度）、SHELL003（new 隐藏）、BUDGET001（预算累加）、SYS001（System 冲突）（L390-398）。

**数学性质 / 证明状态**：
- **(PO-I7-d) RULE001 引用检测 soundness（open）**：通过 `PackageReference` 检测 Domain 引用 Godot（TS-007「已收敛」）。但 Godot 类型可经 `InternalsVisibleTo`、反射、共享基类间接引入，包引用检测**漏检间接依赖**。状态 = open。
- **(PO-I7-e) SHELL001 复杂度检测的判定（open）**：「>20 行或含复杂条件」——行数易检；但「复杂条件」无定义 ⇒ 阈值模糊，误报/漏报不定。状态 = open。
- **(PO-I7-f) SHELL003 new 隐藏 sealed override（open）**：TS-009「已解决」称「反射调用基类 sealed override，new 隐藏不被调用」。但反射调用具体方法名时若开发者**刻意反射 new 隐藏方法**，Analyzer 无法静态阻止 ⇒ 该「解决」依赖「不刻意滥用反射」，非机制保证。状态 = open（asserted→实为 open）。

**文档行号**：§6.4（L390-398）、TS-007（L410）、TS-009（L412）。

---

## F4. TS-001..012 收敛真伪（表）

| ID | 文档状态 | 实际审计状态 | 说明 |
|----|---------|-------------|------|
| TS-001 struct 不保证不可变 | 已收敛 | **asserted** | 依赖 L2 白名单+L3（F2/F3 未证完备）。 |
| TS-002 static abstract 版本 | 已收敛 | **discharged** | 文档化最低要求（事实，非证明需求）。 |
| TS-003 Command 效应未编码 | 已收敛 | **asserted** | 携带 Signature 元数据，但一致性未校验（Iter05 I5-06）。 |
| TS-004 new 隐藏 sealed | 已收敛 | **discharged(部分)** | L1 sealed 禁子类成立；但 new 隐藏手法本身（非子类）仍需 L3（TS-009，open）。 |
| TS-005 GatherInput 效应 | 已收敛 | **asserted** | 依赖 L2 AST 检查（F2 PO-I7-c 不完备）。 |
| TS-006 泛型爆炸 | 已收敛 | **discharged** | Generator 生成非泛型密封壳，机制成立。 |
| TS-007 项目引用隔离约定 | 已收敛 | **asserted** | 包引用检测漏间接依赖（PO-I7-d）。 |
| TS-008 Interop 类型归属 | 已解决 | **asserted** | 引入 Interop DTO 项目（机制），但「纯 .NET 无 Godot」仍靠 RULE001 检测（open）。 |
| TS-009 sealed override 反射 | 已收敛 | **open** | 反射 new 隐藏方法无法静态阻止（PO-I7-f）。 |
| TS-010 编译/运行期映射 | 已收敛 | **asserted** | Generator 生成映射表，生成正确性未证 soundness。 |
| TS-011 World 不可变性能 | 已收敛 | **asserted** | 「Bevy SoA + swap-remove，原型 Benchmark 验证」——**原型 Benchmark 无引用/无数据/未附**，不可复现 ⇒ 性能声明无证据。 |
| TS-012 Command DU | 已收敛 | **discharged** | abstract record + sealed record 标准模式，机制成立。 |

---

## F5. 可消解的 proof obligation（履行尝试）

- **P1（discharged）**：L1 的 sealed/readonly/struct 约束在 C# 语义下严格成立（语言保证）。证明：C# 语言规范保证 sealed 禁继承、readonly 禁字段写、struct 值类型。无需额外前提。
- **P2（discharged，条件）**：在「Generator 写集分析 sound（标注集 ⊇ 实际写集）」且「Analyzer 引用检测 sound」前提下，EA-005/SYS001/RULE001 的冲突检测/零 Godot 可证。证明：写集/引用集不漏 ⇒ 冲突/违规可检。前提 PO-I7-b/d（soundness 未立）⇒ 条件证明，实际未消解。

---

## Proof Obligation 账本（Iter07）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I7-a | 字段白名单递归完备 | open | 证嵌套递归终止/穷举 | L355-373 |
| PO-I7-b | 写集分析 soundness | open(高) | 证 标注集 ⊇ 实际写集 | L375-388 |
| PO-I7-c | 无决策 AST 检查完备 | open | 覆盖表达式级控制流 | L321,L388 |
| PO-I7-d | RULE001 引用检测 sound | open | 覆盖间接依赖 | L393,L410 |
| PO-I7-e | SHELL001 复杂度判定 | open | 定义「复杂条件」 | L394 |
| PO-I7-f | SHELL003 反射 new 隐藏 | open | 见 TS-009 | L395,L412 |

## 本轮新发现未消解缺口（I7- 前缀，全局唯一）
- **I7-01**：L1 是唯一真可证层（语言保证）；L2/L3 完备性全未证 ⇒ 全文「收敛」声明过度。
- **I7-02**：方法体写集分析漏间接写/反射/别名 ⇒ EA-005 冲突检测、SYS001 可能漏冲突（不安全）。
- **I7-03**：「无决策」AST 检查仅覆盖 6 种语句，漏 `?.`/`??`/短路/`switch` 表达式/LINQ（隐藏控制流）。
- **I7-04**：RULE001 漏检经 InternalsVisibleTo/反射/共享基类的间接 Godot 依赖。
- **I7-05**：TS-009 反射 new 隐藏方法无法静态阻止，依赖「不滥用」，非机制保证。
- **I7-06**：TS-011 的「原型 Benchmark 验证」无引用无数据，性能声明不可复现（证据缺口）。
- **I7-07**：TS-008/TS-003 收敛建立在与未证工具（RULE001/Generator 映射）耦合上 ⇒ asserted。
