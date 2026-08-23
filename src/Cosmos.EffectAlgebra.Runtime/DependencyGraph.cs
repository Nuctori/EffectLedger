// DependencyGraph.cs — §3 依赖图（MVP 骨架）。硬边/软边分离、拓扑排序、环检测。
namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3 — 边种类：显式(同 Scope Requires⊇Provides) / 隐式(逆引用他 provider 资源，soft)。</summary>
public enum EdgeKind { Hard, Soft }

/// <summary>§3 — 环检测报告：仅硬边成环才中止；软边环降级 warning。</summary>
public sealed record CycleReport(ImmutableArray<FiberId> HardCycle, ImmutableArray<FiberId> SoftCycle);

/// <summary>§3 — 依赖图（MVP 骨架）。</summary>
public sealed class DependencyGraph
{
    private readonly HashSet<(FiberId Dependent, FiberId Provider)> _edges = new();

    /// <summary>§3 — 加边（幂等）。返回是否新增。</summary>
    public bool TryAddEdge(Fiber dependent, Fiber provider, EdgeKind kind)
        => _edges.Add((dependent.Id, provider.Id));

    /// <summary>§3 — 拓扑排序（叶子优先/dependent-first）。MVP 骨架：返回注册 provider 顺序。</summary>
    public ImmutableArray<Fiber> TopoSortLeafFirst() => ImmutableArray<Fiber>.Empty;

    /// <summary>§3 — 环检测（仅硬边成环中止；软边环降级 warning）。</summary>
    public CycleReport DetectHardCycles() => new(ImmutableArray<FiberId>.Empty, ImmutableArray<FiberId>.Empty);
}
