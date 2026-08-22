using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Analyzer;
using Cosmos.EffectAlgebra.Generator;
using Xunit;

// 迭代15：跨层引用一致性（机械化的「无符号/签名漂移」护栏）。
// 思路：用反射在运行期断言 L2/L3 实际依赖的 L1 符号确实存在于 L1 且签名匹配；
// 任一签名漂移（改名/改参/改返回）⇒ 反射 GetMethod/GetProperty 返回 null ⇒ 断言红。
// 不引魔法数：集合大小用 .Length/.Count，成员判定用 .Contains（数据驱动）。
// 出处注释引 §7（白名单）/§8.1（release-class）/§3.x（L1 类型/运算）。

namespace Cosmos.EffectAlgebra.Tests;

public class CrossLayerTests
{
    // §7 — L2/L3 依赖的 GodotApiWhitelist.All 必须存在且类型正确、非空。
    [Fact]
    public void A_L1_GodotApiWhitelist存在()
    {
        var prop = typeof(GodotApiWhitelist).GetProperty("All");
        Assert.NotNull(prop); // 属性存在（改名⇒null⇒红）
        Assert.Equal(typeof(ImmutableArray<ApiMapping>), prop!.PropertyType); // 返回类型匹配 §7 映射数组
        Assert.True(GodotApiWhitelist.All.Length > 0); // 白名单非空（§7.1–§7.10）
    }

    // §8.1 — ReleaseClass.IsRelease(string)->bool 必须存在且签名匹配；Names 含 7 项权威 release-class。
    [Fact]
    public void A_L1_ReleaseClass存在()
    {
        var m = typeof(ReleaseClass).GetMethod("IsRelease", new[] { typeof(string) });
        Assert.NotNull(m); // 方法存在
        Assert.True(m!.IsStatic); // 静态
        Assert.Equal(typeof(bool), m.ReturnType); // 返回 bool
        Assert.Single(m.GetParameters()); // 单参 string

        // §8.1 权威 7 项（与 ApiMapping / PDR 严格一致；数据驱动，不引魔法数）。
        foreach (var name in new[] { "queue_free", "free", "remove_child", "disconnect", "remove_from_group", "cancel_free", "free_children_in_group" })
            Assert.Contains(name, ReleaseClass.Names); // 遗漏⇒红
        Assert.True(ReleaseClass.IsRelease("queue_free")); // 正向
        Assert.False(ReleaseClass.IsRelease("AddChild")); // 非 release-class⇒false（§8.1）
    }

    // §3.1.1 / §3.1.4b — Claim 五字段（kind, resource, mode, scope, size）必须存在且为公开属性。
    [Fact]
    public void A_L1_Claim字段()
    {
        var props = typeof(Claim).GetProperties().ToDictionary(p => p.Name, p => p);
        foreach (var f in new[] { "Kind", "Resource", "Mode", "Scope", "Size" })
            Assert.True(props.ContainsKey(f), $"Claim 缺少字段 {f}"); // 缺字段⇒红（位置记录漂移）
        Assert.Equal(typeof(Kind), props["Kind"].PropertyType);
        Assert.Equal(typeof(ResourceId), props["Resource"].PropertyType);
        Assert.Equal(typeof(Mode), props["Mode"].PropertyType);
        Assert.Equal(typeof(ScopeId), props["Scope"].PropertyType);
        Assert.Equal(typeof(Interval), props["Size"].PropertyType);
    }

    // §3.2.3 — Compatible.IsCompatible(Mode,Mode)->bool 全函数必须存在且签名匹配（L3 引用）。
    [Fact]
    public void A_L1_Compatible()
    {
        var m = typeof(Compatible).GetMethod("IsCompatible", new[] { typeof(Mode), typeof(Mode) });
        Assert.NotNull(m); // 方法存在
        Assert.True(m!.IsStatic);
        Assert.Equal(typeof(bool), m.ReturnType); // 返回 bool
        Assert.Equal(2, m.GetParameters().Length); // 双参 Mode,Mode
        // 顺带确认全函数无抛（§3.2.3）：代表性组合均返回 bool。
        Assert.True(Compatible.IsCompatible(Mode.Create, Mode.Release)); // P3 良性配对
        Assert.False(Compatible.IsCompatible(Mode.Create, Mode.Create)); // CONFLICT
    }

    // §3.3.1 — NetTable.Compute(Signature,ScopeId)->NetTable 与 NetTable.IsConserved(ResourceId)->bool 必须存在且签名匹配。
    [Fact]
    public void A_L1_NetTable()
    {
        var compute = typeof(NetTable).GetMethod("Compute", new[] { typeof(Signature), typeof(ScopeId) });
        Assert.NotNull(compute); // 静态 Compute 存在
        Assert.True(compute!.IsStatic);
        Assert.Equal(typeof(NetTable), compute.ReturnType); // 返回 NetTable
        Assert.Equal(2, compute.GetParameters().Length);

        var conserved = typeof(NetTable).GetMethod("IsConserved", new[] { typeof(ResourceId) });
        Assert.NotNull(conserved); // 实例 IsConserved 存在
        Assert.False(conserved!.IsStatic); // 实例方法
        Assert.Equal(typeof(bool), conserved.ReturnType); // 返回 bool
        Assert.Single(conserved.GetParameters());
    }

    // §L2 — EffectAlgebraGenerator 必须实现 IIncrementalGenerator（L2 类型边界）。
    [Fact]
    public void A_L2_Generator类型()
    {
        Assert.True(typeof(IIncrementalGenerator).IsAssignableFrom(typeof(EffectAlgebraGenerator)));
        Assert.NotNull(typeof(EffectAlgebraGenerator).GetInterface("Microsoft.CodeAnalysis.IIncrementalGenerator"));
    }

    // §L3 — EffectAlgebraAnalyzer 必须 [DiagnosticAnalyzer] + : DiagnosticAnalyzer；SupportedDiagnostics 含 EAA0901（§3.3.1 DO-9）。
    [Fact]
    public void A_L3_Analyzer类型()
    {
        var t = typeof(EffectAlgebraAnalyzer);
        Assert.True(typeof(DiagnosticAnalyzer).IsAssignableFrom(t)); // : DiagnosticAnalyzer
        Assert.NotNull(t.GetCustomAttribute<DiagnosticAnalyzerAttribute>()); // [DiagnosticAnalyzer]
        var analyzer = new EffectAlgebraAnalyzer();
        Assert.Contains(analyzer.SupportedDiagnostics, d => d.Id == "EAA0901"); // §3.3.1 DO-9 诊断 id 存在
    }

    // §7 / §3.1.1 / iter14 — ApiMapping.GodotApi/Claims 字段存在；每条 Claim 构造即合法（呼应 required 不变量）。
    [Fact]
    public void A_whitelist_ApiMapping字段()
    {
        var godotApi = typeof(ApiMapping).GetProperty("GodotApi");
        var claims = typeof(ApiMapping).GetProperty("Claims");
        Assert.NotNull(godotApi);
        Assert.NotNull(claims);
        Assert.Equal(typeof(ImmutableArray<Claim>), claims!.PropertyType); // Claims 为 Claim 不可变数组

        foreach (var m in GodotApiWhitelist.All)
        {
            Assert.False(string.IsNullOrEmpty(m.GodotApi)); // API 键非空（§7 稳定键）
            Assert.True(m.Claims.Length > 0); // 每条映射至少 1 Claim
            foreach (var c in m.Claims)
            {
                // Claim 五字段均非空/合法（构造即合法，呼应 iter14 required 修饰 + Normalize 不变量）。
                Assert.True(c.Kind == Kind.Read || c.Kind == Kind.Write || c.Kind == Kind.Occupy);
                Assert.NotNull(c.Resource);
                Assert.True(c.Mode == Mode.Use || c.Mode == Mode.Create || c.Mode == Mode.Release || c.Mode == Mode.Move || c.Mode == Mode.Unknown);
                Assert.NotNull(c.Scope);
                Assert.True(c.Size.Lo.Value >= 1 && (c.Size.Hi.IsTop || c.Size.Hi.Value >= 1)); // 非 [0,0] 非法区间
            }
        }
    }
}
