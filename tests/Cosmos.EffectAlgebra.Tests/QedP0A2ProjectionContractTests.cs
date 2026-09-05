// QedP0A2ProjectionContractTests.cs — QED 迭代 P0-A2 双计数语义差异契约钉（iter55 PO-55-13 文档面见 PDR §14.3）：
// At(t) = 集合投影（**在场语义**：Signature 刻意幂等 ⇒ 同刻逐字段相同的重复事件计 1，K 无关）；
// Audit 扫换线 = **计数语义**（net/Peak 逐事件累加）。双语义是 QED-A5 多重性载体决策的直接后果而非缺陷：
// 统一化（Signature → Multiset）已被 P0-4 构造期重复拒结构性封死——契约即「At 在场 / Audit 计数」，
// 自建核对脚本禁止用 At+Peak 对账（README 诚实边界 #5）。xUnit。
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedP0A2ProjectionContractTests
{
    static ResourceId Mem() => new ResourceId.Memory(1);
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    static EffectEvent UseEvent() => new(
        new Interval(NatStar.Of(0), NatStar.Of(10)), Scene("S"),
        Signature.Of(new Claim(Kind.Occupy, Mem(), Mode.Use, Scene("S"), Interval.Exact(1)).Normalize()),
        LoopCount.Of(1));

    static EffectScript ScriptOf(int k)
    {
        var b = ImmutableArray.CreateBuilder<EffectEvent>(k);
        for (int i = 0; i < k; i++) b.Add(UseEvent());
        return new EffectScript(b.ToArray());
    }

    // ── 钉 1（在场语义）：At(t) 对 K 个同构重叠事件恒计 1——与 K 无关（集合投影，QED-A5 幂等决策面）。 ──
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void At_IsSetProjection_IdenticalEventsCollapseToSingleClaim(int k)
    {
        Assert.Single(ScriptOf(k).At(NatStar.Of(5)).OccupyClaims);
    }

    // ── 钉 2（计数语义）：同一剧本形状（K=3）的峰值门逐事件计数——cap=K−1 报 PeakExceeded、cap=K 放行
    //    （严格大于）。At 投影计 1 而峰值实为 3：双语义对照在同一剧本上并存，即 A2 契约本体。 ──
    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Audit_SweepLine_CountsEveryEvent_PeakAuthoritative(ulong cap, bool expectExceeded)
    {
        var caps = new System.Collections.Generic.Dictionary<ResourceId, NatStar>
        {
            [ResourceId.Normalize(Mem())] = NatStar.Of(cap)
        };
        var result = ScriptOf(3).Audit(new Budget(caps));

        Assert.Equal(expectExceeded, result.Violations.Any(v => v.Kind == "PeakExceeded"));
    }
}
