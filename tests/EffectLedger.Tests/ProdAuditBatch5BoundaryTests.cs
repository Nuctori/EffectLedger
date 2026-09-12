// ProdAuditBatch5BoundaryTests.cs — 独立生产就绪审计（2026-09）批 5 回归钉。
// A2-08：EAA0901 控制流近似的边界此前完全无测试钉（实现为纯语法计数，无路径/作用域概念）——
// 六个形态逐条钉住【当前行为】，每条标注「接受的近似」或「待修」；实现演进（如引入真数据流）时翻转对应断言即可。
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

public sealed class ProdAuditBatch5BoundaryTests
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
            "Batch5Boundary", new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private const string Stub = @"
namespace Godot.Shapes
{
    public class Node3D : System.IDisposable
    {
        public Node3D AddChild(Node3D n) => n;   // acquire：Tree create
        public void QueueFree() { }              // release：Tree release
        public Node3D Instantiate() => this;     // acquire：Mem create（Dynamic）
        public void Dispose() { }                // 非白名单 API：Dispose 不计入 release
    }
}
public class Consumer
{
    private readonly Godot.Shapes.Node3D _g = new();
";

    [Theory]
    [InlineData(1, false)] // (1) if/else 分路：一路 acquire 一路 release ⇒ 语法计数 1:1 抵消 ⇒ 不报【接受的近似（假阴）】
    [InlineData(2, false)] // (2) early return：acquire 后条件 return，release 在其后 ⇒ 计数抵消 ⇒ 不报【已知假阴：真实泄漏路径漏报】
    [InlineData(3, true)]  // (3) using var + Instantiate ⇒ Dispose 不在白名单 ⇒ Mem acquire 无配对 ⇒ 报【已知假阳候选（Dispose 是真释放但白名单外）】
    [InlineData(4, false)] // (4) try/finally 配对 ⇒ 计数抵消 ⇒ 不报【接受的近似】
    [InlineData(5, false)] // (5) 循环内 acquire + 循环外一次 release ⇒ 语法计数 1:1 ⇒ 不报【接受（循环倍率归运行期 Σnet 权威）】
    [InlineData(6, false)] // (6) 本地函数含 QueueFree 但从未调用 ⇒ 幽灵 release 计入 ⇒ 抵消 ⇒ 不报【已知假阴：幽灵抵消】
    public async Task EAA0901_ControlFlowBoundary_PinsCurrentBehavior(int shape, bool expectReport)
    {
        var body = shape switch
        {
            1 => @"if (System.DateTime.Now.Ticks > 0) { var c = _g.AddChild(new Godot.Shapes.Node3D()); _ = c; }
                   else { _g.QueueFree(); }",
            2 => @"var c = _g.AddChild(new Godot.Shapes.Node3D());
                   if (c is null) return;
                   _g.QueueFree();",
            3 => @"using var x = _g.Instantiate();
                   _ = x;",
            4 => @"Godot.Shapes.Node3D? c = null;
                   try { c = _g.AddChild(new Godot.Shapes.Node3D()); }
                   finally { c?.QueueFree(); }",
            5 => @"for (int i = 0; i < 3; i++) { var c = _g.AddChild(new Godot.Shapes.Node3D()); _ = c; }
                   _g.QueueFree();",
            _ => @"Godot.Shapes.Node3D Local() { var t = new Godot.Shapes.Node3D(); t.QueueFree(); return t; }
                   var c = _g.AddChild(new Godot.Shapes.Node3D()); _ = c; _ = ((System.Func<Godot.Shapes.Node3D>)Local);",
        };
        var source = Stub + "    public void M()\n    {\n" + body + "\n    }\n}";
        var diags = await RunAnalyzer(source);
        if (expectReport)
            Assert.Contains(diags, d => d.Id == "EAA0901");
        else
            Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }
}
