# Iter05 独立审计

## 范围 / 结论摘要

- **范围**：PDR_Effect_Cost_Algebra_v3_FINAL.md §4（Entity-as-Data 架构定义，L364–L451），含交叉引用的支撑条款（§6.3 L539–561、§14.2 L1021、§2.2 决策表）。审计对象：EntityId 单调性/不可伪造的可证条件、Component 不可变性与递归约束完全性、Entity 完整性不变量强制点、World 版本单调与寿命估算数字正确性、System 纯函数性的可证机制、EA-001..007 收敛真伪。
- **结论摘要**：
  1. **发现 1 处数值错误**：World U64 寿命「9.7 亿年」（L411）低估约 100 倍，正确值 ≈ 9.74×10¹⁰ 年（974 亿年）。结论方向（不处理溢出）不受影响，但文档作为规范应修正。
  2. **发现 1 处直接内部矛盾**：EA-005（L449）与 §6.3（L558–561）声明「Generator 分析 Update 方法体写集」，而 §14.2 S2（L1021）明确「Generator 不分析方法体内部（iter38 已记 S1-S2 不分析方法体）」。二者不可同真，EA-005 的收敛方案在 rA6 版本文本下自相矛盾，状态应回退为 open。
  3. **发现 1 处定义内矛盾**：§4.1.2 同时允许「其他 IComponent（递归检查，必须扁平化）」（L387）与禁止「嵌套对象」（L388）；且 L386 的 `ImmutableArray<T> where T : IComponentField` 与 L387 的 `IComponent` 是两个未定义相互关系的接口，递归检查的终止条件（环检测）未指定。
  4. Entity 完整性不变量「至少一个 Component」（L395）**无任何强制点**：构造函数约束、SpawnEntity 校验、Analyzer 规则均未提及。
  5. System 纯函数性仅对「无 Godot 引用」有机制（RULE001，L584 区域 / DO-2），对 DateTime.Now、Random、静态可变状态、IO 等 C# 固有不纯源**无任何机制**，纯函数性整体为 asserted 而非 discharged。
  6. EA-001 的分布式复合键 (ServerId:u16, LocalId:u64)（L373）不保序也不天然唯一，单调性证明在 v2 语义下失效，「已收敛」仅在单机前提下成立。

---

## 逐命题小节

### 命题 E-1：EntityId 单调递增且全局唯一（L371–372）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 分配序列 id₁ < id₂ < … < idₙ（严格单调），且 ∀i≠j: idᵢ ≠ idⱼ；由 c₀ + n 计数器模型给出 |
| 状态 | **asserted（有条件下可 discharge）** |
| 论证 | 条件证明：若 (i) 所有 SpawnEntity 经单一分配点串行化执行，(ii) 计数器初值 c₀ 固定，(iii) 每次分配恰好 +1 且无回退，则第 n 次 Spawn 得 id = c₀+n，严格单调与唯一性由 ℕ 的性质直接成立。**缺口**：(a) 文档未指明分配点机制（原子递增？调度器统一分配？）；(b) §2.2（L96–98 区域）声明 System 按写集冲突检测并行调度——两个并行 System 各自产出 SpawnEntity 时，Command[] 的合并顺序决定 ID 顺序，若无确定性 tie-break，则「相同输入相同输出」（L420）与 ID 单调性在并行分支上互相牵制；(c) 「持久性（销毁后不复用）」依赖计数器跨 World 快照/会话恢复不重置，未说明序列化要求。 |
| 行号 | L371–374 |

### 命题 E-2：EntityId 不可伪造（L372）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 不可伪造性 := ∀ 使用中的 id，∃ 恰一次分配事件。这是生成纪律的性质，不是数据表示的性质 |
| 状态 | **open** |
| 论证 | EntityId := U64 是裸类型别名（L371），任何代码均可构造任意 U64 并冒充分配结果。文档给出的三层机制（L1/L2/L3，§6.1）中无一条覆盖此点。反例：System 内手写 `new EntityId(12345)` 绕过分配器，编译期不可检。消解所需最小补充：newtype 封装 + 私有构造函数 + 仅 Allocator 可构造（L1 约束），或 Analyzer 规则禁止 EntityId 字面构造。注意：DestroyEntity(EntityId)（L430）以裸 U64 为参数，扩大伪造面。 |
| 行号 | L371–372, L425–431 |

### 命题 E-3：Component 不可变性（L379–388）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 不可变性 := ∀ 构造后引用，字段值恒等（值语义相等不变）。C# 中 readonly record struct 保证自动属性 init-only，但**不保证**：公共可变字段、ImmutableArray<T> 内部 T[] 元素可变性（T 可变时）、unsafe/ref 逃逸写入 |
| 状态 | **asserted（部分 discharged，存在文本矛盾）** |
| 论证 | (a) **矛盾**：L387 允许「其他 IComponent（递归检查，必须扁平化）」而 L388 禁止「嵌套对象」。若嵌套 IComponent 合法则「扁平化」要求自相矛盾；若禁止嵌套则 L387 多余。二选一须明示。(b) **接口关系悬空**：L386 白名单用 `IComponentField`，L387 用 `IComponent`，二者子型关系、递归检查的接受集定义均未给出。(c) **环终止**：struct 不能直接包含自身，但经 `ImmutableArray<T>` 可构成循环定义（A 含 ImmutableArray<B>，B 含 A）。递归字段检查需显式环检测 + 类型图有限性前提，未指定。(d) **EA-002 收敛方案（L446）依赖 §6.3 示例（L541–542）**：示例声明 `[ComponentField] public float X;` 为公共字段再由 Generator「生成 init-only 属性」（L553 区域）——若 Generator 是追加成员而非替换字段，公共可变字段仍存活，不可变性破防。转换语义（字段→属性 or 字段保留+属性并存）未定义。(e) string 豁免（L385）：string 引用不可变但其值可为 null，且破坏「纯 unmanaged 数据」叙事（R-6 的 <128 bytes 目标不受约束保护）。 |
| 行号 | L376–389, L446, L541–542 |

### 命题 E-4：Entity 完整性——至少一个 Component（L395）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 不变量 I(E) :⇔ |E.components| ≥ 1。保持性：∀ 操作 op，I(E) ⇒ I(op(E)) |
| 状态 | **open** |
| 论证 | 文档只陈述性质（L395），无强制点。候选强制点逐一缺失：① 构造函数/工厂不变式——未提；② SpawnEntity(Archetype, ImmutableDictionary<Type,IComponent>)（L429）可携带空字典，无校验规则；③ SetComponent 只写不删（L431），故「删除至空」路径不存在——这一点是唯一有利证据：若初始非空且无 RemoveComponent 命令，则不变量在 Command 层封闭保持。因此最小消解仅需在 SpawnEntity 入口加一条 L2/L3 检查（空组件字典 ⇒ 编译错误或运行时断言）。另注：逻辑表示 ImmutableDictionary（L394）与物理 SoA Archetype 存储（L403）双表示下，不变量须在两处同时成立，Archetype := ∅ 边界情形未讨论。 |
| 行号 | L391–396, L425–431 |

### 命题 E-5：World 单调增长与版本号递增（L409–411）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | version: World → U64，∀ 状态转移 t: version(W′) > version(W)（严格单调）。注意 entities 映射**非**单调：DestroyEntity 使 |entities| 减小 |
| 状态 | **asserted（措辞歧义 + 数字错误）** |
| 论证 | (a) L410「全函数、单调增长、版本号递增」三性质并列：「单调增长」若指 entities 集合则与 DestroyEntity（L430）矛盾，只能解读为 version 单调——措辞应改为「version 严格递增；entities 无单调性承诺」。(b) **数值错误**：2⁶⁴−1 ≈ 1.8447×10¹⁹；÷60 帧/s ÷ 31557600 s/年 ≈ **9.74×10¹⁰ 年 ≈ 974 亿年**。L411 写「9.7 亿年」（≈9.7×10⁸），低估约 100 倍（疑似把每帧 ~100 次 version 递增隐含计入但未声明）。即使按保守的每帧 10⁴ 次递增仍有 ~9.7×10⁶ 年，结论「不处理溢出」（L411、EA-004 L448）在任何合理解释下成立——错误方向是安全侧，但规范文本数字必须更正并写明假设的每次帧递增次数。(c) version 与 Delta Sync（L508 区域）的消费关系未定义：version 单调如何映射到 Spawned/Modified/Destroyed 三集合的计算，缺一桥接定义。 |
| 行号 | L406–412, L448 |

### 命题 E-6：System 纯函数性与确定性（L417–422）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 纯函数 := f(W, Input) = (W′, Cmds) 且求值无外部可观测效应；确定性 := ∀ W₁=W₂ ∧ Input₁=Input₂ ⇒ W′₁=W′₂ ∧ Cmds₁=Cmds₂（按 Claim 相等/结构相等逐层） |
| 状态 | **asserted（仅一小部分有机制）** |
| 论证 | 有机制的子命题：仅「无 Godot 引用」——由 RULE001/DO-2（TS-007，L594 区域；§6.4）在程序集引用层面机械检查，可 discharge。「纯函数」「确定性」在 C# 中无可证机制被指定：System 体可调用 DateTime.UtcNow、Random、Guid.NewGuid、File.IO、静态可变字段、Task.Run，全部绕过现有 L1/L2/L3 检查清单（L578–590 区域列出的 RULE001/SHELL001/BUDGET001/SYS001 无一涉及 BCL 不纯 API 或 static mutable 检查）。EA-005（L449）的写集分析解决的是调度顺序而非纯度。条件证明（若补足前提则成立）：设 Domain 程序集引用闭包仅含白名单纯 BCL 子集，且 Analyzer 禁止 static mutable 字段与环境时钟/随机源，则纯度归约为「被调函数全体纯」的结构归纳，可证；该前提目前不存在。另注：并行 System 的 Command[] 合并顺序影响 Shell 同态映射结果（Def 5.1.2 #4 要求确定性），需要确定性的命令合并代数（如按 System 拓扑序拼接），文档未给。 |
| 行号 | L414–423, L449 |

### 命题 E-7：EA-001 收敛真伪（L445）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 复合键 (ServerId:u16, LocalId:u64) 上的全局序需额外字典序或时间戳混合才良定义 |
| 状态 | **discharged-with-condition（单机）/ open（v2 分布式）** |
| 论证 | 单机情形由 E-1 的条件证明覆盖，可视为收敛（前提：分配点串行化）。但「已收敛」标签掩盖了 v2 方案的缺陷：两个 ServerId 各自独立递增 LocalId 时，复合键既不保持全局单调（Server A 的 id₅ 与 Server B 的 id₄ 交错）也不保证唯一性协议（ServerId 分配冲突）。文档自己标注「v2 扩展」（L373），诚实做法是把 EA-001 标为「单机 discharged / v2 open」。 |
| 行号 | L373, L445 |

### 命题 E-8：EA-002 收敛真伪（L446）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 工具完备性：违规 Component 定义 ⇒ 必报编译错误（completeness）；合法定义 ⇒ 不报（soundness） |
| 状态 | **partially discharged** |
| 论证 | 白名单 + Analyzer 兜底的方向正确，但受 E-3 四个缺口制约：嵌套矛盾（L387 vs L388）、IComponent/IComponentField 关系悬空、环检测未指定、§6.3 公共字段模式与 init-only 生成的转换语义未定义。此外 Analyzer 对反射/unsafe 写入天然 incomplete（与 §14.2 S2 对方法体的自我设限一致），文档未像 §14.2 那样为本层声明 soundness/completeness 边界。 |
| 行号 | L376–389, L446, L1019–1022 |

### 命题 E-9：EA-003 / EA-006 / EA-007 收敛真伪（L447, L450, L451）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | EA-003：Archetype 的有限类型级表示存在性；EA-007：|components| ≤ 16 的静态可判定约束 |
| 状态 | **asserted / discharged-as-scope（低风险设计约束）** |
| 论证 | EA-003「使用泛型参数**或** Source Generator 生成」中的「或」表明方案未定（低严重度，可接受为 deferred choice，但不应标「已解决」而应标「已定域」）。EA-006 自定义 Command 手动 Signature 是受控逃逸通道，但与 §8.3.1 [EffectOverride] 审查规则（人工 approve）不对齐：手动 Signature 无对应审查流程，属一致性缺口而非错误。EA-007 的 16 上限 + Analyzer 检查机械可执行，discharge 成立；16 与 Archetype 组合数 2¹⁶=65536 的存储影响由 R-9 承接，闭环。 |
| 行号 | L447, L450, L451 |

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ------------------ | ------ |
| PO-E05-01 | EntityId 分配点串行化 + 计数器不回退（含 World 快照恢复场景） | open | 在 Def 4.1.1 中写明：ID 由调度器在命令应用阶段单点分配；持久化 World 须同时持久化计数器；并行 System 的 SpawnEntity 由确定性 tie-break 排序 | L371–374 |
| PO-E05-02 | EntityId 不可伪造 | open | newtype 封装 + 私有构造 + Analyzer 禁止 `new EntityId(...)` 字面构造（含 DestroyEntity 参数来源约束） | L371–372, L430 |
| PO-E05-03 | Component 嵌套规则二义消除 | open | 在 L387/L388 二选一：要么删去嵌套 IComponent 豁免（全扁平），要么明确「允许有限深度嵌套 IComponent，禁止 class 嵌套」并定义 IComponent ⊆ IComponentField 关系 | L386–388 |
| PO-E05-04 | 递归字段检查终止性 | open | 补充环检测算法（类型图 DFS）+ 前提「Component 定义类型图有限无环（经 ImmutableArray 间接环同样拒绝）」 | L387 |
| PO-E05-05 | [ComponentField] 公共字段 → init-only 属性的替换语义 | open | 明确 Generator 以属性**替换**字段（原字段从编译产物移除），否则公共可变字段残留使不可变性破防 | L541–542 |
| PO-E05-06 | Entity 至少一个 Component 的强制点 | open | SpawnEntity 空 components ⇒ 编译错误（L3 检查）；并在 SoA 物理表示处声明空 Archetype 不存在 | L395, L429 |
| PO-E05-07 | World 寿命数字修正 | open | 将 L411 改为「2⁶⁴/(60·k) 年，k 为每帧 version 递增次数上界」并代入实际 k；或径直更正为 ≈9.74×10¹⁰ 年（k=1） | L410–411 |
| PO-E05-08 | version 与 Delta Sync 桥接 | open | 补一条定义：Delta(version v→v+1) 的计算基于 entities 字典 diff + version 作脏标记，满足 Shell(Delta(W→W′)) 与命令应用一致 | L409, L508 区域 |
| PO-E05-09 | System 纯度的 BCL 白名单 + static mutable 禁令 | open | 新增 Analyzer 规则：Domain 禁止 Random/DateTime/Guid/File/Task 及 static mutable 字段；声明本规则的 soundness 边界（与 §14 同格式） | L417–422 |
| PO-E05-10 | 并行 System 的确定性命令合并代数 | open | 定义 Command[] 合并为按拓扑序拼接的满足结合律的算子，使 Shell 确定性（Def 5.1.2 #4）成立 | L449, L486–491 |
| PO-E05-11 | EA-005 与 §14.2 S2 矛盾消解 | **open（内部矛盾）** | 二选一并全文一致：若写集分析确需分析方法体，修订 §14.2 S2 的「不分析方法体」边界声明；若维持 S2，则 EA-005 的收敛方案失效，改由 L3 SYS001 全权承担并降级其保证强度 | L449, L1021 |
| PO-E05-12 | 分布式复合键的序与唯一性 | open（v2） | 为 (ServerId, LocalId) 定义全局序（如 ServerId 时间戳混合，Snowflake 型）与 ServerId 分配协议，或在 v2 规范落地前将 EA-001 标注为「单机 discharged / 分布式 open」 | L373, L445 |

---

## 新发现缺口清单

1. **[矛盾·高] EA-005 方法体分析 vs §14.2 S2**：L449/L558–561 说 Generator 分析 Update 方法体记录写集；L1021 说 Generator 不分析方法体内部。同一版本文本内直接互斥，必使 EA-005 或 §14.2 之一虚假收敛。
2. **[矛盾·中] Component 嵌套规则自斥**：L387 允许递归 IComponent vs L388 禁止嵌套对象；「必须扁平化」与允许嵌套不能同时成立。
3. **[数值·中] 寿命估算错误**：L411「9.7 亿年」应为 ≈974 亿年（k=1），低估 100×；结论方向不受影响，规范文本须更正并显式声明每帧递增次数假设 k。
4. **[缺口·高] System 纯函数性无机制**：除 Godot 引用封锁外，C# 固有不纯源（时钟/随机/静态状态/IO/并发原语）零防护，DO-5 的「System = 纯函数」验收标准当前不可判定。
5. **[缺口·中] EntityId 不可伪造无机制**：裸 U64 别名 + DestroyEntity 接受任意 U64；建议 newtype + 构造封禁。
6. **[缺口·中] 完整性不变量无强制点**：至少一个 Component 仅陈述于 L395；SpawnEntity 空字典路径敞开。
7. **[缺口·低] Type 键与值类型一致性**：Entity.components 以 Type 为键（L394），SetComponent(EntityId, Type, IComponent)（L431）未要求 value.GetType() == key 的静态或动态一致性检查，错位写入（Velocity 键存 Position 值）不被排除。
8. **[措辞·低] 「World 单调增长」歧义**：L410 与 DestroyEntity 的实体减少相抵触，应限定为 version 严格递增。
9. **[缺口·低] 持久化场景的计数器恢复**：「销毁后不复用」（L372）在 World 序列化/重载下需持久化计数器，未提及。
10. **[一致性·低] EA-006 手动 Signature 逃逸通道未接入 §8.3 审查流**：自定义 Command 的手动 Signature 无 reason/approve 要求，与 [EffectOverride] 的治理标准不对齐。
