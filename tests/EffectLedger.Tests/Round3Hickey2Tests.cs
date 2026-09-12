// Round3Hickey2Tests.cs — rich-hickey2-round03（值语义与不可变性）审计发现的 TDD 钉。
// V3-001/002/003 Budget 值语义三位一体 / V3-004 default 文档钉 / V3-005 with 后门边界校验 /
// V3-006 memory 数值契约异常。xUnit。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace EffectLedger.Tests;

public class Round3Hickey2Tests
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));

    // ── V3-001：同内容 Budget 必须值相等（record struct 名副其实） ──
    [Fact]
    public void Budget_EqualContent_Equals_True()
    {
        var b1 = new Budget(new Dictionary<ResourceId, NatStar> { [Gpu("a")] = NatStar.Of(5) });
        var b2 = new Budget(new Dictionary<ResourceId, NatStar> { [Gpu("a")] = NatStar.Of(5) }); // 修复前：Equals=False
        Assert.True(b1.Equals(b2));
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
        Assert.Single(new HashSet<Budget> { b1, b2 });
    }

    [Fact]
    public void Budget_DifferentContent_NotEqual()
    {
        var b1 = new Budget(new Dictionary<ResourceId, NatStar> { [Gpu("a")] = NatStar.Of(5) });
        var b2 = new Budget(new Dictionary<ResourceId, NatStar> { [Gpu("a")] = NatStar.Of(6) });
        Assert.False(b1.Equals(b2));
    }

    [Fact]
    public void DefaultBudget_Equals_None()
    {
        Assert.True(default(Budget).Equals(Budget.None)); // 修复前：false（引用相等）
    }

    // ── V3-002：Budget.None 不可被污染（可变单例根除） ──
    [Fact]
    public void BudgetNone_Immutable_AgainstIDictionaryCast()
    {
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ResourceId, NatStar>)Budget.None.Caps).Add(Gpu("poison"), NatStar.Of(1))); // 修复前：静默成功污染全局
    }

    [Fact]
    public void BudgetNone_Empty()
    {
        Assert.Empty(Budget.None.Caps);
    }

    // ── V3-003：构造期防御拷贝——外部字典改动不透传，getter 不可回写 ──
    [Fact]
    public void Budget_DefensiveCopy_ConstructorInputIsolation()
    {
        var d = new Dictionary<ResourceId, NatStar> { [Gpu("x")] = NatStar.Of(1) };
        var s = new EffectScript(System.Collections.Immutable.ImmutableArray<EffectEvent>.Empty, new Budget(d));
        d[Gpu("x")] = NatStar.Of(999);                       // 修复前：script.Budget 同步变 999
        Assert.Equal((ulong)1, s.Budget.Caps[Gpu("x")].Value);
    }

    [Fact]
    public void Budget_Getter_CannotMutate()
    {
        var d = new Dictionary<ResourceId, NatStar> { [Gpu("x")] = NatStar.Of(1) };
        var s = new EffectScript(System.Collections.Immutable.ImmutableArray<EffectEvent>.Empty, new Budget(d));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ResourceId, NatStar>)s.Budget.Caps).Add(Gpu("y"), NatStar.Of(1))); // 修复前：回写成功
    }

    // ── V3-005：with 后门在 Signature 边界拦截（非法 Claim 不可进入签名） ──
    static Claim Oc(ResourceId r, Mode m, ScopeId s)
        => new Claim(Kind.Occupy, r, m, s, Interval.Default);

    [Fact]
    public void Signature_Of_RejectsNullResourceClaim()
    {
        var good = Oc(Gpu("r"), Mode.Create, Scene("S"));
        var bad = good with { Resource = null! };             // with 绕过构造校验
        Assert.Throws<ArgumentException>(() => Signature.Of(bad)); // 修复前：静默收下 null 资源
    }

    [Theory]
    [InlineData((Kind)999)]
    [InlineData((Kind)(-1))]
    public void Signature_Of_RejectsUndefinedKind(Kind k)
    {
        // ArgumentOutOfRange 是 ArgumentException 派生——行为钉：未定义 Kind 在 Signature 边界被拒（既有 fail-fast，防回退）。
        Assert.ThrowsAny<ArgumentException>(() =>
            Signature.Of(new Claim(k, Gpu("r"), Mode.Create, Scene("S"), Interval.Exact(1))));
    }

    // ── V3-004：default 与 Default 的文档区分（doc-only 钉：行为锁定） ──
    [Fact]
    public void DefaultInterval_IsNotIntervalDefault_DocumentedSharpEdge()
    {
        Assert.NotEqual(Interval.Default, default(Interval));  // default=[0,0]，Default=[1,1] —— 锐边留档
        Assert.Equal((ulong)0, default(Interval).Lo.Value);
    }

    // ── V3-006：resource.memory 负数/小数 ⇒ FormatException（契约承诺） ──
    [Fact]
    public void ResourceMemory_NegativeNumber_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(
            """{"events":[{"lifetime":[0,1],"scope":{"scene":"s"},"footprint":[{"kind":"occupy","resource":{"memory":-1},"mode":"create","scope":{"scene":"s"}}]}]}""")); // 修复前：InvalidOperationException
    }

    [Fact]
    public void ResourceMemory_FractionalNumber_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(
            """{"events":[{"lifetime":[0,1],"scope":{"scene":"s"},"footprint":[{"kind":"occupy","resource":{"memory":1.5},"mode":"create","scope":{"scene":"s"}}]}]}"""));
    }
}
