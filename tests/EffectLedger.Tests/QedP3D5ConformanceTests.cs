// QedP3D5ConformanceTests.cs — P3-D5 实现对照性质测试（反例直报）：
// 被测 C# 实现（NatStar/Interval/ScopeId/Compatible）对照「形式规约的独立直译 oracle」。
// oracle 为 Dafny 模型（formal/EffectLedger.dfy / EffectLedgerSweepLine.dfy）的 C# 转写，
// 刻意用不同实现技巧（数学域判定而非环绕检测）——两侧独立，吻合即实现==规约的证据，分歧即反例。
// 生成策略：边界偏置集（0/1/2/MAX/2⁶³…）× 固定种子随机（可复现；失败即输出精确反例对）。
// 对应定律：Add/Mul ⊤ 闭合+溢出⇒⊤+交换（D1）/ Merge 半格+不变量（D1）/ IncludedIn 单向偏序（D2）/
// Compatible 25 组合 + Unknown→Use（D2）。xUnit。
using Xunit;

namespace EffectLedger.Tests;

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

    static bool ModelIncludedIn(EffectLedger.ScopeId a, EffectLedger.ScopeId b)
        => a.Equals(b) || b is EffectLedger.ScopeId.Global;

    // Compatible 的 D2 直译：Unknown→Use 解析 + CONFLICT 同类自冲突
    static EffectLedger.Mode ModelResolve(EffectLedger.Mode m)
        => m == EffectLedger.Mode.Unknown ? EffectLedger.Mode.Use : m;
    static bool ModelInConflict(EffectLedger.Mode a, EffectLedger.Mode b)
    {
        var ra = ModelResolve(a);
        var rb = ModelResolve(b);
        return ra == rb && (ra == EffectLedger.Mode.Create
            || ra == EffectLedger.Mode.Move || ra == EffectLedger.Mode.Release);
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
                    ? EffectLedger.NatStar.Of(a) * EffectLedger.NatStar.Of(b)
                    : EffectLedger.NatStar.Of(a) + EffectLedger.NatStar.Of(b);
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
        var scopes = new (EffectLedger.ScopeId s, string kind)[]
        {
            (new EffectLedger.ScopeId.Method("a"), "Method"),
            (new EffectLedger.ScopeId.Type("a"), "Type"),
            (new EffectLedger.ScopeId.Scene("a"), "Scene"),
            (new EffectLedger.ScopeId.Global(), "Global"),
            (new EffectLedger.ScopeId.Loop("a"), "Loop"),
            (new EffectLedger.ScopeId.Conditional("a"), "Conditional"),
            (new EffectLedger.ScopeId.Async("a"), "Async"),
            (new EffectLedger.ScopeId.Shell(), "Shell"),
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
            EffectLedger.Mode.Use, EffectLedger.Mode.Create,
            EffectLedger.Mode.Release, EffectLedger.Mode.Move,
            EffectLedger.Mode.Unknown
        };
        foreach (var a in modes)
            foreach (var b in modes)
            {
                var impl = EffectLedger.Compatible.IsCompatible(a, b);
                var oracle = !ModelInConflict(a, b);
                Assert.True(impl == oracle, $"反例 Compatible({a}, {b})：实现={impl} 规约={oracle}");
            }
    }

    // ── helpers ──
    static EffectLedger.NatStar CSharp(bool t, ulong v)
        => t ? EffectLedger.NatStar.Top : EffectLedger.NatStar.Of(v);
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

    static EffectLedger.Interval ValidInterval(bool loT, ulong loV, bool hiT, ulong hiV)
    {
        // 构造器拒绝 [⊤, finite] 与 lo > hi——保证传入合法（模型 Valid 前提）
        if (loT && !hiT) hiT = true;                     // [⊤,⊤]
        if (!loT && !hiT && loV > hiV) (loV, hiV) = (hiV, loV);
        return new EffectLedger.Interval(
            loT ? EffectLedger.NatStar.Top : EffectLedger.NatStar.Of(loV),
            hiT ? EffectLedger.NatStar.Top : EffectLedger.NatStar.Of(hiV));
    }

    // Dafny Merge 直译：[min(lo₁,lo₂), max(hi₁,hi₂)]
    static ((bool, ulong) Lo, (bool, ulong) Hi) ModelMerge(
        (bool T, ulong V) xLo, (bool T, ulong V) xHi,
        (bool T, ulong V) yLo, (bool T, ulong V) yHi)
        => (ModelMin(xLo.T, xLo.V, yLo.T, yLo.V), ModelMax(xHi.T, xHi.V, yHi.T, yHi.V));

    // ═══ P5.3-H3 扩展：SignedNet/守恒维度对照（红队 H3「56 条定律无 C# 对照桥」的回填第一批）═══
    // oracle 独立性：数学域饱和判定（b>0 ? a > MAX−b : a < MIN−b），与实现的环绕/符号检测不同技巧。

    static bool ModelZInRange(long v) => v >= long.MinValue && v <= long.MaxValue;

    // D3 ZAdd 直译：任一 ⊤ ⇒ ⊤；有限和越 ℤ* 值域 ⇒ ⊤；否则精确和
    static (bool T, long V) ModelZAdd(bool aT, long a, bool bT, long b)
    {
        if (aT || bT) return (true, 0);
        // 无溢出的数学域判定：b>0 ⇔ 检查 a > MAX−b；b<0 ⇔ 检查 a < MIN−b；b==0 ⇒ a
        var overflow = b > 0 ? a > long.MaxValue - b : a < long.MinValue - b;
        return overflow ? (true, 0) : (false, a + b);
    }

    static readonly long[] ZBoundary =
    {
        0, 1, -1, 2, -2, long.MaxValue, long.MinValue, long.MaxValue - 1, long.MinValue + 1,
        long.MaxValue / 2, long.MinValue / 2
    };

    static IEnumerable<long> ZSample(Random rng)
    {
        foreach (var v in ZBoundary) yield return v;
        for (int i = 0; i < 60; i++) yield return rng.NextInt64(long.MinValue + 1, long.MaxValue);
    }

    // ── 对照 6：ZStar 加法 ⊤ 闭合 + 饱和（D3 ZAdd 定律的 C# 对照；含 ±MAX 边界与随机域）──
    [Fact]
    public void ZStar_Add_ConformsToFormalModel()
    {
        var rng = new Random(23);
        foreach (var a in ZSample(rng))
            foreach (var b in new[] { 0L, 1L, -1L, long.MaxValue, long.MinValue, ZSample(rng).First() })
            {
                var impl = EffectLedger.ZStar.Of(a) + EffectLedger.ZStar.Of(b);
                var oracle = ModelZAdd(false, a, false, b);
                Assert.True((impl.IsTop, impl.Value) == oracle,
                    $"反例 ZAdd({a}, {b})：实现=({impl.IsTop}, {impl.Value}) 规约={oracle}");
            }

        // ⊤ 闭合
        Assert.True((EffectLedger.ZStar.Top + EffectLedger.ZStar.Zero).IsTop);
        Assert.True((EffectLedger.ZStar.Zero + EffectLedger.ZStar.Top).IsTop);
    }

    // ── 对照 7：SignedInterval Add + ContainsZero（守恒 ⇔ 含 0，任一端 ⊤ fail-closed）──
    [Fact]
    public void SignedInterval_Add_And_ContainsZero_Conform()
    {
        var rng = new Random(23);
        for (int iter = 0; iter < 300; iter++)
        {
            long a = rng.NextInt64(long.MinValue + 1, long.MaxValue), b = rng.NextInt64(long.MinValue + 1, long.MaxValue);
            if (a > b) (a, b) = (b, a);
            long c = rng.NextInt64(long.MinValue + 1, long.MaxValue), d = rng.NextInt64(long.MinValue + 1, long.MaxValue);
            if (c > d) (c, d) = (d, c);

            var x = new EffectLedger.SignedInterval(EffectLedger.ZStar.Of(a), EffectLedger.ZStar.Of(b));
            var y = new EffectLedger.SignedInterval(EffectLedger.ZStar.Of(c), EffectLedger.ZStar.Of(d));

            var impl = x.Add(y);
            // oracle：数学域内逐端相加（输入端点已在 long 域内 ⇒ 和可能越域 ⇒ oracle 饱和判定）
            var loSum = ModelZAdd(false, a, false, c);
            var hiSum = ModelZAdd(false, b, false, d);
            Assert.Equal((loSum.T, loSum.V), (impl.Lo.IsTop, impl.Lo.Value));
            Assert.Equal((hiSum.T, hiSum.V), (impl.Hi.IsTop, impl.Hi.Value));

            // ContainsZero：任一端 ⊤ ⇒ false（fail-closed）；否则 lo ≤ 0 ≤ hi
            var expectCz = !impl.Lo.IsTop && !impl.Hi.IsTop && impl.Lo.Value <= 0 && impl.Hi.Value >= 0;
            Assert.Equal(expectCz, impl.ContainsZero);
        }

        // 精确配对回归：create(+8) 与 release(−8) 的区间和必含 0（DO-9 不报警的数学根据）
        var create = new EffectLedger.SignedInterval(EffectLedger.ZStar.Of(8), EffectLedger.ZStar.Of(8));
        var release = new EffectLedger.SignedInterval(EffectLedger.ZStar.Of(-8), EffectLedger.ZStar.Of(-8));
        Assert.True(create.Add(release).ContainsZero);
    }

    // ── 对照 8：NetTable == 逐 claim 符号求和的暴力 oracle（create/release 抵消语义的 D5 桥）──
    [Fact]
    public void NetTable_ConformsToBruteForceSignedSum()
    {
        var rng = new Random(23);
        var scope = new EffectLedger.ScopeId.Global();
        for (int iter = 0; iter < 200; iter++)
        {
            var claims = new System.Collections.Generic.List<EffectLedger.Claim>();
            long brute = 0;
            int n = rng.Next(1, 8);
            for (int i = 0; i < n; i++)
            {
                var sz = (ulong)(i + 1) * 2; // 互异 size：防 P0-4 重复 Claim 拒（同五元组会 loud 抛）
                var mode = rng.Next(2) == 0 ? EffectLedger.Mode.Create : EffectLedger.Mode.Release;
                claims.Add(new EffectLedger.Claim(EffectLedger.Kind.Occupy,
                    new EffectLedger.ResourceId.Memory(1), mode,
                    new EffectLedger.ScopeId.Scene("S"), EffectLedger.Interval.Exact(sz)).Normalize());
                brute += mode == EffectLedger.Mode.Release ? -(long)sz : (long)sz;
            }
            var sig = EffectLedger.Signature.Of(claims.ToArray());
            var net = EffectLedger.NetTable.Compute(sig, scope).Get(EffectLedger.ResourceId.Normalize(new EffectLedger.ResourceId.Memory(1)));

            Assert.Equal(brute, net.Lo.Value);
            Assert.Equal(brute, net.Hi.Value);
        }
    }
}
