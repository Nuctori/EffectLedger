# Cosmos.EffectAlgebra 对抗性审计 · 第 4 轮 — Specs, Guards, and Correctness（Rich Hickey 视角）

> 角度：正确性来自规格，不是来自防御性编程。本轮独立取证，未读取 audit/ 下任何历史报告。
> 探针：仓库外临时工程 `/tmp/hickey-r04-probe`（引用 bin/Release/net10.0 DLL 实测），未修改任何仓库源文件。

## 核实矩阵（PDR / README / EFFECT_SCRIPT 声称 vs 代码与测试实况）

| # | 历史声称 | 出处 | 裁决 | 证据 |
|---|---|---|---|---|
| V1 | Compatible 是全函数 + 对称 + CONFLICT 集闭合（MA-009 收口） | PDR §3.2.3 | **在位（内核）**。5×5 穷举测试真实存在；但 §3.2.2 的前置条件只在 `Parallel` 里对 **occupy 桶**执行——见新发现 F1，规格被实现了四分之一 | Algebra.cs:11-27；PropertyTests.cs:96-131；DerivedMetrics.cs:55-62 |
| V2 | ⊔ 是 join-semilattice（幂等/交换/结合，MA-006） | PDR §3.2.4 | **在位（实现正确，测试缺口）**。本轮探针 2000 组随机输入验证 Join 幂等/交换/结合/吸收全部成立；但 L1 层无 Signature.Join 直接性质测试（AlgebraLawsTests 只测 Interval.Merge），EFFECT_SCRIPT.md:34 声称的「iter19 已证」落在 Script 层 At_Union 测试而非代数层 | Objects.cs:216-237（Join）；tests/EffectScriptEdgeTests.cs:102 |
| V3 | ℕ*/⊤ 运算律闭包、溢出⇒保守⊤（MA-002 / R4-F1） | PDR §3.1.5a | **在位且实测通过**。探针：create size=ulong.MaxValue ⇒ net=[⊤,⊤]、IsConserved=false（fail-closed 正确）；±long.MaxValue 精确抵消 [0,0] 守恒；NatStar 加/乘环绕检测正确 | Numeric.cs:31-52；Algebra.cs:73-90；探针 P6/P6b |
| V4 | net(S,scope) 分组 + DO-9 fail-closed（缺席资源/⊤ ⇒ 不守恒） | PDR §3.3.1 / iter44 | **部分修**。有限域行为正确；但「fail-closed」只对未知值成立，对 **null 引用**整条链路是 crash-closed 而非 spec-closed——见 F2/F5 | Algebra.cs:47,99-108；探针 P2' |
| V5 | Deviation ε=1 分母下界、⊤ 整体跳过、「永不 NaN」（ST-03 / iter33） | PDR §9.1/§3.1.5c | **部分修**。TryMid 先除后加防溢出已落实（SignedNet.cs:100-110）；但公共工厂 `DeviationVal.Of` 不拒 NaN，NaN 经 `ExceedsThreshold` 静默判「不报警」，后门直通承诺的反面——见 F3 | Deviation.cs:44-66；Numeric.cs:125；探针 P3 |
| V6 | ScopeId ⊆* 为偏序、Global 为最大元 | PDR §3.1.3b:150-158 | **规格自相矛盾，代码静默择一**。PDR 表同时声称「Global ⊑_any X」与「X ⊑_any Global」（154-155 行）——两者并存则所有 scope 塌缩为等价类，「偏序」即死。代码只实现 X⊑Global 单向（Objects.cs:97-104），探针确认 Global.IncludedIn(Method)=false。代码选了对的那半，但没有任何一处声明「PDR 表格是错的」 | PDR:154-155；Objects.cs:97-104；探针 P8 |

## 新发现

### F1 · HIGH — Parallel 的 PARA_CONFLICT 守卫只看 occupy 桶：write/read 桶冲突静默吞掉
- **位置**：src/Cosmos.EffectAlgebra/DerivedMetrics.cs:58-62
- **证据**：双重循环 `foreach (var ca in a.OccupyClaims) foreach (var cb in b.OccupyClaims)`。PDR §3.2.2 要求 `∀c₁∈S₁, c₂∈S₂`（不限 kind）。探针 P1：两条 `Kind.Write, Mode.Create` 同资源的 Claim 分属两分支，`Parallel` 不抛、静默 Union；同输入换 `Kind.Occupy` 则抛 PARA_CONFLICT（对照成立）。
- **判词**：守卫只看得见自己桶里的水——你把量纲隔离做成了桶，然后把安全检查也锁进了同一个桶。
- **最小修复**：守卫遍历改用 `AllClaims()`（或至少 read/write/occupy 三桶两两交叉），一行改动即可让 §3.2.2 名副其实。

### F2 · HIGH — 「构造即合法」是假承诺：null 可表达，崩溃点距污染点两层 API
- **位置**：src/Cosmos.EffectAlgebra/Objects.cs:120-126（注释）、:126（record 定义）；崩溃点 Algebra.cs:60
- **证据**：Objects.cs:122 写着「五参位置记录 ⇒ 构造时全必填……构造即合法，不靠运行时 if 漏判」。但 `Claim` 是 record struct，引用字段 Resource/Scope 接受 null。探针 P2'：`new Claim(Kind.Occupy, null, Mode.Create, null, null)` 无任何拒绝地进入 Signature，随后 `NetTable.Compute` 在 `c.Scope.IncludedIn(scope)` 处 NRE（Algebra.cs:60），Deviation.Calculate 同样 NRE。没有 ArgumentNullException 指认参数，没有规格级错误。
- **判词**：「类型能约束的用类型」是你们的铁律，可这里类型什么都没约束，注释替类型撒了谎。
- **最小修复**：Claim 主构造函数体里对 Resource/Scope 做 null 检查抛 ArgumentException（把崩溃点钉回污染点）；或提供 `Signature.Of` 的 `RequireNotNull` 归一入口。

### F3 · MED — `DeviationVal.Of(NaN)` 后门：「永不 NaN」承诺被公共工厂绕过，NaN 静默不报警
- **位置**：src/Cosmos.EffectAlgebra/Numeric.cs:125
- **证据**：`Of(double v) => new(false, v)` 无前置条件。探针 P3：`Of(NaN).ExceedsThreshold(0.2)==false` —— NaN 被当成「合格」放行，恰好是最危险的失败方向；`Of(+∞)` 则恒超阈值。§3.1.5c 明文「不崩溃、永不 NaN」。
- **判词**：规格说这个类型里不存在 NaN，工厂却给 NaN 发了合法签证——非法状态不仅可表示，还自带隐身斗篷。
- **最小修复**：`Of` 中 `double.IsFinite(v) ? new(false,v) : Top`——把 NaN/∞ 归入「需人工界定」，语义自洽且零成本。

### F4 · MED — `ZStar.Min` 与自身注释、PDR ⊤ 律三向矛盾；两个 Merge 同名不同义
- **位置**：src/Cosmos.EffectAlgebra/SignedNet.cs:58-59
- **证据**：第 58 行注释写「min(x,⊤)=x；min(⊤,x)=x」（照抄 PDR §3.1.5a 与 NatStar.Min），第 59 行实现 `(IsTop||o.IsTop) ? Top : ...` 即 min(⊤,x)=⊤。探针 P4：`SignedInterval.Merge([⊤,-1],[-5,-5]) = [⊤,-1]`，按 PDR 应为 [-5,-1]。另注：ℕ* 区间 Merge 会收窄下界（[⊤,⊤]⊔[1,5]=[1,⊤]，IntervalArithmeticTests.cs:88-93 还为此写了辩护注释），ℤ* 区间 Merge 不收窄——同名运算、两套 ⊤ 语义，用户必须读实现才能预测行为。
- **判词**：同一个「⊤」在两个文件里过着两种数学人生，而注释还在给旧行为守灵。
- **最小修复**：二选一并统一：(a) ZStar.Min 改为返回另一端有限值（与 NatStar.Min/PDR 对齐）；(b) 承认 ℤ* 下界未知应向 −∞ 开放并引入独立的 Bottom 表示。无论选哪个，先改注释。

### F5 · MED — Runtime 校验闸门自己先死于 null Provides：ANE('key') 取代了 LoadValidationException
- **位置**：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:16（Coeffect 无 null 约束）、LoadValidation.cs:96-103、NetBenefitClosure.cs:20
- **证据**：探针 P9：`new Coeffect(null, null, Method("m"))` 的 Fiber 调 `ValidateForLoad` 崩溃于 `ArgumentNullException: key`（null 资源作字典键），而非任何一条声明式校验错误。校验层的存在意义是把违规变成可读的诊断，它却先被违规输入杀死了。
- **判词**：门禁系统自己被第一个访客撞倒——这不是闸门，是绊线。
- **最小修复**：Fiber 构造函数校验 Coeffect 两字段非空（一处修复覆盖全部下游校验）。

### F6 · MED — Peak 把 read/write/occupy 三桶 size 相加成一个标量：DO-7 量纲隔离在派生度量层公开失效
- **位置**：src/Cosmos.EffectAlgebra/Algebra.cs:115-127（`foreach (var c in sig.AllClaims())`，仅过滤 Release）
- **证据**：README:56 区、PDR §3.1.4b 反复宣称「read/write/occupy 不可混算」。探针 P5：仅含一条 `Kind.Read, Exact(999999)` 的签名，Peak=999999——读带宽尺寸计入并发占用峰值，内存 MB 与节点个数相加无任何 KIND_MIX 报错。注意 PDR §3.3.2 公式本身也只写 `c.mode≠release` 未按 kind 过滤——这是规格与实现一起含糊，不是单纯实现走样。
- **判词**：你们花了一整个分桶结构去隔离量纲，然后派生度量一步就把苹果、橘子和显存加回了同一个数里。
- **最小修复**：Peak.Compute 加 `if (c.Kind != Kind.Occupy) continue;` 并同步修订 PDR §3.3.2 公式；若确需跨桶预算，走 Weight 显式扩展路径。

### F7 · LOW — 同一 Unknown→Use 行为在四个地方有三种称呼：fail-open / fail-closed / permissive
- **位置**：README.md:56（「fail-open」）；PDR §3.2.3 P4（「fail-closed 为保守兼容」）；Algebra.cs:13（「fail-open/permissive」+「保守」连用）；AlgebraLawsTests.cs:206（「fail-closed」）
- **判词**：行为本身锁死了没错，但文档让读者猜谜——程序员不该需要读三个文件才能知道 Unknown 会不会放行。
- **最小修复**：定一个词（建议「permissive/fail-open」），其余全改。

### F8 · LOW — InverseReplay 裸 `catch {}` 吞掉一切异常，诊断记录只有位置没有原因
- **位置**：src/Cosmos.EffectAlgebra.Runtime/InverseReplay.cs:34-41
- **证据**：catch 子句不捕获异常对象也不记录，`PartialReleaseDiagnosis` 只有 FailedIndex/Pending——OOM 与普通业务异常同等无声。继续回放剩余逆是合理策略，丢失原因是不是。
- **最小修复**：诊断记录加一个 `Exception? Cause` 字段（或至少捕获 `Exception e` 存日志），一行成本。

### F9 · LOW — `Weight.Of` 抛裸 InvalidOperationException，错误通道非类型化且消息指向不存在的调用方义务
- **位置**：src/Cosmos.EffectAlgebra/Algebra.cs:37
- **证据**：消息写「需 L3 报错」，但该 API 是公共 L1 函数，L1 调用者拿到的是一个无结构、无法 catch 分型的字符串异常。KIND_MIX 作为 PDR 明文的错误类别，值得有自己的异常类型或结果类型。
- **最小修复**：定义 `KindMixException : Exception` 或返回 `OneOf<double, KindMix>`。

### 正面核实（避免只报忧）
- Signature.Join 半格四律（幂等/交换/结合/吸收）：本轮 2000 组随机独立复核**全部通过**（探针 P10）——V2 的实现是真的。
- ulong/long 全域溢出⇒⊤ 保守策略实测正确（探针 P6/P6b/P7），R4-F1 的修复是真实的，±long.MaxValue 精确抵消无符号翻转。
- LoopCount.Of(0) 直接拒绝（DerivedMetrics.cs:33-35）：这是「显式前置条件让非法输入不可表达」的正确姿势，值得作为 F2/F3 修复的范本。

## TOP-3

1. **F1** Parallel 守卫只查 occupy 桶——并发冲突检测的规格覆盖率名义 100%、实际 1/3，且静默失败方向朝漏报。
2. **F2** null 可表达性击穿「构造即合法」承诺——污染点与崩溃点相距两层 API，无任何指认信息。
3. **F6** Peak 跨桶求和——DO-7 量纲隔离这一核心卖点在自己的派生度量里失效，且 PDR 公式同谋。

## usability 裁决

内核（NatStar/Interval/ZStar 算术、net 聚合、溢出⇒⊤）确实做到了「类型即边界」，性质测试不是摆设，我独立复核未找到算术反例——这部分配得上规格化的名声。但外壳的纪律是选择性的：Parallel 的前置条件做了 1/3、null 从每个引用字段长驱直入、Peak 当众混算三种量纲、Merge 一名两义、Unknown 一事三名。结论：**当前 API 尚不能让用户不读实现就预测行为**——尤其 SignedInterval.Merge 的 ⊤ 语义与 Peak 的桶混算，属于「读了实现也会猜错」的一类。修复成本低（TOP-3 合计约十行），缺的是把「规格优先」从内核纪律推广到外壳的决心。

## 证据清单（本轮实际读取的文件）

- README.md、EFFECT_SCRIPT.md、PDR_Effect_Cost_Algebra_v3_FINAL.md（§3.1–§3.4）
- src/Cosmos.EffectAlgebra/：Algebra.cs、SignedNet.cs、Numeric.cs、Deviation.cs、DerivedMetrics.cs、Objects.cs
- src/Cosmos.EffectAlgebra.Runtime/：LoadValidation.cs、NetBenefitClosure.cs、InverseReplay.cs、Fiber.cs
- tests/Cosmos.EffectAlgebra.Tests/：PropertyTests.cs、AlgebraLawsTests.cs、IntervalArithmeticTests.cs（另 grep 定位 EffectScriptEdgeTests.cs:102、AlgebraLawsTests.cs:206）
- 仓库外探针工程 /tmp/hickey-r04-probe（11 组探针，输出见 finding 各条）
