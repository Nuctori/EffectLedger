// ProdAuditR3AnalyzerTests.cs — 第三轮独立审计（2026-09）L3 分析器回归钉。
// R3-CG-01：Instantiate 白名单模型修正——Wr(Tree("new_id")) 是永不配对的幻影资源
//           （无任何 release 端），标准 Instantiate→AddChild→QueueFree 生命周期恒报 EAA0901
//           且诊断建议的修复(1)在白名单内不可能达成（结构性误报，每个 spawn 方法都中）。
// R3-CG-02：EAA0303 KIND_MIX 须按 API 归并调用站点——同一白名单 API 重复调用
//           （AddChild×2）的多 kind 是白名单内部多态（类注释明言"非用户混用"），不得报。
// R3-CG-06：[EffectOverride]/[AcceptDeviation] 的 AttributeUsage 收窄到 Method
//           （L2/L3 只消费方法节点；类/属性级标注此前完全静默无效）。
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EffectLedger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

public sealed class ProdAuditR3AnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location),
        };
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name == "System.Runtime" || name == "System.Collections.Immutable")
                refs.Add(MetadataReference.CreateFromFile(p));
        }
        var compilation = CSharpCompilation.Create(
            "R3Analyzer", new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private const string Stub = @"
namespace Godot.Shapes
{
    public sealed class GNode { public void AddChild(object c) { } public void QueueFree() { } }
    public sealed class PackedScene { public GNode Instantiate() { return new GNode(); } }
}";

    // ── R3-CG-01：标准 spawn 生命周期不得报 EAA0901 ──
    [Fact]
    public async Task Instantiate_AddChild_QueueFree_NoLeakDiagnostic()
    {
        var diags = await RunAnalyzer(Stub + @"
namespace GameCode
{
    public sealed class Spawner
    {
        private readonly Godot.Shapes.GNode _node = new();
        private readonly Godot.Shapes.PackedScene _scene = new();
        public void Use()
        {
            var n = _scene.Instantiate();
            _node.AddChild(n);
            n.QueueFree();
        }
    }
}");
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901"); // 修改前：Tree(""new_id"") 永不配对 ⇒ 恒报
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303");
    }

    // ── R3-CG-02：同 API 重复调用不报 KIND_MIX（A4 冲突钉保持不变） ──
    [Fact]
    public async Task SameApiTwice_NoKindMix_ConflictPinned()
    {
        var diags = await RunAnalyzer(Stub + @"
namespace GameCode2
{
    public sealed class MultiAdd
    {
        private readonly Godot.Shapes.GNode _g = new();
        public void Twice()
        {
            _g.AddChild(new object());
            _g.AddChild(new object());
        }
    }
}");
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303"); // 修改前：跨站点 {Write,Occupy} 误报 KIND_MIX
        Assert.Contains(diags, d => d.Id == "EAA0304");       // Create×Create 冲突语义保持（AnalyzerCompleteness 钉）
    }

    // ── R3-CG-06：AttributeUsage 收窄到 Method ──
    [Fact]
    public void EffectAttributes_Usage_NarrowedToMethod()
    {
        var ov = typeof(EffectOverrideAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        var ad = typeof(AcceptDeviationAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(ov);
        Assert.NotNull(ad);
        Assert.Equal(AttributeTargets.Method, ov!.ValidOn); // 修改前：含 Class/Property/Field——L2/L3 均不消费，静默无效
        Assert.Equal(AttributeTargets.Method, ad!.ValidOn);
    }
}
