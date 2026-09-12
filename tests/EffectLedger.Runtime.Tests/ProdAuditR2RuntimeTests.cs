// ProdAuditR2RuntimeTests.cs — 第二轮独立审计（2026-09）Runtime 回归钉。
// R2A-02：看门狗自愈分支（旁路 Unload 路径）须级联依赖者——provider 自愈 Dead 后依赖者不得仍 Active（use-after-free 同型窗口）。
using System;
using System.Collections.Immutable;
using System.Linq;
using EffectLedger;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class ProdAuditR2RuntimeTests
{
    static FiberSpec Spec(string id, ResourceId provides, ResourceId requires, params (ResourceId, Action)[] inv)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        foreach (var (r, a) in inv) stack = stack.Push(new InverseClaim(r, new ScopeId.Shell(), a));
        bool hasReleaseForProvides = inv.Any(e => ResourceId.Normalize(e.Item1) == ResourceId.Normalize(provides));
        if (!hasReleaseForProvides)
            stack = stack.Push(new InverseClaim(provides, new ScopeId.Shell(), () => { }));
        return new FiberSpec(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, new ScopeId.Shell()), stack);
    }

    [Fact]
    public void TickWatchdog_Rescue_CascadesDependents()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        d.Load();
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        p.Unload();                  // 旁路：无级联（宿主绕过 BeginTeardown）
        rt.TickWatchdog(_ => true);  // 自愈回收 p —— 须同帧级联 d（修改前：d 仍 Active 派发，永不 teardown）
        rt.DrainTeardownBatch();
        Assert.True(released, "看门狗自愈分支必须真正回放逆声明（否则资源永不回收，仅状态迁 Dead 是假回收）");
        Assert.Equal(FiberState.Dead, p.State);
        Assert.NotEqual(FiberState.Active, d.State); // 修改前：Active（自愈分支未 NotifyDependents/BeginTeardown）
        rt.DrainTeardownBatch();
        Assert.Equal(FiberState.Dead, d.State);
    }
}
