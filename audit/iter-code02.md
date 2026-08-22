# 迭代02 审计（L1 代数定律单测）

## 摘要
- 测试结果：**28 通过 0 失败**（已独立核实 `dotnet test`：25 `[Fact]` + 2 `[Theory]`(3+2 用例) = 28，与报告一致）。
- open 项总数：**1**（可闭 1 / 设计 out-of-scope 0）。
- 补充覆盖建议（非硬性 open，低严重度）：3 条（见文末）。
- 终止判定：**需继续（1）**——核心代数定律均被真覆盖、无假绿；仅 1 处测试名/注释与其实际断言不符（transitivity 未真测），须修正以对齐 §3.1.3b。

## 逐测试核对（回指行号 + PDR § + 是否真覆盖 + 结论）

| 测试 | 行号 | PDR § | 真覆盖定律? | 假绿? | 结论 |
|---|---|---|---|---|---|
| `NatStar_TopClosure_Add` | L17-22 | §3.1.5a | 真：`Of(3)+Top`/`Top+Top`；若 + 不短路 ⊤ 会算成 3≠⊤ ⇒ 测试必失败 | 否 | OK |
| `NatStar_TopClosure_Mul` | L24-29 | §3.1.5a | 真：`Of(2)*Top`、`Top*Of(0)`（0×⊤=⊤ 保守）；若 * 不短路则 `Top*Of(0)` 访问 Value 抛或得 0≠⊤ ⇒ 必失败 | 否 | OK |
| `NatStar_TopClosure_MaxMin` | L31-37 | §3.1.5a | 真：max(x,⊤)=⊤、min(x,⊤)=x、min(⊤,x)=x | 否 | OK |
| `NatStar_CompareToFinite_TopSemantics` | L39-45 | §3.1.5a | 真：三态（⊤=⊤ / ⊤>有限 / 有限<⊤）；若实现错位必失败 | 否 | OK |
| `NatStar_Associative` | L50-57 | §3.1.5a | 真（但仅有限值）：3 组三元 (x+y)+z == x+(y+z) | 否 | OK（⊤ 未入三元组，见附注 N2） |
| `NatStar_Commutative` | L59-65 | §3.1.5a | 真：2 组 x+y==y+x | 否 | OK |
| `Interval_Merge_Idempotent` | L70-74 | §3.1.5b | 真：`m.Merge(m)==m`；若 Merge 不幂等必失败 | 否 | OK |
| `Interval_Merge_Commutative` | L76-81 | §3.1.5b | 真：`[1,3].Merge([2,5])` 双向 = [1,5] | 否 | OK |
| `Interval_Merge_Associative` | L83-89 | §3.1.5b | 真：3 个不同区间 (a.Merge(b)).Merge(c)==a.Merge(b.Merge(c))=[0,5] | 否 | OK |
| `Interval_Merge_TopLaw` | L91-96 | §3.1.5b | 真：`[1,⊤].Merge([2,⊤])==[1,⊤]`；若 Max 不内嵌 ⊤ 律会算成 [1,0] 触发构造子异常 ⇒ 必失败 | 否 | OK |
| `ScopeId_Reflexive` | L101-105 | §3.1.3b | 真：`Method("m").IncludedIn(Method("m"))` 经 Equals | 否 | OK |
| `ScopeId_GlobalMaximal` | L107-112 | §3.1.3b | 真：Method/Scene ⊆ Global | 否 | OK |
| `ScopeId_CrossLabelIncomparable` | L114-118 | §3.1.3b | 真：`Method("m") ⊄ Scene("s")` | 否 | OK |
| `ScopeId_Transitive` | L120-125 | §3.1.3b | **否**：仅断言 `a.IncludedIn(Global)`（与 GlobalMaximal 重复），未组合 a⊆b ∧ b⊆c ⇒ a⊆c | 否（断言本身真，但名/注释夸测传递性） | **OPEN-1** |
| `Compatible_Symmetric_AllPairs` | L130-135 | §3.2.3 | 真：5×5 全对 `IsCompatible(a,b)==IsCompatible(b,a)` | 否 | OK |
| `Compatible_TotalFunction_NoThrow` | L137-142 | §3.2.3 | 真：25 组合均返回 `bool`（全函数、无抛） | 否 | OK |
| `Compatible_ConflictExcluded` | L144-148 | §3.2.3 | 真：CONFLICT=(C,C)/(M,M)/(R,R) ⇒ false | 否 | OK |
| `Compatible_UseAlwaysAllowed` | L150-156 | §3.2.3 | 真：use 与 5 种 mode 双向 true | 否 | OK |
| `Compatible_UnknownTreatedAsUse` | L158-162 | §3.2.3 | 真：Unknown+Release/Move ⇒ true（P4 体现） | 否 | OK（Unknown+Create 未显式，见 N3） |
| `Compatible_CreateReleasePairing` | L164-168 | §3.2.3 | 真：create+release 双向 true（P3 良性配对） | 否 | OK |
| `ResourceId_Normalize_Idempotent` | L173-177 | §3.1.4a | 真：`Normalize(Normalize(x))==Normalize(x)`（Self("signal_x") 分支） | 否 | OK（仅 1 例，见 N4） |
| `ResourceId_Normalize_SignalBusEquivalence` | L179-188 | §3.1.4a | 真：`Self("signal_x")≡SignalBus("x")`；`Signal("signal_y")≡SignalBus("signal_y")`（结构相等） | 否 | OK |
| `Claim_Normalize_ResourceEquivalence_AndDefaultSize` | L193-200 | §3.1.1 | 真：跨资源归一后相等 + `default` size ⇒ `Interval.Default`（`Size==default` 触发） | 否 | OK |
| `Signature_Union_Idempotent` | L205-214 | §3.2.1 | 真：`Union(s,s)` 三桶 `SetEquals(s)`；若 Union 重复添加则集不等 ⇒ 必失败 | 否 | OK |
| `Signature_Buckets_Disjoint` | L216-226 | §3.1.4b | 真：同资源三 kind 各入独立桶（各 `Single`）；若 Add 不按 kind 分流则失败 | 否 | OK |

## 额外检查
- **测试发现**：28 个用例全部为有效 `[Fact]`/`[Theory]`（xUnit 实际执行 28 通过），无 `[Theory]` 空数据、无未装饰方法。总数与项目报告一致。
- **假绿扫描**：无 `Assert.True(true)` 或恒真断言。逐项回溯：每个被测定律若未在实现中落实，对应断言必失败或构造子抛异常（已在上表标注触发路径），故无假绿。
- **PDR 出处注释**：类级 XML 注释引 §3.1.5a/§3.1.5b/§3.1.3b/§3.2.3/§3.1.4a/§3.1.1/§3.2.1；各方法体内行尾/签名均带 § 标注。满足。

## open 项清单
| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | AlgebraLawsTests.cs L120-125 | `ScopeId_Transitive` 方法名与注释称「传递性」，但仅断言 `a.IncludedIn(Global)`（单步包含），与 `ScopeId_GlobalMaximal` 实质重复；§3.1.3b 传递性 `a⊆b ∧ b⊆c ⇒ a⊆c` 从未被组合验证。反对称性（a⊆b ∧ b⊆a ⇒ a==b）亦未测。 | 可闭 | 中 | 改为真实传递性测试：采样若干 (a,b,c) 满足 `a.IncludedIn(b) && b.IncludedIn(c)` 时断言 `a.IncludedIn(c)`；并补 `ScopeId_Antisymmetric`：`a.IncludedIn(b) && b.IncludedIn(a) ⇒ a.Equals(b)`。 |

## 补充覆盖建议（非硬性 open，低严重度）
- **N2**（低）：`NatStar_Associative`/`NatStar_Commutative` 仅用有限值；⊤ 未进入三元组/二元组。⊤ 闭包已由 `TopClosure_*` 单测覆盖，影响低，但可在 Theory 增加含 ⊤ 用例以显式闭包。
- **N3**（低）：`Compatible_UnknownTreatedAsUse` 仅断言 Unknown+Release / Unknown+Move；P4 全函数语义（Unknown+Create、Unknown+Unknown）未显式断言，建议补 2 行。
- **N4**（低）：`ResourceId_Normalize_Idempotent` 仅 `Self("signal_x")` 一例；`SignalBus`/`Signal`/`Memory` 等规范形式分支的 `Normalize(Normalize(x))==Normalize(x)` 未分别验证，建议补 1-2 例。

## 结论
- 25 个 law 测试方法中 24 个真覆盖其声称的 PDR 定律且非假绿（已逐项回溯「若实现错误则断言必失败」的触发路径）。
- 类型边界主体已进类型，测试确实在证明它成立：NatStar ⊤ 闭包/三态比较、Interval.Merge 四律（含 ⊤ 律）、ScopeId 自反/Global 最大元/跨标签不可比、Compatible 25 对全函数+对称+冲突集+use 放行+Unknown⇒Use+配对、Normalize 幂等+SignalBus 等价、Claim 归一+缺省 size、Signature 幂等+三桶隔离——均落实。
- **1 个 open（OPEN-1）**：`ScopeId_Transitive` 名实不符，未真测 §3.1.3b 传递性（亦缺反对称）。属可闭的测试质量缺陷，不属 L2/L3/实现职责。
- 终止判定：**需继续（1）**——修正 OPEN-1 后，iter02 L1 定律单测可达「每条声称定律被真覆盖、无假绿、无重要定律漏测」的严格标准。
