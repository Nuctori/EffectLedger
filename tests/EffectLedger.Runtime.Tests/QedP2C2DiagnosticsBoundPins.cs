// QedP2C2DiagnosticsBoundPins.cs — P2-C2 结构性内存卫生钉（诚实边界 #19 收口）：
// ①CrashReports 环形上限（保留最近 MaxCrashReports=64 条，last 语义不变）——内存安全不依赖宿主
//   自觉调用 ResetDiagnostics；②批次排空完成 ⇒ _netAccum 剪除非 Active Fiber 条目（零语义损失：
//   CheckPermanentFiberLeak 的 Active 过滤器永久跳过 + 同 FiberId 不可重注册）；③ResetDiagnostics
//   保留为可选显式出口（既有钉 ResetDiagnostics_ClearsCrashReportsAndSoftCycles 继续承载）。
// internal 观测口（NetAccumEntries/MaxCrashReports）经 Runtime.Tests IVT 访问，公共快照不含。xUnit。
using System.Collections.Immutable;
using EffectLedger;
using EffectLedger.Runtime;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class QedP2C2DiagnosticsBoundPins
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

    // ── 钉 1（环形上限）：66 次崩溃 ⇒ CrashReports 恒为 64 条、最旧两条被丢弃、LastCrashReport 恒为最新。 ──
    [Fact]
    public void CrashReports_CappedAt64_KeepsNewest()
    {
        var rt = new PluginRuntime();
        Fiber? last = null;
        for (int i = 1; i <= PluginRuntime.MaxCrashReports + 2; i++) // 66 次：超限 2 条
        {
            var f = rt.Register(Spec($"p{i}", new ResourceId.Memory((ulong)i), new ResourceId.Memory(0),
                (new ResourceId.Memory((ulong)i), () => throw new InvalidOperationException($"boom-{i}"))));
            rt.LoadAll();
            rt.BeginTeardown(f);
            rt.DrainTeardownBatch(); // 失败的逆释放 ⇒ 1 条崩溃报告
            last = f;
        }

        Assert.Equal(PluginRuntime.MaxCrashReports, rt.CrashReports.Length); // 环形上限恒定
        Assert.NotNull(rt.LastCrashReport);
        Assert.Equal(last!.Id, rt.LastCrashReport!.Provider); // last 语义：恒为最新
    }

    // ── 钉 2（排空剪除，零语义损失）：p 恒 Active、q 卸载死亡 ⇒ 排空后 _netAccum 仅存 Active 条目——
    //    死 Fiber 的累积被剪除（不剪则跨批次无界增长）；CheckPermanentFiberLeak 对 p 的判定不受影响。 ──
    [Fact]
    public void DrainCompletion_PrunesDeadFiberAccumulations()
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => { })));
        var q = rt.Register(Spec("q", new ResourceId.Memory(2), new ResourceId.Memory(0),
            (new ResourceId.Memory(2), () => { })));
        rt.LoadAll();

        rt.AccumulateNet(new System.Collections.Generic.Dictionary<FiberId, long> { [p.Id] = 5, [q.Id] = 7 });
        Assert.Equal(2, rt.NetAccumEntries); // 前置：两条都在

        rt.BeginTeardown(q);
        rt.DrainTeardownBatch(); // q 死亡；批次排空完成 ⇒ 剪除非 Active（q）条目

        Assert.Equal(1, rt.NetAccumEntries);                    // 死 Fiber 条目已剪
        Assert.Empty(rt.CheckPermanentFiberLeak(0).Where(id => id.Equals(q.Id))); // q 永不参与判定
        Assert.Contains(rt.CheckPermanentFiberLeak(0), id => id.Equals(p.Id));    // Active 判定不受影响
    }

    // ── 钉 3（零语义损失的剪除不改变泄漏判定语义）：p 累积超阈值 ⇒ 照常告警（剪除只动非 Active）。 ──
    [Fact]
    public void Prune_DoesNotAffectActiveFiberLeakDetection()
    {
        var rt = new PluginRuntime();
        var p = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () => { })));
        var q = rt.Register(Spec("q", new ResourceId.Memory(2), new ResourceId.Memory(0),
            (new ResourceId.Memory(2), () => { })));
        rt.LoadAll();
        rt.AccumulateNet(new System.Collections.Generic.Dictionary<FiberId, long> { [p.Id] = 999, [q.Id] = 999 });

        rt.BeginTeardown(q);
        rt.DrainTeardownBatch();

        Assert.Contains(rt.CheckPermanentFiberLeak(100), id => id.Equals(p.Id)); // Active 泄漏照报
        Assert.Single(rt.CheckPermanentFiberLeak(100));
    }
}
