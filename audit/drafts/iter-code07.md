# 迭代07 审计（L1 性质测试）

## 摘要
- 测试：41 通过 0 失败（父会话已 `dotnet test` 复核：失败 0 / 通过 41 / 跳过 0，可复现）。
- 审计对象：仅 `tests/Cosmos.EffectAlgebra.Tests/PropertyTests.cs`（7 个 `[Fact]` 性质测试 + 6 个确定性生成器，`Random(42)`）；被测实现 Numeric/Objects/Algebra/SignedNet/Deviation（读源码核对断言路径）。
- open 项总数：2（可闭 2 / 设计 out-of-scope 0）。两项均为「测试覆盖深化」，非被测实现的正确性问题（SUT 数学均已在 iter02/iter05 单测中证明）。
- 终止判定：**需继续(2)**（低严重度）——§3.2.3 良性配对结果、§3.1.5a `min(⊤,x)=x` 反向未在本文件断言，属「定律全验证」缺口。

## 逐条核对（回指行号 + PDR § + 真覆盖边界? + 假绿? + 结论）

| 测试 | 行号 | PDR § | 真覆盖边界? | 假绿? | 结论 |
|---|---|---|---|---|---|
| NatStar_RandomTopClosure | L15-36 | §3.1.5a | 真：RandNat(L165) k=0⇒Top、k=1⇒Of(0)、k=2⇒Of(1)、k=3⇒有限；1000 组必含 ⊤/0。乘 ⊤ 律 L26-27（`a*Top==Top`、`Top*a==Top`，a=Of(0) 亦覆盖 ⇒ `0×⊤=⊤`）；max ⊤ L29；min ⊤ L30 `!a.IsTop ⇒ a.Min(Top)==a`；交换 L22、结合 L24 | 否 | OK（但 `min(⊤,x)=x` 反向未断言，见 OPEN-2） |
| NatStar_RandomTotalOrder | L38-60 | §3.1.5a | 真：两 Top L44-45 ⇒ 0；一 Top L47-49 ⇒ ±1 且 `ba=-ab`；有限 L51-53 ⇒ 符号相反 | 否 | OK |
| Interval_RandomMergeLaws | L62-80 | §3.1.5b | 真：RandInterval(L177) form0 `[x,⊤]`、form1 `[⊤,⊤]`、form2 `[x,y]`（lo≤hi），**避开 [⊤,x] 非法**；幂等 L68、交换 L70、结合 L72 | 否 | OK |
| ScopeId_RandomPartialOrder | L82-103 | §3.1.3b | 真：自反 L87（`x.IncludedIn(x)` 七标签含 Global/Shell/Loop/Conditional）；Global 最大元 L89；跨标签不可比 L91-94 `Method(n)⊄Scene(m)` 且反向 false（实现 L?：非 Equals 且非 Global ⇒ false） | 否 | OK（注：仅测 Method×Scene 一对跨标签，未随机化跨标签组合；实现路径平凡，可接受） |
| Compatible_RandomLaws | L105-131 | §3.2.3 | 部分：25×25 穷举（L107-122）断言 CONFLICT L115-117 false、Use L119 true、Unknown L121 true、对称 L113；1000 随机对称 L128。**但良性配对（P3）create+release/create+move/release+move ⇒ true 未断言**（见 OPEN-1） | 否（无恒真断言；对称断言若实现不抛出则恒真，但绑定 CONFLICT/Use/Unknown 断言可捕获回归） | OK（缺 P3 良性配对结果断言，OPEN-1） |
| ResourceId_RandomNormalizeIdempotent | L133-144 | §3.1.4a | 真：RandResource(L208) 9 类含 `Tree.Unknown`/`Self("signal_")/SignalBus` 等；1000 组 `Normalize(Normalize(r))==Normalize(r)` | 否 | OK |
| Claim_RandomNormalizeConsistent | L146-163 | §3.1.1/§3.1.4a | 真：Self("signal_"+s)≡SignalBus(s)（ST-02）L150-152 双向归一相等；缺省 size `default`⇒`Interval.Default` L154-155 | 否 | OK |
| 生成器边界 | RandNat L165 / RandInterval L177 / RandScope L192 / RandMode L203 / RandResource L208 | §3.1.5a/§3.1.5b/§3.1.3b/§3.2.3/§3.1.2 | 真：⊤/0/1/缺省/跨桶（Resource 9 类跨 kind、Scope 7 标签）均触达 | 否 | OK |
| 假绿扫描 | 全局 | — | — | 否：无 `Assert.True(true)`；无「生成器保证必过」的恒真断言；每断言对应可证伪定律 | OK |
| 种子可复现 | L12 `Random Rng = new(42)` | — | 真：确定性种子 | — | OK |
| 出处注释 | 各方法签名注释 | — | 真：7 方法均带 §x.y | — | OK |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | PropertyTests.cs L107-122（Compatible 穷举循环） | 25×25 穷举仅断言 CONFLICT-false / Use-true / Unknown-true / 对称，**未断言 §3.2.3 P3 良性生命周期配对结果**：`(Create,Release)/(Release,Create)/(Create,Move)/(Move,Create)/(Release,Move)/(Move,Release) ⇒ true`。若实现误改良性配对为冲突，本循环不会捕获（对称断言对「false⇔false」仍成立）。§3.2.3 定律含 P3，故「定律全验证」缺口。 | 可闭（测试覆盖） | 低 | 在 L121 后补三行：`if ((a, b) is (Mode.Create, Mode.Release) or (Mode.Release, Mode.Create)) Assert.True(r);` 及 create+move、release+move 同理。注：该结果已在 iter02 `Compatible_CreateReleasePairing` 单测断言，但本性质文件应自洽覆盖。 |
| OPEN-2 | PropertyTests.cs L30 | `min` ⊤ 律仅断言 `min(x,⊤)=x`（receiver 有限，arg=Top）；`min(⊤,x)=x`（receiver=Top，arg 有限）方向未断言。实现 `Min`（Numeric.cs L?）：`return IsTop ? o : this`，receiver=Top 时返回 `o`（有限）→ 结果应为 `a`，未测。 | 可闭（测试覆盖） | 低 | 在 L30 后补一行：`Assert.Equal(a, NatStar.Top.Min(a));`（a=Top 时 `Top.Min(Top)==Top` 亦成立，可无条件断言）。 |

## 结论
- 7 个性质测试均为**真随机穷举 / 1000 组**，断言对应可证伪代数定律，无假绿（无 `Assert.True(true)`、无生成器保证必过），`Random(42)` 可复现。
- ⊤/0/1/缺省/跨桶边界均被生成器触达；§3.1.5a 加乘 ⊤ 律、§3.1.5b join-semilattice、§3.1.3b 偏序、§3.1.4a 归一幂等/SignalBus 等价、§3.1.1 缺省 size ⇒ Default 均真落实。
- 唯一缺口（均低严重度、可闭）：OPEN-1 §3.2.3 P3 良性配对结果未在本文件断言；OPEN-2 §3.1.5a `min(⊤,x)=x` 反向未断言。二者属测试覆盖深化，被测实现正确性已由 iter02/iter05 单测独立证明，无 SUT 正确性风险。
- 终止判定：**需继续(2)**——补 OPEN-1/OPEN-2 两行断言后，§3.2.3 与 §3.1.5a 性质覆盖方达「定律全验证」严格标准。
