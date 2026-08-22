// AdvE2E_R6.cs — Round 6 对抗审计：非侵入式接入鲁棒性 + 接收者限定调用漏报修复验证。
// 复用 IntegrationTests 的 MakeCompilation 驱动方式，但自包含以免改动既有文件。
// 关键可证伪断言：node.QueueFree() / this.QueueFree() / GetTree().Free() / body.ApplyForce() 等
// Godot 主流"接收者限定写法"若泄漏，必须被 EAA0901 捕获（修复前为 false negative）。
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R6
{
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        return CSharpCompilation.Create(
            "AdvIntegrationGameR6",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var comp = MakeCompilation(source);
        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new Cosmos.EffectAlgebra.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ── 接收者限定泄漏：node.AddChild 后缺 node.QueueFree ──
    private const string ReceiverLeakSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public void AddChild(object c) {} public void QueueFree() {} }
    public sealed class Leaky {
        private readonly Node3D _node = new();
        public void Spawn() { _node.AddChild(new object()); /* 缺 _node.QueueFree() */ }
    }
}";

    // ── 无 using Cosmos.EffectAlgebra：白名单是 L1 数据，不依赖 using，仍应命中 ──
    private const string NoUsingSource = @"
namespace SampleGame {
    public sealed class Node3D { public void AddChild(object c) {} public void QueueFree() {} }
    public sealed class Leaky {
        private readonly Node3D _node = new();
        public void Spawn() { _node.AddChild(new object()); }
    }
}";

    // ── 局部变量/字段名恰为 API 名（非调用）→ 不应误报 ──
    private const string NameOnlyNoCallSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public void AddChild(object c) {} public void QueueFree() {} }
    public sealed class Decoy {
        private readonly Node3D _node = new();
        public void Spawn() {
            var QueueFree = 1;                 // 局部变量名，非调用
            var AddChild = _node;              // 局部变量名，非调用
            _node.AddChild(new object());      // 配对的 release 在别处，这里仍泄漏？
            _node.QueueFree();                 // 实际有 release → 不报
        }
    }
}";

    // ── 匿名方法 / lambda 内调用 API ──
    private const string LambdaSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public void AddChild(object c) {} public void QueueFree() {} }
    public sealed class WithLambda {
        private readonly Node3D _node = new();
        public void Spawn() {
            Action a = () => { _node.AddChild(new object()); }; // lambda 内泄漏（分析器不展开 lambda 体？）
            a();
        }
    }
}";

    // ── 泛型方法 QueueFree<T>：canonical 应为 QueueFree，不应误判 ──
    private const string GenericMethodSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public void AddChild<T>(object c) {} public void QueueFree<T>() {} }
    public sealed class WithGeneric {
        private readonly Node3D _node = new();
        public void Spawn() { _node.AddChild<int>(new object()); /* 缺 _node.QueueFree<int>() */ }
    }
}";

    // ── 嵌套类 / partial 类：两部分各调用 API ──
    private const string PartialSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public void AddChild(object c) {} public void QueueFree() {} }
    public partial class Split {
        private readonly Node3D _node = new();
    }
    public partial class Split {
        public void Acquire() { _node.AddChild(new object()); /* 缺 release */ }
        public void Release() { _node.QueueFree(); }
    }
}";

    // ── 平衡对照：有接收者限定 release，不报 ──
    private const string BalancedSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public void AddChild(object c) {} public void QueueFree() {} }
    public sealed class PhysicsBody { public void ApplyForce(float x, float y) {} }
    public sealed class Balanced {
        private readonly Node3D _node = new();
        private readonly PhysicsBody _body = new();
        public void SpawnAndFree() { _node.AddChild(new object()); _node.QueueFree(); }
        public void SelfFree() { this.AddChild(new object()); this.QueueFree(); }
        public void TreeFree() { GetTree().AddChild(new object()); GetTree().QueueFree(); }
        public void Force() { _body.ApplyForce(1, 2); }  // ApplyForce=Mode.Use 非 acquire，不报
    }
    public sealed class Node3DHelper { public static Node3D GetTree() => new(); }
}";

    [Fact]
    public async Task E2E_R6_ReceiverQualifiedLeak_Reported()
    {
        var diags = await Analyze(ReceiverLeakSource);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn"));
    }

    [Fact]
    public async Task E2E_R6_NoUsingStillReported()
    {
        var diags = await Analyze(NoUsingSource);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn"));
    }

    [Fact]
    public async Task E2E_R6_NameOnlyDecoy_NoFalsePositive()
    {
        var diags = await Analyze(NameOnlyNoCallSource);
        // _node.AddChild 有 _node.QueueFree 配对（同方法内、接收者限定）→ 不报
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn"));
    }

    [Fact]
    public async Task E2E_R6_LambdaLeak_Behavior()
    {
        var diags = await Analyze(LambdaSource);
        // 接收者限定 AddChild 在 lambda 内：方法体内 DescendantNodes 可见 InvocationExpression（lambda 是方法体的后代），
        // 故 AddChild 被命中但无 QueueFree → 应报 EAA0901（验证 lambda 体也被覆盖，而非仅顶层语句）。
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    [Fact]
    public async Task E2E_R6_GenericMethodLeak_Reported()
    {
        var diags = await Analyze(GenericMethodSource);
        // QueueFree<T> 的 method 名应为 QueueFree（去泛型参数）→ 仍应捕获泄漏
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn"));
    }

    [Fact]
    public async Task E2E_R6_PartialClassLeak_Reported()
    {
        var diags = await Analyze(PartialSource);
        // partial 类的 Acquire() 内 AddChild 无 release → 应报
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Acquire"));
    }

    [Fact]
    public async Task E2E_R6_BalancedWithReceiver_NoReport()
    {
        var diags = await Analyze(BalancedSource);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("SpawnAndFree"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("SelfFree"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("TreeFree"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Force"));
    }
}
