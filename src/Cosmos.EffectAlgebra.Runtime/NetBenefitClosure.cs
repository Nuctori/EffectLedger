// NetBenefitClosure.cs — §5 net 收益闭合接线：per-Fiber Signature → NetTable.Compute(sig, scope) → ContainsZero。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§5 — 单 Fiber 的 net 收益闭合诊断（声明维度等价；行为闭合由运行时权威 §5）。</summary>
public sealed record NetClosureResult(
    bool Conserved,
    ImmutableArray<ResourceId> ViolatingResources);

/// <summary>§5 — net 收益闭合接线：per-Fiber 计算其 Scope 内的有符号 net，验证每资源区间含 0（生命周期闭合）。
/// Scope 仅分组，绝不跨 Fiber 求和（R4-7）。fail-closed：任一资源 ⊤ 或未记录 ⇒ 不闭合（交人工确认）。</summary>
public static class NetBenefitClosure
{
    /// <summary>§5 — 对单 Fiber：NetTable.Compute(fiber.Effect, fiber.Scope) → 枚举资源 → IsConserved（含 0 即闭合）。</summary>
    public static NetClosureResult Check(Fiber fiber)
    {
        var net = NetTable.Compute(fiber.Effect, fiber.Scope); // 仅该 Fiber 自身 Scope，绝不跨 Fiber
        var violating = ImmutableArray.CreateBuilder<ResourceId>();
        foreach (var r in net.Resources)
            if (!net.IsConserved(r)) violating.Add(r); // 含 0 ⇒ 闭合；⊤/未记录 ⇒ 不闭合（fail-closed）
        return new NetClosureResult(violating.Count == 0, violating.ToImmutable());
    }

    /// <summary>§5 — 批量（每 Fiber 独立；Scope 仅分组，不汇总）。返回任一未闭合即 false。</summary>
    public static NetClosureResult CheckAll(IEnumerable<Fiber> fibers)
    {
        var violating = ImmutableArray.CreateBuilder<ResourceId>();
        bool allConserved = true;
        foreach (var f in fibers)
        {
            var r = Check(f);
            if (!r.Conserved) { allConserved = false; violating.AddRange(r.ViolatingResources); }
        }
        return new NetClosureResult(allConserved, violating.ToImmutable());
    }
}
