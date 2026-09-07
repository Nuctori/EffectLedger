// QedP3D5ConformanceTests.cs — P3-D5 实现对照性质测试（反例直报）：
// 被测 C# 实现（NatStar/Interval/ScopeId/Compatible）对照「形式规约的独立直译 oracle」。
// oracle 为 Dafny 模型（formal/CosmosEffectAlgebra.dfy / CosmosSweepLine.dfy）的 C# 转写，
// 刻意用不同实现技巧（数学域判定而非环绕检测）——两侧独立，吻合即实现==规约的证据，分歧即反例。
// 生成策略：边界偏置集（0/1/2/MAX/2⁶³…）× 固定种子随机（可复现；失败即输出精确反例对）。
// 对应定律：Add/Mul ⊤ 闭合+溢出⇒⊤+交换（D1）/ Merge 半格+不变量（D1）/ IncludedIn 单向偏序（D2）/
// Compatible 25 组合 + Unknown→Use（D2）。xUnit。
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedP3D5ConformanceTests
{
    // ── oracle：Dafny 模型直译（独立第二实现）──
    static bool ModelLe(bool aT, ulong a, bool bT, ulong b) => bT || (!aT && a <= b);
    static (bool T, ulong V) ModelAdd(bool aT, ulong a, bool bT, ulong b)
        => (aT || bT) ? (true, 0UL)
        : (a > ulong.MaxValue - b) ? (true, 0UL)      // x+y > MAX 的无溢出域内判定
        : (false, a + b);
    static (bool T, ulong V) ModelMul(bool aT, ulong a, bool bT, ulong b)
        => (aT || bT) ? (true, 0UL)
        : (a != 0 && b > ulong.MaxValue / a) ? (true, 0UL)  // a×b > MAX ⇔ b > MAX/a（a≠0）
        : (false, a * b);
    static (bool T, ulong V) ModelMin(bool aT, ulong a, bool bT, ulong b) => ModelLe(aT, a, bT, b) ? (aT, a) : (bT, b);
    static (bool T, ulong V) ModelMax(bool aT, ulong a, bool bT, ulong b) => ModelLe(aT, a, bT, b) ? (bT, b) : (aT, a);

    static bool ModelIncludedIn(Cosmos.EffectAlgebra.ScopeId a, Cosmos.EffectAlgebra.ScopeId b)
        => a.Equals(b) || b is Cosmos.EffectAlgebra.ScopeId.Global;

    // Compatible 的 D2 直译：Unknown→Use 解析 + CONFLICT 同类自冲突
    static Cosmos.EffectAlgebra.Mode ModelResolve(Cosmos.EffectAlgebra.Mode m)
        => m == Cosmos.EffectAlgebra.Mode.Unknown ? Cosmos.EffectAlgebra.Mode.Use : m;
    static bool ModelInConflict(Cosmos.EffectAlgebra.Mode a, Cosmos.EffectAlgebra.Mode b)
    {
        var ra = ModelResolve(a);
        var rb = ModelResolve(b);
        return ra == rb && (ra == Cosmos.EffectAlgebra.Mode.Create
            || ra == Cosmos.EffectAlgebra.Mode.Move || ra == Cosmos.EffectAlgebra.Mode.Release);
    }

    // ── 生成器：边界偏置 + 固定种子随机（可复现；失败即反例直报） ──
    static readonly ulong[] Boundary =
    {
        0UL, 1UL, 2UL, ulong.MaxValue / 4, ulong.MaxValue / 2, 1UL << 62, (1UL << 63), (1UL << 63) + 1,
        ulong.MaxValue - 1, ulong.MaxValue
    };

    static IEnumerable<ulong> SampleValues(Random rng)
    {
        foreach (var v in Boundary) yield return v;
        for (int i = 0; i < 100; i++) yield return (ulong)rng.NextInt64(0, long.MaxValue) ^ ((ulong)rng.Next(0, 2) << 63);
    }

    // ── 对照 1/2：NatStar Add/Mul ⊤ 闭合 + 溢出⇒⊤（含 ⊤ 输入与全部边界值）──
    [Theory]
    [InlineData(false)] // Add
    [InlineData(true)]  // Mul
    public void NatStar_Arithmetic_ConformsToFormalModel(bool useMul)
    {
        var rng = new Random(23);
        var values = SampleValues(rng).ToArray();
        var tops = new[] { (true, 0UL), (false, 7UL), (false, ulong.MaxValue) };

        foreach (var (aT, a) in tops)
            foreach (var (bT, b) in tops)
            {
                var impl = useMul
                    ? CSharp(aT, a) * CSharp(bT, b)
                    : CSharp(aT, a) + CSharp(bT, b);
                var oracle = useMul ? ModelMul(aT, a, bT, b) : ModelAdd(aT, a, bT, b);
                Assert.True((impl.IsTop, impl.Value) == oracle,
                    $"反例 {Op(useMul)}({Show(aT, a)}, {Show(bT, b)})：实现={Show(impl.IsTop, impl.Value)} 规约={Show(oracle.T, oracle.V)}");
            }

        foreach (var a in values)
            foreach (var b in new[] { 0UL, 1UL, ulong.MaxValue, values[rng.Next(values.Length)] })
            {
                var impl = useMul
                    ? Cosmos.EffectAlgebra.NatStar.Of(a) * Cosmos.EffectAlgebra.NatStar.Of(b)
                    : Cosmos.EffectAlgebra.NatStar.Of(a) + Cosmos.EffectAlgebra.NatStar.Of(b);
                var oracle = useMul ? ModelMul(false, a, false, b) : ModelAdd(false, a, false, b);
                Assert.True((impl.IsTop, impl.Value) == oracle,
                    $"反例 {Op(useMul)}({a}, {b})：实现={Show(impl.IsTop, impl.Value)} 规约={Show(oracle.T, oracle.V)}");
            }
    }

    // ── 对照 3：Interval Merge == [min(lo), max(hi)] + 不变量保持（随机合法区间对）──
    [Fact]
    public void Interval_Merge_ConformsToFormalModel()
    {
        var rng = new Random(23);
        for (int iter = 0; iter < 200; iter++)
        {
            var (aT, aV) = RandNat(rng);
            var (bT, bV) = RandNat(rng);
            var (cT, cV) = RandNat(rng);
            var (dT, dV) = RandNat(rng);
            var x = ValidInterval(aT, aV, bT, bV);  // [ (aT,aV), (bT,bV) ]（构造器拒绝非法序）
            var y = ValidInterval(cT, cV, dT, dV);

            var merged = x.Merge(y);
            var (mLo, mHi) = ModelMerge(
                (x.Lo.IsTop, x.Lo.Value), (x.Hi.IsTop, x.Hi.Value),
                (y.Lo.IsTop, y.Lo.Value), (y.Hi.IsTop, y.Hi.Value));

            Assert.Equal(mLo, (merged.Lo.IsTop, merged.Lo.Value));
            Assert.Equal(mHi, (merged.Hi.IsTop, merged.Hi.Value));
            Assert.True(merged.Lo.CompareToFinite(merged.Hi) <= 0, $"反例 Merge 破坏不变量：Merge({x}, {y}) = {merged}");
        }
    }

    // ── 对照 4：ScopeId IncludedIn == 单向包含语义（8 构造子随机对）──
    [Fact]
    public void Scope_IncludedIn_ConformsToFormalModel()
    {
        var rng = new Random(23);
        var scopes = new (Cosmos.EffectAlgebra.ScopeId s, string kind)[]
        {
            (new Cosmos.EffectAlgebra.ScopeId.Method("a"), "Method"),
            (new Cosmos.EffectAlgebra.ScopeId.Type("a"), "Type"),
            (new Cosmos.EffectAlgebra.ScopeId.Scene("a"), "Scene"),
            (new Cosmos.EffectAlgebra.ScopeId.Global(), "Global"),
            (new Cosmos.EffectAlgebra.ScopeId.Loop("a"), "Loop"),
            (new Cosmos.EffectAlgebra.ScopeId.Conditional("a"), "Conditional"),
            (new Cosmos.EffectAlgebra.ScopeId.Async("a"), "Async"),
            (new Cosmos.EffectAlgebra.ScopeId.Shell(), "Shell"),
        };
        foreach (var (a, ka) in scopes)
            foreach (var (b, kb) in scopes)
            {
                var impl = a.IncludedIn(b);
                var oracle = ModelIncludedIn(a, b);
                Assert.True(impl == oracle, $"反例 IncludedIn({ka}, {kb})：实现={impl} 规约={oracle}");
            }
    }

    // ── 对照 5：Compatible 25 组合 == D2 直译（Unknown→Use + CONFLICT 同类自冲突）──
    [Fact]
    public void Compatible_ConformsToFormalModel()
    {
        var modes = new[]
        {
            Cosmos.EffectAlgebra.Mode.Use, Cosmos.EffectAlgebra.Mode.Create,
            Cosmos.EffectAlgebra.Mode.Release, Cosmos.EffectAlgebra.Mode.Move,
            Cosmos.EffectAlgebra.Mode.Unknown
        };
        foreach (var a in modes)
            foreach (var b in modes)
            {
                var impl = Cosmos.EffectAlgebra.Compatible.IsCompatible(a, b);
                var oracle = !ModelInConflict(a, b);
                Assert.True(impl == oracle, $"反例 Compatible({a}, {b})：实现={impl} 规约={oracle}");
            }
    }

    // ── helpers ──
    static Cosmos.EffectAlgebra.NatStar CSharp(bool t, ulong v)
        => t ? Cosmos.EffectAlgebra.NatStar.Top : Cosmos.EffectAlgebra.NatStar.Of(v);
    static string Show(bool t, ulong v) => t ? "⊤" : v.ToString();
    static string Op(bool useMul) => useMul ? "Mul" : "Add";

    static (bool, ulong) RandNat(Random rng)
        => rng.Next(4) switch
        {
            0 => (false, (ulong)rng.NextInt64(0, 1024)),            // 小值
            1 => (false, ulong.MaxValue - (ulong)rng.Next(0, 4)),   // 上界邻域
            2 => (true, 0),                                          // ⊤
            _ => (false, (ulong)rng.NextInt64(0, long.MaxValue)),    // 一般随机
        };

    static Cosmos.EffectAlgebra.Interval ValidInterval(bool loT, ulong loV, bool hiT, ulong hiV)
    {
        // 构造器拒绝 [⊤, finite] 与 lo > hi——保证传入合法（模型 Valid 前提）
        if (loT && !hiT) hiT = true;                     // [⊤,⊤]
        if (!loT && !hiT && loV > hiV) (loV, hiV) = (hiV, loV);
        return new Cosmos.EffectAlgebra.Interval(
            loT ? Cosmos.EffectAlgebra.NatStar.Top : Cosmos.EffectAlgebra.NatStar.Of(loV),
            hiT ? Cosmos.EffectAlgebra.NatStar.Top : Cosmos.EffectAlgebra.NatStar.Of(hiV));
    }

    // Dafny Merge 直译：[min(lo₁,lo₂), max(hi₁,hi₂)]
    static ((bool, ulong) Lo, (bool, ulong) Hi) ModelMerge(
        (bool T, ulong V) xLo, (bool T, ulong V) xHi,
        (bool T, ulong V) yLo, (bool T, ulong V) yHi)
        => (ModelMin(xLo.T, xLo.V, yLo.T, yLo.V), ModelMax(xHi.T, xHi.V, yHi.T, yHi.V));
}
