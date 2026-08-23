// FiberStateMachineTests.cs — §2 状态机 TDD（reviewer spec D#1/#2）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class FiberStateMachineTests
{
    static Fiber Make(FiberState target)
    {
        var f = new Fiber(new FiberId("f"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty);
        switch (target)
        {
            case FiberState.Active: f.Load(); break;
            case FiberState.Suspending: f.Load(); f.NotifyProviderTeardown(); break;
            case FiberState.TearingDown: f.Load(); f.Unload(); break;
            case FiberState.Dead: f.Load(); f.Unload(); f.MarkDead(); break;
            case FiberState.Inactive:
            default: break;
        }
        return f;
    }

    [Fact]
    public void Load_FromInactive_SetsActive_And_Idempotent()
    {
        var f = Make(FiberState.Inactive);
        Assert.True(f.Load());
        Assert.Equal(FiberState.Active, f.State);
        Assert.False(f.Load());            // 幂等：Active 时 return false
        Assert.Equal(FiberState.Active, f.State);
    }

    [Theory]
    [InlineData(FiberState.Suspending)]
    [InlineData(FiberState.TearingDown)]
    [InlineData(FiberState.Dead)]
    public void Unload_FromSuspendingTearingDownDead_DirectReturn(FiberState state)
    {
        var f = Make(state);
        var before = f.State;
        f.Unload();
        Assert.Equal(before, f.State);    // 不变（直接 return）
    }

    [Fact]
    public void Unload_FromActive_EnqueuesAndTearsDown()
    {
        var f = Make(FiberState.Active);
        f.Unload();
        Assert.Equal(FiberState.TearingDown, f.State);
        Assert.True(f.TeardownEnqueued);
    }

    [Fact]
    public void Unload_TearingDown_PreventsReEnqueue()
    {
        var f = Make(FiberState.TearingDown);
        Assert.True(f.TeardownEnqueued);
        f.Unload();                        // TearingDown → 直接 return（已 enqueued）
        Assert.Equal(FiberState.TearingDown, f.State);
        Assert.True(f.TeardownEnqueued);
    }

    [Fact]
    public void Unload_Dead_IsNoOp()
    {
        var f = Make(FiberState.Dead);
        var wasEnqueued = f.TeardownEnqueued;  // Dead 经 TearingDown 而来，enqueued 已 true（合法）
        f.Unload();                            // Dead → 直接 return
        Assert.Equal(FiberState.Dead, f.State);
        Assert.Equal(wasEnqueued, f.TeardownEnqueued);
    }

    [Fact]
    public void NotifyProviderTeardown_ActiveToSuspending_Idempotent()
    {
        var f = Make(FiberState.Active);
        f.NotifyProviderTeardown();
        Assert.Equal(FiberState.Suspending, f.State);
        f.NotifyProviderTeardown();        // 幂等：Suspending 时 no-op（防 R4-4 振荡）
        Assert.Equal(FiberState.Suspending, f.State);
    }

    [Fact]
    public void NotifyProviderTeardown_NonActive_NoOp()
    {
        var f = Make(FiberState.TearingDown);
        f.NotifyProviderTeardown();
        Assert.Equal(FiberState.TearingDown, f.State);
    }

    [Fact]
    public void ForceTeardownOnWatchdog_ActiveOrSuspending_Transitions()
    {
        var f = Make(FiberState.Suspending);
        f.ForceTeardownOnWatchdog();
        Assert.Equal(FiberState.TearingDown, f.State);
        Assert.True(f.TeardownEnqueued);
    }

    [Fact]
    public void MarkDead_OnlyFromTearingDown()
    {
        var f = Make(FiberState.TearingDown);
        f.MarkDead();
        Assert.Equal(FiberState.Dead, f.State);
        var g = Make(FiberState.Active);
        g.MarkDead();                       // Active → 不变
        Assert.Equal(FiberState.Active, g.State);
    }
}
