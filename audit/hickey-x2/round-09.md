# Round 09 — Approachability 对抗性审计：用户与 AI 的第一小时（Rich Hickey 视角）

> 审计对象：Cosmos.EffectAlgebra（Godot/C# 效果代数框架）
> 核心问题（用户原话）：**用户和 AI 是否可以简单、正确地使用这个框架进行游戏开发？**
> 纪律：只读；未读取 audit/ 下任何历史报告；每条 finding 带 file:line 证据；关键结论经实际编译执行验证。

---

## 一、「第一小时」走查记录

### 路径 A：预算/泄漏检查（README 主推的「5 分钟上手」）

| 步骤 | 动作 | 需要的前置知识 / 隐藏步骤 | 卡点 |
| --- | --- | --- | --- |
| A1 | clone 后读 README §5分钟上手 | 需理解 L1/L2/L3 分层表；「EAA\*」「DO-9」「§7 白名单」等 PDR 内部术语未解释即使用 | 中：术语墙 |
| A2 | 消费工程挂 Analyzer+Generator ProjectReference（README:11-14） | 需知道 Roslyn analyzer 的 `OutputItemType="Analyzer"` 接法——README 未给，只在 SampleGame.csproj:47-52 有注释版接法 | 低 |
| A3 | 复制 .editorconfig 把 EAA 设为 error（README:18-25） | 需知道默认只是 Warning（EffectAlgebraAnalyzer.cs:53 `defaultSeverity: Warning`），不设则门禁形同虚设 | 低（README 已说明） |
| A4 | 写 acquire/release 方法（README:28-36） | **样例 `var node = AddChild(...)` 在真 Godot 里编译不过**：`Node.AddChild` 返回 void。AI 照抄必炸。且 DELIVERABLE 承认从未对真实 Godot SDK 测过（§13 out-of-scope），SampleGame 用 stub `Node3D`（SampleGame.cs:6-11）——第一小时跑在假 API 上 | **高** |
| A5 | 收到 EAA0901 警告后按文案修复 | 文案给的修复(2) `[EffectOverride("证据")]` **实际无效**（见 H1）。反馈回路永不闭合 | **致命** |
| A6 | 自定义资源操作 | README:40 自认「未命中白名单静默无保护无警告」——诚实，但用户无从得知哪些命中了 | 中 |

**卡住游戏程序员的一步**：A5——警告文案承诺的官方逃生门是死路。
**卡住 AI 代理的一步**：A4——README 样例代码在真 Godot 类型上不可编译 + 白名单按方法名匹配产生的误报无法归因（H3）。

### 路径 B：EffectScript 剧本审计（EFFECT_SCRIPT.md 主推的「AI 闭环」）

| 步骤 | 动作 | 隐藏步骤 / 卡点 |
| --- | --- | --- |
| B1 | 读 EFFECT_SCRIPT.md §4 JSON 契约 | 文档宣称「AI 只产出剧本数据」 |
| B2 | 把文档 JSON 喂给 `EffectScriptContract.Parse` | **没有可运行入口**：仓库无 CLI/dotnet tool/控制台样本，必须自己写 C# harness 引用 L1 DLL。samples/ 下 EffectScript 用法为零，唯一「活样本」在 tests/ |
| B3 | Parse | **实测失败两连**：逐字复制文档 §4 JSON → `FormatException: 缺少字段: scope`（EffectScriptContract.cs:57）；补上事件级 scope 再试 → `FormatException: resource.gpu 须为非空字符串`（EffectScriptContract.cs:158）——文档资源形状是嵌套对象 `{"gpu":{"bufferId":"mesh1"}}`，解析器只收字符串 `{"gpu":"mesh1"}`。旗舰示例连闯自家两道门 |
| B4 | Audit | 修正形状后再跑：报 `Leak t=180 res=Gpu(mesh1)`（gpu buffer create 后无 release）——文档没说这个示例本该失败，AI 无法判断是示例错还是自己错 |
| B5 | 回修 JSON 重投 | Violation 文案质量尚可（时刻/资源/净值齐全），此环节是全仓库反馈回路最健康的一段 |

**卡住双方的一步**：B3——权威契约文档与实现漂移，且无任何可运行入口兜底。

### 结论速记

框架的真实业务是**资源守恒审计**（GPU/内存/场景树占用），不是任务口径所说的「游戏数值效果」：buff/debuff/装备/光环在全仓库零类型、零样本、零测试（grep 全仓无命中）。第一小时的预期管理从名字就开始失真。

---

## 二、核实矩阵（对 README/EFFECT_SCRIPT/SampleGame 内置声明的裁决）

| # | 声明 | 出处 | 裁决 | 证据 |
| --- | --- | --- | --- | --- |
| 1 | 「Unknown 模式 = fail-open：未知冲突静默放行」 | README.md:56 | **部分修（表述矛盾仍在）** | 行为一致但标签相反：Algebra.cs:10,15 与 Objects.cs:96 写「Unknown 按 Use 处理（fail-closed 最弱兼容）」。同一行为两个相反名字，读者只能靠猜 |
| 2 | 「未命中白名单的 API 静默无保护、无警告」 | README.md:40 | **仍在（by design，已诚实标注）** | EffectAlgebraAnalyzer.cs:289-300 `FindWhitelistEntry` 返回 null 即 continue |
| 3 | 「`Claim.Size` 省略 ≠ 未知：?? [1,1]」 | README.md:58 | **已修且属实** | Objects.cs:139-142 `Size ?? Interval.Default` |
| 4 | 「[EffectOverride] ⇒ L3 不报 EAA0901」（样本注释） | SampleGame.cs:55、README.md:38、EAA0901 messageFormat EffectAlgebraAnalyzer.cs:49 | **误报（三处文档集体说谎）** | 实现永不豁免：EffectAlgebraAnalyzer.cs:136 无条件调用 `AnalyzeMissingRelease`，其体内（:183-190）不看 override；EndToEndTests.cs `Override_DoesNotExemptLeak_StillReportsEAA0901` 断言必须报。详见 H1 |
| 5 | 「EffectEvent 是 Lifetime/Footprint/Loop 三属性 record struct」 | EFFECT_SCRIPT.md §2.1（:50-57） | **已漂移** | 实现为四属性（多 `Scope`）：EffectScript.cs:13-24；且 doc :62 称「5 字段」、code :19 称「4 字段」，同一句话三个版本 |
| 6 | 「AI 产出 §4 JSON → Parse → Audit 闭环无需运行游戏」 | EFFECT_SCRIPT.md:134,152 | **部分修** | 闭环机制存在且 Violation 反例可用（本轮实测通过），但权威示例 JSON 无法被自家 Parse 接受（H2），闭环第一步就断 |
| 7 | 「ScopeId 偏序（IncludedIn ⊆*）」 | EFFECT_SCRIPT.md §1.1 表、Objects.cs:87 注释「方法内嵌 §3.1.3b 查表」 | **误报（注释夸大）** | Objects.cs:101-113 实现塌缩为「Equals ∨ other is Global」，无任何层级查表；Scene 嵌套直觉静默失效（M3） |
| 8 | 「loop:"⊤" 居民层豁免 vs lifetime:[1,⊤] 入 net」双语义锐边 | README.md:57 | **仍在（已如实标注）** | EffectScript.cs:132（Lo=⊤ 跳过）、:317（ω=⊤ 豁免守恒） |

---

## 三、新发现

### HIGH

**H1 · EAA0901 的官方修复建议是一条死路——诊断文案、README、样本三方教一个无效的修法**
- 位置：src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs:49（messageFormat 修复选项 (2)）vs :136（`AnalyzeMissingRelease` 无条件调用，注释自书「永不豁免」）及 :183-190（计数逻辑无 override 分支）
- 佐证：tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs `Override_DoesNotExemptLeak_StillReportsEAA0901` 明确断言标了 `[EffectOverride]` 仍报 EAA0901；而 README.md:38「若有意偏离守恒，标 [EffectOverride("证据")]」与 samples/GodotIntegration/SampleGame.cs:55「⇒ L3 不报 EAA0901」都承诺豁免
- 判词：错误信息给出一副不存在的药——这是把「easy」（顺手抄建议）做成了陷阱；用户照做、警告依旧、再照做、永动。
- 最小修复：三选一并同步三处文案：(a) EAA0901 尊重有效 override（与 README/SampleGame 语义对齐）；或 (b) messageFormat 删掉修复(2)，改为「本诊断不接受 override，请补 release 或重构」；或 (c) 新增独立抑制诊断 id。同时改 SampleGame.cs:4/:55 的注释。

**H2 · EFFECT_SCRIPT.md §4 权威 AI 契约示例无法被自家 Parse 解析（实测两连败）**
- 位置：EFFECT_SCRIPT.md:136-151（示例）vs src/Cosmos.EffectAlgebra/EffectScriptContract.cs:57（事件级 `scope` 必填）与 :158（`gpu` 值须为非空字符串，示例却是嵌套对象 `{"gpu":{"bufferId":"mesh1"}}`）
- 执行证据：`EffectScriptContract.Parse(文档JSON逐字复制)` → `FormatException: 缺少字段: scope`；手工补 scope 后 → `FormatException: resource.gpu 须为非空字符串`
- 判词：给 AI 的数据契约，第一条样例就过不了自家类型的大门——契约文档和解析器各自演化，谁也没付「对齐税」。
- 最小修复：把 §4 示例改为可解析形状（每 event 加 `"scope":{...}`、resource 展平为字符串），并加一条测试：**文档 JSON 原文逐字嵌入测试断言 Parse 通过**，杜绝再漂移。

**H3 · 白名单按方法名裸匹配任意接收者：常见游戏方法名撞库即误报，AI 无从归因**
- 位置：EffectAlgebraAnalyzer.cs:288-300（`FindWhitelistEntry` 仅比较 canonical 名，全名或裸方法名任一命中即算）、:303-315（RawName 只看语法，不看接收者类型）
- 场景推演：游戏代码自有方法 `Load(string path)`、`Connect()`、`Play()`、`Instantiate()` 极常见；任何调用这些同名方法的代码会被当作 Godot API 计入 acquire/release，产生与真实资源无关的 EAA0901/EAA0304；反向地，L2 Generator 会为这些自定义方法生成 Godot Claims（Generator :117-121 同键匹配）。警告里没有任何一行提示「这可能是名字碰撞」。
- 判词：用名字当类型的替身，就把整个 C# 词汇表押给了白名单——程序员知道的越少，撞得越莫名其妙。
- 最小修复：至少在诊断 message 中附「命中白名单键：xxx（若非 Godot 调用请改名）」；理想方案是语义模型校验接收者类型含 `Godot.Node` 等基类。

### MED

**M4 · `[EffectOverride]` 一名二义：逃逸通道兼作 L2 codegen 触发器**
- 位置：samples/GodotIntegration/SampleGame.cs:19（「标 [EffectOverride] ⇒ L2 生成 ComputeAddChild」）、:25/:28（配对正常的方法也被迫写「违规证据」字符串）；EffectAlgebraGenerator.cs:23-24,63-77（只挑带注方法生成）
- 判词：「override」这个名字承载了两件不相干的事——声明意图偏离、注册签名生成。结果是最健康的代码（AddChild/RemoveChild 配对）也要写「我知道我在泄漏」式的悔过书。
- 最小修复：给 codegen 触发一个独立标记（如 `[EffectTracked]`），让 override 回归单一职责。

**M5 · 第一小时没有任何真实 Godot 编译证据；README 快速上手代码在真 Godot 上不可编译**
- 位置：README.md:32（`var node = AddChild(...)`——真 `Node.AddChild` 返回 void）；DELIVERABLE.md out-of-scope §13（「L3 在真实 Godot 工程接入…本机未装」）；samples/GodotIntegration/SampleGame.cs:6-11（stub Node3D 占位）
- 判词：「游戏侧照常写 Godot API 零改动」是核心卖点，但卖点从未在真 Godot 上兑现过一次——承诺最贵的地方恰恰是没验证的地方。
- 最小修复：CI 加一个 `godot-dotnet` SDK 的 compile-only 消费工程（哪怕不跑引擎，只证 analyzer 在真 Node 子类上触发）。

**M6 · `lifetime:["⊤","⊤"]` 事件被静默跳过审计——文档说该当非法输入，实现选择 fail-open**
- 位置：src/Cosmos.EffectAlgebra/EffectScript.cs:132（sweep 循环 `if (lt.Lo.IsTop) continue;`）；EFFECT_SCRIPT.md OPEN-B4 注（:「[⊤,⊤] 视为非法输入而非错误项」）
- 执行证据：构造单事件脚本 `Interval(Top,Top)` + occupy create，`At(0)` 空、`Audit().Passed == true`——一个占着 commandBuffer 的「常驻元素」无声消失
- 判词：非法输入静默变绿，比报错危险一个数量级；AI 最常犯的就是端点写错。
- 最小修复：`Parse`/构造器遇 `lo=⊤ ∧ hi=⊤` 抛 `FormatException/ArgumentException`（与 Interval 已有的 lo>hi 抛一致，Numeric.cs:88-95）。

**M7 · ScopeId「偏序」实为相等+Global，场景嵌套直觉静默失效**
- 位置：Objects.cs:101-113（`IncludedIn`：Equals 或 Global 即 true，否则 false）vs EFFECT_SCRIPT.md §1.1 表与 Objects.cs:84-87 的 ⊆* 偏序/查表叙述
- 后果：`Scene("World").IncludedIn(Scene("World/City"))` 为 false，`Signature.Net(scope)` 过滤会漏掉用户以为归属了的 Claim——无警告、无诊断。
- 最小修复：要么实现真前缀层级，要么把注释/文档改成「扁平作用域」，别让名字许诺实现没有的数学。

**M8 · 任务口径的「游戏数值效果」（buff/debuff/装备/光环）零覆盖**
- 位置：全仓库 grep `buff|equipment|aura|debuff` 无任何类型/样本/测试命中；samples/ 仅覆盖 AddChild/QueueFree 泄漏配对（SampleGame.cs 全文）与 AnalyzerConsumer/Game.cs 两类
- 判词：如果第一小时的问题是「怎么给我的攻击力 buff 建模」，答案是这个框架根本不管这件事——而所有入口文档都不提前说清这一点。命名与定位在浪费用户最贵的资源：预期。
- 最小修复：README 首屏加一段「这不是什么」：数值属性/buff 栈/装备聚合不在范围内，本框架管资源守恒。

### LOW

**L1 · EAA0303/EAA0304 文案只有内部规范引用，无可操作修复步骤**
- 位置：EffectAlgebraAnalyzer.cs:79,90 ——「建议显式 [EffectOverride] 标注意图（§14.3 A3…§3.2.3）」，用户手边没有 PDR；对比 EAA0901 至少给了两条具体动作。
- 修复：文案改为「同方法内对资源 X 出现 read+occupy 混用；若有意，标 [EffectOverride("理由")]，或将两类调用拆到不同方法」。

**L2 · `Violation.Kind` 为裸 string（"Leak"|"NegativeDip"|…）**
- 位置：EffectScript.cs:357-385（Kind 注释列举四种）+ 各 `violations.Add(new Violation(...,"NegativeDip",...))` 散落字面量。
- 修复：换 enum，拼错编译期就炸。

**L3 · `NatStar`/`Interval` 无 int 便捷工厂，AI 直觉写法 `new Interval(0,120)` 编译失败**
- 位置：Numeric.cs:44-46（NatStar 私有构造+Of(ulong)）、:80（Interval(NatStar,NatStar)）；C# int 字面量不隐式转 ulong 参数位置亦不适配。
- 反馈回路：秒级（编译期），可接受但纯摩擦。修复：加 `static Interval Of(ulong lo, ulong hi)` 或 NatStar 隐式转换。

**L4 · 样本工程亲手关掉全部 EAA 门禁**
- 位置：samples/GodotIntegration/SampleGame.csproj:22 `<WarningsNotAsErrors>EAA0901;...</WarningsNotAsErrors>`（有注释理由：让构建能完成以展示诊断触发）。
- 判词：示范工程教会用户的第一招就是绕过门禁。修复：拆成「干净消费工程（error 级）+ 故意违规展示工程（warning 级）」。

**L5 · 文档自相矛盾的字段数陈述**
- 位置：EFFECT_SCRIPT.md:62「5 字段位置记录」vs EffectScript.cs:19「4 字段位置记录」vs §2.1 代码块列 3 个属性。
- 修复：随 H2 一并对齐文档。

---

## 四、对抗性验证：AI 最可能的第一次尝试（实测/逐行推演）

### 尝试 ①：真 Godot 工程里的习惯写法 + 按诊断文案修复

```csharp
public partial class Player : Node          // 真 Godot.NET.Sdk
{
    public override void _Ready()
    {
        var enemy = GD.Load<PackedScene>("res://enemy.tscn").Instantiate();
        AddChild(enemy);
        // 敌人由 gameplay 逻辑在别处 queue_free —— 地道的 Godot 生命周期
    }
}
```

逐行推演：
1. 若照 README:32 先写 `var node = AddChild(...)` —— **编译失败**（真 AddChild 返回 void），秒级反馈，但这是入口文档教的。
2. `_Ready` 内 `Instantiate`（Mem create ×2 + Tree new_id create）与 `AddChild`（Tree create）均无方法内 release ⇒ **≥1 条 EAA0901**（EffectAlgebraAnalyzer.cs:183-190 按资源计数）。idiomatic Godot 代码 = 警告刷屏。
3. AI 按诊断文案修复(2)加 `[EffectOverride("freed in _ExitTree")]` ⇒ **仍报 EAA0901**（:136 永不豁免 + EndToEndTests 断言背书）。
4. 结论：**秒级报错 + 永不闭合的修复环**——最快的报错速度配最坏的反馈质量。

### 尝试 ②：AI 按 EFFECT_SCRIPT.md §4 产出剧本 JSON 并喂 Parse

实测（本轮在临时目录编译执行，引用 bin/Debug/net10.0/Cosmos.EffectAlgebra.dll）：

```
A: FormatException: 缺少字段: scope            ← 文档 JSON 逐字复制
B: FormatException: resource.gpu 须为非空字符串 ← 手工补事件级 scope 后
最终修正形状后: Passed=False, Violations=1
   [Leak] t=180 res=Gpu(mesh1) :: 生命周期未闭合（净效应不含 0）：[1,1]
```

三次往返才到达「真正的审计结果」，其中两次是文档-实现漂移税，第三次文档未提示示例本应失败。Violation 反例本身信息充分（时刻/资源/净值），这一段是好的。反馈回路：运行时（但无需跑游戏，秒级）——前提是用户自己先搭好 harness（M-findings：无 CLI、无样本）。

---

## 五、TOP-3

1. **H1 — EAA0901 官方修复建议无效**：诊断文案、README、样本三方共同教授一条被测试证明走不通的路。反馈回路永不闭合，这是 approachability 的死刑级缺陷。
2. **H2 — AI JSON 契约的旗舰示例过不了自家 Parse（实测）**：给 AI 的门面在第一步就崩，且无任何可运行样本/工具兜底。
3. **H3 — 白名单裸名称匹配造成不可归因的误报/伪生成**：`Load/Connect/Play/Instantiate` 等通用动词全在枪口上，警告不解释碰撞来源。

---

## 六、Usability 裁决

**用户和 AI 能否简单、正确地使用？——现状：不能。**

- 能成立的部分：L1 代数核心是诚实的值语义设计（readonly record struct、fail-fast 构造器、⊤ 闭包），EffectScript 的 `Audit`→`Violation` 反例回路信息充分；这两块值得肯定。
- 不能成立的部分：三条第一小时路径各有硬伤——泄漏检查的官方解药无效（H1）、AI 数据契约的门面示例无法解析（H2）、名称匹配制造无法理解的噪音（H3）；再叠加无真实 Godot 验证（M5）、无 EffectScript 可运行样本、文档-实现多处漂移（核实矩阵 #4/#5/#6/#7）。「简单」意味着不纠缠，而现在用户必须在脑内同时维护「文档说的」和「代码做的」两套模型——这正是 complect 的定义。

一句话判词：**代数是简单的，通往代数的路全是 easy 的坑——把路修到和核一样诚实之前，别邀请用户进门。**

---

## 证据清单（本轮实际读取）

- README.md、EFFECT_SCRIPT.md、DELIVERABLE.md
- samples/GodotIntegration/SampleGame.cs、SampleGame.csproj、AdvE2E_R1.cs（前 40 行）
- samples/AnalyzerConsumer/Game.cs
- src/Cosmos.EffectAlgebra/：EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs、ApiMapping.cs、Objects.cs、Numeric.cs
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs（全文）
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs（全文）
- tests/Cosmos.EffectAlgebra.Tests/EndToEndTests.cs（全文）
- 执行验证：临时目录（仓库外）console 工程，引用 `src/Cosmos.EffectAlgebra/bin/Debug/net10.0/Cosmos.EffectAlgebra.dll`，dotnet run 三组实验（Parse 文档 JSON ×2 形态、Audit Leak 反例、[⊤,⊤] lifetime 静默豁免）
