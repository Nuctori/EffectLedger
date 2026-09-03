// ProdAuditBatch3Tests.cs — 独立生产就绪审计（2026-09）批 3 回归钉：Runtime 权威闭合层。
// 每条对应审计发现编号（A3-xx）：先红后绿（TDD），防漂移。
using System;
using System.Collections.Immutable;
using System.Linq;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class ProdAuditBatch3Tests
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

    // ── A3-01：直接 fiber.Unload() 后必须仍有路径真正入队逆回放（此前 TeardownEnqueued 说谎 ⇒ 永久卡死 + Register 锁死） ──
    [Fact]
    public void Unload_DirectCall_ThenBeginTeardown_ReachesDead_AndReleases()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll();
        f.Unload();                 // 规格语义：置 TearingDown（不承诺入队——入队是调度器职责）
        rt.BeginTeardown(f);        // 修改前：标志位说谎 ⇒ 早退，逆永不入队
        rt.DrainTeardownBatch();
        Assert.True(released);      // 修改前：false（回放从未执行）
        Assert.Equal(FiberState.Dead, f.State); // 修改前：永卡 TearingDown
        // Register 锁死解除：级联守卫只在 TearingDown 存在时拒绝
        rt.Register(Spec("q", new ResourceId.Gpu(new Rid("x")), new ResourceId.Gpu(new Rid("x")))); // 修改前：抛 InvalidOperationException
    }

    // ── A3-01b/A3-04：看门狗必须 rescue「TearingDown 但任务不在队列」的 fiber（直接 Unload 旁路 / 环跳过滞留） ──
    [Fact]
    public void TickWatchdog_RescuesTearingDownFiber_NotInQueue()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll();
        f.Unload();                    // 旁路：无人入队
        rt.TickWatchdog(_ => true);    // 看门狗自愈（修改前：排除 TearingDown ⇒ 永久滞留）
        rt.DrainTeardownBatch();
        Assert.Equal(FiberState.Dead, f.State);
        Assert.True(released);
    }

    // ── A3-04：动态硬环子集被跳过后，看门狗承诺的回收路径必须真实存在（逐 fiber 内联回放自愈） ──
    [Fact]
    public void TickWatchdog_RescuesCyclicSkippedFibers()
    {
        var rt = new PluginRuntime();
        var a = rt.Register(Spec("a", new ResourceId.Memory(1), new ResourceId.Memory(1)));
        var b = rt.Register(Spec("b", new ResourceId.Memory(2), new ResourceId.Memory(2)));
        rt.AddDependency(a, b, EdgeKind.Hard);
        rt.AddDependency(b, a, EdgeKind.Hard); // a⇄b 硬环
        a.Load(); b.Load();                    // 动态成环：边在装载后才形成（LoadAll 会拒载硬环，故手动 Load）
        rt.BeginTeardown(a);
        rt.DrainTeardownBatch();               // 环子集被跳过（保守语义保留，CrashReport 已记录）
        Assert.True(rt.CrashReports.Length > 0);
        Assert.Contains(rt.CrashReports, c => c.Exception!.Message.Contains("硬环"));
        rt.TickWatchdog(_ => true);            // 承诺的"看门狗另行回收"必须真存在（修改前：无任何路径）
        rt.DrainTeardownBatch();
        Assert.Equal(FiberState.Dead, a.State); // 修改前：永卡 TearingDown
        Assert.Equal(FiberState.Dead, b.State);
        rt.Register(Spec("q", new ResourceId.Gpu(new Rid("x")), new ResourceId.Gpu(new Rid("x")))); // Register 锁死解除
    }

    // ── A3-02：Inactive（从未装载）fiber 的 teardown 不得回放从未获取的资源（release-without-acquire = 双重释放类崩溃） ──
    [Fact]
    public void BeginTeardown_InactiveFiber_SkipsReplay_MarksDead()
    {
        bool released = false;
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        // 不 LoadAll —— _Ready 未跑，未获取任何资源
        rt.BeginTeardown(f);            // 修改前：照样入队逆回放
        rt.DrainTeardownBatch();
        Assert.False(released, "未装载 fiber 的逆不得执行（释放从未获取的资源）");
        Assert.Equal(FiberState.Dead, f.State); // D4：未 _Ready 也安全 ⇒ 直接 Dead
    }

    // ── A3-03：LoadAll 逆引用派生软边须按 provider Scope 过滤——跨 Scope 同名资源是合法配置，不得炸装载 ──
    [Fact]
    public void LoadAll_CrossScopeSameResource_DoesNotThrow()
    {
        var rt = new PluginRuntime();
        // p1: Shell scope 提供 Memory(0)；p2: Scene scope 提供同名 Memory(0)（合法，R5-6 跨 Scope 同资源通过）
        var p1 = rt.Register(Spec("p1", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var p2Spec = new FiberSpec(new FiberId("p2"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(9), new ResourceId.Memory(0), new ScopeId.Scene("S")),
            ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Scene("S"), () => { })));
        var p2 = rt.Register(p2Spec);
        // d: Shell scope，逆释放 Memory(0)（带 release-class 标签——同 Scope 释放 p1 的资源须 R5-6 防护）+ 释放自身 Provides（§5 生命周期闭合）
        var dStack = ImmutableStack<InverseClaim>.Empty
            .Push(new InverseClaim(new ResourceId.Gpu(new Rid("d")), new ScopeId.Shell(), () => { }))
            .Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { }, new HashSet<string> { "queue_free" }));
        var dSpec = new FiberSpec(new FiberId("d"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(8), new ResourceId.Gpu(new Rid("d")), new ScopeId.Shell()), dStack);
        var d = rt.Register(dSpec);
        var ex = Record.Exception(() => rt.LoadAll());
        // 修改前：InvalidOperationException（AddSoftEdge → ValidateSameScope，跨 Scope 派生未过滤），且异常类型错（非 LoadValidationException）
        Assert.Null(ex);
        rt.DrainTeardownBatch();
    }

    // ── A3-05：Defer 闭包执行时必须复查 _exitDraining——退出前入队、退出后执行的回调不得照跑（use-after-free 窗口） ──
    [Fact]
    public void Defer_QueuedBeforeExit_NotExecutedAfterFlush()
    {
        bool ran = false;
        var host = new FakeHost();
        var shell = new GodotShell(host);
        shell.Defer(() => ran = true);       // 退出【前】入队
        shell.FlushExitDrain();              // 退出期开始（_exitDraining=true）
        host.FlushDeferred();                // 宿主帧晚于退出才排空 call_deferred 队列
        Assert.False(ran, "退出期开始后执行 Defer 闭包 = use-after-free（§3 R4-1）；入队时判一次不够，执行时须复查");
    }

    [Fact]
    public void Defer_QueuedBeforeExit_WithHandle_NotExecutedAfterFlush()
    {
        bool ran = false;
        var host = new FakeHost();
        var shell = new GodotShell(host);
        shell.Defer(() => ran = true, new object()); // 带句柄变体同复查
        shell.FlushExitDrain();
        host.FlushDeferred();
        Assert.False(ran);
    }

    // ── A3-08：部分逆释放升级时必须保留原始异常为 InnerException（根因堆栈不得丢失） ──
    [Fact]
    public void PartialReplayFailure_PreservesOriginalException_AsInner()
    {
        var rt = new PluginRuntime();
        var boom = new InvalidOperationException("boom-根因");
        var stack = ImmutableStack<InverseClaim>.Empty
            .Push(new InverseClaim(new ResourceId.Memory(3), new ScopeId.Shell(), () => throw boom))
            .Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { }));
        rt.Register(new FiberSpec(new FiberId("p"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(9), new ResourceId.Memory(0), new ScopeId.Shell()), stack));
        rt.LoadAll();
        rt.BeginTeardown(rt.Fibers.First());
        rt.DrainTeardownBatch();
        var rep = rt.LastCrashReport;
        Assert.NotNull(rep);
        // 修改前：重新合成的异常不带 InnerException，根因类型/堆栈永久丢失
        Assert.Same(boom, rep!.Exception!.InnerException);
    }

    // ── A3-10：退出路径须对全部存活 fiber 补「标记+入队」（规格 §3 step8）——宿主漏调 BeginTeardown 不得假绿式退出 ──
    [Fact]
    public void SynchronousExitDrain_AutoEnqueuesActiveFibers()
    {
        bool r1 = false, r2 = false;
        var rt = new PluginRuntime();
        var a = rt.Register(Spec("a", new ResourceId.Memory(1), new ResourceId.Memory(1), (new ResourceId.Memory(1), () => r1 = true)));
        var b = rt.Register(Spec("b", new ResourceId.Memory(2), new ResourceId.Memory(2), (new ResourceId.Memory(2), () => r2 = true)));
        rt.LoadAll();
        // 宿主忘了对每个 fiber 调 BeginTeardown，直接 _ExitTree → SynchronousExitDrain
        rt.SynchronousExitDrain();
        Assert.True(r1, "退出时 Active fiber 的逆未回放（假绿式退出）");
        Assert.True(r2);
        Assert.Equal(FiberState.Dead, a.State); // 修改前：保持 Active
        Assert.Equal(FiberState.Dead, b.State);
    }

    // ── A3-11：FindCycle 须回卷收集真环——入环路径（非环节点）不得记入 HardCycle ──
    [Fact]
    public void DetectCycles_HardCycle_ExcludesEntryPath()
    {
        var g = new DependencyGraph();
        var fd = new Fiber(new FiberId("d"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(1), new ResourceId.Memory(1), new ScopeId.Shell()), ImmutableStack<InverseClaim>.Empty);
        var fa = new Fiber(new FiberId("a"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(2), new ResourceId.Memory(2), new ScopeId.Shell()), ImmutableStack<InverseClaim>.Empty);
        var fb = new Fiber(new FiberId("b"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(3), new ResourceId.Memory(3), new ScopeId.Shell()), ImmutableStack<InverseClaim>.Empty);
        g.Register(fd); g.Register(fa); g.Register(fb);
        g.AddHardEdge(fd, fa); // d→a
        g.AddHardEdge(fa, fb); // a→b
        g.AddHardEdge(fb, fa); // b→a ⇒ 真环 a⇄b
        var r = g.DetectCycles();
        Assert.True(r.HasHardCycle);
        Assert.DoesNotContain(fd.Id, r.HardCycle); // 修改前：整条 DFS 路径 d 被误记入环
        Assert.Contains(fa.Id, r.HardCycle);
        Assert.Contains(fb.Id, r.HardCycle);
    }

    // ── A3-13：AttachShell 须幂等/防重——二次接线同一 shell no-op，跨实例拒绝 ──
    [Fact]
    public void AttachShell_TwiceSameShell_NoDoubleDrainRegistration()
    {
        var rt = new PluginRuntime();
        var shell = new GodotShell(new FakeHost());
        rt.AttachShell(shell);
        rt.AttachShell(shell); // 修改前：SynchronousExitDrain 双入队 + OnSuspending 静默覆盖
        var fld = typeof(GodotShell).GetField("_exitDrains", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (System.Collections.Generic.List<Action>)fld!.GetValue(shell)!;
        Assert.Equal(1, list.Count(d => d.Method.DeclaringType == typeof(PluginRuntime)));
        var other = new GodotShell(new FakeHost());
        Assert.Throws<InvalidOperationException>(() => rt.AttachShell(other));
    }

    // ── A3-15：壳上第三方 drain 异常不得凭空消失——OnDrainFault 可观测出口 ──
    [Fact]
    public void FlushExitDrain_ThirdPartyDrainFault_Observable()
    {
        Exception? observed = null;
        var shell = new GodotShell(new FakeHost());
        shell.OnDrainFault = ex => observed = ex;
        shell.EnqueueExitDrain(() => throw new InvalidOperationException("drain-爆炸"));
        shell.FlushExitDrain(); // 修改前：裸 catch{} 吞掉，零出口
        Assert.NotNull(observed);
        Assert.Contains("drain-爆炸", observed!.Message);
    }
}
