// ProviderCrashCascade.cs — §6 provider 崩溃级联：fail-open 标 Dead + 通知依赖者 Suspend + 异常上抛供调度器升级。
using System.Collections.Immutable;

namespace EffectLedger.Runtime;

/// <summary>§6 — provider 崩溃级联诊断。</summary>
public sealed record CrashReport(
    FiberId Provider,
    Exception? Exception,          // 逆回放抛出的异常（null = 干净 Dead）
    ImmutableArray<FiberId> NotifiedDependents);

/// <summary>§6 — provider 崩溃级联处理：fail-open 标 Dead（逆回放已尽力释放）+ 从依赖图查询真实依赖者 + 记录异常上抛供调度器升级。
/// 与 InverseReplay.ReplayAndDead 配合：replay 内部尽量继续释放其余逆（R4-6），此处仅收口失败状态并驱动级联通知。</summary>
public static class ProviderCrashCascade
{
    /// <summary>§6 — 处理单个 provider 的崩溃：标记 Dead（fail-open）+ 从图查依赖者并通知其 Suspending + 记录异常。</summary>
    public static CrashReport Handle(PluginRuntime runtime, Fiber provider, Exception? ex)
    {
        provider.MarkDead(); // fail-open：逆回放已尽力释放；崩溃后直接进 Dead（§2 仅 TearingDown→Dead）
        var dependents = runtime.Graph.DependentsOf(provider.Id);
        foreach (var dep in dependents)
            if (runtime.TryGetFiber(dep, out var d))
            {
                d.MarkSuspending(); // provider-first-notify → dependent Suspending（R4-4 幂等）
                // R3-RT-05（三轮审计）：与 BeginTeardown/看门狗两分支同型触发 OnSuspending（Godot 壳禁用
                // ProcessMode 的唯一接线）——崩溃升级路径新增的 Suspending dependent 此前不禁用 ProcessMode。
                try { runtime.OnSuspending?.Invoke(d); } catch { /* 钩子异常隔离（与其余级联路径一致） */ }
            }
        return new CrashReport(provider.Id, ex, dependents);
    }
}
