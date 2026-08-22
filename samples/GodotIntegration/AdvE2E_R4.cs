// AdvE2E_R4.cs — 对抗审计 R4：L1 守恒代数边界/数值（E2E，直接消费 L1 + §7 白名单）。
// 目标：挖掘 net 聚合语义、溢出、负值守恒、资源路径归一、跨 kind 独立守恒、空/单位元、并的结合/交换 等缺陷。
using Cosmos.EffectAlgebra;
using Xunit;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R4
{
    private static readonly ScopeId Global = new ScopeId.Global();
    private static ResourceId Tree(string p) => new ResourceId.Tree(NodePathOrUnknown.Of(p));

    // ── 数值/溢出：大量 Occupy 同资源，net 须为精确有符号和（含负），且不静默截断 ──
    // 注：Signature 是 Claim 的 ImmutableHashSet（§3.1.4b 去重分桶），完全相同的 Claim 会被去重合并。
    // 故「多次调用同一 API」的语义由调用方（Analyzer/L2）以不同 size 区间表达；本测试用不同 size 表达真实多重性。
    [Fact]
    public void E2E_R4_ManyAcquireRelease_NetIsSignedSum()
    {
        // 5 次 create[1..5] + 3 次 release[1..3]（size 各异，不合并）⇒ 净 = 15−6 = +9 ⇒ [9,9]，不守恒（仍有 9 泄漏）
        var s = Signature.Empty;
        for (int i = 1; i <= 5; i++)
            s = Signature.Union(s, Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact((ulong)i))));
        for (int i = 1; i <= 3; i++)
            s = Signature.Union(s, Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Release, Global, Interval.Exact((ulong)i))));
        var net = NetTable.Compute(s, Global);
        var v = net.Get(Tree("t"));
        Assert.Equal(ZStar.Of(9), v.Lo);
        Assert.Equal(ZStar.Of(9), v.Hi);
        Assert.False(net.IsConserved(Tree("t")), "净 +9 必须报泄漏（不守恒）");
    }

    [Fact]
    public void E2E_R4_MoreReleaseThanAcquire_NetNegative_NotConserved()
    {
        // create[1,1] + release[1,1] + release[2,2]（size 各异）⇒ 净 = 1 − (1+2) = −2 ⇒ [-2,-2]。
        // IsConserved 必须 FALSE（负净效应=超释放，绝不可谎称守恒）
        var s = Signature.Of(
            new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact(1)),
            new Claim(Kind.Occupy, Tree("t"), Mode.Release, Global, Interval.Exact(1)),
            new Claim(Kind.Occupy, Tree("t"), Mode.Release, Global, Interval.Exact(2)));
        var net = NetTable.Compute(s, Global);
        var v = net.Get(Tree("t"));
        Assert.Equal(ZStar.Of(-2), v.Lo);
        Assert.Equal(ZStar.Of(-2), v.Hi);
        Assert.False(net.IsConserved(Tree("t")), "净 -2（超释放）必须报不守恒");
    }

    [Fact]
    public void E2E_R4_Overflow_ManyLargeAcquire_DoesNotSilentlyWrap()
    {
        // 1,000,000 次 create（size = i，各异）⇒ 净 = Σ1..1e6 = 500000500000，不回绕成负/零（ZStar 用 long）。
        var s = Signature.Empty;
        const int n = 1_000_000;
        for (int i = 1; i <= n; i++)
            s = Signature.Union(s, Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact((ulong)i))));
        var net = NetTable.Compute(s, Global);
        var v = net.Get(Tree("t"));
        Assert.Equal(ZStar.Of(500000500000L), v.Lo);
        Assert.Equal(ZStar.Of(500000500000L), v.Hi);
        Assert.False(net.IsConserved(Tree("t")), "百万次纯 acquire 必不守恒");
    }

    [Fact]
    public void E2E_R4_IdenticalClaims_Deduplicated_NotMultiplied()
    {
        // 同一 ResourceId/Mode/Scope/Size 的 Claim 在 Signature 中作为集合去重（§3.1.4b 幂等）。
        // 5 次完全相同的 acquire ⇒ 仅计 1 次 ⇒ 净 +1（非 +5）。这约束了 Analyzer/L2 须以不同 size 表达真实多重性。
        var s = Signature.Empty;
        for (int i = 0; i < 5; i++)
            s = Signature.Union(s, Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact(1))));
        var net = NetTable.Compute(s, Global);
        var v = net.Get(Tree("t"));
        Assert.Equal(ZStar.Of(1), v.Lo);
        Assert.Equal(ZStar.Of(1), v.Hi);
        Assert.False(net.IsConserved(Tree("t")), "5 次相同 acquire 被去重为 1 次 ⇒ 仍泄漏（不守恒）");
    }

    // ── 资源路径归一：node.id / node/id / NODE.ID / node.id. 是否同/异资源 ──
    [Fact]
    public void E2E_R4_TreePath_CaseAndSeparator_TreatedDistinct()
    {
        // §3.1.4a：仅构造子标签+字段逐位相等（结构相等）。"node.id" 与 "node/id" 与 "NODE.ID" 与 "node.id." 路径字符串不同 ⇒ 不同资源。
        // 仅在 create/release 在「完全相同路径串」上配对时才守恒；其余视为泄漏（fail-closed）。
        var p1 = Tree("node.id");
        var s = Signature.Of(
            new Claim(Kind.Occupy, p1, Mode.Create, Global, Interval.Exact(1)),
            new Claim(Kind.Occupy, p1, Mode.Release, Global, Interval.Exact(1)));
        var net = NetTable.Compute(s, Global);
        Assert.True(net.IsConserved(p1));
        // 大小写/分隔符/尾点差异的路径串 ⇒ 结构不相等 ⇒ 不同资源 ⇒ 仅 acquire 时不守恒
        var variants = new[] { Tree("node/id"), Tree("NODE.ID"), Tree("node.id.") };
        foreach (var v in variants)
        {
            var sv = Signature.Of(new Claim(Kind.Occupy, v, Mode.Create, Global, Interval.Exact(1)));
            var nv = NetTable.Compute(sv, Global);
            Assert.False(nv.IsConserved(v), $"路径串 {v} 与 node.id 不同 ⇒ 必须不守恒");
        }
    }

    // ── 跨 kind 独立守恒：(Tree Occupy) + (Object/Tree 不同资源) 互不串 ──
    [Fact]
    public void E2E_R4_DifferentResources_IndependentConservation()
    {
        var treeA = Tree("a");
        var treeB = Tree("b");
        var s = Signature.Of(
            new Claim(Kind.Occupy, treeA, Mode.Create, Global, Interval.Exact(1)),
            new Claim(Kind.Occupy, treeA, Mode.Release, Global, Interval.Exact(1)),
            new Claim(Kind.Occupy, treeB, Mode.Create, Global, Interval.Exact(1))); // treeB 仅 acquire
        var net = NetTable.Compute(s, Global);
        Assert.True(net.IsConserved(treeA));           // a 配对 ⇒ 守恒
        Assert.False(net.IsConserved(treeB));          // b 泄漏 ⇒ 不守恒
        // 两资源键独立，互不污染
        Assert.Equal(2, new System.Collections.Generic.HashSet<ResourceId>(net.Resources).Count);
    }

    // ── 冲突 Claim 合并：同资源 A 中 Occupy、B 中 Release ⇒ net=0 守恒（含 0）──
    [Fact]
    public void E2E_R4_ConflictingClaims_Union_NetZeroConserved()
    {
        var a = Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact(1)));
        var b = Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Release, Global, Interval.Exact(1)));
        var s = Signature.Union(a, b);
        var net = NetTable.Compute(s, Global);
        var v = net.Get(Tree("t"));
        Assert.Equal(ZStar.Of(0), v.Lo);
        Assert.Equal(ZStar.Of(0), v.Hi);
        Assert.True(net.IsConserved(Tree("t")), "create+release 精确抵消 ⇒ [0,0] 含 0 ⇒ 守恒");
    }

    // ── 空签名与单位元 ──
    [Fact]
    public void E2E_R4_EmptyAndIdentity()
    {
        var net = NetTable.Compute(Signature.Empty, Global);
        Assert.False(net.IsConserved(Tree("t")), "空签名：未出现资源 ⇒ 不守恒");
        var s = Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact(1)));
        var unionEmpty = Signature.Union(s, Signature.Empty);
        Assert.Equal(NetTable.Compute(s, Global).Get(Tree("t")), NetTable.Compute(unionEmpty, Global).Get(Tree("t")));
        var emptyUnion = Signature.Union(Signature.Empty, s);
        Assert.Equal(NetTable.Compute(s, Global).Get(Tree("t")), NetTable.Compute(emptyUnion, Global).Get(Tree("t")));
    }

    // ── 并的结合/交换（代数不变量）──
    [Fact]
    public void E2E_R4_Union_AssociativeCommutative_NetStable()
    {
        var x = Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact(1)));
        var y = Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Release, Global, Interval.Exact(1)));
        var z = Signature.Of(new Claim(Kind.Occupy, Tree("t"), Mode.Create, Global, Interval.Exact(2))); // size 各异不合并
        var left = Signature.Union(Signature.Union(x, y), z);
        var right = Signature.Union(x, Signature.Union(y, z));
        var nl = NetTable.Compute(left, Global).Get(Tree("t"));
        var nr = NetTable.Compute(right, Global).Get(Tree("t"));
        Assert.Equal(nl, nr);                 // 结合
        // 交换：用含全部三 Claim 的不同结合顺序 (z∪y)∪x，net 应与 (x∪y)∪z 相同
        var comm = NetTable.Compute(Signature.Union(Signature.Union(z, y), x), Global).Get(Tree("t"));
        Assert.Equal(nl, comm);               // 交换
        Assert.Equal(ZStar.Of(2), nl.Lo);     // create(1+2)−release(1) = 净 +2
        Assert.Equal(ZStar.Of(2), nl.Hi);
    }

    // ── 真实 §7 白名单接入：AddChild(create)∪RemoveChild(release) 在 Tree("node.id") 守恒 ──
    [Fact]
    public void E2E_R4_Whitelist_AddChildRemoveChild_Conserved()
    {
        var add = System.Linq.Enumerable.First(GodotApiWhitelist.All,
            m => m.GodotApi == "AddChild").Claims;
        var rem = System.Linq.Enumerable.First(GodotApiWhitelist.All,
            m => m.GodotApi == "RemoveChild").Claims;
        var s = Signature.Empty;
        foreach (var c in add) s = Signature.Union(s, Signature.Of(c));
        foreach (var c in rem) s = Signature.Union(s, Signature.Of(c));
        var net = NetTable.Compute(s, Global);
        var treeNode = Tree("node.id");
        Assert.True(net.IsConserved(treeNode), "AddChild+RemoveChild 在 Tree(node.id) 应守恒（端到端协议）");
    }
}
