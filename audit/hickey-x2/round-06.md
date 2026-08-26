# Cosmos.EffectAlgebra 对抗性审计 · 第 6/10 轮 · Naming & Hammock-Driven Development（Rich Hickey 视角）

> 独立上下文，未读取 `audit/` 下任何历史审计报告。核实矩阵仅针对**代码与入口文档内嵌的历史结论**（README「已知语义锐边」、EFFECT_SCRIPT.md 的 OPEN-*/auditR* 注记）逐条裁决。
> 本轮唯一问题：**名字是 API 的第一道规格**。一个只看签名 + README 的开发者（或 AI 代理），能否不读实现就预测行为？

---

## 核实矩阵（历史结论逐条裁决）

| # | 内嵌历史结论 | 出处 | 裁决 | 证据 |
|---|---|---|---|---|
| V1 | 「Unknown 模式 = 最弱兼容 = fail-open：未知资源冲突被静默放行」 | README.md:56 | **属实，且代码注释自我矛盾**：行为确为 fail-open（Unknown→Use→兼容→放行），但 Objects.cs:114 与 Algebra.cs:10 均注释为「fail-closed 最弱兼容」。同一行为两份相反的定性，fail-open/fail-closed 词汇在库内已漂移 | Algebra.cs:18-25；Objects.cs:114 |
| V2 | 「loop:"⊤" 居民层被豁免泄漏检测；lifetime:[1,⊤]（ω 有限）仍入 net」 | README.md:57 | **仍在（设计锁死）**：gate(1) 仅对 `!e.Loop.Count.IsTop` 累积 net（EffectScript.cs:158）；hi=⊤ 的事件仍进入 sweep 并累积 net（EffectScript.cs:139 `hi = lt.Hi.IsTop ? ulong.MaxValue : ...`）。行为与描述一致 | EffectScript.cs:139,158 |
| V3 | 「Claim.Size 省略 ≠ 未知：?? [1,1]」 | README.md:58 | **属实/已修**：`Normalize()` 中 `Size ?? Interval.Default`，显式 Exact(0) 经可空类型区分不被膨胀 | Objects.cs:143-147 |
| V4 | OPEN-N2：ω 从「时间循环次数」重解释为「瞬时并发副本数」，"建模约定：时长→Lifetime，并发密度→LoopCount"，维护者须避免混淆 | EFFECT_SCRIPT.md §1.1 注记 | **部分修**：文档警示到位，但类型名仍叫 `LoopCount`、属性名仍叫 `Loop`——名字本身就在教用户犯文档警告的那个错。详见 F6 | DerivedMetrics.cs:8-24；EffectScript.cs:29-31 |
| V5 | OPEN-B4：`[⊤,⊤]` Interval 合法构造但事件永不存活（fail-open 不审计） | EFFECT_SCRIPT.md 边界注记 | **属实（锐边仍在）**：构造子仅拒 `lo=⊤ ∧ hi 有限`（Numeric.cs:83-84），`[⊤,⊤]` 可构造；Alive 判定 Lo=⊤ 恒假（EffectScript.cs:323-324）。文档已诚实标注，属设计选择而非漂移 | Numeric.cs:83-84；EffectScript.cs:323-324 |
| V6 | auditR4 CRITICAL：Global scope round-trip 必炸 → 已修 | EffectScriptContract.cs:89 注释 | **已修**：SerializeScope 输出 `{"type":"global"}`（EffectScriptContract.cs:204），Parse 接受 `"global"` 且不再强制 scene 键（EffectScriptContract.cs:88-100） | EffectScriptContract.cs:88-100,204 |
| V7 | reviewer LOW：scope.type 拼错（如 "gloabl"）须抛而非静默当 Scene | EffectScriptContract.cs:92 注释 | **已修**：switch default 抛 FormatException（EffectScriptContract.cs:95-103） | EffectScriptContract.cs:95-103 |
| V8 | auditR5 F1：Budget 由 init 属性改构造参数固化，EffectScript 成不可变值对象 | EffectScript.cs:63 注释 | **部分修**：构造路径已固化（EffectScript.cs:64-68），但 `Budget` 是无守护的 record struct——`default(Budget).Caps == null`，任何绕过 EffectScript 构造器直接持有默认 Budget 的调用点都会 NRE（EffectScript.cs:339-348） | EffectScript.cs:64-68,339-348 |
| V9 | auditA OPEN-1：Event 自带 Scope，消除自由变量 loopScope | EffectScript.cs:20 注释 | **已修**：`EffectEvent.Scope` 存在并作为 At/Audit 的 scope 来源 | EffectScript.cs:22-24 |

---

## 新发现

### HIGH

**F1 — 入口 JSON 契约示例照抄必炸：文档教的第一个调用就是错的**
- 位置：EFFECT_SCRIPT.md:139 vs src/Cosmos.EffectAlgebra/EffectScriptContract.cs:158,158-166,193
- 证据：§4 示例写 `"resource": {"gpu": {"bufferId":"mesh1"}}`（嵌套对象），而 `ParseResource` 对 gpu 取值执行 `ReqStr(gpu,"gpu")`，要求 `ValueKind == String` 且非空（EffectScriptContract.cs:193-196）。按官方文档产出的第一份剧本，`Parse` 直接抛 FormatException。
- 判词：吊床测试在第一级台阶就摔死人——AI 只看文档写 JSON，100% 写出非法输入；这不是边界情况，这是文档里的**主示例**。
- 最小修复：把 §4 示例改为扁平字符串形状 `{"gpu":"mesh1"}`，或让 Parse 接受嵌套形状；并在 §4 补一张 resource/scope 形状速查表。

**F2 — `CompatibleWith(Claim)` 名为 Claim 级判定，实为纯 Mode 比较**
- 位置：src/Cosmos.EffectAlgebra/Objects.cs:137
- 证据：`public bool CompatibleWith(Claim other) => Compatible.IsCompatible(Mode, other.Mode);` —— 完全不看 Resource、不看 Scope、不看 Kind。两条 Create×Create 的 Claim，哪怕作用于**完全不同的 GPU buffer**，也返回 false（不兼容）。
- 判词：签名承诺「这两条 Claim 相容吗」，实现回答「这两个 mode 相容吗」。名字撒了谎，使用者必须读实现才能知道要自己先比对资源——这正是「程序员知道得越少越好」的反面。
- 最小修复：改名 `ModeCompatibleWith` 或补全资源/scope 判定；二选一，别让名字比行为宽。

**F3 — `RecomputeTopology()` 是公共 no-op：名字承诺重算，身体什么都没做**
- 位置：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:157-162
- 证据：方法体只有关路径守卫 throw + 一行注释「非关路径分支当前为 no-op 占位……真实重算须由宿主在 DependencyGraph 调用方实现」。
- 判词：一个名为 Recompute 的公共方法不 recompute，调用者（尤其 AI 生成的调度代码）会真心相信拓扑已被更新而跳过自己的重算逻辑。空实现可以接受，但至少名字得承认现实（如 `EnsureNoShutdownTopologyChange`）或直接删掉。
- 最小修复：改为内部私有接缝或更名；在 summary 首行用 `<remarks>` 标注 no-op，而非埋在第 160 行注释里。

**F4 — 「峰值」有两个互斥定义：`Peak.Compute` 含 read/write 桶，`Audit` gate(2) 仅 occupy 桶**
- 位置：src/Cosmos.EffectAlgebra/Algebra.cs:114-120 vs src/Cosmos.EffectAlgebra/EffectScript.cs:172
- 证据：`Peak.Compute` 遍历 `sig.AllClaims()`（三桶全含，Algebra.cs:117），凡非 Release 一律累加 size.Hi；扫换线 Audit 的 gate(2) 只遍历 `e.Footprint.OccupyClaims`（EffectScript.cs:172）。同一 Signature 同一 scope，两个「峰值」可以差出一个 read claim 的量。白名单里 `Load` 带 `Rd(Mem(), Use, Shell(), Dynamic)`（ApiMapping.cs:108）——Dynamic=[1,⊤] 会让 Peak.Compute 直接返回 ⊤，而 Audit 可能安然通过。
- 判词：领域词 "Peak" 在同一个程序集里承担两种量纲。EFFECT_SCRIPT.md:11 说峰值审计是核心卖点，核心卖点的度量却没有单一真源。
- 最小修复：`Peak.Compute` 收敛到 occupy-only（与 §3.3.2 公式和 Audit 对齐），read/write 桶另立名字。

### MED

**F5 — `Coeffect`：把一张普通的 Requires/Provides 表冠以范畴论黑话**
- 位置：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:21
- 证据：`public sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope);`
- 判词：coeffect 在 FP 文献里有精确含义（程序对环境的依赖），这里实际是「资源契约/依赖声明」。游戏开发者查遍 Godot 文档也找不到这个词；三个字段名已经把话说完了，类型名却在炫耀出处。概念税收到最贵的一档：**读者必须先学一个错误暗示的术语才能学真正的模型**。
- 最小修复：更名 `ResourceContract` / `ProvidesRequires`。

**F6 — `LoopCount` 名实不符：名字是 loop，文档明令禁止按 loop 理解**
- 位置：src/Cosmos.EffectAlgebra/DerivedMetrics.cs:8-24；EffectScript.cs:29-31
- 证据：类型名 LoopCount、属性名 `EffectEvent.Loop`；而 XML 注释与 EFFECT_SCRIPT.md 反复强调 ω 是「同一时刻并发副本数，非时间重复」（EffectScript.cs:29-30）。
- 判词：需要一整段注释来抵消自己名字的第一含义，这个名字就是负资产。AI 见 `Loop` 会去配 lifetime 循环语义，见 JSON `"loop": 50` 会以为是播放 50 次。
- 最小修复：更名 `Concurrency` / `Replicas`（JSON 键同步改或加别名）。

**F7 — 公共 `Rid`/`StringName` 与 Godot 类型同名**
- 位置：src/Cosmos.EffectAlgebra/Objects.cs:10,13
- 证据：`public readonly record struct Rid(string Value); public readonly record struct StringName(string Value);` —— 游戏工程必然 `using Godot; using Cosmos.EffectAlgebra;` 双开，同名类型产生歧义编译错误或被迫别名消歧。
- 判词：「内部原语别名」却设成 public，把映射层的实现细节变成了消费方的命名冲突。API 面积是最贵的承诺，这两个名字承诺的是永久的歧义。
- 最小修复：更名 `RidRef`/`StringNameRef` 或降为 internal。

**F8 — `ReleaseApiTags` 三态语义（null/空集/非空集）不可从签名预测**
- 位置：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:27-30；LoadValidation.cs:32-38
- 证据：`IReadOnlySet<string>? ReleaseApiTags = null`；null = 显式 opt-out 放行，空集 = 抛 LoadValidationException，非空 = 逐个校验 ∈ ReleaseClass。三种行为，签名上只看得出一种类型。
- 判词：null 和 empty 在 C# 里长得几乎一样，语义却是放行 vs 拒载——非法状态不仅可表示，还恰好是最容易无意构造出的那个。
- 最小修复：拆成 `ReleaseApiTags`（非空必填）+ bool `BareActionOptOut`，或空集即 opt-out，消灭中间态。

**F9 — 白名单自身 scope 用词分裂：Load 的内存 create 挂 Global，QueueFree 的内存 release 挂 Shell，二者在 ⊆* 下永不可配对**
- 位置：src/Cosmos.EffectAlgebra/ApiMapping.cs:109,117 vs :80；Objects.cs:101-107
- 证据：`Load`/`Preload` 的 `Oc(Mem(), Mode.Create, Global(), Dynamic)`（:109,:117）；`QueueFree` 的 `Oc(Mem(), Mode.Release, Shell(), Dynamic)`（:80）。`IncludedIn` 只有同标签相等或目标是 Global 才为真——Shell 永远收不到 Global 的 create，net 守恒对这对 API **结构性不可能成立**。同一张表内 Instantiate 用 Shell（:114）、Load 用 Global，无一处解释何时该用哪个。
- 判词：连框架作者自己在同一页纸上都不能一致使用 Shell/Global，凭什么要求用户猜对？这不是用户的理解问题，是词汇表没有定义。
- 最小修复：统一 Mem 占用的 scope 口径，或在 ApiMapping 头部给出 Shell vs Global 的判定规则。

**F10 — 序列化把 Method/Type scope 的名字塞进 `"scene"` 键**
- 位置：src/Cosmos.EffectAlgebra/EffectScriptContract.cs:199-205
- 证据：`ScopeId.Method m => { ["type"]="method", ["scene"]=m.Name }`、`ScopeId.Type t => { ["type"]="type", ["scene"]=t.Name }`。round-trip 能过（Parse 读 scene 键当 name），但 wire 格式上 method 的名字叫 "scene"。
- 判词：round-trip 正确 ≠ 契约正确。AI 或第三方按直觉写 `{"type":"method","method":"Pickup"}` 会得到 Method("")——静默错值，比炸掉更糟。
- 最小修复：统一键为 `"name"`（scene 场景下兼容旧键读取）。

**F11 — `Violation.Kind` 是四个魔法字符串，违反自家口号「类型即边界」**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:376-377
- 证据：`/// 违例类型：Leak | NegativeDip | PeakExceeded | CompatibleConflict。 public string Kind { get; }`
- 判词：整个库的注释都在念「非法状态不可表示」「enum 保证穷举」（Objects.cs:112），到了对外最重要的诊断出口却让 AI 对字符串做匹配——大小写、拼写全靠约定。
- 最小修复：改 enum，Detail 保留人话。

**F12 — `ValidateScaleClosure` 校验的是 Scope 相等，与 scale 无关**
- 位置：src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs:18-24
- 证据：方法体逐条比较 `inv.Scope != fiber.Scope` 抛「scale 不闭合」。没有任何缩放/系数计算。
- 判词：从论文术语直译过来的名字，把「作用域闭合」叫成了「尺度闭合」。读签名的人会去找 scale 参数——找不到，因为根本没有。
- 最小修复：更名 `ValidateInverseScopeClosure`。

**F13 — `Signature.Join` 文档声称 merge_I 合并，实现是纯并集**
- 位置：src/Cosmos.EffectAlgebra/Objects.cs:190-193 vs :172-186
- 证据：`/// §3.2.4 ⊔：join-semilattice 合并（幂等/交换/结合，非半环）。同 Claim 取 size merge_I。` 但 `Join(a,b) => Union(a,b)`，而 Union/Add 全程只有集合 Add（结构相等去重），不存在任何区间 Merge 调用——size 不同但其余相同的两条 Claim 会作为两个元素共存于桶中。
- 判词：Join 和 Union 两个名字、一行转发、一份兑现不了的前置注释。双名同义诱导使用者臆造差异；错误的注释比没有注释更贵。
- 最小修复：删掉 Join 或修正注释；若确需 merge_I 语义就真的实现它。

### LOW

**F14 — SampleGame 把逃逸通道当正常用法教学，且暴露按名匹配的撞车面**
- 位置：samples/GodotIntegration/SampleGame.cs:22-26,36-40；README.md:40
- 证据：HealthyEnemy 给自己的包装方法贴 `[EffectOverride]` 以换取 L2 生成（:22-26），四个样例类全部如此；同时 L2 按 Canonical 方法名匹配白名单——用户自定义 `public void AddChild(...)`（:24）与引擎 `AddChild` 规范化后同名，会被当作引擎调用审计。
- 判词：样例是隐形的规范。它教会 AI 两件错事：① override 是接入方式而非例外通道；② 只要方法名取得像引擎 API 就会被纳入审计——而 README:40 还承诺「自定义操作需扩展白名单」，没提名字撞车这条暗道。
- 最小修复：样例增加一个「无 override 的正常配对」类；文档明确同名方法的消歧规则。

**F15 — `DeviationVal.ExceedsThreshold` 对 ⊤ 返回 false，注释称「避免掩盖」**
- 位置：src/Cosmos.EffectAlgebra/Numeric.cs:128；Deviation.cs:51-52
- 证据：`=> !IsTop && Value > threshold`。⊤（不可校准）⇒ 不报警。
- 判词：返回 false 就是掩盖，注释说避免掩盖。诚实的设计应把「不可校准」做成第三种结果，而不是塞进布尔里假装通过。
- 最小修复：提供 `DeviationStatus { Ok, Exceeds, Uncalibratable }` 或至少在调用方文档标红。

**F16 — `Fiber` 与既有并发术语正面相撞**
- 位置：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:34
- 证据：五态状态机 + 逆回放的插件组件，命名为 Fiber；与 OS/Godot 语境的 fiber（协程）零关系。
- 判词：借了一个所有人都「认识」的词来表达一个没人猜得到的东西，比生造词更危险——生造词让人去查文档，熟词让人自信地用错。
- 最小修复：`PluginUnit` / `EffectFiber`→`Lease` 之类，或者干脆 `Plugin`。

**F17 — 杂项命名/漂移合集**
- `IsShuttingDown { get; set; }` 公共可写，注释自认「测试可置位」（PluginRuntime.cs:20-21）——测试接缝不该是公共 API 面。
- `AccumulateNet` 要求调用方喂「绝对值」，前提只活在注释里（PluginRuntime.cs:32-39）；传带符号值的调用方会让正负泄漏互相抵消，恰好骗过 `CheckPermanentFiberLeak`（:42-48）。
- `SetProcessMode(FiberId id, bool disabled)`（IHost.cs:14）布尔否定参数，Godot 的 ProcessMode 本是枚举，true=disable 的读向靠记忆。
- EFFECT_SCRIPT.md:62 称 EffectEvent 为「5 字段位置记录」且代码块缺 Scope 字段（实际 4 字段，EffectScript.cs:19）；EFFECT_SCRIPT.md:67 称 `record struct EffectScript`（实际 sealed partial class，EffectScript.cs:55）；EFFECT_SCRIPT.md §10.3 测试数 279 与 README.md:63 的 453 并存——三处漂移说明入口文档没有与代码同步的机制。

---

## 误写推演：AI 编码代理只凭签名与文档

**误写 A（F1，直接命中）**：代理读 EFFECT_SCRIPT.md §4，产出：
```json
{ "kind": "occupy", "resource": {"gpu": {"bufferId": "mesh1"}}, "mode": "create", "scope": {"scene":"Battle"}, "size":[1,1] }
```
`EffectScriptContract.Parse` → `ReqStr(gpu)` → FormatException："resource.gpu 须为非空字符串"。代理看到报错信息提到 gpu，多半会把嵌套对象改成字符串 `"mesh1"` 碰运气通过——但它无从得知 `{"commandBuffer":"gpu"}` 里那个字符串其实是**通道名**而非缓冲 id（SerializeResource 侧 CommandBuffer 序列化 Channel，EffectScriptContract.cs:220），于是 budget 键 `commandBuffer:mesh1` 与 footprint 的 `commandBuffer:gpu` 指向两个不同 ResourceId，峰值预算静默失效——不报错，也不设防。

**误写 B（F2）**：代理实现「同帧不允许两个系统都创建同一张纹理」：
```csharp
bool ok = spawnFx.CompatibleWith(cacheWarmup);   // 两条 Create，不同 Gpu 资源
if (!ok) queueRetry();
```
`CompatibleWith` 只比 mode ⇒ Create×Create ⇒ false ⇒ 两个毫无冲突的资源操作被永久拒绝。代理调试时看不到任何资源信息，只能删掉检查或全局放宽——两种修法都比原 bug 更糟。

**误写 C（F4+V1 组合）**：代理做预算门禁：
```csharp
if (NatStar.Top.Equals(Derived.Peak(atT, scene))) approveAsUnknown(); // 学自 Unknown=fail-open 先例
else if (Derived.Peak(atT, scene).CompareToFinite(cap) > 0) reject();
```
因 `Load` 白名单注入的 `Rd(Mem(),…,Dynamic)`，`Peak.Compute` 恒返回 ⊤（Algebra.cs:118-119），门禁永远走 approveAsUnknown 分支——代理复用了库里自己示范的 fail-open 直觉，而真正权威的 `Audit()`（occupy-only）此时可能正在拒绝同一剧本。两套「峰值」+ 一套「fail-open 先例」，合力产出了一个永远绿灯的门禁。

---

## TOP-3

1. **F1 — 官方 JSON 示例与 Parse 实现形状冲突**：入口文档的第一个完整示例无法通过入口解析器。吊床测试当场不及格。
2. **F2 — `CompatibleWith(Claim)` 名实不符**：签名层面最危险的谎言——跨资源 Create×Create 被判不兼容，且无任何报错提示名字撒了谎。
3. **F4 — "Peak" 双定义**：核心卖点度量存在两套不一致口径，配合库内 fail-open 文化可直接推导出永远放行的预算门禁。

---

## usability 裁决

**不合格（fail）**。这个库的类型层纪律（readonly record struct、构造即全必填、⊤ 显式载体）明显高于平均水平——但命名层完全没有受到同样的纪律约束：`Coeffect`、`LoopCount`、`ValidateScaleClosure`、`RecomputeTopology`、`CompatibleWith` 各自在不同的地方向读者许诺错误的东西；同一概念（峰值、净额、acquire/release/create/release-class、Shell/Global）各有两到三套并行词汇；而入口文档的主 JSON 示例与解析器互斥。简单性的敌人从来不是复杂的问题，而是每一个需要读实现才能澄清的名字。当前状态下，「不读实现就能正确使用」这一条，对人类和 AI 均不成立。

---

## 证据：本轮读取过的文件

- README.md（全文）
- EFFECT_SCRIPT.md（全文）
- src/Cosmos.EffectAlgebra/Objects.cs、Algebra.cs、Numeric.cs、SignedNet.cs、DerivedMetrics.cs、Deviation.cs、EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs、ApiMapping.cs（全文）
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs、Fiber.cs、DependencyGraph.cs、InverseReplay.cs、GodotShell.cs、IHost.cs、LoadValidation.cs、NetBenefitClosure.cs、ProviderCrashCascade.cs（全文）
- samples/GodotIntegration/SampleGame.cs（全文）

*注：任务书中的 `src/Cosmos.EffectAlgebra/Runtime/` 实际位于 `src/Cosmos.EffectAlgebra.Runtime/`，已全部覆盖。*
