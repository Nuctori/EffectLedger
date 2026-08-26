# Cosmos.EffectAlgebra 对抗性 API 审计 · Round 01 —— Simple is not Easy 视角（Rich Hickey）

> 独立上下文第 1/10 轮。未读取 `audit/` 下任何历史报告；所有结论基于本轮实际读取的源码与**实际编译运行**的误用推演（harness 位于系统临时目录，未触碰项目源文件）。

---

## 核实矩阵

本轮为独立上下文首轮，无历史结论可继承；README 自称的「已知语义锐边」逐条核实如下：

| # | README 自述（行号） | 裁决 | 证据 |
| --- | --- | --- | --- |
| K1 | 「Unknown = 最弱兼容 = fail-open，静默放行」（README.md:56） | **仍在，且比自述更糟**：代码注释自称「fail-closed 最弱兼容」（Algebra.cs:14），实际 Unknown→Use 与任意 mode 兼容（Algebra.cs:15-25），实测 `IsCompatible(Unknown,Unknown)=True`——这是 fail-open 却被注释贴上 fail-closed 标签 | Algebra.cs:15 + 本轮 S3 实测 |
| K2 | 「`loop:"⊤"` 居民层静默豁免泄漏检测；同常驻语义两种相反行为」（README.md:57） | **仍在**：Audit gate(1) `enter && !e.Loop.Count.IsTop` 跳过 ⊤ 事件（EffectScript.cs:163），闭包检查同样跳过（EffectScript.cs:296）；ω=⊤ 事件永不产生 Leak/NegativeDip | EffectScript.cs:163,296 |
| K3 | 「Claim.Size 省略 ≠ 未知 ⇒ [1,1]」（README.md:58） | **仍在，设计锁死但方向存疑**：缺省被膨胀成「精确 1」而非「未知 ⊤」，见 F5 | Objects.cs:133 |
| K4 | 「未命中白名单的 API 静默无保护、无警告」（README.md:40） | **仍在**：L3 按归一化方法名字符串匹配白名单（EffectAlgebraAnalyzer.cs:152,292-296），语义押在标识符拼写上 | ApiMapping.cs:58, Analyzer:292-296 |
| K5 | DELIVERABLE 称 212 测试 / README 称 453 测试 | **文档漂移**：两份门面文档数字不一致，至少一份过时 | README.md:66 vs DELIVERABLE.md:9 |

---

## 新发现

严重级：HIGH = 第一次用错得到错误结果且无信号；MED = 需读实现才能预测行为；LOW = 卫生问题。

### HIGH

**F1 · 官方 §4 契约示例喂不进自家解析器——spec 与 guardrail 已经分叉**
- 位置：EFFECT_SCRIPT.md:136-151 vs EffectScriptContract.cs:57
- 判词：你们把 JSON 契约写成 AI 的圣经，然后圣经里的第一个例子在 `Parse` 第一站就炸 `FormatException: 缺少字段: scope`。文档说「AI 改 JSON 重投」——AI 第一步是改出能 parse 的 JSON，因为文档自己都做不到。这不是 bug，是 spec 已死的体征：没人跑过这条黄金路径。
- 实测：S1 harness 输出 `S1: FormatException: 缺少字段: scope`（§4 示例事件层无 `"scope"` 字段，ParseEvent 强制要求）。
- 最小修复：给 EFFECT_SCRIPT.md §4 示例每个 event 补 `"scope": {"scene":"Battle"}`，并加一条 round-trip 测试：`Parse(ToJson(s))==s` 且「文档示例字符串」作为 fixture 常驻测试。

**F2 · budget 键打错一个字母 = 无限量预算，静默通过**
- 位置：EffectScript.cs:236-244（gate(2) 仅遍历 `cap.Caps`）；EffectScriptContract.cs:196-208（键解析）
- 判词：预算检查是「对 cap 表里出现的资源做检查」，而不是「对剧本里存在的资源核对 cap 是否覆盖」。cap 键 `Gpu:MESH1` 与 claim 的 `gpu:mesh1` 大小写不归一、无交叉验证：错键的 cap 永远匹配不到 claim，峰值 1000 对 cap 2 照样 `Passed=True`。这是把「策略」做成 opt-in 又不给覆盖性反馈——最贵的沉默。
- 实测：S2-typo harness：守恒剧本 + 峰值 1000 + cap 2 写错键 → `Passed=True, kinds=[]`；同 cap 写对键 → `[PeakExceeded, PeakExceeded]`。
- 最小修复：Audit 结束时计算「caps 中未被任何 claim 资源命中的键」，存在即报 `UnmatchedBudgetKey` violation（或至少 Passed=false）。资源 id 至少做 OrdinalIgnoreCase 归一或显式声明大小写敏感。

**F3 · `Mode.Unknown` 一词两义：兼容 gate 里是 Use，net gate 里是 Create**
- 位置：Algebra.cs:15（Resolve→Use）；Algebra.cs:63 / EffectScript.cs:169-172（非 Release 一律取正号 ⇒ Unknown 计为 create）
- 判词：同一个 token 在三道 gate 里被解析成两个相反的东西：compat 说「unknown 谁都能共存」，net 说「unknown 是一次创建」。AI 不确定模式时顺手写 `"mode":"unknown"`，得到的不是显式失败，而是一张「无冲突但有 Leak」的诊断单——两条信息各自半真，拼起来没人能推理。程序员不该需要读实现才知道 Unknown 今天扮演谁。
- 实测：S3 harness：两个 mode=unknown 同资源 occupy → `IsCompatible=True`（零 CompatibleConflict），闭包报 `Leak [2,2]`。
- 最小修复：契约层拒绝 `"mode":"unknown"`（ParseMode 抛 FormatException），或在 Violation 中显式标注「Unknown 按 Create 计入 net」。二选一，别两头讨好。

**F4 · footprint 里逐 claim 的 `scope` 是强制必填却被整体丢弃——契约强迫用户填写无效数据**
- 位置：EffectScriptContract.cs:131-137（Require per-claim scope）；EffectScript.cs:87（At 经 `Combination.Loop(e.Footprint, e.Loop, e.Scope)` 把所有 claim 重标到 event.Scope）；EffectScript.cs:177（gate(3) key 用 `e.Scope`）
- 判词：契约逼 AI 给每个 claim 写 scope，然后 At/Audit 全程用 event 级 scope 覆盖之。claim 级 scope 是仪式，不是语义——用户以为自己在表达图层层级，实际写的是会被撕掉的草稿。EFFECT_SCRIPT.md:140-147 的示例还在示范这种无效输入。强制字段没有效果 = 合约诈骗。
- 实测：S4 harness：两个 event 的 claim 分别声明 `Scene(LayerTop)`/`Scene(LayerBottom)`、event scope=Global → 冲突按 Global 归因，`At()` 返回的 claim scope=Global，LayerTop 荡然无存。
- 最小修复：要么让 claim.Scope 参与分组（删掉 Loop 的 scope 重写），要么从 JSON 契约中删除 claim 级 scope 并在 Parse 时忽略之（显式标注）。禁止「必填但无效」。

### MED

**F5 · size 缺省 = 「精确 1」，与全框架的 ⊤ 哲学相逆**
- 位置：Objects.cs:133（`Size = Size ?? Interval.Default`）；NetTable.Compute/EffectScript 多处 `?? Interval.Default`
- 判词：整个类型系统的卖点是「未知 ⇒ ⊤，绝不假装知道」（NatStar/SignedInterval 处处 fail-closed），唯独「没填 size」被翻译成「恰好 1」——把数据缺失伪装成精确知识。README:58 说这是设计锁死；锁死的是错误的方向：省略应映射到 ⊤（诚实），而不是 Default（顺手的谎言）。AI 漏写 size 时，峰值被低估为 1，预算检查形同虚设。
- 最小修复：至少在 L1 提供 `Interval.Unknown = [⊤,⊤]` 并讨论把缺省切过去；短期内契约层对省略 size 的 claim 发 warning。

**F6 · 三个「守恒」入口、两种「峰值」语义——同一问题不同答案**
- 位置：Algebra.cs:47-100（NetTable：scope 过滤 + ContainsZero）；EffectScript.cs:290-330（闭包 net：无 scope 过滤、event-scope 归因、Leak/NegativeDip 两 gate）；DerivedMetrics.cs:44-56（Derived.IsConserved 又一封装）；Algebra.cs:113-127（Peak.Compute 不滤 Kind，read/write 桶也计入）vs EffectScript.cs:171（gate(2) 只算 OccupyClaims）
- 判词：「我的剧本守恒吗？」「我的峰值多少？」各有两套实现、 subtly 不同答案。Peak.Compute 连 read claim 的 size 都加进占用峰值（实测 read[50]+occupy[7]⇒57），而剧本审计只认 occupy——同一个词 `Peak`，两套量纲。API 面积是最贵的承诺，重复入口就是复利计息的债。
- 最小修复：Peak.Compute 加 `if (c.Kind != Kind.Occupy) continue;`；NetTable.IsConserved 与剧本闭包检查收敛到同一私有核心。

**F7 · ω 一词跨层换义：PDR 说「循环次数」，剧本说「并发副本」**
- 位置：DerivedMetrics.cs:7,12（注释仍叫「循环次数」）；EFFECT_SCRIPT.md:38（OPEN-N2 承认重新解释）；Combination.Loop 文档混用
- 判词：承认了「二者概念含义不同，维护者须避免混淆」——把混淆风险写进文档不算解决混淆，只是给纠缠办了登记手续。同一个 `LoopCount.Of` 在两层有两个含义，类型名本身就在撒谎（它根本不管循环）。
- 最小修复：剧本层新增别名 `Concurrency = LoopCount`（或直接改名），旧名保留一个迭代期作 obsolete。

**F8 · `[EffectOverride]` 的 OverrideSize 接受任何 double——逃逸通道自己不设闸**
- 位置：EffectAttributes.cs:35-37
- 判词：框架的铁律是「类型能约束的用类型」，却在唯一的逃逸通道上写「调用方须保证 ≥1，本层不重复校验」。`OverrideSize=-5`、`double.NaN` 构造期照单全收，失败推迟到下游某个说不清的地方。守门人对越狱者免检。
- 最小修复：构造 setter 校验 `≥1 && !NaN`，非法抛 ArgumentOutOfRangeException——与本属性 reason 的 fail-fast 同一标准。

### LOW

**F9 · `new Budget(null)` 可构造，炸点延迟到 Audit 内部 NRE**
- 位置：EffectScript.cs（Budget 构造子无 null 检查）；gate(2) `foreach (var kv in cap.Caps)` 直接 NRE
- 判词：EffectScript 构造器挡了 null（`budget.Caps != null ? … : Budget.None`），裸 Budget 没有——同一个 null 两种命运。fail-fast 要在出生时，不在葬礼上。
- 最小修复：Budget 构造子 `caps ?? throw new ArgumentNullException`。

**F10 · ParseScope 缺 type 静默默认 Scene("")**
- 位置：EffectScriptContract.cs:88-103
- 判词：`{"scene":""}` 或漏写 name 得到 `Scene("")`——又一个静默默认值；刚修完 resource 的静默兜底（auditR2/R4 C2），scope 这边留着同样的坑。
- 最小修复：Scene/Method/Type 分支要求 name 非空，否则 FormatException。

**F11 · NegativeDip 违例在每个采样点重复刷屏**
- 位置：EffectScript.cs:231-238（每 samplePoint 对同一负 net 再报一次）
- 判词：反例的价值在于最小化；同一个负陷报 N 遍是把噪音当信息卖给回修的 AI。
- 最小修复：同 (resource,kind) 只保留首个违例。

---

## 误用场景对抗性推演（实跑记录）

Harness：独立 console 工程，引用 `src/Cosmos.EffectAlgebra/bin/Debug/net10.0/Cosmos.EffectAlgebra.dll`，位于系统临时目录，项目零改动。

| # | 「AI/新手会这样写」 | 代码路径追踪 | 后果（实测输出） |
| --- | --- | --- | --- |
| S1 | 照抄 EFFECT_SCRIPT.md §4 黄金示例投给 `Parse` | EffectScriptContract.Parse→ParseEvent→`Require(ev,"scope")`(:57) 抛 | `FormatException: 缺少字段: scope` ——官方示例不可解析（F1） |
| S2 | 守恒剧本 + 预算键手滑写成 `Gpu:MESH1` | gate(2) 遍历 cap.Caps(:236)，错键 normalize 后查不到 peakSum 条目，p=0≤2 通过；无任何「cap 未命中」反馈 | `Passed=True`，峰值 1000 vs cap 2 从未被比较（F2） |
| S3 | 不确定模式就写 `"mode":"unknown"` | gate(3)：Resolve(Unknown)→Use(:15) ⇒ IsCompatible=True 零冲突；gate(1)/closure：非 Release 取正号(EffectScript.cs:169-172) ⇒ 计两次 create | 无 CompatibleConflict，仅 `Leak [2,2]`——同一 token 两套语义（F3） |
| S4 | 用 claim 级 scope 表达图层层级（文档教的） | At(:87)/gate(3)(:177) 均以 e.Scope 覆盖 claim.Scope | 图层信息消失，冲突按 Global 归因；`At()` 返回 claim scope=Global（F4） |

## TOP-3

1. **F2 budget 键静默失配**——安全机制的失效模式必须是响，不能是静音。这是全框架最锋利的一根 fail-open 针，藏在最该 fail-closed 的地方。
2. **F4 必填但无效的 claim 级 scope**——契约强迫用户输入被丢弃的数据，比缺字段更毒：用户以为自己表达了什么。
3. **F1 官方示例喂不进自家解析器**——spec 已与实现分叉的直接物证；修好它只需五行，但它说明黄金路径无人走过。

---

## 最终 usability 裁决

这个框架的数学内核（NatStar/SignedInterval 的 ⊤ 闭环、区间代数、扫换线等价性）是**简单**的：正交、值语义、可证明。但它外包给使用者的那圈「便利层」——JSON 契约、budget 壳、mode 语义——是**容易**的：字段必填却无效、默认值替你撒谎、错误键静默放行。第一次用错的 AI 或游戏开发者拿到的大多是 `Passed=True` 或一张语义分裂的诊断单，而非显式失败。**裁决：内核 simple，外壳 easy，当前形态不建议直接暴露给自动化生产者；先修 F1/F2/F3/F4（预计一天工作量），再谈「AI 产出剧本直接投喂」的承诺。**

---

## 证据清单（本轮实际读取）

- README.md（全文）
- EFFECT_SCRIPT.md（全文，§4 示例经 harness 实测）
- DELIVERABLE.md（全文）
- src/Cosmos.EffectAlgebra/：Algebra.cs、ApiMapping.cs、Objects.cs、Numeric.cs、SignedNet.cs、DerivedMetrics.cs、Deviation.cs、EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs（全文）
- src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs（EAA0901/Canonical 相关段，grep 定位）
- samples/GodotIntegration/SampleGame.cs、samples/AnalyzerConsumer/Game.cs（全文）
- 实跑：临时 harness（6 组场景，输出见误用推演表）
