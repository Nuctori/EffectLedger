// CosmosEffectConfig.cs — P2 白名单可扩展：cosmos.effect.json 额外映射（L1 增量，零 Godot 依赖）
// 设计：stdlib System.Text.Json 解析 extraMappings[]，复用 GodotApiWhitelist.Canonical 单一真源。
// 无文件或解析失败时回落内置 38 条，不抛；显式 LoadExtra* 抛 FormatException 供 CI 严格门使用。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;

namespace Cosmos.EffectAlgebra;

/// <summary>cosmos.effect.json 可扩展白名单（P2）。文件不存在 ⇒ 回落内置 All。</summary>
public static class CosmosEffectConfig
{
    /// <summary>从 json 文本解析 extraMappings（严格门，非法形状抛 FormatException）。</summary>
    public static ImmutableArray<ApiMapping> LoadExtraFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ImmutableArray<ApiMapping>.Empty;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new FormatException($"cosmos.effect.json JSON 非法: {ex.Message}", ex); }
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("根须为对象，含 extraMappings 数组");
        if (!root.TryGetProperty("extraMappings", out var arr)) return ImmutableArray<ApiMapping>.Empty;
        if (arr.ValueKind != JsonValueKind.Array) throw new FormatException("extraMappings 须为数组");
        var list = new List<ApiMapping>();
        int idx = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new FormatException($"extraMappings[{idx}] 须为对象");
            if (!item.TryGetProperty("api", out var apiEl) || apiEl.ValueKind != JsonValueKind.String)
                throw new FormatException($"extraMappings[{idx}].api 须为非空字符串");
            var api = apiEl.GetString()!;
            if (string.IsNullOrWhiteSpace(api)) throw new FormatException($"extraMappings[{idx}].api 不可为空");
            if (!item.TryGetProperty("claims", out var claimsEl) || claimsEl.ValueKind != JsonValueKind.Array)
                throw new FormatException($"extraMappings[{idx}].claims 须为数组");
            var claims = new List<Claim>();
            int cIdx = 0;
            foreach (var c in claimsEl.EnumerateArray())
            {
                claims.Add(ParseClaim(c, $"extraMappings[{idx}].claims[{cIdx++}]"));
            }
            if (claims.Count == 0) throw new FormatException($"extraMappings[{idx}].claims 不可为空");
            list.Add(new ApiMapping(api, claims.ToImmutableArray()));
            idx++;
        }
        var arr2 = list.ToImmutableArray();
        // 复用 Canonical 碰撞校验（与内置一致）
        GodotApiWhitelist.ValidateNoCollisions(arr2);
        return arr2;
    }

    /// <summary>从文件加载 extraMappings；文件不存在 ⇒ 空（回落）。IO/解析异常在严格模式抛。</summary>
    public static ImmutableArray<ApiMapping> LoadExtra(string path = "cosmos.effect.json", bool strict = false)
    {
        if (!File.Exists(path)) return ImmutableArray<ApiMapping>.Empty;
        try
        {
            var json = File.ReadAllText(path);
            return LoadExtraFromJson(json);
        }
        catch (Exception ex) when (!strict && ex is FormatException or JsonException or IOException)
        {
            return ImmutableArray<ApiMapping>.Empty;
        }
    }

    /// <summary>内置 All 与额外映射合并（Canonical 去重，额外覆盖同 Canonical）。strict=true 时配置错配抛（CI 门）。</summary>
    public static ImmutableArray<ApiMapping> AllWithExtra(string path = "cosmos.effect.json", bool strict = false)
    {
        var extra = LoadExtra(path, strict);
        if (extra.IsDefaultOrEmpty) return GodotApiWhitelist.All;
        var dict = new Dictionary<string, ApiMapping>(StringComparer.Ordinal);
        foreach (var m in GodotApiWhitelist.All) dict[GodotApiWhitelist.Canonical(m.GodotApi)] = m;
        foreach (var m in extra) dict[GodotApiWhitelist.Canonical(m.GodotApi)] = m;
        var merged = new List<ApiMapping>(dict.Values);
        return merged.ToImmutableArray();
    }

    // ── Claim 解析（复用 EffectScriptContract 扁平形态子集：kind/resource/mode/scope/size）──
    static Claim ParseClaim(JsonElement c, string layer)
    {
        if (c.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer} 须为对象");
        var kind = ParseKind(ReqStr(c, "kind", layer));
        var res = ParseResource(Require(c, "resource", layer), $"{layer}.resource");
        var mode = ParseMode(ReqStr(c, "mode", layer));
        var scope = ParseScope(Require(c, "scope", layer), $"{layer}.scope");
        Interval? size = null;
        if (c.TryGetProperty("size", out var sz))
        {
            if (sz.ValueKind != JsonValueKind.Array) throw new FormatException($"{layer}.size 须为 [lo,hi]");
            var items = sz.EnumerateArray().ToArray();
            if (items.Length != 2) throw new FormatException($"{layer}.size 须为 [lo,hi]");
            var lo = ParseNat(items[0], $"{layer}.size.lo");
            var hi = ParseNat(items[1], $"{layer}.size.hi");
            if (lo.IsTop && !hi.IsTop) throw new FormatException($"{layer}.size lo 不可为 ⊤ 而 hi 有限");
            if (!lo.IsTop && !hi.IsTop && lo.Value > hi.Value) throw new FormatException($"{layer}.size lo>hi");
            size = new Interval(lo, hi);
        }
        return new Claim(kind, res, mode, scope, size).Normalize();
    }

    static NatStar ParseNat(JsonElement el, string layer)
    {
        if (el.ValueKind == JsonValueKind.String && el.GetString() == "⊤") return NatStar.Top;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetUInt64(out var v)) return NatStar.Of(v);
        throw new FormatException($"{layer} 须为非负整数或 \"⊤\"");
    }

    static Kind ParseKind(string k) => k switch
    {
        "read" => Kind.Read, "write" => Kind.Write, "occupy" => Kind.Occupy,
        _ => throw new FormatException($"未知 kind: {k}")
    };
    static Mode ParseMode(string m) => m switch
    {
        "use" => Mode.Use, "create" => Mode.Create, "release" => Mode.Release,
        "move" => Mode.Move, "unknown" => Mode.Unknown,
        _ => throw new FormatException($"未知 mode: {m}")
    };
    static ResourceId ParseResource(JsonElement el, string layer)
    {
        if (el.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer} 须为对象");
        if (el.TryGetProperty("gpu", out var gpu)) return new ResourceId.Gpu(new Rid(ReqStr(gpu, $"{layer}.gpu")));
        if (el.TryGetProperty("commandBuffer", out var cb)) return new ResourceId.CommandBuffer(ReqStr(cb, $"{layer}.commandBuffer"));
        if (el.TryGetProperty("memory", out var mem))
        {
            if (mem.ValueKind == JsonValueKind.Number && mem.TryGetUInt64(out var uid)) return new ResourceId.Memory(uid);
            throw new FormatException($"{layer}.memory 须为非负整数");
        }
        if (el.TryGetProperty("occupancy", out var occ)) return new ResourceId.Occupancy(ReqStr(occ, $"{layer}.occupancy"));
        if (el.TryGetProperty("signalBus", out var sb)) return new ResourceId.SignalBus(new StringName(ReqStr(sb, $"{layer}.signalBus")));
        if (el.TryGetProperty("custom", out var cu)) return new ResourceId.Custom(ReqStr(cu, $"{layer}.custom"));
        throw new FormatException($"{layer} 须含 gpu/commandBuffer/memory/occupancy/signalBus/custom 之一");
    }
    static ScopeId ParseScope(JsonElement el, string layer)
    {
        if (el.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer} 须为对象");
        var hasScene = el.TryGetProperty("scene", out var sc);
        var name = hasScene ? ReqStr(sc, $"{layer}.scene") : "";
        if (!el.TryGetProperty("type", out var ty)) return new ScopeId.Scene(name);
        return ReqStr(ty, $"{layer}.type") switch
        {
            "method" => new ScopeId.Method(name),
            "type" => new ScopeId.Type(name),
            "global" => new ScopeId.Global(),
            "scene" => new ScopeId.Scene(name),
            _ => throw new FormatException($"{layer} 未知 scope.type: {ty.GetString()}")
        };
    }
    static string ReqStr(JsonElement el, string key, string layer)
    {
        if (el.ValueKind != JsonValueKind.String) throw new FormatException($"{layer}.{key} 须为字符串");
        var s = el.GetString()!;
        if (string.IsNullOrWhiteSpace(s)) throw new FormatException($"{layer}.{key} 不可为空");
        return s;
    }
    static string ReqStr(JsonElement el, string layer)
    {
        if (el.ValueKind != JsonValueKind.String) throw new FormatException($"{layer} 须为字符串");
        var s = el.GetString()!;
        if (string.IsNullOrWhiteSpace(s)) throw new FormatException($"{layer} 不可为空");
        return s;
    }
    static JsonElement Require(JsonElement obj, string prop, string layer)
    {
        if (!obj.TryGetProperty(prop, out var v)) throw new FormatException($"{layer} 缺少 {prop}");
        return v;
    }
}
