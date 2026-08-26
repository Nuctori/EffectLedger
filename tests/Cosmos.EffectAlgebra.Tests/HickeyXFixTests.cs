// HickeyXFixTests.cs — hickey-x 十轮审计修复轮回归测试（R6-E1/E3、R2-N1、R1-HIGH-3、§4 示例、R4-V2）。xUnit。
using System.Collections.Immutable;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class HickeyXFixTests
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    static string MinimalJson(string budget = "") =>
        $$"""
        {
          "events": [
            { "lifetime": [0, 10], "scope": {"scene":"S"},
              "footprint": [
                { "kind": "occupy", "resource": {"gpu": "bufA"}, "mode": "create",
                  "scope": {"scene":"S"}, "size": [1,1] }
              ] }
          ]{{budget}}
        }
        """;

    // ════════ R6-E1：根级未知键 fail-fast ════════
    [Theory]
    [InlineData("budgat")]     // 拼写错误
    [InlineData("Budget")]     // 大小写
    public void Parse_RejectsUnknownRootKeys(string badKey)
    {
        var json = MinimalJson($",\n  \"{badKey}\": {{ \"gpu:bufA\": 5 }}");
        var ex = Assert.Throws<FormatException>(() => Cosmos.EffectAlgebra.EffectScriptContract.Parse(json));
        Assert.Contains(badKey, ex.Message);
        Assert.Contains("events", ex.Message); // 报错列出合法键集
    }

    // ════════ R6-E3：budget 键存在但类型错 ⇒ 抛（不再静默禁用 gate(2)） ════════
    [Fact]
    public void Parse_RejectsNonObjectBudget()
    {
        var json = MinimalJson(",\n  \"budget\": []");
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("budget", ex.Message);
    }

    // ════════ R2-N1：⊤ 预算 round-trip 不再翻转为 0（虚假 Leak 消除） ════════
    [Fact]
    public void SerializeBudget_TopRoundTripsAsTop_NoFalseLeak()
    {
        var caps = new System.Collections.Generic.Dictionary<Cosmos.EffectAlgebra.ResourceId, NatStar>
            { [new ResourceId.Gpu(new Rid("bufTop"))] = NatStar.Top };
        var script = new EffectScript(
            ImmutableArray.Create(new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), Scene("S"),
                Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("bufTop")), Mode.Create, Scene("S"), Interval.Exact(1)).Normalize()))),
            new Budget(caps));

        var json = EffectScriptContract.ToJson(script);
        Assert.Contains("\"⊤\"", json);                       // 序列化侧写出 ⊤ 而非 0

        var reparsed = EffectScriptContract.Parse(json);
        Assert.True(reparsed.Budget.Caps[new ResourceId.Gpu(new Rid("bufTop"))].IsTop); // 语义保真

        var aud = reparsed.Audit();
        Assert.DoesNotContain(aud.Violations, v => v.Kind == "PeakExceeded"); // 不再因预算 0 报虚假违例
    }

    [Fact]
    public void ParseBudget_AcceptsInfAliasForTop()
    {
        var json = MinimalJson(",\n  \"budget\": { \"gpu:bufA\": \"inf\" }");
        var s = EffectScriptContract.Parse(json);
        Assert.True(s.Budget.Caps[new ResourceId.Gpu(new Rid("bufA"))].IsTop);
    }

    // ════════ R1-HIGH-3：CapsChecked 覆盖面计数——零预算时「没查」可观测 ════════
    [Fact]
    public void Audit_ReportsCapsChecked_ZeroMeansGateNotRun()
    {
        var evts = ImmutableArray.Create(new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), Scene("S"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("g")), Mode.Create, Scene("S"), Interval.Exact(1)).Normalize())));

        var noBudget = new EffectScript(evts).Audit();
        Assert.Equal(0, noBudget.CapsChecked);   // 峰值门未运行——Passed=true 不再冒充全绿

        var withBudget = new EffectScript(evts,
            new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar> { [new ResourceId.Gpu(new Rid("g"))] = NatStar.Of(9) })).Audit();
        Assert.Equal(1, withBudget.CapsChecked); // 实际检查了 1 个资源
    }

    // ════════ §4 文档示例可解析（示例固化为契约测试） ════════
    [Fact]
    public void DocSection4_ExampleParses()
    {
        var json = """
        {
          "events": [
            { "lifetime": [0, 120], "loop": 1,
              "scope": { "scene": "Battle" },
              "footprint": [
                { "kind": "occupy", "resource": {"gpu": "mesh1"}, "mode": "create",
                  "scope": {"scene":"Battle"}, "size": [1,1] },
                { "kind": "occupy", "resource": {"commandBuffer":"gpu"}, "mode": "create",
                  "scope": {"scene":"Battle"}, "size": [1,1] }
              ] },
            { "lifetime": [60, 180], "loop": 1,
              "scope": { "scene": "Battle" },
              "footprint": [
                { "kind": "occupy", "resource": {"commandBuffer":"gpu"}, "mode": "release",
                  "scope": {"scene":"Battle"}, "size": [1,1] }
              ] }
          ],
          "budget": { "commandBuffer:gpu": 64 }
        }
        """;
        var s = EffectScriptContract.Parse(json);   // R8-TOP1：官方示例必须能过自家 Parse
        Assert.Equal(2, s.Events.Length);
        Assert.Equal((ulong)64, s.Budget.Caps[new ResourceId.CommandBuffer("gpu")].Value);
    }

    // ════════ R4-V2：PeakExceeded 违例上报归一化资源键 ════════
    [Fact]
    public void PeakExceeded_ReportsNormalizedKey()
    {
        // claim 用 Self("signal_x") 归一为 SignalBus("x")；budget 键写 SignalBus 别名形态，违例应报归一键。
        var evts = ImmutableArray.Create(new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(5)), Scene("S"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.SignalBus(new StringName("x")), Mode.Create, Scene("S"), Interval.Exact(3)).Normalize())));
        var script = new EffectScript(evts,
            new Budget(new System.Collections.Generic.Dictionary<ResourceId, NatStar> { [new ResourceId.SignalBus(new StringName("x"))] = NatStar.Of(1) }));
        var aud = script.Audit();
        var peak = Assert.Single(aud.Violations, v => v.Kind == "PeakExceeded");
        Assert.Equal(ResourceId.Normalize(new ResourceId.SignalBus(new StringName("x"))), peak.Resource); // 与查找同键面
    }
}
