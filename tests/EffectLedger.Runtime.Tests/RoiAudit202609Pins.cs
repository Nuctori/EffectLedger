// RoiAudit202609Pins.cs — ROI 审计（2026-09-14）修复钉：
// ①TickWatchdog 自愈分支复用本帧 timedOut（原二次调用谓词在异常隔离外，宿主谓词有状态时异常逸出、
//   同帧其余 fiber 失去回收）；②SynchronousExitDrain 重入门（嵌套调用 no-op，仅最外层复位关闭标志——
//   此前嵌套空队列路径中途复位 IsShuttingDown，逆 Action 可借 LoadAll 激活未装载 fiber，退出后永久
//   Active 无 teardown 路径，audit/p5-qed-claim-attack.md 攻击 #2）；
// ③FlushExitDrain 的 OnDrainFault 自身异常隔离（上报通道故障不得阻断其余 drain 与清队）；
// ④GodotShell.Defer 宿主入队失败回滚去重登记（否则同 Action 重试被 _deferred 永久吞掉）。
using System.Collections.Generic;
using System.Collections.Immutable;
using EffectLedger;
using EffectLedger.Runtime;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class RoiAudit202609Pins
{
    static FiberSpec Spec(string id, ResourceId provides, ResourceId requires, params (ResourceId, Action)[] inv)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        foreach (var (r, a) in inv) stack = stack.Push(new InverseClaim(r, new ScopeId.Shell(), a));
        bool hasRelease = System.Linq.Enumerable.Any(inv, e => ResourceId.Normalize(e.Item1) == ResourceId.Normalize(provides));
        if (!hasRelease)
            stack = stack.Push(new InverseClaim(provides, new ScopeId.Shell(), () => { }));
        return new FiberSpec(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, new ScopeId.Shell()), stack);
    }

    // ── 钉 1（ROI-①）：自愈分支不得二次调用谓词——每帧每 Fiber 恰一次。 ──
    //    修复前：fiber 已 TearingDown 且不在队列时，else-if 再次调用 isTimedOut(f)（每帧第二次）。
    [Fact]
    public void Watchdog_PredicateCalledOncePerFiberPerTick()
    {
        var rt = new PluginRuntime();
        var bypass = rt.Register(Spec("bypass", new ResourceId.Memory(1), new ResourceId.Memory(0)));
        rt.LoadAll();     // 先装载（Inactive Unload 会直达 Dead，D4——进不了自愈分支）
        bypass.Unload(); // 旁路 Unload ⇒ TearingDown 且不在队列（自愈分支目标形态）
        var calls = new Dictionary<FiberId, int>();
        rt.TickWatchdog(f =>
        {
            calls.TryGetValue(f.Id, out var n);
            calls[f.Id] = n + 1;
            return true;
        });
        Assert.Equal(1, calls[bypass.Id]); // 修复前：2（主判定 + 自愈分支二次判定）
    }

    // ── 钉 2（ROI-①）：谓词首次 true、再次抛 ⇒ 异常不得逸出 TickWatchdog，同帧其余 fiber 照常回收。
    //    修复前：二次调用抛异常逸出帧循环，后续超时 fiber 永久 Active（逐 fiber 隔离承诺被绕过）。 ──
    [Fact]
    public void Watchdog_SecondPredicateCallWouldThrow_DoesNotEscape_OthersReaped()
    {
        var rt = new PluginRuntime();
        var a = rt.Register(Spec("a", new ResourceId.Memory(1), new ResourceId.Memory(0)));
        var b = rt.Register(Spec("b", new ResourceId.Memory(2), new ResourceId.Memory(0)));
        rt.LoadAll();             // a、b 都 Active（Inactive Unload 会直达 Dead，进不了自愈分支）
        a.Unload();               // a：旁路 Unload ⇒ TearingDown 不在队列 ⇒ 走自愈分支（旧代码在此二次调谓词）

        var aCalls = 0;
        var err = Record.Exception(() => rt.TickWatchdog(f =>
        {
            if (f.Id.Equals(a.Id)) { aCalls++; if (aCalls >= 2) throw new InvalidOperationException("宿主谓词第二次调用抛异常"); }
            return true;
        }));

        Assert.Null(err);                          // 异常不逸出
        Assert.Equal(1, aCalls);                   // 谓词每帧每 Fiber 恰一次（修复后自愈分支复用 timedOut）
        Assert.Equal(FiberState.TearingDown, a.State); // 自愈入队
        Assert.Equal(FiberState.TearingDown, b.State); // 同帧其余 fiber 不被牵连（修复前：仍 Active）
        rt.DrainTeardownBatch();
        Assert.Equal(FiberState.Dead, a.State);
        Assert.Equal(FiberState.Dead, b.State);
    }

    // ── 钉 3（ROI-②）：嵌套 SynchronousExitDrain 保持关闭态——LoadAll 被拒、未装载 fiber 不被激活、
    //    仅最外层复位标志。修复前：嵌套空队列路径中途复位 IsShuttingDown，逆 Action 内 LoadAll 把
    //    预注册未装载的 x 激活，退出结束后 x 永久 Active 且无 teardown 路径。 ──
    [Fact]
    public void NestedExitDrain_KeepsShuttingDown_LoadAllRejected_InactiveStaysInactive()
    {
        var rt = new PluginRuntime();
        var flagDuringNested = true;
        InvalidOperationException? loadErr = null;
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () =>
            {
                rt.SynchronousExitDrain();            // 逆 Action 嵌套重入退出排空
                flagDuringNested = rt.IsShuttingDown; // 必须仍为 true（修复前：被复位为 false）
                try { rt.LoadAll(); }                 // 关闭态下必须被拒（修复前：守卫失效，装载放行）
                catch (InvalidOperationException e) { loadErr = e; }
            })));
        rt.LoadAll();                                 // 仅 p 装载
        var x = rt.Register(Spec("x", new ResourceId.Memory(2), new ResourceId.Memory(0))); // 预注册未装载（Inactive）

        rt.SynchronousExitDrain();

        Assert.Equal(FiberState.Dead, p.State);       // 排空正常完成
        Assert.True(flagDuringNested);                // 嵌套期间关闭态保持（修复前：false）
        Assert.NotNull(loadErr);                      // LoadAll 在关闭态被拒
        Assert.Equal(FiberState.Inactive, x.State);   // 未装载 fiber 不被激活（修复前：Active 永久泄漏）
        Assert.False(rt.IsShuttingDown);              // 仅最外层结束后复位
        Assert.Empty(rt.CrashReports);                // 回放干净完成（LoadAll 异常已在逆 Action 内捕获）
    }

    // ── 钉 4（ROI-③）：OnDrainFault 自身抛异常 ⇒ 不逸出 FlushExitDrain，其余 drain 照常执行、末尾清队。
    //    修复前：上报通道故障直接逸出，剩余 drain（含 PluginRuntime 退出排空）全部不执行。 ──
    [Fact]
    public void FlushExitDrain_OnDrainFaultThrows_RemainingDrainsStillRun()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var secondRan = false;
        shell.OnDrainFault = _ => throw new InvalidOperationException("上报通道故障");
        shell.EnqueueExitDrain(() => throw new InvalidOperationException("drain boom"));
        shell.EnqueueExitDrain(() => secondRan = true);

        var err = Record.Exception(() => shell.FlushExitDrain());

        Assert.Null(err);        // 观测回调异常不逃逸
        Assert.True(secondRan);  // 后续 drain 照常执行
        Assert.True(shell.ExitDraining);
    }

    // ── 钉 5（ROI-④）：宿主入队失败 ⇒ 回滚去重登记并重抛；同 Action 重试成功执行一次。
    //    修复前：_deferred 先登记后入队、失败不回滚 ⇒ 重试被幂等守卫吞掉，Action 静默永不执行。 ──
    [Fact]
    public void Defer_HostEnqueueFails_RollbackDedup_RetrySucceeds()
    {
        var host = new FlakyEnqueueHost();
        var shell = new GodotShell(host);
        var ran = 0;
        Action a = () => ran++;

        Assert.Throws<InvalidOperationException>(() => shell.Defer(a)); // 首次入队失败 loud
        shell.Defer(a);                                                 // 重试同一 Action
        Assert.Single(host.Deferred);                                   // 未被去重集合吞掉（修复前：0）
        host.Deferred[0].Invoke();
        Assert.Equal(1, ran);                                           // 恰执行一次
    }

    // ── 钉 6（P5-10-09）：EnqueueExitDrain 同 Action 去重——重复注册不得双执行
    //    （drain 带资源释放语义，双执行可能双重释放）；不同 Action 各自执行；flush 后重注册属新周期。
    [Fact]
    public void EnqueueExitDrain_DeduplicatesSameAction_KeepsDistinct()
    {
        var shell = new GodotShell(new FakeHost());
        var count = 0;
        Action a = () => count++;
        Action b = () => count += 100;
        shell.EnqueueExitDrain(a);
        shell.EnqueueExitDrain(a); // 重复注册 ⇒ 去重（修复前：执行两次）
        shell.EnqueueExitDrain(b);
        shell.FlushExitDrain();
        Assert.Equal(101, count);

        // flush 后重注册同一 Action：新周期，应重新被接受（去重集已随队列清空）
        shell.EnqueueExitDrain(a);
        shell.FlushExitDrain();
        Assert.Equal(102, count);
    }

    // 首次 Defer 抛（可恢复故障），此后正常接管的宿主替身。
    private sealed class FlakyEnqueueHost : IHost
    {
        public bool FailNextDefer = true;
        public List<Action> Deferred { get; } = new();
        public void Defer(Action action)
        {
            if (FailNextDefer) { FailNextDefer = false; throw new InvalidOperationException("宿主入队故障"); }
            Deferred.Add(action);
        }
        public void DisableDispatch(FiberId id) { }
        public void EnableDispatch(FiberId id) { }
        public bool IsInstanceValid(object handle) => true;
    }
}
