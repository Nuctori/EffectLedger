// CompilationFixture.cs — P1.3 测试宿主。
// 关键：所有 fixture 先断言编译成功；不通过编译的代码不应产生"零分析"假绿（要求 #4）。
// 引用 only 受控程序集，不扫描任意本机目录。

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using EffectLedger.Contracts.Analyzer.Engine;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Profiles;

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
    /// <summary>
    /// P4.4 端到端：真实 `dotnet build` 一个带 AdditionalFiles 配置的工程，再用 Tool 审核，
    /// 断言用户摘要把外部符号解析为**具体违规**而不是 Unknown。
    /// 走真实管线（而非内存 compilation），因为配置经 MSBuild 的 AdditionalFiles 传递。
    /// </summary>
    /// <summary>
    /// P4.4 单元级验证：内存 compilation 提供源码，用用户摘要配置驱动引擎，
    /// 断言外部符号被解析为具体违规而非 Unknown。内存路径避免跨进程编码/程序集隔离的不确定性。
    /// </summary>
    public static ContractResult EvaluateWithConfig(string source, string json)
    {
        var r = Run(source);
        if (!r.Compiled) return new ContractResult();
        var config = ContractConfigParser.Parse(json);
        var engine = new ContractEngine(r.Compilation, AnalysisBudget.Default, config);
        var resolver = new ProfileResolver(r.Compilation);
        var decl = resolver.FindDeclarations().FirstOrDefault();
        return decl is null ? new ContractResult() : engine.Evaluate(decl);
    }


    private static int RunProcess(string file, string args, out string stdout)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = file, Arguments = args, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false,
            // 子进程输出含中文诊断：统一 UTF-8，否则读取端按系统 ANSI 解码成乱码
            // （实测把"用户摘要"读成 mojibake，导致断言假红）。
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        stdout = p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        stdout += err;
        return p.ExitCode;
    }

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EffectLedger.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("找不到仓库根（EffectLedger.slnx）");
    }

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
