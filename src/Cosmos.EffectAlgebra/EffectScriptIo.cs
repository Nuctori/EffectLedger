// EffectScriptIo.cs — EFFECT_SCRIPT.md §4 / Iter11 AI 数据契约（零 Godot 依赖）。L1 增量。
// 将 AI 产出的 EffectScript JSON 反序列化为 L1 类型；亦可序列化回 JSON（round-trip 验证契约稳定）。
// 设计：fail-fast 解析——未知 kind/shape ⇒ 抛 ArgumentException（sound：不静默猜测未知资源）。
// 仅使用 BCL System.Text.Json（net10 自带），零外部依赖、零 Godot 依赖。
using System.Collections.Immutable;
using System.Text.Json;

namespace Cosmos.EffectAlgebra;

/// <summary>
/// §4 / Iter11 — EffectScript 的 JSON ↔ L1 契约。AI 产出此 JSON（不经 §7 白名单、不经 Godot 运行期），
/// 喂 <see cref="EffectScript.Audit"/> 即可静态审计。解析为 fail-fast（未知字段/枚举 ⇒ 抛）。
/// </summary>
public static class EffectScriptIo
{
    /// <summary>§4 — 从 JSON 文本解析剧本；非法结构 ⇒ <see cref="ArgumentException"/>。</summary>
    public static EffectScript Parse(string json)
    {
        var root = JsonDocument.Parse(json).RootElement; // 不 using：RootElement 需存活至解析完成
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("events", out var evArr))
            throw new ArgumentException("EffectScript JSON 必须有 events 数组", nameof(json));
        if (evArr.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("events 必须是数组", nameof(json));

        var events = new List<EffectEvent>();
        foreach (var e in evArr.EnumerateArray())
            events.Add(ParseEvent(e));

        return new EffectScript(events.ToImmutableArray());
    }

    /// <summary>§4 — 从完整 JSON 文本取 Budget（caps 节）；缺省空预算。</summary>
    public static Budget ParseBudget(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("budget", out var b) && b.ValueKind == JsonValueKind.Object)
            return ParseBudget(b);
        return Budget.None;
    }

    /// <summary>§4 — 从 JSON Element 取 Budget（caps 节）；缺省空预算。</summary>
    public static Budget ParseBudget(JsonElement b)
    {
        var caps = new Dictionary<ResourceId, NatStar>();
        if (b.TryGetProperty("caps", out var capsArr) && capsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in capsArr.EnumerateArray())
            {
                if (!c.TryGetProperty("resource", out var resEl) || !c.TryGetProperty("cap", out var capEl))
                    throw new ArgumentException("caps 项需 resource + cap", nameof(b));
                var r = ParseResource(resEl);
                var cap = capEl.ValueKind == JsonValueKind.Number ? NatStar.Of(capEl.GetUInt64()) : NatStar.Top;
                caps[r] = cap;
            }
        }
        return new Budget(caps);
    }

    private static EffectEvent ParseEvent(JsonElement e)
    {
        if (!e.TryGetProperty("lifetime", out var lifeEl))
            throw new ArgumentException("event 缺 lifetime", nameof(e));
        var life = ParseInterval(lifeEl);
        var scope = e.TryGetProperty("scope", out var s) ? ParseScope(s) : new ScopeId.Scene("Default");
        var loop = e.TryGetProperty("loop", out var l)
            ? (l.ValueKind == JsonValueKind.String ? LoopCount.Top : LoopCount.Of(l.ValueKind == JsonValueKind.Number ? l.GetUInt64() : 1))
            : LoopCount.Of(1);
        if (!e.TryGetProperty("footprint", out var fp) || fp.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("event 必须有 footprint 数组", nameof(e));

        var claims = new List<Claim>();
        foreach (var c in fp.EnumerateArray())
            claims.Add(ParseClaim(c, scope));
        return new EffectEvent(life, scope, Signature.Of(claims.ToArray()), loop);
    }

    private static Interval ParseInterval(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Number)
            return Interval.Exact(v.GetUInt64());
        if (v.ValueKind == JsonValueKind.Array)
        {
            var arr = v.EnumerateArray().ToImmutableArray();
            if (arr.Length != 2) throw new ArgumentException("interval 数组需 [lo,hi]", nameof(v));
            var lo = ParseBound(arr[0]);
            var hi = ParseBound(arr[1]);
            return new Interval(lo, hi);
        }
        throw new ArgumentException("interval 需是数或 [lo,hi]", nameof(v));
    }

    private static NatStar ParseBound(JsonElement b)
    {
        if (b.ValueKind == JsonValueKind.String) // "⊤" 或 "top" ⇒ 上界
            return NatStar.Top;
        if (b.ValueKind == JsonValueKind.Number)
            return NatStar.Of(b.GetUInt64());
        throw new ArgumentException("bound 需是数或 '⊤'", nameof(b));
    }

    private static Claim ParseClaim(JsonElement c, ScopeId scope)
    {
        var kindStr = c.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String
            ? k.GetString()! : throw new ArgumentException("claim 缺 kind 字符串", nameof(c));
        var modeStr = c.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()! : throw new ArgumentException("claim 缺 mode 字符串", nameof(c));
        var kind = Enum.Parse<Kind>(kindStr, ignoreCase: true);
        var resource = ParseResource(c.GetProperty("resource"));
        var mode = Enum.Parse<Mode>(modeStr, ignoreCase: true);
        var size = c.TryGetProperty("size", out var sz) ? ParseInterval(sz) : Interval.Default;
        return new Claim(kind, resource, mode, scope, size).Normalize();
    }

    private static ResourceId ParseResource(JsonElement r)
    {
        if (r.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("resource 需是对象", nameof(r));
        foreach (var prop in r.EnumerateObject())
        {
            var name = prop.Name;
            var v = prop.Value;
            if (name == "gpu") return new ResourceId.Gpu(new Rid(Extract(v, "bufferId")));
            if (name == "commandBuffer") return new ResourceId.CommandBuffer(Extract(v, "channel", "gpu"));
            if (name == "memory") return new ResourceId.Memory(ExtractUInt64(v, 0)); // 修 auditR：消费 JSON uid，与 Contract/L1 Memory(uid) 一致，不再硬编码 Memory(0)
            if (name == "tree") return new ResourceId.Tree(NodePathOrUnknown.Of(Extract(v, "path", "root")));
            if (name == "self") return new ResourceId.Self(Extract(v, "component", "self"));
            if (name == "physics") return new ResourceId.Physics(new Rid(Extract(v, "bodyId", "b")));
            if (name == "disk") return new ResourceId.Disk(Extract(v, "path", "disk"));
            if (name == "signal") return new ResourceId.Signal(new StringName(Extract(v, "name", "sig")));
            if (name == "signalBus" || name == "signal_bus") return new ResourceId.SignalBus(new StringName(Extract(v, "name", "bus")));
            if (name == "audioMixer") return new ResourceId.AudioMixer(0);
            if (name == "occupancy") return new ResourceId.Occupancy(Extract(v, "channel", "ch"));
            if (name == "callback") return new ResourceId.Callback(Extract(v, "id", "cb"));
            if (name == "network") return new ResourceId.Network(0, Extract(v, "method", "m"));
            if (name == "input") return new ResourceId.Input(Extract(v, "action", "a"));
            if (name == "custom") return new ResourceId.Custom(Extract(v, "name", "x"));
            throw new ArgumentException($"未知 resource 形状: {name}", nameof(r));
        }
        throw new ArgumentException("resource 对象为空", nameof(r));
    }

    private static ScopeId ParseScope(JsonElement s)
    {
        if (s.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("scope 需是对象", nameof(s));
        foreach (var prop in s.EnumerateObject())
        {
            var name = prop.Name;
            var v = prop.Value;
            if (name == "scene") return new ScopeId.Scene(Extract(v, "name", "S"));
            if (name == "type") return new ScopeId.Type(Extract(v, "name", "T"));
            if (name == "method") return new ScopeId.Method(Extract(v, "name", "M"));
            if (name == "global") return new ScopeId.Global();
            if (name == "shell") return new ScopeId.Shell();
            if (name == "loop") return new ScopeId.Loop(Extract(v, "id", "L"));
            if (name == "conditional") return new ScopeId.Conditional(Extract(v, "branch", "C"));
            if (name == "async") return new ScopeId.Async(Extract(v, "id", "A"));
            throw new ArgumentException($"未知 scope 形状: {name}", nameof(s));
        }
        throw new ArgumentException("scope 对象为空", nameof(s));
    }

    // 取资源子字段中的 ulong（兼容「数字」「{uid:N}」「字符串数字」）；缺省/非数字 ⇒ fallback。
    private static ulong ExtractUInt64(JsonElement e, ulong fallback = 0)
    {
        if (e.ValueKind == JsonValueKind.Number) return e.GetUInt64();
        if (e.ValueKind == JsonValueKind.String && ulong.TryParse(e.GetString(), out var n)) return n;
        if (e.TryGetProperty("uid", out var u) && u.ValueKind == JsonValueKind.Number) return u.GetUInt64();
        return fallback;
    }

    // 取资源/作用域子字段：兼容「对象 {field: "x"}」与「字符串 "x"」两种简写。
    private static string Extract(JsonElement e, string field, string fallback = "")
    {
        if (e.ValueKind == JsonValueKind.String) return e.GetString()!; // 简写："gpu" 等同 {"bufferId":"gpu"}
        if (!e.TryGetProperty(field, out var v)) return fallback;
        if (v.ValueKind == JsonValueKind.String) return v.GetString()!;
        return fallback;
    }
}

