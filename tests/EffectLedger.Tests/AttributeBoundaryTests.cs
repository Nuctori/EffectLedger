using System.Reflection;
using EffectLedger;
using Xunit;

namespace EffectLedger.Tests;

/// <summary>
/// 迭代22 — §8.3.1/§8.3.2 属性构造子边界锁。
/// 用户铁律「类型系统约束不了的用注释，约束了的也要测」：本文件证明
/// reason 非空 / epsilon∈[0,0.5] / kind 不可覆盖 三道边界**由 L1 构造子强制**，
/// 而非运行期 if 漏判（边界在类型构造即 fail-fast）。
/// 任何一条断言若实现去掉抛异常，对应测试必红 ⇒ 可证伪，无假绿。
/// </summary>
public class AttributeBoundaryTests
{
    // ── §8.3.1 [EffectOverride] reason 非空 ──────────────────────────────
    [Fact]
    public void EffectOverride_EmptyReason_Throws() // §8.3.1 边界由构造子强制，非运行期 if 漏判
    {
        Assert.Throws<ArgumentException>(() => new EffectOverrideAttribute(""));
    }

    [Fact]
    public void EffectOverride_NullReason_Throws() // §8.3.1 边界由构造子强制（null 亦拦）
    {
        Assert.Throws<ArgumentException>(() => new EffectOverrideAttribute(null!));
    }

    [Fact]
    public void EffectOverride_WhitespaceReason_Throws() // §8.3.1 边界由构造子强制（IsNullOrWhiteSpace 拦空白）
    {
        Assert.Throws<ArgumentException>(() => new EffectOverrideAttribute("   "));
    }

    [Fact]
    public void EffectOverride_ValidReason_DoesNotThrow() // §8.3.1 合法理由通过，Reason 原值保留
    {
        var attr = new EffectOverrideAttribute("真理由");
        Assert.Equal("真理由", attr.Reason);
    }

    // ── §8.3.2 [AcceptDeviation] epsilon ∈ [0,0.5] ───────────────────────
    [Fact]
    public void AcceptDeviation_AboveUpperBound_Throws() // §8.3.2 边界由构造子强制，超上界 0.6 抛
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptDeviationAttribute(0.6));
    }

    [Fact]
    public void AcceptDeviation_BelowLowerBound_Throws() // §8.3.2 边界由构造子强制，超下界 -0.1 抛
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptDeviationAttribute(-0.1));
    }

    [Fact]
    public void AcceptDeviation_EqualsOne_Throws() // §8.3.2 边界由构造子强制，=1 超 0.5 上界抛
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptDeviationAttribute(1.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.2)]
    public void AcceptDeviation_WithinBounds_DoesNotThrow(double epsilon) // §8.3.2 合法边界+内点通过，Epsilon 原值保留
    {
        var attr = new AcceptDeviationAttribute(epsilon);
        Assert.Equal(epsilon, attr.Epsilon);
    }

    // ── §8.3.1 kind 不可覆盖（类型层即约束） ─────────────────────────────
    [Fact]
    public void EffectOverride_HasNoOverrideKindMember() // §8.3.1 类型层禁止覆盖 kind：反射确认无 OverrideKind 属性/字段
    {
        var prop = typeof(EffectOverrideAttribute).GetProperty("OverrideKind", BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(prop); // 类型层不存在 ⇒ 无法写出命名参数 OverrideKind=...（编译器 CS0117 先错）
    }
}
