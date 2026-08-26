# Round 07 — Rich Hickey 视角对抗性审计：Open/Closed 扩展点透镜

范围：L1 核心 7 文件（Objects / Algebra / SignedNet / DerivedMetrics / Deviation / EffectScript / EffectScriptContract）+ ApiMapping（+1）。未读 audit/ 其他文件。

核心问题：新增一个 ResourceId 构造子 / ScopeId 构造子 / Kind 值 / Mode 值时，哪些地方必须同步改？编译器能兜住多少？

---

## 结论先行

**判定：不是 Open/Closed。** 类型层（abstract record + sealed 子类、enum）本身是好的数据建模，但所有下游消费点都是**带 catch-all 臂的封闭 switch**——catch-all 把「忘改」从编译错误降级成静默数据损坏。这正是 Hickey 所说的 bad place：规范散落在多处 switch 的形状里，而语言本可在缺臂时报警（switch expression 对 sealed 层级去掉 `_` 即得穷举检查），代码却主动放弃了这道护栏。

---

## 一、新增 ResourceId 分支（如 Video(string)）必须同步改的位置

| # | 符号 | 文件:行 | 忘改后果 | 严重度 |
|---|------|---------|----------|--------|
| 1 | `ResourceId.Video` 定义 | Objects.cs:15–30（嵌套 sealed record 区） | 编译不过（引用方找不到类型）——唯一有编译器保护的点 | INFO |
| 2 | `ResourceId.Normalize` | Objects.cs:51–66 | 默认 `_ => r` 兜底；若新构造子需要别名归一（如裸名⇒命名空间），不写则两条别名 Claim 不合并、net 漏抵消。无编译提示 | **HIGH** |
| 3 | `EffectScriptContract.ParseResource` | EffectScriptContract.cs:145–161 | 新资源无法经 JSON 契约表达；白名单硬编码在 147–150 的 `TryGetProperty` 五连判里 | MEDIUM |
| 4 | `EffectScriptContract.ParseResourceKey` | EffectScriptContract.cs:172–192 | budget 键前缀解析不到 ⇒ FormatException（fail-fast，尚可） | LOW |
| 5 | `EffectScriptContract.SerializeResource` | EffectScriptContract.cs:211–220 | **静默损坏**：catch-all `_ => {["memory"] = 0}` 把任何未知构造子序列化成 Memory(0)，round-trip 后数据被改写，无任何报错 | **CRITICAL** |
| 6 | `EffectScriptContract.ResourceKey` | EffectScriptContract.cs:229–237 | 同上：`_ => "memory:0"` 静默吞掉未知构造子的 budget 键 | **CRITICAL** |
| 7 | `GodotApiWhitelist.Build()` + 帮助器 | ApiMapping.cs:44–58（帮助器）、68–174（表） | 数据表，加行即可——这部分是真正 open 的（additive data > code） | INFO |

### 现存 bug（非假设）：今天就已经违反

- SerializeResource/ResourceKey 只覆盖 5/15 个构造子（Gpu/CommandBuffer/Memory/Occupancy/SignalBus）。Tree、Self、Physics、Disk、Signal、AudioMixer、Callback、Network、Input、Custom 一旦进入契约路径，全部被吞成 `memory:0`。与文件头自述「非法形状抛 FormatException（fail-fast，非静默漏报）」（EffectScriptContract.cs:4）直接矛盾。
- SerializeScope（EffectScriptContract.cs:194–200）：`_ => {"type":"global"}` 把 Shell/Loop/Conditional/Async 全部静默降级为 Global——Shell 作用域 Claim 经 round-trip 变成全局作用域，语义被放大而非丢失，比丢弃更危险。
- 而 ParseScope（同文件 88–104）对未知 type 是抛异常的。同一文件里解析端 fail-fast、序列化端静默兜底，失败模式不一致。

## 二、新增 ScopeId 分支

| # | 符号 | 文件:行 | 忘改后果 | 严重度 |
|---|------|---------|----------|--------|
| 1 | `ScopeId.X` 定义 | Objects.cs:105–116 | —（定义即用） | INFO |
| 2 | `IncludedIn` | Objects.cs:118–126 | 泛化实现（Equals 自反 + Global 最大元 + 跨标签 false），新构造子自动获得正确偏序。**这是全库最好的 open/closed 示范** | INFO |
| 3 | `ParseScope` | EffectScriptContract.cs:96–104 | 未知 type 抛 FormatException；但 shell/loop/conditional/async 今天就不在表内 ⇒ 契约无法表达这四个作用域 | MEDIUM |
| 4 | `SerializeScope` | EffectScriptContract.cs:194–200 | 见上：静默降级为 global | **HIGH** |

## 三、新增 Kind 值

| # | 符号 | 文件:行 | 忘改后果 | 严重度 |
|---|------|---------|----------|--------|
| 1 | `Signature.Add` 内 switch | Objects.cs:172–177 | **无 default、无 else**：第 4 个 Kind 的 Claim 被静默丢出三桶，AllClaims/net/Peak 全部看不见它。最危险的静默丢失路径 | **CRITICAL** |
| 2 | `Weight.Of` | Algebra.cs:29 | `a == b ? 1.0 : NaN` 泛化比较，天然 open | INFO |
| 3 | `NetTable.Compute` 桶过滤 | Algebra.cs:60 | `c.Kind != Kind.Occupy` 二元判断，新 kind 自动不进 net——策略隐式但保守可辩护 | LOW |
| 4 | `ParseKind` | EffectScriptContract.cs:132–136 | 未知值抛 FormatException（好） | INFO |
| 5 | `SerializeClaim` kind 输出 | EffectScriptContract.cs:204 | `ToString().ToLowerInvariant()`，自动 open（好） | INFO |
| 6 | `Signature` 三桶字段本身 | Objects.cs:145–149 | 三桶是**字段不是集合**：每加一个 Kind 要改 Signature 类结构 + Union + Equals + GetHashCode 四处 | MEDIUM |

## 四、新增 Mode 值

| # | 符号 | 文件:行 | 忘改后果 | 严重度 |
|---|------|---------|----------|--------|
| 1 | `Compatible.IsCompatible` | Algebra.cs:18–28 | 新 mode 未配对 ⇒ 落入末尾 `return false`（冲突）。保守方向正确，但注释宣称「16 对全函数，无未覆盖对」在加 mode 后变成谎言，且无断言强制 | MEDIUM |
| 2 | `NetTable.Compute` 符号选择 | Algebra.cs:63 | `c.Mode == Mode.Release ? Negate : ToSigned` —— 新「负向」mode 默认按正号计，net 守恒误判 | MEDIUM |
| 3 | `Peak.Compute` 排除项 | Algebra.cs:96 | `c.Mode == Mode.Release continue` —— 新 mode 默认计入峰值，偏保守 | LOW |
| 4 | gate(3) 冲突三元组 | EffectScript.cs:237–245 | `(int)Mode.Create \|\| Move \|\| Release` 与 detail 字符串把 CONFLICT 集**第二遍硬编码**（第一遍在 Algebra.cs:9 注释+IsCompatible）。两处漂移风险；且此处用 `(int)` 强转做字典键（EffectScript.cs:170），enum 加值后旧键序无影响但三元组列表不会自动更新 | **HIGH** |
| 5 | `ParseMode` / mode 序列化 | EffectScriptContract.cs:138–143 / 206 | 解析端 fail-fast；序列化端 ToString 自动 open | INFO |

## 五、白名单

| # | 符号 | 文件:行 | 评估 |
|---|-----|---------|------|
| 1 | `GodotApiWhitelist.Build()` | ApiMapping.cs:61–175 | 纯数据表 + `M/Rd/Wr/Oc` 帮助器，additive-only，真 open。`Canonical()`（ApiMapping.cs:64–65）单一真源，Analyzer/Generator 不再各自内联——这是本轮最佳实践之一 |
| 2 | `ReleaseClass.Names` | ApiMapping.cs:186–189 | 不可变数据集，additive-only，open |

## 六、耦合成本核算

**新增 1 个 ResourceId 分支的最小改动面：**

- 必改：3 文件 ≥6 个编辑点（Objects.cs 定义[+Normalize]、EffectScriptContract.cs ×4 函数、ApiMapping.cs 表）
- 应改：测试（round-trip、net 合并）
- 无需改：Runtime（Fiber/LoadValidation/PluginRuntime）、Analyzer 全部走泛化的 `ResourceId.Normalize`——这一层隔离做得对
- **但编译器只能抓到第 1 点**；漏掉 #5/#6 不报错、不抛异常，只产生错误的审计结论（memory:0 冒充真实资源）。漏改的成本被 catch-all 臂推迟到最远的下游。

**结论：牵 3 文件 6 点，其中 2 点是 CRITICAL 级静默损坏，0 点有编译保护。**

## 七、最小修法（Hickey 式：让规范回到一处，让编译器当哨兵）

1. 删掉 SerializeResource / ResourceKey / SerializeScope 的 `_` catch-all 臂（EffectScriptContract.cs:211–220, 229–237, 194–200），换成显式枚举全部构造子或直接 `throw new NotSupportedException(...)`。switch expression 对 sealed 层级去 `_` 后，C# 编译器会对非穷举发 CS8509 警告——免费的全局扩展点哨兵。
2. `Signature.Add`（Objects.cs:172–177）补 default 抛异常，或改 switch expression 去 `_`。
3. CONFLICT 集单一真源：EffectScript.cs:237–245 改调 `Compatible.IsCompatible(mode, mode)`（同 mode 对角线恰为 CONFLICT 集），删除第二份硬编码。
4. ParseResource/ParseResourceKey 与 SerializeResource/ResourceKey 是同一映射的正反两份手写编码——可由一张 (前缀, 构造/解构) 表派生两端，消一半同步点。

## 残余风险

- Analyzer/Generator/Runtime 未在本轮精读范围内（仅 grep 确认其消费方式为泛化 Normalize）；若其中存在按构造子类型的 switch，会追加耦合点。
- SerializeScope 的 global 降级是否被现有 round-trip 测试覆盖未知（未读 tests/）。
