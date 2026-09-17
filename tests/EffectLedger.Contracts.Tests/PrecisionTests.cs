// PrecisionTests.cs — 审查发现 F1/F4/F11 的判别性回归。
// 与粗粒度断言不同，这里要求**具体证据种类**（已知写入 vs Unknown），防止"全都变 Unknown"冒充检出。

using System.Linq;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Engine;
using EffectLedger.Contracts.Analyzer.Profiles;
using EffectLedger.Contracts.Tests.Testing;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class PrecisionTests
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
        var type = r.Compilation.GetTypeByMetadataName(typeName) ?? throw new Xunit.Sdk.XunitException("type not found: " + typeName);
        var m = type.GetMembers(methodName).OfType<IMethodSymbol>().First();
        var cga = new CallGraphAnalyzer(r.Compilation, AnalysisBudget.Default);
        return cga.Resolve(m);
    }

    // F4 判别：修改输入参数必须是**已知写入**（Parameter），不能靠 Unknown 蒙混。
    [Fact]
    public void MutatingInput_IsKnownWrite_NotUnknown()
    {
        var sum = SummaryOf(Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public void Touch(List<int> input) { input.Add(1); }
        }
        """, "C", "Touch");

        Assert.True(sum.Writes.Any(w => w.Target == WriteTargetKind.Parameter),
            $"应产生已知 Parameter 写入；实际 writes=[{string.Join(",", sum.Writes.Select(w => w.Target + ":" + w.Description))}]");
    }

    // F1 判别：把外部可变引用存入自身字段 → 必须被记录为参数/别名逃逸。
    [Fact]
    public void StoringInputAliasIntoField_IsDetected()
    {
        var src = Header + """

        public sealed class Bag : IConstrained<ImmutableValue>
        {
            private readonly List<int> _items;
            public Bag(List<int> input) { _items = input; }
            public int Count => _items.Count;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(),
            "把调用方可变集合存入 readonly 字段应被检出（别名保留），不得静默通过");
    }

    // F1 判别：构造后通过局部别名修改 receiver 的集合 → 必须检出（不能因经局部变量而漏）。
    [Fact]
    public void MutatingReceiverCollection_ViaLocalAlias_IsDetected()
    {
        var src = Header + """

        public sealed class Bag : IConstrained<ImmutableValue>
        {
            private readonly List<int> _items = new();
            public void Mutate() { var alias = _items; alias.Clear(); }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(), "经局部别名的 receiver 集合修改应被检出");
    }

    // F11 判别：事件订阅（receiver 字段写入路径）必须被检出。
    [Fact]
    public void SubscribingEventOnReceiver_IsDetected()
    {
        var src = Header + """

        public sealed class Ev : IConstrained<ImmutableValue>
        {
            private event Action? Changed;
            public void Hook(Action h) { Changed += h; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(), "事件订阅修改 receiver 状态应被检出");
    }

    // F1/F3 判别：公开可变属性暴露内部集合 → 必须检出。
    [Fact]
    public void ExposingInternalMutableCollection_IsDetected()
    {
        var src = Header + """

        public sealed class W : IConstrained<ImmutableValue>
        {
            private readonly List<int> _inner = new();
            public List<int> Items => _inner;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(), "公开内部可变集合应被检出");
    }
}
