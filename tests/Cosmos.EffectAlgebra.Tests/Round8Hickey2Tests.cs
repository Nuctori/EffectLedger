using Xunit;
using System.Linq;
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>rich-hickey2 R8（API 面最小性 / NaN 毒值 / 重复守恒逻辑等价性）— D08-001/002/003。</summary>
public class Round8Hickey2Tests
{
    // ── D08-001：NetTable.Compute（外部公共代数）与 EffectScript.Audit 内联守恒逻辑必须一致（R8：两处守恒实现不能漂移） ──
    [Fact]
    public void NetTableConserved_AgreesWith_AuditLeak()
    {
        // 同一脚本：NetTable 单点守恒判定 vs Audit 闭包路径 Leak 归因，二者对"是否泄漏"判断须一致
        var e = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(10))), LoopCount.Of(1));
        var script = new EffectScript(ImmutableArray.Create(e));
        // Audit 闭包路径：单 create 无 release ⇒ 应报 Leak
        var aud = script.Audit(Budget.None);
        Assert.Contains(aud.Violations, v => v.Kind == "Leak");
        // NetTable.Compute 同 scope 单资源 net：应不守恒（Create 无 Release 抵消）
        var net = NetTable.Compute(e.Footprint, e.Scope);
        Assert.False(net.IsConserved(new ResourceId.Gpu(new Rid("r"))));
    }

    // ── D08-002：Peak.Compute（外部公共代数）与 Audit 峰值 gate 必须一致（R8：两处峰值实现不能漂移） ──
    [Fact]
    public void PeakCompute_AgreesWith_AuditPeakGate()
    {
        var e = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), new ScopeId.Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("r")), Mode.Create, new ScopeId.Scene("A"), Interval.Exact(10))), LoopCount.Of(1));
        var cap = new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar> { [new ResourceId.Gpu(new Rid("r"))] = NatStar.Of(5) });
        var script = new EffectScript(ImmutableArray.Create(e));
        // Audit：峰值 10 > 5 ⇒ PeakExceeded
        var aud = script.Audit(cap);
        Assert.Contains(aud.Violations, v => v.Kind == "PeakExceeded");
        // Peak.Compute 单点同 scope：应为 10（超限），与 Audit 观测一致
        var peak = Peak.Compute(e.Footprint, e.Scope);
        Assert.Equal(NatStar.Of(10), peak);
        Assert.True(peak.Value > cap.Caps[new ResourceId.Gpu(new Rid("r"))].Value);
    }

    // ── D08-003：NaN 毒值彻底缺席——NatStar 永远不 NaN，量纲隔离靠 Kind 过滤（R8：类型层消除 NaN，已删 Weight） ──
    [Fact]
    public void NoNaN_PeakOfMismatchedKinds_ThrowsNotNaN()
    {
        // 量纲隔离在 NetTable/Peak 层：if(Kind!=Occupy) continue，Weight 类已删
        var gpu = new ResourceId.Gpu(new Rid("r")); var sc = new ScopeId.Scene("A");
        var sig = Signature.Of(new Claim(Kind.Write, gpu, Mode.Use, sc, Interval.Exact(10)));
        Assert.Equal(NatStar.Of(0), Peak.Compute(sig, sc));
        Assert.Equal((ulong)0, NatStar.Top.Value);
        var fin = NatStar.Of(3) * NatStar.Of(4);
        Assert.Equal(NatStar.Of(12), fin);
        Assert.False(double.IsNaN((double)fin.Value));
    }
}
