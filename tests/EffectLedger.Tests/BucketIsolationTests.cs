// BucketIsolationTests.cs — §3.1.4b Signature 三桶维度隔离 + §3.3.1 NetTable 仅 occupy（DO-7）+ §8.2 量纲隔离锁。
// LANDING_PLAN §3.2：L1 代数运算。证明 read/write/occupy 三桶独立、net 仅 occupy 参与（量纲隔离）。

using EffectLedger;
using Xunit;

namespace EffectLedger.Tests;

public class BucketIsolationTests
{
    // §3.1.4b — 三桶独立：同资源、不同 kind 的 claim 各入其桶，互不串（DO-7 量纲隔离在 Signature 层）。
    [Fact]
    public void Signature_ThreeBucketsIndependent() // §3.1.4b / DO-7
    {
        var res = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var scope = new ScopeId.Global();
        var readClaim = new Claim(Kind.Read, res, Mode.Use, scope, Interval.Default);   // size [1,1]
        var writeClaim = new Claim(Kind.Write, res, Mode.Use, scope, Interval.Default); // size [1,1]
        var occupyClaim = new Claim(Kind.Occupy, res, Mode.Use, scope, Interval.Default);// size [1,1]

        var sig = Signature.Of(readClaim, writeClaim, occupyClaim);

        // 每桶仅含其 kind 的 claim；各桶 Count==1，互不串。
        Assert.Single(sig.ReadClaims);
        Assert.Single(sig.WriteClaims);
        Assert.Single(sig.OccupyClaims);

        Assert.Contains(readClaim, sig.ReadClaims);
        Assert.DoesNotContain(readClaim, sig.WriteClaims);
        Assert.DoesNotContain(readClaim, sig.OccupyClaims);

        Assert.Contains(writeClaim, sig.WriteClaims);
        Assert.DoesNotContain(writeClaim, sig.ReadClaims);
        Assert.DoesNotContain(writeClaim, sig.OccupyClaims);

        Assert.Contains(occupyClaim, sig.OccupyClaims);
        Assert.DoesNotContain(occupyClaim, sig.ReadClaims);
        Assert.DoesNotContain(occupyClaim, sig.WriteClaims);
    }

    // §3.3.1 / DO-7 — NetTable.Compute 仅含 occupy 桶；read/write 不进 net（量纲隔离）。
    [Fact]
    public void NetTable_OnlyOccupiesResource() // §3.3.1 / DO-7
    {
        var res = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var scope = new ScopeId.Global();
        var readClaim = new Claim(Kind.Read, res, Mode.Use, scope, Interval.Default);     // size [1,1]
        var writeClaim = new Claim(Kind.Write, res, Mode.Use, scope, Interval.Default);   // size [1,1]
        var occupyClaim = new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(5)); // size [5,5]

        var sig = Signature.Of(readClaim, writeClaim, occupyClaim);
        var net = NetTable.Compute(sig, scope);

        // net 只反映 occupy 的 [5,5]，read/write 不累加。
        var v = net.Get(res);
        Assert.False(v.Lo.IsTop);
        Assert.False(v.Hi.IsTop);
        Assert.Equal(5L, v.Lo.Value);
        Assert.Equal(5L, v.Hi.Value);
    }

    // §3.1.4a(DO-6) — 跨资源不串：Tree("x") 与 Memory("m") 各 occupy claim，net 字典两键独立。
    [Fact]
    public void NetTable_CrossResourceNotMixed() // §3.1.4a(DO-6)
    {
        var treeRes = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var memRes = new ResourceId.Memory(42); // Memory(uid) — §7 裸 'memory'
        var scope = new ScopeId.Global();

        var treeClaim = new Claim(Kind.Occupy, treeRes, Mode.Create, scope, Interval.Exact(5)); // [5,5]
        var memClaim = new Claim(Kind.Occupy, memRes, Mode.Create, scope, Interval.Exact(3));   // [3,3]

        var sig = Signature.Of(treeClaim, memClaim);
        var net = NetTable.Compute(sig, scope);

        // 两键独立存在，值各自对应其 size（不串）。
        Assert.Contains(treeRes, net.Resources);
        Assert.Contains(memRes, net.Resources);

        var tv = net.Get(treeRes);
        Assert.Equal(5L, tv.Lo.Value);
        Assert.Equal(5L, tv.Hi.Value);

        var mv = net.Get(memRes);
        Assert.Equal(3L, mv.Lo.Value);
        Assert.Equal(3L, mv.Hi.Value);
    }

    // §3.2.1 / §3.1.4b — Union 幂等 + 可交换；三桶各自 SetEquals / 结构相等。
    [Fact]
    public void Signature_UnionIdempotentAndCommutative() // §3.2.1 / §3.1.4b
    {
        var res = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var scope = new ScopeId.Global();
        var readClaim = new Claim(Kind.Read, res, Mode.Use, scope, Interval.Default);
        var writeClaim = new Claim(Kind.Write, res, Mode.Use, scope, Interval.Default);
        var occupyClaim = new Claim(Kind.Occupy, res, Mode.Use, scope, Interval.Default);

        var s = Signature.Of(readClaim, writeClaim, occupyClaim);

        // 幂等：Union(s,s) 三桶各 SetEquals s。
        var idem = Signature.Union(s, s);
        Assert.True(idem.ReadClaims.SetEquals(s.ReadClaims));
        Assert.True(idem.WriteClaims.SetEquals(s.WriteClaims));
        Assert.True(idem.OccupyClaims.SetEquals(s.OccupyClaims));

        // 可交换：分桶构造 a/b 交换后结构相等。
        var a = Signature.Of(readClaim, writeClaim);
        var b = Signature.Of(occupyClaim);
        var ab = Signature.Union(a, b);
        var ba = Signature.Union(b, a);

        Assert.True(ab.ReadClaims.SetEquals(ba.ReadClaims));
        Assert.True(ab.WriteClaims.SetEquals(ba.WriteClaims));
        Assert.True(ab.OccupyClaims.SetEquals(ba.OccupyClaims));
        Assert.Equal(ab.ReadClaims.Count, ba.ReadClaims.Count);
        Assert.Equal(ab.WriteClaims.Count, ba.WriteClaims.Count);
        Assert.Equal(ab.OccupyClaims.Count, ba.OccupyClaims.Count);
    }

    // §3.3.1 DO-9 — 守恒判据：occupy create+release 同 size ⇒ 含 0 ⇒ 守恒(true)；仅 create ⇒ 不守恒(false)。
    [Fact]
    public void NetTable_ConservationCriterion() // §3.3.1 DO-9
    {
        var res = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var scope = new ScopeId.Global();

        // 仅 create[5,5] ⇒ 不守恒。
        var onlyCreate = Signature.Of(new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(5)));
        var net1 = NetTable.Compute(onlyCreate, scope);
        Assert.False(net1.IsConserved(res));

        // create[5,5] + release[5,5] ⇒ signed sum = [0,0] 含 0 ⇒ 守恒（有符号 net 求和为真求和，非 min/max 包络）。
        var balanced = Signature.Of(
            new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(5)),
            new Claim(Kind.Occupy, res, Mode.Release, scope, Interval.Exact(5)));
        var net2 = NetTable.Compute(balanced, scope);
        Assert.True(net2.IsConserved(res));

        var v = net2.Get(res);
        Assert.Equal(0L, v.Lo.Value);
        Assert.Equal(0L, v.Hi.Value);
        Assert.True(v.ContainsZero);
    }

    // §3.1.4b / §8.2 — 量纲隔离双重保障说明：Signature 三桶分存 + NetTable.Compute 的
    //   `if (c.Kind != Kind.Occupy) continue;` 共同确保 read/write 永不与 occupy 卷积（DO-7）。
    // 以下断言反向验证：在 occupy 桶外注入 read，net 不受其污染（已由 NetTable_OnlyOccupiesResource 覆盖）。
    [Fact]
    public void BucketIsolation_DoubleGuarantee() // §3.1.4b / §8.2
    {
        var res = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var scope = new ScopeId.Global();
        // 仅 read（无 occupy）：net 中该资源无记录 ⇒ IsConserved fail-closed 返回 false。
        var onlyRead = Signature.Of(new Claim(Kind.Read, res, Mode.Use, scope, Interval.Exact(9)));
        var net = NetTable.Compute(onlyRead, scope);
        Assert.False(net.IsConserved(res)); // 无净占用记录 ⇒ 未闭合，交人工确认
    }

    // §3.1.4a(R4 P1) — Signature 结构相等：看似值、构造方式不同但桶内容相同 ⇒ == / GetHashCode 一致。
    // 补结构相等前，Signature 是 class 引用相等，两结构相同签名不会 ==（Hickey 式 footgun）。
    [Fact]
    public void Signature_StructuralEquality() // §3.1.4a / R4 P1
    {
        var res = new ResourceId.Tree(NodePathOrUnknown.Of("x"));
        var scope = new ScopeId.Global();
        var a = Signature.Of(
            new Claim(Kind.Read, res, Mode.Use, scope, Interval.Default),
            new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(5)));
        // 构造顺序不同、经 Union 合并，但桶内容完全相同 ⇒ 应结构相等。
        var b = Signature.Union(
            Signature.Of(new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(5))),
            Signature.Of(new Claim(Kind.Read, res, Mode.Use, scope, Interval.Default)));

        Assert.Equal(a, b);              // Equals(object)
        Assert.True(a == b);             // operator ==
        Assert.False(a != b);            // operator !=
        Assert.Equal(a.GetHashCode(), b.GetHashCode()); // 哈希一致（放入 HashSet/Dictionary 键安全）

        // 内容不同 ⇒ 不相等（反向）。
        var c = Signature.Of(new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(7)));
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }
}
