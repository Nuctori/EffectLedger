# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 2/10 轮 —— Value, Identity, State（Rich Hickey 视角）

> 独立上下文声明：本轮按纪律**未读取** `audit/` 下任何历史审计报告（含 `hickey-x2/`、`hickey-x3/` 及各 rich-hickey-round0N）。故「核实矩阵」改为逐条裁决**源码注释与文档中自我宣称的修复/性质**——这些是项目自己对使用者的承诺，承诺与代码不符处即为本轮靶点。
> 所有对抗推演均以仓库外临时探针工程实测（引用 Release 编译产物，未改仓库任何源文件）。

---

## 核实矩阵（文档/注释自我宣称 vs 代码现实）

| # | 自我宣称（位置） | 裁决 | 行号证据 |
|---|---|---|---|
| V1 | Objects.cs:141-144 注释：「补结构相等使『值』语义与外观一致（R4 P1）」 | **已修** | `Signature.Equals/GetHashCode/operator==` 落地于 Objects.cs:154-190；BucketIsolationTests.cs:130-155 全绿 |
| V2 | EffectScript.cs:59 注释：「构造即固定，使 EffectScript 为不可变值对象（修 auditR5 F1）」 | **部分修** | 属性 `Budget { get; }` 确不可再赋值（EffectScript.cs:61），但 `Caps` 是外部字典的**活引用**（EffectScript.cs:346），构造后仍被外部改写 ⇒ 探针 A 实测审计结果翻转，见新发现 H1 |
| V3 | PluginRuntime.cs:62 注释：「R7-L1：重复 FiberId 抛异常——禁止静默覆盖」 | **已修但有缝** | 抛异常落地（PluginRuntime.cs:63-64）；但 Dead Fiber 永不移除（`DependencyGraph.Remove` 是死代码，DependencyGraph.cs:35-40 全仓无调用者）⇒ 同 id 重载插件永久失败，见 M2 |
| V4 | EffectScriptContract.cs:84-88：「[⊤,⊤] 寿命 fail-fast 拒绝（R10-F2）」 | **已修** | `if (lo.IsTop) throw` 生效 |
| V5 | EffectScript.cs:109-110：「default(Budget).Caps == null ⇒ 归一为无上限，不 NRE（R10-F1）」 | **部分修** | 只堵了 `Audit` 一个入口；`Budget`/`AuditResult` 作为 record struct 的默认值陷阱仍在（null Caps / 未初始化 ImmutableArray），见 L2 |
| V6 | GodotShell.cs:19-23：「此前声称 Suspending 后由 IsInstanceValid 门控却未实装——现真接通（fail-closed）」 | **部分修** | Defer 路径确实接通（GodotShell.cs:30-34）；但**逆回放路径完全无门控**：`inv.Execute()` 裸调（InverseReplay.cs:31），`InverseClaim` 连 handle 字段都没有（Fiber.cs:27-32）⇒ 同一防线只装了一扇门，见 M3 |
| V7 | EFFECT_SCRIPT.md §2.2：`public readonly record struct EffectScript` | **文档漂移（误报级）** | 实现为 `sealed partial class`（EffectScript.cs:55）。类本身不可变，无害，但文档向 AI 使用者描述的类型形状与真实 API 不符 |
| V8 | InverseReplay.cs 头注：「声明维度等价；不声称行为等价」 | **诚实** | 这句免责声明是对的——正因如此 M3 的行为缺口不该被壳层门控的注释掩盖 |

---

## 新发现

### H1 · CRITICAL —— `Budget` 不是值，是披着 record struct 外衣的活视图

- **位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:346（`public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }`）、EffectScript.cs:64-69（构造函数原样存储引用）
- **判词**：你把一个接口类型塞进「不可变值对象」，就等于把别人家的钥匙发给了所有住客——`IReadOnlyDictionary` 只是不给你写的借口，不给别人不写的能力。
- **探针 A 实测**（临时工程引用 Release DLL）：构造守恒剧本 + cap=100 ⇒ `Passed=True`；随后仅对外部 `Dictionary` 写一行 `caps[gpu]=10`；同一 `script` 实例零修改再审计 ⇒ `Passed=False`（PeakExceeded），且 `script.Budget.Caps[gpu]` 读回 10。「同剧本同输入 ⇒ 同审计结果」这一纯函数承诺被一行外部赋值击穿。
- **最小修复**：`Budget` 构造函数将 caps 拷入 `ImmutableDictionary<ResourceId, NatStar>`（或 `FrozenDictionary`），`Caps` 类型改为该不可变类型。

### H2 · CRITICAL —— 状态机转移方法是 public 的：持有引用者一脚绕开调度器，整个运行时永久卡死

- **位置**：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:73-105（`Load`/`Unload`/`NotifyProviderTeardown`/`ForceTeardownOnWatchdog` 全部 public）、Fiber.cs:82-89（`Unload` 只置标志不入队）、PluginRuntime.cs:57-59（级联期 Register 全拒）、PluginRuntime.cs:12/113-118（任务入队只发生在调度器的 `BeginTeardown`/`TickWatchdog`）
- **判词**：状态机的价值在于「只有一扇门能推进它」。你现在给每个状态都留了一扇旋转门，然后惊讶为什么有人从窗户进来。
- **探针 B 实测**：`Register` 返回的 `fiber` 被使用者直接调一次 `fiber.Unload()` ⇒ State=TearingDown、TeardownEnqueued=true，但 `_teardownQueue` 里**没有任何任务**；此后一切 `Register` 永久抛「级联 teardown 进行中禁止新装载」；`DrainTeardownBatch()` 无事可排，fiber 永远到不了 Dead。**一行合法调用 = 运行时整体不可用**。
- **最小修复**：`Load/Unload/NotifyProviderTeardown/ForceTeardownOnWatchdog` 收为 internal（调度器同程序集），对外只暴露只读 `State` 与显式 `BeginTeardown(Fiber)` 入口；若必须保留 public `Unload`，则它必须经调度器入队而非自置标志。

### M1 · MAJOR —— `AddDependency` 既不验注册也不验存活性：幽灵边 + 给死 provider 挂边后依赖者照常派发

- **位置**：src/Cosmos.EffectAlgebra/Runtime/PluginRuntime.cs:69-77（唯一前置校验是 Scope 相等）、DependencyGraph.cs:24-27（`AddHardEdge` 只按 id 加边）
- **判词**：图里的每条边都该指向一个活着的节点。允许边指向图外的幽灵，这张图的拓扑序就是在给鬼排班。
- **探针 C 实测**：
  - C1/C2：从未 `Register` 的 `Fiber` 直接 `AddDependency` 成功；`Graph.DependentsOf(prov)` 计入它，而 `runtime.TryGetFiber(ghost)=False` —— 级联遍历到它时只能静默跳过（PluginRuntime.cs:123 `_fibers.TryGetValue(dep, out var d)` 假即略），依赖关系凭空丢失。
  - C3/C4：provider 已 Dead 并排空后，`Register`+`LoadAll` 新依赖者为 Active，再对死 provider `AddDependency(Hard)` 成功且无告警 ⇒ `ShouldDispatch(dep2)=True`。依赖的资源已随 provider 蒸发，派发门控照常放行——正是本框架自己定义要防的 use-after-free 形态。
- **最小修复**：`AddDependency` 开头校验两个 Fiber 均 ∈ `_fibers`，且 `provider.State == Active`（TearingDown/Dead/Suspending 一律抛）。

### M2 · MAJOR —— Dead Fiber 永久占据注册表：身份泄漏 + 同 id 插件无法重载

- **位置**：DependencyGraph.cs:35-40（`Remove(FiberId)` 存在但全仓**零调用者**，死代码）；PluginRuntime.cs:63-64（重复 id 抛异常）；PluginRuntime.cs:213（`Fibers` 快照含全部历史 Dead fiber）
- **判词**：回收了资源却回收不了名字。每个死掉的 Fiber 都变成墓碑，插在所有后来者的注册路径上。
- **后果推演**：场景重载 → 旧插件 teardown 至 Dead → 以同 `FiberId` 再注册 ⇒ 必抛 R7-L1 异常。宿主唯一的出路是丢弃整个 `PluginRuntime` 实例——那么 `SynchronousExitDrain` 末尾特意复位的 `IsShuttingDown=false`（PluginRuntime.cs:195，「允许实例复用」）所服务的复用场景根本走不通。
- **最小修复**：`DrainTeardownBatch` 中 `diag.AllCompleted && !cyclic` 的 fiber 成功 Dead 后调用 `_fibers.Remove(id)` + `_graph.Remove(id)`；或提供显式 `Unregister(FiberId)`。

### M3 · MAJOR —— 逆回放的 `Action` 无句柄门控：壳层 use-after-free 防线只装了一扇门

- **位置**：src/Cosmos.EffectAlgebra/Runtime/InverseReplay.cs:31（`inv.Execute()` 裸调用）；对照 GodotShell.cs:25-36（Defer 有 handle 判空 + 异常隔离，fail-closed）；InverseClaim 定义 Fiber.cs:27-32（只有裸 `Action Execute`，无 handle 元数据）
- **判词**：你在前门装了三道锁，后门连合页都没安——而逆回放恰恰是在 teardown 之后执行的那条路，是最需要判 `IsInstanceValid` 的时刻。
- **后果推演**：Fiber 的逆闭包捕获某 Godot 节点；节点先被 QueueFree；provider 触发 teardown ⇒ `ReplayAndDead` 依次执行逆 ⇒ 对失效原生句柄调用 ⇒ 未定义行为/崩溃。诊断表里它只会记成 `PartialReleaseDiagnosis` 的 pending 项——框架以为自己在优雅降级，实际是在废墟上继续跳舞。
- **最小修复**：`InverseClaim` 增加 `object? Handle` 字段；`ReplayAndDead` 接受 `Func<object,bool>` 门控（或经 `IHost.IsInstanceValid`），失效则记 pending 并跳过执行。

### M4 · MAJOR ——「是否正在关闭」有两个真相源，且复位语义相反

- **位置**：PluginRuntime.cs:170-195（`SynchronousExitDrain` 结束时 `IsShuttingDown=false`，注释明言「允许实例复用」）；GodotShell.cs:12/24/56（`_exitDraining` 置 true 后**全文件无任何复位路径**，注释称「节点释放不可逆」）
- **判词**:运行时的钟说「可以重新营业了」，壳的钟停在打烊那一刻——此后每一次 `Defer` 都被静默扔进碎纸机，没有日志，没有异常。
- **后果推演**：`AttachShell(runtime, shell)` 后场景重载（runtime 设计支持复用）：新场景一切 `Defer` 进入 GodotShell.cs:24 早退分支被静默丢弃 ⇒ 依赖 deferred 执行的初始化逻辑无声消失。使用者问「现在系统处于什么状态」，两个组件给出相反回答。
- **最小修复**：单一真相源——shell 的 drain latch 由 runtime 复位（如 `FlushExitDrain` 返回后提供 `ResetForReuse()`），或干脆让 `Defer` 在 draining 后抛异常而不是静默丢弃。

### L1 · MINOR —— 文档漂移：EffectScript 声明为 record struct，实现为 class

- **位置**：EFFECT_SCRIPT.md §2.2 vs EffectScript.cs:55
- **判词**：给 AI 读的契约文档里写着一种类型，编译器里躺着另一种。契约的第一义务是与现实一致。
- **最小修复**：改文档（实现不可变性没问题，class 亦可），或在 §2.2 标注「实现为不可变 class」。

### L2 · MINOR —— record struct 默认值绕过构造不变量：`default(Budget)`/`default(AuditResult)` 是非法但可表示的状态

- **位置**：EffectScript.cs:110（null Caps 只在 Audit 入口补）、EffectScript.cs:359-367（`AuditResult.Violations` 为 ImmutableArray，default 后访问抛 InvalidOperationException）
- **判词**：「非法状态不可表示」是你的口号；`default` 是 C# 给每个 struct 发的后门钥匙。补丁堵一个口，后门还有一百个。
- **最小修复**：访问器归一（`Caps ?? Budget.None.Caps`、`Violations.IsDefault ? Empty : Violations`），与 R10-F1 同法推广到所有公共 record struct。

### L3 · MINOR —— `Signature` 是字段非 readonly 的 class：不可变靠纪律不靠类型

- **位置**：Objects.cs:146-150（`private ImmutableHashSet<Claim> _read ...` 三字段均无 readonly）、Objects.cs:159（静态共享 `Empty`）
- **判词**：今天没人改它，明天有人加个缓存字段就破了功。不可变要么写进类型（readonly/struct），要么写进 CI，不能只写在评审员的记忆里。
- **最小修复**：三个字段加 `readonly`（现有构造路径全部兼容，零行为变化）。

### L4 · MINOR —— `IsShuttingDown { get; set; }` 把测试后门焊在生产 API 上

- **位置**：PluginRuntime.cs:20-21（注释自认「测试可置位以模拟关闭路径」）
- **判词**：测试需要的开关应该是测试自己造的，不该是生产引擎上的一个任何人都能扳的拉杆。
- **最小修复**：`internal set`，测试用 `InternalsVisibleTo`；或引入接受注入状态的内部构造器。

---

## TOP-3（本轮最重要）

1. **H1 Budget 活视图**（EffectScript.cs:346）——L1 层苦心经营的「纯值代数、确定性 At(t)、可复现反例」叙事，被一个 `IReadOnlyDictionary` 引用在构造后一击即穿。这是「easy 胜过 simple」的标准案例：接口顺手，值语义破产。
2. **H2 public 状态机旋转门**（Fiber.cs:82-89 + PluginRuntime.cs:57-59）——使用者持有 `Register` 返回的引用即是持有破坏系统的能力；一行 `Unload()` 让整个运行时永久拒绝服务。标识（Fiber 引用）不该自带改变全局状态的权限。
3. **M1 AddDependency 无守卫**（PluginRuntime.cs:69-77）——依赖图是 teardown 正确性的根基，而建边入口不检查两端是否活着。幽灵边 + 死 provider 挂边直接产生「Active 却失去依赖」的不可观测状态。

## 对抗性推演结论（问题 3/4 专答）

「现在效果系统处于什么状态？」目前**无法在任意时刻单点回答**：`_teardownQueue` 私有不可观测（PluginRuntime.cs:12）、Dead fiber 滞留注册表冒充成员（M2）、runtime/shut-down 双钟漂移（M4）、以及探针 B/C 制造出的「TearingDown 但永无任务」「Active 但依赖已蒸发」两种图外状态。InverseReplay 的「声明维度等价、不承认行为等价」是诚实的（V8），但诚实声明不等于可以不给逆 Action 装门（M3）。

## usability 裁决

L1 纯代数层（NatStar/Interval/Claim/NetTable/Peak）接近理想的值语义，结构相等补齐后基本可用；但 **Runtime 层当前不具备安全交付条件**：三条 HIGH 级路径（H1/H2/M1）全部可在正常使用姿势下（拿返回值、调公开方法）触发，无需反射或竞态。裁决：**L1 可用（附 L1-L3 小修）；Runtime 层在 H2/M1/M2 修复前不应接入真实游戏工程。**

---

## 证据：本轮读取过的文件清单

- README.md、EFFECT_SCRIPT.md
- src/Cosmos.EffectAlgebra/Objects.cs、Algebra.cs、Numeric.cs、DerivedMetrics.cs、EffectScript.cs、EffectScriptContract.cs
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs、PluginRuntime.cs、GodotShell.cs、IHost.cs、DependencyGraph.cs、LoadValidation.cs、InverseReplay.cs、ProviderCrashCascade.cs、NetBenefitClosure.cs（grep 级核对）
- tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs、BucketIsolationTests.cs
- 探针（仓库外 `/tmp/value-probe`，引用 Release DLL，输出见报告内 [A]/[B]/[C] 各行）
