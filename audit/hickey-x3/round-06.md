# Cosmos.EffectAlgebra 对抗性审计 · 第 6 轮 · Naming & Hammock-Driven Development 视角

> 审计人视角：Rich Hickey（命名即规格；simple ≠ easy）。本轮独立上下文，未读取 `audit/` 下任何历史报告。
> 方法：通读入口文档（README.md / EFFECT_SCRIPT.md）→ 通读 L1 全部源文件与 SampleGame → 以仓库外探针工程实测「只凭文档和签名能否预测行为」。所有探针结论均实际运行验证。

---

## 核实矩阵（对仓库自带声明逐条裁决）

本轮不读历史审计，改核 **README「已知语义锐边」与 EFFECT_SCRIPT 正文的自家承诺**：

| # | 声明（出处） | 裁决 | 证据 |
| --- | --- | --- | --- |
| V1 | `Unknown` = 最弱兼容 = fail-open，未知冲突静默放行（README.md:56） | **仍在** | Algebra.cs:15 `Resolve(m)=>m==Mode.Unknown?Mode.Use:m`；Algebra.cs:24 use 与任意兼容。探针 P5 实测 `IsCompatible(Unknown,Create)=True` |
| V2 | `loop:"⊤"` 居民层豁免守恒，`lifetime:[1,⊤]`（ω 有限）仍入 net 并报警——同语义相反行为（README.md:57） | **仍在（如实披露，但属设计纠缠）** | EffectScript.cs:161（gate1 仅累计有限 ω）、EffectScript.cs:286（闭包跳过 ω=⊤）、EffectScript.cs:112（hi=⊤ 采 maxFinite+1 仍入峰值）。「常驻」一词在框架内有两个不可互换的实现路径，用户须自行分辨 |
| V3 | `Claim.Size` 省略 ≠ 未知 ⇒ `[1,1]`（README.md:58） | **已修/表述准确** | Objects.cs:134 `Size ?? Interval.Default`；Numeric.cs:92 `Default=[1,1]`；注释明言显式 `Exact(0)` 经可空类型与 null 区分 |
| V4 | EFFECT_SCRIPT.md:81 称 `At(t)` 用 `Combination.Loop(Footprint, ω, loopScope)`，loopScope 为自由变量 | **部分修** | EffectScript.cs:70-77 实现已改用 `e.Scope`（Event 自带 scope，OPEN-1），但文档 :81 正文伪码仍保留旧自由变量写法，仅 :225 变更表提及。读文档的人拿到的还是过期签名 |
| V5 | EFFECT_SCRIPT.md §2.2 `Audit(Budget cap)` 签名 | **一致（有分叉）** | EffectScript.cs:105 `Audit(Budget cap)` + EffectScriptContract.cs:263 无参重载 `Audit()`（用自带 Budget）。两个入口并存，`Audit(cap)` 会**忽略**实例上存的 Budget——签名看不出这一差别 |

---

## 新发现

严重级：HIGH（会直接导致错误使用）/ MED（可预测性受损或语义漂移）/ LOW（面积与卫生）。

### [F1][HIGH] 入口文档唯一的端到端示例 JSON 无法解析；修好解析后它本身就是一份泄漏剧本

- 位置：EFFECT_SCRIPT.md:135-150 ↔ EffectScriptContract.cs:57,161-162
- 证据：
  - 文档示例每个 event **没有顶层 `scope` 字段**（EFFECT_SCRIPT.md:137,144），而 `ParseEvent` 执行 `Require(ev, "scope")`（EffectScriptContract.cs:57）；
  - 文档示例 resource 写成嵌套对象 `{"gpu": {"bufferId":"mesh1"}}`（EFFECT_SCRIPT.md:139），契约却要求非空**字符串**（EffectScriptContract.cs:161 `ReqStr(gpu,"gpu")`）。
  - 探针实测：文档 JSON 逐字投喂 → `FormatException: 缺少字段: scope`；仅补 scope → `FormatException: resource.gpu 须为非空字符串`；两处都修好后 `Audit.Passed=False`，违例为 `Leak res=Gpu(mesh1) @180`——mesh1 的 create 无配对 release。
- 判词：你把「AI 回修闭环」当作卖点，却在门口放了一份自己都编译不过、语义上还在漏资源的样例——AI 学的不是你的代数，是你的坏习惯。
- 最小修复：更新 §4 示例（event 加 `"scope"`、resource 改字符串形态），并把 mesh1 补一条 release 或在示例旁标注「此剧本应报 Leak」作为教学反例。

### [F2][HIGH] `LoopCount` 名实不符：名字说「循环次数」，语义是「并发副本数」，且两处官方注释互相打架

- 位置：DerivedMetrics.cs:10-30 ↔ EffectScript.cs:31-34 ↔ EFFECT_SCRIPT.md:38
- 证据：DerivedMetrics.cs:12 注释「循环次数 ω（§3.2.5）」；EffectScript.cs:33-34 注释「并发实例数……并发副本，非时间重复」；EFFECT_SCRIPT.md:38 自己承认这是「重新解释」并要求「后续维护者须避免将二者混淆」。
- 判词：当一个名字需要一段「请勿按字面理解」的免责声明时，错的不是读者，是名字。时间重复与瞬时并发是两个概念，被塞进同一个类型，靠注释站岗。
- 最小修复：剧本层引入别名 `Concurrency Count`（如 `readonly record struct Concurrency { NatStar Peaks; }`）或在 EffectEvent 上把属性改名 `Loop→Copies`，保留 LoopCount 仅作 PDR 兼容视图。

### [F3][HIGH] `[EffectOverride]` 一名三义：逃逸通道、L2 生成开关、还被健康代码当「参与审计」的门票

- 位置：README.md:38 ↔ EffectAlgebraGenerator.cs:61-62 ↔ samples/GodotIntegration/SampleGame.cs:19,25,28,72,76 ↔ EffectAttributes.cs:16-22
- 证据：README:38 定义其为「有意偏离守恒」的逃逸通道；Generator.cs:61「仅挑选带 [EffectOverride]/[AcceptDeviation] 的方法；其余忽略」——它同时是 L2 codegen 的 opt-in 总闸；SampleGame.cs:25/28/76 中 Spawn/Despawn 这类**完全守恒的健康方法**也被迫标注，reason 填的是「spawn/despawn 配对」这种配对说明而非偏离证据。
- 判词：一个叫 Override（覆盖/豁免）的特性被用来表达 Include（纳入），reason 字段被迫承载无关内容——审查通道从此堆满假阳性证词，真逃逸混在其中没人再看。
- 最小修复：给 L2 生成加独立标记（如 `[EffectTracked]`），让 `[EffectOverride]` 回归纯豁免语义；SampleGame 相应换标。

### [F4][MED] `Peak.Compute` 把**所有资源**的 size 上界混加成一个数——名字许诺每资源峰值，实现给出跨量纲总和

- 位置：Algebra.cs:114-123 ↔ EFFECT_SCRIPT.md:112 ↔ DerivedMetrics.cs:56-58
- 证据：Algebra.cs:122 对 scope 内全部非 release claim 无差别 `sum += size.Hi`；探针 P3：GPU buffer(10) + Memory(3) ⇒ `Derived.Peak(sig, Global)=13`。而 EFFECT_SCRIPT.md:112 的公式是 per-resource `Peak(At(t),scope) ≤ Budget.Caps[r]`；Audit 内部 gate(2) 也是按资源分开算（EffectScript.cs:150,180-188）。
- 判词：同一个「Peak」名词，公共 API 一个语义（混量纲总和）、文档一个语义、审计器第三个实现——三个真相，零个可预测。
- 最小修复：要么让 `Peak.Compute` 返回 `IReadOnlyDictionary<ResourceId,NatStar>`，要么改名 `TotalOccupancyBound` 并注明跨资源求和不构成预算判定依据。

### [F5][MED] `DeviationVal.ExceedsThreshold`：⊤ ⇒ false，是藏在谓词里的 fail-open

- 位置：Numeric.cs:128 ↔ Deviation.cs:20-27（注释口径）↔ SignedNet.cs:92（对照组）
- 证据：`public bool ExceedsThreshold(double t) => !IsTop && Value > t`——探针 P4：`Top.ExceedsThreshold(0.2)=False`。注释自称「视为需人工界定，避免掩盖」，但返回 false 在调用方就是「未超限、不报警」；对比 `SignedInterval.ContainsZero`（SignedNet.cs:92）对 ⊤ 返回 false 触发报警（fail-closed）。同为 ⊤，两个谓词方向相反。
- 判词：「unknown ⇒ not exceeding」是把最危险的输入翻译成最安心的输出——掩盖不是被避免了，是被 API 签名合法化了。
- 最小修复：改为三态 `ThresholdVerdict { Under, Exceeds, Unverifiable }`，或至少让 IsTop 返回 true 并由 Detail 标注「需人工界定」。

### [F6][MED] `Compatible.IsCompatible(Mode,Mode)` / `Claim.CompatibleWith(Claim)` 只比较 mode——claim 级的名字，mode 级的判断

- 位置：Algebra.cs:17-33 ↔ Objects.cs:138 ↔ EFFECT_SCRIPT.md:113
- 证据：`CompatibleWith` 直接丢弃 other 的 Resource/Scope（Objects.cs:138）；探针 P2：不同资源（Gpu vs Memory）、不同 Scene 的两个 Create ⇒ `CompatibleWith=False`（误报冲突）；同资源 Use×Create 则恒 True。EFFECT_SCRIPT.md:113 却向读者许诺「同 scope 兄弟 Claim 用 Compatible.IsCompatible 全通过」——真正的资源/scope 分组逻辑藏在 Audit 私有实现里（EffectScript.cs:150,197-199,231-240）。
- 判词：把半截判断挂上全称名字，等于邀请调用者写出看起来对、其实漏检又误报的守卫。
- 最小修复：改名 `ModesCompatible(Mode,Mode)`；`CompatibleWith` 要么补齐 resource 归一相等 + scope 分组判断，要么删除。

### [F7][MED] JSON 线格式复用 `"scene"` 键携带 method/type 作用域名

- 位置：EffectScriptContract.cs:205-206 ↔ :95-97
- 证据：`SerializeScope(Method m) => { ["type"]="method", ["scene"]=m.Name }`——产出 `{"type":"method","scene":"Spawn"}`，「scene」字段里装的是方法名；`ParseScope` 缺 type 时又默认按 `Scene(name)` 解释（:95-97），同一份 JSON 少一个字段就换了含义。
- 判词：键名是合同文字。写 `scene` 装 method，等于让每个调试这个 wire format 的人先怀疑自己的眼睛。
- 最小修复：统一为 `{"type":"method","name":"Spawn"}`，Scene 用 `{"type":"scene","name":"Battle"}`，Global 保持无 name；解析侧拒绝缺 name 的非 global 类型。

### [F8][MED] `Union` 与 `Join`：两个近义名字，两种不同的合并语义，签名层面不可区分

- 位置：Objects.cs:184-194（Union＝集合去重并）↔ Objects.cs:196-215（Join＝同键 size 取 `Interval.Merge` 包络）
- 证据：`Union(Signature.Of(c[10,10]), Signature.Of(c[50,50]))` 得到**两条** Claim；`Join` 同输入得**一条** `[10,50]`（Objects.cs:196-215 注释自述）。二者都是「合并两个 Signature」，返回类型相同、名字相近、结果结构不同，选择权完全靠读注释。
- 判词：API 里最贵的东西是「不用读实现也能预测」。Union/Join 这一对外观孪生、内质异构的组合，正是逼用户去读实现的典型。
- 最小修复：文档级区分即可救急（XML doc 写明「Union=多假设并存，Join=分支包络」）；更优是给 Join 更名 `MergeBranches`。

### [F9][MED] EFFECT_SCRIPT.md §2.1 的 C# 片段与真实构造函数漂移：文档 3 字段、代码 4 字段必填、同页还写着「5 字段」

- 位置：EFFECT_SCRIPT.md:49-53,62 ↔ EffectScript.cs:17-46
- 证据：文档片段只有 `Lifetime/Footprint/Loop` 三属性；代码 `EffectEvent(Interval, ScopeId, Signature, LoopCount)` 四参必填（EffectScript.cs:35-40），另有无 ω 三参便捷构造（:43-45）；文档 :62 却称「5 字段位置记录」——同一页内自相矛盾。
- 判词：入口文档里连字段数都对不上，读者只能放弃文档、直接翻源码——吊床测试就地阵亡。
- 最小修复：同步片段为 4 字段版本，修正「5 字段」表述，标注 Scope 为何必须存在（归因需要）。

### [F10][LOW] `Rid` / `StringName` 别名与 Godot 同名类型正面碰撞

- 位置：Objects.cs:11,14
- 证据：`public readonly record struct Rid(string Value)`、`StringName(string Value)`；消费工程同时 `using Godot; using Cosmos.EffectAlgebra;` 时引用裸名 `Rid` 即 CS0104 歧义。同文件 `NodePathOrUnknown`（Objects.cs:41）表明作者意识到了撞名风险，却只给 NodePath 绕了路。
- 判词：给别人的类型起别人已经占用的名字，省了两行映射代码，赔上每个消费文件的消歧噪音。
- 最小修复：更名 `PrimRid` / `PrimStringName`（或 `EaaRid`），映射层内部转换。

### [F11][LOW] `Violation.Kind` 是字符串魔法值，不是 enum

- 位置：EffectScript.cs:381（`public string Kind`）↔ :245,252,259,295（赋值点散落 "NegativeDip"/"PeakExceeded"/"CompatibleConflict"/"Leak"）
- 证据：消费方要匹配违例类型只能硬编码四段字符串；拼写错误无编译期保护。讽刺的是同文件就有穷举 enum 先例（Objects.cs:112 `enum Kind`——还因此造成 `Violation.Kind` 与 `Kind.Read/Write/Occupy` 同名异物，见下）。
- 判词：同一个库里 `Kind` 一词二义：一个是 enum，一个是 string——命名纪律在自己的产物里先失守了。
- 最小修复：新增 `enum ViolationKind { Leak, NegativeDip, PeakExceeded, CompatibleConflict }`；`Violation.Kind` 改名 `Type` 并用新 enum。

### [F12][LOW] `Mode.Unknown`：名字零提示地携带「按 Use 静默放行」的策略决定

- 位置：Objects.cs:117 ↔ Algebra.cs:15,24 ↔ EffectScriptContract.cs:151（JSON 接受 `"unknown"`）
- 证据：README.md:56 有书面披露，但签名层面 `IsCompatible(Mode.Unknown, …)` 与 `Mode.Use` 行为一致（探针 P5=True），JSON 契约还允许 AI 剧本直接写入 unknown mode 从而整条 claim 免检。
- 判词：把策略藏进枚举值可以接受，前提是名字肯说实话——`Any`/`Permissive` 都比 `Unknown` 诚实，因为后者暗示「系统不知道」，而非「系统决定不查」。
- 最小修复：至少在 XML doc 与 Violation 输出中把 Unknown 放行的 claim 显式列为 `UnverifiedPass` 类提示（不报警、但要可见）。

---

## 误写推演（AI 编码代理只凭签名 + 文档写代码）

**推演 A ——照抄 §4 官方示例（对应 F1/F9）**
AI 收到任务「生成视觉剧本 JSON 并验证」，唯一参考是 EFFECT_SCRIPT.md §4。它产出带嵌套 resource、无顶层 scope 的 JSON → `EffectScriptContract.Parse` 抛 FormatException（探针 P1、P7 实测复现两条不同异常）。最可能的后续行为：为「修好」而 try/catch 吞异常或删掉 footprint 中的可疑项——最终交付一份从未通过验证的剧本；而即便 AI 正确补全语法，文档示例本身的 mesh1 泄漏会让它学到「Leak 是常态」（探针 P8）。

**推演 B ——按名字组合公共 API 自建检查器（对应 F4/F6/F5）**
AI 看到 `Claim.CompatibleWith`、`Derived.Peak(sig, scope)`、`DeviationVal.ExceedsThreshold`，写出通用守卫：
```csharp
if (!a.CompatibleWith(b)) conflicts.Add(...);            // 不同资源的 create×create 全部误报（探针 P2=False）
if (Derived.Peak(atT, global) > myBudget) alarm();       // GPU+内存混加成 13，预算失去量纲（探针 P3）
if (!dev.ExceedsThreshold(0.2)) shipIt();                // ⊤（不可校准）静默放行（探针 P4）
```
三行代码全部「编译通过、类型正确、语义全错」，且没有任何诊断拦截——这正是命名失职的账单：错误不在调用者的逻辑里，在他不得不做的合理猜测里。

---

## TOP-3（本轮最重要）

1. **F1** 入口文档唯一端到端示例不可解析且本身泄漏（EFFECT_SCRIPT.md:135-150）——吊床的第一级台阶就是断的。
2. **F2** `LoopCount` 名实分裂 + 双源注释互相矛盾（DerivedMetrics.cs:12 ↔ EffectScript.cs:33）——需要免责声明的名字必须换掉。
3. **F3** `[EffectOverride]` 一名三义，健康代码被迫伪造豁免理由（Generator.cs:61-62 ↔ SampleGame.cs:25）——审查通道被假阳性淹没只是时间问题。

## Usability 裁决

**有条件通过（whitelist/L2-L3 路径），AI 剧本契约路径不通过。**
- 从 README「5 分钟上手」进入、只用 acquire/release 白名单审计的游戏开发者：可以在不读实现的情况下成功走通（README.md:28-38 步骤与 ApiMapping.cs 白名单一致，SampleGame 可复现），这条路径的 Hammock 测试成立。
- 从 EFFECT_SCRIPT.md 进入的 AI/剧本用户：必学概念约 10 个（NatStar/⊤、Interval、ZStar/SignedInterval、ScopeId、Kind、Mode、Claim、Signature、LoopCount、Budget），且第一个权威示例就会抛异常——**不通过**。修复 F1/F7/F9 之前，不应宣传 §4 契约可用。

---

## 证据清单（本轮实际读取）

- README.md（全文）
- EFFECT_SCRIPT.md（全文）
- src/Cosmos.EffectAlgebra/Objects.cs、Algebra.cs、Numeric.cs、SignedNet.cs、DerivedMetrics.cs、Deviation.cs、ApiMapping.cs、EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs（全文）
- src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs（关键行 61-62,105,139）
- src/Cosmos.EffectAlgebra.Runtime/*.cs（公共面扫描，命名盘点：Fiber/Coeffect/InverseClaim/EdgeKind 等）
- src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj（TFM 排除表）
- samples/GodotIntegration/SampleGame.cs（全文）
- 仓库外探针 `/tmp/eaa-probe`（net10.0，ProjectReference 至本库）：8 项实测 P1-P8 全部执行，输出见各 finding 证据栏；未修改任何仓库源文件。
