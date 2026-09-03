using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace SampleGame.IntegrationTests;

// R11 — 真实 Godot 4.6.3 API 形状验证：用 GodotApiStub（按 §7 白名单的 28 个真实 Godot 方法名 + 真实签名）
// 驱动 L3 分析器，证明「按方法名规范化匹配 §7 白名单」设计对接近真实 Godot 方法形状（本机无 Godot 引擎，用结构等价 stub 替代）。
// 断言：仅 acquire（AddChild）无 release 且无 [EffectOverride] ⇒ EAA0901；Instantiate + QueueFree 配对 ⇒ 不报。
public sealed class AdvE2E_R11
{
    private const string Source = @"
using Godot.Shapes;
namespace SampleGame {
    public sealed class RealShapedLeaker {
        private readonly Node3D _node = new();
        // 真实 Godot 签名形状：Node AddChild(Node)，仅 acquire，无 [EffectOverride] ⇒ 应触发 EAA0901
        public void Spawn(Node child) { _node.AddChild(child); }
    }
    public sealed class RealShapedBalanced {
        private readonly Node3D _node = new();
        private Node? _child;
        // AddChild(acquire) + QueueFree(release, §7 release-class) 配对 ⇒ 不报
        public void Burst(Node child) { _child = _node.AddChild(child); _node.QueueFree(); }
    }
    public sealed class RealShapedPooled {
        private readonly Node3D _node = new();
        private PackedScene _scene = new();
        // Instantiate(acquire Tree.new_id) + 把实例加入 _node(AddChild acquire) 后 QueueFree(释放 _node 的 Tree.node.id) ⇒ 跨资源不配对⇒ 报；
        // 仅作「真实 Godot 形状」演示：配对须在归一资源上，见 RealShapedBalanced。
        public void Use() { var n = _scene.Instantiate(); _node.AddChild(n); n.QueueFree(); }
    }
}";

    [Fact]
    public async Task R11_Analyzer_FiresOnRealGodotShapedLeak_NotOnPaired()
    {
        var refs = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Godot.Shapes.Node).Assembly.Location));
        var comp = CSharpCompilation.Create("R11Game",
            new[] { CSharpSyntaxTree.ParseText(Source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn")); // 泄漏必报（方法名 Spawn）
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Burst"));
        // Use 因 Instantiate/AddChild 与 QueueFree 跨归一资源（new_id ≠ node.id）不配对，预期仍报；此处仅断言 Burst 配对不报
    }
}
