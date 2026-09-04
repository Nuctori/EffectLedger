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
        // R3-CG-01（三轮审计）翻转：Instantiate 的 Tree.new_id 幻影声明已删（无 release 端 ⇒ 标准
        // Instantiate→AddChild→QueueFree 生命周期恒报 EAA0901 的结构性误报）——Use 现为完整生命周期，不报。
        public void Use() { var n = _scene.Instantiate(); _node.AddChild(n); n.QueueFree(); }
    }
}";

    [Fact]
    public async Task R11_Analyzer_FiresOnRealGodotShapedLeak_NotOnPaired()
    {
var refs = CompilationRefs.Lean(typeof(Godot.Shapes.Node).Assembly.Location);
        var comp = CSharpCompilation.Create("R11Game",
            new[] { CSharpSyntaxTree.ParseText(Source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn")); // 泄漏必报（方法名 Spawn）
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Burst"));
        // R3-CG-01 翻转后：Use（Instantiate→AddChild→QueueFree 完整生命周期）也不再报
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Use"));
    }
}
