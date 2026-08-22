using System;
using System.Collections.Generic;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// §3.1.5a / §3.1.5b — Interval 算术完整性锁。
/// Merge 是 join-semilattice（min(Lo)/max(Hi)），⊤ 经 NatStar 内嵌律吸收；
/// 构造子 lo=⊤ 且 hi 有限 → 类型/构造子强制拒绝（§3.1.5a）。
/// 所有断言可证伪：若 Merge 的 ⊤ 律实现错误（如把 [1,⊤] 算成 [1,0]），对应测试必抛或结果不相等。
/// </summary>
public class IntervalArithmeticTests
{
    // ===== 1. Exact/Dynamic/Default 边界（§3.1.5） =====

    [Fact]
    public void Exact_SingleValue() // §3.1.5 Exact(s) ⇔ [s,s]
    {
        var v = Interval.Exact(5);
        Assert.Equal(NatStar.Of(5), v.Lo);
        Assert.Equal(NatStar.Of(5), v.Hi);
        Assert.False(v.Lo.IsTop);
        Assert.False(v.Hi.IsTop);
    }

    [Fact]
    public void Dynamic_UpperUnknown() // §3.1.5(c) 动态 ⇔ [1,⊤]
    {
        Assert.Equal(NatStar.Of(1), Interval.Dynamic.Lo);
        Assert.True(Interval.Dynamic.Hi.IsTop);
        Assert.Equal(NatStar.Top, Interval.Dynamic.Hi);
    }

    [Fact]
    public void Default_Conservative() // §3.1.5(a) 缺省 ⇔ [1,1]
    {
        Assert.Equal(NatStar.Of(1), Interval.Default.Lo);
        Assert.Equal(NatStar.Of(1), Interval.Default.Hi);
        Assert.False(Interval.Default.Lo.IsTop);
        Assert.False(Interval.Default.Hi.IsTop);
    }

    [Fact]
    public void Exact_Zero_Allowed() // §3.1.5 Exact(0) ⇔ [0,0]，非负允许 0，不崩
    {
        var v = Interval.Exact(0);
        Assert.Equal(NatStar.Of(0), v.Lo);
        Assert.Equal(NatStar.Of(0), v.Hi);
    }

    // ===== 2. Merge 全 ⊤ 组合（§3.1.5b join-semilattice） =====

    [Fact]
    public void Merge_OrdinaryJoin() // §3.1.5b min/max 常规 join
    {
        var a = new Interval(NatStar.Of(1), NatStar.Of(3));
        var b = new Interval(NatStar.Of(2), NatStar.Of(5));
        Assert.Equal(new Interval(NatStar.Of(1), NatStar.Of(5)), a.Merge(b));
    }

    [Fact]
    public void Merge_TopAbsorbsInMax() // §3.1.5b：max 吸收 ⊤ ⇒ [1,⊤]
    {
        var r = new Interval(NatStar.Of(1), NatStar.Top).Merge(new Interval(NatStar.Of(2), NatStar.Top));
        Assert.Equal(new Interval(NatStar.Of(1), NatStar.Top), r);
    }

    [Fact]
    public void Merge_TopHiWithFinite() // §3.1.5b：[1,3] Merge [1,⊤] == [1,⊤]
    {
        var r = new Interval(NatStar.Of(1), NatStar.Of(3)).Merge(new Interval(NatStar.Of(1), NatStar.Top));
        Assert.Equal(new Interval(NatStar.Of(1), NatStar.Top), r);
    }

    [Fact]
    public void Merge_FullyUnknownNarrowsLowerBound() // §3.1.5b：join-semilattice，Max 吸收 ⊤(top)、Min 对 ⊤ 取 meet(min(⊤,x)=x)
    {
        // [⊤,⊤]（完全未知）join [1,5] ⇒ 下界收窄为已知 1，上界仍未知 ⇒ [1,⊤]
        // 非 [⊤,⊤]：Max 把 ⊤ 当 top 吸收，Min 把 ⊤ 当 top 的 meet（⊤ 是上界），故 Lo=min(⊤,1)=1。
        var r = new Interval(NatStar.Top, NatStar.Top).Merge(new Interval(NatStar.Of(1), NatStar.Of(5)));
        Assert.Equal(new Interval(NatStar.Of(1), NatStar.Top), r);
    }

    [Fact]
    public void Merge_Idempotent() // §3.1.5b join-semilattice 幂等
    {
        var a = Interval.Exact(7);
        Assert.Equal(a, a.Merge(a));
    }

    [Fact]
    public void Merge_Commutative() // §3.1.5b join-semilattice 交换
    {
        var a = new Interval(NatStar.Of(1), NatStar.Of(3));
        var b = new Interval(NatStar.Of(2), NatStar.Of(5));
        Assert.Equal(a.Merge(b), b.Merge(a));
    }

    // ===== 3. Merge 非法构造（§3.1.5a） =====

    [Fact]
    public void Ctor_LoTopHiFinite_Throws() // §3.1.5a：lo=⊤ 且 hi 有限 → 类型层拒绝
    {
        Assert.Throws<ArgumentException>(() => new Interval(NatStar.Top, NatStar.Of(5)));
    }

    // ===== 4. Lo/Hi 不变量（§3.1.5） =====

    [Fact]
    public void LoLeHi_Invariant() // §3.1.5：有限区间恒 Lo <= Hi
    {
        var v = new Interval(NatStar.Of(2), NatStar.Of(9));
        Assert.True(v.Lo.Value <= v.Hi.Value);
    }

    [Fact]
    public void IsTop_WhenEitherBoundTop() // §3.1.5：Lo 或 Hi 为 ⊤ ⇒ IsTop
    {
        Assert.True(Interval.Dynamic.Hi.IsTop);
        Assert.True(new Interval(NatStar.Top, NatStar.Top).Lo.IsTop);
    }

    // ===== 5. 随机互补（Random(27)，§3.1.5a/§3.1.5b） =====

    [Fact]
    public void Merge_RandomLaws() // §3.1.5b 幂等+交换+Lo<=Hi 保持，1000 组
    {
        var rng = new Random(27);
        for (int i = 0; i < 1000; i++)
        {
            var a = RandInterval(rng);
            var b = RandInterval(rng);

            // 幂等
            Assert.Equal(a, a.Merge(a));
            // 交换
            Assert.Equal(a.Merge(b), b.Merge(a));
            // 结合
            var c = RandInterval(rng);
            Assert.Equal(a.Merge(b).Merge(c), a.Merge(b.Merge(c)));
            // Lo<=Hi 保持（Merge 不破坏不变量）
            var m = a.Merge(b);
            if (!m.Lo.IsTop && !m.Hi.IsTop)
                Assert.True(m.Lo.Value <= m.Hi.Value);
        }
    }

    private static Interval RandInterval(Random rng)
    {
        int form = rng.Next(3); // 0:[x,⊤] 1:[⊤,⊤] 2:[x,y]
        switch (form)
        {
            case 0:
                return new Interval(NatStar.Of((ulong)rng.Next(0, 100)), NatStar.Top);
            case 1:
                return new Interval(NatStar.Top, NatStar.Top);
            default:
                ulong x = (ulong)rng.Next(0, 100);
                ulong y = (ulong)rng.Next((int)x, 200); // 保证 x<=y
                return new Interval(NatStar.Of(x), NatStar.Of(y));
        }
    }
}
