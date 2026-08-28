using System.Collections.Immutable;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class SamplePointsTests
{
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz = default) => new Claim(Kind.Occupy, r, m, s, sz).Normalize();
    static EffectEvent Ev(Interval life, ResourceId r, Mode m, ScopeId s, Interval sz) => new(life, s, Signature.Of(Oc(r, m, s, sz)), LoopCount.Of(1));

    [Fact]
    public void ComputeSamplePoints_MaxValue_NoGhost()
    {
        var maxEv = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(ulong.MaxValue)), Scene("S"), Signature.Of(Oc(Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1))));
        var (pts, closureT) = EffectScript.ComputeSamplePoints(ImmutableArray.Create(maxEv));
        // maxFinite==MaxValue and hi is finite MaxValue (no open end) => no ghost
        Assert.DoesNotContain(NatStar.Of(0), pts.Skip(1));
        Assert.Equal(ulong.MaxValue, closureT.Value);
    }

    [Fact]
    public void ComputeSamplePoints_OpenEnd_AddsGhost()
    {
        var e = Ev(new Interval(NatStar.Of(5), NatStar.Top), Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1));
        var (pts, closureT) = EffectScript.ComputeSamplePoints(ImmutableArray.Create(e));
        Assert.Contains(NatStar.Of(6), pts);
        Assert.Equal((ulong)5, closureT.Value);
    }

    [Fact]
    public void ComputeSamplePoints_Empty_ReturnsZero()
    {
        var (pts, closureT) = EffectScript.ComputeSamplePoints(ImmutableArray<EffectEvent>.Empty);
        Assert.Single(pts);
        Assert.Equal((ulong)0, pts[0].Value);
        Assert.Equal((ulong)0, closureT.Value);
    }
}
