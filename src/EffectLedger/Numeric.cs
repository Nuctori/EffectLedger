// Numeric.cs — PDR §3.1.5a/§3.1.5b/§3.1.5c 实现：ℕ*/区间/DeviationVal 的 ⊤-闭环代数载体。LANDING_PLAN §3.1：L1 纯代数核心（零 Godot 依赖）。
#if ANALYZER_SHARED
namespace EffectLedger.Analyzer.Shared;
#elif GENERATOR_SHARED
namespace EffectLedger.Generator.Shared;
#else
namespace EffectLedger;
#endif

/// <summary>
/// §3.1.5a — 扩展自然数 ℕ* = ℕ ∪ {⊤}。⊤ 为上界标记（非 IEEE ∞，永不崩溃、永不 NaN/发散）。
/// 所有 §3.1.5a 运算律由运算符/方法内嵌，编译器强制，无需运行时检查。MA-002 闭包（∞ 的代数性质已定义）。
/// </summary>
public readonly record struct NatStar
{
    /// <summary>§3.1.5a — 当 true 时表示上界 ⊤ 未知；<see cref="Value"/> 无效。</summary>
    public bool IsTop { get; }

    /// <summary>§3.1.5a — 仅当 !IsTop 有效（有限自然数）。</summary>
    public ulong Value { get; }

    private NatStar(bool isTop, ulong value) { IsTop = isTop; Value = value; }

    /// <summary>§3.1.5a — 上界标记 ⊤（未知上界）。</summary>
    public static readonly NatStar Top = new(true, 0);

    /// <summary>§3.1.5a — 从具体自然数构造（有限值）。</summary>
    public static NatStar Of(ulong v) => new(false, v);

    // §3.1.5a 加法律：x+⊤=⊤；⊤+⊤=⊤；溢出（ulong 静默环绕）⇒ ⊤ 保守（上界标记，不崩溃、不溢出到负，MA-002）。
    public static NatStar operator +(NatStar a, NatStar b)
    {
        if (a.IsTop || b.IsTop) return Top;
        var sum = a.Value + b.Value;                 // 无 checked：ulong 环绕检测
        return sum < a.Value ? Top : Of(sum);        // sum < 任一操作数 ⇒ 环绕 ⇒ 保守 ⊤（非负值，不溢出到负）
    }

    // §3.1.5a 乘法律：x×⊤=⊤(x>0)；0×⊤=⊤（保守标记未知，MA-002）；⊤×⊤=⊤；溢出（ulong 静默环绕）⇒ ⊤ 保守。
    public static NatStar operator *(NatStar a, NatStar b)
    {
        if (a.IsTop || b.IsTop) return Top;
        var prod = a.Value * b.Value;                // 无 checked：ulong 环绕检测
        // 环绕判定：a≠0 且 prod/a ≠ b ⇒ 溢出（prod 已含 2^64 倍数）⇒ 保守 ⊤
        return (a.Value != 0 && prod / a.Value != b.Value) ? Top : Of(prod);
    }

    // §3.1.5a max：max(x,⊤)=⊤；max(⊤,x)=⊤
    public NatStar Max(NatStar o) => (IsTop || o.IsTop) ? Top : Of(Math.Max(Value, o.Value));

    // §3.1.5a min：min(x,⊤)=x；min(⊤,x)=x
    public NatStar Min(NatStar o)
    {
        if (!IsTop && !o.IsTop) return Of(Math.Min(Value, o.Value));
        return IsTop ? o : this; // 一个为 ⊤ 时，min 取另一个（必为有限值或同为 ⊤）
    }

    // §3.1.5a compare：∀x∈ℕ*, x<⊤；⊤=⊤；无 x>⊤（类型不可直接表达，故为注释契约）。
    // 比较帮助器：先判 IsTop 再比较数值，避免任何 NaN。
    public int CompareToFinite(NatStar o)
    {
        if (IsTop && o.IsTop) return 0;
        if (IsTop) return 1;   // ⊤ 最大
        if (o.IsTop) return -1;
        return Value.CompareTo(o.Value);
    }

    /// <summary>§3.1.5a 调试字符串（无代数语义，仅 ⊤ 或数值表示）。</summary>
    public override string ToString() => IsTop ? "⊤" : Value.ToString();
}

/// <summary>
/// §3.1.5 / §3.1.5b — SizeVal 载体 = 区间 [lo, hi]，lo ≤ hi。
/// 单值 s ⇔ [s,s]；缺省 ⇔ [1,1]；动态 Instantiate ⇔ [1,⊤]（§3.1.5(c) 动态项）。
/// merge_I 的 min/max 直接套用 §3.1.5a ⊤ 律（§3.1.5b 注）。
/// </summary>
public readonly record struct Interval
{
    /// <summary>§3.1.5 — 下界（含 ⊤ 表示未知上界）。</summary>
    public NatStar Lo { get; }

    /// <summary>§3.1.5 — 上界（含 ⊤ 表示未知上界）。</summary>
    public NatStar Hi { get; }

    /// <summary>§3.1.5 不变量：lo ≤ hi。下界必须为有限，除非上界同为 ⊤（即 [⊤,⊤] 表示未知区间，合法）。
    /// [x,⊤] 合法（动态 Instantiate [1,⊤]）；[⊤,x]（x 有限）非法（下界不可为 ⊤ 而上界有限）。</summary>
    public Interval(NatStar lo, NatStar hi)
    {
        // PDR §3.1.5：下界必须有限（除非上下界同为 ⊤，表示未知区间）；上界可为 ⊤。
        if (lo.IsTop && !hi.IsTop)
            throw new ArgumentException("Interval lower bound must be finite when upper is finite; lo=⊤ invalid (§3.1.5)");
        if (!lo.IsTop && !hi.IsTop && lo.Value > hi.Value)
            throw new ArgumentException($"Interval lo({lo}) > hi({hi}) violates §3.1.5 lo≤hi");
        Lo = lo;
        Hi = hi;
    }

    /// <summary>§3.1.5(a) 代数缺省 → [1,1]（单值上界保守；AUDIT002/003 启发式仍落 (a)(b)，不另算）。</summary>
    public static readonly Interval Default = new(NatStar.Of(1), NatStar.Of(1));

    /// <summary>§3.1.5(c) 动态 Instantiate 变量场景 ⇔ [1,⊤]。</summary>
    public static readonly Interval Dynamic = new(NatStar.Of(1), NatStar.Top);

    /// <summary>§3.1.5 — 精确单值区间 [s,s]。</summary>
    public static Interval Exact(ulong s) => new(NatStar.Of(s), NatStar.Of(s));

    /// <summary>§3.1.5b merge_I：join-semilattice（幂等/交换/结合），min/max 内嵌 ⊤ 律。</summary>
    public Interval Merge(Interval o) => new(Lo.Min(o.Lo), Hi.Max(o.Hi));

    /// <summary>§3.1.5 调试字符串（无代数语义，仅区间表示）。</summary>
    public override string ToString() => $"[{Lo},{Hi}]";
}

/// <summary>
/// §3.1.5c — DeviationVal = double ∪ {⊤}。⊤ 表示「偏差不可校准/需人工界定」。
/// 比较须先判 IsTop 再比数值（§9.1 修正：原 `deviation > 0.2f` 类型不可比）。
/// </summary>
public readonly record struct DeviationVal
{
    /// <summary>§3.1.5c — 当 true 时表示上界 ⊤ 未知/需人工界定；<see cref="Value"/> 无效。</summary>
    public bool IsTop { get; }

    /// <summary>§3.1.5c — 仅当 !IsTop 有效（有限偏差值）。</summary>
    public double Value { get; }

    private DeviationVal(bool isTop, double value) { IsTop = isTop; Value = value; }

    /// <summary>上界标记：调用方视为需人工界定，不触发普通数值报警（避免掩盖，§8.3.2/§9.1）。</summary>
    public static readonly DeviationVal Top = new(true, 0.0);

    /// <summary>§3.1.5c — 从具体偏差值构造（有限值）。</summary>
    public static DeviationVal Of(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw new ArgumentOutOfRangeException(nameof(v), v, "DeviationVal finite value must be neither NaN nor infinity");
        return new(false, v);
    }

    /// <summary>§9.1 语义：仅当有限值时与阈值比较；IsTop ⇒ 视为需人工界定返回 false（不报警）。</summary>
    public bool ExceedsThreshold(double threshold) => !IsTop && Value > threshold;

    /// <summary>§3.1.5c 调试字符串（无代数语义，仅 ⊤ 或百分比表示）。</summary>
    public override string ToString() => IsTop ? "⊤" : Value.ToString("P");
}
