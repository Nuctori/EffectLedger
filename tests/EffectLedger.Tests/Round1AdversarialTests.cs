// Round1AdversarialTests.cs — ROUND 1 对抗审计：L1 纯代数核心真实缺陷（测试驱动）复现。
// 每个 [Fact] 对应一个在审计中发现的真实、可复现缺陷；先红（证明），再由 worker 在 L1 源码修复至绿。
using EffectLedger;

namespace EffectLedger.Tests;

public class Round1AdversarialTests
{
    private static readonly ResourceId _mem = new ResourceId.Memory(1);
    private static readonly ScopeId _scope = new ScopeId.Global();

    // ── DEFECT A：Algebra.cs Negate 端点反置 ───────────────────────────────
    // §3.3.1 注释明确：release 取 size 的「负向」[-hi,-lo]。
    // 但实现用 var lo = -Lo; var hi = -Hi ⇒ SignedInterval(-Lo,-Hi)（端点反置）。
    // 任意非精确 release 区间 [lo,hi]（lo<hi，如 [1,2]）：
    //   -Lo > -Hi ⇒ SignedInterval 构造子抛 ArgumentException（对合法输入崩溃）；
    //   -即使不抛（lo==hi 时），区间也是错的（应为 [-hi,-lo]）。
    [Fact]
    public void A_Negate_NondegenerateRelease_NoCrash_And_CorrectSign()
    {
        // release [1,2] ⇒ 净效应应为 [-2,-1]
        var s = Signature.Of(
            new Claim(Kind.Occupy, _mem, Mode.Release, _scope,
                new Interval(NatStar.Of(1), NatStar.Of(2))));
        var net = NetTable.Compute(s, _scope); // 当前实现在此抛 ArgumentException（崩溃）
        var v = net.Get(_mem);
        Assert.Equal(ZStar.Of(-2), v.Lo); // 应为 -hi
        Assert.Equal(ZStar.Of(-1), v.Hi); // 应为 -lo
    }

    [Fact]
    public void A_Negate_WithCreate_SignedSum_Conserved()
    {
        // create [1,2] + release [1,2] ⇒ 净效应 [1-2, 2-1] = [-1,1]，含 0 ⇒ 守恒
        var create = Signature.Of(new Claim(Kind.Occupy, _mem, Mode.Create, _scope,
            new Interval(NatStar.Of(1), NatStar.Of(2))));
        var release = Signature.Of(new Claim(Kind.Occupy, _mem, Mode.Release, _scope,
            new Interval(NatStar.Of(1), NatStar.Of(2))));
        var s = Signature.Union(create, release);
        var net = NetTable.Compute(s, _scope);
        var v = net.Get(_mem);
        Assert.Equal(ZStar.Of(-1), v.Lo);
        Assert.Equal(ZStar.Of(1), v.Hi);
        Assert.True(net.IsConserved(_mem));
    }

    // ── DEFECT B：ResourceId.Normalize 幂等性（双 "signal_" 前缀） ──────────
    // 注释 + 既有测试锁定「归一化是幂等函数」。但 Self("signal_signal_x")：
    //   一次 ⇒ SignalBus("signal_x")；二次 ⇒ SignalBus("x")（SignalBus 自身再剥前缀）
    //   ⇒ Normalize(Normalize(x)) != Normalize(x)（非不动点）。
    [Fact]
    public void B_Normalize_Idempotent_DoubleSignalPrefix()
    {
        var x = new ResourceId.Self("signal_signal_x");
        var once = ResourceId.Normalize(x);
        var twice = ResourceId.Normalize(once);
        Assert.Equal(once, twice); // 幂等（不动点）
    }


    // ── DEFECT C：Claim.Normalize 将显式零区间 [0,0] 误当缺省 ⇒ 膨胀为 [1,1] ──
    // §3.1.5 Exact(0) ⇔ [0,0] 合法（非负允许 0）；algebra 不应把合法零 size 改写为 [1,1]。
    // 根因：Size == default 中 default(Interval)==[0,0]==Interval.Exact(0)，无法区分「缺省」与「显式 0」。
    [Fact]
    public void C_ClaimNormalize_PreservesExplicitZeroSize()
    {
        var c = new Claim(Kind.Occupy, _mem, Mode.Create, _scope, Interval.Exact(0));
        var n = c.Normalize();
        Assert.Equal(Interval.Exact(0), n.Size); // 显式 [0,0] 必须保留，不得膨胀为 [1,1]
    }

    [Fact]
    public void C_ClaimNormalize_ZeroSize_NotInflatedInNet()
    {
        // 零 size 的 create 对 net 应贡献 0，而非被膨胀成 1（否则误报泄漏）。
        var c = new Claim(Kind.Occupy, _mem, Mode.Create, _scope, Interval.Exact(0));
        var net = NetTable.Compute(Signature.Of(c), _scope);
        var v = net.Get(_mem);
        Assert.Equal(ZStar.Of(0), v.Lo);
        Assert.Equal(ZStar.Of(0), v.Hi);
        Assert.True(net.IsConserved(_mem));
    }
}
