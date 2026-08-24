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
        // §5（reviewer #188 F1）：若未显式声明释放 Provides 的逆，默认追加「释放 Provides」逆，使 Fiber 生命周期闭合（提供即释放），闸门不会误拒。
        bool hasReleaseForProvides = inv.Any(e => ResourceId.Normalize(e.Item1) == ResourceId.Normalize(provides));
        if (!hasReleaseForProvides)
            stack = stack.Push(new InverseClaim(provides, new ScopeId.Shell(), () => { }));
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

    [Fact]
    public void TickWatchdog_EnqueuesReplayTask_ReclaimsResource() // reviewer #188 F-Tick：看门狗强制态后须真正入队逆回放，否则资源永不回收
    {
        bool released = false;
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll(); // Active
        rt.TickWatchdog(fib => fib.State == FiberState.Active); // 超时 ⇒ TearingDown + 入队
        Assert.Equal(FiberState.TearingDown, f.State);
        rt.DrainTeardownBatch(); // 排空看门狗入队的逆回放任务
        Assert.True(released);   // 逆回放真执行 ⇒ 资源回收
        Assert.Equal(FiberState.Dead, f.State);
    }

    [Fact]
    public void DrainTeardownBatch_PartialReleaseEscalatesViaProviderCrashCascade() // reviewer #188 F2：部分逆释放失败（AllCompleted=false）升级 Handle
    {
        var rt = new PluginRuntime();
        bool notified = false;
        // p 提供 Memory(0)（默认自动释放逆）与 Memory(1)；其中释放 Memory(1) 的逆抛异常 ⇒ 部分失败。
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => { }),                    // 成功释放 Memory(1)
            (new ResourceId.Memory(1), () => throw new InvalidOperationException("partial")))); // 失败：同资源二次逆抛异常 ⇒ 部分释放
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0),
            (new ResourceId.Gpu(new Rid("a")), () => notified = true)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p); // 级联入队 p（部分失败）+ d
        rt.DrainTeardownBatch();
        // p 部分释放失败 ⇒ Handle 升级：p 标记 Dead（fail-open）+ 依赖者 d 经级联完全 teardown。
        Assert.Equal(FiberState.Dead, p.State);       // 升级后 Handle 标记 Dead（不再静默 MarkDead 掩盖部分失败）
        Assert.True(notified);                          // d 的逆回放仍执行（崩溃级联兜底）
        Assert.Equal(FiberState.Dead, d.State);         // 依赖者经级联完全 teardown
    }

    [Fact]
    public void LoadAll_AutoDerivesSoftEdge_FromInverseToProviderResource() // reviewer #187 F4：逆引用自动派生软边，使 NotifyDependents 通知到软依赖者
    {
        var rt = new PluginRuntime();
        // p 提供 Memory(0)；d 不显式 AddDependency，但其逆释放 Memory(0)（同 Scope）⇒ 软依赖 p。
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        // d 提供 Gpu(a)（自有释放 ⇒ 闭合）+ 借用 p 的 Memory(0)（跨 Fiber 逆释放须标注 R5-6 release-class 标签）。
        var dSpec = new FiberSpec(new FiberId("d"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Gpu(new Rid("a")), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty
                .Push(new InverseClaim(new ResourceId.Gpu(new Rid("a")), new ScopeId.Shell(), () => { }))           // 自有释放 ⇒ §5 闭合
                .Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { }, ImmutableHashSet.Create("queue_free")))); // 跨 Fiber 逆释放 p 的 Memory(0)，标注标签
        var d = rt.Register(dSpec);
        rt.LoadAll(); // 自动派生软边 d→p
        // 软边存在于图中 ⇒ NotifyDependents(p) 通知到 d。
        Assert.Contains(d.Id, rt.Graph.DependentsOf(p.Id).ToArray());
        rt.BeginTeardown(p); // provider 级联通知 + 递归 teardown（含软依赖）⇒ d 推进至 TearingDown
        Assert.Equal(FiberState.TearingDown, d.State);
    }
}
