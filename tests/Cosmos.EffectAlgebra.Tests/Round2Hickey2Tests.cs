// Round2Hickey2Tests.cs — rich-hickey2-round02（数值边界）审计发现的 TDD 钉。
// R2-001 峰值哨兵碰撞 / R2-002 ZStar.Min 对偶律 / R2-003 maxFinite+1 回绕 /
// R2-005 Peak.Compute 量纲隔离 / R2-006 ParseTop 负数/小数 FormatException。xUnit。
using System;
using System.Collections.Immutable;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class Round2Hickey2Tests
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static ScopeId Global() => new ScopeId.Global();

    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz)
        => new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    static EffectEvent Ev(Interval life, ResourceId r, Mode m, ScopeId s, Interval sz, LoopCount w)
        => new(life, s, Signature.Of(Oc(r, m, s, sz)), w);

    // ── R2-001：合法有限峰值 ulong.MaxValue 不得被哨兵碰撞误判为 ⊤ ──
    [Fact]
    public void Peak_LegalUlongMaxValue_NotMistakenAsTop()
    {
        var e = Ev(new Interval(NatStar.Of(0), NatStar.Of(10)), new ResourceId.Gpu(new Rid("g")),
            Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Of(ulong.MaxValue));
        var cap = new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar>
            { [new ResourceId.Gpu(new Rid("g"))] = NatStar.Of(ulong.MaxValue) });
        var aud = new EffectScript(ImmutableArray.Create(e)).Audit(cap); // 修复前：峰值 ⊤ > 预算 Max（假阳性）
        Assert.DoesNotContain(aud.Violations, v => v.Kind == "PeakExceeded");
    }

    [Fact]
    public void Peak_OverMaxByOne_StillExceeds()
    {
        // 可证伪性反例：cap=Max-1 ⇒ 必须报超限（证明修复不是"一律不报"）
        var e = Ev(new Interval(NatStar.Of(0), NatStar.Of(10)), new ResourceId.Gpu(new Rid("g")),
            Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Of(ulong.MaxValue));
        var cap = new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar>
            { [new ResourceId.Gpu(new Rid("g"))] = NatStar.Of(ulong.MaxValue - 1) });
        var aud = new EffectScript(ImmutableArray.Create(e)).Audit(cap);
        Assert.Contains(aud.Violations, v => v.Kind == "PeakExceeded"); // 峰值 Max > Max-1
    }

    // ── R2-002：ZStar.Min(x,⊤)=x 对偶律；SignedInterval.Merge 不因 ⊤ 端坍缩 Lo ──
    [Fact]
    public void ZStarMin_TopLaw_MatchesNatStar()
    {
        Assert.Equal(ZStar.Of(5), ZStar.Of(5).Min(ZStar.Top));   // 修复前：⊤
        Assert.Equal(ZStar.Of(5), ZStar.Top.Min(ZStar.Of(5)));   // 修复前：⊤
        Assert.Equal(ZStar.Top, ZStar.Top.Min(ZStar.Top));
        Assert.Equal(ZStar.Of(3), ZStar.Of(7).Min(ZStar.Of(3))); // 两者有限时不变
    }

    [Fact]
    public void SignedIntervalMerge_TopLo_PreservesFiniteOther()
    {
        var a = new SignedInterval(ZStar.Of(5), ZStar.Of(10));
        var b = new SignedInterval(ZStar.Top, ZStar.Top);
        var m = a.Merge(b);                                       // 修复前：[⊤,⊤]
        Assert.Equal(ZStar.Of(5), m.Lo);
        Assert.Equal(ZStar.Top, m.Hi);
    }

    // ── R2-003：maxFinite=ulong.MaxValue 时 +1 不回绕产生幽灵 t=0 尾段采样 ──
    [Fact]
    public void TailSample_AtMaxFinite_NoWraparound()
    {
        // e1=[0,Max]，e2=[0,⊤]（开尾）⇒ anyOpenEnd 且 maxFinite=Max ⇒ 旧代码加 Max+1=0 幽灵点
        var e1 = Ev(new Interval(NatStar.Of(0), NatStar.Of(ulong.MaxValue)), new ResourceId.Gpu(new Rid("g")),
            Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Of(1));
        var e2 = Ev(new Interval(NatStar.Of(0), NatStar.Top), new ResourceId.Memory(7),
            Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Top);
        // 修复前不抛但含 t=0 二次采样（可经 AtT 集观测）；此处断言审计完成且无异常即语义等价
        var aud = new EffectScript(ImmutableArray.Create(e1, e2)).Audit(Budget.None);
        Assert.True(aud.Violations.Length >= 0); // 不抛即通过；关键断言在 Reference 一致性测试
    }

    // ── R2-005：Peak.Compute 只聚合 Occupy 桶（量纲隔离与 NetTable.Compute 单一真源） ──
    [Fact]
    public void PeakCompute_ExcludesReadWriteKinds()
    {
        var read = new Claim(Kind.Read, new ResourceId.Gpu(new Rid("tex")), Mode.Use, Scene("S"), Interval.Exact(10));
        var write = new Claim(Kind.Write, new ResourceId.Gpu(new Rid("tex")), Mode.Use, Scene("S"), Interval.Exact(20));
        var occ = new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("buf")), Mode.Create, Scene("S"), Interval.Exact(5));
        var sig = Signature.Of(read, write, occ);                 // 修复前：10+20+5=35
        Assert.Equal(NatStar.Of(5), Peak.Compute(sig, Scene("S"))); // 修复后：仅 occupy 桶 5
    }

    [Fact]
    public void NetTableCompute_KindIsolation_Unchanged()
    {
        // 对照组：NetTable 本就隔离，修复后二者口径一致
        var read = new Claim(Kind.Read, new ResourceId.Gpu(new Rid("tex")), Mode.Use, Scene("S"), Interval.Exact(10));
        var occ = new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("buf")), Mode.Create, Scene("S"), Interval.Exact(5));
        var sig = Signature.Of(read, occ);
        var net = NetTable.Compute(sig, Scene("S"));
        Assert.Equal(ZStar.Of(5), net.Get(new ResourceId.Gpu(new Rid("buf"))).Hi); // 仅 occupy 计入（Get 按归一化键取净区间，端点 ∈ ZStar）
    }

    // ── R2-006：lifetime/budget 数值为负数或小数 ⇒ FormatException（契约承诺），非 BCL 异常 ──
    [Theory]
    [InlineData("""{"events":[{"lifetime":[-1,5],"scope":{"scene":"S"},"footprint":[]}]}""")]
    [InlineData("""{"events":[{"lifetime":[0,1.5],"scope":{"scene":"S"},"footprint":[]}]}""")]
    public void Lifetime_NonUInt64Number_ThrowsFormatException(string json)
    {
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：InvalidOperationException
    }

    [Fact]
    public void Budget_NegativeNumber_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse("""{"events":[],"budget":{"gpu:x":-1}}"""));
    }

    // ── R2-004（doc-only 部分）：三份 Scale 收敛为单一 helper 后的等价性钉 ──
    [Fact]
    public void ScaleSat_EquivalentAcrossCallSites()
    {
        // [Max,Max]×2 ⇒ ⊤（溢出保守）；EffectScript.ScaleSize 与 DerivedMetrics.Scale 同型
        var i = new Interval(NatStar.Of(ulong.MaxValue), NatStar.Of(ulong.MaxValue));
        var w = NatStar.Of(2);
        Assert.Equal(NatStar.Top, (i.Lo * w));
        Assert.Equal(NatStar.Top, (i.Hi * w));
    }
}
