// QedP53RuntimeR2HardeningPins.cs — P5.3 Runtime 二轮审计处置钉
//（auditor-runtime-r2：SynchronousExitDrain 重入门 / 看门狗谓词隔离 / Graph 突变口收编）：
// ①重入关路径：逆 Action 重入 SynchronousExitDrain ⇒ 嵌套排空跳过正在回放的 fiber（无 R3-RT-04
//   假崩溃、无中途复位）——修复前嵌套 A3-10 补队会把回放中 fiber 再入队（红队 P10/P11）；
// ②看门狗谓词异常隔离：单 fiber 谓词抛异常只跳过该 fiber，同帧其余照常处理（与 OnSuspending 钩子隔离纪律对称）；
// ③Graph.NotifyDependents/Register 收编 internal（消公共突变后门与同 id 静默覆盖双账本失步）。
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class QedP53RuntimeR2HardeningPins
{
    static FiberSpec Spec(string id, ResourceId provides, ResourceId requires, params (ResourceId, Action)[] inv)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        foreach (var (r, a) in inv) stack = stack.Push(new InverseClaim(r, new ScopeId.Shell(), a));
        bool hasRelease = inv.Any(e => ResourceId.Normalize(e.Item1) == ResourceId.Normalize(provides));
        if (!hasRelease)
            stack = stack.Push(new InverseClaim(provides, new ScopeId.Shell(), () => { }));
        return new FiberSpec(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, new ScopeId.Shell()), stack);
    }

    // ── 钉 1：逆 Action 重入 SynchronousExitDrain ⇒ 无 R3-RT-04 假崩溃、无中途复位、
    //    fiber 正常 Dead（嵌套排空按 ReplayInProgress 跳过回放中的 fiber）。 ──
    [Fact]
    public void ReentrantExitDrain_NoFakeCrash_FiberDiesCleanly()
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () =>
            {
                if (rt.IsShuttingDown) rt.SynchronousExitDrain(); // 逆 Action 重入关路径（红队 P10 向量）
            })));
        rt.LoadAll();
        rt.BeginTeardown(f);
        rt.SynchronousExitDrain();

        Assert.Equal(FiberState.Dead, f.State);
        Assert.Empty(rt.CrashReports); // 无 R3-RT-04 假崩溃报告（修复前：fail-open 提前 Dead + 假报告）
        Assert.False(rt.IsShuttingDown); // 排空完成后复位
    }

    // ── 钉 2：嵌套排空不重入队回放中的 fiber（外层排空独占完成）。
    //    修复前：嵌套 A3-10 把 ReplayInProgress fiber 再入队 ⇒ R3-RT-04 loud 抛 + fail-open 提前 Dead。 ──
    [Fact]
    public void NestedDrain_SkipsReplayInProgressFiber()
    {
        var rt = new PluginRuntime();
        var reentrantSeen = false;
        var f = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () =>
            {
                if (rt.IsShuttingDown && !reentrantSeen)
                {
                    reentrantSeen = true;
                    rt.SynchronousExitDrain(); // 嵌套重入（恰在 f 回放期间）
                }
            })));
        rt.LoadAll();
        rt.BeginTeardown(f);
        rt.SynchronousExitDrain();

        Assert.Equal(FiberState.Dead, f.State);
        Assert.Empty(rt.CrashReports);
    }

    // ── 钉 3：看门狗谓词抛异常 ⇒ 仅跳过该 fiber，同帧其余 fiber 照常强制入队（与 OnSuspending 钩子隔离纪律对称）。 ──
    [Fact]
    public void Watchdog_PredicateException_IsolatedPerFiber()
    {
        var rt = new PluginRuntime();
        var boom = rt.Register(Spec("boom", new ResourceId.Memory(1), new ResourceId.Memory(0)));
        var ok = rt.Register(Spec("ok", new ResourceId.Memory(2), new ResourceId.Memory(0)));
        rt.LoadAll();

        rt.TickWatchdog(f =>
        {
            if (f.Id.Equals(boom.Id)) throw new InvalidOperationException("宿主谓词抛异常");
            return true;
        });

        // 谓词异常的 fiber：未被强制（本钉断言隔离语义——异常 fiber 的处置归属宿主）
        Assert.Equal(FiberState.Active, boom.State);
        // 正常 fiber 不受牵连：照常强制入队
        Assert.Equal(FiberState.TearingDown, ok.State);
    }
}
