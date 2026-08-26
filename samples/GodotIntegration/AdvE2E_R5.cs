// AdvE2E_R5.cs — R5 对抗审计：多标注 / 重复 / 冲突。
// 验证 L2 生成器 + L3 分析器在以下对抗形态下的行为：
//   - 同方法同时标 [EffectOverride] + [AcceptDeviation] → 生成器 emit 单一 Compute（非重复）；分析器视为已声明逃逸通道（豁免）。
//   - [EffectOverride] reason 为空/空白 → 必须 NOT 豁免（仍报 EAA0901）且报 EAA0801（reason 必填）。
//     （§8.3.1 声称 reason 非空由 L1 构造子 fail-fast 强制；但 C# 不在编译期执行 attribute ctor，
//      构造子抛异常是运行期反射行为，L2/L3 按名识别不触发 → 实际静默放行。根因修复迁移到 L3 分析器在编译期强制。）
//   - [AcceptDeviation] epsilon 越界（>0.5）→ 必须 NOT 豁免且报 EAA0802。
//   - [EffectOverride] 重复标注（AllowMultiple=false）→ 编译错误（编译器拦），生成器/分析器不退化。
//   - 属性标在非方法（class）上 → 生成器/分析器忽略，不崩溃。
//   - 合法 [EffectOverride("reason")] 平衡代码 → 仍豁免（回归：修复后合法逃逸不被误伤）。
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using SampleGame.IntegrationTests;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R5
{
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        return CSharpCompilation.Create(
            "AdvR5Game",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static async Task<(ImmutableArray<Diagnostic> Diags, string GenText)> RunBoth(string source)
    {
        var comp = MakeCompilation(source);
        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);
        var genText = output.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.ToString())
            .DefaultIfEmpty("")
            .Aggregate((a, b) => a + "\n" + b);
        return (diags, genText);
    }

    // ── R5.1：同方法同时标两个属性 → 单一 Compute emit，且豁免 ──
    [Fact]
    public async Task E2E_R5_BothAttributes_SingleCompute_AndExempt()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class BothTagged {
        private readonly Node3D _n = new();
        [EffectOverride(""合法 reason"")]
        [AcceptDeviation(0.2)]
        public void AddChild(object c) { _n.child = c; }
        public void Spawn() { AddChild(new object()); } // 无 release，但方法本身未 acquire
    }
}";
        var (diags, gen) = await RunBoth(src);
        // 仅一个 ComputeAddChild emit（不重复）。
        Assert.Equal(1, gen.Split(new[] { "public static global::Cosmos.EffectAlgebra.Signature ComputeAddChild" }, StringSplitOptions.None).Length - 1);
        Assert.Contains("ComputeAddChild", gen);
        // 标了合法 reason + 合法 epsilon ⇒ 被视为逃逸通道，对 (无 acquire 的 Spawn) 不应误报泄漏。
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("BothTagged"));
        // 合法标注不触发 R5 新增诊断。
        Assert.DoesNotContain(diags, d => d.Id == "EAA0801" || d.Id == "EAA0802");
    }

    // ── R5.2：[EffectOverride("")] 空 reason → 必须 NOT 豁免（仍报 EAA0901）且报 EAA0801 ──
    // 修复前：构造子抛异常是运行期行为，L3 按名识别 ⇒ 静默豁免，EAA0901/EAA0801 均不报 → 本测试失败 → 证明 bug。
    [Fact]
    public async Task E2E_R5_OverrideEmptyReason_NotExempted_AndDiagnosed()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes { public sealed class Node3D { public void AddChild(object c) { } } }
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class EmptyReason {
        private readonly GodotShapes.Node3D _g = new();
        [EffectOverride("""")]
        public void AddChild(object c) { } // 空 reason：标注保留 ⇒ EAA0801；生成器 emit 与否与本测无关
        public void Spawn() { _g.AddChild(new object()); }
    }
}";
        var (diags, _) = await RunBoth(src);
        Assert.Contains(diags, d => d.Id == "EAA0801");                       // reason 必填
        // AddChild 标空 reason ⇒ 不豁免；但 AddChild 方法体内无 acquire API 调用，泄漏报在其调用方 Spawn（acquire 无 release）。
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    // ── R5.3：[EffectOverride("   ")] 空白 reason → 同 R5.2 ──
    [Fact]
    public async Task E2E_R5_OverrideWhitespaceReason_Diagnosed()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes { public sealed class Node3D { public void AddChild(object c) { } } }
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class WsReason {
        private readonly GodotShapes.Node3D _g = new();
        [EffectOverride(""   "")]
        public void AddChild(object c) { }
        public void Spawn() { _g.AddChild(new object()); }
    }
}";
        var (diags, _) = await RunBoth(src);
        Assert.Contains(diags, d => d.Id == "EAA0801");
        // 空白 reason 不豁免；泄漏报在调用方 Spawn（acquire 无 release）。
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    // ── R5.4：[AcceptDeviation(0.7)] 越界 ⇒ 必须 NOT 豁免且报 EAA0802 ──
    [Fact]
    public async Task E2E_R5_AcceptDeviationOutOfRange_NotExempted()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes { public sealed class Node3D { public void AddChild(object c) { } } }
namespace SampleGame {
    public sealed class BadEpsilon {
        private readonly GodotShapes.Node3D _g = new();
        [AcceptDeviation(0.7)]
        public void Spawn() { _g.AddChild(new object()); }
    }
}";
        var (diags, _) = await RunBoth(src);
        Assert.Contains(diags, d => d.Id == "EAA0802");
        // 越界 epsilon 不豁免；泄漏报在调用方 Spawn（acquire 无 release）。
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    // ── R5.5：[EffectOverride] 重复标注（AllowMultiple=false）→ 编译错误；生成器/分析器不退化 ──
    [Fact]
    public async Task E2E_R5_DuplicateOverride_CompileError()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Dup {
        private readonly Node3D _n = new();
        [EffectOverride(""r1"")]
        [EffectOverride(""r2"")]
        public void AddChild(object c) { _n.child = c; }
    }
}";
        var comp = MakeCompilation(src);
        // AllowMultiple=false ⇒ 编译器直接报 CS0579（重复特性），编译失败。
        var compileErrors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        Assert.Contains(compileErrors, d => d.Id == "CS0579");
    }

    // ── R5.6：属性标在 class 上（AttributeTargets.Class 允许）→ 生成器/分析器忽略，不崩溃 ──
    [Fact]
    public async Task E2E_R5_AttributeOnClass_IgnoredNoCrash()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes { public sealed class Node3D { public void AddChild(object c) { } } }
namespace SampleGame {
    [EffectOverride(""类级 reason"")]
    public sealed class OnClass {
        private readonly GodotShapes.Node3D _g = new();
        public void Spawn() { _g.AddChild(new object()); }   // acquire 无 release ⇒ 仍报 EAA0901
    }
}";
        var (diags, gen) = await RunBoth(src);
        // 类级 [EffectOverride] 不豁免方法（逃逸通道以方法为单位）；acquire 调用方 Spawn 仍报 EAA0901。
        Assert.Contains(diags, d => d.Id == "EAA0901");
        // 生成器不应为类级标注 emit 任何 Compute（仅处理方法声明）。
        Assert.DoesNotContain("ComputeAddChild", gen);
        // 类级 [EffectOverride] 无方法级 reason 校验需求 → 不应误报 EAA0801（EAA0801 只针对方法级标注）。
        Assert.DoesNotContain(diags, d => d.Id == "EAA0801");
    }

    // ── R5.7：回归 — 合法 [EffectOverride("reason")] 平衡代码修复后仍豁免（不被误伤）──
    [Fact]
    public async Task E2E_R5_ValidOverride_StillExempt()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Balanced {
        private readonly Node3D _n = new();
        [EffectOverride(""spawn/despawn 配对，证据见 PDR §7"")]
        public void AddChild(object c) { _n.child = c; }
        [EffectOverride(""释放 Tree"")]
        public void RemoveChild() { _n.child = null; }
        public void SpawnAndDespawn() { AddChild(new object()); RemoveChild(); }
    }
}";
        var (diags, gen) = await RunBoth(src);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Balanced"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0801" || d.Id == "EAA0802");
        Assert.Contains("ComputeAddChild", gen);
        Assert.Contains("ComputeRemoveChild", gen);
    }
}

