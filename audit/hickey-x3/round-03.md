# Cosmos.EffectAlgebra 对抗性审计 · 第 3 轮 · Rich Hickey 视角（Decomplect）

> 纪律声明：本轮独立上下文，未读取 `audit/` 下任何历史审计报告。核实矩阵仅针对代码注释与设计文档中**自述**的历史结论做逐条裁决。
> 视角：complect 是复杂度的唯一根源。simple = 不纠缠；每个公共成员都是永久承诺。
> 本轮含 2 组仓库外探针实测（`P:\Temp\hickey-x3-probe`，net10.0 控制台引用 L1 工程源码构建），关键 finding 均有运行时输出佐证，非纯静态推断。

---

## 核实矩阵

历史结论来源：README.md「已知语义锐边」、EFFECT_SCRIPT.md 审计轨迹、docs/spatial-plugin-shell-design.md 第4–6/C7 轮总表、各源文件内嵌的 reviewer/audit 注释。

| # | 历史结论（出处） | 裁决 | 行号证据 |
| --- | --- | --- | --- |
| V1 | C7-1：删除第二套 JSON 解析器 `EffectScriptIo.cs`，收敛到 `EffectScriptContract` 单一真源（shell-design §C7） | **已修** | `src/Cosmos.EffectAlgebra/` 下无 EffectScriptIo.cs；解析仅剩 EffectScriptContract.cs:20 `Parse` |
| V2 | OPEN-1：`EffectEvent` 自带 `Scope` 消除自由变量 loopScope（EFFECT_SCRIPT.md §10.1 Iter1） | **已修，但引出新 complect（见 N1）** | EffectScript.cs:28 `Scope` 属性；:87 At 用 `e.Scope` |
| V3 | MA-002 / README「已知锐边」：ω=⊤ 居民层静默豁免守恒检测，是设计锁死非 bug | **仍在（确认属实）** | EffectScript.cs:161 `if (enter && !e.Loop.Count.IsTop)` —— ω=⊤ 的 occupy 完全不进 net；豁免与缩放共用同一旋钮（N2 判词见下） |
| V4 | R10-F2 / OPEN-B4：JSON 层拒绝 `[⊤,⊤]` 寿命防假绿（Contract 注释） | **部分修** | EffectScriptContract.cs:80-82 拒绝 Lo=⊤ 仅在 **JSON 路径**；L1 C# 构造路径仍允许 `new Interval(NatStar.Top, NatStar.Top)`，直接 API 使用者依旧能写出永不存活的事件（fail-open 面未收口） |
| V5 | R4-4：provider notify 幂等守卫防振荡（shell-design §R4-4） | **已修** | Fiber.cs:96-99 `NotifyProviderTeardown` 仅 `Active → Suspending` |
| V6 | R7-L1：重复 FiberId 抛异常禁止静默覆盖 | **已修** | PluginRuntime.cs:66-68 `TryAdd` 失败即抛 |
| V7 | R2 #3：「Canonical 归一化单一真源，避免 Analyzer/Generator 各写一份漂移」（ApiMapping.cs:56-59 自述） | **部分修（emit 层复发）** | ApiMapping.cs:60 确为单一真源；但 Generator 生成代码把归一化逻辑**再次内联**进每个生成文件（EffectAlgebraGenerator.cs:131 生成的 `Replace(".","").Replace("_","").ToLowerInvariant()`），改 Canonical 规则时生成代码静默漂移——单一真源承诺只覆盖了编译期，没覆盖 emit 期 |
| V8 | R5-6：装载期双重释放拒绝（逆释放他 provider 资源须标 release-class 标签） | **已修（但判据数据源有漂移，见 N7）** | LoadValidation.cs:47-70 `ValidateDoubleRelease` |
| V9 | shell-design §7.1 自称 release-class 白名单「复用 ApiMapping 同款分类，避免重复定义」 | **误报（自述与事实不符）** | 文档列的是 `free/dispose/queue_free/destroy/Close/Release/Dispose`（docs/spatial-plugin-shell-design.md:124）；代码实际集合是 `queue_free/free/remove_child/disconnect/remove_from_group/cancel_free/free_children_in_group`（ApiMapping.cs:188-191）。两集合交集仅 2 项——文档描述的恰恰是那个不存在的重复定义（详见 N7） |

---

## 新发现

严重级分布：HIGH ×3，MED ×5，LOW ×5。

### N1（HIGH）类型 complect：Claim.Scope 与 EffectEvent.Scope 双重表示，事件 scope 静默改写 claim scope

- **位置**：EffectScript.cs:87（At 用 `e.Scope`）、:180（gate(3) 分组键 `(r, e.Scope, mode)`）、:170/:187/:310（net/peak/leak 归因全取 `e.Scope`）；DerivedMetrics.cs:41-45（`Combination.Loop` 把每条 claim `with { Scope = loopScope }` 整体改写）；EffectScriptContract.cs:135（JSON 里 per-claim `scope` 是**必填字段**，解析、校验、序列化 round-trip 全保留）。
- **实测**（探针 P1）：Event scope = Scene("A")，两条 claim 分别写 Scene("B")/Scene("C")。输出：`At(50) claim scope rewritten to: Scene { Name = A }`，且 gate(3) 按 A 报 create×create 冲突。用户写的 B/C 被解析→验证→丢弃，全程零警告。
- **判词**：同一个决策存在两个存储位，一个静默赢——这不是边界，是陷阱。非法状态（claim.Scope ≠ event.Scope）完全可表示且被 round-trip 忠实保存，唯独语义上作废。EffectScript.cs:179 注释自己承认曾因此产生「两视角冲突归因错位」的 bug——补丁修的是症状，双源还在。
- **最小修复**：从 JSON 契约删除 claim 级 `scope` 字段（或校验其必须等于 event.Scope，不等即 FormatException）；`Combination.Loop` 的改写行为改为断言而非覆盖。

### N2（HIGH）参数 complect：LoopCount 一个旋钮焊死两个正交关注点 + 一条豁免策略

- **位置**：DerivedMetrics.cs:12-27（LoopCount 定义）；EFFECT_SCRIPT.md §1.1 OPEN-N2 自述「重新解释为并发副本数……后续维护者须避免混淆」；EffectScript.cs:161（ω=⊤ ⇒ 整个事件退出守恒检查）与 :184（ω=⊤ ⇒ size 上界拉 ⊤ 进峰值）。
- **推演（实际代价）**：使用者想表达「粒子系统循环发射 1000 波、每波同屏最多 1 个」——时间重复维度在剧本层根本不存在，只能塞进 Lifetime；想表达「单波 1000 并发」用的却是同一个字段。更糟的是 ω 从有限值改到 ⊤ 不是量变：它一键切换三条正交策略——size 缩放变开放上界、退出 net 守恒检查、进入峰值 ⊤ 兜底。「我只想要个未知并发数」的使用者被迫连带接受「免泄漏审计」。三个后果共享一个枚举值，调用者无法只改其中一个维度。
- **判词**：把「我不知道有多少个」和「别审计我了」拧在一个 ⊤ 里，是把策略走私进机制的经典手法。
- **最小修复**：拆成两个独立声明——`Concurrency`（数值/未知，只影响缩放）+ `Resident`（布尔，显式 opt-out 守恒检查并要求人工标注）。未知 ≠ 豁免。

### N3（HIGH）层次 complect：`PluginRuntime.AttachShell(GodotShell)` 具体类穿透 IHost 抽象

- **位置**：PluginRuntime.cs:207-210 `public void AttachShell(GodotShell shell)` 直接绑定具体类并把 `shell.CascadeProcessModeDisabled`、`SynchronousExitDrain` 双向接线；对照 IHost.cs:5「宿主抽象：运行时层与 Godot 的唯一接触面」与 GodotShell.cs:8「IHost 可被测试替身替换」的自述。
- **推演**：使用者想换掉壳实现（比如非 Godot 宿主、或对壳做装饰/代理收集指标）——IHost 明明就是为此设计的缝——却发现调度器内核硬引用 `GodotShell` 具体类：要么继承 sealed class，要么改内核。抽象接口成了摆设，真正的接缝被 AttachShell 的签名锁死在具体类型上。测试侧同理：想隔离调度器必须连壳一起搭。
- **判词**：造了 IHost 这扇门，然后把墙砌在门框旁边。
- **最小修复**：`AttachShell(IHost host)` 或拆出 `IRuntimeShell` 窄接口（仅 CascadeProcessModeDisabled + EnqueueExitDrain 两个成员）；GodotShell 只是众多实现之一。

### N4（MED）名字即身份：L2/L3 以方法名字符串匹配白名单，换个写法就静默失效/误伤

- **位置**：EffectAlgebraAnalyzer.cs:290-301 `FindWhitelistEntry` 先按全名匹配、失败后**退回裸方法名**匹配（:293-299），接收者类型完全不参与；EffectAlgebraGenerator.cs:107+131 生成的 Signature 由标注方法的**名字**查白名单得出。README.md:36 自认「未命中白名单的 API 静默无保护、无警告」。
- **推演**：(a) 使用者自己写了 `void Connect(string id)` 做连接池管理——裸名 fallback 把它映射到 Godot `Connect` 的 Claim（Oc(Callback) acquire），方法体内没有 Disconnect ⇒ EAA0901 在 error 门禁下**编译失败**，被迫改名或加 [EffectOverride] 走人工审批——自己的 API 面被 Godot 词表劫持。(b) 反向：`void Spawn() { AddChild(...); }` 包装一层 ⇒ Spawn 不在白名单 ⇒ 该方法的资源效应**静默消失**于 L2 生成的 Signature。同名不同义与异名同义双双失守，因为身份锚定在了最不可靠的东西——拼写的规范形。
- **判词**：用字符串拼写当类型系统用，等于把正确性抵押给了命名习惯。
- **最小修复**：Analyzer 用 SemanticModel 解析 `IMethodSymbol` 的 containing type 判定是否真为 Godot Node/Object 派生（符号级判定，不是语法级拼写）；白名单条目挂接限定类型名。

### N5（MED）参数 complect：Audit 的预算双源——字段预算与参数预算可对同一实例给出相反结论

- **位置**：EffectScript.cs:64（构造时 Budget 固化为字段，注释自称「使 Audit 结果不依赖可变状态」）、:105 `Audit(Budget cap)` 仍收外部 cap；EffectScriptContract.cs:264 `Audit() => Audit(Budget)`。
- **实测**（探针 P2）：同一 script 实例（自带 cap gpu=1，事件占用配对完整）：`Audit()` → Passed=True；`Audit(cap gpu=0)` → Passed=False [PeakExceeded]。构造函数注释宣称的「确定性」只在固定入参下成立——预算既是数据的属性又是操作的参数，两个真源并存。
- **判词**：修 F1 时把门装进了墙里，却忘了拆掉原来那扇窗。
- **最小修复**：删掉 `Audit(Budget)` 公共重载（或改为 internal 供等价性测试），预算只随剧本走。

### N6（MED）层次边界类型侵蚀：AccumulateNet 把按 ResourceId 维度的类型化 net 压扁成 long 标量

- **位置**：PluginRuntime.cs:33-50 `_netAccum: ImmutableDictionary<FiberId, long>`、`AccumulateNet(IReadOnlyDictionary<FiberId, long>)`、`CheckPermanentFiberLeak(long threshold)`；对照 L1 的 NetTable/SignedInterval 按 ResourceId 有符号区间（NetBenefitClosure.cs:24-31 正确用法）。
- **推演**：宿主喂给周期快照监控的数据必须先把多资源 net 折叠成一个数——同一 Fiber 内 GPU 泄漏 +1000 与内存节省 -1000 相加得 0，`CheckPermanentFiberLeak` 永远沉默。L1 辛苦建立的量纲隔离（DO-7 三桶、ResourceId 判别联合）在运行时监控这条边界上退化成了 `long`。设计文档 §5 R4-7 特意强调「逐 Fiber、不跨 Fiber 抵消」，却没人拦住 Fiber **内部**跨资源抵消。
- **判词**：在离泄漏告警最近的那一厘米处丢掉了量纲，前面九十九米的代数白建了。
- **最小修复**：`_netAccum` 改为 `ImmutableDictionary<(FiberId, ResourceId), long>`（或直接复用 SignedInterval），阈值检查逐资源进行。

### N7（MED）文档与代码两套 release-class 定义漂移，「复用同一集」的承诺反向成立

- **位置**：ApiMapping.cs:186-199（代码权威集：queue_free/free/remove_child/disconnect/remove_from_group/cancel_free/free_children_in_group）；docs/spatial-plugin-shell-design.md:124（文档声称的白名单："free/dispose/queue_free/destroy/Close/Release/Dispose"）。
- **代价推演**：插件作者照文档给 InverseClaim.ReleaseApiTags 写 `"dispose"` ⇒ LoadValidation.cs:57-60 校验 `ReleaseClass.IsRelease("dispose")==false` ⇒ **装载期拒绝**，错误信息还让他「须为 queue_free/free/... 之一」——他照着文档写、被代码打脸，再照着异常信息改。文档、代码、错误消息三方各自为政。
- **判词**：注释说「避免重复定义」，于是重复定义搬进了文档——更危险的那种，因为编译器管不了 markdown。
- **最小修复**：文档 §7.1 改为引用 `ReleaseClass.Names` 的真实内容并加一行「以此为准」；或加一个 doc-test 断言文档表格与集合一致。

### N8（MED）类型 complect：PluginRuntime 是五合一上帝类，且暴露 public 可变关路径标志

- **位置**：PluginRuntime.cs:13-16（依赖图+Fiber 表+teardown 队列）、:21（`IsShuttingDown { get; set; }` public 可写，注释直言「测试可置位」）、:26-31（崩溃报告+软环诊断存储）、:33-50（net 记账）、:207-210（壳接线）、:195（排空完毕自动复位标志以支持实例复用）。
- **推演**：使用者想在生产环境复现实测中看到的关闭路径竞态——发现唯一的入口是把生产类型的公共布尔属性置 true：任何业务代码都能随手翻转全局关路径状态绕过 Register/RecomputeTopology 守卫（:55、:165 的两道检查全部失效）。机制（排空流程）与策略（何时算关闭、能否复用实例）拧在同一块可变状态上，复位时机（:195）又把「场景重载复用」这一产品决策焊死在排空函数里。
- **判词**：测试后门开在大门上，守卫越严，后门越诱人。
- **最小修复**：IsShuttingDown 改 private，经 `BeginShutdown()/internal` 构造注入或专用测试工厂提供；诊断存储（CrashReports/SoftCycles/netAccum）抽成独立 DiagnosticsSink。

### N9（LOW）Compatible 策略三处消费、两种失败模式

- **位置**：DerivedMetrics.cs:53-64（Parallel 内联抛 PARA_CONFLICT 异常，Sequence 却静默 Union）；EffectScript.cs:176-196（Audit 自建 grp 字典分组，冲突记为 Violation）；Algebra.cs Compatible.IsCompatible（纯函数本体）。
- **判词**：同一个兼容性判定，一条路抛异常、一条路记账、一条路什么都不查（Sequence）——使用者每次组合都要先问「这次冲突会以什么姿势炸出来」。
- **最小修复**：Parallel 去掉 throw，返回带冲突信息的 Result 或交由统一审计通道；Sequence/Parallel 对 Compatible 采取一致口径。

### N10（LOW）哨兵占位符：Mem() = Memory(0) 白名单数据里埋了二段填充契约

- **位置**：ApiMapping.cs:39（注释「真实 UID 由映射层运行时填入」）——但该文件就是映射层，没有任何填充逻辑；Load/Preload/Instantiate/Rpc 的内存 acquire/release 全部聚合到 Memory(0) 同一资源键（:80、:109、:113 等）。
- **判词**：强类型白名单里藏了一个「以后再说」的幻数，所有加载类调用的内存量纲被压成一个桶。
- **最小修复**：Memory 哨兵改为显式命名的 `Memory.UnknownUid` 构造子并参与 Normalize 归并规则，让「未知 uid」成为可表示的一等状态而非魔法数 0。

### N11（LOW）Fiber.EffectiveSignature 在 getter 里做所有权策略推导

- **位置**：Fiber.cs:48-66：属性 getter 内实时判断「Effect 是否已含 create(Provides)」「哪些逆释放自身 Provides」，折算规则承载 R4-7 所有权契约，全部写在注释里。
- **判词**：数据（Effect/Inverses）与验证策略（如何折算有效签名）complect 在同一类型；NetBenefitClosure.CheckAll 的结论实际取决于这段隐藏推导，读者不看 Fiber 内部无法预测闸门行为。
- **最小修复**：折算提为 `EffectiveSignature.Of(Fiber)` 静态纯函数（或构造时一次性计算存字段），Fiber 回归纯数据。

### N12（LOW）default(Budget).Caps == null：结构体默认值绕过构造不变式，靠消费点自觉补救

- **位置**：EffectScript.cs:113-114（Audit 内 ad-hoc 归一，注释 R10-F1 自认此坑）；探针 P3 实测 `default(Budget).Caps is null == true`。
- **判词**：「缺省=无上限」这个合法状态用 null 表示而不是空表，于是每个新消费点都要记得再补一次 if——今天补了 Audit，明天的新入口呢？
- **最小修复**：Caps 类型改为非空 + `Budget.None` 单例约定（`IReadOnlyDictionary` 永不为 null，default 场景由 `[] = Empty` 的静态只读兜底），或改 class 杜绝 default 绕过。

### N13（LOW）Generator emit 层内联归一化逻辑，V7 的单一真源在生成物里二次漂移

- **位置**：EffectAlgebraGenerator.cs:131：生成的匹配代码硬编码 `m.GodotApi.Replace(".","").Replace("_","").ToLowerInvariant()` 而非调用 `GodotApiWhitelist.Canonical(m.GodotApi)`（生成程序集本就引用 L1，:128 已在用 `GodotApiWhitelist.All`）。
- **判词**：修漂移的补丁自己带着漂移出生。
- **最小修复**：生成代码改为 `global::Cosmos.EffectAlgebra.GodotApiWhitelist.Canonical(m.GodotApi)`。

---

## 对抗性推演：两个最严重 complect 点的实际代价

**推演一（N1 双 scope）**：AI 依 EFFECT_SCRIPT.md §4 契约产出剧本，其中同景深图层分属 Scene("HUD")/Scene("World") 两个作用域，claim 级 scope 写得完全正确。使用者只想调整 HUD 图层的作用域归属（只改一个关注点：scope），于是修改 JSON 中 claim 的 scope 字段——重投审计，结果**一个字节都没变**：gate(3) 按 e.Scope 分组、Peak/Leak 归因取 e.Scope，claim scope 是契约里唯一「必填、校验、序列化、然后丢弃」的字段。被迫连带处理：要真正改变作用域归属必须去改 event 级 scope，而那会同时改变该 event 全部 footprint 的冲突分组与归因——想动一根线，得理解整张网。这不是假设：EffectScript.cs:179 的注释记录了历史上真实发生过的「两视角归因错位」事故。

**推演二（N4 名字即身份）**：使用者在消费工程里定义 `void Load(string path)` 封装自定义缓存加载（不含任何 Godot 调用）。只关注点：给这个方法加缓存语义。连带发生：L3 裸名 fallback 命中白名单 "load" ⇒ 方法获得 Rd(Disk)+Oc(Mem,Create,Global) 两个 acquire 且无 release ⇒ EAA0901（根 .editorconfig 设为 error）阻断 CI；使用者被迫二选一：改名（污染自己的 API 设计）或加 `[EffectOverride("证据")]`（进入人工审批流）。与此同时，隔壁同事把 `AddChild` 包进 `Spawn()` 助手方法，同样的静态工具对该方法**静默零报告**。同一个机制，惩罚无辜者、放过真泄漏——因为它的世界观里「名字就是身份」。README:36 承诺的「无需改游戏代码」在这个反例下不成立。

---

## TOP-3

1. **N1 双 scope 静默覆盖**（HIGH）——同一决策两个存储位、一个静默赢，且有事故前科；契约层面必填又丢弃，是对使用者最恶劣的组合。（EffectScript.cs:180 / DerivedMetrics.cs:41-45 / EffectScriptContract.cs:135）
2. **N4 名字即身份**（HIGH）——L2/L3 的正确性锚定在拼写规范形上，误伤自有 API、放跑真泄漏双向失效；这是整个静态审计层的地基裂缝。（EffectAlgebraAnalyzer.cs:290-301 / EffectAlgebraGenerator.cs:107）
3. **N3 AttachShell 穿透 IHost**（HIGH）——抽象接口造好后被内核签名锁死在具体类上，宿主可替换性是纸面的。（PluginRuntime.cs:207-210）

---

## usability 裁决

框架的 L1 代数核心（Claim/Signature/NetTable/Compatible）是本轮见过最接近「值即边界」的部分：readonly record struct、判别联合、构造即全必填——这部分合格。但围绕它的四层各自引入了一个关键 complect：剧本层双 scope（N1）、工具层名字即身份（N4/N5/N13）、运行时层上帝类与标量侵蚀（N6/N8）、文档层白名单漂移（N7）。共同模式是：**每个抽象都被下一个具体决定悄悄短路**。程序员知道的越少才越好——而当前状态下，不读 Fiber.EffectiveSignature 的注释就无法预测 net 闸门，不读 EffectScript.Audit 的扫换线就无法预测哪份预算生效。裁决：**L1 可用可信；L2/L3/壳层在「换个写法就静默失效」的意义上尚不可托付门禁**，修复均不需重写，只需把已经分开的关注点的焊接点剪断。

---

## 证据清单：本轮实际读取过的文件

- README.md
- EFFECT_SCRIPT.md
- docs/spatial-plugin-shell-design.md
- src/Cosmos.EffectAlgebra/ApiMapping.cs
- src/Cosmos.EffectAlgebra/EffectAttributes.cs
- src/Cosmos.EffectAlgebra/EffectScript.cs
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs
- src/Cosmos.EffectAlgebra/Objects.cs
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs
- src/Cosmos.EffectAlgebra.Runtime/IHost.cs
- src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs
- src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs
- src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs
- src/Cosmos.EffectAlgebra.Runtime/NetBenefitClosure.cs
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs
- 两工程 csproj（Runtime/L1）

**未读取**（纪律遵守）：audit/ 目录下任何历史审计报告，含 audit/hickey-x2/、audit/hickey-x3/。

**探针实测**：`P:\Temp\hickey-x3-probe`（仓库外，net10.0，ProjectReference 引用 L1 源码）。输出摘要：
- P1：claim scope Scene("B")/Scene("C") 被 At/Audit 改写归并为 Scene("A")，报 create×create 冲突（证实 N1）。
- P2：同一 EffectScript 实例，字段预算 Passed=True，外部参数预算(cap=0) Passed=False [PeakExceeded]（证实 N5）。
- P3：`default(Budget).Caps is null == true`（证实 N12）。
