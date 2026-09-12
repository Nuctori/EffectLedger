// Round4Hickey2Tests.cs — rich-hickey2-round04（错误模式对称性）审计发现的 TDD 钉。
// R4-001 外部输入异常类型对称 / R4-002 ParseLoop TryGetUInt64 / R4-003 Combination.Loop 0 值守卫 /
// R4-004 AuditResult Passed≡Violations.IsEmpty 不变量。xUnit。
using System;
using System.Collections.Immutable;
using Xunit;

namespace EffectLedger.Tests;

public class Round4Hickey2Tests
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);
    static ScopeId Global() => new ScopeId.Global();
    static ResourceId Gpu(string b) => new ResourceId.Gpu(new Rid(b));

    // ── R4-001：JSON 路径的重复 Claim 与 lo>hi ⇒ FormatException（契约方言统一），非裸 ArgumentException ──
    [Fact]
    public void Parse_DuplicateClaim_ThrowsFormatException()
    {
        var json = """{"events":[{"lifetime":[0,1],"scope":{"scene":"s"},"footprint":[{"kind":"occupy","resource":{"gpu":"g"},"mode":"create","scope":{"scene":"s"},"size":[1,1]},{"kind":"occupy","resource":{"gpu":"g"},"mode":"create","scope":{"scene":"s"},"size":[1,1]}]}]}""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：ArgumentException 漏接 catch(FormatException)
        Assert.Contains("重复", ex.Message); // 反例信息保留
    }

    [Fact]
    public void Parse_SizeLoGreaterThanHi_ThrowsFormatException()
    {
        var json = """{"events":[{"lifetime":[0,1],"scope":{"scene":"s"},"footprint":[{"kind":"occupy","resource":{"gpu":"g"},"mode":"create","scope":{"scene":"s"},"size":[5,1]}]}]}""";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：ArgumentException
    }

    // ── R4-002：loop 数值 -1/1.5 ⇒ FormatException，不漏 BCL 异常 ──
    [Theory]
    [InlineData("""{"events":[{"lifetime":[0,1],"scope":{"scene":"s"},"loop":-1,"footprint":[]}]}""")]
    [InlineData("""{"events":[{"lifetime":[0,1],"scope":{"scene":"s"},"loop":1.5,"footprint":[]}]}""")]
    public void Parse_LoopNonUInt64_ThrowsFormatException(string json)
    {
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：InvalidOperationException
        Assert.Contains("loop", ex.Message);
    }

    // ── R4-003：Combination.Loop 对 default/非法 ω=0 fail-fast（与 EffectEvent 构造守卫对称） ──
    [Fact]
    public void Combination_Loop_RejectsZeroOmega()
    {
        var sig = Signature.Of(new Claim(Kind.Occupy, Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(1)));
        Assert.ThrowsAny<ArgumentException>(() => Combination.Loop(sig, default, Global())); // 修复前：静默产 [0,0] 签名
        Assert.ThrowsAny<ArgumentException>(() => Combination.Loop(sig, LoopCount.Of(0), Global())); // Of 已拒，双保险钉
    }

    // ── R4-004：AuditResult 不变量——Passed ≡ Violations.IsEmpty；派生 IsPeakChecked ──
    [Fact]
    public void AuditResult_PassedMustMatchViolationsEmpty()
    {
        var v = new Violation(NatStar.Of(0), Gpu("g"), Global(), "Leak", "");
        Assert.Throws<ArgumentException>(() => new AuditResult(true, ImmutableArray.Create(v)));          // 矛盾状态不可构造
        Assert.Throws<ArgumentException>(() => new AuditResult(false, ImmutableArray<Violation>.Empty));  // 反向矛盾同拒
    }

    [Fact]
    public void AuditResult_ConsistentStates_Constructible()
    {
        Assert.True(new AuditResult(true, ImmutableArray<Violation>.Empty).Passed);
        var v = new Violation(NatStar.Of(0), Gpu("g"), Global(), "Leak", "");
        Assert.False(new AuditResult(false, ImmutableArray.Create(v)).Passed);
    }

    [Fact]
    public void AuditResult_IsPeakChecked_DerivesFromCapsChecked()
    {
        Assert.False(new AuditResult(true, ImmutableArray<Violation>.Empty).IsPeakChecked);      // CapsChecked=0 ⇒ 未审峰值
        Assert.True(new AuditResult(true, ImmutableArray<Violation>.Empty, 3).IsPeakChecked);
    }

    [Fact]
    public void Audit_ZeroBudget_PeakGateNotRun_NotPeakChecked()
    {
        // 行为语义：闭合计入的 create+release 配对 ⇒ Passed；零预算 ⇒ gate(2) 未运行 ⇒ IsPeakChecked=false（调用方不再把"没查"当"查过全绿"）
        var e = new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), Scene("S"),
            Signature.Of(new Claim(Kind.Occupy, Gpu("g"), Mode.Create, Scene("S"), Interval.Exact(1)).Normalize()));
        var eRel = new EffectEvent(new Interval(NatStar.Of(5), NatStar.Of(10)), Scene("S"),
            Signature.Of(new Claim(Kind.Occupy, Gpu("g"), Mode.Release, Scene("S"), Interval.Exact(1)).Normalize()));
        var aud = new EffectScript(ImmutableArray.Create(e, eRel)).Audit(); // Budget.None ⇒ Caps=空
        Assert.True(aud.Passed);
        Assert.Equal(0, aud.CapsChecked);
        Assert.False(aud.IsPeakChecked); // 钉：零预算 ⇒ 峰值门未运行
    }
}
