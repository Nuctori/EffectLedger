# Iter05 审计 — §4 Entity-as-Data 公理与证明义务（独立审计 #5，hy3 单独进程，本轮重跑）

- **审计视角**：对象不变式 / 公理（独立 pass #5，全新上下文）
- **范围**：§4.1.1-4.1.7（EntityId/Component/Entity/Archetype/World/System/Command）；§4.2 EA-001..007
- **结论摘要**：§4 的对象均带「性质」陈述（单调性、不可变性、完整性、全函数、纯函数性…），但几乎全无**可证条件**——即「在何种机制下成立」。其中 World.version 单调性、EntityId 单调性可在给定封装下 discharged；Component 不可变性、System 纯函数性、Entity 完整性、Command Signature 维护性均**无法在 §4 自身机制下证明**（依赖 §6 的 L1/L2/L3 工具，而 §6 的工具性又未形式化——见 Iter07），故为 open。EA-002/005/006 的「已收敛」建立在未形式化的 Analyzer/Generator 兜底上，实为 asserted。EA-003/004/007 收敛真伪见末表。另发现明确**事实错误**：§4.1.5 L235「U64 在 60fps 下可用 9.7 亿年」数量级错（应为约 309 年，高估约 3 千万倍）。

---

## D1. EntityId 单调性（§4.1.1）

**命题** EntityId := U64 单调递增、全局唯一、不可伪造、持久（销毁不复用）；分布式 v2 `(ServerId:u16, LocalId:u64)`。

**数学性质 / 证明状态**：
- 单进程 U64 计数器单调：`id_{n+1}=id_n+1` ⇒ 严格增。在「中央分配器封装、外部不可直接构造」前提下 **discharged**（前提是 `EntityId` 构造受限，但 §4.1.1 未说明构造器封装 ⇒ **(PO-I5-a) 构造受限未声明**，open 弱）。
- 「不可伪造」：在 C# 中 EntityId 若为 public struct 则可被任意构造 ⇒ 不可伪造**不成立**，除非构造器私有 + 工厂。open（与 EA-002 同根：struct 不保证不变）。
- 分布式 v2 复合键：仅声明扩展方向，无冲突/重复证明需求在此 stage。N/A。

**文档行号**：§4.1.1（L197-202）、EA-001（L267）。

---

## D2. Component 不可变性（§4.1.2）

**命题** `readonly record struct`；值类型、不可变（init-only）、字段类型白名单、禁止引用类型/嵌套。

**数学性质 / 证明状态**：
- `readonly record struct` 提供**编译期+运行时值相等**与**不可变字段**（C# 保证）：discharged（机制成立）。
- **(PO-I5-b) 「不可变」需字段全 readonly 且无逃离引用**：白名单禁止 List/Dictionary/T[]/class（L215），但 `string`（L212 豁免）与 `ImmutableArray<T>` 是引用类型——`string` 不可变故安全；`ImmutableArray<T>` 不可变故安全。然而「`ImmutableArray<T> where T:IComponentField`」且 T 可含 `ImmutableArray` 递归 ⇒ 深层不可变依赖递归约束**完全性**，仅声称「必须扁平化」（L214）。open（递归不可变未被证明）。
- EA-002「已收敛」依据 L2 Generator 字段白名单 + L3 Analyzer 兜底。但 L2/L3 的**检查完备性未证明**（Iter07）→ 实为 **asserted**。

**文档行号**：§4.1.2（L204-216）、EA-002（L268）、§6.3（L364-373）。

---

## D3. Entity / Archetype / World（§4.1.3-4.1.5）

- **Entity 完整性**「至少一个 Component」（L221）：`components` 非空不变量。在强制 `SpawnEntity` 携带非空 Archetype 前提下 discharged；但 §4.1.7 Command 定义未写「非空校验」⇒ **(PO-I5-c) 完整性不变量强制点未指定**，open。
- **Archetype = Set<ComponentType>**：相等 = 集合相等。但 ComponentType 是运行时 `Type`/`ComponentType` 标识 ⇒ 依赖 Iter01 I1-02（跨表示等价）。open（桥接）。
- **World.version U64 单调增长**（L233「版本号递增」「单调增长」）：在「每次变更 version+1」封装下 discharged；溢出声明「不处理」（EA-004 已收敛）。但 §4.1.5 L235「U64 在 60fps 下可用 9.7 亿年」——该数字未给推导（9.7 亿年 = 2^64 / 60 / 365/24/3600 ≈ ? 实际约 9.77×10^9 秒 ≈ 309 年，**非 9.7 亿年**）。**(PO-I5-d) 寿命估算数量级错误**，open（事实错误）。
  - 复核：2^64 ≈ 1.84×10^19。每秒 60 增 ⇒ 年增 60×3600×24×365≈1.89×10^9。1.84e19/1.89e9 ≈ 9.74×10^9 秒 ≈ **309 年**。文档「9.7 亿年」高估约 3千万倍。属明确事实错误。

**文档行号**：§4.1.3（L218-222）、§4.1.4（L224-229）、§4.1.5（L231-236）、EA-004（L270）。

---

## D4. System 纯函数性（§4.1.6）

**命题** `System: (World, Input) → (World, Command[])`；纯函数、确定性、无 Godot、经 Archetype 查询。

**数学性质 / 证明状态**：
- **(PO-I5-e) 纯函数性不可由 §4 自身保证**：C# 方法体内可写 `static` 可变状态、可调用 Godot、可随机。纯函数性依赖 §6 的 L2 Generator 方法体分析 + L3 Analyzer（SHELL001/SYS001），而这些工具的**检测完备性未证明**（Iter07）。故「纯函数」在此为**契约声明**，非可证性质 ⇒ open。
- 「确定性：相同输入相同输出」：同上，依赖无副作用 + 无随机源，open（声明）。

**文档行号**：§4.1.6（L238-246）、§6.4（L390-398）。

---

## D5. Command Signature（§4.1.7 / EA-006）

**命题** 每个 Command 携带预定义 `Signature`（ImmutableHashSet<Claim>）。

**数学性质 / 证明状态**：
- **(PO-I5-f) Signature 与 Command 行为保证一致性（open）**：文档仅说「携带预定义 Signature」，未要求 Signature 必须等于该 Command 实际触发的 Godot API 的 Claim 集合（§7 映射）。Signature 是手工维护的元数据（EA-006「自定义 Command 需手动实现 Signature」），存在**声明与实际不符**的可能且无校验机制 ⇒ 签名可信性 open。
- EA-006「已收敛」基于「核心框架定义，自定义手工」——收敛的是「有机制」，不是「机制正确」。

**文档行号**：§4.1.7（L248-261）、EA-006（L272）。

---

## D6. EA-003/004/007 收敛真伪（补充）

| ID | 文档状态 | 实际审计状态 | 说明 |
|----|---------|-------------|------|
| EA-003 Archetype 编译期表示 | 已解决 | **discharged(机制)** | 泛型参数或 Source Generator 生成 Archetype 表示——机制成立；但生成映射表的**正确性**依赖 Iter07 L2 soundness 未证（桥接）。 |
| EA-004 World 版本溢出 | 已收敛 | **discharged(事实)** | U64 溢出概率可忽略（见 D3 复核：约 309 年/帧增），「不处理」合理。但 L235 数字错误需修（PO-I5-d）。 |
| EA-007 Component 数量上限 | 已收敛 | **asserted** | 「限 16 个/Entity，SoA 存储，Analyzer 检查」——16 上限的**依据**（性能/缓存线）未给；Analyzer 检查完备性未证（Iter07）。 |

---

## D7. 可消解 proof obligation（履行尝试）

- **P1（discharged）**：给定「EntityId 经私有构造+中央分配器」，则 EntityId 严格单调且不可伪造。证明：构造器唯一 ⇒ 所有 id 经同一计数器 ⇒ 单调；外部无构造入口 ⇒ 不可伪造。前提需补 PO-I5-a。
- **P2（discharged）**：`readonly record struct` + 全白名单字段 ⇒ Component 值相等且不可变（表层）。证明：C# readonly struct 语义。深层递归（ImmutableArray<T>）见 PO-I5-b。

---

## Proof Obligation 账本（Iter05）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I5-a | EntityId 构造受限 | open(弱) | 私有构造+工厂 | L197-202 |
| PO-I5-b | Component 递归不可变 | open | 证递归约束完全性 | L204-216 |
| PO-I5-c | Entity 完整性强制点 | open | 在 Spawn 校验非空 | L218-222, L248-261 |
| PO-I5-d | World 寿命估算数量级错误 | open(事实) | 修正为 ~309 年 | L235 |
| PO-I5-e | System 纯函数可证性 | open | 见 Iter07 | L238-246 |
| PO-I5-f | Command Signature 一致性 | open | 校验 Sig=实际 Claim | L248-261, §7 |

## 本轮新发现未消解缺口（I5- 前缀，全局唯一）
- **I5-01**：EntityId「不可伪造」在 public struct 下不成立，需构造封装（PO-I5-a）。
- **I5-02**：`ImmutableArray<T>` 递归不可变性未被证明（仅「必须扁平化」声明）。
- **I5-03**：World.version 寿命「9.7 亿年」数量级错误，应为约 309 年（2^64/60/s 复核）——**明确事实错误**。
- **I5-04**：Entity 完整性不变量无强制点（Spawn 未声明非空校验）。
- **I5-05**：System「纯函数」是契约声明，非 §4 可证性质，依赖未形式化工具（Iter07）。
- **I5-06**：Command Signature 与 §7 Godot API 实际 Claim 无一致性校验机制（声明≠行为）。
- **I5-07**：EA-002/005/006「已收敛」建立在未形式化的 L2/L3 兜底上，实为 asserted 而非 discharged。
- **I5-08**：EA-007「16 个 Component 上限」无依据（性能/缓存线），Analyzer 检查完备性未证（Iter07）。
