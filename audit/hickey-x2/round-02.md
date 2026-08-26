# Cosmos.EffectAlgebra 对抗性审计 · 第 2 轮 —— Value, Identity, State 视角（Rich Hickey）

> 审计纪律：本轮为独立上下文，按任务约束**未读取 audit/ 下任何历史报告**（含 hickey-x2 与 rich-hickey-*）。故「核实矩阵」不裁决历史报告原文，改为对**代码注释中自述的历史修复承诺**（auditR5 F1、reviewer #188/#191 等）逐条核实——注释是项目对自己许下的契约，兑现与否可当场对证。
> 判据：simple = 不纠缠、一个职责、值语义；每个公共成员都是永久承诺；「程序员知道得越少越好」——用户不该读实现才能预测行为。

---

## 核实矩阵（代码内自述修复承诺的裁决）

| # | 自述承诺 | 位置 | 裁决 | 证据 |
|---|---|---|---|---|
| V1 | 「构造即固定，使 EffectScript 为不可变值对象……同实例 Audit 结果不再依赖可变状态」（auditR5 F1） | EffectScript.cs:60 | **部分修 / 名义修复** | `Budget` 构造器原样存引用：`public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; }`（EffectScript.cs:345）。字段本身不可变，但字典是调用者的活引用；`Budget.None` 的静态字典（EffectScript.cs:348）强转回 `Dictionary` 即可全局改写。「Audit 不依赖可变状态」的承诺以新形式回归（见 N1 推演 A） |
| V2 | Signature 结构相等补齐（R4 P1，「Hickey 式 footgun」自认） | Objects.cs:198-225 | **已修** | `Equals` 三桶 SetEquals + 顺序无关哈希 + operator==；BucketIsolationTests.cs:120-148 `Signature_StructuralEquality` 断言背书。注释诚实承认了问题，修复到位 |
| V3 | 逆回放仅 TearingDown 态（reviewer #188 F3） | InverseReplay.cs:20-21 | **已修** | 非 TearingDown 直接抛。守卫存在且正确 |
| V4 | 部分逆释放失败升级崩溃级联、不被静默 MarkDead 掩盖（reviewer #188 F2） | PluginRuntime.cs:143-148 | **部分修** | 诊断确实上抛并记 CrashReports；但 `ReplayAndDead` 在 `failedIndex >= 0` 时**仍无条件** `fiber.MarkDead()`（InverseReplay.cs:43），`ProviderCrashCascade.Handle` 再补一刀 fail-open MarkDead（ProviderCrashCascade.cs:19）。异常被「观测到」了，但状态机照旧说谎——掩盖从状态层转移到了日志层，没消除（见 N2 推演 B） |
| V5 | Defer 句柄判空真接通、fail-closed 防 use-after-free（reviewer #191 F2） | GodotShell.cs:23-33 | **已修但有缝隙** | 门控逻辑本身在；但去重键是委托**引用身份**（GodotShell.cs:10,25），同一 Action 实例服务不同 handle 时第二个请求被静默吞掉——fail-closed 变成 fail-silent-loss（见 N4 推演 C） |
| V6 | EFFECT_SCRIPT.md §2.2 承诺剧本为 `readonly record struct EffectScript` | EffectScript.cs:55 | **文档-实现脱节** | 实现是 `sealed partial class EffectScript`——引用相等、可别名共享。文档向 AI 使用者许诺值语义，代码交付实体语义。两个结构相同的 Script 不相等；把 Script 当字典键/做快照比对的用户会踩身份坑 |
| V7 | OnSuspending/ExitDrain 孤岛接线（reviewer #191 F1/F2） | PluginRuntime.cs:248-253 | **已修** | AttachShell 双向接线到位 |
| V8 | gate(3) 冲突分组改用 e.Scope 与 At 投影一致（auditR3b TC7） | EffectScript.cs:186-188 | **已修** | `var key = (r, e.Scope, (int)c.Mode)` 确用事件 scope |

---

## 新发现

### N1 · HIGH — `Budget.Caps` 是活视图不是值：「不可变值对象」的自我声明为假
- **位置**：EffectScript.cs:345（构造器存引用）、EffectScript.cs:348（`Budget.None` 共享静态可变字典）、EffectScript.cs:60（虚假声明）
- **判词**：你宣称剧本是纯数据、Audit 是纯函数（EFFECT_SCRIPT.md §5「反例 100% 可复现」），却让审计结果挂在调用者手里的一本可变字典上——这不是 immutable，这是 optimistic immutability。
- **推演 A（使用者持有引用后系统行为改变）**：
  ```csharp
  var caps = new Dictionary<ResourceId, NatStar>();
  var gpu = new ResourceId.Gpu(new Rid("mesh1"));
  caps[gpu] = NatStar.Of(64);
  var script = new EffectScript(events, new Budget(caps));   // :345 存的是同一个 dict
  caps[gpu] = NatStar.Of(ulong.MaxValue);                    // 调用者改写
  script.Audit();                                            // Audit():105 → foreach kv in cap.Caps
  ```
  追踪：`Audit(Budget cap)` 的 gate(2) 直接迭代 `cap.Caps`（EffectScript.cs:222 起），拿到的是被改写后的 MaxValue ⇒ PeakExceeded 永不触发 ⇒ 显存爆炸预算门静默失效。更糟：`((Dictionary<ResourceId,NatStar>)Budget.None.Caps)[r] = ...` 可污染进程级共享的 `None`，波及一切默认预算的剧本。
- **最小修复**：构造器做防御性拷贝 `caps.ToImmutableDictionary()` 或至少 `new Dictionary<>(caps)`；`Caps` 类型改 `IImmutableDictionary`。

### N2 · HIGH — 部分逆回放失败仍标 Dead：状态机断言与资源事实脱节
- **位置**：InverseReplay.cs:43（无条件 MarkDead）、Fiber.cs:17（`Dead // 已完成逆回放，资源释放`）、PluginRuntime.cs:45（泄漏看门狗只看 Active）
- **判词**：`Dead` 的文档语义是「资源释放」，代码语义是「我放弃了」。当枚举的字面含义和转移条件分道扬镳，五态状态机就从「可推理」降格为「可祈祷」。
- **推演 B（以为拿到状态其实是谎言）**：
  Fiber F 声明 inverses = [release GPU buffer（会抛）, release memory]。BeginTeardown → TearingDown → DrainTeardownBatch → ReplayAndDead：
  - inv1 抛 ⇒ failedIndex=0, pending=[Gpu]，循环继续（InverseReplay.cs:34-38 注释明言「不中断」）
  - inv2 成功
  - **无条件 `fiber.MarkDead()`**（InverseReplay.cs:43）⇒ State=Dead
  - 返回 AllCompleted=false ⇒ PluginRuntime 记 CrashReport 并级联（PluginRuntime.cs:143-148）
  此刻任何宿主查询 `TryGetFiber(F).State` 得到 `Dead`，按 Fiber.cs:17 的语义即「GPU buffer 已释放」——实际它还挂着。第二重脱节：周期泄漏看门狗 `CheckPermanentFiberLeak` 过滤条件是 `f.State == FiberState.Active`（PluginRuntime.cs:45），这个「Dead 但泄漏」的 Fiber 同时逃过状态查询与泄漏告警双通道；唯一出口是宿主主动轮询 CrashReports（PluginRuntime.cs:24 注释自认无头不上抛）。
- **最小修复**：引入第六态 `PartiallyReleased`（或 Dead 携带 `PartialReleaseDiagnosis`）；`MarkDead` 仅在 `AllCompleted` 时允许；`CheckPermanentFiberLeak` 把带 pending 的终态纳入扫描。

### N3 · HIGH — `InverseClaim.Execute` 裸委托回放无句柄门控：权威清理路径独享 use-after-free 通道
- **位置**：InverseReplay.cs:31（`inv.Execute()` 无任何有效性检查）；对照 GodotShell.cs:27-32（Defer 有 IsSafeToInvoke 门控）
- **判词**：同一个框架里，普通延迟调用有原生句柄判空的 fail-closed 防护，而最危险的路径——teardown 时逆释放别人持有的资源——反而裸奔。防护不成体系，等于没有防护。
- **推演 D（快照与现实脱节的缝隙）**：装载期用户注册 `new InverseClaim(res, scope, () => node.QueueFree(), …)`，闭包捕获当时的节点活引用。数小时后 provider teardown 触发回放，该节点早已被游戏侧自行 QueueFree ⇒ `inv.Execute()` 操作已释放的原生对象。Defer 路径对此有 `IsInstanceValid` 门控（且异常隔离为 not-safe，GodotShell.cs:28-31），InverseReplay 无 IHost 访问、无等价机制——异常会被 catch 进 pending（InverseReplay.cs:34-37），但 use-after-free 往往不抛异常而是破坏引擎状态。这正是「装载期声明的逆（快照）」与「teardown 时刻的现实状态」之间无人值守的缝隙。
- **最小修复**：`InverseClaim` 增加 `object? Handle` 字段，`ReplayAndDead` 经注入的 `IHost.IsInstanceValid` 门控后再执行；或约定逆 Action 必须经壳 Defer 包装。

### N4 · MED — GodotShell 以委托引用身份做去重键：身份冒充意图
- **位置**：GodotShell.cs:10（`HashSet<Action>` 注释「同 Action 引用仅入一次」）、GodotShell.cs:25
- **判词**：把「是不是同一个 lambda」当成「是不是同一件事」——引用相等是实现的巧合，不是语义。
- **推演 C（合并导致静默丢失）**：子系统 A 与 B 复用同一缓存委托 `s_save`（各自绑定不同 handle）：`shell.Defer(s_save, handleA); shell.Defer(s_save, handleB);` 第二次 `_deferred.Add` 失败 ⇒ B 的请求无声消失。若排空前 handleA 失效，门控丢弃回调（:29-33），B 的工作**从未执行且无人知晓**。
- **最小修复**：去重键改为 `(action, handle)` 二元组；或提供显式幂等键参数。

### N5 · MED — `Register` 返回活 Fiber 且 `Load()` 公开：装载闸门可整体绕过
- **位置**：PluginRuntime.cs:53（返回 Fiber）、Fiber.cs:73（public Load）、PluginRuntime.cs:74-102（闸门全在 LoadAll 内）
- **判词**：硬环拒载、ValidateForLoad、VerifyNetClosure 全部只在 `LoadAll` 一条路上；类型却把 `Load()` 这把钥匙发给每个人。好的 API 让非法状态不可表示，这里非法状态只需一行就能表示。
- **后果**：`runtime.Register(spec)` 后直接 `fiber.Load()` ⇒ Active 态、未经 net 闭合校验、未经双重释放交叉判定的 Fiber 进入调度（`ShouldDispatch` 只查 State==Active，PluginRuntime.cs:259）——§5 闸门形同虚设的旁门。`TeardownEnqueued`/`Dependents` 的 internal set 是对的，唯独状态机的入口没锁。
- **最小修复**：`Load()` 改 internal，唯一公共入口为 `PluginRuntime.LoadAll`（或提供 `Load(fiber)` 版本内联执行校验）。

### N6 · MED — `PluginRuntime.Graph` 直接暴露内部可变依赖图：查询接口实为活视图
- **位置**：PluginRuntime.cs:15（`public DependencyGraph Graph => _graph`）、DependencyGraph.cs:18-20（内部 HashSet/Dictionary 可变）
- **判词**：使用者拿到的不是图快照，是一根插进心脏的导管——运行时每次 Register/AddEdge/Remove 都实时改写观察者手里的「状态」。想回答「此刻拓扑是什么」的人必须知道实现里哪些方法恰好是纯读的。
- **最小修复**：暴露只读投影（`IReadOnlyCollection<(Dependent,Provider)> Edges { get; }` 快照），Graph 转 internal。

### N7 · MED — `IsShuttingDown` 公开可写布尔承载安全不变量
- **位置**：PluginRuntime.cs:21（`public bool IsShuttingDown { get; set; }`）、PluginRuntime.cs:189（排空完毕无条件复位 false）
- **判词**：一条「关闭期禁止新装载」的安全不变量，被建模成任何人都能随手拨动的开关。不变量的守护者应该是类型，不是使用者的自觉。
- **后果**：任意消费代码置 `IsShuttingDown=false` 即可在级联 teardown 中途 Register（:55 的守卫立刻失明）；:189 的复位也使「退出排空进行中」这一区间对外不可观测。状态机的关闭维度游离在五态之外。
- **最小修复**：改 `private set`，对外仅暴露 `bool IsShutdownInProgress => _draining;`。

### N8 · LOW — teardown 入队维度旁路状态机：`TeardownEnqueued` 与五态纠缠
- **位置**：Fiber.cs:43（布尔字段）、Fiber.cs:85-88（Unload 依赖它短路）、Fiber.cs:100-104（ForceTeardownOnWatchdog 又置又查）
- **判词**：「已入队待回放」是一个真实的状态，却被塞进五态之外的旁路布尔。回答「这个 Fiber 现在处于什么阶段」需要同时读两个字段并心算笛卡尔积——TearingDown+enqueued、TearingDown+watchdog-forced、Suspending+not-enqueued……组合态数量翻了倍。
- **最小修复**：入队并入状态机（如 `TeardownQueued` 中间态），消灭旁路布尔。

### N9 · LOW — Violation 输出顺序依赖 Dictionary 插入序，「确定性」承诺打了折
- **位置**：EffectScript.cs:227（`foreach kv in net` 产出 NegativeDip 顺序）、EffectScript.cs:234（cap.Caps 同理）；EFFECT_SCRIPT.md §5 承诺「反例 100% 可复现」
- **判词**：同一运行时同输入确可复现；但 .NET 不保证 Dictionary 枚举序跨版本稳定——给 AI 回修的反例清单顺序是偶然的，而 Violation 数组是公共 API 形状。
- **最小修复**：violations 按 (AtT, Resource, Kind) 排序后输出。

---

## TOP-3

1. **N2 · 部分逆回放失败仍 MarkDead（HIGH）**——状态机的核心承诺（Dead=资源释放）在最需要它的故障路径上为假，且同时致盲泄漏看门狗。系统对「我现在处于什么状态」给出了系统性错误答案。
2. **N1 · Budget.Caps 活视图（HIGH）**——框架自己写着「修好了 Audit 依赖可变状态」，实际只是把可变性藏进了字典引用。纯函数外衣下的隐式输入，是最典型的 easy-over-simple。
3. **N3 · 逆回放裸执行无句柄门控（HIGH）**——防护体系只在顺手的地方安装（Defer），权威清理路径反而裸奔。快照式逆声明与真实世界状态之间的缝隙无人值守。

---

## usability 裁决

L1 代数层（Claim/Interval/NatStar/Signature）是本轮见过最接近「值」的部分：readonly record struct、构造即全必填、Normalize 单点归一、Signature 结构相等已补齐——这部分可以放心交给不看实现的使用者。但两条公共主线不及格：**EffectScript 层的「不可变值对象」声明与 Budget 活视图事实相悖**（文档承诺 record struct、实现交付 class + 活字典），以及 **Runtime 层的状态机在故障路径上主动说谎**（Dead≠已释放、闸门可绕过、不变量是公开开关）。结论：**当前形态不适合直接暴露给插件作者/宿主集成者**；修复 TOP-3（预计均为小改动：防御拷贝、条件 MarkDead、Handle 门控）之前，Runtime 公共面应视为内部 API。修复后 L1+EffectScript 可先行开放。

---

## 证据：本轮读取过的文件

- README.md
- EFFECT_SCRIPT.md
- src/Cosmos.EffectAlgebra/Objects.cs
- src/Cosmos.EffectAlgebra/Algebra.cs
- src/Cosmos.EffectAlgebra/Numeric.cs
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs
- src/Cosmos.EffectAlgebra/EffectScript.cs
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs
- src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs
- src/Cosmos.EffectAlgebra.Runtime/IHost.cs
- src/Cosmos.EffectAlgebra/Runtime/InverseReplay.cs（src/Cosmos.EffectAlgebra.Runtime/InverseReplay.cs）
- src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs
- src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs
- src/Cosmos.EffectAlgebra.Runtime/ProviderCrashCascade.cs
- tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs
- tests/Cosmos.EffectAlgebra.Tests/BucketIsolationTests.cs
