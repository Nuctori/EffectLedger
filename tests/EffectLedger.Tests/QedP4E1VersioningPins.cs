// QedP4E1VersioningPins.cs — P4-E1 semver 单一真源钉：
// 全包族版本只允许在 Directory.Build.props 声明一次（strict semver 三段数字）；
// 任何 src csproj 私藏 <Version> 即红（防漂移——漂移即 NuGet 同 id+version 缓存陷阱的根源）。
// 发布流程：改 Directory.Build.props 这一行 → PUBLISH-CHECKLIST。xUnit。
using System.Text.RegularExpressions;
using Xunit;

namespace EffectLedger.Tests;

public class QedP4E1VersioningPins
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（EffectLedger.slnx）");
    }

    // ── 钉 1：Directory.Build.props 恰含一个 <Version>，且为 strict semver 三段数字。 ──
    [Fact]
    public void BuildProps_HasSingleStrictSemverVersion()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        var matches = Regex.Matches(props, @"<Version>([^<]+)</Version>");
        Assert.True(matches.Count == 1, $"P4-E1：Version 必须只在 Directory.Build.props 声明一次，实际 {matches.Count} 处");
        Assert.Matches(@"^\d+\.\d+\.\d+$", matches[0].Groups[1].Value); // strict semver（发布前不带后缀）
    }

    // ── 钉 2：任何 src csproj 私藏 <Version> 即红（防漂移——历史根源：五处 1.0.0 各自为政）。 ──
    [Fact]
    public void SrcCsproj_NoLocalVersionDeclarations()
    {
        var violations = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"<Version>"))
            .Select(Path.GetFileName)
            .ToArray();
        Assert.Empty(violations); // 任何残留 ⇒ 单一真源被破坏 ⇒ 红
    }

    // ── 钉 3：五包 PackageId 清单稳定（包族成员不可静默增删——新增包须显式更新本钉并过评审）。 ──
    [Fact]
    public void PackageFamily_Members_Frozen()
    {
        var expected = new[]
        {
            "EffectLedger", "EffectLedger.Analyzer", "EffectLedger.Generator",
            "EffectLedger.Runtime", "EffectLedger.Tool"
        };
        foreach (var id in expected)
        {
            var csproj = Path.Combine(RepoRoot(), "src", id, $"{id}.csproj");
            Assert.True(File.Exists(csproj), $"包族成员缺失：{csproj}");
            Assert.Contains($"<PackageId>{id}</PackageId>", File.ReadAllText(csproj));
        }
    }
}
