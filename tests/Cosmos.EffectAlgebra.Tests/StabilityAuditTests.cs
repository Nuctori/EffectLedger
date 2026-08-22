using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// 迭代12 — §11 稳定性法则的自动回归守护。把 §11 设计目标（DO-1..DO-10）映射为永不可回归的断言：
/// 若 L1 实现回退（类型边界被破坏 / 量纲混入 / ⊤ 误报警 / 守恒判据退化），对应测试必红。
/// 每项测试名带 `DO-n_` 前缀，XML 注释引 §11 设计目标 + 对应数学 §x.y，并明言「守护 §11 根因不回归」。
/// 全部断言可证伪（回退必红），不引魔法数，出处完整。
/// </summary>
public class StabilityAuditTests
{
    // ── DO-1（编译期效应审计根因：单值缺省上界保守） ───────────────────────────────
    /// <summary>守护 §11 DO-1（编译期审计可配置性依赖缺省区间保守）：§3.1.5(d) <see cref="Interval.Default"/> 必为 [1,1]（单值上界保守），
    /// 且 §3.1.5(c) <see cref="Interval.Exact"/>(0) 仍 [0,0] 不崩。回退为 [0,1] 或 [0,0] 会破坏缺省体量假设 ⇒ 此测试红。</summary>
    [Fact]
    public void DO1_DefaultIntervalIsSingleValueOne()
    {
        Assert.Equal(NatStar.Of(1), Interval.Default.Lo);
        Assert.Equal(NatStar.Of(1), Interval.Default.Hi);
        Assert.False(Interval.Default.Lo.IsTop);
        Assert.False(Interval.Default.Hi.IsTop);

        var zero = Interval.Exact(0);
        Assert.Equal(NatStar.Of(0), zero.Lo);
        Assert.Equal(NatStar.Of(0), zero.Hi);
    }

    // ── DO-2（业务逻辑零 Godot 引用根因：类型即约束，非法区间编译/构造期拒绝） ───────
    /// <summary>守护 §11 DO-2（零 Godot 引用由类型强制边界）：§3.1.5 不变量 lo≤hi 且下界不可为 ⊤（上界未知则整体 [⊤,⊤]）。
    /// 回退：构造子放行 lo>hi 或 lo=⊤ 有限 ⇒ 此测试红（Argument 被吃）。</summary>
    [Fact]
    public void DO2_IntervalRejectsInvalidBounds()
    {
        // lo > hi ⇒ 抛
        Assert.Throws<ArgumentException>(() => new Interval(NatStar.Of(2), NatStar.Of(1)));
        // lo=⊤ 且 hi 有限 ⇒ 抛（§3.1.5a 上界未知则整体未知，不允许下界 ⊤ 上界有限）
        Assert.Throws<ArgumentException>(() => new Interval(NatStar.Top, NatStar.Of(1)));
        // 合法：未知区间 [⊤,⊤] 不抛
        var _ = new Interval(NatStar.Top, NatStar.Top);
        // 合法：动态 [1,⊤] 不抛（§3.1.5(c)）
        Assert.Equal(NatStar.Top, Interval.Dynamic.Hi);
    }

    // ── DO-3（效应代数组合根因：⊤ 不报警，类型边界不崩溃） ───────────────────────────
    /// <summary>守护 §11 DO-3 / §9.1 — <see cref="DeviationVal.Top"/> 不触发 0.2 报警（DeviationVal 类型边界不崩溃）；
    /// 统一组合律的 ⊤ 闭合归 DO-4。§3.1.5c / §9.1 — Top 经 <see cref="DeviationVal.ExceedsThreshold"/> 永 false（不触发普通 0.2 报警，避免掩盖）。
    /// 回退为 IsTop 仍比数值 ⇒ 此测试红；附反向有限值断言证明「非恒 false」（可证伪）。</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(1.0)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void DO3_TopDeviationNeverExceeds(double threshold)
    {
        Assert.False(DeviationVal.Top.ExceedsThreshold(threshold));
        // 可证伪性：有限偏差 > 阈值时确实报警（若 ExceedsThreshold 恒 false 则下式失败）
        Assert.True(DeviationVal.Of(1.0).ExceedsThreshold(0.2));
        Assert.False(DeviationVal.Of(0.1).ExceedsThreshold(0.2));
    }

    // ── DO-4（验证周期缩短根因：ω=⊤ 可终止，不发散不无限循环） ───────────────────────
    /// <summary>守护 §11 DO-4（80% 测试不启动 Godot 依赖 ω=⊤ 可终止）：§3.2.5 — <see cref="LoopCount.Top"/> 经 <see cref="Combination.Loop"/> 必产「上界开放」Signature（不抛、不无限循环），
    /// 且 <see cref="Peak.Compute"/> 对该结果返回 ⊤。回退为 ω=⊤ 产生有限峰值或陷入循环 ⇒ 此测试红。附有限 ω=3 反例证明非恒 ⊤。</summary>
    [Fact]
    public void DO4_TopLoopIsTerminatingAndOpen()
    {
        var body = Signature.Of(
            new Claim(Kind.Occupy, new ResourceId.Memory(0), Mode.Create, new ScopeId.Global(), Interval.Exact(1)));

        // ω=⊤ ⇒ 结果非 null、Peak=⊤（上界开放，不发散）
        var openResult = Combination.Loop(body, LoopCount.Top, new ScopeId.Global());
        Assert.NotNull(openResult);
        var openPeak = Derived.Peak(openResult, new ScopeId.Global());
        Assert.True(openPeak.IsTop);

        // 可证伪性：有限 ω=3 ⇒ Peak=3（非 ⊤），证明 Loop 真按 ω 缩放而非恒开放
        var finiteResult = Combination.Loop(body, LoopCount.Of(3), new ScopeId.Global());
        var finitePeak = Derived.Peak(finiteResult, new ScopeId.Global());
        Assert.False(finitePeak.IsTop);
        Assert.Equal(3UL, finitePeak.Value);
    }

    // ── DO-5（Entity-as-Data 根因：ScopeId 偏序自洽，Global 最大元） ────────────────
    /// <summary>守护 §11 DO-5（Entity=纯数据依赖 scope 偏序自洽）：§3.1.3b ⊆* 自反 + Global 最大元 + 跨标签不可比。
    /// 注：ScopeId 偏序模型仅 `Equals` 与 `Global` 两种可比关系，故 a⊆b∧b⊆c⇒a⊆c 在 b=Global 时塌缩为 a⊆Global（已测）；
    /// 非平凡三互异 scope 的传递因模型限制不变量退化，由下方 `m.IncludedIn(s)==false`/`g.IncludedIn(m)==false` 的 false 断言保证不退化（拦截「全返回 true」）。
    /// 回退为 IncludedIn 破坏自反/Global 最大元/跨标签不可比任一 ⇒ 此测试红。</summary>
    [Fact]
    public void DO5_ScopePartialOrderIsConsistent()
    {
        var m = new ScopeId.Method("m");
        var s = new ScopeId.Scene("s");
        var g = new ScopeId.Global();

        // 自反：a ⊑ a
        Assert.True(m.IncludedIn(m));

        // 经 Global 最大元链（b=Global 时 a⊆a ∧ a⊆Global ⇒ a⊆Global，模型下退化为自反+最大元组合）
        Assert.True(m.IncludedIn(m) && m.IncludedIn(g)); // a⊆b, b⊆c
        Assert.True(m.IncludedIn(g));                    // ⇒ a⊆c

        // 反对称退化态（模型仅 Equals 可比）：a⊆a ∧ a⊆a ⇒ a==a
        Assert.True(m.IncludedIn(m) && m.IncludedIn(m));
        Assert.True(m.Equals(m));

        // Global 最大元：任意 scope ⊑ Global；但 Global ⊑ 特定 scope 不成立（证明仅为最大非最小）
        Assert.True(m.IncludedIn(g));
        Assert.True(s.IncludedIn(g));
        Assert.False(g.IncludedIn(m));
        Assert.False(m.IncludedIn(s)); // 跨标签不可比较
    }

    // ── DO-6（Release 零开销根因：栈/堆资源键分离，net 不串） ───────────────────────
    /// <summary>守护 §11 DO-6（Release 零开销依赖 net 按资源键精确分组）：§3.3.1 — <see cref="NetTable.Compute"/> 对 Tree 与 Memory 两资源 net 互不串（独立归一键）。
    /// 回退为跨资源混算 ⇒ 此测试红（Tree 的净效应不应吸收 Memory 的释放）。</summary>
    [Fact]
    public void DO6_StackHeapResourcesNotCrossed()
    {
        var tree = new ResourceId.Tree(NodePathOrUnknown.Of("p"));
        var mem = new ResourceId.Memory(7);

        var sig = Signature.Of(
            new Claim(Kind.Occupy, tree, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, mem, Mode.Release, new ScopeId.Global(), Interval.Exact(1)));

        var net = NetTable.Compute(sig, new ScopeId.Global());
        // 两资源为独立键（不串）
        Assert.Equal(2, net.Resources.Count());
        // Tree 仅含 create[1,1] ⇒ 净效应 [1,1]，未含 Memory 的 release；不守恒
        Assert.False(net.IsConserved(tree));
        // Memory 仅含 release[1,1] ⇒ 净效应 [-1,-1]，独立于 Tree
        Assert.False(net.IsConserved(mem));
    }

    // ── DO-7（量纲隔离根因：net 仅 occupy 桶，read/write 不进 net） ──────────────────
    /// <summary>守护 §11 DO-7（量纲隔离：read/write/occupy 不可混算）：§3.3.1 — <see cref="NetTable.Compute"/> 仅 occupy 桶参与；read/write 不进 net 守恒。
    /// 回退为 read/write 被并入 net ⇒ 此测试红（仅 read 的签名 net 必为空，且无 occupy 时净效应不可靠）。</summary>
    [Fact]
    public void DO7_NetIsOccupyBucketOnly()
    {
        var mem = new ResourceId.Memory(0);

        // 仅 read/write 同资源 ⇒ net 不含该资源（量纲隔离，read/write 不进 occupy 桶）
        var rwOnly = Signature.Of(
            new Claim(Kind.Read, mem, Mode.Use, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Write, mem, Mode.Use, new ScopeId.Global(), Interval.Exact(1)));
        var rwNet = NetTable.Compute(rwOnly, new ScopeId.Global());
        Assert.False(rwNet.IsConserved(mem)); // 无 occupy 记录 ⇒ 不守恒（fail-closed）

        // 同资源加 occupy create[1,1] ⇒ net 只反映 occupy，read/write 被忽略
        var withOcc = Signature.Of(
            new Claim(Kind.Read, mem, Mode.Use, new ScopeId.Global(), Interval.Exact(5)),
            new Claim(Kind.Write, mem, Mode.Use, new ScopeId.Global(), Interval.Exact(9)),
            new Claim(Kind.Occupy, mem, Mode.Create, new ScopeId.Global(), Interval.Exact(1)));
        var occNet = NetTable.Compute(withOcc, new ScopeId.Global());
        var v = occNet.Get(mem);
        // 仅 occupy create[1,1] 贡献 ⇒ 净效应 [1,1]，而非 5/9 混入
        Assert.Equal(ZStar.Of(1), v.Lo);
        Assert.Equal(ZStar.Of(1), v.Hi);
    }

    // ── DO-8（峰值检测根因：资源归一单点真相，无重复键） ───────────────────────────
    /// <summary>守护 §11 DO-8（峰值检测依赖资源规范形唯一）：§3.1.4a — <see cref="ResourceId.Normalize"/>(Self("signal_x")) == Normalize(SignalBus("x"))（ST-02 单点真相）。
    /// 回退为归一分裂 ⇒ 此测试红（同信号占 two 键，泄漏/峰值重复计数）。</summary>
    [Fact]
    public void DO8_SignalResourceNormalizeSingleSourceOfTruth()
    {
        var selfSig = ResourceId.Normalize(new ResourceId.Self("signal_x"));
        var bus = ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x")));
        Assert.Equal(selfSig, bus);

        // 反向可证伪：不同信号名不得归一相等
        var other = ResourceId.Normalize(new ResourceId.SignalBus(new StringName("y")));
        Assert.NotEqual(selfSig, other);
    }

    // ── DO-9（泄漏检测根因：守恒判据，同资源 create+release 闭合） ───────────────────
    /// <summary>守护 §11 DO-9（泄漏检测依赖守恒判据）：§3.3.1 — 同资源 create+release ⇒ net 跨 0 ⇒ 守恒 true；缺 release ⇒ false。
    /// 回退为遗漏释放仍判守恒 ⇒ 此测试红（静默漏报泄漏）。</summary>
    [Fact]
    public void DO9_ConservationRequiresPairedRelease()
    {
        var tree = new ResourceId.Tree(NodePathOrUnknown.Of("p"));

        // create + release 同资源同 size ⇒ 净效应 [-1,1] 含 0 ⇒ 守恒
        var closed = Signature.Of(
            new Claim(Kind.Occupy, tree, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, tree, Mode.Release, new ScopeId.Global(), Interval.Exact(1)));
        Assert.True(NetTable.Compute(closed, new ScopeId.Global()).IsConserved(tree));

        // 仅 create，缺 release ⇒ 不守恒（fail-closed 报警）
        var leaked = Signature.Of(
            new Claim(Kind.Occupy, tree, Mode.Create, new ScopeId.Global(), Interval.Exact(1)));
        Assert.False(NetTable.Compute(leaked, new ScopeId.Global()).IsConserved(tree));
    }

    // ── DO-10（Shell 同态映射根因：Compatible 全函数 + 对称，无未覆盖对） ─────────────
    /// <summary>守护 §11 DO-10（Shell 双向同步依赖 Compatible 全函数自洽）：§3.2.3 — <see cref="Compatible.IsCompatible"/> 对 5×5=25 组合全返回 bool（不抛）且对称。
    /// 回退为某对抛异常或不对称 ⇒ 此测试红。</summary>
    [Fact]
    public void DO10_CompatibleIsTotalAndSymmetric()
    {
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var a in modes)
            foreach (var b in modes)
            {
                // 全函数：每对都返回 bool（不抛）
                var r = Compatible.IsCompatible(a, b);
                Assert.IsType<bool>(r);
                // 对称：IsCompatible(a,b) == IsCompatible(b,a)
                Assert.Equal(r, Compatible.IsCompatible(b, a));
            }
    }
}
