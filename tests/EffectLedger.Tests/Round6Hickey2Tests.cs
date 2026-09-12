// Round6Hickey2Tests.cs — rich-hickey2-round06（scope与组合性）审计发现的 TDD 钉。
// S06-001 双scope真相 / S06-002 Budget Caps归一化 / S06-004 峰值归因陈旧。xUnit。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace EffectLedger.Tests;

public class Round6Hickey2Tests
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ScopeId Global() => new ScopeId.Global();

    // ── S06-001：事件级scope与claim级scope不一致 ⇒ FormatException（单一真相为事件级） ──
    [Fact]
    public void Parse_ClaimScopeMismatchesEventScope_Throws()
    {
        // 事件 scope=A，claim scope=B —— 修复前：静默吞掉错位；At 时重写 scope=Event.Scope，经过往返 scope 再分裂
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"A"},"footprint":[{"kind":"occupy","resource":{"gpu":"r1"},"mode":"create","scope":{"scene":"B"},"size":[1,1]}]}]}""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("scope", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Parse_ClaimScopeMatchesEventScope_Passes()
    {
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"A"},"footprint":[{"kind":"occupy","resource":{"gpu":"r1"},"mode":"create","scope":{"scene":"A"},"size":[1,1]}]}]}""";
        var s = EffectScriptContract.Parse(json); // 一致时可解析
        Assert.Single(s.Events);
        Assert.Equal(Scene("A").ToString(), s.At(NatStar.Of(5)).OccupyClaims.First().Scope.ToString());
    }

    // ── S06-002：Budget caps 同一归一化资源占两条目 ⇒ 合并为一条或抛 ──
    [Fact]
    public void Budget_CapsNormalizedOnConstruction()
    {
        // Self("signal_x") 与 SignalBus(x) 归一为同一键
        var dup = new Dictionary<ResourceId, NatStar>
        {
            [new ResourceId.Self("signal_x")] = NatStar.Of(1),
            [new ResourceId.SignalBus(new StringName("x"))] = NatStar.Of(999),
        };
        var b = new Budget(dup);
        Assert.Single(b.Caps); // 修复前：2 条并存，相等/哈希/ToJson 全分裂
    }

    [Fact]
    public void Budget_SelfAndSignalBus_AreEqualAfterNormalization()
    {
        var b1 = new Budget(new Dictionary<ResourceId, NatStar> { [new ResourceId.Self("signal_foo")] = NatStar.Of(5) });
        var b2 = new Budget(new Dictionary<ResourceId, NatStar> { [new ResourceId.SignalBus(new StringName("foo"))] = NatStar.Of(5) });
        Assert.True(b1.Equals(b2)); // 修复前：False（结构不等，hash 不同）
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
    }

    [Fact]
    public void Budget_ToJson_WorksForSelfNormalized()
    {
        var b = new Budget(new Dictionary<ResourceId, NatStar> { [new ResourceId.Self("signal_foo")] = NatStar.Of(5) });
        var script = new EffectScript(ImmutableArray<EffectEvent>.Empty, b);
        var json = EffectScriptContract.ToJson(script); // 修复前：抛 不可序列化的 budget 键资源
        Assert.Contains("signalBus:foo", json);
    }

    // ── S06-004：峰值归因在首个事件过期后仍指旧 scope（跨事件资源归因） ──
    // 场景：两事件时序不重叠同资源，首事件未超限，次事件才超限+泄漏同在但首个峰值 scope 已陈旧
    [Fact]
    public void PeakExceeded_AfterFirstEventExpired_ReportsCurrentScope()
    {
        // e1[0,5] scope=A 占 Gpu/r 2（不超 cap=5）；e2[10,15] scope=B 同资源 10 ⇒ 在 t=10 超限且闭包泄漏，峰值与泄漏均应归 B
        var e1 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), Scene("A"),
            Signature.Of(new Claim(Kind.Occupy, Gpu("r"), Mode.Create, Scene("A"), Interval.Exact(2))), LoopCount.Of(1));
        var e2 = new EffectEvent(new Interval(NatStar.Of(10), NatStar.Of(15)), Scene("B"),
            Signature.Of(new Claim(Kind.Occupy, Gpu("r"), Mode.Create, Scene("B"), Interval.Exact(10))), LoopCount.Of(1));
        var cap = new Budget(new Dictionary<ResourceId, NatStar> { [Gpu("r")] = NatStar.Of(5) });
        var aud = new EffectScript(ImmutableArray.Create(e1, e2)).Audit(cap);
        Assert.Contains(aud.Violations, v => v.Kind == "PeakExceeded" && v.Scope.ToString().Contains("B"));
        Assert.Contains(aud.Violations, v => v.Kind == "Leak" && v.Scope.ToString().Contains("B"));
    }
}
