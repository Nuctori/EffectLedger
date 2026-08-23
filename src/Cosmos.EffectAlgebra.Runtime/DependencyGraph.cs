// DependencyGraph.cs — §3 依赖图：显式/隐式边、硬/软环检测、拓扑排序（叶子优先/dependent-first）。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3 — 边种类：显式(同 Scope Requires⊇Provides) / 隐式(逆引用他 provider 资源，soft)。</summary>
public enum EdgeKind { Hard, Soft }

/// <summary>§3 — 环检测报告：仅硬边成环才中止（hard）；软边环降级 warning（soft）。</summary>
public sealed record CycleReport(
    bool HasHardCycle,
    ImmutableArray<FiberId> HardCycle,
    ImmutableArray<FiberId> SoftCycle);

/// <summary>§3 — 依赖图：B 依赖 A ⇔ 边 dependent=B → provider=A。</summary>
public sealed class DependencyGraph
{
    // 边：dependent → provider。provider 必须先于 dependent 释放（dependent-first teardown）。
    private readonly HashSet<(FiberId Dependent, FiberId Provider)> _hard = new();
    private readonly HashSet<(FiberId Dependent, FiberId Provider)> _soft = new();
    private readonly Dictionary<FiberId, Fiber> _fibers = new();

    /// <summary>§1/§3 — 注册 Fiber（用于拓扑排序解析依赖）。</summary>
    public void Register(Fiber f) => _fibers[f.Id] = f;

    /// <summary>§3 — 加显式边（同 Scope 内 B.Requires ⊇ A.Provides）。返回是否新增。</summary>
    public bool AddHardEdge(Fiber dependent, Fiber provider)
        => _hard.Add((dependent.Id, provider.Id));

    /// <summary>§3 step2 — 加隐式边（B 的某逆引用 A 提供的资源）。返回是否新增。</summary>
    public bool AddSoftEdge(Fiber dependent, Fiber provider)
        => _soft.Add((dependent.Id, provider.Id));

    /// <summary>§3 — 移除某 Fiber 的全部边（dead 后清理）。</summary>
    public void Remove(FiberId id)
    {
        _hard.RemoveWhere(e => e.Dependent == id || e.Provider == id);
        _soft.RemoveWhere(e => e.Dependent == id || e.Provider == id);
        _fibers.Remove(id);
    }

    /// <summary>§3 — 环检测：仅硬边成环才中止（HasHardCycle=true）；软边成环仅记 SoftCycle 降级 warning。</summary>
    public CycleReport DetectCycles()
    {
        var hardCycle = FindCycle(_hard);
        var softCycle = hardCycle.IsEmpty ? FindCycle(_soft) : ImmutableArray<FiberId>.Empty;
        return new CycleReport(!hardCycle.IsEmpty, hardCycle, softCycle);
    }

    /// <summary>§3/§6 — 拓扑排序（叶子优先 / dependent-first）：被依赖者(provider) 排在依赖者(dependent) 之后释放。
    /// 仅对硬边排序；软边环不参与（已降级）。返回卸载顺序（先释放最底层 dependent，最后释放根 provider）。</summary>
    public ImmutableArray<FiberId> TopoSortLeafFirst()
    {
        var indeg = new Dictionary<FiberId, int>();
        foreach (var f in _fibers.Keys) indeg[f] = 0;
        foreach (var (dep, prov) in _hard) if (indeg.ContainsKey(dep) && indeg.ContainsKey(prov)) indeg[dep]++;
        // Kahn：从 indeg==0（根 provider）开始，得 provider-first 拓扑序；teardown 需 dependent-first ⇒ 反转。
        var queue = new Queue<FiberId>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var topo = ImmutableArray.CreateBuilder<FiberId>();
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            topo.Add(n);
            foreach (var (dep, prov) in _hard)
                if (prov == n && indeg.ContainsKey(dep))
                {
                    if (--indeg[dep] == 0) queue.Enqueue(dep);
                }
        }
        // 若有硬环（拓扑序不完整），将剩余节点追加（调用方须先 DetectCycles 拒绝）。
        foreach (var f in _fibers.Keys) if (!topo.Contains(f)) topo.Add(f);
        // 反转 ⇒ dependent-first（叶子优先）：最底层 dependent 先释放，根 provider 最后。
        var result = ImmutableArray.CreateBuilder<FiberId>();
        for (int i = topo.Count - 1; i >= 0; i--) result.Add(topo[i]);
        return result.ToImmutable();
    }

    /// <summary>§3 — provider 通知其 dependents 进入 Suspending（provider-first-notify, R4-4 幂等）。</summary>
    public void NotifyDependents(Fiber provider)
    {
        foreach (var (dep, prov) in _hard)
            if (prov == provider.Id && _fibers.TryGetValue(dep, out var d))
                d.NotifyProviderTeardown();
        foreach (var (dep, prov) in _soft)
            if (prov == provider.Id && _fibers.TryGetValue(dep, out var d))
                d.NotifyProviderTeardown();
    }

    private static ImmutableArray<FiberId> FindCycle(HashSet<(FiberId, FiberId)> edges)
    {
        var adj = new Dictionary<FiberId, List<FiberId>>();
        foreach (var (dep, prov) in edges)
        {
            if (!adj.ContainsKey(prov)) adj[prov] = new();
            adj[prov].Add(dep);
        }
        var color = new Dictionary<FiberId, int>(); // 0=white 1=gray 2=black
        var stack = new Stack<FiberId>();
        foreach (var node in adj.Keys.Concat(edges.Select(e => e.Item2)).Distinct())
        {
            if (color.TryGetValue(node, out var c) && c != 0) continue;
            if (Dfs(node)) break;
        }
        return stack.ToImmutableArray();

        bool Dfs(FiberId u)
        {
            color[u] = 1; stack.Push(u);
            foreach (var v in adj.GetValueOrDefault(u, new()))
            {
                if (!color.TryGetValue(v, out var vc)) { if (Dfs(v)) return true; }
                else if (vc == 1) { stack.Push(v); return true; }
            }
            color[u] = 2; stack.Pop();
            return false;
        }
    }
}
