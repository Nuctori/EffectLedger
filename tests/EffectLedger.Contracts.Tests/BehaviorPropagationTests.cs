// BehaviorPropagationTests.cs — 行为传播/回调/构造期逃逸回归（要求 3、4）。

using EffectLedger.Contracts.Tests.Testing;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class BehaviorPropagationTests
{
    private const string Header = """
        using System;
        using System.Collections.Generic;
        using EffectLedger.Contracts;
        """;

    // 未调用的局部 lambda 不计入执行效果（不可妥协要求 4）。
    [Fact]
    public void UncalledLocalFunction_DoesNotTrigger()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Run()
            {
                int Evil() => DateTime.UtcNow.Day;
                return 1;
            }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.WithId("EBC2001"));
    }

    // 执行了捕获状态的回调 → 其效果必须计入（委托"执行"与"创建"分离）。
    [Fact]
    public void ExecutedCallback_PropagatesEffect()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Run(Action cb)
            {
                Console.WriteLine("before");
                cb();
                return 0;
            }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC2002"));
    }

    // 构造期把 this 传给未知方法 = 构造期逃逸（EBC1003）。
    [Fact]
    public void CtorEscapesThis_Reports()
    {
        var src = Header + """

        public sealed class Holds : IConstrained<ImmutableValue>
        {
            private readonly int _x;
            public Holds()
            {
                _x = 0;
                Sink(this);   // 构造期 this 逃逸
            }
            private static void Sink(object o) { }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.EbcViolations());
    }

    // 合法：只读标量 + 构造后只读属性访问，无逃逸。
    [Fact]
    public void ReadOnlyValue_NoEscape_Passes()
    {
        var src = Header + """

        public sealed class Pair : IConstrained<ImmutableValue>
        {
            private readonly int _a;
            private readonly int _b;
            public Pair(int a, int b) { _a = a; _b = b; }
            public int Sum => _a + _b;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // 要求 4：区分"创建"与"执行"——存起来的委托必须记为逃逸（之后可能被调用）。
    [Fact]
    public void StoringDelegateIntoField_IsMarkedAsEscape()
    {
        var src = Header + """

        public sealed class Holder : IConstrained<ImmutableValue>
        {
            private Action? _saved;
            public Holder(Action cb) { _saved = cb; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(),
            "把回调存入字段（之后可被任意时刻调用）应被检出，不得静默");
    }

    // 要求 4：委托执行本身被识别为"执行"（与仅创建区分）。
    [Fact]
    public void DelegateInvocation_IsRecordedAsExecution()
    {
        var src = Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Run(Func<int> f) => f();
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        // 委托目标不可解析 ⇒ 必须至少报 Unknown（不能当作"没执行、无副作用"）。
        Assert.True(r.EbcViolations().Any(),
            "执行未知委托应至少报告 Unknown，不得静默通过");
    }
}
