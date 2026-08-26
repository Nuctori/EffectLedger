// GraphScopeGuardTests.cs — R7-N5（hickey-x3）：DependencyGraph 直加边同样受同 Scope 前置约束，
// 堵住 PluginRuntime.Graph 公共后门绕过 AddDependency 校验的通道。可证伪：跨 Scope 边必抛。
using System;
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class GraphScopeGuardTests
{
    private static Fiber MakeFiber(string id, ScopeId scope)
    {
        var provides = new ResourceId.Memory(0);
        var inverses = ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(provides, scope, () => { }));
        return new Fiber(new FiberId(id), Signature.Empty, new Coeffect(provides, provides, scope), inverses);
    }

    [Fact]
    public void AddHardEdge_CrossScope_Throws()
    {
        var g = new DependencyGraph();
        var d = MakeFiber("d", new ScopeId.Shell());
        var p = MakeFiber("p", new ScopeId.Global());
        Assert.Throws<InvalidOperationException>(() => g.AddHardEdge(d, p));
    }

    [Fact]
    public void AddSoftEdge_CrossScope_Throws()
    {
        var g = new DependencyGraph();
        var d = MakeFiber("d", new ScopeId.Shell());
        var p = MakeFiber("p", new ScopeId.Global());
        Assert.Throws<InvalidOperationException>(() => g.AddSoftEdge(d, p));
    }

    [Fact]
    public void AddHardEdge_SameScope_Ok()
    {
        var g = new DependencyGraph();
        var d = MakeFiber("d", new ScopeId.Shell());
        var p = MakeFiber("p", new ScopeId.Shell());
        Assert.True(g.AddHardEdge(d, p));
    }

    [Fact]
    public void PluginRuntime_GraphBackdoor_CrossScope_Throws()
    {
        var rt = new PluginRuntime();
        var specP = new FiberSpec(new FiberId("p"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty.Push(
                new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { })));
        var specD = new FiberSpec(new FiberId("d"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(1), new ResourceId.Memory(1), new ScopeId.Global()),
            ImmutableStack<InverseClaim>.Empty.Push(
                new InverseClaim(new ResourceId.Memory(1), new ScopeId.Global(), () => { })));
        var p = rt.Register(specP);
        var d = rt.Register(specD);
        // 后门路径：绕过 AddDependency 直接改图 ⇒ 仍须被图内守卫拦截（R7-N5）
        Assert.Throws<InvalidOperationException>(() => rt.Graph.AddHardEdge(d, p));
    }
}
