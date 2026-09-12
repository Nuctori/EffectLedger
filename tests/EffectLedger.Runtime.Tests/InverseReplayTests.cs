// InverseReplayTests.cs — §4 结构化逆回放 TDD（reviewer spec D#7）。
using System.Collections.Immutable;
using EffectLedger;
using EffectLedger.Runtime;
using Xunit;

namespace EffectLedger.Runtime.Tests;

public class InverseReplayTests
{
    static Fiber FiberWithInverses(params (ResourceId res, Action act)[] inverses)
    {
        var stack = ImmutableStack<InverseClaim>.Empty;
        // 压栈顺序：最后压入的在最上（LIFO 先回放）。
        foreach (var (res, act) in inverses) stack = stack.Push(new InverseClaim(res, new ScopeId.Shell(), act));
        var f = new Fiber(new FiberId("f"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()), stack);
        f.Load();
        f.Unload(); // → TearingDown，使 MarkDead 可用
        return f;
    }

    [Fact]
    public void Replay_Lifo_Order()
    {
        var order = new System.Collections.Generic.List<string>();
        var f = FiberWithInverses(
            (new ResourceId.Memory(1), () => order.Add("a")),
            (new ResourceId.Memory(2), () => order.Add("b")),
            (new ResourceId.Memory(3), () => order.Add("c")));
        // 压栈顺序 a,b,c ⇒ 栈顶 c；LIFO 回放 c,b,a。
        var diag = InverseReplay.ReplayAndDead(f);
        Assert.True(diag.AllCompleted);
        Assert.Equal(new[] { "c", "b", "a" }, order);
        Assert.Equal(FiberState.Dead, f.State);
    }

    [Fact]
    public void Replay_AllCompleted_MarksDead()
    {
        var f = FiberWithInverses(
            (new ResourceId.Memory(1), () => { }),
            (new ResourceId.Memory(2), () => { }));
        var diag = InverseReplay.ReplayAndDead(f);
        Assert.True(diag.AllCompleted);
        Assert.Empty(diag.Pending);
        Assert.Equal(FiberState.Dead, f.State);
    }

    [Fact]
    public void Replay_MidStackException_PartialReleaseDiagnosis()
    {
        int calls = 0;
        var f = FiberWithInverses(
            (new ResourceId.Memory(1), () => { calls++; }),            // c：栈顶，先执行，成功
            (new ResourceId.Memory(2), () => throw new InvalidOperationException("boom")), // b：异常
            (new ResourceId.Memory(3), () => { calls++; }));           // a：继续回放，成功
        var diag = InverseReplay.ReplayAndDead(f);
        Assert.False(diag.AllCompleted);
        Assert.Equal(2, calls);                     // 异常后仍继续回放其余逆
        Assert.Contains(new ResourceId.Memory(1), diag.Completed);
        Assert.Contains(new ResourceId.Memory(3), diag.Completed);
        Assert.Contains(new ResourceId.Memory(2), diag.Pending);  // 失败资源留 pending
        Assert.Equal(FiberState.Dead, f.State);     // 仍标记 Dead（R4-6：尽量继续）
    }

    [Fact]
    public void Replay_Throws_WhenNotTearingDown() // reviewer #188 F3：逆回放前置 TearingDown，禁止对 Active/Suspending 回放
    {
        var f = new Fiber(new FiberId("x"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()),
            ImmutableStack<InverseClaim>.Empty.Push(new InverseClaim(new ResourceId.Memory(0), new ScopeId.Shell(), () => { }, ImmutableHashSet.Create("queue_free"))));
        f.Load(); // → Active
        Assert.Equal(FiberState.Active, f.State);
        Assert.Throws<InvalidOperationException>(() => InverseReplay.ReplayAndDead(f)); // 非 TearingDown ⇒ 抛
    }

    [Fact]
    public void Replay_EmptyStack_NoOp_StillDead()
    {
        var f = FiberWithInverses();
        var diag = InverseReplay.ReplayAndDead(f);
        Assert.True(diag.AllCompleted);
        Assert.Equal(FiberState.Dead, f.State);
    }
}
