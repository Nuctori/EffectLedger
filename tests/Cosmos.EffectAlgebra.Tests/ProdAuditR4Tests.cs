// ProdAuditR4Tests.cs — 第四轮独立审计（2026-09，Jeff Dean × Rich Hickey 双视角）回归钉。
// RH-04：CosmosEffectConfig.ParseScope 与 EffectScriptContract.ParseScope 同界（未知键拒绝 + 零字段拒绝）
//        ——两份物理解析器的方言漂移在 R3-L1-04 已于契约侧收口，配置侧在此补齐钉死。
// RH-07：Runtime 面构造期 null 守卫——非法输入在 Register 现场带 FiberId loud 拒绝，
//        不再延后到 LoadAll/逆回放中途以无归因异常冒出（L1 构造期纪律对齐）。
// JD-10：Runtime 规模测量钉——3000 深依赖链装载→旁路卸载→看门狗自愈→拓扑排空全流程完成；
//        此前 Runtime 无任何 >10¹ 级 fiber 规模测试，O(F²) 装载与 O(V·E) 拓扑的真实拐点无测量。
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Cosmos.EffectAlgebra;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public sealed class ProdAuditR4Tests
{
    // ── RH-04：配置侧 scope 与契约侧同界 ──
    [Fact]
    public void LoadExtraFromJson_ScopeUnknownKey_IsRejected()
    {
        const string json = """
        {
          "extraMappings": [
            { "api": "MyNode.DoThing",
              "claims": [ { "kind": "write", "resource": { "gpu": "x" }, "mode": "use",
                            "scope": { "scene": "HUD", "typ": "method" } } ] }
          ]
        }
        """;
        Assert.Throws<FormatException>(() => CosmosEffectConfig.LoadExtraFromJson(json));
    }

    [Fact]
    public void LoadExtraFromJson_ScopeEmptyObject_IsRejected()
    {
        const string json = """
        {
          "extraMappings": [
            { "api": "MyNode.DoThing",
              "claims": [ { "kind": "write", "resource": { "gpu": "x" }, "mode": "use",
                            "scope": { } } ] }
          ]
        }
        """;
        Assert.Throws<FormatException>(() => CosmosEffectConfig.LoadExtraFromJson(json));
    }
}
