using System;
using System.Collections.Immutable;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

// R6-RB 独立审计（audit/effect-api-auditR6-RB.md）收口回归钉：
// 失败方言单一（FormatException）+ 消息可定位 + 重复键拒绝 + 控制字符拒绝。
public class ProdAuditR6ContractTests
{
    const string BaseEvent = """
    { "lifetime":[0,6], "scope":{"scene":"Battle"},
      "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] }
    """;

    static string Script(string eventsJson, string? budget = null) =>
        $$"""{ "events": [ {{eventsJson}} ]{{(budget is null ? "" : $", \"budget\": {budget}")}} }""";

    // ── R6RB-01（MEDIUM）：非对象 claim 须落 FormatException 方言，不得泄 InvalidOperationException ──
    // 修改前：ParseClaim 仅在 Object 时做白名单，Require→TryGetProperty 对 Array 泄 BCL InvalidOperationException，
    //         库侧 catch(FormatException) 调用方必漏接（ParseEvent 事件层有同型守卫，claim 层漏）。
    [Theory]
    [InlineData("""{ "lifetime":[0,6], "scope":{"scene":"S"}, "footprint":[[0,1]] }""")]          // claim 为数组
    [InlineData("""{ "lifetime":[0,6], "scope":{"scene":"S"}, "footprint":[42] }""")]             // claim 为数字
    [InlineData("""{ "lifetime":[0,6], "scope":{"scene":"S"}, "footprint":["kind"] }""")]         // claim 为字符串
    public void Parse_NonObjectClaim_IsFormatException(string ev)
    {
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(Script(ev)));
        Assert.Contains("events[0][0]", ex.Message); // 定位到 claim 层（金标准路径口径）
    }

    // ── R6RB-04（LOW→按 R3-L1-03 教义收口）：重复 JSON 键静默 last-win 是结构性假绿向量 ──
    // 修改前：根级 "events" 重复（一空一非空）被静默取后者 ⇒ 含泄漏事件的剧本审计全绿。
    [Fact]
    public void Parse_DuplicateRootEventsKey_IsRejected()
    {
        const string json = """
        {
          "events": [ { "lifetime":[0,6], "scope":{"scene":"Battle"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ],
          "events": []
        }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("events", ex.Message);   // 指认重复键名
        Assert.Contains("重复", ex.Message);
    }

    [Fact]
    public void Parse_DuplicateBudgetKey_IsRejected()
    {
        const string json = """
        {
          "events": [ { "lifetime":[0,6], "scope":{"scene":"Battle"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ],
          "budget": { "gpu:x": 5, "gpu:x": 10 }
        }
        """;
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }

    [Fact]
    public void Parse_DuplicateKeyInsideEvent_IsRejected()
    {
        const string json = """
        {
          "events": [ { "lifetime":[0,6], "lifetime":[0,3], "scope":{"scene":"S"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ]
        }
        """;
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }

    // 合法剧本不受重复键预扫描影响（负对照：仍可 Parse + Audit）
    [Fact]
    public void Parse_ValidScript_StillParses()
    {
        var s = EffectScriptContract.Parse(Script(BaseEvent, """{"gpu:x": 600}"""));
        Assert.Single(s.Events);
        Assert.True(s.Audit().IsPeakChecked); // 预算门真的跑过
    }

    // ── R6RB-03（LOW）：失败消息全路径可定位 ──
    [Fact]
    public void Parse_UnknownKind_MessageCarriesEventLocation()
    {
        const string json = """
        { "events": [ { "lifetime":[0,6], "scope":{"scene":"S"},
                        "footprint":[{"kind":"zap","resource":{"gpu":"x"},"mode":"create"}] } ] }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("events[0][0]", ex.Message);
        Assert.Contains("未知 kind", ex.Message);
    }

    [Fact]
    public void Parse_UnknownMode_MessageCarriesEventLocation()
    {
        const string json = """
        { "events": [ { "lifetime":[0,6], "scope":{"scene":"S"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"destroy"}] } ] }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("events[0][0]", ex.Message);
        Assert.Contains("未知 mode", ex.Message);
    }

    // 修改前：ReqStr 硬编码 "resource." 前缀 ⇒ kind=42 报 "resource.events[0][0].kind"（路径撒谎）
    [Fact]
    public void Parse_NonStringKind_MessagePathIsNotMisleading()
    {
        const string json = """
        { "events": [ { "lifetime":[0,6], "scope":{"scene":"S"},
                        "footprint":[{"kind":42,"resource":{"gpu":"x"},"mode":"create"}] } ] }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.DoesNotContain("resource.events", ex.Message); // 不得把 kind 字段报成 resource 路径
        Assert.Contains("events[0][0].kind", ex.Message);
    }

    // 修改前：lifetime/loop 消息缺事件索引（10 万事件剧本中无法定位是哪条）
    [Fact]
    public void Parse_ReversedLifetime_MessageCarriesEventIndex()
    {
        const string json = """
        { "events": [ { "lifetime":[6,0], "scope":{"scene":"S"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ] }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("events[0].lifetime", ex.Message);
    }

    [Fact]
    public void Parse_BadLoop_MessageCarriesEventIndex()
    {
        const string json = """
        { "events": [ { "lifetime":[0,6], "scope":{"scene":"S"}, "loop":-1,
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ] }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("events[0].loop", ex.Message);
    }

    // ── R6RB-06（NOTE→按 A1-12 同口径收口）：资源 id/scope 名拒 C0 控制字符 ──
    // NUL 破坏下游日志/原生互操作，RTL 覆写符可伪装报告文本；budget 键侧 A1-12 已拒空 id，
    // claim 侧字符串 id 同为报告/分组身份，控制字符静默接受与 fail-fast 立场相悖。
    [Theory]
    [InlineData("\u0000")]
    [InlineData("\u0007")]
    [InlineData("\u001F")]
    public void Parse_ResourceIdWithControlChar_IsRejected(string bad)
    {
        // 注入 JSON 转义形式（裸控制字符在 JSON 文本层本就非法，须走 \uXXXX 才能到达 ReqStr 身份检查）
        string ev = $$"""{ "lifetime":[0,6], "scope":{"scene":"S"}, "footprint":[{"kind":"occupy","resource":{"gpu":"a\u{{((int)bad[0]):x4}}b"},"mode":"create"}] }""";
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(Script(ev)));
        Assert.Contains("控制字符", ex.Message);
    }

    [Fact]
    public void Parse_SceneNameWithControlChar_IsRejected()
    {
        const string json = """
        { "events": [ { "lifetime":[0,6], "scope":{"scene":"Ba\u0000ttle"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ] }
        """;
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }

    [Fact]
    public void Parse_BudgetKeyIdWithControlChar_IsRejected()
    {
        const string json = """
        { "events": [ { "lifetime":[0,6], "scope":{"scene":"S"},
                        "footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create"}] } ],
          "budget": { "gpu:a\u0001b": 5 } }
        """;
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }
}
