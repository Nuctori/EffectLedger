# Rich Hickey 视角对抗性审计 — Round 01（Simple Made Easy 透镜）

范围：`src/Cosmos.EffectAlgebra` 下 7 个源文件（Algebra / Objects / Numeric / EffectScript / EffectScriptContract / DerivedMetrics / SignedNet）。
透镜：Hickey「Simple vs Easy」——**简单 = 客观、不交织（complect）、一个事物一个职责**；**容易 = 熟悉/顺手，但常常是伪装的复杂度**。逐符号判定本质（essential）/ 偶然（accidental），给严重度与证据行号。

---

## 总判定

**核心代数（Numeric/SignedNet/Objects 的载体类型）基本合格：载体小、不可变、值语义、构造即合法——这是真正的 simple。**

但 API 表面层存在系统性问题：

1. **四名一实（Union/Join/Sequence/Parallel）是最典型的 Hickey 靶子**：同一个函数挂四个名字，用「词汇」伪装语义差异。Sequence/Parallel 注释自己承认「均为 ∪」「并行检查由 L3 补」——即当前名字在撒谎：叫 Parallel 但没有任何并行性被表达。调用方读代码时无法区分 `Sequence(a,b)` 和 `Parallel(a,b)` 有何不同，因为**没有不同**。这不是 polymorphism，是 alias 膨胀。
2. **Claim.Scope 与 EffectEvent.Scope 双份真相（complecting）**：同一资源占用有两个 scope 字段，gate(3) 用 Event.Scope、NetTable.Compute 用 Claim.Scope。注释里已经承认这造成过真实 bug（auditR3b TC7「两视角冲突归因错位」）。两个独立可变的 scope 来源 complect 了「元素存在域」和「claim 归属域」两个概念。
3. **ResourceId 是 15 个构造子的上帝联合**：契约层只认 5 个，序列化层对其余 10 个静默坍缩为 `memory:0`（数据损坏），Normalize 只处理 signal 前缀两种情形。领域模型声称的完备性与实际使用面严重脱节。

---

## 逐符号表

判定：✅本质 / ⚠️偶然 / ❌偶然且有害。严重度：高 / 中 / 低。

| 符号 | 位置 | 判定 | 严重度 | 说明 |
|---|---|---|---|---|
| `NatStar` | Numeric.cs:9-66 | ✅本质 | — | ℕ∪{⊤} 单一职责，运算律内嵌，环绕→保守⊤。simple。 |
| `Interval` | Numeric.cs:71-117 | ✅本质 | — | 区间载体 + 构造校验 lo≤hi，merge 为唯一 join。 |
| `DeviationVal` | Numeric.cs:122-153 | ✅本质 | — | double∪{⊤}，ExceedsThreshold 把 ⊤ 决策收进类型。 |
| `ZStar.Min` | SignedNet.cs:39 | ⚠️偶然 | 中 | **文档/代码矛盾**：注释写 `min(x,⊤)=x`，实现返回 `Top`。对照 NatStar.Min（Numeric.cs:52-57）语义相反。同名运算跨载体语义不一致 = 认知陷阱，读者必错一次。 |
| `SignedInterval.Add` vs `.Merge` | SignedNet.cs:70-76 | ✅本质（区分正确） | — | 求和 vs min/max 包络刻意分开，注释解释了为什么 Merge 会漏报泄漏。这是好的概念拆分。 |
| `Rid` / `StringName` | Objects.cs:10,14 | ✅本质 | — | 零 Godot 依赖约束下的最小别名，映射职责显式推给 §7。 |
| `ResourceId`（15 构造子） | Objects.cs:22-39 | ❌偶然 | **高** | 上帝类型：Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Occupancy/Callback/Network/Input/Custom/CommandBuffer/SignalBus。其中 AudioMixer(int)、Network(PeerId,Method) 等在全部 7 文件中无任何消费点；契约层只支持 5 个（EffectScriptContract.cs:160-176）。未被使用的构造子是投机泛化（speculative generality）。 |
| `ResourceId.Normalize` | Objects.cs:51-68 | ⚠️偶然 | 低 | 只归一 signal 前缀两类，其余恒等。字符串前缀匹配（`StartsWith("signal_")`)把命名约定编码进代数核心——数据里藏协议，Hickey 式坏味道。建议 Signal 构造时即归一，删掉运行时前缀猜测。 |
| `NodePathOrUnknown` | Objects.cs:73-94 | ⚠️偶然 | 低 | 手写 Option<string>。可用 `string?` + 静态 Unknown 单例或直接复用 ZStar 式模式。不致命但属于重复发明。 |
| `ScopeId`（8 构造子）+ `IncludedIn` | Objects.cs:88-112 | ⚠️偶然 | 中 | Loop/Conditional/Async/Shell 四个构造子在 7 文件内零消费；`IncludedIn` 实为 `Equals(other) \|\| other is Global`——偏序记号 ⊆* 承诺了传递闭包，实现只有平凡情形。名实不符（承诺 ⊆*，交付 ==）。 |
| `Kind`(enum) / `Mode`(enum) | Objects.cs:116,123 | ✅本质 | — | 穷举枚举，Unknown 显式建模优于 null。 |
| `Claim.CompatibleWith(Claim)` | Objects.cs:137 | ⚠️偶然 | 中 | 名字说「Claim 兼容」，实现只比 Mode（`IsCompatible(Mode, other.Mode)`），完全忽略 Resource/Scope。两个不相干资源的 create/create 会判「冲突」？不会——会判 true/false 与资源无关。API 名撒谎，调用方必然误用。应改名 `ModeCompatibleWith` 或下沉到 Compatible 类私有。 |
| `Signature` 三桶 + 结构相等 | Objects.cs:146-217 | ✅本质 | — | ImmutableHashSet 分桶量纲隔离，手补 Equals/GetHashCode 修引用相等 footgun（含自嘲注释）。桶枚举 AllClaims 收口在 Algebra.cs:126-135。 |
| `Signature.Union` | Objects.cs:182-190 | ✅本质 | — | 半格并的唯一真身。 |
| `Signature.Join` | Objects.cs:192 | ❌偶然 | 高 | `=> Union(a,b)`。纯别名，零附加语义。 |
| `Combination.Sequence` | DerivedMetrics.cs:50 | ❌偶然 | 高 | `=> Union(a,b)`。同上。 |
| `Combination.Parallel` | DerivedMetrics.cs:53 | ❌偶然 | 高 | 同上，且更糟：名字暗示并行语义但没有任何并行性表达，「Compatible 检查由 L3 补」意味着现在调用它是安全谎言。 |
| `Combination.Loop` | DerivedMetrics.cs:33-47 | ⚠️偶然 | 中 | 逻辑正确但实现绕：三桶各一遍循环、每个 claim 走 `Signature.Union(result, Signature.Of(c with {...}))`——单 claim 建 Signature 再并集，O(n²) 且啰嗦。内部应有 claim 级 Add。 |
| `LoopCount` | DerivedMetrics.cs:12-29 | ✅本质 | — | NatStar 上的 newtype，区分「ω 语义」与普通自然数。Haskell newtype 惯例，成本一行。 |
| `Derived.Peak` / `Derived.Net` | DerivedMetrics.cs:69,73 | ⚠️偶然 | 低 | 纯转发别名层（`Peak.Compute` / `NetTable.Compute`）。两个入口做一件事。留一个。 |
| `Derived.IsConserved(s,r,scope)` | DerivedMetrics.cs:77 | ⚠️偶然 | 低 | 为查一个资源构建整张 net 表。API 简单但隐藏 O(全 claims) 成本；多资源场景调用方会反复全量重算。 |
| `NetTable.Negate`/`ToSigned` (Algebra.cs:72-90) vs `EffectScript.ToZ`/`Negate`/`ScaleSize` (EffectScript.cs:299-306) vs `Combination.Scale` (DerivedMetrics.cs:57-62) | 多处 | ❌偶然 | **高** | **ℕ*→ℤ* 符号转换 + ω 缩放逻辑有三份拷贝**：Algebra.cs 一份（区间级）、EffectScript.cs 一份（端点级）、DerivedMetrics.cs 一份（Scale 与 ScaleSize 逐字符等价）。三处将来必然漂移——这正是 complecting 的温床。应收敛到 Interval/ZStar 上的单一运算符。 |
| `Peak.Compute` 不滤 Kind | Algebra.cs:110-124 | ⚠️偶然 | 中 | NetTable 严格只取 Occupy 桶（Algebra.cs:59「量纲隔离」），Peak 却把 Read/Write 桶的 size.Hi 也加总（仅滤 Release）。同一份 PDR 下两个聚合器对「量纲隔离」执行不一致。若 read/write claim 带 size 即混入峰值——疑似 bug 或至少概念泄漏。需对照 §3.3.2 确认意图。 |
| `Compatible.Resolve(Unknown→Use)` | Algebra.cs:16-30 | ✅本质 | — | fail-closed 最弱兼容，决策集中一处。 |
| `Weight.Of` | Algebra.cs:38-41 | ⚠️偶然 | 低 | 用 NaN 编码 ⊥——用 IEEE 哨兵当代数值，正是 NatStar/DeviationVal 全文避免的模式。同类问题两套方案并存 = 内部不一致。 |
| `EffectEvent.Scope` + `Claim.Scope` 双字段 | EffectScript.cs:28 / Objects.cs:126 | ❌偶然 | **高** | 双 scope 真相已产过真实缺陷（EffectScript.cs:180-182 注释自述 auditR3b TC7）。Event 已带 Scope 并重标所有 claim（Combination.Loop 重写 c.Scope=loopScope），则 Claim.Scope 在剧本路径上是死重量；而 NetTable 路径又读 Claim.Scope。两条路径对「scope」来源不同 ⇒ complecting。应二选一：要么 Claim 无 Scope 由容器携带，要么 Event 不带。 |
| `EffectScript.Audit` 闭包块重复行 | EffectScript.cs:272,274 | ⚠️偶然 | 低 | `if (e.Lifetime.Lo.IsTop) continue;` 出现两次（中间夹一行），明显的合并残留。 |
| `EffectScript.Audit` 本体 ~200 行 | EffectScript.cs:97-291 | ⚠️偶然 | 中 | 一个方法同时做：采样点生成、扫换线排序、三个 gate、闭包检查。gate(2) enter/exit 两分支各自手写 ulong 环绕防护（154-171 vs 172-189）——本可让 NatStar 运算符承担（它已有环绕→⊤ 律！）。手工复刻已有代数律 = 典型「绕过抽象」。 |
| `Budget` record struct + `budget = default` | EffectScript.cs:313-325 | ⚠️偶然 | 低 | `default(Budget).Caps == null`，靠构造函数 `budget.Caps != null ? budget : Budget.None` 兜底。可空字典当哨兵，不如显式 None 单例 + 私有构造。 |
| `ParseScope` 用 "scene" 键承载一切 name | EffectScriptContract.cs:100-119, 194-199 | ❌偶然 | 中 | 序列化 Method(m) 输出 `{"type":"method","scene":m}` ——method 的 name 放在叫 "scene" 的字段里。schema 撒谎，AI 契约消费方最受伤。应为统一 `"name"` 字段。 |
| `SerializeResource` 兜底 `_ => memory:0` | EffectScriptContract.cs:211-219（211 `_ =>` 分支 :218） | ❌偶然 | **高** | 15 个 ResourceId 构造子只序列化 5 个，其余**静默改写为 `{memory:0}`**。round-trip 数据损坏且无诊断——fail-silent 正是本项目注释里到处宣称要消灭的东西（对比 ReqStr 的 fail-fast 哲学）。至少应抛 FormatException。 |
| `ParseResourceKey` budget 键解析 | EffectScriptContract.cs:184-191 | ⚠️偶然 | 中 | 与 SerializeResource 平行的第二套字符串编解码（`gpu:` 前缀拼接）。resource 的文本表示有两套（对象形 / key 形），加上 C# record 形共三种真相。 |
| `EffectScript.Audit()` 无参重载 | EffectScriptContract.cs:236 | ⚠️偶然 | 低 | partial class 里塞便捷重载导致 Audit 有两个入口（Audit(cap) / Audit()），行为依赖实例 Budget——可接受但加剧入口分裂。 |

---

## Top 3 可砍

### 1. 砍掉 Join / Sequence / Parallel 三个别名（Objects.cs:192; DerivedMetrics.cs:50,53）
四个名字一个实现。保留 `Signature.Union`；若 §PDR 要求 Sequence/Parallel 词汇，让 **L3 Analyzer 层**提供带检查的真正不同的函数，而不是在同构实现上贴语义标签。删除后 API 从「四个相似函数该选哪个」回到「一个半格并」——这是纯粹的减法，零行为变化。

### 2. 统一 scope 单一真相：删 `Claim.Scope`（剧本路径）或 `EffectEvent.Scope`
现状双字段已经烧过一轮修复（TC7）。既然 `Combination.Loop` 无条件把 claim scope 重写为 loopScope，说明剧本域内 Claim.Scope 从不被信任；而 `NetTable.Compute` 读的是 Claim.Scope。收敛方案：Claim 保留 Scope（L1 通用），EffectScript 域内所有读取一律走 `e.Scope`（gate(3) 已如此），并在 Parse 时强制 `c.Scope == e.Scope` 否则 fail-fast——把「两个必须相等的字段」变成「一个字段」。

### 3. 合并三份 ℕ*→ℤ* 转换/缩放拷贝，并裁剪 ResourceId
(a) 把 `Negate`/`ToZ`/`ScaleSize`/`Scale`（Algebra.cs:72-90、EffectScript.cs:299-306、DerivedMetrics.cs:57-62）收敛为 `SignedInterval`/`Interval` 上的公开运算（如 `Interval.ScaleBy(NatStar)`、`Interval.ToSigned(Mode)`），EffectScript.Audit 的手写 ulong 环绕防护随之消失（NatStar 运算符已有此律）。
(b) ResourceId 从 15 构造子砍到契约层实际支持的集合（Gpu/CommandBuffer/Memory/Occupancy/SignalBus），未消费者移到映射层或按需再加；`SerializeResource` 的 `_ => memory:0` 兜底改为抛 FormatException，与文件自身 fail-fast 声明一致。

---

## 附注（非阻塞）

- `ZStar.Min` 注释与实现矛盾（SignedNet.cs:38-39）建议先改注释或对齐 NatStar 语义——一行修。
- `Peak.Compute` 是否应滤 Occupy 桶需对照 PDR §3.3.2 确认；若是 bug 则升为 Blocker 级。
- `Weight.Of` 的 NaN-as-⊥ 编码与全库 ⊤ 哨兵风格冲突，长期应换专用载体。
