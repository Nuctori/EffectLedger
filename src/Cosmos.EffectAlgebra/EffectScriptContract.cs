// EffectScriptContract.cs — EFFECT_SCRIPT.md §4 AI 数据契约（L1 增量，零 Godot 依赖）。
// LANDING_PLAN 角色：把「AI 看参考图/视频 → 写视觉剧本 JSON」这一外部产物，映射为内部 L1 EffectScript 数据。
// 数学全部在 EffectScript.cs（At/Audit）；本文件只做 JSON ⇄ L1 的类型安全搬运，不重算代数。
// 类型即边界：序列化/反序列化逐字段校验 kind/mode/resource/scope 形状，非法形状抛 FormatException（fail-fast，非静默漏报）。
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cosmos.EffectAlgebra;

/// <summary>
/// §4 — AI 视觉剧本的 JSON 契约（数据形态，不经 §7 白名单、不经 Godot 运行期）。
/// AI 产出此 JSON → <see cref="EffectScriptContract.Parse"/> 映射为 <see cref="EffectScript"/> → <see cref="EffectScript.Audit"/> 验证。
/// 验证失败 ⇒ <see cref="EffectScript.Audit"/> 返回 <see cref="Violation"/>（供 AI 回修 JSON）。本文件不承载数学语义。
/// </summary>
public static class EffectScriptContract
{
    /// <summary>§4 — 反序列化 JSON 文本为 <see cref="EffectScript"/>。非法形状（未知 kind/mode/resource/scope 或字段缺失）⇒ FormatException（fail-fast）。</summary>
    public static EffectScript Parse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("events", out var evArr) || evArr.ValueKind != JsonValueKind.Array)
            throw new FormatException("EFFECT_SCRIPT §4：根须含 'events' 数组");

        var events = new List<EffectEvent>();
        foreach (var ev in evArr.EnumerateArray())
            events.Add(ParseEvent(ev));

        IReadOnlyDictionary<ResourceId, NatStar> caps = Budget.None.Caps;
        if (root.TryGetProperty("budget", out var bud) && bud.ValueKind == JsonValueKind.Object)
            caps = ParseBudget(bud);

        return new EffectScript(events.ToImmutableArray()) { Budget = new Budget(caps) };
    }

    /// <summary>§4 — 序列化 <see cref="EffectScript"/> 为契约 JSON（round-trip 用）。</summary>
    public static string ToJson(EffectScript script)
    {
        var root = new Dictionary<string, object?>();
        var evList = new List<object?>();
        foreach (var e in script.Events)
            evList.Add(SerializeEvent(e));
        root["events"] = evList;
        if (script.Budget.Caps.Count > 0)
            root["budget"] = SerializeBudget(script.Budget.Caps);
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── 事件解析 ──
    static EffectEvent ParseEvent(JsonElement ev)
    {
        if (ev.ValueKind != JsonValueKind.Object) throw new FormatException("event 须为对象");
        var life = ParseInterval(Require(ev, "lifetime"));
        var scope = ParseScope(Require(ev, "scope"));
        var loop = ev.TryGetProperty("loop", out var l) ? ParseLoop(l) : LoopCount.Of(1);
        var fp = ParseFootprint(Require(ev, "footprint"));
        return new EffectEvent(life, scope, fp, loop);
    }

    static Interval ParseInterval(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var items = el.EnumerateArray().ToArray();
            if (items.Length != 2) throw new FormatException("lifetime 数组须 [lo,hi]");
            var lo = ParseTop(items[0]);
            var hi = ParseTop(items[1]);
            return new Interval(lo, hi);
        }
        throw new FormatException("lifetime 须为 [lo,hi] 数组（hi 可为 \"⊤\" 表示∞）");
    }

    // hi="⊤" 或数字字符串；lo 必须有限。
    static NatStar ParseTop(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String && el.GetString() == "⊤") return NatStar.Top;
        if (el.ValueKind == JsonValueKind.Number)
            return NatStar.Of(el.GetUInt64());
        throw new FormatException("lifetime 端点须为数字或 \"⊤\"");
    }

    static ScopeId ParseScope(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("scene", out var sc))
            throw new FormatException("scope 须为 {\"scene\":\"Name\"} 等");
        var name = sc.GetString() ?? throw new FormatException("scope.name 缺失");
        return el.TryGetProperty("type", out var ty) ? ty.GetString() switch
        {
            "method" => new ScopeId.Method(name),
            "type" => new ScopeId.Type(name),
            "global" => new ScopeId.Global(),
            "scene" => new ScopeId.Scene(name),
            _ => new ScopeId.Scene(name)
        } : new ScopeId.Scene(name);
    }

    static LoopCount ParseLoop(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String && el.GetString() == "⊤") return LoopCount.Top;
        if (el.ValueKind == JsonValueKind.Number) return LoopCount.Of(el.GetUInt64());
        throw new FormatException("loop 须为数字或 \"⊤\"");
    }

    static Signature ParseFootprint(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Array) throw new FormatException("footprint 须为 claim 数组");
        var claims = new List<Claim>();
        foreach (var c in el.EnumerateArray())
            claims.Add(ParseClaim(c));
        return Signature.Of(claims.ToArray());
    }

    static Claim ParseClaim(JsonElement c)
    {
        var kind = ParseKind(Require(c, "kind").GetString() ?? throw new FormatException("kind 缺失"));
        var res = ParseResource(Require(c, "resource"));
        var mode = ParseMode(Require(c, "mode").GetString() ?? throw new FormatException("mode 缺失"));
        var scope = ParseScope(Require(c, "scope"));
        var size = c.TryGetProperty("size", out var sz) ? ParseInterval(sz) : Interval.Default;
        return new Claim(kind, res, mode, scope, size).Normalize();
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

    static ResourceId ParseResource(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) throw new FormatException("resource 须为对象");
        if (!el.TryGetProperty("gpu", out var g) && !el.TryGetProperty("commandBuffer", out g) &&
            !el.TryGetProperty("memory", out g) && !el.TryGetProperty("occupancy", out g) &&
            !el.TryGetProperty("signalBus", out g))
            throw new FormatException("resource 须含 gpu/commandBuffer/memory/occupancy/signalBus 之一");
        if (el.TryGetProperty("gpu", out var gpu)) return new ResourceId.Gpu(new Rid(gpu.GetString() ?? ""));
        if (el.TryGetProperty("commandBuffer", out var cb)) return new ResourceId.CommandBuffer(cb.GetString() ?? "gpu");
        if (el.TryGetProperty("memory", out var mem)) return new ResourceId.Memory(mem.ValueKind == JsonValueKind.Number ? mem.GetUInt64() : 0);
        if (el.TryGetProperty("occupancy", out var occ)) return new ResourceId.Occupancy(occ.GetString() ?? "");
        if (el.TryGetProperty("signalBus", out var sb)) return new ResourceId.SignalBus(new StringName(sb.GetString() ?? ""));
        throw new FormatException("resource 形状非法");
    }

    static IReadOnlyDictionary<ResourceId, NatStar> ParseBudget(JsonElement bud)
    {
        var dict = new Dictionary<ResourceId, NatStar>();
        foreach (var prop in bud.EnumerateObject())
        {
            var r = ParseResourceKey(prop.Name);
            dict[r] = NatStar.Of(prop.Value.GetUInt64());
        }
        return dict;
    }

    static ResourceId ParseResourceKey(string key) => key switch
    {
        var k when k.StartsWith("gpu:", StringComparison.Ordinal) => new ResourceId.Gpu(new Rid(k["gpu:".Length..])),
        var k when k.StartsWith("commandBuffer:", StringComparison.Ordinal) => new ResourceId.CommandBuffer(k["commandBuffer:".Length..]),
        var k when k.StartsWith("memory:", StringComparison.Ordinal) => new ResourceId.Memory(k["memory:".Length..].Length > 0 ? ulong.Parse(k["memory:".Length..], CultureInfo.InvariantCulture) : 0),
        var k when k.StartsWith("occupancy:", StringComparison.Ordinal) => new ResourceId.Occupancy(k["occupancy:".Length..]),
        var k when k.StartsWith("signalBus:", StringComparison.Ordinal) => new ResourceId.SignalBus(new StringName(k["signalBus:".Length..])),
        _ => throw new FormatException($"未知 budget 键: {key}")
    };

    // ── 序列化（round-trip） ──
    static object SerializeEvent(EffectEvent e) => new Dictionary<string, object?>
    {
        ["lifetime"] = new object[] { e.Lifetime.Lo.IsTop ? "⊤" : (object)e.Lifetime.Lo.Value, e.Lifetime.Hi.IsTop ? "⊤" : (object)e.Lifetime.Hi.Value },
        ["scope"] = SerializeScope(e.Scope),
        ["loop"] = e.Loop.Count.IsTop ? "⊤" : (object)e.Loop.Count.Value,
        // OPEN-2 修（auditR3b TC5）：序列化为完整 footprint，含 read/write/occupy 三桶；
        // 仅 Occupy 会丢桶破坏 round-trip（Parse 已按 claim.kind 路由三桶，故对称）。
        ["footprint"] = e.Footprint.ReadClaims.Concat(e.Footprint.WriteClaims).Concat(e.Footprint.OccupyClaims)
            .Select(SerializeClaim).ToArray()
    };

    static object SerializeScope(ScopeId s) => s switch
    {
        ScopeId.Scene sc => new Dictionary<string, object?> { ["scene"] = sc.Name },
        ScopeId.Method m => new Dictionary<string, object?> { ["type"] = "method", ["scene"] = m.Name },
        ScopeId.Type t => new Dictionary<string, object?> { ["type"] = "type", ["scene"] = t.Name },
        _ => new Dictionary<string, object?> { ["type"] = "global" }
    };

    static object SerializeClaim(Claim c) => new Dictionary<string, object?>
    {
        ["kind"] = c.Kind.ToString().ToLowerInvariant(),
        ["resource"] = SerializeResource(ResourceId.Normalize(c.Resource)),
        ["mode"] = c.Mode.ToString().ToLowerInvariant(),
        ["scope"] = SerializeScope(c.Scope),
        ["size"] = new object[] { (c.Size ?? Interval.Default).Lo.IsTop ? "⊤" : (object)(c.Size ?? Interval.Default).Lo.Value, (c.Size ?? Interval.Default).Hi.IsTop ? "⊤" : (object)(c.Size ?? Interval.Default).Hi.Value }
    };

    static object SerializeResource(ResourceId r) => r switch
    {
        ResourceId.Gpu g => new Dictionary<string, object?> { ["gpu"] = g.BufferId.Value },
        ResourceId.CommandBuffer cb => new Dictionary<string, object?> { ["commandBuffer"] = cb.Channel },
        ResourceId.Memory m => new Dictionary<string, object?> { ["memory"] = m.Uid },
        ResourceId.Occupancy o => new Dictionary<string, object?> { ["occupancy"] = o.Channel },
        ResourceId.SignalBus sb => new Dictionary<string, object?> { ["signalBus"] = sb.Name.Value },
        _ => new Dictionary<string, object?> { ["memory"] = 0 }
    };

    static object SerializeBudget(IReadOnlyDictionary<ResourceId, NatStar> caps)
    {
        var d = new Dictionary<string, object?>();
        foreach (var kv in caps)
            d[ResourceKey(kv.Key)] = kv.Value.Value;
        return d;
    }

    static string ResourceKey(ResourceId r) => r switch
    {
        ResourceId.Gpu g => "gpu:" + g.BufferId.Value,
        ResourceId.CommandBuffer cb => "commandBuffer:" + cb.Channel,
        ResourceId.Memory m => "memory:" + m.Uid,
        ResourceId.Occupancy o => "occupancy:" + o.Channel,
        ResourceId.SignalBus sb => "signalBus:" + sb.Name.Value,
        _ => "memory:0"
    };

    static JsonElement Require(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v)) throw new FormatException($"缺少字段: {prop}");
        return v;
    }
}

/// <summary>§2.3 — 预算可附着在剧本上（便捷：Parse 后直接 Audit）。</summary>
public sealed partial class EffectScript
{
    /// <summary>§2.3 — 剧本级预算（默认无上限）。Parse 时由 JSON 'budget' 填充。</summary>
    public Budget Budget { get; init; } = Budget.None;

    /// <summary>§3 — 用自带 <see cref="Budget"/> 审计。</summary>
    public AuditResult Audit() => Audit(Budget);
}
