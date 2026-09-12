// Round1Hickey2Tests.cs — rich-hickey2-round01（序列化 round-trip 保真）审计发现的 TDD 钉。
// F1 文档§4示例可解析+幂等 / F2+F6 层级未知键白名单与定位 / F3 default(LoopCount) 毒值 /
// F4 异常类型对称(FormatException) / F5 空scope对象拒绝。xUnit。
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Xunit;

namespace EffectLedger.Tests;

public class Round1Hickey2Tests
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    static Claim Oc(ResourceId r, Mode m, ScopeId s)
        => new Claim(Kind.Occupy, r, m, s, Interval.Default).Normalize();

    // ── F1：EFFECT_SCRIPT.md §4 旗舰示例必须可 Parse 且 round-trip 幂等（文档即夹具，防漂移） ──
    static string ExtractSection4Json()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EFFECT_SCRIPT.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var md = File.ReadAllText(Path.Combine(dir!.FullName, "EFFECT_SCRIPT.md"));
        int sec = md.IndexOf("## 4.", StringComparison.Ordinal);
        Assert.True(sec >= 0, "EFFECT_SCRIPT.md 缺 §4");
        int fence = md.IndexOf("```json", sec, StringComparison.Ordinal);
        int body = md.IndexOf('\n', fence) + 1;
        int end = md.IndexOf("```", body, StringComparison.Ordinal);
        return md[body..end].Trim();
    }

    [Fact]
    public void DocSection4_Example_Parses()
    {
        var json = ExtractSection4Json();
        var script = EffectScriptContract.Parse(json); // 修复前：FormatException「缺少字段: scope」
        Assert.NotEmpty(script.Events);
        Assert.Equal(2, script.Events.Length);
    }

    [Fact]
    public void DocSection4_RoundTrip_Idempotent()
    {
        var json = ExtractSection4Json();
        var once = EffectScriptContract.ToJson(EffectScriptContract.Parse(json));
        var twice = EffectScriptContract.ToJson(EffectScriptContract.Parse(once));
        Assert.Equal(once, twice); // 二次往返字节级稳定
    }

    // ── F2/F6：事件/claim 层未知键拒绝，报错带 events[i] 定位 ──
    [Fact]
    public void EventLevel_UnknownKey_Rejected_WithLayerIndex()
    {
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[],"Loop":"⊤"}]}""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：静默吞掉大写 Loop
        Assert.Contains("events[0]", ex.Message);
        Assert.Contains("Loop", ex.Message);
    }

    [Fact]
    public void ClaimLevel_UnknownKey_Rejected()
    {
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"gpu":"g"},"mode":"use","scope":{"scene":"S"},"sizes":[1,1]}]}]}""";
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // sizes 拼写错
    }

    [Fact]
    public void MissingField_Message_CarriesEventIndex()
    {
        var json = """{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[]},{"lifetime":[0,5],"scope":{"scene":"S"},"footprint":[]},{"scope":{"scene":"S"},"footprint":[]}]}""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("events[2]", ex.Message); // 第 3 个事件缺 lifetime
    }

    // ── F3：default(LoopCount) 后门在构造期封死 ──
    [Fact]
    public void DefaultLoopCount_Rejected_AtConstruction()
    {
        var sig = Signature.Of(Oc(new ResourceId.Gpu(new Rid("g")), Mode.Use, Scene("S")));
        var ex = Assert.ThrowsAny<ArgumentException>(
            () => new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(10)), Scene("S"), sig, default)); // 修复前：构造成功，Audit 时 DivideByZeroException
        Assert.IsNotType<DivideByZeroException>(ex);
    }

    // ── F4：非字符串注入点统一 FormatException（异常类型契约对称） ──
    [Theory]
    [InlineData("""{"events":[{"lifetime":[0,10],"scope":{"scene":42},"footprint":[]}]}""", "scene")]
    [InlineData("""{"events":[{"lifetime":[0,10],"scope":{"type":7},"footprint":[]}]}""", "type")]
    [InlineData("""{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[{"kind":42,"resource":{"gpu":"g"},"mode":"use","scope":{"scene":"S"}}]}]}""", "kind")]
    [InlineData("""{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[{"kind":"occupy","resource":{"gpu":"g"},"mode":true,"scope":{"scene":"S"}}]}]}""", "mode")]
    public void NonStringField_Throws_FormatException(string json, string field)
    {
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：InvalidOperationException（BCL 官话）
        Assert.Contains(field, ex.Message);
    }

    [Fact]
    public void Budget_NonNumberValue_Throws_FormatException()
    {
        var json = """{"events":[],"budget":{"gpu:x":true}}""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json)); // 修复前：InvalidOperationException
        Assert.Contains("gpu:x", ex.Message);
    }

    // ── F5：零字段 scope 对象拒绝（不许凭空捏匿名身份） ──
    [Fact]
    public void EmptyScopeObject_Rejected()
    {
        var json = """{"events":[{"lifetime":[0,10],"scope":{},"footprint":[]}]}"""; // 修复前：Scene("") 参与冲突分组
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }
}
