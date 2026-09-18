// ContractConfig.cs — P1.4 + P4.4：effectledger.contracts.json 配置入口与用户摘要。
//
// 设计要点（对照计划原文）：
//   P1.4：独立文件名、schema/version/policy 框架；经 AdditionalFiles 读取；
//         重复文件/未知键/非法版本/大小超限 ⇒ **明确配置错误**（绝不静默）。
//   P4.4：用户摘要字段（schemaVersion/assembly identity/符号 ID/effect/alias/callback 条件/
//         reason/evidenceRef）；拒重复键与未知字段；三步校验（语法→符号唯一→条件适用）；
//         不得静默覆盖内建禁止项；摘要是 **trust 不是 proof**，内容指纹进报告。
//
// 为什么必须有这个入口：没有它，任何跨工程/第三方依赖只能永久落 `ExternalSummaryMissing`，
// strict 必失败且**无补救途径**——这是用户视角审计判定的最大采纳障碍（BC-124/BC-133）。
//
// 本文件同时被分析器与 Tool 编译（共享源），因此不依赖 any 单侧特有 API。

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

#if ANALYZER_SHARED
namespace EffectLedger.Analyzer.Shared;
#elif GENERATOR_SHARED
namespace EffectLedger.Generator.Shared;
#else
namespace EffectLedger.Contracts.Analyzer.Analysis;
#endif

/// <summary>一条用户摘要：声明某个外部符号的行为，供分析器作为 <b>trust</b> 使用。</summary>
public sealed record UserSummary(
    /// <summary>精确符号 ID：`命名空间.类型::成员`（如 `MyApp.Clock::Now`）。</summary>
    string SymbolId,
    /// <summary>可选程序集身份约束；null = 不约束。</summary>
    string? AssemblyIdentity,
    /// <summary>效果类别：none/pure/hidden-time/hidden-random/hidden-ambient/hidden-culture/io-console/io-file/io-network/static-write。</summary>
    string Effect,
    /// <summary>返回值是否可能是入参/receiver 的别名。</summary>
    bool ReturnsAlias,
    /// <summary>是否同步执行传入的回调。</summary>
    bool ExecutesCallback,
    /// <summary>是否保存传入的回调（逃逸）。</summary>
    bool StoresCallback,
    /// <summary>必填：为何可信（须引用证据，不接受"信任我"）。</summary>
    string Reason,
    /// <summary>必填：证据引用（ADR/issue/文档路径等）。</summary>
    string EvidenceRef);

/// <summary>配置读取结果：摘要集 + 内容指纹 + 错误集（错误非空 ⇒ 调用方必须 loud 处理）。</summary>
public sealed record ContractConfig(
    ImmutableArray<UserSummary> Summaries,
    /// <summary>配置内容指纹（sha256 前 16 位）；无配置时为 "none"。进报告与缓存键（P4.4）。</summary>
    string Fingerprint,
    ImmutableArray<string> Errors,
    /// <summary>策略开关（P1.4 框架 + P4.4 例外审批）。</summary>
    PolicyOptions Policy)
{
    public static readonly ContractConfig Empty = new(
        ImmutableArray<UserSummary>.Empty, "none", ImmutableArray<string>.Empty, PolicyOptions.Default);

    public bool IsValid => Errors.IsEmpty;
}

/// <summary>策略项（P1.4 框架；P4.4 的例外审批在此承载）。</summary>
public sealed record PolicyOptions(
    /// <summary>是否允许用户摘要参与判定（strict 默认允许，但报告必须标注 trust）。</summary>
    bool AllowUserSummaries,
    /// <summary>是否允许用户摘要**覆盖内建禁止项**（默认 false；开启须逐条给出批准依据）。</summary>
    bool AllowBuiltinOverride,
    /// <summary>本条目的批准依据（AllowBuiltinOverride 为 true 时必填）。</summary>
    string? OverrideJustification)
{
    public static readonly PolicyOptions Default = new(true, false, null);
}

/// <summary>
/// effectledger.contracts.json 的严格解析器。
/// 不抛异常：所有问题汇总为 <see cref="ContractConfig.Errors"/>，由调用方 loud 报告
/// （分析器报诊断、Tool 返回 exit 1）——"配置错配静默"是本模块明令禁止的形态。
/// </summary>
public static class ContractConfigParser
{
    /// <summary>单文件大小上限（计划 P1.4："大小超限报明确配置错误"）。</summary>
    public const int MaxBytes = 256 * 1024;

    /// <summary>摘要条目上限（计划 P4.4："设置尺寸和条目数限额"）。</summary>
    public const int MaxSummaries = 1_000;

    private static readonly HashSet<string> LegalRootKeys = new(StringComparer.Ordinal)
    { "$schema", "schemaVersion", "policy", "summaries" };

    private static readonly HashSet<string> LegalSummaryKeys = new(StringComparer.Ordinal)
    { "symbolId", "assemblyIdentity", "effect", "returnsAlias", "executesCallback", "storesCallback", "reason", "evidenceRef" };

    private static readonly HashSet<string> LegalEffectValues = new(StringComparer.Ordinal)
    {
        "none", "pure",
        "hidden-time", "hidden-random", "hidden-ambient", "hidden-culture",
        "io-console", "io-file", "io-network", "io-static-write",
    };

    public static ContractConfig Parse(string jsonText)
    {
        var errors = ImmutableArray.CreateBuilder<string>();

        if (jsonText is null) { errors.Add("配置内容为 null"); return Invalid(errors); }
        if (System.Text.Encoding.UTF8.GetByteCount(jsonText) > MaxBytes)
        {
            errors.Add($"配置超过大小上限 {MaxBytes} 字节（计划 P1.4 要求 loud 拒绝）");
            return Invalid(errors);
        }
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            errors.Add("配置为空（请删除该文件或填入 {\\\"schemaVersion\\\": \\\"1.0.0\\\"}）");
            return Invalid(errors);
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(jsonText, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow }); }
        catch (JsonException ex) { errors.Add($"JSON 语法非法（第一步校验失败）：{ex.Message}"); return Invalid(errors); }
        using var _ = doc;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add("根须为对象（第一步校验失败）");
            return Invalid(errors);
        }

        // ── 根层未知键与重复键 ──
        var seenRoot = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in root.EnumerateObject())
        {
            if (!LegalRootKeys.Contains(p.Name))
                errors.Add($"未知根键 \"{p.Name}\"（合法：$schema/schemaVersion/policy/summaries）；拼错会让配置静默失效，故 loud 拒绝");
            if (!seenRoot.Add(p.Name))
                errors.Add($"根键重复：\"{p.Name}\"（JSON 重复键会被静默择一，故拒绝）");
        }

        // ── schemaVersion（合法版本白名单）──
        if (!root.TryGetProperty("schemaVersion", out var verEl) || verEl.ValueKind != JsonValueKind.String)
        {
            errors.Add("缺少 schemaVersion（须为字符串，当前支持 \"1.0.0\"）");
        }
        else if (verEl.GetString() != "1.0.0")
        {
            errors.Add($"不支持的 schemaVersion \"{verEl.GetString()}\"（当前仅支持 1.0.0）");
        }

        var policy = ParsePolicy(root, errors);
        var summaries = ParseSummaries(root, errors);

        var fingerprint = Fingerprint(jsonText);
        if (errors.Count > 0) return new ContractConfig(summaries, fingerprint, errors.ToImmutable(), policy);
        return new ContractConfig(summaries, fingerprint, ImmutableArray<string>.Empty, policy);
    }

    private static PolicyOptions ParsePolicy(JsonElement root, ImmutableArray<string>.Builder errors)
    {
        if (!root.TryGetProperty("policy", out var pol)) return PolicyOptions.Default;
        if (pol.ValueKind != JsonValueKind.Object)
        {
            errors.Add("policy 须为对象");
            return PolicyOptions.Default;
        }
        bool allowSummaries = true, allowOverride = false;
        string? justification = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in pol.EnumerateObject())
        {
            if (p.Name is not ("allowUserSummaries" or "allowBuiltinOverride" or "overrideJustification"))
                errors.Add($"policy 未知键 \"{p.Name}\"");
            if (!seen.Add(p.Name)) errors.Add($"policy 键重复：\"{p.Name}\"");
            switch (p.Name)
            {
                case "allowUserSummaries":
                    if (p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) errors.Add("policy.allowUserSummaries 须为布尔");
                    else allowSummaries = p.Value.GetBoolean();
                    break;
                case "allowBuiltinOverride":
                    if (p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) errors.Add("policy.allowBuiltinOverride 须为布尔");
                    else allowOverride = p.Value.GetBoolean();
                    break;
                case "overrideJustification":
                    justification = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                    break;
            }
        }
        // P4.4：覆盖内建禁止项须独立策略项 + 批准依据；strict 默认不允许。
        if (allowOverride && string.IsNullOrWhiteSpace(justification))
            errors.Add("policy.allowBuiltinOverride=true 必须同时提供 overrideJustification（计划 P4.4：例外须独立策略项，不允许未批准例外）");
        return new PolicyOptions(allowSummaries, allowOverride, justification);
    }

    private static ImmutableArray<UserSummary> ParseSummaries(JsonElement root, ImmutableArray<string>.Builder errors)
    {
        var list = ImmutableArray.CreateBuilder<UserSummary>();
        if (!root.TryGetProperty("summaries", out var arr)) return list.ToImmutable();
        if (arr.ValueKind != JsonValueKind.Array) { errors.Add("summaries 须为数组"); return list.ToImmutable(); }
        if (arr.GetArrayLength() > MaxSummaries)
        {
            errors.Add($"summaries 条目数 {arr.GetArrayLength()} 超过上限 {MaxSummaries}（计划 P4.4 条目限额）");
            return list.ToImmutable();
        }

        int idx = 0;
        var seenSymbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr.EnumerateArray())
        {
            var at = $"summaries[{idx++}]";
            if (item.ValueKind != JsonValueKind.Object) { errors.Add($"{at} 须为对象"); continue; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in item.EnumerateObject())
            {
                if (!LegalSummaryKeys.Contains(p.Name)) errors.Add($"{at} 未知键 \"{p.Name}\"");
                if (!seen.Add(p.Name)) errors.Add($"{at} 键重复：\"{p.Name}\"");
            }
            var symbolId = Str(item, "symbolId");
            if (string.IsNullOrWhiteSpace(symbolId)) { errors.Add($"{at}.symbolId 必填且非空（精确符号 ID，如 MyApp.Clock::Now）"); continue; }
            // P4.4：冲突摘要（同符号多条）拒绝
            if (!seenSymbols.Add(symbolId!)) errors.Add($"{at}: 符号 \"{symbolId}\" 存在冲突摘要（同符号多条，拒绝）");
            var effect = Str(item, "effect") ?? "none";
            if (!LegalEffectValues.Contains(effect)) errors.Add($"{at}.effect 非法值 \"{effect}\"");
            var reason = Str(item, "reason");
            if (string.IsNullOrWhiteSpace(reason)) errors.Add($"{at}.reason 必填（摘要是 trust：须说明为何可信，不接受\"信任我\"）");
            var evid = Str(item, "evidenceRef");
            if (string.IsNullOrWhiteSpace(evid)) errors.Add($"{at}.evidenceRef 必填（引用 ADR/issue/文档路径）");

            list.Add(new UserSummary(
                symbolId!,
                Str(item, "assemblyIdentity"),
                effect,
                Bool(item, "returnsAlias"),
                Bool(item, "executesCallback"),
                Bool(item, "storesCallback"),
                reason ?? "",
                evid ?? ""));
        }
        return list.ToImmutable();
    }

    private static string? Str(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool Bool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static ContractConfig Invalid(ImmutableArray<string>.Builder errors)
        => new(ImmutableArray<UserSummary>.Empty, "none", errors.ToImmutable(), PolicyOptions.Default);

    /// <summary>内容指纹（P4.4：进报告与缓存键）。</summary>
    public static string Fingerprint(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
