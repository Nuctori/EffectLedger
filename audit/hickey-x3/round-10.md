# Cosmos.EffectAlgebra · Final Verdict — 正确性与易用性总裁决（Rich Hickey 视角 · 第 10/10 轮收官）

> 审计方式：独立上下文，未读取任何历史审计报告。全部结论基于本轮实读源码 + `dotnet build/test` 基线 + 6 个仓库外探针实测（探针工程位于 `D:/probe-hickey-x3/`，零仓库改动）。

## 0. 基线验证（对抗性复跑）

| 论断 | 本轮实测 | 结果 |
| --- | --- | --- |
| `dotnet build` 0 error / 0 warning（DELIVERABLE.md:5） | `已成功生成。0 个警告 0 个错误` | ✅ 属实 |
| 测试全绿 | 306 + 91 + 69 = **466 passed / 0 failed**（README.md:64 写的是 453 = 297+69+87） | ✅ 全绿，但 README 数字已漂移（LOW） |
| 「AddChild + QueueFree 配对 ⇒ 不报」（samples/AnalyzerConsumer/Game.cs:14 注释、README.md:30-38 示例） | 探针 H：同方法内 `AddChild(new object()); QueueFree();` → **EAA0303 照报**；EAA0901 不报但 EAA0303 报 | ❌ **部分证伪**（见 F1） |
| 「有意偏离标 `[EffectOverride]` 即可」（README.md:38、EAA0901 消息自述 EffectAlgebraAnalyzer.cs:49） | 探针 A2 + EndToEndTests.cs:180 自证：带合法 reason 的 override 后 EAA0901 **仍然报** | ❌ **证伪——文档教的路走不通**（见 F2） |
| 「未知 API 静默无保护」（README.md:47 自认） | 设计如此，属诚实标注 | ✅ 属实（fail-open 是刻意选择，但代价计入评分） |

## 核实矩阵（历史公开论断逐条裁决）

| # | 论断来源 | 裁决 | 行号证据 |
| --- | --- | --- | --- |
| 1 | L1 构造期不变量（lo≤hi、LoopCount≥1、溢出⇒⊤） | **仍在，且属实** | Numeric.cs:83-91、DerivedMetrics.cs:33-34、HickeyX2FixTests.cs:14-32 |
| 2 | ZStar 溢出守卫 R4-F1（禁止强转回卷假阴性） | **仍在（已修后保持）** | SignedNet.cs:44-58、HickeyX2FixTests.cs:16-32 |
| 3 | Join 实现 merge_I（R4-F2） | **仍在** | Objects.cs:203-220、HickeyX2FixTests.cs:36-52 |
| 4 | Audit(default(Budget)) 不 NRE（R10-F1） | **仍在** | EffectScript.cs:96-97、HickeyX2FixTests.cs:76-86 |
| 5 | JSON 拒绝 [⊤,⊤] 寿命防假绿（R10-F2） | **部分修——只在 Parse 层堵住，类型层仍放行** | EffectScriptContract.cs:73-77 已拒；Numeric.cs:78-91 构造子仍允许 [⊤,⊤]；探针 F 实测直接 C# 构造的 [⊤,⊤] 寿命剧本 `Audit Passed=True`（create-only 泄漏全绿） |
| 6 | 「控制流近似可能漏报跨方法配对」（Analyzer 类注释） | **仍在，且比声明更糟：不只是漏报，还会误报** | EffectAlgebraAnalyzer.cs:293-297,315-321；探针 G/A2 |
| 7 | 「白名单外 API 静默无保护」 | **仍在** | README.md:47、ApiMapping.cs:99-196 |

## 新发现

### F1 · HIGH — README 的「5 分钟上手」示例在 README 自己的门禁配置下编译失败
- **位置**：README.md:20-22（EAA0303=error 配置）× README.md:30-38（SpawnEnemy 示例）× ApiMapping.cs:100-101,131-133（AddChild 含 Write+Occupy 双 Claim）
- **判词**：入门文档第一步教你把警报调成 error，第二步给你的示例代码就是第一个触发者——这不是上手指南，是陷阱说明书。
- **机制**：AddChild 与 QueueFree 各自带 Write+Occupy 两桶 Claim，跨调用 kind 多样性 ⇒ EAA0303 KIND_MIX 必发。探针 H 实测：`方法 'Spawn' ... EAA0303`。
- **最小修复**：A3 对「同一调用站点集合整体构成白名单已知 acquire/release 对」的情形豁免，或把 EAA0303 默认降为 info 并从 README error 清单移除。

### F2 · HIGH — [EffectOverride] 这条被文档与诊断消息双双承诺的逃逸通道，实际是死路
- **位置**：README.md:38、EffectAlgebraAnalyzer.cs:49（消息文本承诺 "(2) 若有意偏离守恒，标 [EffectOverride]"）vs EffectAlgebraAnalyzer.cs:136（`AnalyzeMissingRelease(...); // EAA0901：永不豁免`）、tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs:180-184（测试自证带 override 仍报）
- **判词**：诊断消息亲手给用户指了一条被自己的实现焊死的路——程序员照做、CI 依旧红、最后只能学会无脑喷 attribute 或关掉整个分析器。
- **后果**：按 README 配置 severity=error 后，跨方法配对的正确代码（Godot 中 `_Ready` 里 Connect、`_ExitTree` 里 Disconnect 是教科书模式，探针 A2 实测必红）**没有任何合法通过路径**。
- **最小修复**：让合法 reason 的 [EffectOverride] 豁免 EAA0901（与消息/README 承诺一致），或改写消息与文档承认它只豁免 A3/A4。

### F3 · HIGH — 分析器按裸方法名匹配白名单：用户自己的 `Load("slot1")` 读字典也会被判泄漏
- **位置**：EffectAlgebraAnalyzer.cs:293-297（`c == full || c == method`）、315-321（MethodName 剥掉接收者）；白名单撞名高危名：ApiMapping.cs:107（Load）、123（Connect）
- **判词**：不看接收者类型、只看方法拼写就定罪——这是把「同名」当成「同义」，C# 世界里 Load 和 Connect 是最烂的指纹。
- **探针 G 实测**：纯内存字典类 `SaveSystem.Boot()` 调用自有 `Load("slot1")` → `EAA0901: 方法 'Boot'`。severity=error 下任何含这些常见方法名的工程直接编译失败。
- **最小修复**：method-name-only 匹配要求接收者类型可判定为 Godot 类型（或至少排除接收者为非 Godot 命名空间符号的情况），否则退回仅 full-name 匹配。

### F4 · MED — Signature 是集合不是多重集：两份并发占用静默坍缩成一份
- **位置**：Objects.cs:148-150,175-177（ImmutableHashSet.Add 按 Normalize 键去重）
- **判词**：值语义没错，但把「数量」这个游戏数值框架最核心的维度在 Union 里悄悄丢了——预算表从此说谎而不留痕迹。
- **探针 B 实测**：`Signature.Of(c, c)`（两个相同 create size=[1,1]）→ OccupyClaims.Count=1，Peak=1，net=[1,1]。用户预期 2。正确路径只有绕道 `Combination.Loop(ω=2)`，而没有任何诊断提示你走错了。
- **最小修复**：`Signature.Of` 检测归一化后重复 Claim 且 size 未按 ω 缩放时抛异常或发诊断；文档在 Claim 注释首行写明「并发副本必须用 LoopCount 表达」。

### F5 · MED — AI JSON 契约只能表达 15 种资源里的 5 种：契约承诺与表达能力脱节
- **位置**：EffectScriptContract.cs:155-159（resource 仅 gpu/commandBuffer/memory/occupancy/signalBus）vs Objects.cs:22-37（ResourceId 共 15 个构造子）；EFFECT_SCRIPT.md §4 承诺「AI 只产出剧本数据」
- **判词**：给 AI 一张只能画五种颜色的调色板，然后宣称「AI 可以描述整个屏幕」——场景树资源（Tree）恰恰是 Godot 泄漏的主角，却被契约挡在门外。
- **探针 D 实测**：`{"tree":"res://enemy.tscn"}` → FormatException「resource 须含 gpu/commandBuffer/memory/occupancy/signalBus 之一」。
- **最小修复**：ParseResource 补 tree/self/disk/callback 等构造子，或在 EFFECT_SCRIPT.md §4 显式声明契约覆盖面并说明其余资源的替代建模。

### F6 · LOW — 编程构造的 Shell/Loop/Conditional/Async scope 剧本无法 round-trip
- **位置**：EffectScriptContract.cs:200-208（SerializeScope 只认 Scene/Method/Type/Global，其余抛 FormatException）
- **判词**：类型系统收下的作用域，序列化器不认——同一个库的两个门彼此不认识对方的客人。
- **探针 E 实测**：Shell-scope 事件 ToJson 抛「不可序列化的 scope: Shell { }」。
- **最小修复**：补 SerializeScope 分支（shell/loop/conditional/async），与 ParseScope 对称。

### F7 · LOW — PluginRuntime.IsShuttingDown 是公共可写布尔
- **位置**：PluginRuntime.cs:21（`public bool IsShuttingDown { get; set; }`，注释自承「测试可置位」）
- **判词**：为了测试方便把状态机的总闸做成公共旋钮——非法状态从此人人可表示。
- **最小修复**：改为 internal setter 或提供 `ITestAccessor`。

### F8 · LOW — RecomputeTopology() 非关路径是无声 no-op
- **位置**：PluginRuntime.cs:160-167
- **判词**：一个名字承诺重算拓扑、身体却什么都不做的公共方法，是最典型的 easy——调用顺手，心智账户里记下了一笔从未发生的保障。
- **最小修复**：非实现期抛 NotImplementedException 或改名为 `RecomputeTopologyPlaceholder` 并在 XML doc 顶层警告。

## 维度评分（simple ≠ easy 标尺）

| 维度 | 分 | 判词 |
| --- | --- | --- |
| 正确性保障（错误输入能否表达） | 7 | L1 构造期不变量扎实（lo≤hi/ω≥1/溢出⇒⊤），但 [⊤,⊤] 幽灵事件与 default struct 仍是可表达的非法态 |
| 可推理（不看实现预测行为） | 5 | 集合语义吞并数量（F4）、裸方法名定罪（F3）、override 承诺不兑现（F2）——三处都必须读实现才不会踩 |
| 上手成本 | 3 | README 第一小时即撞上自家 error 门禁（F1/F3）：第一次正确使用到不了手 |
| 组合性 | 7 | Union/Join/Combination.Loop 律性质有性质测试背书，Parallel 冲突 fail-fast 是亮点；扣分在多重集语义缺位 |
| 扩展性 | 4 | 白名单硬编码 38 条、自定义 API 静默裸奔（README.md:47 自认）；JSON 契约覆盖 5/15 资源（F5） |
| AI 友好度 | 6 | fail-fast 解析 + Violation 反例（时刻/资源/当前 vs 上限）设计对 AI 回修极友好；但表达力缺口（F5/F6）让 AI 大量合法意图无法落笔 |

## TOP-3 最危险的问题（真实项目事故概率排序）

1. **F2+F1+F3 合流：L3 门禁在真实工程里不可用**。触发条件：按 README 把 EAA* 设为 error。后果：(a) 教科书式 Connect/Disconnect 跨方法配对直接 CI 红；(b) 用户自有 Load/Connect 方法误判；(c) 文档承诺的 [EffectOverride] 逃逸无效 → 团队要么关掉分析器（门禁失效），要么满屏无意义 attribute（信号噪声比归零）。修复：F2 二选一对齐承诺 + F3 接收者类型判定 + F1 豁免白名单内部配对。
2. **F4 集合语义静默低估占用**。触发条件：用户用两个相同 create Claim 表达双份并发占用（最自然的写法）。后果：峰值与净占用量减半，预算审计系统性漏报——数值框架最不能犯的就是数值错。修复：Of 重复键检测 + 文档强制 LoopCount 表达并发。
3. **F5+F[⊤,⊤] 残洞：AI 数据通道表达力不足且仍有假绿缝**。触发条件：AI 按真实游戏需求产出含 Tree/Disk 资源的剧本 → Parse 必炸；绕过 JSON 直接 C# 构造 [⊤,⊤] 寿命的 create-only 事件 → Audit 全绿（探针 F）。后果：AI 闭环在最常见的场景树资源上断裂，或以假绿收场。修复：契约扩容或显式限界 + Interval 构造层禁 [⊤,⊤] 进入 EffectEvent。

## TOP-3 值得保留的优点

1. **L1 值类型纪律**：readonly record struct + 构造期抛错的非法态拒绝 + 溢出一律保守 ⊤（Numeric.cs、SignedNet.cs），配合 1000 组随机代数律测试（PropertyTests.cs:20-160）——这是「非法状态不可表示」的正确做法，数学内核值得原样保留。
2. **诚实的残差标注文化**：Analyzer 类注释逐条自陈近似边界与漏报方向（EffectAlgebraAnalyzer.cs:24-40）、README「已知语义锐边」主动亮出家丑（README.md:49-53）、Runtime 每个非常规决策都带 reviewer 编号溯源——注释与行为基本对表（F2 是唯一发现的承诺违约处）。
3. **可证伪的测试结构**：sweep-line 与暴力参考实现的 Violation 集合相等断言、HickeyX2FixTests 每条对应一个实证过的错误行为、466 测试 <2s 全绿——回归防线是真的，不是装饰。

## P0 清单（落档 (b) 的前置条件）

1. **P0-1**：对齐 [EffectOverride] 与 EAA0901 的豁免契约——要么实现豁免，要么改消息+README（F2）。
2. **P0-2**：分析器裸方法名匹配加接收者类型判定，消除用户代码撞名误报（F3）。
3. **P0-3**：EAA0303 对白名单已知 acquire/release 配对豁免；README 门禁配置与示例自洽（F1）。
4. **P0-4**：Signature.Of 归一化后重复 Claim 显式报错或告警，并发语义强制走 LoopCount（F4）。
5. **P0-5**：Interval 层禁止 [⊤,⊤] 进入 EffectEvent（不只 JSON 层）（核实矩阵 #5）。

## 最终裁决

**档位：(b) 修完 P0 后可用。**

理由：L1 代数内核（Numeric/Objects/Algebra/SignedNet）是真正的 simple——职责单一、值语义、律性质有测试背书，今天就可以给团队用于纯数据层的预算推演。但围绕它的三条「easy 通道」尚未兑现承诺：分析器门禁会惩罚正确的代码并把逃逸通道焊死（P0-1~3），集合语义让最自然的并发写法静默算错账（P0-4），AI 数据契约在最常见的资源类型上断裂（F5）。这些不是数学问题，是承诺与实现的失约——失约可以修，修完之前，「用户和 AI 可以简单、正确地使用」这句话只能说一半：AI 写不出能解析的完整剧本，用户过不了自己文档设的门禁。

## 证据：本轮读取过的文件

- README.md、EFFECT_SCRIPT.md、DELIVERABLE.md、PDR_Effect_Cost_Algebra_v3_FINAL.md（§1-§3 头部）
- src/Cosmos.EffectAlgebra/: Numeric.cs、Objects.cs、Algebra.cs、SignedNet.cs、DerivedMetrics.cs、Deviation.cs、ApiMapping.cs、EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs
- src/Cosmos.EffectAlgebra.Runtime/: PluginRuntime.cs、Fiber.cs、IHost.cs、DependencyGraph.cs、InverseReplay.cs、LoadValidation.cs、NetBenefitClosure.cs、GodotShell.cs、ProviderCrashCascade.cs
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs、src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs
- samples/GodotIntegration/SampleGame.cs、samples/AnalyzerConsumer/Game.cs
- tests/: EndToEndTests.cs、PropertyTests.cs、HickeyX2FixTests.cs
- 仓库外探针：D:/probe-hickey-x3/probe-l1（B/C/D/E/F 六项）、D:/probe-hickey-x3/probe-analyzer（A2/G/H 三项）
