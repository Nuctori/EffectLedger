# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 9/10 轮 —— Approachability（用户与 AI 编码代理的第一小时）

视角：Rich Hickey（Simple Made Easy）。本轮只回答一个问题：**用户和 AI 是否可以简单、正确地使用这个框架进行游戏开发？**
审计纪律：未读取 `audit/` 下任何历史报告；所有结论基于本轮实测与 file:line 证据。探针工程位于仓库外 `/tmp/hickey-r9/`，未改动任何仓库源文件。

---

## 核实矩阵（入口文档自我声明的逐条裁决）

本轮被禁止读历史审计，故核实对象为三份入口文档（README / EFFECT_SCRIPT / DELIVERABLE）与样本注释中**对使用者可见的承诺**。

| # | 声明 | 位置 | 裁决 | 证据 |
|---|---|---|---|---|
| 1 | 「写一个含 acquire/release 的方法（**无需任何特性**…）」且配对后不报 | README.md:27 | **部分修/误导** | 实测 `AddChild+QueueFree` 平衡方法不报 EAA0901，但报 EAA0303；按 README.md:30-36 推荐配置 EAA0303=error ⇒ 正确用法直接编译失败。实证：`samples/AnalyzerConsumer/Game.cs:19` 与 GodotIntegration `HealthyEnemy.SpawnAndDespawn` 均触发 EAA0303 |
| 2 | 「未命中白名单的 API 静默无保护、无警告」 | README.md:40 | **真但不完整** | 只披露了漏保护方向；反方向（普通 .NET 方法名撞白名单 ⇒ 误报）未披露。实测 `Assembly.Load` 触发 6 处 EAA0901（GodotIntegration 测试代码），见新发现 F4 |
| 3 | 「dotnet test … 453 passed：297 单元 + 69 E2E + 87 Runtime」 | README.md:63 | **过期** | 本轮实测 306 + 91 + 69 = 466 passed, 0 failed |
| 4 | DELIVERABLE「212 用例」 | DELIVERABLE.md 头部测试段 | **过期** | 同上，EffectScript.Tests 已达 306 |
| 5 | EFFECT_SCRIPT §10.3「279 passed」 | EFFECT_SCRIPT.md §10.3 | **过期** | 同上 |
| 6 | 「AI 产出 EffectScript JSON → Parse → Audit，闭环无需运行游戏」 | EFFECT_SCRIPT.md:135-151, §4 | **前提不成立** | §4 官方示例 JSON **两次独立解析失败**（实测 FormatException），详见 F1 |
| 7 | 样本注释「[EffectOverride] ⇒ L3 不报 EAA0901」 | samples/GodotIntegration/SampleGame.cs:55、IntegrationTests.cs:71 | **误报（已证伪）** | 分析器对 EAA0901 永不豁免（EffectAlgebraAnalyzer.cs:136）；实测 TempHold 带 override 仍报 EAA0901；EndToEndTests.cs:178 断言的恰是相反行为 |
| 8 | 样本注释「acquire+release 在 Spawn/Despawn 中配对 ⇒ L3 不报 EAA0901」 | SampleGame.cs:18 | **字面为真、整体误导** | 确实不报 0901，但同方法报 EAA0303（推荐配置=error）；「不报」的承诺在推荐配置下不成立 |
| 9 | EFFECT_SCRIPT §2.1 `EffectEvent` 三属性（Lifetime/Footprint/Loop）、「5 字段位置记录」 | EFFECT_SCRIPT.md:46-62 | **已漂移** | 实现为 4 属性（多出必填 `Scope`，EffectScript.cs:28），构造子 3/4 参（EffectScript.cs:41-49）；doc 说 5 字段，代码注释说 4 字段（EffectScript.cs:19）。AI 按 doc 写 ⇒ CS1729（实测） |

---

## 第一小时走查记录（clone → 第一个能跑的效果）

以「不懂代数学的游戏程序员」和「只有 C#+Godot 先验的 AI 代理」两个角色走查：

1. **clone 后读 README「5 分钟上手」**：第一步要求在消费工程 `.csproj` 写指向 `..\src\...` 的相对路径 ProjectReference（README.md:11-15）。无 NuGet 包、无版本号、无 `dotnet add` 命令 —— 用户必须理解本仓库目录结构并把游戏工程摆放在正确相对位置。AI 代理会卡在这里：它无法从 README 推断出「仓库必须与游戏工程同级」。net10.0 目标框架也要求最新 SDK。
2. **第二步 .editorconfig**：README 诚实说明不设 error 则门禁失效（README.md:17-24）。步骤本身清晰 ✅。
3. **第三步写 SpawnEnemy**：照抄 README 步骤 3 的 AddChild+QueueFree 配对 —— 这是本轮实测的**第一个卡点**：EAA0303（推荐配置下 error）命中平衡生命周期，要求加 `[EffectOverride]`，与步骤标题「无需任何特性」直接矛盾（F5）。程序员此刻无从判断是自己的错还是工具的错。
4. **真实 Godot 工程接入**：samples 全部用 stub 类型（SampleGame.cs:2 明言非真 Godot SDK）；没有任何一份「真实 Godot.NET.Sdk 工程」的接入样例或说明。PDR/DELIVERABLE 承认本机未装 Godot（DELIVERABLE out-of-scope §13）。AI 无法验证真实工程下的行为差异。
5. **改走剧本 DSL 路线**（EFFECT_SCRIPT.md 承诺的主路线）：复制 §4 官方 JSON → `EffectScriptContract.Parse` → **FormatException「缺少字段: scope」**（实测 P1a）。补上 scope 再试 → **FormatException「resource.gpu 须为非空字符串」**（实测 P3）——因为文档把 resource 写成嵌套对象 `{"gpu":{"bufferId":...}}`，契约要求扁平字符串 `"gpu":"mesh1"`。两处错误都是文档自身埋的雷（F1）。
6. **修好 JSON 后审计**：官方示例报 `Leak`（实测 P6）——文档没有告知这个示例本来就是泄漏剧本还是应该全绿，AI 无从判断「这是预期失败还是我又错了」（F11）。
7. **改走 C# API 路线**：按 §2.1 文档形状 `new EffectEvent(interval, footprint)` ⇒ CS1729；`At(60)` ⇒ CS1503（int→NatStar 无隐式转换）（实测 docshape 探针）。文档通篇未展示 `NatStar.Of`/`ScopeId.Scene`/`Signature.Of` 的任何可运行调用片段 —— 用户被迫去读 Objects.cs/Numeric.cs 源码才能写出第一个合法构造（F6）。
8. **Runtime 层**：README 一句「预算-only 用户完全不必接触 Runtime」，但想用运行期权威闭合的游戏开发者没有任何 quickstart、没有 GodotShell 注册样本 —— 第二条主路线在第一小时是死路。

**结论性观察**：三条入口路径（L3 分析器 quickstart / JSON 剧本契约 / C# 剧本 API）各自在第一小时给出**与入口文档相反的反馈**。这不是能力问题——分析器诊断文案详细、FormatException 指名道姓、编译错误秒级——而是**文档作为地图把人引进了雷区**。

---

## 新发现

### F1 · CRITICAL — EFFECT_SCRIPT.md §4 官方 AI 契约示例无法解析（双重错误）
- 位置：EFFECT_SCRIPT.md:137-147 vs src/Cosmos.EffectAlgebra/EffectScriptContract.cs:57（event 必填 scope）与 ReqStr（resource 须扁平字符串，EffectScriptContract.cs 尾部 `ReqStr`）
- 实测：逐字节照抄 §4 JSON → `FormatException: 缺少字段: scope`；手工补 event 级 scope 后 → `FormatException: resource.gpu 须为非空字符串`（文档写嵌套对象 `{"gpu":{"bufferId":"mesh1"}}`:139，契约要求 `"gpu":"mesh1"`）
- 判词：你给 AI 的唯一数据契约，第一个用户就是你的文档自己——而它没通过。这比缺文档严重：AI 会忠实复制规范里的示例，然后在规范声称不存在的错误上打转。
- 最小修复：用 `EffectScriptContract.ToJson` 的输出反向生成 §4 示例（round-trip 已实测可用，P4），并加一条「此示例 Parse+Audit 的预期输出」断言进测试。

### F2 · CRITICAL — EAA0901 文案给出的修复方案 (2) 实际无效（永不豁免 vs 文案教用户加 [EffectOverride]）
- 位置：src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs:49（messageFormat 教「标 [EffectOverride("证据")]」）vs :136（`AnalyzeMissingRelease` 注释明言「EAA0901：永不豁免」且无条件调用）
- 实测：`SampleGame.cs:63` TempHold 带 `[EffectOverride]` 仍报 EAA0901；EndToEndTests.cs:178 也断言仍报
- 判词：错误信息里写着药方，吃了药方病不好——用户会得出「工具坏了」的结论，然后要么弃用要么开始无脑堆注解。文案与行为必须二选一对齐。
- 最小修复：若设计锁死「DO-9 永不豁免」，从 messageFormat 删掉方案 (2)，改为「如属误报（非 Godot 同名方法），见 <抑制指南>」；同时补上抑制机制（见 F4）。

### F3 · HIGH — 样本注释系统性教授错误心智模型
- 位置：samples/GodotIntegration/SampleGame.cs:18,55；IntegrationTests.cs:59,69-71
- 证据：两处声称「[EffectOverride] ⇒ 不报 EAA0901」「逃逸类不报」，实测全部仍报（见 F2）；IntegrationTests.cs:70 自己承认「具体诊断对象难精确匹配」于是放弃断言——注释与测试双双失守
- 判词：样本是用户抄的第一份代码，注释撒谎比代码出错贵十倍；测试作者知道断言不了，却留着那条谎言注释。
- 最小修复：改注释与实际行为一致（override 只豁免 A3/A4，不豁免 DO-9），并让 IntegrationTests 显式断言 TempHold 报 EAA0901。

### F4 · HIGH — 裸方法名匹配零接收者类型检查 ⇒ 普通 .NET/用户代码产生不可消除的误报
- 位置：EffectAlgebraAnalyzer.cs:290-301（`FindWhitelistEntry`：`c == full || c == method`，纯语法层，无 semantic model 接收者检查）
- 实测：GodotIntegration 测试代码中 6 处 `Assembly.Load(...)`（标准 .NET 反射）被当作 §7.4 Load 报 EAA0901「疑似资源泄漏」；同理任何名为 `Load/Connect/Play/AddChild/Rpc/MoveAndSlide` 的自定义辅助方法都会命中
- 判词：「非侵入式按名匹配」是把双刃剑磨尖了两头——Godot 用户少敲了类型判断，.NET 用户多了六个假泄漏警报，而且这些警报没有合法关闭方式（F2 复合：override 无效，唯一出路是改名或压制整个诊断 ID）。
- 最小修复：至少对接收者类型做语义检查（继承自 Godot.Node/Object 才匹配），或在诊断中提供 per-call-site 抑制码并写进文档。

### F5 · HIGH — 推荐配置下，README 官方「正确用法」自身触发 error 级 EAA0303，要求加它刚说过不需要的特性
- 位置：README.md:27（「无需任何特性」）vs README.md:32（EAA0303=error）；实证 AnalyzerConsumer/Game.cs:19（Balanced.Lifecycle，AddChild+QueueFree）报 EAA0303；GodotIntegration HealthyEnemy.SpawnAndDespawn 同样中招
- 根因：AddChild 白名单 Claims 同时含 Write+Occupy（ApiMapping.cs:66-69），QueueFree 亦然（ApiMapping.cs:72-76）；跨两个调用站点 kind 集 {Write,Occupy} 数量 >1 即报（EffectAlgebraAnalyzer.cs:238-252）⇒ **每一个教科书式 spawn/despawn 方法必然报 KIND_MIX**
- 为何测试没抓住：EndToEndTests.cs:47-49 的 BalancedSource 给 AddChild/RemoveChild 都标了 `[EffectOverride]` ⇒ hasValidOverride=true ⇒ EffectAlgebraAnalyzer.cs:137 直接跳过 A3/A4 检查。活样本恰好绕过了自己要证明的路径。
- 判词：最讽刺的一种 easy——平衡写法在警告级看着一切正常，一开推荐门禁就满屏红，逼每个老实配对的用户都去学逃逸通道。守恒的好公民被当成嫌疑人盘问。
- 最小修复：A3 只在 kind 多样**且 mode 冲突**时报，或把 create/release 配对模式（同一资源既有 Create 又有 Release 且数量平衡）列为白名单豁免形态。

### F6 · MED — 文档-代码形状漂移：§2.1 缺 Scope 字段，「5 字段」vs 实现 4 字段
- 位置：EFFECT_SCRIPT.md:46-62（三属性代码块、宣称「5 字段位置记录」）vs EffectScript.cs:19-37（4 属性含必填 Scope、注释称「4 字段」）
- 实测：按文档形状构造 ⇒ CS1729（无 2 参构造）；`At(60)` ⇒ CS1503（int→NatStar 无隐式转换，Numeric.cs 全文无 implicit operator）
- 判词：反馈回路倒是快（秒级编译错），但每一步都在罚认真读文档的人。文档展示的类型签名就是 API 契约，漂移即违约。
- 最小修复：§2.1 代码块换成含 Scope 的真实签名，附一段 10 行内可编译的最小 C# 用例（含 NatStar.Of / ScopeId.Scene / Signature.Of）。

### F7 · MED — JSON 契约资源词汇表与 §7 白名单不相交：场景树（游戏最核心资源）在 AI 契约里不存在
- 位置：EffectScriptContract.cs `ParseResource`（仅 gpu/commandBuffer/memory/occupancy/signalBus 五种）vs ApiMapping.cs:66-190（Tree/Self/Disk/AudioMixer/Callback/Network/Input 共 12 种）
- 实测：`{"tree":"player"}` ⇒ FormatException「resource 须含 gpu/commandBuffer/memory/occupancy/signalBus 之一」（P5）
- 判词：C# 世界里 AddChild 占用的头号资源是场景树节点，JSON 世界里它不存在——同一个代数，两种方言，词汇表却不通婚。「AI 产出剧本」的远景在这道缝上是断的。
- 最小修复：ParseResource 增加 tree/self/disk 等判别分支（复用 ResourceId 既有构造子即可，零新增代数）。

### F8 · MED — EAA0303/EAA0304 把 API 名当资源名显示，归因信息失真
- 位置：EffectAlgebraAnalyzer.cs:218（`resourceLabel[key] = m.Value.GodotApi;`）→ 实测文案「对同一资源（AddChild）混用了多类效应」
- 判词：诊断让用户去找一个叫「AddChild」的资源——世上没有这种资源。归因错误的错误信息比没有信息更糟，因为它自信地指错了方向。
- 最小修复：resourceLabel 改存 `ResourceId.Normalize(...)` 的规范化资源描述。

### F9 · LOW — 入口文档测试计数三处互相矛盾且全部过期
- 位置：README.md:63（453）、DELIVERABLE.md（212）、EFFECT_SCRIPT.md §10.3（279）；实测 466
- 最小修复：计数改为 CI badge 或删除具体数字。

### F10 · LOW — 典型游戏场景样本覆盖为零
- 位置：全仓 grep buff/debuff/装备/光环/aura 无命中（仅 spawn/despawn/pool 场景）；EffectScript JSON 契约除 tests 外无任何可运行 sample；Runtime 层无接入样本
- 判词：一个「游戏数值效果代数」的样本库里没有一个 buff。第一小时的用户只能靠想象把 spawn/despawn 类推到装备词条上——而类推正是这个框架声称要用类型系统消灭的东西。
- 最小修复：补一个「攻击力 buff（create@t0 / release@t_end）叠加防 debuff」的最小剧本 sample + 一份预期 AuditResult 快照。

### F11 · LOW — 官方示例本身是泄漏剧本，且未标注预期审计结果
- 位置：EFFECT_SCRIPT.md:135-151；实测修好形状后 `Audit()` 报 `Leak: t=180 生命周期未闭合（净效应不含 0）：[1,1]`（P6）
- 判词：示例可以是反例教学，但必须挂牌子；现在它是无牌反例，AI 会把它当正例回修到自己怀疑人生。
- 最小修复：§4 加一行「此示例故意含 gpu 泄漏，预期 Violations=[Leak@180]」。

### 值得肯定的（对抗审计也要说公道话）
- FormatException 文案普遍指名道姓（字段名 + 期望形状，如 `loop 必须 ≥1（0 无意义）或 "⊤"`，实测 P7 秒级回路）✅
- `[⊤,⊤]` lifetime 被 fail-fast 拒绝并有理由说明（EffectScriptContract.cs ParseInterval）✅
- EAA0801/EAA0802 编译期校验属性参数（属性 ctor 运行期才执行的坑被诚实处理，EffectAlgebraAnalyzer.cs:53-56）✅
- ToJson→Parse round-trip 实测通过（P4），Global scope 序列化修复有效 ✅

---

## TOP-3（本轮最重要）

1. **F1 — 官方 AI 契约示例解析失败（双重）**：框架的核心卖点「AI 产 JSON → 不跑游戏即审计」在其唯一的规范示例处断裂。修复成本最低、收益最大。
2. **F2+F4 — EAA0901 的不可消除误报 + 自相矛盾的修复指引**：`Assembly.Load` 实测 6 连报，文案教的 `[EffectOverride]` 药方无效。用户/AI 的理性结局只有一个：全局压制诊断 ID，门禁形同虚设。
3. **F5 — 推荐配置惩罚教科书式正确用法**：平衡 spawn/despawn 必报 EAA0303(error)，与「无需任何特性」承诺互斥；测试因给 helper 标了 override 而恰好盲测。这是把 simple 做成 hard 的标本。

---

## Usability 裁决

**问：用户和 AI 能否简单、正确地使用这个框架进行游戏开发？**

**答：当前不能——不是数学不行，是第一小时的三条入口各自在第一步给出与文档相反的反馈。**

- 数学内核（NatStar/Interval/Signature/sweep-line Audit）经本轮 round-trip 与审计行为抽查，语义自洽、fail-fast 纪律良好；
- 但入口文档（README 步骤 3、EFFECT_SCRIPT §2.1/§4）与实现漂移到「照文档写必错」的程度；样本注释主动教授已被测试证伪的行为模型；
- L3 分析器存在结构性误报源（裸名匹配）+ 不可豁免 + 文案误导三者叠加，使「把 EAA 设为 error」的推荐配置在真实工程中等价于「每个正常生命周期都要写一次逃逸注解」；
- AI 可用性专项结论：AI 能从 FormatException/CS 错误秒级回修（回路长度合格），但会被 F1/F6 引导着在错误目标上收敛；JSON 契约词汇表缺口（F7）则让它根本无法表达典型游戏场景（场景树占用）。

修复优先序：F1/F11（半天）→ F2/F3/F8 文案对齐（半天）→ F5 语义修正 → F4/F7 结构补齐。完成前四项后，本框架才配得上它 README 里那句「5 分钟上手」。

---

## 证据清单（本轮实际读取/执行）

**读取的文件**
- README.md、EFFECT_SCRIPT.md、DELIVERABLE.md
- samples/GodotIntegration/SampleGame.cs、SampleGame.csproj、IntegrationTests.cs（部分）
- samples/AnalyzerConsumer/Game.cs、AnalyzerConsumer.csproj
- src/Cosmos.EffectAlgebra/EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs、ApiMapping.cs、Numeric.cs（API 面扫描）
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs（全文分段）
- tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs
- .editorconfig

**执行的命令/探针**
- `dotnet build Cosmos.EffectAlgebra.slnx`（0 err / 0 warn）
- `dotnet test Cosmos.EffectAlgebra.slnx`（306+91+69 = 466 passed / 0 failed）
- `dotnet build samples/GodotIntegration -t:rebuild`（18×EAA0901 + 2×EAA0303 实证，含 Assembly.Load 误报与 TempHold/SpawnAndDespawn 行为）
- `dotnet build samples/AnalyzerConsumer -t:rebuild`（Game.cs:10 EAA0901、Game.cs:19 EAA0303 实证）
- 仓库外探针 `/tmp/hickey-r9/probe`：官方 §4 JSON 解析（P1a/P3/P6）、round-trip（P4）、tree 资源拒绝（P5）、loop=0 文案（P7）、空 footprint（P8）
- 仓库外探针 `/tmp/hickey-r9/docshape`：文档形状 C# 构造（CS1729/CS1503 实证）
