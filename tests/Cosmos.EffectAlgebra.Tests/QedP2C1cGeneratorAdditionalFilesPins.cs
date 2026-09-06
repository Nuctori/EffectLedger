// QedP2C1cGeneratorAdditionalFilesPins.cs — P2-C1c 生成器消费 AdditionalFiles 钉：
// cosmos.effect.json 经 additionalTextsProvider 进入生成器——扩展-only 方法 emit 字面量 Claims
//（契约面类型全 public，消费方可构造）；基础表命中方法维持运行期枚举 body（不变）；
// 配置解析/碰撞失败 ⇒ EAA0701（generator 侧同契约 ID，扩展整体弃用、基础白名单不受影响、绝不静默）。
// 与 C1b（L3 侧接线）对称，完成「存在但不生效」(#12) 的 L2 半边收口。xUnit。
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedP2C1cGeneratorAdditionalFilesPins
{
    private sealed class InMemoryText : AdditionalText
    {
        private readonly string _text;
        public InMemoryText(string path, string text) { Path = path; _text = text; }
        public override string Path { get; }
        public override SourceText? GetText(System.Threading.CancellationToken cancellationToken = default) => SourceText.From(_text);
    }

    private const string Consumer = """
        namespace Consumer;
        public class Game
        {
            [Cosmos.EffectAlgebra.EffectOverride("qed-c1c")]
            public void MyWidget_Show() { }
        }
        """;

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated) Run(string? configJson)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        if (configJson is not null)
            driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(new InMemoryText("cosmos.effect.json", configJson)));

        var parse = CSharpSyntaxTree.ParseText(Consumer);
        var refs = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
        var comp = CSharpCompilation.Create("qed-c1c", new[] { parse }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        driver.RunGeneratorsAndUpdateCompilation(comp, out var outComp, out var genDiags);

        var generated = string.Join("\n", outComp!.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.ToString()));
        return (genDiags, generated);
    }

    // ── 钉 1（扩展-only 方法）：cosmos.effect.json 的扩展 API（不在基础表）⇒ 生成字面量 Claims——
    //    消费方运行期枚举的 All 不含扩展项，故必须字面量内嵌（契约面类型全 public 可构造）。 ──
    [Fact]
    public void ExtendedApi_EmitsLiteralClaims()
    {
        const string config = """
            { "extraMappings": [ { "api": "MyWidget.Show",
                "claims": [ { "kind": "occupy", "resource": { "memory": 42 }, "mode": "use", "scope": { "scene": "S" }, "size": [1, 1] } ] } ] }
            """;
        var (_, generated) = Run(config);
        Assert.Contains("ComputeMyWidget_Show", generated);
        Assert.Contains("ResourceId.Memory(42UL)", generated); // 字面量（非运行期枚举）
        Assert.DoesNotContain("GodotApiWhitelist.All", generated); // 扩展-only：不生成运行期枚举 body
    }

    // ── 钉 2（配置错误 loud）：坏配置 ⇒ EAA0701 + 基础路径不受影响（基础方法仍生成运行期枚举 body）。 ──
    [Fact]
    public void MalformedConfig_ReportsEaa0701_BaseUnaffected()
    {
        var (diags, generated) = Run("{ not json");
        Assert.Contains(diags, d => d.Id == "EAA0701");
        Assert.Contains("GodotApiWhitelist.All", generated); // 基础枚举 body 仍在
    }

    // ── 钉 3（无配置 = 原行为）：无 cosmos.effect.json ⇒ 无 EAA0701，基础路径正常。 ──
    [Fact]
    public void NoConfig_NoDiagnostic_NormalEmit()
    {
        var (diags, generated) = Run(null);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0701");
        Assert.Contains("ComputeMyWidget_Show", generated);
        Assert.Contains("GodotApiWhitelist.All", generated); // 基础表命中（MyWidget_Show 名义匹配为基础路径演示）：运行期枚举
    }
}
