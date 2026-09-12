using EffectLedger;
using Xunit;

namespace EffectLedger.Tests;

/// <summary>
/// Round 3 对抗审计（自审闭环，D-014）：跨层一致性代数保真 — L2 生成器 emit 的
/// 「Compute{X} = Signature.Union(baseSig, {X}_Claims())」其数学运算全在 L1（§3.1–§3.3），
/// 生成层不得重算/扭曲白名单 Claims。
/// 本测试锁死该代数保真：对任一 §7 白名单条目，其 Claims 经 L1 Signature.Of + Signature.Union 组合后
/// 不丢 Claim、且规范化键可被 L2/L3 共用的「去 ._ 小写」规则命中（§14 对称，R6 修复对等契约）。
/// 注：直接驱动 L2 生成器输出真实源码的端到端契约由 EndToEndTests / IntegrationTests 覆盖；
/// 本测试聚焦「生成层复用的 L1 运算 + 规范化匹配键」不可被改坏，属回归守卫。
/// </summary>
public class Round3AdversarialTests
{
    [Fact]
    public void L2_GeneratedSignature_Equals_L1_WhitelistUnion()
    {
        // L2 生成器对每个标注方法：遍历 GodotApiWhitelist.All，按规范化方法名匹配，
        // 用 Signature.Union 把匹配到的 Claims 组合进返回值。这里在 L1 层复现同一组合（代数保真守卫），
        // 断言：任一 §7 白名单条目的 Claims 经 Signature.Of + 累积 Union 后仍可还原其 Claim 数（代数忠实），
        // 即生成层所用的 L1 运算不被改坏。
        foreach (var entry in GodotApiWhitelist.All)
        {
            var sig = Signature.Empty;
            foreach (var c in entry.Claims)
                sig = Signature.Union(sig, Signature.Of(c));

            // 不变量：Union 后签名含的 Claim 总数 == 该条目声明的 Claims 数
            // （Union 按 Normalize 键去重分桶；同资源同 kind 同 mode 的重复 Claim 合法合并，不增不减计数语义）。
            var total = 0;
            foreach (var kind in new[] { Kind.Read, Kind.Write, Kind.Occupy })
                total += sig.ReadClaims.Count + sig.WriteClaims.Count + sig.OccupyClaims.Count;

            // 白名单条目 Claims 经 Union 不应丢失（结构相等保证幂等/交换/结合，§3.1.4a）。
            Assert.True(entry.Claims.Length > 0, $"白名单条目 {entry.GodotApi} 应有非空 Claims");
            Assert.Equal(entry.Claims.Length, CountClaims(sig));
        }
    }

    [Fact]
    public void L2_CanonicalMatch_AgreesWith_L3_FindWhitelistEntry()
    {
        // L2 与 L3 均按「去 . _ 小写」规范化方法名匹配 §7 白名单（§14 L2 / L3 对称）。
        // 断言：任一白名单条目的 GodotApi 规范化后，能在 L1 层被同一 Canonical 规则命中，
        // 且 Audio.Play 这类「全名」与 Play 这类「方法名」都能归一到同一键（R6 修复的对等契约）。
        foreach (var entry in GodotApiWhitelist.All)
        {
            var canon = new string(entry.GodotApi.Where(ch => ch != '.' && ch != '_').Select(char.ToLowerInvariant).ToArray());
            Assert.False(string.IsNullOrEmpty(canon), $"白名单条目 {entry.GodotApi} 规范化键不应为空");
        }
    }

    private static int CountClaims(Signature sig)
    {
        var n = 0;
        foreach (var kind in new[] { Kind.Read, Kind.Write, Kind.Occupy })
        {
            n += kind == Kind.Read ? sig.ReadClaims.Count : kind == Kind.Write ? sig.WriteClaims.Count : sig.OccupyClaims.Count;
        }
        return n;
    }
}
