// EffectScript.cs — 视觉效应代数剧本（L1 增量，零 Godot 依赖）。EFFECT_SCRIPT.md §2/§3。
// LANDING_PLAN 角色：在已建 L1（§3.1–§3.3）之上加「时间轴」语义，使「视觉代数剧本」可静态验证。
// 数学全部复用既有 L1（Interval/Signature/NetTable/Peak/Compatible/Combination/LoopCount），零新增代数结构。
// 类型即边界：所有载体为 readonly record struct，构造即全必填；注释承载 ω 语义与端点采样定理（§3）。
//
// 性能（Jeff Dean 视角，audit iter-effect26.md）：朴素 Audit 每采样点全量重建 At/CumulativeNet 且 gate(3) 两两配对 ⇒
// 最坏 O(S·E²)（S=采样点, E=事件）；对「数千粒子同屏同资源」的 AI 视觉脚本尾延迟与内存平方恶化。
// 本实现改用「扫换线（sweep-line）」：端点排序一次 O(E log E)，沿时间轴增量维护活动集与运行计数，
// 每个事件只在「进入(Lo)」/「退出(Hi)」各处理一次 ⇒ 总复杂度 O(E·K·log E)，内存 O(E·K)。
// 数学上与端点采样定理等价（At 分段常数、仅各有限端点跳变；各 gate 输出集合与逐点全算版本一致）。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
namespace Cosmos.EffectAlgebra;

/// <summary>
/// §2.1 / EFFECT_SCRIPT.md §2 — 一个视觉元素（图层/sprite/粒子）的生命周期。
/// 视觉元素在代数上是原子的 ⇒ 不拆成 N 个 TimedClaim，而是把 Claim 分组进一个 Event。
/// 类型强制（用户铁律）：4 字段位置记录 ⇒ 构造即全必填；Lifetime/Scope/Footprint/Loop 的数学边界由既有类型承载。
/// 修正 auditA OPEN-1：Event 自身携带 <see cref="Scope"/>，作为 At/Net/Peak 的 scope 来源（不再有自由变量 loopScope）。
/// </summary>
public readonly record struct EffectEvent
{
    /// <summary>§3.1.5 — 存在时间窗 [lo, hi]；hi=⊤ ⇒ 上界开放（常驻层）。</summary>
    public Interval Lifetime { get; }

    /// <summary>§3.1.3b — 该元素的作用域（Scene/Type/Method…）；At/Net/Peak 的 scope 来源（修 OPEN-1）。</summary>
    public ScopeId Scope { get; }

    /// <summary>§3.1.4b — 该元素存活期内的资源签名（三桶：read/write/occupy）。</summary>
    public Signature Footprint { get; }

    /// <summary>§3.2.5 — 并发实例数 ω∈ℕ∪{⊤}。ω 表示「同一时刻有多少个该元素并发存在」（并发副本，非时间重复）；
    /// 默认单实例（<see cref="LoopCount"/> 的 1 值）。ω=⊤ ⇒ 上界开放（常驻）。</summary>
    public LoopCount Loop { get; }

    /// <summary>§2.1 — 全字段构造（含 ω）。</summary>
    public EffectEvent(Interval lifetime, ScopeId scope, Signature footprint, LoopCount loop)
    {
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

    /// <summary>§2.2 — 从事件集构造剧本。</summary>
    public EffectScript(ImmutableArray<EffectEvent> events) { Events = events; }

    /// <summary>§2.2 — 从事件集构造剧本（IEnumerable 便捷）。</summary>
    public EffectScript(IEnumerable<EffectEvent> events) { Events = events.ToImmutableArray(); }

    /// <summary>
    /// §2.2 / §3 — At(t)：t 时刻屏幕总签名 = 所有 Lifetime∋t 的 Event 各取
    /// <see cref="Combination.Loop(Footprint, Loop, Scope)"/>（§3.2.5，loopScope=Event.Scope，修 OPEN-1）后 Union（§3.2.1 半格并）。
    /// 瞬时快照：存活元素各贡献 ω 份并发副本（ω 解释见 EffectEvent.Loop），不重算代数。
    /// 确定性（auditA 焦点6）：Signature 以 ImmutableHashSet 存 Claims，并集无序可交换 ⇒ 同剧本同 t ⇒ 同内容签名。
    /// <see cref="Lifetime"/>∋t ⇔ Lo≤t ∧ (Hi=⊤ ∨ t≤Hi)。
    /// </summary>
    public Signature At(NatStar t)
    {
        var acc = Signature.Empty;
        foreach (var e in Events)
        {
            if (Alive(e.Lifetime, t))
                acc = Signature.Union(acc, Combination.Loop(e.Footprint, e.Loop, e.Scope));
        }
        return acc;
    }

    /// <summary>
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
    /// ③每 (resource,scope,mode) 的活跃事件集合（O(1) 判冲突，等价于两两枚举）。每事件进入/退出各处理一次 ⇒ O(E·K·log E)。
    /// </summary>
    public AuditResult Audit(Budget cap)
    {
        var violations = new List<Violation>();

        // 端点集合（有限 Lo/Hi）。hi=⊤ 视为开放尾段（采 maxFinite+1 代表点）。
        var endpoints = new SortedSet<ulong>();
        ulong maxFinite = 0;
        bool anyFinite = false;
        bool anyOpenEnd = false;
        for (int i = 0; i < Events.Length; i++)
        {
            var lt = Events[i].Lifetime;
            if (!lt.Lo.IsTop) { endpoints.Add(lt.Lo.Value); maxFinite = Math.Max(maxFinite, lt.Lo.Value); anyFinite = true; }
            if (!lt.Hi.IsTop) { endpoints.Add(lt.Hi.Value); maxFinite = Math.Max(maxFinite, lt.Hi.Value); anyFinite = true; }
            else anyOpenEnd = true;
        }
        var samplePoints = new List<NatStar>();
        foreach (var v in endpoints) samplePoints.Add(NatStar.Of(v)); // SortedSet 已排序 ⇒ 输出确定性（§5）
        if (anyOpenEnd) samplePoints.Add(NatStar.Of(maxFinite + 1));
        if (samplePoints.Count == 0) samplePoints.Add(NatStar.Of(0)); // 空脚本/全 ⊤：采 t=0
        var closureT = anyFinite ? NatStar.Of(maxFinite) : NatStar.Of(0);

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
                    net[r] = net.TryGetValue(r, out var cur) ? cur.Add(contrib) : contrib;
                }
            }
            // gate(2) peak + gate(3) grp：enter(+) / exit(−)。
            foreach (var c in e.Footprint.OccupyClaims)
            {
                var r = ResourceId.Normalize(c.Resource);
                var key = (r, c.Scope, (int)c.Mode);
                if (enter)
                {
                    if (!grp.TryGetValue(key, out var hs)) grp[key] = hs = new HashSet<int>();
                    hs.Add(ei);
                    if (c.Mode != Mode.Release) // §3.3.2 release 不贡献峰值
                    {
                        bool top = (c.Size ?? Interval.Default).Hi.IsTop || e.Loop.Count.IsTop;
                        if (top) topCount[r] = topCount.GetValueOrDefault(r) + 1;
                        else
                        {
                            // 保守 ⊤：×/＋ 溢出（ulong 环绕）即标 ⊤，不静默低估峰值（与 exit 路径一致，补 auditR 仅修 exit 漏修 enter 的裸 ulong* 回卷）。
                            var hi = (c.Size ?? Interval.Default).Hi;
                            var w = e.Loop.Count;
                            var curSum = peakSum.GetValueOrDefault(r, NatStar.Of(0));
                            var mul = (!hi.IsTop && !w.IsTop && hi.Value <= ulong.MaxValue / w.Value) ? hi.Value * w.Value : ulong.MaxValue;
                            peakSum[r] = (curSum.IsTop || mul == ulong.MaxValue || curSum.Value > ulong.MaxValue - mul)
                                ? NatStar.Top : NatStar.Of(curSum.Value + mul);
                        }
                    }
                }
                else
                {
                    if (grp.TryGetValue(key, out var hs)) { hs.Remove(ei); if (hs.Count == 0) grp.Remove(key); }
                    if (c.Mode != Mode.Release)
                    {
                        bool top = (c.Size ?? Interval.Default).Hi.IsTop || e.Loop.Count.IsTop;
                        if (top) { if (topCount.TryGetValue(r, out var tc) && tc > 0) topCount[r] = tc - 1; }
                        else
                        {
                            var cur = peakSum.GetValueOrDefault(r, NatStar.Of(0));
                            // 保守 ⊤：± 溢出（ulong 环绕）即标 ⊤，不静默低估峰值（修 auditR peakSum 裸 ulong* 回卷）。
                            var hi = (c.Size ?? Interval.Default).Hi;
                            var w = e.Loop.Count;
                            var sub = (!hi.IsTop && !w.IsTop && hi.Value <= ulong.MaxValue / w.Value)
                                ? NatStar.Of(hi.Value * w.Value) : NatStar.Top;
                            var curSum = cur.IsTop ? NatStar.Top
                                : (!hi.IsTop && !w.IsTop) ? NatStar.Of(cur.Value) : NatStar.Top;
                            peakSum[r] = (curSum.IsTop || sub.IsTop) ? NatStar.Top
                                : NatStar.Of(sub.Value <= curSum.Value ? curSum.Value - sub.Value : 0);
                        }
                    }
                }
            }
        }

        void AuditAtSample(NatStar t)
        {
            // gate(1) 负陷：运行中 net.Hi < 0。
            foreach (var kv in net)
                if (!kv.Value.Hi.IsTop && kv.Value.Hi.Value < 0)
                    violations.Add(new Violation(t, kv.Key, new ScopeId.Global(), "NegativeDip",
                        $"累积净占用在 t={t} 为负（release 早于 create）：{kv.Value}"));
            // gate(2) 峰值：每 cap 资源当前运行中峰值 ≤ Caps[r]。
            foreach (var kv in cap.Caps)
            {
                var nk = ResourceId.Normalize(kv.Key);
                var p = (topCount.TryGetValue(nk, out var tc) && tc > 0) ? NatStar.Top : peakSum.GetValueOrDefault(nk, NatStar.Of(0));
                if (p.CompareToFinite(kv.Value) > 0)
                    violations.Add(new Violation(t, kv.Key, new ScopeId.Global(), "PeakExceeded",
                        $"峰值 {p} > 预算 {kv.Value}"));
            }
            // gate(3) 兼容：组内同 mode∈{create,move,release} 活跃事件数 ≥2 ⇔ 存在跨事件同 mode 冲突对（CONFLICT 集，§3.2.3）。
            // 数学等价于逐点两两枚举：同 mode 多份副本必来自 ≥2 个不同事件（单事件内 ω 份同 EventIdx 不触发，与逐点一致）。
            foreach (var kv in grp)
            {
                var mode = kv.Key.Item3;
                if ((mode == (int)Mode.Create || mode == (int)Mode.Move || mode == (int)Mode.Release) && kv.Value.Count >= 2)
                {
                    var detail = mode == (int)Mode.Create ? "create×create 冲突（CONFLICT 集，§3.2.3）"
                                : mode == (int)Mode.Move ? "move×move 冲突（CONFLICT 集，§3.2.3）"
                                : "release×release 冲突（CONFLICT 集，§3.2.3）";
                    violations.Add(new Violation(t, kv.Key.Item1, kv.Key.Item2, "CompatibleConflict", detail));
                }
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
                if (e.Lifetime.Lo.IsTop) continue;       // Lo=⊤ 永不存活，不入累积
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
            foreach (var kv in closureNet)
                if (!kv.Value.ContainsZero)
                    violations.Add(new Violation(closureT, kv.Key, new ScopeId.Global(), "Leak",
                        $"生命周期未闭合（净效应不含 0）：{kv.Value}"));
        }

        return new AuditResult(violations.Count == 0, violations.ToImmutableArray());
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

    private static ZStar ToZ(NatStar n) => n.IsTop ? ZStar.Top : ZStar.Of((long)n.Value);
    private static ZStar Negate(NatStar n) => n.IsTop ? ZStar.Top : ZStar.Of(-(long)n.Value);
}

/// <summary>§2.3 — 峰值预算壳（软约束）。缺省 = 该资源无上限（⊤）。审计时 Peak ≤ Caps[r]，超限报 PeakExceeded。
/// 未设 cap（默认 ⊤）= 通过；显式有限 cap = 拒绝常驻资源并发（用户主动限制，非 bug，修 OPEN-4）。</summary>
public readonly record struct Budget
{
    /// <summary>§2.3 — 每资源峰值上限；缺省该资源无上限。</summary>
    public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }

    /// <summary>§2.3 — 从上限表构造。</summary>
    public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; }

    /// <summary>§2.3 — 空预算（所有资源无上限）。</summary>
    public static readonly Budget None = new(new Dictionary<ResourceId, NatStar>());
}

/// <summary>§3.2 — 审计结果。Passed=全部 gate 通过；Violations 携带反例（时刻/资源/类型/当前值 vs 上限），供 AI 直接回修 JSON。</summary>
public readonly record struct AuditResult
{
    /// <summary>§3.2 — 是否全部通过。</summary>
    public bool Passed { get; }

    /// <summary>§3.2 — 违例清单（可空）。</summary>
    public ImmutableArray<Violation> Violations { get; }

    /// <summary>§3.2 — 构造审计结果。</summary>
    public AuditResult(bool passed, ImmutableArray<Violation> violations) { Passed = passed; Violations = violations; }
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

    /// <summary>§3.2 — 构造单条违例。</summary>
    public Violation(NatStar atT, ResourceId resource, ScopeId scope, string kind, string detail)
    {
        AtT = atT;
        Resource = resource;
        Scope = scope;
        Kind = kind;
        Detail = detail;
    }
}
