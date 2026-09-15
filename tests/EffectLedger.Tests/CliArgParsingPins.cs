// CliArgParsingPins.cs — 审计 2026-09-15 修复钉：CLI 参数解析 loud 化。
// 缺陷（实测）：`--out` 无值 / `--out=path` 等号形态 / 未知 flag 全被静默忽略 ⇒ 脚本按 README ⑤
// 的 AI 闭环期望落盘违例文件，实际拿到 exit 0/2 却找不到文件（"静默少做一件事"，与立库「绝不静默」不符）。
// 修复：三种形态一律 stderr 报错 + exit 1（契约内码位）；正常 `--out path` 行为不变。
using System.Diagnostics;
using System.IO;
using Xunit;

namespace EffectLedger.Tests;

public sealed class CliArgParsingPins
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent!;
        return dir!.FullName;
    }

    private static (int Code, string StdOut, string StdErr) RunTool(params string[] args)
    {
        var dll = Path.Combine(RepoRoot(), "src", "EffectLedger.Tool", "bin", "Release", "net10.0", "EffectLedger.Tool.dll");
        if (!File.Exists(dll)) return (0, "(skip: Tool 未构建)", "");
        var psi = new ProcessStartInfo { FileName = "dotnet", ArgumentList = { "exec", dll } };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return ChildProcessRunner.Run(psi, 60_000);
    }

    private static string PassScript()
    {
        var p = Path.Combine(Path.GetTempPath(), $"cli-arg-{System.Guid.NewGuid():N}.json");
        File.WriteAllText(p, """
            {
              "events": [
                { "lifetime": [0, 10], "scope": {"scene": "S"},
                  "footprint": [
                    { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" },
                    { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"release" } ] }
              ]
            }
            """);
        return p;
    }

    // ── 钉 1：`--out` 缺值 ⇒ exit 1 + stderr 报错（修复前：exit 0，静默不落盘）。 ──
    [Fact]
    public void OutWithoutValue_Exits1_Loud()
    {
        var script = PassScript();
        try
        {
            var (code, _, stderr) = RunTool("audit", script, "--out");
            if (code == 0 && stderr.Contains("skip")) return;
            Assert.Equal(1, code);
            Assert.Contains("--out", stderr);
        }
        finally { File.Delete(script); }
    }

    // ── 钉 2：`--out=<path>` 等号形态 ⇒ exit 1（不被支持，须 loud 而非静默忽略）。 ──
    [Fact]
    public void OutEqualsForm_Exits1_Loud()
    {
        var script = PassScript();
        var outp = Path.Combine(Path.GetTempPath(), $"cli-eq-{System.Guid.NewGuid():N}.json");
        try
        {
            var (code, _, stderr) = RunTool("audit", script, $"--out={outp}");
            if (code == 0 && stderr.Contains("skip")) return;
            Assert.Equal(1, code);
            Assert.False(File.Exists(outp)); // 明确不落盘（而非"以为落了"）
        }
        finally { File.Delete(script); }
    }

    // ── 钉 3：未知 flag ⇒ exit 1（修复前：exit 0 静默忽略）。 ──
    [Fact]
    public void UnknownFlag_Exits1_Loud()
    {
        var script = PassScript();
        try
        {
            var (code, _, stderr) = RunTool("audit", script, "--bogus");
            if (code == 0 && stderr.Contains("skip")) return;
            Assert.Equal(1, code);
            Assert.Contains("--bogus", stderr);
        }
        finally { File.Delete(script); }
    }

    // ── 钉 4（回归守卫）：正常 `--out path` 仍 exit 0 且真实落盘——loud 化不得误伤契约用法。 ──
    [Fact]
    public void OutWithValue_StillWorks_AndWritesFile()
    {
        var script = PassScript();
        var outp = Path.Combine(Path.GetTempPath(), $"cli-ok-{System.Guid.NewGuid():N}.json");
        try
        {
            var (code, _, _) = RunTool("audit", script, "--out", outp);
            if (code == 0 && !File.Exists(outp) && !Directory.Exists(Path.GetDirectoryName(outp)!)) return;
            Assert.Equal(0, code);
            Assert.True(File.Exists(outp), "--out 须真实落盘（README ⑤ 的 AI 闭环依赖）");
        }
        finally { File.Delete(script); if (File.Exists(outp)) File.Delete(outp); }
    }
}
