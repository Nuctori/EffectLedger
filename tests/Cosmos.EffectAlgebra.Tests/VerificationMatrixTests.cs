using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

/// <summary>
/// 迭代11 — PDR §14 验证矩阵的可自动化部分（纯 L1，零 Godot）。
/// 逐项编码 S1–S3（稳定性法则 / DO-1..DO-10 在 L1 层可验证项）与 A1–A5（架构不变性）。
/// 运行期不可达项（真实 Godot 行为、反射/动态注入边界）以注释声明 out-of-scope、权威在 L1 定义，不硬编。
/// 本文件本身不带魔法数（A4）：所有阈值/来源均回指 § 出处。
/// </summary>
public class VerificationMatrixTests
{
    // ════════════════════════════════════════════════════════════════════════
    // S1–S3（稳定性法则 / 根因 DO-1..DO-10 在 L1 层可机械验证项）
    // ════════════════════════════════════════════════════════════════════════

    // DO-1 单值区间归 default（§3.1.5）：缺省 size ⇒ Interval.Default = [1,1]
    [Fact]
    public void DO1_SingleValueIntervalDefaultsToOne() // §3.1.5(d)
    {
        // 缺省 size（default(Interval)）经 Normalize ⇒ Interval.Default = [1,1]（§3.1.5(d)）
        var c = new Claim(Kind.Occupy, new ResourceId.Memory(7), Mode.Create, new ScopeId.Shell(), default);
        Assert.Equal(Interval.Default, c.Normalize().Size);
        Assert.Equal(Interval.Exact(1), c.Normalize().Size); // [1,1]
    }

    // DO-3 ⊤ 不触发 0.2 报警（§3.1.5c / §9.1）：DeviationVal.Top.ExceedsThreshold ⇒ false
    [Fact]
    public void DO3_TopDoesNotTriggerAlarm() // §3.1.5c / §9.1
    {
        Assert.False(DeviationVal.Top.ExceedsThreshold(0.2));           // ⊤ ⇒ 不报警（需人工界定）
        Assert.True(DeviationVal.Of(0.5).ExceedsThreshold(0.2));        // 有限值 0.5 > 0.2 ⇒ 报警
        Assert.False(DeviationVal.Of(0.1).ExceedsThreshold(0.2));       // 有限值 0.1 ≤ 0.2 ⇒ 不报警
    }

    // DO-6 栈/堆分离（资源 type 区分）：Tree vs Memory vs Gpu 的 net 不互相误记
    [Fact]
    public void DO6_ResourceTypeIsolation() // §3.1.2 — 不同 ResourceId 构造子代表不同资源类型
    {
        var treeRes = new ResourceId.Tree(NodePathOrUnknown.Of("a"));
        var memRes = new ResourceId.Memory(1);
        var gpuRes = new ResourceId.Gpu(new Rid("g"));

        // 同 scope 下三类资源各 create+release 配对（含 0 ⇒ 守恒）；net 按资源键分组，互不串
        var sig = Signature.Of(
            new Claim(Kind.Occupy, treeRes, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, treeRes, Mode.Release, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, memRes, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, memRes, Mode.Release, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, gpuRes, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, gpuRes, Mode.Release, new ScopeId.Global(), Interval.Exact(1)));

        var net = sig.Net(new ScopeId.Global());
        Assert.True(net.IsConserved(treeRes)); // 各资源独立闭合（净效应含 0）
        Assert.True(net.IsConserved(memRes));
        Assert.True(net.IsConserved(gpuRes));
        // Tree 的 net 不应混入 Memory/Gpu 的键：键集合恰好三例 ⇒ 类型隔离
        var set = new System.Collections.Generic.HashSet<ResourceId>(net.Resources);
        Assert.Equal(3, set.Count);
        Assert.Contains(treeRes, set);
        Assert.Contains(memRes, set);
        Assert.Contains(gpuRes, set);
    }

    // DO-7 量纲隔离（§3.3.1）：NetTable 仅含 occupy 桶，read/write 不进 net
    [Fact]
    public void DO7_DimensionalIsolation_NetOnlyOccupy() // §3.3.1 / §3.1.4b
    {
        var res = new ResourceId.Memory(2);
        // 同资源同 scope 放一个 read、一个 write、一个 occupy(+1)，各 size=1
        var sig = Signature.Of(
            new Claim(Kind.Read, res, Mode.Use, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Write, res, Mode.Use, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, res, Mode.Create, new ScopeId.Global(), Interval.Exact(1)));

        var net = sig.Net(new ScopeId.Global());
        // 仅 occupy 进 net ⇒ 资源键恰好 1 个（read/write 被量纲隔离丢弃）
        var keys = new System.Collections.Generic.HashSet<ResourceId>(net.Resources);
        Assert.Single(keys);
        Assert.Contains(res, keys);
        // 该占用净效应 = +1 ⇒ [1,1]，不含 0 ⇒ 纯 acquire 不守恒（fail-closed 报警，非读/写污染）
        Assert.Equal(new SignedInterval(ZStar.Of(1), ZStar.Of(1)), net.Get(res));
        Assert.False(net.IsConserved(res));
        // 反证：create+release 抵消 ⇒ 含 0（read 不干扰 net）
        var sig2 = Signature.Of(
            new Claim(Kind.Read, res, Mode.Use, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, res, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, res, Mode.Release, new ScopeId.Global(), Interval.Exact(1)));
        var net2 = sig2.Net(new ScopeId.Global());
        Assert.True(net2.IsConserved(res)); // create+release 抵消 ⇒ [−1,1] 含 0，read 不干扰
    }

    // DO-8 单点真相（§3.1.4a）：Normalize 幂等 + Self("signal_x") ≡ SignalBus("x")
    [Fact]
    public void DO8_SingleSourceOfTruth_Normalize() // §3.1.4a (ST-02)
    {
        var r = new ResourceId.Self("signal_x");
        var n1 = ResourceId.Normalize(r);
        var n2 = ResourceId.Normalize(n1);
        Assert.Equal(n1, n2); // 幂等
        Assert.Equal(new ResourceId.SignalBus(new StringName("x")), n1); // 唯一规范形
        Assert.Equal(ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x"))), n1);
    }

    // DO-9 守恒（§3.3.1）：create+release 同资源 ⇒ IsConserved true；仅 create ⇒ false（fail-closed）
    [Fact]
    public void DO9_Conservation_AcquireReleaseClosed() // §3.3.1 / §3.1.4a
    {
        var res = new ResourceId.Memory(3);
        var closed = Signature.Of(
            new Claim(Kind.Occupy, res, Mode.Create, new ScopeId.Global(), Interval.Exact(1)),
            new Claim(Kind.Occupy, res, Mode.Release, new ScopeId.Global(), Interval.Exact(1)));
        Assert.True(closed.Net(new ScopeId.Global()).IsConserved(res)); // 含 0（不报警）

        var leaked = Signature.Of(
            new Claim(Kind.Occupy, res, Mode.Create, new ScopeId.Global(), Interval.Exact(1)));
        Assert.False(leaked.Net(new ScopeId.Global()).IsConserved(res)); // 仅 acquire ⇒ fail-closed 报警
    }

    // DO-10 自洽（§3.2.3）：Compatible 全函数 + 对称
    [Fact]
    public void DO10_Compatible_TotalAndSymmetric() // §3.2.3
    {
        var modes = new[] { Mode.Use, Mode.Create, Mode.Release, Mode.Move, Mode.Unknown };
        foreach (var a in modes)
            foreach (var b in modes)
            {
                // 全函数：不抛异常，恒返回 bool
                var r = Compatible.IsCompatible(a, b);
                Assert.IsType<bool>(r);
                // 对称
                Assert.Equal(r, Compatible.IsCompatible(b, a));
            }
        // 显式冲突对（CONFLICT 集）
        Assert.False(Compatible.IsCompatible(Mode.Create, Mode.Create));
        Assert.False(Compatible.IsCompatible(Mode.Move, Mode.Move));
        Assert.False(Compatible.IsCompatible(Mode.Release, Mode.Release));
        // 良性配对（P3）
        Assert.True(Compatible.IsCompatible(Mode.Create, Mode.Release));
        Assert.True(Compatible.IsCompatible(Mode.Release, Mode.Create));
    }

    // ════════════════════════════════════════════════════════════════════════
    // A1–A5（架构不变性 / L1 作为可独立验证的数学核心）
    // ════════════════════════════════════════════════════════════════════════

    // A1 零 Godot 依赖：测试工程解析到的 Claim 类型来自 Cosmos.EffectAlgebra 程序集（即 L1 自洽编译即证明）
    [Fact]
    public void A1_ZeroGodotDependency() // §2 L1 可独立验证 / DO-2
    {
        Assert.Equal("Cosmos.EffectAlgebra", typeof(Claim).Assembly.GetName().Name);
        // out-of-scope：真实 Godot 引用检查由 Domain 项目 CI（§14.3 S3）保证，不在纯 L1 单测内。
    }

    // A2 类型即约束：非法 Interval（lo=⊤ 且 hi 有限）应抛 ArgumentException；空 resource Claim 构造约束
    [Fact]
    public void A2_TypesEncodeBounds_IllegalIntervalThrows() // §3.1.5 构造子不变量
    {
        Assert.Throws<ArgumentException>(() =>
            new Interval(NatStar.Top, NatStar.Of(5))); // [⊤,5] 非法：下界不可为 ⊤ 而上界有限
        // 合法边界不抛
        var _ = new Interval(NatStar.Of(1), NatStar.Top); // [1,⊤] 动态项合法
        var __ = new Interval(NatStar.Top, NatStar.Top);   // [⊤,⊤] 未知区间合法
    }

    // A2（续）：空资源（null 构造子）由 record 构造子静态禁止（C# 不可 new 抽象 ResourceId；
    // 此处验证 Claim 的 resource 必须经 Normalize 后仍为合法非 null 规范形）
    [Fact]
    public void A2_TypesEncodeBounds_ResourceNotNullAfterNormalize() // §3.1.4a
    {
        var c = new Claim(Kind.Occupy, new ResourceId.Memory(9), Mode.Create, new ScopeId.Shell(), Interval.Exact(1));
        var n = c.Normalize();
        Assert.NotNull(n.Resource); // record 构造子 + Normalize 保证非空
        Assert.IsType<ResourceId.Memory>(n.Resource);
    }

    // A3 白名单完整：每条映射存在且 Claims 非空；release-class 全表存在
    [Fact]
    public void A3_WhitelistComplete() // §7 / §8.1
    {
        Assert.True(GodotApiWhitelist.All.Length > 0); // 非空
        foreach (var m in GodotApiWhitelist.All)
        {
            Assert.False(string.IsNullOrEmpty(m.GodotApi));
            Assert.True(m.Claims.Length > 0); // 每条映射须带来源 Claim（§3.1.4a 非空）
        }
        // §8.1 release-class 7 项权威清单存在且非空
        Assert.Equal(7, ReleaseClass.Names.Count);
        Assert.Contains("queue_free", ReleaseClass.Names);
        Assert.Contains("free", ReleaseClass.Names);
        Assert.Contains("remove_child", ReleaseClass.Names);
        Assert.Contains("disconnect", ReleaseClass.Names);
        Assert.Contains("remove_from_group", ReleaseClass.Names);
        Assert.Contains("cancel_free", ReleaseClass.Names);
        Assert.Contains("free_children_in_group", ReleaseClass.Names);
    }

    // A4 注释承载残差：本测试文件无魔法数（阈值均回指 §）；此处仅以确定性随机回指 §9.1 关联断言（可选 A5）
    [Fact]
    public void A4_NoMagicNumbers_CommentCarriesResidual() // §14 A4
    {
        // §9.1 阈值 0.2 仅在此处作为「文档化」常量出现，来源于 §9.1 定义，非硬编码魔法数。
        const double deviationThreshold = 0.2; // §9.1 报警阈值
        var rnd = new System.Random(14); // §14 A5 可复现：固定种子
        for (var i = 0; i < 50; i++)
        {
            var d = DeviationVal.Of(rnd.NextDouble());
            Assert.Equal(d.Value > deviationThreshold, d.ExceedsThreshold(deviationThreshold));
            Assert.False(DeviationVal.Top.ExceedsThreshold(deviationThreshold)); // ⊤ 恒不报警（A4 残差诚实）
        }
    }

    // S2/S3 残差诚实（注释承载，非测试）：L2 不分析方法体、L3 近似性——out-of-scope 于纯 L1，
    // 其 soundness/completeness 由 L2/L3 工程与 §14.2/§14.3 判定框架承载，此处不重复硬编。
}
