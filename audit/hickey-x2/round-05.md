# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 5 轮 —— Maybe Not（可选性、默认值与约束设计）

- 视角：Rich Hickey（Simple Made Easy）· 独立上下文，未读取 audit/ 下任何历史报告
- 日期：本轮（round 5/10）
- 方法：只读源码 + 行级证据 + 「省略可选参数」路径推演

> 总判词：这个库嘴上说「类型即边界、构造即全必填」，却在四个关键位置留了 `?` 和 `= default`——每一个都是「写代码的人觉得合理」的默认：省略 size 得到精确 [1,1]、省略 budget 参数得到「不检查」、null 标签得到「信任我」、缺 type 的 scope 得到 Scene。**可选性不是便利，是把决策推给最不了解全局的那个人（AI 代理）再替他选一个错答案。**

---

## 核实矩阵（历史公开结论逐条裁决）

历史结论取自 README「已知语义锐边」与 EFFECT_SCRIPT.md 内嵌注记（按任务纪律未读 audit/ 目录）。

| # | 历史结论 | 裁决 | 证据 |
|---|---|---|---|
| 1 | 「Unknown 模式 = 最弱兼容 = fail-open：未知资源冲突被静默放行」（README 锐边 §3.2.3 P4） | **仍在** | Algebra.cs:14 `Resolve(m) => m == Mode.Unknown ? Mode.Use : m`；Algebra.cs:19 `if (aa == Mode.Use \|\| bb == Use) return true` ⇒ Unknown×Create/Release 全放行。另见新发现 L12：Algebra.cs:10 注释自称「fail-closed」与行为相反 |
| 2 | 「loop:⊤ 居民层被静默豁免泄漏检测；lifetime:[1,⊤] 仍入 net 并报警——同一常驻语义两种相反行为（MA-002）」 | **仍在（部分修）** | EffectScript.cs:161 `if (enter && !e.Loop.Count.IsTop)`——ω=⊤ 事件完全跳过 gate(1) 守恒；EffectScript.cs:284 `if (e.Loop.Count.IsTop) continue;` 闭包豁免；而 gate(2) 峰值仍计入（EffectScript.cs:185）。行为差异依旧存在且靠注释解释，非类型区分 |
| 3 | 「Claim.Size 省略 ≠ 未知：?? [1,1]（精确 1，既非未知 ⊤ 也非 0 预算）（§3.1.5a）」 | **仍在，且危害被低估** | Objects.cs:126 `Interval? Size`；Objects.cs:133 `Size = Size ?? Interval.Default`；Numeric.cs:96 `Default = new(Of(1), Of(1))`。这不是「锐边」，是给 AI JSON 生产者的头号陷阱（见 TOP-3 / F1） |
| 4 | EFFECT_SCRIPT.md §2.3：「Budget 缺省 = 该资源无上限（⊤）……不写进代数内核」 | **误报（已偏离设计）** | 设计说 Budget 是软壳；实现里它成了 `Audit(Budget cap)` 的必经参数且资源不在 Caps 即静默跳过（EffectScript.cs:236），同时剧本还挂着第二个 Budget 属性（EffectScript.cs:61）——一个语义两处存放，见 F2 |

---

## 新发现

严重级：HIGH = 会静默改变审计结论；MED = 静默削弱保护或可表示非法状态；LOW = API 面污染/文档谎言。

### HIGH

**F1 · 省略 size ⇒ 静默按「恰好 1」记账，预算审计形同虚设**
- 位置：src/Cosmos.EffectAlgebra/Objects.cs:126,133；src/Cosmos.EffectAlgebra/EffectScriptContract.cs:133；src/Cosmos.EffectAlgebra/Numeric.cs:96
- 证据：`Claim(... Interval? Size)` → `Size = Size ?? Interval.Default`；JSON 解析 `c.TryGetProperty("size", out var sz) ? ParseInterval(sz) : Interval.Default`；`Default = [1,1]`。
- 判词：null 在这里的意思不是「未指定」，而是「我替你发誓它是 1」。EFFECT_SCRIPT.md §4 的示例 JSON 每条 claim 都带 `"size": [1,1]`，等于教 AI：size 是可选装饰。一个 5000 并发粒子的 footprint 省掉 size，扫换线就按 1 记峰值——审计通过，游戏爆显存。**把「未知」表达成「精确已知的最小值」，是 fail-open 披着类型的外衣。**
- 最小修复：契约层（ParseClaim）强制要求 size 字段缺失即 FormatException；L1 层把 `Interval?` 改为必须显式选择 `Exact/Dynamic/Unknown` 三构造子之一，删掉 `?? Default`。

**F2 · Budget 双真源 + 「不在 Caps ⇒ 不检查」：省略一个参数，峰值门整体消失**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:61,64-68,105,236,99
- 证据：剧本构造时存 `Budget`（:61,:67），但权威检查用 `Audit(Budget cap)` 的参数（:105,:236 `foreach (var kv in cap.Caps)`）；参数化调用完全无视剧本自带预算；gate(2) 只遍历 `cap.Caps` 中列出的资源（:236），未列出者零检查。
- 判词：同一个事实（预算上限）有两个存放点、三个入口（构造参数、`Audit()` 无参重载、`Audit(cap)` 重载），其中「省事」的那个入口会静默关闭整道门。测试文件自己就在示范危险用法：`new EffectScript(events).Audit(Budget.None)`（tests/EffectScriptContractTests.cs:45）——换成真实使用者，这就是「我以为有预算」和「其实没人查」的分界线。
- 最小修复：删掉 `Audit(Budget cap)` 参数化重载，只留 `Audit()` 用构造期预算；要对比多预算就显式 `script.WithBudget(b).Audit()`。一个正解优于两个入口。

**F3 · `default(Budget)` 与 `Budget.None`：「无预算」的两种表示，一种 NRE 一种通过**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:67 vs :105/:236 与 :345
- 证据：构造子防御了 null Caps（:67 `budget.Caps != null ? budget : Budget.None`），但 `Audit(Budget cap)` 直接 `foreach (var kv in cap.Caps)`（:236）——传入 `default(Budget)`（record struct 合法值）即 NullReferenceException。
- 判词：同一条语义边界，在一个构造子里设了岗，在另一个公共方法里撤了哨。null 在这里是「未初始化的 struct」还是「显式无」？类型系统不知道，只有崩不崩知道。
- 最小修复：`Budget` 构造子拒绝 null caps（抛），或 Audit 入口 `cap.Caps ?? Budget.None.Caps` 归一；更彻底：Caps 类型改 `ImmutableDictionary` 非空。

### MED

**F4 · ReleaseApiTags 三态：null=信任、空=报错、非空=校验——null 与空语义相反**
- 位置：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:26（`IReadOnlySet<string>? ReleaseApiTags = null`）；src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs:32（`if (inv.ReleaseApiTags == null) continue; // 显式 opt-out`）vs :60（空集合抛异常）
- 证据：如上三行连读。`null` 跳过全部双重释放防护并自称「自证安全」；`[]` 反而是装载错误；`{...}` 才走白名单校验。
- 判词：用一个引用类型的三个状态编码三种信任级别，还要靠注释才能读懂——这是「程序员知道得越少越好」的反面教材。逆回放是本框架最危险的通道，它的守卫却以「什么都不写」为最强通行证。
- 最小修复：砍成两态——`ReleaseApiTags` 必填非空集，裸 Action 用具名构造子 `InverseClaim.Unverified(action)` 显式声明逃逸，让 opt-out 在代码里可见、在 code review 里可见。

**F5 · JSON scope 缺 `type` ⇒ 静默降级 Scene；Method/Type 序列化时名字塞进 `"scene"` 键**
- 位置：src/Cosmos.EffectAlgebra/EffectScriptContract.cs:92-95（缺 type 默认 `new ScopeId.Scene(name)`）、:202-203（`ScopeId.Method m => { ["type"]="method", ["scene"]=m.Name }`）
- 证据：如上。手写 JSON `{"scene":"Boss"}` 与 round-trip 产物 `{"type":"method","scene":"Boss"}` 是不同 ScopeId；gate(3) 按 `(resource, ScopeId, mode)` 分组（EffectScript.cs:181 `var key = (r, e.Scope, (int)c.Mode)`），Scene("Boss") 与 Method("Boss") 永不入同组。
- 判词：「字段名说谎」是最便宜也最长久的纠缠——`scene` 键装着 method 名，缺一个可选字段就从方法作用域静默变成场景作用域，create×create 冲突随之漏检。AI 代理面对示例只会写短形态，于是永远拿到降级语义。
- 最小修复：Parse 要求 `type` 必填（Scene 也要显式 `"type":"scene"`）；序列化键改名 `name`。schema 里不留「省略也行」的形状。

**F6 · `Defer(Action)` 单参重载：省略 handle = 免检 use-after-free**
- 位置：src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs:17（`public void Defer(Action action) => Defer(action, null);`）、:36（`safe = handle == null || IsSafeToInvoke(handle)`）
- 证据：handle==null 时 `IsSafeToInvoke` 短路为真，回调无条件执行。
- 判词：注释里大书「fail-closed：句柄失效即丢回调」，但默认重载传的就是 null——**保护的默认值是不保护**。顺手（easy）的那条路恰恰是埋雷的那条路。
- 最小修复：删单参重载；确无原生句柄时要求显式 `Defer(action, GodotShell.NoHandle)`，把「我确认无需门控」变成可 grep 的声明。

**F7 · `[EffectOverride].OverrideSize` 是 `double?` 且零校验，非法状态可表示**
- 位置：src/Cosmos.EffectAlgebra/EffectAttributes.cs:33-37
- 证据：注释原文「负值非法……调用方须保证 ≥1，本层不重复校验（L1 仅承载属性数据）」。
- 判词：同一文件的 `AcceptDeviationAttribute` 构造子肯为 ε∈[0,0.5] 抛异常（EffectAttributes.cs:66-70），OverrideSize 却把约束写成注释。约束表达力在同一对逃逸通道属性上分裂——一处 fail-closed，一处「碰运气」。size=-3 的 override 一路流到 L2 展开成 [-3,-3]，谁消费谁倒霉。
- 最小修复：构造/setter 处校验 `value >= 1` 否则抛，与 Epsilon 同规格。

**F8 · EffectiveSignature 折入的 create/release Claim 全部 size=null ⇒ 按 [1,1] 做 net 闭合判定**
- 位置：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:56,60（`new Claim(Kind.Occupy, ..., null)` 两处）→ Objects.cs:133 膨胀为 [1,1]
- 证据：如上链路。§5 装载闸门 VerifyNetClosure（LoadValidation.cs:79-88）据此判定生命周期闭合。
- 判词：provider 实际提供 ω 份并发占用，闭合闸门却按「恰好借 1 还 1」判平账——守恒检查的输入被默认值预先洗白了。
- 最小修复：折入时要求 Coeffect 携带规模（Provides 数量），或至少复用 Effect 中同名资源的 size。

**F9 · 白名单哨兵 `Memory(0)` / `AudioMixer(0)`：占位符冒充身份**
- 位置：src/Cosmos.EffectAlgebra/ApiMapping.cs:39（`static ResourceId Mem() => new ResourceId.Memory(0); // 真实 UID 由映射层运行时填入`）、:44（AudioMixer(0) 同款）
- 证据：Load/Preload/Instantiate/QueueFree 的内存 Claim 全部坍缩到 uid=0 同一桶（ApiMapping.cs:80,109,112 等）；「运行时填入」在本仓库内找不到任何填充点。
- 判词：默认值写了 0，承诺写在注释里，兑现遥遥无期——所有内存操作共享一个假身份，net 守恒在桶间互相抵消或互相污染。
- 最小修复：要么让 L2 Generator 真注入实例 uid，要么把哨兵改成显式 `ResourceId.Memory.Sentinel` 并在文档承认聚合语义，别让注释背债。

### LOW

**F10 · `IsShuttingDown` 公共可写 setter：测试旋钮暴露在生产 API 面**
- 位置：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:20-21
- 判词：「测试可置位以模拟关闭路径」——测试需求长进了生产接口，任何人一行代码就能伪造关机态绕过 RecomputeTopology 守卫。
- 修复：internal set + [FriendAccess] 或测试专用子类。

**F11 · AccumulateNet/CheckPermanentFiberLeak 协议纯靠自觉：喂帧频率与阈值全无约束**
- 位置：src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs:38-52（「每 N 帧」的 N 未定义；threshold 由每次调用随手给）
- 判词：宿主忘了喂 ⇒ 泄漏盲区（本特性要解决的那个盲区）原样回归，且无任何信号。
- 修复：阈值作为 PluginRuntime 构造参数固定；喂帧计数器内置。

**F12 · Compatible 注释自称「fail-closed 最弱兼容」，实际是 fail-open 最宽兼容**
- 位置：src/Cosmos.EffectAlgebra/Algebra.cs:10-11 vs :14,19
- 判词：Unknown→Use→与一切兼容，是能想到的最宽语义；给它贴 fail-closed 标签，读者会带着错误的信任模型写代码。README 倒是诚实（「fail-open」），两处自相矛盾。
- 修复：统一措辞为 fail-open，或在 Resolve 处真的 fail-closed（Unknown⇒不兼容）二选一。

**F13 · `Budget.None` 包的是可变 Dictionary**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:348（`new(new Dictionary<ResourceId, NatStar>())`）；`new Budget(dict)` 接受任意外部可变字典（:345）
- 判词：全库标榜不可变值语义（auditR5 F1 还专门修过 init 可变问题），Budget 却留着活引用后门——构造后再改字典，同一次 Audit 结果随外部状态漂移。
- 修复：构造时 `ToImmutableDictionary` 快照。

---

## 对抗性推演（使用者省略可选参数 → 静默异变）

**推演 A（F1+F8 复合）：AI 写粒子剧本省略所有 size**
1. AI 参照 EFFECT_SCRIPT.md §4 示例产出 JSON：5000 个粒子 event，footprint 仅 `{kind:"occupy", resource:{memory:42}, mode:"create", scope:{"scene":"Battle"}}`，无 `size` 字段。
2. ParseClaim（EffectScriptContract.cs:133）静默填 `Interval.Default=[1,1]`——没有任何警告。
3. `Audit()` 扫换线：每事件净贡献 ±1，release 配平 ⇒ gate(1) 通过；峰值累计 = 事件重叠数×1 而非 ×5000 ⇒ 只要没写 budget 或 budget ≥ 重叠数，gate(2) 通过。
4. 结论：`Passed=true`，游戏运行时 OOM。**审计工具亲手签发了它本该拦住的爆炸。**

**推演 B（F2）：构造时给了预算，审计时省了参数**
1. 团队按「预算附着在剧本上」的心智模型写：`var s = EffectScriptContract.Parse(jsonWithBudget64);`
2. 另一位开发者（或 AI 从测试样例模仿）调用 `s.Audit(Budget.None)`——编译通过、语义合法。
3. gate(2) 遍历 `Budget.None.Caps`（空表）：循环体零次执行，PeakExceeded 结构性不可能出现（EffectScript.cs:236）。
4. 违例清单里峰值项凭空消失，`Passed` 可能由 false 翻成 true。**同一个剧本、两次审计、两种结论，差异藏在一个被省略的参数里。**

**推演 C（F5）：scope 少写一个字段，冲突检测换目标**
1. AI 为 Boss 战写两层特效：背景层 `{"lifetime":[0,300],"scope":{"scene":"Boss"},footprint:[create gpu mesh]}`；前景层 `{"lifetime":[10,200],"scope":{"type":"method","scene":"Boss"},footprint:[create gpu mesh]}`——或者反过来，AI 直接抄 SerializeScope 的输出格式但漏了 `"type"`。
2. Parse 把漏 type 的那条解析成 `ScopeId.Scene`，另一条是 `ScopeId.Method`（EffectScriptContract.cs:94-95）。
3. gate(3) 分组键含 ScopeId 相等性（EffectScript.cs:181）：两条 create 永远不同组 ⇒ `CompatibleConflict` 不报。
4. 若两条都带有限 size 且无 release，Leak 仍会报（跨 scope 聚合于归一化资源）——但冲突诊断消失，AI 回修循环拿到的是误导性的残缺反例。

---

## TOP-3

1. **F1 — 省略 size ⇒ 静默 [1,1]**：整个审计体系的地基参数有一个「省略即最小」的默认，AI 生产者是主要用户，这是系统性漏报的根源。
2. **F2 — Budget 双真源 + 不在 Caps 不查**：一道安全门的开关被做成可选参数，省略它门就不存在；且库里自己的测试在示范危险姿势。
3. **F5 — scope 缺 type 静默降级 + scene 键名说谎**：可选字段改变了作用域偏序这一核心语义，冲突检测目标随之漂移，且序列化格式主动诱导该错误。

## 可用性裁决（usability_verdict）

不通过。该库的类型纪律（readonly record struct、构造即全必填）是真的，但它止步于「字段齐了」，没管「值从哪来」：四条主要数据通路（JSON 契约、Attribute 属性、Fiber 折入、白名单哨兵）各自引入一个「写的人觉得合理」的默认，且全部朝「更容易通过审计」的方向倾斜。对一个宣称「AI 产 JSON → 自动审计闭环」的系统，默认值就是 AI 的决策——而这些默认全是替 AI 选的错答案。

---

## 证据：本轮读取过的文件

- README.md
- EFFECT_SCRIPT.md
- src/Cosmos.EffectAlgebra/EffectAttributes.cs
- src/Cosmos.EffectAlgebra/EffectScript.cs
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs
- src/Cosmos.EffectAlgebra/Algebra.cs
- src/Cosmos.EffectAlgebra/Objects.cs
- src/Cosmos.EffectAlgebra/Numeric.cs
- src/Cosmos.EffectAlgebra/ApiMapping.cs
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs（节选 grep）
- src/Cosmos.EffectAlgebra/Deviation.cs（节选 grep）
- src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs
- src/Cosmos.EffectAlgebra.Runtime/PluginRuntime.cs
- src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs
- samples/GodotIntegration/SampleGame.cs
- tests/Cosmos.EffectAlgebra.Tests/EffectScriptContractTests.cs、EffectScriptEdgeTests.cs（grep 取证）
