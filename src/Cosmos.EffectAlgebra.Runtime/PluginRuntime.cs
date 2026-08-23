// PluginRuntime.cs — §3/§6/§7 调度器（MVP 骨架）。装载/卸载/重拓扑/关闭路径守卫。
namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3/§6/§7 — 插件运行时（MVP 骨架）。</summary>
public sealed class PluginRuntime
{
    /// <summary>§6 R5-7 — 关闭路径标志：关路径 RecomputeTopology 硬拒绝（非关路径延迟执行）。</summary>
    public bool IsShuttingDown { get; internal set; }

    /// <summary>§3 — 重拓扑（N2 守卫：关路径硬拒绝）。</summary>
    public void RecomputeTopology()
    {
        if (IsShuttingDown) throw new InvalidOperationException("关闭路径禁止 RecomputeTopology（资源已不可靠）");
    }
}

/// <summary>§1 — Fiber 标识。</summary>
public readonly record struct FiberId(string Value);
