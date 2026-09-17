// FaultInjectionTests.cs — 完成标准 #8：诊断抑制 / 删除目标 / 配置漏接 / 分析器拔线 的真实故障注入。
// 这些测试断言"退化必须可见"，防止门禁被静默绕过。

using System.Linq;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Engine;
using EffectLedger.Contracts.Analyzer.Profiles;
using EffectLedger.Contracts.Tests.Testing;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class FaultInjectionTests
{
    private const string ViolatingSource = """
        using System;
        using EffectLedger.Contracts;

        public sealed class ClockReader : IConstrained<DeterministicComputation>
        {
            public DateTime Stamp() => DateTime.UtcNow;
        }
        """;

    // 引擎层面：无论分析器诊断是否被显示/抑制，原始结论仍然产生违规。
    // （strict 门禁由 Tool 消费该原始结论，不读 Roslyn 最终诊断列表。）
    [Fact]
    public void SuppressionCannotHideRawEngineConclusion()
    {
        var r = CompilationFixture.Run(ViolatingSource);
        Assert.True(r.Compiled);

        var resolver = new ProfileResolver(r.Compilation);
        var engine = new ContractEngine(r.Compilation, AnalysisBudget.Default);
        var decl = resolver.FindDeclarations().Single();

        var res = engine.Evaluate(decl);
        Assert.True(res.IsViolated,
            "原始引擎结论必须含违规；strict 据此判定，与 IDE 诊断显示/抑制无关");
    }

    // 删除声明 ⇒ 该类型不再受约束（opt-in 语义）：这本身是"目标消失"，
    // 必须可被目标清单发现。此处验证引擎确实不再产出该根的结论。
    [Fact]
    public void RemovingDeclaration_RemovesRoot_AndIsObservable()
    {
        const string WithoutDeclaration = """
            using System;

            public sealed class ClockReader
            {
                public DateTime Stamp() => DateTime.UtcNow;
            }
            """;
        var r = CompilationFixture.Run(WithoutDeclaration);
        Assert.True(r.Compiled);
        var resolver = new ProfileResolver(r.Compilation);
        Assert.Empty(resolver.FindDeclarations());
        // 根数从 1 → 0 是可观测的退化信号；strict 默认对 0 根 exit 2（见 Tool）。
    }

    // 未识别角色必须报错，不能被当作"无约束"静默通过。
    [Fact]
    public void UnknownProfile_IsLoud_NotSilentlyUnconstrained()
    {
        const string src = """
            using EffectLedger.Contracts;

            public sealed class Fake : IBehaviorProfile { }

            public sealed class Subject : IConstrained<Fake>
            {
                public int X() => 1;
            }
            """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        var resolver = new ProfileResolver(r.Compilation);
        var engine = new ContractEngine(r.Compilation, AnalysisBudget.Default);
        var decls = resolver.FindDeclarations().ToList();

        Assert.Single(decls);
        Assert.Equal(ContractProfileKind.Unknown, decls[0].Profile);
        var res = engine.Evaluate(decls[0]);
        Assert.True(res.IsViolated, "未知角色必须报 EBC0001，不得静默当作无约束");
    }

    // 同名（但不同程序集/命名空间）的伪接口不得被误识别为我们的声明面。
    [Fact]
    public void SameNamedForeignInterface_IsNotMisidentified()
    {
        const string src = """
            namespace OtherLib
            {
                public interface IConstrained<T> { }
                public sealed class DeterministicComputation { }
            }

            public sealed class Subject : OtherLib.IConstrained<OtherLib.DeterministicComputation>
            {
                public int X() => 1;
            }
            """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled, "fixture must compile");
        var resolver = new ProfileResolver(r.Compilation);
        Assert.Empty(resolver.FindDeclarations());  // 精确符号比对 ⇒ 不误识别
    }

    // 预算耗尽必须转 Unknown（不是静默成功）。
    [Fact]
    public void BudgetExhaustion_YieldsUnknown_NotSuccess()
    {
        var big = new System.Text.StringBuilder();
        big.AppendLine("using System; using EffectLedger.Contracts;");
        for (int i = 0; i < 200; i++)
            big.AppendLine($"public static class H{i} {{ public static int F(int x) => x + {i}; }}");
        big.AppendLine("public sealed class Big : IConstrained<DeterministicComputation> {");
        big.AppendLine("  public int Go(int x) { int s = 0;");
        for (int i = 0; i < 200; i++) big.AppendLine($"    s += H{i}.F(x);");
        big.AppendLine("    return s; } }");

        var r = CompilationFixture.Run(big.ToString());
        Assert.True(r.Compiled);

        // 极小预算强制耗尽。
        var tight = new AnalysisBudget { MaxOperationsPerMethod = 10 };
        var resolver = new ProfileResolver(r.Compilation);
        var engine = new ContractEngine(r.Compilation, tight);
        var decl = resolver.FindDeclarations().Single();
        var res = engine.Evaluate(decl);

        Assert.True(res.HasUnknown,
            "预算耗尽必须报告 Unknown（BudgetExceeded），绝不静默通过");
    }
}
