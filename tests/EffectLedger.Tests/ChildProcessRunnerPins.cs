// ChildProcessRunnerPins.cs — ChildProcessRunner 行为回归钉（台账 O-2026-09-14-01 修复证明）：
// 钉 1（stderr 灌流）：子进程向 stderr 写 4MB（远超管道 ~4KB 缓冲）——旧同步 ReadToEnd 写法在此
//   形态下【确定性死锁】（子进程阻塞在 stderr 写上、stdout 永不 EOF、父侧 ReadToEnd 永不返回）；
//   修复后并发排空必须干净返回。
// 钉 2（有界等待）：子进程长时间驻留 ⇒ 按超时杀整进程树并抛 TimeoutException，绝不无限挂起。
// 两钉的子进程经【内存编译 + dotnet exec】构造——全程不触碰 MSBuild/共享 Roslyn 编译服务器
//（VBCSCompiler 排队 15 分钟超时循环是 O-2026-09-14-01 挂起的第二根因，钉自身不得依赖它）。
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EffectLedger.Tests;

public sealed class ChildProcessRunnerPins
{
    // 内存编译临时程序 → 落盘 dll + 手写 runtimeconfig → dotnet exec（零 MSBuild 参与）。
    private static string EmitTempApp(string source)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cpr-{Guid.NewGuid():N}"));
        var refs = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var p in tpa)
            if (Path.GetFileNameWithoutExtension(p) is "System.Runtime")
                refs.Add(MetadataReference.CreateFromFile(p));
        // Console 属独立门面程序集，最小 TPA 引用集不含——从共享框架目录补引（Thread 在 CoreLib 故钉 2 不需要）。
        var consoleDll = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Console.dll");
        if (File.Exists(consoleDll)) refs.Add(MetadataReference.CreateFromFile(consoleDll));
        var comp = CSharpCompilation.Create("cprapp",
            new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)); // 顶级语句须可执行形态
        var dll = Path.Combine(dir.FullName, "app.dll");
        var emit = comp.Emit(dll);
        Assert.True(emit.Success,
            "临时程序编译失败：" + string.Join("; ", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage())));
        File.WriteAllText(Path.Combine(dir.FullName, "app.runtimeconfig.json"),
            """{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");
        return dll;
    }

    [Fact]
    public void Runner_StderrFlood_ReturnsCleanly_NoDeadlock()
    {
        var dll = EmitTempApp("""
            for (int i = 0; i < 4096; i++)
            {
                System.Console.Error.Write(new string('x', 1024));
                System.Console.Error.Write('\n');
            }
            System.Console.Out.Write("DONE\n");
            """);
        try
        {
            var psi = new ProcessStartInfo("dotnet");
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add(dll);
            var (code, stdout, stderr) = ChildProcessRunner.Run(psi, 120_000);
            Assert.Equal(0, code);
            Assert.Equal("DONE", stdout.Trim()); // 行事件累加带行尾换行
            Assert.True(stderr.Length >= 4 * 1024 * 1024, $"stderr 应完整回收（≥4MB），实际 {stderr.Length}");        }
        finally { Directory.Delete(Path.GetDirectoryName(dll)!, recursive: true); }
    }

    [Fact]
    public void Runner_Timeout_KillsTree_AndThrows()
    {
        var dll = EmitTempApp("System.Threading.Thread.Sleep(60_000);");
        try
        {
            var psi = new ProcessStartInfo("dotnet");
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add(dll);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Assert.Throws<TimeoutException>(() => ChildProcessRunner.Run(psi, 3_000));
            Assert.True(sw.ElapsedMilliseconds < 15_000, "超时后必须及时杀树返回，不得等满子进程时长");
        }
        finally { Directory.Delete(Path.GetDirectoryName(dll)!, recursive: true); }
    }
}
