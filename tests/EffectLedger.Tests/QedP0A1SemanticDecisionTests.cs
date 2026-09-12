// QedP0A1SemanticDecisionTests.cs — QED 迭代 P0-A1 语义定稿钉：两个 ⊤ 闭合不同的轴，
// 居民豁免的判据是「配对 release 结构性不可枚举」（population-⊤），而非「语义常驻」。
// time-⊤（lifetime.hi=⊤，有限 ω）是完整事件缺 release ⇒ Leak；population-⊤（ω=⊤）无法枚举配对
// ⇒ 豁免 gate(1) 守恒、仍受 gate(2) 峰值审计。决策记录：audit/qed/ROADMAP.md A1 +
// EFFECT_SCRIPT.md §2.1 Loop 注记 + README「已知语义锐边」。xUnit。
using System.Collections.Immutable;
using Xunit;

namespace EffectLedger.Tests;

public class QedP0A1SemanticDecisionTests
{
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz = default)
        => new Claim(Kind.Occupy, r, m, s, sz).Normalize();

    // ════════════ 定稿钉 1：time-⊤ + 有限 ω ⇒ Leak（时间轴 ⊤ 不豁免） ════════════
    [Fact]
    public void TimeAxisTop_FiniteLoop_CreateWithoutRelease_IsLeak()
    {
        // lifetime.hi=⊤（永占）、ω=1 有限：事件完整可枚举，release 可表达而缺席 ⇒ 守恒判定 Leak。
        // 与 ω=⊤ 豁免（OPEN-4）对照：豁免判据是「配对结构性不可枚举」，非「语义常驻」。
        var forever = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Top), Scene("S"),
            Signature.Of(Oc(Gpu("leak"), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));

        var result = new EffectScript(ImmutableArray.Create(forever)).Audit(Budget.None);

        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("leak"))));
    }

    // ════════════ 定稿钉 2：双 ⊤ 并置对照（同剧本、同规则、相反结论——契约面） ════════════
    [Fact]
    public void TwoTopAxes_SideBySide_PopulationTopExempt_TimeTopLeak()
    {
        // population-⊤（ω=⊤）：gate(1) 豁免 ⇒ 无 Leak；time-⊤（hi=⊤，ω=1）：gate(1) 权威判定 ⇒ Leak。
        // 同剧本并置，钉死两轴不同契约——结论相反是设计而非不一致（MA-002 旧表述「同一常驻语义」不成立）。
        var resident = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Top), Scene("S"),
            Signature.Of(Oc(Gpu("bg"), Mode.Use, Scene("S"), Interval.Exact(1))), LoopCount.Top);
        var forever = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Top), Scene("S"),
            Signature.Of(Oc(Gpu("leak"), Mode.Create, Scene("S"), Interval.Exact(1))), LoopCount.Of(1));

        var result = new EffectScript(ImmutableArray.Create(resident, forever)).Audit(Budget.None);

        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Gpu("leak"))));
        Assert.DoesNotContain(result.Violations, v => v.Resource.Equals(ResourceId.Normalize(Gpu("bg"))));
    }
}
