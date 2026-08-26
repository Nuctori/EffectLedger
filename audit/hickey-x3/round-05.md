# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 5 轮 · Maybe Not（可选性 / 默认值 / 约束设计）

> 视角：Rich Hickey（Simple Made Easy）。独立上下文本轮，未读取 `audit/` 下任何历史报告；
> 历史结论仅以源代码/文档中可见的锚点（README「已知语义锐边」、MA-002、OPEN-B4、R10-F1/F2 等）为准入矩阵。
> 所有探针均在仓库外临时工程 `/tmp/eaa-probe` 实测（引用 `bin/Debug/net10.0/Cosmos.EffectAlgebra.dll`），未改动任何仓库源文件。

---

## 核实矩阵（源内可见历史锚点 · 本轮裁决）

| 历史锚点 | 出处（本轮可见） | 裁决 | 证据 |
| --- | --- | --- | --- |
| `Claim.Size` 省略 ≠ 未知：`?? [1,1]`（精确 1） | README.md:58；Objects.cs:127,134 | **仍在**（设计锁死）。实测：raw Size=null 经 `Signature.Of` 后 size=`[1,1]`，非 ⊤ 非 0 | Objects.cs:127 `Interval? Size`；:134 `Size = Size ?? Interval.Default` |
| `[⊤,⊤]` 寿命视为非法输入（OPEN-B4） | EFFECT_SCRIPT.md §边界注记；EffectScriptContract.cs:73 | **部分修**：JSON 层已 fail-fast 拒绝；**C# 构造通道仍放行**（见新发现 F3） | EffectScriptContract.cs:73 抛 FormatException；Numeric.cs:83 仅拦 `lo.IsTop && !hi.IsTop`，`[⊤,⊤]` 合法通过 |
| `default(Budget).Caps == null ⇒ 归一为无上限，不 NRE`（R10-F1） | EffectScript.cs:110 注释 | **已修但修错了方向**：NRE 是没了，「忘写预算」被静默归一为「无上限」，把 fail-open 固化成默认语义（见 F1） | EffectScript.cs:64 `Budget budget = default`；:67 `Caps != null ? budget : Budget.None`；:110 同样兜底 |
| `Mode.Unknown` 按 Use 处理 = fail-open | Algebra.cs:10,15；Objects.cs:115 | **仍在**（文档自认「设计锁死」）。另注意 `enum Mode { Use, ... }` 使 `default(Mode)==Use`——struct 默认值恰好是最弱权限 | Algebra.cs:15 `Resolve`；Objects.cs:99 enum 定义 |
| `LoopCount.Of(0)` 抛异常（0 会使 scale 退化 [0,0]） | DerivedMetrics.cs:27 | **已修**（构造子强制 ≥1）。但 JSON/C# 双通道的「缺省 ω=1」仍制造数量级低估（见 F2） | DerivedMetrics.cs:27 throw；EffectScript.cs:48 默认 `LoopCount.Of(1)` |
| `Interval` lo≤hi 构造子强制 | Numeric.cs:83–87 | **在，但有旁路**：`default(Interval)`=[0,0] 合法无害；真正的旁路是 `default(Claim)` 携带 null ResourceId（见 F4） | Numeric.cs:83–87；Objects.cs:127 |
| `EffectScript.Budget` 由 `{ get; init; }` 改为不可变构造参数（auditR5 F1 类锚点） | EffectScript.cs:60 注释 | **部分修**：属性确实只读了，但构造器原样存调用方字典引用，可变性只是搬了个地方（见 F5） | EffectScript.cs:60 注释 vs :349 `public Budget(IReadOnlyDictionary<...> caps) { Caps = caps; }` |

---

## 新发现

### HIGH

**F1 · Budget 缺省 = 无上限：整个峰值闸门的默认状态是「不设防」**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:64,67,71,110；EffectScriptContract.cs:41–45,264
- 证据：`EffectScript(events, Budget budget = default)`；`:67 Budget = budget.Caps != null ? budget : Budget.None`；JSON 侧 `root.TryGetProperty("budget", ...)` 缺失即沿用 `Budget.None.Caps`。实测（探针 P1）：峰值 1,000,000 的剧本，省略 budget ⇒ `Passed=True`；同一剧本 cap=64 ⇒ `PeakExceeded 峰值 1000000 > 预算 64`。
- 判词：写库的人觉得「不设上限最方便」，游戏得到的却是「审计永远全绿」。默认值不是中立的——它是你替所有懒惰用户做的唯一一次、也是最错的一次决定。
- 最小修复：反转默认。`Audit()` 无参重载在 `Budget == Budget.None` 时至少返回一条 warning 级 Violation（`UnboundedBudget`），或要求显式 `Budget.None` 才放行无上限审计——让「无上限」成为说出口的决定，而不是沉默的结果。

**F2 · loop 与 size 双双缺省 ⇒ 并发密度被静默压缩一个数量级以上**
- 位置：EffectScriptContract.cs:58（loop 缺省 `LoopCount.Of(1)`）、:136（size 缺省 `Interval.Default`）；Objects.cs:127,134；README.md:58
- 证据：实测（探针 P2）：意图为 5000 并发粒子的 JSON，同时省略 `loop` 和每 claim `size` ⇒ 每 claim 被膨胀为精确 `[1,1]`×ω=1 ⇒ 显式 cap=64 下 `Passed=True, viol=0`；仅把 `"loop": 5000` 写回去 ⇒ 立刻 `Passed=False`（PeakExceeded）。两个「合理」默认相乘，把 5000 份占用审成了 1 份。
- 判词：每个默认值单独看都「说得过去」，组合起来就是系统性的假绿——省略两个可选字段，语义差了三个数量级，而 API 连眨都不眨一下。
- 最小修复：occupy 桶 claim 的 size 缺省应落 ⊤（未知）而非精确 [1,1]，或要求 AI 契约中 occupy 必填 size/loop 二选一并显式声明；至少在 Violation 里报告「该资源峰值基于缺省假设 ω=1,size=[1,1] 计算」。

**F3 · `[⊤,⊤]` lifetime：JSON 门卫拦得住，C# 大门敞着**
- 位置：src/Cosmos.EffectAlgebra/Numeric.cs:83–87（构造子）；对照 EffectScriptContract.cs:73
- 证据：Numeric.cs:83 仅当 `lo.IsTop && !hi.IsTop` 抛错；`new Interval(NatStar.Top, NatStar.Top)` 合法构造。实测（探针 P3）：C# 构造 create-only 泄漏事件 + lifetime `[⊤,⊤]` ⇒ `Audit Passed=True, viol=0`（事件永不存活，整段被静默排除出采样与闭包）；同一形状 JSON 被 FormatException 拒绝。
- 判词：同一个非法状态，两条入口一收一放——你在前门查身份证，后门却贴着「施工人员请进」。约束不属于某一层，属于类型；类型没拦住的，别的层拦了也白拦。
- 最小修复：`Interval` 构造子直接拒绝 `lo.IsTop && hi.IsTop`（或提供 `Interval.UnboundedStart` 之外的唯一合法形态并在 EffectEvent 构造子校验 `Lifetime.Lo.IsTop` 时抛），让 JSON 层的规则下沉到类型层。

**F4 · `default(Claim)`：null ResourceId 是可表示的非法状态，爆炸点在千里之外**
- 位置：src/Cosmos.EffectAlgebra/Objects.cs:127（`record struct Claim(... ResourceId Resource ...)`）
- 证据：实测（探针 P4a/b）：`Signature.Of(default(Claim))` 不抛——null 资源的 Read claim 成功入桶；随后 `Peak.Compute` 抛 NullReferenceException、`NetTable.Compute` 抛 `ArgumentNullException("key")`——异常与构造点隔着一整个代数层。
- 判词：你们在注释里写了八遍「类型即边界」，却留了一个 `default` 表达式就能穿过的洞。struct 的默认值是 C# 送你的免费后门，不堵它，「构造即合法」就是一句广告词。
- 最小修复：Claim 主构造体加校验：`Resource is null => throw ArgumentNullException`；或引入非空包装。一行防线，消灭整类远端 NRE。

### MED

**F5 · Budget「不可变值对象」承诺破产：字典引用从构造器直通外界**
- 位置：EffectScript.cs:349（`public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; }`）
- 证据：实测（探针 P5）：同一 EffectScript 实例，调用方对传入字典 `Clear()` 后重审，结果由 `False` 静默翻转为 `True`。注释（:60）宣称「构造即固定，使 EffectScript 为不可变值对象」。
- 判词：不可变性不是属性声明，是所有权声明。你把可变的 Dictionary 穿着 IReadOnlyDictionary 的外衣放进「纯数据契约」，等于把钥匙留在锁上然后宣布门是锁着的。
- 最小修复：构造器内 `caps.ToImmutableDictionary()`（或在 EffectScript/Budget 构造时快照拷贝）。

**F6 · `ReleaseApiTags` 三态语义：null=信任我，空=报错，非空=校验——没人能猜对这个表**
- 位置：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:31（`IReadOnlySet<string>? ReleaseApiTags = null`）；LoadValidation.cs:32–33,60
- 证据：LoadValidation.cs:32 `if (inv.ReleaseApiTags == null) continue;`（null ⇒ 整条双重释放防护跳过）；:33 `Count == 0 ⇒ throw`（空集合反而非法）；:60 又把 null||empty 并列处理。三种取值三种命运，其中「什么都不填」恰好是唯一不经过任何检查的通道。
- 判词：null 在这里的意思是「我保证没问题」，而空列表的意思是「你犯了错」——这两个概念在日常语言里几乎同义，在你们的 API 里却互为反面。这不是设计，是谜语。
- 最小修复：删掉 null 态。要么必填非空 tag 集，要么提供显式 `InverseClaim.UnvalidatedAction` 哨兵常量——「跳过校验」必须是一个喊得出名字的构造子，而不是缺席。

**F7 · GodotShell.Defer 的默认失败模式是静默吞噬**
- 位置：src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs:24（`if (_exitDraining) return;`）、:29–34（句柄不安全/IsSafeToInvoke 抛异常 ⇒ `safe=false` ⇒ action 不执行，无任何记录）
- 证据：三条丢弃路径（退出期、已 enqueue 重复、句柄失效）全部无声返回；FakeHost 也无丢弃计数。调用方无从得知自己的回调根本没跑。
- 判词：「省得用户操心」和「瞒着用户」的区别，就是一个 DroppedCallback 计数器的距离。
- 最小修复：暴露 `DroppedDeferCount` 或可选 `Action<Action> OnDrop` 钩子；至少 XML doc 明示三类静默丢弃条件（目前只有零散注释）。

**F8 · `Claim.CompatibleWith` 名字撒谎：只看 mode，其余三维装聋**
- 位置：src/Cosmos.EffectAlgebra/Objects.cs:138
- 证据：`CompatibleWith(other) => Compatible.IsCompatible(Mode, other.Mode)`。实测（探针 P6）：Kind.Read/Mem/Scene("A") 对 Kind.Write/Gpu/Method("M") 返回 `true`——kind、resource、scope 全不同的两条 claim「兼容」。
- 判词：方法名承诺的是关系判断，实现交付的是枚举查表。使用者不需要读实现就能预测行为？他预测不了，因为他会按名字预测。
- 最小修复：改名 `ModesCompatibleWith` 或补齐 `(resource, scope)` 维度的判断；二者都比现在的签名诚实。

**F9 · 属性逃逸通道的约束停在注释里**
- 位置：src/Cosmos.EffectAlgebra/EffectAttributes.cs:35（OverrideSize「负值非法…本层不重复校验」）、:56（AcceptDeviation ε<0.2「无意义 ⇒ 构造子发警告（此处仅注释…）」）
- 证据：实测（探针 P8）：`OverrideSize = -100.0` 属性层照单全收；`AcceptDeviationAttribute(0.0)` 构造成功、Epsilon=0、无任何警告机制存在。「编译警告由 L2/L3 触发」——即本层明知有坑，把填坑责任记在了别人名下。
- 判词：注释里写「非法」的地方，运行时都合法——那注释就不是约束，是忏悔。
- 最小修复：OverrideSize setter 或 EffectScript/L2 消费点拒绝 `<1`；AcceptDeviation 对 `<0.2` 直接 ArgumentException（0.0~0.2 区间没有任何合法用途，留着只是给 AI 一个猜错的机会）。

### LOW

**F10 · 测试旋钮混进生产 API 面**
- 位置：src/Cosmos.EffectAlgebra/Runtime/PluginRuntime.cs:21（`public bool IsShuttingDown { get; set; }`，注释自认「测试可置位以模拟关闭路径」）、:30（`public Action<Fiber>? OnSuspending { get; set; }`）、:36/:43（AccumulateNet 的「每 N 帧」节奏与 CheckPermanentFiberLeak(threshold) 阈值全部外泄给每个调用者）
- 判词：每一个 public set 都是一份永久邀请，邀请下一位集成者替框架做框架自己的决定。阈值这种东西一旦交给调用方，就再也不会有人统一它。
- 最小修复：IsShuttingDown 收窄为 internal + 测试用 InternalsVisibleTo；threshold/N 收敛为一个带默认值的 `LeakWatchOptions` 记录，集中一处。

**F11 · 白名单未命中 = 静默无保护：API 面的最贵承诺给了最弱默认**
- 位置：README.md:40（自认「未命中白名单的 API 静默无保护、无警告」）
- 判词：安全工具的第一美德是不装死。命中即保护、未命中即消失——这套白名单的默认语义是「我们不认识的东西不存在」。
- 最小修复：L2/L3 至少对未命中方法发 info 级提示（可配置抑制），把「不在册」变成可见事实而非宇宙背景。（注：README 已如实披露，故降 LOW；披露不等于豁免。）

---

## 对抗性推演实录（仓库外探针 `/tmp/eaa-probe`，引用仓库 DLL，未改源）

| # | 使用者省略/误用的东西 | 预期（游戏正确） | 实测结果 |
| --- | --- | --- | --- |
| P1 | C# `new EffectScript(events)` 省略 budget | 峰值 1e6 应触发告警 | `Passed=True, viol=0`；加 cap=64 后立即 `PeakExceeded` —— 同一副本，一次省略，闸门整体蒸发 |
| P2 | JSON 同时省略 `loop` 与 `size`（5000 粒子意图） | 峰值≈5000，cap=64 应拒绝 | `Passed=True`（按 1×[1,1] 审）；补回 `"loop":5000` ⇒ `Passed=False` —— 两个「合理」默认复合出数量级假绿 |
| P3 | C# `new Interval(Top, Top)` 作 lifetime（create-only 泄漏剧本） | 应拒绝或报警 | `Passed=True`（事件永不存活，静默出局）；同一形状 JSON 被拒 —— 约束只活在序列化层 |
| P4 | `Signature.Of(default(Claim))` | 应在构造点抛 | 入桶成功；`Peak.Compute` NRE / `NetTable` ArgumentNullException("key")，爆点远离现场 |
| P5 | 调用方持有传入 Budget 字典并事后 `Clear()` | 不可变对象审计结果应稳定 | 同实例两次 Audit：`False → True` —— 「不可变值对象」承诺被一次 Clear 击穿 |

## TOP-3

1. **F1 Budget 缺省=无上限**：峰值预算这道卖点是核心的闸门，其默认状态是断开的。AI 代理省略 budget 是概率上的必然，而省略必然得到全绿。
2. **F2 loop+size 复合缺省**：单个默认尚可辩解，两个默认相乘把并发密度压扁三个数量级且无任何痕迹——这是「程序员知道得越少越好」的反面教材：他知道得越少，审计结果错得越自信。
3. **F3 `[⊤,⊤]` 通道**：约束只在一层强制就等于没有强制。C# 类型层放行的非法区间让泄漏剧本在权威审计里安静地全绿，JSON 层的努力形同虚设。

## usability 裁决

这个系统的每一处省略都通向 fail-open：省 budget 得「无上限」，省 loop 得 ω=1，省 size 得精确 [1,1]，省 ReleaseApiTags（null）得免检，白名单未命中得无保护。对一个**以抓泄漏为使命**的库，全部默认值一致地偏向漏报而非误报——方向选反了。误报烦人一次，漏报杀人于无形。AI 编码代理面对这份 API 会怎么猜？它会猜最少按键的路径，而最少按键的路径恰恰是审计结果最不可信的路径。simple 的做法是把非法状态堵在类型里（F3/F4 各一行代码的事），把「无约束」变成必须显式说出的决定（F1/F6），而不是把它做成默认。

---

## 证据：本轮读取过的文件清单

- README.md（全文）
- EFFECT_SCRIPT.md（全文）
- src/Cosmos.EffectAlgebra/EffectAttributes.cs（全文）
- src/Cosmos.EffectAlgebra/EffectScript.cs（全文）
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs（全文）
- src/Cosmos.EffectAlgebra/Algebra.cs（全文）
- src/Cosmos.EffectAlgebra/ApiMapping.cs（全文）
- src/Cosmos.EffectAlgebra/Objects.cs（全文）
- src/Cosmos.EffectAlgebra/Numeric.cs（全文）
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs（全文）
- src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs（全文）
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs（全文）
- src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs（全文）
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs（头部 + EffectiveSignature 区域）
- samples/GodotIntegration/SampleGame.cs（全文）
- 未读取 audit/ 目录下任何历史审计报告（纪律遵守）。
