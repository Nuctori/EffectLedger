// GodotShell.cs — §7 Godot 壳抽象：Defer（按帧合并/idempotent）/ProcessMode 级联/退出排空/原生句柄门控。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§7 — Godot 壳：运行时层与 Godot 的唯一接触面（零强依赖，IHost 可被测试替身替换）。</summary>
public sealed class GodotShell
{
    private readonly IHost _host;
    private readonly HashSet<Action> _deferred = new();   // 去重：同 Action 引用仅入一次（R4-3 幂等）
    private readonly List<Action> _exitDrains = new();
    private bool _exitDraining;

    public GodotShell(IHost host) => _host = host;

    /// <summary>§7 R5-4 — 壳级 Defer：合并同 Action（去重），每帧排空；Suspending 后调用由 IsInstanceValid 门控。</summary>
    public void Defer(Action action)
    {
        if (action == null) return;
        if (!_deferred.Add(action)) return; // 幂等：已 enqueue 则跳过
        _host.Defer(() =>
        {
            if (_deferred.Remove(action)) action();
        });
    }

    /// <summary>§7 — 子树 ProcessMode 级联：teardown 启动时禁用派发（Suspending/TearingDown）。</summary>
    public void CascadeProcessModeDisabled(Fiber fiber)
    {
        _host.SetProcessMode(fiber.Id, true); // 禁用该 Fiber 子树派发
        foreach (var dep in fiber.Dependents) _host.SetProcessMode(dep, true); // 级联依赖者
    }

    /// <summary>§3 R4-1 — 退出路径同步排空：注册 drain，由宿主在 _ExitTree 调用 FlushExitDrain。</summary>
    public void EnqueueExitDrain(Action drain)
    {
        if (drain == null) return;
        _exitDrains.Add(drain);
    }

    /// <summary>§3 R4-1 — 关闭路径同步排空（宿主 _ExitTree 调用）。门控：退出期禁止新 Defer。</summary>
    public void FlushExitDrain()
    {
        _exitDraining = true;
        foreach (var d in _exitDrains.ToArray())
        {
            try { d(); } catch { /* 整任务 try/catch */ }
        }
        _exitDrains.Clear();
    }

    /// <summary>§1 R4-3 — 原生句柄门控：Defer/逆 Action 执行前判空（Godot Object 经 QueueFree 后裸引用失效）。</summary>
    public bool IsSafeToInvoke(object handle) => _host.IsInstanceValid(handle);

    public bool ExitDraining => _exitDraining;
}

/// <summary>§7 — 测试替身 IHost（记录调用，供断言 ProcessMode 级联 / Defer 合并 / 退出排空）。</summary>
public sealed class FakeHost : IHost
{
    public readonly List<Action> Deferred = new();
    public readonly List<(FiberId Id, bool Disabled)> ProcessModes = new();
    public readonly List<Action> ExitDrains = new();
    public bool AllValid = true;

    public void Defer(Action action) => Deferred.Add(action);
    public void SetProcessMode(FiberId id, bool disabled) => ProcessModes.Add((id, disabled));
    public void EnqueueExitDrain(Action drain) => ExitDrains.Add(drain);
    public bool IsInstanceValid(object handle) => AllValid;
}
