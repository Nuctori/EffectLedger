// DeterministicComputationTests.cs — P4 反例/正例/未知例回归。
// 断言纪律（要求 #3）：支持的违规必须命中具体诊断，不能用 Unknown 代替；
// 不支持形状必须命中具体 Unknown 原因；合法形状必须真正通过。

using EffectLedger.Contracts.Tests.Testing;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class DeterministicComputationTests
{
    private const string Header = """
        using System;
        using System.Collections.Generic;
        using EffectLedger.Contracts;
        """;

    // ── 正例：显式输入 + 局部集合 + 不可逃逸修改 ──

    [Fact]
    public void ExplicitInput_WithLocalMutation_Passes()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Sum(int[] inputs)
            {
                var local = new List<int>();
                foreach (var x in inputs) local.Add(x * 2);
                int total = 0;
                foreach (var v in local) total += v;
                return total;
            }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled, "fixture must compile");
        Assert.Empty(r.WithId("EBC2001"));
        Assert.Empty(r.WithId("EBC2002"));
    }

    // ── 反例：直接隐藏时钟 ──

    [Fact]
    public void DirectClockRead_ReportsHiddenInput()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public DateTime Stamp() => DateTime.UtcNow;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC2001"));
    }

    // ── 反例：间接（经 helper）读取时钟 → 证明跨方法传播 ──

    [Fact]
    public void IndirectClockThroughHelper_ReportsHiddenInput()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Value() => Helper();

            private static int Helper() => DateTime.UtcNow.Day;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC2001"));
    }

    // ── 反例：外部 IO ──

    [Fact]
    public void ConsoleWrite_ReportsExternalEffect()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Do(int x) { Console.WriteLine(x); return x; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC2002"));
    }

    // ── 反例：修改输入 ──

    [Fact]
    public void MutatingInputParameter_ReportsExternalWrite()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public void Touch(List<int> input) { input.Add(1); }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC2002").Any(), "修改参数应报外部写入");
    }

    // ── 反例：静态可变状态 ──

    [Fact]
    public void StaticMutableState_Reports()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            private static int _counter;
            public int Next() { _counter = _counter + 1; return _counter; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.EbcViolations());
    }

    // ── 未知例：外部依赖无摘要 → Unknown，不是成功 ──

    [Fact]
    public void UnknownExternalDependency_ReportsUnknown()
    {
        var src = Header + """

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Read() => Environment.ExitCode;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.EbcViolations());
    }

    // ── 无声明类型的代码不产生约束噪声（opt-in） ──

    [Fact]
    public void UnconstrainedType_ProducesNoContractDiagnostics()
    {
        var src = Header + """

        public sealed class Free
        {
            public DateTime Stamp() => DateTime.UtcNow;
            public void Log() => Console.WriteLine("x");
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }
}
