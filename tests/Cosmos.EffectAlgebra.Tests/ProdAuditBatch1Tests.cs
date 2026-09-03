// ProdAuditBatch1Tests.cs — 独立生产就绪审计（2026-09）批 1 回归钉：L1 DSL 正确性。
// 每条对应审计发现编号（A1-xx）：先红后绿（TDD），防漂移。
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class ProdAuditBatch1Tests
{
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ScopeId.Scene Scene(string s) => new ScopeId.Scene(s);
    static ScopeId.Global Global() => new ScopeId.Global();

    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz) =>
        new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    static EffectEvent Ev(Interval life, ResourceId r, Mode m, ScopeId s, Interval sz, LoopCount? loop = null) =>
        new EffectEvent(life, s, Signature.Of(Oc(r, m, s, sz)), loop ?? LoopCount.Of(1));

    // ── A1-01：[⊤,⊤] size（未知区间）经 ToJson→Parse 往返不得崩 ──
    // Interval 构造子明文允许 [⊤,⊤]（Numeric.cs「未知区间，合法」）；Parse 曾把 lifetime 的
    // 「lo 不可 ⊤」规则错套到 size 上，使 C# 侧合法构造的 claim 导出后无法读回。
    [Fact]
    public void Contract_RoundTrip_TopTopSize_DoesNotThrow()
    {
        var ev = new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)),
            Scene("S"),
            Signature.Of(Oc(Gpu("u"), Mode.Create, Scene("S"), new Interval(NatStar.Top, NatStar.Top))),
            LoopCount.Of(1));
        var json = EffectScriptContract.ToJson(new EffectScript(ImmutableArray.Create(ev)));
        var back = EffectScriptContract.Parse(json);
        var size = back.Events[0].Footprint.OccupyClaims.Single().Size ?? Interval.Default;
        Assert.True(size.Lo.IsTop && size.Hi.IsTop, $"[⊤,⊤] size 往返后应为未知区间，实际 {size}");
    }

    [Fact]
    public void Contract_Parse_SizeTopWithFiniteHi_StillRejected()
    {
        // [⊤,x]（x 有限）对 size 与 Interval 构造子同界：仍非法（下界不可 ⊤ 而上界有限）。
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"gpu":"u"},"mode":"create","scope":{"scene":"S"},"size":["⊤",5]}]}]}""";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }

    // ── A1-02①：构造期拒绝 null Footprint / null Scope（record struct with/default 后门的构造侧封堵） ──
    [Fact]
    public void EffectEvent_Ctor_NullFootprint_ThrowsArgumentNull()
        => Assert.Throws<ArgumentNullException>(
            () => new EffectEvent(Interval.Exact(0), Scene("S"), null!, LoopCount.Of(1)));

    [Fact]
    public void EffectEvent_Ctor_NullScope_ThrowsArgumentNull()
        => Assert.Throws<ArgumentNullException>(
            () => new EffectEvent(Interval.Exact(0), null!, Signature.Empty, LoopCount.Of(1)));

    // ── A1-02②：default(EffectEvent) 混入剧本 ⇒ Audit/At 必须loud抛 ArgumentException（带 events[i] 定位），不得 NRE ──
    // 注：审计报告还声称 with{Loop=default} 后门——经编译验证不成立（EffectEvent 属性 get-only 无 init，
    // CS0200 禁止 with 改写），该子项为审计误报；default 后门真实存在，用以下测试钉死。
    [Fact]
    public void Audit_DefaultEvent_ThrowsArgumentException_WithEventIndex()
    {
        var good = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var s = new EffectScript(ImmutableArray.Create(default(EffectEvent), good));
        var ex = Assert.Throws<ArgumentException>(() => s.Audit(s.Budget));
        Assert.Contains("events[0]", ex.Message);
    }

    [Fact]
    public void At_DefaultEvent_ThrowsArgumentException_NotNRE()
    {
        var s = new EffectScript(ImmutableArray.Create(default(EffectEvent)));
        Assert.Throws<ArgumentException>(() => s.At(NatStar.Of(0)));
    }

    // ── A1-04：write 桶的 create×create 冲突必须被 gate(3) 检出（此前只扫 occupy 桶） ──
    [Fact]
    public void Audit_WriteCreate_Conflict_IsReported()
    {
        var mk = () => new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)),
            Scene("S"),
            Signature.Of(new Claim(Kind.Write, Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1)).Normalize()),
            LoopCount.Of(1));
        var s = new EffectScript(ImmutableArray.Create(mk(), mk()));
        var r = s.Audit(Budget.None);
        Assert.Contains(r.Violations, v => v.Kind == "CompatibleConflict" && v.Resource.Equals(Gpu("tex")));
    }

    [Fact]
    public void Audit_WriteRelease_Conflict_IsReported()
    {
        var mk = () => new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)),
            Scene("S"),
            Signature.Of(new Claim(Kind.Write, Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1)).Normalize()),
            LoopCount.Of(1));
        var s = new EffectScript(ImmutableArray.Create(mk(), mk()));
        var r = s.Audit(Budget.None);
        Assert.Contains(r.Violations, v => v.Kind == "CompatibleConflict");
    }

    [Fact]
    public void Audit_WriteUse_NoFalseConflict()
    {
        // write×use 自兼容（Use 自兼容）⇒ 不得误报（含 read 桶 Use 也不报）。
        var mk = (Kind k) => new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)),
            Scene("S"),
            Signature.Of(new Claim(k, Gpu("tex"), Mode.Use, Scene("S"), Interval.Exact(1)).Normalize()),
            LoopCount.Of(1));
        var s = new EffectScript(ImmutableArray.Create(mk(Kind.Write), mk(Kind.Write), mk(Kind.Read)));
        var r = s.Audit(Budget.None);
        Assert.DoesNotContain(r.Violations, v => v.Kind == "CompatibleConflict");
    }

    // ── A1-06：budget 键 memory 段溢出 ⇒ FormatException（异常方言统一），不得 OverflowException ──
    [Fact]
    public void Parse_BudgetMemoryKeyOverflow_ThrowsFormatException()
    {
        var json = """{"events":[],"budget":{"memory:99999999999999999999999":1}}""";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }

    // ── A1-12：budget 键空资源 id（"gpu:"）⇒ FormatException（与 claim 侧 ReqStr 对齐；空 id 产出永不匹配的幽灵预算并虚增 CapsChecked） ──
    [Theory]
    [InlineData("gpu:")]
    [InlineData("custom:")]
    [InlineData("signalBus:")]
    [InlineData("commandBuffer:")]
    [InlineData("occupancy:")]
    public void Parse_BudgetKeyEmptyId_ThrowsFormatException(string key)
    {
        var json = "{\"events\":[],\"budget\":{\"" + key + "\":1}}";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains(key, ex.Message);
    }

    // ── A1-07：lifetime/loop 的 "⊤" 接受 "inf" 别名（与 budget 单一真源；README 已承诺该形态） ──
    [Fact]
    public void Parse_LifetimeInfAlias_IsTop()
    {
        var json = """{"events":[{"lifetime":[0,"inf"],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"gpu":"u"},"mode":"use","scope":{"scene":"S"}}]}]}""";
        var s = EffectScriptContract.Parse(json);
        Assert.True(s.Events[0].Lifetime.Hi.IsTop);
    }

    [Fact]
    public void Parse_LoopInfAlias_IsTop()
    {
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"loop":"inf","footprint":[{"kind":"occupy","resource":{"gpu":"u"},"mode":"use","scope":{"scene":"S"}}]}]}""";
        var s = EffectScriptContract.Parse(json);
        Assert.True(s.Events[0].Loop.Count.IsTop);
    }

    [Fact]
    public void Parse_TopSymbol_StillAccepted()
    {
        var json = """{"events":[{"lifetime":[0,"⊤"],"scope":{"scene":"S"},"loop":"⊤","footprint":[{"kind":"occupy","resource":{"gpu":"u"},"mode":"use","scope":{"scene":"S"}}]}]}""";
        var s = EffectScriptContract.Parse(json);
        Assert.True(s.Events[0].Lifetime.Hi.IsTop);
        Assert.True(s.Events[0].Loop.Count.IsTop);
    }
}
