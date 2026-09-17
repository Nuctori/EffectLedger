// PerformanceTests.cs — P6.3 性能基线（固定语料，记录实测值；不设通过/失败阈值以免制造"绿灯"假象）。
// 目的：给出"分析成本随根数如何增长"的真实数据，而非宣称达标。

using System.Diagnostics;
using System.Text;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Engine;
using EffectLedger.Contracts.Analyzer.Profiles;
using EffectLedger.Contracts.Tests.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EffectLedger.Contracts.Tests;

public class PerformanceTests
{
    private readonly ITestOutputHelper _out;
    public PerformanceTests(ITestOutputHelper o) => _out = o;

    private static string Corpus(int roots)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using EffectLedger.Contracts;");
        sb.AppendLine("public static class Shared {");
        sb.AppendLine("  public static int Helper(int x) {");
        sb.AppendLine("    var list = new List<int>();");
        sb.AppendLine("    for (int i = 0; i < x; i++) list.Add(i);");
        sb.AppendLine("    int s = 0; foreach (var v in list) s += v; return s; }");
        sb.AppendLine("}");
        for (int i = 0; i < roots; i++)
        {
            sb.AppendLine($"public sealed class Root{i} : IConstrained<DeterministicComputation> {{");
            sb.AppendLine($"  public int Compute(int x) => Shared.Helper(x) + {i};");
            sb.AppendLine("}");
        }
        return sb.ToString();
    }

    private (long Ms, int Roots, int Diags) Measure(int roots)
    {
        var r = CompilationFixture.Run(Corpus(roots));
        Assert.True(r.Compiled);
        var resolver = new ProfileResolver(r.Compilation);
        var engine = new ContractEngine(r.Compilation, AnalysisBudget.Default);
        var sw = Stopwatch.StartNew();
        int n = 0;
        foreach (var d in resolver.FindDeclarations()) { engine.Evaluate(d); n++; }
        sw.Stop();
        return (sw.ElapsedMilliseconds, n, r.ContractDiagnostics.Length);
    }

    [Fact]
    public void Measure_ScalingCurve()
    {
        // 预热（JIT/缓存），不计入。
        Measure(10);

        var r100 = Measure(100);
        var r1000 = Measure(1000);

        _out.WriteLine($"roots=100  : {r100.Ms} ms (found={r100.Roots}, diags={r100.Diags})");
        _out.WriteLine($"roots=1000 : {r1000.Ms} ms (found={r1000.Roots}, diags={r1000.Diags})");
        double growth = r100.Ms == 0 ? 0 : (double)r1000.Ms / r100.Ms;
        _out.WriteLine($"增长倍数(1000/100) = {growth:F2}x （线性≈10x，超线性≈100x）");

        // 只做健全性断言：根数确实被找到（防止"快是因为什么都没做"）。
        Assert.Equal(100, r100.Roots);
        Assert.Equal(1000, r1000.Roots);
    }
}
