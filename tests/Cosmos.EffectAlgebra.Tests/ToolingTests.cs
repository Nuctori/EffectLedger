using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// §14 / L2+L3 — 工具层（Source Generator + Roslyn Analyzer）的集成测试。
/// 用 Roslyn 自带 API 构造最小编译，真实触发 L2/L3，不新增 nuget 测试包。
/// 注意：本测试仅验证「工具层真实触发且产出预期诊断/代码」，数学正确性由 L1 单测/性质测试负责（§3.1–§3.3）。
/// </summary>
public sealed class ToolingTests
{
    // ── 辅助：构造最小可编译的 C# 编译（仅引用 object + L1 + 运行时基础）──
    private static CSharpCompilation MakeCompilation(string source, params MetadataReference[] extra)
    {
        var refs = DefaultRefs().Concat(extra).ToArray();
        return CSharpCompilation.Create(
            "ToolingTestAssembly",
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

    // ── §3.3.1 DO-9 近似泄漏：运行 L3 Analyzer，返回全部 analyzer 诊断 ──
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var compilation = MakeCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new Cosmos.EffectAlgebra.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ── §L2：运行 L2 Source Generator，返回更新后的全部语法树文本 ──
    private static string RunGenerator(string source)
    {
        var compilation = MakeCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return output.SyntaxTrees.Select(t => t.ToString()).Aggregate((a, b) => a + "\n" + b);
    }

    // §14 L2 — 标注方法应触发生成器产出注册桩（class EffectAlgebraGenerated）。
    [Fact]
    public void Generator_EmitsRegistryForAnnotatedMethod()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class C
{
    [EffectOverrideAttribute(""r"")]
    public void M() { }
}";
        var generated = RunGenerator(source);
        Assert.Contains("EffectAlgebraGenerated", generated);
    }

    // §14 L2 — 标注方法应触发生成器产出真实每方法 Signature 组合代码（非桩）：
    // 生成文本必须真引用 L1（GodotApiWhitelist.All + Signature.Union），而非仅生成类壳。
    [Fact]
    public void Generator_EmitsRealSignatureDelegatingToL1()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class C
{
    [EffectOverrideAttribute(""r"")]
    public void M() { }
}";
        var generated = RunGenerator(source);
        Assert.Contains("EffectAlgebraGenerated", generated);
        Assert.Contains("GodotApiWhitelist.All", generated);   // 真引用 §7 白名单（非桩）
        Assert.Contains("Signature.Union", generated);           // 真委托 L1 代数（非桩）
        Assert.Contains("ComputeM", generated);                 // 每方法组合入口
    }

    // §14 L2 / §3.1.4b — 生成代码真可运行消费：编译含 [EffectOverride] 方法 + 生成器的 source，
    // 反射调用 EffectAlgebraGenerated.ComputeAddChild，断言返回 Signature 非空且含 Kind.Occupy
    // （AddChild ∈ §7 映射，其 Claims 含 Occupy(Tree(node.id),Create,Exact(1))）。
    // 注意：方法名用短形式 [EffectOverride("r")]（生成器 HasAttributeName 同时匹配短/长形式，§8.3）。
    [Fact]
    public void Generator_EmittedCompute_AddChild_ReturnsWhitelistedClaims()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    [EffectOverride(""r"")]
    public void AddChild(object x) { }
}";
        var compilation = MakeCompilationWithGenerator(source);
        Assert.False(compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            "生成代码编译不得有错误：" + string.Join("\n", compilation.GetDiagnostics()));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        Assert.True(emit.Success, "emit 必须成功：" + string.Join("\n", emit.Diagnostics));

        var asm = Assembly.Load(ms.ToArray());
        var genType = asm.GetType("EffectAlgebraGenerated")
            ?? asm.GetTypes().FirstOrDefault(t => t.Name == "EffectAlgebraGenerated");
        Assert.NotNull(genType);
        var method = genType.GetMethod("ComputeAddChild");
        Assert.NotNull(method);

        var empty = (global::Cosmos.EffectAlgebra.Signature)
            typeof(global::Cosmos.EffectAlgebra.Signature)
                .GetField("Empty", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;
        var result = (global::Cosmos.EffectAlgebra.Signature)method.Invoke(null, new[] { empty })!;
        var all = global::Cosmos.EffectAlgebra.SignatureExtensions.AllClaims(result).ToArray();
        Assert.NotEmpty(all);                                  // 含 §7 AddChild 的 Claims
        Assert.Contains(all, c => c.Kind == global::Cosmos.EffectAlgebra.Kind.Occupy);
    }

    // §14 L2 — 构造含生成器的编译（MakeCompilation + 运行 L2 Generator）。
    private static Compilation MakeCompilationWithGenerator(string source)
    {
        var refs = DefaultRefs().ToArray();
        var compilation = CSharpCompilation.Create(
            "ToolingGenAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return output;
    }

    // §3.3.1 DO-9 近似：方法内 acquire（AddChild∈§7 create）无对应 release-class ⇒ 报 EAA0901（至少 1 条）。
    [Fact]
    public async Task Analyzer_ReportsMissingRelease()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    public void AddChild(object x) { }
    public void QueueFree() { }
    public void AcquireNoRelease()
    {
        AddChild(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    // §3.3.1：方法内同时含 acquire(AddChild) 与 release-class(QueueFree) ⇒ 无 EAA0901 误报。
    [Fact]
    public async Task Analyzer_NoDiagnosticWhenReleased()
    {
        const string source = @"
public class Sample
{
    public void AddChild(object x) { }
    public void QueueFree() { }
    public void AcquireAndRelease()
    {
        AddChild(new object());
        QueueFree();
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }

    // §8.3.1：acquire 但标 [EffectOverride]（逃逸通道）⇒ 不报 EAA0901（hasEscape 跳过）。
    [Fact]
    public async Task Analyzer_NoDiagnosticWhenOverrideAttr()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    public void AddChild(object x) { }
    [EffectOverride(""r"")]
    public void AcquireWithOverride()
    {
        AddChild(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }
}
