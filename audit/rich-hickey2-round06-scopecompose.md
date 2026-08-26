# Rich Hickey 视角 — Round 06 scope 与组合性（scope 是否在组合中一致传播）

> 审计员：hickey-auditor · 透镜：scope 与组合性 — scope 是否在组合中一致传播、组合是否可组合 · 轮次：R6/10
> 判据：decomplect — 耦合的解耦、可组合的保持可组合；边界做成类型，非法状态不可表示；API 面积是最昂贵的承诺

## 核实矩阵（历史结论逐条裁决 — 仍在/已修/部分修/误报 + 行号证据）

| 历史项 | 来源 | 本轮裁决 | 行号证据 |
|---|---|---|---|
| Sequence/Parallel/Join L1 警告（幻象区分诚实化） | R5 V5-001 / e0b966b | **已修** | `DerivedMetrics.cs:67-70` Sequence doc 含 `L1 警告…与 Union 完全等价`；`DerivedMetrics.cs:72-75` Parallel 同型 + 前置 PARA_CONFLICT 守卫；`Objects.cs:207` Join doc 含 `L1 警告…与 Union 仅在同键合并上差异`。警告已置顶 |
| LoopCount.IsValid / TryOf 派生 | R5 V5-002 / e0b966b | **已修** | `DerivedMetrics.cs:26` `IsValid => Count.IsTop \|\| Value>=1`；`DerivedMetrics.cs:29-34` TryOf；`EffectScript.cs:46` 消费侧统一 `if(!loop.IsValid) throw` |
| Audit 采样点审计诚实声明 | R5 V5-003 / e0b966b | **已修** | `EffectScript.cs:104` Audit doc 首句 `**采样点审计**…端点 ∪ 尾段代表点采样≠全连续区间` 已置顶与 EFFECT_SCRIPT.md §锐边对齐 |
| Claim record struct default/with 锐边留档 | R5 V5-004-007 | **已修（文档+边界校验）** | `Objects.cs:183-192` Signature.Add 校验 `null Resource/Scope` + `default: throw 未知 Kind`；`EffectScript.cs:46` 拦截 default Loop；R5 报告已留档锐边 |
| Budget 可变字典 → ImmutableDictionary | R3 V3-E / R4 | **已修** | `EffectScript.cs:357-360` 构造 `ToImmutableDictionary()` 防御拷贝；`EffectScript.cs:363` None=Immutable 空；`EffectScript.cs:366-384` 值相等/哈希按内容 |
| Unknown→Use fail-open 锁死 | synthesis J / PDR §3.2.3 P4 | **锁死（仍 fail-open，术语已统一）** | `Algebra.cs:10` 注释 `fail-open/permissive`；`Algebra.cs:12` Resolve Unknown=>Use |
| Size ?? [1,1] 缺省 12 处散布 | synthesis K / §3.1.5a DO-1 | **锁死（语义锁，散布仍在）** | `Claim.Normalize: Size??Interval.Default` 为单一归一源；消费点仍 `c.Size ?? Interval.Default` 12 处散布（设计锁死，见 R4-005） |
| Sequence≡Parallel≡Union 于 L1 锁死 | synthesis F | **锁死+部分分化** | Sequence 仍 `=>Union` 纯等价；Parallel 已分化为 Union+Compatible 守卫；Join 已分化为 Merge 配对（`Objects.cs:208-224`），L1 警告已补 |
| Join 注释承诺 merge_I 但实现为裸 Union | synthesis 4.1 | **已修** | `Objects.cs:208-224` Join 现为按 `(Kind,Resource,Mode,Scope)` Merge 同键区间，非裸 Union |
| Scope 双份真相 / Violation 伪造 Global | synthesis C / R1 F3 | **部分修** | `EffectScript.cs:159-163,305-321` Violation 归因已取首个 `e.Scope` 不再硬编码 Global；但 **JSON 层双真相仍在**（见新发现 S06-001） |
| A. 序列化三臂 `_=>` 静默兜底 | synthesis A | **已修** | `EffectScriptContract.cs:251-274` SerializeScope/Resource/ResourceKey 均 `_=>throw FormatException` |
| H. Peak.Compute 跨桶聚合 | synthesis H / R2-005 | **已修** | `Algebra.cs:119` `if(c.Kind!=Occupy) continue` 已与 NetTable 单一真源 |

## 新发现（scope 与组合性透镜）

### S06-001 — HIGH — 双份 scope 真相在 JSON 层仍未收敛：事件级 scope 与 claim 级 scope 可不一致，Contract 不校验，Audit 静默重写

- **位置**：`EffectScriptContract.cs:83` `ParseEvent` 读事件 `scope` + `EffectScriptContract.cs:184` `ParseClaim` 读 claim `scope` + `DerivedMetrics.cs:58-62` `Loop` 重写 `Scope=loopScope` + `EffectScript.cs:95` `At` 经 `Combination.Loop(...,e.Scope)` 重写 + `EffectScriptContract.cs:271` `SerializeClaim` 原样写 `c.Scope`
- **判词**：让 claim 自带 scope 与 event scope 同时存在，却只让运行时以 event scope 为准——是把“层次一致”留在注释里，让两个真相在 JSON 里各说各话。
- **证据**：
  ```csharp
  // EffectScriptContract.cs:83 ParseEvent 接受事件级 scope
  var scope = ParseScope(Require(ev,"scope",layer));
  // :184 ParseClaim 同时接受 claim 级 scope
  var scope = ParseScope(Require(c,"scope",layer), $"{layer}.scope");
  // EffectScript.cs:95 At(t) 丢弃原 c.Scope
  acc = Union(acc, Combination.Loop(e.Footprint,e.Loop,e.Scope)); // loopScope = Event.Scope
  // DerivedMetrics.cs:58 Loop 内
  result = Union(result, Of(c with { Scope = loopScope, ... }));
  ```
  探针实测（`/tmp/cosmos_scope`）：
  ```
  Event.Scope=Scene { Name = A } Footprint claim scope=Scene { Name = B }
  At(0) claim scope=Scene { Name = A } resource=Gpu { BufferId = Rid { Value = r1 } }
  ToJson contains scene B? True contains scene A? True
  After roundtrip claim scope=Scene { Name = B }   // 往返后仍 B，下一轮 At 又重写为 A
  ```
  同一剧本在磁盘形态（B）与运行时形态（A）上 scope 永久分裂；gate(3) 分组键 ` (r, e.Scope, mode)` 与 claim 自带 `c.Scope` 分裂曾导致“两视角冲突归因错位”（已在 gate 3 修为 e.Scope），但数据契约侧仍允许错位输入无声通过。
- **最小修复**：二选一收口：(a) Contract 层校验 `Require claimScope == eventScope else throw FormatException($"{layer}: claim scope 须与所属 event scope 一致")` 并在文档声明单一真相为事件级；或 (b) 废弃 claim 级 scope 字段（Parse 时忽略/拒绝），`SerializeClaim` 统一写事件 scope。各选一配套测试钉。
- **severity**：HIGH
- **testHint**：`Parse("""{"events":[{"lifetime":[0,10],"scope":{"scene":"A"},"footprint":[{"kind":"occupy","resource":{"gpu":"r1"},"mode":"create","scope":{"scene":"B"},"size":[1,1]}]}]}""") => FormatException`；`Parse(ToJson(script)).At(t).Equals(script.At(t))` scope 一致断言
- **verdict**：fixable

### S06-002 — HIGH — Budget Caps 未在构造期归一化：同一归一化资源可占两条目，相等/哈希/ToJson 全分裂

- **位置**：`Objects.cs:52-60` `ResourceId.Normalize`（Self("signal_foo")→SignalBus("foo")） + `EffectScript.cs:357-360` `Budget(IReadOnlyDictionary caps) => caps.ToImmutableDictionary()` 无 Normalize + `EffectScript.cs:243-247` Audit 侧 `nk = Normalize(kv.Key)` 才归一 + `EffectScriptContract.cs:290-293` `ResourceKey` 对未归一 Self 抛
- **判词**：把归一化的真相推迟到 Audit 采样点才做，是让 Budget 这个值在构造时是两个键、在审计时是一个键——值语义在时间上 complect。
- **证据**：
  ```csharp
  // EffectScript.cs:357 Budget 构造未归一
  Caps = caps is null ? Empty : (caps as ImmutableDictionary<...>) ?? caps.ToImmutableDictionary();
  // EffectScript.cs:245 Audit 才归一
  var nk = ResourceId.Normalize(kv.Key);
  var p = (topCount.TryGetValue(nk,...)?Top:peakSum.GetValueOrDefault(nk,...));
  ```
  探针实测：
  ```
  bDup Caps count=2 keys=Self { Component = signal_x }->1 | SignalBus { Name = signal_x }->999  // 同一归一化资源占两条
  b1.Equals(b2)=False expected TRUE -> 自 signal_foo vs SignalBus foo 结构不等
  Hash equal? False
  ToJson b1 threw: 不可序列化的 budget 键资源: Self { Component = signal_foo }
  ```
  后果：① 同内容不等（值语义破）；② `new Budget({Self(signal_foo):1, SignalBus(foo):999})` 静默保留两条，Audit 迭代两条 `nk` 相同，`peakReported` 去重后第二条 cap 值被丢弃，预算语义不确定；③ 经 `new Budget(dict)` 构造的合法内存对象 ToJson 直接抛（Parse 侧已归一为 SignalBus 的输入却 ToJson 失败，往返断裂）。
- **最小修复**：Budget 构造期归一化 + 合并：
  ```csharp
  Caps = caps is null ? Empty : caps.GroupBy(kv=>ResourceId.Normalize(kv.Key))
                               .ToImmutableDictionary(g=>g.Key, g=>g.Last().Value); // 或显式去重抛 FormatException
  ```
  同步修 `Equals/GetHashCode` 已基于 Caps 内容，但 Caps 本身须先归一；`SerializeBudget` 前可断言 `kv.Key equals Normalize(kv.Key)`。
- **severity**：HIGH
- **testHint**：`Budget bDup = new(new Dictionary{{Self("signal_x"),Of(1)},{SignalBus("x"),Of(999)}}); Assert.Equal(1,bDup.Caps.Count);`；`new Budget({Self("signal_foo"):5}).Equals(new Budget({SignalBus("foo"):5})) == true`；`ToJson(new EffectScript(events, bDup))` 不抛
- **verdict**：fixable

### S06-003 — MED — Budget 序列化/反序列化不对称：Parse 归一后合法，ToJson 对未归一输入 fail-fast 抛，往返只在“恰好归一”子集上成立

- **位置**：`EffectScriptContract.cs:218-228` `ParseBudget` 经 `ParseResourceKey` 产归一键 + `EffectScript.cs:357` Budget 不归一 + `EffectScriptContract.cs:295-303` `ResourceKey` 仅处理归一后 5 构造子 ` _=>throw`
- **判词**：让 Parse 做归一、Serialize 不做归一，是让“可解析”不等于“可序列化”——往返的承诺在资源身份上断开。
- **证据**：`EffectScriptContract.cs:290` `ResourceKey` 对 `Self` 直接 `throw new FormatException($"不可序列化的 budget 键资源: {r}")`，而 `new Budget(new Dictionary{{Self("signal_foo"),Of(5)}})` 构造成功，`EffectScriptContract.ToJson(script)` 抛。`Parse("""{"budget":{"signalBus:foo":5}}""")` 产 `SignalBus(foo)` 却 `ToJson` 成功——同一语义两种写法一可往返一不可。
- **最小修复**：随 S06-002 构造期归一后，`ResourceKey` 的 `throw` 仅对真正不可序列化资源生效；或在 `SerializeBudget` 入口显式 `Normalize` 后再 `ResourceKey`，使未归一输入也能往返（与 `SerializeClaim` 侧 `SerializeResource(Normalize(c.Resource))` 对称）。
- **severity**：MED
- **testHint**：`var b=new Budget(new Dictionary{{new ResourceId.Self("signal_foo"),NatStar.Of(5)}}); var json=EffectScriptContract.ToJson(new EffectScript(ImmutableArray<EffectEvent>.Empty,b)); Assert.Contains("signalBus:foo",json);`
- **verdict**：fixable

### S06-004 — MED — Audit 峰值归因 scope 取“首个贡献者”而非“当前 t 存活集”，跨事件资源归因在首个事件过期后仍指旧 scope

- **位置**：`EffectScript.cs:159-163` `netScope/peakScope` 仅 `if(!ContainsKey) set` 首个 + `EffectScript.cs:196` `if(!peakScope.ContainsKey(r)) peakScope[r]=e.Scope` + `EffectScript.cs:162-163` `ResolvePeakScope` 取首个 + `EffectScript.cs:222-228` exit 不删 `peakScope` + `EffectScript.cs:245-249` PeakExceeded 用 `ResolvePeakScope(nk)`
- **判词**：把资源的“首个出现”当资源的“当前归属”，是让时间上的首因谬误写进审计归因——首个≠当前。
- **证据**：
  ```csharp
  // EffectScript.cs:196 只在首个活跃时写入，退出不清理
  if (!peakScope.ContainsKey(r)) peakScope[r] = e.Scope;
  // :222 exit 仅减 topCount/peakSum，不删 peakScope
  if (top) { ... topCount[r]=tc-1; } else { peakSum[r]=... }
  // :247 归因取首个
  var scope = ResolvePeakScope(nk);
  ```
  场景：`e1[0,5] scope=A 占用 Gpu/r size10`，`e2[10,15] scope=B 同资源 size10`，预算 cap=5。在 `t=12` 峰值来自 B，但 `peakScope[r]` 仍为 A（e1 首个写入后未清理），Violation.Scope 误归 A。netScope 同理虽影响 NegativeDip 归因。当首个资源事件过期后，归因恒旧。
- **最小修复**：维护 `Dictionary<ResourceId, HashSet<ScopeId>>` 或 `Dictionary<ResourceId, Dictionary<ScopeId,int>>` 的活跃 scope 计数，exit 时递减并在 `Resolve` 取任意当前活跃 scope（如任一存活事件的 scope）；或在 `AuditAtSample` 现场从 `grp`/`peak 活集` 反推当前活跃 scope（`grp` 已按 `(r,scope,mode)` 存活，`peakScope` 可派生）。属加法性归因修。
- **severity**：MED
- **testHint**：两事件时序不重叠同资源：`[0,5] scope A` + `[10,15] scope B`，budget cap=5；在 `t=12` 的 PeakExceeded 断言 `Violation.Scope is Scene B` 非 A
- **verdict**：fixable

### S06-005 — LOW — ScopeId 序列化仅支持 4/8 构造子，程序侧 8 构造子可构造但 ToJson 对 Shell/Loop/Conditional/Async 直接抛，API 面积不对称

- **位置**：`Objects.cs:90-99` ScopeId 8 构造子（Method/Type/Scene/Global/Shell/Loop/Conditional/Async） + `EffectScript.cs:32-35` EffectEvent 接受任意 ScopeId + `EffectScriptContract.cs:251-256` `SerializeScope` 仅 `Scene/Method/Type/Global else throw`
- **判词**：让类型许诺 8 种 scope，让序列化只认 4 种——是让“可构造”不等于“可持久化”，可组合性在数据契约处收窄。
- **证据**：`EffectScriptContract.cs:251-256` `SerializeScope` 除 4 分支外 `_=>throw FormatException("不可序列化的 scope")`，而 `new EffectEvent(..., new ScopeId.Shell(), ...)` 构造成功且 `script.At(t)` 正常，`ToJson` 抛。Synthesis 已称三臂序列化由 fail-silent 改 fail-fast，但未提 scope 集合的不对称收缩。
- **最小修复**：文档显式声明 Contract scope 白名单为 4 种（Scene/Method/Type/Global），`ParseScope` 已拒绝其他 type，`SerializeScope` 抛保持；或扩展 Contract 接受 Shell/Loop/Conditional/Async（需新增 type 字符串与 Parse 分支）。前者为 doc-only。
- **severity**：LOW
- **testHint**：`new EffectEvent(Interval.[0,10], new ScopeId.Shell(), sig)` 后 `EffectScriptContract.ToJson(script)` 断言抛 FormatException 且消息含不可序列化 scope
- **verdict**：doc-only（设计取舍，fail-fast 已正确，仅需文档单一真相）

### S06-006 — LOW — Join/Scope 键精确相等 vs Net/Peak 的 IncludedIn 包含，分组语义在 L1 内双轨

- **位置**：`EffectScript.cs:189` gate(3) `key=(r,e.Scope,mode)` 精确相等 + `Objects.cs:210` Join `key=(Kind,Resource,Mode,Scope)` 精确 + `Algebra.cs:60,122` Net/Peak `if(!c.Scope.IncludedIn(scope)) continue` 包含
- **判词**：让网表/峰值用偏序包含，让冲突用精确相等——是让“同 scope”在同一库内有两种定义，分组的可组合性靠调用方记住哪条路径用哪种。
- **证据**：探针 `Two scopes A/B same resource create×create violations=1` 实际因 Leak 1 条而非 CompatibleConflict——`gate(3)` 按精确 scope 分组，A/B 不同组不冲突；若按 IncludedIn 则 Global 含一切会跨 scope 报冲突。二者今日按 spec 各自正确（Compatible 按精确同 scope，Net/Peak 按包含聚合），但未在 doc 统一说明。
- **最小修复**：doc-only：在 `EffectScript.Audit` gate(3) 注释与 `Signature.Join` 注释中显式声明“冲突分组为精确 Scope 相等，非 ⊆* 包含；Net/Peak 的 scope 参数为包含查询”，使两种分组的选用理由可预测。
- **severity**：LOW
- **testHint**：`Signature.Join` 对同资源同 Mode 不同 Scope 的 Claims 断言合并后仍两条（scope 区分）；`NetTable.Compute(sig, Global)` 对 Scene A 的 claim 断言被包含
- **verdict**：doc-only

## TOP-3（本轮最重要的三个发现）

1. **S06-001 双份 scope 真相（HIGH）** — JSON 同时接受事件级与 claim 级 scope，运行层静默以事件级为准，序列化层保留 claim 级旧值，往返后分裂永久化。层次一致性无校验是 scope 组合性的根裂缝。
2. **S06-002 Budget Caps 构造期未归一（HIGH）** — 归一化延迟到 Audit 才做，导致相等/哈希/去重/ToJson 全部分裂，同一资源可占两条目，预算语义不确定。需收口到 Budget 构造单一真源。
3. **S06-004 峰值归因 scope 取首个而非当前（MED）** — 首个贡献者 scope 在事件过期后仍被复用，跨时段同资源违例归因错位，削弱 Violation 供 AI 回修的可用性。

## 证据清单（本轮实际读取）

- `src/Cosmos.EffectAlgebra/EffectScript.cs` 全文（EffectEvent/At/Audit 扫换线、net/peak/group、Resolve*Scope、ScaleSize、Budget/AuditResult/Violation 204-451 行）
- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs` 全文（LoopCount.IsValid/TryOf、Combination.Loop Scope 重写、Scale、Sequence/Parallel L1 警告）
- `src/Cosmos.EffectAlgebra/Objects.cs` 全文（ResourceId.Normalize、ScopeId 8 构造子与 IncludedIn、Claim.Normalize、Signature.Union/Join 与 Join key、GetHashCode）
- `src/Cosmos.EffectAlgebra/Algebra.cs` 全文（Compatible.IsCompatible、Weight、NetTable.Compute、Peak.Compute）
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` 全文（ParseEvent/ParseClaim/ParseScope/ParseBudget、SerializeEvent/SerializeScope/SerializeClaim/SerializeResource/ResourceKey、Budget 往返）
- `src/Cosmos.EffectAlgebra/Numeric.cs` 1-120 行（NatStar Interval）
- `audit/rich-hickey-round10-synthesis.md` 全文（锁死项与 6 条 HIGH 根因）
- `audit/rich-hickey2-round01-serialize.md`（R1 序列化与双真相修复面）
- `audit/rich-hickey2-round02-numeric.md`（R2 数值边界与 Peak 跨桶隔离）
- `audit/rich-hickey2-round03-values.md`（R3 Budget 值语义三修）
- `audit/rich-hickey2-round04-errorsym.md`（R4 异常对称与 Budget 接线）
- `audit/rich-hickey2-round05-naming.md`（R5 L1 警告与 IsValid 派生）
- `EFFECT_SCRIPT.md` 全文（§4 契约要点与事件/claim scope 形状）
- 动态探针：`/tmp/cosmos_scope` 三轮（`/tmp/cosmos_scope_probe.cs` / `/tmp/cosmos_scope2.cs` / `/tmp/cosmos_scope3.cs`，`dotnet run --project /tmp/cosmos_scope/cosmos_scope.csproj`，.NET 10.0.103），输出见报告内摘录

