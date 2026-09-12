// QedP53DialectHardeningPins.cs — P5.3 方言审计 HIGH 处置钉（budget 键空白 id 幽灵预算）：
// 资源 id 的前后空白 = LLM/JSON 书写签名级拼写错误（"custom: residency" 冒号后空格是经典形态），
// budget 与 claim 拼写失配时 gate 静默失配（幽灵预算虚增 CapsChecked）——拒绝而非 Trim 改写
// （R3-L1-03 教义：拒绝而非改写）。嵌入空格合法（身份逐字精确匹配）。xUnit。
using System.Collections.Immutable;
using Xunit;

namespace EffectLedger.Tests;

public class QedP53DialectHardeningPins
{
    static string Script(string budgetKey, string claimRes) => $$"""
        {
          "events": [
            { "lifetime": [0, 10], "scope": { "scene": "S" },
              "footprint": [ { "kind": "occupy", "resource": { "custom": {{claimRes}} }, "mode": "create", "size": [4, 4] } ] }
          ],
          "budget": { {{budgetKey}}: 8 }
        }
        """;

    // ── 钉 1：budget 键前后空白 id ⇒ loud FormatException（拒绝而非改写）。 ──
    [Theory]
    [InlineData("custom: residency")]     // 冒号后空格（审计 HIGH 的原始反例形态）
    [InlineData("custom:residency ")]     // 尾随空格
    [InlineData("custom:  ")]             // 纯空白 id（Trim 后为空）
    public void BudgetKey_WhitespacePaddedId_Throws(string key)
    {
        var ex = Assert.Throws<FormatException>(
            () => EffectLedger.EffectScriptContract.Parse(Script($"\"{key}\"", "\"residency\"")));
        Assert.Contains("空白", ex.Message);
    }

    // ── 钉 2：claim 资源 id 前后空白 ⇒ 同样 loud（budget/claim 双侧对称，拼写失配无处遁形）。 ──
    [Theory]
    [InlineData("\" res\"")]
    [InlineData("\"res \"")]
    public void ClaimResource_WhitespacePaddedId_Throws(string claimRes)
    {
        var ex = Assert.Throws<FormatException>(
            () => EffectLedger.EffectScriptContract.Parse(Script("\"custom:residency\"", claimRes)));
        Assert.Contains("空白", ex.Message);
    }

    // ── 钉 3：嵌入空格合法（身份逐字精确匹配——两侧一致即正常审计）。 ──
    [Fact]
    public void ClaimResource_InternalSpace_RemainsLegal()
    {
        var script = EffectLedger.EffectScriptContract.Parse(Script("\"custom:residency\"", "\"my res\""));
        Assert.NotEmpty(script.Events);
    }

    // ── 钉 4（方言审计 MED）：type:"global" 与 scene 并存 ⇒ loud 拒绝——
    //    静默丢弃 scene 名 = 拒绝而非改写教义的反例（拼写失配会让冲突分组漂移）。 ──
    [Fact]
    public void Scope_GlobalWithSceneName_Throws()
    {
        var json = """
            {
              "events": [
                { "lifetime": [0, 5], "scope": { "scene": "S", "type": "global" },
                  "footprint": [ { "kind": "occupy", "resource": { "memory": 1 }, "mode": "use" } ] }
              ]
            }
            """;
        var ex = Assert.Throws<FormatException>(
            () => EffectLedger.EffectScriptContract.Parse(json));
        Assert.Contains("并存矛盾", ex.Message);
    }

    // ── 钉 5（回归守卫）：纯 {"type":"global"}（无 scene）仍是合法 Global。 ──
    [Fact]
    public void Scope_GlobalWithoutScene_RemainsLegal()
    {
        var json = """
            {
              "events": [
                { "lifetime": [0, 5], "scope": { "type": "global" },
                  "footprint": [ { "kind": "occupy", "resource": { "memory": 1 }, "mode": "use" } ] }
              ]
            }
            """;
        Assert.NotEmpty(EffectLedger.EffectScriptContract.Parse(json).Events);
    }
}
