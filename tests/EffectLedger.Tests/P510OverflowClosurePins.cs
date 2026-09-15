// P510OverflowClosurePins.cs — P5-10-07（P5-8-08）修复钉：L1 Audit 闭包/扫换线路径的 Int128 中间累加。
// 缺陷：gate(1) 逐事件 `cur.Add(contrib)` 走 ZStar+（unchecked long，环绕 ⇒ ⊤ 且不具结合律）——
// 中间和超 long 域即变号，`ContainsZero` 假 false ⇒ 数学上闭合（net=0）的剧本被误报 Leak。
// 反例（本文件钉 1）：create 2^62 + create(2^62+1) + release(2^62+1) + release 2^62，数学 net=0；
// 两 create 累加中途 2^63+1 环绕。修复：与 P5-8-01 的 NetTable 同型，改 Int128 中间累加，
// 仅最终结果超 long 域才取 ⊤（真实越界 fail-closed）。
// 钉 2/3 是对照与方向守卫：小值同形态行为不变（防修复改变语义）；真泄漏仍必须检出（防修成假绿）。
using System.Linq;
using Xunit;

namespace EffectLedger.Tests;

public class P510OverflowClosurePins
{
    private const ulong A = 1UL << 62;          // 2^62
    private const ulong B = (1UL << 62) + 1;    // 2^62 + 1（A+A = 2^63 溢出 long）

    // 单资源 memory:1、单事件四 Claim（两 create 两 release，size 互异故集合不去重）。
    private static string Script(ulong c1, ulong c2, ulong r1, ulong r2) => $$"""
        {
          "events": [
            { "lifetime": [0, 40], "scope": {"scene":"S"},
              "footprint": [
                { "kind":"occupy", "resource": {"memory": 1}, "mode":"create",  "size": [{{c1}}, {{c1}}] },
                { "kind":"occupy", "resource": {"memory": 1}, "mode":"create",  "size": [{{c2}}, {{c2}}] },
                { "kind":"occupy", "resource": {"memory": 1}, "mode":"release", "size": [{{r1}}, {{r1}}] },
                { "kind":"occupy", "resource": {"memory": 1}, "mode":"release", "size": [{{r2}}, {{r2}}] } ] }
          ]
        }
        """;

    // ── 钉 1：中间和溢出但数学闭合 ⇒ 不得误报 Leak（修复前：Leak，且违反 fail-closed 之外还语义错误）。 ──
    [Fact]
    public void Closure_MidSumOverflowsLong_ButNetZero_NoLeak()
    {
        var script = EffectScriptContract.Parse(Script(B, A, B, A));
        var audit = script.Audit(script.Budget);
        Assert.DoesNotContain(audit.Violations, v => v.Kind == "Leak");
        // 该剧本本身自洽（无峰值预算 ⇒ 峰值门不运行；同资源 create×create 在同事件内是集合成员而非并发冲突）
        Assert.True(audit.Passed, $"溢出反例应通过，实际违例：{string.Join(",", audit.Violations.Select(v => $"{v.Kind}@{v.Resource}"))}");
    }

    // ── 钉 2：小值同形态行为不变（修复不得改变既有语义）。 ──
    [Fact]
    public void Closure_SmallValues_SameShape_Passes()
    {
        var script = EffectScriptContract.Parse(Script(5, 3, 5, 3));
        var audit = script.Audit(script.Budget);
        Assert.True(audit.Passed, $"小值对照应通过：{string.Join(",", audit.Violations.Select(v => $"{v.Kind}@{v.Resource}"))}");
    }

    // ── 钉 3（方向守卫）：溢出量级的【真】泄漏必须照常检出——防修复退化为假绿。 ──
    [Fact]
    public void Closure_OverflowMagnitude_RealLeak_StillDetected()
    {
        // 只有两条 create（净 2^62 + (2^62+1)，超 long 域）、无任何 release ⇒ 须判 Leak（fail-closed 保持）
        var onlyCreates = $$"""
            {
              "events": [
                { "lifetime": [0, 40], "scope": {"scene":"S"},
                  "footprint": [
                    { "kind":"occupy", "resource": {"memory": 1}, "mode":"create", "size": [{{B}}, {{B}}] },
                    { "kind":"occupy", "resource": {"memory": 1}, "mode":"create", "size": [{{A}}, {{A}}] } ] }
              ]
            }
            """;
        var script = EffectScriptContract.Parse(onlyCreates);
        var audit = script.Audit(script.Budget);
        Assert.Contains(audit.Violations, v => v.Kind == "Leak");
        Assert.False(audit.Passed);
    }

    // ── 钉 4（方向守卫）：溢出量级的部分释放（净 > 0）仍判 Leak。 ──
    [Fact]
    public void Closure_OverflowMagnitude_PartialRelease_StillLeaks()
    {
        var partial = $$"""
            {
              "events": [
                { "lifetime": [0, 40], "scope": {"scene":"S"},
                  "footprint": [
                    { "kind":"occupy", "resource": {"memory": 1}, "mode":"create",  "size": [{{B}}, {{B}}] },
                    { "kind":"occupy", "resource": {"memory": 1}, "mode":"release", "size": [1, 1] } ] }
              ]
            }
            """;
        var script = EffectScriptContract.Parse(partial);
        var audit = script.Audit(script.Budget);
        Assert.Contains(audit.Violations, v => v.Kind == "Leak");
    }

    // ── 钉 5：扫换线路径（NegativeDip 判定）同用 Int128——溢出量级不得伪造"负陷"。 ──
    // 形态：先大额 release 再大额 create 会真正下溢（应报 NegativeDip）；此处验证【不下溢】的等价剧本不误报。
    [Fact]
    public void SweepLine_OverflowMagnitude_NoSpuriousNegativeDip()
    {
        var script = EffectScriptContract.Parse(Script(B, A, B, A));
        var audit = script.Audit(script.Budget);
        Assert.DoesNotContain(audit.Violations, v => v.Kind == "NegativeDip");
    }
}
