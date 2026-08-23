# Cosmos.EffectAlgebra — API 简单性对抗审计（Rich Hickey 透镜）

**审计员立场**：独立、只读 7 个源文件（EffectScript.cs / EffectScriptContract.cs / Objects.cs /
Numeric.cs / Algebra.cs / SignedNet.cs / DerivedMetrics.cs），未读 `audit/` 历史。
**立场声明**：宁误报「偶然复杂」。本文在「本质复杂度 vs 偶然复杂度」「complecting（纠缠）」
「值语义 vs 隐式状态」「魔法字符串/位置参数」「用户写错频率」五个透镜下逐符号评判。

**总判断（先给结论）**：代数内核（NatStar / Interval / ZStar / SignedInterval / Compatible /
NetTable / Peak / LoopCount / Combination.Loop）是**本质且简单**的——它真实地映射了「视觉资源在
时间轴上的占用/守恒/峰值」这一问题的不可约结构。但**外围 API 表面显著 complect 了**：

1. 同一道运算被起了 **3~4 个名字**（`Union`≡`Join`≡`Sequence`≡`Parallel`），承诺了代数并不存在的区分；
2. 用 **`double.NaN` 当 ⊥ 编码**（Weight）、用**字符串当枚举**（Violation.Kind / "⊤" / 资源键）；
3. **资源标识符**被膨胀成 15 支 union，其中 10 支在 JSON 契约里根本不可达，且同一资源有 3 个名字
   （SignalBus / Self("signal_*") / Signal("signal_*")，Gpu / CommandBuffer("gpu")）；
4. **scope 在 Event 与 Claim 两个层级重复携带**，必须手动保持一致（已有 OPEN-1 类 bug 历史证明）；
5. 一个 200 行方法 `Audit` 把**三道正交 gate（守恒/峰值/兼容）+ 扫换线 + 闭包**纠缠在一起。

这些都不是问题域本质，是「为了易写/易接而堆出来的偶合」。会让 AI/人类写剧本时频繁写错、反复审计回修。

---

## 逐符号判定表

| 符号 | 本质 / 偶然 | 判定 | 证据（file:line） | 严重度 |
|---|---|---|---|---|
| `NatStar` (ℕ∪{⊤}) | 本质 | 简单、值语义清晰 | Numeric.cs:10-74 | LOW |
| `Interval` [lo,hi] | 本质 | 简单；lo=⊤ 校验合理 | Numeric.cs:44-104 | LOW |
| `ZStar` (ℤ∪{⊤}) | 本质 | 有符号净占用必须负，合理 | SignedNet.cs:24-55 | LOW |
| `SignedInterval` | 本质 | `ContainsZero` 即守恒判定，类型安全 | SignedNet.cs:62-95 | LOW |
| `DeviationVal` | 本质（弱） | 仅 §9.1 引用，本批文件未使用 | Numeric.cs:113 | LOW |
| `ResourceId`（15 支 union） | **偶然** | 膨胀：15 支中仅 5 支 JSON 可达 | Objects.cs:20-39；契约仅 5 支 EffectScriptContract.cs:141-152 | **HIGH** |
| `ResourceId.Normalize`（SignalBus/Self/Signal 三义） | **偶然** | 同一资源 3 名字（signal_*），Gpu/CommandBuffer("gpu") 双名 | Objects.cs:51-72；EffectScriptContract.cs:146-150 | **HIGH** |
| `ScopeId`（8 支 union） | **偶然** | 8 支中仅 4 支契约可达（Shell/Loop/Conditional/Async 死支） | Objects.cs:89-98；契约仅 Scene/Method/Type/Global EffectScriptContract.cs:89-98 | MED |
| `ScopeId.IncludedIn` 偏序 | 本质 | ⊆* 偏序是本质 | Objects.cs:101-108 | LOW |
| `Claim`（5 位置记录 + scope 重复） | **本质+偶然** | 5 元组本质；但 **Claim.Scope 与 Event.Scope 冗余必须一致**（OPEN-1 级） | Objects.cs:126；强制一致注释 EffectScript.cs:162 | **HIGH** |
| `Kind` 枚举 | 本质 | 穷举、类型安全 | Objects.cs:111 | LOW |
| `Mode` 枚举（含 Unknown） | 本质（弱） | Unknown 静默==Use（隐藏强制），易误判 | Objects.cs:116；Algebra.cs:15 | MED |
| `Signature`（三桶隔离） | 本质 | 量纲隔离合理 | Objects.cs:145-195 | LOW |
| `Signature.Union` | 本质 | 半格并，正确 | Objects.cs:182-190 | LOW |
| `Signature.Join` | **偶然** | **Join≡Union 别名**（Objects.cs:192 直接调 Union），无语义区分 | Objects.cs:192 | **MED** |
| `Compatible.IsCompatible` | 本质 | 全函数 16 对对称，合理 | Algebra.cs:12-33 | LOW |
| `Weight.Of`（返回 NaN 表 ⊥） | **偶然** | **用 double.NaN 当 ⊥ 编码**（非类型安全，NaN 是数！） | Algebra.cs:35-38 | **HIGH** |
| `NetTable` / `Peak` | 本质 | 净占用/峰值，正确 | Algebra.cs:46-109,111-127 | LOW |
| `Combination.Loop`（×ω） | 本质 | ω 缩放，正确 | DerivedMetrics.cs:37-48 | LOW |
| `Combination.Sequence` | **偶然** | **≡Union**（无顺序语义差异） | DerivedMetrics.cs:50 | **MED** |
| `Combination.Parallel` | **偶然** | **≡Union**（`Sequence` 同物异名，承诺并行/串行区分但代数无区分） | DerivedMetrics.cs:53 | **HIGH** |
| `LoopCount` | 本质 | ω∈ℕ∪{⊤} 合理 | DerivedMetrics.cs:10-33 | LOW |
| `Derived`（便利封装） | **偶然（弱）** | 在 Algebra 之外又开一套入口，重复 API 表面 | DerivedMetrics.cs:68-78 | MED |
| `EffectEvent`（4 位置记录） | 本质+**偶然** | 4 元组本质；但 **位置参数 + ω 需手动包 LoopCount** 易错 | EffectScript.cs:22-48 | MED |
| `EffectScript` | 本质 | 事件集，纯数据 | EffectScript.cs:50-65 | LOW |
| `At(t)` | 本质 | 瞬时快照，正确 | EffectScript.cs:73-85 | LOW |
| `Audit(cap)` | **偶然（纠缠）** | **单方法 200 行，三道 gate + 扫换线 + 闭包 + 4 个字典 + 2 个闭包函数**；一道方法做了太多事 | EffectScript.cs:97-301 | **HIGH** |
| `Budget` | 本质 | 软约束壳，合理 | EffectScript.cs:303-312 | LOW |
| `AuditResult` / `Violation` | 本质（弱） | Violation.**Kind 是字符串**枚举（"Leak"/...），非类型安全 | EffectScript.cs:316-355 | MED |
| `EffectScriptContract.Parse/Serialize` | 本质+**偶然** | 契约本质；但魔法字符串 "⊤" + 资源键 + scope 类型字符串，且 **Serialize 兜底 `"memory:0"` 静默改资源** | EffectScriptContract.cs:104,115,141-152,229 | **HIGH** |
| `ParseScope` 兜底 `_=>Scene` | **偶然** | 未知 type 静默归为 Scene，隐藏行为 | EffectScriptContract.cs:97 | MED |

---

## 三大偶然复杂度（应砍），附证据

### 砍 #1：同一运算起了 3~4 个名字，承诺代数不存在的区分
`Signature.Union`(Objects.cs:182) 与 `Signature.Join`(Objects.cs:192) 完全等价；
`Combination.Sequence`(DerivedMetrics.cs:50) 与 `Combination.Parallel`(DerivedMetrics.cs:53) 也各自 ≡ `Signature.Union`。
于是 API 表面给出「并 / 连接 / 序列 / 并行」四个名字，但数学上只有 **并** 一个操作（序列与并行在半格并下无区别）。
**Hickey 视角**：这是经典的「为易接而造词」——名字暗示语义区分（串行有时序、并行有并发），代数并不支持。
调用方（尤其 AI）会被诱导去「选对」Sequence vs Parallel，而选哪个都一样；既增加认知负担，又埋下「将来若想真的区分时无处下手」的债。
**建议**：只保留 `Union`（或 `Compose`）；若真要区分串/并，应在类型或签名上体现时序语义，而非当前空壳别名。

### 砍 #2：`Weight.Of` 用 `double.NaN` 当 ⊥（⊥ 是类型，不是浮点哨兵）
Algebra.cs:38 `return a == b ? 1.0 : double.NaN;` 把「跨 kind 未定义（⊥）」编码进一个 `double`。
`NaN` 是 IEEE 数，会参与任何比较都返回 false、会与 `double` 一起流入求和/序列化而不报错——它**不是**类型安全的不动点。
注释自己承认「约定为 ⊥ 编码」。这正是 Hickey 痛批的「用值域哨兵代替类型」：⊥ 应当是一个判别联合 / `double?` / `Option<double>`，
让编译器强制「必须先判是否定义」。**风险**：跨 kind 聚合若漏判 `IsNaN`，会静默得到一个 NaN 权重而非编译期报错。

### 砍 #3：ResourceId 膨胀 + scope 跨层重复（双倍的「同一事实两处写」）
- `ResourceId` 有 **15 支** 构造子（Objects.cs:23-39），但 JSON 契约只产生 5 支
  （gpu/commandBuffer/memory/occupancy/signalBus，EffectScriptContract.cs:141-152）——其余 10 支对「视觉效应脚本」是死代码表面。
- 同一资源有**多个名字**：`SignalBus` vs `Self("signal_*")` vs `Signal("signal_*")`（Objects.cs:51-72），
  `Gpu` vs `CommandBuffer("gpu")`（EffectScriptContract.cs:146-150）。归一化要靠 `Normalize` 事后兜底，
  把「一个资源」拆成「多个构造子 + 一张映射表」，正是 complecting 的反面教材。
- **scope 在两层重复**：`EffectEvent.Scope`（EffectScript.cs:27）与 `Claim.Scope`（Objects.cs:126）必须一致，
  Audit 内部注释明确「此前用 claim 自带 c.Scope 与 At 视角 scope 分裂，导致同一剧本两视角冲突归因错位」
  （EffectScript.cs:162，即 OPEN-1 类 bug）。这是「一个事实两处写、必须手动同步」的典型偶然复杂度——
  Claim.Scope 本可从所属 Event 派生，却要 AI 在每个 claim 里再写一遍且不能写错。

**次重要但仍应砍**：
- `Violation.Kind` 是字符串（EffectScript.cs:329-355），应改为判别联合/枚举，否则 AI 回修时要匹配魔法字符串。
- `EffectScriptContract` 序列化兜底 `"memory:0"`（EffectScriptContract.cs:211,229）：任何未识别资源在 round-trip 时
  **静默变成 memory(0)**——这是数据完整性隐患（写错类型不报错，悄悄丢信息）。
- `ParseScope` 未知 type 静默归 Scene（EffectScriptContract.cs:97）：fail-soft 隐藏错误。
- 位置记录 `EffectEvent`(4 参)/`Claim`(5 参)（EffectScript.cs:38-48，Objects.cs:126）易错，应优先命名参数/构造器。

---

## 值语义 / 状态评估（总体良好）
内核普遍是 `readonly record struct`（NatStar/Interval/ZStar/SignedInterval/Claim/EffectEvent/Budget/AuditResult/Violation），
`Signature` 内部可变但对外暴露 `ImmutableHashSet`，`Audit` 的字典均为局部变量——**无隐藏全局状态**，这点符合 Hickey 的「用不可变值避免隐藏状态」原则，应保留。
问题集中在**命名、表面膨胀、契约哨兵**，而非值模型本身。

## 用户难度结论（是否造成写错/回修）
**会，且概率不低**。具体高频踩坑点：
1. scope 必须在 event 与每个 claim 里重复且一致（已用 OPEN-1 级 bug 证明易错）；
2. 资源名要在 {gpu, commandBuffer, memory, occupancy, signalBus} 里精确选，且 signalBus 还有 Self/Signal 别名陷阱；
3. "⊤" 必须精确写成该 Unicode 字符（契约硬编码），写错即 FormatException；
4. `Sequence` vs `Parallel` 名异实同，诱导错误心智模型；
5. 位置参数 4~5 个，缺参/错序编译器不拦（record 构造全必填但**顺序**无提示）。

**Top 3 应砍的偶然复杂度**（按 ROI）：
1. 砍 #1 —— 合并 `Union/Join/Sequence/Parallel` 为一个 `Union`；
2. 砍 #2 —— `Weight.Of` 的 `double.NaN` 改为类型化 ⊥（`double?` 或判别联合）；
3. 砍 #3 —— 收窄 `ResourceId` 到契约可达的 5 支、消除资源别名、让 `Claim.Scope` 从所属 `EffectEvent` 派生而非重复携带。
