// EffectScriptEdgeTests.cs — EFFECT_SCRIPT.md §2/§3 边界/性质/确定性/性能/残差验证（L1 增量，零 Godot）。xUnit。
// 覆盖 Iter3/5/6/7/9/11/12/13/14/15/16/17/18/19/20/21/22/23/24/25 范围。风格同 EffectScriptTests.cs。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class EffectScriptEdgeTests
{
    // ── 资源/作用域/Claim 构造器（零 Godot 依赖） ──
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ResourceId CmdBuf() => new ResourceId.CommandBuffer("gpu");
    static ResourceId Mem() => new ResourceId.Memory(0);
    static ResourceId Occ(string ch) => new ResourceId.Occupancy(ch);
    static ResourceId SigBus(string n) => new ResourceId.SignalBus(new StringName(n));
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static ScopeId Global() => new ScopeId.Global();

    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz = default)
        => new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    // §2.1 — 构造事件（默认 ω=1）。
    static EffectEvent Ev(Interval life, ResourceId r, Mode m, ScopeId s, Interval sz)
        => new(life, s, Signature.Of(Oc(r, m, s, sz)), LoopCount.Of(1));
    static EffectEvent Ev(Interval life, ResourceId r, Mode m, ScopeId s, Interval sz, LoopCount w)
        => new(life, s, Signature.Of(Oc(r, m, s, sz)), w);

    // 签名相等（Signature 是 class，按三桶集合比较）。
    static bool SigEquals(Signature a, Signature b) =>
        a.ReadClaims.SetEquals(b.ReadClaims) &&
        a.WriteClaims.SetEquals(b.WriteClaims) &&
        a.OccupyClaims.SetEquals(b.OccupyClaims);

    // ════════════ Iter3 (OPEN-4 居民层净化) ════════════
    [Fact]
    public void Audit_ResidentCreate_Top_NotLeak_OnlyPeakBound()
    {
        // ω=⊤ 常驻 create（无 release）⇒ 豁免 Leak；仅受 Peak 约束。
        var resident = Ev(Interval.Exact(0), Gpu("bg"), Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Top);
        Assert.True(new EffectScript(ImmutableArray.Create(resident)).Audit(Budget.None).Passed);
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("bg")] = NatStar.Of(1) };
        var r = new EffectScript(ImmutableArray.Create(resident)).Audit(new Budget(cap));
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "PeakExceeded");
    }

    [Fact]
    public void Audit_ResidentWithClosedFinite_Create_NoLeak()
    {
        // 居民层(ω=⊤ Use) + 有限 create 有匹配 release ⇒ 不报 Leak（居民仍豁免，有限闭合）。
        var resident = Ev(Interval.Exact(0), Gpu("x"), Mode.Use, Scene("S"), Interval.Exact(1), LoopCount.Top);
        var create = Ev(Interval.Exact(0), Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1));
        var release = Ev(Interval.Exact(10), Gpu("x"), Mode.Release, Scene("S"), Interval.Exact(1));
        Assert.True(new EffectScript(ImmutableArray.Create(resident, create, release)).Audit(Budget.None).Passed);
    }

    // ════════════ Iter5 (Budget ⊤ 交互) ════════════
    [Fact]
    public void Audit_PeakExactlyEqualsCap_Passes()
    {
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(5));
        var release = Ev(Interval.Exact(0), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(5));
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("tex")] = NatStar.Of(5) };
        var s = new EffectScript(ImmutableArray.Create(create, release));
        Assert.True(s.Audit(new Budget(cap)).Passed); // 边界：峰值精确等于 cap ⇒ 不超限（且生命周期闭合无 Leak）
    }

    [Fact]
    public void Audit_PeakTopVsFiniteCap_Reported()
    {
        // ω=⊤ ⇒ size [1,⊤] ⇒ 并发峰值 ⊤ > 有限 cap ⇒ PeakExceeded。
        var resident = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Top);
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("tex")] = NatStar.Of(100) };
        var r = new EffectScript(ImmutableArray.Create(resident)).Audit(new Budget(cap));
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "PeakExceeded");
    }

    // ════════════ Iter6 (代数定律) ════════════
    [Fact]
    public void At_EmptyScript_IsEmpty()
    {
        var s = new EffectScript(ImmutableArray<EffectEvent>.Empty);
        Assert.True(SigEquals(s.At(NatStar.Of(0)), Signature.Empty));
        Assert.True(SigEquals(s.At(NatStar.Of(99)), Signature.Empty));
    }

    [Fact]
    public void At_Idempotent_SameT()
    {
        var a = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var s = new EffectScript(ImmutableArray.Create(a));
        Assert.True(SigEquals(s.At(NatStar.Of(0)), s.At(NatStar.Of(0))));
    }

    [Fact]
    public void At_Union_Commutative_Associative()
    {
        var a = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1));
        var c = Ev(Interval.Exact(0), Mem(), Mode.Create, Scene("S"), Interval.Exact(1));
        var ab = new EffectScript(ImmutableArray.Create(a, b));
        var bc = new EffectScript(ImmutableArray.Create(b, c));
        // (A∪B) 在 t=0 的快照 == A(t) ∪ B(t)（手动 Union）。
        var lhs = ab.At(NatStar.Of(0));
        var rhs = Signature.Union(new EffectScript(ImmutableArray.Create(a)).At(NatStar.Of(0)),
                                  new EffectScript(ImmutableArray.Create(b)).At(NatStar.Of(0)));
        Assert.True(SigEquals(lhs, rhs));
        // 交换：A∪B == B∪A。
        var ba = new EffectScript(ImmutableArray.Create(b, a));
        Assert.True(SigEquals(ab.At(NatStar.Of(0)), ba.At(NatStar.Of(0))));
        // 结合：(A∪B)∪C == A∪(B∪C)。
        var abc1 = new EffectScript(ImmutableArray.Create(a, b, c)).At(NatStar.Of(0));
        var abc2 = Signature.Union(ab.At(NatStar.Of(0)), new EffectScript(ImmutableArray.Create(c)).At(NatStar.Of(0)));
        Assert.True(SigEquals(abc1, abc2));
    }

    [Fact]
    public void At_EndpointSampling_CapturesPiecewiseConstant()
    {
        // 各事件端点 {0,10,20,40}；在每段开区间内 At 恒定。取严格内部两点比较。
        var a = Ev(new Interval(NatStar.Of(0), NatStar.Of(20)), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(new Interval(NatStar.Of(10), NatStar.Of(40)), Gpu("m2"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b));
        // 段 (10,20)：a 与 b 都存活 ⇒ At 恒定。
        Assert.True(SigEquals(script.At(NatStar.Of(11)), script.At(NatStar.Of(19))));
        // 段 (20,40)：仅 b 存活 ⇒ At 恒定。
        Assert.True(SigEquals(script.At(NatStar.Of(21)), script.At(NatStar.Of(39))));
        // 段 (0,10)：仅 a 存活 ⇒ At 恒定。
        Assert.True(SigEquals(script.At(NatStar.Of(1)), script.At(NatStar.Of(9))));
    }

    [Fact]
    public void At_DenseScan_FullSignatureEquivalence()
    {
        var a = Ev(new Interval(NatStar.Of(0), NatStar.Of(20)), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(new Interval(NatStar.Of(10), NatStar.Of(40)), Gpu("m2"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b));
        for (ulong t = 0; t <= 40; t++)
        {
            var s1 = script.At(NatStar.Of(t));
            var s2 = script.At(NatStar.Of(t));
            Assert.True(SigEquals(s1, s2));
            // 期望：lo≤t≤hi 的事件贡献。
            var acc = Signature.Empty;
            foreach (var e in new[] { a, b })
                if (e.Lifetime.Lo.CompareToFinite(NatStar.Of(t)) <= 0 &&
                    (e.Lifetime.Hi.IsTop || NatStar.Of(t).CompareToFinite(e.Lifetime.Hi) <= 0))
                    acc = Signature.Union(acc, Combination.Loop(e.Footprint, e.Loop, e.Scope));
            Assert.True(SigEquals(s1, acc));
        }
    }

    // ════════════ Iter7 (性质测试：随机 300 脚本) ════════════
    static readonly Random _rng = new(0xCAFE);
    static ResourceId PickResource(int k) => k switch
    {
        0 => Gpu("g" + _rng.Next(0, 5)),
        1 => CmdBuf(),
        2 => Mem(),
        _ => Occ("ch" + _rng.Next(0, 3)),
    };

    static EffectScript RandomScript(int n)
    {
        var list = new List<EffectEvent>();
        for (int i = 0; i < n; i++)
        {
            var lo = (ulong)_rng.Next(0, 50);
            var hi = (ulong)_rng.Next((int)lo, 60);
            var life = _rng.Next(0, 4) == 0
                ? new Interval(NatStar.Of(lo), NatStar.Top)
                : new Interval(NatStar.Of(lo), NatStar.Of(hi));
            var res = PickResource(_rng.Next(0, 4));
            var mode = (Mode)_rng.Next(0, 5); // Use/Create/Release/Move/Unknown
            var scope = Scene("S" + _rng.Next(0, 3));
            var w = _rng.Next(0, 5) == 0 ? LoopCount.Top : LoopCount.Of((ulong)_rng.Next(1, 5));
            var sz = Interval.Exact((ulong)_rng.Next(1, 5));
            list.Add(new EffectEvent(life, scope, Signature.Of(Oc(res, mode, scope, sz)), w));
        }
        return new EffectScript(list);
    }

    [Fact]
    public void Property_Random300_AuditDeterministicAndTerminates()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int rep = 0; rep < 300; rep++)
        {
            var s = RandomScript(_rng.Next(1, 20));
            var r1 = s.Audit(Budget.None);
            var r2 = s.Audit(Budget.None);
            // 确定性（集合语义）：两次结果 Violations 作为集合一致（输出顺序无关，§5）。
            Assert.Equal(new HashSet<Violation>(r1.Violations), new HashSet<Violation>(r2.Violations));
        }
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1000, $"300 随机脚本审计应 <1s，实际 {sw.ElapsedMilliseconds}ms");
    }

    // ════════════ Iter9 (溢出→⊤ 守卫) ════════════
    [Fact]
    public void At_LargeOmega_OverflowToTop_NoCrash()
    {
        // ω=1e19, size=Exact(2) ⇒ 2e19 超过 ulong 上限 ⇒ 溢出 ⇒ size 变 [⊤,⊤] ⇒ 峰值 ⊤。
        var big = LoopCount.Of(10_000_000_000_000_000_000UL);
        var e = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(2), big);
        var script = new EffectScript(ImmutableArray.Create(e));
        // At 不崩溃，且峰值应为 ⊤。
        var at = script.At(NatStar.Of(0));
        var peak = Derived.Peak(at, Scene("S"));
        Assert.True(peak.IsTop);
        // 有限 cap ⇒ PeakExceeded。
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("tex")] = NatStar.Of(1000) };
        var r = script.Audit(new Budget(cap));
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "PeakExceeded");
    }

    // ════════════ Iter11 (JSON 契约：AI 数据格式) ════════════
    [Fact]
    public void Contract_Parse_Then_Audit_Works()
    {
        var json = "{\"events\":[{\"lifetime\":[0,10],\"scope\":{\"scene\":\"S\"},\"loop\":1,\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"tex\"},\"mode\":\"create\",\"scope\":{\"scene\":\"S\"},\"size\":[1,1]}]},{\"lifetime\":[10,10],\"scope\":{\"scene\":\"S\"},\"loop\":1,\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"tex\"},\"mode\":\"release\",\"scope\":{\"scene\":\"S\"},\"size\":[1,1]}]}]}";
        var script = EffectScriptContract.Parse(json);
        Assert.True(script.Audit().Passed);
    }

    [Fact]
    public void Contract_Parse_CreateOnly_ReportsLeak()
    {
        var json = "{\"events\":[{\"lifetime\":[0,100],\"scope\":{\"scene\":\"S\"},\"loop\":1,\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"tex\"},\"mode\":\"create\",\"scope\":{\"scene\":\"S\"},\"size\":[1,1]}]}]}";
        var script = EffectScriptContract.Parse(json);
        var r = script.Audit();
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak");
    }

    [Fact]
    public void Contract_ResidentTop_PeakBudget_Enforced()
    {
        var json = "{\"events\":[{\"lifetime\":[0,\"⊤\"],\"scope\":{\"scene\":\"S\"},\"loop\":\"⊤\",\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"bg\"},\"mode\":\"use\",\"scope\":{\"scene\":\"S\"},\"size\":[1,1]}]}],\"budget\":{\"gpu:bg\":1}}";
        var script = EffectScriptContract.Parse(json);
        var r = script.Audit();
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "PeakExceeded");
    }

    [Fact]
    public void Contract_RoundTrip_PreservesAudit()
    {
        var ev = Ev(new Interval(NatStar.Of(0), NatStar.Of(10)), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var rel = Ev(Interval.Exact(10), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(ev, rel));
        var json = EffectScriptContract.ToJson(script);
        var back = EffectScriptContract.Parse(json);
        // round-trip 后审计结论一致（构造即闭合 ⇒ 两者 Passed）。
        Assert.Equal(script.Audit().Passed, back.Audit().Passed);
    }

    [Fact]
    public void Contract_Parse_UnknownKind_Throws()
    {
        var json = "{\"events\":[{\"lifetime\":[0,10],\"scope\":{\"scene\":\"S\"},\"loop\":1,\"footprint\":[{\"kind\":\"frobnicate\",\"resource\":{\"gpu\":\"tex\"},\"mode\":\"create\",\"scope\":{\"scene\":\"S\"},\"size\":[1,1]}]}]}";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }

    // ════════════ Iter12 (确定性) ════════════
    [Fact]
    public void At_MultipleCalls_IdenticalSignature()
    {
        var a = Ev(Interval.Exact(0), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1));
        var s = new EffectScript(ImmutableArray.Create(a, b));
        var r1 = s.At(NatStar.Of(0));
        var r2 = s.At(NatStar.Of(0));
        var r3 = s.At(NatStar.Of(0));
        Assert.True(SigEquals(r1, r2));
        Assert.True(SigEquals(r2, r3));
    }

    [Fact]
    public void Audit_EventOrdering_DoesNotAffectResult()
    {
        var a = Ev(Interval.Exact(0), Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(Interval.Exact(0), Gpu("x"), Mode.Release, Scene("S"), Interval.Exact(1));
        var s1 = new EffectScript(ImmutableArray.Create(a, b));
        var s2 = new EffectScript(ImmutableArray.Create(b, a));
        var sort = (ImmutableArray<Violation> vs) => vs
            .OrderBy(v => v.Kind).ThenBy(v => v.Resource.ToString())
            .ThenBy(v => v.AtT.Value).ThenBy(v => v.Detail).ToImmutableArray();
        Assert.Equal(sort(s1.Audit(Budget.None).Violations), sort(s2.Audit(Budget.None).Violations));
    }

    // ════════════ Iter13 (类型硬化锚定) ════════════
    [Fact]
    public void EffectEvent_NoDefaultConstructor_AllFieldsRequired()
    {
        var ctors = typeof(EffectEvent).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(ctors, c => c.GetParameters().Length == 0); // 无默认构造 ⇒ 全字段必填
        Assert.Contains(ctors, c => c.GetParameters().Length == 4); // 全字段构造
    }

    [Fact]
    public void EffectEvent_ThreeArgCtor_DefaultsOmegaToOne()
    {
        var e = new EffectEvent(Interval.Exact(0), Scene("S"), Signature.Of(Oc(Gpu("m"), Mode.Use, Scene("S"))));
        Assert.Equal(LoopCount.Of(1), e.Loop);
    }

    // ════════════ Iter14 (居民层豁免语义) ════════════
    [Fact]
    public void ResidentExempt_MixedFiniteTop_NotExempt_Leak()
    {
        // 资源 X：有限 create(无 release，应泄漏) + ω=⊤ create(无 release)。⇒ X 不豁免 ⇒ 报 Leak。
        var finiteLeak = Ev(Interval.Exact(0), Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1));
        var resident = Ev(Interval.Exact(0), Gpu("x"), Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Top);
        var s = new EffectScript(ImmutableArray.Create(finiteLeak, resident));
        var r = s.Audit(Budget.None);
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("x"))));
    }

    [Fact]
    public void ResidentExempt_OnlyTopUse_Exempt()
    {
        // 资源 Y：仅 ω=⊤ Use ⇒ 豁免 ⇒ 不报 Leak。
        var resident = Ev(Interval.Exact(0), Gpu("y"), Mode.Use, Scene("S"), Interval.Exact(1), LoopCount.Top);
        Assert.True(new EffectScript(ImmutableArray.Create(resident)).Audit(Budget.None).Passed);
    }

    // ════════════ Iter15 (空/单/∞) ════════════
    [Fact]
    public void EmptyScript_AuditPasses_AllAtEmpty()
    {
        var s = new EffectScript(ImmutableArray<EffectEvent>.Empty);
        Assert.True(s.Audit(Budget.None).Passed);
        for (ulong t = 0; t < 10; t++) Assert.True(SigEquals(s.At(NatStar.Of(t)), Signature.Empty));
    }

    [Fact]
    public void SingleEvent_AliveOutsideLifetime()
    {
        var a = Ev(new Interval(NatStar.Of(5), NatStar.Of(15)), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var s = new EffectScript(ImmutableArray.Create(a));
        Assert.Empty(s.At(NatStar.Of(0)).OccupyClaims);   // 外
        Assert.Single(s.At(NatStar.Of(10)).OccupyClaims); // 内
        Assert.Empty(s.At(NatStar.Of(20)).OccupyClaims);  // 外
    }

    [Fact]
    public void InfiniteLifetime_AliveAtLargeT_AuditClosureCorrect()
    {
        // hi=⊤（∞ 寿命），ω=⊤ Use 常驻 ⇒ 任意大有限 t 仍存活，Audit 闭包点取 maxFinite(=Lo=0) 正确。
        var e = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Top), Scene("S"),
            Signature.Of(Oc(Gpu("bg"), Mode.Use, Scene("S"), Interval.Exact(1))), LoopCount.Top);
        var s = new EffectScript(ImmutableArray.Create(e));
        Assert.Single(s.At(NatStar.Of(1_000_000_000)).OccupyClaims); // 大 t 仍存活
        Assert.True(s.Audit(Budget.None).Passed);                    // 闭包逻辑正确（无漏报/误报）
    }

    // ════════════ Iter16 (重叠/嵌套作用域) ════════════
    [Fact]
    public void OverlappingLifetimes_PartialOverlap()
    {
        var a = Ev(new Interval(NatStar.Of(0), NatStar.Of(10)), Gpu("a"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(new Interval(NatStar.Of(5), NatStar.Of(15)), Gpu("b"), Mode.Create, Scene("S"), Interval.Exact(1));
        var s = new EffectScript(ImmutableArray.Create(a, b));
        Assert.Single(s.At(NatStar.Of(2)).OccupyClaims);   // 仅 a（[0,10] 存活）
        Assert.Equal(2, s.At(NatStar.Of(7)).OccupyClaims.Count); // 重叠（[0,10]∩[5,15]）
        Assert.Single(s.At(NatStar.Of(12)).OccupyClaims); // 仅 b（[5,15] 存活）
        Assert.Empty(s.At(NatStar.Of(20)).OccupyClaims);  // 两者皆死（>15）
    }

    [Fact]
    public void NestedScope_GlobalIncludedInImplicitAudit()
    {
        // Scene("A") ⊆* Global（L1 偏序）；脚本级 Audit 用 Global 隐含 scope，应在 Scene 作用域也能检测泄漏。
        Assert.True(Scene("A").IncludedIn(Global()));
        var leak = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("tex"), Mode.Create, Scene("A"), Interval.Exact(1));
        var r = new EffectScript(ImmutableArray.Create(leak)).Audit(Budget.None);
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak");
    }

    // ════════════ Iter17 (负陷：跨资源) ════════════
    [Fact]
    public void Audit_ReleaseBeforeCreate_TwoResources_TwoNegativeDips()
    {
        var rel1 = Ev(Interval.Exact(0), Gpu("t1"), Mode.Release, Scene("S"), Interval.Exact(1));
        var cre1 = Ev(Interval.Exact(10), Gpu("t1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var rel2 = Ev(Interval.Exact(0), CmdBuf(), Mode.Release, Scene("S"), Interval.Exact(1));
        var cre2 = Ev(Interval.Exact(10), CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1));
        var s = new EffectScript(ImmutableArray.Create(rel1, cre1, rel2, cre2));
        var r = s.Audit(Budget.None);
        Assert.False(r.Passed);
        Assert.Equal(2, r.Violations.Count(v => v.Kind == "NegativeDip"));
    }

    // ════════════ Iter18 (多资源/事件) ════════════
    [Fact]
    public void MultiResource_AllClosed_Passes()
    {
        var e = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(
                Oc(Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(1)),
                Oc(CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1)),
                Oc(Mem(), Mode.Create, Scene("S"), Interval.Exact(1))));
        var relG = Ev(Interval.Exact(5), Gpu("g"), Mode.Release, Scene("S"), Interval.Exact(1));
        var relC = Ev(Interval.Exact(5), CmdBuf(), Mode.Release, Scene("S"), Interval.Exact(1));
        var relM = Ev(Interval.Exact(5), Mem(), Mode.Release, Scene("S"), Interval.Exact(1));
        Assert.True(new EffectScript(ImmutableArray.Create(e, relG, relC, relM)).Audit(Budget.None).Passed);
    }

    [Fact]
    public void MultiResource_MissingOneRelease_OnlyThatLeaks()
    {
        var e = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(
                Oc(Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(1)),
                Oc(CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1)),
                Oc(Mem(), Mode.Create, Scene("S"), Interval.Exact(1))));
        var relG = Ev(Interval.Exact(5), Gpu("g"), Mode.Release, Scene("S"), Interval.Exact(1));
        var relC = Ev(Interval.Exact(5), CmdBuf(), Mode.Release, Scene("S"), Interval.Exact(1));
        // 缺 Mem release ⇒ 仅 Mem 报 Leak。
        var r = new EffectScript(ImmutableArray.Create(e, relG, relC)).Audit(Budget.None);
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Mem())));
        Assert.DoesNotContain(r.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("g"))));
    }

    // ════════════ Iter19 (有限 ω 缩放) ════════════
    [Fact]
    public void At_FiniteOmega_ScalesPeakAndNet()
    {
        // ω=3, size=Exact(2) ⇒ At 峰值 = 2×3 = 6；累积 net = [6,6]（无 release ⇒ 报 Leak）。
        var e = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(2), LoopCount.Of(3));
        var s = new EffectScript(ImmutableArray.Create(e));
        var peak = Derived.Peak(s.At(NatStar.Of(0)), Scene("S"));
        Assert.Equal(NatStar.Of(6), peak);
        // 对照：手动 Combination.Loop 缩放。
        var manual = Combination.Loop(e.Footprint, LoopCount.Of(3), e.Scope).OccupyClaims;
        var manualPeak = manual.Where(c => c.Mode != Mode.Release).Aggregate(NatStar.Of(0UL), (acc, c) => acc + (c.Size ?? Interval.Default).Hi);
        Assert.Equal(NatStar.Of(6), manualPeak);
        // 无 release ⇒ 累积 net [6,6] 不含 0 ⇒ Leak。
        var r = s.Audit(Budget.None);
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("tex"))));
    }

    // ════════════ Iter20 (§ 出处引用，静态检查) ════════════
    [Fact]
    public void PublicApi_HasSectionCitations()
    {
        // 仓库相对路径（跨平台）：从测试输出目录向上定位仓库根，再指向源码。
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Cosmos.EffectAlgebra.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "src", "Cosmos.EffectAlgebra", "EffectScript.cs");
        Assert.True(File.Exists(path), "EffectScript.cs 应存在: " + path);
        var lines = File.ReadAllLines(path);
        int missing = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("public ")) continue;
            bool found = false;
            for (int j = Math.Max(0, i - 6); j <= i; j++)
                if (lines[j].TrimStart().StartsWith("///") && lines[j].Contains("§")) { found = true; break; }
            if (!found) missing++;
        }
        Assert.Equal(0, missing);
    }

    // ════════════ Iter21 (全 Compatible 矩阵，循环枚举) ════════════
    [Fact]
    public void Compatible_AllModePairs_MatrixConsistent()
    {
        // 穷举 5 模式对（含 Unknown=Use 约化），与 Algebra.Compatible 全函数逐项一致（§3.2.3）。
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var a in modes) foreach (var b in modes)
        {
            var expect = Compatible.IsCompatible(a, b);
            var viaScript = CompatibleAuditPair(a, b);
            Assert.Equal(expect, viaScript);
        }
    }
    static bool CompatibleAuditPair(Mode a, Mode b)
    {
        // 同资源同 scope 两事件各持 a/b 重叠 ⇒ 是否报 CompatibleConflict（不注入 release，避免干扰配对）。
        var e1 = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("x"), a, Scene("S"), Interval.Exact(1));
        var e2 = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("x"), b, Scene("S"), Interval.Exact(1));
        var r = new EffectScript(ImmutableArray.Create(e1, e2)).Audit(Budget.None);
        return !r.Violations.Any(v => v.Kind == "CompatibleConflict");
    }

    // ════════════ Iter22 (1000 随机脚本 fuzz：确定性+终止+不抛) ════════════
    [Fact]
    public void Fuzz_1000RandomScripts_AllDeterministicNoThrow()
    {
        for (int rep = 0; rep < 1000; rep++)
        {
            var s = RandomScript(_rng.Next(1, 25));
            var r1 = s.Audit(Budget.None);
            var r2 = s.Audit(Budget.None);
            // 确定性（集合语义，不抛）。
            Assert.Equal(new HashSet<Violation>(r1.Violations), new HashSet<Violation>(r2.Violations));
        }
    }

    // ════════════ Iter23 (端点采样 == 全密集扫描，证明) ════════════
    [Fact]
    public void EndpointSampling_EqualsDenseIntervalScan()
    {
        // 3 事件错峰；端点 {0,5,10,15,20,30}；对任意 t∈[0,30] At 签名 == 手动 Union（含段内任意点 + 端点）。
        var a = Ev(new Interval(NatStar.Of(0), NatStar.Of(20)), Gpu("m1"), Mode.Create, Scene("S"), Interval.Exact(1));
        var b = Ev(new Interval(NatStar.Of(5), NatStar.Of(15)), Gpu("m2"), Mode.Create, Scene("S"), Interval.Exact(1));
        var c = Ev(new Interval(NatStar.Of(10), NatStar.Of(30)), Gpu("m3"), Mode.Create, Scene("S"), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(a, b, c));
        for (ulong t = 0; t <= 30; t++)
        {
            var at = script.At(NatStar.Of(t));
            var acc = Signature.Empty;
            foreach (var e in new[] { a, b, c })
                if (e.Lifetime.Lo.CompareToFinite(NatStar.Of(t)) <= 0 &&
                    (e.Lifetime.Hi.IsTop || NatStar.Of(t).CompareToFinite(e.Lifetime.Hi) <= 0))
                    acc = Signature.Union(acc, Combination.Loop(e.Footprint, e.Loop, e.Scope));
            Assert.True(SigEquals(at, acc));
        }
    }

    // ════════════ Iter24 (§7 形状一致：ResourceId 不发明新 kind) ════════════
    [Fact]
    public void ResourceKinds_MatchKnownSet()
    {
        var kinds = typeof(ResourceId).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ResourceId).IsAssignableFrom(t))
            .Select(t => t.Name).ToArray();
        var expected = new[]
        {
            "Tree", "Self", "Physics", "Memory", "Disk", "Signal", "Gpu", "AudioMixer",
            "Occupancy", "Callback", "Network", "Input", "Custom", "CommandBuffer", "SignalBus"
        };
        Assert.Equal(new HashSet<string>(expected), new HashSet<string>(kinds)); // 集合比较，不依赖顺序
    }

    // ════════════ Iter25 (性能守卫：1000 事件) ════════════
    [Fact]
    public void Performance_1000Events_AuditUnder5s_StillDetectsConflict()
    {
        var list = new List<EffectEvent>();
        for (int i = 0; i < 1000; i++)
        {
            var lo = (ulong)(i % 50);
            var hi = lo + 5;
            var res = PickResource(i % 4);
            var mode = (Mode)(i % 4); // Use/Create/Release/Move
            var scope = Scene("S" + (i % 3));
            var w = (i % 7 == 0) ? LoopCount.Top : LoopCount.Of((ulong)(i % 4 + 1));
            var sz = Interval.Exact((ulong)(i % 5 + 1));
            list.Add(new EffectEvent(new Interval(NatStar.Of(lo), NatStar.Of(hi)), scope,
                Signature.Of(Oc(res, mode, scope, sz)), w));
        }
        // 注入一个明确冲突：同资源同 scope 两 create 重叠。
        var conflictA = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("conf"), Mode.Create, Scene("S0"), Interval.Exact(1));
        var conflictB = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("conf"), Mode.Create, Scene("S0"), Interval.Exact(1));
        var conflictRel = Ev(new Interval(NatStar.Of(0), NatStar.Of(100)), Gpu("conf"), Mode.Release, Scene("S0"), Interval.Exact(1));
        list.Add(conflictA); list.Add(conflictB); list.Add(conflictRel);

        var s = new EffectScript(list);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r1 = s.Audit(Budget.None);
        var r2 = s.Audit(Budget.None);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 5000, $"1000 事件审计应 <5s，实际 {sw.ElapsedMilliseconds}ms");
        // 确定性（集合语义）
        Assert.Equal(new HashSet<Violation>(r1.Violations), new HashSet<Violation>(r2.Violations));
        // 注入的冲突仍被检出
        Assert.Contains(r1.Violations, v => v.Kind == "CompatibleConflict");
    }

    // ════════════ Iter26 (Jeff Dean 性能审计：O(S·E²) → O(E·K·log E) 扫换线重写 + 等价证明) ════════════
    // 独立暴力参考实现（原始 O(S·E²) 语义，仅用 public API 重写），用于证明扫换线 Audit 与其逐条 Violation 集合等价。
    private static AuditResult ReferenceAudit(EffectScript s, Budget cap)
    {
        var violations = new List<Violation>();
        var endpoints = new HashSet<ulong>();
        ulong maxFinite = 0; bool anyFinite = false, anyOpenEnd = false;
        foreach (var e in s.Events)
        {
            if (!e.Lifetime.Lo.IsTop) { endpoints.Add(e.Lifetime.Lo.Value); maxFinite = Math.Max(maxFinite, e.Lifetime.Lo.Value); anyFinite = true; }
            if (!e.Lifetime.Hi.IsTop) { endpoints.Add(e.Lifetime.Hi.Value); maxFinite = Math.Max(maxFinite, e.Lifetime.Hi.Value); anyFinite = true; }
            else anyOpenEnd = true;
        }
        var samplePoints = new List<NatStar>();
        if (anyFinite) foreach (var v in endpoints.OrderBy(x => x)) samplePoints.Add(NatStar.Of(v));
        if (anyOpenEnd) samplePoints.Add(NatStar.Of(maxFinite + 1));
        if (samplePoints.Count == 0) samplePoints.Add(NatStar.Of(0));
        var closureT = anyFinite ? NatStar.Of(maxFinite) : NatStar.Of(0);

        bool Alive(Interval lt, NatStar t) => lt.Lo.CompareToFinite(t) <= 0 && (lt.Hi.IsTop || t.CompareToFinite(lt.Hi) <= 0);
        Interval Scale(Interval sz, NatStar w) => w.IsTop ? new Interval(sz.Lo, NatStar.Top) : new Interval(sz.Lo * w, sz.Hi * w);
        ZStar ZTo(NatStar n) => n.IsTop ? ZStar.Top : ZStar.Of((long)n.Value);
        ZStar ZNeg(NatStar n) => n.IsTop ? ZStar.Top : ZStar.Of(-(long)n.Value);

        // 闭包守恒
        var closure = new Dictionary<ResourceId, SignedInterval>();
        foreach (var e in s.Events)
        {
            if (e.Loop.Count.IsTop) continue;
            if (e.Lifetime.Lo.IsTop) continue;
            foreach (var c in e.Footprint.OccupyClaims)
            {
                var r = ResourceId.Normalize(c.Resource);
                var scaled = Scale(c.Size ?? Interval.Default, e.Loop.Count);
                var contrib = c.Mode == Mode.Release
                    ? new SignedInterval(ZNeg(scaled.Hi), ZNeg(scaled.Lo))
                    : new SignedInterval(ZTo(scaled.Lo), ZTo(scaled.Hi));
                closure[r] = closure.TryGetValue(r, out var cur) ? cur.Add(contrib) : contrib;
            }
        }
        foreach (var kv in closure) if (!kv.Value.ContainsZero)
            violations.Add(new Violation(closureT, kv.Key, new ScopeId.Global(), "Leak", ""));

        foreach (var t in samplePoints)
        {
            // gate(1) 累积 net
            var cum = new Dictionary<ResourceId, SignedInterval>();
            foreach (var e in s.Events)
            {
                if (e.Loop.Count.IsTop) continue;
                if (e.Lifetime.Lo.CompareToFinite(t) > 0) continue;
                foreach (var c in e.Footprint.OccupyClaims)
                {
                    var r = ResourceId.Normalize(c.Resource);
                    var scaled = Scale(c.Size ?? Interval.Default, e.Loop.Count);
                    var contrib = c.Mode == Mode.Release
                        ? new SignedInterval(ZNeg(scaled.Hi), ZNeg(scaled.Lo))
                        : new SignedInterval(ZTo(scaled.Lo), ZTo(scaled.Hi));
                    cum[r] = cum.TryGetValue(r, out var cur) ? cur.Add(contrib) : contrib;
                }
            }
            foreach (var kv in cum) if (!kv.Value.Hi.IsTop && kv.Value.Hi.Value < 0)
                violations.Add(new Violation(t, kv.Key, new ScopeId.Global(), "NegativeDip", ""));
            // gate(2) 峰值（用 public At，忽略 scope 匹配 cap key，与原 PeakForResource 一致）
            var sig = s.At(t);
            foreach (var kv in cap.Caps)
            {
                NatStar sum = NatStar.Of(0);
                foreach (var c in sig.OccupyClaims)
                {
                    if (c.Mode == Mode.Release) continue;
                    if (!ResourceId.Normalize(c.Resource).Equals(ResourceId.Normalize(kv.Key))) continue;
                    if ((c.Size ?? Interval.Default).Hi.IsTop) { sum = NatStar.Top; break; }
                    sum = sum + (c.Size ?? Interval.Default).Hi;
                }
                if (sum.CompareToFinite(kv.Value) > 0)
                    violations.Add(new Violation(t, kv.Key, new ScopeId.Global(), "PeakExceeded", ""));
            }
            // gate(3) 兼容（逐点全算，两两枚举，与原审计一致）
            var groups = new Dictionary<(ResourceId, ScopeId), List<(int, Mode)>>();
            for (int ei = 0; ei < s.Events.Length; ei++)
            {
                var e = s.Events[ei];
                if (!Alive(e.Lifetime, t)) continue;
                var looped = Combination.Loop(e.Footprint, e.Loop, e.Scope).OccupyClaims;
                foreach (var c in looped)
                {
                    var key = (ResourceId.Normalize(c.Resource), c.Scope);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new();
                    var copies = e.Loop.Count.IsTop ? 2 : (int)e.Loop.Count.Value;
                    for (int k = 0; k < copies; k++) list.Add((ei, c.Mode));
                }
            }
            foreach (var kv in groups)
                for (int i = 0; i < kv.Value.Count; i++)
                    for (int j = i + 1; j < kv.Value.Count; j++)
                        if (kv.Value[i].Item1 != kv.Value[j].Item1 && !Compatible.IsCompatible(kv.Value[i].Item2, kv.Value[j].Item2))
                            violations.Add(new Violation(t, kv.Key.Item1, kv.Key.Item2, "CompatibleConflict", ""));
        }
        return new AuditResult(violations.Count == 0, violations.ToImmutableArray());
    }

    // 仅比较 (t,resource,scope,kind)，忽略 Detail 字符串差异（实现细节）。
    private static HashSet<(string, string, string, string)> ViolationKeys(AuditResult r) =>
        new(r.Violations.Select(v => (v.AtT.ToString(), v.Resource.ToString(), v.Scope.ToString(), v.Kind)));

    [Fact]
    public void Iter26_SweepLine_EqualsBruteForce_Reference_Random100()
    {
        for (int rep = 0; rep < 100; rep++)
        {
            var s = RandomScript(_rng.Next(1, 30));
            var cap = Budget.None;
            if (rep % 3 == 0)
            {
                var c = new Dictionary<ResourceId, NatStar> { [Gpu("g0")] = NatStar.Of(2), [CmdBuf()] = NatStar.Of(1) };
                cap = new Budget(c);
            }
            var a = s.Audit(cap);
            var b = ReferenceAudit(s, cap);
            Assert.Equal(ViolationKeys(b), ViolationKeys(a)); // 扫换线 == 暴力参考（逐条等价）
        }
    }

    [Fact]
    public void Iter26_SweepLine_EqualsBruteForce_AdversarialShapes()
    {
        // 对抗形状：① 全同资源同 scope 重叠（触发 gate3 最坏）② 常驻 ω=⊤ 叠加 ③ 大 ω 峰值 ④ release 早于 create。
        var shapes = new List<EffectScript>();
        // ① 同屏 200 粒子全 create 重叠 → 必报 create×create 冲突（每采样点）
        {
            var list = new List<EffectEvent>();
            for (int i = 0; i < 200; i++)
                list.Add(Ev(new Interval(NatStar.Of(0), NatStar.Of(1000)), Gpu("particle"), Mode.Create, Scene("S"), Interval.Exact(1)));
            shapes.Add(new EffectScript(list));
        }
        // ② 常驻 ω=⊤ use × 有限 create（应不泄漏，仅峰值约束）
        {
            var res = Ev(new Interval(NatStar.Of(0), NatStar.Top), Gpu("bg"), Mode.Use, Scene("S"), Interval.Exact(1), LoopCount.Top);
            var cr = Ev(new Interval(NatStar.Of(0), NatStar.Of(50)), Gpu("bg"), Mode.Create, Scene("S"), Interval.Exact(1));
            var rel = Ev(new Interval(NatStar.Of(50), NatStar.Of(50)), Gpu("bg"), Mode.Release, Scene("S"), Interval.Exact(1));
            shapes.Add(new EffectScript(ImmutableArray.Create(res, cr, rel)));
        }
        // ③ 大 ω 峰值超限
        {
            var e = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1), LoopCount.Of(1000));
            shapes.Add(new EffectScript(ImmutableArray.Create(e)));
        }
        // ④ release 早于 create（负陷 + 泄漏）
        {
            var rel = Ev(Interval.Exact(0), Gpu("m"), Mode.Release, Scene("S"), Interval.Exact(1));
            var cr = Ev(Interval.Exact(10), Gpu("m"), Mode.Create, Scene("S"), Interval.Exact(1));
            shapes.Add(new EffectScript(ImmutableArray.Create(rel, cr)));
        }
        foreach (var s in shapes)
            Assert.Equal(ViolationKeys(ReferenceAudit(s, Budget.None)), ViolationKeys(s.Audit(Budget.None)));
    }

    [Fact]
    public void Iter26_Adversarial_ThousandsOverlappingParticles_BoundedTime()
    {
        // Jeff Dean 最坏场景：5000 粒子全部同资源同 scope 重叠存活（原 O(S·E²) 会平方爆炸）。
        // 扫换线应线性伸缩：远低于朴素版本（预期毫秒级）。
        var list = new List<EffectEvent>();
        for (int i = 0; i < 5000; i++)
        {
            var lo = (ulong)(i % 100);
            var w = LoopCount.Of((ulong)(i % 5 + 1));
            // 全部重叠于 [0,1000] 同 Gpu("particle")/Scene("S")，mode 多数 create（触发冲突计数）
            var mode = (i % 7 == 0) ? Mode.Use : Mode.Create;
            list.Add(Ev(new Interval(NatStar.Of(0), NatStar.Of(1000)), Gpu("particle"), mode, Scene("S"), Interval.Exact(1), w));
        }
        var s = new EffectScript(list);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r1 = s.Audit(Budget.None);
        var r2 = s.Audit(Budget.None);
        sw.Stop();
        // 5000 事件全重叠：扫换线 O(E·K·log E) 应在数百毫秒内（朴素 O(S·E²) 会卡死/分钟级）。
        Assert.True(sw.ElapsedMilliseconds < 2000, $"5000 同屏粒子审计应 <2s，实际 {sw.ElapsedMilliseconds}ms");
        // 确定性（集合语义）
        Assert.Equal(new HashSet<Violation>(r1.Violations), new HashSet<Violation>(r2.Violations));
        // 正确检测到大量 create×create 冲突
        Assert.Contains(r1.Violations, v => v.Kind == "CompatibleConflict");
    }

    // ── auditR3b OPEN-2 回归：ToJson 须序列化 read/write 三桶，round-trip 不丢桶 ──
    [Fact]
    public void Contract_RoundTrip_PreservesReadWriteBuckets()
    {
        var ev = new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)),
            Scene("S"),
            Signature.Of(
                new Claim(Kind.Read, Gpu("tex"), Mode.Use, Scene("S"), Interval.Exact(1)),
                new Claim(Kind.Write, Gpu("tex"), Mode.Use, Scene("S"), Interval.Exact(1)),
                Oc(Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1))),
            LoopCount.Of(1));
        var script = new EffectScript(ImmutableArray.Create(ev));
        // 修改前：ToJson 仅写 OccupyClaims ⇒ 反解析后 read/write 桶为空。
        var json = EffectScriptContract.ToJson(script);
        Assert.Contains("\"kind\": \"read\"", json);
        Assert.Contains("\"kind\": \"write\"", json);
        var back = EffectScriptContract.Parse(json);
        Assert.Equal(ev.Footprint.ReadClaims.Count, back.Events[0].Footprint.ReadClaims.Count);
        Assert.Equal(ev.Footprint.WriteClaims.Count, back.Events[0].Footprint.WriteClaims.Count);
        Assert.Equal(ev.Footprint.OccupyClaims.Count, back.Events[0].Footprint.OccupyClaims.Count);
    }

    // ── auditR4 CRITICAL 回归：Global scope round-trip 不再必炸（SerializeScope 输出 {"type":"global"} 无 scene） ──
    [Fact]
    public void Contract_RoundTrip_GlobalScope_Preserves()
    {
        var ev = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Global(), Interval.Exact(1));
        var script = new EffectScript(ImmutableArray.Create(ev));
        var json = EffectScriptContract.ToJson(script);
        Assert.Contains("\"type\": \"global\"", json);
        var back = EffectScriptContract.Parse(json); // 修改前：ParseScope 强制 scene ⇒ 此处抛 FormatException。
        Assert.Equal(1, back.Events.Length);
        Assert.IsType<ScopeId.Global>(back.Events[0].Scope);
    }

    // ── auditR2/R4 C2 回归：resource 值缺失/类型错 ⇒ fail-fast，不静默改写（memory 类型错曾静默成 0） ──
    [Fact]
    public void Contract_Parse_ResourceBadValue_Throws()
    {
        // memory 须数字；字符串/缺失 ⇒ 抛（修改前静默成 Memory(0)）。
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse("{\"events\":[{\"lifetime\":[0,10],\"scope\":{\"type\":\"global\"},\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"memory\":\"oops\"},\"mode\":\"create\",\"scope\":{\"type\":\"global\"}}]}]}"));
        // gpu 空串 ⇒ 抛（修改前静默成 Gpu("")）。
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse("{\"events\":[{\"lifetime\":[0,10],\"scope\":{\"type\":\"global\"},\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"gpu\":\"\"},\"mode\":\"create\",\"scope\":{\"type\":\"global\"}}]}]}"));
        // 合法数字 memory 仍解析为对应 uid。
        var s = EffectScriptContract.Parse("{\"events\":[{\"lifetime\":[0,10],\"scope\":{\"type\":\"global\"},\"footprint\":[{\"kind\":\"occupy\",\"resource\":{\"memory\":42},\"mode\":\"create\",\"scope\":{\"type\":\"global\"}}]}]}");
        Assert.Equal(new ResourceId.Memory(42UL), s.Events[0].Footprint.OccupyClaims.First().Resource);
    }

    // ── auditR3b OPEN-1 回归：gate(3) 冲突分组 scope 须与 At 投影(e.Scope) 一致，不按 claim 自带 c.Scope 分裂 ──
    [Fact]
    public void Audit_ConflictScope_MatchesAtProjection()
    {
        // 两事件：event scope=Global，但 claim 自带 scope=Scene("Battle")。
        // 修改前：Audit 用 c.Scope=Scene 报冲突，而 At 经 Combination.Loop 投影到 e.Scope=Global ⇒ 视角错位。
        var mk = (ScopeId evScope, ScopeId claimScope) => new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)),
            evScope,
            Signature.Of(Oc(Gpu("tex"), Mode.Create, claimScope, Interval.Exact(1))),
            LoopCount.Of(1));
        var s = new EffectScript(ImmutableArray.Create(
            mk(Global(), Scene("Battle")),
            mk(Global(), Scene("Battle"))));
        var r = s.Audit(Budget.None);
        var conflicts = r.Violations.Where(v => v.Kind == "CompatibleConflict").ToImmutableArray();
        // 冲突须归因到 At 视角所用的事件 scope(Global)，而非 claim 自带 scope(Scene)。
        Assert.All(conflicts, v => Assert.IsType<ScopeId.Global>(v.Scope));
        Assert.All(conflicts, v => Assert.False(v.Scope is ScopeId.Scene));
        // At 投影签名也含该冲突资源（Combination.Loop 改写到 e.Scope=Global，与冲突归因 scope 一致）；
        // 注：两事件 claim 经 Normalize 后相等，Union 去重 ⇒ 计 1，不掩 scope 对齐。
        var at = s.At(NatStar.Of(5));
        Assert.True(at.OccupyClaims.Count(c => ResourceId.Normalize(c.Resource).Equals(Gpu("tex")) && c.Scope is ScopeId.Global) >= 1);
    }

    // ── auditR5 F1 回归：EffectScript.Budget 为不可变构造参数（非可变属性），值语义无隐藏状态 ──
    [Fact]
    public void EffectScript_Budget_IsImmutableValue()
    {
        // 闭合事件（create+release）避免 Leak，专注验证 Budget 不可变 + 值语义确定性。
        var create = Ev(Interval.Exact(0), Gpu("tex"), Mode.Create, Scene("S"), Interval.Exact(1));
        var release = Ev(Interval.Exact(1), Gpu("tex"), Mode.Release, Scene("S"), Interval.Exact(1));
        var evs = ImmutableArray.Create(create, release);
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("tex")] = NatStar.Of(2) };
        var budget = new Budget(cap);
        var s = new EffectScript(evs, budget);
        Assert.Equal(budget, s.Budget);
        var sDefault = new EffectScript(evs);
        Assert.Equal(Budget.None, sDefault.Budget);
        var r1 = s.Audit();
        var r2 = s.Audit();
        Assert.Equal(new HashSet<Violation>(r1.Violations), new HashSet<Violation>(r2.Violations));
        Assert.True(r1.Passed); // 闭合 + 预算 2≥1 ⇒ 通过
    }
}
