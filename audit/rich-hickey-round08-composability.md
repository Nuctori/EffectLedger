# Round08 — Rich Hickey 视角组合性对抗审计（代数律透镜）

- 审计范围：`src/Cosmos.EffectAlgebra/` 下 7 个源文件（Objects.cs / DerivedMetrics.cs / EffectScript.cs / Algebra.cs / Numeric.cs / SignedNet.cs / EffectScriptContract.cs 契约层经 grep 交叉验证）。未读 `audit/`。
- 视角：组合性 = 「大程序的属性能否由小组件的属性推出」。逐符号检验：半格律、分配律、置换不变、gate 独立性、毒值（NaN）。
- 方法：只读源码 + grep 全 src 验证调用点，不做臆测。

---

## 0. 符号总表

| 符号 | 文件:行 | 代数声明 | 实际性质 | 判定 |
|---|---|---|---|---|
| `Signature.Union` | Objects.cs:182 | §3.2.1 半格并 | 按 Normalize 键的集合并：幂等✓ 交换✓ 结合✓ 单位元 Empty✓ | **成立** |
| `Signature.Join` | Objects.cs:192 | §3.2.4 join + "同 Claim 取 size merge_I" | `=> Union(a,b)`；size 是 Claim 身份一部分，**同键不同 size 不做 merge_I** | **注释与实现背离**（M1） |
| `Signature.Equals/GetHashCode` | Objects.cs:198-212 | 结构相等"值语义" | Equals 用 SetEquals ✓；GetHashCode 按桶内**迭代序**累加 `h*31^c` → 同一值不同构造序可产生不同哈希 | **违反 Eq/Hash 契约**（M2，潜伏） |
| `Combination.Sequence` | DerivedMetrics.cs:50 | (S₁;S₂) := S₁∪S₂ | 与 Union 全同；交换 ⇒ 时序/因果信息在 L1 被抹除 | 设计取舍（N1） |
| `Combination.Parallel` | DerivedMetrics.cs:53 | (S₁∥S₂) := S₁∪S₂ | 同上；跨调用点 Compatible 只能靠 L3 Analyzer 带外补 | **L1 内不安全可组合**（N1/M3） |
| `Combination.Loop` | DerivedMetrics.cs:37-48 | Σ_{i=1..ω} copy_i(S) | 对 ∪ 分配律成立✓；×ω 缩放结合（含 ⊤ 保守律）成立✓；但把 body 全部 claim 重 scope 到 loopScope ⇒ 嵌套 Loop 内层 scope 信息被压平丢失 | 律成立，信息丢失（N2） |
| `EffectScript.At(t)` | EffectScript.cs:81-91 | 存活事件 Loop 后 Union | 集合语义，与 Events 枚举序无关 ⇒ **置换不变成立** | 成立 |
| `EffectScript.Audit` 三 gate | EffectScript.cs:105-292 | gate(1)守恒/gate(2)峰值/gate(3)兼容 | 三 gate 各持独立运行态（net / peakSum+topCount / grp），互不读写对方状态 ⇒ 可独立增删 | **独立可组合成立**（一处耦合见 N3） |
| `Audit` 违例输出序 | EffectScript.cs:145, 218-247 | "§5 输出确定性" | 违例集合置换不变，但 foreach Dictionary 插入序 + `List.Sort` 不稳定 ⇒ Violation **列表顺序**随事件排列变化 | 低危（L1） |
| `Weight.Of` | Algebra.cs:32-38 | Kind×Kind→ℝ∪{⊥}，NaN 编码 ⊥ | NaN≠NaN 自反性破坏、double 运算静默传播；全 src **零调用点**（grep 验证）→ 潜伏 API 债 | 中危（M4） |
| `NatStar/ZStar/SignedInterval` | Numeric.cs / SignedNet.cs | ⊤ 闭环，永不 NaN | 加乘溢出保守 ⊤、min/max ⊤ 律、ContainsZero fail-closed — 一致且无 NaN | **成立**（正面样板） |

---

## 1. Signature.Union / Join 是否满足半格律？

**载体辨析（Hickey 第一步：你的值到底是什么？）**：Signature 实为「Normalize 后 Claim 的有限集」，不是「(key → interval) 的 map」。这决定了两条结论：

1. **Union 是真正的 join-semilattice 并**（Objects.cs:182-190）：Add 路径先 Normalize 再入 ImmutableHashSet，幂等/交换/结合均由集合论承担；Empty 是单位元；Equals 用 SetEquals 使 `Union(a,b) ≡ Union(b,a)` 在值上成立。**律本身无可击破。**

2. **Join 是假 join（M1，Medium）**。Objects.cs:190 注释宣称 "§3.2.4 ⊔：join-semilattice 合并…同 Claim 取 size merge_I"，但 Objects.cs:192 实现为裸 `=> Union(a,b)`。由于 `Claim.Size` 参与 record 结构相等，`Of(write,mem,create,S,Exact(2))` 与 `Of(write,mem,create,S,Exact(3))` 在 Join 下**保留两条**而非合并为 `Merge` 结果 `[2,3]`。grep 全 src 证实 `Interval.Merge`（Numeric.cs:101）与 `SignedInterval.Merge`（SignedNet.cs:79）**零调用点** —— 承诺的 value-lattice 逐点 join 根本不存在。后果：同一逻辑占用写两笔不同 size 区间，Peak.Compute（Algebra.cs:143-157）会**求和双计**而非取包络。这是典型的 doc-driven 而非 proof-driven 接口：名字许诺了律，实现交付了另一个 monoid。

3. **Eq/Hash 契约破裂（M2，Medium-High，潜伏）**。Objects.cs:204-212 的 GetHashCode 按 `_read/_write/_occupy` 桶内迭代顺序做 `h=(h*31)^c`。ImmutableHashSet 迭代序依赖构造历史，故 `Union(a,b)` 与 `Union(b,a)` Equals 为真但哈希可不同。当前 src 无任何 `Dictionary<_,Signature>`/`HashSet<Signature>` 用法（grep 验证），故是潜伏雷——一旦有人把 Signature 放进哈希容器，「相等的东西查不到」即违反最小惊讶原则。修法一行：对三桶各取与序无关的聚合（如 `Aggregate(0, (h,c)=>h ^ c.GetHashCode()*31)` 的纯 XOR 形式需先混入常量避免全撞），或直接 XOR 各桶元素哈希。

## 2. Combination.Loop / Sequence / Parallel 是否尊重组合？

- **分配律 ✓**：Loop 逐 claim 独立映射（DerivedMetrics.cs:39-47），故 `Loop(a∪b, ω, s) = Loop(a,ω,s) ∪ Loop(b,ω,s)` 成立；这是「局部推理全局」的关键律，守住了。
- **缩放结合 ✓ 含 ⊤**：`Scale(Scale(size,ω₁),ω₂)=size·(ω₁ω₂)`（有限）；ω=⊤ 走 `[lo,⊤]` 保守通道（DerivedMetrics.cs:58-62），与 NatStar 乘法 ⊤ 律一致，无发散路径。
- **Sequence ≡ Parallel ≡ Union（N1）**：DerivedMetrics.cs:50-53。交换并抹掉时序：`(create ; release)` 与 `release ; create` 在 L1 同一签名，靠 net 的符号求和兜底；`(create ; create)` 也无法在 L1 与并行区分——Compatible 冲突检测完全外包给 L3 Analyzer（注释自认）。Hickey 判语：**两个名字一个实现 = 说谎的 API**。若 Sequence/Parallel 就是 Union，就不要起两个名字暗示不同语义；要么引入非交换的时序载体，要么承认这是「效应集合」而非「程序组合」。
- **Loop 的 scope 压平（N2）**：所有 claim 被 `Scope = loopScope` 重写（DerivedMetrics.cs:41/43/45），嵌套循环 `Loop(Loop(S,ω₁,s₁),ω₂,s₂)` 中 s₁ 彻底丢失，Global-scoped 的 body claim 也被拽进 loop scope。数值上无害（缩放仍正确），但 scope 维度不可组合——嵌套结构的信息在单次映射中被销毁，违背「组合保信息」。
- 细节：`c.Size ?? Interval.Default`（41/43/45 行）是死防御——入桶时已 Normalize 补 Default；三段 foreach 复制粘贴可直接用 `AllClaims()`（Algebra.cs:166-176）。低危噪音。

## 3. At(t) 是否置换不变？

**成立**（EffectScript.cs:81-91）：acc 以 Union 累积，Union 交换/结合 + Equals 结构化 ⇒ 任意重排 Events 得到值相等的签名；Alive 过滤是逐事件谓词，与序无关。注释中"确定性"声明（auditA 焦点6）在**内容层面**属实。

**但 Audit 不是完全置换不变（L1，Low）**：
- Violation **集合**与 `Passed` 不变：gate(3) 按组计数（≥2 判冲突，EffectScript.cs:238-247）、gate(1)/(2) 数值求和有交换保守律（peakSum 的环绕检测等价于判总量是否超界，与加法次序无关）。
- Violation **列表顺序**变：`foreach Dictionary` 按插入序枚举（net/grp/closureNet 的键序由首次 enter 的事件序决定），加上 sweep.Sort（EffectScript.cs:145）是不稳定排序。同一剧本换序喂入 ⇒ Passed 相同、诊断文本顺序不同。对「AI 读 Violation 回修」的消费方，这是非确定输出。修法：返回前按 `(AtT, Kind, Resource, Scope)` 排序一次。
- 边缘确认：Lo=⊤ 事件被正确排除（132 行，永不存活）；零时长 [t,t] 事件因相位2先采样后退出而被计入 ✓。

## 4. Audit 三道 gate 是否可独立组合？

**结构性独立成立**：Step（EffectScript.cs:150-216）同时维护三份互不相交的运行态——`net`（gate1，仅 enter 累加、exit 不减 = 累积净额语义正确）、`peakSum/topCount`（gate2）、`grp`（gate3）；AuditAtSample（218-247）各 gate 只读自己的状态。删掉任一 gate 的检查块不影响其余 gate 的违例产出 ⇒ 可独立启停/组合 ✓。

三点保留：
- **N3（Low-Medium）**：gate 之间共享 ω=⊤ 的「居民豁免」策略但方向相反——gate1 豁免守恒（154 行）、gate2 照常计峰值（179 行 top 判定）。这不是 bug（OPEN-4 有意为之），但意味着 gate1 的结果**依赖全局 ω 分布假设**：若某资源正向贡献只来自居民层，Leak 检查整体失效。gate1 并非纯局部的独立谓词，是带全局豁免条款的谓词——文档已声明，列为残留风险。
- **闭包块死代码**：EffectScript.cs:272 与 274 是逐字重复的 `if (e.Lifetime.Lo.IsTop) continue;`；且 273 行的 `Lo > closureT` 过滤恒假（closureT=maxFinite ≥ 所有有限 Lo）⇒ 该"修 auditR"过滤是空操作。无害但说明此处靠补丁堆叠而非不变量推理。
- **At 与 Audit 双轨**：Audit 不调用 At(t)，而是扫换线重建活动态。等价性靠注释断言（"数学上与端点采样定理等价"），src 中无共享代码保证二者不漂移——At 正确 ≠ Audit 正确，反之亦然。测试层应锁住 `Audit 违例 ⇔ 逐点 At+gate 全算` 这一交叉验证律。

## 5. Weight.NaN 是否破坏组合？

**当前未破坏，但埋着毒值（M4，Medium）**：
- Algebra.cs:38 `a == b ? 1.0 : double.NaN`。NaN 作为 ⊥ 编码违反自反（NaN≠NaN）、污染后续 double 运算且比较恒 false —— 它是"静默失败"的具体化，与本仓库其余部分（Numeric.cs:5 "永不 NaN"、Deviation.cs:18 "不 NaN 不 ∞"、CompareToFinite 先判 IsTop）的 ⊤-闭环哲学直接矛盾。
- 缓解事实：grep 全 src，`Weight.Of` **零调用点**，KIND_MIX 目前只有 Analyzer 层静态检查（EffectAlgebraAnalyzer.cs:189,238）。故今日无数值可被污染。
- 风险：它是公开 API 面。第一个跨 kind 聚合的调用者拿到的不是异常而是 NaN，错误延迟到下游某个 `> threshold` 恒 false 处才显形。DeviationVal.Top（Numeric.cs:113-140 已示范正确形态：显式 IsTop 包装、ExceedsThreshold fail-closed）就在同一个 codebase 里——没有理由 Weight 不照做（返回 `WeightVal` 或抛 InvalidOperationException 让 KIND_MIX 硬失败）。

## 6. 其他观察

- `default(NatStar)/default(LoopCount)/default(Interval)` 因私有构造器挡不住 struct default，得到 IsTop=false、Value=0 的合法外观实例（ω=0）。当前构造路径均显式赋值，未触发；记录为类型系统边界备注。
- ScopeId.IncludedIn（Objects.cs:99-108）实为「扁平偏序 + Global 最大元」，自反/反对称/传递均可验证 ✓。
- NetTable.IsConserved 对"资源不在 net 中"fail-closed 返回 false（Algebra.cs:126-135）：与 SignedInterval.ContainsZero 的 ⊤-fail-closed 方向一致 ✓。

---

## 结论

| # | 发现 | 位置 | 严重度 |
|---|---|---|---|
| M1 | Join 注释承诺 size merge_I，实现为裸 Union；Interval/SignedInterval.Merge 零调用点，value-lattice join 不存在 | Objects.cs:190-192; Numeric.cs:101; SignedNet.cs:79 | Medium |
| M2 | Signature.GetHashCode 依赖桶内迭代序，结构相等可哈希不等，违反 Eq/Hash 契约（当前无哈希容器使用，潜伏） | Objects.cs:204-212 | Medium-High（潜伏） |
| M4 | Weight.Of 以 NaN 编码 ⊥，违反仓库 ⊤-闭环铁律；现零调用点，属公开 API 毒值债 | Algebra.cs:32-38 | Medium |
| N1 | Sequence≡Parallel≡Union：交换并抹除时序/因果，Parallel 的安全完全外包给带外 L3 Analyzer；两名一实是说谎的 API | DerivedMetrics.cs:50-53 | Note（设计取舍，需明示） |
| N2 | Loop 重 scope 压平嵌套作用域信息，scope 维度不可组合 | DerivedMetrics.cs:41-45 | Note |
| N3 | gate1 的居民豁免依赖全局 ω 分布，非纯局部谓词；At 与 Audit 双轨无共享代码锁定等价律 | EffectScript.cs:154 vs 179; 105-292 | Low-Medium |
| L1 | Violation 列表顺序随事件置换变化（Dictionary 插入序 + List.Sort 不稳定），诊断输出非规范序 | EffectScript.cs:145, 218-247 | Low |
| L2 | 闭包块重复死行（272=274）与恒假的 closureT 过滤（273） | EffectScript.cs:272-274 | Low |

**总评**：核心半格律（Union 幂等/交换/结合/单位元）、Loop 分配律、At(t) 置换不变、三 gate 状态独立——这些**真正承重的组合性律全部成立**，且 ℕ*/ℤ* 的 ⊤ 闭环是教科书级的正面样本。失分点集中在「接口许诺与实现交付不符」（Join 的 merge_I、Sequence/Parallel 的命名）和两处潜伏毒点（哈希序、NaN）。均为可定点修复项，无需重构。
