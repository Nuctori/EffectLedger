# Hickey-X R4：值语义与身份 —— 反 POP（place-oriented programming）专项审计

> 视角：哪些 API 诱导用户把「值」当「位置」操作？不可变性承诺是否贯穿？相等语义与哈希一致性如何？
> 纪律：一切行号取自当前磁盘工作树（含未提交修复），不引用 audit/ 历史行号。分析由 hickey-auditor 完成。

---

## 核实矩阵（对 R1-R3 相关结论的抽验裁决）

| 历史结论 | 裁决 | 当前证据 |
| --- | --- | --- |
| R1-C5 / Audit 非确定性 | **已修** | EffectScript.cs:52-54 注释与实现一致：`Budget` 为构造期固定属性；sweep 全程用局部 `Dictionary/HashSet`，出口 :258 `violations.ToImmutableArray()`。同输入重复 Audit 结果恒等（测试 EffectScriptEdgeTests.cs:719-733 亦钉住）。 |
| R2-N2 Fiber.State/Dependents internal set | **仍在** | Fiber.cs:37/:42/:43 三处 `{ get; internal set; }`。程序集内仍可绕状态机直写（如 `f.State = FiberState.Dead`）；当前 Runtime 内变更路径受控（Load/Unload/NotifyProviderTeardown/ForceTeardownOnWatchdog/MarkDead 五门），但 internal 边界对同程序集新增代码无防御。 |
| R3-N1 SerializeBudget ⊤→0 | **未复核本轮范围外**（序列化语义非值语义焦点），R3 已二次确认成立。 |
| R1-C3 Signature 结构相等补丁 | **已修且质量合格** | Objects.cs:186-218：`Equals` 用三桶 `SetEquals`、哈希 XOR 折叠 + 桶计数混淆，顺序无关。这是全仓唯一被明确修掉的「假值真引用」。 |

---

## 新发现（V 系列）

### V1 [HIGH] Budget 构造函数直接持有调用方字典引用——record struct 包装的是一把可变位置的钥匙
- **位置**：EffectScript.cs:344-348
  ```csharp
  public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }
  public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; }
  public static readonly Budget None = new(new Dictionary<ResourceId, NatStar>());
  ```
- **判词**：类型名字叫 Budget、长得像值，实则是一张指向调用方 `Dictionary` 的借据——你把「值」交出去，换回一张别人随时可以涂改的「位置」收据。
- **机制**：接口 `IReadOnlyDictionary` 只约束视图不改事实；调用方持原始 `Dictionary` 可随时 `cap[r]=...` 改写预算。更糟的两条链：
  1. **共享别名**：`var b = new Budget(d); var s1 = new EffectScript(evts, b); d[Gpu("x")] = 0;` → s1 的预算被静默改写，Audit 结果随外部位置漂移——这正是 auditR5 F1 在剧本层修掉的问题（:52-54）从字典后门原路回来了。
  2. **静态 None 也非免疫**：`Budget.None.Caps` 运行时类型是可变 `Dictionary`；任何拿到它的一方一次 `(Dictionary<ResourceId,NatStar>)b.Caps` 强转即可污染全局单例（`EffectScriptContract.Parse` 默认路径 :24 正复用该实例）。
- **实证**：现有测试即以裸可变字典喂构造函数（tests/Cosmos.EffectAlgebra.Tests/EffectScriptEdgeTests.cs:46-47、:728）——雷管已经插好，只等有人碰。
- **最小修复**：`Caps { get; } = caps.ToImmutableDictionary(ResourceId.Normalize);` 一行同时完成防御性拷贝 + 键归一化。

### V2 [HIGH] Audit gate(2) 用未归一化 cap 键查归一化峰值表——键纪律在同一函数内精神分裂
- **位置**：EffectScript.cs:236-245
  ```csharp
  foreach (var kv in cap.Caps)
  {
      var nk = ResourceId.Normalize(kv.Key);   // 归一化后查 peakSum/topCount ✓
      ...
      violations.Add(new Violation(t, kv.Key, scope, "PeakExceeded", ...)); // 回填 kv.Key ✗ 未归一化
  ```
- **判词**：查找前把钥匙磨成规范形状，报告里却塞回原来的毛坯——同一把锁，两种键面，Violation 消费者拿到的资源身份取决于用户当初怎么拼写预算键。
- **后果**：`budget["signalBus:s"]` 与 claim 归一出的 SignalBus("s") 是结构相等的键，但 Violation.Resource == Self("signal_s") 原始形态；下游按 Resource 分组/去重/round-trip（SerializeResource 仅认 5 规范构造子）时同一违例裂成两个身份。gate(1)/gate(3)/Leak 路径全部以归一键入 Violation（:162/:174/:288），唯独此处漏网。
- **最小修复**：`new Violation(t, nk, scope, "PeakExceeded", ...)`。

### V3 [MED] ZStar ± 溢出静默回绕——NatStar 有环绕检测，ℤ* 却没有，同一份 PDR 两套溢出律
- **位置**：SignedNet.cs:30-33 `Of(a.Value + b.Value)` / `Of(a.Value - b.Value)`（裸 long 运算，无 checked、无环绕检测）；对照 Numeric.cs:27-29 NatStar 加法显式检测 `sum < a.Value ⇒ Top`。
- **判词**：ℕ* 承诺「永不静默错数」（Numeric.cs:15-16），ℤ* 同页纸承诺 MA-002 闭包，却在极端值下静默给出错号——同一个代数家族，两种诚实标准。
- **触发面**：net 求和路径（Algebra.cs:65 `t._net[r].Add(signed)` → SignedNet.cs:30）。需要 ulong 级 size 差值才触发，概率低但与 N6 maxFinite+1 属同一族「绕过 ⊤ 律的裸算术」。
- **最小修复**：`checked(a.Value + b.Value) catch (OverflowException) => Top`，或仿 NatStar 做符号位环绕检测。

### V4 [MED] ApiMapping.Claims 裸 ImmutableArray 字段 + 公共构造函数——零拷贝优化把可变性风险下放给每个消费者
- **位置**：ApiMapping.cs:13-26：`public ImmutableArray<Claim> Claims { get; }` 无 `readonly` 修饰，公共构造函数原样存储传入数组。
- **判词**：`ImmutableArray` 是「不可变视图包着可变数组」——把它当字段类型不等于拥有不可变字段；struct 里裹着可变引用还免拷贝，这是 easy 打败 simple 的教科书案例。
- **后果**：白名单静态表 `All`（:55）自身安全（构建后无人持原数组），但任何第三方 `new ApiMapping("X", myArray)` 后继续改 `myArray[i] = ...` 即改写「白名单」——而 Claims 数组元素是进 Signature 去重/哈希体系的 Claim，事后变异直接制造哈希表幽灵项。
- **最小修复**：构造函数内 `claims.ToBuilder()` 逐项校验后 `ToImmutable()`，或至少私有化构造、只暴露 `M(...)` 工厂。

### V5 [MED] PluginRuntime 把三个可变位置钉在公共面上——Graph/IsShuttingDown/OnSuspending 各是一条绕过调度器的走廊
- **位置**：
  - PluginRuntime.cs:15 `public DependencyGraph Graph => _graph;`
  - PluginRuntime.cs:21 `public bool IsShuttingDown { get; set; }`（注释自承「测试可置位以模拟关闭路径」）
  - PluginRuntime.cs:30 `public Action<Fiber>? OnSuspending { get; set; }`
- **判词**：注释说 IsShuttingDown 是为测试开的口子——为测试方便而在生产 API 上凿洞，就是让最不重要的用户决定最重要的不变量。
- **后果**：(a) 外部可直接 `_runtime.Graph.AddHardEdge(...)` 完全绕开 AddDependency 的 Dependents 回填（PluginRuntime.cs:82-88 双写协议的一半），制造图与 Fiber.Dependents 永久分叉；(b) AttachShell(:229) 会无条件覆盖 OnSuspending，宿主自设钩子被静默吞掉；(c) SynchronousExitDrain 结尾复位 IsShuttingDown=false（:215）期间任何线程读到的都是瞬时假象。
- **最小修复**：Graph 收窄为 `IReadOnlyDependencyGraph`（暴露查询方法）；IsShuttingDown 改 internal + 测试经 InternalsVisibleTo；OnSuspending 提供 AttachShell 单一写入方。

### V6 [LOW-MED] record 自动相等携带引用成员的隐式契约——Coeffect/CycleReport/Violation(string Kind)/PartialReleaseDiagnosis 四处
- **位置**：
  - Fiber.cs:14 `sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope)` — Require/Provide 引用成员参与自动 Equals/GetHashCode；
  - DependencyGraph.cs:10-13 `CycleReport(… ImmutableArray<FiberId> HardCycle, …)`；
  - InverseReplay.cs:7-11 `PartialReleaseDiagnosis(… Completed, Pending …)`；
  - EffectScript.cs:365-380 Violation 含 `string Kind`。
- **判词**：record 生成的相等是「逐字段比引用」，于是两个语义相同的状态是否相等，悄悄取决于字符串来自字面量池还是 Substring——程序员必须知道 string interning 的实现细节才能预测行为，这违背了「知道得越少越好」。
- **现实边界**：本仓所有字符串均来自字面量/JSON 解析，实际碰撞风险低；真正的坑在未来有人用 `Substring` 或动态拼接构造 Component/Name（Normalize 内部恰有 Substring，Objects.cs:70-73，但其结果随即被 new StringName 包装成新对象，不受此影响）。
- **最小修复**：无需大动；在 Coeffect 等 record 上补一行注释声明「Resource/Scope 参数视为已归一化值，相等按引用成员逐位」，或对高频做字典键的成员提供显式 Comparer。

### V7 [LOW] IncludedIn 用 record 相等冒充偏序——半格的 join 在哪？
- **位置**：Objects.cs:101-107：`if (Equals(other)) return true; if (other is Global) return true; return false;`
- **判词**：注释自称偏序 ⊆*，实现只有「相等 ∨ 对方是 Global」两档——Method("a") ⊑ Method("b") 判 false 没错，但这不是一个格，是一个只有底和顶的两层脚手架；文档许诺的数学和代码交付的数学不是同一个。
- **现状评估**：当前所有消费点（Algebra.cs:57 过滤、Deviation 取 Global）只用「同 scope ∨ Global」语义，行为正确；风险在于后续有人按「偏序」二字推导出上确界/下确界存在。
- **最小修复**：注释如实降级为「两层预序：相等或 Global 包含」，或在引入真实子作用域时同步给 join。

### V8 [LOW] Signature.Of(params Claim[]) 数组形参诱导批量变异模式
- **位置**：Objects.cs:161 `public static Signature Of(params Claim[] claims)`
- **判词**：params 数组让调用方先攒一个可变 `List<Claim>` 再倒手——数据在进入不可变世界前的最后一公里仍走可变通道；Signature 内部 Add 时会 Normalize+去重（:167-170），所以仅多一次中间分配，无正确性问题。
- **最小修复**：可加 `Of(IEnumerable<Claim>)` 重载（EffectScript 构造函数已有 IEnumerable 先例 :62-63）；纯锦上添花。

### V9 [INFO] 身份模型选型审计结论：双轨制有清晰的分界线（本轮正面发现）
- **证据**：L1 代数域全部 `readonly record struct`（NatStar/Interval/ZStar/SignedInterval/LoopCount/Claim/Budget/AuditResult/Violation，Numeric.cs:13/:66、SignedNet.cs:12/:44、EffectScript.cs:336/:352/:365、Objects.cs:130）+ `abstract sealed record class` 判别联合（ResourceId/ScopeId 及其嵌套 sealed record，Objects.cs:20/:92）；L2 运行时实体为 mutable class（Fiber/PluginRuntime/DependencyGraph/GodotShell）。
- **判词**：「数学对象是值，运行时实体是生命」——这条线画得干净，值得肯定；唯一的越界者是 V1 的 Budget：它身处值区却持有引用内脏。另注意 `readonly record struct` 免疫了 `default(T)` 对这些载体的部分攻击面（default(Claim).Size=null 已被 Normalize 兜住），但 default(LoopCount) 后门（R2-N3/R3 确认）不在本轮范围。

---

## TOP-3

1. **V1 Budget 别名泄漏**：唯一一处「值区里的位置」，且 EffectScript.Budget 固定化修复（auditR5 F1）被字典引用绕开；一行 ToImmutableDictionary 同时修防御性拷贝与键归一。
2. **V2 PeakExceeded 违例键未归一**：同一函数内查找归一、报告不归一，Violation 消费者的资源身份不可预测；一行改动。
3. **V5 PluginRuntime 公共可变三角**：Graph 直通 + 测试专用布尔 + 可覆盖钩子，三条绕过调度器协议的走廊；收窄接口即可。

## 文件清单

- 通读（当前磁盘）：src/Cosmos.EffectAlgebra/{Objects,Algebra,Numeric,SignedNet,EffectScript,DerivedMetrics,Deviation,ApiMapping,EffectAttributes,EffectScriptContract}.cs
- 通读：src/Cosmos.EffectAlgebra.Runtime/{Fiber,DependencyGraph,PluginRuntime,InverseReplay,LoadValidation,NetBenefitClosure,ProviderCrashCascade,GodotShell,IHost}.cs
- 抽验：tests/Cosmos.EffectAlgebra.Tests/{EffectScriptEdgeTests,EffectScriptContractTests}.cs（Budget 构造传参方式）
