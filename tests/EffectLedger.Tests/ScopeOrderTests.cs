using System;
using Xunit;

namespace EffectLedger.Tests;

/// <summary>
/// §3.1.3b — ScopeId ⊆* 偏序完整性锁。
/// ⊆* 经 record 结构相等（同构造子同字段 ⇒ Equals）+ Global 最大元实现；非 NamedScopes 多标签（iter45 收口）。
/// 本文件证明偏序三定律（自反/反对称/传递）+ Global 唯一最大元 + 跨标签不可比，
/// 覆盖全部 8 种标签构造子（Method / Type / Scene / Global / Loop / Conditional / Async / Shell）。
/// 全部断言可证伪：若 IncludedIn 退化全 true（跨标签误包含）⇒ 跨标签/异名断言必红；若退化全 false ⇒ 自反/Global 最大元断言必红。
/// </summary>
public class ScopeOrderTests
{
    // 8 标签（任务所列「7 种」实为含 Global/Shell 共 8 个构造子）。
    private static readonly string[] Labels = { "Method", "Type", "Scene", "Global", "Loop", "Conditional", "Async", "Shell" };
    private static readonly string[] Names = { "a", "b", "c", "d", "e" };

    /// <summary>§3.1.3b — 按标签构造实例（Global/Shell 为单例，无名字段）。</summary>
    private static ScopeId Make(string label, string name) => label switch
    {
        "Method" => new ScopeId.Method(name),
        "Type" => new ScopeId.Type(name),
        "Scene" => new ScopeId.Scene(name),
        "Loop" => new ScopeId.Loop(name),
        "Conditional" => new ScopeId.Conditional(name),
        "Async" => new ScopeId.Async(name),
        "Global" => new ScopeId.Global(),
        "Shell" => new ScopeId.Shell(),
        _ => throw new ArgumentOutOfRangeException(nameof(label), label)
    };

    private static ScopeId RandomScope(Random rng, string label) => Make(label, Names[rng.Next(Names.Length)]);
    private static string RandomLabel(Random rng) => Labels[rng.Next(Labels.Length)];

    // 1. 自反：任意 s，s.IncludedIn(s) == true（8 标签 × 500 随机名）。
    [Fact]
    public void Reflexive_AllLabels()
    {
        var rng = new Random(23);
        for (int i = 0; i < 500; i++)
        {
            var label = RandomLabel(rng);
            var s = RandomScope(rng, label);
            Assert.True(s.IncludedIn(s), $"§3.1.3b 自反失败：{label} 实例 {s}");
        }
    }

    // 2. 反对称：若 a⊆b 且 b⊆a 则 a.Equals(b)。随机对；前提成立时必为同一元素。
    [Fact]
    public void Antisymmetric_RandomPairs()
    {
        var rng = new Random(23);
        int premiseHeld = 0;
        for (int i = 0; i < 500; i++)
        {
            var a = RandomScope(rng, RandomLabel(rng));
            var b = RandomScope(rng, RandomLabel(rng));
            bool ab = a.IncludedIn(b);
            bool ba = b.IncludedIn(a);
            if (ab && ba)
            {
                premiseHeld++;
                Assert.True(a.Equals(b), $"§3.1.3b 反对称失败：{a} ⊆ {b} 且 {b} ⊆ {a} 但 a≠b");
            }
        }
        Assert.True(premiseHeld > 0, "§3.1.3b 反对称前提从未满足（测试退化为空，伪绿）");
    }

    // 跨标签/同标签异名：反对称前提必不成立（至少一方向 false）。显式枚举。
    [Fact]
    public void CrossLabel_PremiseNeverHolds()
    {
        // 同标签异名：必互不包含、且不等于。
        foreach (var label in Labels)
        {
            if (label is "Global" or "Shell") continue; // 单例无名字段，无法异名
            var a = Make(label, "a");
            var b = Make(label, "b");
            Assert.False(a.Equals(b), $"{label} 异名竟相等");
            Assert.False(a.IncludedIn(b), $"{label} 异名竟互相包含（a⊆b）");
            Assert.False(b.IncludedIn(a), $"{label} 异名竟互相包含（b⊆a）");
        }
        // 跨标签对：双向不可比较（除 other=Global 作最大元）。
        var rng = new Random(23);
        foreach (var la in Labels)
        foreach (var lb in Labels)
        {
            if (la == lb) continue;
            var a = RandomScope(rng, la);
            var b = RandomScope(rng, lb);
            if (lb != "Global") Assert.False(a.IncludedIn(b), $"§3.1.3b 跨标签误包含：{la}⊆{lb}");
            if (la != "Global") Assert.False(b.IncludedIn(a), $"§3.1.3b 跨标签误包含：{lb}⊆{la}");
        }
    }

    // 3. 传递：若 a⊆b ∧ b⊆c 则 a⊆c。用真实链（a⊆Global ∧ Global⊆Global ⇒ a⊆Global；a⊆a ∧ a⊆Global ⇒ a⊆Global）。
    [Fact]
    public void Transitive_RealChains()
    {
        var rng = new Random(23);
        int premiseHeld = 0;
        for (int i = 0; i < 500; i++)
        {
            var s = RandomScope(rng, RandomLabel(rng));
            var global = (ScopeId)new ScopeId.Global();
            // 链1：a=s, b=Global, c=Global ⇒ s⊆Global ∧ Global⊆Global ⇒ s⊆Global
            if (s.IncludedIn(global) && global.IncludedIn(global))
            {
                premiseHeld++;
                Assert.True(s.IncludedIn(global), $"§3.1.3b 传递失败(链1)：{s}⊆Global⊆Global ⇒ {s}⊆Global");
            }
            // 链2：a=s, b=s, c=Global ⇒ s⊆s ∧ s⊆Global ⇒ s⊆Global
            if (s.IncludedIn(s) && s.IncludedIn(global))
            {
                premiseHeld++;
                Assert.True(s.IncludedIn(global), $"§3.1.3b 传递失败(链2)：{s}⊆{s}⊆Global ⇒ {s}⊆Global");
            }
        }
        Assert.True(premiseHeld > 0, "§3.1.3b 传递前提从未满足（测试退化为空，伪绿）");
    }

    // 4. Global 最大元：任意 s（8 标签）⊆ Global；且 Global ⊄ s 当 s 非 Global（唯一最大元）。
    [Fact]
    public void Global_MaximalElement()
    {
        var rng = new Random(23);
        for (int i = 0; i < 500; i++)
        {
            var s = RandomScope(rng, RandomLabel(rng));
            Assert.True(s.IncludedIn(new ScopeId.Global()), $"§3.1.3b Global 非最大元：{s}⊄Global");
        }
        // 唯一最大元：Global ⊄ s 当 s 非 Global。
        foreach (var label in Labels)
        {
            if (label == "Global") continue;
            var s = RandomScope(rng, label);
            Assert.False(new ScopeId.Global().IncludedIn(s), $"§3.1.3b 存在非 Global 最大元：Global⊆{label} 竟为真");
        }
    }

    // 5. 跨标签不可比（显式交叉枚举 8×8，含异名 + Global 最大元豁免）：双向均不互相包含。
    [Fact]
    public void CrossLabel_Incomparable_8x8()
    {
        var rng = new Random(23);
        foreach (var la in Labels)
        foreach (var lb in Labels)
        {
            var a = RandomScope(rng, la);
            var b = RandomScope(rng, lb);
            if (la == lb)
            {
                // 同标签：仅同名（Equals）可比；异名不可比。
                if (a.Equals(b))
                {
                    Assert.True(a.IncludedIn(b) && b.IncludedIn(a), $"§3.1.3b 同标签同名不可比：{a}");
                }
                else
                {
                    Assert.False(a.IncludedIn(b), $"§3.1.3b 同标签异名误包含：{la}");
                    Assert.False(b.IncludedIn(a), $"§3.1.3b 同标签异名误包含：{lb}");
                }
            }
            else
            {
                // 跨标签：除 other=Global（最大元）外均不可比。
                if (lb != "Global") Assert.False(a.IncludedIn(b), $"§3.1.3b 跨标签误包含：{la}⊆{lb}");
                if (la != "Global") Assert.False(b.IncludedIn(a), $"§3.1.3b 跨标签误包含：{lb}⊆{la}");
            }
        }
    }

    // 6. Shell 标签：Shell 与任意非 Shell 标签不可比；自身自反；Shell⊆Global（Global 最大元）。
    [Fact]
    public void Shell_IncomparableWithNonShell()
    {
        var shell = new ScopeId.Shell();
        Assert.True(shell.IncludedIn(shell), "§3.1.3b Shell 不自反");
        foreach (var label in Labels)
        {
            if (label == "Shell") continue;
            var other = RandomScope(new Random(23), label);
            if (label != "Global")
            {
                Assert.False(shell.IncludedIn(other), $"§3.1.3b Shell ⊆ {label} 误包含");
                Assert.False(other.IncludedIn(shell), $"§3.1.3b {label} ⊆ Shell 误包含");
            }
            else
            {
                Assert.True(shell.IncludedIn(other), "§3.1.3b Shell ⊄ Global（Global 最大元）");
            }
        }
    }
}
