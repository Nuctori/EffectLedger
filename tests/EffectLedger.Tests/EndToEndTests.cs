using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

/// <summary>
/// 迭代21 — §14 L2/L3 端到端集成测试：证明「§7 白名单 → L2 生成器 → L1 数学 → L3 分析器」全链路闭环。
/// 真实触发 generator + analyzer + L1 数学（反射运行生成代码），断言可证伪：
///   - 平衡（acquire+release 配对）⇒ analyzer 不误报 + 生成的 L1 Signature 经 NetTable 确守恒；
///   - 不平衡（仅 acquire）⇒ analyzer 报 EAA0901；
///   - [EffectOverride] 豁免 ⇒ analyzer 不报；
///   - 生成代码真委托 L1（含 GodotApiWhitelist.All + Signature.Union，非桩）。
/// stub 类型（AddChild/RemoveChild/...）仅为编译占位，非真 Godot（注释明言，LANDING_PLAN §3.11）。
///
/// 诚实设计（防假绿）：L2 生成器按「方法名规范化」匹配 §7 白名单键（见 EffectAlgebraGenerator.GenerateMethodSignature），
/// 故单方法签名只含与其方法名同键的 API Claims。§7 中真正合并成平衡占用对的配对是
///   AddChild → Oc(Tree("node.id"), Create, Exact(1))   与
///   RemoveChild → Oc(Tree("node.id"), Release, Exact(1))   （二者归一后同资源 Tree("node.id")）。
/// 因此「生成签名确守恒」用 Compute_AddChild ∪ Compute_RemoveChild 在 L1 验证，而非虚构 Compute_SpawnAndDespawn 守恒
/// （后者方法名 spawnanddespawn 不匹配任何 §7 键，生成空签名，断言其守恒即假绿）。
/// </summary>
public sealed class EndToEndTests
{
    // ── 辅助：最小可编译 C# 编译（引用 object + L1 + 运行时基础）──
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = DefaultRefs().ToArray();
        return CSharpCompilation.Create(
            "E2ETestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> DefaultRefs()
    {
        yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location);
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name == "System.Runtime" || name == "System.Collections.Immutable")
                yield return MetadataReference.CreateFromFile(p);
        }
    }

    // ── §3.3.1 DO-9 近似：运行 L3 Analyzer，返回全部诊断 ──
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var compilation = MakeCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ── §L2：运行生成器，返回更新后全部语法树文本 ──
    private static string RunGenerator(string source)
    {
        var compilation = MakeCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new EffectLedger.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return output.SyntaxTrees.Select(t => t.ToString()).Aggregate((a, b) => a + "\n" + b);
    }

    // ── §L2：运行生成器并 emit，返回反射用 Assembly（生成代码真可运行消费）──
    private static Assembly EmitWithGenerator(string source)
    {
        var compilation = MakeCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new EffectLedger.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var diags = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(diags); // 生成代码 + 样本必须零编译错误

        using var ms = new MemoryStream();
        var emit = output.Emit(ms);
        Assert.True(emit.Success, "emit 必须成功：" + string.Join("\n", emit.Diagnostics));
        return Assembly.Load(ms.ToArray());
    }

    // 样本源（§7/§8.1/§3.3.1）：
    //   AddChild   → 标注 [EffectOverride] 使生成器 emit ComputeAddChild（§7 AddChild = occupy create Tree("node.id")）
    //   RemoveChild→ 标注 [EffectOverride] 使生成器 emit ComputeRemoveChild（§7 RemoveChild = occupy release Tree("node.id")）
    //   SpawnAndDespawn → 方法体内 AddChild + RemoveChild（平衡），无 override ⇒ analyzer 看 acquire+release 不误报
    // stub 类型 AddChild(object)/RemoveChild() 仅为编译占位（非真 Godot）。
    private const string BalancedSource = @"
using EffectLedger;
public class Enemy
{
    public object child;
    [EffectOverride(""spawn/despawn 配对"")]
    public void AddChild(object x) { }
    [EffectOverride(""spawn/despawn 配对"")]
    public void RemoveChild() { }
    public void SpawnAndDespawn()
    {
        AddChild(child);
        RemoveChild();
    }
}";

    /// <summary>§14 L2/L3 + §3.3.1 — 平衡方法（acquire+release 同资源）⇒ analyzer 不报 EAA0901。</summary>
    [Fact]
    public async Task Balanced_Body_NoAnalyzerWarning()
    {
        var diags = await RunAnalyzer(BalancedSource);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }

    /// <summary>§14 L2 + §3.3.1 — 生成的 L1 Signature 经 Union 后 NetTable 确守恒（create+release 抵消）。
    /// 反射 ComputeAddChild / ComputeRemoveChild（L2 真委托 §7 白名单），Union 后在 L1 断言 IsConserved(Tree("node.id"))=true。
    /// 真证伪：若 §7 配对被破坏或 L1 net 有符号抵消失效，此断言必红（无假绿）。</summary>
    [Fact]
    public void GeneratedSignature_Union_IsConservedViaL1()
    {
        var asm = EmitWithGenerator(BalancedSource);
        var genType = asm.GetType("EffectAlgebraGenerated")
            ?? asm.GetTypes().FirstOrDefault(t => t.Name == "EffectAlgebraGenerated");
        Assert.NotNull(genType);

        var empty = (global::EffectLedger.Signature)
            typeof(global::EffectLedger.Signature)
                .GetField("Empty", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;

        var addChild = (global::EffectLedger.Signature)
            genType.GetMethod("ComputeAddChild")!.Invoke(null, new object[] { empty })!;
        var removeChild = (global::EffectLedger.Signature)
            genType.GetMethod("ComputeRemoveChild")!.Invoke(null, new object[] { empty })!;

        // 二者确实各自含 occupy 桶 Claim（§7 AddChild/RemoveChild 各自有 Oc(Tree(...)))
        var addAll = global::EffectLedger.SignatureExtensions.AllClaims(addChild).ToArray();
        var remAll = global::EffectLedger.SignatureExtensions.AllClaims(removeChild).ToArray();
        Assert.Contains(addAll, c => c.Kind == global::EffectLedger.Kind.Occupy);
        Assert.Contains(remAll, c => c.Kind == global::EffectLedger.Kind.Occupy);

        // L1 数学：Union 后在 Global scope 上 NetTable 守恒（create[1,1] + release[-1,-1] = [-1,1] 含 0）
        var union = global::EffectLedger.Signature.Union(addChild, removeChild);
        var treeNodeId = new global::EffectLedger.ResourceId.Tree(
            global::EffectLedger.NodePathOrUnknown.Of("node.id"));
        var net = global::EffectLedger.NetTable.Compute(union, new global::EffectLedger.ScopeId.Global());
        Assert.True(net.IsConserved(treeNodeId), "AddChild(create) + RemoveChild(release) 在 Tree(\"node.id\") 应守恒");
    }

    /// <summary>§3.3.1 DO-9 — 不平衡方法（仅 acquire，无 release、无 [EffectOverride]）⇒ analyzer 报 ≥1 EAA0901。</summary>
    [Fact]
    public async Task Unbalanced_Body_ReportsEAA0901()
    {
        const string source = @"
using EffectLedger;
namespace Godot.Shapes { public sealed class Node3D { public void AddChild(object x) { } } }
public class Leaker
{
    private readonly Godot.Shapes.Node3D _n = new();
    public async Task Leak()
    {
        _n.AddChild(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    /// <summary>§8.3.1(3) — 仅 acquire 但标 [EffectOverride]（逃逸通道）⇒ EAA0901（泄漏根因）仍报；
    /// 逃逸标注仅豁免 A3/A4 意图提示，不掩盖真实泄漏。</summary>
    [Fact]
    public async Task Override_DoesNotExemptLeak_StillReportsEAA0901()
    {
        const string source = @"
using EffectLedger;
namespace Godot.Shapes { public sealed class Node3D { public void AddChild(object x) { } } }
public class Intended
{
    private readonly Godot.Shapes.Node3D _n = new();
    [EffectOverride(""帧内临时占用，已知泄漏"")]
    public async Task Temp()
    {
        _n.AddChild(new object());
    }
}";
        var diags = await RunAnalyzer(source);
        // §8.3.1(3) DO-9 仍报警：EAA0901（泄漏根因）一律不豁免 [EffectOverride]；
        // 逃逸标注仅豁免 A3/A4 意图提示，不掩盖真实泄漏。
        Assert.Contains(diags, d => d.Id == "EAA0901");
    }

    /// <summary>§14 L2 — 生成代码真委托 L1（非桩）：含 GodotApiWhitelist.All + Signature.Union，
    /// 且反射 ComputeAddChild 返回非空 Signature（含 occupy Claim）。</summary>
    [Fact]
    public void Generator_EmittedCode_DelegatesToL1()
    {
        var generated = RunGenerator(BalancedSource);
        Assert.Contains("GodotApiWhitelist.All", generated);   // 真引用 §7 白名单（非桩）
        Assert.Contains("Signature.Union", generated);           // 真委托 L1 代数（非桩）
        Assert.Contains("ComputeAddChild", generated);           // 每方法组合入口

        var asm = EmitWithGenerator(BalancedSource);
        // A2-14：生成类已移入 namespace EffectLedger.Generated——反射按 Name 查找（与 namespace 无关）
        var genType = asm.GetTypes().First(t => t.Name == "EffectAlgebraGenerated")!;
        var empty = (global::EffectLedger.Signature)
            typeof(global::EffectLedger.Signature)
                .GetField("Empty", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;
        var result = (global::EffectLedger.Signature)
            genType.GetMethod("ComputeAddChild")!.Invoke(null, new object[] { empty })!;
        var all = global::EffectLedger.SignatureExtensions.AllClaims(result).ToArray();
        Assert.NotEmpty(all);                                  // 生成签名非空（含 §7 AddChild 的 Claims）
        Assert.Contains(all, c => c.Kind == global::EffectLedger.Kind.Occupy);
    }
}
