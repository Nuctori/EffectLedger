using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// §14.3 完备性判据 A3(KIND_MIX)/A4(Compat 冲突) 的 L3 诊断测试（迭代29 补，对应 iter-code29 OPEN-1/OPEN-2）。
/// 数学已由 L1 类型保护（§3.1.4b 三桶隔离、§3.2.3 Compatible 全函数）；此处仅验证 L3 诊断显式落地、数据驱动、且逃逸通道生效。
/// 诊断数据全部来自 §7 GodotApiWhitelist / §3.2.3 Compatible（零 Godot 依赖）。
/// </summary>
public sealed class AnalyzerCompletenessTests
{
    // 最小可编译编译（仅引用 object + L1 + 运行时基础）；与 ToolingTests 同源构造，避免跨类依赖私有成员。
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = DefaultRefs().ToArray();
        return CSharpCompilation.Create(
            "AnalyzerCompletenessAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static System.Collections.Generic.IEnumerable<MetadataReference> DefaultRefs()
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

    // §14.3 A3 / §3.1.4b DO-7：同一方法内 Connect（Wr Self("signal_x")→SignalBus）与 IsConnected（Rd Self("signal_x")→SignalBus）
    // 同归一资源 SignalBus("signal_x") 跨调用混用 Read/Write ⇒ 报 EAA0303（无 [EffectOverride]）。
    [Fact]
    public async Task Analyzer_ReportsKindMixOnSameResource()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    public void Connect(object s, object c) { }
    public void IsConnected(object s) { }
    public void SignalMix()
    {
        Connect(new object(), new object());
        IsConnected(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0303");
    }

    // §14.3 A4 / §3.2.3：同方法内两次 AddChild（Create×2 同 Tree("node.id")）无 release、无 [EffectOverride] ⇒ 报 EAA0304。
    // 也触发 EAA0901（acquire 无 release），但本断言仅锁定 A4（EAA0304 存在）。
    [Fact]
    public async Task Analyzer_ReportsCompatConflictOnSameResource()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    public void AddChild(object x) { }
    public void DoubleCreate()
    {
        AddChild(new object());
        AddChild(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0304");
    }

    // §8.3 / §14.3 A3+A4 逃逸通道：标 [EffectOverride("...")] 的方法不报 A3/A4（hasEscape 跳过）。
    [Fact]
    public async Task Analyzer_NoKindMixOrConflictWhenOverrideAttr()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    public void Connect(object s, object c) { }
    public void IsConnected(object s) { }
    public void AddChild(object x) { }
    [EffectOverride(""intent: paired signal + node lifecycle"")]
    public void MixedWithOverride()
    {
        Connect(new object(), new object());
        IsConnected(new object());
        AddChild(new object());
        AddChild(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303");
        Assert.DoesNotContain(diags, d => d.Id == "EAA0304");
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }

    // §14.3 A3/A4 近似护栏：单条 API 内部天然跨 kind（AddChild 含 Write+Occupy）或重复同模式（Load 含两次 Occupy(Create)）
    // 不应触发 A3/A4（仅检测跨调用站点，单 API 内部多态是 PDR §7 有意设计，非用户混用）。
    [Fact]
    public async Task Analyzer_NoFalsePositiveForSingleApiInternalClaims()
    {
        const string source = @"
using Cosmos.EffectAlgebra;
public class Sample
{
    public void AddChild(object x) { }    // §7 内部 Write+Occupy(Tree) — 单 API 不报 A3
    public void Load(string p) { }        // §7 内部两次 Occupy(Mem,Create) — 单 API 不报 A4
    public void SingleApiOnly()
    {
        AddChild(new object());
        Load(""res"");
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303");
        Assert.DoesNotContain(diags, d => d.Id == "EAA0304");
        // 注意：SingleApiOnly 仍含 acquire(AddChild/Load) 且无 release ⇒ EAA0901 仍应报（DO-9 近似独立），
        // 此处不断言 EAA0901 存在/缺失，避免与 A3/A4 护栏断言耦合。
    }
}
