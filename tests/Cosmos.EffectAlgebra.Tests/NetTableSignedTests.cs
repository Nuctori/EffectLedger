using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// 迭代05 回归 — §3.3 有符号 net（ZStar/SignedInterval）OPEN-1/2/3 修复验证。
/// 证明 net 真取负、含 0 即守恒、Peak 排除 release。
/// </summary>
public class NetTableSignedTests
{
    private static readonly ResourceId _mem = new ResourceId.Memory(1);
    private static readonly ScopeId _scope = new ScopeId.Global();

    private static Signature Occupy(Mode mode, Interval size) =>
        Signature.Of(new Claim(Kind.Occupy, _mem, mode, _scope, size));

    [Fact]
    public void OPEN1_Create1_Release1_NotNet_Zero_AndConserved() // create[1,1] + release[1,1] ⇒ [-1,1]，含 0 ⇒ 守恒
    {
        var s = Signature.Union(Occupy(Mode.Create, Interval.Exact(1)), Occupy(Mode.Release, Interval.Exact(1)));
        var net = NetTable.Compute(s, _scope);
        var v = net.Get(_mem);
        Assert.Equal(ZStar.Of(-1), v.Lo);   // 真取负
        Assert.Equal(ZStar.Of(1), v.Hi);
        Assert.True(net.IsConserved(_mem));      // 含 0 ⇒ 不误报
    }

    [Fact]
    public void OPEN1_CreateNoRelease_NotConserved() // create[1,1] 无 release ⇒ [1,1]，不守恒（报警）
    {
        var s = Occupy(Mode.Create, Interval.Exact(1));
        var net = NetTable.Compute(s, _scope);
        var v = net.Get(_mem);
        Assert.Equal(ZStar.Of(1), v.Lo);
        Assert.Equal(ZStar.Of(1), v.Hi);
        Assert.False(net.IsConserved(_mem));
    }

    [Fact]
    public void OPEN2_Create12_Release11_ContainsZero_Conserved() // create[1,2] + release[1,1] ⇒ [-1,2]，含 0 ⇒ 守恒
    {
        var s = Signature.Union(
            Occupy(Mode.Create, new Interval(NatStar.Of(1), NatStar.Of(2))),
            Occupy(Mode.Release, Interval.Exact(1)));
        var net = NetTable.Compute(s, _scope);
        var v = net.Get(_mem);
        Assert.Equal(ZStar.Of(-1), v.Lo);
        Assert.Equal(ZStar.Of(2), v.Hi);
        Assert.True(net.IsConserved(_mem)); // 区间含 0 ⇒ 可能闭合
    }

    [Fact]
    public void OPEN2_UnknownResource_NotConserved() // 未出现在 net 中的资源 ⇒ fail-closed 不守恒
    {
        var s = Occupy(Mode.Create, Interval.Exact(1));
        var net = NetTable.Compute(s, _scope);
        Assert.False(net.IsConserved(new ResourceId.Memory(999)));
    }

    [Fact]
    public void OPEN3_Peak_ExcludesRelease() // Peak 不含 release ⇒ release 的 size 不进求和
    {
        var create = Occupy(Mode.Create, Interval.Exact(5));
        var release = Occupy(Mode.Release, Interval.Exact(5));
        var both = Signature.Union(create, release);
        // 仅 create 贡献 ⇒ Peak = 5；若误含 release ⇒ 10
        Assert.Equal(NatStar.Of(5), Peak.Compute(both, _scope));
        Assert.Equal(NatStar.Of(5), Peak.Compute(create, _scope));
    }
}
