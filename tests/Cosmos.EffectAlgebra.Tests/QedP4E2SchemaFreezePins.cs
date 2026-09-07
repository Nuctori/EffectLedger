// QedP4E2SchemaFreezePins.cs — P4-E2 契约面冻结钉：
// docs/effect-script.schema.json 的契约面（6 资源 × 4 scope × kind 3 × mode 5）与版本标记被钉死——
// 任何契约面变更 = 破坏性（semver major + PDR 决策记录），schema 的 version 字段必须同步递增。
// 冻结声明：QED-E2（2026-09-07），对应 PDR §4 与 EffectScriptContract.Parse 白名单（A4 方言冻结）。
// 模板可解析性由既有 DocGuard A1-03 钉承载，此处不重复。xUnit。
using System.Text.Json;
using Xunit;

namespace Cosmos.EffectAlgebra.Tests;

public class QedP4E2SchemaFreezePins
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cosmos.EffectAlgebra.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未找到仓库根（Cosmos.EffectAlgebra.slnx）");
    }

    static JsonElement Schema()
    {
        var path = Path.Combine(RepoRoot(), "docs", "effect-script.schema.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    // ── 钉 1：版本标记存在且为冻结值（契约面变更 ⇒ version 必须同步递增 = semver major）。 ──
    [Fact]
    public void Schema_HasFrozenVersionMarker()
    {
        var schema = Schema();
        Assert.Equal("1.0.0", schema.GetProperty("version").GetString());
        Assert.Contains("QED-E2", schema.GetProperty("x-contract-frozen").GetString());
    }

    // ── 钉 2：资源面冻结 = 恰好 6 键（gpu/commandBuffer/memory/occupancy/signalBus/custom）。 ──
    [Fact]
    public void Schema_ResourceFace_Frozen()
    {
        var res = Schema()
            .GetProperty("properties").GetProperty("events")
            .GetProperty("items").GetProperty("properties").GetProperty("footprint")
            .GetProperty("items").GetProperty("properties").GetProperty("resource");
        Assert.Equal(1, res.GetProperty("minProperties").GetInt32());
        Assert.Equal(1, res.GetProperty("maxProperties").GetInt32());
        var keys = res.GetProperty("properties").EnumerateObject().Select(p => p.Name).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "commandBuffer", "custom", "gpu", "memory", "occupancy", "signalBus" }, keys);
    }

    // ── 钉 3：scope 面冻结 = 恰好 4 键（scene/type 直接键 + type 枚举 method/type/global/scene）。 ──
    [Fact]
    public void Schema_ScopeFace_Frozen()
    {
        var scope = Schema()
            .GetProperty("properties").GetProperty("events")
            .GetProperty("items").GetProperty("properties").GetProperty("scope");
        var keys = scope.GetProperty("properties").EnumerateObject().Select(p => p.Name).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "scene", "type" }, keys);
        var typeEnum = scope.GetProperty("properties").GetProperty("type").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "global", "method", "scene", "type" }, typeEnum);
    }

    // ── 钉 4：kind/mode 枚举冻结（3 kind × 5 mode，mode 含 Unknown=Use 的 fail-open 面）。 ──
    [Fact]
    public void Schema_KindAndModeEnums_Frozen()
    {
        var claim = Schema()
            .GetProperty("properties").GetProperty("events")
            .GetProperty("items").GetProperty("properties").GetProperty("footprint")
            .GetProperty("items").GetProperty("properties");
        Assert.Equal(new[] { "read", "write", "occupy" },
            claim.GetProperty("kind").GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal(new[] { "use", "create", "release", "move", "unknown" },
            claim.GetProperty("mode").GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray());
    }
}
