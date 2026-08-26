# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 7 轮 — Rich Hickey 视角：Extensibility without Complecting（不纠缠的扩展性）

> 审计对象：Cosmos.EffectAlgebra（L1 代数 + Runtime 壳层）。本轮独立上下文，未读取 audit/ 下任何历史审计报告；
> 「历史结论」取自仓库内设计文档 `docs/spatial-plugin-shell-design.md` §9 自载的审计总表与 README 声明，逐条对代码裁决。
> 含仓库外探针工程实测（%TEMP%/hickey-probe，net10.0，引用 L1+Runtime 工程源码，零仓库文件改动）。

---

## 核实矩阵（历史结论 → 本轮裁决）

| # | 历史结论（出处） | 裁决 | 行号证据 |
| --- | --- | --- | --- |
| V1 | R5-6 装载期拒绝「逆释放他 provider 资源」（设计 §9 R5 表） | **已修** | `LoadValidation.ValidateDoubleRelease` 存在且接线：LoadValidation.cs:44-66；`PluginRuntime.LoadAll` 调用：PluginRuntime.cs:88 |
| V2 | R4-4 notify 幂等守卫防振荡 | **已修** | Fiber.cs:92-94 `NotifyProviderTeardown` 仅 `Active→Suspending`，其它态 no-op |
| V3 | #1/R4-9 看门狗帧计数强制 teardown + 入队回放 | **已修** | PluginRuntime.cs:216-238 `TickWatchdog`：守卫 Active/Suspending、入队 `ReplayAndDead`、级联 NotifyDependents；测试 PluginRuntimeTests.cs `TickWatchdog_EnqueuesReplayTask_ReclaimsResource` |
| V4 | C7-1 删除第二 JSON 解析器 `EffectScriptIo.cs` | **已修** | `src/Cosmos.EffectAlgebra/` 目录无 EffectScriptIo.cs；契约单口径在 EffectScriptContract.cs（ParseResource 仅 5 形状，:186-198） |
| V5 | §7.1 release-class 白名单「复用 ApiMapping release-class 集」落位（R6-1） | **已修，但见新发现 N2**——复用是复用了，可它是一套**封闭集** | 数据源：ApiMapping.cs:188-190（7 个名字硬编码）；消费点：LoadValidation.cs:50-66 |
| V6 | R5-4 壳级 Defer() 包装替代 call_deferred | **已修** | GodotShell.cs:26-42（含句柄失效 fail-closed 丢弃） |
| V7 | R4-1/R5-2/R5-3 关闭路径同步排空 + IsShuttingDown 守卫 | **已修** | PluginRuntime.cs:169-200 `SynchronousExitDrain`；关路径 Register 拒绝：PluginRuntime.cs:55 |
| V8 | #2/#4/R4-5 硬环拒载 / 软环降级 warning 可观测 | **已修** | 装载期硬环抛：PluginRuntime.cs:85-87；软环记录 `SoftCycles`：PluginRuntime.cs:107 |
| V9 | #9 动态重组合 MVP=静态组合，`RecomputeTopology()` 为「接缝」 | **部分修（纸面接缝）** | PluginRuntime.cs:163-167——非关路径分支注释自认「no-op 占位」；探针 [E] 实测静默返回。公开方法承诺了行为却无行为，见 N4 |
| V10 | README：「自定义资源操作需扩展白名单并重建」「未命中白名单的 API 静默无保护、无警告」 | **仍在（自认的架构事实）** | README.md §5 分钟上手注意段；分析器侧证据 EffectAlgebraAnalyzer.cs:287「null 表示不在白名单，本近似不处理」。框架把这当锐边声明了，但没给任何出路——见 N1 |

误报核查：本轮未发现历史表中存在「声称已修实未修」的条目；V9 是唯一「接缝名存实亡」项（设计文档 §8 已诚实列为 deferred gap，但**公共 API 面已承诺**，二者矛盾归入 N4）。

---

## 扩展任务推演（对抗性实测：三个真实扩展任务）

探针工程 `%TEMP%/hickey-probe` 引用 `Cosmos.EffectAlgebra.csproj` + `Cosmos.EffectAlgebra.Runtime.csproj`，不改仓库任何源文件。

### 任务 1：新增一种效果类型（新 Mode / 新引擎 API 映射）
必须触碰（全部为框架源码，fork + rebuild）：
1. `Objects.cs:113` — `enum Mode { Use, Create, Release, Move, Unknown }` 封闭枚举；
2. `Algebra.cs:18-37` — `Compatible.IsCompatible` 16 对**全函数**手写封闭目录，新 Mode 必须改核心代数语义表；
3. `EffectScriptContract.cs:168-173`（ParseMode switch）、`:223`（SerializeClaim 小写化）— 契约层第二处封闭映射；
4. `ApiMapping.cs:64-185` — 私有 `Build()` 追加条目。
**接触面 = ≥4 个框架源文件 + 全量重建**。第三方程序集内无法注册：探针 [B] 实测 `GodotApiWhitelist.All` setter 不存在、38 条固定。更糟的是不注册也无警告——分析器对未命中 API 静默放行（EffectAlgebraAnalyzer.cs:152-156, :287），你的新 API 得到的是**假绿**而不是错误。

### 任务 2：新增一个宿主（IHost 实现）
外部 1 个文件实现 `IHost` 四方法（Defer/SetProcessMode/EnqueueExitDrain/IsInstanceValid，IHost.cs:7-17），配 `GodotShell(host)` + `PluginRuntime.AttachShell` 即跑通全生命周期。
探针 [C] 实测：自定义 MyHost 下 Register→LoadAll→BeginTeardown→DrainTeardownBatch 全链路成功，逆回放执行、Fiber 达 Dead，零 Godot 依赖。FakeHost（GodotShell.cs:63-77）亦证此路可行。
**接触面 = 1 个外部文件。这是全仓唯一真实、干净、不纠缠的开放扩展点。**

### 任务 3：新增一种资源类型
- **正道**（框架认可）：fork `Objects.cs:21-38` 加嵌套 sealed record + `ApiMapping.cs:41-52` 构造帮助器 + `EffectScriptContract.cs` **四处** switch（ParseResource :186-198 / ParseResourceKey :203-211 / SerializeResource :220-228 / ResourceKey :231-240）→ **≥3 个框架源文件**。
- **歧道**（意外发现）：`ResourceId` 基类是 `abstract record` **而非 sealed**（Objects.cs:21）——探针 [A1] 实测外部子类 `sealed record MyResource(string Name) : ResourceId` 能编译、能走通 `NetTable.IsConserved=True`；但同一子类型送进 `EffectScriptContract.ToJson` 在资源/序列化层抛 FormatException（EffectScriptContract.cs:208,227），且没有任何白名单能给它挂 Claim。**半开的判别联合是最坏形态：一半层承认你，一半层拒绝你，且没有文档告诉你边界在哪。**
- 另注：`ResourceId.Custom(Name)`（Objects.cs:36）自称「§3.1.2 自定义资源」，全仓 grep 零引用——纸面逃生门，钉死的门画在墙上。

### 任务汇总

| 任务 | 接触面（文件数） | 是否需 fork 框架 | 卡点 |
| --- | --- | --- | --- |
| 新效果类型/API | ≥4 框架源文件 | 是 | 白名单静态只读 + 分析器静默放行（假绿） |
| 新宿主 | 1 外部文件 | 否 | 无 —— 真实可用 ✔ |
| 新资源类型 | 正道 ≥3；歧道 0 但半开残废 | 是（或接受半开） | 契约层四处 switch + Custom 死构造子误导 |

---

## 新发现

### N1 · HIGH — API 白名单是编译期封闭目录，扩展 = 给框架做手术
- **位置**：src/Cosmos.EffectAlgebra/ApiMapping.cs:62（`public static ImmutableArray<ApiMapping> All { get; } = Build()`）、:64（私有 Build）；README.md 注意段自认。
- **证据**：探针 [B] `All` setter 不存在、count=38 固定；Analyzer/Generator 双双在各自编译产物里遍历这份静态表（EffectAlgebraAnalyzer.cs:294、EffectAlgebraGenerator.cs:128），即使反射篡改运行时实例也影响不了编译期诊断。未命中者静默放行（EffectAlgebraAnalyzer.cs:152-156）。
- **判词**：把「哪些调用值得保护」这一最易变化的知识焊死在框架源码里——每接入一个新引擎 API 都是一次核心移植手术，而手术失败的并发症是静默失去全部保护，病人还以为自己活着。
- **最小修复**：把白名单改为「内置表 + 公开注册 API」（如 `[assembly: EffectApiMapping(...)]` 特性或 MSBuild 附加 JSON 清单）；分析器对**显式注册为零且命中启发式疑似 acquire/release 配对的调用**发 info 级诊断，消灭静默假绿。

### N2 · HIGH — ReleaseClass 封闭集与装载期安全校验耦合：扩展压力直接转化为安全降级
- **位置**：src/Cosmos.EffectAlgebra/ApiMapping.cs:188-190（7 个名字硬编码 `ImmutableHashSet`）；src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs:56-66。
- **证据**：探针 [B] `ReleaseClass.IsRelease("my_custom_free") = False` 且不可注册。第三方插件若用自己的释放 API（如包装器 `MyFree()`）释放他 provider 的资源，`ValidateDoubleRelease` 必然拒载（LoadValidation.cs:60-66 要求标签 ∈ ReleaseClass）——唯一出路是把 `ReleaseApiTags` 置 null（LoadValidation.cs:57 显式 opt-out），即**彻底绕开双重释放防护**。
- **判词**：扩展点和安全闸门共用一把锁，钥匙只有框架作者有——于是每个想长大的游戏都被制度性地推向「拔掉保险丝」。这是 complecting 的教科书案例：两个正交关注点（API 目录 vs 释放语义校验）纠缠在同一封闭常量上。
- **最小修复**：`InverseClaim.ReleaseApiTags` 的校验语义改为「标签非空即可 + 由宿主注册自定义 release-class 集」，或提供 `ReleaseClass.Register(string)`；至少让 opt-out null 路径在 CrashReports 里留一条永久告警痕迹。

### N3 · MAJOR — OnSuspending 单槽可覆写属性 + 多播吞异常：壳级联可被静默拆毁
- **位置**：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:30（`public Action<Fiber>? OnSuspending { get; set; }`）、:209（AttachShell 用赋值接线）、:124 与 ：235（`try { OnSuspending?.Invoke(d); } catch { }`）。
- **证据**：(a) AttachShell 之后用户一句 `rt.OnSuspending = myHook;` 即**静默覆盖**壳的 ProcessMode 级联接线，无异常无日志；(b) 多播委托 invoke 语义：链上第一个订阅者抛异常则**后续订阅者不被调用**，bare catch 吞掉后注释宣称「钩子异常隔离：不阻断级联」（:124）——实际只隔离了运行时状态机，不隔离同链的其他订阅者；若第三方钩子排在 shell.CascadeProcessModeDisabled 之前且抛出，Godot 壳的 Suspending 门控整段丢失，use-after-free 防线静默失效。
- **判词**：一个 settable 属性同时承载「壳的基础设施」和「用户的观察欲」，谁后写听谁的——这不是扩展点，是撞车点。
- **最小修复**：拆成两个口：基础设施走内部字段（AttachShell 直写私有 delegate），用户观察走独立的 `event Action<Fiber> FiberSuspending`；invoke 处逐订阅者 try/catch（`GetInvocationList()` 循环），兑现注释承诺的真隔离。

### N4 · MAJOR — RecomputeTopology()：公开 API 承诺了不存在的行为
- **位置**：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:163-167。
- **证据**：非关路径分支注释自认「no-op 占位」；探针 [E] 实测调用静默返回，拓扑不变。设计文档 §8 将动态组合列为 deferred，但方法已是 public 面——API 面积是最昂贵的承诺，这个签名一旦发布就是永久的，而它唯一的功能是让调用者**误以为**重算发生过。
- **判词**：占位函数是写给未来自己的便条，不是交给用户的接口——便条应该 private。
- **最小修复**：降级 internal/private 或删除；待真实实现落地再公开。若必须保留接缝形态，返回 bool 表示「是否真的重算」，让 no-op 可被调用方检测。

### N5 · MAJOR — `Graph` 公共属性暴露可变边集后门，绕过 Scope 前置校验
- **位置**：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:15（`public DependencyGraph Graph => _graph;`）、:113-118（AddDependency 的同 Scope 校验，R7-M2）；DependencyGraph.cs:27/:31/:37（AddHardEdge/AddSoftEdge/Register 全 public）。
- **证据**：R7-M2 特意在 AddDependency 里加了「跨 Scope 边非法状态不可表示」守卫，但任何人持 runtime 引用即可 `runtime.Graph.AddHardEdge(a, b)` 直接向底层图加跨 Scope 边，校验形同虚设；同理 `Graph.Register` 可绕过 PluginRuntime.Register 的重复 Id 抛错（:61-62）造成两处登记不一致。
- **判词**：前门装了防盗门，后墙写着「欢迎翻越」——不变量靠自觉维护的系统，等于没有不变量。
- **最小修复**：DependencyGraph 的变更方法降 internal，仅暴露只读查询（DependentsOf/DetectCycles/TopoSortLeafFirst）；所有变更经 PluginRuntime.AddDependency 单一口径。

### N6 · MED — ResourceId 判别联合「意外开放」+ Custom 死构造子：非法状态可表示且无人认领
- **位置**：src/Cosmos.EffectAlgebra/Objects.cs:21（`public abstract record ResourceId` 非 sealed）、:36（`Custom` 全仓零引用）；EffectScriptContract.cs:208,227（序列化对外部子类型抛 FormatException）。
- **证据**：探针 [A1]/[A2]：外部子类可通过 net 层（IsConserved=True）却被契约层拒绝；`Custom` 无任何 Normalize 分支、白名单挂载点或测试。
- **判词**：要么关门（seal + 明确的正道注册机制），要么开门（Custom 一等公民 + 各层真实消费）——现在这扇门半开着，风从缝里灌进来的都是「在我的机器上能过 net 检查、一到序列化就炸」的 bug 报告。
- **最小修复**：短期 seal ResourceId 并删除或实现 Custom；长期给出资源类型注册机制（N1 同一方案覆盖）。

### N7 · LOW — 扩展 API 零版本化策略
- **位置**：全仓 grep `Obsolete|Deprecated` 为空；`InverseClaim`（Fiber.cs:31-34）/`FiberSpec`（PluginRuntime.cs:246-250）/`CrashReport`（ProviderCrashCascade.cs:9-13）均为位置 record，增参即 source-breaking；csproj Version=1.0.0。
- **判词**：1.0.0 的意思是「从此以后每个参数都是永恒的」——没有 Obsolete 缓冲带，演化策略只剩「改了就崩」一种。
- **最小修复**：为位置 record 补次要构造子重载作为演化缓冲；发布前过一遍公共面标注 `[Obsolete]` 计划。

### N8 · LOW — 同一实体两套登记规则：DependencyGraph.Register 覆盖 vs PluginRuntime.Register 抛错
- **位置**：DependencyGraph.cs:24（`_fibers[f.Id] = f` 静默覆盖）vs PluginRuntime.cs:61-62（重复 Id 抛 InvalidOperationException，R7-L1）。
- **判词**：同一个「Fiber 已存在」的事实，在一层是错误、在另一层是覆盖——程序员必须读两层实现才能预测行为，违反「知道得越少越好」。
- **最小修复**：DependencyGraph.Register 改 `TryAdd` 语义并向上传播失败，单一规则贯穿两层。

---

## TOP-3

1. **N1 — 白名单封闭目录 + 静默假绿**：第三方今天无法在不 fork 框架的情况下保护任何一个自定义 API，且不知道自己没被保护。这是「扩展性」声明的最大空洞。
2. **N2 — ReleaseClass 与安全校验纠缠**：扩展压力被制度性地转化为关闭安全防护（null opt-out），框架在惩罚想长大的用户。
3. **N3 — OnSuspending 单槽 + 吞异常多播**：唯一的集成缝合点本身就是可被静默拆毁的单点故障，且失败模式完全不可观测。

---

## Usability 裁决

宿主轴（IHost）是真实、极简、值语义的扩展点——四个方法、一个探针文件全链路跑通，这是本轮唯一合格的答案。其余三根轴（效果类型、资源类型、API 目录）全部采用**编译期封闭目录**模型，扩展 = fork 框架源码 + 全量重建，且两处（白名单静默放行、ReleaseClass 强制 opt-out）把扩展压力直接转化为安全性降级。框架声称 plugin/shell 架构，但今天的真相是：**壳是真的，插件生态的地基还没打**。开放递归方面整体克制（Fiber 是纯数据非虚方法继承树，值得肯定），唯 OnSuspending 一处的覆写/吞异常语义需要立刻收紧。

---

## 证据清单（本轮实际读取）

- README.md
- docs/spatial-plugin-shell-design.md
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs、IHost.cs、Fiber.cs、DependencyGraph.cs、ProviderCrashCascade.cs、GodotShell.cs、LoadValidation.cs、InverseReplay.cs、NetBenefitClosure.cs
- src/Cosmos.EffectAlgebra/EffectAttributes.cs、Algebra.cs、ApiMapping.cs、Objects.cs、EffectScriptContract.cs
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs（节选）、src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs（关键行）
- tests/Cosmos.EffectAlgebra.Runtime.Tests/PluginRuntimeTests.cs
- 探针实测：%TEMP%/hickey-probe（dotnet run 输出 [A1][A2][B][C][D][E]，未修改仓库任何文件）
