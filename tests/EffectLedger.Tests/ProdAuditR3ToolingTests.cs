// ProdAuditR3ToolingTests.cs — 第三轮独立审计（2026-09）工具链/打包回归钉。
// R3-TQ-02：effectledger CLI 行为级测试此前为零（doc-guard 只守 README 字样）——退出码契约 0/1/2、
//           --out 落盘、events 计数（R3-CG-08 反「空剧本假绿」知情标记）全走真实子进程。
// R3-CG-03：Directory.Build.props 的 Exists() 须用正斜杠（反斜杠在 Linux MSBuild 按字面文件名
//           解析恒 false ⇒ README 不进包 ⇒ effectledger-audit.yml (ubuntu) 的 pack 门 NU5039 必红）。
// R3-CG-04：Generator 包多目标 net8.0;net9.0;net10.0——单目标 net10.0 时非 net10 消费者的
//           NuGet 依赖组静默丢失，生成代码硬引用 L1 类型必 CS0246（R2B-01 修复只对 net10 成立）。
// R3-CI-01：GodotReal（net8 真实消费样板）/ AnalyzerConsumer（真实分析器接法验证工程）入 slnx，
//           否则「net8 兼容验证」无任何门背书、可静默腐烂。
using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace EffectLedger.Tests;

public sealed class ProdAuditR3ToolingTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（EffectLedger.slnx）");
    }

    static (string Dll, string Config) ResolveToolDll()
    {
        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var p = Path.Combine(RepoRoot(), "src", "EffectLedger.Tool", "bin", cfg, "net10.0", "EffectLedger.Tool.dll");
            if (File.Exists(p)) return (p, cfg);
        }
        throw new InvalidOperationException("Tool DLL 未构建（先 dotnet build）");
    }

    static (int Code, string StdOut, string StdErr) RunTool(params string[] args)
    {
        var (dll, _) = ResolveToolDll();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "exec", dll },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(60_000);
        return (p.ExitCode, stdout, stderr);
    }

    // ── R3-TQ-02：退出码 1 = 解析失败 ──
    [Fact]
    public void EffectLedgerAudit_InvalidJson_Exits1()
    {
        var bad = Path.Combine(Path.GetTempPath(), $"r3-bad-{Guid.NewGuid():N}.json");
        File.WriteAllText(bad, "{ not json");
        try
        {
            var (code, _, stderr) = RunTool("audit", bad);
            Assert.Equal(1, code);
            Assert.Contains("parse", stderr);
        }
        finally { File.Delete(bad); }
    }

    // ── R3-TQ-02：退出码 2 = 违例 + --out 落盘 ──
    [Fact]
    public void EffectLedgerAudit_Violation_Exits2_AndWritesOutFile()
    {
        var leak = Path.Combine(Path.GetTempPath(), $"r3-leak-{Guid.NewGuid():N}.json");
        var outp = Path.Combine(Path.GetTempPath(), $"r3-out-{Guid.NewGuid():N}.json");
        File.WriteAllText(leak, """
        {
          "events": [
            { "lifetime":[0,10], "scope":{"scene":"Battle"},
              "footprint":[{"kind":"occupy","resource":{"gpu":"tex"},"mode":"create","size":[1,1]}] }
          ]
        }
        """);
        try
        {
            var (code, stdout, _) = RunTool("audit", leak, "--out", outp);
            Assert.Equal(2, code); // 修改前该路径零覆盖；误改 return 1 时 CI 不会红
            Assert.Contains("Leak", stdout);
            Assert.True(File.Exists(outp), "--out 须真实落盘");
        }
        finally { File.Delete(leak); File.Delete(outp); }
    }

    // ── R3-CG-05：--out 非法路径 → 退出码 1（此前未处理异常炸出非契约退出码） ──
    [Fact]
    public void EffectLedgerAudit_OutToInvalidPath_Exits1_NotCrash()
    {
        var leak = Path.Combine(Path.GetTempPath(), $"r3-leak2-{Guid.NewGuid():N}.json");
        File.WriteAllText(leak, """
        {
          "events": [
            { "lifetime":[0,10], "scope":{"scene":"Battle"},
              "footprint":[{"kind":"occupy","resource":{"gpu":"tex"},"mode":"create","size":[1,1]}] }
          ]
        }
        """);
        var badOut = Path.Combine(Path.GetTempPath(), $"no-such-dir-{Guid.NewGuid():N}", "x", "violations.json");
        try
        {
            var (code, _, _) = RunTool("audit", leak, "--out", badOut);
            Assert.Equal(1, code); // 修改前：Unhandled IO 异常（-532462766 类），破坏 0/1/2 契约
        }
        finally { File.Delete(leak); }
    }

    // ── R3-CG-08：payload 暴露 events 计数（「审计了 0 个事件」不再不可见） ──
    [Fact]
    public void EffectLedgerAudit_Payload_ContainsEventsCount()
    {
        var ok = Path.Combine(Path.GetTempPath(), $"r3-ok-{Guid.NewGuid():N}.json");
        File.WriteAllText(ok, """
        {
          "events": [
            { "lifetime":[0,10], "scope":{"scene":"S"},
              "footprint":[
                {"kind":"occupy","resource":{"gpu":"t"},"mode":"create","size":[1,1]},
                {"kind":"occupy","resource":{"gpu":"t"},"mode":"release","size":[1,1]} ] }
          ]
        }
        """);
        try
        {
            var (code, stdout, _) = RunTool("audit", ok);
            Assert.Equal(0, code);
            Assert.Matches(new Regex("\"events\":\\s*1\\b"), stdout);
            // QED-P5.3 方言 LOW 收口：载荷透出 CapsChecked/IsPeakChecked——AI 闭环可区分「没查/查了/幽灵查」
            Assert.Matches(new Regex("\"capsChecked\":\\s*0"), stdout); // 无 budget ⇒ 0（透出状态而非断言值；QED-P5.3 LOW 收口）
            Assert.Matches(new Regex("\"isPeakChecked\":\\s*(true|false)"), stdout);
        }
        finally { File.Delete(ok); }
    }

    // ── R3-CG-03：打包 README Include 条件须正斜杠（Linux MSBuild Exists 字面匹配） ──
    [Fact]
    public void BuildProps_ReadmeInclude_UsesForwardSlashCondition()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        Assert.DoesNotContain("Exists('..\\..\\README.md')", props); // 修改前：Linux 上恒 false ⇒ NU5039
        Assert.Contains("Exists('../../README.md')", props);
    }

    // ── R3-CG-04：Generator 多目标（依赖组覆盖 net8/net9 消费者） ──
    // ── R3-CG-04（P1-B5 更新）：Generator 多目标与 L1 对齐（net8.0;net10.0）——net9 消费者按
    //    NuGet 就近原则消费 net8.0 资产（覆盖面不变）；原「net8.0;net9.0;net10.0」随 L1 net9.0
    //    缩减切片（A2-06 后残迹）一并删除，同包各 TFM 同一公共面（QedP1B1 唯一准绳）。 ──
    [Fact]
    public void GeneratorCsproj_MultiTargets_AllConsumerTfms()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "EffectLedger.Generator", "EffectLedger.Generator.csproj"));
        Assert.Contains("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", csproj); // 修改前：单 net10.0 ⇒ 依赖组仅 net10.0，net8 消费者静默丢 L1
        // P1-B5：net9.0 档不再存在于 TFM 列表（注释中的历史记述不算数，只认元素本身）
        Assert.DoesNotContain("<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>", csproj);
        Assert.DoesNotContain("<TargetFrameworks>net9.0", csproj);
    }

    // ── R3-CI-01：net8 验证工程入 slnx（否则无门背书） ──
    [Fact]
    public void Slnx_ContainsNet8VerificationSamples()
    {
        var slnx = File.ReadAllText(Path.Combine(RepoRoot(), "EffectLedger.slnx"));
        Assert.Contains("GodotReal", slnx);
        Assert.Contains("AnalyzerConsumer", slnx);
    }
}
