// QedP53RuntimeHardeningPins.cs — P5.3 Runtime 生命周期攻击处置钉
//（第三轮审计 auditor-runtime：6 条实现缺陷中的可钉 5 条）：
// ①LoadAll 关闭/级联期守卫（MED 泄漏向量：逆 Action 重入装载 ⇒ Inactive fiber 永久 Active）；
// ②AttachShell(null) 拒绝（与 Register 的 ArgumentNullException 纪律对称）；
// ③AccumulateNet(null) / ④TickWatchdog(null) 拒绝（NRE → ArgumentNullException 方言）；
// ⑤CheckPermanentFiberLeak 负阈值拒绝（零累积误报泄漏）。
using System;
using System.Collections.Immutable;
using System.Linq;
using EffectLedger;
using EffectLedger.Runtime;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class QedP53RuntimeHardeningPins
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

    // ── 钉 1（MED 泄漏向量）：退出排空中逆 Action 重入 LoadAll ⇒ 守卫 loud 拒绝且崩溃被记录——
    //    修复前：重入装载静默成功 ⇒ Inactive fiber 被激活且无 teardown 覆盖（永久 Active 泄漏）。 ──
    [Fact]
    public void LoadAll_ReentrantDuringExitDrain_GuardThrowsAndRecorded()
    {
        var rt = new PluginRuntime();
        var reentered = false;
        var f = rt.Register(Spec("p", new ResourceId.Memory(1), new ResourceId.Memory(0),
            (new ResourceId.Memory(1), () =>
            {
                if (rt.IsShuttingDown) // 仅在退出排空期重入（红队 MED 攻击向量）
                {
                    reentered = true;
                    rt.LoadAll();
                }
            })));
        rt.LoadAll();
        rt.BeginTeardown(f);
        rt.SynchronousExitDrain();

        Assert.True(reentered, "前置破坏：逆 Action 未在排空中执行");
        // 守卫抛出的 InvalidOperationException 被级联捕获 ⇒ CrashReport 记录（loud 而非静默激活）
        Assert.Contains(rt.CrashReports, c => c.Exception != null && c.Exception.ToString().Contains("LoadAll")); // ToString 含完整异常链（内层即守卫消息）
    }

    // ── 钉 2：AttachShell(null) 拒绝（ArgumentNullException，与 Register 纪律对称——
    //    静默 no-op 会让宿主误以为接线成功而退出 drain 未注册）。 ──
    [Fact]
    public void AttachShell_Null_Throws()
    {
        var rt = new PluginRuntime();
        Assert.Throws<ArgumentNullException>(() => rt.AttachShell(null!));
    }

    // ── 钉 3：AccumulateNet(null) 拒绝（ArgumentNullException 方言，非 NRE）。 ──
    [Fact]
    public void AccumulateNet_Null_ThrowsArgumentNull()
    {
        var rt = new PluginRuntime();
        Assert.Throws<ArgumentNullException>(() => rt.AccumulateNet(null!));
    }

    // ── 钉 4：TickWatchdog(null) 拒绝（同上方言）。 ──
    [Fact]
    public void TickWatchdog_Null_ThrowsArgumentNull()
    {
        var rt = new PluginRuntime();
        Assert.Throws<ArgumentNullException>(() => rt.TickWatchdog(null!));
    }

    // ── 钉 5：CheckPermanentFiberLeak 负阈值拒绝（负阈值会把零累积误报为泄漏）。 ──
    [Fact]
    public void CheckPermanentFiberLeak_NegativeThreshold_Throws()
    {
        var rt = new PluginRuntime();
        Assert.Throws<ArgumentOutOfRangeException>(() => rt.CheckPermanentFiberLeak(-1));
    }
}
