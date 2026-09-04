// AdvE2E_P0.cs — hickey-x3 P0 修复的可证伪回归（P0-2 接收者类型判定 / P0-3 acquire-release 配对豁免 A3）。
// 修复前必红：P0-2 前用户自有 Load("slot") 被裸名定罪；P0-3 前 AddChild+QueueFree 官方配对被 EAA0303 误报。
using System;
using System.Collections.Immutable;
using System.Linq;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_P0
{
    private static CSharpCompilation MakeCompilation(string source)
    {
var refs = CompilationRefs.Lean(typeof(Claim).Assembly.Location);
        return CSharpCompilation.Create(
            "AdvIntegrationGameP0",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var comp = MakeCompilation(source);
        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ── P0-2a：用户自有 Load（非 Godot 命名空间）读字典 ⇒ 不再被裸名定罪 ──
    private const string UserOwnLoadSource = @"
namespace UserCode {
    public sealed class SaveSystem {
        public string? Load(string slot) { return slot; }   // 名撞白名单 Load，语义完全无关
        public void Boot() { Load(""slot1""); }
    }
}";

    [Fact]
    public async System.Threading.Tasks.Task P0_2_UserOwnLoad_NotFlagged()
    {
        var diags = await Analyze(UserOwnLoadSource);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
        Assert.DoesNotContain(diags, d => d.Id.StartsWith("EAA", StringComparison.Ordinal));
    }

    // ── P0-2b：Godot 类型（Godot.Shapes 桩）的 Load ⇒ 仍须命中白名单报泄漏 ──
    private const string GodotTypedLoadSource = @"
using Cosmos.EffectAlgebra;
namespace Godot.Shapes {
    public sealed class ResourceLoader { public object Load(string path) => new(); }
}
namespace GameCode {
    public sealed class Leaky {
        private readonly Godot.Shapes.ResourceLoader _rl = new();
        public void Spawn() { _rl.Load(""res://enemy.tscn""); }   // acquire 无 release ⇒ EAA0901
    }
}";

    [Fact]
    public async System.Threading.Tasks.Task P0_2_GodotTypedLoad_StillFlagged()
    {
        var diags = await Analyze(GodotTypedLoadSource);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Spawn"));
    }

    // ── P0-3：官方推荐配对 AddChild→QueueFree ⇒ 不报 EAA0303（量纲提示豁免），且平衡不报 EAA0901 ──
    private const string PairedAcquireReleaseSource = @"
using Cosmos.EffectAlgebra;
namespace Godot.Shapes {
    public sealed class Node3D { public void AddChild(object c) { } public void QueueFree() { } }
}
namespace GameCode2 {
    public sealed class Spawner {
        private readonly Godot.Shapes.Node3D _n = new();
        public void SpawnAndDespawn() { _n.AddChild(new object()); _n.QueueFree(); }
    }
}";

    [Fact]
    public async System.Threading.Tasks.Task P0_3_PairedAcquireRelease_NoKindMixNoLeak()
    {
        var diags = await Analyze(PairedAcquireReleaseSource);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303");   // 配对的 kind 差异来自 Release 端 ⇒ 豁免
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");   // 平衡配对 ⇒ 不报泄漏
    }

    // ── P0-3 反向：非配对的真实混用仍报 A3 ──
    private const string GenuineMixSource = @"
using Cosmos.EffectAlgebra;
namespace Godot.Shapes2 {
    public sealed class Node3D { public void AddChild(object c) { } }
    public sealed class SignalHub { public void Connect(object s, object c) { } public void IsConnected(object s) { } }
}
namespace GameCode3 {
    public sealed class Mixer {
        private readonly Godot.Shapes2.Node3D _n = new();
        private readonly Godot.Shapes2.SignalHub _bus = new();
        public void Mixed() { _bus.Connect(new object(), new object()); _bus.IsConnected(new object()); _n.AddChild(new object()); }
    }
}";

    [Fact]
    public async System.Threading.Tasks.Task P0_3_GenuineCrossKindMix_StillReported()
    {
        var diags = await Analyze(GenuineMixSource);
        // Connect=SignalBus(Write)，IsConnected=SignalBus(Read)：同归一资源跨调用 Read×Write ⇒ kind 多样性 ⇒ EAA0303 保留
        Assert.Contains(diags, d => d.Id == "EAA0303");
    }
}
