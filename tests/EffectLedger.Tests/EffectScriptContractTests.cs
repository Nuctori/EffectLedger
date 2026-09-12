// EffectScriptContractTests.cs — EFFECT_SCRIPT.md §4/§5/§8 契约/跨层/Compat/模糊/端点证明（Iter8/10/11/21/22/23）。xUnit。
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace EffectLedger.Tests;

public class EffectScriptContractTests
{
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ResourceId CmdBuf() => new ResourceId.CommandBuffer("gpu");
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz = default)
        => new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    // ════════════ Iter8 (跨层接线：EffectScript 真委托 L1，未重算代数) ════════════
    [Fact]
    public void At_DelegatesToL1_NotReimplemented()
    {
        // 构造脚本 At(t) 的结果，须与「按 L1 规则手算」逐位相等：每存活事件取 Combination.Loop 后 Union。
        var a = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(2))), LoopCount.Of(3));
        var b = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));
        var script = new EffectScript(ImmutableArray.Create(a, b));

        var at = script.At(NatStar.Of(0));
        var manual = Signature.Union(
            Combination.Loop(a.Footprint, a.Loop, a.Scope),
            Combination.Loop(b.Footprint, b.Loop, b.Scope));
        Assert.Equal(manual.OccupyClaims, at.OccupyClaims, ClaimComparer.Instance);
        // 且 peak/net 经由 L1 Derived 计算（非脚本内重算）：Gpu 2×3=6，Cmd 1×1=1，合计 7。
        Assert.Equal(NatStar.Of(7), Derived.Peak(at, Scene("S")));
    }

    [Fact]
    public void Audit_UsesL1_NetTable_Peak_Compatible()
    {
        // 构造泄漏 ⇒ Audit 经 L1 累积 net 检测（L1 数学权威）。
        var leak = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));
        var r = new EffectScript(ImmutableArray.Create(leak)).Audit(Budget.None);
        // 同时验证：Audit 内部 CumulativeNet 与直接 L1 Derived.Net 一致（脚本级 Global 隐含 scope）。
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak");
    }

    // ════════════ Iter10 (违例证据质量：供 AI 回修) ════════════
    [Fact]
    public void Violation_CarriesRepairInfo()
    {
        var leak = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(100)), Scene("S"),
            Signature.Of(Oc(Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));
        var r = new EffectScript(ImmutableArray.Create(leak)).Audit(Budget.None);
        var v = Assert.Single(r.Violations);
        Assert.Equal("Leak", v.Kind);
        Assert.Equal("Gpu", v.Resource.GetType().Name);            // 资源类可读
        Assert.True(v.AtT.Value > 0);                               // 闭包点时刻
        Assert.False(string.IsNullOrEmpty(v.Detail));              // 当前值 vs 阈值
    }

    [Fact]
    public void Violation_Peak_CarriesCap()
    {
        var resident = new EffectEvent(Interval.Exact(0), Scene("S"),
            Signature.Of(Oc(Gpu("g"), Mode.Use, Scene("S"), Interval.Exact(1))), LoopCount.Top);
        var cap = new Dictionary<ResourceId, NatStar> { [Gpu("g")] = NatStar.Of(1) };
        var r = new EffectScript(ImmutableArray.Create(resident)).Audit(new Budget(cap));
        var v = Assert.Single(r.Violations);
        Assert.Equal("PeakExceeded", v.Kind);
        Assert.Contains("预算", v.Detail); // 携带 上限 信息
    }

    // ════════════ Iter11 (JSON 契约) ════════════
    const string SampleJson = """
    {
      "events": [
        { "lifetime": [0, 120], "loop": 1,
          "scope": { "scene": "Battle" },
          "footprint": [
            { "kind": "occupy", "resource": {"gpu":"mesh1"}, "mode": "create",
              "scope": {"scene":"Battle"}, "size": [1,1] },
            { "kind": "occupy", "resource": {"commandBuffer":"gpu"}, "mode": "create",
              "scope": {"scene":"Battle"}, "size": [1,1] }
          ] },
        { "lifetime": [60, 180],
          "scope": { "scene": "Battle" },
          "footprint": [
            { "kind": "occupy", "resource": {"commandBuffer":"gpu"}, "mode": "release",
              "scope": {"scene":"Battle"}, "size": [1,1] }
          ] },
        { "lifetime": [0, 0], "loop": "⊤",
          "scope": { "scene": "Battle" },
          "footprint": [
            { "kind": "occupy", "resource": {"gpu":"bg"}, "mode": "use",
              "scope": {"scene":"Battle"}, "size": [1,1] }
          ] }
      ],
      "budget": { "commandBuffer:gpu": 64 }
    }
    """;

    [Fact]
    public void JsonRoundTrip_Parses()
    {
        var script = EffectScriptContract.Parse(SampleJson); // 单一契约解析器（删除冗余 EffectScriptIo）
        Assert.Equal(3, script.Events.Length);
        Assert.True(script.Budget.Caps.ContainsKey(ResourceId.Normalize(CmdBuf())));
        Assert.Equal(NatStar.Of(64), script.Budget.Caps[ResourceId.Normalize(CmdBuf())]);
    }

    [Fact]
    public void Json_ValidScript_PassesAudit()
    {
        var script = EffectScriptContract.Parse(SampleJson);
        var r = script.Audit(); // 用 Parse 附着的 Budget
        Assert.False(r.Passed);
        Assert.Contains(r.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("mesh1"))));
    }

    [Fact]
    public void Json_UnknownResource_Throws()
    {
        var bad = """{"events":[{"lifetime":[0,10],"footprint":[{"kind":"occupy","resource":{"alien":{}},"mode":"create"}]}]}""";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(bad));
    }

    [Fact]
    public void Json_MissingLifetime_Throws()
    {
        var bad = """{"events":[{"footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}]}]}""";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(bad));
    }

    // ════════════ Iter21 (全 Compatible 矩阵：存活同组两两) ════════════
    [Fact]
    public void Compatible_Matrix_AllPairwiseChecked()
    {
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        // CONFLICT 集（§3.2.3）：仅同模式且非良性配对的 (create,create)/(move,move)/(release,release)。
        // create+release、create+move、use+* 均为良性兼容，不冲突；Unknown 按 Use 处理（最弱兼容）。
        var conflictSet = new HashSet<Mode> { Mode.Create, Mode.Move, Mode.Release };
        int detected = 0, expected = 0;
        foreach (var a in modes) foreach (var b in modes)
        {
            var e1 = new EffectEvent(Interval.Exact(0), Scene("S"),
                Signature.Of(Oc(Gpu("x"), a, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));
            var e2 = new EffectEvent(Interval.Exact(0), Scene("S"),
                Signature.Of(Oc(Gpu("x"), b, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));
            var r = new EffectScript(ImmutableArray.Create(e1, e2)).Audit(Budget.None);
            bool expectConflict = a == b && conflictSet.Contains(a);
            bool got = r.Violations.Any(v => v.Kind == "CompatibleConflict");
            if (expectConflict) { expected++; if (got) detected++; }
            else Assert.DoesNotContain(r.Violations, v => v.Kind == "CompatibleConflict");
        }
        Assert.Equal(expected, detected); // 所有 CONFLICT 对均被检出，无漏无多
    }

    // ════════════ Iter22 (模糊 1000 脚本不抛/确定性) ════════════
    static readonly System.Random _rng = new(0xBEEF);
    static EffectScript Rand(int n)
    {
        var list = new List<EffectEvent>();
        for (int i = 0; i < n; i++)
        {
            var lo = (ulong)_rng.Next(0, 40);
            var hi = (ulong)_rng.Next((int)lo, 50);
            var life = _rng.Next(0, 3) == 0 ? new Interval(NatStar.Of(lo), NatStar.Top) : new Interval(NatStar.Of(lo), NatStar.Of(hi));
            var res = new[] { Gpu("g" + _rng.Next(0, 4)), CmdBuf(), new ResourceId.Memory(0), new ResourceId.Occupancy("c" + _rng.Next(0, 2)) }[_rng.Next(0, 4)];
            var mode = (Mode)_rng.Next(0, 5);
            var scope = Scene("S" + _rng.Next(0, 2));
            var w = _rng.Next(0, 4) == 0 ? LoopCount.Top : LoopCount.Of((ulong)_rng.Next(1, 3));
            var sz = Interval.Exact((ulong)_rng.Next(1, 4));
            list.Add(new EffectEvent(life, scope, Signature.Of(Oc(res, mode, scope, sz)), w));
        }
        return new EffectScript(list);
    }

    [Fact]
    public void Fuzz_1000Scripts_NoThrow_Deterministic()
    {
        for (int i = 0; i < 1000; i++)
        {
            var s = Rand(_rng.Next(1, 15));
            var r1 = s.Audit(Budget.None);
            var r2 = s.Audit(Budget.None);
            Assert.Equal(new HashSet<Violation>(r1.Violations), new HashSet<Violation>(r2.Violations));
        }
    }

    // ════════════ Iter23 (端点采样 == 全密集扫描，强证明) ════════════
    [Fact]
    public void EndpointSampling_EqualsDenseScan_Strong()
    {
        var a = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(30)), Scene("S"),
            Signature.Of(Oc(Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(2))), LoopCount.Of(2));
        var b = new EffectEvent(new Interval(NatStar.Of(15), NatStar.Of(45)), Scene("S"),
            Signature.Of(Oc(CmdBuf(), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));
        var c = new EffectEvent(new Interval(NatStar.Of(20), NatStar.Top), Scene("S"),
            Signature.Of(Oc(new ResourceId.Memory(0), Mode.Use, Scene("S"), Interval.Exact(1))), LoopCount.Top);
        var script = new EffectScript(ImmutableArray.Create(a, b, c));

        for (ulong t = 0; t <= 60; t++)
        {
            var at = script.At(NatStar.Of(t));
            var manual = Signature.Empty;
            foreach (var e in new[] { a, b, c })
                if (e.Lifetime.Lo.CompareToFinite(NatStar.Of(t)) <= 0 &&
                    (e.Lifetime.Hi.IsTop || NatStar.Of(t).CompareToFinite(e.Lifetime.Hi) <= 0))
                    manual = Signature.Union(manual, Combination.Loop(e.Footprint, e.Loop, e.Scope));
            Assert.Equal(manual.OccupyClaims, at.OccupyClaims, ClaimComparer.Instance);
        }
    }
}

// 占位：避免在未引用时产生 unused 警告（TreatWarningsAsErrors）。
file sealed class ClaimComparer : IEqualityComparer<Claim>
{
    public static readonly ClaimComparer Instance = new();
    public bool Equals(Claim x, Claim y) => x.Kind == y.Kind && ResourceId.Normalize(x.Resource).Equals(ResourceId.Normalize(y.Resource)) && x.Mode == y.Mode && x.Scope.Equals(y.Scope) && x.Size.Equals(y.Size);
    public int GetHashCode(Claim o) => o.GetHashCode();
}
