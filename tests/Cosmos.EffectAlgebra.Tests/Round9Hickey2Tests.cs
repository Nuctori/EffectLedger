using Xunit;
using System.Linq;
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>rich-hickey2 R9（可诊断性 / 回修反例信息可定位性）— D09-001/002：Violation 须携带来源事件索引，用户据报错直接定位 events[N] 回修。</summary>
public class Round9Hickey2Tests
{
    // ── D09-001：Leak 违例须报出"哪条事件未闭合"（EventIndex 可定位 events[N]） ──
    [Fact]
    public void LeakViolation_CarriesSourceEventIndex()
    {
        var e0 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(10))), LoopCount.Of(1));
        var e1 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(10))), LoopCount.Of(1));
        var script = new EffectScript(ImmutableArray.Create(e0, e1));
        var aud = script.Audit(Budget.None);
        var leak = aud.Violations.FirstOrDefault(v => v.Kind == "Leak");
        Assert.True(leak.EventIndex >= 0, "Leak 须携带来源事件索引（可定位 events[N]）");
        Assert.Equal(0, leak.EventIndex); // 首个贡献该未闭合资源的事件 ⇒ events[0]
    }

    // ── D09-002：PeakExceeded 违例须报出"哪条事件触发峰值超限"（EventIndex 可定位 events[N]） ──
    [Fact]
    public void PeakViolation_CarriesTriggeringEventIndex()
    {
        var e0 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(3))), LoopCount.Of(1));
        var e1 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(10))), LoopCount.Of(1));
        var cap = new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar> { [new ResourceId.Gpu(new Rid("r"))] = NatStar.Of(5) });
        var script = new EffectScript(ImmutableArray.Create(e0, e1));
        var aud = script.Audit(cap);
        var peak = aud.Violations.FirstOrDefault(v => v.Kind == "PeakExceeded");
        Assert.True(peak.EventIndex >= 0, "PeakExceeded 须携带触发事件索引（可定位 events[N]）");
        Assert.Equal(0, peak.EventIndex); // 峰值 13 > 5 由首个贡献该资源峰值的事件 events[0] 起累加触发
    }
}
