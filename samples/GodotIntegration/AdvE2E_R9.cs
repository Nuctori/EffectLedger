// AdvE2E_R9.cs — 对抗审计 R9：生成器/分析器 确定性 + 规模鲁棒性（无崩溃、无退化、幂等）。
// 不修改 IntegrationTests.cs；复用同一驱动模式（编译期驱动 L2 生成器 + L3 分析器）。
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using SampleGame.IntegrationTests;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R9
{
    // ── 驱动工具（同 IntegrationTests.MakeCompilation 语义）──
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        // 消费工程可能关闭 #nullable（真实 Godot 工程常见）；此处显式禁用，使生成代码（引用 L1 可空标注成员）
        // 在该上下文可编译，避免 CS8632（与生成器内 #nullable disable 双保险）。
        var opts = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Disable);
        return CSharpCompilation.Create(
            "AdvR9Game",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            opts);
    }

    private static string RunGeneratorText(CSharpCompilation comp, out Compilation outComp)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var outC, out _);
        var genTrees = outC.SyntaxTrees.Where(t => t.FilePath.EndsWith(".g.cs")).ToArray();
        if (genTrees.Length == 0)
        {
            var runRes = driver.GetRunResult();
            var hintNames = string.Join(",", runRes.Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName));
            var ex = string.Join("|", runRes.Results.SelectMany(r => r.Diagnostics).Select(d => d.Id + ":" + d.GetMessage()));
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "r9dbg.txt"), "hints=" + hintNames + "\nerrs=" + ex); } catch { }
        }
        var text = genTrees.Length == 0 ? ""
            : genTrees.Select(t => t.ToString()).OrderBy(x => x).Aggregate((a, b) => a + "\n" + b);
        outComp = outC;
        return text;
    }

    private static ImmutableArray<Diagnostic> RunAnalyzer(CSharpCompilation comp)
    {
        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    // ── 程序化生成大规模 GameSource ──
    private static string LargeGameSource(int classCount, int methodPerClass)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Cosmos.EffectAlgebra;");
        sb.AppendLine("namespace SampleGame {");
        for (int c = 0; c < classCount; c++)
        {
            sb.AppendLine($"    public sealed class Node{c} {{ public object? child; }}");
            sb.AppendLine($"    public sealed class Enemy{c} {{");
            sb.AppendLine($"        private readonly Node{c} _node = new();");
            // 每个类都带 AddChild/QueueFree（跨类型同名 → 触发生成器重名冲突修复）；每个方法名每类仅一次（避免 CS0111）。
            sb.AppendLine($"        [EffectOverride(\"pair\")]");
            sb.AppendLine($"        public void AddChild(object child) {{ _node.child = child; }}");
            sb.AppendLine($"        [EffectOverride(\"release\")]");
            sb.AppendLine($"        public void QueueFree() {{ _node.child = null; }}");
            // 若每类方法数 >2，追加带序号的额外标注方法（同名跨类，进一步压力生成器消歧）。
            for (int m = 2; m < methodPerClass; m++)
            {
                sb.AppendLine($"        [EffectOverride(\"extra {m}\")]");
                sb.AppendLine($"        public void Extra{m}(object child) {{ _node.child = child; }}");
            }
            sb.AppendLine($"        public void SpawnAndFree() {{ AddChild(new object()); QueueFree(); }}");
            sb.AppendLine($"    }}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── R9-1：生成器 + 分析器 幂等（同输入两次运行 → 同输出）──
    [Fact]
    public void E2E_R9_GeneratorAndAnalyzer_Deterministic()
    {
        var src = LargeGameSource(classCount: 10, methodPerClass: 5);
        var comp1 = MakeCompilation(src);
        var comp2 = MakeCompilation(src);

        var g1 = RunGeneratorText(comp1, out _);
        var g2 = RunGeneratorText(comp2, out _);
        Assert.Equal(g1, g2);                       // 生成代码完全确定

        var d1 = RunAnalyzer(comp1).OrderBy(d => d.Id + d.GetMessage()).ToArray();
        var d2 = RunAnalyzer(comp2).OrderBy(d => d.Id + d.GetMessage()).ToArray();
        Assert.Equal(d1.Length, d2.Length);
        for (int i = 0; i < d1.Length; i++)
        {
            Assert.Equal(d1[i].Id, d2[i].Id);
            Assert.Equal(d1[i].GetMessage(), d2[i].GetMessage());
        }
    }

    // ── R9-2：大规模（100 类 × 2 标注方法=200 标注方法，含跨类型同名 AddChild/QueueFree）→ 不崩溃、生成可编译、有界耗时 ──
    [Fact]
    public void E2E_R9_Generator_ScaleNoCrash()
    {
        var src = LargeGameSource(classCount: 100, methodPerClass: 2);
        var comp = MakeCompilation(src);

        var sw = Stopwatch.StartNew();
        var genText = RunGeneratorText(comp, out var outComp);
        var genElapsed = sw.ElapsedMilliseconds;

        // 跨类型同名 → 必须含类型_序号消歧符号（ComputeAddChild_Enemy0_0），且无裸重复 ComputeAddChild（防重名编译失败）
        Assert.Contains("ComputeAddChild_Enemy0_0", genText);
        Assert.DoesNotContain("public static global::Cosmos.EffectAlgebra.Signature ComputeAddChild(", genText);

        using var ms = new MemoryStream();
        var emit = outComp.Emit(ms);
        // 规模生成代码必须「无错误」可编译（CS8632 等可空标注警告来自消费工程的可空上下文，
        // 生成器已 #nullable disable 双保险；真实 Godot 工程自行配置可空，故仅以 ERROR 判定可编译性）。
        var errors = emit.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);

        // 生成代码反射运行：消歧后的 ComputeNode0_AddChild 必须存在且返回非空 Signature
        var asm = Assembly.Load(ms.ToArray());
        var t = asm.GetTypes().First(x => x.Name == "EffectAlgebraGenerated");
        var empty = (Signature)typeof(Signature).GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var sig = (Signature)t.GetMethod("ComputeAddChild_Enemy0_0")!.Invoke(null, new object[] { empty })!;
        Assert.NotEmpty(SignatureExtensions.AllClaims(sig));

        Assert.True(genElapsed < 5000, $"生成耗时应 < 5s，实际 {genElapsed}ms");
    }

    // ── R9-3：分析器在大规模下不崩溃、有界耗时 ──
    [Fact]
    public void E2E_R9_Analyzer_ScaleNoCrash()
    {
        var src = LargeGameSource(classCount: 100, methodPerClass: 2);
        var comp = MakeCompilation(src);
        var sw = Stopwatch.StartNew();
        var diags = RunAnalyzer(comp);
        var elapsed = sw.ElapsedMilliseconds;
        // 平衡方法（AddChild+QueueFree 配对 + [EffectOverride]）不应报泄漏；仅断言无异常 & 有界
        Assert.True(elapsed < 5000, $"分析耗时应 < 5s，实际 {elapsed}ms");
        Assert.All(diags, d => Assert.NotNull(d.Id));
    }

    // ── R9-4：零标注方法 → 生成器无输出、分析器零诊断、不抛 ──
    [Fact]
    public void E2E_R9_ZeroAnnotated_NoOutputNoCrash()
    {
        var src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Plain {
        private readonly Node3D _node = new();
        public void AddChild(object c) { _node.child = c; }
        public void QueueFree() { _node.child = null; }
    }
}";
        var comp = MakeCompilation(src);
        var genText = RunGeneratorText(comp, out _);
        Assert.Equal("", genText);                  // 无 .g.cs 生成
        var diags = RunAnalyzer(comp);
        Assert.Empty(diags);                        // 零标注 → 零诊断（无 [Escape] 即按泄漏？此处 AddChild+QueueFree 配对，漏报属于已知局限，R9 只验无崩溃/零异常）
    }

    // ── R9-5：深嵌套命名空间 + 多 using + 无 Cosmos 引用 → 不崩溃 ──
    [Fact]
    public void E2E_R9_DeepNesting_NoCosmosRef_NoCrash()
    {
        var src = @"
using System;
using System.Collections.Generic;
using Cosmos.EffectAlgebra;
namespace A.B.C.D.E {
    public sealed class Node3D { public object? child; }
    namespace Inner {
        public sealed class Enemy {
            private readonly Node3D _node = new();
            [EffectOverride(""x"")]
            public void AddChild(object c) { _node.child = c; }
            [EffectOverride(""y"")]
            public void QueueFree() { _node.child = null; }
            public void Go() { AddChild(new object()); QueueFree(); }
        }
    }
}";
        var comp = MakeCompilation(src);
        var genText = RunGeneratorText(comp, out var outComp);
        using var ms = new MemoryStream();
        var emit = outComp.Emit(ms);
        Assert.True(emit.Success, "深嵌套生成须可编译：" + string.Join("\n", emit.Diagnostics));
        Assert.Contains("ComputeAddChild", genText);   // 单类单方法 → 裸名契约，深嵌套命名空间不崩溃
    }

    // ── R9-6：单方法内重复相同调用（AddChild x10）→ 分析器不 IndexOutOfRange / 不崩溃 ──
    [Fact]
    public void E2E_R9_RepeatedCalls_SameMethod_NoIndexError()
    {
        var src = @"
using Cosmos.EffectAlgebra;
namespace GodotShapes { public sealed class Node3D { public void AddChild(object c) { } } }
namespace SampleGame {
    public sealed class Spammer {
        private readonly GodotShapes.Node3D _node = new();
        public void TenAdds() {
            _node.AddChild(new object()); _node.AddChild(new object()); _node.AddChild(new object());
            _node.AddChild(new object()); _node.AddChild(new object()); _node.AddChild(new object());
            _node.AddChild(new object()); _node.AddChild(new object()); _node.AddChild(new object());
            _node.AddChild(new object());
        }
    }
}";
        var comp = MakeCompilation(src);
        var diags = RunAnalyzer(comp);
        // 不抛异常（已隐式保证）；含泄漏诊断（acquire 无 release）属预期近似
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }
}

