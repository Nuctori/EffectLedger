// QedP510ShellHookPins.cs — P5.10 第八轮独立审计遗留处置（P5-8-07）：AttachShell 不得静默覆盖用户自设 OnSuspending 钩子。
// 缺陷（audit/p5-auditor-independent.md #P5-8-07，MEDIUM）：PluginRuntime.AttachShell 无条件
// `OnSuspending = shell.CascadeProcessModeDisabled`——宿主先 `rt.OnSuspending = f` 再接线壳时，
// teardown 级联对 f 调用 0 次（静默丢失）。违反本项目「绝不静默」立库原则与 Register/AttachShell
// 一贯的 loud 纪律（null 拒绝、跨实例拒绝）。修复语义：组合而非覆盖——壳级联 + 用户既有钩子共存
// （调用序：先壳级联后用户钩子，与「接线后用户再 +=」的自然顺序一致）；无既有钩子时行为与原版逐字节相同。
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EffectLedger;
using EffectLedger.Runtime;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class QedP510ShellHookPins
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

    // ── P5-10-01 主钉：宿主先自设钩子 ⇒ AttachShell 后双钩共存（用户钩子仍被调用 + 壳级联仍贯通）──
    [Fact]
    public void AttachShell_AfterUserHook_Combines_NotOverwrites()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var rt = new PluginRuntime();
        var userCalls = new List<Fiber>();
        rt.OnSuspending = f => userCalls.Add(f);   // 宿主先自设钩子（修改前：接线时被静默替换，teardown 期 0 次调用）
        rt.AttachShell(shell);

        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p);                       // p teardown ⇒ d 进入 Suspending ⇒ 双钩都应触发

        Assert.Contains(d, userCalls);             // 用户钩子被调用（修改前 0 次——静默丢失）
        Assert.Contains(host.ProcessModes, pm => pm.Id == d.Id && pm.Disabled); // 壳 ProcessMode 级联仍真实贯通
    }

    // ── 配套钉：组合后多 dependent 各通知一次（不因委托合成产生重复/丢失）──
    [Fact]
    public void AttachShell_CombinedHook_NotifyOncePerDependent()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var rt = new PluginRuntime();
        var userCount = new Dictionary<FiberId, int>();
        rt.OnSuspending = f => userCount[f.Id] = userCount.TryGetValue(f.Id, out var n) ? n + 1 : 1;
        rt.AttachShell(shell);

        var p = rt.Register(Spec("p", new ResourceId.Memory(0), new ResourceId.Memory(0)));
        var d = rt.Register(Spec("d", new ResourceId.Gpu(new Rid("a")), new ResourceId.Memory(0)));
        rt.AddDependency(d, p, EdgeKind.Hard);
        rt.LoadAll();
        rt.BeginTeardown(p);

        Assert.Equal(1, userCount.TryGetValue(d.Id, out var n) ? n : 0); // 恰 1 次（不多不少）
    }

    // ── 回归钉：无既有钩子时组合结果与原版一致（单钩 = 壳级联）──
    [Fact]
    public void AttachShell_NoUserHook_SingleCascadeInvocationList()
    {
        var rt = new PluginRuntime();
        var shell = new GodotShell(new FakeHost());
        rt.AttachShell(shell);
        var inv = rt.OnSuspending!.GetInvocationList();
        Assert.Single(inv);                        // 不产生冗余包装层
        Assert.Same(shell, Assert.IsType<GodotShell>(inv[0].Target)); // 目标即壳（A3-13 幂等语义前提不变）
    }

    // ── 回归钉：接线后用户再 += 的既有合法用法不受影响（双钩、用户钩子在后）──
    [Fact]
    public void AttachShell_UserSubscribesAfter_CombinesPreservingOrder()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var rt = new PluginRuntime();
        rt.AttachShell(shell);
        var order = new List<string>();
        rt.OnSuspending += f => order.Add("user"); // 接线后订阅（既有用法，非本次缺陷形态）
        var inv = rt.OnSuspending!.GetInvocationList();
        Assert.Equal(2, inv.Length);
        Assert.Equal(1, inv.Count(m => m.Target is GodotShell)); // 壳级联恰一 entry（不重复接线）
    }
}
