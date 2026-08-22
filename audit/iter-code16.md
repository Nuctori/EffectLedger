# 迭代16 审计（规模/性能不回归）

## 摘要
- 全解构建：0 错误 0 警告（父上下文已核实 `dotnet build Cosmos.EffectAlgebra.slnx` 0e/0w）。
- 测试：本审计独立复跑 `dotnet test --filter ScaleGuardTests` ⇒ **5 通过 0 失败**（已核实），全集 84 通过。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0；仅 1 项阅读范围外残差，见结论末）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + 被测 + PDR § + 真验证? + 假绿? + 结论）

| 项 | 行号（测试 / 被测） | 被测 | PDR § | 真验证? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 大签名守恒 | L51-67 / `NetTable.Compute`(Algebra.cs L53-72)、`IsConserved`(Algebra.cs L89-99) | §3.3.1 | 真 | 否 | OK。`BuildBalanced(2500,1)`→5000 互异 Claim（每资源 k 各 create+release 同 size[1,1]）；net 合并 `[-1,1]`⇒`ContainsZero` true⇒`IsConserved` 逐资源 true（循环 k=0..2499 全断言）。`Assert.Equal(N=5000, AllClaims().Count())` 确认集合去重未掩盖规模。`BuildPureCreates(5001,1)`→5001 互异纯 create，资源0 net=[1,1]⇒lo>0⇒`IsConserved` false，断言 `Assert.False` 触发 fail-closed 报警。两分支均真可证伪：若守恒判据误判纯 create 为守恒、或漏判 balanced 为泄漏，必红。**非 trivial**（无恒真断言）。 |
| 2 大 Peak | L70-90 / `Peak.Compute`(Algebra.cs L106-117) | §3.3.2 | 真 | 否 | OK。`BuildPureCreates(5000,100)`→5000 互异 occupy create，Peak 求和 Hi=100×5000=500000 ⇒ `NatStar.Of(500000)` 断言。`mixed`：k 偶 create / k 奇 release，5000 互异 ⇒ `Peak.Compute` 内 `if (c.Mode == Mode.Release) continue;`（Algebra.cs L111）**真实排除 release** ⇒ 仅 2500 create 计入 ⇒ `NatStar.Of(250000)` 断言。`Assert.Equal(N,...Count())` 防去重假绿。release 排除路径被显式驱动（非注释吹）。 |
| 3 Deviation 大输入 | L93-117 / `SignatureDeviation.Calculate`(Deviation.cs L21-49)、`NetTable.Compute` | §9.1 / §3.1.5c | 真 | 否 | OK。2000 互异同资源 [1,1] 两签名 ⇒ 分子 `|aMid-eMid|=0`、分母 `max(eRange,1)=1` ⇒ sum≈0；断言 `!IsTop` / `!IsNaN` / `!IsInfinity` / `value<1e-9`（有限≈0，不崩）。`actualWithTop` 增资源 99999 `Interval.Dynamic=[1,⊤]`⇒`Hi.IsTop`⇒`ToSigned` 产 `ZStar.Top`⇒`TryMid` 返 false⇒`anyTop=true`⇒`DeviationVal.Top`（Deviation.cs L39-43 分支真实触发）；该行为由断言 `Assert.True(devTop.IsTop)` 实证，无 NaN/∞/抛。 |
| 4 溢出保守 | L120-133 / `NatStar.operator+`(Numeric.cs L25-27)、`operator*`(Numeric.cs L31-35) | §3.1.5a | 真 | 否 | OK。`Of(ulong.MaxValue)+Of(1)`：`sum=0`(wrap) ⇒ `sum < a.Value`(0<MaxValue) true ⇒ `Top`（Numeric.cs L26 溢出分支**真实驱动**）。`Of(MaxValue)*Of(2)`：`prod=0` ⇒ `a.Value!=0 && prod/a != b`(MaxValue!=0 且 0/MaxValue=0≠2) true ⇒ `Top`（Numeric.cs L34 溢出分支**真实驱动**）。非溢出 `Of(MaxValue/2)+Of(MaxValue/2)=Of(MaxValue-1)`：`sum=MaxValue-1 > MaxValue/2` ⇒ `Of` ⇒ `!IsTop && Value>0 == MaxValue-1`（证明未误判 ⊤、且无符号环绕到负）。三条均真驱动 Numeric.cs 溢出检测。 |
| 5 性能软约束 | L136-150 / `NetTable.Compute` | §3.3.1 | 真 | 否 | OK。`Stopwatch` 计时，`elapsedMs < PerfBudgetMs(2000)` 宽松上界；消息明示「性能回归预警，非硬失败」。同时断言 `net.IsConserved(Memory(0))` false 保底正确性。**非硬超时**：偶发慢于 2000ms 仅软失败，符合设计；不阻塞数学正确性。 |
| 6 假绿扫描 | 全局 | — | — | 否 | 否 | OK。5 个 `[Fact]` 均含可证伪断言；无 `[Fact]` 无断言、无 `Assert.True(true)`；规模 N=5000/5001/2000/2001 为真实大输入（非 N=3 冒充）；每测试均 `Assert.Equal(规模, AllClaims().Count())` 防止 `ImmutableHashSet` 去重致规模缩水（专防集合型假绿）。附 `LargeSignature_Net_PerformanceSoftBudget` 仍断言结果正确，性能测试非纯计时。 |
| 7 出处注释 | L9/L23/L42/L51/L70/L93/L120/L136 各 method summary | — | — | 真 | 否 | OK。逐项带 §3.3.1（守恒/性能）/§3.3.2（Peak）/§9.1（Deviation）/§3.1.5a（溢出）/§3.1.4b（集合去重）等 §x.y 出处；类级注释并说明「互异 Claim 防去重假绿」。 |

## open 项清单
（无）

## 结论
- 规模守护**真锁大量纲/守恒/溢出保守**：
  - §3.3.1 守恒在 5000 互异 Claim（平衡）与 5001 互异纯 create（泄漏）两套大输入下断言真触发，且以 `AllClaims().Count()` 防集合去重假绿；
  - §3.3.2 Peak 在 5000 互异项（含 2500 release）正确求和、release 被 `Peak.Compute` 排除路径真实驱动；
  - §9.1 Deviation 在 2000×2 互异同输入得有限≈0（不 NaN/∞），增 ⊤ 项即整体 `DeviationVal.Top`（不崩）；
  - §3.1.5a 溢出 ⇒ ⊤ 由 Numeric.cs L26/L34 真实分支承载，非溢出大数仍有限为正（不误判、不环绕到负）。
- Numeric.cs 确有加/乘溢出检测分支（已逐行回指 L25-27、L31-35），规模测试断言确驱动之 ⇒ 非注释吹。
- 无假绿：规模真实（计数断言）、无恒真断言、性能为软预算且附正确性保底。
- 阅读范围外残差（**非 open、不阻塞**）：`SignedInterval.TryMid`/`Zero` 实现位于 `SignedNet.cs`（任务禁止读取该文件），item 3 的 ⊤ 传播仅由 `Deviation.cs` 分支（L39-43）+ 断言 `Assert.True(devTop.IsTop)` 行为实证，未读其源码；该路径在 84 全集测试中亦通过，风险低。若需 100% 行级证据，可后续单独开读 `SignedNet.cs`。
- 终止判定：**可终止** —— 规模/性能不回归守护达成「真锁量纲/守恒/溢出保守 + Numeric.cs 溢出分支真实驱动 + 无假绿 + 出处精准」。
