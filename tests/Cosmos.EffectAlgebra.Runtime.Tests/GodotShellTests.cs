// GodotShellTests.cs — §7 Godot 壳抽象 TDD（reviewer spec D#1/#2）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class GodotShellTests
{
    static Fiber ActiveFiber(string id)
        => new(new FiberId(id), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty);

    [Fact]
    public void Defer_CoalescesDuplicateActions()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        Action a = () => { };
        shell.Defer(a);
        shell.Defer(a); // 重复：应被去重
        Assert.Single(host.Deferred);
    }

    [Fact]
    public void Defer_Null_IsNoOp()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        shell.Defer(null!);
        Assert.Empty(host.Deferred);
    }

    [Fact]
    public void CascadeProcessModeDisabled_DisablesFiber()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        var f = ActiveFiber("f");
        shell.CascadeProcessModeDisabled(f);
        Assert.Contains(host.ProcessModes, pm => pm.Id == f.Id && pm.Disabled);
    }

    [Fact]
    public void EnqueueExitDrain_AndFlush_Executes()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        bool ran = false;
        shell.EnqueueExitDrain(() => ran = true);
        Assert.False(ran); // 未 flush 不执行
        shell.FlushExitDrain();
        Assert.True(ran);
        Assert.True(shell.ExitDraining);
    }

    [Fact]
    public void IsSafeToInvoke_GatesOnInstanceValid()
    {
        var host = new FakeHost { AllValid = false };
        var shell = new GodotShell(host);
        Assert.False(shell.IsSafeToInvoke(new object())); // 句柄失效 → 不安全
        host.AllValid = true;
        Assert.True(shell.IsSafeToInvoke(new object()));   // 句柄有效 → 安全
    }

    [Fact]
    public void FlushExitDrain_WithException_Continues()
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        bool secondRan = false;
        shell.EnqueueExitDrain(() => throw new InvalidOperationException("boom"));
        shell.EnqueueExitDrain(() => secondRan = true);
        shell.FlushExitDrain(); // 第一个异常不阻断第二个
        Assert.True(secondRan);
    }

    [Fact]
    public void Defer_DropsActionWhenHandleInvalid() // reviewer #191 F2：Defer 执行前判空门控（此前 IsSafeToInvoke 死代码）——句柄失效则静默丢弃，避免 use-after-free
    {
        var host = new FakeHost { AllValid = false }; // Godot Object 已 QueueFree ⇒ 句柄失效
        var shell = new GodotShell(host);
        bool ran = false;
        shell.Defer(() => ran = true, new object()); // 携带失效句柄
        host.FlushDeferred();                          // 模拟帧末 call_deferred 排空
        Assert.False(ran); // 句柄失效 ⇒ 回调被丢弃（fail-closed），不执行
        // 对照：有效句柄仍执行
        host.AllValid = true;
        bool ran2 = false;
        shell.Defer(() => ran2 = true, new object());
        host.FlushDeferred();
        Assert.True(ran2);
    }

    [Fact]
    public void Defer_DropsActionWhenInstanceValidThrows() // R6 并发安全：IsSafeToInvoke 抛异常（真实宿主 IsInstanceValid 异常路径）不得逃逸进宿主 defer 机制——隔离为 not-safe，fail-closed 丢弃回调
    {
        var host = new ThrowingInstanceValidHost(); // IsInstanceValid 恒抛
        var shell = new GodotShell(host);
        bool ran = false;
        bool threw = false;
        shell.Defer(() => ran = true, new object());
        try { host.FlushDeferred(); } catch { threw = true; } // 异常不得冒出
        Assert.False(threw);   // 异常在壳内被隔离，不逃逸
        Assert.False(ran);     // 句柄门控异常 ⇒ 回调被丢弃（fail-closed）
    }

    [Fact]
    public void Defer_IsDroppedDuringExitDraining() // reviewer #194/R9 medium（§3 R4-1）：退出期（_ExitTree 触发 FlushExitDrain 期间）禁止新 Defer，避免退出序结束后的 use-after-free；flush 末复位允许后续正常 Defer
    {
        var host = new FakeHost();
        var shell = new GodotShell(host);
        shell.FlushExitDrain();                 // 进入退出期（_exitDraining 置位，flush 末不复位——节点释放不可逆）
        Assert.True(shell.ExitDraining);        // 退出期标志保持
        bool ran = false;
        shell.Defer(() => ran = true);           // 退出期 Defer ⇒ 应被丢弃
        host.FlushDeferred();
        Assert.False(ran);                        // 退出期 Defer 不执行（避免退出序后 use-after-free）
        // 退出期永久不可逆：flush 后再 Defer 仍丢弃
        bool ran2 = false;
        shell.Defer(() => ran2 = true);
        host.FlushDeferred();
        Assert.False(ran2);                       // 仍被丢弃
        Assert.True(shell.ExitDraining);         // 标志持续为真
    }
}

// R6 测试替身：IsInstanceValid 恒抛，验证 Defer 门控异常被隔离为 fail-closed
sealed class ThrowingInstanceValidHost : IHost
{
    public void Defer(Action action) => action(); // 立即同步执行，暴露任何逃逸异常
    public void FlushDeferred() { }
    public void SetProcessMode(FiberId id, bool disabled) { }
    public void EnqueueExitDrain(Action drain) { }
    public bool IsInstanceValid(object handle) => throw new InvalidOperationException("host IsInstanceValid boom");
}
