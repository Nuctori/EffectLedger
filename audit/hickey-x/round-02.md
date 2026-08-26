# Hickey-X R2: Illegal State Unrepresentable — 类型正确性审计

> 视角：类型驱动正确性。目标不是「能表达所有合法状态」，而是「无法构造任何非法状态」。
> 来源说明：本报告的分析由 hickey-auditor 子代理完成（其 API 输出捕获连续失败后从会话记录恢复），**全部载荷性引用的行号已由主会话逐一独立复核**。对 R1 结论的裁决均标注「已核实/未复核」。

---

## 一、核实矩阵（对 R1 发现的逐条裁决）

| R1 发现 | 裁决 | 复核证据 |
| --- | --- | --- |
| HIGH-1 剧本 DSL 无第一公里 | **同意（部分未复核）** | 本轮视角外（文档/samples 未读）。代码侧佐证成立：`Parse` 要求 events/lifetime/scope/footprint + claim 四字段（EffectScriptContract.cs:53 起、Require 模式贯穿）——「约 10 个概念才起步」的概念数主张与解析器结构一致。samples 缺失留待 R6 文档轮复核。 |
| HIGH-2 budget 键双轨方言 | **同意 + 补强** | 对象路径五键校验（:146 区域）vs 平面键 switch（:177-185）、未知键裸抛（:184）均已核实。**补强**：budget 键路径完全绕过 `ResourceId.Normalize`（对象路径 ParseClaim 显式 `.Normalize()`，budget 路径无此步）。结合 Normalize 规则 `Self("signal_"+s) ⇒ SignalBus(s)`（Objects.cs:54-55）：claim 写 `Self("signal_signal_x")` 归一为 `SignalBus("x")`，budget 键 `"signalBus:signal_x"` 却是 `SignalBus("signal_x")` ⇒ gate(2) 查找永不命中，**预算静默失效**。同一前缀剥离规则在两个入口可见性不同，非法的「声明-检查错位」可表示且无声。 |
| HIGH-3 零预算=恒真审计 | **同意 + 类型角度补刀** | gate(2) 只遍历 `cap.Caps` 已核实。「补刀」：`AuditResult.Passed: bool` 是有损压缩——「查过且通过」与「零门运行」在类型上不可区分（`Budget.None` 与「用户显式空预算」同为 Caps.Count==0，EffectScript 构造器还把 default 归一为 None）。这正是本轮主题的标本：**「未审计的通过」这个非法状态可表示，且无法从结果类型观测**。 |
| MED-1 lifetime.lo="⊤" 静默消失 | **同意 + 精化** | 需按 hi 分叉：(a) `[⊤, finite]` 其实被 Interval 构造器拒绝（Numeric.cs lo.IsTop&&!hi.IsTop 抛 ArgumentException）——但异常类型不符契约自家 FormatException 标准；(b) `[⊤,⊤]` 合法通过构造器（注释自证「未知区间」），随后 sweep 以 `if (lt.Lo.IsTop) continue` 静默跳过（EffectScript.cs:127 注释直认）。**病根精确化：Interval 类型允许的状态空间 ⊃ 全部消费者可处理的状态空间，差额靠 continue 吸收。** |
| MED-2 契约面≠API 面 | **方向同意，数字修正** | SerializeScope 覆盖 Scene/Method/Type/Global 四种，Shell/Loop/Conditional/Async 抛（EffectScriptContract.cs:199-206）。SerializeResource 覆盖 Gpu/CommandBuffer/Memory/Occupancy/SignalBus 五种，**其余 10 种抛**（:217-225）——ResourceId 子类实际是 **15 个**（Objects.cs:23-39：Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Occupancy/Callback/Network/Input/Custom/CommandBuffer/SignalBus），R1 的「14 个中 9 个」有误，正确为 **15 中 10**。不对称比 R1 陈述的更宽。 |
| MED-3 claim.scope 幽灵字段 | **同意（加强版）** | 新证据：剧本层 sweep 的 net 累加**完全没有 scope 过滤**（gate(1) 直接遍历 OccupyClaims 按 e.Scope 归组），而 L1 的 NetTable/Peak.Compute 按 claim scope 过滤（Algebra.cs:60/119）——两条路径连「忽略方式」都不一致。且 Combination.Loop 会重写 claim scope（DerivedMetrics.cs），证明连 L1 自己都认为该字段不可信。幽灵字段的判定从「必填但被忽略」升级为「必填、被序列化、被改写、唯独不被读取」。 |
| MED-4 mode:"unknown" 免检 | **同意** | Resolve(Unknown→Use)、Use 与一切兼容（Algebra.cs:15/:22）、gate(3) 分组内 IsCompatible(mode,mode) 经 Resolve 得 true——链路完整核实。 |

---

## 二、新发现（本轮独有）

### N1 · HIGH — SerializeBudget 把 ⊤ 上限序列化为 0：round-trip 语义翻转

- **位置**：`EffectScriptContract.cs:227-233`——`d[ResourceKey(kv.Key)] = kv.Value.Value`，`NatStar.Top` 的 `.Value` 是 0。
- **判词**：C# 侧合法剧本（cap=⊤ 即「不设上限标记」）经 ToJson→Parse 后 cap 变成 **0**——峰值检查从最宽变成最严，peak>0 即报 Violation。同一个符号 ⊤，进门前是「无限」，出门后是「零」。Parse 侧同样无法表达 ⊤（`:172` 只有 GetUInt64）。这是本轮最锋利的发现：**非法状态不但可表示，还是序列化器亲手制造的**。
- **修复**：SerializeBudget 对 IsTop 输出 `"⊤"`（或 `"inf"`），ParseBudget 对应接受；一行对称修复。

### N2 · HIGH — Runtime 层不变量全靠约定维持：图结构可携带语义矛盾

- **位置**：`DependencyGraph.cs:24`（`Register` 用 `_fibers[f.Id]=f` 静默覆盖同 Id fiber——重复身份可表示）；`:26-28`（`AddHardEdge` 不验证两 fiber 已注册、**完全不检查注释承诺的「B.Requires ⊇ A.Provides」不变式**）；`Fiber.cs:37/42`（State/Dependents 为 internal set，程序集内任何代码可绕状态机赋值）。
- **判词**：L1 把边界做成了类型，Runtime 层却退回「注释即契约」：边的不变量写在 XML 注释里，执行时无人站岗。`Coeffect.Requires` 疑似死字段（全仓未见消费，仅 AddDependency 注释提及）——claim.scope 幽灵字段的模式在 Runtime 层重演。
- **修复**：AddHardEdge 校验注册性 + Requires⊇Provides，违约抛；Register 重复 Id 改抛或返回 bool。

### N3 · HIGH-MED — `default(T)` 挖穿全部 record struct

- **位置**：`Claim` 为 readonly record struct（Objects.cs:126），Resource/Scope 是引用类型字段且无 null 校验——`default(Claim)` 与 `new Claim(kind, null!, mode, null!, null)` 均合法构造。Coeffect/InverseClaim 同型。
- **判词**：项目自己的铁律注释就写在旁边：「不存在漏字段的 Claim（构造即合法）」（Objects.cs:120-122）。`default` 让这句注释成为假话——五参位置记录挡住了「少写参数」，没挡住「null 冒充值」。struct 的默认值通道是 C# 类型系统给所有 record struct 开的后门，本项目零设防。
- **修复**：Normalize() 入口 null-check 抛（最小）；或 Claim 改 sealed class（彻底，代价是分配）。

### N4 · MED — Interval.Merge 声音性缺陷：未知下界静默变精确值

- **位置**：`Numeric.cs:101` `Merge => new(Lo.Min(o.Lo), Hi.Max(o.Hi))`，配合 `Min` 律「min(⊤,x)=x」（:45-49）。
- **判词**：`Merge([⊤,⊤], [1,1]) = [1,⊤]`。[⊤,⊤] 表示「完全未知区间」，join 之后下界却断言为精确 1——未知蕴含「下界可能是任何 ≥0 的值」，结果声称 lo≥1，**抽象不健全**（unsound overapproximation）。Hi 位 ⊤ 表现为 +∞（吸收），Lo 位 ⊤ 表现为「被有限方吃掉」，同一符号两种格论行为。
- **修复**：Lo.Min 对 ⊤ 保持 ⊤（除非双方有限），或在 Merge 里特判任一方为全未知则结果为全未知。

### N5 · MED — 「峰值」双定义：桶口径分叉

- **位置**：`Peak.Compute`（Algebra.cs:113-124）遍历 **AllClaims 三桶**、仅跳过 Release；剧本层 sweep 的峰值只累加 **OccupyClaims**（EffectScript.cs gate(2) 路径）。
- **判词**：同名「峰值」两个口径：L1 版把 read/write claim 的 size 也计入并发占用，剧本版不算。跨层对账时（Runtime 权威闭合 vs 剧本审计）同一脚本可能得出不同峰值。read/write 两桶本就「进了 Signature 却无消费者」（R1 C3 的观察），这里是它们唯一的隐性消费者——而且消费得可疑。
- **修复**：Peak.Compute 过滤 Kind==Occupy，或文档明示两种峰值的适用层并改名区分。

### N6 · MED — 扫换线裸算术绕过 NatStar 溢出律

- **位置**：`EffectScript.cs:123` `samplePoints.Add(NatStar.Of(maxFinite + 1))`——裸 ulong 加法；maxFinite==ulong.MaxValue 时回绕为 0，采样点排序破坏、审计结果错误。Numeric.cs 承诺「溢出 ⇒ ⊤ 保守」（:37-38），此处手抄逻辑绕开。同函数内乘法哨兵（mul==ulong.MaxValue 当 Top）是第三套约定。
- **判词**：NatStar 存在的全部意义就是独占溢出律，结果审计热路径里它被绕开了三次。类型法律不强制执行路径，就等于没有法律。
- **修复**：`maxFinite == ulong.MaxValue` 时直接用 Top 采点或跳过 +1 采样；乘法走 NatStar.Multiply。

### N7 · MED — 根级未知键静默忽略：拼写错误放大 HIGH-3

- **位置**：`Parse` 只读 `root["events"]` 与可选 `root["budget"]`（EffectScriptContract.cs:33），其余根级键一律无视。
- **判词**：`"Budget"`（大写）或 `"budges"` → 解析成功、caps 为空、gate(2) 整体失效——配合 HIGH-3，「 Passed=true」依旧。对象内多余键同样静默（如 lifetime 数组多一个元素）。形状校验只查「必需的在不在」，不查「多余的有没有」——fail-fast 只做了半边。
- **修复**：根级白名单校验，未知键抛并列出合法键集（与 ParseResource 五键报错同一标准）。

### LOW 群

- **N8** sweep 内手抄乘法哨兵（ulong.MaxValue）与 NatStar.Top 双哨兵并存（EffectScript.cs enter/exit 路径），约定漂移温床。
- **N9** budget JSON 无法表达显式 ⊤ 上限（GetUInt64 单出口，:172）——想写「明确无上限」的用户只能靠省略键（然后掉进 HIGH-3）。
- **N10** exit 路径减法 clamp 到 0 掩盖账目不一致（curSum 减单事件贡献不应为负，负了说明 bookkeeping 有 bug，clamp 把 bug 吃了）。

---

## 三、「throw 保护 vs 类型排除」比例分析

throw 分布 grep（本轮实测）：EffectScriptContract.cs 25 处、Numeric.cs 2、Objects.cs 1、Algebra.cs 1、DerivedMetrics.cs 1、SignedNet.cs 1、EffectAttributes.cs 2、EffectScript.cs **0**、Runtime 层若干（IHost 实现与 PluginRuntime 装载期为主）。

三个结构性观察：

1. **25/33 的运行时 throw 集中在契约解析层**——JSON 边界的 fail-fast 做得很重（这是对的，信任边界该拦）；但 **EffectScript.cs 零 throw** 意味着核心审计循环完全依赖输入已合法，而 N6/N10 显示它在边界内自己手抄算术、自己 clamp 异常——核心层既不设防也不借力类型层。
2. **类型排除的覆盖率呈梯度**：载体层（NatStar/Interval/SignedInterval）构造即合法 ✅；聚合层（Claim/Signature）靠 Normalize 补、default 后门洞开 ⚠️；Runtime 层几乎全靠注释与装载期 throw ❌。越靠近 Godot 交互面，「illegal state unrepresentable」的承诺衰减越严重。
3. **异常类型不一致**：同样是用户输入错误，契约层抛 FormatException、Interval 构造抛 ArgumentException、budget 值抛 System.Text.Json 原生异常——三种诚实度，用户要学三套恢复姿势。

## TOP-3（本轮）

1. **N1 SerializeBudget ⊤→0**：序列化器主动制造语义翻转，round-trip 即损坏——比「不可表示」更糟，是「被篡改」。
2. **N2 Runtime 图不变量无执行**：Requires⊇Providers 只活在注释里，重复 Id 静默覆盖——权威闭合层的地基靠君子协定。
3. **N3 default(T) 后门**：一行 `default(Claim)` 让项目铁律注释成为假话；所有 record struct 共享同一个未设防通道。

---

## 证据

- 主会话独立复核行号的文件：`src/Cosmos.EffectAlgebra/EffectScriptContract.cs`（:33/:146/:172/:177-185/:199-206/:217-233）、`Objects.cs`（:23-39/:51-55/:120-126）、`Numeric.cs`（:37-49/:101）、`Algebra.cs`（:113-125）、`EffectScript.cs`（:105-127）、`src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs`（:16-35）、`Fiber.cs`（:33-47）
- 子代理会话内读取（未逐行复核，仅采纳非载荷结论）：tests/ 抽查、IHost.cs、PluginRuntime.cs、GodotShell.cs、InverseReplay.cs、SignedNet.cs、DerivedMetrics.cs
- 对 R1 的裁决基线：audit/hickey-x/round-01.md
