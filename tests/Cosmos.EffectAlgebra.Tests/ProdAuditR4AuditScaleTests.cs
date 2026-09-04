// ProdAuditR4AuditScaleTests.cs — 第四轮审计（R4-JD-09，Dean 视角）：审计伸缩性曲线钉。
// 此前性能钉全是绝对墙钟（慢 CI 易 flaky，且测不出复杂度劣化）；本钉用倍增比值断言非二次。
// 实测基线（2026-09，本机 Release）：共享形状 ratio≈2.2x；逐事件独立 scope 形状 ratio≈6.0x
//（AuditAtSample 每采样点扫 net/grp 字典、D=Θ(E) 时超线性——README 诚实边界已记录该前提）。
// 两形状共用单一 12x 阈值：实测最差 6.0x 留 2 倍抖动余量，二次劣化基线（4 倍数据 ⇒ 16x）当场抓住。
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

    [Theory]
    [InlineData(false)]  // 共享资源×scope（常规形状）
    [InlineData(true)]   // 逐事件独立 scope（D=Θ(E) 劣化区）
    public void Audit_Scaling_Subquadratic(bool distinctScope)
    {
        // 预热（JIT/首次分配不进比值）
        AuditMs(200, distinctScope);
        var t1 = AuditMs(1000, distinctScope);
        var t4 = AuditMs(4000, distinctScope);
        var ratio = t4 / Math.Max(t1, 0.001);
        Assert.True(ratio < 12.0,
            $"{(distinctScope ? "独立 scope" : "共享形状")} 形状疑似二次劣化：t(4000)/t(1000)={ratio:F1}x（阈值 12x；二次基线 16x；实测基线 {(distinctScope ? 6.0 : 2.2):F1}x）");
        Assert.True(t4 < 30_000, $"绝对墙钟护栏：t(4000)={t4:F0}ms");
    }
}
