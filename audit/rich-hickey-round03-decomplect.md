# Rich Hickey 视角对抗性审计 · Round 03 — Decomplect 正交性透镜

- 审计范围：仅 7 个 L1 源文件（未读 `audit/`）
  - `src/Cosmos.EffectAlgebra/Objects.cs`
  - `src/Cosmos.EffectAlgebra/Algebra.cs`
  - `src/Cosmos.EffectAlgebra/Numeric.cs`
  - `src/Cosmos.EffectAlgebra/SignedNet.cs`
  - `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`
  - `src/Cosmos.EffectAlgebra/EffectScript.cs`
  - `src/Cosmos.EffectAlgebra/Deviation.cs`
- 透镜：Decomplect —— 把不纠缠的东西分开；一个事实只写一处；组合空间不得虚报（free product ≠ semantic domain）。

---

## 符号表（逐符号）

| # | 符号 | 文件:行 | 判定 |
|---|------|---------|------|
| S1 | `ResourceId`（15 构造子） | Objects.cs:19-36 | ✅ 判别联合 + 结构相等 + 单点归一 `Normalize`（Objects.cs:60-79）。构造子轴本身正交、干净。 |
| S2 | `ResourceId.Normalize` | Objects.cs:61-79 | ✅ 幂等性有注释护栏（SignalBus 不二次剥前缀）。但它是**约定**而非类型保证：每个消费点必须记得调用（见 N3）。 |
| S3 | `ScopeId`（8 构造子）+ `IncludedIn` | Objects.cs:92-113 | ⚠️ 偏序退化为「相等 ∨ 对方是 Global」两行查表（Objects.cs:105-110）。Loop/Conditional/Async 与 Method/Type/Scene 跨标签一律不可比——8 支名义上存在，实际序结构只有 2 条规则。命名空间虚报了结构。 |
| S4 | `Kind` {Read,Write,Occupy} | Objects.cs:116 | ✅ 三桶存储隔离正确（Signature.Add 按 Kind 分桶，Objects.cs:163-176）。 |
| S5 | `Mode` {Use,Create,Release,Move,Unknown} | Objects.cs:119 | ⚠️ Mode × Kind 组合语义耦合（见 F2）。`Unknown` 在三处消费点有三种不同解释（见 F5）。 |
| S6 | `Claim` 五元组 | Objects.cs:127-152 | ⚠️ 自由积 15×8×3×5=1800 组合全部可构造；`Claim(Kind.Read, Mode.Create)` 等无意义组合无护栏——类型层虚报了语义域。 |
| S7 | `Claim.CompatibleWith` | Objects.cs:143 | ✅ 纯 mode-pair 函数，与 resource/scope 解耦——这是正确的 decomplect（兼容性本来只是 mode 的事实）。但它与 EffectScript gate(3) 的重编码不一致（F3）。 |
| S8 | `Compatible.IsCompatible` / `Resolve` | Algebra.cs:10-28 | ⚠️ CONFLICT 集的唯一权威定义在此，但被 gate(3) 二次编码（F3）。`Unknown⇒Use` 的解析策略**私有于此**，其他消费点不共享（F5）。 |
| S9 | `Weight.Of` → NaN 编码 ⊥ | Algebra.cs:33-39 | ❌ **死代码 + 违反项目自身不变量**（F4，本轮核心发现）。全仓零调用点（已 grep 验证）。 |
| S10 | `NetTable.Compute` | Algebra.cs:54-70 | ✅ 只取 Occupy 桶 + `⊆*` 过滤 + 有符号求和，量纲隔离在 net 处正确执行。但 Negate/ToSigned 符号约定与他处重复（F6）。 |
| S11 | `NetTable.IsConserved` | Algebra.cs:96-107 | ✅ fail-closed 三分支（缺键/含⊤/含0）自洽。 |
| S12 | `Peak.Compute` | Algebra.cs:113-127 | ❌ **跨桶量纲隔离被破坏**（F1，最高严重度）：遍历 `AllClaims()` 把 read/write/occupy 三桶 size 直接相加成一个标量。 |
| S13 | `Derived.Peak/Net/IsConserved` | DerivedMetrics.cs:69-78 | ⚠️ 只是转发，但把 S12 的跨桶缺陷暴露为公共 API 面。 |
| S14 | `Combination.Loop` / `Scale` | DerivedMetrics.cs:37-62 | ⚠️ scale-by-ω 逻辑第一份拷贝（F6）。 |
| S15 | `EffectScript.At` | EffectScript.cs:88-101 | ✅ 经 `Combination.Loop(e.Footprint, e.Loop, e.Scope)` 把全部 claim 重投影到 e.Scope——At 视角 scope 来源单一。 |
| S16 | `EffectScript.Audit` gate(1)(2)(3) | EffectScript.cs:104-292 | ⚠️ 三道 gate 共享扫换线状态但各自独立判定，大体正交；gate(3) 已修 TC7 用 e.Scope（EffectScript.cs:151-153），但 gate(1)(2) 完全丢弃 scope 维度（F7）。violation 归因伪造 Global（F7）。 |
| S17 | gate(3) 冲突判定 | EffectScript.cs:234-243 | ❌ CONFLICT 集二次编码为 `{Create,Move,Release}` 同 mode 计数 ≥2——与 `Compatible.IsCompatible` 是同一事实两处写（F3）。 |
| S18 | `ScaleSize` / `ToZ` / `Negate`(EffectScript) | EffectScript.cs:299-306 | ⚠️ scale-by-ω 第二份拷贝、release 取负第三份编码（F6）；Step 内联算术还有第四份（EffectScript.cs:184-191）。 |
| S19 | closure 块 signed-contrib 构造 | EffectScript.cs:276-284 vs 158-165 | ⚠️ 同一构造式在 Audit 内部原样出现两次；且 :272/:274 存在逐字重复的死条件行——复制粘贴腐烂的直接物证（F6/N4）。 |
| S20 | `NatStar/ZStar/Interval/SignedInterval/DeviationVal` | Numeric.cs / SignedNet.cs | ✅ 全部「类型字段即边界」、⊤ 显式闭环、无魔法数。这是本仓最干净的正交设计。 |
| S21 | `Deviation.Calculate` | Deviation.cs:22-53 | ✅ 占用桶对账、⊤ 传播、ε=1 分母——量纲隔离遵守正确（正面对照组）。 |

---

## 发现（按严重度）

### F1 · BLOCKER — `Peak.Compute` 跨桶聚合，§3.1.4b 量纲隔离在峰值处失效
- 位置：`src/Cosmos.EffectAlgebra/Algebra.cs:113-127`
- 证据：`foreach (var c in sig.AllClaims())`（:117）后仅排除 `Mode.Release`（:120），随后 `sum += (c.Size ?? Interval.Default).Hi`（:125）。Read/Write/Occupy 三桶的 size 被加进同一个 `NatStar`。
- Hickey 式陈述：read/write 的「次数」和 occupy 的「并发占用」是不同量纲；`Peak` 把它们揉成一个数，正是 §3.1.4b（Objects.cs:142 注释自称「跨桶聚合须显式 Weight 否则 KIND_MIX」）所禁止的事，而它就发生在 L1 权威实现里。
- 后果：`Derived.Peak(s, scope)`（DerivedMetrics.cs:71）作为公共入口返回的「峰值」对含 read/write claim 的签名是无意义数字。对照：`EffectScript.Audit` gate(2) 只遍历 `OccupyClaims`（EffectScript.cs:172），同一概念「峰值」两个实现语义不同——同名不同义，complect 的典型症状。
- 修复方向：`Peak.Compute` 过滤 `c.Kind == Kind.Occupy`；或将 per-resource 峰值逻辑收敛到唯一实现，L1 版委托之。

### F2 · MAJOR — Kind × Mode 组合域虚报（自由积 ≠ 语义域）
- 位置：Objects.cs:116-152（Kind/Mode/Claim 定义）
- 证据：`Mode.Create/Move/Release` 只对 Occupy 桶有意义（net 符号约定、peak 排除规则都以此为前提），但类型层允许 `Claim(Kind.Read, Mode.Create, …)` 等 ~12×8 个退化组合进入签名并参与一切运算。1800 个可构造组合中语义有效的远少于彼。
- Hickey 式陈述：「能构造」被当成了「有意义」。要么收窄构造面（如 Occupy-only mode 子类型），要么在文档/Analyzer 明确退化组合的处理契约——目前两者皆无。

### F3 · MAJOR — CONFLICT 集「一个事实两处写」
- 位置 A（权威）：`src/Cosmos.EffectAlgebra/Algebra.cs:18-27` —— `IsCompatible` 的模式配对表，CONFLICT = {(C,C),(M,M),(R,R)}。
- 位置 B（重编码）：`src/Cosmos.EffectAlgebra/EffectScript.cs:234-243` —— gate(3) 以 `(mode==Create||mode==Move||mode==Release) && count>=2` 重写同一知识。
- 后果：若 PDR §2.3 扩展 CONFLICT 集（如加入 Unknown 配对）或调整 `Resolve(Unknown→Use)`，gate(3) 会静默漂移——扫换线审计与 `Compatible.IsCompatible` 给出矛盾结论，且编译期无任何信号。
- 当前等价性核对：今日两处恰好等价（同-mode 对 ⇔ CONFLICT；Unknown 组因不在三元组内而不报，与 Resolve→Use 不冲突一致）。等价是巧合的当前快照，不是设计保证。
- 修复方向：gate(3) 应调用 `Compatible.IsCompatible`（按组内 mode 两两判），或至少让 CONFLICT 集成为单一静态数据源被两侧引用。

### F4 · MAJOR — `Weight.NaN`：⊥ 编码是死代码，且违反项目自身「永不 NaN」不变量
- 位置：`src/Cosmos.EffectAlgebra/Algebra.cs:33-39`；声明于 Objects.cs:142。
- 证据：
  1. 全 src 目录 `Weight\.` 零调用点（grep 验证）。跨桶隔离的运行时守卫根本不存在——于是 F1 得以发生。KIND_MIX 仅以另一种口径存在于 L3 Analyzer（EffectAlgebraAnalyzer.cs:238-246，按调用点资源分组启发式），与 `Weight` 无接线。
  2. `double.NaN` 作 ⊥ 哨兵直接违反 Numeric.cs:10 的成文承诺（「永不崩溃、永不 NaN/发散」，MA-002）与 ST-03 的修复先例（Deviation.cs:9 注释明言「不 NaN 不 ∞」）。NaN 的病态恰是最危险的：`NaN > x` 恒 false ⇒ 未来任何 `sum += Weight.Of(a,b)` 聚合都会**静默吞掉报警**而不是 fail-closed。
- 回答任务问题「跨桶量纲隔离是否被 Weight.NaN 破坏？」：准确说法是——隔离不是被 Weight.NaN *破坏*，而是 *从未被执行*：Weight 是装饰性的死代码，真正的破坏发生在 Peak.Compute（F1）。而 Weight.NaN 这个设计本身就是一颗哑弹：一旦被接线，其失败模式（比较恒 false、不等自身）恰是本项目已在 Deviation/NatStar 中花大力气消灭的那类静默错误。
- 修复方向：删除 `Weight` 或改为返回 `Option<double>`/专用 `WeightVal`（带 IsTop 字段，仿 `DeviationVal`），并在 Peak/net 聚合处真正消费 ⊥。

### F5 · NOTE(MEDIUM) — `Mode.Unknown` 一个值三种解释
- 位置：Algebra.cs:15（`Resolve: Unknown→Use`，兼容视角）；Algebra.cs:63 / EffectScript.cs:160,279（net 视角：Unknown 非 Release ⇒ 正向贡献）；EffectScript.cs:182,239（gate(3) 键：`(int)c.Mode` 原样保留 Unknown 为独立组）。
- 「Unknown 按 Use 处理」的策略写在 `Compatible.Resolve` 私有方法里，未被共享。三种解释今天各自局部自洽，但这正是策略该有一处定义的证据。

### F6 · MEDIUM — 同一数学事实多处手写（scale / sign / contrib）
- scale-by-ω：DerivedMetrics.cs:57-62（`Combination.Scale`）≡ EffectScript.cs:299-303（`ScaleSize`）≡ EffectScript.cs:184-191（Step enter 内联溢出算术）。三份。
- release 取负 + create 正向的 SignedInterval 构造：Algebra.cs:72-89（`Negate/ToSigned`）、EffectScript.cs:158-165（Step）、EffectScript.cs:276-284（closure 块）。三份。
- 物证：EffectScript.cs:272 与 :274 逐字重复的 `if (e.Lifetime.Lo.IsTop) continue;` 死行——复制粘贴已经留下痕迹。
- 修复方向：抽公共 helper（signed-contrib、scaleSize），closure 块复用 Step 的累加路径或提取 `AccumulateOccupy(dict, event)`。

### F7 · MEDIUM — At 与 Audit 的 scope 投影：gate(3) 一致，gate(1)(2) 投影丢失
- At 侧：`Combination.Loop(..., e.Scope)` 把所有 claim 重投影到 e.Scope（EffectScript.cs:95）——单一 scope 事实源。
- gate(3)：分组键用 `e.Scope`（EffectScript.cs:153，TC7 修复注释确认与 At 对齐）✅。
- gate(1)(2)：net/peak 字典键只有 `ResourceId`（EffectScript.cs:145-147），scope 维度被整体丢弃；违例归因硬编码 `new ScopeId.Global()`（EffectScript.cs:222-223、230-231、286-287）——这不是「Global 视角聚合」的诚实标注，而是把丢失的信息伪造回填。
- 后果：`At(t)` 之后走 `Derived.Net/Peak`（会按 `⊆*` 过滤 c.Scope，且 At 已把 scope 统一为 e.Scope）与 `Audit` 的全局聚合在事件异 scope 剧本上**数值可能一致但归因维度不同**；`Violation.Scope` 字段对 NegativeDip/PeakExceeded/Leak 恒为 Global，消费者无法区分「真全局」与「scope 未记录」。两条「t 时刻世界状态」的计算路径仅在 scope=Global 处重合。
- 修复方向：gate(1)(2) 至少把 e.Scope（或 claim scope 并集）写入 Violation.Scope；或在文档明确 Audit 即 Global 投影、At-scoped 查询须另行走 NetTable 路径。

### N1 · NOTE — `ScopeId.IncludedIn` 名义偏序实为两层
- Objects.cs:105-110：规则只有「相等 ∨ other is Global」。Loop("x")⊄Scene("s")、Shell 与 Scene 互不可比等均靠跨标签 false 兜底。8 支构造子的存在暗示了层次结构，实际没有——不算 bug，但属于「命名暗示了不存在的结构」。传递性测试（AlgebraLawsTests.cs:116）通过是因为两步链必经 Global，属平凡真。

### N2 · NOTE — `Peak` 名称一处两义
- Algebra.cs:114（L1：跨桶全签名标量和）vs EffectScript gate(2)（per-resource、occupy-only、含 ⊤ 计数）。同名不同度量纲，建议改名或统一。

### N3 · NOTE — Normalize 纪律靠人肉
- ResourceId 归一是单点真相（好），但每个消费点（Algebra.cs:61,98,100; EffectScript.cs:157,171,225,277…）都必须记得调用。一次遗漏即产生幽灵资源键。可考虑让 Signature 存储层保证已归一（Add 时已做），下游统一信任桶内不变量、删去重复调用——目前两种风格混用。

---

## 正交性总评

| 组合空间 | 结论 |
|----------|------|
| ResourceId(15) | ✅ 判别联合 + 归一单点，干净。 |
| ScopeId(8) | ⚠️ 名义 8 支、实际序结构 2 条规则（N1）。 |
| Kind(3) | ✅ 存储层隔离正确；❌ Peak.Compute 读取时击穿（F1）。 |
| Mode(5) | ⚠️ 与 Kind 语义耦合未建模（F2）；Unknown 三义（F5）。 |
| 四轴自由积 | ❌ 1800 组合可构造，语义域小得多，无护栏（F2）。 |
| 三道 gate 相互正交 | ⚠️ 判定逻辑独立（好），但共享事实（CONFLICT 集、scale、sign、release-exclusion）各写多份（F3/F6），漂移即失正交。 |
| At ↔ Audit scope 投影 | ⚠️ gate(3) 一致；gate(1)(2) 丢维 + 伪 Global 归因（F7）。 |
| 跨桶量纲隔离 vs Weight.NaN | ❌ Weight 是死代码哨兵且违反 no-NaN 不变量；隔离实际由「各消费点的自觉」维持，已被 Peak 击穿（F1/F4）。 |

最干净的部分：Numeric.cs / SignedNet.cs 的 ⊤-闭环载体与 Deviation.cs 的消费方式——「类型字段即边界」贯彻到位，可作为其余符号收敛的模板。

## Residual risks（残留风险）
- 本审计为纯静态只读，未运行测试复核 gate(3) 与 `IsCompatible` 的当前等价性（结论基于源码推演，等价性是当前快照性质，见 F3）。
- L3 Analyzer 的 KIND_MIX 启发式（EffectAlgebraAnalyzer.cs:189-246）不在本轮 7 文件范围内，其与 L1 缺口的互补程度未验证。
- F1 的实际影响面取决于现有调用方是否给含 read/write claim 的签名调过 `Derived.Peak`（未全仓排查 samples/tests）。
