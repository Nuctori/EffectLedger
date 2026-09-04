// ProdAuditR3ContractTests.cs — 第三轮独立审计（2026-09）L1 契约层回归钉。
// R3-L1-01：Parse 对 JSON 语法错误须落 FormatException 方言（此前漏裸 JsonException，catch(FormatException) 必漏接）。
// R3-L1-02：CosmosEffectConfig 解析须全量 FormatException 方言（read+create 的 ArgumentException 曾击穿 LoadExtra 非 strict 回落）。
// R3-L1-03：resource 多键须拒绝（schema maxProperties:1 同界；此前按固定优先级静默择一改写资源身份）。
// R3-L1-04：scope 对象未知键须拒绝（与根/事件/claim 层白名单同口径，防 "typ" 拼写静默降级致 gate(3) 假绿）。
// R3-L1-05：budget 键 "memory:" 空段须拒绝（与 schema ^memory:\d+$ 及 A1-12 空拒口径同界；"memory:0" 是合法拼写）。
using System;
using System.IO;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public sealed class ProdAuditR3ContractTests
{
    // ── R3-L1-01：JSON 语法错误的异常方言 ──
    [Theory]
    [InlineData("{")]
    [InlineData("not json at all")]
    [InlineData("{\"events\": [},]")]
    public void Parse_SyntaxError_ThrowsFormatException_NotJsonException(string json)
    {
        var ex = Assert.ThrowsAny<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.IsType<FormatException>(ex); // 精确方言：不是 ArgumentException 等近亲
    }

    // ── R3-L1-03：resource 多键静默择一 → 拒绝 ──
    [Fact]
    public void Parse_ResourceMultiKey_IsRejected()
    {
        const string json = """
        {
          "events": [
            { "lifetime":[0,6], "scope":{"scene":"Battle"},
              "footprint":[{"kind":"occupy","resource":{"memory":3,"gpu":"x"},"mode":"create"}] }
          ]
        }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("resource", ex.Message);
    }

    // ── R3-L1-04：scope 层未知键 → 拒绝 ──
    [Fact]
    public void Parse_ScopeUnknownKey_IsRejected()
    {
        const string json = """
        {
          "events": [
            { "lifetime":[0,6], "scope":{"scene":"HUD","typ":"method"},
              "footprint":[{"kind":"occupy","resource":{"gpu":"a"},"mode":"create"}] }
          ]
        }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("typ", ex.Message);
    }

    // ── R3-L1-05：budget "memory:" 空段 → 拒绝（"memory:0" 合法） ──
    [Fact]
    public void Parse_BudgetMemoryEmptySuffix_IsRejected()
    {
        const string json = """
        {
          "events": [
            { "lifetime":[0,6], "scope":{"scene":"Battle"},
              "footprint":[{"kind":"occupy","resource":{"memory":0},"mode":"create"}] }
          ],
          "budget": { "memory:": 5 }
        }
        """;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("memory:", ex.Message);

        // 对照：显式 0 段是合法拼写且语义等价（Memory(0)）
        const string ok = """
        {
          "events": [
            { "lifetime":[0,6], "scope":{"scene":"Battle"},
              "footprint":[{"kind":"occupy","resource":{"memory":0},"mode":"create"}] }
          ],
          "budget": { "memory:0": 5 }
        }
        """;
        Assert.NotNull(EffectScriptContract.Parse(ok));
    }

    // ── R3-L1-02：CosmosEffectConfig 全量 FormatException 方言 + 非 strict 回落 ──

    // 前置钉（R3-TQ-01 零覆盖腐烂实证）：合法 extraMappings 须能解析出映射（修复前 ParseClaim 把
    // 整个 claim 对象当值传给 ReqStr，任何输入都抛「kind 须为字符串」，API 完全不可用）。
    [Fact]
    public void LoadExtraFromJson_ValidMapping_Parses()
    {
        const string json = """
        {
          "extraMappings": [
            { "api": "MyNode.DoThing",
              "claims": [ { "kind": "write", "resource": { "gpu": "x" }, "mode": "use", "scope": { "scene": "S" } } ] }
          ]
        }
        """;
        var extra = CosmosEffectConfig.LoadExtraFromJson(json);
        Assert.Single(extra);
        Assert.Equal("MyNode.DoThing", extra[0].GodotApi);
    }

    [Fact]
    public void AllWithExtra_ExtraOverridesSameCanonical()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cosmos-r3-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "extraMappings": [
                { "api": "my_node.add_child",
                  "claims": [ { "kind": "read", "resource": { "custom": "probe-canonical-override" }, "mode": "use", "scope": { "scene": "S" } } ] }
              ]
            }
            """);
            var all = CosmosEffectConfig.AllWithExtra(path, strict: true);
            var probe = GodotApiWhitelist.Canonical("my_node.add_child");
            Assert.Contains(all, m => GodotApiWhitelist.Canonical(m.GodotApi) == probe
                && m.Claims[0].Resource is ResourceId.Custom c && c.Name == "probe-canonical-override");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadExtraFromJson_ReadPlusCreate_ThrowsFormatException_NotArgumentException()
    {
        const string json = """
        {
          "extraMappings": [
            { "api": "MyNode.DoThing",
              "claims": [ { "kind": "read", "resource": { "gpu": "x" }, "mode": "create", "scope": { "scene": "S" } } ] }
          ]
        }
        """;
        var ex = Assert.ThrowsAny<Exception>(() => CosmosEffectConfig.LoadExtraFromJson(json));
        Assert.IsType<FormatException>(ex);
    }

    [Fact]
    public void LoadExtraFromJson_CollisionWithinExtra_ThrowsFormatException()
    {
        // 同 Canonical 的两条额外映射 → ValidateNoCollisions 的 ArgumentException 须翻为契约方言
        const string json = """
        {
          "extraMappings": [
            { "api": "MyNode.DoThing",
              "claims": [ { "kind": "write", "resource": { "gpu": "x" }, "mode": "use", "scope": { "scene": "S" } } ] },
            { "api": "my_node.do_thing",
              "claims": [ { "kind": "read", "resource": { "gpu": "x" }, "mode": "use", "scope": { "scene": "S" } } ] }
          ]
        }
        """;
        Assert.Throws<FormatException>(() => CosmosEffectConfig.LoadExtraFromJson(json));
    }

    [Fact]
    public void LoadExtra_NonStrict_BadMappingFallsBackWithWarning()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cosmos-r3-{Guid.NewGuid():N}.json");
        try
        {
            // read+create：修复前 ArgumentException 不在回落过滤器内，直接炸出（击穿非 strict 回落承诺）
            File.WriteAllText(path, """
            {
              "extraMappings": [
                { "api": "MyNode.DoThing",
                  "claims": [ { "kind": "read", "resource": { "gpu": "x" }, "mode": "create", "scope": { "scene": "S" } } ] }
              ]
            }
            """);
            var extra = CosmosEffectConfig.LoadExtra(path, strict: false);
            Assert.True(extra.IsEmpty, "非 strict 解析失败须回落内置（空 extra）");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadExtra_Strict_BadMappingThrowsFormatException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cosmos-r3-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "extraMappings": [
                { "api": "MyNode.DoThing",
                  "claims": [ { "kind": "read", "resource": { "gpu": "x" }, "mode": "create", "scope": { "scene": "S" } } ] }
              ]
            }
            """);
            Assert.Throws<FormatException>(() => CosmosEffectConfig.LoadExtra(path, strict: true));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
