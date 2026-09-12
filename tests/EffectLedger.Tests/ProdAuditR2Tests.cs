// ProdAuditR2Tests.cs — 第二轮独立审计（2026-09）回归钉。
// R2A-01 peakScope 陈旧归因回归 / R2A-07 反冒充负例 / R2A-08 ComputeSamplePoints 守卫 / R2A-05 inf 文案。
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

public class ProdAuditR2Tests
{
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));
    static ScopeId.Scene Scene(string s) => new ScopeId.Scene(s);
    static Claim Oc(ResourceId r, Mode m, ScopeId s, Interval sz) => new Claim(Kind.Occupy, r, m, s, sz).Normalize();
    static Claim Rd(ResourceId r, ScopeId s, Interval sz) => new Claim(Kind.Read, r, Mode.Use, s, sz).Normalize();

    // ── R2A-01：gate(3) 全桶化后，跨生命周期的 read claim 不得让 peakScope 退出清理失效（S06-004 回归） ──
    // 场景：e0 occupy create [0,5] scope A；e1 read use [0,100]（跨 e0/e2 生命周期）；e2 occupy create [10,15] scope B size 20，cap=10。
    // t=10 的 PeakExceeded 必须归因 (B, e2)，不得因 e1 的 read 组员滞留指向 (A, e0)。
    [Fact]
    public void PeakAttribution_AcrossReadClaimLifetime_ReportsCurrentScope()
    {
        var e0 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), Scene("A"),
            Signature.Of(Oc(Gpu("x"), Mode.Create, Scene("A"), Interval.Exact(2))), LoopCount.Of(1));
        var e1 = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(100)), Scene("A"),
            Signature.Of(Rd(Gpu("x"), Scene("A"), Interval.Exact(1))), LoopCount.Of(1));
        var e2 = new EffectEvent(new Interval(NatStar.Of(10), NatStar.Of(15)), Scene("B"),
            Signature.Of(Oc(Gpu("x"), Mode.Create, Scene("B"), Interval.Exact(20))), LoopCount.Of(1));
        var cap = new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar> { [Gpu("x")] = NatStar.Of(10) });
        var r = new EffectScript(ImmutableArray.Create(e0, e1, e2)).Audit(cap);
        var peak = r.Violations.Single(v => v.Kind == "PeakExceeded");
        Assert.Equal("B", ((ScopeId.Scene)peak.Scope).Name);   // 修改前：A（read 组员令 hasActiveGrp 恒真 ⇒ 清理被跳过）
        Assert.Equal(2, peak.EventIndex);                       // 修改前：0（指向已闭合事件，误导 AI 回修）
    }

    // ── R2A-08：ComputeSamplePoints 是第三个公开消费入口，须与 At/Audit 同源 loud 守卫 ──
    [Fact]
    public void ComputeSamplePoints_DefaultEvent_IsLoud()
    {
        Assert.Throws<ArgumentException>(
            () => EffectScript.ComputeSamplePoints(ImmutableArray.Create(default(EffectEvent))));
    }

    // ── R2A-05：interval/size 拒绝文案须含 "inf"（IsTopAlias 统一后的方言自洽） ──
    [Fact]
    public void Parse_LifetimeTopTop_ErrorMentionsInf()
    {
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(
            """{"events":[{"lifetime":["⊤","⊤"],"scope":{"scene":"S"},"footprint":[]}]}"""));
        Assert.Contains("inf", ex.Message);
    }

    // ── R2A-07：用户自有同名 EffectOverrideAttribute 不得豁免 A3/A4（反冒充负例，语义识别的安全方向） ──
    [Fact]
    public async Task Analyzer_SpoofedOverrideAttribute_DoesNotExemptA3()
    {
        const string source = @"
using EffectLedger;
namespace MyLib { class EffectOverrideAttribute : System.Attribute { public EffectOverrideAttribute(string r) { } } }
namespace Godot.Shapes { public class Body { public void MoveAndSlide() { } public int GetSlideCollisionCount() => 0; } }
public class Spoofed
{
    [MyLib.EffectOverride(""fake"")]
    void M(Godot.Shapes.Body b) { b.MoveAndSlide(); _ = b.GetSlideCollisionCount(); }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0303"); // 冒充特性不得豁免意图提示
        Assert.DoesNotContain(diags, d => d.Id == "EAA0801"); // 自有特性也不触发 EffectLedger 的 reason 校验
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var refs = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location),
        };
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name == "System.Runtime" || name == "System.Collections.Immutable")
                refs.Add(MetadataReference.CreateFromFile(p));
        }
        var compilation = CSharpCompilation.Create(
            "R2Audit", new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
