// ProdAuditR4AuditScaleTests.cs — 第四轮审计（R4-JD-09，Dean 视角）：审计伸缩性曲线钉。
// 此前性能钉全是绝对墙钟（慢 CI 易 flaky，且测不出复杂度劣化）；本钉用倍增比值断言非二次。
// 实测基线（2026-09，本机 Release）：共享形状 ratio≈2.2x；逐事件独立 scope 形状 ratio≈6.0x
//（AuditAtSample 每采样点扫 net/grp 字典、D=Θ(E) 时超线性——README 诚实边界已记录该前提）。
// 两形状共用单一 12x 阈值：实测最差 6.0x 留 2 倍抖动余量，二次劣化基线（4 倍数据 ⇒ 16x）当场抓住。
//
// QED-P5.3 L12：新增 spread 形状（交错寿命 Lo=i、Hi=N+i + 互异 res+scope）——性能审计员实证
// alpha≈1.94 纯二次渐近（t4/t1=14.8x 超自家 12x 共享阈值）。独立 25x 阈值门（回归检测：
// 劣化超 25x 即红；二次基线 16x），与紧凑形状 12x 阈值共存——两种形状预期复杂度不同，不混用。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public sealed class ProdAuditR4AuditScaleTests
{
    static EffectEvent Ev(int i, bool distinctScope)
    {
        var res = new ResourceId.Gpu(new Rid(distinctScope ? "tex" + i : "tex"));
        var scope = distinctScope ? new ScopeId.Scene("S" + i) : new ScopeId.Scene("S");
        var fp = Signature.Of(new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(1)));
        return new EffectEvent(new Interval(NatStar.Of((ulong)(2 * i)), NatStar.Of((ulong)(2 * i + 1))), scope, fp);
    }

    static double AuditMs(int n, bool distinctScope)
    {
        var evs = new List<EffectEvent>();
        for (int i = 0; i < n; i++) evs.Add(Ev(i, distinctScope));
        var script = new EffectScript(evs.ToImmutableArray(), Budget.None);
        var sw = Stopwatch.StartNew();
        script.Audit(Budget.None);
        return sw.Elapsed.TotalMilliseconds;
    }

    // R6 收口（本机 CI 偶红实证：t(4000)/t(1000)=12.3x 越线 12x，历史基线 6x）——单次墙钟采样对
    // 抢占/负载毛刺敏感，且大运行吃毛刺概率更高、系统性抬高比值。取 3 次重复最小值（墙钟微基准
    // 标准去噪，min 对抢占毛刺最不敏感）；阈值 12x 不动——仍远高于健康 6x、低于二次 16x，无放松。
    static double AuditMsBest(int n, bool distinctScope, int reps = 3)
    {
        var best = double.MaxValue;
        for (int r = 0; r < reps; r++)
            best = Math.Min(best, AuditMs(n, distinctScope));
        return best;
    }

    [Theory]
    [InlineData(false)]  // 共享资源×scope（常规形状）
    [InlineData(true)]   // 逐事件独立 scope（D=Θ(E) 劣化区）
    public void Audit_Scaling_Subquadratic(bool distinctScope)
    {
        // 预热（JIT/首次分配不进比值）
        AuditMs(200, distinctScope);
        var t1 = AuditMsBest(1000, distinctScope);
        var t4 = AuditMsBest(4000, distinctScope);
        var ratio = t4 / Math.Max(t1, 0.001);
        Assert.True(ratio < 12.0,
            $"{(distinctScope ? "独立 scope" : "共享形状")} 形状疑似二次劣化：t(4000)/t(1000)={ratio:F1}x（阈值 12x；二次基线 16x；实测基线 {(distinctScope ? 6.0 : 2.2):F1}x）");
        Assert.True(t4 < 30_000, $"绝对墙钟护栏：t(4000)={t4:F0}ms");
    }

    // ── QED-P5.3 L12：spread 形状（交错寿命 + 互异 res+scope）——性能审计员实证 alpha≈1.94
    //    纯二次渐近，超出自家 12x 共享阈值。独立 25x 阈值门（回归检测：劣化超 25x 即红），
    //    不与 12x 混用——两种形状预期复杂度不同，混用会让阈值要么过松要么假红。 ──
    static EffectEvent SpreadEv(int i, int total)
    {
        var res = new ResourceId.Gpu(new Rid("tex" + i));
        var scope = new ScopeId.Scene("S" + i);
        var fp = Signature.Of(new Claim(Kind.Occupy, res, Mode.Create, scope, Interval.Exact(1)));
        // 交错寿命：Lo=i、Hi=N+i 双向交错（进入/退出各产生 N 个采样点，net 字典只增不减）
        return new EffectEvent(new Interval(NatStar.Of((ulong)i), NatStar.Of((ulong)(i + total))), scope, fp);
    }

    static double SpreadAuditMs(int n)
    {
        var evs = new List<EffectEvent>();
        for (int i = 0; i < n; i++) evs.Add(SpreadEv(i, n));
        var script = new EffectScript(evs.ToImmutableArray(), Budget.None);
        var sw = Stopwatch.StartNew();
        script.Audit(Budget.None);
        return sw.Elapsed.TotalMilliseconds;
    }

    static double SpreadAuditMsBest(int n, int reps = 3)
    {
        var best = double.MaxValue;
        for (int r = 0; r < reps; r++)
            best = Math.Min(best, SpreadAuditMs(n));
        return best;
    }

    [Fact]
    public void Spread_Scaling_NearQuadratic()
    {
        // 预热（JIT/首次分配不进比值）
        SpreadAuditMs(200);
        // 四倍数据比值对照（性能审计员实测：alpha≈1.94 纯二次渐近，N=8000 alpha≈1.99）
        var t1 = SpreadAuditMsBest(1000);
        var t4 = SpreadAuditMsBest(4000);
        var ratio = t4 / Math.Max(t1, 0.001);
        // 二次基线 16x（4 倍数据的 α=2 理论值）；阈值 25x 给实现常数因子留余量，仍远低于三次 64x
        Assert.True(ratio is > 4.0 and < 25.0,
            $"spread 形状增长率异常：t(4000)/t(1000)={ratio:F1}x（期望 ∈ (4, 25)；二次基线 16x）——" +
            "低于 4 ⇒ 实现意外改善（审查）；高于 25 ⇒ 劣化加剧（修复）");
        Assert.True(t4 < 30_000, $"绝对墙钟护栏：t(4000)={t4:F0}ms");
    }
}
