// QedP53HardeningPins.cs — P5.3 对抗审计处置钉（会议纪要 audit/p5-adversarial-meeting.md）：
// ①H3 计数门（测试侧静态代理）：formal/*.dfy 的 lemma 声明总量 ≥ 60——定律被删减即红；
//   CI 侧真实 verified 计数 ≥ 89 由 ci.sh/ci.yml 的 dafny 步骤承载（两层防御）。
// ②M7 诊断面快照：L3 SupportedDiagnostics 恰 6 ID + 默认 Warning + 默认启用——
//   诊断公共面与 43 类型面同等冻结（新增/删除诊断 = 破坏性，EAA0701 先例：新 ID 须登记）。
// ③L11：effect-script schema 的 version major 段必须与 Directory.Build.props 版本 major 一致
//   （消解「钉硬编码 1.0.0 vs checklist 一致性要求」的矛盾——只锁 major，minor/patch 各自独立）。
// xUnit。
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectLedger.Tests;

public class QedP53HardeningPins
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（EffectLedger.slnx）");
    }

    // ── 钉 1（H3 计数门·测试侧代理）：formal/*.dfy 的 lemma 声明 ≥ 60。 ──
    [Fact]
    public void DafnyLemmaDeclarations_AtLeast60()
    {
        var total = 0;
        foreach (var f in new[] { "EffectLedger.dfy", "EffectLedgerSweepLine.dfy" })
        {
            foreach (var line in File.ReadAllLines(Path.Combine(RepoRoot(), "formal", f)))
            {
                var l = line.TrimStart();
                if (l.StartsWith("lemma ") || l.StartsWith("twostate lemma ")) total++;
            }
        }
        Assert.True(total >= 60, $"P3-H3 计数门（代理）：lemma 声明 {total} < 60——定律被删减即红；" +
            "真实 verified 计数由 CI 的 dafny verify 步骤断言 ≥ 89");
    }

    // ── 钉 2（M7 诊断面快照）：L3 SupportedDiagnostics 恰 6 ID + 默认 Warning + 默认启用。 ──
    [Fact]
    public void Analyzer_SupportedDiagnostics_Frozen()
    {
        var analyzer = new EffectLedger.Analyzer.EffectAlgebraAnalyzer();
        var supported = analyzer.SupportedDiagnostics;

        var ids = supported.Select(d => d.Id).OrderBy(s => s).ToArray();
        Assert.Equal(
            new[] { "EAA0303", "EAA0304", "EAA0701", "EAA0801", "EAA0802", "EAA0901" }, ids);
        Assert.All(supported, d => Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity));
        Assert.All(supported, d => Assert.True(d.IsEnabledByDefault, $"{d.Id} 必须默认启用"));
        // 生成器侧 EAA0701 描述符的 ID 一致性由 QedP2C1c pins（坏配置 → EAA0701）端到端承载
    }

    // ── 钉 3（L11）：schema version major == 包版本 major（Directory.Build.props 单一真源）。 ──
    [Fact]
    public void SchemaVersion_Major_MatchesPackageVersion()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        var pkg = Regex.Match(props, @"<Version>(\d+)\.(\d+)\.(\d+)</Version>");
        Assert.True(pkg.Success, "Directory.Build.props 缺 strict semver Version（P4-E1 钉应已拦）");
        var pkgMajor = pkg.Groups[1].Value;

        var schema = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "docs", "effect-script.schema.json"))).RootElement;
        var schemaVersion = schema.GetProperty("version").GetString();
        Assert.Matches(@"^\d+\.\d+\.\d+$", schemaVersion);
        Assert.Equal(pkgMajor, schemaVersion!.Split('.')[0]); // 只锁 major：minor/patch 各自独立演进
    }
}
