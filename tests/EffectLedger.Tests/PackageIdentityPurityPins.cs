// PackageIdentityPurityPins.cs — 审计 2026-09-15 修复钉：发布物不得含改名前（Cosmos.*）身份残留。
// 缺陷（实证）：src/EffectLedger.Tool/bin/.../publish/ 存有改名（07809a2 Cosmos → EffectLedger）之前的
// Cosmos.EffectAlgebra* 产物，而 dotnet pack 对该工程走 publish 目录（PackAsTool）⇒ Tool nupkg 的
// tools/net10.0/any/ 同时含两套程序集身份（7 个 Cosmos.* 文件）。功能未坏（DotnetToolSettings 入口指向
// EffectLedger.Tool.dll），但发布物携带旧身份 = 供应链/合规面的身份混淆，且与改名提交自述矛盾。
// 根因是 bin/ 不进版本库、但 pack 复用既有输出目录 ⇒ 任何「改名前后都构建过」的机器都会复现（含发布人本机）。
// 修复：清理陈旧产物；本钉以「pack 产物不得含 Cosmos 前缀文件」把纯度钉死。
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace EffectLedger.Tests;

[Collection("SerialDotnetBuild")] // 与其他真实 dotnet 子进程测试互斥（复用 build 产物）
public sealed class PackageIdentityPurityPins
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent!;
        return dir!.FullName;
    }

    // ── 钉 1：Tool 的 pack 输入目录（bin/.../net10.0 与 publish）不得含 Cosmos.* 残留文件。 ──
    //    修复前：publish/ 含 6+1 个 Cosmos.EffectAlgebra* ⇒ 本钉红。
    [Theory]
    [InlineData("src/EffectLedger.Tool/bin/Release/net10.0")]
    [InlineData("src/EffectLedger.Tool/bin/Release/net10.0/publish")]
    public void ToolPackOutput_ContainsNoPreRenameArtifacts(string relDir)
    {
        var dir = Path.Combine(RepoRoot(), relDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dir)) return; // 未构建过 ⇒ 无残留可言（钉不因缺目录假红）
        var stale = Directory.GetFiles(dir, "Cosmos.*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToArray();
        Assert.True(stale.Length == 0,
            $"{relDir} 含改名前（Cosmos.*）产物 {stale.Length} 个——会被 PackAsTool 打进发布包，"
            + $"造成发布物身份混淆：{string.Join(", ", stale)}");
    }

    // ── 钉 2：已产出的 nupkg（若存在）不得含 Cosmos 前缀条目——端到端纯度。 ──
    [Fact]
    public void ToolNupkg_ContainsNoPreRenameEntries()
    {
        var feed = Path.Combine(RepoRoot(), "artifacts", "feed");
        if (!Directory.Exists(feed)) return; // 未 pack ⇒ 跳过（ConsumerSmoke 会产出）
        foreach (var pkg in Directory.GetFiles(feed, "*.nupkg"))
        {
            using var zip = ZipFile.OpenRead(pkg);
            var stale = zip.Entries.Select(e => e.FullName)
                .Where(n => n.Contains("/Cosmos.", System.StringComparison.Ordinal)
                            || n.Contains("/Cosmos.", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.True(stale.Length == 0,
                $"{Path.GetFileName(pkg)} 含改名前条目：{string.Join(", ", stale)}");
        }
    }
}
