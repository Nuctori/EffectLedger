// GodotShell.cs — §7 Godot 壳抽象：Defer（按帧合并/idempotent）/ProcessMode 级联/退出排空/原生句柄门控。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§7 — Godot 壳：运行时层与 Godot 的唯一接触面（零强依赖，IHost 可被测试替身替换）。
/// A3-06（生产审计批3）线程契约：非线程安全（零锁）——Defer/FlushExitDrain/Cascade 等全部调用须在宿主主线程（帧循环）内。</summary>
public sealed class GodotShell
{
    private readonly IHost _host;
    private readonly HashSet<Action> _deferred = new();   // 去重：同 Action 引用仅入一次（R4-3 幂等）
    private readonly List<Action> _exitDrains = new();
    private bool _exitDraining;

    /// <summary>A3-15（生产审计批3）— 壳上第三方 drain 的异常观测出口：此前裸 catch{} 凭空吞掉，
    /// 与 Runtime「崩溃须可观测」口径不符；宿主可挂接上报（不挂接则维持吞掉语义，不向上抛）。</summary>
    public Action<Exception>? OnDrainFault { get; set; }

    public GodotShell(IHost host) => _host = host;

    /// <summary>§7 R5-4 — 壳级 Defer：合并同 Action（去重），每帧排空。无句柄重载：host 级合法性由 FakeHost/真实 Godot 宿主在 _host.Defer 内保证。</summary>
    public void Defer(Action action) => Defer(action, null);

    /// <summary>§7 R5-4 / §1 R4-3（reviewer #191 F2）— 壳级 Defer：合并同 Action（去重），每帧排空；执行前判空（Godot Object 经 QueueFree 后裸引用失效则丢弃，不调用）。
    /// handle 为 Action 触及的 Godot 原生对象；传 null 表示无需门控（host 级合法性由宿主保证）。此前 §7 注释声称「Suspending 后由 IsInstanceValid 门控」却未实装——现真接通（fail‑closed：句柄失效即丢回调，避免 use‑after‑free）。</summary>
    public void Defer(Action action, object? handle)
    {
        if (action == null) return;
        if (_exitDraining) return; // §3 R4-1：退出期（_ExitTree 触发 FlushExitDrain 已置 _exitDraining）禁止新 Defer，避免退出序结束后的 use-after-free；_exitDraining 在退出路径持续为真（节点释放不可逆），flush 后新 Defer 仍丢弃
        if (!_deferred.Add(action)) return; // 幂等：已 enqueue 则跳过
        _host.Defer(() =>
        {
            // A3-05（生产审计批3）：入队时判 _exitDraining 只挡「新」Defer——退出【前】已入队、退出【后】
            // 才被宿主帧执行的闭包（call_deferred 队列与本帧 _ExitTree 先后不保证）必须在此复查，
            // 否则树拆除后回调照跑（use-after-free 窗口，§3 R4-1 的时序洞）。fail-closed：直接丢弃。
            if (_exitDraining) { _deferred.Remove(action); return; }
            // §1 R4-3：句柄失效则静默丢弃；IsSafeToInvoke 可能抛（真实宿主 IsInstanceValid 异常）→ 隔离为 not-safe，fail-closed 不逃逸进宿主 defer 机制
            if (_deferred.Remove(action))
            {
                bool safe;
                try { safe = handle == null || IsSafeToInvoke(handle); }
                catch { safe = false; }
                if (safe) action();
            }
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

    /// <summary>§3 R4-1 — 关闭路径同步排空（宿主 _ExitTree 调用）。门控：退出期禁止新 Defer。
    /// A3-15：第三方 drain 异常经 OnDrainFault 观测（无订阅者则吞掉，维持不中断其余 drain 的语义）。</summary>
    public void FlushExitDrain()
    {
        _exitDraining = true;
        foreach (var d in _exitDrains.ToArray())
        {
            try { d(); }
            catch (Exception ex) { OnDrainFault?.Invoke(ex); }
        }
        _exitDrains.Clear();
    }

    /// <summary>§1 R4-3 — 原生句柄门控：Defer/逆 Action 执行前判空（Godot Object 经 QueueFree 后裸引用失效）。</summary>
    public bool IsSafeToInvoke(object handle) => _host.IsInstanceValid(handle);

    public bool ExitDraining => _exitDraining;
}

/// <summary>§7 — 测试替身 IHost（记录调用，供断言 ProcessMode 级联 / Defer 合并 / 退出排空）。
/// A3-12：IHost.EnqueueExitDrain 死成员已移除——GodotShell 自持 _exitDrains，宿主从不接收 drain 注册。</summary>
public sealed class FakeHost : IHost
{
    public readonly List<Action> Deferred = new();
    public readonly List<(FiberId Id, bool Disabled)> ProcessModes = new();
    public bool AllValid = true;

    public void Defer(Action action) => Deferred.Add(action);
    /// <summary>测试用：同步执行全部已记录 Defer 闭包（Godot 壳真实宿主由 _Process 帧驱动排空；FakeHost 无帧循环故显式 flush）。</summary>
    public void FlushDeferred() { foreach (var a in Deferred.ToArray()) a(); Deferred.Clear(); }
    public void SetProcessMode(FiberId id, bool disabled) => ProcessModes.Add((id, disabled));
    public bool IsInstanceValid(object handle) => AllValid;
}
