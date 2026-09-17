// CompilationFixture.cs — P1.3 测试宿主。
// 关键：所有 fixture 先断言编译成功；不通过编译的代码不应产生"零分析"假绿（要求 #4）。
// 引用 only 受控程序集，不扫描任意本机目录。

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EffectLedger.Contracts.Tests.Testing;

public sealed class CompilationResult
{
    public Compilation Compilation { get; init; } = null!;
    public ImmutableArray<Diagnostic> ContractDiagnostics { get; init; }
    public bool Compiled { get; init; }
}

public static class CompilationFixture
{
    private static readonly string[] DefaultNamespaces =
    {
        "System", "System.Collections.Generic", "System.Linq", "System.Threading",
        "System.Threading.Tasks", "EffectLedger.Contracts",
    };

    /// <summary>把源码编译进带 Contracts + Contracts.Analyzer 的 compilation，并运行分析器。</summary>
    public static CompilationResult Run(params string[] sources)
    {
        var refs = BuildReferences();
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var compilation = CSharpCompilation.Create(
            "ContractsTest_" + Guid.NewGuid().ToString("N"),
            trees,
            refs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: true));

        var compileDiagnostics = compilation.GetDiagnostics();
        var compiled = true;
        foreach (var d in compileDiagnostics)
            if (d.Severity == DiagnosticSeverity.Error) compiled = false;

        // 强行运行分析器（验证真实加载）。
        var analyzer = new EffectLedger.Contracts.Analyzer.BehaviorContractAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var contractDiagnostics = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();

        return new CompilationResult
        {
            Compilation = compilation,
            ContractDiagnostics = contractDiagnostics,
            Compiled = compiled,
        };
    }

    private static List<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Threading.Tasks").Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            // 集合/不可变集合/反射面：缺失会让 `ImmutableArray<T>` 等解析为 TypeKind.Error，
            // 于是测试断言比较的是"错误类型"而非真实语义（用户视角审计发现：
            // 夹具曾静默把 ImmutableArray 判成不可变以外的形态，测出假结论）。
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections.Immutable").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime.Extensions").Location),
        };
        // Contracts 声明程序集（来自本项目）。
        var contractsAsm = typeof(EffectLedger.Contracts.IConstrained<>).Assembly;
        refs.Add(MetadataReference.CreateFromFile(contractsAsm.Location));
        return refs;
    }

    public static IEnumerable<Diagnostic> WithId(this CompilationResult r, string id)
        => r.ContractDiagnostics.Where(d => d.Id == id);

    public static IEnumerable<Diagnostic> EbcViolations(this CompilationResult r)
        => r.ContractDiagnostics.Where(d => d.Id.StartsWith("EBC", StringComparison.Ordinal));
}
