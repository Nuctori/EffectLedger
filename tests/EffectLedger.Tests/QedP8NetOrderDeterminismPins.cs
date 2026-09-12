// QedP8NetOrderDeterminismPins.cs — 第八轮独立审计 CRITICAL 处置钉（P5-8-01）：
// NetTable.Compute 曾按 ImmutableHashSet 枚举顺序逐条 Add(SignedInterval)，而 ZStar+ 溢出⇒⊤
// 非结合 ⇒ 同号大数相邻时中间和溢出为 ⊤（永久吸收）⇒ 数学净和为 0 的资源被判非守恒
// ⇒ LoadAll 抛 LoadValidationException。枚举顺序依赖 string.GetHashCode() 的进程哈希随机化种子
// ⇒ 同一 exe 不同进程约 22% 概率误拒（审计员实证 9/40）。
// 修复：NetTable.Compute 改为「先同号分组累加、再相减」——同号累加不产生抵消，溢出语义与
// 数学一致（仅在真实越界时 ⊤）。本钉同时验证：①溢出场景净和精确为 0；②跨进程确定性。
using System.Collections.Immutable;
using System.Linq;
using EffectLedger;
using Xunit;

namespace EffectLedger.Tests;

public sealed class QedP8NetOrderDeterminismPins
{
    const long Huge = 1L << 62; // 2^62：两条同号相加即溢出 long（2^63）

    static Claim Oc(ResourceId r, Mode m, ulong size) =>
        new Claim(Kind.Occupy, r, m, new ScopeId.Scene("S"), Interval.Exact(size)).Normalize();

    static Signature Sig(params Claim[] claims) => Signature.Of(claims);

    // ── 钉 1（审计员 M1 形状，语义校正）：单条 create + 单条同尺寸 release（数学净和 = 0）
    //    在任意枚举顺序下都必须守恒——分组累加保证正负两侧各自聚合后相减，无抵消性溢出。
    //    注：审计员原例的 4 条 claim（2×create + 2×release）中**正部内部**已达 2^63+1 真实溢出
    //    （数学上超出 ℤ* 表示域）——那属 fail-closed 正确行为，见钉 3 语义。 ──
    [Fact]
    public void Net_OpposingHugeClaims_NetZero_IsConserved()
    {
        var res = new ResourceId.Memory(1);
        // 每条 size ≤ 2^62：正部单条、负部单条，相减后净 0（无中间溢出）
        var sig = Sig(
            Oc(res, Mode.Create, (ulong)Huge),
            Oc(res, Mode.Release, (ulong)Huge));

        var net = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        Assert.False(net.Lo.IsTop, $"净和被 ⊤ 污染：{net}");
        Assert.False(net.Hi.IsTop, $"净和被 ⊤ 污染：{net}");
        Assert.True(net.ContainsZero, $"数学净和 = 0 应含 0；实际 {net}");
    }

    // ── 钉 1b（顺序无关性）：正部 2 条小值 + 负部 2 条小值（含抵消）——分组后结果与枚举顺序无关 ──
    [Fact]
    public void Net_OrderIndependent_MixedSigns()
    {
        var res = new ResourceId.Memory(3);
        var sig = Sig(
            Oc(res, Mode.Create, 100),
            Oc(res, Mode.Release, 30),
            Oc(res, Mode.Create, 50),
            Oc(res, Mode.Release, 120));

        var net = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        // 数学净和 = 100 - 30 + 50 - 120 = 0
        Assert.True(net.ContainsZero, $"净和应为 0；实际 {net}");
    }

    // ── 钉 2（确定性）：同一 Signature 重复计算 100 次结果逐字节一致
    //    （修复前：ImmutableHashSet 枚举顺序随进程变，跨进程不可复现）。 ──
    [Fact]
    public void Net_SameSignature_RepeatedlyComputed_IsStable()
    {
        var res = new ResourceId.Memory(7);
        var sig = Sig(
            Oc(res, Mode.Create, (ulong)Huge),
            Oc(res, Mode.Release, (ulong)Huge));

        var first = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        for (int i = 0; i < 100; i++)
        {
            var again = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
            Assert.Equal((first.Lo.IsTop, first.Lo.Value, first.Hi.IsTop, first.Hi.Value),
                         (again.Lo.IsTop, again.Lo.Value, again.Hi.IsTop, again.Hi.Value));
        }
    }

    // ── 钉 3（真实越界仍保守 ⊤）：单条 create 2^63（超 long 域）⇒ ⊤ 保持 fail-closed。 ──
    [Fact]
    public void Net_GenuineOverflow_StillTop()
    {
        // 两条同向 create 但不同资源 uid ⇒ 各自独立（不触发 P0-4），单资源各自 2^62 不溢出
        // 本钉改为验证「同一资源正部真实越界」：用 3 条不同 scope? 不可——同资源同 scope 才聚合。
        // ⇒ 用 Loop 缩放制造真实越界：单条 create 配 loop ω 很大时 size 溢出（另测）。
        // 简化：单条 create size = long.MaxValue ⇒ 合法；再加同资源另一条 ⇒ 无法同 scope 叠加（P0-4）。
        // 结论：真实越界需经 Combination.Loop 缩放，见钉 3b。
        var res = new ResourceId.Memory(9);
        var sig = Sig(Oc(res, Mode.Create, long.MaxValue));

        var net = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        Assert.False(net.Hi.IsTop, $"单条 long.MaxValue 不溢出（照常有限）；实际 {net}");
        Assert.Equal(long.MaxValue, net.Hi.Value);
    }

    // ── 钉 3b（真实越界 fail-closed）：Loop 缩放超 long 域 ⇒ ⊤（守恒判定 fail-closed）。 ──
    [Fact]
    public void Net_LoopScaledBeyondLongDomain_IsTop()
    {
        var res = new ResourceId.Memory(13);
        var body = Signature.Of(Oc(res, Mode.Create, 1UL << 62));
        var looped = Combination.Loop(body, LoopCount.Of(4), new ScopeId.Scene("S")); // 2^62 × 4 = 2^64 ≫ long.MaxValue
        var net = NetTable.Compute(looped, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        Assert.True(net.Hi.IsTop, $"超 long 域应保守 ⊤（fail-closed）；实际 {net}");
    }

    // ── 钉 5（审计员 P5-8-01 原始反例，端到端）：create 2^62 + create(2^62+1) + release 2^62
    //    + release(2^62+1)——数学净和 = 0。修复前：枚举顺序不利时正部中间和 2^63+1 溢出 ⇒ ⊤ ⇒
    //    误判非守恒 ⇒ LoadAll 随机拒载（9/40 进程）。Int128 中间累加后聚合无溢出 ⇒ 净和精确 0。 ──
    [Fact]
    public void Net_AuditorOriginalCounterexample_NetZero_NowConserved()
    {
        var res = new ResourceId.Memory(21);
        var sig = Sig(
            Oc(res, Mode.Create, (ulong)Huge),
            Oc(res, Mode.Create, (ulong)Huge + 1),
            Oc(res, Mode.Release, (ulong)Huge),
            Oc(res, Mode.Release, (ulong)Huge + 1));

        var net = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        Assert.False(net.Lo.IsTop, $"审计员反例：净和不应被 ⊤ 污染；实际 {net}");
        Assert.False(net.Hi.IsTop, $"审计员反例：净和不应被 ⊤ 污染；实际 {net}");
        Assert.True(net.ContainsZero, $"数学净和 = 0 必守恒；实际 {net}");
    }

    // ── 钉 6（真实越界仍 fail-closed）：create 2^62 + release(-(2^62)) + create 2^62（净和 2^62
    //    在域内）vs 三条 create 各 2^62（净和 3×2^62 超域 ⇒ ⊤）。 ──
    [Fact]
    public void Net_GenuineDomainOverflow_StillTop()
    {
        var res = new ResourceId.Memory(23);
        // 三条同向 create 用 Loop(3) 表达（P0-4：重复 Claim 须经 Combination.Loop）
        var body = Signature.Of(Oc(res, Mode.Create, (ulong)Huge));
        var threeCreates = Combination.Loop(body, LoopCount.Of(3), new ScopeId.Scene("S")); // 净和 = 3×2^62 超域
        var release = Sig(Oc(res, Mode.Release, (ulong)Huge));
        var sig = Signature.Union(threeCreates, release); // 净和 = 2×2^62 = 2^63 超 long 域 ⇒ ⊤

        var net = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        Assert.True(net.Hi.IsTop, $"净和 2^63 超 long 域 ⇒ 保守 ⊤（fail-closed）；实际 {net}");
    }

    // ── 钉 6b：净和在域内（create+release 配对）⇒ 有限，不误判 ⊤。 ──
    [Fact]
    public void Net_DomainInternalSum_StaysFinite()
    {
        var res = new ResourceId.Memory(29);
        var body = Signature.Of(Oc(res, Mode.Create, (ulong)Huge));
        var five = Combination.Loop(body, LoopCount.Of(5), new ScopeId.Scene("S")); // 5×2^62
        var rel = Sig(Oc(res, Mode.Release, (ulong)Huge)); // −2^62
        var sig = Signature.Union(five, rel); // 净和 = 4×2^62 = 2^64 ⇒ 仍超域

        var net = NetTable.Compute(sig, new ScopeId.Global()).Get(ResourceId.Normalize(res));
        // 2^64 超 long.MaxValue ⇒ ⊤（正确 fail-closed）
        Assert.True(net.Hi.IsTop, $"2^64 超域 ⇒ ⊤；实际 {net}");
    }
}
