# Effect Cost Algebra — 落地计划（一次性完美交付）

> 来源：PDR_Effect_Cost_Algebra_v3_FINAL.md（v3.0-FINAL-rA6，已收敛，iter54 终止判定=可终止）
> 质量铁律（用户原话）：**每一个符号都要有明确的数学边界定义 + 明确语义；类型系统能约束的用类型，类型系统约束不了的写在注释上。** 不留技术债。

---

## 1. 架构（由 PDR §14 L1/L2/L3 + §6 执行模型决定，非推测）

三层 + 一个纯代数核心。**L1 不依赖 Godot**，是可被形式化校验的纯库；L2/L3 只做「Godot 语法 → L1 类型」的薄翻译。

```
Cosmos.EffectAlgebra/          L1 纯代数核心（零 Godot 依赖，可独立单测/性质测试）
Cosmos.EffectAlgebra.Generator/ L2 Source Generator（在 §7 白名单调用点注入 EffectAudit 包装）
Cosmos.EffectAlgebra.Analyzer/  L3 Roslyn Analyzer（AUDIT001-003 / KIND_MIX / Compat 冲突 / Unknown 人工确认）
Cosmos.EffectAlgebra.Tests/     xUnit：§14 测试矩阵 + 代数定律测试 + 性质测试
```

**为什么 L1 必须零 Godot 依赖**：它是「数学边界」的唯一真相源。Godot 只是 §7 映射表的输入之一；把映射与代数分离，代数层可被性质测试穷举证明，Godot 侧只是数据。

---

## 2. 类型 vs 注释 的硬性分工约定

| 机制 | 承载什么 | 例子 |
| --- | --- | --- |
| **类型（record/enum/struct + 运算符）** | 构造子标签、字段类型、结构相等、⊤ 闭包算术、偏序方法、Compatible 真值表 | `NatStar` 运算符内嵌 §3.1.5a 律；`ResourceId` 判别联合保证「构造子标签+字段」 |
| **注释（/// doc + 行注）** | 类型表达不了的不变式：归一化前置条件、控制流敏感性近似、fail-closed 理由、§ 出处、WHY | `Claim` 的「集合运算须用 Normalize() 后键」；`Signature` 跨桶聚合须 weight 否则 KIND_MIX |

**规则**：每个公共类型的 `///` 注释必须含 `(§x.y)` 出处 + 一句不变式；每个运算符注释必须声明它落实了哪条 § 定律。注释不是装饰，是「类型约束不到的数学边界」的载体。

---

## 3. 逐符号类型设计（数学边界 → 类型 + 注释残留）

### 3.1 ℕ* 扩展自然数（§3.1.5a）

```csharp
// §3.1.5a — ℕ* = ℕ ∪ {⊤}，⊤ 为上界标记（非 IEEE ∞，不崩溃）。
// 类型内嵌全部 ⊤ 律（MA-002 闭包）；注释标注 0×⊤=⊤ 的保守理由。
public readonly record struct NatStar {
    public bool IsTop { get; }
    public ulong Value { get; }            // 仅当 !IsTop 有效
    public static readonly NatStar Top = new(true, 0);
    public static NatStar Of(ulong v) => new(false, v);
    // §3.1.5a 律（运算符内嵌，编译器强制）：
    public static NatStar operator +(NatStar a, NatStar b) => (a.IsTop||b.IsTop) ? Top : Of(a.Value+b.Value);
    public static NatStar operator *(NatStar a, NatStar b) => (a.IsTop||b.IsTop) ? Top : Of(a.Value*b.Value); // 0×⊤=⊤ 保守标记未知
    public NatStar Max(NatStar o) => (IsTop||o.IsTop) ? Top : Of(Math.Max(Value,o.Value));
    public NatStar Min(NatStar o) => (IsTop||o.IsTop) ? (IsTop ? o : this) : Of(Math.Min(Value,o.Value));
    // compare: ∀x, x<Top; Top==Top; 无 x>Top（注释，非类型）
}
```

### 3.2 SizeVal = Interval（§3.1.5 / §3.1.5b）

```csharp
// §3.1.5b — [lo,hi]，lo≤hi；单值 s⇔[s,s]；缺省 [1,1]；动态 [1,⊤]（ED-004）。
// merge_I 用 Min/Max，已内嵌 §3.1.5a ⊤ 律（§3.1.5b 注）。
public readonly record struct Interval {
    public NatStar Lo { get; } public NatStar Hi { get; }
    public static readonly Interval Default = new(NatStar.Of(1), NatStar.Of(1));   // 缺省 size
    public static readonly Interval Dynamic = new(NatStar.Of(1), NatStar.Top);     // 动态 Instantiate
    public Interval Merge(Interval o) => new(Lo.Min(o.Lo), Hi.Max(o.Hi));           // join-semilattice
    // 结构相等即 [a,b]=[c,d] ⇔ a=c∧b=d（record 自动）
}
```

### 3.3 ResourceId 判别联合（§3.1.2 / §3.1.2b）

```csharp
// §3.1.2+§3.1.2b — 判别联合：构造子标签 + 字段逐位相等（record 结构相等直接给）。
// 类型保证「标签完整性」；字段类型即边界。
public abstract record ResourceId;
public sealed record Tree(NodePathOrUnknown Path) : ResourceId;
public sealed record Self(string Component) : ResourceId;
public sealed record Physics(Rid BodyId) : ResourceId;
public sealed record Memory(ulong Uid) : ResourceId;          // §7 裸 'memory' ⇒ Memory(uid="mem")
public sealed record Disk(string Path) : ResourceId;
public sealed record Signal(StringName Name) : ResourceId;
public sealed record Gpu(Rid BufferId) : ResourceId;
public sealed record AudioMixer(int ChannelId) : ResourceId;
public sealed record Occupancy(string Channel) : ResourceId;  // audio_channel / animation_state
public sealed record Callback(string Id) : ResourceId;
public sealed record Network(int PeerId, string Method) : ResourceId;
public sealed record Input(string Action) : ResourceId;
public sealed record Custom(string Name) : ResourceId;
public sealed record CommandBuffer(string Channel) : ResourceId;  // "gpu"
public sealed record SignalBus(StringName Name) : ResourceId;
```

### 3.4 ResourceId.Normalize（§3.1.4a 归一化，注释承载）

```csharp
// §3.1.4a — 归一化是函数，非结构相等（类型给不了）。Two Claims 相等 ⇔ Normalize() 后相等。
// 映射表（与 §7 白名单裸名一一对应）：
//   signal_bus / "signal_"+s / Self("signal_"+s) ⇒ SignalBus(s)
//   gpu / command_buffer ⇒ CommandBuffer("gpu")
//   memory⇒Memory("mem") disk⇒Disk(p) physics⇒Physics(b) audio_mixer⇒AudioMixer(c)
//   audio_channel⇒Occupancy("audio") animation_state⇒Occupancy("animation") callback⇒Callback("cb")
//   network⇒Network(...) input⇒Input(a) self⇒Self(c) tree⇒Tree(p)
public static ResourceId Normalize(ResourceId r) => /* 上述 switch */;
// 注释：∪ / net / Deviation 分组一律用 Normalize 后的键；Unknown 与已知不等、与另一 Unknown 相等。
```

### 3.5 ScopeId + ⊆* 偏序（§3.1.3 / §3.1.3b）

```csharp
// §3.1.3b — 偏序 ⊆* = (a ⊑ b) ∨ (b == Global)。方法内嵌查表（注释引 §3.1.3b 表）。
public abstract record ScopeId;
public sealed record Method(string Name), Type(string Name), Scene(string Name) : ScopeId;
public sealed record Global, Shell : ScopeId;
public sealed record Loop(string Id), Conditional(string Branch), Async(string Id) : ScopeId;
public bool IncludedIn(ScopeId other) =>
    this.Equals(other) || other is Global
    || (this, other) switch { (Method a, Method b) => a==b, /* 同标签同字段其余类似 */ _ => false };
// 注释：跨标签（Method(m) vs Scene(s), m≠s）不可比较 ⇒ 既不满足 ⊑ 也不满足 ⊆*（§3.1.3b）
```

### 3.6 Mode / Kind + Compatible 全函数（§3.2.3）

```csharp
// §3.2.3 — Compatible 为 16 对全函数 + 对称；CONFLICT={(C,C),(M,M),(R,R)}。
public enum Mode { Use, Create, Release, Move, Unknown }   // Unknown 按 Use 处理（fail-closed）
public enum Kind { Read, Write, Occupy }
public static bool Compatible(Mode a, Mode b) {
    var aa = a == Mode.Unknown ? Mode.Use : a;   // §3.2.3 P4
    var bb = b == Mode.Unknown ? Mode.Use : b;
    if (aa == Mode.Use || bb == Mode.Use) return true;          // use 最弱
    return (aa, bb) is not (Mode.Create, Mode.Create)
                    and not (Mode.Move, Mode.Move)
                    and not (Mode.Release, Mode.Release);        // CONFLICT 集
}
// 性质（注释 + 定律测试证明）：对称、全函数（无未覆盖对）、create+release 不误判。
```

### 3.7 Claim（§3.1.1 + §3.1.4a）

```csharp
// §3.1.1 — (kind, resource, mode, scope, size?)。record 给结构相等，但 resource 须归一。
public readonly record struct Claim(Kind Kind, ResourceId Resource, Mode Mode, ScopeId Scope, Interval Size) {
    public Claim Normalize() => this with { Resource = ResourceId.Normalize(Resource),
                                            Size = Size == default ? Interval.Default : Size };
    // 注释：集合运算（∪ / net 分组）必须用 Normalize() 后的键；否则同资源多 Claim 不被合并（§3.1.4a 后果）。
}
```

### 3.8 Signature（§3.1.4b 量纲分桶 + §3.3 net/Peak）

```csharp
// §3.1.4b — 按 kind 分三不相交桶；跨桶聚合须显式 weight，否则 KIND_MIX（L3 诊断）。
// 类型暴露三桶访问器；cross-kind 求和的类型层护栏由 Analyzer 补（运行时集无法静态知 kind 混用）。
public sealed class Signature {
    private readonly ImmutableHashSet<Claim> _read, _write, _occupy;
    // §3.2.1/§3.2.2 ∪：按 Claim.Normalize() 去重（幂等由结构相等保证，§3.1.4a）
    public static Signature Union(Signature a, Signature b) => /* 三桶各自 merge，去重 */;
    // §3.3.1 net(S,scope)：按资源分组，Σ 带符号 size（create/release 抵消），size 用 Interval.Merge
    public NetTable Net(ScopeId scope) => /* 仅含 ⊆* 过滤的 Claim */;
    // §3.3.2 Peak：size 求和（§3.2.5 旧 cardinality 形式已废弃）；ω=⊤ ⇒ 返回 ⊤
}
// weight: Kind×Kind→ℝ∪{⊥}（§3.3.2b）：类型定义为 partial 函数；⊥ 表示跨 kind 无定义 ⇒ KIND_MIX。
```

### 3.9 DeviationVal（§3.1.5c）

```csharp
// §3.1.5c — double ∪ {⊤}。先判 ⊤ 再比数值（§9.1 修正）。
public readonly record struct DeviationVal {
    public bool IsTop { get; } public double Value { get; }
    public static readonly DeviationVal Top = new(true, 0);
    public static DeviationVal Of(double v) => new(false, v);
    // 比较：仅当 !IsTop 时与阈值比；IsTop ⇒ 视为「需人工界定」不触发 0.2 报警（§9.1/§8.3.2）
}
```

### 3.10 属性：EffectOverride / AcceptDeviation（§8.3）

```csharp
// §8.3.1 — [EffectOverride(target, reason, scope?)]：编译期属性；可覆盖域受限（禁止覆盖 kind）。
// §8.3.2 — [AcceptDeviation(epsilon)]：epsilon∈[0,0.5]，越界 ⇒ 编译错误（上界约束，类型层可部分把守）。
[AttributeUsage(AttributeTargets.Method|AttributeTargets.Class)]
public sealed class EffectOverrideAttribute : Attribute { /* target, reason(非空), scope */ }
[AttributeUsage(AttributeTargets.Method)]
public sealed class AcceptDeviationAttribute : Attribute { public double Epsilon {get;} } // 构造子校验 [0,0.5]
```

### 3.11 §7 白名单 + release-class（数据，源码核实）

- 作为**强类型数据表**（`ImmutableArray<ApiMapping>`），每条 = (GodotApiName, Claim[]) 。
- release-class 集合（`queue_free, free, remove_child, disconnect, remove_from_group, cancel_free, free_children_in_group`）单独 `ImmutableHashSet<string>`，强制 emit release。
- 注释标注每条映射的 §7.x 出处 + 源码依据（node.cpp queue_free 递归释放 children）。

---

## 4. L2 / L3 薄翻译层（类型约束不到的部分）

| 不变式 | 落在哪 | 为什么类型给不了 |
| --- | --- | --- |
| 调用点注入 EffectAudit（DO-2 Domain 禁引 Godot） | L2 Generator + L3 Analyzer | 语法树分析，非类型 |
| AUDIT002 泄漏（Instantiate 后无 release 配对，控制流） | L3 Analyzer（流敏感近似） | 控制流无法纯类型表达 |
| KIND_MIX 跨 kind 聚合 | L3 Analyzer | 运行时集异质，静态未知 |
| Compat 冲突（create+create 同资源） | L3 Analyzer | 需跨调用点资源流 |
| Unknown 回落 fail-closed 人工确认 | L3 Analyzer 诊断 | 需人工判断，非类型 |

**L3 注释必须写明近似边界**：「控制流泄漏检测为保守近似——方法体内反射/动态调用不被分析（iter38 S2），此时 sound（不冤枉）但 incomplete，需 [EffectOverride] 兜底」。这正是「类型约束不了的写注释」的范例。

---

## 5. 验证策略（证明完美交付，而非声明）

1. **代数定律测试（xUnit）**：对 NatStar（⊤ 闭包、结合律、交换律）、Interval.Merge（幂等/交换/结合）、ScopeId.IncludedIn（自反/反对称/传递）、Compatible（对称/全函数/CONFLICT 精确）、Normalize（幂等）、Claim.Normalize（一致性）各写断言。
2. **性质测试（FsCheck/QuickCheck 风格）**：随机生成 NatStar/Claim，验证定律对所有输入成立（数学边界穷举证明）。
3. **§14 测试矩阵（真实 xUnit）**：iter54 列的正例/反例/⊤ 边界全部落成测试方法，绿 = L2/L3 完备性「已证」。
4. **稳定性审计**：代码落地后重跑一轮独立 subagent 审计，确认无 open（同 PDR 闭环流程）。

---

## 6. 分阶段（每阶段结束即自洽、可单测）

- **P0 脚手架**：solution + 4 工程，LangVersion=latest，Nullable=enable，TreatWarningsAsErrors。
- **P1 L1 代数核心**：§3.1–§3.3 全部类型（3.1–3.10）+ 定律/性质测试。**依赖根，先建**。
- **P2 §7 数据层**：白名单 + release-class 强类型表 + 裸名归一映射。
- **P3 L2 Generator**：注入包装 + DO-2 强制。
- **P4 L3 Analyzer**：AUDIT001-003 / KIND_MIX / Compat / Unknown 诊断 + 近似边界注释。
- **P5 §14 矩阵测试**：正/反/⊤ 用例全绿。
- **P6 稳定审计**：独立复审计，确认无 open。

---

## 7. 明确 out-of-scope / 残留风险（不藏）

- **D1 godot-csharp 真实工程落地**：需 Godot.NET.Sdk、实际 .tscn 解析、CI。PDR 已标注 out-of-scope，本计划给出类型骨架与测试，真实 SDK 集成单列。
- **控制流分析精度**：L3 为保守近似（sound/incomplete），注释已写明，非「假收敛」。
- **运行时 Deviation 采样**：§9.1 采样逻辑需真实引擎钩子，P1 仅给类型与公式，钩子接入留 D1。
