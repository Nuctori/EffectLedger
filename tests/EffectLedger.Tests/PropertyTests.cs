using EffectLedger;

namespace EffectLedger.Tests;

/// <summary>
/// 迭代07 — L1 性质测试（随机/穷举，无 FsCheck：用 System.Random 确定性种子驱动）。
/// 每个方法对一条代数定律做「随机输入下恒真」的穷举式证明，验证数学边界对所有输入成立。
/// 出处见各方法 §x.y 标注（§3.1.5a / §3.1.5b / §3.1.3b / §3.2.3 / §3.1.4a / §3.1.1）。
/// </summary>
public class PropertyTests
{
    private static readonly Random Rng = new(42); // 确定性种子，可复现

    // ── 1. NatStar 随机 ⊤ 闭包（§3.1.5a） ──────────────────────────────
    [Fact]
    public void NatStar_RandomTopClosure() // §3.1.5a 加/乘 ⊤ 律、交换、结合（1000 组）
    {
        for (int i = 0; i < 1000; i++)
        {
            var a = RandNat();
            var b = RandNat();
            var c = RandNat();

            // 交换：a+b == b+a
            Assert.Equal(a + b, b + a);
            // 结合：(a+b)+c == a+(b+c)
            Assert.Equal((a + b) + c, a + (b + c));
            // 乘 ⊤ 吸收：x*⊤ == ⊤（保守）
            Assert.Equal(NatStar.Top, a * NatStar.Top);
            Assert.Equal(NatStar.Top, NatStar.Top * a);
            // max/min ⊤ 律（双向）
            Assert.Equal(NatStar.Top, a.Max(NatStar.Top));
            Assert.Equal(NatStar.Top, NatStar.Top.Max(a));
            if (!a.IsTop) Assert.Equal(a, a.Min(NatStar.Top)); // §3.1.5a min(x,⊤)=x（receiver 有限）
            Assert.Equal(a, NatStar.Top.Min(a));               // §3.1.5a min(⊤,x)=x（receiver=Top ⇒ 返回 o）
        }
    }

    // ── 2. NatStar CompareToFinite 全序（§3.1.5a） ──────────────────────
    [Fact]
    public void NatStar_RandomTotalOrder() // §3.1.5a compare 符号相反（1000 组）
    {
        for (int i = 0; i < 1000; i++)
        {
            var a = RandNat();
            var b = RandNat();
            int ab = a.CompareToFinite(b);
            int ba = b.CompareToFinite(a);
            // 两 Top ⇒ 0；一 Top ⇒ ±1；否则符号相反或等于 0
            if (a.IsTop && b.IsTop)
            {
                Assert.Equal(0, ab);
            }
            else if (a.IsTop || b.IsTop)
            {
                Assert.Equal(1, Math.Abs(ab));      // ±1
                Assert.Equal(-ab, ba);
            }
            else
            {
                Assert.Equal(-Math.Sign(ab), Math.Sign(ba)); // 符号相反（含 0）
                Assert.Equal(ab, -ba);
            }
        }
    }

    // ── 3. Interval.Merge 随机 幂等/交换/结合（§3.1.5b） ─────────────────
    [Fact]
    public void Interval_RandomMergeLaws() // §3.1.5b join-semilattice（1000 组，避非法 [⊤,x]）
    {
        for (int i = 0; i < 1000; i++)
        {
            var a = RandInterval();
            var b = RandInterval();
            var c = RandInterval();

            // 幂等：m.Merge(m) == m
            Assert.Equal(a, a.Merge(a));
            // 交换：a.Merge(b) == b.Merge(a)
            Assert.Equal(a.Merge(b), b.Merge(a));
            // 结合：(a.Merge(b)).Merge(c) == a.Merge(b.Merge(c))
            Assert.Equal(a.Merge(b).Merge(c), a.Merge(b.Merge(c)));
        }
    }

    // ── 4. ScopeId ⊆* 随机 偏序（§3.1.3b） ─────────────────────────────
    [Fact]
    public void ScopeId_RandomPartialOrder() // §3.1.3b ⊆* 自反、Global 最大元、跨标签不可比（1000 组）
    {
        for (int i = 0; i < 1000; i++)
        {
            var x = RandScope();
            var y = RandScope();

            // 自反：x ⊆ x
            Assert.True(x.IncludedIn(x));
            // Global 最大元：任意 x ⊆ Global
            Assert.True(x.IncludedIn(new ScopeId.Global()));
            // 跨标签不同名不可比较（Method/Type/Scene 间不同名 ⇒ false；同标签同字段已被自反命中）
            var method = new ScopeId.Method(RandName());
            var scene = new ScopeId.Scene(RandName());
            Assert.False(method.IncludedIn(scene));
            Assert.False(scene.IncludedIn(method));
        }
    }

    // ── 5. Compatible 随机 全函数/对称/CONFLICT（§3.2.3） ───────────────
    [Fact]
    public void Compatible_RandomLaws() // §3.2.3 全函数、对称、CONFLICT、Use/Unknown、P3 良性配对（25×25 + 1000 随机）
    {
        // 穷举 25×25 全组合（确定性，无随机）
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var a in modes)
            foreach (var b in modes)
            {
                // 全函数：不抛
                bool r = Compatible.IsCompatible(a, b);
                // 对称：IsCompatible(a,b)==IsCompatible(b,a)
                Assert.Equal(r, Compatible.IsCompatible(b, a));
                // CONFLICT：(C,C)/(M,M)/(R,R) ⇒ false
                if (a == Mode.Create && b == Mode.Create) Assert.False(r);
                if (a == Mode.Move && b == Mode.Move) Assert.False(r);
                if (a == Mode.Release && b == Mode.Release) Assert.False(r);
                // Use 与任意 ⇒ true
                if (a == Mode.Use || b == Mode.Use) Assert.True(r);
                // Unknown 与任意 ⇒ 同 Use（即恒 true，因 Use 已覆盖）
                if (a == Mode.Unknown || b == Mode.Unknown) Assert.True(r);
                // §3.2.3 P3 良性生命周期配对 ⇒ true（OPEN-1 补：对称断言对 false⇔false 仍成立，故须显式结果断言）
                if ((a == Mode.Create && b == Mode.Release) || (a == Mode.Release && b == Mode.Create)) Assert.True(r);
                if ((a == Mode.Create && b == Mode.Move) || (a == Mode.Move && b == Mode.Create)) Assert.True(r);
                if ((a == Mode.Release && b == Mode.Move) || (a == Mode.Move && b == Mode.Release)) Assert.True(r);
            }

        // 1000 随机对
        for (int i = 0; i < 1000; i++)
        {
            var a = RandMode();
            var b = RandMode();
            Assert.Equal(Compatible.IsCompatible(a, b), Compatible.IsCompatible(b, a));
        }
    }

    // ── 6. Normalize 幂等随机（§3.1.4a） ────────────────────────────────
    [Fact]
    public void ResourceId_RandomNormalizeIdempotent() // §3.1.4a Normalize(Normalize(r))==Normalize(r)（1000 组）
    {
        for (int i = 0; i < 1000; i++)
        {
            var r = RandResource();
            var n1 = ResourceId.Normalize(r);
            var n2 = ResourceId.Normalize(n1);
            Assert.Equal(n1, n2);
        }
    }

    // ── 7. Claim.Normalize 一致随机（§3.1.1 / §3.1.4a） ─────────────────
    [Fact]
    public void Claim_RandomNormalizeConsistent() // §3.1.1/§3.1.4a 跨资源归一相等 + size 缺省⇒Default（500 组）
    {
        for (int i = 0; i < 500; i++)
        {
            // Self("signal_"+s) ≡ SignalBus(s)（§3.1.2b / ST-02）
            var s = RandName();
            var cSelf = new Claim(Kind.Occupy, new ResourceId.Self("signal_" + s), Mode.Create, new ScopeId.Global(), Interval.Default);
            var cBus = new Claim(Kind.Occupy, new ResourceId.SignalBus(new StringName(s)), Mode.Create, new ScopeId.Global(), Interval.Default);
            Assert.Equal(cSelf.Normalize(), cBus.Normalize());

            // size 缺省（default 结构）⇒ Normalize 后 ⇒ Interval.Default
            var cDefault = new Claim(Kind.Read, RandResource(), Mode.Use, new ScopeId.Global(), default);
            Assert.Equal(Interval.Default, cDefault.Normalize().Size);
        }
    }

    // ── 生成器（覆盖边界：⊤、0、缺省、跨桶） ────────────────────────────
    private static NatStar RandNat()
    {
        int k = Rng.Next(0, 4);
        return k switch
        {
            0 => NatStar.Top,                          // 上界未知
            1 => NatStar.Of(0),                        // 边界 0
            2 => NatStar.Of(1),                        // 边界 1
            _ => NatStar.Of((ulong)Rng.Next(2, 1000)), // 随机有限
        };
    }

    private static Interval RandInterval()
    {
        // 仅生成合法形式：[x,y]、[x,⊤]、[⊤,⊤]，避免 [⊤,x] 构造异常
        int form = Rng.Next(0, 3);
        if (form == 0) // [x,⊤]
        {
            var lo = (ulong)Rng.Next(0, 100);
            return new Interval(NatStar.Of(lo), NatStar.Top);
        }
        if (form == 1) // [⊤,⊤]
        {
            return new Interval(NatStar.Top, NatStar.Top);
        }
        // [x,y] 且 lo<=hi
        var a = (ulong)Rng.Next(0, 500);
        var b = (ulong)Rng.Next(0, 500);
        var loVal = Math.Min(a, b);
        var hiVal = Math.Max(a, b);
        return new Interval(NatStar.Of(loVal), NatStar.Of(hiVal));
    }

    private static ScopeId RandScope()
    {
        int k = Rng.Next(0, 7);
        return k switch
        {
            0 => new ScopeId.Method(RandName()),
            1 => new ScopeId.Type(RandName()),
            2 => new ScopeId.Scene(RandName()),
            3 => new ScopeId.Global(),
            4 => new ScopeId.Shell(),
            5 => new ScopeId.Loop(RandName()),
            _ => new ScopeId.Conditional(RandName()),
        };
    }

    private static Mode RandMode()
    {
        return (Mode)(Rng.Next(0, 5));
    }

    private static ResourceId RandResource()
    {
        int k = Rng.Next(0, 9);
        return k switch
        {
            0 => new ResourceId.Tree(NodePathOrUnknown.Of(RandName())),
            1 => new ResourceId.Tree(NodePathOrUnknown.Unknown),
            2 => new ResourceId.Self(RandName()),
            3 => new ResourceId.Self("signal_" + RandName()),       // 归一支
            4 => new ResourceId.Signal(new StringName(RandName())),
            5 => new ResourceId.SignalBus(new StringName(RandName())),
            6 => new ResourceId.CommandBuffer("gpu"),
            7 => new ResourceId.Gpu(new Rid(RandName())),
            _ => new ResourceId.Memory((ulong)Rng.Next(0, 100)),
        };
    }

    private static string RandName() => "n" + Rng.Next(0, 1000);
}
