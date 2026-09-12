// ProdAuditR6ToolingTests.cs — 第六轮独立审计（R6-P）工具链回归钉。
// R6-P-03：AnalyzerConsumer 样例声称「EAA0901 真在编译期出现」，但其 Game.cs 形状（自有
//           AddChild(object)、namespace Game）在 Godot.* 命名空间门控（A2-09）下永远零诊断
//           ——门为绿灯偶然，抓不住分析器静默失效。本钉以真实子进程 build 断言 EAA0901
//           必须出现在样例构建输出中（真实 ProjectReference + 编译器 ALC 路径，非 WithAnalyzers 内存桩）。
// R6-P-01 注：本机全局 NuGet 缓存按 id+version 复用且不校验内容（batch5 时代同版本旧包会毒化
//           本地重打包实验）——一切「装包行为」类结论必须以干净 NUGET_PACKAGES 复核。
using System.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

// R6：本类 spawn 真实 dotnet build（AnalyzerConsumer 样例），与 ProdAuditBatch4ToolingTests 的
// GateFixture 构建会并发触碰同一组 src/* obj ⇒ CS2001 偶红（GeneratedMSBuildEditorConfig 竞态）
// ——与 batch4 共用串行集合，互相排斥执行。
[CollectionDefinition("SerialDotnetBuild")]
public sealed class SerialDotnetBuildCollection { }

[Collection("SerialDotnetBuild")]
public sealed class ProdAuditR6ToolingTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EffectLedger.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（EffectLedger.slnx）");
    }

    [Fact]
    public void AnalyzerConsumer_Build_EmitsEaa0901()
    {
        var sample = Path.Combine(RepoRoot(), "samples", "AnalyzerConsumer", "AnalyzerConsumer.csproj");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            // -p:TargetFramework=net10.0：多目标 L1 只建 net10 切片，钉测试时长可控
            ArgumentList = { "build", sample, "-c", "Release", "--no-incremental", "-p:TargetFramework=net10.0" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(240_000);
        var output = stdout + stderr;
        Assert.True(p.ExitCode == 0, $"样例构建须成功，实际 exit={p.ExitCode}\n{output}");
        Assert.Contains("EAA0901", output); // 分析器须在真实编译期触发（样本 GodotLeaker 故意泄漏）
    }
}
