// PluginRuntime.cs — §3/§6/§7 调度器：装载/卸载/重拓扑/关闭路径守卫/级联 teardown。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3/§6 — 插件运行时调度器（TearingDown 帧安全队列 + 拓扑排序 + 关闭守卫）。
/// A3-06（生产审计批3）线程契约：本类型【非线程安全】（零锁）——Register/BeginTeardown/DrainTeardownBatch/
/// TickWatchdog 等全部调用须在宿主主线程（帧循环）内；看门狗尤其不得从工作线程调（账本 Dictionary 无同步）。</summary>
public sealed class PluginRuntime
{
    private readonly DependencyGraph _graph = new();
    private readonly Dictionary<FiberId, Fiber> _fibers = new();
    // 队列元素携带 provider FiberId，便于按拓扑序排空（R2：dependent-first）。任务返回逆回放诊断，供崩溃级联升级（reviewer #188 F2）。
    private readonly List<(FiberId Provider, Func<PartialReleaseDiagnosis> Task)> _teardownQueue = new();

    /// <summary>§6 — 依赖图（崩溃级联/拓扑排序查询用）。</summary>
    public DependencyGraph Graph => _graph;

    /// <summary>§6 — 按 Id 取 Fiber（崩溃级联通知依赖者用）。</summary>
    public bool TryGetFiber(FiberId id, out Fiber fiber) => _fibers.TryGetValue(id, out fiber!);

    /// <summary>§6 R5-7 — 关闭路径标志：关路径 RecomputeTopology 硬拒绝（非关路径延迟执行）。测试可置位以模拟关闭路径。</summary>
    public enum RuntimePhase { Running, ShuttingDown }
    public RuntimePhase Phase { get; private set; } = RuntimePhase.Running;
    public bool IsShuttingDown { get => Phase == RuntimePhase.ShuttingDown; set => Phase = value ? RuntimePhase.ShuttingDown : RuntimePhase.Running; }

    /// <summary>§6（reviewer #189 F1 / #190 F1）— 崩溃级联报告累积表（ProviderCrashCascade.Handle 每次填充一条）。用 List 累积而非覆盖，使单批多 Fiber 失败均能观测（§6 记录累积、供宿主轮询，不自动上抛/日志，不丢早期失败）。</summary>
    public ImmutableArray<CrashReport> CrashReports { get; private set; } = ImmutableArray<CrashReport>.Empty;
    /// <summary>§6（reviewer #190 F1）— 最近一次崩溃报告（CrashReports 末条）便捷访问；保留 last 语义，供宿主轮询观测（运行时无头不自动上抛/日志）。</summary>
    public CrashReport? LastCrashReport => CrashReports.IsEmpty ? null : CrashReports[^1];
    /// <summary>§3 step4（reviewer #190 F3）— 装载期检出的软环（降级 warning，不中止装载）；可观测出口，供调度器记录/上报（§10 软环不实落地问题）。运行时无头不自动上抛/日志，须由宿主轮询消费。</summary>
    public ImmutableArray<FiberId> SoftCycles { get; private set; } = ImmutableArray<FiberId>.Empty;
    /// <summary>§7（reviewer #190 F4）— provider 通知 dependent 进入 Suspending 时的钩子（Godot 壳据此禁用 ProcessMode）。集成缝合点，默认 null 无操作。</summary>
    public Action<Fiber>? OnSuspending { get; set; }

    /// <summary>§5 R5-7 / §10（reviewer #193 #6）— 永久存活 Fiber 周期快照网积累表：每 N 帧调度器喂入各 Active Fiber 的当帧 net（绝对值），运行时跨帧累积。逐 Fiber 退出判零对全程存活插件永久不触发 → 泄漏盲点；此表补足「周期快照阈值告警」。</summary>
    private ImmutableDictionary<FiberId, long> _netAccum = ImmutableDictionary<FiberId, long>.Empty;

    /// <summary>§10（reviewer #193 #6）— 累积当帧网：将调度器提供的 per-Fiber 当帧 net 累加到永久 Fiber 周期快照表（同 Fiber 跨帧相加）。每 N 帧调用一次即实现「周期快照」。</summary>
    public void AccumulateNet(IReadOnlyDictionary<FiberId, long> perFrameNet)
    {
        foreach (var (id, v) in perFrameNet)
            _netAccum = _netAccum.SetItem(id, _netAccum.GetValueOrDefault(id, 0L) + v);
    }

    /// <summary>§10（reviewer #193 #6）— 周期快照阈值告警：返回累积网绝对值超过 threshold 的【Active】Fiber（永久存活插件不退出，靠此告警泄漏盲点）。非 Active/TearingDown/Dead/Suspending 不计（已退出路径由 §6 正常回收）。空表⇒空数组。</summary>
    public ImmutableArray<FiberId> CheckPermanentFiberLeak(long threshold)
        => _fibers.Values
            .Where(f => f.State == FiberState.Active && _netAccum.TryGetValue(f.Id, out var acc) && (acc > threshold || acc < -threshold)) // 不用 Math.Abs：避免累积达 long.MinValue 时 OverflowException（reviewer #194 low 边界）
            .Select(f => f.Id)
            .ToImmutableArray();

    /// <summary>§10（reviewer #193 #6）— 清空周期快照网积累表（跨批次/场景重载复位，避免无界增长与跨批次泄漏观测）。</summary>
    public void ResetNetAccum() => _netAccum = ImmutableDictionary<FiberId, long>.Empty;

    /// <summary>§3 — 注册 Fiber（装载期）。返回 Fiber 供后续 Load/Unload。</summary>
    public Fiber Register(FiberSpec spec)
    {
        if (IsShuttingDown) throw new InvalidOperationException("关闭路径禁止新装载（级联期新装载须延迟到 provider 真正 Dead 后）");
        // §10 must-land（reviewer #190 #2）：级联进行中（有 provider 处于 TearingDown）也禁止新装载，避免挂上正在拆除的 provider。
        if (_fibers.Values.Any(f => f.State == FiberState.TearingDown))
            throw new InvalidOperationException("级联 teardown 进行中禁止新装载（provider 正在拆除，须待其 Dead 后）");
        var fiber = new Fiber(spec.Id, spec.Effect, spec.Coeffect, spec.Inverses);
        // R7-L1：重复 FiberId 抛异常——原静默覆盖使外部持有的旧 Fiber 成幽灵（State 可驱动、图中却是新实例）。
        if (!_fibers.TryAdd(fiber.Id, fiber))
            throw new InvalidOperationException($"Fiber {fiber.Id} 已注册：禁止静默覆盖（R7-L1）");
        _graph.Register(fiber);
        return fiber;
    }

    /// <summary>§3 — 批量建立显式依赖边（同 Scope Requires⊇Provides）；软边同时回填 provider.Dependents（medium #3：否则 Godot 壳 ProcessMode 级联遍历空集 no-op）。
    /// R7-M2：校验「同 Scope」前置条件（设计 §3 step1）——跨 Scope 边装载能过但 teardown 语义未定义，非法状态不可表示。</summary>
    public void AddDependency(Fiber dependent, Fiber provider, EdgeKind kind)
    {
        if (dependent.Scope != provider.Scope)
            throw new InvalidOperationException(
                $"AddDependency 前置条件违反（§3 step1）：dependent {dependent.Id} Scope({dependent.Scope}) != provider {provider.Id} Scope({provider.Scope})，跨 Scope 依赖边 teardown 语义未定义（R7-M2）");
        // R3-RT-02（三轮审计）：与 Register 同型关路径/级联期守卫——级联期把新依赖者挂上正在拆除的
        // provider，provider 排空为 Dead 后无任何路径再推进该依赖者（永久 Active 派发于已 Dead provider，
        // use-after-free 同型窗口）；Register 已有同守卫，此处补齐后门。
        if (IsShuttingDown || _fibers.Values.Any(f => f.State == FiberState.TearingDown))
            throw new InvalidOperationException(
                $"AddDependency 禁止于关闭/级联 teardown 进行中调用（provider {provider.Id} 正在拆除或全 runtime 关闭中；待其 Dead 后重建 runtime 关系）");
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
        // §3 step2（reviewer #187 F4）：逆引用自动派生软边——若 fiber 的逆释放了某 provider 提供的资源（同 Scope），则 fiber 软依赖该 provider（R4-4 幂等通知）。
        foreach (var f in all)
            foreach (var inv in f.Inverses)
            {
                if (inv.Scope != f.Scope) continue; // 软边须同 Scope（跨 Scope 不构成同图依赖）
                foreach (var other in all)
                {
                    if (other.Id == f.Id) continue;
                    // A3-03（生产审计批3）：派生须按 provider(other) Scope 过滤——跨 Scope 同名资源是合法配置
                    //（LoadValidationTests R5-6：provider 在 shell、releaser 在 scene ⇒ 不误判），不过滤会让
                    // AddSoftEdge 的 ValidateSameScope 在装载中途抛 InvalidOperationException（非 LoadValidationException 方言）且图半派生。
                    if (other.Scope != f.Scope) continue;
                    if (ResourceId.Normalize(inv.Resource) == ResourceId.Normalize(other.Coeffect.Provides))
                    {
                        _graph.AddSoftEdge(f, other);           // 软依赖：teardown 不强制顺序（降级 warning），但参与 NotifyDependents 级联
                        other.Dependents = other.Dependents.Add(f.Id); // 回填依赖者集合（与 AddDependency 一致，供 Godot 壳 ProcessMode 级联遍历）
                    }
                }
            }
        // §3 step4（reviewer #190 F3）：软环降级 warning 须可观测——在自动派生软边【之后】再检一次环，
        // 否则纯由逆引用派生的软环（如 a⇄b）不会被记录（仅记录显式软边环）。不中止装载。
        SoftCycles = _graph.DetectCycles().SoftCycle;
        foreach (var f in all) f.Load();
    }

    /// <summary>§3/§6 — 触发单 provider teardown：标记 TearingDown + provider-first 通知依赖者 → Suspending（级联 staged）+ 入队自身逆回放。
    /// 级联：provider 的 dependent 也经此递归入队（Suspending→TearingDown 由 Fiber.Unload 放行，reviewer MEDIUM 防资源泄漏），dedup 由 TeardownEnqueued 守卫。</summary>
    public void BeginTeardown(Fiber provider)
    {
        if (provider.TeardownEnqueued || provider.State == FiberState.Dead) return; // 防二次入队 / 已终结
        provider.Unload();
        // A3-02（生产审计批3）：Inactive fiber 经 Unload 直达 Dead（D4：未 _Ready 也安全）——
        // 无已获取资源，不得入队逆回放（release-without-acquire = 对未拥有句柄的双重释放类崩溃）。
        if (provider.State != FiberState.TearingDown) return;
        provider.TeardownEnqueued = true; // A3-01：标志位=「任务真已入队」，由本方法在入队前置位（Fiber.Unload 不再代置）
        _graph.NotifyDependents(provider);       // provider-first-notify → dependent Suspending（R4-4 幂等）
        _teardownQueue.Add((provider.Id, () => InverseReplay.ReplayAndDead(provider))); // 返回诊断 ⇒ DrainTeardownBatch 按 AllCompleted 升级
        foreach (var dep in _graph.DependentsOf(provider.Id))
        {
            if (_fibers.TryGetValue(dep, out var d) && !d.TeardownEnqueued)
            {
                // §7（reviewer #190 F4 / #191 F4）：dependent 进入 Suspending ⇒ Godot 壳禁用 ProcessMode（集成缝合）；钩子异常须隔离，避免中断后续依赖者级联（与 teardown 任务 try/catch 一致）。
                try { OnSuspending?.Invoke(d); } catch { /* 钩子异常隔离：不阻断级联 */ }
                BeginTeardown(d);
            }
        }
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
            // R3-RT-01b（三轮审计）：环内子集无有效拓扑序（dependent-first 不可满足）——按入队序兜底回放
            //（回放 per-fiber 独立，与旧自愈内联等价）并保留崩溃报告可观测。原「跳过+另行回收」在自愈改
            // 入队后永不收敛（重入队→再跳过），救援路径断裂（reviewer #187 跳过语义 + A3-01b 自愈的组合修复）。
            if (cyclic.Contains(providerId))
                CrashReports = CrashReports.Add(new CrashReport(providerId, new InvalidOperationException($"动态硬环子集无有效拓扑序，已按入队序兜底回放（环 {string.Join(" -> ", cycle.HardCycle)}）——硬环本身须宿主修复（LoadAll 拒载静态硬环）"), cycle.HardCycle));
            // REG-01（复审计）：陈旧任务防御——排空中同批兄弟的逆 Action 重入 TickWatchdog 会把
            // 尚未回放的 batch-mate 二次入队；其陈旧任务随后由外层快照执行至 Dead。下一批若再执行
            // 会对 Dead fiber 回放抛异常 ⇒ 假 CrashReport 污染 §6 诊断 + 冗余 OnSuspending。丢弃之。
            if (_fibers.TryGetValue(providerId, out var st) && st.State == FiberState.Dead) continue;
            try
            {
                var diag = task(); // 逆回放（R4-6 部分释放诊断）
                // §6（reviewer #188 F2）：回放部分失败（AllCompleted=false）即升级崩溃级联——不再被 ReplayAndDead 静默 MarkDead 掩盖。
                if (!diag.AllCompleted && _fibers.TryGetValue(providerId, out var pf))
                    // A3-08（生产审计批3）：原始逆异常作 InnerException 传递——根因类型/堆栈不得在重新合成时丢失。
                    CrashReports = CrashReports.Add(ProviderCrashCascade.Handle(this, pf, new InvalidOperationException($"部分逆释放失败（位置 {diag.FailedIndex}，未释放 {diag.Pending.Length} 项：{string.Join(", ", diag.Pending)}）", diag.FirstError)));
            }
            catch (Exception ex) // 整任务抛异常（R4-6 外层）→ 升级到 ProviderCrashCascade.Handle（§6 reviewer #187）
            {
                if (_fibers.TryGetValue(providerId, out var pf))
                    CrashReports = CrashReports.Add(ProviderCrashCascade.Handle(this, pf, ex));
            }
        }
    }

    /// <summary>§3 R5-7 / §8（reviewer #191 F4）— 重拓扑（N2 守卫：关路径硬拒绝，非关路径延迟执行）。
    /// 注意：非关路径分支当前为 no-op 占位（设计 §8 明确为 deferred gap——动态拓扑重算不在 MVP 运行时范围；环检测由 DrainTeardownBatch 在每批重排时触发）。方法保留为生命周期内拓扑变更接缝，真实重算须由宿主在 DependencyGraph 调用方实现。</summary>
    public void RecomputeTopology()
    {
        if (IsShuttingDown) throw new InvalidOperationException("关闭路径禁止 RecomputeTopology（资源已不可靠）");
        // 非关路径：no-op 占位（见 summary，§8 deferred）。
    }

    /// <summary>§3 — 关闭路径同步排空（R4-1）：在调度器 _ExitTree 内调用，按 dependent-first 顺序释放全部 TearingDown。
    /// A3-10（生产审计批3，规格 §3 step8）：退出时对全部存活 fiber 补「标记+入队」——宿主漏调 BeginTeardown 时
    /// 队列为空 ⇒ 逆回放整批不执行且无诊断（假绿式退出）；Inactive fiber 跳过（D4，无资源可回放）。</summary>
    public void SynchronousExitDrain()
    {
        IsShuttingDown = true;
        // A3-10：对仍 Active/Suspending（未标记）与 TearingDown 但任务不在队列（旁路 Unload / 上一批已清队）的 fiber 补入队。
        foreach (var f in _fibers.Values)
        {
            bool inQueue = _teardownQueue.Any(t => t.Provider == f.Id);
            if (inQueue) continue;
            if (f.State is FiberState.Active or FiberState.Suspending)
            {
                f.Unload();
                f.TeardownEnqueued = true;
                _teardownQueue.Add((f.Id, () => InverseReplay.ReplayAndDead(f)));
            }
            else if (f.State == FiberState.TearingDown)
            {
                f.TeardownEnqueued = true;
                _teardownQueue.Add((f.Id, () => InverseReplay.ReplayAndDead(f)));
            }
            // Inactive：D4——未装载无资源，跳过（不入队不标记）；Dead：已终结。
        }
        // §3（reviewer #187 F3）：关闭路径同步排空也按 dependent-first（TopoSortLeafFirst）顺序，与 DrainTeardownBatch 一致。
        var order = _graph.TopoSortLeafFirst();
        var rank = new Dictionary<FiberId, int>();
        for (int i = 0; i < order.Length; i++) rank[order[i]] = i;
        var ordered = _teardownQueue.OrderBy(e => rank.TryGetValue(e.Provider, out var r) ? r : int.MaxValue).ToArray();
        _teardownQueue.Clear();
        // §3（reviewer #188 F5）：关闭路径崩溃也升级到 ProviderCrashCascade.Handle，不再静默 catch{} 吞掉（与 DrainTeardownBatch 一致）。
        foreach (var (providerId, task) in ordered)
        {
            // REG-01（复审计）：同 DrainTeardownBatch 的陈旧任务防御。
            if (_fibers.TryGetValue(providerId, out var st) && st.State == FiberState.Dead) continue;
            try
            {
                var diag = task();
                // §6（reviewer #189 F1）：退出路径部分失败也须上抛升级（与 DrainTeardownBatch 一致），并存 LastCrashReport 供调度器观测。
                if (!diag.AllCompleted && _fibers.TryGetValue(providerId, out var pf))
                    // A3-08：退出路径同样保留原始异常（与 DrainTeardownBatch 对称）。
                    CrashReports = CrashReports.Add(ProviderCrashCascade.Handle(this, pf, new InvalidOperationException($"退出路径部分逆释放失败（位置 {diag.FailedIndex}，未释放 {diag.Pending.Length} 项：{string.Join(", ", diag.Pending)}）", diag.FirstError)));
            }
            catch (Exception ex)
            {
                if (_fibers.TryGetValue(providerId, out var pf))
                    CrashReports = CrashReports.Add(ProviderCrashCascade.Handle(this, pf, ex));
            }
        }
        IsShuttingDown = false; // §3（reviewer #191 F5）：关路径标志仅限本次排空，复位为防御性默认（README 诚实边界 10：实例仍为单场景生命周期——场景重载请新建 PluginRuntime，勿复用）
    }

    /// <summary>§6/§7（reviewer #191 F3 / #194 LOW）— 清空累积诊断（CrashReports/SoftCycles/_netAccum 永久 Fiber 周期快照表）。跨批次/场景重载时由宿主定期调用，避免无界增长与跨批次泄漏观测；一并 ResetNetAccum 防止旧 FiberId 在永久 Fiber 表中残留。</summary>
    public void ResetDiagnostics()
    {
        CrashReports = ImmutableArray<CrashReport>.Empty;
        SoftCycles = ImmutableArray<FiberId>.Empty;
        _netAccum = ImmutableDictionary<FiberId, long>.Empty;
    }

    /// <summary>§7（reviewer #191 F1/F2）— 接线 Godot 壳：provider 通知 dependent 进入 Suspending 时驱动壳禁用 ProcessMode 级联；关闭路径排空时驱动壳 flush 退出 drain。打通 §7 ProcessMode 级联（此前 OnSuspending/ExitDrain 为孤岛）。
    /// A3-13（生产审计批3）：幂等守卫——同一 shell 重复接线 no-op（热重载/重绑定场景 drain 会双入队），跨实例抛。</summary>
    public void AttachShell(GodotShell shell)
    {
        if (ReferenceEquals(_attachedShell, shell)) return;
        if (_attachedShell is not null)
            throw new InvalidOperationException("PluginRuntime 已接线另一 GodotShell：跨实例接线须先解绑或新建运行时（重复接线会双入队退出 drain 并静默覆盖 OnSuspending）");
        _attachedShell = shell;
        OnSuspending = shell.CascadeProcessModeDisabled; // dependent Suspending ⇒ 壳禁用其子树派发（级联）
        shell.EnqueueExitDrain(SynchronousExitDrain);   // 关闭路径由壳 _ExitTree 触发运行时同步排空
    }
    private GodotShell? _attachedShell;

    public IReadOnlyCollection<Fiber> Fibers => _fibers.Values.ToImmutableArray();

    /// <summary>§7 medium #4 — 仅 Active 派发门控：Fiber 处于 Active 态才允许派发（Godot 壳调度器据此 gate，避免 Suspending/TearingDown 态误派发）。</summary>
    public static bool ShouldDispatch(Fiber fiber) => fiber.State == FiberState.Active;

    /// <summary>§2 R4-9（reviewer #187 F5 / #188 F-Tick / #189 F2）— 看门狗帧时钟驱动：调度器每帧调用，对超时未达 Dead 的 Active/Suspending Fiber 强制 TearingDown 并【入队逆回放任务】+（若为 provider）通知依赖者并递归 BeginTeardown（兜底回收须真正执行 InverseReplay.ReplayAndDead 且级联，否则依赖者仍 Active 派发/永不回收）。
    /// 纯逻辑层；真实帧时钟由 Godot 壳 _Process 驱动（deferred）。</summary>
    public void TickWatchdog(Func<Fiber, bool> isTimedOut)
    {
        foreach (var f in _fibers.Values)
        {
            // §6（reviewer #190 #1）：仅当 f 仍处 Active/Suspending（本帧发生转移）才强制+入队+级联——已 Dead/TearingDown 的 f 不重复入队（避免二次回放抛异常误填 LastCrashReport）。
            if (isTimedOut(f) && (f.State == FiberState.Active || f.State == FiberState.Suspending))
            {
                f.ForceTeardownOnWatchdog();                         // Active/Suspending → TearingDown
                f.TeardownEnqueued = true;                           // A3-01：与 BeginTeardown 同契约——入队前置位
                _teardownQueue.Add((f.Id, () => InverseReplay.ReplayAndDead(f))); // 入队逆回放 ⇒ DrainTeardownBatch 真正回收资源
                // §6（reviewer #189 F2）：超时 provider 须级联依赖者——否则依赖者仍 Active 派发且永不 teardown（use-after-free/泄漏）。
                _graph.NotifyDependents(f);
                foreach (var dep in _graph.DependentsOf(f.Id))
                {
                    // §7（reviewer #191 low）：OnSuspending 与 BeginTeardown 同用 !TeardownEnqueued 去重守卫（与 BeginTeardown 对称），避免对同一 dependent 重复触发 Suspending 通知。
                    if (_fibers.TryGetValue(dep, out var d) && !d.TeardownEnqueued)
                    {
                        try { OnSuspending?.Invoke(d); } catch { /* §191 F4 钩子异常隔离 */ } // §7 钩子：dependent 进入 Suspending ⇒ Godot 壳禁用 ProcessMode
                        BeginTeardown(d);
                    }
                }
            }
            // A3-01b/A3-04（生产审计批3）：看门狗自愈分支——TearingDown 但任务【不在队列】的 fiber（旁路 fiber.Unload()、
            // 动态硬环子集被跳过后承诺"看门狗另行回收"却无路径）：入队逆回放任务，消灭永久滞留 + Register 永久锁死。
            // 已在队列中的 TearingDown 不受影响（原防重语义保留）。
            // R3-RT-01（三轮审计）：与主路径同构【入队 → DrainTeardownBatch 按 dependent-first 拓扑排空】——
            // 原内联先回放 provider，后级联依赖者（其回放下次排空才执行），破坏回收序：依赖者逆声明若释放
            // provider 所供资源（跨 Fiber 借用），将在资源被 provider 释放后才执行（use-after-free 同型回收序变体）。
            // R3-RT-04：ReplayInProgress 条件排除「正在回放」的 fiber——排空中队列已清空，仅凭不在队列判定
            // 会让逆 Action 重入 TickWatchdog 时二次入队/二次回放。
            else if (isTimedOut(f) && f.State == FiberState.TearingDown
                     && !f.ReplayInProgress
                     && !_teardownQueue.Any(t => t.Provider == f.Id))
            {
                f.TeardownEnqueued = true;
                _teardownQueue.Add((f.Id, () => InverseReplay.ReplayAndDead(f))); // 异常/部分失败由 DrainTeardownBatch 升级崩溃级联（与第一分支同契约）
                // R2A-02（二轮审计）：自愈须与第一分支同型级联——旁路 Unload 路径下依赖者从未收 Suspending 通知，
                // provider 自愈 Dead 后依赖者仍 Active 派发（use-after-free 同型窗口，第一分支注释同源）。
                // 硬环跳过路径的依赖者早经 BeginTeardown 级联过，此处幂等（!TeardownEnqueued 守卫去重）。
                _graph.NotifyDependents(f);
                foreach (var dep in _graph.DependentsOf(f.Id))
                    if (_fibers.TryGetValue(dep, out var d) && !d.TeardownEnqueued)
                    {
                        try { OnSuspending?.Invoke(d); } catch { /* 钩子异常隔离（与第一分支一致） */ }
                        BeginTeardown(d);
                    }
            }
        }
    }
}

/// <summary>§3 — 装载期 Fiber 规格（纯数据，无 Godot 依赖）。</summary>
public sealed record FiberSpec(
    FiberId Id,
    Signature Effect,
    Coeffect Coeffect,
    ImmutableStack<InverseClaim> Inverses);
