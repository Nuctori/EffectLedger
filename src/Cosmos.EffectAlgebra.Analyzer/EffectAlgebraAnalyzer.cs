using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Analyzer;

/// <summary>
/// L3 Roslyn Analyzer 骨架（迭代09）+ §14.3 A3(KIND_MIX)/A4(Compat 冲突) 完备性判据（迭代29 补）。
/// 角色：仅发**根因**诊断，绝不重算代数（net/Peak/Compatible 全在 L1，类型强制）。
/// 控制流近似不变式：本分析为调用级（intra-method）近似；运行期 Σnet（§3.3.1）为权威，
///   编译期静态结果仅作早期根因提示，不得据此声称"数学已验证"（注释明言，避免假绿）。
/// 诊断 id 引 §x.y：
///   EAA0901 = §3.3.1 DO-9 近似泄漏（acquire 无配对 release）；
///   EAA0303 = §14.3 A3 / §3.1.4b DO-7 量纲隔离：同归一资源跨 kind 混用；
///   EAA0304 = §14.3 A4 / §3.2.3 全函数冲突：同归一资源同模式对 Compatible==false。
/// 零 Godot 依赖：Godot API 名与 Claim 来自 L1 GodotApiWhitelist / ReleaseClass 数据（§7/§8.1），编译期字符串匹配。
///
/// §8.3.1 kind 覆盖边界（诚实声明）：[EffectOverride] 的 kind（read/write/occupy 互转）覆盖由 L1
///   <see cref="EffectOverrideAttribute"/> 类型层**根本不提供** OverrideKind 属性 ⇒ 类型强制禁止，良性代码
///   写不出该命名参数（编译器先报 CS0117）。因此 L3 不重检该约束（冗余护栏已删，迭代09 OPEN-1）。
///
/// DO-9 / A3 / A4 近似边界（诚实声明，迭代09 OPEN-2 + 迭代29 补）：本近似**仅扫描同一方法体内语法上可见的调用表达式**，
///   且按 canonical API 名（去 `.`/`_`、小写）匹配，不区分接收者。故：
///   - 带接收者前缀（如 `node.QueueFree()`、`GetTree().Free()`）与裸调用（如 `QueueFree()`、`this.QueueFree()`）
///     均被 canonical 名匹配，**同方法内**的配对可见；
///   - 但**跨方法**（释放/配对发生在被调用助手/不同方法中）、**跨对象**（发生在另一实例且经由参数/字段传递）
///     的配对**不在本静态近似覆盖内**，可能静默漏报（false negative，不误报）；
///   - 本分析为 flow-insensitive（不追踪控制流分支/条件），仅做"方法体内是否同时出现"。
///   真实闭合以运行期 <see cref="Cosmos.EffectAlgebra.NetTable.IsConserved"/> 为权威（§3.3.1）。
///
/// A3 / A4 误报护栏（迭代29 补）：§7 单条 API 的内部 Claim 可能天然跨 kind（如 AddChild 含 Write+Occupy）或
///   重复同模式（如 Load 含两次 Occupy(Mem,Create)）；这些**单条 API 内部**的多态是 PDR 白名单有意设计，非用户混用。
///   故 A3/A4 仅检测**跨调用（不同 InvocationExpression 站点）同归一资源**的 kind 多样性 / mode 冲突，不针对单条 API 内部。
///   数学已由 L1 类型保护（§3.1.4b 三桶隔离、§3.2.3 Compatible 全函数）；EAA0303/EAA0304 仅提示意图清晰度，非数学缺。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EffectAlgebraAnalyzer : DiagnosticAnalyzer
{
    // ── §3.3.1 DO-9 近似泄漏：方法内 acquire（create/occupy-create）无对应 release-class 且未标 [EffectOverride] ──
    // 诊断 id EAA0901（"09"=§3.3.1，"01"=DO-9 第 1 个静态近似规则）。
    private static readonly DiagnosticDescriptor MissingReleaseForAcquire = new(
        id: "EAA0901",
        title: "疑似资源泄漏（DO-9 静态近似）",
        messageFormat: "方法 '{0}' 调用了 acquire 类 API（{1}）但无对应 release-class 调用且未标 [EffectOverride]；运行期 Σnet 可能泄漏（§3.3.1 DO-9）。此为控制流近似，运行期 net 为权威。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "§3.3.1 DO-9 and §8.1 release-class: acquire (AddChild/Instantiate/Connect etc.) must be paired with a release-class (QueueFree/RemoveChild/Disconnect etc.); L3 performs call-site approximation only, with authoritative closure decided by runtime net (§3.3.1); this flow-insensitive scan may silently miss cross-method or cross-object pairings (see class comment).");

    // ── §8.3.1/§8.3.2 逃逸通道参数校验（编译期强制）：属性 ctor 不在编译期执行，故 L3 必须亲自校验参数 ──
    // C# attribute constructor 仅在运行期反射时执行，编译期 L2/L3 仅按名识别 ⇒ ctor 内的 reason 非空 / epsilon 上界
    // 检查是“死代码”（静默放行 = 审计门禁可被无证据 [EffectOverride("")] 绕过）。本诊断把该不变式迁移到编译期。
    private static readonly DiagnosticDescriptor OverrideReasonRequired = new(
        id: "EAA0801",
        title: "§8.3.1 [EffectOverride] reason 必填且非空",
        messageFormat: "方法 '{0}' 的 [EffectOverride] reason 必须是编译期可验证的非空字符串（引用证据，CI 人工 approve）。C# 不在编译期执行属性构造子，故由 L3 在编译期强制；空/空白 reason 视为无证逃逸通道，方法不豁免 DO-9/A3/A4。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "§8.3.1: [EffectOverride] reason 非空由 L1 构造子声称强制，但 attribute ctor 运行期才执行；本分析器在编译期用语义模型校验参数，避免无证逃逸通道被静默放行.");

    private static readonly DiagnosticDescriptor AcceptDeviationRange = new(
        id: "EAA0802",
        title: "§8.3.2 [AcceptDeviation] epsilon ∈ [0.0, 0.5]",
        messageFormat: "方法 '{0}' 的 [AcceptDeviation(ε)] 必须是编译期可验证的常量且 ε ∈ [0.0, 0.5]；越界或非常量视为非法，方法不豁免 DO-9/A3/A4。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "§8.3.2: [AcceptDeviation] epsilon 上界由 L1 构造子声称强制，但 attribute ctor 运行期才执行；本分析器在编译期用语义模型校验参数，避免越界 epsilon 被静默放行.");
    // 诊断 id EAA0303（"03"=§3.1.4b DO-7，"03"=第 3 个完备性判据 A3）。
    private static readonly DiagnosticDescriptor KindMixOnSameResource = new(
        id: "EAA0303",
        title: "同资源混用多类效应（A3 量纲隔离提示）",
        messageFormat: "方法 '{0}' 对同一资源（{1}）混用了多类效应（{2}：read/write/occupy），量纲隔离由 L1 运行期保证（§3.1.4b DO-7），但建议显式 [EffectOverride] 标注意图。此为控制流近似，运行期 net 为权威。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "§14.3 A3 and §3.1.4b DO-7: different effect kinds (read/write/occupy) on the same normalized resource are dimensionally isolated by L1 (NetTable only sums occupy bucket); this diagnostic only prompts intent clarity via [EffectOverride], not a math defect. Cross-invocation only (single API's internal claims excluded).");

    // ── §14.3 A4 / §3.2.3 全函数冲突：同归一资源 mode 对 Compatible==false 且未标 [EffectOverride] ──
    // 诊断 id EAA0304（"03"=§3.2.3，"04"=第 4 个完备性判据 A4）。
    private static readonly DiagnosticDescriptor CompatConflictOnSameResource = new(
        id: "EAA0304",
        title: "同资源并发冲突模式（A4 Compat 冲突）",
        messageFormat: "方法 '{0}' 对同一资源（{1}）出现冲突模式对（{2}，如重复 Create 无配对 Release），运行期可能泄漏/竞态（§3.2.3 Compatible 全函数）。未标 [EffectOverride] 时报告。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "§14.3 A4 and §3.2.3 Compatible total function: mode pairs in CONFLICT set {(Create,Create),(Move,Move),(Release,Release)} on the same normalized resource are reported; conflict semantics defined by L1 Compatible (§3.2.3). Cross-invocation only (single API's internal repeated claims excluded).");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(MissingReleaseForAcquire, KindMixOnSameResource, CompatConflictOnSameResource,
            OverrideReasonRequired, AcceptDeviationRange);

    // 预先从 L1 数据计算 canonical 名集合（控制流近似匹配用，零 Godot 依赖）。
    // Acquire：§7 白名单中任一 Claim 为 Mode.Create（含 Occupy+Create）的 API。
    // Release：§7 中任一 Claim 为 Mode.Release 的 API，并并上 §8.1 release-class（queue_free/... 等）。
    // §8.1 release-class（含不在 §7 白名单的泛型释放名，如 free/remove_from_group）：用于泛型释放兜底，避免误报（见 AnalyzeMissingRelease）。
    private static readonly ImmutableHashSet<string> ReleaseApiNames = BuildReleaseNames();

    private static string Canonical(string name) =>
        name.ToLowerInvariant().Replace(".", "").Replace("_", "");

    private static ImmutableHashSet<string> BuildReleaseNames()
    {
        var set = ImmutableHashSet.CreateBuilder<string>();
        foreach (var m in GodotApiWhitelist.All)
            if (m.Claims.Any(c => c.Mode == Mode.Release))
                set.Add(Canonical(m.GodotApi));
        // §8.1 release-class：Godot 方法名（snake_case 源）规范化为 C# PascalCase 匹配键。
        foreach (var r in ReleaseClass.Names)
            set.Add(Canonical(r));
        return set.ToImmutable();
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // §3.3.1 DO-9 / §14.3 A3 / §14.3 A4：以方法声明为分析单元（控制流近似：仅方法内调用可见性）。
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // 方法标注 [EffectOverride]/[AcceptDeviation] ⇒ 逃逸通道已声明。
        // 但 C# 不在编译期执行 attribute ctor，故 L1 构造子的 reason 非空 / epsilon 上界检查是“死代码”，
        // 必须由 L3 用语义模型在编译期校验参数。仅“参数合法”的标注才豁免 DO-9/A3/A4；
        // 非法标注报 EAA0801/EAA0802 且方法照常参与泄漏分析（§8.3.1/§8.3.2 根因修复）。
        bool hasValidEscape = false;
        foreach (var attr in method.AttributeLists.SelectMany(l => l.Attributes))
        {
            var name = attr.Name.ToString();
            if (IsEffectOverride(name))
            {
                if (IsValidOverrideReason(attr, context, method)) hasValidEscape = true;
            }
            else if (IsAcceptDeviation(name))
            {
                if (IsValidAcceptEpsilon(attr, context, method)) hasValidEscape = true;
            }
        }
        if (hasValidEscape) return;

        // 收集方法体内所有调用表达式（控制流近似：不展开被调用方法内部；
        // 仅语法可见 → 跨方法/跨对象释放配对不在覆盖内，可能静默漏报，见类注释 OPEN-2）。
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();

        AnalyzeMissingRelease(context, method, invocations);                    // EAA0901：永不豁免（fail-open）
        AnalyzeKindMixAndCompat(context, method, invocations);  // EAA0303/4：逃逸通道已在上方 hasValidEscape 提前 return 时豁免
    }

    // ── §3.3.1 DO-9 近似：按归一资源聚合「acquire>release」⇒ 疑似泄漏（运行期 net 为权威，见类注释）──
    // R8 对抗审计改进：逐资源计数，可捕获「跨资源错配释放」「部分释放（acquire 多于 release）」，
    // 而非旧版仅全局布尔（会漏报跨类型/部分释放）。§8.1 release-class-only 泛型释放（如 free）作兜底，避免误报。
    private static void AnalyzeMissingRelease(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method,
        InvocationExpressionSyntax[] invocations)
    {
        var acquire = new Dictionary<ResourceId, int>();
        var release = new Dictionary<ResourceId, int>();
        var firstAcquireName = new Dictionary<ResourceId, string>();
        bool hasReleaseClassOnly = false; // §8.1 泛型释放（不在 §7 白名单），可能覆盖任意资源

        foreach (var inv in invocations)
        {
            var canon = CanonicalOfInvocation(inv);
            var m = FindWhitelistEntry(inv);
            if (m is null)
            {
                // §8.1 release-class 但不在 §7 白名单：作为泛型释放兜底（避免误报，运行期 net 为权威）。
                if (ReleaseApiNames.Contains(canon)) hasReleaseClassOnly = true;
                continue;
            }
            foreach (var c in m.Value.Claims)
            {
                var key = ResourceId.Normalize(c.Resource);
                if (c.Mode == Mode.Create)
                {
                    acquire.TryGetValue(key, out var a); acquire[key] = a + 1;
                    if (!firstAcquireName.ContainsKey(key)) firstAcquireName[key] = RawName(inv);
                }
                else if (c.Mode == Mode.Release)
                {
                    release.TryGetValue(key, out var r); release[key] = r + 1;
                }
            }
        }

        // §3.3.1 DO-9 近似：存在归一资源 net 获取（acquire>release）⇒ 报告。
        // 除非存在 §8.1 泛型释放（可能覆盖该资源，运行期 net 为权威，避免误报）。
        if (hasReleaseClassOnly) return;
        foreach (var kv in acquire)
        {
            release.TryGetValue(kv.Key, out var r);
            if (kv.Value > r)
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingReleaseForAcquire,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text,
                    firstAcquireName[kv.Key]));
        }
    }

    // ── §14.3 A3 (KIND_MIX) / A4 (Compat 冲突) ──
    // 按归一资源分桶，跨调用（不同 InvocationExpression 站点）聚合 (Kind,Mode)。
    // 单条 API 内部的多态 Claim（如 AddChild 含 Write+Occupy、Load 含两次 Occupy(Create)）不报（见类注释护栏）。
    // 实现：记录每条 (归一资源, 调用序号) → 该调用的 (kind,mode) 集合；仅当"跨调用"出现 kind 多样 / mode 冲突才报告。
    private static void AnalyzeKindMixAndCompat(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method,
        InvocationExpressionSyntax[] invocations)
    {
        // 归一资源 → (调用序号 → 该调用的 (kind,mode) 集合)
        var byResource = new Dictionary<ResourceId, Dictionary<int, List<(Kind Kind, Mode Mode)>>>();
        var resourceLabel = new Dictionary<ResourceId, string>();

        int invIndex = 0;
        foreach (var inv in invocations)
        {
            var m = FindWhitelistEntry(inv);
            if (m is null) { invIndex++; continue; }

            // 同站点去重：本调用贡献的 (kind,mode) 集合（按（kind,mode）去重，避免单 API 内部重复 Claim 计入）
            var siteClaims = m.Value.Claims
                .Select(c => (c.Kind, c.Mode))
                .Distinct()
                .ToArray();

            foreach (var (kind, mode) in siteClaims)
            {
                var key = ResourceId.Normalize(m.Value.Claims.First(c => c.Kind == kind && c.Mode == mode).Resource);
                if (!byResource.ContainsKey(key))
                {
                    byResource[key] = new Dictionary<int, List<(Kind, Mode)>>();
                    resourceLabel[key] = m.Value.GodotApi;
                }
                if (!byResource[key].ContainsKey(invIndex))
                    byResource[key][invIndex] = new List<(Kind, Mode)>();
                byResource[key][invIndex].Add((kind, mode));
            }
            invIndex++;
        }

        foreach (var kv in byResource)
        {
            // 跨调用：收集"不同调用"各自的 kind 集合与 mode 集合（单调用内部多态不计入跨调用多样性）。
            var perInvocationKinds = kv.Value.Values.Select(site => site.Select(x => x.Kind).ToImmutableHashSet())
                .ToImmutableArray();
            var perInvocationModes = kv.Value.Values.Select(site => site.Select(x => x.Mode).ToImmutableHashSet())
                .ToImmutableArray();

            // 仅当参与跨调用的调用数 ≥ 2 时才考虑（排除单 API 内部多态的误报）。
            bool crossInvocation = kv.Value.Count >= 2;

            // A3 KIND_MIX：跨调用出现多类效应（read/write/occupy）混用（§3.1.4b DO-7）。
            if (crossInvocation)
            {
                var allKinds = perInvocationKinds.SelectMany(k => k).ToImmutableHashSet();
                if (allKinds.Count > 1)
                {
                    var kindList = string.Join(",", allKinds.Select(k => k.ToString()));
                    context.ReportDiagnostic(Diagnostic.Create(
                        KindMixOnSameResource,
                        method.Identifier.GetLocation(),
                        method.Identifier.Text,
                        resourceLabel[kv.Key],
                        kindList));
                }
            }

            // A4 Compat 冲突：枚举"不同调用"间的 mode 对，存在 Compatible==false ⇒ §3.2.3 CONFLICT 集（§14.3 A4）。
            if (crossInvocation)
            {
                bool conflict = false;
                string conflictPair = "";
                for (int i = 0; i < perInvocationModes.Length && !conflict; i++)
                    for (int j = 0; j < perInvocationModes.Length && !conflict; j++)
                    {
                        if (i == j) continue;
                        foreach (var a in perInvocationModes[i])
                            foreach (var b in perInvocationModes[j])
                            {
                                if (!Compatible.IsCompatible(a, b))
                                {
                                    conflict = true;
                                    conflictPair = $"{a}+{b}";
                                    break;
                                }
                            }
                    }
                if (conflict)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        CompatConflictOnSameResource,
                        method.Identifier.GetLocation(),
                        method.Identifier.Text,
                        resourceLabel[kv.Key],
                        conflictPair));
                }
            }
        }
    }

    // 调用 canonical 名 → §7 白名单条目（null 表示不在白名单，本近似不处理）。
    private static ApiMapping? FindWhitelistEntry(InvocationExpressionSyntax inv)
    {
        var canon = CanonicalOfInvocation(inv);
        foreach (var m in GodotApiWhitelist.All)
            if (Canonical(m.GodotApi) == canon)
                return m;
        return null;
    }

    // 调用的 canonical 键：成员访问 "Audio.Play" ⇒ "audioplay"；裸 "AddChild" ⇒ "addchild"。
    // 不区分接收者（node.QueueFree / this.QueueFree / 裸 QueueFree 同归 queuefree）。
    private static string CanonicalOfInvocation(InvocationExpressionSyntax inv) =>
        Canonical(RawName(inv));

    private static string RawName(InvocationExpressionSyntax inv)
    {
        return inv.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Expression.ToString() + "." + ma.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => inv.Expression.ToString()
        };
    }

    private static bool IsValidOverrideReason(AttributeSyntax attr, SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
        if (arg is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(OverrideReasonRequired, method.Identifier.GetLocation(), method.Identifier.Text));
            return false;
        }
        var cv = context.SemanticModel.GetConstantValue(arg.Expression);
        if (!cv.HasValue || cv.Value is not string s || string.IsNullOrWhiteSpace(s))
        {
            context.ReportDiagnostic(Diagnostic.Create(OverrideReasonRequired, method.Identifier.GetLocation(), method.Identifier.Text));
            return false;
        }
        return true;
    }

    private static bool IsValidAcceptEpsilon(AttributeSyntax attr, SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
        if (arg is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(AcceptDeviationRange, method.Identifier.GetLocation(), method.Identifier.Text));
            return false;
        }
        var cv = context.SemanticModel.GetConstantValue(arg.Expression);
        if (!cv.HasValue || cv.Value is not double e || e < 0.0 || e > 0.5)
        {
            context.ReportDiagnostic(Diagnostic.Create(AcceptDeviationRange, method.Identifier.GetLocation(), method.Identifier.Text));
            return false;
        }
        return true;
    }
    private static bool IsEffectOverride(string name) =>
        name == "EffectOverride" || name == "EffectOverrideAttribute";
    private static bool IsAcceptDeviation(string name) =>
        name == "AcceptDeviation" || name == "AcceptDeviationAttribute";
}
