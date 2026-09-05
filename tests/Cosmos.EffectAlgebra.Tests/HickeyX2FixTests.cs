// HickeyX2FixTests.cs — hickey-x2 十轮审计 P0/critical 修复的可证伪回归测试（R4-F1/F2/F3/F4、R10-F1/F2）。
// 每条断言对应审计实证过的错误行为：修复前必红，修复后绿；回归即红。
using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class HickeyX2FixTests
{
    private static readonly ScopeId G = new ScopeId.Global();

    // ── R4-F1：ZStar 溢出守卫——create(2^64-1)+create(1) 零释放不得再报「守恒」 ──
    [Fact]
    public void ZStarOverflow_GiantCreate_IsNotConserved()
    {
        var giant = new Interval(NatStar.Of(ulong.MaxValue), NatStar.Of(ulong.MaxValue));
        var one = Interval.Exact(1);
        var sig = Signature.Of(
            new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Create, G, giant),
            new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Create, G, one));
        var net = sig.Net(G);
        var mem = ResourceId.Normalize(new ResourceId.Memory(1));
        Assert.False(net.IsConserved(mem), "溢出经 (long) 强转回卷为负曾使泄漏闸门假阴性");
    }

    [Fact]
    public void ZStar_AdditionOverflow_ReturnsTop()
    {
        var a = ZStar.Of(long.MaxValue);
        var b = ZStar.Of(1);
        Assert.True((a + b).IsTop);
        Assert.True((ZStar.Of(long.MinValue) - ZStar.Of(1)).IsTop);
    }

    // ── R4-F2：Join 实现 merge_I——[10,10]⊔[50,50] ⇒ 单条 [10,50]，Peak=50 而非 60 ──
    [Fact]
    public void Join_MergesSameClaimKey_WithMergedInterval()
    {
        var r = new ResourceId.Gpu(new Rid("buf"));
        var a = Signature.Of(new Claim(Kind.Occupy, r, Mode.Create, G, Interval.Exact(10)));
        var b = Signature.Of(new Claim(Kind.Occupy, r, Mode.Create, G, Interval.Exact(50)));
        var j = Signature.Join(a, b);
        var claim = Assert.Single(j.OccupyClaims);
        Assert.Equal(NatStar.Of(10), (claim.Size ?? default).Lo);
        Assert.Equal(NatStar.Of(50), (claim.Size ?? default).Hi);
        Assert.Equal(NatStar.Of(50), Peak.Compute(j, G)); // 非 60
    }

    [Fact]
    public void Join_IsIdempotentCommutative()
    {
        var r = new ResourceId.Gpu(new Rid("buf"));
        var a = Signature.Of(new Claim(Kind.Occupy, r, Mode.Create, G, Interval.Exact(10)));
        Assert.True(Signature.Join(a, a).Equals(a));
        var b = Signature.Of(new Claim(Kind.Occupy, r, Mode.Create, G, Interval.Exact(50)));
        Assert.True(Signature.Join(a, b).Equals(Signature.Join(b, a)));
    }

    // ── R4-F3：TryMid 中点不再 long 内先加爆 ──
    [Fact]
    public void TryMid_MaxLong_DoesNotOverflow()
    {
        var si = new SignedInterval(ZStar.Of(long.MaxValue), ZStar.Of(long.MaxValue));
        Assert.True(si.TryMid(out var mid, out _));
        Assert.Equal(long.MaxValue / 2.0 + long.MaxValue / 2.0, mid); // 原 -1
    }

    // 【P1-B3】Parallel_CreateCreate_Throws 随被钉的 Combination.Parallel 一并删除
    //（PARA_CONFLICT 前置守卫随别名移除；冲突检测权威 = Audit gate(3)，其create×create 冲突
    // 已由 CompatibleMatrixTests 25 组合矩阵 + 扫换线 gate(3) 端到端钉承载）。

    // ── R10-F1：Audit(default(Budget)) 不再 NRE ──
    [Fact]
    public void Audit_DefaultBudget_DoesNotThrow()
    {
        var ev = new EffectEvent(
            new Interval(NatStar.Of(0), NatStar.Of(10)), G,
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Memory(1), Mode.Use, G, Interval.Default)),
            LoopCount.Of(1));
        var script = new EffectScript(new[] { ev });
        var result = script.Audit(default);
        Assert.True(result.Passed || result.Violations.Length > 0); // 正常返回即可
    }

    // ── R10-F2：契约解析拒绝 [⊤,⊤] 寿命（防假绿） ──
    [Fact]
    public void Parse_TopTopLifetime_Throws()
    {
        const string json = """
        {"events":[{"lifetime":["⊤","⊤"],"scope":{"type":"global"},
          "footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"type":"global"},"size":[1,1]}]}]}
        """;
        Assert.Throws<FormatException>(() => EffectScriptContract.Parse(json));
    }
}
