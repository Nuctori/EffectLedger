// AuditRound2Tests.cs — 第二轮对抗审查发现的可执行回归（先红后修）。
// 这些断言刻意要求**具体证据类别**，防止"全部变 Unknown"或"用别的违规凑数"冒充检出。

using System.Linq;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Tests.Testing;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class AuditRound2Tests
{
    private const string Header = """
        using System;
        using System.Collections.Generic;
        using EffectLedger.Contracts;
        """;

    private static MethodSummary SummaryOf(string src, string typeName, string methodName)
    {
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled, "fixture must compile");
        var type = r.Compilation.GetTypeByMetadataName(typeName)!;
        var m = type.GetMembers(methodName).OfType<Microsoft.CodeAnalysis.IMethodSymbol>().First();
        var cga = new CallGraphAnalyzer(r.Compilation, AnalysisBudget.Default);
        return cga.Resolve(m);
    }

    // F-01：经自身属性读取的时钟必须被检出（属性 getter 需经调用图传播）。
    [Fact]
    public void ClockReadThroughOwnProperty_IsDetected()
    {
        var src = Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            private int P => DateTime.UtcNow.Day;
            public int Go() => P;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2001").Any(),
            "经私有属性 getter 读取时钟应报 EBC2001，不得静默通过");
    }

    // F-01 变体：经用户静态属性读取的时钟。
    [Fact]
    public void ClockReadThroughStaticUserProperty_IsDetected()
    {
        var src = Header + """

        public static class Cfg { public static int Max => DateTime.UtcNow.Day; }

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Go() => Cfg.Max;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2001").Any(), "经用户静态属性读取时钟应报 EBC2001");
    }

    // F-02：`int Count => _items.Count` 是标量访问器，绝不能报"只读视图"。
    [Fact]
    public void ScalarAccessorOverMutableField_IsNotFlagged()
    {
        var src = Header + """

        public sealed class S : IConstrained<ImmutableValue>
        {
            private readonly List<int> _items = new();
            public S() { _items.Add(1); }
            public int Count => _items.Count;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // F-03：get-only 自动属性在构造函数中赋值是合法初始化，不得报 EBC1001。
    [Fact]
    public void GetOnlyAutoProperty_AssignedInCtor_Passes()
    {
        var src = Header + """

        public sealed class F : IConstrained<ImmutableValue>
        {
            public int X { get; }
            public F(int x) { X = x; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // F-05：用户 IDisposable 的 using 必须传播 Dispose 效应。
    [Fact]
    public void UsingOverUserDisposable_PropagatesDispose()
    {
        var src = Header + """

        public sealed class D : IDisposable
        {
            public void Dispose() { Console.WriteLine("d"); }
        }

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Go() { using (var d = new D()) { } return 1; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2002").Any(), "using 用户 IDisposable 应传播 Dispose 的外部效果");
    }

    // F-07：返回调用方传入的可变引用 = 别名外泄，必须检出（ReturnsInputAlias 需有产生点）。
    [Fact]
    public void ReturningInputAlias_IsDetected()
    {
        var src = Header + """

        public sealed class P : IConstrained<ImmutableValue>
        {
            public List<int> Peek(List<int> callerOwned) => callerOwned;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(), "返回输入参数的可变引用应被检出");
    }

    // F-09：返回 this 是 receiver 逃逸。
    [Fact]
    public void ReturningThis_IsDetected()
    {
        var src = Header + """

        public sealed class M : IConstrained<ImmutableValue>
        {
            public object Self() => this;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(), "返回 this 应被检出为 receiver 逃逸");
    }

    // F-08：把输入参数存入静态字段 = 参数逃逸。
    [Fact]
    public void StoringParameterIntoStaticField_IsDetected()
    {
        var src = Header + """

        public sealed class M : IConstrained<ImmutableValue>
        {
            public static List<int>? Leaked;
            public M(List<int> input) { Leaked = input; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(), "把输入参数存入静态字段应被检出为参数逃逸");
    }

    // F-12 判别：经局部别名的 receiver 集合修改，证据类别必须是 Receiver（不是 Unknown）。
    [Fact]
    public void ReceiverMutationViaLocalAlias_IsKnownReceiverWrite()
    {
        var sum = SummaryOf(Header + """

        public sealed class B : IConstrained<ImmutableValue>
        {
            private readonly List<int> _items = new();
            public void Mutate() { var alias = _items; alias.Clear(); }
        }
        """, "B", "Mutate");

        Assert.True(sum.Writes.Any(w => w.Target == WriteTargetKind.Receiver),
            $"应给出 Receiver 类别写入；实际=[{string.Join(",", sum.Writes.Select(w => w.Target + ":" + w.Description))}]");
    }

    // F-04：用户自定义可枚举的 GetEnumerator/MoveNext/Current 必须传播（否则枚举器内的隐藏 IO 静默）。
    [Fact]
    public void ForeachOverUserEnumerable_PropagatesEnumeratorEffects()
    {
        var src = Header + """

        public sealed class ClockEnum
        {
            public ClockEnum GetEnumerator() => this;
            public bool MoveNext() => DateTime.UtcNow.Day > 0;
            public int Current => 1;
        }

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Go()
            {
                int s = 0;
                foreach (var x in new ClockEnum()) s += x;
                return s;
            }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2001").Any(),
            "foreach 用户枚举器读取时钟应报 EBC2001（经 GetEnumerator/MoveNext 传播）");
    }

    // F-04 变体：用户 IEnumerable<T> 实现的 GetEnumerator 产生外部效果。
    [Fact]
    public void ForeachOverUserIEnumerable_PropagatesGetEnumerator()
    {
        var src = Header + """

        using System.Collections;

        public sealed class NoisyEnumerable : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() { Console.WriteLine("e"); yield break; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Go()
            {
                int s = 0;
                foreach (var x in new NoisyEnumerable()) s += x;
                return s;
            }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2002").Any(),
            "foreach 用户 IEnumerable 的 GetEnumerator 应传播其外部效果");
    }

    // EBC2003（本轮实现）：确定性入口的可变输入必须被检出。
    [Fact]
    public void MutableEntryInput_IsReportedAsEntryCondition()
    {
        var src = Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Sum(List<int> input) => input.Count;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2003").Any(),
            "入口接受可变引用（调用方可并发修改）应报 EBC2003");
    }

    // EBC2003 合法对照：全稳定值入口不得误报。
    [Fact]
    public void StableEntryValues_ProduceNoEntryCondition()
    {
        var src = Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Double(int x) => x * 2;
            public string Label(int x) => x.ToString();
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.WithId("EBC2003"));
    }
}
