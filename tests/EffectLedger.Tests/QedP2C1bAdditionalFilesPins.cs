// QedP2C1bAdditionalFilesPins.cs — P2-C1b 分析器 AdditionalFiles 真接线钉（诚实边界 #12 的 L3 消费本体）：
// effectledger.config.json 经 AnalyzerOptions.AdditionalFiles 进入编译 ⇒ per-compilation 合并白名单
// （GodotApiWhitelist.MergedWith，QED-C1a 视图）；配置错误（解析/schema/Canonical 碰撞）⇒ 新诊断
// EAA0701（诊断 ID 属公共契约面，QED-C1b 登记）：该文件扩展整体弃用 + 基础白名单不受影响，
// 绝不静默忽略（假绿向量，R3-L1-03 教义）。真实构建接线另由 tests/GateFixture/ExtendedWhitelist 门钉
// （ProdAuditBatch4ToolingTests.GateFixture_ExtendedWhitelist_BuildFails_WithEAA0901）。
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace EffectLedger.Tests;

public sealed class QedP2C1bAdditionalFilesPins
{
    // 有效扩展：CustomSpawn（acquire）+ CustomDespawn（release）——均不在基础白名单。
    private const string ValidConfig = """
        {
          "extraMappings": [
            { "api": "CustomSpawn",   "claims": [ { "kind": "occupy", "resource": { "memory": 7 }, "mode": "create",  "scope": { "scene": "Battle" } } ] },
            { "api": "CustomDespawn", "claims": [ { "kind": "occupy", "resource": { "memory": 7 }, "mode": "release", "scope": { "scene": "Battle" } } ] }
          ]
        }
        """;

    // 扩展 api 与基础表 QueueFree 同 Canonical（queuefree）⇒ MergedWith loud 抛（QED-C1a）。
    private const string CollidingWithBaseConfig = """
        {
          "extraMappings": [
            { "api": "QueueFree", "claims": [ { "kind": "occupy", "resource": { "memory": 7 }, "mode": "create", "scope": { "scene": "Battle" } } ] }
          ]
        }
        """;

    // stub Godot 类型（命名空间门按 "Godot" 精确放行，P0-2/A2-09 契约）+ 消费者。
    // BaseLeaky 用基础白名单 AddChild 作对照；ExtLeaky/ExtPaired 只能靠扩展映射解释。
    private const string Source = """
        namespace Godot { public class Node { public Node AddChild(Node n) => n; public void CustomSpawn() { } public void CustomDespawn() { } } }
        public class C
        {
            void ExtLeaky(Godot.Node n) { n.CustomSpawn(); }
            void ExtPaired(Godot.Node n) { n.CustomSpawn(); n.CustomDespawn(); }
            void BaseLeaky(Godot.Node n) { n.AddChild(n); }
        }
        """;

    // ── 钉 1：有效扩展 ⇒ 扩展 API 参与泄漏分析（ExtLeaky 报 EAA0901）+ 基础白名单同时不受影响
    //      （BaseLeaky 照报）+ ExtPaired 不误报 + 有效配置零 EAA0701。修改前：静态字典不识 CustomSpawn ⇒ ExtLeaky 静默。──
    [Fact]
    public async System.Threading.Tasks.Task AdditionalFiles_ExtendedWhitelist_LeakReported_BaseUnaffected()
    {
        var diags = await RunAnalyzer(Source, Config(ValidConfig, "effectledger.config.json"));

        var leaks = diags.Where(d => d.Id == "EAA0901").ToArray();
        Assert.Equal(2, leaks.Length); // ExtLeaky（扩展 API）+ BaseLeaky（基础表）
        Assert.Contains(leaks, d => d.GetMessage().Contains("CustomSpawn"));
        Assert.Contains(leaks, d => d.GetMessage().Contains("AddChild"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0701"); // 有效配置不得误报配置诊断
    }

    // ── 钉 2：无 AdditionalFiles ⇒ 扩展 API 静默（分析器不得偷读磁盘/工作目录的 effectledger.config.json）、
    //      基础白名单照常保护——接线前后基础行为逐字节不变。 ──
    [Fact]
    public async System.Threading.Tasks.Task NoAdditionalFiles_CustomApiSilent_BaseStillProtects()
    {
        var diags = await RunAnalyzer(Source);

        var leaks = diags.Where(d => d.Id == "EAA0901").ToArray();
        var leak = Assert.Single(leaks);
        Assert.Contains("AddChild", leak.GetMessage());
        Assert.DoesNotContain(diags, d => d.Id == "EAA0701");
    }

    // ── 钉 3：格式坏配置 ⇒ EAA0701（loud）+ 该文件扩展整体弃用（ExtLeaky 不报——宁缺勿假）
    //      + 基础白名单不受影响（BaseLeaky 照报）。修改前：无 EAA0701 诊断存在。 ──
    [Fact]
    public async System.Threading.Tasks.Task BadJsonConfig_ReportsEAA0701_ExtensionDropped_BaseStillProtects()
    {
        var diags = await RunAnalyzer(Source, Config("{ nope", "effectledger.config.json"));

        Assert.Contains(diags, d => d.Id == "EAA0701");
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("CustomSpawn"));
        var leak = Assert.Single(diags.Where(d => d.Id == "EAA0901"));
        Assert.Contains("AddChild", leak.GetMessage());
    }

    // ── 钉 4：扩展与基础表 Canonical 同键 ⇒ EAA0701 loud（绝不允许部分生效/静默覆盖），
    //      合并集整体回退基础表（ExtLeaky 不报、BaseLeaky 照报）。 ──
    [Fact]
    public async System.Threading.Tasks.Task ExtraCollidingWithBase_ReportsEAA0701_FallsBackToBase()
    {
        var diags = await RunAnalyzer(Source, Config(CollidingWithBaseConfig, "effectledger.config.json"));

        var configDiag = Assert.Single(diags.Where(d => d.Id == "EAA0701"));
        Assert.Contains("白名单扩展碰撞", configDiag.GetMessage());
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("CustomSpawn"));
        var leak = Assert.Single(diags.Where(d => d.Id == "EAA0901"));
        Assert.Contains("AddChild", leak.GetMessage());
    }

    // ── 钉 5：多个配置文件彼此同键 ⇒ EAA0701 定位在首个配置文件（跨文件碰撞的归属契约），
    //      碰撞消息自含双方 API 名（MergedWith loud 语义透传）。
    //      文件名契约 = 精确 effectledger.config.json；多文件场景 = 不同目录同名（解决方案多消费工程各贡献一份）。 ──
    [Fact]
    public async System.Threading.Tasks.Task CrossFileCollision_ReportsEAA0701_AtFirstConfigFile()
    {
        var dup = """
            {
              "extraMappings": [
                { "api": "My_A", "claims": [ { "kind": "occupy", "resource": { "memory": 7 }, "mode": "use", "scope": { "scene": "Battle" } } ] }
              ]
            }
            """;
        // 路径用 Path.Combine 构造（平台正确：Linux 上 \ 不是分隔符，GetFileName 会失效）
        var path1 = System.IO.Path.Combine("a", "proj1", "effectledger.config.json");
        var path2 = System.IO.Path.Combine("b", "proj2", "effectledger.config.json");
        var diags = await RunAnalyzer(Source, Config(dup, path1), Config(dup, path2));

        var configDiag = Assert.Single(diags.Where(d => d.Id == "EAA0701"));
        Assert.Contains("白名单扩展碰撞", configDiag.GetMessage());
        Assert.Contains("My_A", configDiag.GetMessage());
        Assert.Equal(path1, configDiag.Location.GetLineSpan().Path); // 碰撞归属首个配置文件
    }

    // ── 测试基建（与 ProdAuditBatch4ToolingTests 同型拷贝 + AnalyzerOptions 通道，保持测试文件自包含）──
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source, params AdditionalText[] additionalFiles)
    {
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location),
        };
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(p);
            if (name is "System.Runtime" or "System.Collections.Immutable")
                refs = refs.Append(MetadataReference.CreateFromFile(p)).ToArray();
        }
        var compilation = CSharpCompilation.Create(
            "QedP2C1b",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()),
            new AnalyzerOptions(ImmutableArray.Create(additionalFiles)));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static AdditionalText Config(string json, string path) => new InMemoryAdditionalText(path, json);

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
