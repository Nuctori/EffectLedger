// ProviderCrashCascadeTests.cs — §6 provider 崩溃级联 TDD（reviewer spec D#8）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class ProviderCrashCascadeTests
{
    static (PluginRuntime rt, Fiber p, Fiber d) Scenario(bool pCrashes, bool dCrashes)
    {
        var rt = new PluginRuntime();
        var pInv = pCrashes
            ? new (ResourceId, Action)[] { (new ResourceId.Memory(0), () => throw new InvalidOperationException("boom")) }
            : new (ResourceId, Action)[] { (new ResourceId.Memory(0), () => { }) };
        var dInv = dCrashes
            ? new (ResourceId, Action)[] { (new ResourceId.Gpu(new Rid("a")), () => throw new InvalidOperationException("boom")) }
            : new (ResourceId, Action)[] { (new ResourceId.Gpu(new Rid("a")), () => { }) };
        var p = Make("p", new ResourceId.Memory(0), new ResourceId.Memory(0), pInv);
        var d = Make("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0), dInv);
        var pf = rt.Register(new FiberSpec(p.Id, p.Effect, p.Coeffect, p.Inverses));
        var df = rt.Register(new FiberSpec(d.Id, d.Effect, d.Coeffect, d.Inverses));
        rt.AddDependency(df, pf, EdgeKind.Hard); // d 依赖 p
        rt.LoadAll(); // 通过 rt 真正装载（ValidateForLoad + Load），返回实例即 rt 内实例
        return (rt, pf, df);
    }

    static Fiber Make(string id, ResourceId provides, ResourceId requires, params (ResourceId, Action)[] inv)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        foreach (var (r, a) in inv) stack = stack.Push(new InverseClaim(r, new ScopeId.Shell(), a));
        return new Fiber(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, new ScopeId.Shell()), stack);
    }

    [Fact]
    public void Handle_Crash_ProviderFailOpenToDead_AndCascadesDependent()
    {
        var (rt, p, d) = Scenario(pCrashes: true, dCrashes: false);
        rt.BeginTeardown(p); // → TearingDown（级联自动将 d 也 enqueue → TearingDown）

        var ex = new InvalidOperationException("boom");
        var report = ProviderCrashCascade.Handle(rt, p, ex);

        Assert.Equal(FiberState.Dead, p.State);       // fail-open：崩溃后直接 Dead
        Assert.Equal(p.Id, report.Provider);
        Assert.Same(ex, report.Exception);            // 异常上抛供升级
        Assert.Contains(d.Id, report.NotifiedDependents); // 从依赖图查到的真实依赖者
        Assert.True(d.TeardownEnqueued);              // 级联已将 d 入队（依赖者不泄漏）
        Assert.Equal(FiberState.TearingDown, d.State); // 级联推进 d → TearingDown（尚未 drain）
    }

    [Fact]
    public void BeginTeardown_Crash_ThenDrain_ContinuesOtherFibers()
    {
        bool dependentReleased = false;
        var rt = new PluginRuntime();
        var p = Make("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => throw new InvalidOperationException("boom")));
        var d = Make("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0),
            (new ResourceId.Gpu(new Rid("a")), () => dependentReleased = true));
        var pf = rt.Register(new FiberSpec(p.Id, p.Effect, p.Coeffect, p.Inverses));
        var df = rt.Register(new FiberSpec(d.Id, d.Effect, d.Coeffect, d.Inverses));
        rt.AddDependency(df, pf, EdgeKind.Hard);
        rt.LoadAll();

        rt.BeginTeardown(pf);  // 级联入队：p + d 都进队列
        rt.DrainTeardownBatch(); // 捕获 p 的异常，继续 d（按拓扑序排空）

        Assert.True(dependentReleased);          // d 释放成功（未因 p 崩溃而中断）
        Assert.Equal(FiberState.Dead, df.State); // d 经级联也 Dead
    }

    [Fact]
    public void Handle_CleanDead_NoException()
    {
        var (rt, p, d) = Scenario(pCrashes: false, dCrashes: false);
        rt.BeginTeardown(p); // → TearingDown（级联通知 d）
        var report = ProviderCrashCascade.Handle(rt, p, null);
        Assert.Equal(FiberState.Dead, p.State);
        Assert.Null(report.Exception);
        Assert.Contains(d.Id, report.NotifiedDependents); // 仍有依赖者 d（干净死也通知）
    }
}
