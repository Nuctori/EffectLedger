// QedP0A5MultiplicityPins.cs — QED 迭代 P0-A5 多重性语义对抗钉（PO-55-01/02 代码侧收口证据）：
// 多重性合法载体唯二——剧本事件序列（审计逐条累加）与 Combination.Loop 的 size×ω（端点乘法）；
// 集合路径由 Signature.Of 构造期重复拒封死（P0-4）。PDR 推导见 §3.1.4/§3.2.1/§3.2.5/§3.3.1/§3.3.2
// 的【QED-A5】注记；对账依据 audit/qed/PO55-TRIAGE.md §2.1。xUnit。
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace EffectLedger.Tests;

public class QedP0A5MultiplicityPins
{
    static ResourceId Mem() => new ResourceId.Memory(1);
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static ScopeId LoopScope(string id) => new ScopeId.Loop(id);
    static ScopeId Global() => new ScopeId.Global();

    static Claim Oc(Mode m, ScopeId s, Interval sz)
        => new Claim(Kind.Occupy, Mem(), m, s, sz).Normalize();

    // ── 钉 1（P0-4 直连面）：重复 Claim 在 Signature.Of 构造期 loud 拒——
    //    「N 次 ∪ 静默坍缩 ⇒ 计数丢失」的 iter55 反例路径不可表达。 ──
    [Fact]
    public void Signature_Of_DuplicateClaim_Throws()
    {
        var c = Oc(Mode.Create, Scene("S"), Interval.Exact(64));
        var ex = Assert.Throws<ArgumentException>(() => Signature.Of(c, c));
        Assert.Contains("重复", ex.Message);
    }

    // ── 钉 2（PO-55-01 签名层载体）：Loop(body, 20) ⇒ net = 20×s——
    //    AUDIT003 的 1280=20×64 在签名层成立（计数在 size 量纲，不在集合基数）。 ──
    [Fact]
    public void Loop_Of20_Net_ScalesTo20x()
    {
        var body = Signature.Of(Oc(Mode.Create, Scene("S"), Interval.Exact(64)));
        var looped = Combination.Loop(body, LoopCount.Of(20), LoopScope("enemies"));

        var net = NetTable.Compute(looped, Global()).Get(ResourceId.Normalize(Mem()));
        Assert.Equal(20L * 64, net.Lo.Value);
        Assert.Equal(20L * 64, net.Hi.Value);
    }

    // ── 钉 3（PO-55-02 反死变量）：Peak 随 ω 线性增长——ω 不是死变量
    //    （max-over-copies 退化反例的代码面否定；含 AUDIT003 的 ω=20 ⇒ 1280 点）。 ──
    [Theory]
    [InlineData(1, 64)]
    [InlineData(2, 128)]
    [InlineData(5, 320)]
    [InlineData(20, 1280)]
    public void Peak_Grows_Linearly_WithOmega(ulong omega, ulong expected)
    {
        var body = Signature.Of(Oc(Mode.Create, Scene("S"), Interval.Exact(64)));
        var looped = Combination.Loop(body, LoopCount.Of(omega), LoopScope("enemies"));

        var peak = Peak.Compute(looped, Global());
        Assert.False(peak.IsTop);
        Assert.Equal(expected, peak.Value);
    }

    // ── 钉 4（PO-55-01 剧本层载体）：AUDIT003 原场景——20 条同构事件（非代码循环，size=1），
    //    扫换线逐事件计数：cap=19 报 PeakExceeded / cap=20 峰值门放行（严格大于语义）。
    //    计数发生在审计层的事件序列上，与 At(t) 集合投影的坍缩无关。 ──
    [Theory]
    [InlineData(19, true)]
    [InlineData(20, false)]
    public void Audit_TwentyIdenticalEvents_PeakGateCountsEveryEvent(ulong cap, bool expectExceeded)
    {
        var builder = ImmutableArray.CreateBuilder<EffectEvent>();
        for (int i = 0; i < 20; i++)
            builder.Add(new EffectEvent(Interval.Exact(0), Scene("S"),
                Signature.Of(Oc(Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1)));
        var script = new EffectScript(builder.ToImmutableArray());

        var caps = new Dictionary<ResourceId, NatStar> { [ResourceId.Normalize(Mem())] = NatStar.Of(cap) };
        var result = script.Audit(new Budget(caps));

        Assert.Equal(expectExceeded, System.Linq.Enumerable.Any(result.Violations, v => v.Kind == "PeakExceeded"));
    }
}
