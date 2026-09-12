// HickeyX3FixTests.cs — hickey-x3 P0 修复的可证伪回归测试（P0-3/4/5 + R7-N5 相关 L1 面）。
// 每条断言对应审计实证过的错误行为：修复前必红，修复后绿。
using System;
using System.Linq;
using EffectLedger;
using Xunit;

namespace EffectLedger.Tests;

public class HickeyX3FixTests
{
    private static readonly ScopeId G = new ScopeId.Global();

    // ── P0-4：Signature.Of 重复 Claim 显式报错（并发须走 LoopCount）──
    [Fact]
    public void Of_ExactDuplicateClaim_Throws()
    {
        var c = new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Create, G, Interval.Exact(1));
        var ex = Assert.Throws<ArgumentException>(() => Signature.Of(c, c));
        Assert.Contains("LoopCount", ex.Message);
    }

    [Fact]
    public void Of_DistinctSizes_Ok()
    {
        // 同键不同 size 非重复（ScaleGuardTests 的规模构造依赖此形态）
        var r = new ResourceId.Memory(2);
        var sig = Signature.Of(
            new Claim(Kind.Occupy, r, Mode.Create, G, Interval.Exact(1)),
            new Claim(Kind.Occupy, r, Mode.Create, G, Interval.Exact(2)));
        Assert.Equal(2, SignatureExtensions.AllClaims(sig).Count());
    }

    [Fact]
    public void Of_NormalizationCollision_Throws()
    {
        // 归一化后相等（Signal("signal_bus") ≡ SignalBus("bus")，§3.1.4a）也算重复
        var a = new Claim(Kind.Write, new ResourceId.SignalBus(new StringName("bus")), Mode.Use, G, null);
        var b = new Claim(Kind.Write, new ResourceId.Signal(new StringName("signal_bus")), Mode.Use, G, null);
        Assert.True(a.Normalize().Resource.Equals(b.Normalize().Resource), "前置：两条经归一化必须同键");
        Assert.Throws<ArgumentException>(() => Signature.Of(a, b));
    }

    // ── P0-5：EffectEvent 构造拒绝 Lo=⊤ 寿命（封死幽灵事件假绿通道）──
    [Fact]
    public void EffectEvent_TopLowerLifetime_Throws()
    {
        var fp = Signature.Of(new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Create, G, Interval.Default));
        var topTop = new Interval(NatStar.Top, NatStar.Top);
        Assert.Throws<ArgumentException>(() => new EffectEvent(topTop, G, fp, LoopCount.Of(1)));
    }

    [Fact]
    public void EffectEvent_OpenUpperLifetime_Ok()
    {
        var fp = Signature.Of(new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Use, G, Interval.Default));
        var openEnd = new Interval(NatStar.Of(0), NatStar.Top); // [1,⊤] 合法（常驻层）
        var ev = new EffectEvent(openEnd, G, fp);
        Assert.True(ev.Lifetime.Hi.IsTop);
    }
}
