// ProdAuditR3RuntimeTests.cs — 第三轮独立审计（2026-09）Runtime 回归钉。
// R3-RT-01：看门狗自愈分支须与主路径同构「入队 + dependent-first 拓扑排空」——内联先回放 provider
//           会破坏回收序（依赖者逆释放 provider 所供资源时，资源先被 provider 释放 ⇒ use-after-free 同型）。
// R3-RT-02：AddDependency 须与 Register 同型关路径/级联期守卫（级联期挂新依赖者 ⇒ 永久 Active 派发于已 Dead provider）。
// R3-RT-04：逆回放重入须 loud 拒绝（逆 Action 内重入 TickWatchdog 曾致整栈多次回放=多重重放/双释放）。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class ProdAuditR3RuntimeTests
{
    static FiberSpec Spec(string id, ResourceId provides, ResourceId requires, params (ResourceId, Action)[] inv)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        foreach (var (r, a) in inv) stack = stack.Push(new InverseClaim(r, new ScopeId.Shell(), a));
        bool hasReleaseForProvides = false;
        foreach (var e in inv)
            if (ResourceId.Normalize(e.Item1) == ResourceId.Normalize(provides)) hasReleaseForProvides = true;
        if (!hasReleaseForProvides)
            stack = stack.Push(new InverseClaim(provides, new ScopeId.Shell(), () => { }));
        return new FiberSpec(new FiberId(id), Signature.Empty,
            new Coeffect(requires, provides, new ScopeId.Shell()), stack);
    }

    // ── R3-RT-01：自愈路径回收序 = dependent-first ──
    [Fact]
    public void Watchdog_SelfHeal_ReleasesDependent_Before_Provider()
    {
        var order = new List<string>();
        var rt = new PluginRuntime();
        var mem = new ResourceId.Memory(0);
        // p 提供 mem；d 的逆声明释放 mem（跨 Fiber 借用，LoadAll 自动派生软边 d→p）
        var p = rt.Register(Spec("p", mem, mem, (mem, () => order.Add("p"))));
        var dInverse = ImmutableStack<InverseClaim>.Empty.Push(
            new InverseClaim(mem, new ScopeId.Shell(), () => order.Add("d"),
                new HashSet<string> { "queue_free" })); // R5-6：跨 Fiber 释放须声明 release-class 标签
        var d = rt.Register(new FiberSpec(new FiberId("d"), Signature.Empty,
            new Coeffect(new ResourceId.Gpu(new Rid("g")), mem, new ScopeId.Shell()), dInverse));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();

        p.Unload();                  // 旁路：p TearingDown 且不在队列（自愈分支前提）
        rt.TickWatchdog(_ => true);  // d 走第一分支入队；p 走自愈分支
        rt.DrainTeardownBatch();

        Assert.Equal(FiberState.Dead, p.State);
        Assert.Equal(FiberState.Dead, d.State);
        // 回收序铁律：依赖者（逆释放 p 所供 mem）先于 provider 释放——与 BeginTeardown 主路径拓扑序一致
        Assert.Equal(new[] { "d", "p" }, order); // 修改前：["p","d"]（自愈内联先回放 provider）
    }

    // ── R3-RT-02：AddDependency 级联期守卫 ──
    [Fact]
    public void AddDependency_DuringTeardown_IsRejected()
    {
        var rt = new PluginRuntime();
        var mem = new ResourceId.Memory(0);
        var p = rt.Register(Spec("p", mem, mem));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("g")), mem));
        var late = rt.Register(Spec("late", new ResourceId.Gpu(new Rid("h")), mem));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();

        rt.BeginTeardown(p); // p → TearingDown（级联进行中）
        var ex = Assert.Throws<InvalidOperationException>(
            () => rt.AddDependency(late, p, EdgeKind.Hard)); // 修改前：成功建边，late 永久 Active 于已 Dead provider
        Assert.Contains("级联", ex.Message);
    }

    // ── R3-RT-04：逆回放重入 loud 拒绝 ──
    [Fact]
    public void InverseReplay_ReentrantWatchdog_Rejected_NotReplayed()
    {
        int runs = 0;
        var rt = new PluginRuntime();
        var mem = new ResourceId.Memory(0);
        var p = rt.Register(Spec("p", mem, mem,
            (mem, () => { runs++; if (runs < 5) rt.TickWatchdog(_ => true); })));
        p.Load();
        p.Unload();                 // 旁路 → TearingDown 且不在队列
        rt.TickWatchdog(_ => true); // 自愈启动回放；逆 Action 内重入 TickWatchdog
        rt.DrainTeardownBatch();

        Assert.Equal(1, runs);      // 修改前：5（整栈被反复回放 = 多重重放/双释放）
        Assert.Equal(FiberState.Dead, p.State);
        rt.DrainTeardownBatch();    // 修复前重入会把「正在回放」的 fiber 二次入队 ⇒ 再排空就二次回放
        Assert.Equal(1, runs);      // 二次排空后仍 1 次：重入 no-op（ReplayInProgress 门），无残留任务
    }
}
