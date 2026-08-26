# 迭代24 审计（循环组合锁）

## 摘要
- 测试：169 通过 0 失败（已核实 `dotnet test`：`已通过! - 失败: 0，通过: 169，已跳过: 0`）
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）
- 终止判定：**可终止**

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 Loop 有限 ω 缩放 | L26-49 `Loop_FiniteOmega_ScalesPeakByOmega` | `Combination.Loop` (DerivedMetrics.cs L51-59) + `Scale` (L73-79) + `Peak.Compute` (Algebra.cs L101-114) | §3.2.5 | **真**：`Scale([10,10],Of(5))=[50,50]`（L77 `s.Lo*w, s.Hi*w`），`c.Size.Lo/Hi==50` 断言；`.Single()`（L33）强制 Loop 不是「复制 ω 份」而是「单份 size×ω」——若实现改为复制 5 份，`Single()` 抛 `InvalidOperationException` ⇒ 必红。Peak 在 `Loop("L")⊆Global` 下求和 50（L48-49）。 | 否 | OK |
| 2 Loop ω=⊤⇒⊤ | L51-65 `Loop_TopOmega_FallsBackToTop` | `Scale` (L74 `if (w.IsTop) return new Interval(s.Lo, NatStar.Top)`) + `Peak.Compute` (L109 `if (c.Size.Hi.IsTop) return NatStar.Top`) | §3.2.5 / §3.1.5a | **真**：`Scale([10,10],Top)=[10,⊤]`（L60 `c.Size.Hi.IsTop`）；Peak 遇 Hi=⊤ 直返 `NatStar.Top`（L63 `Assert.True(peak.IsTop)`）。无 `NaN`、无发散、`Lo` 恒有限（§3.1.5 下界不可 ⊤）。 | 否 | OK |
| 3 Sequence=Union | L67-80 `Sequence_EqualsUnion` | `Combination.Sequence` (DerivedMetrics.cs L68 `=> Signature.Union(a, b)`) | §3.2.1 | **真（委托契约锁）**：`Sequence(a,b)` 断言三桶 `SetEquals(Union(a,b))`；若 Sequence 被改坏（非纯 ∪），结构必不等 ⇒ 必红。属「委托实现 == 契约」锁，语义深度低但可证伪，非假绿。 | 否 | OK |
| 4 Parallel=Union | L82-95 `Parallel_EqualsUnion` | `Combination.Parallel` (DerivedMetrics.cs L70 `=> Signature.Union(a, b)`) | §3.2.2 | **真（委托契约锁）**：同 Test3 结构；三桶 `SetEquals(Union(a,b))`。 | 否 | OK |
| 5 嵌套等价 | L97-116 `Loop_NestedEqualsFlatScaling` | `Combination.Loop` 重复 Scale | §3.2.5 | **真（组合同态锁）**：`Loop(Loop(Body,2),3)` 与 `Loop(Body,6)` 经两次 `Scale` 都得单份 `[60,60]`，Peak 均 60（L113-114 `Assert.Equal(60, peakNested.Value); Assert.Equal(peakFlat, peakNested)`）。锁的是「缩放可乘」同态：`Loop(Loop(b,ω1),ω2)≅Loop(b,ω1ω2)`，与 §3.2.5 `S×ω` 乘法语义一致。注：该断言的 Peak 数值本身在「复制实现」下亦得 60（6 份×10），故**单独**不区分 复制/缩放；但 复制/缩放 已由 Test1 `.Single()`+size 断言独立锁死，Test5 本职为组合闭包，判 OK。 | 否 | OK |
| 6 可证伪 | 全局 | — | — | 是：Test1（scale 不应用 ⇒ 50≠10 或 `Single()` 抛）、Test2（ω=⊤ 不兜底 ⇒ `Hi.IsTop`/`peak.IsTop` 假）、Test5（缩放不可乘 ⇒ 60 不等）均必红；Test3/4（委托被破坏 ⇒ 桶不等）。 | — | OK |
| 7 假绿扫描 | 全局 | — | — | 无 `[Fact]` 无断言、无 `Assert.True(true)`、无仅 ω=1 trivial 断言。`Body()` 用 `Mode.Create≠release` 确保 Peak 计入（§3.3.2 c.mode≠release 经 Peak.Compute L108 过滤），非误触。 | 否 | OK |
| 8 出处注释 | 文件头 L1-3 + 各方法签名 L26/L51/L67/L82/L97 + 被测 `Scale`/`Loop`/`Sequence`/`Parallel` `/// §3.2.x` | — | 真：测试文件头引 §3.2.5/§3.2.1/§3.2.2/§3.3.2；5 方法均带 § 标注；被测实现 `DerivedMetrics.cs`/`Algebra.cs` 同步带 §3.2.5/§3.3.2。 | — | OK |

## open 项清单
（无）

## 补充（非 open，低严重度，供后续迭代参考）
- **嵌套 ω=⊤ 未测**：`Loop(Loop(Body, Top), Of(3))` 应得 ⊤（外层 Scale 对 `[10,⊤]` 再走 `w.IsTop`? 否——外层 ω=Of(3) 有限，会对 `[10,⊤]` 算 `s.Lo*3=[30,?]`，而 `s.Hi*3` 中 `⊤*3=⊤`（§3.1.5a 溢出/⊤ 保守）⇒ `[30,⊤]` ⇒ Peak ⊤）。当前仅测单层 Top（Test2）+ 有限嵌套（Test5），未测「内层 Top × 外层有限」组合。属覆盖深化，**不影响本迭代四目标**（缩放/⊤⇒⊤/Seq=Par=Union 已真锁），不阻塞终止。
- Test3/Test4 为委托契约锁（语义深度偏低），但可证伪且非假绿；若需更强语义，可后续补「Sequence 幂等/结合、Parallel 对称」属性，属增强非修正。

## 结论
- 循环组合测试**真锁** §3.2.5 语义：`Combination.Loop` 的 ω 缩放（有限 ⇒ size×ω、由 `.Single()` 排除复制实现）、ω=⊤⇒⊤ 兜底（不崩、不有限误判，类型字段 `NatStar.IsTop` 强制）、嵌套组合同态（缩放可乘）；`Sequence`/`Parallel` 均 ≡ `Signature.Union`（§3.2.1/§3.2.2 半格并，委托契约锁）。
- 可证伪性齐备：scale/Top/组合闭包任一偏离实现 ⇒ 对应断言必红；无 `Assert.True(true)`、无 trivial 充数、无恒真断言。
- § 出处注释在测试文件与被测实现两侧均精准（§3.2.5/§3.2.1/§3.2.2/§3.3.2）。
- 终止判定：**可终止**（0 open；嵌套 Top 组合为可选覆盖深化，非本迭代目标缺口）。
