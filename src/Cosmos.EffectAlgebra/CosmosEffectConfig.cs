// CosmosEffectConfig.cs — P2 白名单可扩展：cosmos.effect.json 额外映射（L1 增量，零 Godot 依赖）
// 设计：stdlib System.Text.Json 解析 extraMappings[]，复用 GodotApiWhitelist.Canonical 单一真源。
// 无文件或解析失败时回落内置 38 条，不抛；显式 LoadExtra* 抛 FormatException 供 CI 严格门使用。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;

#if ANALYZER_SHARED
namespace Cosmos.EffectAlgebra.Analyzer.Shared;
#elif GENERATOR_SHARED
namespace Cosmos.EffectAlgebra.Generator.Shared;
#else
namespace Cosmos.EffectAlgebra;
#endif

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
        using var _ = doc; // R4-JD-07：同契约侧，归还池化缓冲
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("根须为对象，含 extraMappings 数组");
        // 【QED-P5.2 H1 根层白名单】根键拼错（如 "extramappings"/"extra_mapping"）此前静默返回空 =
        // 扩展静默死亡且无任何诊断（违反「绝不静默」承诺，红队 P5.2-H1）。现在：根层只认
        // $schema / extraMappings；出现其他键而缺 extraMappings ⇒ loud FormatException。
        // （$schema 与 extraMappings 并存合法——模板即此形态。）
        var hasExtra = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "$schema") continue;
            if (prop.Name == "extraMappings") { hasExtra = true; continue; }
            throw new FormatException(
                $"未知根键 \"{prop.Name}\"（合法根键: $schema, extraMappings）。" +
                "根键拼错会让扩展静默失效，故 loud 拒绝而非静默忽略（QED-P5.2 H1）");
        }
        if (!hasExtra) return ImmutableArray<ApiMapping>.Empty;
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
        // 复用 Canonical 碰撞校验（与内置一致）；R3-L1-02：碰撞异常（InvalidOperationException，实测）
        // 须翻为契约方言，否则 extra 内部同 Canonical 碰撞会绕过 LoadExtra 非 strict 回落过滤器直接炸出。
        try { GodotApiWhitelist.ValidateNoCollisions(arr2); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { throw new FormatException($"extraMappings: {ex.Message}", ex); }
        return arr2;
    }

    /// <summary>从文件加载 extraMappings；文件不存在 ⇒ 空（回落）。IO/解析异常在严格模式抛；
    /// 非 strict 回落时必须留一条可观测告警（A4-14，生产审计批2）——静默吞配置错误 = "静默无保护"，
    /// 与本项目 fail-fast 立库原则冲突；stderr 告警不改变回落语义，仅消灭无声失败。</summary>
    // RS1035：本两成员为进程内文件加载路径（CLI/CI 严格门用），分析器共享副本（QED-C1b）不调用它们——
    // 分析器侧一律经 AdditionalText.GetText 拿文本再走 LoadExtraFromJson（编译器供给文本，零直接 IO）。
    // 精确范围禁用而非项目级 NoWarn：解析/校验成员保持 RS1035 守护。
#pragma warning disable RS1035
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
            Console.Error.WriteLine($"[cosmos-effect] 警告：{path} 解析失败已回落内置白名单（strict=true 可改为抛出）：{ex.Message}");
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
#pragma warning restore RS1035

    // ── Claim 解析（复用 EffectScriptContract 扁平形态子集：kind/resource/mode/scope/size）──
    // R3-L1-02（三轮审计）：ParseClaim 曾把整个 claim 对象当值传给三参 ReqStr（应先 Require 取属性值），
    // 任何输入都在 "kind 须为字符串" 处炸出——公共扩展入口完全不可用且零测试腐烂（R3-TQ-01）。
    // 归一化 ArgumentException（如 read+create）同步翻为契约 FormatException，保 LoadExtra 非 strict 回落过滤器的方言前提。
    static Claim ParseClaim(JsonElement c, string layer)
    {
        if (c.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer} 须为对象");
        var kind = ParseKind(ReqStr(Require(c, "kind", layer), $"{layer}.kind"));
        var res = ParseResource(Require(c, "resource", layer), $"{layer}.resource");
        var mode = ParseMode(ReqStr(Require(c, "mode", layer), $"{layer}.mode"));
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
        try { return new Claim(kind, res, mode, scope, size).Normalize(); }
        catch (ArgumentException ex) { throw new FormatException($"{layer}: {ex.Message}", ex); }
    }

    static NatStar ParseNat(JsonElement el, string layer)
    {
        // REG-03（复审计）：⊤/inf 双别名与 EffectScriptContract.IsTopAlias 单一真源对齐（A1-07 方言承诺）。
        if (el.ValueKind == JsonValueKind.String && (el.GetString() == "⊤" || el.GetString() == "inf")) return NatStar.Top;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetUInt64(out var v)) return NatStar.Of(v);
        throw new FormatException($"{layer} 须为非负整数或 \"⊤\"/\"inf\"");
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
        // R3-L1-03（三轮审计，与 EffectScriptContract.ParseResource 同界）：多键静默择一 ⇒ 拒绝。
        // REG-02（复审计）：未知键同界拒绝（拼写键静默丢弃 = 静默改写数据）。
        foreach (var prop in el.EnumerateObject())
            if (prop.Name is not ("gpu" or "commandBuffer" or "memory" or "occupancy" or "signalBus" or "custom"))
                throw new FormatException($"{layer} 未知键 \"{prop.Name}\"（合法键: gpu, commandBuffer, memory, occupancy, signalBus, custom）");
        int hitCount = 0;
        foreach (var prop in el.EnumerateObject())
            if (prop.Name is "gpu" or "commandBuffer" or "memory" or "occupancy" or "signalBus" or "custom")
                hitCount++;
        if (hitCount > 1)
            throw new FormatException($"{layer}: resource 至多含一键（gpu/commandBuffer/memory/occupancy/signalBus/custom 之一），实际命中 {hitCount} 键");
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
        // R4-RH-04（Hickey 视角）：与 EffectScriptContract.ParseScope 同界——未知键白名单 + 零字段拒绝。
        // 此前两份物理解析器严格性漂移：拼写键此处静默降级、零字段静默产出匿名 scope 参与冲突分组。
        foreach (var prop in el.EnumerateObject())
            if (prop.Name is not ("scene" or "type"))
                throw new FormatException($"{layer} 未知键 {prop.Name}（合法键: scene, type；区分大小写与拼写）");
        if (!el.TryGetProperty("scene", out _) && !el.TryGetProperty("type", out _))
            throw new FormatException($"{layer}: scope 须含 scene 或 type（至少一个字段）");
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
