// PluginRuntimeTests.cs — §3/§6 调度器 TDD（reviewer spec D#5/#6）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class PluginRuntimeTests
{
    static FiberSpec Spec(string id, ResourceId provides, ResourceId requires, params (ResourceId, Action)[] inv)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        foreach (var (r, a) in inv) stack = stack.Push(new InverseClaim(r, new ScopeId.Shell(), a));
        return new FiberSpec(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, new ScopeId.Shell()), stack);
    }

    [Fact]
    public void Register_ThenLoadAll_Activates()
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        rt.LoadAll();
        Assert.Equal(FiberState.Active, f.State);
    }

    [Fact]
    public void BeginTeardown_CascadesProviderAndDependentToTearingDown()
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        d.Load();
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p);                  // provider 进入 TearingDown + 级联自动入队 dependent
        Assert.Equal(FiberState.TearingDown, p.State);
        Assert.True(d.TeardownEnqueued);      // 级联已将 dependent 入队（不泄漏）
        Assert.Equal(FiberState.TearingDown, d.State); // 依赖者经级联推进至 TearingDown（Suspending→TearingDown 由 Unload 放行）
    }

    [Fact]
    public void BeginTeardown_DependentThenDrain_ReachesDead()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        d.Load();
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p);  // p→TearingDown，并级联 d→TearingDown
        rt.DrainTeardownBatch();
        Assert.True(released);          // p 释放
        Assert.Equal(FiberState.Dead, d.State); // d 经级联 teardown 达 Dead
    }

    [Fact]
    public void BeginTeardown_DoubleCall_PreventsReEnqueue()
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        rt.LoadAll();
        rt.BeginTeardown(p);
        rt.BeginTeardown(p);                  // 二次调用：TeardownEnqueued 防重
        Assert.Equal(FiberState.TearingDown, p.State);
    }

    [Fact]
    public void DrainTeardownBatch_ReplaysAndMarksDead()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll();
        rt.BeginTeardown(p);
        rt.DrainTeardownBatch();
        Assert.True(released);
        Assert.Equal(FiberState.Dead, p.State);
    }

    [Fact]
    public void RecomputeTopology_OnShutdownPath_HardRejects()
    {
        var rt = new PluginRuntime();
        rt.IsShuttingDown = true;
        Assert.Throws<InvalidOperationException>(() => rt.RecomputeTopology());
    }

    [Fact]
    public void Register_OnShutdownPath_Rejects()
    {
        var rt = new PluginRuntime();
        rt.IsShuttingDown = true;
        Assert.Throws<InvalidOperationException>(() =>
            rt.Register(Spec("x", new ResourceId.Memory(0), new ResourceId.Memory(0))));
    }

    [Fact]
    public void SynchronousExitDrain_MarksDead()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll();
        rt.BeginTeardown(p);
        rt.SynchronousExitDrain();
        Assert.True(released);
        Assert.Equal(FiberState.Dead, p.State);
    }

    [Fact]
    public void AddDependency_BackfillsProviderDependents() // reviewer #185 medium #3：否则 Godot 壳级联遍历空集 no-op
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Soft);
        Assert.Contains(d.Id, p.Dependents); // provider 的 Dependents 被回填（供 Godot 壳 ProcessMode 级联）
    }

    [Fact]
    public void ShouldDispatch_OnlyWhenActive() // reviewer #185 medium #4：仅 Active 派发门控
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        rt.LoadAll();
        Assert.True(PluginRuntime.ShouldDispatch(f));   // Active ⇒ 可派发
        rt.BeginTeardown(f);                            // → TearingDown（级联）
        Assert.False(PluginRuntime.ShouldDispatch(f));  // 非 Active ⇒ 不派发
    }

    [Fact]
    public void LoadAll_RejectsHardCycle() // reviewer #187 blocker：装载期硬环拒载（否则 teardown 死锁/泄漏）
    {
        var rt = new PluginRuntime();
        var a = rt.Register(Spec("a", new ResourceId.Memory(0), new ResourceId.Memory(1)));
        var b = rt.Register(Spec("b", new ResourceId.Memory(1), new ResourceId.Memory(0)));
        rt.AddDependency(a, b, EdgeKind.Hard); // a 依赖 b
        rt.AddDependency(b, a, EdgeKind.Hard); // b 依赖 a ⇒ A→B→A 硬环
        Assert.Throws<LoadValidationException>(() => rt.LoadAll());
    }

    [Fact]
    public void SynchronousExitDrain_ReleasesDependentBeforeProvider() // reviewer #187 F3：关闭排空按 dependent-first
    {
        var order = new System.Collections.Generic.List<string>();
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => order.Add("p"))));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0),
            (new ResourceId.Gpu(new Rid("a")), () => order.Add("d"))));
        rt.AddDependency(d, p, EdgeKind.Hard); // d 依赖 p
        rt.LoadAll();
        rt.BeginTeardown(p); // 级联入队 p + d
        rt.SynchronousExitDrain();
        Assert.Equal(new[] { "d", "p" }, order); // dependent(d) 先于 provider(p) 释放
    }

    [Fact]
    public void DrainTeardownBatch_CrashEscalatesViaProviderCrashCascade() // reviewer #187 F-provider：崩溃不再是 catch{} 吞掉
    {
        var rt = new PluginRuntime();
        bool dependentReleased = false;
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => throw new InvalidOperationException("boom"))));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0),
            (new ResourceId.Gpu(new Rid("a")), () => dependentReleased = true)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p); // 级联：p(崩溃) + d 都入队
        rt.DrainTeardownBatch(); // p 的异常应经 ProviderCrashCascade.Handle 升级；d 仍被释放
        Assert.True(dependentReleased);
        Assert.Equal(FiberState.Dead, d.State);
        Assert.Equal(FiberState.Dead, p.State); // fail-open：崩溃后 Handle 标记 Dead
    }

    [Fact]
    public void TickWatchdog_ForcesTimedOutFiberToTearingDown() // reviewer #187 F5：看门狗帧时钟驱动
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        rt.LoadAll(); // Active
        Assert.Equal(FiberState.Active, f.State);
        rt.TickWatchdog(fib => fib.State == FiberState.Active); // 超时 ⇒ 强制 TearingDown
        Assert.Equal(FiberState.TearingDown, f.State);
    }
}
