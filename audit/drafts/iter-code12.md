# 迭代12 审计（§11 稳定性回归守护）

## 摘要
- 测试：71 通过 0 失败（已核实；其中 `StabilityAuditTests` 14 个 DO-n 用例全绿，DO3 为 5 例 `[Theory]`）。
- 审计对象：`tests/.../StabilityAuditTests.cs`（10 个 `DO-n_` 方法）+ 被测 src（Numeric/Objects/Algebra/SignedNet/DerivedMetrics）。
- open 项总数：2（可闭 K=2 / 设计 out-of-scope M=0，均低严重度）。
- 终止判定：**需继续(2)** —— 10 条 DO-n 各自可证伪锁定 §11 根因、无假绿、出处完整；仅 2 处低严重度可闭项（DO5 传递/反对称覆盖退化、DO3 注释根因措辞）。

## 逐条核对（回指行号 + PDR § + 可证伪? + 假绿? + 结论）

| DO-n | 行号 | 被测 | PDR § | 可证伪? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| DO-1 | L30-41 | Numeric.cs `Interval.Default`(L62-63 `[1,1]`)、`Interval.Exact`(L68) | §3.1.5(d)/(c) | 是：Default 改 `[0,0]`/`[0,1]` ⇒ Lo/HI≠Of(1) 必红 | 否 | OK |
| DO-2 | L44-54 | Numeric.cs `Interval` 构造子(L49-53) | §3.1.5 | 是：放开 lo>hi 或 lo=⊤ 校验 ⇒ 不抛 ⇒ 必红；合法 `[⊤,⊤]`/`[1,⊤]` 不抛也断言 | 否 | OK |
| DO-3 | L57-66 | Numeric.cs `DeviationVal.ExceedsThreshold`(`!IsTop && Value>threshold`) | §3.1.5c/§9.1 | 是：`Top` 任阈值恒 false；附 `Of(1.0)>0.2`=true、`Of(0.1)>0.2`=false 证非恒 false | 否 | OK（注释根因措辞弱，见 OPEN-2） |
| DO-4 | L69-84 | DerivedMetrics.cs `Combination.Loop`(L36-46)+`Peak.Compute`(Algebra.cs) | §3.2.5/§3.3.2 | 是：ω=⊤ 若产生有限 Peak 或死循环 ⇒ 必红/超时；附 ω=3⇒Peak=3 证非恒 ⊤ | 否 | OK |
| DO-5 | L87-104 | Objects.cs `ScopeId.IncludedIn`(L? ⊆* 查表) | §3.1.3b | 部分：自反/Global 最大元/跨标签不可比均真；**传递与反对称断言退化为自反**（见 OPEN-1） | 否 | OK（OPEN-1） |
| DO-6 | L107-122 | Algebra.cs `NetTable.Compute`(按归一资源键分组)+`IsConserved` | §3.3.1 | 是：若跨资源混算（Tree 吸收 Memory release）⇒ 键数≠2 或某资源误判守恒 ⇒ 必红 | 否 | OK |
| DO-7 | L125-146 | Algebra.cs `NetTable.Compute`(`if c.Kind!=Occupy continue`) | §3.3.1 | 是：若 read/write 被并入 net ⇒ `withOcc` 的 `Get(mem)` 值≠[1,1] ⇒ 必红；`rwOnly` 不守恒也锁 | 否 | OK |
| DO-8 | L149-160 | Objects.cs `ResourceId.Normalize`(`Self("signal_"+s)⇒SignalBus(s)`, ST-02) | §3.1.4a | 是：若归一分裂（Self 不归 SignalBus）⇒ 两键不等 ⇒ 必红；反向不同名 `NotEqual` 证非恒等 | 否 | OK |
| DO-9 | L163-179 | Algebra.cs `NetTable.Negate`(有符号 `-hi,-lo`)+`IsConserved`(区间含 0) | §3.3.1 | 是：锁回退 iter05 OPEN-1 旧 bug（无符号 `[1,1]` 误判守恒）；create+release⇒true、仅 create⇒false 均真 | 否 | OK |
| DO-10 | L182-195 | Algebra.cs `Compatible.IsCompatible`(全函数+对称) | §3.2.3 | 是：25 组合均返 bool（不抛）且对称；若某对抛/不对称 ⇒ 必红 | 否 | OK |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | StabilityAuditTests.cs L87-104 (`DO5_ScopePartialOrderIsConsistent`) | 传递性断言 `m.IncludedIn(m) && m.IncludedIn(g) ⇒ m.IncludedIn(g)` 实为「a⊆a 且 a⊆g ⇒ a⊆g」，仅等价于 Global 最大元 + 自反，未组合三个**互异** scope 的 a⊆b∧b⊆c⇒a⊆c；反对称性断言 `m.IncludedIn(m) && m.IncludedIn(m) ⇒ m.Equals(m)` 亦退化为自反，未测 a⊆b∧b⊆a⇒a==b（互异 a,b）。当前 `IncludedIn` 模型仅 `Equals`/`Global` 两种可比情形，故 genuine 的传递/反对称无法被非平凡违反（除「全返回 true」已被 `m.IncludedIn(s)`/`g.IncludedIn(m)` 的 false 断言拦截）；但测试名/注释称「锁传递+反对称」与实际断言强度不符，审阅者易误判已覆盖。 | 可闭（测试覆盖/注释） | 低 | 在方法注释补一句：「本模型偏序仅含 `Equals` 与 `Global` 两种可比关系，故传递/反对称可归约为自反+Global 最大元+跨标签不可比；下方 false 断言已拦截全返回 true 的退化」，或将两退化断言改名 `DO5_ReflexiveAndGlobalMaximal` 以免名实不符。 |
| OPEN-2 | StabilityAuditTests.cs L57-58 (`DO3_TopDeviationNeverExceeds` XML 注释) | 注释称「守护 §11 DO-3（统一组合律依赖 ⊤ 不崩溃）」，但本测试锁的是 `DeviationVal.Top.ExceedsThreshold` 恒 false（§9.1「⊤ 不触发 0.2 报警」），与「统一组合律」(Combination/Loop 的 ⊤ 闭合，已归 DO-4) 并非同一根因。根因措辞错挂。 | 可闭（注释） | 低 | 注释根因改为「§11 DO-3 / §9.1 — ⊤ 不报警（类型边界不崩溃）」，与 DO-4（组合律 ⊤ 闭合）区分。 |

## 结论
- 10 条 DO-n 各自**真可证伪锁定一条 §11 根因**：Default 常量(DO-1)、构造不变量(DO-2)、⊤ 不报警(DO-3)、ω=⊤ 可终止(DO-4)、scope 偏序(DO-5)、资源键不串(DO-6)、net 仅 occupy 桶(DO-7)、归一单点真相(DO-8)、守恒判据(DO-9)、Compatible 全函数对称(DO-10)。无 `Assert.True(true)`、无生成器保证必过之恒真断言、无「永远通过」弱断言。
- DO-9 确实锁回 iter05 OPEN-1 旧 bug（无符号 net 误判守恒），回退必红 —— 此守护有效防止该 soundness 回归。
- 唯一弱点为低严重度测试覆盖/注释：(OPEN-1) DO5 传递/反对称断言退化为自反，名实略不符；(OPEN-2) DO3 注释根因错挂「统一组合律」。二者均不波及被测实现正确性（已由 iter02/05/10 单测独立证明），仅影响审阅可理解性。
- 终止判定：**需继续(2)** —— 闭 OPEN-1/OPEN-2（均注释/改名级）后即达「10 条 DO-n 严格可证伪 + 无假绿 + 出处精准」可终止标准。
