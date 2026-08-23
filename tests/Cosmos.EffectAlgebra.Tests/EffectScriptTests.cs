// EffectScriptTests.cs — EFFECT_SCRIPT.md §2/§3 演进验证（L1 增量，零 Godot）。xUnit。
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class EffectScriptTests
{
    // §3.1.2 — 资源构造器（零 Godot 依赖）。
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ResourceId CmdBuf() => new ResourceId.CommandBuffer("gpu");
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    // §3.1.1/§3.1.4b — 单条 occupy claim 便捷构造（场景作用域）。
    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz = default)
        => new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    // §2.1 — 构造一个「场景内占用某资源」的事件（默认 ω=1）。
    static EffectEvent Ev(Interval life, ResourceId r, Mode m, ScopeId s, Interval sz)
        => new(life, s, Signature.Of(Oc(r, m, s, sz)), LoopCount.Of(1));

    // ════════════ 基础：At(t) 瞬时并集（对照手动 Union） ════════════
    [Fact]
    public void At_OverlappingEvents_UnionsFootprints()
    {
        var a = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b));

        var at = script.At(NatStar.Of(0));
        // 对照：手动 Union 这两个事件在 t=0 的瞬时贡献。
        var manual = Signature.Union(Combination.Loop(a.Footprint, a.Loop, a.Scope),
                                     Combination.Loop(b.Footprint, b.Loop, b.Scope));
        Assert.Equal(manual.OccupyClaims.Count, at.OccupyClaims.Count);
        Assert.Equal(2, at.OccupyClaims.Count);
    }

    [Fact]
    public void At_NonOverlapping_IgnoresDeadEvents()
    {
        var a = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a));
        Assert.Single(script.At(NatStar.Of(0)).OccupyClaims);
        Assert.Empty(script.At(NatStar.Of(10)).OccupyClaims); // 不在 lifetime 内
    }

    // ════════════ 守恒：合法临时占用不误报（修 OPEN-2） ════════════
    [Fact]
    public void Audit_TemporaryOccupancy_Passes()
    {
        // create@t=0, release@t=10：合法 10 帧临时占用。
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var release = Ev(Interval.Exact(10), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(create, release));

        var result = script.Audit(Budget.None);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Audit_CreateOnly_ReportedLeak()
    {
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(create));

        var result = script.Audit(Budget.None);
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("tex"))));
    }

    [Fact]
    public void Audit_ReleaseBeforeCreate_ReportedNegativeDip()
    {
        // release@0 早于 create@10。
        var release = Ev(Interval.Exact(0), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1));
        var create = Ev(Interval.Exact(10), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(release, create));

        var result = script.Audit(Budget.None);
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "NegativeDip");
    }

    // ════════════ 居民层：ω=⊤ 常驻不报泄漏，但 Peak 仍受限（修 OPEN-4） ════════════
    [Fact]
    public void Audit_ResidentLayer_NotFlaggedLeak()
    {
        // ω=⊤ 常驻层：持续占用某资源（Mode.Use，良性常驻，不需 release ⇒ 不报 Leak）。
        var resident = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(Gpu("bg"), Mode.Use, Scene("S"), Interval.Exact(1))), LoopCount.Top);
        var script = new EffectScript(ImmutableArray.Create(resident));

        var result = script.Audit(Budget.None); // 无 peak cap ⇒ 不报 PeakExceeded
        Assert.True(result.Passed);
    }

    [Fact]
    public void Audit_ResidentLayer_PeakExceededStillReported()
    {
        var resident = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(Gpu("bg"), Mode.Use, Scene("S"), Interval.Exact(1))), LoopCount.Top);
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("bg")] = NatStar.Of(1) };
        var script = new EffectScript(ImmutableArray.Create(resident));

        var result = script.Audit(new Budget(cap));
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "PeakExceeded");
    }

    // ════════════ Budget：在 Caps 且超限 ⇒ PeakExceeded；不在 Caps ⇒ 通过（修 OPEN-4b） ════════════
    [Fact]
    public void Audit_ResourceAbsentFromCap_Passes()
    {
        // 自闭合占用（create+release 同刻，生命周期闭合）以隔离 Budget 检查：tex 不在 Caps ⇒ 不报 PeakExceeded。
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(5));
        var release = Ev(Interval.Exact(0), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(5));
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("other")] = NatStar.Of(1) };
        var script = new EffectScript(ImmutableArray.Create(create, release));
        Assert.True(script.Audit(new Budget(cap)).Passed); // tex 不在 Caps ⇒ 不检查（资源被占用，但无 cap 故无 PeakExceeded）
    }

    [Fact]
    public void Audit_ResourceInCap_Exceeded_Reported()
    {
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(5));
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("tex")] = NatStar.Of(1) };
        var script = new EffectScript(ImmutableArray.Create(create));
        var result = script.Audit(new Budget(cap));
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "PeakExceeded");
    }

    // ════════════ Compatible：同资源同 scope 两 create ⇒ 冲突（修 OPEN-3/OPEN-5） ════════════
    [Fact]
    public void Audit_SameResourceTwoCreates_ReportedConflict()
    {
        // 两事件同时在 [0,0] 占用 Gpu("tex") 各 create ⇒ 同组冲突。
        var a = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        // 避免泄漏误报：加一个匹配的 release（闭合生命周期）以隔离冲突检测。
        var rel = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b, rel));
        var result = script.Audit(Budget.None);
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "CompatibleConflict");
    }

    [Fact]
    public void Audit_CrossResource_NoConflict()
    {
        // 自闭合跨资源占用（各自 create+release），无冲突也无泄漏。
        var a1 = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var a2 = Ev(Interval.Exact(0), Gpu("m1"), Mode.Release, Scene("S"), Interval.Exact(1));
        var b1 = Ev(Interval.Exact(0), Gpu("m2"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b2 = Ev(Interval.Exact(0), Gpu("m2"), Mode.Release, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a1, a2, b1, b2));
        Assert.True(script.Audit(Budget.None).Passed);
    }

    [Fact]
    public void Audit_CreateReleaseSameInstant_NoConflict()
    {
        // 同资源同 scope 同刻 create+release ⇒ Compatible 良性配对（§3.2.3 P3）。
        var a = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b));
        Assert.True(script.Audit(Budget.None).Passed);
    }

    // ════════════ 确定性：同脚本同 t ⇒ 同签名（auditA 焦点6 成立） ════════════
    [Fact]
    public void At_Deterministic_AcrossEnumerationOrder()
    {
        var a = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1));
        var s1 = new EffectScript(ImmutableArray.Create(a, b));
        var s2 = new EffectScript(ImmutableArray.Create(b, a));
        Assert.Equal(s1.At(NatStar.Of(0)).OccupyClaims.Count, s2.At(NatStar.Of(0)).OccupyClaims.Count);
    }

    // ════════════ 端点采样 == 全整数密集扫描（§3 完备性） ════════════
    [Fact]
    public void At_EndpointSampling_MatchesDenseScan()
    {
        var a = Ev(new Interval(NatStar.Of(0), NatStar.Of(20)), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(new Interval(NatStar.Of(10), NatStar.Of(40)), Gpu("m2"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b));

        for (ulong t = 0; t <= 40; t++)
        {
            // 对照：手动 Union 在该时刻存活事件的贡献（非同义反复）。
            var acc = Signature.Empty;
            foreach (var e in new[] { a, b })
                if (e.Lifetime.Lo.CompareToFinite(NatStar.Of(t)) <= 0 &&
                    (e.Lifetime.Hi.IsTop || NatStar.Of(t).CompareToFinite(e.Lifetime.Hi) <= 0))
                    acc = Signature.Union(acc, Combination.Loop(e.Footprint, e.Loop, e.Scope));
            Assert.Equal(acc.OccupyClaims.Count, script.At(NatStar.Of(t)).OccupyClaims.Count);
        }
    }

    // 端点集合本身：最大有限 hi 作为闭包点，泄漏在闭包点被捕获。
    [Fact]
    public void Audit_LeakDetectedAtClosure()
    {
        var create = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(create));
        var result = script.Audit(Budget.None);
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak");
    }

    // 部分重叠净效应：create[+5] 与 release[−3] 真实净和 = [+2,+2]（不含 0）⇒ 必须报 Leak。
    // 审计器若用 Merge(min/max join) 聚合 net，会得 [−3,+5]（含 0），吞掉泄漏（false-negative，HIGH）。
    // 正确聚合必须用 SignedInterval.Add（区间逐端求和，见 SignedNet.Add）。
    [Fact]
    public void Audit_PartialOverlapNet_ReportedLeak()
    {
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(5));
        var release = Ev(new Interval(NatStar.Of(10), NatStar.Of(100)), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(3));
        var script = new EffectScript(ImmutableArray.Create(create, release));
        var result = script.Audit(Budget.None);
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("tex"))));
    }

    // ════════════ OPEN-N1 回归：residentExempt 顺序无关（两遍法修正） ════════════
    [Fact]
    public void Audit_ResidentExempt_OrderIndependent()
    {
        // 资源 Gpu("x") 同时有：A(ω=1 create, 无 release ⇒ 应报 Leak) + B(ω=⊤ create, 无 release ⇒ 常驻豁免)。
        // 正确：A 的有限 create 永不释放 ⇒ 报 Leak(Gpu x)，无论 A/B 排列顺序。
        var finiteLeak = Ev(Interval.Exact(0), Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1));
        var resident = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Top);

        var sForward = new EffectScript(ImmutableArray.Create(finiteLeak, resident)); // A 先 B 后
        var sReverse = new EffectScript(ImmutableArray.Create(resident, finiteLeak));   // B 先 A 后

        var rForward = sForward.Audit(Budget.None);
        var rReverse = sReverse.Audit(Budget.None);
        // 顺序无关：两序均须报 Leak（false-negative 已修）。
        Assert.False(rForward.Passed);
        Assert.False(rReverse.Passed);
        Assert.Contains(rForward.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("x"))));
        Assert.Contains(rReverse.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("x"))));
    }
}
