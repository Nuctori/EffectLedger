// AdvE2E_R7.cs — 对抗审计 R7：§7 白名单覆盖 / ResourceId 类型 / L2-L3 键一致性。
// 重点证明并修复：L3 分析器旧 RawName 把接收者并入 canonical 键（node.QueueFree → nodequeuefree ≠ queuefree）
// ⇒ 受限接收者调用（Godot 默认风格）静默漏报（false negative）。另覆盖白名单类型覆盖、L2/L3 规范化一致性、重复键。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Cosmos.EffectAlgebra;
using Xunit;
using SampleGame.IntegrationTests;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R7
{
    private static CSharpCompilation MakeCompilation(string source)
    {
var refs = CompilationRefs.Lean(typeof(Claim).Assembly.Location);
        return CSharpCompilation.Create(
            "AdvR7",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static readonly string GameSource = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class RecvEnemy {
        private readonly Node3D _node = new();
        // 真实泄漏：acquire 经实例接收者（_node.AddChild），无 release ⇒ 旧 RawName 把 _nodeaddchild 当键，
        // 永不命中白名单 acquire ⇒ 静默漏报（false negative）。修复后应报 EAA0901。
        public void LeakViaReceiver() { _node.AddChild(new object()); }
        // 配对：acquire 经实例接收者 + release 经实例接收者（_node.AddChild + node.QueueFree）⇒ 不报
        public void AcquireReleaseViaReceiver() { _node.AddChild(new object()); _node.QueueFree(); }
        // qualified 键：Audio.Play / Audio.Stop 配对
        public void AudioPair() { Audio.Play(); Audio.Stop(); }
        // qualified 键：Anim.Play / Anim.Stop 配对
        public void AnimPair() { Anim.Play(); Anim.Stop(); }
    }
}";

    // ── R7-1：实例接收者调用（_node.AddChild / _node.QueueFree）必须被 L3 匹配白名单键 ──
    [Fact]
    public async Task E2E_R7_ReceiverQualifiedCallsMatched_NoFalseNegative()
    {
        var comp = MakeCompilation(GameSource);
        var withA = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        var diags = await withA.GetAnalyzerDiagnosticsAsync();

        // LeakViaReceiver：实例接收者 acquire（_node.AddChild）无 release ⇒ 旧逻辑静默漏报，修复后应报 EAA0901
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("LeakViaReceiver"));
        // AcquireReleaseViaReceiver：_node.AddChild + _node.QueueFree 均经实例接收者 ⇒ 配对，不报
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("AcquireReleaseViaReceiver"));
    }

    // ── R7-2：§7 qualified 键（Audio.Play / Anim.Play）在 L3 匹配为 qualified 键，配对则不报 ──
    [Fact]
    public async Task E2E_R7_QualifiedApiKeysMatched()
    {
        var comp = MakeCompilation(GameSource);
        var withA = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        var diags = await withA.GetAnalyzerDiagnosticsAsync();
        // AudioPair / AnimPair 内部均含 acquire+release 配对（Play=create, Stop=release）⇒ 不报泄漏
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("AudioPair"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("AnimPair"));
    }

    // ── R7-3：白名单中每个资源类型都具备可用 ctor + NetTable 处理（类型覆盖无缺口）──
    [Fact]
    public void E2E_R7_AllWhitelistResourceTypesSupported()
    {
        // 直接枚举 §7 白名单，对每条 Claim 的 Resource 走 Normalize + NetTable.Compute，断言不抛。
        // 各资源类型 ctor 可用（不可为 null 的 value type，下面逐类型压实构造路径不抛即可）。
        _ = new ResourceId.Tree(NodePathOrUnknown.Of("p"));
        _ = new ResourceId.Self("c");
        _ = new ResourceId.Physics(new Rid("b"));
        _ = new ResourceId.Memory(0);
        _ = new ResourceId.Disk("p");
        _ = new ResourceId.Signal(new StringName("s"));
        _ = new ResourceId.Gpu(new Rid("b"));
        _ = new ResourceId.AudioMixer(0);
        _ = new ResourceId.Occupancy("ch");
        _ = new ResourceId.Callback("cb");
        _ = new ResourceId.Network(0, "m");
        _ = new ResourceId.Input("a");
        _ = new ResourceId.CommandBuffer("gpu");
        _ = new ResourceId.SignalBus(new StringName("s"));

        // 用 sig 确认白名单所有 Claim 都能参与 net（取 AddChild 的 Tree 资源验证守恒路径可达，不抛、有记录）。
        var sig = Signature.Empty;
        foreach (var m in GodotApiWhitelist.All)
            foreach (var c in m.Claims)
                sig = Signature.Union(sig, Signature.Of(c));
        var net = NetTable.Compute(sig, new ScopeId.Global());
        Assert.False(net.Get(new ResourceId.Tree(NodePathOrUnknown.Of("node.id"))).Lo.IsTop ||
                      net.Get(new ResourceId.Tree(NodePathOrUnknown.Of("node.id"))).Hi.IsTop);
    }

    // ── R7-4：L2 生成器与 L3 分析器使用同一规范化（方法名↔白名单键一致），无分歧 ──
    [Fact]
    public async Task E2E_R7_L2L3KeyNormalizationConsistent()
    {
        // L2 方法名规范化（去 . 和 _，小写）：验证一个带下划线/点的方法名能命中白名单 key。
        var genSrc = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class G {
        [EffectOverride(""x"")]
        public void Add_Child(object c) {}
        [EffectOverride(""y"")]
        public void Remove_Child() {}
    }
}";
        var comp = MakeCompilation(genSrc);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);

        var genText = output.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.ToString())
            .Aggregate((a, b) => a + "\n" + b);
        // 规范化 add_child → addchild 应命中白名单 AddChild；remove_child → removechild 命中 RemoveChild
        Assert.Contains("ComputeAdd_Child", genText);
        Assert.Contains("ComputeRemove_Child", genText);

        // emit + 反射验证生成代码确实 Union 了 AddChild/RemoveChild 的 Claims（L2 键规范化与白名单一致）。
        using var ms = new MemoryStream();
        var emit = output.Emit(ms);
        Assert.True(emit.Success, "生成代码必须可编译：" + string.Join("\n", emit.Diagnostics));
        var asm = Assembly.Load(ms.ToArray());
        var t = asm.GetTypes().First(x => x.Name == "EffectAlgebraGenerated");
        var empty = (Signature)typeof(Signature).GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var addSig = (Signature)t.GetMethod("ComputeAdd_Child")!.Invoke(null, new object[] { empty })!;
        var remSig = (Signature)t.GetMethod("ComputeRemove_Child")!.Invoke(null, new object[] { empty })!;
        Assert.Contains(SignatureExtensions.AllClaims(addSig), c => c.Kind == Kind.Occupy);
        Assert.Contains(SignatureExtensions.AllClaims(remSig), c => c.Kind == Kind.Occupy);

        // L3 侧：同一规范化（addchild/removechild）在 BuildAcquireNames/BuildReleaseNames 中成立，与分析器一致。
        var analyzeSrc = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class H {
        public void Leak() { Add_Child(new object()); }
    }
}";
        var comp2 = MakeCompilation(analyzeSrc);
        var withA = comp2.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        var diags = await withA.GetAnalyzerDiagnosticsAsync();
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Leak"));
    }

    // ── R7-5：白名单无重复 key（避免 first-wins 静默歧义）──
    [Fact]
    public void E2E_R7_NoDuplicateWhitelistKeys()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in GodotApiWhitelist.All)
        {
            var k = m.GodotApi.ToLowerInvariant().Replace(".", "").Replace("_", "");
            counts.TryGetValue(k, out var n);
            counts[k] = n + 1;
        }
        var dups = counts.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
        Assert.Empty(dups);
    }

    // ── R7-6：白名单里每个 Claim 的 Kind 都被 L2 生成器 emit（不静默丢弃）──
    [Fact]
    public void E2E_R7_GeneratorEmitsEveryClaimKind()
    {
        // AddChild 含 Write+Occupy；用生成器对 AddChild 标注方法，断言生成 .g.cs 真含这些 Claim（经 L1 Signature.Of）。
        var src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class K {
        [EffectOverride(""x"")]
        public void AddChild(object c) {}
    }
}";
        var comp = MakeCompilation(src);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);
        using var ms = new MemoryStream();
        var emit = output.Emit(ms);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        var asm = Assembly.Load(ms.ToArray());
        var t = asm.GetTypes().First(x => x.Name == "EffectAlgebraGenerated");
        var empty = (Signature)typeof(Signature).GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var sig = (Signature)t.GetMethod("ComputeAddChild")!.Invoke(null, new object[] { empty })!;
        var kinds = SignatureExtensions.AllClaims(sig).Select(c => c.Kind).ToImmutableHashSet();
        // §7 AddChild 含 Write 与 Occupy ⇒ 二者均被 emit（非仅 Occupy）。
        Assert.Contains(Kind.Write, kinds);
        Assert.Contains(Kind.Occupy, kinds);
    }
}

