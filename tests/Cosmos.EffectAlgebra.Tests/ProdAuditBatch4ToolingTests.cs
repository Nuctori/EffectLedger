// ProdAuditBatch4ToolingTests.cs — 独立生产就绪审计（2026-09）批 4 回归钉：L2 生成器 / L3 分析器 / 门禁存在性。
// 每条对应审计发现编号（A2-xx）：先红后绿（TDD），防漂移。
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public sealed class ProdAuditBatch4ToolingTests
{
    // ── Roslyn 测试基建（与 ToolingTests 同型拷贝，保持测试文件自包含） ──
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = DefaultRefs().ToArray();
        return CSharpCompilation.Create(
            "ProdAuditBatch4Assembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> DefaultRefs()
    {
        yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(Cosmos.EffectAlgebra.Claim).Assembly.Location);
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name == "System.Runtime" || name == "System.Collections.Immutable")
                yield return MetadataReference.CreateFromFile(p);
        }
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var compilation = MakeCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new Cosmos.EffectAlgebra.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static (ImmutableArray<Diagnostic> genDiagnostics, CSharpCompilation output) RunGenerator(string source)
    {
        var compilation = MakeCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return (driver.GetRunResult().Diagnostics, (CSharpCompilation)outputCompilation);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cosmos.EffectAlgebra.slnx")))
            dir = dir.Parent!;
        return dir!.FullName;
    }

    // ── A2-03a：[AcceptDeviation(double.NaN)] 必须触发 EAA0802（NaN 骗过 e<0 || e>0.5 的双层穿透） ──
    [Fact]
    public async Task Analyzer_AcceptDeviationNaN_ReportsEAA0802()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class C
{
    [AcceptDeviation(double.NaN)]
    public void M() { }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0802"); // 修改前：NaN 两个比较均 false ⇒ 零诊断（ε 逃逸）
    }

    // ── A2-03b：L1 构造子同样拦 NaN（双层同界） ──
    [Fact]
    public void AcceptDeviationCtor_NaN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Cosmos.EffectAlgebra.AcceptDeviationAttribute(double.NaN));
    }

    // ── A2-04：生成器须识别 global:: 全限定特性（AliasQualifiedNameSyntax），与 L3 行为一致 ──
    [Fact]
    public void Generator_GlobalQualifiedOverrideAttribute_Emits()
    {
        const string source = @"
public class C
{
    [global::Cosmos.EffectAlgebra.EffectOverride(""r"")]
    public void Load() { }
}";
        var (genDiags, output) = RunGenerator(source);
        var text = string.Join("\n", output.SyntaxTrees.Select(t => t.ToString()));
        Assert.DoesNotContain(genDiags, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("ComputeLoad", text); // 修改前：AliasQualifiedNameSyntax 落入 _ => null ⇒ 静默少生成
    }

    // ── A2-05：不同命名空间的同名类型各含同名标注方法 ⇒ 生成产物必须可编译（此前 _Cfg_0 撞名 CS0111） ──
    [Fact]
    public void Generator_SameNameTypesInNamespaces_EmitsCompilableOutput()
    {
        const string source = @"
namespace NS1 { public class Cfg { [Cosmos.EffectAlgebra.EffectOverride(""r"")] public void Load() { } } }
namespace NS2 { public class Cfg { [Cosmos.EffectAlgebra.EffectOverride(""r"")] public void Load() { } } }
";
        var (genDiags, output) = RunGenerator(source);
        Assert.DoesNotContain(genDiags, d => d.Severity == DiagnosticSeverity.Error);
        using var pe = new MemoryStream();
        var result = output.Emit(pe);
        Assert.True(result.Success,
            "两同名类型 + 同名方法应生成可编译产物；修改前两份 ComputeLoad_Cfg_0 落入同一 partial 类（CS0111）："
            + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage())));
    }

    // ── A2-14：消费者自有全局 EffectAlgebraGenerated 不得 CS0101（生成类移入 namespace 后须可编译） ──
    [Fact]
    public void Generator_ConsumerOwnGeneratedClass_NoCollision()
    {
        const string source = @"
public static partial class EffectAlgebraGenerated { }
public class C
{
    [Cosmos.EffectAlgebra.EffectOverride(""r"")]
    public void Load() { }
}";
        var (genDiags, output) = RunGenerator(source);
        Assert.DoesNotContain(genDiags, d => d.Severity == DiagnosticSeverity.Error);
        using var pe = new MemoryStream();
        var result = output.Emit(pe);
        Assert.True(result.Success,
            "消费者自有同名全局类应可共存（生成类已在命名空间内）："
            + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage())));
    }

    // ── A2-09：类型门须精确匹配 Godot 命名空间——用户 GodotXxx 前缀命名空间的自有 Load 不得误定罪 ──
    [Fact]
    public async Task Analyzer_UserNamespaceWithGodotPrefix_NotFlagged()
    {
        const string source = @"
namespace GodotTesting.Utils { public class C { public void Load(string p) { } } }
public class Consumer
{
    void M() { new GodotTesting.Utils.C().Load(""x""); }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901"); // 修改前：StartsWith("Godot") 放行 ⇒ EAA0901 假红
    }

    // ── A2-02：EAA*=error 门禁存在性——泄漏 fixture 构建必须红（对门禁做 mutation 测试） ──
    [Fact]
    public void GateFixture_Leaky_BuildFails_WithEAA0901()
    {
        var (code, output) = DotnetBuild(Path.Combine(RepoRoot(), "tests", "GateFixture", "Leaky"));
        Assert.True(code != 0, $"泄漏 fixture 构建应为红（EAA0901=error 未被行使）：\n{output}");
        Assert.Contains("EAA0901", output);
    }

    [Fact]
    public void GateFixture_Paired_BuildsClean()
    {
        var (code, output) = DotnetBuild(Path.Combine(RepoRoot(), "tests", "GateFixture", "Paired"));
        Assert.True(code == 0, $"配对 fixture 构建应绿：\n{output}");
    }

    private static (int code, string output) DotnetBuild(string projectDir)
    {
        var psi = new ProcessStartInfo("dotnet", $"build \"{projectDir}\" -c Release --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output);
    }
}
