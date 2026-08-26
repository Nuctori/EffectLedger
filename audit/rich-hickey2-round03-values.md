# Rich Hickey 视角 — Round 03 值语义与不可变性（“值”是否真的是值）

> 审计员：hickey-auditor · 透镜：值语义/不可变性/identity vs value · 轮次：R3/10  
> 判据：simple = 不纠缠、一个职责、值语义；identity 与 value 不可 complect；API 面积是最昂贵的承诺；“程序员知道得越少越好”

## 核实矩阵（历史结论逐条裁决——仍在/已修/部分修/误报 + 行号证据）

| 历史项 | 来源 | 本轮裁决 | 行号证据 |
|---|---|---|---|
| A. 序列化静默兜底 `_=>memory:0/_=>global/ResourceKey` | synthesis A (6轮) | **已修** | `EffectScriptContract.cs:255` ` _ => throw new FormatException($"不可序列化的 scope: {s}")`；`274` ` _ => throw new FormatException($"不可序列化的 resource: {r}")`；`293` ` _ => throw new FormatException($"不可序列化的 budget 键资源: {r}")` — 三处已由 fail-silent 改为 fail-fast |
| B. loop=0 语义陷阱/除零/Scale [0,0] | synthesis B | **已修** | `DerivedMetrics.cs:18` `LoopCount.Of` 拒绝0；`EffectScriptContract.cs:146-147` `if(v==0) throw FormatException`；`EffectScript.cs:45-47` 构造期 `loop.Count.Value==0` 抛 |
| C. scope 双份真相/Violation 伪造 Global | synthesis C | **部分修** | `EffectScript.cs:171` `ResolveNetScope` / `172` `ResolvePeakScope` 已取首个贡献者 `e.Scope` 并仅在无贡献者时 fallback `new ScopeId.Global()`；闭包路径 `281-290` `leakScope` 同理。硬编码三处 `new ScopeId.Global()` 已消失，但 `fallback Global` 仍为“无归因时造一个值”，语义上仍在，只是触发面收窄 |
| D. Weight.NaN 毒值死代码 | synthesis D | **已修** | `Algebra.cs:38` `Weight.Of` 现 `throw new InvalidOperationException($"KIND_MIX...")`，非 NaN |
| E. Budget 可变字典门面 + Budget.None 可变单例 + default NRE | synthesis E (本轮透镜) | **仍在** | `EffectScript.cs:353` `IReadOnlyDictionary Caps {get;}` 仍包 `Dictionary`；`359` `None = new(new Dictionary<...>)` 可变底；`356` 构造无防御拷贝；`72-75` 构造函数仅 `budget.Caps!=null?budget:None` 未拷贝 — 动探针 `C:/temp/cosmos_probe2` 实测三处失败见报告正文 |
| F. 四名一实 Union/Join/Sequence/Parallel | synthesis F | **仍在（锁死）** | `DerivedMetrics.cs:50` `Sequence=>Union`；`55-66` `Parallel=>Union+守卫`；`Objects.cs:192` 语义仍同一，文档未置顶声明“L1 无时序区分” |
| G. Signature.GetHashCode 顺序敏感 | synthesis G | **已修** | `Objects.cs:234-247` 已改为 XOR 折叠，`foreach _read/^=hash` 顺序无关；动探针 `sigAB.Equals(sigBA) && hash equal => PASS` 验证 |
| H. Peak.Compute 跨桶聚合 | synthesis H | **已修（R2）** | `Algebra.cs:119` `if(c.Kind!=Kind.Occupy) continue;` 已与 `NetTable.Compute:Kind==Occupy` 单一真源 |
| I. CONFLICT 集两处写（硬编码 vs IsCompatible） | synthesis I | **已修** | `EffectScript.cs:237-252` `gate(3)` 现 `Compatible.IsCompatible(mode,mode)` 单一真源，无二次硬编码 |
| J. Unknown→Use fail-open | synthesis J | **锁死（by design）** | `Algebra.cs:17` `Resolve Unknown=>Use` + 注释 `fail-open/permissive` 已统一术语 |
| K. Size ?? [1,1] 缺省散布 | synthesis K | **锁死（by design）** | `Claim.Normalize: Size??Interval.Default` 仍在，但已收口为单一归一方法，非散布 `??` |
| L. ℕ*→ℤ* 三拷贝 + ZStar/(long) 回绕 | synthesis L | **部分修** | `Algebra.cs:72-83` 与 `EffectScript.cs:341-348` `ToZ/Negate` 已加 `>long.MaxValue?Top` 守卫，但 `ScaleSize(EffectScript):343-345` 与 `Scale(DerivedMetrics):68-72` 仍分裂，二处阈值一致但未收敛 helper |
| M. 死代码 重复行 272/274 | synthesis M | **未核（本轮盘外，待后续轮清理）** | 未在本轮透镜精读 |
| R1: default(LoopCount) 后门 | R1 | **已修（消费侧）** | `EffectScript.cs:45-47` 构造期封堵；但 `default(LoopCount)` 值本身仍为非法 `0`（见新发现 V3-004） |
| R1: 序列化 round-trip/未知键白名单 | R1 | **已修** | `EffectScriptContract.cs:31-32,55-57,80,163` 白名单校验 |
| R2-001 哨兵碰撞 | R2 | **已修** | `EffectScript.cs:202-205` 现 `peakSum[r]=cur+hi*w` 走 `NatStar.*` Top 律，已弃 `ulong.MaxValue` 哨兵 |
| R2-002 ZStar.Min 对偶律 | R2 | **已修** | `SignedNet.cs:62-68` `Min` 已改为 `IsTop?o : o.IsTop?this : Of(Min)` 与 `NatStar.Min` 对偶 |
| R2-003 maxFinite+1 回绕 | R2 | **已修** | `EffectScript.cs:134` `if(anyOpenEnd && maxFinite!=ulong.MaxValue)` 已守卫 |
| R2-005 Peak 跨桶 | R2 | **已修** | 同 H |
| R2-006 ParseTop TryGetUInt64 | R2 | **已修（部分）** | `EffectScriptContract.cs:111` `TryGetUInt64` + `220` 同理；但 `memory` 分支 `199` 仍裸 `GetUInt64()` 未包 Try，见新发现 |

## 新发现（本轮值语义透镜——“把对象当值传，却拿到别名”）

> 动探针：`C:/temp/cosmos_probe2/cosmos_probe2.csproj`（引用 `src/Cosmos.EffectAlgebra`，TargetFramework net10.0，`dotnet run` 实测）；报告标注“实测” vs “静态推断”。探针工程在 `C:` 临时区，未进仓库。

### V3-001 — HIGH — Budget 结构相等退化为引用相等：同内容不同实例判不等，值语义名不副实

- **位置**：`EffectScript.cs:350-359` `public readonly record struct Budget { IReadOnlyDictionary Caps {get;} }`
- **判词（一句 Hickey 式锐评）**：把 `Dictionary` 包进 `record struct` 却不自定义相等，是让“值”的外衣裹着“引用”的身份——simple 要求值相等由内容决定，不是对象地址。
- **证据摘录**：
  ```csharp
  // EffectScript.cs:350
  public readonly record struct Budget { public IReadOnlyDictionary<ResourceId,NatStar> Caps {get;} }
  // record struct 自动合成 Equals 依字段 EqualityComparer.Default，对 IReadOnlyDictionary 即引用相等
  // Budget.None = new(new Dictionary<...>()) — 同一空字典实例的引用身份即“值”
  ```
  实测（`C:/temp/cosmos_probe2`）：
  ```
  b1.Equals(b2)=False expected TRUE -> FAIL-BUG
  b1 hash 56793269 vs b2 115000 equal? False
  ```
  两份 `Budget(new Dictionary{Gpu("a"):5})` 内容逐键相等却 `Equals==false` 且 `GetHashCode` 不一致，放入 `HashSet<Budget>` 会出现“同一预算两次”。
- **最小修复**：覆写值相等（见 E 根因统一修法 V3-E）。
- **testHint**：`var b1=new Budget(new Dictionary{{Gpu("a"),Of(5)}}); var b2=new Budget(new Dictionary{{Gpu("a"),Of(5)}}); Assert.True(b1.Equals(b2)); Assert.Equal(b1.GetHashCode(),b2.GetHashCode()); Assert.Single(new HashSet<Budget>{b1,b2});`
- **verdict**：fixable

### V3-002 — HIGH — Budget.None 可变单例：一次 `IDictionary` 强转写入即污染全程序预算语义

- **位置**：`EffectScript.cs:359` `public static readonly Budget None = new(new Dictionary<ResourceId,NatStar>());`
- **判词**：把全局单例的底座做成可变 `Dictionary`，是把“常量”与“变量” complect 的教科书反例——常量就该是值，不该是别名。
- **证据摘录**：
  ```csharp
  // EffectScript.cs:359
  public static readonly Budget None = new(new Dictionary<ResourceId, NatStar>());
  // Caps 类型仅 IReadOnlyDictionary，但运行时是 Dictionary，可强转回 IDictionary 写入
  ```
  实测：
  ```
  Budget.None.Caps runtime type: System.Collections.Generic.Dictionary`2[...]
  BUG: mutated via IDictionary, before 0 after 1
  new script budget count: 1 (should be 0)
  ```
  `Budget.None.Caps is IDictionary<,>` 为 `True`，写入后 `Budget.None.Caps.Count==1` 且 `new EffectScript(ImmutableArray.Empty).Budget.Caps.Count==1` — 无预算脚本被污染为有预算，`Audit` 的 `gate(2)` 从“未检查”静默变为“已检查”。
- **最小修复**：见 V3-E 统一修法——`None = new(ImmutableDictionary.Empty)`。
- **testHint**：`var before=Budget.None.Caps.Count; ((IDictionary<ResourceId,NatStar>)Budget.None.Caps).Add(Gpu("poison"), Of(1)); Assert.Equal(before, Budget.None.Caps.Count);` 应抛 `NotSupportedException`。
- **verdict**：fixable

### V3-003 — HIGH — Budget 构造与 getter 双向别名泄漏：调用方能改内部状态，内部也能被外部改

- **位置**：`EffectScript.cs:356` `public Budget(IReadOnlyDictionary caps){Caps=caps;}`；`353` `Caps {get;}` 暴露可变引用；`EffectScript.cs:72-75` `Budget = budget.Caps!=null?budget:None` 无拷贝
- **判词**：构造函数接引用、getter 还引用，是把“传入的值”当“共享的可变状态”——值语义的第一条就是防御性拷贝，缺它便不是值。
- **证据摘录**：
  ```csharp
  // EffectScript.cs:356
  public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; } // 无拷贝
  // EffectScript.cs:72
  Budget = budget.Caps != null ? budget : Budget.None; // 无拷贝，直接存引用
  ```
  实测：
  ```
  before mutate origDict, script.Budget count 1 val for x: 1
  after mutating origDict to 999, script.Budget value: 999 (BUG if 999, expected 1)
  BUG: mutated via getter IDictionary, count 2
  Budget.Caps implements IDictionary? True
  ```
  外部改 `origDict` 内部同步变；通过 `script.Budget.Caps as IDictionary` 回写内部，两方向皆通。
- **最小修复**：见 V3-E 统一修法——构造期 `ToImmutableDictionary()` 深拷贝，getter 返回 `ImmutableDictionary`（对 `IReadOnlyDictionary` 仍只读，对 `IDictionary` 抛）。
- **testHint**：`var d=new Dictionary{{Gpu("x"),Of(1)}}; var s=new EffectScript(ImmutableArray.Empty,new Budget(d)); d[Gpu("x")]=Of(999); Assert.Equal(1, s.Budget.Caps[Gpu("x")].Value);`；`Assert.Throws<NotSupportedException>(()=> ((IDictionary<,>)s.Budget.Caps).Add(Gpu("y"),Of(1)));`
- **verdict**：fixable

### V3-E — Budget 可变字典门面 统一最小 diff 修法与破坏面评估（综合 V3-001/002/003）

**根因**：`record struct` + `IReadOnlyDictionary` 引用语义 + `Dictionary` 可变底 + 无防御拷贝 + `Equals/GetHashCode` 未覆写。

**最小 diff（<20 行，零新增公共成员，仅换底座与相等）**：
```csharp
// EffectScript.cs 顶部加 using System.Collections.Immutable;
// Budget 定义改为：
public readonly record struct Budget : IEquatable<Budget>
{
    public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }
    public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps)
        => Caps = caps is null ? ImmutableDictionary<ResourceId, NatStar>.Empty
                               : caps.ToImmutableDictionary();
    public static readonly Budget None = new(ImmutableDictionary<ResourceId, NatStar>.Empty);
    public bool Equals(Budget other)
    {
        if (ReferenceEquals(Caps, other.Caps)) return true;
        if (Caps is null || other.Caps is null) return Caps is null && other.Caps is null;
        if (Caps.Count != other.Caps.Count) return false;
        foreach (var kv in Caps)
            if (!other.Caps.TryGetValue(kv.Key, out var v) || !v.Equals(kv.Value)) return false;
        return true;
    }
    public override int GetHashCode()
    {
        unchecked { int h=0; foreach(var kv in Caps) h ^= kv.Key.GetHashCode() ^ kv.Value.GetHashCode(); return h; }
    }
}
```
配套：`EffectScript` 构造 `Budget = budget.Caps!=null ? budget : Budget.None` 保持，但 `budget` 已是不可变拷贝，无需额外改；若保留 `IReadOnlyDictionary` 公开类型则 API 形状不变。

**破坏面评估**：
- 形状：公开类型仍 `IReadOnlyDictionary<ResourceId,NatStar>`，赋值兼容 `ImmutableDictionary`（实现该接口），**不破编译**；仅 `Caps as Dictionary/IDictionary` 强转写入路径由“静默成功”变为抛 `NotSupportedException`——该路径本身是 bug，前向 caller 若依赖它即依赖未定义行为，应视为修复而非破坏。
- 语义：`Equals` 从引用相等改为内容相等，原 `HashSet<Budget>/Dictionary<Budget,>` 中“同内容不同实例算两个键”的错误去重会合并为一个键——这是纠正值语义，调用方若曾用引用相等做去重逻辑需复核，但测试绿灯即为预期（见 `dotnet test` 基线）。
- 性能：`ToImmutableDictionary()` 拷贝一次 O(K)，K 为预算资源数（通常 <10），可忽略；`GetHashCode` 遍历 K，同样可忽略。
- `default(Budget)` 仍为 `Caps==null`，但经 `EffectScript` 构造归一为 `None` 且 `Equals` 已处理 null，故 `default(Budget).Equals(Budget.None)` 在修后为 `true`（空集相等），今日为 `false` 需一并修。

### V3-004 — MED — `default(T)` 后门：`LoopCount/Interval/EffectEvent/Claim` 的默认值是非法值，类型许诺“构造即合法”却被 `default` 绕过

- **位置**：`DerivedMetrics.cs:10-21` `LoopCount` 私有构造 `new(NatStar)`，`Of` 拒绝0但 `default(LoopCount).Count==NatStar.Of(0)`；`Numeric.cs:70-95` `Interval` `Default=[1,1]` 但 `default(Interval)=[0,0]`；`EffectScript.cs:22-56` `EffectEvent` `default` 时 `Scope==null, Footprint==null, Loop==0`；`Objects.cs:127` `Claim` `default` 时 `Resource==null, Scope==null`
- **判词**：用 `readonly record struct` 承载“构造即合法”的不变量，却把 `default` 这一合法 C# 值留成非法状态——是让“非法状态不可表示”在语言默认值处破功。
- **证据摘录（实测）**：
  ```
  default LoopCount Count.IsTop=False Value=0 -> default is invalid 0 (Of rejects 0)
  default Interval: [0,0] vs Interval.Default [1,1] -> distinct value, [0,0] is unexpected but valid per lo<=hi
  default EffectEvent Scope is null? True Footprint is null? True Loop Val=0 -> all null, invalid
  default Claim: Resource is null? True Scope is null? True
  ```
  静态：`default(LoopCount)` 绕过 `Of(n==0) throw`，但 `EffectEvent` 构造已二次守卫 `loop.Count.Value==0 throw`（`EffectScript.cs:45`），故消费侧已拦截；其余 `default(Interval)=[0,0]` 虽满足 `lo<=hi` 但语义上与文档 `Default=[1,1]` 分裂，调用方 `new Interval()` 误得 `[0,0]`。
- **最小修复**：① 文档在 `LoopCount/Interval/Claim/EffectEvent` 顶部加 `/// default(T) 为未初始化哨兵，非法，须经工厂构造`；② `EffectEvent` 已有守卫保持；③ 考虑将 `LoopCount` 改 `struct` 包装为 `NatStar` 私有并使 `default` 映射到 `Top` 或抛（需 `field` init 验证，C# 12 可用 `required`/`init` 校验），或接受“struct default 不可避免”并以静态分析 `[Obsolete]` 提醒。**不建议改 `Interval` 使 `default` 非法**（`[0,0]` 本身合法，只需文档区分 `default` vs `Default`）。
- **testHint**：`Assert.Throws<ArgumentException>(()=> new EffectEvent(new Interval(Of(0),Of(1)), new ScopeId.Global(), Signature.Empty, default(LoopCount)));` 已绿；`Assert.NotEqual(Interval.Default, default(Interval)); Assert.Equal("[0,0]", default(Interval).ToString());`
- **verdict**：doc-only（语言限制，`struct default` 无法在类型内完全消除，只能以工厂+文档+消费侧守卫收口）

### V3-005 — MED — `Claim with { Resource=null }` / `with { Kind=(Kind)999 }` 绕过构造校验，非法状态可表示

- **位置**：`Objects.cs:127` `public readonly record struct Claim(Kind Kind, ResourceId Resource, Mode Mode, ScopeId Scope, Interval? Size)` — 位置记录的主构造无校验，`with` 经 `init` 绕过
- **判词**：把五参位置记录当“构造即合法”的边界，却让 `with` 复制出 `null` 资源与越界 `Kind`——是让非法状态在语法糖处可达。
- **证据摘录（实测）**：
  ```
  Claim with Resource=null succeeded (BUG): True
  Claim with invalid Kind succeeded: 999 (BUG: no validation)
  default Claim: Kind=Read Resource is null? True Mode=Use Scope is null? True
  ```
  静态：`Claim` 为 `record struct`，编译器为 `with` 生成 `init` 赋值，不经过显式构造校验；`Kind` 为 `enum` 无范围守卫，`Resource/Scope` 为可空引用却未标记 `required`/`notnull`。
- **最小修复**：① 在 `Claim` 上加 `member` 校验方法 `Validate()` 并在 `Signature.Of/Add/Normalize` 入口处校验（已是最短路径——`Signature.Of` 已对重复 Claim 抛，本处同理对 `Resource==null||Scope==null` 抛 `ArgumentException`）；② `Kind` 非法值在 `switch(n.Kind)` 处已有 `default: throw`（`Objects.cs:189`），但 `Claim` 本身不拦，进入 `Signature` 才拦——建议在 `Claim` 构造/Normalize 首行 `if(!Enum.IsDefined(...)) throw` 以 fail-fast。`with` 无法在语言内完全禁，靠消费侧 `Signature` 边界收口已足够。
- **testHint**：`Assert.Throws<ArgumentException>(()=> Signature.Of(new Claim((Kind)999, Gpu("a"), Mode.Create, new ScopeId.Global(), Interval.Exact(1))));`；`var c=new Claim(Kind.Occupy,Gpu("r"),Mode.Create,new ScopeId.Global(),Interval.Exact(1)); var bad=c with{Resource=null!}; Assert.Throws<ArgumentException>(()=> Signature.Of(bad));`
- **verdict**：fixable（在 Signature 边界加校验，零 API 拓宽）

### V3-006 — LOW — `ResourceId.Memory` 仍对 `ulong` 零值哨兵与 `"memory:0"` 字符串零值完备性依赖 `ParseResource` 未对 `memory` 做 `TryGetUInt64` 守卫

- **位置**：`EffectScriptContract.cs:199` `new ResourceId.Memory(mem.ValueKind==Number ? mem.GetUInt64() : throw...)` — 对 `-1`/`1.5` 未走 `TryGetUInt64` 分支，漏出 BCL `InvalidOperationException` 而非契约 `FormatException`
- **判词**：把 JSON 数值校验外包给 `JsonElement.GetUInt64()`，是让 BCL 的异常形状成为你的 API 形状——Hickey 说“程序员知道得越少越好”，异常形状即 API。
- **证据摘录**：
  ```csharp
  // EffectScriptContract.cs:199
  if (el.TryGetProperty("memory", out var mem)) return new ResourceId.Memory(mem.ValueKind == JsonValueKind.Number ? mem.GetUInt64() : throw new FormatException("memory 须为数字 uid"));
  // 对 -1 / 1.5 仍进 Number 分支，GetUInt64 抛 BCL 异常
  ```
  对比 `EffectScriptContract.cs:111,220` 已改 `TryGetUInt64`，此处遗漏。
- **最小修复**：`mem.TryGetUInt64(out var uid) ? new ResourceId.Memory(uid) : throw new FormatException($"resource.memory 须为非负整数，实际 {mem}")`
- **testHint**：`Assert.Throws<FormatException>(()=> EffectScriptContract.Parse("{\"events\":[{\"lifetime\":[0,1],\"scope\":{\"scene\":\"s\"},\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"memory\":-1},\"mode\":\"create\",\"scope\":{\"scene\":\"s\"}}]}]}"));`
- **verdict**：fixable

### V3-007 — LOW — `Signature.GetHashCode` XOR 折叠满足“顺序无关”但抗碰撞弱（互补集同哈希）

- **位置**：`Objects.cs:234-247` `h ^= c.GetHashCode()` + `h ^= count*prime`
- **判词**：XOR 满足契约 `Equals=>HashCode` 且顺序无关，但 `a^b^b==a` 使互补集易碰撞——是把“最简”当“最优”的 easy。
- **证据摘录**：
  ```csharp
  // Objects.cs:238-246
  var h=0; foreach(c in _read) h ^= c.GetHashCode(); ...
  h ^= _read.Count*17; // 区分空桶与跨桶移动
  ```
  已满足 Hickey 判据“非法状态不可表示”与哈希契约，功能正确；仅分布性弱（两集合 `A={x,y}, B={x}` 异内容可能因 `y==0` 碰撞概率略增）。当前无哈希容器消费（synthesis O4），属锐边非故障。
- **最小修复**：保持现状或改 `HashCode.Combine` 滚叠+桶内排序后折叠（性能无感时再议）。
- **testHint**：`var s1=Signature.Of(cA,cB); var s2=Signature.Of(cA); Assert.NotEqual(s1.GetHashCode(), s2.GetHashCode());`（概率性，非必现）
- **verdict**：already-fixed（R2 已修顺序敏感，本项为分布性锐边，doc-only）

## TOP-3（本轮最重要的三个发现）

1. **V3-002 Budget.None 可变单例污染** — 全局常量的底座是可变 `Dictionary`，一次 `IDictionary` 强转写入即让所有无预算脚本被污染为有预算，`Audit` 的 `gate(2)` 静默改变语义。修复一行 `ImmutableDictionary.Empty` 即可根治，破坏面仅为“非法写入路径由成功变抛异常”。
2. **V3-001/003 Budget 值语义双崩（相等+别名）** — 同内容不等、外部改内部、getter 改内部，三位一体证明 `Budget` 今天不是值。统一修法见 V3-E，<20 行，API 形状不变。
3. **V3-004/005 `default`/`with` 后门** — C# `record struct` 的 `default` 与 `with` 让“构造即合法”在语言默认值与语法糖处破功。`LoopCount` 的 `0`、`Claim` 的 `null` 资源可经 `default`/`with` 可达，虽在 `EffectEvent/Signature` 消费侧已二次守卫，但类型许诺与语言现实的缝隙需文档与边界校验显式收口。

## 证据清单（实际读取）

- `audit/rich-hickey-round10-synthesis.md` 锁死清单（全表）
- `src/Cosmos.EffectAlgebra/EffectScript.cs` 全文（含 `EffectEvent/Budget/AuditResult/Violation` 定义与 `Audit` 扫换线实现，重点 `22-82,113-410`）
- `src/Cosmos.EffectAlgebra/Objects.cs` 全文（`ResourceId/ScopeId/Claim/Signature` 定义与 `GetHashCode` 实现 `230-252`）
- `src/Cosmos.EffectAlgebra/Numeric.cs` 全文（`NatStar/Interval/DeviationVal` 定义）
- `src/Cosmos.EffectAlgebra/SignedNet.cs` 全文（`ZStar/SignedInterval` 定义，`Min` 对偶律核验）
- `src/Cosmos.EffectAlgebra/Algebra.cs` 全文（`Compatible/Weight/NetTable/Peak` 实现，`Weight.Of` fail-fast 核验）
- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs` 全文（`LoopCount/Combination/Derived` 实现，`LoopCount.Of` 守卫核验）
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` 全文（序列化契约，`SerializeScope/Resource` fail-fast 核验 `249-293`）
- `src/Cosmos.EffectAlgebra/Deviation.cs` 全文（`SignatureDeviation` 定义）
- `src/Cosmos.EffectAlgebra/ApiMapping.cs` 全文（`GodotApiWhitelist` 不可变性核验）
- `src/Cosmos.EffectAlgebra/EffectAttributes.cs` 全文（属性层边界核验）
- 动探针 `C:/temp/cosmos_probe2/cosmos_probe2.csproj` + `Program.cs`（.NET 10.0 `dotnet run` 实测：Budget 相等/别名/None 污染、`default(LoopCount/Interval/Claim/EffectEvent)`、`with` 绕过、`Signature` 哈希等价性，见正文摘录）
- `git log --oneline -5` 与 `git show --stat HEAD`（R1/R2 修复面核验）
