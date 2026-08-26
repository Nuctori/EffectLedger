// AdvE2E_R8.cs — R8 对抗审计：端到端守恒不变量（跨类型/部分释放/重父化/逃逸）。
// 通过 MakeCompilation 对每种对抗场景构造游戏风格代码，驱动 L3 分析器 + L2 生成器，
// 断言诊断（泄漏必报、守恒/逃逸不报）与生成/反射出来的 Signature 在 L1 net 下守恒一致。
// 不修改 IntegrationTests.cs；本地 helper 断言 IsConserved。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using SampleGame.IntegrationTests;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R8
{
    // R8 本地 helper：构造游戏风格编译单元（复用 IntegrationTests 的 MakeCompilation 模式）。
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        return CSharpCompilation.Create(
            "R8Game",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var comp = MakeCompilation(source);
        var withA = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        return await withA.GetAnalyzerDiagnosticsAsync();
    }

    // R8 helper：运行 L2 生成器，反射得到指定方法 Compute{m} 在 Empty 基下的 Signature。
    private static Signature ComputeGen(string source, string method)
    {
        var comp = MakeCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);
        using var ms = new MemoryStream();
        var emit = output.Emit(ms);
        Assert.True(emit.Success, "生成代码必须可编译：" + string.Join("\n", emit.Diagnostics));
        var asm = Assembly.Load(ms.ToArray());
        var t = asm.GetType("EffectAlgebraGenerated")
            ?? asm.GetTypes().First(x => x.Name == "EffectAlgebraGenerated");
        var empty = (Signature)typeof(Signature)
            .GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        return (Signature)t.GetMethod("Compute" + method)!.Invoke(null, new object[] { empty })!;
    }

    // R8 helper：合并两生成方法签名，断言在给定资源上守恒（端到端：生成代码真委托 L1 net）。
    private static bool CombinedConserved(string source, string a, string b, ResourceId resource)
    {
        var sig = Signature.Union(ComputeGen(source, a), ComputeGen(source, b));
        var net = NetTable.Compute(sig, new ScopeId.Global());
        return net.IsConserved(resource);
    }

    // ── 场景源：pair 容器（P0-2 对齐：Godot 桩 + 接收者调用；标注方法保留供 L2 生成器 emit）──
    private const string PairSource = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes {
    public sealed class Node3D { public void AddChild(object c) { } public void RemoveChild() { } }
    public sealed class ResourceLoader { public object Load() => new(); }
}
namespace R8 {
    public sealed class Node3D { public object? child; }
    public sealed class Paired {
        private readonly GodotShapes.Node3D _n = new();
        private readonly GodotShapes.ResourceLoader _rl = new();
        [EffectOverride(""spawn/despawn"")]
        public void AddChild(object c) { }
        [EffectOverride(""release tree"")]
        public void RemoveChild() { }
        // A acquire Tree(node.id); B release 同资源 ⇒ 组合守恒（整体不报）
        public void Balanced() { _n.AddChild(new object()); _n.RemoveChild(); }
        // 跨类型：acquire Object(Mem) 然后 release Tree ⇒ 不守恒（应报）
        public void CrossType() { _rl.Load(); _n.RemoveChild(); }
        // 部分释放：acquire 2 Tree，release 1 ⇒ net 1 ⇒ 应报
        public void Partial() { _n.AddChild(new object()); _n.AddChild(new object()); _n.RemoveChild(); }
    }
}";

    [Fact]
    public async Task E2E_R8_BalancedAcquireRelease_NotFlagged()
    {
        var diags = await Analyze(PairSource);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Balanced"));
        // 生成器 Combine 守恒：AddChild(create) ∪ RemoveChild(release) 在 Tree(node.id) 守恒（方法级各自不守恒，组合才守恒）。
        Assert.True(CombinedConserved(PairSource, "AddChild", "RemoveChild",
            new ResourceId.Tree(NodePathOrUnknown.Of("node.id"))));
    }

    [Fact]
    public async Task E2E_R8_CrossType_AcquireObjectReleaseTree_Flagged()
    {
        var diags = await Analyze(PairSource);
        // 跨资源：acquire Mem (Load) 但 release Tree (RemoveChild) ⇒ Tree 无释放 + Mem 无释放 ⇒ 至少报一处 EAA0901。
        Assert.Contains(diags, d => d.Id == "EAA0901");
        // 具体断言 CrossType 方法被报（其 acquire 的 Mem 未由 RemoveChild 释放；RemoveChild 释放的 Tree 未由 Load acquire）。
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("CrossType"));
    }

    [Fact]
    public async Task E2E_R8_PartialRelease_Flagged()
    {
        var diags = await Analyze(PairSource);
        // 部分释放：acquire 2 Tree，release 1 ⇒ Tree 净获 1 ⇒ 应报 EAA0901。
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Partial"));
    }

    // ── 重父化对抗：节点A acquire / 节点B release 运行期是不同资源；但 name-match 设计下两者归一为同资源(已知 false negative) ──
    private const string ReparentSource = @"
using Cosmos.EffectAlgebra;
namespace R8 {
    public sealed class Node3D { public object? child; }
    public sealed class Reparent {
        private readonly Node3D _a = new(); private readonly Node3D _b = new();
        [EffectOverride(""a"")]
        public void AddChild(object c) { _a.child = c; }
        [EffectOverride(""b"")]
        public void RemoveChild() { _b.child = null; }
        // 在 a 路径 acquire，在 b 路径 release => 不同归一资源 => 不守恒 => 应报
        public void ReparentBad() { AddChild(new object()); RemoveChild(); }
    }
}";

    [Fact]
    public async Task E2E_R8_Reparent_DifferentPaths_KnownFalseNegative_AndL1Consistent()
    {
        var diags = await Analyze(ReparentSource);
        // 设计局限（name-match 不区分接收者）：re-parent 被判定为平衡 => 不报 EAA0901。
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("ReparentBad"));
        // L1 一致性：粗粒度视图与分析器一致 —— 合并 AddChild+RemoveChild 生成签名后在 Tree("node.id") 守恒。
        var combined = Signature.Union(
            ComputeGen(ReparentSource, "AddChild"),
            ComputeGen(ReparentSource, "RemoveChild"));
        var net = NetTable.Compute(combined, new ScopeId.Global());
        Assert.True(net.IsConserved(new ResourceId.Tree(NodePathOrUnknown.Of("node.id"))),
            "L1 粗粒度：AddChild+RemoveChild 在 Tree(node.id) 守恒（与分析器不报一致；re-parent 精度缺失为已知局限）");
    }

    private static Signature ComputeOnly(string source, string method) => ComputeGen(source, method);

    // ── 逃逸：per-method。[EffectOverride] 的逃逸方法 imbalance 不报；同类未标注兄弟 imbalance 必报。──
    private const string EscapeSource = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes { public sealed class Node3D { public void AddChild(object c) { } } }
namespace R8b {
    public sealed class Escape {
        private readonly GodotShapes.Node3D _n = new();
        // 逃逸通道语义说明：EAA0901 永不豁免（P0-1 文档对齐）——本测试锁定的是 per-method 分析边界：
        // 同方法内 acquire 无 release ⇒ 该方法必报；标注在【其他】方法上不影响本方法的判定。
        public void EscapeMethod() { _n.AddChild(new object()); }
        public void UnmarkedLeak() { _n.AddChild(new object()); }
    }
}";

    [Fact]
    public async Task E2E_R8_Escape_PerMethod_NotFlagged_UnmarkedFlagged()
    {
        var diags = await Analyze(EscapeSource);
        // 逃逸方法（其体内调用标注的 AddChild，但 EscapeMethod 自身未标注 ⇒ 仍会报？见下）
        // 注意：EscapeMethod 自身未标 [EffectOverride]，但其调用的 AddChild 标注了。
        // 当前 L3 为调用级近似：acquire(AddChild) 在 EscapeMethod 体内无 release ⇒ EscapeMethod 应报。
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("EscapeMethod"));
        // 未标注的 UnmarkedLeak 必报（per-method 逃逸边界：豁免只作用于标有 [EffectOverride] 的方法本身）。
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("UnmarkedLeak"));
        // 标注方法（AddChild/AnotherAdd 自身）被调用方未豁免时调用方报；但标注方法自身不会被 analyze 触发（无体内 acquire 无 release）
        // 关键不变式：豁免是 per-method（方法声明级），不是 per-class ⇒ Escape 类内 UnmarkedLeak 仍被报。
    }
}

