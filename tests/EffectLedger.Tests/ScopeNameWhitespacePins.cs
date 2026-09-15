// ScopeNameWhitespacePins.cs — 审计 2026-09-15 修复钉：scope 名前后空白拒绝。
// 缺陷（实证）：Parse 的 ReqStr 只判 Length==0 ⇒ " S" 这类带前后空白的 scope 名被接受，而 gate(3)
// 按 (资源, scope, mode) 分组 ⇒ 同一资源同一时刻的两个 create 仅因 scope 名差一空格就从冲突分组
// 分裂出去，CompatibleConflict 静默消失（只报 Leak）。资源 id 侧早已有 NoPad 防护、EffectLedgerConfig
// 用 IsNullOrWhiteSpace，唯在 scope 名上留了洞——L1 权威审计层的漏报，比 EAA0304 静态近似更严重。
// 修复：ReqStr 纯空白按空串拒；ParseScope 的 scene 名过 NoPad（拒绝而非改写，与资源 id 同口径）。
using Xunit;

namespace EffectLedger.Tests;

public class ScopeNameWhitespacePins
{
    private const string TrailingSpaceScope = """
        {
          "events": [
            { "lifetime": [0, 10], "scope": {"scene": " S"},
              "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] }
          ]
        }
        """;
    private const string PureWhitespaceScope = """
        {
          "events": [
            { "lifetime": [0, 10], "scope": {"scene": "   "},
              "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] }
          ]
        }
        """;
    private const string LeadingSpaceTypeScope = """
        {
          "events": [
            { "lifetime": [0, 10], "scope": {"type": "method", "scene": "M "},
              "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] }
          ]
        }
        """;

    // ── 钉 1：前端/后端空白 scope 名一律 loud 拒绝（修复前：静默接受 ⇒ 冲突分组分裂）。 ──
    [Theory]
    [InlineData("leading-space", TrailingSpaceScope)]
    [InlineData("pure-whitespace", PureWhitespaceScope)]
    [InlineData("method-type-trailing-space", LeadingSpaceTypeScope)]
    public void ScopeName_PaddedOrWhitespace_ThrowsLoud(string label, string json)
    {
        _ = label;
        var ex = Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
        Assert.Contains("空白", ex.Message);
    }

    // ── 钉 2（回归守卫）：无空白的 scope 名照常接受——修复不得误伤合法输入。 ──
    [Fact]
    public void ScopeName_Clean_StillAccepted()
    {
        var script = EffectScriptContract.Parse("""
            {
              "events": [
                { "lifetime": [0, 10], "scope": {"scene": "S"},
                  "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] }
              ]
            }
            """);
        Assert.Single(script.Events);
    }

    // ── 钉 3（回归守卫）：嵌入空格仍合法（身份逐字精确匹配，仅前后空白被拒）——与 NoPad 注释口径一致。 ──
    [Fact]
    public void ScopeName_EmbeddedSpace_StillAccepted()
    {
        var script = EffectScriptContract.Parse("""
            {
              "events": [
                { "lifetime": [0, 10], "scope": {"scene": "Battle Scene"},
                  "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] }
              ]
            }
            """);
        Assert.Single(script.Events);
    }

    // ── 钉 4（威胁模型）：修复前该剧本漏报 CompatibleConflict；现因解析期拒绝，漏报路径不可达。 ──
    //     两 scope 相同时（"S" vs "S"）冲突照常报——证明分裂确实源于命名差异而非别的机制。
    [Fact]
    public void SameResourceSameMode_SameScopePair_ReportsConflict()
    {
        var script = EffectScriptContract.Parse("""
            {
              "events": [
                { "lifetime": [0, 10], "scope": {"scene": "S"},
                  "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] },
                { "lifetime": [0, 10], "scope": {"scene": "S"},
                  "footprint": [ { "kind":"occupy", "resource": {"gpu":"g"}, "mode":"create" } ] }
              ]
            }
            """);
        var audit = script.Audit(script.Budget);
        Assert.Contains(audit.Violations, v => v.Kind == "CompatibleConflict");
    }
}
