# Round09 — Approachability 对抗性审计（Rich Hickey 视角：Simple Made Easy）

- 审计范围：README.md、EFFECT_SCRIPT.md（+1 文档）、src/Cosmos.EffectAlgebra/{EffectScript.cs, EffectScriptContract.cs, Objects.cs, Numeric.cs, Algebra.cs, DerivedMetrics.cs, SignedNet.cs}（7 源文件）。未读 audit/。
- 视角：「简单 ≠ 容易」。评估新用户写出第一个**审计通过**剧本的真实步数、隐式规则负担、错误信息可操作性。
- 结论先行：**「5 行写出合法剧本」不成立**。构造一个对象约需 5 行，但一个能通过 Audit 的最小守恒剧本（create+release 配对）在 C# 侧 ≥8 行且涉及 7 个类型；JSON 侧 ≥14 行。更糟的是，EFFECT_SCRIPT.md §4 的旗舰 JSON 示例本身就会产出 Leak 违例（见 F1）。

---

## 一、「5 分钟上手」路径实测

### 1.1 C# 构造路径（无 Builder）

最小**可审计通过**剧本需要 create 与 release 配对（守恒闭合），实测形态：

```csharp
var scope = new ScopeId.Scene("Battle");
var create = new EffectEvent(
    new Interval(NatStar.Of(0), NatStar.Of(120)), scope,
    Signature.Of(new Claim(Kind.Occupy, new ResourceId.CommandBuffer("gpu"),
        Mode.Create, scope, null)));          // size=null ⇒ 隐式 [1,1]
var release = new EffectEvent(
    new Interval(NatStar.Of(121), NatStar.Of(180)), scope,
    Signature.Of(new Claim(Kind.Occupy, new ResourceId.CommandBuffer("gpu"),
        Mode.Release, scope, null)));
var result = new EffectScript(new[] { create, release }).Audit();
```

≈10 行（不含 using）。若只写 5 行单事件 create，`Audit()` 必报 Leak——即「5 行」最多得到一个**必然失败**的剧本。JSON 路径（§4 契约）最小通过样本同样需要两个 event、每个含 lifetime/scope/loop/footprint 四键 ≈14 行。

### 1.2 步数清单（新用户从零到第一个绿灯）

1. 发现 EffectScript 层存在（README「5 分钟上手」只讲 L3 Analyzer，**不含剧本 DSL**，需自行跳转 EFFECT_SCRIPT.md）；
2. 理解 EffectEvent 四字段与 Interval/NatStar/ScopeId/Signature/Claim/LoopCount 六类载体的组合方式（无 Builder，全部裸构造子）；
3. 记住隐式规则集（见第三节）；
4. 处理 create/release 必须拆成两个 Event 且 release 的 Lo > create 的 Hi 或重叠区间内 net 不为负的时序约束（该约束**任何文档都未明说**）；
5. 跑 Audit，解读 Violation。

真实步数：≥5 步、≥10 行 C# 或 ≥14 行 JSON。「5 行合法剧本」仅在「类型上可构造」意义上勉强成立，「审计通过」意义上不成立。

---

## 二、发现清单（按严重度）

### F1 · BLOCKER · EFFECT_SCRIPT.md §4 旗舰 JSON 示例自身触发 Leak

位置：`EFFECT_SCRIPT.md` §4 JSON 中第二个事件 `"loop": 0`。
证据链：`EffectScript.cs` `ScaleSize`（w 有限时返回 `[s.Lo*w, s.Hi*w]`）× `Numeric.cs` `NatStar.operator*`（1×0=0）⇒ loop=0 把 size 缩放为 `[0,0]` ⇒ 该事件的 release 贡献为 `[-0,-0]` ⇒ 闭包检查 `ContainsZero` 对累积净 `[+1,+1]` 为 false ⇒ 报 `Leak: 生命周期未闭合（净效应不含 0）：[1,1]`。
后果：新用户复制文档第一段示例 → 第一次 Audit 即失败，且失败原因（ω=0 是「零并发副本」而非「不循环」）正是文档 §1.1 OPEN-N2 警告过的混淆点——**文档自己踩了自己标记的坑**。正确写法是删掉 `loop` 键（默认 1）或写 `"loop": 1`。

### F2 · HIGH · 无任何 C# Builder / 可复制示例，认知负荷全压在源码注释上

证据：`samples/` 仅 AnalyzerConsumer（L3 用法）与 GodotIntegration E2E，grep 全仓库无一处 `new EffectEvent(` 出现在 samples；README 与 EFFECT_SCRIPT.md 均只有 JSON 形态示例。Hickey 口径：API 要求用户在第一行代码前同时装下 `Interval`/`NatStar`/`ScopeId`(8 种标签)/`Signature.Of`/`Claim`(五元位置参数)/`LoopCount`/`Budget` 七个概念，且 `Claim(Kind, ResourceId, Mode, ScopeId, Interval?)` 五个位置参数在调用点无自解释性。测试文件里有大量构造样例但不是入门材料。

### F3 · HIGH · claim 级 scope 是「必填但被忽略」的字段

证据：`EffectScriptContract.ParseClaim` 对每个 claim `Require(c, "scope")`（缺失即抛）；但 `EffectScript.Audit` 三道 gate 全部使用事件级 `e.Scope` 分组（gate(3) 注释自认「builder 恒 c.Scope==e.Scope」），claim 自带 scope 在剧本审计路径中**从不参与判定**。新用户会合理推断「claim 里写了不同 scope 会影响分组」，实则静默无效。这是典型 hidden coupling：同一概念两处填写、一处说了算。JSON 契约应至少在错误信息或文档中声明冗余性。

### F4 · MEDIUM · 隐式规则负担 ≈9 条，全部需跨文件拼凑

写出正确剧本前必须记住（每条均有代码证据）：

| # | 隐式规则 | 出处 |
| --- | --- | --- |
| 1 | size 省略 ⇒ `[1,1]`（非 ⊤ 非 0）；显式 `[0,0]` 与省略语义不同 | Objects.cs `Claim.Normalize`；README「语义锐边」 |
| 2 | `loop` 是瞬时并发副本数，不是时间重复次数；时长归 Lifetime | EFFECT_SCRIPT.md §1.1 OPEN-N2（埋在动机节） |
| 3 | `Lifetime.Lo` 必须有限；`[⊤,⊤]` 合法但永不存活（静默脱离审计，fail-open） | EffectScript.cs Alive/扫换线 skip；§2.1 边界注记 |
| 4 | ω=⊤ 事件豁免守恒闭包（居民层），但仍计峰值 | EffectScript.cs Step gate(1) `if (enter && !e.Loop.Count.IsTop)` |
| 5 | read/write 两桶完全不进守恒与峰值；只有 `kind:"occupy"` 有审计效果 | Algebra.cs NetTable.Compute `if (c.Kind != Kind.Occupy) continue` |
| 6 | 同 mode∈{create,move,release} 两事件并发即冲突；use 与一切兼容 | Algebra.cs Compatible.IsCompatible |
| 7 | budget 键用扁平字符串前缀 `"commandBuffer:gpu"`，与 footprint 内嵌套对象 `{"commandBuffer":"gpu"}` 是同一资源的两种编码 | EffectScriptContract ParseResourceKey vs ParseResource |
| 8 | release 的负向 net 记在其自身 Lo 时刻 ⇒ release 必须「晚于」create 开始，否则 NegativeDip | EffectScript.cs gate(1) 注释「release 自带负向贡献于其自身 Lo」 |
| 9 | 闭包检查只统计 `Lo ≤ maxFinite` 的事件：起点晚于全剧最后端点的未来事件被静默排除出守恒检查 | EffectScript.cs 闭包块 `CompareToFinite(closureT) > 0 continue` |

Hickey 判定：这已不是 guard rails，而是 ritual——每一处都是「不知道就静默出错或误判」的 complected 默认值。

### F5 · MEDIUM · 解析错误信息「半可操作」：有字段名，无定位路径，部分异常漏原生英文

好的部分：`FormatException` 消息多为中文、指名期望形状（如 `缺少字段: lifetime`、`resource 须含 gpu/commandBuffer/memory/occupancy/signalBus 之一`、`未知 scope.type: xxx`）。
缺陷：
1. 无 JSON 定位路径——多事件剧本第 N 个事件第 M 条 claim 出错时报错不区分（如 `lifetime 须为 [lo,hi] 数组` ×50 个事件无法定位是哪个）。
2. 类型转换走 `GetUInt64()` 原生异常：`"lifetime":[5.5,10]`、budget 值写成 `"64"` 时抛 System.Text.Json 英文 `FormatException`，无字段上下文（EffectScriptContract.ParseTop / ParseBudget）。
3. `lo="⊤"` 时由 `Interval` 构造子抛**英文 ArgumentException**（Numeric.cs），与契约层中文 FormatException 双语混杂、异常类型不一致。
4. 一次只报第一个错（fail-fast 单错误），AI 回修循环每轮只能消一个错。

### F6 · LOW · Violation 的 Scope 恒为 Global，削弱反例可回修性

证据：EffectScript.cs gate(1)/gate(2) 违例均 `new ScopeId.Global()`，仅 gate(3) 带真实 scope。多作用域脚本中 Leak/PeakExceeded 反例丢失「发生在哪层」的信息——Violation 文档承诺「供 AI 直接回修的充分信息」，此处未兑现。

### F7 · LOW · README 快速开始与头牌功能错位

README「5 分钟上手」只覆盖 L3 acquire/release 白名单路径；作为项目愿景核心的剧本 DSL 在 README 仅有一行表格链接。新用户的默认入口不会引导到 EFFECT_SCRIPT.md，且 README 未提示剧本层还有一套独立隐式规则（F4）。

### F8 · NOTE · 值语义双轨制增加记忆负担

`ScopeId.Scene(name)` 在 JSON 里默认形态是 `{"scene":"name"}`，而 Method/Type 要显式 `"type"` 键、Global 则**不能**有 scene（auditR4 修过 round-trip）。三种序列化形状并存于同一个 ParseScope switch。加上 `NatStar`/`ZStar`/`DeviationVal` 三个同构 ⊤ 载体，用户需自行记住何时用哪个——对「AI 直接产出数据」的目标人群是持续摩擦。

---

## 三、符号表逐一评估（上手视角）

| 符号 | 上手难度 | 问题 |
| --- | --- | --- |
| `NatStar` (Numeric.cs) | 中 | `IsTop`/`Value` 二态清晰，溢出⇒⊤保守良好；但「⊤ 是未知而非无穷」需注释才能懂，`Value` 在 IsTop 时无效是隐式前置条件 |
| `Interval` (Numeric.cs) | 低-中 | 构造校验好（lo≤hi、lo=⊤∧hi有限 抛错）；但错误是英文 ArgumentException，与契约层风格断裂（F5.3） |
| `ZStar/SignedInterval` (SignedNet.cs) | 低 | 设计干净；用户通常无需直接接触 ✓ |
| `LoopCount` (DerivedMetrics.cs) | **高** | 名字叫「循环次数」，实际语义是并发副本数（OPEN-N2 重解释）——名字与语义直接冲突，F1 的根因 |
| `Claim` (Objects.cs) | **高** | 五元位置参数、scope 必填但在剧本路径被忽略（F3）、size 可空三义（null/[1,1]/[0,0]） |
| `Signature` (Objects.cs) | 中 | 三桶隔离合理；`Signature.Of(params)` 便捷 ✓；class 伪装值类型已有结构相等补丁（注释自知 footgun） |
| `ResourceId` (Objects.cs) | 中-高 | 15 个构造子 + Normalize 幂等规则（signal_ 前缀剥离等），剧本用户只需 5 种但无从得知哪 5 种，除非读契约解析器源码 |
| `ScopeId` (Objects.cs) | 高 | 8 种标签 + Global 最大元偏序；IncludedIn 跨标签恒 false 的含义需自行推导；JSON 序列化三形状并存（F8） |
| `Budget` (EffectScript.cs) | 中 | 键格式与 footprint 资源格式不一致（F4#7）；`default` ⇒ None 的兜底是善意陷阱（忘设预算 = 全部通过，静默无保护） |
| `EffectEvent` (EffectScript.cs) | 中 | 4 字段必填 ✓，3 参便捷构造 ✓；但 Loop 字段语义坑（见上） |
| `EffectScript.At/Audit` (EffectScript.cs) | 中 | Audit() 无参重载便捷 ✓；扫换线实现复杂但被封装，用户不可见 ✓ |
| `EffectScriptContract` (EffectScriptContract.cs) | 中 | Parse/ToJson 对称、fail-fast 方向正确；扣分项见 F5 |

---

## 四、残余风险

- 本轮为纯静态阅读 + 推理验证（F1 的 Leak 推导基于 ScaleSize/NatStar 乘法与闭包检查的代码路径，未运行复现；建议以一条最小 JSON 用例跑 `EffectScriptContract.Parse(...).Audit()` 固化）。
- 未审计 Generator/Analyzer/Runtime 对上手性的影响（超范围）。
- 「步数/行数」计数基于当前 API 面，若后续加 Builder 则结论需更新。

## 五、修复优先级建议（供编排方参考）

1. **立即**：改 EFFECT_SCRIPT.md §4 示例 `"loop": 0` → 删除该键（F1，BLOCKER）。
2. 近期：提供 `EffectScriptSample`（C# + JSON 各一，审计通过版）入 samples/ 或 README；Parse 错误带 JSON 路径（`events[2].footprint[0].size`）。
3. 中期：考虑 Builder（`EffectEvent.Create(lifetime, scope).Occupies(...)`）或在 ParseClaim 对「claim scope ≠ event scope」发警告，消除 F3 的静默忽略。
