# Rich Hickey Round 02 — Value / Identity / State 审计（Cosmos.EffectAlgebra）

审计视角：Value vs Identity vs State（"值不做伪装，可变状态不藏起来"）。
范围：仅 7 个源文件，未读 `audit/`。行号以当前工作区文件为准。

## 总判定

代数载体层（Numeric/SignedNet/Objects 的 record 部分）是**真值**：readonly record struct、get-only 属性、构造即全必填、⊤ 闭环——这是本代码库最健康的地带。
但存在 **3 个高严重度问题**：`Budget.Caps` 可变字典泄漏进「不可变」EffectScript、`Budget.None` 全局单例指向可变字典、`Signature.GetHashCode` 违反 Equals/GetHashCode 契约。另有若干中低severity 的伪装值与引用身份陷阱。

---

## 一、真值清单（Correct：确认为真值的符号）

| 符号 | 位置 | 判定 |
|---|---|---|
| `Rid`, `StringName` | Objects.cs:10,13 | readonly record struct，真值 ✓ |
| `NodeIdPathOrUnknown` (`NodePathOrUnknown`) | Objects.cs:68-84 | get-only 属性 + 私有 ctor + 工厂；`Unknown` 为 readonly struct 单例，内容不可变 ✓ |
| `ResourceId` 及全部嵌套 sealed record | Objects.cs:20-66 | abstract record + sealed 构造子，结构相等 = 标签+字段，真值 ✓ |
| `ScopeId` 及全部嵌套 sealed record | Objects.cs:89-110 | 同上 ✓ |
| `Kind`, `Mode` enum | Objects.cs:114,121 | ✓ |
| `Claim` | Objects.cs:126-142 | readonly record struct 五元组；含引用类型字段（ResourceId/ScopeId）但均为 record ⇒ 结构相等传导正确 ✓ |
| `NatStar`, `Interval`, `DeviationVal` | Numeric.cs:8,70,111 | readonly record struct，ctor 校验 lo≤hi 不变量，静态单例（Default/Dynamic/Top）内容不可变 ✓ |
| `ZStar`, `SignedInterval` | SignedNet.cs:10,50 | 同上，ctor 校验 lo≤hi ✓ |
| `LoopCount` | DerivedMetrics.cs:10 | 私有 ctor + Of/Top 工厂，真值 ✓ |
| `EffectEvent` | EffectScript.cs:22 | readonly record struct 四字段；Footprint 是 class 但 Signature 已补结构相等 ⇒ 相等语义正确 ✓ |
| `AuditResult`, `Violation` | EffectScript.cs:324,337 | readonly record struct ✓ |
| `Compatible`, `Weight`, `Peak`, `Combination`, `Derived`, `EffectScriptContract`, `SignatureExtensions` | Algebra.cs:11-43/107-128, DerivedMetrics.cs:26-90, EffectScriptContract.cs:17, Algebra.cs:129 | 纯静态函数，无状态 ✓ |
| `ImmutableHashSet<Claim>` 三桶本体 | Objects.cs:147-149 内容 | 载体不可变 ✓（容器字段声明方式见 Note N1） |

---

## 二、逐符号问题判定

### HIGH

**H1 — Budget 是「readonly record struct 包着可变字典」的伪装值**
- 位置：EffectScript.cs:311-320
- `Budget` 声明为 `readonly record struct`，但唯一字段 `Caps` 是 `IReadOnlyDictionary<ResourceId,NatStar>`。三重问题：
  1. **自动生成的相等是引用相等**：record struct 对 `IReadOnlyDictionary` 字段用默认 EqualityComparer → Dictionary 的 object.Equals → 引用比较。两个 caps 内容完全相同的 Budget 不相等也不同哈希。struct 外观、identity 语义——正是 Hickey 所说 "it looks like a value but isn't"。
  2. **接口只是门面**：调用方持有原始 Dictionary 引用即可在传入后继续改写（Parse 路径 EffectScriptContract.cs:32-38 构造后立即交给 Budget，本地构造尚安全；但任何 API 边界上外部持有的字典都可事后变异）。
  3. **可向下转型破坏**：`(Dictionary<ResourceId,NatStar>)budget.Caps` 合法且无人拦截。

**H2 — `Budget.None` 全局单例指向可变字典（隐藏共享可变状态）**
- 位置：EffectScript.cs:320 `public static readonly Budget None = new(new Dictionary<ResourceId, NatStar>());`
- 该字典被所有默认预算共享。一次恶意/意外的 cast 后 `Clear()` 或 `[r]=cap` 即改变**全程序**的「无上限」语义——后续所有 Audit 结果依赖这个被污染的全局状态。Hickey 判据：你无法从类型签名看出 `None` 是否安全共享；这里答案是"不安全"。修复成本极低：用 `ImmutableDictionary.Empty` 或冻结子类抛异常。

**H3 — auditR5 F1 的修复不彻底：EffectScript 自称「构造即固定」，但 Budget.Caps 仍是逃逸的可变引用**
- 位置：EffectScript.cs:58-68（注释宣称 "构造即固定，使 EffectScript 为不可变值对象（修 auditR5 F1）"）
- `Budget = budget.Caps != null ? budget : Budget.None` 只是替换了 null 壳，**没有防御性拷贝**。调用方保留传入字典的引用，在 Audit() 之后继续写入新 cap ⇒ 同一 EffectScript 实例前后两次 `Audit()` 结果不同 ⇒ 注释里的承诺（"原 { get; init; } 可被改写 ⇒ 同实例 Audit 结果依赖可变状态"）只堵了一条路，状态依赖从属性移到了字典里。这正是把可变状态"藏"进抽象的另一层的反模式。

**H4 — `Signature.GetHashCode` 违反 Equals/GetHashCode 契约（等值对象可能不同哈希）**
- 位置：Objects.cs:204-213
- `Equals` 用 `SetEquals`（顺序无关），但哈希按 `ImmutableHashSet` 枚举序做 `(h*31)^c` 折叠——该折叠对顺序敏感（乘加 XOR 链非交换）。两个 SetEquals 相等的 Signature 因插入历史不同而枚举序不同时，哈希不等。一旦有人把 Signature 放进 Dictionary/HashSet 键（代码注释自己都拿它当值用），会出现查不到/重复条目这类最难排查的 identity 幽灵 bug。修法：对每桶先算各元素哈希的顺序无关折叠（如 XOR 或排序后折叠），再三桶组合。

### MEDIUM

**M1 — Signature 字段非 readonly：持久化风格靠纪律而非类型维持**
- 位置：Objects.cs:147-149, 171, 184
- Add/Union 用 `new Signature { _read = _read, ... }` 复制构造（persistent style，正确），但 `_read/_write/_occupy` 未标 `readonly`，且类内任意方法都能就地改写而不留痕迹。当前无就地变异，但类型没拦住未来的自己。标 readonly 成本为零。

**M2 — NetTable 无相等语义：同一签名的两张 net 表是不同身份**
- 位置：Algebra.cs:46-100
- `sealed class` + 私有可变 Dictionary，无 Equals/GetHashCode。作为 Compute 的返回读模型可接受，但它被 `Derived.Net` 公开暴露并跨方法传递（DerivedMetrics.cs:78）——调用方无法判断两表是否代表同一净效应，只能逐资源手比。伪装值。另注：`Resources => _net.Keys`（Algebra.cs:88）暴露的是活视图，当前构造后无再变异故安全，但契约靠约定不靠类型。

**M3 — SerializeResource / ResourceKey 的静默兜底 `_ => memory:0` 会销毁数据（表示层无验证）**
- 位置：EffectScriptContract.cs:216-218, 234-236
- `ResourceId` 有 15 个构造子，契约层 ParseResource 只认 5 种，序列化侧遇到其余（Tree/Self/Physics/Disk/Signal/Gpu/AudioMixer/Callback/Network/Input/Custom…）**静默改写成 `{memory:0}`**。round-trip 后资源身份被偷换，net/Peak 全部对错资源计算——比抛 FormatException 危险得多。与文件头自述 "fail-fast，非静默漏报" 直接矛盾（Parse 侧已修 C2 fail-fast，Serialize 侧漏了同样的原则）。同理 `ResourceKey` 兜底 `"memory:0"`。

**M4 — ZStar 加法无溢出守卫，违反自身「永不崩溃、保守 ⊤」纪律**
- 位置：SignedNet.cs:30-31 vs Numeric.cs:44-48
- NatStar 的 +/- 显式检测 ulong 回卷转 ⊤；ZStar 的 `+`/`-` 裸 long 运算，回绕成负值/正值会**伪造出错误的有符号 net 区间**（如 long.MaxValue 级 size 求和翻负 ⇒ ContainsZero 误判守恒）。同一代码库两套 ⊤ 纪律不一致；SignedInterval.Add（SignedNet.cs:76）直接继承此缺陷。

### LOW / NOTE

**N1 — Signature 桶属性返回 ImmutableHashSet 本身 ✓，但字段声明为 mutable-typed `private ImmutableHashSet<Claim> _read = ...` 且通过对象初始化器赋值**（Objects.cs:147-149,171,184）。与 M1 同根：建议 `readonly` + 构造函数注入。

**N2 — EffectScript 是 class、无 Equals：作为「纯数据契约」却是身份语义**
- 位置：EffectScript.cs:55
- 文档自称纯数据/值对象，但两个事件+预算完全相同的脚本不相等。若它只作一次性管道输入可接受（Note 级）；若未来进入集合或缓存即成陷阱。Signature 已吃过一次这亏（Objects.cs:196-198 注释自认 "Hickey 式 footgun"），同类风险别再犯第二次。

**N3 — Audit 扫换线局部可变状态（net/peakSum/topCount/grp 四个 Dictionary + HashSet + 闭包 Step）**
- 位置：EffectScript.cs:150-156, 159-216
- 这是**良性的局部可变状态**：作用域封闭于方法内、不逃逸、方法对外纯函数（输入不可变→输出 AuditResult）。Hickey 并不反对局部 mutation，反对的是隐藏的、共享的、逃逸的可变状态。此处无此问题。仅注意 `grp` 以 `(ResourceId, ScopeId, int)` 为键——ScopeId record 作字典键正确依赖其结构 GetHashCode ✓。

**N4 — `default(Budget).Caps == null` 是潜伏的 NRE**
- 位置：EffectScript.cs:67 有守卫，但 `Audit(Budget cap)`（EffectScript.cs:96）直接遍历 `cap.Caps`——传 `default(Budget)` 即 NRE。struct 的 default 实例绕过一切 ctor 不变量，这是用 struct 承载引用字段的固有代价；要么在 Audit 入口判空，要么让 Caps 恒非 null（如 lazy 初始化到 None）。

**N5 — NodePathOrUnknown.Unknown 与 Interval.Default 等静态 struct 单例**（Objects.cs:79, Numeric.cs:95-99）：readonly struct 值拷贝语义，安全 ✓。对照之下 Budget.None（H2）是唯一的危险单例。

---

## 三、结论摘要

- 真值比例极高：15+ 个 readonly record struct / record 类型构成不可变代数核心，⊤ 闭环设计（NatStar/ZStar/DeviationVal）是教科书级的「用类型承载边界」。
- 状态泄漏集中在一处：**Budget 及其字典**（H1/H2/H3/N4 全部围绕它）。一个 `IReadOnlyDictionary` 门面 + 共享可变单例污染了整条 EffectScript 不可变性叙事。换成 ImmutableDictionary + 结构相等即可一并解决四项。
- 身份陷阱集中在 Signature 家族：GetHashCode 契约违规（H4）是当下真实 bug，非风格问题。
- 契约层 Serialize 的静默 memory:0 兜底（M3）违背本库自己的 fail-fast 原则。

优先级建议：H2/H3/H1（同一处修复）> H4 > M3 > M4 > 其余。
