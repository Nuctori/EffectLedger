// QedP53SweepModelConformanceTests.cs — P3-H3 残项：扫换线（C# Audit 实现）对照 D4 阶跃模型。
// 随机重叠同资源事件集（size=1，Use 模式自兼容、入峰值），暴力计算模型峰值 = max_t 存活数；
// 断言 C# 扫换线的 PeakExceeded 阈值行为与模型精确一致：cap=peak−1 必报、cap=peak 必不报。
// 任何「贡献入账点 / Alive 边界（lo/hi 含闭）/ exit 先后序」的实现分歧都会改变实测峰值 ⇒ 本钉必红。
// （红队 P5.2-H3：D4 阶跃模型与 C# 扫换线之间缺对照桥——本测试为该桥的峰值维度。）
// 负陷维度由既有 Round/Iter 系列承载；泄漏维度由 QedP0A1/A3 钉承载。xUnit。
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace EffectLedger.Tests;

public class QedP53SweepModelConformanceTests
{
    static ResourceId Mem() => new ResourceId.Memory(1);
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    // 随机重叠事件集：同资源、size=1、Use 模式（Use×Use 自兼容 ⇒ gate(3) 不掺噪音）。
    static (ImmutableArray<EffectLedger.EffectEvent> Events, int ModelPeak) RandomOverlapScript(Random rng, int n)
    {
        var b = ImmutableArray.CreateBuilder<EffectLedger.EffectEvent>(n);
        var lives = new List<(int lo, int hi)>();
        for (int i = 0; i < n; i++)
        {
            var lo = rng.Next(0, 25);
            var hi = lo + rng.Next(0, 12);
            lives.Add((lo, hi));
            b.Add(new EffectLedger.EffectEvent(
                new EffectLedger.Interval(EffectLedger.NatStar.Of((ulong)lo), EffectLedger.NatStar.Of((ulong)hi)),
                Scene("S"),
                EffectLedger.Signature.Of(new Claim(
                    EffectLedger.Kind.Occupy, Mem(), EffectLedger.Mode.Use,
                    Scene("S"), EffectLedger.Interval.Exact(1)).Normalize()),
                EffectLedger.LoopCount.Of(1)));
        }
        // 模型峰值（D4 阶跃语义）：max_t |{e : lo(e) ≤ t ≤ hi(e)}|
        var modelPeak = 0;
        for (var t = 0; t <= 40; t++)
        {
            var alive = lives.Count(l => l.lo <= t && t <= l.hi);
            modelPeak = System.Math.Max(modelPeak, alive);
        }
        return (b.ToImmutableArray(), modelPeak);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(10)]
    public void Sweep_PeakMatchesModel_MaxOverlap(int n)
    {
        var rng = new Random(20260907 + n);
        for (int round = 0; round < 20; round++)
        {
            var (events, modelPeak) = RandomOverlapScript(rng, n);
            var script = new EffectLedger.EffectScript(events);

            // cap = modelPeak − 1 ⇒ 采样峰值必超（严格大于语义）⇒ PeakExceeded 必现
            var capLow = new Dictionary<ResourceId, EffectLedger.NatStar>
            {
                [EffectLedger.ResourceId.Normalize(Mem())] = EffectLedger.NatStar.Of((ulong)(modelPeak - 1))
            };
            Assert.Contains(script.Audit(new EffectLedger.Budget(capLow)).Violations,
                v => v.Kind == "PeakExceeded");

            // cap = modelPeak ⇒ 峰值门放行（Peak 维度精确等于模型；Leak 维度与本钉无关）
            var capExact = new Dictionary<ResourceId, EffectLedger.NatStar>
            {
                [EffectLedger.ResourceId.Normalize(Mem())] = EffectLedger.NatStar.Of((ulong)modelPeak)
            };
            Assert.DoesNotContain(script.Audit(new EffectLedger.Budget(capExact)).Violations,
                v => v.Kind == "PeakExceeded");
        }
    }

    // ── P3-H3 净额维度：NegativeDip 段首可见性（D4a NetAtAgree 的 C# 可观测推论）──
    // net 只在 lo 端点跳变 ⇒ release 先于 create 时，net 自 release.lo 起为负 ⇒
    // NegativeDip 的首个 AtT 必精确等于 release.lo（段首采样可见，不漏报不漂移）。
    // 任何「入账点漂移到 exit/采样点」的实现分歧都会改变首个 AtT ⇒ 本钉必红。
    [Theory]
    [InlineData(5, 10, 3)]
    [InlineData(2, 17, 5)]
    [InlineData(0, 30, 1)]
    public void NegativeDip_FirstVisibleAt_ReleaseLo(int relLo, int creLo, int size)
    {
        var rel = new EffectLedger.EffectEvent(
            new EffectLedger.Interval(EffectLedger.NatStar.Of((ulong)relLo), EffectLedger.NatStar.Of((ulong)(relLo + 5))),
            Scene("S"),
            EffectLedger.Signature.Of(new Claim(
                EffectLedger.Kind.Occupy, Mem(), EffectLedger.Mode.Release,
                Scene("S"), EffectLedger.Interval.Exact((ulong)size)).Normalize()),
            EffectLedger.LoopCount.Of(1));
        var cre = new EffectLedger.EffectEvent(
            new EffectLedger.Interval(EffectLedger.NatStar.Of((ulong)creLo), EffectLedger.NatStar.Of((ulong)(creLo + 5))),
            Scene("S"),
            EffectLedger.Signature.Of(new Claim(
                EffectLedger.Kind.Occupy, Mem(), EffectLedger.Mode.Create,
                Scene("S"), EffectLedger.Interval.Exact((ulong)size)).Normalize()),
            EffectLedger.LoopCount.Of(1));
        var script = new EffectLedger.EffectScript(ImmutableArray.Create(rel, cre));

        var dips = script.Audit(EffectLedger.Budget.None).Violations
            .Where(v => v.Kind == "NegativeDip")
            .Select(v => (int)v.AtT.Value)
            .OrderBy(t => t)
            .ToArray();

        Assert.NotEmpty(dips);
        Assert.Equal(relLo, dips[0]); // 首个负陷精确出现在 release 的 lo（段首采样可见）
        Assert.All(dips, t => Assert.InRange(t, relLo, creLo - 1)); // create 入账后回正，此后无负陷
    }
}
