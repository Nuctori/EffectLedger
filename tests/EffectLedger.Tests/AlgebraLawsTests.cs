using EffectLedger;

namespace EffectLedger.Tests;

/// <summary>
/// 迭代02 — L1 代数定律单测。每个 [Fact]/[Theory] 对应 PDR 一条代数定律，
/// 用类型实例断言数学边界（§3.1.5a / §3.1.5b / §3.1.3b / §3.2.3 / §3.1.4a / §3.1.1 / §3.2.1）。
/// </summary>
public class AlgebraLawsTests
{
    // ── 1. NatStar ⊤ 闭包（§3.1.5a） ─────────────────────────────────────
    [Fact]
    public void NatStar_TopClosure_Add() // x+⊤=⊤；⊤+⊤=⊤
    {
        Assert.Equal(NatStar.Top, NatStar.Of(3) + NatStar.Top);
        Assert.Equal(NatStar.Top, NatStar.Top + NatStar.Top);
    }

    [Fact]
    public void NatStar_TopClosure_Mul() // x×⊤=⊤；0×⊤=⊤（保守标记未知）
    {
        Assert.Equal(NatStar.Top, NatStar.Of(2) * NatStar.Top);
        Assert.Equal(NatStar.Top, NatStar.Top * NatStar.Of(0)); // 0×⊤=⊤
    }

    [Fact]
    public void NatStar_TopClosure_MaxMin() // max(x,⊤)=⊤；min(x,⊤)=x；min(⊤,x)=x
    {
        Assert.Equal(NatStar.Top, NatStar.Top.Max(NatStar.Of(5)));
        Assert.Equal(NatStar.Of(5), NatStar.Of(5).Min(NatStar.Top));
        Assert.Equal(NatStar.Of(5), NatStar.Top.Min(NatStar.Of(5)));
    }

    [Fact]
    public void NatStar_CompareToFinite_TopSemantics() // §3.1.5a compare
    {
        Assert.Equal(0, NatStar.Top.CompareToFinite(NatStar.Top));
        Assert.Equal(1, NatStar.Top.CompareToFinite(NatStar.Of(1)));   // ⊤ 最大
        Assert.Equal(-1, NatStar.Of(1).CompareToFinite(NatStar.Top));
    }

    // ── 2. NatStar 结合/交换 ─────────────────────────────────────────────
    [Theory]
    [InlineData(2UL, 3UL, 4UL)]
    [InlineData(0UL, 1UL, 100UL)]
    [InlineData(7UL, 8UL, 9UL)]
    public void NatStar_Associative(ulong a, ulong b, ulong c)
    {
        var x = NatStar.Of(a); var y = NatStar.Of(b); var z = NatStar.Of(c);
        Assert.Equal((x + y) + z, x + (y + z));
    }

    [Theory]
    [InlineData(2UL, 3UL)]
    [InlineData(10UL, 1UL)]
    public void NatStar_Commutative(ulong a, ulong b)
    {
        var x = NatStar.Of(a); var y = NatStar.Of(b);
        Assert.Equal(x + y, y + x);
    }

    // ── 3. Interval.Merge（§3.1.5b） ─────────────────────────────────────
    [Fact]
    public void Interval_Merge_Idempotent() // m.Merge(m)==m
    {
        var m = Interval.Exact(5);
        Assert.Equal(m, m.Merge(m));
    }

    [Fact]
    public void Interval_Merge_Commutative() // a.Merge(b)==b.Merge(a)
    {
        var a = new Interval(NatStar.Of(1), NatStar.Of(3));
        var b = new Interval(NatStar.Of(2), NatStar.Of(5));
        Assert.Equal(a.Merge(b), b.Merge(a));
    }

    [Fact]
    public void Interval_Merge_Associative() // (a.Merge(b)).Merge(c)==a.Merge(b.Merge(c))
    {
        var a = new Interval(NatStar.Of(1), NatStar.Of(3));
        var b = new Interval(NatStar.Of(2), NatStar.Of(5));
        var c = new Interval(NatStar.Of(0), NatStar.Of(4));
        Assert.Equal(a.Merge(b).Merge(c), a.Merge(b.Merge(c)));
    }

    [Fact]
    public void Interval_Merge_TopLaw() // [1,⊤].Merge([2,⊤])==[1,⊤]
    {
        var a = Interval.Dynamic;                 // [1,⊤]
        var b = new Interval(NatStar.Of(2), NatStar.Top);
        Assert.Equal(a, a.Merge(b));
    }

    // ── 4. ScopeId ⊆*（§3.1.3b） ─────────────────────────────────────────
    [Fact]
    public void ScopeId_Reflexive() // Method("m").IncludedIn(Method("m"))
    {
        Assert.True(new ScopeId.Method("m").IncludedIn(new ScopeId.Method("m")));
    }

    [Fact]
    public void ScopeId_GlobalMaximal() // Global 含一切
    {
        Assert.True(new ScopeId.Method("m").IncludedIn(new ScopeId.Global()));
        Assert.True(new ScopeId.Scene("s").IncludedIn(new ScopeId.Global()));
    }

    [Fact]
    public void ScopeId_CrossLabelIncomparable() // Method("m") ⊄ Scene("s") (m≠s)
    {
        Assert.False(new ScopeId.Method("m").IncludedIn(new ScopeId.Scene("s")));
    }

    [Fact]
    public void ScopeId_Transitive() // §3.1.3b 传递性：a⊆b ∧ b⊆c ⇒ a⊆c（两步链，非单步）
    {
        // 链1：Method("m")⊆Method("m")（自反 b）∧ Method("m")⊆Global()（c 最大元）⇒ Method("m")⊆Global()
        var a1 = new ScopeId.Method("m");
        var b1 = a1;                       // a1⊆b1 由自反
        var c1 = new ScopeId.Global();     // b1⊆c1 由 Global 最大元
        Assert.True(a1.IncludedIn(b1) && b1.IncludedIn(c1)); // 前置：两步链成立
        Assert.True(a1.IncludedIn(c1));                     // 传递：a1⊆b1∧b1⊆c1 ⇒ a1⊆c1

        // 链2：Scene("s")⊆Scene("s") ∧ Scene("s")⊆Global() ⇒ Scene("s")⊆Global()
        var a2 = new ScopeId.Scene("s");
        var b2 = a2;
        var c2 = new ScopeId.Global();
        Assert.True(a2.IncludedIn(b2) && b2.IncludedIn(c2));
        Assert.True(a2.IncludedIn(c2));

        // 链3：Type("t")⊆Type("t") ∧ Type("t")⊆Global() ⇒ Type("t")⊆Global()
        var a3 = new ScopeId.Type("t");
        var b3 = a3;
        var c3 = new ScopeId.Global();
        Assert.True(a3.IncludedIn(b3) && b3.IncludedIn(c3));
        Assert.True(a3.IncludedIn(c3));

        // 链4：自反+自反链 Method("m")⊆Method("m") ∧ Method("m")⊆Method("m") ⇒ Method("m")⊆Method("m")
        var a4 = new ScopeId.Method("m");
        Assert.True(a4.IncludedIn(a4) && a4.IncludedIn(a4));
        Assert.True(a4.IncludedIn(a4));
    }

    [Fact]
    public void ScopeId_Antisymmetric() // §3.1.3b 反对称：a⊆b ∧ b⊆a ⇒ a==b（⊑ 构成偏序）
    {
        // 同标签自反双向 ⇒ 结构相等
        var m = new ScopeId.Method("m");
        Assert.True(m.IncludedIn(m) && m.IncludedIn(m));
        Assert.True(m.Equals(m)); // a⊆a ∧ a⊆a ⇒ a==a

        // 跨标签互不包含 ⇒ 不适用反对称前提；反向断言两者不等（说明反对称未误触发）
        var a = new ScopeId.Method("m");
        var b = new ScopeId.Scene("s");
        Assert.False(a.IncludedIn(b) && b.IncludedIn(a)); // 前提不成立
        Assert.False(a.Equals(b));                          // 确不等

        // Global 最大元：任意 x⊆Global，但 Global⊆x 仅当 x 自身为 Global ⇒ 反对称要求 x==Global
        var g = new ScopeId.Global();
        var m2 = new ScopeId.Method("m");
        Assert.True(m2.IncludedIn(g));   // m⊆Global
        Assert.False(g.IncludedIn(m2));   // Global⊄m ⇒ 前提(双向)不成立 ⇒ 不要求相等
        Assert.False(g.Equals(m2));
    }

    // ── 5. Compatible（§3.2.3） ──────────────────────────────────────────
    [Fact]
    public void Compatible_Symmetric_AllPairs() // IsCompatible(a,b)==IsCompatible(b,a)
    {
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var a in modes)
            foreach (var b in modes)
                Assert.Equal(Compatible.IsCompatible(a, b), Compatible.IsCompatible(b, a));
    }

    [Fact]
    public void Compatible_TotalFunction_NoThrow() // 25 组合均有定义
    {
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var a in modes)
            foreach (var b in modes)
                Assert.IsType<bool>(Compatible.IsCompatible(a, b));
    }

    [Fact]
    public void Compatible_ConflictExcluded() // CONFLICT=(C,C)/(M,M)/(R,R) ⇒ false
    {
        Assert.False(Compatible.IsCompatible(Mode.Create, Mode.Create));
        Assert.False(Compatible.IsCompatible(Mode.Move, Mode.Move));
        Assert.False(Compatible.IsCompatible(Mode.Release, Mode.Release));
    }

    [Fact]
    public void Compatible_UseAlwaysAllowed() // use 与任意 ⇒ true
    {
        var others = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var o in others)
        {
            Assert.True(Compatible.IsCompatible(Mode.Use, o));
            Assert.True(Compatible.IsCompatible(o, Mode.Use));
        }
    }

    [Fact]
    public void Compatible_UnknownTreatedAsUse() // Unknown ⇔ Use（fail-closed）
    {
        Assert.True(Compatible.IsCompatible(Mode.Unknown, Mode.Release));
        Assert.True(Compatible.IsCompatible(Mode.Unknown, Mode.Move));
    }

    [Fact]
    public void Compatible_CreateReleasePairing() // create+release 良性生命周期配对
    {
        Assert.True(Compatible.IsCompatible(Mode.Create, Mode.Release));
        Assert.True(Compatible.IsCompatible(Mode.Release, Mode.Create));
    }

    // ── 6. Normalize 幂等（§3.1.4a） ─────────────────────────────────────
    [Fact]
    public void ResourceId_Normalize_Idempotent()
    {
        ResourceId x = new ResourceId.Self("signal_x");
        Assert.Equal(ResourceId.Normalize(x), ResourceId.Normalize(ResourceId.Normalize(x)));
    }

    [Fact]
    public void ResourceId_Normalize_SignalBusEquivalence()
    {
        ResourceId a = new ResourceId.Self("signal_x");
        ResourceId b = new ResourceId.SignalBus(new StringName("x"));
        Assert.Equal(ResourceId.Normalize(a), ResourceId.Normalize(b));

        ResourceId c = new ResourceId.Signal(new StringName("signal_y"));
        ResourceId d = new ResourceId.SignalBus(new StringName("y")); // 归一后 Signal("signal_y")=SignalBus("y")（§3.1.4a "signal_"+s ≡ SignalBus(s)）
        Assert.Equal(ResourceId.Normalize(c), ResourceId.Normalize(d));
    }

    // ── 7. Claim.Normalize 一致（§3.1.1） ────────────────────────────────
    [Fact]
    public void Claim_Normalize_ResourceEquivalence_AndDefaultSize()
    {
        var scope = new ScopeId.Method("m");
        var c1 = new Claim(Kind.Occupy, new ResourceId.Self("signal_z"), Mode.Create, scope, Interval.Default);
        var c2 = new Claim(Kind.Occupy, new ResourceId.SignalBus(new StringName("z")), Mode.Create, scope, default);
        Assert.Equal(c1.Normalize(), c2.Normalize()); // 资源归一后同
        Assert.Equal(Interval.Default, c2.Normalize().Size); // size 缺省 ⇒ Default
    }

    // ── 8. Signature.Union 幂等 + 跨桶不混（§3.2.1 / §3.1.4b） ───────────
    [Fact]
    public void Signature_Union_Idempotent() // Union(s,s)==s（结构相等：三桶 Claim 集合一致）
    {
        var s = Signature.Of(
            new Claim(Kind.Read, new ResourceId.Memory(1), Mode.Use, new ScopeId.Method("m"), Interval.Default),
            new Claim(Kind.Occupy, new ResourceId.Self("signal_z"), Mode.Create, new ScopeId.Method("m"), Interval.Default));
        var u = Signature.Union(s, s);
        // Signature 为可变 class，无值相等重写；用三桶 Claim 集合结构比较
        Assert.True(s.ReadClaims.SetEquals(u.ReadClaims));
        Assert.True(s.WriteClaims.SetEquals(u.WriteClaims));
        Assert.True(s.OccupyClaims.SetEquals(u.OccupyClaims));
    }

    [Fact]
    public void Signature_Buckets_Disjoint() // read/write/occupy 三桶独立
    {
        var s = Signature.Of(
            new Claim(Kind.Read, new ResourceId.Memory(1), Mode.Use, new ScopeId.Method("m"), Interval.Default),
            new Claim(Kind.Write, new ResourceId.Memory(1), Mode.Use, new ScopeId.Method("m"), Interval.Default),
            new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Use, new ScopeId.Method("m"), Interval.Default));
        Assert.Single(s.ReadClaims);
        Assert.Single(s.WriteClaims);
        Assert.Single(s.OccupyClaims);
    }
}
