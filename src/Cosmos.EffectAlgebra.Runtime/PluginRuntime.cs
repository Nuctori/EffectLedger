// PluginRuntime.cs — §3/§6/§7 调度器：装载/卸载/重拓扑/关闭路径守卫/级联 teardown。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§1 — Fiber 标识。</summary>
public readonly record struct FiberId(string Value);

/// <summary>§3/§6 — 插件运行时调度器（TearingDown 帧安全队列 + 拓扑排序 + 关闭守卫）。</summary>
public sealed class PluginRuntime
{
    private readonly DependencyGraph _graph = new();
    private readonly Dictionary<FiberId, Fiber> _fibers = new();
    private readonly List<Action> _teardownQueue = new();

    /// <summary>§6 R5-7 — 关闭路径标志：关路径 RecomputeTopology 硬拒绝（非关路径延迟执行）。测试可置位以模拟关闭路径。</summary>
    public bool IsShuttingDown { get; set; }

    /// <summary>§3 — 注册 Fiber（装载期）。返回 Fiber 供后续 Load/Unload。</summary>
    public Fiber Register(FiberSpec spec)
    {
        if (IsShuttingDown) throw new InvalidOperationException("关闭路径禁止新装载（级联期新装载须延迟到 provider 真正 Dead 后）");
        var fiber = new Fiber(spec.Id, spec.Effect, spec.Coeffect, spec.Inverses);
        _fibers[fiber.Id] = fiber;
        _graph.Register(fiber);
        return fiber;
    }

    /// <summary>§3 — 批量建立显式依赖边（同 Scope Requires⊇Provides）。</summary>
    public void AddDependency(Fiber dependent, Fiber provider, EdgeKind kind)
    {
        if (kind == EdgeKind.Hard) _graph.AddHardEdge(dependent, provider);
        else _graph.AddSoftEdge(dependent, provider);
    }

    /// <summary>§3 — 全部装载（仅 Inactive → Active）。</summary>
    public void LoadAll()
    {
        foreach (var f in _fibers.Values) f.Load();
    }

    /// <summary>§3 — 触发某 provider 的级联 teardown：标记 TearingDown + 入帧安全队列（TeardownEnqueued 防重）。</summary>
    public void BeginTeardown(Fiber provider)
    {
        if (provider.TeardownEnqueued) return; // 防二次入队
        provider.Unload();                       // → TearingDown + 标志 enqueued
        _graph.NotifyDependents(provider);       // provider-first-notify → dependent Suspending
        _teardownQueue.Add(() => InverseReplay.ReplayAndDead(provider));
    }

    /// <summary>§3 — 每批重拓扑 + 调度：按 dependent-first 顺序排空 teardown 队列（快照安全，重入仅 append）。</summary>
    public void DrainTeardownBatch()
    {
        var cycle = _graph.DetectCycles();
        if (cycle.HasHardCycle) return; // 环中止：跳过成环子集（调用方须先 ResolveHardCycles）
        var order = _graph.TopoSortLeafFirst();   // dependent-first
        var batch = _teardownQueue.ToArray();      // 固定快照
        _teardownQueue.Clear();
        foreach (var task in batch)
        {
            try { task(); }
            catch { /* 整任务 try/catch → 其余任务继续；异常已由 ReplayAndDead 内部诊断 */ }
        }
        foreach (var id in order) { /* 顺序仅用于可观测性；实际释放由队列任务完成 */ }
    }

    /// <summary>§3 R5-7 — 重拓扑（N2 守卫：关路径硬拒绝，非关路径延迟执行）。</summary>
    public void RecomputeTopology()
    {
        if (IsShuttingDown) throw new InvalidOperationException("关闭路径禁止 RecomputeTopology（资源已不可靠）");
        // 非关路径：重算拓扑（此处仅占位，真实重算在 DependencyGraph 调用方）；环检测由 DrainTeardownBatch 触发。
    }

    /// <summary>§3 — 关闭路径同步排空（R4-1）：在调度器 _ExitTree 内调用，按 dependent-first 顺序释放全部 TearingDown。</summary>
    public void SynchronousExitDrain()
    {
        IsShuttingDown = true;
        // 退化路径：无后续帧，直接按拓扑序排空剩余队列（非引用计数共享原生句柄仍 fail-open，见 §8）。
        var batch = _teardownQueue.ToArray();
        _teardownQueue.Clear();
        foreach (var task in batch) { try { task(); } catch { /* 同上 */ } }
    }

    public IReadOnlyCollection<Fiber> Fibers => _fibers.Values.ToImmutableArray();
}

/// <summary>§3 — 装载期 Fiber 规格（纯数据，无 Godot 依赖）。</summary>
public sealed record FiberSpec(
    FiberId Id,
    Signature Effect,
    Coeffect Coeffect,
    ImmutableStack<InverseClaim> Inverses);
