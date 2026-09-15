// ChildProcessRunner.cs — ROI 审计（2026-09-14）修复：测试内真实子进程的统一安全封装。
// 根因（台账 O-2026-09-14-01，dotnet-stack 栈证据钉死）：子进程 stdout/stderr 重定向管道的 EOF
// 可能被【第三方持有者】无限推迟——`dotnet build` 默认 nodeReuse=true 留下的驻留 MSBuild 节点
// 继承了管道句柄，节点一旦被外部终止成为孤儿（会话清理/看门狗杀进程均会制造孤儿），管道永不
// EOF，任何"读到 EOF"语义的等待（同步 ReadToEnd 或 ReadToEndAsync 的 await/GetResult）都永久
// 阻塞 ⇒ 测试宿主 CPU 空转挂起，被 vstest blame/hang 回收后表现为"测试主机进程崩溃"。
// （旧写法的双同步 ReadToEnd 另有经典的双流死锁变体：stderr 灌满 ~4KB 管道缓冲 ⇒ 同型永久阻塞。）
// 修复四件套：
//   ①事件累加（BeginOutputReadLine/BeginErrorReadLine）——输出增量落账，不依赖 EOF 才可得；
//   ②进程退出为唯一等待边界（WaitForExit 有界版），EOF 仅作宽限 flush（有界）；
//   ③超时杀整进程树——确定性红，绝不挂死；
//   ④注入 MSBUILDDISABLENODEREUSE=1——子构建的 MSBuild 节点随构建消亡，从源头消灭孤儿句柄持有者
//    （对非 MSBuild 子进程为无害环境变量）。UseSharedCompilation=false 由调用方按需追加。
using System.Diagnostics;
using System.Text;

namespace EffectLedger.Tests;

internal static class ChildProcessRunner
{
    internal static (int Code, string StdOut, string StdErr) Run(ProcessStartInfo psi, int timeoutMs)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1"; // 根因④：驻留节点不再持有管道句柄（对非 MSBuild 子进程无害）
        using var p = Process.Start(psi)!;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (stdout) stdout.AppendLine(e.Data); } };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (stderr) stderr.AppendLine(e.Data); } };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* 已退出竞态：忽略 */ }
            p.WaitForExit(2_000); // 给事件排空一个有界宽限
            throw new TimeoutException(
                $"子进程 {timeoutMs}ms 未退出，已杀整进程树：{psi.FileName} {string.Join(" ", psi.ArgumentList)}"
                + $"\n--- 已收到的部分输出 ---\nstdout:\n{Snapshot(stdout)}\nstderr:\n{Snapshot(stderr)}");
        }
        // EOF 宽限：正常情况子进程退出 ⇒ 管道关闭 ⇒ 事件立即排空；孤儿句柄病态下最多付 5s，绝不永久阻塞。
        p.WaitForExit(5_000);
        return (p.ExitCode, Snapshot(stdout), Snapshot(stderr));
    }

    private static string Snapshot(StringBuilder sb) { lock (sb) return sb.ToString(); }
}
