using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using EffectLedger.Analyzer.Shared;

namespace EffectLedger.Analyzer;

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
///   真实闭合以运行期 <see cref="EffectLedger.NetTable.IsConserved"/> 为权威（§3.3.1）。
///
/// A3 / A4 误报护栏（迭代29 补）：§7 单条 API 的内部 Claim 可能天然跨 kind（如 AddChild 含 Write+Occupy）或
///   重复同模式（如 Load 含两次 Occupy(Mem,Create)）；这些**单条 API 内部**的多态是 PDR 白名单有意设计，非用户混用。
///   故 A3/A4 仅检测**跨调用（不同 InvocationExpression 站点）同归一资源**的 kind 多样性 / mode 冲突，不针对单条 API 内部。
///   数学已由 L1 类型保护（§3.1.4b 三桶隔离、§3.2.3 Compatible 全函数）；EAA0303/EAA0304 仅提示意图清晰度，非数学缺。
///
/// QED-C1b（2026-09-06）：白名单扩展真接线——Options.AdditionalFiles 中名为 effectledger.config.json 的文件经
///   L1 <see cref="EffectLedger.Analyzer.Shared.EffectLedgerConfig"/>（源副本）严格解析 + GodotApiWhitelist.MergedWith
///   合并，构建 per-compilation 查找表（静态字典退役）。配置错误（解析/schema/Canonical 碰撞）⇒ EAA0701
///   （该文件扩展整体弃用、基础白名单不受影响、绝不静默）；碰撞 ⇒ 合并集整体回退基础表（无部分生效）。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EffectAlgebraAnalyzer : DiagnosticAnalyzer
{
    // A2-10（生产审计批4）：语义化特性识别的 EffectLedger 归属 FQN（分析器自包含编译，不引用 L1 程序集，按字符串精确比对）。
    private const string EffectLedgerOverrideFqn = "EffectLedger.EffectOverrideAttribute";
    private const string EffectLedgerAcceptDeviationFqn = "EffectLedger.AcceptDeviationAttribute";

    // ── §3.3.1 DO-9 近似泄漏：方法内 acquire（create/occupy-create）无对应 release-class 且未标 [EffectOverride] ──
    // 诊断 id EAA0901（"09"=§3.3.1，"01"=DO-9 第 1 个静态近似规则）。
    private static readonly DiagnosticDescriptor MissingReleaseForAcquire = new(
        id: "EAA0901",
        title: "疑似资源泄漏（DO-9 静态近似）",
        messageFormat: "方法 '{0}' 调用了 acquire 类 API（{1}）但无对应 release-class 调用。注意：[EffectOverride] 不豁免本诊断（它仅豁免 A3/A4 意图提示；DO-9 静态泄漏近似永不抑制，防止全标 override 静默泄漏）。修复：(1) 在同一方法体内补 release-class 配对调用（QueueFree/RemoveChild/Disconnect 等），或 (2) 若配对在跨方法/跨对象（静态近似盲区），以运行期 Σnet 为权威判据并在 CI 基线中显式豁免本警告（附证据）。",
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

    // ── QED-C1b：effectledger.config.json 白名单扩展配置错误（§7 扩展通道的 loud 失败面）──
    // 诊断 id EAA0701（"07"=§7 白名单，"01"=扩展通道第 1 条规则；id 属公共契约面，2026-09-06 登记）。
    // 消费语义：配置错误 ⇒ 该文件扩展整体弃用（基础白名单不受影响），绝不静默忽略（静默无保护=假绿向量）。
    private static readonly DiagnosticDescriptor ConfigInvalid = new(
        id: "EAA0701",
        title: "effectledger.config.json 白名单扩展配置错误（该文件扩展未生效）",
        messageFormat: "effectledger.config.json 白名单扩展未生效：{0}。该文件的扩展映射已整体忽略，基础白名单不受影响；修复配置后重新编译。配置错误静默忽略 = 扩展 API 静默无保护，故 loud 报告（QED-C1b）。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "QED-C1b: the effectledger.config.json whitelist extension is consumed per-compilation via AdditionalFiles; parse/schema/canonical-collision errors invalidate that file's extensions entirely (loud), leaving the base whitelist unaffected.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(MissingReleaseForAcquire, KindMixOnSameResource, CompatConflictOnSameResource,
            OverrideReasonRequired, AcceptDeviationRange, ConfigInvalid);

    // §3.3.1 DO-9 / §14.3 A3 / §14.3 A4：以方法声明为分析单元（控制流近似：仅方法内调用可见性）。
    // §3.3.1 / §14.3 — 白名单键归一化单一真源（R2 #3）：统一走 L1 GodotApiWhitelist.Canonical，避免与 Generator 各写一份漂移。
    private static string Canonical(string name) => GodotApiWhitelist.Canonical(name);

    // QED-C1b：白名单扩展真接线——静态字典退役，改为 per-compilation 合并快照
    // （Options.AdditionalFiles 中的 effectledger.config.json ⇒ MergedWith 合并视图 ⇒ 本次编译的查找表）。
    // Initialize → OnCompilationStart：解析/合并失败（FormatException/InvalidOperationException/IOException）
    // ⇒ EAA0701（该文件扩展整体弃用，基础白名单不受影响）；碰撞 ⇒ 合并集整体回退基础表（无部分生效）。
    private static void OnCompilationStart(CompilationStartAnalysisContext ctx)
    {
        var merged = GodotApiWhitelist.All;
        var configDiags = new List<Diagnostic>();
        var extras = new List<ApiMapping>();
        string? firstConfigPath = null;

        foreach (var file in ctx.Options.AdditionalFiles)
        {
            if (!IsConfigFile(file.Path)) continue;
            firstConfigPath ??= file.Path;
            try
            {
                var text = file.GetText(ctx.CancellationToken)?.ToString();
                if (text is null)
                {
                    configDiags.Add(ConfigDiagnostic(file.Path, "文件不可读"));
                    continue;
                }
                var entries = EffectLedgerConfig.LoadExtraFromJson(text);
                if (!entries.IsDefaultOrEmpty) extras.AddRange(entries);
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or IOException)
            {
                configDiags.Add(ConfigDiagnostic(file.Path, ex.Message));
            }
        }

        if (extras.Count > 0)
        {
            try
            {
                merged = GodotApiWhitelist.MergedWith(extras.ToImmutableArray());
            }
            catch (InvalidOperationException ex)
            {
                // 碰撞（vs 基础表/跨文件）＝扩展集整体弃用（回基础表，无部分生效）；诊断定位首个配置文件，
                // 消息自含碰撞双方 API 名（MergedWith loud 语义透传，QED-C1a）。
                merged = GodotApiWhitelist.All;
                configDiags.Add(ConfigDiagnostic(firstConfigPath ?? "effectledger.config.json", ex.Message));
            }
        }

        var lookup = WhitelistLookup.Build(merged);
        ctx.RegisterSyntaxNodeAction(nodeCtx => AnalyzeMethod(nodeCtx, lookup), SyntaxKind.MethodDeclaration);

        if (configDiags.Count > 0)
            ctx.RegisterCompilationEndAction(endCtx =>
            {
                foreach (var d in configDiags) endCtx.ReportDiagnostic(d);
            });
    }

    // 配置文件契约名：仅认文件名 effectledger.config.json（大小写不敏感，跨 OS 稳定）；目录深度不限。
    // 【跨平台】Linux 上 \ 是合法文件名字符（非分隔符）——统一归一为 / 后再取文件名；
    // AdditionalFiles 的路径分隔符随宿主工程书写习惯（Win 工程 \、Linux 工程 /），两侧都要能匹配。
    private static bool IsConfigFile(string path) =>
        string.Equals(Path.GetFileName(path.Replace('\\', '/')), "effectledger.config.json", StringComparison.OrdinalIgnoreCase);

    private static Diagnostic ConfigDiagnostic(string path, string message) =>
        Diagnostic.Create(ConfigInvalid,
            Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(LinePosition.Zero, LinePosition.Zero)),
            message);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // §3.3.1 DO-9 / §14.3 A3 / §14.3 A4：以方法声明为分析单元（控制流近似：仅方法内调用可见性）。
        // 注册经 CompilationStart：per-compilation 合并白名单（AdditionalFiles 快照）被捕获进闭包。
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    // QED-C1b：per-compilation 白名单查找表（A2-07 预计算结构保留：canonical 键预计算、线程安全）。
    // merged 已由 GodotApiWhitelist.All（启动期 ValidateNoCollisions）+ MergedWith（合并集 Canonical 唯一）
    // 双重保证无同键 ⇒ ByFullCanon 用 ToImmutableDictionary 安全；ByMethodCanon 同键碰撞取首个（与旧一致）。
    private readonly struct WhitelistLookup
    {
        private readonly ImmutableDictionary<string, ApiMapping> _byFullCanon;
        private readonly ImmutableDictionary<string, ApiMapping> _byMethodCanon;

        private WhitelistLookup(ImmutableDictionary<string, ApiMapping> byFull, ImmutableDictionary<string, ApiMapping> byMethod)
        {
            _byFullCanon = byFull;
            _byMethodCanon = byMethod;
        }

        internal static WhitelistLookup Build(ImmutableArray<ApiMapping> whitelist) => new(
            whitelist.ToImmutableDictionary(m => GodotApiWhitelist.Canonical(m.GodotApi)),
            whitelist
                .GroupBy(m => GodotApiWhitelist.Canonical(m.GodotApi.Split('.').Last()))
                .ToImmutableDictionary(g => g.Key, g => g.First()));

        // A2-07：廉价语法预检——裸方法名/全名 canonical 不在任何白名单键集 ⇒ 不付 GetSymbolInfo 语义查询成本。
        // 绝大多数调用（ToString/LINQ/业务方法）与白名单无关，此项把分析时延从 O(调用点×语义查询) 压回 O(调用点×字符串)。
        internal bool MaybeWhitelisted(InvocationExpressionSyntax inv)
        {
            if (_byMethodCanon.ContainsKey(Canonical(MethodName(inv)))) return true;
            return _byFullCanon.ContainsKey(Canonical(RawName(inv)));
        }

        internal ApiMapping? FindWhitelistEntry(InvocationExpressionSyntax inv, SemanticModel model)
        {
            // P0-2：门控前置——裸标识符调用的 RawName 即方法名，若不先过 Godot 类型门，
            // 用户自有同名方法会经「全名」路径被定罪（hickey-x3 F3 的实际触发形态）。
            // 符号不可解析（无引用的裸语法编译）⇒ IsGodotTypedInvocation 返回 true，保留旧回退行为（召回优先）。
            if (!IsGodotTypedInvocation(inv, model)) return null;

            return _byFullCanon.TryGetValue(Canonical(RawName(inv)), out var fullMatch) ? fullMatch
                : _byMethodCanon.TryGetValue(Canonical(MethodName(inv)), out var methodMatch) ? methodMatch
                : null;
        }
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, WhitelistLookup lookup)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        bool hasValidOverride = false;
        foreach (var attr in method.AttributeLists.SelectMany(l => l.Attributes))
        {
            // A2-10（生产审计批4）：语义化特性识别（此前纯字符串末段匹配——用户自有同名 MyLib.EffectOverride
            // 可冒充逃逸通道豁免 A3/A4，反向误触发 EAA0801）。语义可解析 ⇒ 按特性类型完全限定名精确比对 EffectLedger 归属，
            // 非 EffectLedger 特性一律不参与；语义不可解析（无 L1 引用的裸语法编译）⇒ 回退名称末段匹配（召回优先）。
            var attrType = context.SemanticModel.GetTypeInfo(attr.Name).Type;
            if (attrType is not null && attrType.TypeKind != TypeKind.Error)
            {
                var fqn = attrType.OriginalDefinition.ToDisplayString();
                if (fqn == EffectLedgerOverrideFqn)
                {
                    if (IsValidOverrideReason(attr, context, method)) hasValidOverride = true;
                }
                else if (fqn == EffectLedgerAcceptDeviationFqn)
                {
                    IsValidAcceptEpsilon(attr, context, method); // 仅校验并报告 EAA0802，不豁免
                }
                continue;
            }
            var name = attr.Name.ToString();
            if (IsEffectOverride(name))
            {
                if (IsValidOverrideReason(attr, context, method)) hasValidOverride = true;
            }
            else if (IsAcceptDeviation(name))
            {
                IsValidAcceptEpsilon(attr, context, method); // 仅校验并报告 EAA0802，不豁免
            }
        }

        // 收集方法体内所有调用表达式（控制流近似：不展开被调用方法内部；
        // 仅语法可见 → 跨方法/跨对象释放配对不在覆盖内，可能静默漏报，见类注释 OPEN-2）。
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();

        // EAA0901：永不豁免（§8.3.1(3)；R2 对抗修复——全标 override 即零报警 = 静默泄漏通道，不得重开）。
        // P0-1（hickey-x3 F2）按「改文档」方向对齐：诊断消息与 README 已改为如实说明 [EffectOverride] 仅豁免 A3/A4。
        AnalyzeMissingRelease(context, method, invocations, lookup);
        if (!hasValidOverride) AnalyzeKindMixAndCompat(context, method, invocations, lookup);  // EAA0303/4：仅合法逃逸豁免意图提示
    }

    // ── §3.3.1 DO-9 近似：按归一资源聚合「acquire>release」⇒ 疑似泄漏（运行期 net 为权威，见类注释）──
    // R8 对抗审计改进：逐资源计数，可捕获「跨资源错配释放」「部分释放（acquire 多于 release）」，
    // 而非旧版仅全局布尔（会漏报跨类型/部分释放）。§8.1 release-class-only 泛型释放（如 free）作兜底，避免误报。
    private static void AnalyzeMissingRelease(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method,
        InvocationExpressionSyntax[] invocations, WhitelistLookup lookup)
    {
        var acquire = new Dictionary<ResourceId, int>();
        var release = new Dictionary<ResourceId, int>();
        var firstAcquireName = new Dictionary<ResourceId, string>();

        foreach (var inv in invocations)
        {
            if (!lookup.MaybeWhitelisted(inv)) continue; // A2-07：语法预检未命中 ⇒ 跳过语义查询
            var canon = Canonical(RawName(inv));
            var m = lookup.FindWhitelistEntry(inv, context.SemanticModel);
            if (m is null)
            {
                // §8.1 release-class 但不在 §7 白名单：无对应资源映射，本近似不处理（运行期 net 为权威）。
                // 不以「方法体内出现过任一 release-class-only 调用」整方法 blanket 抑制 EAA0901（§8.3.1(3) 永不抑制；
                // 否则 RemoveFromGroup(仅释放组隶属)+AddChild(占用 Tree 未释放) 这类无关资源真实泄漏被漏报，BUG D）。
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

        // §3.3.1 DO-9 近似：存在归一资源 net 获取（acquire>release）⇒ 报告（fail-open，§8.3.1(3) 永不抑制）。
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
        InvocationExpressionSyntax[] invocations, WhitelistLookup lookup)
    {
        // 归一资源 → (调用序号 → 该调用的 (kind,mode) 集合)
        var byResource = new Dictionary<ResourceId, Dictionary<int, List<(Kind Kind, Mode Mode)>>>();
        // R3-CG-02（三轮审计）：调用序号 → 白名单 API 名（A3 按 API 归并站点用）
        var apiBySite = new Dictionary<int, string>();
        var resourceLabel = new Dictionary<ResourceId, string>();

        int invIndex = 0;
        foreach (var inv in invocations)
        {
            if (!lookup.MaybeWhitelisted(inv)) { invIndex++; continue; } // A2-07：语法预检未命中 ⇒ 跳过语义查询
            var m = lookup.FindWhitelistEntry(inv, context.SemanticModel);
            if (m is null) { invIndex++; continue; }
            apiBySite[invIndex] = m.Value.GodotApi; // R3-CG-02

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
            // P0-3（hickey-x3 F1）：白名单已知 acquire/release 配对（如 AddChild→QueueFree）跨调用的 kind 差异
            // 来自 Release-mode claim（配对的另一半），属推荐模式而非混用 ⇒ A3 豁免之。
            // A4 冲突检测不受影响（Create×Create 等冲突与 Release 端无关）。
            // R3-CG-02（三轮审计）：A3 站点按【API】归并——同一白名单 API 重复调用（如 AddChild×2）的
            // 多 kind 是白名单内部多态（类注释明言"非用户混用"），按调用站点归并会把它误报 KIND_MIX；
            // 仅当 ≥2 条不同 API 在同资源上贡献非 Release claim 且 kind 多样才报（真量纲混用仍报）。
            var nonReleaseKindsByApi = new Dictionary<string, ImmutableHashSet<Kind>>();
            foreach (var (siteIdx, site) in kv.Value)
            {
                if (!apiBySite.TryGetValue(siteIdx, out var api)) continue;
                var kinds = site.Where(x => x.Mode != Mode.Release).Select(x => x.Kind).ToImmutableHashSet();
                if (kinds.IsEmpty) continue;
                nonReleaseKindsByApi[api] = nonReleaseKindsByApi.TryGetValue(api, out var merged)
                    ? merged.Union(kinds) : kinds;
            }

            // A3 KIND_MIX：跨【不同 API】出现多类效应（read/write/occupy）混用（§3.1.4b DO-7）。
            if (nonReleaseKindsByApi.Count >= 2)
            {
                var allKinds = nonReleaseKindsByApi.Values.SelectMany(k => k).ToImmutableHashSet();
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
            // 跨调用判定沿用全量站点（含 Release 端）：release×release 同为 CONFLICT，不得因 P0-3 豁免漏报。
            var perInvocationModes = kv.Value.Values.Select(site => site.Select(x => x.Mode).ToImmutableHashSet())
                .ToImmutableArray();
            bool crossInvocation = kv.Value.Count >= 2;
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

    // 调用 canonical 名 → §7 白名单条目的匹配语义（Godot 类型门 / 全名-方法名双键）已并入 WhitelistLookup。
    // P0-2（hickey-x3 F3）：方法名回退匹配要求接收者类型可判定为 Godot 类型——
    // 用户自有 Load()/Connect() 等撞名方法不再被裸名定罪。A2-09（生产审计批4）：namespace 精确匹配
    // "Godot" 或 "Godot."（前缀命名空间不误放行）；符号不可解析保留旧回退（召回优先）。

    // P0-2 — 接收者类型是否可判定为 Godot 类型。
    // A2-09（生产审计批4）：精确匹配 namespace "Godot" 或 "Godot.*"——此前 StartsWith("Godot") 会放行用户自有的
    // GodotShapes/GodotTesting.Utils 等前缀命名空间，其撞名方法（Load/Connect）被误定罪（error 级假红）。
    private static bool IsGodotTypedInvocation(InvocationExpressionSyntax inv, SemanticModel model)
    {
        var info = model.GetSymbolInfo(inv);
        var sym = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (sym?.ContainingType is null) return true; // 无法解析 ⇒ 保守保留旧回退（无引用编译场景）
        var ns = sym.ContainingType.ContainingNamespace.ToDisplayString();
        return ns == "Godot" || ns.StartsWith("Godot.", StringComparison.Ordinal);
    }

    // 调用的全名 canonical 键：成员访问 "Audio.Play" ⇒ "audioplay"；裸 "AddChild" ⇒ "addchild"。
    private static string RawName(InvocationExpressionSyntax inv)
    {
        return inv.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Expression.ToString() + "." + ma.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => inv.Expression.ToString()
        };
    }

    // 方法名（去接收者 / 泛型参数）：MemberAccess "node.QueueFree" ⇒ "QueueFree"；裸 "AddChild" ⇒ "AddChild"。
    // R6 修复：以方法名二次匹配白名单键，使 Godot 主流「接收者限定调用」不再漏报（Audio.Play 仍走全名匹配）。
    private static string MethodName(InvocationExpressionSyntax inv) =>
        inv.Expression switch
        {
            MemberAccessExpressionSyntax ma => NameText(ma.Name),
            GenericNameSyntax g => g.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => inv.Expression.ToString()
        };

    private static string NameText(SimpleNameSyntax name) =>
        name switch
        {
            GenericNameSyntax g => g.Identifier.Text,
            IdentifierNameSyntax i => i.Identifier.Text,
            _ => name.ToString()
        };

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
        // BUG B 修复：Roslyn 对整数字面量 0 的 GetConstantValue 返回 boxed int（非 double），
        // 须按 int/float/double 灵活拆箱，否则 [AcceptDeviation(0)] 被误报 EAA0802。
        var cv = context.SemanticModel.GetConstantValue(arg.Expression);
        double? e = cv.HasValue ? cv.Value switch
        {
            double d => d,
            int i => i,
            float f => f,
            _ => null
        } : null;
        if (e is null || double.IsNaN(e.Value) || e.Value < 0.0 || e.Value > 0.5)
        {
            // A2-03（生产审计批4）：NaN 是唯一同时骗过 e<0 与 e>0.5 的 double 常量——双层穿透使 ε 失去全部约束。
            context.ReportDiagnostic(Diagnostic.Create(AcceptDeviationRange, method.Identifier.GetLocation(), method.Identifier.Text));
            return false;
        }
        return true;
    }
    // BUG A 修复：特性名末段匹配（忽略 global:: / 命名空间限定），同时兼容 X 与 XAttribute 两种写法。
    // 旧实现逐字比对 name=="EffectOverride"，导致 [EffectLedger.EffectOverride] /
    // [global::EffectLedger.EffectOverride] 被漏识 ⇒ A3/A4 误报 + 逃逸通道失效。
    private static bool IsEffectOverride(string name) =>
        SimpleAttrName(name) is "EffectOverride" or "EffectOverrideAttribute";
    private static bool IsAcceptDeviation(string name) =>
        SimpleAttrName(name) is "AcceptDeviation" or "AcceptDeviationAttribute";
    private static string SimpleAttrName(string name) =>
        name.Replace("global::", "", StringComparison.Ordinal).Split('.').Last();
}
