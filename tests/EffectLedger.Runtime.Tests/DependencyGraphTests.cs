// DependencyGraphTests.cs — §3 依赖图拓扑 TDD（reviewer spec D#3/#4/#5）。
using System.Collections.Immutable;
using EffectLedger;
using EffectLedger.Runtime;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class DependencyGraphTests
{
    static Fiber Fiber(string id, ResourceId provides, ResourceId requires, ScopeId scope)
        => new(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, scope), ImmutableStack<InverseClaim>.Empty);

    static DependencyGraph Graph(params Fiber[] fibers)
    {
        var g = new DependencyGraph();
        foreach (var f in fibers) g.Register(f);
        return g;
    }

    [Fact]
    public void AddHardEdge_SameScope_RequiresSuperset_Registers()
    {
        var provider = Fiber("p", new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell());
        var dependent = Fiber("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0), new ScopeId.Shell());
        var g = Graph(provider, dependent);
        Assert.True(g.AddHardEdge(dependent, provider));
        Assert.False(g.AddHardEdge(dependent, provider)); // 幂等
    }

    [Fact]
    public void DetectCycles_HardCycle_Blocks()
    {
        var a = Fiber("a", new ResourceId.Memory(0), new ResourceId.Memory(1), new ScopeId.Shell());
        var b = Fiber("b", new ResourceId.Memory(1), new ResourceId.Memory(0), new ScopeId.Shell());
        var g = Graph(a, b);
        g.AddHardEdge(a, b); // a 依赖 b（需 memory1，b 提供）
        g.AddHardEdge(b, a); // b 依赖 a（需 memory0，a 提供）→ 硬环
        var r = g.DetectCycles();
        Assert.True(r.HasHardCycle);
        Assert.NotEmpty(r.HardCycle);
    }

    [Fact]
    public void DetectCycles_SoftCycle_OnlyWarning()
    {
        var a = Fiber("a", new ResourceId.Memory(0), new ResourceId.Memory(1), new ScopeId.Shell());
        var b = Fiber("b", new ResourceId.Memory(1), new ResourceId.Memory(0), new ScopeId.Shell());
        var g = Graph(a, b);
        g.AddSoftEdge(a, b);
        g.AddSoftEdge(b, a); // 软环 → 仅 warning，不阻塞
        var r = g.DetectCycles();
        Assert.False(r.HasHardCycle);
        Assert.NotEmpty(r.SoftCycle); // 软环被记录
    }

    [Fact]
    public void TopoSortLeafFirst_DependentBeforeProvider()
    {
        // d 依赖 p（d 需 memory0，p 提供）。卸载顺序：d 先于 p（dependent-first）。
        var p = Fiber("p", new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell());
        var d = Fiber("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0), new ScopeId.Shell());
        var g = Graph(p, d);
        g.AddHardEdge(d, p);
        var order = g.TopoSortLeafFirst();
        Assert.True(order.IndexOf(new FiberId("d")) < order.IndexOf(new FiberId("p")));
    }

    [Fact]
    public void TopoSortLeafFirst_NoHardCycle_AllNodesOrdered()
    {
        var p = Fiber("p", new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell());
        var d = Fiber("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0), new ScopeId.Shell());
        var g = Graph(p, d);
        g.AddHardEdge(d, p);
        var order = g.TopoSortLeafFirst();
        Assert.Equal(2, order.Length);
    }

    [Fact]
    public void NotifyDependents_ProviderNotifies_ActiveDependentToSuspending()
    {
        var p = Fiber("p", new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell());
        var d = Fiber("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0), new ScopeId.Shell());
        d.Load();
        var g = Graph(p, d);
        g.AddHardEdge(d, p);
        g.NotifyDependents(p); // provider-first-notify
        Assert.Equal(FiberState.Suspending, d.State); // Active → Suspending（幂等 R4-4）
    }

    [Fact]
    public void Remove_ClearsEdges()
    {
        var p = Fiber("p", new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell());
        var d = Fiber("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0), new ScopeId.Shell());
        var g = Graph(p, d);
        g.AddHardEdge(d, p);
        g.Remove(p.Id);
        var r = g.DetectCycles();
        Assert.False(r.HasHardCycle); // 移除后无环
    }
}
