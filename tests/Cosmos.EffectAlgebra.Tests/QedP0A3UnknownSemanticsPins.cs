// QedP0A3UnknownSemanticsPins.cs — QED 迭代 P0-A3 Unknown 语义定稿钉（iter55 PO-55-05/06 收口）：
// Unknown 的三维契约——net/peak 按 +size 保守计入（泄漏/峰值检测不静默，PO-55-06 的「贡献恒 0」
// 是 PDR 旧公式遗漏而非实现缺陷；「Unknown ⇒ ⊤ 上界」被否决：有限 cap 下峰值门必爆=未映射 API
// 警报洪水）；Compatible 按 Use 最弱兼容放行（fail-open，25 组合矩阵已穷举钉单点，本文件补
// 扫换线端到端链路钉）；生命周期冲突（CONFLICT 集）为 A4 权威域，写写竞争移交 L2 写集分析（F 轨）。
// 决策记录：PDR §3.2.3 P4 / §3.3.1 / §8.1 / §14.3 A4【QED-A3】+ README 诚实边界 #1。xUnit。
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedP0A3UnknownSemanticsPins
{
    static ResourceId Mem() => new ResourceId.Memory(1);
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    // §8.1 默认规则的 L1 可测形状：occupy 载体 + mode=Unknown（未映射 API 的占用声明）。
    static EffectEvent UnknownOccupyEvent() => new(
        new Interval(NatStar.Of(0), NatStar.Of(10)), Scene("S"),
        Signature.Of(new Claim(Kind.Occupy, Mem(), Mode.Unknown, Scene("S"), Interval.Exact(1)).Normalize()),
        LoopCount.Of(1));

    static EffectScript ScriptOf(int k)
    {
        var b = ImmutableArray.CreateBuilder<EffectEvent>(k);
        for (int i = 0; i < k; i++) b.Add(UnknownOccupyEvent());
        return new EffectScript(b.ToArray());
    }

    // ── 钉 1（net 保守计入，PO-55-06 否决「⊤ 上界」的实证面）：未知 mode 占用无 release ⇒
    //    闭包 net 不含 0 ⇒ Leak——未映射 API 的泄漏不被静默吞（fail-open 仅限 Compatible 维度）。 ──
    [Fact]
    public void UnknownMode_Occupation_CountedInNet_LeakReported()
    {
        var result = ScriptOf(1).Audit(Budget.None);
        Assert.False(result.Passed);
        Assert.Contains(result.Violations, v => v.Kind == "Leak" && v.Resource.Equals(ResourceId.Normalize(Mem())));
    }

    // ── 钉 2（peak 计入）：未知 mode 占用计入峰值和——2 事件 cap=1 ⇒ PeakExceeded（峰值检测不静默）。 ──
    [Fact]
    public void UnknownMode_Occupation_CountedInPeak()
    {
        var caps = new System.Collections.Generic.Dictionary<ResourceId, NatStar>
        {
            [ResourceId.Normalize(Mem())] = NatStar.Of(1)
        };
        var result = ScriptOf(2).Audit(new Budget(caps));
        Assert.Contains(result.Violations, v => v.Kind == "PeakExceeded");
    }

    // ── 钉 3（Compatible fail-open，端到端）：同资源同 scope 双 Unknown 事件 ⇒ gate(3) 不报冲突——
    //    若 Unknown 改 fail-closed（对任意 mode 报冲突），本钉必红。泄漏（Leak）照常在。 ──
    [Fact]
    public void UnknownMode_PairwiseCompatible_NoConflictAlarm()
    {
        var result = ScriptOf(2).Audit(Budget.None);
        Assert.DoesNotContain(result.Violations, v => v.Kind == "CompatibleConflict");
        Assert.Contains(result.Violations, v => v.Kind == "Leak"); // 三维并存：占用在计、冲突不报
    }
}
