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
}
