// QedP2C1aMergedWhitelistPins.cs — P2-C1a 白名单合并视图钉（诚实边界 #12 接线本体的 L1 侧）：
// GodotApiWhitelist.MergedWith(extra) = 基础表 + 用户扩展（cosmos.effect.json）的不可变合并视图；
// 碰撞语义 loud——合并集内任何 Canonical 同键（扩展 vs 基础表 / 扩展彼此 / 精确重名）⇒
// InvalidOperationException（静默覆盖=假绿向量，R3-L1-03 教义）。L3（C1b）/L2（C1c）消费此视图。
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedP2C1aMergedWhitelistPins
{
    static ScopeId Scene(string n) => new ScopeId.Scene(n);

    static ApiMapping ExtraMapping(string api) => new(api,
        ImmutableArray.Create(new Claim(Kind.Occupy, new ResourceId.Memory(42), Mode.Use, Scene("S"), Interval.Exact(1)).Normalize()));

    // ── 钉 1：合并含基础表 + 扩展（数量相加、双方皆在、基础表本体不被修改）。 ──
    [Fact]
    public void MergedWith_ContainsBaseAndExtra_BaseUntouched()
    {
        var before = GodotApiWhitelist.All.Length;
        var merged = GodotApiWhitelist.MergedWith(ImmutableArray.Create(ExtraMapping("MyOrg_MyWidget_Show")));

        Assert.Equal(before + 1, merged.Length);
        Assert.Contains(merged, m => m.GodotApi == "MyOrg_MyWidget_Show");
        Assert.Equal(before, GodotApiWhitelist.All.Length); // 基础表不可变视图，不被修改
    }

    // ── 钉 2：扩展与基础表 Canonical 同键 ⇒ loud 抛（用户重定义 QueueFree 必须失败，不得静默覆盖）。 ──
    [Fact]
    public void MergedWith_CollideWithBase_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => GodotApiWhitelist.MergedWith(ImmutableArray.Create(ExtraMapping("queue_free"))));
        Assert.Contains("白名单扩展碰撞", ex.Message);
    }

    // ── 钉 3：扩展彼此同键（含精确重名）⇒ loud 抛。 ──
    [Fact]
    public void MergedWith_CollideWithinExtra_Throws()
    {
        var dup = ImmutableArray.Create(ExtraMapping("My_Foo"), ExtraMapping("My_Foo"));
        Assert.Throws<InvalidOperationException>(() => GodotApiWhitelist.MergedWith(dup));
    }

    // ── 钉 4：空扩展 = 原基础表（长度相等、内容一致）。 ──
    [Fact]
    public void MergedWith_EmptyExtra_ReturnsBase()
    {
        var merged = GodotApiWhitelist.MergedWith(default);
        Assert.Equal(GodotApiWhitelist.All.Length, merged.Length);
        Assert.True(merged.SequenceEqual(GodotApiWhitelist.All));
    }
}
