# Cosmos.EffectAlgebra 第 7 轮对抗审计 —— Extensibility without Complecting（Rich Hickey 视角）

> 轮次：第 7/10 轮（独立上下文，未读取 audit/ 下任何历史报告）。
> 审计问题：游戏会长大。plugin/shell 架构是真实可用，还是纸面设计？
> 结论速览：**运行时宿主扩展是真的，数据层效果扩展是假的**。`IHost` 四方法接口经 `FakeHost` 验证可替换；而白名单、release-class 集、资源构造子目录全部编译期封死在框架源码里——第三方今天想加一种效果类型，唯一的路是 fork 本仓。

## 核实矩阵

本轮为独立上下文，不核对历史审计报告，改为逐条核实设计文档 `docs/spatial-plugin-shell-design.md`（v7）自身声称已落地的扩展相关承诺：

| 设计文档声称 | 裁决 | 行号证据 |
| --- | --- | --- |
| §10「动态组合/`RecomputeTopology()` 仅接缝，非关路径 no-op 占位」 | **属实（诚实声明）** | `src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:185-191` —— 方法体仅剩关路径抛异常，注释明言 deferred gap |
| §7 R5-4「壳提供 `Defer()` 包装替代 `call_deferred`」 | **已修** | `src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs:17-40`（含句柄判空 fail-closed） |
| 同条「建议硬 lint 规则禁止插件直接调用 `call_deferred`」 | **部分修** | README.md:23-28 仅列 EAA0901/0303/0304/0801/0802，无 call_deferred 诊断；Analyzer 中亦无对应规则 |
| §3 step2b/R5-6「装载期拒逆释放他 provider 资源」 | **已修** | `src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs:47-71`（ValidateDoubleRelease 交叉判定） |
| §6 R5-7/N2「关闭路径 `IsShuttingDown` 硬拒绝」 | **已修** | `PluginRuntime.cs:56-57, 187-188` |
| §5 #6「永久存活 Fiber 周期快照阈值告警」 | **已修** | `PluginRuntime.cs:49-52`（AccumulateNet/CheckPermanentFiberLeak） |
| §1/IHost「宿主抽象：运行时层与 Godot 的唯一接触面」 | **部分修（见 H2）** | 接口本身真实（`src/Cosmos.EffectAlgebra.Runtime/IHost.cs:5-19`），但集成缝 `AttachShell` 吃具体类 `GodotShell`（`PluginRuntime.cs:201-205`） |
| README.md:40「自定义资源操作需扩展白名单并重建」 | **仍在（自认封闭）** | 白名单为静态闭集：`src/Cosmos.EffectAlgebra/ApiMapping.cs:62-64`（`All { get; } = Build()`，Build 为私有静态） |

## 新发现

### HIGH

**H1 · 效果类型/资源扩展 = fork 框架，「插件生态」在数据层不存在**
- 位置：`src/Cosmos.EffectAlgebra/ApiMapping.cs:62-64,145,188-190`；`README.md:40`
- 证据：`GodotApiWhitelist.All` 是 `static ImmutableArray` 由私有 `Build()` 构造（:62-64），无任何注册入口；`ReleaseClass.Names` 是 7 元素硬编码 `ImmutableHashSet`（:188-190）。Analyzer/Generator 在**编译期**直接遍历该静态表（`EffectAlgebraAnalyzer.cs:294`、`EffectAlgebraGenerator.cs:128`），即使提供运行时注册也救不了 L2/L3。README.md:40 自认「自定义资源操作需扩展白名单并重建……未命中白名单的 API **静默无保护、无警告**」。`ResourceId.Custom` 构造子虽存在（`Objects.cs:35`），但全白名单无一条目 emit 它——它是给用户看的橱窗样品，走 Custom 的资源完全绕开守恒保护。
- 判词：把边界做成类型是对的，把类型的目录锁进自家保险柜是错的——开放封闭原则在这里只对仓库主人开放。
- 最小修复：`GodotApiWhitelist.All` 改为「内置表 + 公开 `ImmutableArray<ApiMapping>.Builder` 注册口」，Generator/Analyzer 改为消费合并后的冻结表（MSBuild 传入或附加文件）；至少先让 `ReleaseClass.Names` 可注册。

### MED

**M1 · `OnSuspending` 单槽可变属性：后设者通吃，钩子不可组合**
- 位置：`src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:30`（声明）、`:203`（AttachShell 整槽覆盖）
- 证据：`public Action<Fiber>? OnSuspending { get; set; }`。测试里用户设了自定义钩子（PluginRuntimeTests.cs:364 `rt.OnSuspending = fib => ...`）后再调 `AttachShell` 即被静默顶替；反之 AttachShell 后再设用户钩子则 ProcessMode 级联断线，且无任何告警。单槽 set 语义使「壳钩子 + 用户钩子」互斥。
- 判词：只有一个插座的墙，第二个电器来了就得拔第一个——这不是扩展点，是轮流坐庄。
- 最小修复：改多播（内部 `List<Action<Fiber>>` + `AddSuspendingHook()`），或至少 AttachShell 时检测已有钩子并抛异常。

**M2 · `AddDependency` 不校验「同 Scope」前置条件，非法状态可表示**
- 位置：`src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:66-72`；对照设计 `docs/spatial-plugin-shell-design.md` §3 step1「显式边（hard）：**同 Coeffect.Scope 内**」
- 证据：`AddDependency` 直接转投 `_graph.AddHardEdge/AddSoftEdge`（`DependencyGraph.cs:27-31`），零校验。`LoadValidation.ValidateScaleClosure`（LoadValidation.cs:16-25）只查逆 Scope == 自身 Scope，不查边两端 Scope。宿主接错线即可造出跨 Scope 硬边，装载通过、teardown 语义未定义。
- 判词：文档写「同 Scope 内」三个字，代码一行没拦——前置条件留在纸上就是留给未来的地雷。
- 最小修复：`AddDependency` 开头校验 `dependent.Scope == provider.Scope`，否则抛 `LoadValidationException`。

**M3 · 扩展 API 零版本化：契约演化策略不存在**
- 位置：`src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:238-242`（FiberSpec 位置 record）；`ProviderCrashCascade.cs:7-12`（CrashReport）；全仓 `grep -rn "Version\|Obsolete" src/` 为空
- 证据：所有跨边界公共契约是无版本字段的裸位置 record；无 `[Obsolete]`、无 schema 版本、无序列化兼容说明。给 `FiberSpec` 加第 5 个必填参数 = 全部插件源码级编译破裂；`CrashReport.Exception.Message` 格式甚至已被形状钉测试锁定语义（PluginRuntimeTests.cs:509-513），说明团队知道消息是契约，却没有版本账本。
- 判词：API 面积是最昂贵的承诺，而这张合同没有修订条款——第一次破坏性变更就会教会所有人什么叫「改了就崩」。
- 最小修复：为 `FiberSpec`/`CrashReport` 增加可选尾参（带默认值）演化纪律 + 一页 VERSIONING.md 声明 breaking 窗口。

**M4 · `DependencyGraph.Remove` 是死代码：Dead Fiber 永久滞留，图无界增长**
- 位置：`src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs:35-39`（Remove 定义）；`PluginRuntime.cs:60`（`_fibers[fiber.Id] = fiber` 只增不减）；`PluginRuntime.cs:222-228`（ResetDiagnostics 不清 _fibers/_graph）
- 证据：grep 全仓，`Remove(FiberId)` 除定义外零调用。Fiber 达 Dead 后仍驻留 `PluginRuntime._fibers` 与 `_graph._fibers`；长生命周期宿主反复场景重载 ⇒ 字典与边集线性膨胀，且 `TopoSortLeafFirst` 内 `topo.Contains(f)`（DependencyGraph.cs:79）是线性扫描 ⇒ 每批排序 O(n²) 随死纤维数退化。
- 判词：状态机有五态，唯独没有「遗忘」这一态；不会遗忘的调度器终将死于自己的记忆。
- 最小修复：DrainTeardownBatch/SynchronousExitDrain 排空成功后调用 `_graph.Remove(id)` 并从 `_fibers` 移除 Dead Fiber。

**M5 · 崩溃级联路径绕过 `OnSuspending` 壳钩子：同一状态转移两条通知路径语义分叉**
- 位置：`src/Cosmos.EffectAlgebra.Runtime/ProviderCrashCascade.cs:20-26`；对照正常路径 `PluginRuntime.cs:117-119, 228-230`
- 证据：`Handle` 直接调 `d.NotifyProviderTeardown()` 使 dependent 进 Suspending，但**不触发 `OnSuspending`**——经此路径 Suspending 的 dependent 其 ProcessMode 永不被禁用（除非恰好另有 BeginTeardown 递归路过它）。「dependent 进入 Suspending」这一个事实有两个发射点，一个通知壳、一个不通知：典型 complect——把状态转移和通知职责拆散在两处。
- 判词：同一个门铃装了两根线，一根通门铃一根通隔壁——客人按哪个全凭运气。
- 最小修复：`Handle` 内依赖者置 Suspending 后统一经 runtime 的钩子派发（把 notify+hook 收敛为一个 `SuspendDependent(d)` 私有方法）。

### LOW

**L1 · `Register` 对重复 FiberId 静默覆盖**
- 位置：`PluginRuntime.cs:60`
- 证据：`_fibers[fiber.Id] = fiber` 无冲突检测；外部持有的旧 Fiber 实例成幽灵（State 仍可驱动，图中却是新实例）。
- 判词：两个插件同名，后者悄悄谋杀前者且不留案底。
- 最小修复：重复 Id 抛 `InvalidOperationException` 或返回 false。

**L2 · `IsShuttingDown` 公共可变 setter**
- 位置：`PluginRuntime.cs:21`
- 证据：生命周期核心守卫是 `{ get; set; }`，注释自承「测试可置位」。任意代码可在运行中翻转它，绕过/误触发全部关路径拒绝。
- 判词：为了测试方便把保险丝做成开关——生产环境里每个开关都会被人拨。
- 最小修复：setter 改 internal + `[Friend]` 式测试入口，或提供 `BeginShutdown()` 显式方法。

**L3 · `OnSuspending` 钩子异常静默吞掉，不留任何诊断**
- 位置：`PluginRuntime.cs:118, 229`（两处 `catch { /* … */ }`）
- 证据：框架对 CrashReports/SoftCycles 反复强调「可观测」（:27-34 注释），唯独壳钩子死亡零记录——宿主的 ProcessMode 级联断了都不知道为什么。
- 判词：一边宣称一切失败皆可观测，一边让集成钩子的死悄无声息。
- 最小修复：catch 中追加 `CrashReports.Add(new CrashReport(d.Id, ex, []))` 或专用 HookFailure 表。

## 扩展任务对抗性推演

### 任务 A：第三方添加新资源类型 `SaveSlot` + acquire/release API（SaveToSlot/DeleteSlot）

逐步追踪必须触碰之处：

1. `src/Cosmos.EffectAlgebra/Objects.cs:22-38`：新增 `sealed record SaveSlot(int Id) : ResourceId;`（**改框架源码 ①**）
2. `src/Cosmos.EffectAlgebra/ApiMapping.cs:33-46`：加 helper；`:64-186 Build()` 内加 2 条映射（**改框架源码 ②**）
3. DeleteSlot 若需参与双重释放防护：`ApiMapping.cs:188-190` `ReleaseClass.Names` 是 readonly 静态集，只能再加源码（**改框架源码 ③**）
4. Analyzer + Generator 编译期静态消费 L1 表（`EffectAlgebraAnalyzer.cs:294`、`EffectAlgebraGenerator.cs:128`）⇒ 两个工具程序集必须重建并向所有消费工程再分发
5. Runtime 层无需改动（ResourceId 泛型流动，这点干净）

**客观接触面：≥3 个框架内源文件编辑 + 2 个工具程序集重建再分发。不改框架源的替代路径 = 用 `ResourceId.Custom` 手工拼 Claim ⇒ 完全绕开 L3 分析器（静默无保护，README.md:40 自认）。**
卡点结论：任务 A 今天不可能由第三方完成。运行时层（Fiber/FiberSpec/Coeffect）对任意 ResourceId 是开放的，但保护价值最高的静态层是封闭世界。

### 任务 B：第三方面向无头服务器写一个新宿主

1. 实现 `IHost` 四方法（`IHost.cs:8-17`）：真实可行，`FakeHost`（GodotShell.cs:53-68）即活证
2. 帧驱动 API 公开充足：`TickWatchdog`/`DrainTeardownBatch`/`SynchronousExitDrain` 均 public（PluginRuntime.cs）
3. **卡点**：`AttachShell(GodotShell shell)`（PluginRuntime.cs:201）签名纠缠具体类而非 `IHost`——不想用 GodotShell 的宿主必须手工补两根线：`rt.OnSuspending = …; shell.EnqueueExitDrain(rt.SynchronousExitDrain)`，即复刻本应藏在抽象后的集成逻辑（并踩 M1 的单槽互斥）
4. 另注意 M5：手工接线的宿主在崩溃级联路径同样收不到 Suspending 通知

**客观接触面：1 个新文件 + 2 行手工缝合。结论：真扩展点，缝上有一根毛刺（H2/M1/M5 三处共因）。**

### 附：新增一种效果模式（Mode 维度）的客观计数

`Objects.cs:116` enum + `Algebra.cs:18-33` Compatible 全函数表 + `Algebra.cs:63` net 符号分支 + `Algebra.cs:120` Peak 分支 = L1 内 **4 处条件分支**。复杂度 = 分支数的计费口径下这算明码标价，且 Mode 五值稳定，属可控封闭，不算重罪——真正的问题是框架把**资源**这个高频增长维度也做成了同样的编译期封闭（H1）。

## TOP-3

1. **H1** 数据层扩展即 fork（ApiMapping.cs:62-64/188-190）——「支持 plugin/shell」的承诺在效果类型维度上是纸面的。
2. **H2** `AttachShell(GodotShell)` 在唯一需要抽象的集成缝上纠缠具体类（PluginRuntime.cs:201-205 vs IHost.cs:5-19）——IHost 的抽象红利被最后一步兑现失败抵消大半。
3. **M4** Dead Fiber 永久滞留 + `DependencyGraph.Remove` 死代码（DependencyGraph.cs:35-39）——长生命周期游戏的调度器会死于自己的记忆，这正是「游戏会长大」最直接的代价。

## usability 裁决

宿主抽象（IHost/FakeHost/公开帧驱动 API）是真实、最小、值得表扬的扩展面；代数核心的值语义（record/Immutable*/Normalize 键）让「不看实现也能预测行为」大体成立。但作为「plugin/shell 架构框架」，它在最需要生长的两个维度——新效果类型与第二宿主——分别给出了 fork 与毛刺。扩展性不是功能清单，是「第三方今天能不能不改你的源码」这道是非题；目前答案是：宿主能，效果不能。

## 证据：读取过的文件

- README.md
- docs/spatial-plugin-shell-design.md
- src/Cosmos.EffectAlgebra.Runtime/: PluginRuntime.cs, IHost.cs, Fiber.cs, DependencyGraph.cs, ProviderCrashCascade.cs, GodotShell.cs, LoadValidation.cs, InverseReplay.cs
- src/Cosmos.EffectAlgebra/: EffectAttributes.cs, Algebra.cs, ApiMapping.cs, Objects.cs, Numeric.cs
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs（节选，白名单消费点）
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs（节选，白名单消费点）
- tests/Cosmos.EffectAlgebra.Runtime.Tests/PluginRuntimeTests.cs
- grep 全仓交叉验证：DependencyGraph.Remove 调用点、Version/Obsolete 存在性、ResourceId.Custom 引用
