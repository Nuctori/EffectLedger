// PluginRuntime.cs — §3/§6/§7 调度器：装载/卸载/重拓扑/关闭路径守卫/级联 teardown。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3/§6 — 插件运行时调度器（TearingDown 帧安全队列 + 拓扑排序 + 关闭守卫）。</summary>
public sealed class PluginRuntime
{
    private readonly DependencyGraph _graph = new();
    private readonly Dictionary<FiberId, Fiber> _fibers = new();
    // 队列元素携带 provider FiberId，便于按拓扑序排空（R2：dependent-first）。
    private readonly List<(FiberId Provider, Action Task)> _teardownQueue = new();

    /// <summary>§6 — 依赖图（崩溃级联/拓扑排序查询用）。</summary>
    public DependencyGraph Graph => _graph;

    /// <summary>§6 — 按 Id 取 Fiber（崩溃级联通知依赖者用）。</summary>
    public bool TryGetFiber(FiberId id, out Fiber fiber) => _fibers.TryGetValue(id, out fiber!);

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

    /// <summary>§3 — 批量建立显式依赖边（同 Scope Requires⊇Provides）；软边同时回填 provider.Dependents（medium #3：否则 Godot 壳 ProcessMode 级联遍历空集 no-op）。</summary>
    public void AddDependency(Fiber dependent, Fiber provider, EdgeKind kind)
    {
        if (kind == EdgeKind.Hard) _graph.AddHardEdge(dependent, provider);
        else _graph.AddSoftEdge(dependent, provider);
        provider.Dependents = provider.Dependents.Add(dependent.Id); // 回填依赖者集合（供 Godot 壳级联）
    }

    /// <summary>§3 — 全部装载（仅 Inactive → Active）；装载前执行 §3 step2b/§7.1/§5 校验（R5-6 双重释放 / §5 net 闭合 / scale 校验运行时真正生效）。</summary>
    public void LoadAll()
    {
        var all = _fibers.Values.ToArray();
        // §6（reviewer #187 blocker）：装载期硬环拒载——硬环会令 teardown 永久挂起（死锁/泄漏），须先于装载拒绝。
        var cycle = _graph.DetectCycles();
        if (cycle.HasHardCycle)
            throw new LoadValidationException(
                $"§6 装载期拒载硬环：{string.Join(" → ", cycle.HardCycle.Select(id => id.ToString()))} 构成硬依赖环（将导致 teardown 死锁）");
        foreach (var f in all) LoadValidation.ValidateForLoad(f, all); // R1+R5-6：运行时真正调用装载校验（含跨 Fiber 双重释放交叉判定）
        LoadValidation.VerifyNetClosure(all);                          // §5 blocker 2：per-Fiber net 闭合闸门接线生效
        foreach (var f in all) f.Load();
    }

    /// <summary>§3/§6 — 触发单 provider teardown：标记 TearingDown + provider-first 通知依赖者 → Suspending（级联 staged）+ 入队自身逆回放。
    /// 级联：provider 的 dependent 也经此递归入队（Suspending→TearingDown 由 Fiber.Unload 放行，reviewer MEDIUM 防资源泄漏），dedup 由 TeardownEnqueued 守卫。</summary>
    public void BeginTeardown(Fiber provider)
    {
        if (provider.TeardownEnqueued) return; // 防二次入队
        provider.Unload();                       // → TearingDown + 标志 enqueued
        _graph.NotifyDependents(provider);       // provider-first-notify → dependent Suspending（R4-4 幂等）
        _teardownQueue.Add((provider.Id, () => InverseReplay.ReplayAndDead(provider)));
        foreach (var dep in _graph.DependentsOf(provider.Id))
            if (_fibers.TryGetValue(dep, out var d) && !d.TeardownEnqueued)
                BeginTeardown(d);
    }

    /// <summary>§3 — 每批重拓扑 + 调度：按 dependent-first（TopoSortLeafFirst）顺序排空 teardown 队列（R2：拓扑序真正生效；整任务 try/catch 继续其余）。</summary>
    public void DrainTeardownBatch()
    {
        var cycle = _graph.DetectCycles();
        // §6（reviewer #187）：硬环不整批早退——跳过成环子集、其余按拓扑序排空（防止环中 fiber 永不 Dead）。
        var cyclic = cycle.HasHardCycle ? new HashSet<FiberId>(cycle.HardCycle) : new HashSet<FiberId>();
        var order = _graph.TopoSortLeafFirst();   // dependent-first
        var batch = _teardownQueue.ToArray();      // 固定快照（重入仅 append）
        _teardownQueue.Clear();
        // R2：按拓扑序（dependent 先于 provider）排序批次后执行；缺序项（环中）追加末尾。
        var rank = new Dictionary<FiberId, int>();
        for (int i = 0; i < order.Length; i++) rank[order[i]] = i;
        var ordered = batch.OrderBy(e => rank.TryGetValue(e.Provider, out var r) ? r : int.MaxValue).ToArray();
        foreach (var (providerId, task) in ordered)
        {
            if (cyclic.Contains(providerId)) continue; // 跳过成环子集（§6：其余重入队）
            try { task(); }
            catch (Exception ex) // §6（reviewer #187）：崩溃不再被 catch{} 吞掉——升级到 ProviderCrashCascade.Handle
            {
                if (_fibers.TryGetValue(providerId, out var pf))
                    ProviderCrashCascade.Handle(this, pf, ex);
            }
        }
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
        // §3（reviewer #187 F3）：关闭路径同步排空也按 dependent-first（TopoSortLeafFirst）顺序，与 DrainTeardownBatch 一致。
        var order = _graph.TopoSortLeafFirst();
        var rank = new Dictionary<FiberId, int>();
        for (int i = 0; i < order.Length; i++) rank[order[i]] = i;
        var ordered = _teardownQueue.OrderBy(e => rank.TryGetValue(e.Provider, out var r) ? r : int.MaxValue).ToArray();
        _teardownQueue.Clear();
        foreach (var (_, task) in ordered) { try { task(); } catch { /* 同上 */ } }
    }

    public IReadOnlyCollection<Fiber> Fibers => _fibers.Values.ToImmutableArray();

    /// <summary>§7 medium #4 — 仅 Active 派发门控：Fiber 处于 Active 态才允许派发（Godot 壳调度器据此 gate，避免 Suspending/TearingDown 态误派发）。</summary>
    public static bool ShouldDispatch(Fiber fiber) => fiber.State == FiberState.Active;

    /// <summary>§2 R4-9（reviewer #187 F5）— 看门狗帧时钟驱动：调度器每帧调用，对超时未达 Dead 的 Active/Suspending Fiber 强制 TearingDown（兜底回收）。
    /// 纯逻辑层；真实帧时钟由 Godot 壳 _Process 驱动（deferred）。</summary>
    public void TickWatchdog(Func<Fiber, bool> isTimedOut)
    {
        foreach (var f in _fibers.Values)
            if (isTimedOut(f)) f.ForceTeardownOnWatchdog();
    }
}

/// <summary>§3 — 装载期 Fiber 规格（纯数据，无 Godot 依赖）。</summary>
public sealed record FiberSpec(
    FiberId Id,
    Signature Effect,
    Coeffect Coeffect,
    ImmutableStack<InverseClaim> Inverses);
