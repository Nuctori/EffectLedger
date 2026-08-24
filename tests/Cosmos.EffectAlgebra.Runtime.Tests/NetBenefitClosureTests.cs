// NetBenefitClosureTests.cs — §5 net 收益闭合接线 TDD（reviewer spec D#10）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class NetBenefitClosureTests
{
    static Fiber FiberWithSignature(Signature sig, ScopeId scope)
        => new(new FiberId("f"), sig,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), scope),
            ImmutableStack<InverseClaim>.Empty);

    [Fact]
    public void Check_Conserved_WhenCreateBalancesRelease()
    {
        var scope = new ScopeId.Shell();
        var r = new ResourceId.Memory(0);
        var sig = Signature.Of(
            new Claim(Kind.Occupy, r, Mode.Create, scope, Interval.Exact(1)),
            new Claim(Kind.Occupy, r, Mode.Release, scope, Interval.Exact(1))); // create+release 平衡 ⇒ 含 0
        var f = FiberWithSignature(sig, scope);
        var res = NetBenefitClosure.Check(f);
        Assert.True(res.Conserved);
        Assert.Empty(res.ViolatingResources);
    }

    [Fact]
    public void Check_NotConserved_WhenReleaseMissing()
    {
        var scope = new ScopeId.Shell();
        var r = new ResourceId.Memory(0);
        var sig = Signature.Of(
            new Claim(Kind.Occupy, r, Mode.Create, scope, Interval.Exact(1))); // 只有 create，无 release ⇒ net>0 ⇒ 不闭合
        var f = FiberWithSignature(sig, scope);
        var res = NetBenefitClosure.Check(f);
        Assert.False(res.Conserved);
        Assert.Contains(r, res.ViolatingResources);
    }

    [Fact]
    public void Check_ScopeOnlyGroups_NoCrossFiberSum()
    {
        // 两个 Fiber 各自 Scope 内平衡；核验器 per-Fiber，绝不跨 Fiber 求和（R4-7）。
        var scope = new ScopeId.Shell();
        var ra = new ResourceId.Memory(1);
        var rb = new ResourceId.Memory(2);
        // Provides == 各 Fiber 创建的资源（EffectiveSignature 折入 create(Provides, null)）；Effect 仅放 release(null)（与 Provides create 同尺度 ⇒ 净含 0 ⇒ 真正闭合）。
        var fa = new Fiber(new FiberId("fa"),
            Signature.Of(new Claim(Kind.Occupy, ra, Mode.Release, scope, null)),
            new Coeffect(ra, ra, scope), ImmutableStack<InverseClaim>.Empty);
        var fb = new Fiber(new FiberId("fb"),
            Signature.Of(new Claim(Kind.Occupy, rb, Mode.Release, scope, null)),
            new Coeffect(rb, rb, scope), ImmutableStack<InverseClaim>.Empty);
        var res = NetBenefitClosure.CheckAll(new[] { fa, fb });
        Assert.True(res.Conserved); // 各自闭合，未汇总
    }

    [Fact]
    public void Check_ScopeFiltersClaims_OutsideScopeIgnored()
    {
        var shell = new ScopeId.Shell();
        var other = new ScopeId.Scene("battle");
        var r = new ResourceId.Memory(0);
        // create 在 shell、release 在 scene ⇒ 不同 scope ⇒ shell 内 net 仅含 create ⇒ 不闭合（scope 仅分组）
        var sig = Signature.Of(
            new Claim(Kind.Occupy, r, Mode.Create, shell, Interval.Exact(1)),
            new Claim(Kind.Occupy, r, Mode.Release, other, Interval.Exact(1)));
        var f = FiberWithSignature(sig, shell);
        var res = NetBenefitClosure.Check(f);
        Assert.False(res.Conserved); // shell 内无匹配的 release ⇒ 不闭合
    }
}
