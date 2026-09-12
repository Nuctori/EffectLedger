using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using EffectLedger.Generator.Shared; // A2-06：L1 自包含副本（命名空间改写），切断对 L1 程序集的运行期引用

namespace EffectLedger.Generator;

/// <summary>
/// §L2 / §14 — Godot 调用语法 → L1 代数 Signature 的「翻译层」(Roslyn source generator)。
/// 铁律：数学全在 L1（EffectLedger）；本生成器只做「识别标注方法 + 生成每方法 Signature 组合代码」，
/// 绝不重算代数：生成代码**真委托 L1**（`GodotApiWhitelist` + `Signature.Union`/`Of`），在运行期把 §7 白名单 Claims
/// 组合成该方法的效应签名，供运行期 Σnet / Peak / Compatible（§3.3.1）与 Analyzer 消费。
/// L2 残差（honest，非假绿）：§7 白名单的实例 Claim 在编译期已可静态枚举（白名单本身是 L1 强类型数据），
/// 故生成代码直接按方法名规范化匹配白名单键并 Union；但「方法体实际调用了哪些 Godot API」的语义绑定，
/// 仍由运行期/Analyzer 通过方法名↔白名单键（规范化去 `.`/`_` 后小写）完成（见 §14 L2 / LANDING_PLAN §4）。
/// 类型边界由 L1 强制；生成器只搬运语法位置信息，不承载未定义边界。
/// </summary>
[Generator]
public sealed class EffectAlgebraGenerator : IIncrementalGenerator
{
    private const string EffectOverrideName = "EffectOverride";
    private const string AcceptDeviationName = "AcceptDeviation";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ① 识别标注：收集带 [EffectOverride] / [AcceptDeviation] 的 MethodDeclarationSyntax（含声明类型名用于消歧）。
        var methods = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                static (ctx, _) => GetAnnotatedMethod(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // ② 【QED-C1c】effectledger.config.json（AdditionalFiles）⇒ 白名单扩展：基础表命中的方法维持原
        //    运行期枚举 body；扩展-only 方法 emit 字面量 Claims（配置解析限契约面 6 资源 × 4 scope，
        //    全 public 类型，消费方可构造）。缓存键含配置文本（文本变更 ⇒ 管线重跑）。
        var configTexts = context.AdditionalTextsProvider
            .Where(static t => System.IO.Path.GetFileName(t.Path).Equals("effectledger.config.json", StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => t.GetText(ct)?.ToString() ?? string.Empty)
            .Collect();

        // ③ 收集全部标注方法，按方法名判定是否重名（跨类同名在真实工程中常见）；
        //    重名时以「类型名+序号」消歧，避免 AddSource 同名 hint 被 Roslyn 丢弃（R1.8：否则整层生成静默丢失），
        //    且避免 emit 期同名 Compute{Method} 成员冲突。非重名保持裸 Compute{Method}（兼容既有调用契约）。
        var collected = methods.Collect().Combine(configTexts);
        context.RegisterSourceOutput(collected, static (spc, pair) =>
        {
            var (all, configs) = pair;
            var extras = LoadExtras(configs, spc);
            // A2-05（生产审计批4）：消歧键 = (方法名, 完全限定类型名)——跨命名空间同名类型各得唯一后缀，
            // 同一 partial 类不再产出重复成员（CS0111），AddSource hint 不再互相覆盖被 Roslyn 丢弃（R1.8）。
            var dupCount = all.GroupBy(m => m.MethodName).ToDictionary(g => g.Key, g => g.Count());
            var seen = new System.Collections.Generic.Dictionary<(string MethodName, string FullType), int>();
            foreach (var method in all)
            {
                var suffix = string.Empty;
                if (dupCount[method.MethodName] > 1)
                {
                    var key = (method.MethodName, method.TypeName);
                    seen.TryGetValue(key, out int idx);
                    suffix = $"_{method.TypeName}_{idx}";
                    seen[key] = idx + 1;
                }
                var source = GenerateMethodSignature(method, suffix, extras);
                // hint 名不允许含 '@'（转义关键字方法名如 @class）；仅 method.MethodName 可能带前导 '@'，先剥离再拼文件名。
                var saneName = method.MethodName.TrimStart('@');
                var hintBase = (method.TypeName + "_" + saneName + suffix);
                spc.AddSource($"{hintBase}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        });
    }

    // ── QED-C1c：配置解析与合并（语义与 C1b L3 侧同契约）：
    //    每个 effectledger.config.json 独立解析/合并；解析失败（FormatException）或 Canonical 碰撞
    //    （MergedWith loud，含 vs 基础表/扩展彼此）⇒ EAA0701 且该文件扩展整体弃用（基础白名单
    //    不受影响，无部分生效），绝不静默。空文本跳过（无文件/空文件 = 纯基础表）。
    private static readonly DiagnosticDescriptor ConfigInvalid = new(
        id: "EAA0701",
        title: "effectledger.config.json 白名单扩展配置错误（该文件扩展未生效）",
        messageFormat: "effectledger.config.json 白名单扩展未生效：{0}。该文件的扩展映射已整体忽略，基础白名单不受影响；修复配置后重新编译。配置错误静默忽略 = 扩展 API 静默无保护，故 loud 报告（QED-C1c，与 L3 侧 QED-C1b 同契约 ID：两工具对同一配置错误各报一次，一致性优先）。",
        category: "EffectAlgebra",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "QED-C1c: the generator consumes effectledger.config.json via AdditionalFiles; parse/schema/canonical-collision errors invalidate that file's extensions entirely (loud), leaving the base whitelist unaffected.");

    private static ImmutableDictionary<string, EffectLedger.Generator.Shared.ApiMapping> LoadExtras(ImmutableArray<string> configs, SourceProductionContext spc)
    {
        // 命名空间警示：本文件处于 EffectLedger.Generator，其父级 EffectLedger（真实 L1，
        // 经 ProjectReference 可见）在名称解析上优先于 using 导入——L1 来源类型必须以
        // EffectLedger.Generator.Shared. 前缀完全限定，否则静默绑定到真实 L1 类型（无 internal
        // MergedWith / 模式匹配永不命中 Shared 实例）。与既有代码的完全限定约定一致。
        var builder = ImmutableDictionary.CreateBuilder<string, EffectLedger.Generator.Shared.ApiMapping>(StringComparer.Ordinal);
        foreach (var text in configs)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            try
            {
                var extra = EffectLedger.Generator.Shared.EffectLedgerConfig.LoadExtraFromJson(text);
                EffectLedger.Generator.Shared.GodotApiWhitelist.MergedWith(extra); // vs 基础表碰撞复核
                // 【QED-P5.2 己 HIGH 修复】跨文件同键 ⇒ 整文件弃用 + EAA0701（loud，无部分生效）——
                // 修复前 builder[c] = m 静默 last-win，两文件拼写照会拿错 claims（L2/L3 分叉）。
                var filePairs = new List<(string Canon, EffectLedger.Generator.Shared.ApiMapping Mapping)>();
                var fileKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var m in extra)
                {
                    var c = EffectLedger.Generator.Shared.GodotApiWhitelist.Canonical(m.GodotApi);
                    if (!fileKeys.Add(c))
                        throw new FormatException($"文件内重复 Canonical：'{c}'（扩展同键，QED-P5.2 己 loud）");
                    if (builder.ContainsKey(c))
                        throw new InvalidOperationException($"跨文件 Canonical 碰撞：'{c}' 已被先前配置文件扩展（QED-P5.2 己 loud）");
                    filePairs.Add((c, m));
                }
                foreach (var (c, m) in filePairs)
                    builder[c] = m;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                spc.ReportDiagnostic(Diagnostic.Create(ConfigInvalid, Location.None, ex.Message));
            }
        }
        return builder.ToImmutable();
    }

    /// <summary>§8.3 — 仅挑选带 [EffectOverride]/[AcceptDeviation] 的方法；其余忽略。
    /// §8.3.1 不变式(1)：[EffectOverride] reason 非空由 L1 <c>EffectOverrideAttribute</c> 构造子强制（fail-fast 抛），故编译期无需额外诊断（类型保障，非运行期可发点）。
    /// A2-04（生产审计批4）：特性名匹配补 AliasQualifiedNameSyntax（global:: 全限定形态）——此前落入 _ => null
    /// 静默不生成，与 L3（认 global::）行为分叉。A2-10 对称：语义可解析时按 FQN 精确比对 EffectLedger 归属，防同名冒充。</summary>
    private static AnnotatedMethod? GetAnnotatedMethod(GeneratorSyntaxContext ctx)
    {
        var method = (MethodDeclarationSyntax)ctx.Node;
        bool hasOverride = false, hasAccept = false;
        foreach (var attr in method.AttributeLists.SelectMany(l => l.Attributes))
        {
            var attrType = ctx.SemanticModel.GetTypeInfo(attr.Name).Type;
            if (attrType is not null && attrType.TypeKind != TypeKind.Error)
            {
                // 语义路径：精确 FQN（与 L3 A2-10 同契约）；R2A-06：error type（解析失败）不算可解析，须落名称回退
                var fqn = attrType.OriginalDefinition.ToDisplayString();
                if (fqn == EffectLedgerOverrideFqn) hasOverride = true;
                else if (fqn == EffectLedgerAcceptDeviationFqn) hasAccept = true;
                continue;
            }
            // 语义不可解析 ⇒ 名称末段回退（召回优先）
            if (HasAttributeName(attr.Name, EffectOverrideName)) hasOverride = true;
            else if (HasAttributeName(attr.Name, AcceptDeviationName)) hasAccept = true;
        }
        // 取完全限定类型名（A2-05，生产审计批4）：命名空间链 + 外层类型链，'.' 折叠为 '_' 保证生成成员名合法。
        // 此前仅取最近类型名——NS1.Cfg 与 NS2.Cfg 同名方法都得到 _Cfg_0 ⇒ CS0111 / AddSource hint 被丢。
        string fullType = "Global";
        {
            var parts = new List<string>();
            for (var p = method.Parent; p is not null; p = p.Parent)
            {
                if (p is TypeDeclarationSyntax tds) parts.Insert(0, tds.Identifier.Text);
                else if (p is NamespaceDeclarationSyntax nsd) parts.Insert(0, SanitizeTypePart(nsd.Name.ToString()));
                else if (p is FileScopedNamespaceDeclarationSyntax fsd) parts.Insert(0, SanitizeTypePart(fsd.Name.ToString()));
            }
            if (parts.Count > 0) fullType = string.Join("_", parts);
        }
        return !hasOverride && !hasAccept
            ? null
            : new AnnotatedMethod(method.Identifier.Text, fullType, hasOverride, hasAccept);
    }

    private static string SanitizeTypePart(string s) =>
        s.Replace("global::", "").Replace('.', '_');

    private const string EffectLedgerOverrideFqn = "EffectLedger.EffectOverrideAttribute";
    private const string EffectLedgerAcceptDeviationFqn = "EffectLedger.AcceptDeviationAttribute";

    /// <summary>按简单名匹配特性（含 `X` 与 `XAttribute` 两种写法）。等价于 Roslyn 常见 IsOrHasName 语义，本地实现以保证零依赖编译。
    /// A2-04（生产审计批4）：补 AliasQualifiedNameSyntax——[global::EffectLedger.EffectOverride] 此前落入
    /// _ => null 静默不生成（与 L3 行为分叉，方法被校验/豁免却没有 L2 产物）。</summary>
    private static bool HasAttributeName(NameSyntax name, string simpleName)
    {
        var text = name switch
        {
            IdentifierNameSyntax i => i.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            AliasQualifiedNameSyntax a => a.Name is IdentifierNameSyntax i2 ? i2.Identifier.Text : null,
            _ => null
        };
        return text == simpleName || text == simpleName + "Attribute";
    }

    /// <summary>§14 L2 — 生成「每标注方法 → 其效应 Signature 组合」代码。
    /// 生成代码真引用 L1：遍历 <c>GodotApiWhitelist.All</c>，按方法名规范化（去 `.`/`_` 小写）匹配白名单键，
    /// 用 <c>Signature.Union</c> 把匹配到的 <c>Claim</c> 组合进 <c>Compute{method}</c> 的返回签名。数学在 L1（§3.1–§3.3）。
    /// 【QED-C1c】effectledger.config.json 扩展-only 方法（基础表无此键）⇒ emit 字面量 Claims（配置解析限
    /// 契约面 6 资源 × 4 scope，全 public 类型消费方可构造）；基础表命中方法维持运行期枚举 body（不变）。
    /// <param name="suffix">重名消歧后缀（非空仅当跨类同名，避免 hint/成员名冲突）；非重名为空串维持裸名契约。</param>
    /// <param name="extras">配置扩展映射（canonical → ApiMapping）；空字典 = 无扩展（行为与旧版一致）。</param></summary>
    private static string GenerateMethodSignature(AnnotatedMethod m, string suffix,
        ImmutableDictionary<string, EffectLedger.Generator.Shared.ApiMapping> extras)
    {
        // 转义关键字方法名（如 @class）的 Identifier.Text 含前导 '@'，直接拼接进生成代码会产出非法标识符
        // （Compute@class 被 Lexer 视为 Compute + @class 两段），且 AddSource 的 hint 名也不允许含 '@'。
        // 故生成成员名与白名单 canon 统一去掉前导 '@'（@class → class，Computeclass 合法）。
        var memberName = m.MethodName.TrimStart('@');
        var tags = (m.HasOverride ? "[EffectOverride] " : "") + (m.HasAccept ? "[AcceptDeviation] " : "");
        // 规范化方法名（单一真源：L1 GodotApiWhitelist.Canonical；memberName 已去前导 '@'，见上）。用于白名单键匹配（R2 #3 收敛）。
        // R2B-01：Shared 副本完全限定——本工程虽编译期引用 L1（为 pack 流转依赖），但绝不使用其类型，
        // 生成器 PE 不携带 L1.dll 引用 ⇒ 隔离 ALC 可加载（A2-06 自包含）。
        var canon = EffectLedger.Generator.Shared.GodotApiWhitelist.Canonical(memberName);
        // 【QED-C1c】扩展-only 分支：基础表无此 canonical 且配置含此键 ⇒ 字面量 Claims body
        //（消费方运行期的 All 不含扩展项，运行期枚举永远空转 = 静默无保护，故必须字面量内嵌）。
        var inBase = EffectLedger.Generator.Shared.GodotApiWhitelist.All
            .Any(b => string.Equals(EffectLedger.Generator.Shared.GodotApiWhitelist.Canonical(b.GodotApi), canon, StringComparison.OrdinalIgnoreCase));
        extras.TryGetValue(canon, out var extraMapping);
        var hasExtra = extras.ContainsKey(canon);
        var isExtrasOnly = !inBase && hasExtra;
        var claimsBody = isExtrasOnly
            ? string.Join("\n", extraMapping.Claims.Select(c =>
                $"        s = global::EffectLedger.Signature.Union(s, global::EffectLedger.Signature.Of({RenderClaim(c)}));"))
            : "        foreach (var m in global::EffectLedger.GodotApiWhitelist.All)\n" +
              "        {\n" +
             $"            if (string.Equals(global::EffectLedger.GodotApiWhitelist.Canonical(m.GodotApi), \"{canon}\", global::System.StringComparison.OrdinalIgnoreCase))\n" +
              "                foreach (var c in m.Claims) s = global::EffectLedger.Signature.Union(s, global::EffectLedger.Signature.Of(c));\n" +
              "        }";
        var claimsDoc = isExtrasOnly
            ? $"    /// <summary>§7 / §14 L2 — effectledger.config.json 扩展映射（canonical={canon}）的 Claims 字面量组合（QED-C1c：扩展项不在消费方运行期 All 中，故编译期字面量内嵌；契约面类型全 public）。</summary>"
            : "    /// <summary>§7 / §14 L2 — 从 GodotApiWhitelist.All 取方法名（规范化后）匹配的 Claims，组合成该方法的 Signature（真委托 L1，非桩）。</summary>";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        // 生成代码引用 L1 中带可空标注的成员（如 GodotApiWhitelist 项的 string?）；消费工程可能关闭 #nullable，
        // 故显式禁用以避免 CS8632（TreatWarningsAsErrors 下会致 emit 失败，规模场景尤甚）。
        sb.AppendLine("#nullable disable");
        sb.AppendLine("// §L2 / §14 — EffectAlgebraGenerator 生成的「每方法效应 Signature 组合」代码（非桩）。");
        sb.AppendLine("// §L2 — 数学在 L1（EffectLedger）：本代码仅按方法名匹配 §7 白名单并 Union 出 Signature，");
        sb.AppendLine("// 不重算代数（net/Peak/Compatible 在运行期由 L1 完成，§3.3.1）。");
        sb.AppendLine("// L2 残差（honest）：方法体语义绑定靠「方法名↔白名单键」规范化匹配（§14 L2），");
        sb.AppendLine("// 真实调用级闭合由运行期 Σnet 判定（DO-9 近似见 L3）。");
        // A2-14（生产审计批4）：生成类移入专属命名空间——全局命名空间形态会与消费者自有同名类 CS0101。
        // 消费方经反射按 Name 查找的既有用法不受影响（Name 与 namespace 无关）。
        sb.AppendLine("namespace EffectLedger.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    public static partial class EffectAlgebraGenerated");
        sb.AppendLine("    {");
        sb.AppendLine($"    /// <summary>§14 L2：方法 {m.MethodName}（{tags}）的效应签名 = baseSig ∪ §7 白名单中同名 API 的 Claims 组合（L1 数学在运行期 Σnet 权威）。</summary>");
        sb.AppendLine($"    public static global::EffectLedger.Signature Compute{memberName}{suffix}(global::EffectLedger.Signature baseSig)");
        sb.AppendLine($"        => global::EffectLedger.Signature.Union(baseSig, {memberName}{suffix}_Claims());");
        sb.AppendLine();
        sb.AppendLine(claimsDoc);
        sb.AppendLine($"    private static global::EffectLedger.Signature {memberName}{suffix}_Claims()");
        sb.AppendLine("    {");
        sb.AppendLine("        var s = global::EffectLedger.Signature.Empty;");
        sb.AppendLine(claimsBody);
        sb.AppendLine("        return s;");
        sb.AppendLine("    }");
        sb.AppendLine("    }"); // class（A2-14：随命名空间包裹新增层级）
        sb.AppendLine("}");     // namespace
        return sb.ToString();
    }

    // ── QED-C1c：扩展 Claims 的字面量渲染（配置解析已限契约面 6 资源 × 4 scope——全部 public 类型，
    //    消费方可构造；防御性 default 分支兜底非契约面形状，loud 抛而非 emit 坏代码）。 ──
    private static string RenderClaim(EffectLedger.Generator.Shared.Claim c)
    {
        var kind = $"global::EffectLedger.Kind.{c.Kind}";
        var mode = $"global::EffectLedger.Mode.{c.Mode}";
        var res = c.Resource switch
        {
            EffectLedger.Generator.Shared.ResourceId.Gpu g => $"new global::EffectLedger.ResourceId.Gpu(new global::EffectLedger.Rid({Lit(g.BufferId.Value)}))",
            EffectLedger.Generator.Shared.ResourceId.Memory m => $"new global::EffectLedger.ResourceId.Memory({m.Uid}UL)",
            EffectLedger.Generator.Shared.ResourceId.CommandBuffer cb => $"new global::EffectLedger.ResourceId.CommandBuffer({Lit(cb.Channel)})",
            EffectLedger.Generator.Shared.ResourceId.SignalBus sb => $"new global::EffectLedger.ResourceId.SignalBus(new global::EffectLedger.StringName({Lit(sb.Name.Value)}))",
            EffectLedger.Generator.Shared.ResourceId.Occupancy o => $"new global::EffectLedger.ResourceId.Occupancy({Lit(o.Channel)})",
            EffectLedger.Generator.Shared.ResourceId.Custom cu => $"new global::EffectLedger.ResourceId.Custom({Lit(cu.Name)})",
            _ => throw new InvalidOperationException($"QED-C1c：effectledger.config.json 产生非契约面资源 {c.Resource.GetType().Name}（解析器已限契约面，此为防御分支）")
        };
        var scope = c.Scope switch
        {
            EffectLedger.Generator.Shared.ScopeId.Scene s => $"new global::EffectLedger.ScopeId.Scene({Lit(s.Name)})",
            EffectLedger.Generator.Shared.ScopeId.Method m => $"new global::EffectLedger.ScopeId.Method({Lit(m.Name)})",
            EffectLedger.Generator.Shared.ScopeId.Type t => $"new global::EffectLedger.ScopeId.Type({Lit(t.Name)})",
            EffectLedger.Generator.Shared.ScopeId.Global => "new global::EffectLedger.ScopeId.Global()",
            _ => throw new InvalidOperationException($"QED-C1c：effectledger.config.json 产生非契约面 scope {c.Scope.GetType().Name}（解析器已限契约面，此为防御分支）")
        };
        var size = c.Size is null ? "null" : RenderInterval(c.Size.Value);
        return $"new global::EffectLedger.Claim({kind}, {res}, {mode}, {scope}, {size}).Normalize()";
    }

    private static string RenderInterval(EffectLedger.Generator.Shared.Interval sz) =>
        $"new global::EffectLedger.Interval({RenderNat(sz.Lo)}, {RenderNat(sz.Hi)})";

    private static string RenderNat(EffectLedger.Generator.Shared.NatStar n) =>
        n.IsTop ? "global::EffectLedger.NatStar.Top" : $"global::EffectLedger.NatStar.Of({n.Value}UL)";

    private static string Lit(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

    private sealed record AnnotatedMethod(string MethodName, string TypeName, bool HasOverride, bool HasAccept);
}
