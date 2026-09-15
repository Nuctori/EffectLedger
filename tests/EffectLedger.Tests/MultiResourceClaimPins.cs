// MultiResourceClaimPins.cs — ROI 审计（2026-09-14）修复钉：L3 分析器同站点 Claim 分桶按
//（归一资源, kind, mode）三元组去重。修复前按 (kind,mode) 二元组去重后以 First(...) 取资源——
// 同一 API 对多资源声明同 (kind,mode) 时后续资源被静默丢弃，跨 API 同资源冲突（EAA0304）
// 在支持范围内漏报（非已声明的跨方法/控制流近似边界）。
// 场景经 QED-C1b 扩展白名单（AdditionalFiles）注入多资源映射，与消费方真实接线同形。
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace EffectLedger.Tests;

public class MultiResourceClaimPins
{
    // Multi 对 mem:111 与 mem:222 都声明 occupy+move；Other 仅对 mem:222 声明 occupy+move。
    private const string MultiFirstConfig = """
        {
          "extraMappings": [
            { "api": "Multi",  "claims": [
              { "kind": "occupy", "resource": { "memory": 111 }, "mode": "move", "scope": { "scene": "Battle" } },
              { "kind": "occupy", "resource": { "memory": 222 }, "mode": "move", "scope": { "scene": "Battle" } } ] },
            { "api": "Other",  "claims": [
              { "kind": "occupy", "resource": { "memory": 222 }, "mode": "move", "scope": { "scene": "Battle" } } ] }
          ]
        }
        """;

    // 同一场景、Multi 的两条 Claim 顺序对调（222 在前）——资源选取不得依赖 Claim 顺序。
    private const string MultiLastConfig = """
        {
          "extraMappings": [
            { "api": "Multi",  "claims": [
              { "kind": "occupy", "resource": { "memory": 222 }, "mode": "move", "scope": { "scene": "Battle" } },
              { "kind": "occupy", "resource": { "memory": 111 }, "mode": "move", "scope": { "scene": "Battle" } } ] },
            { "api": "Other",  "claims": [
              { "kind": "occupy", "resource": { "memory": 222 }, "mode": "move", "scope": { "scene": "Battle" } } ] }
          ]
        }
        """;

    private const string Source = """
        namespace Godot { public class Node { public void Multi() { } public void Other() { } } }
        public class C
        {
            void M(Godot.Node n) { n.Multi(); n.Other(); }
        }
        """;

    // ── 钉 1：多资源映射 + 第二资源上的跨 API 冲突 ⇒ EAA0304 报告（修复前：mem:222 被 First() 丢弃 ⇒ 零诊断）。 ──
    [Fact]
    public async System.Threading.Tasks.Task ConflictOnLaterResource_Reported()
    {
        var diags = await RunAnalyzer(Source, Config(MultiFirstConfig));
        var eaa0304 = diags.Where(d => d.Id == "EAA0304").ToArray();
        Assert.Single(eaa0304);
        Assert.Contains("Move+Move", eaa0304[0].GetMessage());
        Assert.DoesNotContain(diags, d => d.Id == "EAA0701"); // 配置本身有效
    }

    // ── 钉 2：Claim 顺序对调 ⇒ 诊断不变（资源分桶不依赖 First() 选取）。 ──
    [Fact]
    public async System.Threading.Tasks.Task ConflictReported_IndependentOfClaimOrder()
    {
        var diags = await RunAnalyzer(Source, Config(MultiLastConfig));
        var eaa0304 = diags.Where(d => d.Id == "EAA0304").ToArray();
        Assert.Single(eaa0304);
        Assert.Contains("Move+Move", eaa0304[0].GetMessage());
    }

    // ── 钉 3：对照——单资源映射行为不变（同资源跨 API 冲突照报；mem:111 仅 Multi 独占不误报）。 ──
    [Fact]
    public async System.Threading.Tasks.Task SingleResourceConflict_StillReported_NoFalsePositiveOnExclusiveResource()
    {
        const string singleResourceConfig = """
            {
              "extraMappings": [
                { "api": "Multi",  "claims": [
                  { "kind": "occupy", "resource": { "memory": 222 }, "mode": "move", "scope": { "scene": "Battle" } } ] },
                { "api": "Other",  "claims": [
                  { "kind": "occupy", "resource": { "memory": 222 }, "mode": "move", "scope": { "scene": "Battle" } } ] }
              ]
            }
            """;
        var diags = await RunAnalyzer(Source, Config(singleResourceConfig));
        Assert.Single(diags.Where(d => d.Id == "EAA0304")); // 修复不得回归既有单资源冲突检测
    }

    // ── 测试基建（与 QedP2C1bAdditionalFilesPins 同型拷贝，保持文件自包含）。 ──
    private static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source, params AdditionalText[] additionalFiles)
    {
        var refs = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location),
        };
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(p);
            if (name is "System.Runtime" or "System.Collections.Immutable")
                refs.Add(MetadataReference.CreateFromFile(p));
        }
        var compilation = CSharpCompilation.Create(
            "MultiResourceClaimPins",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()),
            new AnalyzerOptions(ImmutableArray.Create(additionalFiles)));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static AdditionalText Config(string json) => new InMemoryAdditionalText("effectledger.config.json", json);

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
