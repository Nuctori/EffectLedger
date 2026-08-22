// SignedNet.cs — PDR §3.3.1 实现：有符号网值 ZStar/SignedInterval（create 正、release 负、区间含 0 即守恒）。LANDING_PLAN §3.3：类型边界载体。
namespace Cosmos.EffectAlgebra;

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

    // §3.3.1 加法律：任一 ⊤ ⇒ ⊤（未知 + 任何 = 未知）
    public static ZStar operator +(ZStar a, ZStar b) => (a.IsTop || b.IsTop) ? Top : Of(a.Value + b.Value);

    // §3.3.1 减法律：任一 ⊤ ⇒ ⊤（未知 − 任何 = 未知）
    public static ZStar operator -(ZStar a, ZStar b) => (a.IsTop || b.IsTop) ? Top : Of(a.Value - b.Value);

    // §3.3.1 max：max(x,⊤)=⊤；max(⊤,x)=⊤（内嵌 ⊤ 律）
    public ZStar Max(ZStar o) => (IsTop || o.IsTop) ? Top : Of(Math.Max(Value, o.Value));

    // §3.3.1 min：min(x,⊤)=x；min(⊤,x)=x（内嵌 ⊤ 律）
    public ZStar Min(ZStar o) => (IsTop || o.IsTop) ? Top : Of(Math.Min(Value, o.Value));

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

    /// <summary>§3.3.1 — merge 为 join：min/max 内嵌 ZStar ⊤ 律（合并同向净效应）。</summary>
    public SignedInterval Merge(SignedInterval o) => new(Lo.Min(o.Lo), Hi.Max(o.Hi));

    /// <summary>§9.1/§3.3.1 — 当两端均有限时算 mid=(lo+hi)/2 与 range=(hi−lo) 返回 true；任一端 IsTop ⇒ false（调用方据此判 ⊤，整体 Deviation 标 ⊤）。</summary>
    public bool TryMid(out double mid, out double range)
    {
        mid = 0.0;
        range = 0.0;
        if (Lo.IsTop || Hi.IsTop) return false;
        mid = (Lo.Value + Hi.Value) / 2.0;
        range = (double)Hi.Value - Lo.Value;
        return true;
    }

    /// <summary>§3.3.1 调试字符串（无代数语义，仅区间表示）。</summary>
    public override string ToString() => $"[{Lo},{Hi}]";
}
