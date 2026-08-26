# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 3/10 轮 —— Rich Hickey 视角：Decomplect（解纠缠）

> 独立上下文审计：未读取 `audit/` 下任何历史报告。所有裁决仅基于本仓源码与文档本身。
> 判据：simple ≠ easy；complexity = 纠缠 + 条件分支；API 面积是最昂贵的承诺；好的设计让用户不读实现也能预测行为。

---

## 核实矩阵（可独立核实的历史/文档结论 → 仍在 / 已修 / 部分修 / 误报）

| # | 来源主张 | 裁决 | 行号证据 |
| --- | --- | --- | --- |
| V1 | README「已知语义锐边」：`loop:"⊤"` 居民层被静默豁免守恒检查，`lifetime:[1,⊤]`（ω 有限）仍入 net 并在闭包点报警——同一「常驻」两种相反行为 | **仍在**（文档诚实标注为设计锁死，但行为分裂是真实的 complect：「常驻」这一个概念被拆成两种互斥编码）| EffectScript.cs:158（gate1 仅有限 ω 入 net）、EffectScript.cs:283（闭包跳过 ω=⊤）、EffectScript.cs:123-125（开放尾段采 maxFinite+1，闭包点对未闭合 create 报 Leak） |
| V2 | README：`Claim.Size` 省略 ≠ 未知，`?? [1,1]` 精确 1 | **仍在**（属实且为有意设计；但一个 `Interval?` 参数承载三种语义——省略/显式 [0,0]/⊤——调用者无法只改其中一个维度，见 F8） | Objects.cs:133（`Size = Size ?? Interval.Default`）；Objects.cs:120（`Interval? Size` 五元组） |
| V3 | README：未命中 §7 白名单的 API **静默无保护、无警告** | **仍在**（Analyzer 对 null 命中直接 continue，无诊断、无提示；fail-open 面比 README 写的更大：见 F1 的同名误伤反向面） | EffectAlgebraAnalyzer.cs:290-299（FindWhitelistEntry 返回 null 即放弃）、EffectAlgebraAnalyzer.cs:153-156 与 203-205（两处 `if (m is null) … continue`） |
| V4 | docs/spatial-plugin-shell-design.md v7 §7.1：release-class 白名单「复用 ApiMapping release-class 集，避免重复定义」 | **部分修**：清单确实单点（ApiMapping.cs:188-190），但**归一化规范没有复用**——白名单键用 `Canonical`（去 `.`/`_` + 小写，ApiMapping.cs:57-59），而 `ReleaseClass.IsRelease` 只做小写（ApiMapping.cs:192）。两个消费者、两套归一化，正是「复用」声称所要防止的漂移，已在标签层面复现（F4） | ApiMapping.cs:57-59 vs ApiMapping.cs:188-192 |
| V5 | docs v7 §10/reviewer 接线主张：OnSuspending / ExitDrain 已打通壳层 | **已修**（接线存在），但接线方式本身是新的层次穿透：内核 PluginRuntime 反向依赖具体壳类 `GodotShell` 而非自家 `IHost` 端口（F6） | PluginRuntime.cs:201-204 |
| V6 | EFFECT_SCRIPT.md §2.2：`Audit(Budget)` 是「验证」，Budget 是「工程策略不是数学性质，不写进代数内核」 | **部分修**：预算确实没进代数核，却被塞进了剧本记录本体（`EffectScript.Budget` 构造字段）+ 又保留 `Audit(cap)` 自由参数——同一决策两个入口，可互相矛盾且无人校验（F9） | EffectScript.cs:52-63（构造存 Budget）、EffectScript.cs:105（`Audit(Budget cap)` 直接用参数）、EffectScriptContract.cs:246-248（`Audit()` 用自带 Budget） |

---

## 新发现

### F1 · HIGH — L2/L3 以「方法名」为唯一绑定键：对使用者代码形状的隐式假设双向埋雷

**位置**：
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs:290-299（`FindWhitelistEntry`：先匹配全名 canonical，再退化为**去接收者的裸方法名**匹配）
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs:107（`var canon = GodotApiWhitelist.Canonical(memberName)`——memberName 是**被标注方法自身的名字**，不是它体内调用的任何 API）
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs:135（生成代码按该 canon 匹配白名单）

**判词**：把「这个方法做了什么」纠缠进「这个方法叫什么名字」——名字是环境里最容易被别人占用的资源，你却拿它当类型用。

**推演（对抗性验证 ①）**：使用者想只改一个关注点——「给我的数据加载助手 `Load(string path)` 加保护/或让它别被误保护」：
- 改名？`myRepo.Load(path)` 在任何方法体内出现一次，Analyzer 经裸名退化匹配（Analyzer.cs:297 `c == method`）即命中白名单 `Load`（Rd(Disk)+Oc(Mem,Create)），acquire 无配对 release ⇒ EAA0901 报错。用户没碰任何 Godot 资源，却被迫要么改名、要么全文件铺 `[EffectOverride("…")]`。
- 不改名想加白名单条目？得改 ApiMapping.Build()（ApiMapping.cs:73 起）并重建——README 自己承认这是唯一通道。
- L2 生成器更空转：给名为 `SpawnEnemy` 的方法标 `[EffectOverride]`，canon="spawnenemy" 永不命中白名单 ⇒ 生成的 `ComputeSpawnEnemy` 恒等于 `baseSig ∪ ∅`。「每方法效应签名」实际从未读过方法体——生成器与分析器共享同一个错误假设（名字↔效应），却各自实现一份匹配逻辑。

**最小修复**：绑定键从方法名改为**语法事实**：Analyzer 已有 InvocationExpressionSyntax，应按「体内实际调用的成员符号（经 semantic model 解析接收者类型）」聚合 Claims；生成器要么删除（其产物当前信息量为零），要么消费 Analyzer 的调用站点数据。

### F2 · HIGH — 同一依赖关系存两份真源：DependencyGraph 边集 vs Fiber.Dependents 手工回填

**位置**：
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:70（AddDependency 回填 `provider.Dependents.Add(dependent.Id)`）
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:95（LoadAll 自动派生软边时**再写一遍**回填，注释自认「与 AddDependency 一致」）
- src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs:14-16（权威边集 `_hard/_soft`）
- src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs:40-43（CascadeProcessModeDisabled 读的是 `fiber.Dependents`，不是图）

**判词**：图是关系的一次表达，`Dependents` 是它的手抄副本——而抄写义务散落在每个添加边的调用点上，漏抄一处就静默分叉。

**推演（对抗性验证 ②）**：使用者想只改一个关注点——「Suspending 时的暂停策略」（比如改为逐级确认、或只暂停直接依赖者）：
- 他必须同时动四处：① PluginRuntime.AddDependency 的回填行（:70）② LoadAll 软边派生的回填行（:95）③ GodotShell.CascadeProcessModeDisabled 的遍历（GodotShell.cs:43）④ 若经公共 API `_graph.AddSoftEdge` 直加边（DependencyGraph.cs:41 公开可调），回填根本不会发生 ⇒ 壳级联对该边**静默失明**。
- 更糟：CascadeProcessModeDisabled 只遍历**一层** Dependents（GodotShell.cs:43 无递归），而 BeginTeardown 的级联是递归的（PluginRuntime.cs:117-127）——同一个「级联」概念在两条路径上有不同的深度语义。A→B→C 时 A teardown，C 被 BeginTeardown 递归入队，但其 ProcessMode 不会被禁用：暂停与拆除两个关注点已经悄悄解绑，只是没人声明。

**最小修复**：删除 `Fiber.Dependents` 可变回填，让它是 `graph.DependentsOf(id)` 的只读投影；壳级联改为遍历图的传递闭包（或在图中提供 `TransitiveDependents`），使「暂停范围」与「拆除范围」由同一份边集推导。

### F3 · HIGH — PluginRuntime 是六合一调度器上帝类：机制、策略、监控、诊断、生命周期开关、壳接线同体

**位置**：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:7-243
- 装载守卫（:77-86 Register）+ 级联 teardown（:110-128 BeginTeardown）+ 批排空调度（:131-162 DrainTeardownBatch）+ 关闭路径（:172-191 SynchronousExitDrain）+ 看门狗（:214-233 TickWatchdog）+ 泄漏监控（:36-49 AccumulateNet/CheckPermanentFiberLeak/ResetNetAccum）+ 诊断存储（:25-33 CrashReports/SoftCycles）+ 壳接线（:201-208 AttachShell）
- :21 `public bool IsShuttingDown { get; set; }`——生命周期策略被降格为任何人可写的公共布尔。

**判词**：一个类同时知道什么时候拆、怎么拆、拆坏了记在哪、谁在泄漏、以及 Godot 该怎么暂停——这不是运行时，这是把整个壳层塞进了一个 `sealed class`。

**代价**：宿主想替换任一策略（如换掉看门狗时钟、把 CrashReports 换成事件流）都必须 fork 整个类；`IsShuttingDown` 公共可写意味着任何代码都能在帧中途翻转关闭态，Register 的守卫（:79）随之失效。注释自称「纯逻辑层」（:213），但 AttachShell(:201) 已把它焊死在具体壳类型上（见 F6）。

**最小修复**：至少拆出三个协作对象——TeardownScheduler（队列+拓扑）、LeakMonitor（net 快照）、DiagnosticsSink（CrashReports/SoftCycles）；`IsShuttingDown` 收窄为私有 + `BeginShutdown()` 方法。

### F4 · HIGH/MED — 归一化双规范：`Canonical` 单一真源只覆盖了一半，`ReleaseClass.IsRelease` 用另一套

**位置**：
- src/Cosmos.EffectAlgebra/ApiMapping.cs:57-59（Canonical：lower + 去 `.`/`_`，注释自称「单一真源……避免漂移」）
- src/Cosmos.EffectAlgebra/ApiMapping.cs:188-192（`Names = {"queue_free","remove_child",…}`；`IsRelease(api) => Names.Contains(api.ToLowerInvariant())`——只小写，不去 `_`)
- src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs:30-42（ValidateReleaseClass 用 IsRelease 裁决装载期异常）

**判词**：你在白名单键上建立了 Canonical 法律，却在释放类清单上沿用方言——同一个仓库，两套正字法，裁判还拿着其中一套去扔石头。

**推演**：使用者在 `InverseClaim.ReleaseApiTags` 里按 C# 惯例写 `"QueueFree"` 或 `"queueFree"` ⇒ ToLowerInvariant 得 `"queuefree"` ≠ `"queue_free"` ⇒ IsRelease=false ⇒ LoadValidation.ValidateReleaseClass 抛 LoadValidationException（LoadValidation.cs:38-40），装载被拒。用户唯一的自救是偷看 ReleaseClass.Names 的 snake_case 内部拼写——违反「程序员知道得越少越好」。这正是 V4 中「避免重复定义」承诺要防的事故，只是漂移发生在**归一化函数**而非清单本身。

**最小修复**：`IsRelease` 改走 `GodotApiWhitelist.Canonical` 后比对（Names 同样 canonical 化），一行改动消灭整套方言。

### F5 · MED — 类型契约承诺的逃逸通道，静态工具静默不认：AttributeTargets 与分析器消费面不同步

**位置**：
- src/Cosmos.EffectAlgebra/EffectAttributes.cs:17（`[EffectOverride]` 合法目标：Method | Property | Field | Class）
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs:109（只注册 `SyntaxKind.MethodDeclaration`）、:137-147（只在方法自身 AttributeLists 里找豁免）
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs:30,75-90（同样只挑 MethodDeclarationSyntax）

**判词**：L1 类型说「你可以标在 Class 上」，L3 分析器说「我只看得见 Method」——契约的两半各说各话，用户照类型系统办事得到的是沉默。

**后果**：`[EffectOverride("证据")]` 标在类上以豁免整类的方法级 A3/A4 提示（属性用法明示的目标场景），分析器不识别 ⇒ EAA0303/EAA0304 照报；反之亦无任何诊断告诉用户「此处的标注不被消费」。非法状态（无效标注）不但可表示，还编译通过、零警告。Property/Field 目标同理全空转。

**最小修复**：要么把 AttributeUsage 收窄到 Method（诚实的 API 面积——删掉永不生效的承诺），要么 Analyzer 向外层 TypeDeclaration 冒泡查找豁免并同步 Generator。

### F6 · MED — IHost 端口词汇表被宿主机制污染，AttachShell 又把内核焊在具体壳类上

**位置**：
- src/Cosmos.EffectAlgebra.Runtime/IHost.cs:11（`void SetProcessMode(FiberId id, bool disabled)`——「ProcessMode」「disabled 方向」都是 Godot 机制词汇，进了内核端口定义）
- src/Cosmos.EffectAlgebra.Runtime/IHost.cs:14（`EnqueueExitDrain(Action)` 把「退出树时排空」这一 _ExitTree 生命周期假设固化进接口）
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:201-204（`AttachShell(GodotShell shell)` 参数是具体类而非 IHost；方向还是内核伸手拉壳）

**判词**：抽象的本意是让内核说自己的语言（suspend/resume fiber、flush pending work），你却让宿主的方言成了端口的官方用语，然后又绕过端口直接握手具体类。

**代价**：换一个非 Godot 宿主（编辑器内预览、专用服务器、单元测试之外的任何东西）必须假装自己有 ProcessMode 和 ExitTree——接口强迫所有实现者携带 Godot 概念残留。`bool disabled` 还把 enable/disable 两方向捆进一个参数，调用方永远要传 `true` 来表达「禁用」（GodotShell.cs:42-43 两处硬编码 `true`）——从未有人用过 false，这个维度本不该存在。

**最小修复**：IHost 改为内核语言：`Suspend(FiberId)/Resume(FiberId)`、`OnShutdown(Action)`；AttachShell 改收 IHost 或移出内核程序集。

### F7 · MED — EffectEvent 双 scope 真源：JSON 强制每 claim 写 scope，引擎却用 event scope 整体改写

**位置**：
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs:132（ParseClaim `Require(c,"scope")`——每条 claim 必填 scope，缺失即 FormatException）
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs:44-50（Combination.Loop 把每条 claim 的 Scope 无条件改写为 loopScope）
- src/Cosmos.EffectAlgebra/EffectScript.cs:177（gate(3) 分组键用 `e.Scope`，注释自认「测试 builder 恒 c.Scope==e.Scope，无回归」）

**判词**:合同逼用户填一个字段，引擎转头就用另一个字段覆盖它——强制必填的死数据是 API 面积里最贵的一种谎言。

**推演**：AI 按 EFFECT_SCRIPT.md §4 示例产出 JSON，在 claim 里认真写了与 event 不同的 scope（比如嵌套图层归属）⇒ Parse 通过、At/Audit 全程无视之，冲突归因和 Compatible 分组一律落到 e.Scope。用户想只修「claim 级作用域」这个关注点时发现根本没有这个维度可改——要么接受静默改写，要么去读 At/gate(3) 实现才能预测行为。

**最小修复**：二选一：JSON 契约删掉 claim.scope（由 event.scope 统一注入，构造期完成，消除第二真源）；或让 gate(3) 尊重 claim scope 并在两者不一致时报 Violation。

### F8 · MED — ω 缩放律双实现 + net 贡献算式在 Audit 内部自我复制

**位置**：
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs:57-60（私有 `Scale(Interval, NatStar)`：ω=⊤ ⇒ [lo,⊤]）
- src/Cosmos.EffectAlgebra/EffectScript.cs:327-331（私有 `ScaleSize(Interval, NatStar)`——逐字符等价的第二份）
- src/Cosmos.EffectAlgebra/EffectScript.cs:163-166 与 :289-292（同一段 `SignedInterval(Negate…)/ToZ…` 贡献算式在扫换线 enter 与闭包两处粘贴）

**判词**：一条代数定律写在两个文件的 private 方法里，靠注释互相指认——定律不会因为复制而变成两条，但维护者会。

**代价**：未来调整 ω 缩放语义（EFFECT_SCRIPT.md §1.1 OPEN-N2 明说 ω 的解释曾被重定义过一次！）时，`At()` 与 `Audit()` 会静默分叉：At 说峰值 X，Audit 说峰值 Y，ReferenceAudit 等价性测试是唯一防线，而它锁死的恰是当前这份复制。net 贡献算式同理：改符号约定漏一处 ⇒ NegativeDip/Leak 误报。

**最小修复**：ScaleSize 并入 `Combination.Scale` 并公开；net 贡献提取为 `SignedInterval Contribute(Claim, Interval scaled)` 单一函数，enter 与闭包共用。

### F9 · LOW/MED — Budget 双入口：剧本自带预算与 Audit(cap) 参数可互相矛盾

**位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:52-63（构造固定 Budget）、:105（`Audit(Budget cap)` 只用参数，完全忽略 this.Budget）、EffectScriptContract.cs:246-248（无参重载才用自带值）

**判词**：同一个决策给了两个门，走进哪扇门结果不同，而门上没有任何告示。

**后果**：`script.Audit(otherCap)` 静默丢弃构造时声明的预算；调用者无法从签名预测「审计用的是哪个 budget」——必须读实现。参数 complect 的标准形态：预算所有权与审计动作被捆在一个可选参数上。

**最小修复**：删掉 `Audit(Budget)` 公共重载（或改名 `AuditWithOverride` 并显式要求非默认），保留无参 `Audit()` 单一口径。

### F10 · LOW — Violation.Kind 是字符串判别域

**位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:373-377（`public string Kind`，注释列举 Leak|NegativeDip|PeakExceeded|CompatibleConflict 四个魔法串；产生点散布在 :170/:186/:206/:315）

**判词**：四个值的封闭集合用 string 承载——把穷举交给拼写检查，把重构安全交给人眼。消费方（AI 回修循环）只能字符串比对，多打一个字母即静默失配。枚举零成本，此处无借口。

### F11 · LOW — Fiber.EffectiveSignature 在数据记录 getter 中内嵌净额折算策略

**位置**：src/Cosmos.EffectAlgebra/Runtime/Fiber.cs:48-70（getter 内判断「Effect 是否已含 create(Provides)」并折入逆 release claims——这是 §5 闸门的业务策略，藏在看似数据的属性后面）

**判词**：属性的名字承诺数据，函数体执行政策。每次访问重算一遍策略，读者以为在取值，实际在做审计口径推导。至少应为具名方法 `ComputeEffectiveSignature()` 或独立的 closure 计算器，让「昂贵且有立场」显形。

---

## TOP-3（本轮最重要的三个发现）

1. **F1 — 名字即契约的 L2/L3 绑定**：整个静态工具链建立在「方法名 ≈ Godot API」这一隐式形状假设上。正向：同名用户方法被强加 Claim（EAA0901 误伤任意 `Load`/`Connect`）；负向：改名/包装即静默失去保护；侧向：生成器的「每方法签名」因从不读方法体而是恒空壳。这是「换个写法就静默失效」的最大实例，且修复路径（semantic-model 符号绑定）在 Analyzer 中已具备全部原料。
2. **F2 — 依赖关系的双真源与两层深度的级联分裂**：`Fiber.Dependents` 是 DependencyGraph 的手工抄本，回填义务散布两处、公共 AddSoftEdge 可绕过；壳级联遍历一层、teardown 级联递归——「谁会被暂停」与「谁会被拆除」已经是两个答案，只是尚无人踩中。双真源必然漂移，问题只是时间。
3. **F4 — Canonical 单一真源的半途而废**：为了消灭漂移建了 Canonical，却在同一文件里给 ReleaseClass 留了方言版归一化，装载闸门（LoadValidation）据此拒绝合法标签。讽刺性极强：防漂移机制自身成为漂移点，且故障模式是运行时装载失败而非编译警告。

---

## Usability 裁决

**框架的核心数学（L1 代数）是 simple 的**：值类型、判别联合、三桶量纲隔离、全函数 Compatible——这些是不纠缠的好设计，值得肯定。**但它被三层「easy」的胶水平包住**：名字匹配的静态工具链（F1/F5）、手抄副本式的运行时状态管理（F2/F3）、以及半途而废的统一化努力（F4/F7/F8）。共同症状是：用户无法在不阅读实现的前提下预测行为——哪个 budget 生效要看重载（F9），哪个 scope 生效要看 gate(3)，哪个标签合法要看 Names 的内部拼写。判定：**L1 可用，工具链与 Runtime 层当前不适合对外承诺 API 面积**；按 TOP-3 顺序修复后可重新评估。

---

## 证据：本轮读取过的文件清单

- README.md
- EFFECT_SCRIPT.md
- docs/spatial-plugin-shell-design.md
- src/Cosmos.EffectAlgebra/ApiMapping.cs
- src/Cosmos.EffectAlgebra/EffectAttributes.cs
- src/Cosmos.EffectAlgebra/EffectScript.cs
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs
- src/Cosmos.EffectAlgebra/Algebra.cs
- src/Cosmos.EffectAlgebra/Objects.cs
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs
- src/Cosmos.EffectAlgebra.Runtime/IHost.cs
- src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs
- src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs
- src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs
- src/Cosmos.EffectAlgebra.Runtime/InverseReplay.cs
- src/Cosmos.EffectAlgebra.Runtime/ProviderCrashCascade.cs
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs
- src/Cosmos.EffectAlgebra/*.csproj、src/Cosmos.EffectAlgebra.Runtime/*.csproj
