// AdvE2E_R1.cs — 对抗审计 R1：L2 生成器 方法名↔§7 白名单键 匹配（PDR §7 核心）。
// 复用 IntegrationTests 的驱动范式：MakeCompilation + CSharpGeneratorDriver 驱动生成器，emit 到 MemoryStream 后 Assembly.Load 反射运行。
// 全部可证伪：规范化匹配（大小写/下划线/点号）、前缀误匹配、数字误匹配、退化名不崩、跨记号相等、重名不崩。
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace SampleGame.AdvE2E;

public sealed class AdvE2E_R1
{
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        return CSharpCompilation.Create(
            "AdvR1Game",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static (string genText, Assembly asm) RunGenerator(string src)
    {
        var comp = MakeCompilation(src);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);
        var genText = output.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.ToString())
            .Aggregate((a, b) => a + "\n" + b);
        using var ms = new MemoryStream();
        var emit = output.Emit(ms);
        Assert.True(emit.Success, "生成代码必须可编译：" + string.Join("\n", emit.Diagnostics));
        var asm = Assembly.Load(ms.ToArray());
        return (genText, asm);
    }

    private static Signature InvokeCompute(Assembly asm, string methodName, Signature baseSig)
    {
        var t = asm.GetType("EffectAlgebraGenerated")
            ?? asm.GetTypes().First(x => x.Name == "EffectAlgebraGenerated");
        // 容错：重名场景下方法名为 Compute{methodName}_{Type}_{idx}，按前缀匹配。
        var m = t.GetMethod("Compute" + methodName)
            ?? t.GetMethods().FirstOrDefault(x => x.Name.StartsWith("Compute" + methodName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Compute" + methodName + " 未生成");
        return (Signature)m.Invoke(null, new object[] { baseSig })!;
    }

    private static Signature EmptySig()
        => (Signature)typeof(Signature).GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private const string Node = "public sealed class Node3D { public object? child; }";

    /// <summary>R1.1 — snake_case 方法名（Godot 风格）规范化匹配 §7 AddChild：应含 Occupy(Tree)。</summary>
    [Fact]
    public void R1_SnakeCase_MapsToAddChild()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{ {Node}
  public sealed class C {{ private readonly Node3D _n = new();
    [EffectOverride(""x"")] public void add_child(object c) {{ _n.child = c; }} }} }}";
        var (_, asm) = RunGenerator(src);
        var sig = InvokeCompute(asm, "add_child", EmptySig());
        Assert.NotEmpty(SignatureExtensions.AllClaims(sig));
        Assert.Contains(SignatureExtensions.AllClaims(sig), c => c.Kind == Kind.Occupy);
        Assert.Contains(SignatureExtensions.AllClaims(sig), c => c.Resource is ResourceId.Tree);
    }

    /// <summary>R1.2 — 全大写方法名规范化匹配 §7 AddChild：应含 Occupy(Tree)。</summary>
    [Fact]
    public void R1_UpperCase_MapsToAddChild()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{ {Node}
  public sealed class C {{ private readonly Node3D _n = new();
    [EffectOverride(""x"")] public void ADDCHILD(object c) {{ _n.child = c; }} }} }}";
        var (_, asm) = RunGenerator(src);
        var sig = InvokeCompute(asm, "ADDCHILD", EmptySig());
        Assert.Contains(SignatureExtensions.AllClaims(sig), c => c.Kind == Kind.Occupy);
        Assert.Contains(SignatureExtensions.AllClaims(sig), c => c.Resource is ResourceId.Tree);
    }

    /// <summary>R1.3 — 前缀/子串不得误匹配：方法 Add（非 AddChild）规范化 add ≠ addchild，必须返回空 Signature。</summary>
    [Fact]
    public void R1_Prefix_NoFalseMatch()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{ {Node}
  public sealed class C {{ private readonly Node3D _n = new();
    [EffectOverride(""x"")] public void Add(object c) {{ _n.child = c; }} }} }}";
        var (genText, asm) = RunGenerator(src);
        var sig = InvokeCompute(asm, "Add", EmptySig());
        Assert.Empty(SignatureExtensions.AllClaims(sig));          // 不误匹配 AddChild
        Assert.DoesNotContain(genText, "GodotApiWhitelist.All");  // 无匹配 ⇒ 生成体不引用白名单循环
    }

    /// <summary>R1.4 — 数字后缀不得误匹配：AddChild3 ≠ AddChild，必须返回空 Signature。</summary>
    [Fact]
    public void R1_Digits_NoFalseMatch()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{ {Node}
  public sealed class C {{ private readonly Node3D _n = new();
    [EffectOverride(""x"")] public void AddChild3(object c) {{ _n.child = c; }} }} }}";
        var (_, asm) = RunGenerator(src);
        var sig = InvokeCompute(asm, "AddChild3", EmptySig());
        Assert.Empty(SignatureExtensions.AllClaims(sig));
    }

    /// <summary>R1.5 — 退化名（仅下划线）规范化为空串：必须返回空 Signature 且不抛（无崩溃）。</summary>
    [Fact]
    public void R1_DegenerateName_NoThrow_EmptySig()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{ {Node}
  public sealed class C {{ private readonly Node3D _n = new();
    [EffectOverride(""x"")] public void _() {{ _n.child = null; }} }} }}";
        var (_, asm) = RunGenerator(src);
        var sig = InvokeCompute(asm, "_", EmptySig());
        Assert.Empty(SignatureExtensions.AllClaims(sig));
    }

    /// <summary>R1.6 — 跨记号相等：PositionGet（去点小写）应匹配 §7 Position.get 的读自身 transform Claims。</summary>
    [Fact]
    public void R1_CrossNotation_Equality()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{
  public sealed class C {{
    [EffectOverride(""x"")] public void PositionGet() {{ }} }} }}";
        var (_, asm) = RunGenerator(src);
        var sig = InvokeCompute(asm, "PositionGet", EmptySig());
        Assert.NotEmpty(SignatureExtensions.AllClaims(sig));
        Assert.Contains(SignatureExtensions.AllClaims(sig), c => c.Resource is ResourceId.Self);
    }

    /// <summary>R1.7 — Instantiate 与 Instance 必须区分：Instantiate 命中 §7（Rd Mem/Wr Tree/Oc Mem），Instance 无键⇒空。</summary>
    [Fact]
    public void R1_InstantiateVsInstance_Distinct()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{
  public sealed class C {{
    [EffectOverride(""x"")] public void Instantiate() {{ }}
    [EffectOverride(""y"")] public void Instance() {{ }} }} }}";
        var (_, asm) = RunGenerator(src);
        var inst = InvokeCompute(asm, "Instantiate", EmptySig());
        Assert.Contains(SignatureExtensions.AllClaims(inst), c => c.Resource is ResourceId.Tree);
        var instance = InvokeCompute(asm, "Instance", EmptySig());
        Assert.Empty(SignatureExtensions.AllClaims(instance));    // Instance 不是白名单键
    }

    /// <summary>R1.8 — 不同类中同名标注方法：生成不得崩溃，且 Compute{Name} 存在并含正确 Claims（last-wins 或合并均须可得）。</summary>
    [Fact]
    public void R1_DuplicateMethodName_NoCrash()
    {
        var src = $@"
using Cosmos.EffectAlgebra;
namespace SampleGame {{ {Node}
  public sealed class A {{ private readonly Node3D _n = new();
    [EffectOverride(""a"")] public void AddChild(object c) {{ _n.child = c; }} }}
  public sealed class B {{ private readonly Node3D _n = new();
    [EffectOverride(""b"")] public void AddChild(object c) {{ _n.child = c; }} }} }}";
        var (_, asm) = RunGenerator(src);
        // 跨类同名：两个 ComputeAddChild_* 均须存在且含正确 Claims（生成层未整体丢失）。
        var t = asm.GetType("EffectAlgebraGenerated")
            ?? asm.GetTypes().First(x => x.Name == "EffectAlgebraGenerated");
        var computes = t.GetMethods().Where(x => x.Name.StartsWith("ComputeAddChild", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, computes.Count);
        foreach (var c in computes)
        {
            var sig = (Signature)c.Invoke(null, new object[] { EmptySig() })!;
            Assert.Contains(SignatureExtensions.AllClaims(sig), x => x.Kind == Kind.Occupy);
            Assert.Contains(SignatureExtensions.AllClaims(sig), x => x.Resource is ResourceId.Tree);
        }
    }
}
