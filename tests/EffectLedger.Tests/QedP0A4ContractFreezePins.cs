// QedP0A4ContractFreezePins.cs — QED 迭代 P0-A4 契约冻结钉：
// ①CLI 退出码 0（通过路径）——R3 已钉 1/2 与 --out 落盘，通过路径此前无行为钉（README ⑤ 契约表最后一格）；
// ②L1 两族异常方言单点汇总（JSON 契约=FormatException / L1 参数违约=ArgumentException 族），
//   与既有 Round4Hickey2/R6RB 分散钉互为冻结快照；Runtime 两族（LoadValidationException /
//   InvalidOperationException）由 Runtime.Tests 既有钉承载，跨工程不重复。
// 冻结声明：README ⑤/诚实边界 #17 + EFFECT_SCRIPT §4【QED-A4】——自 2026-09-06 起语义冻结，变更=semver major。xUnit。
using System.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

public sealed class QedP0A4ContractFreezePins
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（EffectLedger.slnx）");
    }

    static (int Code, string StdOut, string StdErr) RunTool(params string[] args)
    {
        var dll = Path.Combine(RepoRoot(), "src", "EffectLedger.Tool", "bin", "Release", "net10.0", "EffectLedger.Tool.dll");
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

    // ── 冻结钉 1：CLI 退出码 0 = 审计通过（README ⑤ 契约表的通过格，真实子进程）。 ──
    [Fact]
    public void EffectLedgerAudit_Pass_Exits0_AndReportsPassedTrue()
    {
        var ok = Path.Combine(Path.GetTempPath(), $"qed-a4-pass-{Guid.NewGuid():N}.json");
        // README ④ 同款守恒对（create@0-6 / release@6-12，同 gpu 实例）：净占用闭合、无预算 ⇒ Passed。
        File.WriteAllText(ok, """
            {
              "events": [
                { "lifetime": [0, 6], "scope": { "scene": "Battle" },
                  "footprint": [ { "kind": "occupy", "resource": { "gpu": "m0" }, "mode": "create", "size": [256, 256] } ] },
                { "lifetime": [6, 12], "scope": { "scene": "Battle" },
                  "footprint": [ { "kind": "occupy", "resource": { "gpu": "m0" }, "mode": "release", "size": [256, 256] } ] }
              ]
            }
            """);
        try
        {
            var (code, stdout, _) = RunTool("audit", ok);
            Assert.Equal(0, code);
            Assert.Contains("\"passed\": true", stdout);
        }
        finally { File.Delete(ok); }
    }

    // ── 冻结钉 2：L1 异常方言表（README 诚实边界 #17 前两族的单点冻结快照）。 ──
    [Fact]
    public void L1_ExceptionDialect_Table_Frozen()
    {
        // JSON 契约族 = FormatException（非法形状；含 R6-RB 重复键/控制字符/非对象 claim 各分散钉）
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse("{ not json"));
        // L1 参数违约族 = ArgumentException（含 ArgumentOutOfRangeException 子类）
        var sig = Signature.Of(new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Use, new ScopeId.Scene("S"), Interval.Exact(1)).Normalize());
        Assert.ThrowsAny<ArgumentException>(() => Combination.Loop(sig, default, new ScopeId.Global()));
    }
}
