using System.Diagnostics;
using System.Linq;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// 迭代16 — 规模/性能不回归守护（§3.3.1/§3.3.2/§9.1/§3.1.5a）。
/// 目的：证明 L1 在大规模 Signature 下 net/Peak/Deviation 仍正确、非指数增长、
///   非负溢出（溢出保守 ⇒ ⊤）、且不崩溃（无 NaN/∞/抛异常）。
/// 规模常量见 <see cref="N"/>/<see cref="SmallN"/>；所有断言可证伪（回归必红）。
/// 不引魔法数：规模、性能预算、size 均为命名常量。
/// 重要：<see cref="Signature"/>（§3.1.4b）是 Claim 的**集合**（ImmutableHashSet），
///   相同 Claim 会去重 ⇒ 规模由「不同资源/不同作用域/不同 size」的**互异** Claim 承载，
///   重复 identical Claim 不增加规模。故本文件一律构造互异 Claim（逐资源 k），并以
///   <see cref="SignatureExtensions.AllClaims"/> 计数断言规模真实（防集合去重导致假绿）。
/// </summary>
public class ScaleGuardTests
{
    private const int N = 5000;            // §3.3.1 大签名规模（互异 Claim 数量级）
    private const int SmallN = 2000;       // §9.1 大输入规模（Deviation 每签名互异 Claim 数）
    private const int PerfN = 5000;        // §3.3.1 性能软约束规模
    private const int PerfBudgetMs = 2000; // §3.3.1 性能宽松上界（弱约束，非硬超时）
    private const ulong UnitSize = 1;      // §3.3.1 单值 size
    private const ulong PeakUnit = 100;    // §3.3.2 单值 size（峰值求和项）

    private static readonly ScopeId GlobalScope = new ScopeId.Global();

    // §3.3.1 — 构造 count 个**互异** occupy claim（各占不同 Memory 资源 k），mode 全部 create，size 精确 [s,s]。
    // 纯 create 的 joined net = [s,s]（lo=s>0）⇒ IsConserved 必 false（泄漏，fail-closed 报警）。
    private static Signature BuildPureCreates(int count, ulong s)
    {
        var claims = new Claim[count];
        for (int k = 0; k < count; k++)
            claims[k] = new Claim(Kind.Occupy, new ResourceId.Memory((ulong)k), Mode.Create, GlobalScope, Interval.Exact(s));
        return Signature.Of(claims);
    }

    // §3.3.1 — 构造 2*count 个**互异** occupy claim：每个资源 k 配一个 create + 一个 release，size 精确 [s,s]。
    // create[+s] 与 release[−s] 同资源抵消 ⇒ joined net = [−s,s]（含 0）⇒ IsConserved true（闭合，不报警）。
    private static Signature BuildBalanced(int count, ulong s)
    {
        var claims = new Claim[2 * count];
        for (int k = 0; k < count; k++)
        {
            claims[2 * k] = new Claim(Kind.Occupy, new ResourceId.Memory((ulong)k), Mode.Create, GlobalScope, Interval.Exact(s));
            claims[2 * k + 1] = new Claim(Kind.Occupy, new ResourceId.Memory((ulong)k), Mode.Release, GlobalScope, Interval.Exact(s));
        }
        return Signature.Of(claims);
    }

    /// <summary>§3.3.1 — 大签名 net 守恒正确：5000 个互异 Claim（2500 资源各 create+release）⇒ 每资源 IsConserved true；
    /// 5001 个纯 create 互异 Claim（无匹配释放）⇒ 样本资源 IsConserved false。断言不抛、不超时，且规模真实（计数断言防集合去重假绿）。</summary>
    [Fact]
    public void LargeSignature_NetConservation_Correct()
    {
        // 平衡：2500 资源 × {create, release} = 5000 互异 Claim ⇒ 每资源 joined net 含 0 ⇒ 守恒
        var balanced = BuildBalanced(N / 2, UnitSize);
        Assert.Equal(N, balanced.AllClaims().Count()); // §3.1.4b 集合去重后仍为 5000（互异）⇒ 规模真实
        var balancedNet = NetTable.Compute(balanced, GlobalScope);
        for (int k = 0; k < N / 2; k++)
            Assert.True(balancedNet.IsConserved(new ResourceId.Memory((ulong)k))); // §3.3.1 DO-9：含 0 ⇒ 不报警

        // 泄漏：5001 互异纯 create（无 release）⇒ 样本资源 joined net = [1,1]，lo>0 ⇒ 不守恒（fail-closed 报警）
        var leaked = BuildPureCreates(N + 1, UnitSize);
        Assert.Equal(N + 1, leaked.AllClaims().Count()); // 规模真实
        var leakedNet = NetTable.Compute(leaked, GlobalScope);
        Assert.False(leakedNet.IsConserved(new ResourceId.Memory(0))); // §3.3.1 DO-9：净占用 > 0 ⇒ 报警
    }

    /// <summary>§3.3.2 — 大签名 Peak 正确：5000 个非 release 互异 occupy（size [100,100]）⇒ Peak.Value == 100*5000；
    /// 其中 2500 个为 release 时被排除 ⇒ Peak.Value == 100*2500（仅算 mode≠release）。规模真实（计数断言）。</summary>
    [Fact]
    public void LargeSignature_Peak_Correct()
    {
        // 全 create 互异 occupy（mode≠release）⇒ 求和 100*5000
        var allCreate = BuildPureCreates(N, PeakUnit);
        Assert.Equal(N, allCreate.AllClaims().Count()); // 规模真实（互异，未去重）
        Assert.Equal(NatStar.Of(PeakUnit * (ulong)N), Peak.Compute(allCreate, GlobalScope)); // §3.3.2 size 求和

        // 半 release 半 create（互异资源）：release 被排除，仅 2500 个 create 计入 ⇒ 100*2500
        var half = new Claim[N];
        for (int k = 0; k < N; k++)
        {
            var mode = (k % 2 == 0) ? Mode.Create : Mode.Release; // §3.3.2 c.mode≠release：release 不贡献峰值
            half[k] = new Claim(Kind.Occupy, new ResourceId.Memory((ulong)k), mode, GlobalScope, Interval.Exact(PeakUnit));
        }
        var mixed = Signature.Of(half);
        Assert.Equal(N, mixed.AllClaims().Count()); // 规模真实
        Assert.Equal(NatStar.Of(PeakUnit * (ulong)(N / 2)), Peak.Compute(mixed, GlobalScope)); // release 已排除
    }

    /// <summary>§9.1 — Deviation 大输入不崩：两签名各 2000 个互异 occupy（expected/actual 全 [1,1] 同资源集）⇒ 有限小偏差（≈0），
    /// 不 NaN/不 ∞；actual 多一个 ⊤ 项资源 ⇒ 整体 DeviationVal.Top（§3.1.5c 跳过，不崩溃）。</summary>
    [Fact]
    public void LargeDeviation_NoCrash_And_TopWhenUnknown()
    {
        var expectedClaims = new Claim[SmallN];
        var actualClaims = new Claim[SmallN];
        for (int k = 0; k < SmallN; k++)
        {
            var res = new ResourceId.Memory((ulong)k);
            expectedClaims[k] = new Claim(Kind.Occupy, res, Mode.Create, GlobalScope, Interval.Exact(1));
            actualClaims[k] = new Claim(Kind.Occupy, res, Mode.Create, GlobalScope, Interval.Exact(1));
        }
        var expected = Signature.Of(expectedClaims);
        var actual = Signature.Of(actualClaims);
        Assert.Equal(SmallN, expected.AllClaims().Count()); // 规模真实
        Assert.Equal(SmallN, actual.AllClaims().Count());

        var dev = SignatureDeviation.Calculate(expected, actual);
        Assert.False(dev.IsTop);              // §9.1 有限偏差
        Assert.False(double.IsNaN(dev.Value)); // 不 NaN
        Assert.False(double.IsInfinity(dev.Value)); // 不 ∞
        Assert.True(dev.Value < 1e-9);        // 同输入 ⇒ 偏差 ≈ 0

        // actual 多一个 ⊤ 项资源（Dynamic [1,⊤]，新资源不在 expected）⇒ 该资源 TryMid 失败 ⇒ 整体 ⊤（§3.1.5c 跳过）
        var actualWithTop = new Claim[SmallN + 1];
        for (int k = 0; k < SmallN; k++)
            actualWithTop[k] = actualClaims[k];
        actualWithTop[SmallN] = new Claim(Kind.Occupy, new ResourceId.Memory(99999), Mode.Create, GlobalScope, Interval.Dynamic); // Hi=⊤
        var actualTopSig = Signature.Of(actualWithTop);

        var devTop = SignatureDeviation.Calculate(expected, actualTopSig);
        Assert.True(devTop.IsTop);            // §9.1/§3.1.5c：任一 ⊤ ⇒ 整体 ⊤（不崩溃、不报警）
    }

    /// <summary>§3.1.5a — 规模上限保护：溢出 ⇒ 保守 ⊤（不溢出到负）；非溢出大数仍有限且为正。</summary>
    [Fact]
    public void NatStar_Overflow_ConservativeTop()
    {
        // 溢出：MaxValue + 1 与 MaxValue * 2 均超出 ulong 范围 ⇒ 环绕检测 ⇒ ⊤ 保守
        Assert.True((NatStar.Of(ulong.MaxValue) + NatStar.Of(1)).IsTop);   // §3.1.5a 加溢出 ⇒ ⊤
        Assert.True((NatStar.Of(ulong.MaxValue) * NatStar.Of(2)).IsTop);   // §3.1.5a 乘溢出 ⇒ ⊤

        // 非溢出：MaxValue/2 + MaxValue/2 = 2^64-2，仍 ≤ MaxValue ⇒ 有限且为正（不溢出到负，证明守护有效）
        var big = NatStar.Of(ulong.MaxValue / 2) + NatStar.Of(ulong.MaxValue / 2);
        Assert.False(big.IsTop);
        Assert.True(big.Value > 0); // §3.1.5a：非负，不上溢到负
        Assert.Equal(ulong.MaxValue - 1, big.Value);
    }

    /// <summary>§3.3.1 — 性能软约束：N=5000 互异 occupy 签名的 net 计算 < 2000ms（弱约束，非硬超时；超时仅记软失败）。</summary>
    [Fact]
    public void LargeSignature_Net_PerformanceSoftBudget()
    {
        var sig = BuildPureCreates(PerfN, UnitSize);
        Assert.Equal(PerfN, sig.AllClaims().Count()); // 规模真实

        var sw = Stopwatch.StartNew();
        var net = NetTable.Compute(sig, GlobalScope);
        sw.Stop();

        // 正确性保底（性能测试同时验证结果正确）
        Assert.False(net.IsConserved(new ResourceId.Memory(0)));

        // 性能软约束：宽松上界 2000ms；若超界仅作软失败（CI 弱约束，不硬阻塞，见 §3.3.1 性能注记）
        var elapsedMs = sw.ElapsedMilliseconds;
        Assert.True(elapsedMs < PerfBudgetMs,
            $"net 计算耗时 {elapsedMs}ms 超出软预算 {PerfBudgetMs}ms（性能回归预警，非硬失败）");
    }
}
