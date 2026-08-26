# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 10/10 轮 —— Final Verdict（Rich Hickey 视角）

> 独立上下文收官轮：未读取 audit/ 下任何历史报告。全部结论基于本轮实际通读的源码、文档与**实测运行**（dotnet build / dotnet test / 独立探针程序）。
> 核心问题：用户和 AI 是否可以简单、正确地使用这个框架进行游戏开发？

---

## 一、跨维度总裁决（1–10 分）

| 维度 | 分 | 判词 |
| --- | --- | --- |
| 正确性保障 | **6** | 类型即边界做得认真（溢出⇒⊤、lo≤hi 构造强制），但三条 fail-open 通道（白名单未命中、`[⊤,⊤]` 寿命、Unknown mode）让「非法状态」不仅能表示，还能**静默通过审计拿绿灯**。 |
| 可推理 | **5** | 值语义载体无可挑剔；但 `Peak.Compute` 把 GPU+音频+树节点搅成一个标量（实测 108，见 F4）、`NetTable.Get` 与 `IsConserved` 对同一问题给出相反答案（F7）——不看实现无法预测行为。 |
| 上手成本 | **4** | 「5 分钟上手」文档诚实，但第一小时内会连撞三堵墙：`Audit(default)` 抛 NRE（F1）、自己写的 `Load()` 方法被当成资源加载（F3）、用了白名单 API 却永远收到守恒误报（F5）。 |
| 组合性 | **7** | `Signature.Union`/`Combination.Loop`/`EffectScript.At` 是纯函数组合值语义，这是全框架最 Hickey 的部分；半格律有 1000 组性质测试背书。 |
| 扩展性 | **5** | ResourceId/ScopeId 开放判别联合利于扩展；但自定义 API 要改源码重建白名单（README.md:40 自认），名字规范化匹配天然无法承载参数化调用。 |
| AI 友好度 | **6** | JSON 契约 + `Violation(t, resource, scope, kind, detail)` 反例回修闭环是对 AI 最友好的形状；但 fail-open 洞意味着 **AI 可以产出「全绿但零验证」的剧本**——对最大的目标用户群体而言，静默假绿比报错危险一个数量级。 |

---

## 二、核实矩阵（对本项目自述声明的核实，非历史审计）

本轮禁读历史审计，改为逐条核实项目自身声明：

| 项目声明 | 出处 | 裁决 | 证据 |
| --- | --- | --- | --- |
| 「0 警告 0 错误构建门禁」 | DELIVERABLE.md:12-16 | **已核实（成立）** | 本轮 `dotnet build Cosmos.EffectAlgebra.slnx --nologo` → 0 警告 0 错误 |
| 「212 测试绿」 | DELIVERABLE.md:14 | **漂移（部分修/过期）** | 本轮实测 298 + 91 + 69 = **458 passed / 0 failed**；DELIVERABLE 数字停在早期迭代 |
| 「453 passed：297 单元 + 69 E2E + 87 Runtime」 | README.md:63 | **漂移** | 实测单元已 298、Runtime 已 91；自述数字落后于代码，三处文档三个版本 |
| 「未命中白名单的 API 静默无保护、无警告」 | README.md:40 | **仍在（诚实承认但未解决）** | EffectAlgebraAnalyzer.cs:290-320 按裸方法名 Canonical 匹配；这是安全产品的默认姿态 = fail-open |
| 「`[⊤,⊤]` 视为非法输入而非错误项」 | EFFECT_SCRIPT.md:60 | **文实不符（声称非法，实际放行）** | Numeric.cs:80 构造子接受 `[⊤,⊤]`；EffectScriptContract.cs:63-84 解析不拒；实测探针 P2：该事件解析成功且 `Audit Passed=True` |
| 「Unknown 按 Use 处理（fail-closed 最弱兼容）」 | Objects.cs:47-49 注释 | **注释自相矛盾** | Algebra.cs:15-18 + 实测 P5：`Compatible(Unknown, Create)=True`——这是 **fail-open**（静默放行），注释却自称 fail-closed |

---

## 三、新发现

### HIGH

**F1 · 公共 API 的毒默认值：`Audit(default(Budget))` 必炸 NRE**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:105（`Audit(Budget cap)`）、:236（`foreach (var kv in cap.Caps)`）；Budget 为 record struct，`default(Budget).Caps == null`
- 判词：构造函数在 :52-55 特意把 null Caps 归一为 `Budget.None`，却在更常用的公共重载里把同一个雷原样留给调用者——你修了门上的锁，窗户敞着。结构体默认值是 C# 送上门的非法状态，类型没拦住它。
- 实测：探针 P1 `script.Audit(default(Budget))` → `NullReferenceException`。
- 最小修复：`Audit` 入口一行 `cap = cap.Caps is null ? Budget.None : cap;`，并给 `Budget` 加 `Init` 时的不变式。

**F2 · `[⊤,⊤]` 寿命静默通过 AI 契约：审计绿灯可能是零验证的假绿**
- 位置：EFFECT_SCRIPT.md:60（明言「视为非法输入」）vs Numeric.cs:80-96（构造子接受 `[⊤,⊤]`）vs EffectScriptContract.cs:63-84（`ParseTop` 对 lo="⊤" 不拒绝）
- 判词：文档说这是非法输入，实现却说「欢迎」。Lo=⊤ 的事件永不存活，于是 create-without-release 的泄漏剧本在端点采样下**真空通过**——AI 回修循环最怕的就是这种「改对了没有反馈信号」的静默面。
- 实测：探针 P2 —— 该 JSON 解析成功，`Audit Passed=True, violations=0`，尽管 footprint 含无配对 release 的 occupy-create。
- 最小修复：`ParseEvent` 对 `lifetime.Lo.IsTop` 抛 `FormatException`；顺带修正 Numeric.cs:86-87 注释与 EFFECT_SCRIPT.md 的矛盾。

**F3 · 安全产品自身的默认姿态是 fail-open：白名单按裸方法名匹配，双向误伤**
- 位置：EffectAlgebraAnalyzer.cs:290-320（`FindWhitelistEntry`/`RawName` 仅取 InvocationExpression 的标识符文本做 Canonical 匹配）；ApiMapping.cs:58-60；README.md:40
- 判词：用户自己的存档方法叫 `Load()` 就会被记一笔「磁盘读取+内存占用」；而 Godot 里真正的高频泄漏源（属性赋值、`ResourceLoader.Load` 经成员访问链、Tween、Timer）因不是裸标识符调用而**静默无保护**。一个以「抓泄漏」为卖点的工具，漏报时一声不吭——这不是保护，是安慰剂加副作用。
- 最小修复：(a) 未命中白名单时发一条低严重级 informational 诊断（可一键关），把「静默」变成「可见的选择」；(b) 白名单键带接收者类型限定。

### MED

**F4 · `Peak.Compute` 跨资源混成一个标量——量纲纠缠的活标本**
- 位置：src/Cosmos.EffectAlgebra/Algebra.cs:114-127（对所有非 release claim 的 size.Hi 无分组累加）
- 判词：GPU buffer 100 + 音频通道 7 + 默认 read size 1 = 108（探针 P3）。这个数字不能和任何单一资源的预算比较，也不能除以任何东西解释——它把三种量纲搅成一锅，然后交给用户。扫换线 `Audit` 内部明明已经实现了正确的每资源峰值，公共入口却留着这个误导版本。
- 最小修复：返回 `IReadOnlyDictionary<ResourceId, NatStar>`，或标记 obsolete 指向 `NetTable` 风格的分组结果。

**F5 · 白名单自带配对永远无法证明守恒：Dynamic 尺寸资源系统性误报**
- 位置：ApiMapping.cs:107-110（`Load` ⇒ `Oc(Mem(), Create, Global(), Interval.Dynamic=[1,⊤])`）、:77-82（`QueueFree` ⇒ `Oc(Mem(), Release, Shell(), Dynamic)`）；Algebra.cs:96-104（任一端 ⊤ ⇒ `IsConserved=false`）
- 判词：官方白名单教你的 Load↔QueueFree 配对，net 恒为 `[⊤,⊤]`（探针 P4），守恒判定恒假。用户照文档做对每一件事，仍然天天收报警——报警疲劳的终点就是全员贴 `[EffectOverride]`，闸门烂掉。fail-closed 用在这里等于把警报器调成常鸣。
- 最小修复：Dynamic 尺寸的 net 采用「区间算术保守化」之外的第三态（「未知-需人工」单独 violation kind），或对同资源 create/release 数目做符号计数闭合而非区间含 0。

**F6 · 注释与行为相反：「fail-closed 最弱兼容」实为 fail-open**
- 位置：Objects.cs:47-49（注释称 Unknown 按 Use 处理为 fail-closed）vs Algebra.cs:15-18 + 探针 P5（`Compatible(Unknown, Create)=True`，冲突静默放行）
- 判词：README 把它列为「设计锁死非 bug」，我尊重这个决定；但把 fail-open 行为标注成 fail-closed 的注释是在撒谎，下一个维护者会信错注释。
- 最小修复：改注释（一处字符串的事），并在 CompatibleMatrix 测试里固化语义命名。

### LOW

**F7 · 同一问题两个公共谓词答案相反**
- 位置：Algebra.cs:91（`Get(r)` 对缺失键返回 `Zero`，`Zero.ContainsZero==true` 即「守恒」）vs :96-104（`IsConserved(r)` 对缺失键返回 false）
- 判词：问 `net` 「r 守恒吗」，`Get(r).ContainsZero` 说是，`IsConserved(r)` 说否——用户不需要读实现就能预测行为的承诺在这里破了。
- 最小修复：`Get` 缺失键抛异常或返回 `SignedInterval?`。

**F8 · Runtime 公共面的占位与测试替身**
- 位置：PluginRuntime.cs:157-161（`RecomputeTopology()` 公共方法是 no-op 占位）、:21（`IsShuttingDown { get; set; }` 公共可变全局旗标，排空后复位允许关机中再装载）、GodotShell.cs:71（`FakeHost` 测试替身随生产程序集发布）
- 判词：一个名字承诺重拓扑、身体什么都不做的公共方法，是最贵的谎言——API 面积永久，行为临时。
- 最小修复：RecomputeTopology 改 internal 或改名 `ReserveRecomputeTopology`；FakeHost 挪到测试包。

**F9 · 自述数字三处漂移**
- 位置：DELIVERABLE.md:14（212）、README.md:63（453=297+69+87）、本轮实测（458=298+91+69）
- 判词：文档数字落后于代码不算罪，但三份文档三个版本，说明没有一处「单一真相」——你们给资源占用建了归一化单点真相，却没给自己的测试计数建。
- 最小修复：CI 生成测试计数注入 README。

---

## 四、TOP-3 危险点（真实游戏项目中最可能造成事故）

1. **F2 假绿审计**：触发条件＝AI 产出含 `[⊤,⊤]` 寿命的剧本（对「常驻 UI 层」这类需求，LLM 有天然倾向写出它）；后果＝显存/回调泄漏剧本全绿过审直接进管线，审计层形同虚设；最小修复＝契约解析拒绝 Lo=⊤（两行代码）。
2. **F5 守恒常鸣误报**：触发条件＝团队按 README 上手、使用白名单推荐配对（Load/Instantiate + QueueFree）；后果＝持续误报→批量 override→真泄漏被淹没在噪音里；最小修复＝引入「未知-需人工」独立 violation kind。
3. **F1+NRE 毒默认值**：触发条件＝任何人在脚本热更/编辑器工具里写 `script.Audit(new Budget())` 之外的自然写法 `default` 或从配置反序列化出空 Budget；后果＝运行时崩溃而非编译期错误；最小修复＝Audit 入口归一 null Caps。

## 五、TOP-3 值得保留的优点

1. **值语义载体纪律**（Numeric.cs / SignedNet.cs）：`readonly record struct` + 构造子不变式 + 溢出⇒⊤保守闭包 + 全程零 NaN；PropertyTests.cs 以 1000 组随机输入验证结合/交换/幂等律。这是「类型能约束的绝不用注释」的正确示范。
2. **反例驱动的审计产物**（EffectScript.cs:338-360 `Violation(t, resource, scope, kind, detail)` + 扫换线 O(E·K·log E) 与暴力参考实现的集相等断言）：机器可回修的反例 + 性能与等价性双重证明，是给 AI 用的正确形状。
3. **诚实边界文化**（README「已知语义锐边」节、DELIVERABLE out-of-scope 清单、EFFECT_SCRIPT §7）：即便我有异议的 fail-open 决策也被摆上台面而非藏起来——这比多数项目强；问题只在于个别注释与行为脱节（F6）。

## 六、最终裁决

**落档：(b) 修完 P0 后可用。**

理由：
- L1 纯代数核心（Numeric/SignedNet/Objects/Algebra 的值语义部分）现在就可以给团队用——数学认真、类型边界扎实、性质测试充分；
- 但作为安全产品，它的三条 fail-open 通道（F2/F3/F6）与两条误报机器（F5/F4）意味着**当前默认配置下的绿灯与红灯都不可信**：绿灯可能是零验证假绿，红灯可能来自官方推荐的正确用法。「错误输入能否表达」这一题，框架目前答的是「能，而且能通过」；
- Runtime 层（Fiber/PluginRuntime）是带占位公共方法的 MVP，不建议进真实项目；
- P0 清单：F1（一行修复）、F2（两行修复）、F3(a)（一条 informational 诊断）。三者皆小修，修完即可升入 (b)+；若再解决 F5 的 Dynamic 守恒语义，L3 分析器才第一次值得设成 CI 门禁 error。

---

## 证据：本轮读取过的文件

- 文档：README.md、EFFECT_SCRIPT.md、DELIVERABLE.md（PDR_Effect_Cost_Algebra_v3_FINAL.md 目录级扫描，未逐行引用）
- L1 全部 11 文件：Numeric.cs、Objects.cs、Algebra.cs、SignedNet.cs、DerivedMetrics.cs、Deviation.cs、ApiMapping.cs、EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs（csproj 略）
- Runtime 全部 10 文件：PluginRuntime.cs、Fiber.cs、DependencyGraph.cs、GodotShell.cs、IHost.cs、InverseReplay.cs、LoadValidation.cs、NetBenefitClosure.cs、ProviderCrashCascade.cs、SanityTests 定位用 csproj 略
- Analyzer/Generator 关键段：EffectAlgebraAnalyzer.cs:101-330、EffectAlgebraGenerator.cs:106-107
- samples：GodotIntegration/SampleGame.cs、AnalyzerConsumer/Game.cs
- tests 抽样：EndToEndTests.cs（全文）、PropertyTests.cs:1-80、Runtime SanityTests.cs、各测试 [Fact] 计数
- 运行验证：`dotnet build Cosmos.EffectAlgebra.slnx`（0 警告 0 错误）；`dotnet test`（458 passed / 0 failed）；独立探针程序 5 项断言（P1-P5，仓库外 temp 目录，未触碰项目源文件）
