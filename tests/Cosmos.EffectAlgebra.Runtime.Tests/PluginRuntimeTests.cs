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
        // reviewer #189 F3：自动派生的软边须回填 provider.Dependents（供 Godot 壳 ProcessMode 级联遍历）。
        Assert.Contains(d.Id, p.Dependents);
        rt.BeginTeardown(p); // provider 级联通知 + 递归 teardown（含软依赖）⇒ d 推进至 TearingDown
        Assert.Equal(FiberState.TearingDown, d.State);
    }

    [Fact]
    public void DrainTeardownBatch_PartialRelease_PopulatesLastCrashReport() // reviewer #189 F1：崩溃升级须可观测（LastCrashReport），否则升级路径不可证伪
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => { }),
            (new ResourceId.Memory(1), () => throw new InvalidOperationException("partial"))));
        rt.LoadAll();
        rt.BeginTeardown(p);
        rt.DrainTeardownBatch();
        Assert.NotNull(rt.LastCrashReport);                 // 升级确有消费方（可观测）
        Assert.Equal(p.Id, rt.LastCrashReport!.Provider);    // 记录崩溃 provider
        Assert.NotNull(rt.LastCrashReport!.Exception);       // 异常随级联上抛
    }

    [Fact]
    public void TickWatchdog_CascadesDependentsOfTimedOutProvider() // reviewer #189 F2：看门狗对超时 provider 须级联依赖者，否则依赖者仍 Active 派发/永不回收
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll(); // p, d 均 Active
        // 仅 p 超时（provider）
        rt.TickWatchdog(fib => fib.Id == p.Id);
        Assert.Equal(FiberState.TearingDown, p.State);    // provider 被强制回收
        Assert.Equal(FiberState.TearingDown, d.State);    // 依赖者经看门狗级联推进（不再滞留 Active）
    }

    [Fact]
    public void DrainTeardownBatch_AccumulatesCrashReports_NotOverwrite() // reviewer #190 F1：单批多 Fiber 失败须累积，不丢早期失败
    {
        var rt = new PluginRuntime();
        var p1 = rt.Register(Spec("p1", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => throw new InvalidOperationException("boom1"))));
        var p2 = rt.Register(Spec("p2", new ResourceId.Memory(2), new ResourceId.Memory(0),
            (new ResourceId.Memory(2), () => throw new InvalidOperationException("boom2"))));
        rt.LoadAll();
        rt.BeginTeardown(p1);
        rt.BeginTeardown(p2);
        rt.DrainTeardownBatch();
        Assert.Equal(2, rt.CrashReports.Length);            // 两条崩溃均保留（累积而非覆盖）
        Assert.NotNull(rt.LastCrashReport);                 // 仍可取末条
    }

    [Fact]
    public void SynchronousExitDrain_PartialRelease_PopulatesCrashReport() // reviewer #190 F2/F3：退出路径崩溃须可观测（与 DrainTeardownBatch 一致）
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => { }),
            (new ResourceId.Memory(1), () => throw new InvalidOperationException("partial"))));
        rt.LoadAll();
        rt.BeginTeardown(p);
        rt.SynchronousExitDrain();                          // 关闭路径排空
        Assert.NotNull(rt.LastCrashReport);                 // 退出路径也填充崩溃报告
        Assert.Single(rt.CrashReports);                     // 退出路径崩溃被记录（xUnit2013 合规）
    }

    [Fact]
    public void LoadAll_RecordsSoftCycle_WhenSoftCyclePresent() // reviewer #190 F3：软环降级 warning 须可观测（不实落地）
    {
        var rt = new PluginRuntime();
        // a 提供 Memory(0)（自有释放 Memory(0) ⇒ §5 自闭合）+ 逆释放 Memory(1)（b 的 Provides ⇒ 软边 a→b）；
        // b 提供 Memory(1)（自有释放 Memory(1) ⇒ §5 自闭合）+ 逆释放 Memory(0)（a 的 Provides ⇒ 软边 b→a）；
        // ⇒ a⇄b 软环（各自额外逆释放对方提供物，同 Scope），且各自 §5 守恒不因跨 Fiber 逆释放被误拒。
        var aSpec = new FiberSpec(new FiberId("a"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty
                .Push(new InverseClaim(new ResourceId.Memory(1), new ScopeId.Shell(), () => { }, ImmutableHashSet.Create("queue_free"))) // 逆释放 b 的 Provides ⇒ 软边 a→b
                .Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { }, ImmutableHashSet.Create("queue_free")))); // 自有释放 ⇒ §5 闭合
        var bSpec = new FiberSpec(new FiberId("b"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(1), new ResourceId.Memory(1), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty
                .Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { }, ImmutableHashSet.Create("queue_free"))) // 逆释放 a 的 Provides ⇒ 软边 b→a
                .Push(new InverseClaim(new ResourceId.Memory(1), new ScopeId.Shell(), () => { }, ImmutableHashSet.Create("queue_free")))); // 自有释放 ⇒ §5 闭合
        rt.Register(aSpec);
        rt.Register(bSpec);
        rt.LoadAll();                                        // 软环不中止装载（仅硬环拒载）
        Assert.True(rt.SoftCycles.Length >= 2);             // 软环被记录（可观测出口）
    }

    [Fact]
    public void BeginTeardown_InvokesOnSuspending_ForDependents() // reviewer #190 F4：§7 集成缝合点——dependent 进入 Suspending 触发钩子
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        var suspended = new System.Collections.Generic.List<FiberId>();
        rt.OnSuspending = fib => suspended.Add(fib.Id);     // Godot 壳据此禁用 ProcessMode
        rt.LoadAll();
        rt.BeginTeardown(p);
        Assert.Contains(d.Id, suspended);                   // dependent 进入 Suspending ⇒ 钩子触发
    }

    [Fact]
    public void Register_Rejects_WhenProviderTearingDown() // reviewer #190 #2：级联进行中禁止新装载（避免挂上正在拆除的 provider）
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        rt.LoadAll();
        rt.BeginTeardown(p);                                 // p → TearingDown（级联进行中）
        Assert.Throws<InvalidOperationException>(() => rt.Register(Spec("late", new ResourceId.Gpu(new Rid("z")), new ResourceId.Memory(0))));
    }

    [Fact]
    public void TickWatchdog_DoesNotReEnqueue_DeadFiber() // reviewer #190 #1：已 Dead 的超时 fiber 不重复入队（避免二次回放误填 LastCrashReport）
    {
        var rt = new PluginRuntime();
        bool released = false;
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll();
        rt.TickWatchdog(fib => fib.State == FiberState.Active); // 第一帧：超时⇒TearingDown+入队
        rt.DrainTeardownBatch();                            // 排空⇒Dead
        Assert.Equal(FiberState.Dead, f.State);
        var before = rt.CrashReports.Length;
        rt.TickWatchdog(fib => true);                       // 第二帧：isTimedOut 恒 true，但 f 已 Dead ⇒ 不重复入队
        rt.DrainTeardownBatch();
        Assert.Equal(before, rt.CrashReports.Length);       // 无新增崩溃报告（未二次回放）
        Assert.True(released);
    }

    [Fact]
    public void AttachShell_WiresOnSuspendingToProcessModeCascade() // reviewer #191 F1：§7 集成缝合——provider teardown 经壳禁用 dependent 子树 ProcessMode
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var rt = new PluginRuntime();
        rt.AttachShell(shell);                               // 接线：OnSuspending ⇒ shell.CascadeProcessModeDisabled
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p);                                 // p teardown ⇒ d 进入 Suspending ⇒ 壳禁用 d 子树派发
        Assert.Contains(host.ProcessModes, pm => pm.Id == d.Id && pm.Disabled); // §7 ProcessMode 级联真实贯通（非孤岛）
    }

    [Fact]
    public void AttachShell_WiresExitDrainToShell() // reviewer #191 F2：关闭路径排空经壳触发运行时同步排空（双轨贯通）
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var rt = new PluginRuntime();
        rt.AttachShell(shell);                               // 接线：SynchronousExitDrain ⇒ shell 退出 drain
        bool released = false;
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => released = true)));
        rt.LoadAll();
        rt.BeginTeardown(f);
        shell.FlushExitDrain();                             // 宿主 _ExitTree 触发壳 flush ⇒ 运行时 SynchronousExitDrain 被驱动
        Assert.True(released);                              // 运行时退出排空经壳真执行（双轨缝合，非孤岛）
    }

    [Fact]
    public void ResetDiagnostics_ClearsCrashReportsAndSoftCycles() // reviewer #191 F3：累积诊断可清空，避免跨批次无界增长/泄漏观测
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => throw new InvalidOperationException("boom"))));
        rt.LoadAll();
        rt.BeginTeardown(p);
        rt.DrainTeardownBatch();
        Assert.True(rt.CrashReports.Length > 0);            // 先有崩溃记录
        rt.ResetDiagnostics();
        Assert.Empty(rt.CrashReports);                      // 清空
        Assert.Empty(rt.SoftCycles);
    }

    [Fact]
    public void SynchronousExitDrain_ResetsShuttingDown() // reviewer #191 F5：排空完毕复位 IsShuttingDown，允许实例复用（场景重载）
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => { })));
        rt.LoadAll();
        rt.BeginTeardown(f);
        rt.SynchronousExitDrain();
        Assert.False(rt.IsShuttingDown);                    // 排空后复位
        Assert.Equal(FiberState.Dead, f.State);
        // 复位后复用例证：可再次 Register（不抛 IsShuttingDown 异常）
        var g = rt.Register(Spec("q", new ResourceId.Gpu(new Rid("z")), new ResourceId.Memory(0)));
        Assert.Equal(FiberState.Inactive, g.State);
    }

    [Fact]
    public void CheckPermanentFiberLeak_ReturnsActiveFibersExceedingThreshold() // reviewer #193 #6（§10 MVP 内建议落地）：永久存活 Fiber 周期快照阈值告警——逐 Fiber 退出判零对全程存活插件不触发，靠此补足泄漏盲点
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("leak", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => { })));
        rt.LoadAll();
        Assert.Equal(FiberState.Active, f.State);
        // 每帧喂入当帧 net，运行时跨帧累积（模拟「每 N 帧周期快照」）
        rt.AccumulateNet(new Dictionary<FiberId, long> { [f.Id] = 10 });
        rt.AccumulateNet(new Dictionary<FiberId, long> { [f.Id] = 10 });
        Assert.Empty(rt.CheckPermanentFiberLeak(threshold: 25)); // 累积 20 < 25 ⇒ 不告警
        var alerts = rt.CheckPermanentFiberLeak(threshold: 15);   // 累积 20 > 15 ⇒ 告警
        Assert.Contains(f.Id, alerts);
        rt.ResetNetAccum();                                      // 跨批次复位
        Assert.Empty(rt.CheckPermanentFiberLeak(threshold: 0));
    }

    [Fact]
    public void CheckPermanentFiberLeak_IgnoresNonActiveFibers() // reviewer #193 #6：已 TearingDown/Dead 的 Fiber 不计入永久存活告警（退出路径由 §6 正常回收）
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("gone", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => { })));
        rt.LoadAll();
        rt.BeginTeardown(f);                                     // → TearingDown
        rt.AccumulateNet(new Dictionary<FiberId, long> { [f.Id] = 999 });
        Assert.Empty(rt.CheckPermanentFiberLeak(threshold: 1));   // 非 Active ⇒ 不告警
    }

    [Fact]
    public void BeginTeardown_OnSuspendingThrows_DoesNotAbortCascade() // reviewer #194 MEDIUM：OnSuspending 钩子抛异常须被 try/catch 隔离，不中断依赖者级联（dependent 仍进 TearingDown）
    {
        var rt = new PluginRuntime();
        rt.OnSuspending = _ => throw new InvalidOperationException("hook boom"); // 钩子异常
        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p); // 钩子抛异常不应中止级联
        Assert.Equal(FiberState.TearingDown, p.State);   // provider 进入 TearingDown
        Assert.Equal(FiberState.TearingDown, d.State);   // dependent 仍被级联进入 TearingDown（未被异常中断）
    }

    [Fact]
    public void TickWatchdog_DoesNotReEnqueue_TearingDownFiber() // reviewer #194 LOW：看门狗对超时但已 TearingDown 的 fiber 不重复强制/入队（State==Active||Suspending 守卫），避免二次回放误填 CrashReports
    {
        var rt = new PluginRuntime();
        var f = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0),
            (new ResourceId.Memory(0), () => { })));
        rt.LoadAll();
        rt.BeginTeardown(f);                                 // → TearingDown + 入队（未排空）
        var before = rt.CrashReports.Length;
        // 模拟看门狗：f 已 TearingDown（非 Active/Suspending）且 isTimedOut 恒 true ⇒ 守卫不重复强制/入队
        rt.TickWatchdog(fib => true);
        rt.DrainTeardownBatch();
        Assert.Equal(before, rt.CrashReports.Length);         // 无新增崩溃报告（未二次回放）
        Assert.Equal(FiberState.Dead, f.State);               // 原队列正常排空 ⇒ Dead
    }
}
