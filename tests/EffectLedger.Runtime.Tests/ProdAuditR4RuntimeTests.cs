// ProdAuditR4RuntimeTests.cs — 第四轮独立审计（2026-09）Runtime 回归钉。
// RH-07：Register 构造期 null 守卫（spec null / 逆声明 Execute null）——带 FiberId 的 loud 拒绝。
// JD-10：3000 深依赖链全流程规模测量钉（装载校验 O(F²·I)、看门狗自愈、拓扑排空）。
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using EffectLedger;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class ProdAuditR4RuntimeTests
{
    // ── RH-07：构造期 null 守卫 ──
    [Fact]
    public void Register_NullSpec_ThrowsArgumentNull()
    {
        var rt = new PluginRuntime();
        Assert.Throws<ArgumentNullException>(() => rt.Register(null!)); // 修改前：NRE 在 spec.Id 解引用处
    }

    [Fact]
    public void Register_NullInverseExecute_ThrowsArgumentNull_NamingFiber()
    {
        var rt = new PluginRuntime();
        var res = new ResourceId.Memory(7);
        var bad = ImmutableStack<InverseClaim>.Empty.Push(
            new InverseClaim(res, new ScopeId.Shell(), null!)); // Execute=null：修复前延后到逆回放中途 NRE（teardown 最不可承受路径）
        var spec = new FiberSpec(new FiberId("p"), Signature.Empty,
            new Coeffect(res, res, new ScopeId.Shell()), bad);
        var ex = Assert.Throws<ArgumentNullException>(() => rt.Register(spec));
        Assert.Contains("p", ex.Message); // 归因到 FiberId
    }

    // ── REG-01'（复审计）：入队单点成对——BeginTeardown 后看门狗不得对已入队者二次入队 ──
    // 此前 _queuedProviders.Add 被 3/5 站点误并入行注释未执行（字节级确认，提交 340eccd），
    // set 与队列失步 ⇒ 已入队 fiber 每帧重复入队。PendingTeardownCount 数量契约钉死之。
    [Fact]
    public void BeginTeardown_ThenWatchdog_NoDuplicateEnqueue()
    {
        var rt = new PluginRuntime();
        var mem = new ResourceId.Memory(0);
        // 构造与 R3-RT-01 同型（过 §5 闭合 + R5-6 跨 Fiber 释放须 release-class 标签）
        var pInv = ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(mem, new ScopeId.Shell(), () => { }));
        var dInv = ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(mem, new ScopeId.Shell(), () => { },
            new HashSet<string> { "queue_free" }));
        var p = rt.Register(new FiberSpec(new FiberId("p"), Signature.Empty,
            new Coeffect(mem, mem, new ScopeId.Shell()), pInv));
        var d = rt.Register(new FiberSpec(new FiberId("d"), Signature.Empty,
            new Coeffect(new ResourceId.Gpu(new Rid("g")), mem, new ScopeId.Shell()), dInv));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();

        rt.BeginTeardown(p);                    // p 入队 + 级联 d 入队 ⇒ 2
        int afterBegin = rt.PendingTeardownCount;
        rt.TickWatchdog(_ => true);             // 自愈分支对已入队者必须 no-op（成员集 O(1) 判定）
        Assert.Equal(afterBegin, rt.PendingTeardownCount); // 失步时：每帧 +N 无界增长

        rt.DrainTeardownBatch();
        Assert.Equal(0, rt.PendingTeardownCount);
        Assert.Equal(FiberState.Dead, p.State);
        Assert.Equal(FiberState.Dead, d.State);
    }

    // ── JD-10：3000 深依赖链全流程规模钉（软墙钟 <30s，硬断言是全部 Dead） ──
    [Fact]
    public void DeepChain_3000Fibers_FullLifecycle_Completes()
    {
        const int depth = 3000;
        var rt = new PluginRuntime();
        var sw = Stopwatch.StartNew();

        var fibers = new Fiber[depth];
        for (int i = 0; i < depth; i++)
        {
            var res = new ResourceId.Memory((uint)(i + 1));
            var stack = ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(res, new ScopeId.Shell(), () => { }));
            fibers[i] = rt.Register(new FiberSpec(new FiberId("f" + (i + 1)), Signature.Empty,
                new Coeffect(res, res, new ScopeId.Shell()), stack));
        }
        for (int i = 0; i + 1 < depth; i++) rt.AddDependency(fibers[i], fibers[i + 1], EdgeKind.Hard);
        rt.LoadAll();
        Assert.All(fibers, f => Assert.Equal(FiberState.Active, f.State));

        foreach (var f in fibers) f.Unload();      // 旁路卸载
        rt.TickWatchdog(_ => true);                // 自愈全量入队
        rt.DrainTeardownBatch();                   // dependent-first 拓扑排空
        Assert.All(fibers, f => Assert.Equal(FiberState.Dead, f.State));
        Assert.True(sw.ElapsedMilliseconds < 30_000, $"3000 链全流程应 <30s（软墙钟），实际 {sw.ElapsedMilliseconds}ms");
    }
}
