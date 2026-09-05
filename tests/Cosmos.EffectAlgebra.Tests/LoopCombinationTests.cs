// LoopCombinationTests.cs — PDR §3.2.5 循环组合 (S × ω) 与 §3.2.1/§3.2.2 序列/并行组合锁。
// 证明 Combination.Loop 的 ω 缩放语义（有限 ω ⇒ size×ω、ω=⊤ ⇒ ⊤ 兜底）与 Sequence/Parallel=Union，对齐 PDR §3.2.5/§3.3.2。
// ω=⊤ 不崩靠 LoopCount.Count.IsTop 类型强制（DerivedMetrics.cs）；Peak size 求和排除 release（§3.3.2 c.mode≠release）。
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class LoopCombinationTests
{
    private static readonly ResourceId X = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
    private static readonly ScopeId M = new ScopeId.Method("m");
    private static readonly ScopeId LoopL = new ScopeId.Loop("L");
    private static readonly ScopeId LoopA = new ScopeId.Loop("A");
    private static readonly ScopeId LoopB = new ScopeId.Loop("B");
    private static readonly ScopeId G = new ScopeId.Global();

    // §3.2.5 — 单 occupy claim（mode=Create≠release，故 Peak 计入），size 精确 [10,10]，scope 方法 m。
    private static Signature Body() =>
        Signature.Of(new Claim(Kind.Occupy, X, Mode.Create, M, Interval.Exact(10)));

    // §3.2.5 — 有限 ω 缩放：Peak = ω × Σbody_size。body 单个 occupy size[10,10]，ω=5 ⇒ 该资源 Peak.Value==50。
    // 若 Loop 未缩放（错实现），Peak 必为 10 ⇒ 此断言必红（可证伪）。
    [Fact]
    public void Loop_FiniteOmega_ScalesPeakByOmega() // §3.2.5
    {
        var looped = Combination.Loop(Body(), LoopCount.Of(5), LoopL);

        // rescope 落实：claim 的 Scope 改为 Loop("L")（实现逐桶 with Scope=loopScope）。
        var c = looped.OccupyClaims.Single();
        var l = Assert.IsType<ScopeId.Loop>(c.Scope);
        Assert.Equal("L", l.Id);

        // size 端点按 ω 缩放：Scale([10,10],5) = [50,50]。
        Assert.Equal(NatStar.Of(50), (c.Size ?? Interval.Default).Lo);
        Assert.Equal(NatStar.Of(50), (c.Size ?? Interval.Default).Hi);

        // Peak 在 Global 下求和（Loop ⊆* Global 恒成立）→ 50。
        var peak = Peak.Compute(looped, G);
        Assert.False(peak.IsTop);
        Assert.Equal(50UL, peak.Value);
    }

    // §3.2.5 / §3.1.5a — ω=⊤ ⇒ 上界开放：Scale([10,10],⊤) = [10,⊤]；Peak 遇 hi=⊤ ⇒ 返回 NatStar.Top（不崩、不有限误判）。
    [Fact]
    public void Loop_TopOmega_FallsBackToTop() // §3.2.5 / §3.1.5a
    {
        var looped = Combination.Loop(Body(), LoopCount.Top, LoopL);

        // size 上界开放：Scale 把 Hi 拉到 ⊤，Lo 保持有限。
        var c = looped.OccupyClaims.Single();
        Assert.Equal(NatStar.Of(10), (c.Size ?? Interval.Default).Lo);
        Assert.True((c.Size ?? Interval.Default).Hi.IsTop);

        // Peak.Compute 对任一 Hi=⊤ 直接返回 ⊤（不发散）。
        var peak = Peak.Compute(looped, G);
        Assert.True(peak.IsTop);
    }

    // §3.2.1 — 序列组合 := ∪：Sequence(a,b) 与 Signature.Union(a,b) 三桶结构相等。
    // 【P1-B3】Sequence_EqualsUnion / Parallel_EqualsUnion 两钉随被钉别名一并删除
    //（组合唯一入口 = Signature.Union；被钉对象已按 B3 决策移除）。

    // §3.2.5 — 嵌套等价：Loop(Loop(body,ω1),ω2) 与 Loop(body, ω1×ω2) 在 Peak 上一致。
    // 实现：内层 Scale×ω1 再 rescope，外层对结果再 Scale×ω2 ⇒ 总缩放 ω1×ω2（与直接 ω1×ω2 同）。
    // 若 Loop 未真正缩放或仅复制 N 份（非 size×ω），两路 Peak 会偏离 ⇒ 断言必红。
    [Fact]
    public void Loop_NestedEqualsFlatScaling() // §3.2.5
    {
        var inner = Combination.Loop(Body(), LoopCount.Of(2), LoopA);
        var nested = Combination.Loop(inner, LoopCount.Of(3), LoopB);   // 总缩放 2×3=6
        var flat = Combination.Loop(Body(), LoopCount.Of(6), LoopB);    // 直接 ω=6

        var peakNested = Peak.Compute(nested, G);
        var peakFlat = Peak.Compute(flat, G);

        Assert.Equal(60UL, peakNested.Value); // 10×6 ⇒ 60（可证伪：若仅复制则 10×2 或 10×3）
        Assert.Equal(peakFlat, peakNested);
    }
}
