// SignedNet.cs — PDR §3.3.1 实现：有符号网值 ZStar/SignedInterval（create 正、release 负、区间含 0 即守恒）。LANDING_PLAN §3.3：类型边界载体。
#if ANALYZER_SHARED
namespace Cosmos.EffectAlgebra.Analyzer.Shared;
#elif GENERATOR_SHARED
namespace Cosmos.EffectAlgebra.Generator.Shared;
#else
namespace Cosmos.EffectAlgebra;
#endif

/// <summary>
/// §3.3.1 — 有符号网值 ZStar = ℤ ∪ {⊤}。net = Σcreate c.size − Σrelease c.size 是有符号量，可为负，
/// 故不能复用 §3.1.5a 的非负 ℕ*。ℤ* 承载 net 的有符号边界。
/// ⊤ 表示「上界未知」；所有运算律由运算符/方法内嵌，编译器强制，无需运行时检查（MA-002 闭包）。
/// 类型字段 <see cref="IsTop"/> 即边界：ℕ* 与 ℤ* 区分清晰，负值由 ZStar.Value:long 承载（非运行时 if 漏判）。
/// </summary>
public readonly record struct ZStar
{
    /// <summary>§3.3.1 — 当 true 时表示 ⊤ 未知；<see cref="Value"/> 无效。</summary>
    public bool IsTop { get; }

    /// <summary>§3.3.1 — 仅当 !IsTop 有效（可为负的有符号值）。</summary>
    public long Value { get; }

    private ZStar(bool isTop, long value) { IsTop = isTop; Value = value; }

    /// <summary>§3.3.1 — 上界标记 ⊤（未知网值）。</summary>
    public static readonly ZStar Top = new(true, 0);

    /// <summary>§3.3.1 — 零元（有符号 0）。</summary>
    public static readonly ZStar Zero = new(false, 0);

    /// <summary>§3.3.1 — 从具体整数构造（可为负）。</summary>
    public static ZStar Of(long v) => new(false, v);

    // §3.3.1 加法律：任一 ⊤ ⇒ ⊤（未知 + 任何 = 未知）；long 溢出 ⇒ 保守 ⊤（与 ℕ* 环绕策略对齐，R4-F1：不静默回卷翻转符号）
    public static ZStar operator +(ZStar a, ZStar b)
    {
        if (a.IsTop || b.IsTop) return Top;
        unchecked
        {
            var r = a.Value + b.Value;
            // 同号相加结果符号翻转 ⇒ 溢出 ⇒ 保守 ⊤
            bool overflow = ((a.Value ^ r) & (b.Value ^ r)) < 0;
            return overflow ? Top : Of(r);
        }
    }

    // §3.3.1 减法律：任一 ⊤ ⇒ ⊤（未知 − 任何 = 未知）；long 溢出 ⇒ 保守 ⊤（R4-F1）
    public static ZStar operator -(ZStar a, ZStar b)
    {
        if (a.IsTop || b.IsTop) return Top;
        unchecked
        {
            var r = a.Value - b.Value;
            // 异号相减结果符号与被减数不同 ⇒ 溢出 ⇒ 保守 ⊤
            bool overflow = ((a.Value ^ b.Value) & (a.Value ^ r)) < 0;
            return overflow ? Top : Of(r);
        }
    }

    // §3.3.1 max：max(x,⊤)=⊤；max(⊤,x)=⊤（内嵌 ⊤ 律）
    public ZStar Max(ZStar o) => (IsTop || o.IsTop) ? Top : Of(Math.Max(Value, o.Value));

    // §3.3.1 min：min(x,⊤)=x；min(⊤,x)=x（内嵌 ⊤ 律，与 NatStar.Min 对偶；rich-hickey2 R2-002 修——原 (IsTop||o.IsTop)?Top 把 Max 的上界律误抄给 Min，Min(x,⊤) 坍缩为 ⊤）
    public ZStar Min(ZStar o)
    {
        if (IsTop && o.IsTop) return Top;
        if (IsTop) return o;
        if (o.IsTop) return this;
        return Of(Math.Min(Value, o.Value));
    }

    /// <summary>§3.3.1 调试字符串（无代数语义，仅 ⊤ 或有符号值表示）。</summary>
    public override string ToString() => IsTop ? "⊤" : Value.ToString();
}

/// <summary>
/// §3.3.1 — 有符号 net 区间 [lo, hi]，lo ≤ hi，端点 ∈ ZStar。承载「create(+) − release(−)」后的净占用区间。
/// 类型强制有符号边界：负值经 ZStar.Value:long 表达；Lo≤Hi 由构造子校验（IsTop 不参与数值比较）。
/// ContainsZero 即 DO-9「net 区间含 0 ⇒ 可能闭合（不报警）」的类型安全判定。
/// </summary>
public readonly record struct SignedInterval
{
    /// <summary>§3.3.1 — 下界（∈ ZStar，可为负或 ⊤）。</summary>
    public ZStar Lo { get; }

    /// <summary>§3.3.1 — 上界（∈ ZStar，可为负或 ⊤）。</summary>
    public ZStar Hi { get; }

    /// <summary>§3.3.1 不变量：lo ≤ hi（均为有限时）；任一端 IsTop（未知）时不比较（视为未知区间）。</summary>
    public SignedInterval(ZStar lo, ZStar hi)
    {
        if (!lo.IsTop && !hi.IsTop && lo.Value > hi.Value)
            throw new ArgumentException($"SignedInterval lo({lo}) > hi({hi}) violates §3.3.1 lo≤hi");
        Lo = lo;
        Hi = hi;
    }

    /// <summary>§3.3.1 零区间 [0,0]（缺省净效应，含 0）。</summary>
    public static readonly SignedInterval Zero = new(ZStar.Zero, ZStar.Zero);

    /// <summary>§3.3.1/DO-9 — 区间含 0 ⇔ 生命周期可能闭合（不报警）。
    /// 任一端 IsTop（未知）⇒ 视为需人工界定 ⇒ 返回 false（fail-closed，§3.3.1 DO-9）。</summary>
    public bool ContainsZero => (Lo.IsTop || Hi.IsTop) ? false : (Lo.Value <= 0 && Hi.Value >= 0);

    /// <summary>§3.3.1 — 有符号 net 求和：同资源多 Claim 的净效应 = 区间逐端相加 [lo+o.lo, hi+o.hi]（create(+) 与 release(−) 符号相反，自然抵消）。
    /// 任一端 IsTop（未知）⇒ 整体 ⊤（ fail-closed，不谎称守恒）。这是 net 聚合的「真·求和」，区别于 <see cref="Merge"/>（min/max 仅用于单 Claim size 不确定区间）。</summary>
    public SignedInterval Add(SignedInterval o) => new(Lo + o.Lo, Hi + o.Hi);

    /// <summary>§3.3.1 — merge 为 join：min/max 内嵌 ZStar ⊤ 律（合并同向净效应 / size 不确定区间，非 net 求和）。</summary>
    public SignedInterval Merge(SignedInterval o) => new(Lo.Min(o.Lo), Hi.Max(o.Hi));

    /// <summary>§9.1/§3.3.1 — 当两端均有限时算 mid=(lo+hi)/2 与 range=(hi−lo) 返回 true；任一端 IsTop ⇒ false（调用方据此判 ⊤，整体 Deviation 标 ⊤）。</summary>
    public bool TryMid(out double mid, out double range)
    {
        mid = 0.0;
        range = 0.0;
        if (Lo.IsTop || Hi.IsTop) return false;
        // R4-F3：先除后加，防 (lo+hi) 在 long 内先溢出（[MaxLong,MaxLong] 原得 -1）
        mid = Lo.Value / 2.0 + Hi.Value / 2.0;
        range = (double)Hi.Value - Lo.Value;
        return true;
    }

    /// <summary>§3.3.1 调试字符串（无代数语义，仅区间表示）。</summary>
    public override string ToString() => $"[{Lo},{Hi}]";
}
