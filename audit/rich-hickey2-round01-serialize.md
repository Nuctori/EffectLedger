# Round 1/10 — 序列化 round-trip 数据保真（对抗性审计）

- 审计员：ox-alpha（Rich Hickey 透镜：值在往返中是否保真、失败是否 loud、简单性是否被牺牲）
- 输入：`audit/rich-hickey-round10-synthesis.md` + 全部 7 个 L1 源文件精读 + EFFECT_SCRIPT.md 全文 + 测试取证
- 方法升级：本轮工具链已修复（dotnet 10.0.103），**所有关键发现均经动态探针复现**（TEMP 探针工程引用 src 工程，未改动仓库任何源文件）；基线 `dotnet test tests/Cosmos.EffectAlgebra.Tests` = **312 passed / 0 failed**。

---

## 一、核实矩阵（上批 10 轮结论 × 当前磁盘源码逐条裁决）

| 根因 | 综合表状态 | 本轮裁决 | 证据 |
|---|---|---|---|
| **A. Serialize 三臂 `_=>` 静默兜底** | 仍在 | **已修（already-fixed）** | SerializeScope `_ => throw new FormatException($"不可序列化的 scope: {s}")`（EffectScriptContract.cs:236）、SerializeResource 同型（:255）、ResourceKey 同型（:274）。探针实证：含 `Tree` 资源的脚本 ToJson 抛 FormatException，不再无声降级。Parse 侧资源值也补了 fail-fast（ReqStr :284–286，测试 EffectScriptEdgeTests.cs:841/843 在守） |
| **B. loop=0 语义陷阱** | 仍在 | **部分修** | 契约层 ：136 `if (v == 0) throw …("loop 必须 ≥1…")` ✓；LoopCount.Of 拒 0（DerivedMetrics.cs:18）✓；文档 ：137 已改 `"loop": 1` ✓。**但 struct default 后门未堵**——见新发现 F4（探针实测 DivideByZeroException 仍可达） |
| **C. 违例归因伪造 Global / claim-scope 双真相** | 仍在 | **已修** | netScope/peakScope/leakScope 三处归因均填真实 e.Scope（EffectScript.cs:168–174、245–249、310–321）；gate(3) 分组键用 e.Scope（:184），注释说明与 At 视角一致 |
| **D. Weight.Of 以 NaN 编码 ⊥** | 仍在 | **已修** | Algebra.cs:37–40 改抛 `InvalidOperationException("KIND_MIX: 跨 kind 权重未定义…")`，NaN 毒值消除 |
| **E. Budget 可变字典 + None 可变单例** | 仍在 | **仍在（未修，非本轮透镜）** | EffectScript.cs:349–358：`readonly record struct Budget` 包 `Dictionary`，`None = new(new Dictionary<…>())` 仍是可变单例。综合表 Top 削减建议未执行 |
| **F. 四名一实 Union/Join/Sequence/Parallel** | 仍在（锁死待核） | **部分修** | Parallel 已分化：跨分支冲突前置守卫 PARA_CONFLICT（DerivedMetrics.cs:53–61）；Join 已实现真正的 Interval.Merge 配对合并而非裸 Union（Objects.cs:196–214）——综合表「Join 注释承诺 merge_I 但实现为裸 Union」一条**已过时**。Sequence 仍恒等于 Union（DerivedMetrics.cs:51）|
| **G. Signature.GetHashCode 顺序敏感** | 仍在 | **已修** | Objects.cs:216–230 改为顺序无关 XOR 折叠 + 桶计数混淆 |
| **H. Peak.Compute 跨桶聚合** | 仍在 | **仍在** | Algebra.cs:118–127 仍对 AllClaims()（read/write/occupy 三桶）只滤 release 就求和，量纲隔离在峰值维度仍被击穿（非本轮透镜，留档） |
| **I. CONFLICT 集两处写** | 仍在 | **已修** | gate(3) 改调 `Compatible.IsCompatible(mode, mode)` 单一真源（EffectScript.cs:257–259），漂移面消除 |
| **M. EffectScript 死代码重复行 272/274** | 仍在 | **已修（随扫换线重写消失）** | 现 EffectScript.cs 无逐字重复行；旧行号内容已被 O(E·K·log E) 重写取代 |

矩阵小结：上一批点名的 6 条 HIGH 根因中，A/C/D/G/I 五条确认已修且修复质量良好（fail-fast 方向正确）；B 半修；E 未动。**但修复序列化侧时在相邻层留下了新的洞**——见下节，这正是本轮的主收获。

---

## 二、新发现

### F1 · Blocker · EFFECT_SCRIPT.md §4 旗舰示例今天无法通过 Parse —— AI 数据契约的第一公里是断的

- **位置**：EFFECT_SCRIPT.md:135–150 vs EffectScriptContract.cs:77、:184、:284–286
- **证据**（探针实测）：把 §4 JSON 原样喂 `EffectScriptContract.Parse` ⇒ `FormatException: 缺少字段: scope`。两处契约漂移叠加：
  1. ParseEvent 强制**事件级** `"scope"`（EffectScriptContract.cs:77 `var scope = ParseScope(Require(ev, "scope"));`），而 §4 示例每个 event 只有 footprint 内的 claim 级 scope，没有事件级字段；
  2. 即便补上事件级 scope，示例的 `"resource": {"gpu": {"bufferId":"mesh1"}}` 是嵌套对象，而 ParseResource 经 ReqStr 要求 gpu 为**非空字符串**（:184、:284–286），ToJson 侧实际输出的也是扁平字符串形态（SerializeResource :250 `["gpu"] = g.BufferId.Value`）。
  - 全仓 grep `bufferId`：tests/samples/docs **零命中**——文档示例从未被任何测试当作解析输入守护过。这正是上一批根因 B 的同构复发：「文档即第一个用户输入」，而这次连第一次 Parse 都活不过去。
- **判词**：一个声称「AI 产出此 JSON → Parse」的契约，其教科书示例本身是非法输入——这不是文档瑕疵，是契约没有单一真相：代码、文档、序列化输出三方各说各话。
- **最小修复**：二选一并加守护测试——(a) 更新 §4 示例：每 event 加 `"scope": {"scene":"Battle"}`，gpu 改为 `{"gpu":"mesh1"}`；(b) 若嵌套对象是有意设计，则 ParseResource 支持 `{"bufferId":…}` 形态并与 SerializeResource 对齐。配套红测试：§4 示例原文逐字进测试资源文件，断言 `Parse(docJson)` 不抛且 `Parse(ToJson(Parse(docJson)))` 幂等。
- **verdict**：fixable
- **testHint**：`Assert.Throws<FormatException>(() => EffectScriptContract.Parse(DocSection4Json))` 今天为绿（证明断裂）；修复后改为 round-trip 断言。

### F2 · HIGH · 未知键白名单只设在根层——事件层/claim 层拼写错误被静默吞掉，语义被无声改写

- **位置**：EffectScriptContract.cs:29（仅 `RejectUnknownKeys(root, "根", "events", "budget")`）；ParseEvent :75–83 用 TryGetProperty 读可选键
- **证据**（探针实测）：`{"events":[{"lifetime":[0,10],"scope":{"type":"global"},"Loop":"⊤","footprint":[]}]}` 解析**成功**，且 `Events[0].Loop.Count == 1`——用户写了大写 `Loop:"⊤"`，被静默丢弃后落回缺省 ω=1。「ω=⊤ 的常驻居民层」瞬间变成「单副本瞬时元素」，后续审计结论整体错位，**无任何声响**。R6-E1 注释里自己给出的理由——「拼写错误静默禁用整个预算门」——在事件层一字不差地成立，却只修了根层。
- **判词**：这是旧根因 A 的转世：兜底臂从 Serialize 挪到了键匹配层。fail-fast 白名单做了一层就停手，等于城墙只砌了正门。
- **最小修复**：`RejectUnknownKeys(ev, $"events[{i}]", "lifetime", "scope", "loop", "footprint")` 与 claim 层同型校验（kind/resource/mode/scope/size）；循环变量 i 顺手解决 F6 的定位问题。
- **verdict**：fixable
- **testHint**：`Assert.Throws<FormatException>(() => Parse("""…"Loop":"⊤"…""))`；property test：对合法 JSON 任意单字符变异键名 ⇒ 必须 FormatException。

### F3 · HIGH · `default(LoopCount)` 毒值后门：类型层拒了 0，struct default 又把 0 送回来了

- **位置**：DerivedMetrics.cs:15（private ctor 挡不住 `default`）→ EffectScript.cs:200、:219（`ulong.MaxValue / w.Value`）
- **证据**（探针实测）：`new EffectEvent(life, scope, fp, default)` 编译通过、构造成功；Audit 时 `DivideByZeroException`。链条：`default(NatStar)` = {IsTop=false, Value=0} ⇒ `default(LoopCount).Count` 是「有限 0」，绕过了 Of() 的 ≥1 校验与 ParseLoop 的拒绝。enter/exit 两处除法守卫只防溢出不防 0；若走闭包路径 ScaleSize 则是更糟的静默版：[1,1]×0=[0,0] ⇒ 净效应含 0 判定被污染（Leak 误报/漏报）。上一轮宣称「非法状态不可表示」的修复，被 C# struct default 这个狗门整个穿透。
- **判词**：你把 0 关在构造函数门外，struct default 从地下室走了进来。record struct 的「构造即合法」承诺只有配上 `Count==0 ⇒ throw` 的消费点守卫才算数——否则类型边界只是装饰。
- **最小修复**：EffectEvent 构造函数追加 `if (!loop.Count.IsTop && loop.Count.Value == 0) throw new ArgumentException(...)`（一处收口，覆盖 enter/exit/closure 三条路径）；或在两处除法点补 `w.Value != 0 &&` 转 ⊤。前者符合本库「构造期拒非法」纪律。
- **verdict**：fixable
- **testHint**：`Assert.ThrowsAny<Exception>(() => new EffectEvent(..., default).Audit(Budget.None))` 修前应捕获 DivideByZeroException（红），修后应为 ArgumentException 且发生在构造期。

### F4 · MED · 异常类型不对称：文件头承诺 FormatException，实际漏出 InvalidOperationException / ArgumentException

- **位置**：EffectScriptContract.cs:4（头注「非法形状抛 FormatException（fail-fast）」）vs :115、:120、:153、:155、:203、:148
- **证据**（探针实测）：`{"scene":42}` ⇒ `InvalidOperationException: The requested operation requires an element of type 'String'…`（:115 `sc.GetString()` 对非字符串元素直接炸，`?? throw FormatException` 根本走不到）；`{"type":7}`（:120）、kind/mode 非字符串（:153/:155）、budget 值为 `true`（:203 GetUInt64）同理；footprint 含两条完全相同的 claim 时 Signature.Of（:148）抛 **ArgumentException**。方向是对的（loud），但异常类型和消息全是 System.Text.Json 的英文原文——调用方按文档 catch FormatException 会漏接；AI 回修拿到的反例信息退化成 Json 内部抱怨。
- **判词**：loud 但口音不对：契约说好都用 FormatException 说方言，一半哨兵却在讲 BCL 官话——失败保真了，失败的消息没保真。
- **最小修复**：这几处包一层 ValueKind 预检（仿 ReqStr :284–286 的既有范式），统一抛带字段名的 FormatException；Signature.Of 的重复 claim 属语义错误可保留 ArgumentException，但需在 Parse 文档注释里如实区分两种异常。
- **verdict**：fixable
- **testHint**：对 scene/type/kind/mode/budget 各注入错型值，断言 `Throws<FormatException>`（当前为红）。

### F5 · LOW · scope 形状不完整时伪造空名身份，与 resource 侧的非空纪律不对称

- **位置**：EffectScriptContract.cs:113–119
- **证据**：`{"scope":{}}` ⇒ `Scene("")`；`{"scope":{"type":"method"}}` ⇒ `Method("")`。resource 侧 ReqStr 明确拒绝空串（:284–286），scope 侧却默默制造一个不存在的作用域身份——gate(3) 按 `(r, e.Scope, mode)` 分组，空名身份会参与冲突判定与违例归因。缺 type 默认 Scene 是有注释的刻意选择（与 SerializeScope 场景形态对称），可留；但**零字段对象**应视为形状非法。
- **判词**：同一份契约，资源不许无名，作用域却能凭空捏一个匿名者进来——严格性不该因构造子而异。
- **最小修复**：ParseScope 开头 `if (!hasScene && !el.TryGetProperty("type", out _)) throw new FormatException("scope 须含 scene 或 type");`
- **verdict**：fixable
- **testHint**：`Assert.Throws<FormatException>(() => Parse("""{"scope":{}}…"""))`。

### F6 · LOW · Parse 报错不带位置坐标，AI 回修闭环缺少「在哪改」的半边信息

- **位置**：EffectScriptContract.cs:279（`$"缺少字段: {prop}"`）、:164/:171 等
- **证据**：500 个事件的剧本在第 437 个事件缺 `lifetime`，报错只有 `缺少字段: lifetime`——Violation 侧精心携带 (t, resource, detail) 反例供 AI 回修，Parse 侧却不给事件下标/claim 下标。F2 的修复（传入 `events[{i}]` 层名）顺手即可解决。
- **判词**：给 AI 的错误消息省掉了地址，等于让回修者在停车场里找一辆没记车牌的车。
- **最小修复**：随 F2 一并落地（层名带下标）；独立实施则在 Require 增加 layer 参数。
- **verdict**：fixable（可与 F2 合并）
- **testHint**：断言异常消息包含 `"events[437]"` 子串。

### 保真正面记录（round-trip 做对了的地方，避免误伤）

- budget ⊤ 往返：序列化写 `"⊤"` 字符串（:206–209）、Parse 接受 `"⊤"/"inf"`（:199–202）——C# 合法的无上限预算不再翻转为 0。
- 归一化幂等：Self("signal_bell") 经 ToJson 写成规范形 `{"signalBus":"bell"}`（探针实测），二次往返稳定，语义等价于 §3.1.4a Normalize——这是「规范化」而非「改写」，无罪。
- CapsChecked 已正确接线（EffectScript.cs:328 三参构造，探针实测 capsChecked=1）——综合表 O9 的「测试绿灯证据链悬空」在本机已闭合（312 绿）。

---

## 三、TOP-3

1. **F1（Blocker）**：§4 旗舰 JSON 无法 Parse——AI 契约的第一公里断裂，且零测试守护。一行文档都不用改代码逻辑，但要立「文档示例即测试夹具」的规矩。
2. **F2（HIGH）**：事件/claim 层未知键静默吞噬——旧根因 A「静默改写数据」的转世，只是从 Serialize 臂搬到了键匹配层；ω=⊤ 被无声降级为 1 的探针案例是铁证。
3. **F3（HIGH）**：default(LoopCount) 毒值穿透「构造即合法」——B 根因宣称已修的部分其实留着狗门，DivideByZeroException 实测可达。

---

## 四、证据清单（本轮实际读取/验证）

- 源码精读：EffectScriptContract.cs（全文）、EffectScript.cs（全文）、Objects.cs（全文）、Numeric.cs:1–120、DerivedMetrics.cs（全文）、Algebra.cs:1–135、ApiMapping.cs（grep 取证）
- 文档：EFFECT_SCRIPT.md（全文）
- 测试取证：EffectScriptContractTests.cs、EffectScriptEdgeTests.cs（grep ToJson/RoundTrip/FormatException）、CapsChecked 全仓 grep
- 动态验证：`dotnet test tests/Cosmos.EffectAlgebra.Tests` = 312 passed / 0 failed（net10.0.103）；TEMP 探针工程 9 组用例（文档示例 Parse、非字符串 scope/kind/mode、重复 claim、budget true/-3、CapsChecked、default LoopCount、未知事件键、ToJson Tree、Normalize 往返）
- 行号以本轮工作区快照为准。
