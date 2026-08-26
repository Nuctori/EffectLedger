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
        // R6-E1（hickey-x）：根级未知键静默忽略会让拼写错误（budgat / 大写 Budget）静默禁用整个预算门——
        // fail-fast 白名单：根级只认 events/budget，其余键拒绝并列出合法键集。
        RejectUnknownKeys(root, "根", "events", "budget");

        var events = new List<EffectEvent>();
        int evIdx = 0;
        foreach (var ev in evArr.EnumerateArray())
            events.Add(ParseEvent(ev, $"events[{evIdx++}]"));

        IReadOnlyDictionary<ResourceId, NatStar> caps = Budget.None.Caps;
        if (root.TryGetProperty("budget", out var bud))
        {
            // R6-E3（hickey-x）：budget 键存在但类型不对（数组/字符串等）⇒ 抛，而非静默跳过让 gate(2) 整体失效。
            if (bud.ValueKind != JsonValueKind.Object)
                throw new FormatException($"budget 须为对象（形如 {{\"gpu:x\": 5}}），实际为 {bud.ValueKind}");
            caps = ParseBudget(bud);
        }

        return new EffectScript(events.ToImmutableArray(), new Budget(caps));
    }

    /// <summary>R6-E1（hickey-x）— 对象层未知键白名单校验：layer 为层名（如「根」），合法键列表进报错。</summary>
    static void RejectUnknownKeys(JsonElement obj, string layer, params string[] known)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            bool ok = false;
            foreach (var k in known)
                if (prop.Name == k) { ok = true; break; }
            if (!ok)
                throw new FormatException($"{layer}层未知键 \"{prop.Name}\"（合法键: {string.Join(", ", known)}；区分大小写与拼写）");
        }
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
        // rich-hickey2 R1-F4：UnsafeRelaxedJsonEscaping —— ⊤ 是契约一等公民（budget/lifetime/loop 合法值），
        // 默认编码器把它转成 \u264b 破坏 AI 可读性与幂等 round-trip（Parse 接受 "⊤"/"inf"，序列化侧须输出同形）。
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    // ── 事件解析 ──
    static EffectEvent ParseEvent(JsonElement ev, string layer)
    {
        if (ev.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer}: event 须为对象");
        // rich-hickey2 R1-F2：事件层与根层同型未知键白名单——拼写错误（如大写 "Loop"）不得被静默吞掉后落回缺省 ω=1。
        RejectUnknownKeys(ev, layer, "lifetime", "scope", "loop", "footprint");
        var life = ParseInterval(Require(ev, "lifetime", layer));
        var scope = ParseScope(Require(ev, "scope", layer));
        var loop = ev.TryGetProperty("loop", out var l) ? ParseLoop(l) : LoopCount.Of(1);
        var fp = ParseFootprint(Require(ev, "footprint", layer), layer);
        return new EffectEvent(life, scope, fp, loop);
    }

    static Interval ParseInterval(JsonElement el, string layer = "lifetime")
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var items = el.EnumerateArray().ToArray();
            if (items.Length != 2) throw new FormatException($"{layer}: lifetime 数组须 [lo,hi]");
            var lo = ParseTop(items[0]);
            var hi = ParseTop(items[1]);
            // R10-F2 / EFFECT_SCRIPT.md §「已知锐边」：[⊤,⊤] 寿命视为非法输入——Lo=⊤ 的事件永不存活，
            // 会让 create-without-release 泄漏剧本在端点采样下静默全绿（假绿）。fail-fast 拒绝。
            if (lo.IsTop) throw new FormatException("lifetime 下界不可为 \"⊤\"（[⊤,⊤] 非法：事件永不存活会掩盖泄漏，EFFECT_SCRIPT.md）");
            return new Interval(lo, hi);
        }
        throw new FormatException("lifetime 须为 [lo,hi] 数组（hi 可为 \"⊤\" 表示∞）");
    }

    // hi="⊤" 或数字字符串；lo 必须有限。
    static NatStar ParseTop(JsonElement el, string layer = "lifetime")
    {
        if (el.ValueKind == JsonValueKind.String && el.GetString() == "⊤") return NatStar.Top;
        // rich-hickey2 R2-006：负数/小数等非 UInt64 数字 ⇒ 契约 FormatException，不漏 BCL 异常。
        if (el.ValueKind == JsonValueKind.Number)
            return el.TryGetUInt64(out var n) ? NatStar.Of(n)
                : throw new FormatException($"{layer}: 端点须为非负整数或 \"⊤\"");
        throw new FormatException("lifetime 端点须为数字或 \"⊤\"");
    }

    static ScopeId ParseScope(JsonElement el, string layer = "scope")
    {
        if (el.ValueKind != JsonValueKind.Object)
            throw new FormatException($"{layer}: scope 须为对象");
        // rich-hickey2 R1-F5：零字段 scope 对象 ⇒ 拒绝——resource 侧要求非空身份，scope 侧不许凭空捏匿名者参与冲突分组。
        if (!el.TryGetProperty("scene", out _) && !el.TryGetProperty("type", out _))
            throw new FormatException($"{layer}: scope 须含 scene 或 type（至少一个字段）");
        // Global 无 name（修 auditR4 CRITICAL：SerializeScope 输出 {"type":"global"} 无 scene，原 Parse 强制 scene ⇒ round-trip 必炸）。
        var hasScene = el.TryGetProperty("scene", out var sc);
        var name = hasScene ? ReqStr(sc, $"{layer}.scene") : "";
        // 缺 type ⇒ 默认 Scene(name)（与 SerializeScope 的 Scene 形态 {"scene":"S"} 一致）；
        // 仅未知 type（如 "gloabl"）才抛，避免静默当成 Scene("")（修 reviewer LOW）。
        if (!el.TryGetProperty("type", out var ty))
            return new ScopeId.Scene(name);
        // rich-hickey2 R1-F4：type 非字符串 ⇒ FormatException（带字段名），不漏 BCL InvalidOperationException。
        return ReqStr(ty, $"{layer}.type") switch
        {
            "method" => new ScopeId.Method(name),
            "type" => new ScopeId.Type(name),
            "global" => new ScopeId.Global(),
            "scene" => new ScopeId.Scene(name),
            _ => throw new FormatException($"{layer}: 未知 scope.type: {ty.GetString()}")
        };
    }

    static LoopCount ParseLoop(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String && el.GetString() == "⊤") return LoopCount.Top;
        if (el.ValueKind == JsonValueKind.Number)
        {
            var v = el.GetUInt64();
            if (v == 0) throw new FormatException("loop 必须 ≥1（0 无意义）或 \"⊤\"");
            return LoopCount.Of(v);
        }
        throw new FormatException("loop 须为数字或 \"⊤\"");
    }

    static Signature ParseFootprint(JsonElement el, string layer = "footprint")
    {
        if (el.ValueKind != JsonValueKind.Array) throw new FormatException($"{layer}: footprint 须为 claim 数组");
        var claims = new List<Claim>();
        int cIdx = 0;
        foreach (var c in el.EnumerateArray())
            claims.Add(ParseClaim(c, $"{layer}[{cIdx++}]"));
        return Signature.Of(claims.ToArray());
    }

    static Claim ParseClaim(JsonElement c, string layer = "claim")
    {
        if (c.ValueKind == JsonValueKind.Object)
            RejectUnknownKeys(c, layer, "kind", "resource", "mode", "scope", "size");
        // rich-hickey2 R1-F4：kind/mode 非字符串 ⇒ 带字段名的 FormatException（原 GetString() 漏 BCL 异常）。
        var kind = ParseKind(ReqStr(Require(c, "kind", layer), $"{layer}.kind"));
        var res = ParseResource(Require(c, "resource", layer));
        var mode = ParseMode(ReqStr(Require(c, "mode", layer), $"{layer}.mode"));
        var scope = ParseScope(Require(c, "scope", layer), $"{layer}.scope");
        var size = c.TryGetProperty("size", out var sz) ? ParseInterval(sz, $"{layer}.size") : Interval.Default;
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

    static ResourceId ParseResource(JsonElement el, string layer = "resource")
    {
        if (el.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer}: resource 须为对象");
        if (!el.TryGetProperty("gpu", out var g) && !el.TryGetProperty("commandBuffer", out g) &&
            !el.TryGetProperty("memory", out g) && !el.TryGetProperty("occupancy", out g) &&
            !el.TryGetProperty("signalBus", out g))
            throw new FormatException("resource 须含 gpu/commandBuffer/memory/occupancy/signalBus 之一");
        // 修 auditR2/R4 C2：resource 值缺失/类型错 ⇒ fail-fast（原静默兜底 "gpu"/""/0 会静默改写数据，比报错更危险）。
        if (el.TryGetProperty("gpu", out var gpu)) return new ResourceId.Gpu(new Rid(ReqStr(gpu, $"{layer}.gpu")));
        if (el.TryGetProperty("commandBuffer", out var cb)) return new ResourceId.CommandBuffer(ReqStr(cb, $"{layer}.commandBuffer"));
        if (el.TryGetProperty("memory", out var mem)) return new ResourceId.Memory(mem.ValueKind == JsonValueKind.Number ? mem.GetUInt64() : throw new FormatException("memory 须为数字 uid（如 {\"memory\":42}）"));
        if (el.TryGetProperty("occupancy", out var occ)) return new ResourceId.Occupancy(ReqStr(occ, "occupancy"));
        if (el.TryGetProperty("signalBus", out var sb)) return new ResourceId.SignalBus(new StringName(ReqStr(sb, "signalBus")));
        throw new FormatException("resource 形状非法");
    }

    static IReadOnlyDictionary<ResourceId, NatStar> ParseBudget(JsonElement bud)
    {
        var dict = new Dictionary<ResourceId, NatStar>();
        foreach (var prop in bud.EnumerateObject())
        {
            var r = ParseResourceKey(prop.Name);
            // R2-N1（hickey-x）：⊤ 须可往返——接受 "⊤"/"inf" 字符串为 NatStar.Top，否则序列化侧写出的 ⊤ 解析回有限值造成语义翻转。
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var s = prop.Value.GetString();
                if (s == "⊤" || s == "inf") { dict[r] = NatStar.Top; continue; }
                throw new FormatException($"budget[\"{prop.Name}\"] 字符串值仅接受 \"⊤\" 或 \"inf\"（表示无上限），实际 \"{s}\"");
            }
            // rich-hickey2 R2-006：非数字非"⊤"字符串值，或负数/小数 ⇒ FormatException（原 GetUInt64() 漏 BCL 异常）。
            var pv = prop.Value;
            if (pv.ValueKind != JsonValueKind.Number || !pv.TryGetUInt64(out var cap))
                throw new FormatException($"budget[\"{prop.Name}\"] 须为非负整数或 \"⊤\"/\"inf\" 字符串，实际为 {pv.ValueKind}");
            dict[r] = NatStar.Of(cap);
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
        ScopeId.Global => new Dictionary<string, object?> { ["type"] = "global" },
        _ => throw new FormatException($"不可序列化的 scope: {s}")
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
        _ => throw new FormatException($"不可序列化的 resource: {r}")
    };

    static object SerializeBudget(IReadOnlyDictionary<ResourceId, NatStar> caps)
    {
        var d = new Dictionary<string, object?>();
        foreach (var kv in caps)
            // R2-N1（hickey-x）：⊤ 序列化为 "⊤" 而非 .Value(=0)——否则 C# 合法的无上限预算 round-trip 后变 0，产生虚假 PeakExceeded/Leak。
            d[ResourceKey(kv.Key)] = kv.Value.IsTop ? (object)"⊤" : kv.Value.Value;
        return d;
    }

    static string ResourceKey(ResourceId r) => r switch
    {
        ResourceId.Gpu g => "gpu:" + g.BufferId.Value,
        ResourceId.CommandBuffer cb => "commandBuffer:" + cb.Channel,
        ResourceId.Memory m => "memory:" + m.Uid,
        ResourceId.Occupancy o => "occupancy:" + o.Channel,
        ResourceId.SignalBus sb => "signalBus:" + sb.Name.Value,
        _ => throw new FormatException($"不可序列化的 budget 键资源: {r}")
    };

    static JsonElement Require(JsonElement e, string prop, string layer = "根")
    {
        if (!e.TryGetProperty(prop, out var v)) throw new FormatException($"{layer}: 缺少字段: {prop}");
        return v;
    }

    // 资源值 fail-fast 提取（修 auditR2/R4 C2）：空串/非字符串 ⇒ 抛，不静默兜底。
    static string ReqStr(JsonElement v, string field) => v.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(v.GetString())
        ? v.GetString()!
        : throw new FormatException($"resource.{field} 须为非空字符串");
}

/// <summary>§2.3 — 预算可附着在剧本上（便捷：Parse 后直接 Audit）。</summary>
public sealed partial class EffectScript
{
    /// <summary>§3 — 用自带 <see cref="Budget"/> 审计（Budget 在主 EffectScript 定义为不可变构造参数，修 auditR5 F1）。</summary>
    public AuditResult Audit() => Audit(Budget);
}
