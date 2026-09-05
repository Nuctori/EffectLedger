// EffectScript.cs — 视觉效应代数剧本（L1 增量，零 Godot 依赖）。EFFECT_SCRIPT.md §2/§3。
// LANDING_PLAN 角色：在已建 L1（§3.1–§3.3）之上加「时间轴」语义，使「视觉代数剧本」可静态验证。
// 数学全部复用既有 L1（Interval/Signature/NetTable/Peak/Compatible/Combination/LoopCount），零新增代数结构。
// 类型即边界：所有载体为 readonly record struct，构造即全必填；注释承载 ω 语义与端点采样定理（§3）。
//
// 性能（Jeff Dean 视角，audit iter-effect26.md）：朴素 Audit 每采样点全量重建 At/CumulativeNet 且 gate(3) 两两配对 ⇒
// 最坏 O(S·E²)（S=采样点, E=事件）；对「数千粒子同屏同资源」的 AI 视觉脚本尾延迟与内存平方恶化。
// 本实现改用「扫换线（sweep-line）」：端点排序一次 O(E log E)，沿时间轴增量维护活动集与运行计数，
// 每个事件只在「进入(Lo)」/「退出(Hi)」各处理一次 ⇒ 总复杂度 O(E·K·log E)，内存 O(E·K)。
// R4-JD-05 前提修正：O(E·K·log E) 以「互异 (资源,scope) 组数 D 有界」为前提——gate(1)/(3) 每采样点
// 全量扫 net/grp 字典（O(S·D) 乘子），逐事件独立 scope/resource 的脚本 D=Θ(E) 时整体超线性
//（实测 4 倍数据 ≈6x，见 ProdAuditR4AuditScaleTests 曲线钉与 README 诚实边界 16）。
// 数学上与端点采样定理等价（At 分段常数、仅各有限端点跳变；各 gate 输出集合与逐点全算版本一致）。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
namespace Cosmos.EffectAlgebra;

/// <summary>
/// §2.1 / EFFECT_SCRIPT.md §2 — 一个视觉元素（图层/sprite/粒子）的生命周期。
/// 视觉元素在代数上是原子的 ⇒ 不拆成 N 个 TimedClaim，而是把 Claim 分组进一个 Event。
/// 类型强制（用户铁律）：4 字段位置记录 ⇒ 构造即全必填；Lifetime/Scope/Footprint/Loop 的数学边界由既有类型承载。
/// 修正 auditA OPEN-1：Event 自身携带 <see cref="Scope"/>，作为 At/Net/Peak 的 scope 来源（不再有自由变量 loopScope）。
/// </summary>
public readonly record struct EffectEvent
{
    /// <summary>§3.1.5 — 存在时间窗 [lo, hi]；hi=⊤ ⇒ 上界开放（time-⊤ 永占；常驻豁免的依据是 loop:⊤ 而非本字段，见 EFFECT_SCRIPT.md §2.1 P0-A1 注记）。</summary>
    public Interval Lifetime { get; }

    /// <summary>§3.1.3b — 该元素的作用域（Scene/Type/Method…）；At/Net/Peak 的 scope 来源（修 OPEN-1）。</summary>
    public ScopeId Scope { get; }

    /// <summary>§3.1.4b — 该元素存活期内的资源签名（三桶：read/write/occupy）。</summary>
    public Signature Footprint { get; }

    /// <summary>§3.2.5 — 并发实例数 ω∈ℕ∪{⊤}。ω 表示「同一时刻有多少个该元素并发存在」（并发副本，非时间重复）；
    /// 默认单实例（<see cref="LoopCount"/> 的 1 值）。ω=⊤ ⇒ 上界开放（常驻）。</summary>
    public LoopCount Loop { get; }

    /// <summary>§2.1 — 全字段构造（含 ω）。
    /// P0-5（hickey-x3 核实矩阵#5 / 探针 F）：拒绝 Lo=⊤ 寿命——Lo=⊤ 的事件永不存活，
    /// 会让 create-without-release 泄漏剧本在端点采样下静默全绿（假绿）。非法输入在构造期即不可表达。</summary>
    public EffectEvent(Interval lifetime, ScopeId scope, Signature footprint, LoopCount loop)
    {
        if (lifetime.Lo.IsTop)
            throw new ArgumentException("EffectEvent lifetime 下界不可为 ⊤（[⊤,⊤] 非法：事件永不存活会掩盖泄漏审计）");
        // rich-hickey2 R1-F3：封 default(LoopCount) 后门——struct default 绕过 LoopCount.Of 的 ≥1 校验，
        // 会让 Audit 除法 DivideByZero / 闭包路径规模缩放为 [0,0] 致 Leak 误报。构造期拒绝，一处收口覆盖全部消费路径（R5 V5-002：以 IsValid 派生替散落判定）。
        if (!loop.IsValid)
            throw new ArgumentException("EffectEvent loop 须 ≥1 或 ⊤（default(LoopCount) 非法；用 LoopCount.Of(n≥1) 或 LoopCount.Top）", nameof(loop));
        // A1-02①（生产审计批1）：Footprint/Scope 为引用类型字段，显式传 null 能过编译——
        // 延后到 Audit 才 NRE 会把错误源头藏进调用栈深处，构造期即拒绝。
        if (footprint is null) throw new ArgumentNullException(nameof(footprint));
        if (scope is null) throw new ArgumentNullException(nameof(scope));
        Lifetime = lifetime;
        Scope = scope;
        Footprint = footprint;
        Loop = loop;
    }

    /// <summary>§2.1 — 默认 ω=1 的便捷构造（单实例元素）。</summary>
    public EffectEvent(Interval lifetime, ScopeId scope, Signature footprint)
        : this(lifetime, scope, footprint, LoopCount.Of(1)) { }
}

/// <summary>
/// §2.2 / EFFECT_SCRIPT.md §2 — 视觉剧本 = 有限个 <see cref="EffectEvent"/> 的集合。
/// 剧本是纯数据契约（AI 产出 JSON，不经 §7 白名单、不经 Godot 运行期）；喂 L1 验证即可在「没渲染、没跑游戏」前审计。
/// </summary>
public sealed partial class EffectScript
{
    /// <summary>§2.2 — 剧本内所有视觉事件（有限集）。</summary>
    public ImmutableArray<EffectEvent> Events { get; }

    /// <summary>§2.3 — 剧本级预算（默认无上限）。构造即固定，使 EffectScript 为不可变值对象（修 auditR5 F1：原 { get; init; } 可被改写 ⇒ 同实例 Audit 结果依赖可变状态）。</summary>
    public Budget Budget { get; }

    /// <summary>§2.2 — 从事件集构造剧本（预算默认无上限）。</summary>
    public EffectScript(ImmutableArray<EffectEvent> events, Budget budget = default)
    {
        Events = events;
        // R4-RH-14：Caps getter 单点归一后 budget.Caps 恒非 null，原死三分支已简化。
        Budget = budget;
    }

    /// <summary>§2.2 — 从事件集构造剧本（IEnumerable 便捷）。</summary>
    public EffectScript(IEnumerable<EffectEvent> events, Budget budget = default)
        : this(events.ToImmutableArray(), budget) { }

    // A1-02②（生产审计批1）：default(EffectEvent) 经 ImmutableArray.Create(default, ...) 等惯用法绕过构造期守卫
    // （readonly record struct 的 default 不调用构造子），非法事件延后消费会变成 NRE（Footprint null）或
    // 静默 [0,0] 缩放吞掉 Leak（假绿）。消费侧 loud 复查：At/Audit 入口统一拦截，带 events[i] 定位。
    static void ValidateEvent(in EffectEvent e, int index)
    {
        if (e.Footprint is null || e.Scope is null || !e.Loop.IsValid || e.Lifetime.Lo.IsTop)
            throw new ArgumentException(
                $"events[{index}] 含非法字段（default(EffectEvent) 或旁路构造？）：Footprint/Scope 不可为 null，Loop 须 ≥1 或 ⊤，Lifetime.Lo 不可为 ⊤",
                "events");
    }

    /// <summary>
    /// §2.2 / §3 — At(t)：t 时刻屏幕总签名 = 所有 Lifetime∋t 的 Event 各取
    /// <see cref="Combination.Loop(Footprint, Loop, Scope)"/>（§3.2.5，loopScope=Event.Scope，修 OPEN-1）后 Union（§3.2.1 半格并）。
    /// 瞬时快照：存活元素各贡献 ω 份并发副本（ω 解释见 EffectEvent.Loop），不重算代数。
    /// 确定性（auditA 焦点6）：Signature 以 ImmutableHashSet 存 Claims，并集无序可交换 ⇒ 同剧本同 t ⇒ 同内容签名。
    /// <see cref="Lifetime"/>∋t ⇔ Lo≤t ∧ (Hi=⊤ ∨ t≤Hi)。
    /// </summary>
    public Signature At(NatStar t)
    {
        for (int i = 0; i < Events.Length; i++) ValidateEvent(Events[i], i);
        var acc = Signature.Empty;
        foreach (var e in Events)
        {
            if (Alive(e.Lifetime, t))
                acc = Signature.Union(acc, Combination.Loop(e.Footprint, e.Loop, e.Scope));
        }
        return acc;
    }
    /// <summary>§3 / PR1 — 纯函数：从事件集计算采样点与闭包时刻（可独立测试）。与 Audit 内联逻辑等价（含 R2-003 幽灵点规则）。</summary>
    public static (IReadOnlyList<NatStar> SamplePoints, NatStar ClosureT) ComputeSamplePoints(ImmutableArray<EffectEvent> events)
    {
        // R2A-08（二轮审计）：本方法是 At/Audit 之外的第三个公开消费入口——同源 loud 守卫，
        // 防 default(EffectEvent) 静默产出 [0] 采样点（独立测试替身等价性可被非法输入掩盖）。
        for (int i = 0; i < events.Length; i++) ValidateEvent(events[i], i);
        var endpoints = new SortedSet<ulong>();
        ulong maxFinite = 0;
        bool anyFinite = false;
        bool anyOpenEnd = false;
        for (int i = 0; i < events.Length; i++)
        {
            var lt = events[i].Lifetime;
            if (!lt.Lo.IsTop) { endpoints.Add(lt.Lo.Value); maxFinite = Math.Max(maxFinite, lt.Lo.Value); anyFinite = true; }
            if (!lt.Hi.IsTop) { endpoints.Add(lt.Hi.Value); maxFinite = Math.Max(maxFinite, lt.Hi.Value); anyFinite = true; }
            else anyOpenEnd = true;
        }
        var samplePoints = new List<NatStar>();
        foreach (var v in endpoints) samplePoints.Add(NatStar.Of(v));
        if (anyOpenEnd && maxFinite != ulong.MaxValue) samplePoints.Add(NatStar.Of(maxFinite + 1));
        if (samplePoints.Count == 0) samplePoints.Add(NatStar.Of(0));
        var closureT = anyFinite ? NatStar.Of(maxFinite) : NatStar.Of(0);
        return (samplePoints, closureT);
    }

        /// <summary>**采样点审计**（rich-hickey2 R5 V5-003）——端点 ∪ 尾段代表点采样≠全连续区间；采样点之间守恒由闭包路径补（lo≤closureT），已对齐 EFFECT_SCRIPT.md §「已知锐边」。与 At(t) 共享采样点。
    /// §3 / EFFECT_SCRIPT.md §3 — 扫换线审计（数学等价于端点采样审计，Jeff Dean 性能审计 iter-effect26.md）。
    /// At(t) 是分段常数函数，仅在各 Lifetime 的有限 Lo/Hi 端点跳变；故对全部有限端点采样 = 全量检查。
    /// 三道 gate（修 auditA OPEN-2/OPEN-3/OPEN-4/OPEN-5），与逐点全算版本逐条 Violation 集合一致：
    ///   (1) 守恒：累积净 net C(t)=Σ_{e.Lo≤t, 有限ω} Σ_{occupy c} sign(c.Mode)·scaleSize(c.Size, e.Loop)（create/move=+，release=−）。
    ///       要求全采样点 C.Hi≥0（无负陷 NegativeDip）；在「全部有限事件结束后」C 区间含 0（生命周期闭合，否则 Leak）。
    ///       居民层豁免（修 OPEN-4）：资源全部正向贡献仅来自 ω=⊤ 事件 ⇒ 免守恒检查（仍参与 Peak）。
    ///   (2) 峰值预算（修 OPEN-4b/OPEN-5）：At(t) 内每资源并发占用峰值 ≤ Budget.Caps[r]；资源不在 Caps ⇒ 不检查。
    ///   (3) 兼容（修 OPEN-3/OPEN-5）：同时存活、按（归一化 ResourceId, ScopeId）分组的 occupy Claims 两两 Compatible.IsCompatible；
    ///       单事件内 ω 份并发副本属同一逻辑元素自配对不报冲突（避免 false-positive）。
    /// 扫换线（§3 / EFFECT_SCRIPT.md §3）：沿时间轴增量维护①运行中累积 net（仅 enter 累加，release 自带负向 Lo）②每资源运行中峰值（含 ⊤ 计数）
    /// ③每 (resource,scope,mode) 的活跃事件集合（O(1) 判冲突，等价于两两枚举）。每事件进入/退出各处理一次 ⇒ O(E·K·log E)。</summary>
    public AuditResult Audit(Budget cap)
    {
        var violations = new List<Violation>();

        // A1-02②（生产审计批1）：消费侧 loud 复查（default 旁路守卫，与 At 同源）。
        for (int i = 0; i < Events.Length; i++) ValidateEvent(Events[i], i);

        // R10-F1：default(Budget).Caps == null（struct 默认值绕过构造函数归一）⇒ 归一为无上限，不 NRE。
        // R4-RH-14：cap.Caps 不可能为 null（Budget.Caps getter 单点归一），原死防御已删。

        // PR1：采样点与闭包时刻由纯函数统一计算（可独立测试，含 R2-003 幽灵点规则）。
        var (samplePoints, closureT) = ComputeSamplePoints(Events);

        // 扫换线事件：enter@Lo / exit@Hi。Lo=⊤ 的事件永不存活（Alive: ⊤>有限t 恒假）⇒ 跳过。
        var sweep = new List<(ulong time, int ei, bool enter)>();
        for (int ei = 0; ei < Events.Length; ei++)
        {
            var lt = Events[ei].Lifetime;
            if (lt.Lo.IsTop) continue;
            var lo = lt.Lo.Value;
            var hi = lt.Hi.IsTop ? ulong.MaxValue : lt.Hi.Value;
            sweep.Add((lo, ei, true));
            sweep.Add((hi, ei, false));
        }
        // 时间升序；同时间 enter 先于 exit（与相位拆分一致；相位本身已分离 enter/exit）。
        sweep.Sort((a, b) =>
            a.time != b.time ? a.time.CompareTo(b.time)
            : (a.enter ? 0 : 1).CompareTo(b.enter ? 0 : 1));

        // 运行态（引用类型，局部函数捕获）。
        var net = new Dictionary<ResourceId, SignedInterval>();                 // gate(1) 累积 net（仅 enter 累加）
        var peakSum = new Dictionary<ResourceId, NatStar>();                    // gate(2) 有限峰值和（不含 ⊤ 声明）
        var topCount = new Dictionary<ResourceId, int>();                       // gate(2) 活跃 ⊤ 声明计数（>0 ⇒ 该资源峰值 ⊤）
        var grp = new Dictionary<(ResourceId, ScopeId, int), HashSet<int>>();   // gate(3) 每 (res,scope,mode) 的活跃事件集合
        var netScope = new Dictionary<ResourceId, (ScopeId scope, int ei)>();   // gate(1) 资源→首个贡献者 (scope,ei)（用于 Violation 归因）
        var peakScope = new Dictionary<ResourceId, (ScopeId scope, int ei)>();  // gate(2) 资源→首个峰值贡献者 (scope,ei)
        var peakReported = new HashSet<ResourceId>();                           // rich-hickey2 R1：PeakExceeded 每（归一化）资源只报首个反例——同一违例逐采样点重复上报是时间序列不是问题集
        var peakActive = new Dictionary<ResourceId, int>();                     // R2A-01（二轮审计）：峰值相关的活跃 occupy 非 release 计数——gate(3) 全桶化后 grp 含 read/write 组员，
                                                                                // 若用 grp 判活跃会让 read claim 跨生命周期时清理被跳过 ⇒ peakScope 陈旧归因（S06-004 回归）
        ScopeId ResolveNetScope(ResourceId r) => netScope.TryGetValue(r, out var s) ? s.scope : new ScopeId.Global();
        int ResolveNetEi(ResourceId r) => netScope.TryGetValue(r, out var s) ? s.ei : -1;
        ScopeId ResolvePeakScope(ResourceId r) => peakScope.TryGetValue(r, out var s) ? s.scope : new ScopeId.Global();
        int ResolvePeakEi(ResourceId r) => peakScope.TryGetValue(r, out var s) ? s.ei : -1;

        void Step(int ei, bool enter)
        {
            var e = Events[ei];
            // gate(1) net：仅 enter 且有限 ω（居民层 ω=⊤ 豁免守恒闭包，修 OPEN-4）。
            // release 自带负向贡献于其自身 Lo（CumulativeNet 仅按 Lo 累加），故 exit 不改 net。
            if (enter && !e.Loop.Count.IsTop)
            {
                foreach (var c in e.Footprint.OccupyClaims)
                {
                    var r = ResourceId.Normalize(c.Resource);
                    var scaled = ScaleSize(c.Size ?? Interval.Default, e.Loop.Count);
                    var contrib = c.Mode == Mode.Release
                        ? new SignedInterval(Negate(scaled.Hi), Negate(scaled.Lo))
                        : new SignedInterval(ToZ(scaled.Lo), ToZ(scaled.Hi));
                    if (!netScope.ContainsKey(r)) netScope[r] = (e.Scope, ei);
                    net[r] = net.TryGetValue(r, out var cur) ? cur.Add(contrib) : contrib;
                }
            }
            // gate(3) grp 先于 gate(2) peak 处理（A1-04，生产审计批1）：exit 时 peakScope 清理（S06-004）要读
            // hasActiveGrp——grp 必须先移除本事件，否则陈旧组员让清理被跳过、峰值归因退回旧 scope（Round6 回归）。
            // 全桶参与（A1-04）——JSON 契约公开表达 kind=write + mode=create/release，只扫 occupy 桶会让
            // write create×create / release×release 冲突静默放行（假绿，§3.2.3 CONFLICT 应报）。
            // read 桶经 Claim.Normalize 仅存 Use/Unknown，二者自兼容/配对豁免（§3.2.3 P4），进组不产生冲突，无回归面。
            foreach (var c in e.Footprint.AllClaims())
            {
                var r = ResourceId.Normalize(c.Resource);
                // OPEN-1 修（auditR3b TC7）：gate(3) 冲突分组按事件 scope(e.Scope)，与 At/ReferenceAudit 的 Combination.Loop 投影一致；
                // 此前用 claim 自带 c.Scope 与 At 视角 scope 分裂，导致同一剧本两视角冲突归因错位。测试 builder 恒 c.Scope==e.Scope，无回归。
                var key = (r, e.Scope, (int)c.Mode);
                if (enter)
                {
                    if (!grp.TryGetValue(key, out var hs)) grp[key] = hs = new HashSet<int>();
                    hs.Add(ei);
                }
                else
                {
                    if (grp.TryGetValue(key, out var hs)) { hs.Remove(ei); if (hs.Count == 0) grp.Remove(key); }
                }
            }
            // gate(2) peak：仅 occupy 桶（量纲隔离 DO-7；与 Peak.Compute/R2 单桶等价钉同口径）。
            foreach (var c in e.Footprint.OccupyClaims)
            {
                var r = ResourceId.Normalize(c.Resource);
                if (c.Mode == Mode.Release) continue; // §3.3.2 release 不贡献峰值
                if (enter)
                {
                    if (!peakScope.ContainsKey(r)) peakScope[r] = (e.Scope, ei);
                    peakActive[r] = peakActive.GetValueOrDefault(r) + 1; // R2A-01：峰值活跃计数（与 grp 解耦）
                    bool top = (c.Size ?? Interval.Default).Hi.IsTop || e.Loop.Count.IsTop;
                    if (top) topCount[r] = topCount.GetValueOrDefault(r) + 1;
                    else
                    {
                        var hi = (c.Size ?? Interval.Default).Hi;
                        var w = e.Loop.Count;
                        // rich-hickey2 R2-001：弃 ulong.MaxValue 哨兵（合法峰值 Max 被碰撞误判 ⊤，假阳性）——
                        // NatStar 算术自带「环绕 ⇒ 保守 ⊤」，哨兵不再藏进值域。
                        peakSum[r] = peakSum.GetValueOrDefault(r, NatStar.Of(0)) + hi * w;
                    }
                }
                else
                {
                    bool top = (c.Size ?? Interval.Default).Hi.IsTop || e.Loop.Count.IsTop;
                    if (top) { if (topCount.TryGetValue(r, out var tc) && tc > 0) topCount[r] = tc - 1; }
                    else
                    {
                        var cur = peakSum.GetValueOrDefault(r, NatStar.Of(0));
                        // rich-hickey2 R2-001：与 enter 路径同型——NatStar 乘法环绕⇒⊤，弃哨兵；此分支两端恒有限。
                        var hi = (c.Size ?? Interval.Default).Hi;
                        var w = e.Loop.Count;
                        var prod = hi * w;
                        peakSum[r] = (prod.IsTop || cur.IsTop) ? NatStar.Top
                            : prod.Value <= cur.Value ? NatStar.Of(cur.Value - prod.Value) : NatStar.Of(0);
                    }
                    // rich-hickey2 R6 S06-004：exit 后若该资源无活跃贡献者（无 peakSum 也不在 grp/存活峰值集），
                    // 则清理陈旧的 peakScope——下一采样点若再触发峰值，其归因 scope 需取新存活者而非首个历史者。
                    // 判定：当前既无 top 也无 peakSum 计数>0 且 grp 中无该资源的活跃条目 ⇒ 可视为"当前无活跃峰值贡献者"
                    if (peakActive.TryGetValue(r, out var pa) && pa > 0) peakActive[r] = pa - 1; // R2A-01：对称递减
                    bool hasActivePeak = (topCount.TryGetValue(r, out var tc2) && tc2 > 0)
                        || (peakSum.TryGetValue(r, out var ps) && !ps.Equals(NatStar.Of(0)));
                    bool hasPeakContributor = peakActive.TryGetValue(r, out var pa2) && pa2 > 0; // R2A-01：仅峰值相关桶参与判定（grp 全桶不具代表性）
                    if (!hasActivePeak && !hasPeakContributor && peakSum.GetValueOrDefault(r, NatStar.Of(0)).Equals(NatStar.Of(0)))
                    {
                        peakScope.Remove(r);
                    }
                }
            }
        }

        void AuditAtSample(NatStar t)
        {
            // gate(1) 负陷：运行中 net.Hi < 0（按资源归属的事件 scope 归因，非 Global）。
            foreach (var kv in net)
                if (!kv.Value.Hi.IsTop && kv.Value.Hi.Value < 0)
                {
                    // 取该资源的任一活跃贡献者的 scope 作归因（net 已按归一化资源聚合，scope 取首个活跃 key 的 e.Scope）
                    var scope = ResolveNetScope(kv.Key);
                    violations.Add(new Violation(t, kv.Key, scope, "NegativeDip",
                        $"累积净占用在 t={t} 为负（release 早于 create）：{kv.Value}", ResolveNetEi(kv.Key)));
                }
            // gate(2) 峰值：每 cap 资源当前运行中峰值 ≤ Caps[r]（按 cap 对应的归因 scope）。
            foreach (var kv in cap.Caps)
            {
                var nk = ResourceId.Normalize(kv.Key);
                var p = (topCount.TryGetValue(nk, out var tc) && tc > 0) ? NatStar.Top : peakSum.GetValueOrDefault(nk, NatStar.Of(0));
                if (p.CompareToFinite(kv.Value) > 0 && peakReported.Add(nk))
                {
                    var scope = ResolvePeakScope(nk);
                    // R4-V2（hickey-x）：上报归一化键 nk（与查找一致），否则同一违例随用户拼写呈现两种资源身份。
                    violations.Add(new Violation(t, nk, scope, "PeakExceeded",
                        $"峰值 {p} > 预算 {kv.Value}", ResolvePeakEi(nk)));
                }
            }
            // gate(3) 兼容：组内同 mode 活跃事件数 ≥2 ⇔ 同 mode 冲突（单一真源：Compatible.IsCompatible 同值必冲突）。
            // 组已按 (resource,scope,mode) 分组，组内 mode 相同，冲突等价于 IsCompatible(mode,mode)==false 且组大小≥2。
            foreach (var kv in grp)
            {
                if (kv.Value.Count < 2) continue;
                var mode = (Mode)kv.Key.Item3;
                if (Compatible.IsCompatible(mode, mode)) continue; // Use 等自兼容 ⇒ 组内不冲突
                var detail = mode == Mode.Create ? "create×create 冲突（CONFLICT 集，§3.2.3）"
                            : mode == Mode.Move ? "move×move 冲突（CONFLICT 集，§3.2.3）"
                            : "release×release 冲突（CONFLICT 集，§3.2.3）";
                var grpEi = kv.Value.Count > 0 ? kv.Value.First() : -1; // 取组内首个冲突事件索引（可定位 events[N]）
                violations.Add(new Violation(t, kv.Key.Item1, kv.Key.Item2, "CompatibleConflict", detail, grpEi));
            }
        }

        int sweepIdx = 0;
        for (int s = 0; s < samplePoints.Count; s++)
        {
            var t = samplePoints[s];
            var tv = t.Value; // 采样点恒为有限
            // 相位1：应用 time < tv 的全部扫换线事件（在 t 之前已完全解析，含早退出的事件）。
            while (sweepIdx < sweep.Count && sweep[sweepIdx].time < tv) Step(sweep[sweepIdx++].ei, sweep[sweepIdx - 1].enter);
            // 相位2：在 tv 处 enter 的事件（t=Lo 即存活）进入活动集。
            while (sweepIdx < sweep.Count && sweep[sweepIdx].time == tv && sweep[sweepIdx].enter) Step(sweep[sweepIdx++].ei, true);
            // 相位3：在采样点 tv 审计当前活动态（此刻 alive ⇔ Lo≤tv≤Hi）。
            AuditAtSample(t);
            // 相位4：在 tv 处 exit 的事件（t=Hi 仍存活，于 tv 之后移除）。
            while (sweepIdx < sweep.Count && sweep[sweepIdx].time == tv && !sweep[sweepIdx].enter) Step(sweep[sweepIdx++].ei, false);
        }

        // 闭包：全部有限事件结束后（t=maxFinite），有限 ω 累积 net 必须含 0（生命周期闭合），否则 Leak。
        // 独立于扫换线（仅一次 O(E·K)），与 CumulativeNet(closureT) 等价（全部 Lo≤closureT）。
        {
            var closureNet = new Dictionary<ResourceId, SignedInterval>();
            for (int ei = 0; ei < Events.Length; ei++)
            {
                var e = Events[ei];
                if (e.Loop.Count.IsTop) continue;       // 居民层豁免
                if (e.Lifetime.Lo.IsTop) continue;       // Lo=⊤ 永不存活，不入累积
                if (e.Lifetime.Lo.CompareToFinite(closureT) > 0) continue; // 修 auditR：仅纳入已开始（Lo≤closureT）事件，排除未来事件
                foreach (var c in e.Footprint.OccupyClaims)
                {
                    var r = ResourceId.Normalize(c.Resource);
                    var scaled = ScaleSize(c.Size ?? Interval.Default, e.Loop.Count);
                    var contrib = c.Mode == Mode.Release
                        ? new SignedInterval(Negate(scaled.Hi), Negate(scaled.Lo))
                        : new SignedInterval(ToZ(scaled.Lo), ToZ(scaled.Hi));
                    closureNet[r] = closureNet.TryGetValue(r, out var cur) ? cur.Add(contrib) : contrib;
                }
            }
            // rich-hickey2 R6 S06-004 + A1-09（生产审计批1 正名）：Leak 归因取「最晚开始的有限贡献者」（Lo 最大者）。
            // 注释曾声称额外按 Hi>closureT 过滤「尚未结束」，但 closureT=全剧本最大有限端点时该条件恒假，实现从未过滤——
            // 按实现正名（R9 YAGNI：不做存活性过滤，归因可能指向已正常闭合但最晚开始的事件）。
            var leakScope = new Dictionary<ResourceId, (ulong lo, ScopeId scope, int ei)>();
            for (int ei2 = 0; ei2 < Events.Length; ei2++)
            {
                var ee = Events[ei2];
                if (ee.Loop.Count.IsTop) continue;
                if (ee.Lifetime.Lo.IsTop) continue;
                if (ee.Lifetime.Lo.CompareToFinite(closureT) > 0) continue; // 闭包期间已开始
                foreach (var cc in ee.Footprint.OccupyClaims)
                {
                    var rr = ResourceId.Normalize(cc.Resource);
                    if (!leakScope.TryGetValue(rr, out var prev) || ee.Lifetime.Lo.Value > prev.lo)
                        leakScope[rr] = (ee.Lifetime.Lo.Value, ee.Scope, ei2);
                }
            }
            foreach (var kv in closureNet)
                if (!kv.Value.ContainsZero)
                {
                    var (_, ls, ei) = leakScope.TryGetValue(kv.Key, out var t) ? t : (0UL, new ScopeId.Global(), -1);
                    violations.Add(new Violation(closureT, kv.Key, ls, "Leak",
                        $"生命周期未闭合（净效应不含 0）：{kv.Value}", ei));
                }
        }

        // R1-HIGH-3（hickey-x）：报告 gate(2) 实际检查的预算资源数——Caps.Count==0 ⇒ 峰值门未运行，调用方须知情。
        return new AuditResult(violations.Count == 0, violations.ToImmutableArray(), cap.Caps.Count);
    }

    // §2.2 — Lifetime ∋ t 判定（hi=⊤ 视为无上界；Lo=⊤ ⇒ ⊤>有限t ⇒ 永不存活）。
    private static bool Alive(Interval lt, NatStar t) =>
        lt.Lo.CompareToFinite(t) <= 0 && (lt.Hi.IsTop || t.CompareToFinite(lt.Hi) <= 0);

    // §3.2.5 — scaleSize：ω=⊤ ⇒ 上界开放 [lo, ⊤]；否则端点 ×ω（§3.1.5a × 律内嵌 ⊤）。
    private static Interval ScaleSize(Interval s, NatStar w)
    {
        if (w.IsTop) return new Interval(s.Lo, NatStar.Top);
        return new Interval(s.Lo * w, s.Hi * w);
    }

    // R4-F1：超 long 表示域 ⇒ ZStar.Top（禁止 (long) 强转静默翻转符号）。
    private static ZStar ToZ(NatStar n) => (n.IsTop || n.Value > long.MaxValue) ? ZStar.Top : ZStar.Of(unchecked((long)n.Value));
    private static ZStar Negate(NatStar n) => (n.IsTop || n.Value > long.MaxValue) ? ZStar.Top : ZStar.Of(-unchecked((long)n.Value));
}

/// <summary>§2.3 — 峰值预算壳（软约束）。缺省 = 该资源无上限（⊤）。审计时 Peak ≤ Caps[r]，超限报 PeakExceeded。
/// 未设 cap（默认 ⊤）= 通过；显式有限 cap = 拒绝常驻资源并发（用户主动限制，非 bug，修 OPEN-4）。
/// rich-hickey2 R3（V3-E）：构造期防御拷贝为 ImmutableDictionary——外部字典改动不透传、getter 不可回写、
/// None 不可被 IDictionary 强转污染；Equals/GetHashCode 按内容（record struct 名副其实的值语义）。</summary>
public readonly record struct Budget : IEquatable<Budget>
{
    /// <summary>§2.3 — 每资源峰值上限；缺省该资源无上限。恒为不可变底座（ImmutableDictionary）。
    /// R4-RH-14（Hickey 视角）：getter 单点归一——default(Budget).Caps 永不为 null 外泄，
    /// 归一策略从 ctor/Audit/Equals/GetHashCode 四处散布收敛到一点。</summary>
    public IReadOnlyDictionary<ResourceId, NatStar> Caps => _caps ?? ImmutableDictionary<ResourceId, NatStar>.Empty;
    private readonly IReadOnlyDictionary<ResourceId, NatStar>? _caps;

    /// <summary>§2.3 — 从上限表构造（防御拷贝，null ⇒ 空预算；S06-002 归一键：caps 按归一化资源分组，同一资源的自别名如 Self(signal_x)/SignalBus(x) 合并为一条）。</summary>
    public Budget(IReadOnlyDictionary<ResourceId, NatStar>? caps)
    {
        if (caps is null) { _caps = ImmutableDictionary<ResourceId, NatStar>.Empty; return; }
        // rich-hickey2 R6 S06-002：caps 按归一化 ResourceId 分组（单一真源与 Audit 峰值键对齐），避免同一资源占两条目导致相等/哈希/ToJson 分裂。
        // 多条同归一键时取最后一条（后者赢），与 EffectScript.Audit 中 peakReported 去重后的单值一致。
        var norm = ImmutableDictionary.CreateBuilder<ResourceId, NatStar>();
        foreach (var kv in caps) norm[ResourceId.Normalize(kv.Key)] = kv.Value;
        _caps = norm.ToImmutable();
    }

    /// <summary>§2.3 — 空预算（所有资源无上限；不可变单例，不可经 IDictionary 强转写入）。等价于显式“无上限”声明（Dean 有条件项的显式化；IsPeakChecked==false 可区分“没查”与“查过全绿”）。</summary>
    public static readonly Budget None = new(ImmutableDictionary<ResourceId, NatStar>.Empty);

    // R4-RH-03（Hickey 视角）：Budget.Unbounded 与 None 完全等价的别名已删（零消费）。

    /// <summary>rich-hickey2 R3 V3-001 — 值相等：按键值对内容比较，与底座实例身份无关（§2.3；default(Budget).Caps=null 视为空预算）。</summary>
    public bool Equals(Budget other)
    {
        var a = Caps;
        var b = other.Caps;
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
            if (!b.TryGetValue(kv.Key, out var v) || !v.Equals(kv.Value)) return false;
        return true;
    }

    /// <summary>§2.3 — 与 Equals 同源的内容哈希（null Caps 视为空）。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var h = 17;
            foreach (var kv in Caps)
                h = h * 31 + (kv.Key.GetHashCode() ^ kv.Value.GetHashCode());
            return h;
        }
    }
}

/// <summary>§3.2 — 审计结果。Passed=全部 gate 通过；Violations 携带反例（时刻/资源/类型/当前值 vs 上限），供 AI 直接回修 JSON。
/// R1-HIGH-3（hickey-x）：CapsChecked 记录 gate(2) 实际检查的预算资源数——0 表示峰值门未运行，
/// 使「查过通过」与「没查」可区分（零预算时 Passed=true 不再冒充全绿）。</summary>
public readonly record struct AuditResult
{
    /// <summary>§3.2 — 是否全部通过。</summary>
    public bool Passed { get; }

    /// <summary>§3.2 — 违例清单（可空）。</summary>
    public ImmutableArray<Violation> Violations { get; }

    /// <summary>§3.2 R1-HIGH-3（hickey-x）— gate(2) 实际检查的预算资源数。0 ⇒ 峰值门整体未运行（无预算声明），调用方应显式知情而非默认全绿。</summary>
    public int CapsChecked { get; }

    /// <summary>§3.2 — 构造审计结果（兼容旧签名，CapsChecked=0）。
    /// rich-hickey2 R4-004：Passed 必须等于 Violations.IsEmpty（矛盾状态不可构造），
    /// 否则下游 `Passed==true && Violations≠∅` 是把"诚实"切成两半。调用方如确需 CapsChecked=0，可显式委托三参。</summary>
    public AuditResult(bool passed, ImmutableArray<Violation> violations)
        : this(passed, violations, 0) { }

    /// <summary>§3.2 R1-HIGH-3 — 全参构造（含覆盖面计数）。rich-hickey2 R4-004：同型 Passed≡Violations.IsEmpty 守卫。</summary>
    public AuditResult(bool passed, ImmutableArray<Violation> violations, int capsChecked)
    {
        if (passed != violations.IsDefaultOrEmpty)
            throw new ArgumentException($"AuditResult 不变量：Passed 必须等于 Violations.IsEmpty（passed={passed}, violations={violations.Length}）", nameof(passed));
        Passed = passed;
        Violations = violations;
        CapsChecked = capsChecked;
    }

    /// <summary>rich-hickey2 R4-004：派生只读——峰值门是否实际运行过（§3.2；R1-HIGH-3）。
    /// CapsChecked=0 ⇒ 零预算或未走 gate(2)，调用方应知情而非把"没查"当"全绿"。</summary>
    public bool IsPeakChecked => CapsChecked > 0;
}

/// <summary>§3.2 — 单条违例（反例）。携带供 AI 回修的充分信息：哪个时刻、哪个资源、哪类问题、当前值 vs 上限/阈值。</summary>
public readonly record struct Violation
{
    /// <summary>§3.2 — 违例发生的采样时刻 t。</summary>
    public NatStar AtT { get; }

    /// <summary>§3.2 — 涉事资源（归一化）。</summary>
    public ResourceId Resource { get; }

    /// <summary>§3.2 — 涉事作用域。</summary>
    public ScopeId Scope { get; }

    /// <summary>§3.2 — 违例类型：Leak | NegativeDip | PeakExceeded | CompatibleConflict。</summary>
    public string Kind { get; }

    /// <summary>§3.2 — 当前值 vs 上限/阈值（供 AI 回修 JSON）。</summary>
    public string Detail { get; }

    /// <summary>§3.2/R9 — 来源事件索引（可定位 JSON 的 events[N]，直接回修）。-1 表示该违例属累积层（非单条事件，如 NegativeDip 净占用负陷跨事件）。</summary>
    public int EventIndex { get; }

    /// <summary>§3.2 — 构造单条违例。</summary>
    public Violation(NatStar atT, ResourceId resource, ScopeId scope, string kind, string detail, int eventIndex = -1)
    {
        AtT = atT;
        Resource = resource;
        Scope = scope;
        Kind = kind;
        Detail = detail;
        EventIndex = eventIndex;
    }
}
