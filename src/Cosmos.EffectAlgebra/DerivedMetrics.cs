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

    // R4-RH-11（Hickey 视角）：LoopCount.TryOf 已删除——零消费且失败时递出 IsValid=false 毒值
    //（V5-002「非法值构造期不可表达」被自家旁路 API 削弱）。需要软失败时自写 n >= 1 ? Of(n) : Top。
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

    // 【P1-B3】Sequence / Parallel 两名别名已删除（四名一实收口：组合唯一入口 = Signature.Union，
    // 幂等并、无时序/并行语义）。原 Parallel 的 PARA_CONFLICT 前置守卫随删——冲突检测权威 =
    // EffectScript.Audit gate(3)（25 组合矩阵 + 扫换线集成钉），直连别名上的冗余守卫不再保留。
    // 时序/并行真语义若未来需要，归 F 轨（F2 精化类型同窗评估），不得再以别名形态复活。

    // R4-RH-03（Hickey 视角）：UnionChecked 与 Parallel 完全等价的别名已删（零消费；揭示语义见 Parallel 的 XML doc）。
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
