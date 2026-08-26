// DerivedMetrics.cs — PDR §3.2.5/§3.3 实现：循环组合 ω∈ℕ∪{⊤}、便利封装 Derived.Peak/Net/IsConserved。LANDING_PLAN §3.2：L1 派生度量。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra;

/// <summary>
/// §3.2.5 — 循环次数 ω ∈ ℕ ∪ {⊤}。静态未知 ⇒ ⊤（上界标记，非发散，MA-002）。
/// 类型字段 <see cref="Count"/>.<see cref="NatStar.IsTop"/> 即边界：ω=⊤ 由类型强制检测，不靠运行时魔法数。
/// </summary>
public readonly record struct LoopCount
{
    /// <summary>§3.2.5 — 循环次数 ω（⊤ 时 <see cref="NatStar.IsTop"/>=true，<see cref="NatStar.Value"/> 无效）。</summary>
    public NatStar Count { get; }

    private LoopCount(NatStar count) { Count = count; }

    /// <summary>§3.2.5 — 有限循环次数 ω=n（ω≥1，0 无意义，会使 scale 退化为 [0,0] 致 Leak 误报）。</summary>
    public static LoopCount Of(ulong n) => n == 0 ? throw new ArgumentOutOfRangeException(nameof(n), "LoopCount 必须 ≥1（0 会使规模缩放为 [0,0] 致守恒误报）") : new(NatStar.Of(n));

    /// <summary>§3.2.5 — 静态未知循环次数 ω=⊤（上界开放，供 Peak/net 以 ⊤ 兜底）。</summary>
    public static readonly LoopCount Top = new(NatStar.Top);

    /// <summary>rich-hickey2 R5 V5-002：派生合法性——ω 须 ≥1 或 ⊤。
    /// 用途：消费侧守卫（EffectEvent/Combination.Loop）可统一 `if (!loop.IsValid) throw`，
    /// 替代散落的 `!IsTop && Value==0` 判定；`default(LoopCount)` 即非法。</summary>
    public bool IsValid => Count.IsTop || Count.Value >= 1;

    /// <summary>rich-hickey2 R5 V5-002：n≥1 ⇒ 返回合法值；n==0 ⇒ 静默失败并返回 default（非法值，让 IsValid 显式化）。</summary>
    public static bool TryOf(ulong n, out LoopCount result)
    {
        if (n >= 1) { result = Of(n); return true; }
        result = default;
        return false;
    }
}

/// <summary>
/// §3.2.5 / §3.2.1 / §3.2.2 — 组合算子。
/// 循环组合把 body 的每个 Claim 重新 scope 到 loopScope，并按 ω 缩放 size（ω=⊤ ⇒ 上界开放）。
/// 序列/并行组合均为 ∪（§3.2.1/§3.2.2 半格并；并行跨调用点 Compatible 检查由 L3 Analyzer 补，本层不重复）。
/// </summary>
public static class Combination
{
    /// <summary>
    /// §3.2.5 — (S × ω) = Σ_{i=1..ω} copy_i(S)。copy_i 为 S 副本，scope 重新标注为 loopScope。
    /// ω 有限：每个 Claim 的 size 区间按 ω 缩放（ω 个副本之和 ⇒ size×ω），上界/下界分别乘（§3.1.5a × 律内嵌 ⊤）。
    /// ω=⊤：每个 Claim 的 size 上界拉到 ⊤（上界开放副本集合），供 Peak/net 以 ⊤ 兜底（§3.2.5/§3.3.2 MA-002）。
    /// 类型保障：ω=⊤ 经 <see cref="LoopCount.Count"/>.<see cref="NatStar.IsTop"/> 检测，无魔法数。
    /// </summary>
    public static Signature Loop(Signature body, LoopCount ω, ScopeId loopScope)
    {
        // rich-hickey2 R4-003：与 EffectEvent 构造期守卫对称——直接调 Combination.Loop 不许 default(LoopCount) 静默产 [0,0] 签名
        // （ω=0 不会让 Loop 抛，size 端点 ×0=0 ⇒ Leak 误报/守恒坍缩）。失败模式单一真源。
        if (!ω.Count.IsTop && ω.Count.Value == 0)
            throw new ArgumentOutOfRangeException(nameof(ω), "LoopCount 必须 ≥1 或 ⊤（ω=0 会使 size 缩放为 [0,0] 致守恒误报；请用 LoopCount.Of(n≥1) 或 LoopCount.Top，勿传 default(LoopCount)）");
        var result = Signature.Empty;
        foreach (var c in body.ReadClaims)
            result = Signature.Union(result, Signature.Of(c with { Scope = loopScope, Size = Scale(c.Size ?? Interval.Default, ω.Count) }));
        foreach (var c in body.WriteClaims)
            result = Signature.Union(result, Signature.Of(c with { Scope = loopScope, Size = Scale(c.Size ?? Interval.Default, ω.Count) }));
        foreach (var c in body.OccupyClaims)
            result = Signature.Union(result, Signature.Of(c with { Scope = loopScope, Size = Scale(c.Size ?? Interval.Default, ω.Count) }));
        return result;
    }

    /// <summary>§3.2.1 — 序列组合 (S₁ ; S₂) := S₁ ∪ S₂。
    /// **L1 警告**（rich-hickey2 R5 V5-001）：本方法不承载时序区分，与 <see cref="Signature.Union"/> 完全等价。
    /// 用户以 "Sequence" 命名许诺时序是 L1 类型不承载的幻象区分——时序性由 L3 Analyzer 跨调用点 Compatible 检查补（§3.2.1/§3.2.3）。
    /// 新代码请直接用 <see cref="Signature.Union"/>；此名仅保留以避免破坏既有调用。</summary>
    public static Signature Sequence(Signature a, Signature b) => Signature.Union(a, b);

    /// <summary>§3.2.2 — 并行组合 (S₁ ∥ S₂) := S₁ ∪ S₂。
    /// R4-F4：跨分支同归一化资源做 Compatible 前置守卫——CONFLICT 对（如 create×create）抛 PARA_CONFLICT，
    /// 不再静默 Union 吞掉冲突证据（L3 分析器看不到直接调用，前置条件必须在函数内执行）。
    /// **L1 警告**（rich-hickey2 R5 V5-001）：本方法不承载并行区分，并行性由前置 Compatible 检查 + L3 跨调用点补；与 Union 等价（差异在守卫抛 PARA_CONFLICT）。</summary>
    public static Signature Parallel(Signature a, Signature b)
    {
        foreach (var ca in a.OccupyClaims)
            foreach (var cb in b.OccupyClaims)
                if (ResourceId.Normalize(ca.Resource).Equals(ResourceId.Normalize(cb.Resource))
                    && !Compatible.IsCompatible(ca.Mode, cb.Mode))
                    throw new InvalidOperationException(
                        $"PARA_CONFLICT: 并行分支对资源 {ca.Resource} 的 mode {ca.Mode}×{cb.Mode} 冲突（CONFLICT 集，§3.2.3）");
        return Signature.Union(a, b);
    }

    // §3.2.5 × ω 的 size 缩放：ω=⊤ ⇒ 上界开放（[lo, ⊤]）；否则区间端点按 §3.1.5a 乘法缩放。
    // lo 恒有限（§3.1.5 下界不可为 ⊤），故 lo×ω 无 NaN 路径；hi=⊤ 时 ⊤×有限=⊤ 保持开放。
    private static Interval Scale(Interval s, NatStar w)
    {
        if (w.IsTop) return new Interval(s.Lo, NatStar.Top); // 上界开放
        return new Interval(s.Lo * w, s.Hi * w);
    }
}

/// <summary>
/// §3.3 — 派生度量便利封装（net / Peak / 守恒），把 Algebra.cs 的 <see cref="NetTable"/>、<see cref="Peak"/> 暴露为函数式入口。
/// 类型字段即边界：Peak 用 size 求和（§3.3.2，非 §3.2.5 废弃的 cardinality 形式）；ω=⊤ 经 <see cref="NatStar.IsTop"/> 兜底返回 ⊤（§3.2.5/MA-002）。
/// </summary>
public static class Derived
{
    /// <summary>§3.3.2 — Peak(S,scope)：scope 下全部 claim 的 size 求和；任一 ⊤（含 Loop ω=⊤ 标记的开放上界）⇒ 整体 ⊤。</summary>
    public static NatStar Peak(Signature s, ScopeId scope) => Cosmos.EffectAlgebra.Peak.Compute(s, scope);

    /// <summary>§3.3.1 — net(S,scope)：按归一化资源分组，occupy 桶带符号 size 求和（create/release 抵消）。</summary>
    public static NetTable Net(Signature s, ScopeId scope) => NetTable.Compute(s, scope);

    /// <summary>§3.3.1 DO-9 — 守恒判定：资源净效应跨 0 ⇒ 生命周期闭合；⊤（未知）⇒ 不守恒（fail-closed，交人工确认）。</summary>
    public static bool IsConserved(Signature s, ResourceId r, ScopeId scope) => Net(s, scope).IsConserved(r);
}
