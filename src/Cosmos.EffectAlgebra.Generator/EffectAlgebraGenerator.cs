using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cosmos.EffectAlgebra.Generator;

/// <summary>
/// §L2 / §14 — Godot 调用语法 → L1 代数 Signature 的「翻译层」(Roslyn source generator)。
/// 铁律：数学全在 L1（Cosmos.EffectAlgebra）；本生成器只做「识别标注方法 + 生成每方法 Signature 组合代码」，
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

        // ② 收集全部标注方法，按方法名判定是否重名（跨类同名在真实工程中常见）；
        //    重名时以「类型名+序号」消歧，避免 AddSource 同名 hint 被 Roslyn 丢弃（R1.8：否则整层生成静默丢失），
        //    且避免 emit 期同名 Compute{Method} 成员冲突。非重名保持裸 Compute{Method}（兼容既有调用契约）。
        var collected = methods.Collect();
        context.RegisterSourceOutput(collected, static (spc, all) =>
        {
            var dupCount = all.GroupBy(m => m.MethodName).ToDictionary(g => g.Key, g => g.Count());
            var seen = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var method in all)
            {
                var suffix = string.Empty;
                if (dupCount[method.MethodName] > 1)
                {
                    seen.TryGetValue(method.MethodName, out int idx);
                    suffix = $"_{method.TypeName}_{idx}";
                    seen[method.MethodName] = idx + 1;
                }
                var source = GenerateMethodSignature(method, suffix);
                // hint 名不允许含 '@'（转义关键字方法名如 @class）；仅 method.MethodName 可能带前导 '@'，先剥离再拼文件名。
                var saneName = method.MethodName.TrimStart('@');
                var hintBase = (method.TypeName + "_" + saneName + suffix);
                spc.AddSource($"{hintBase}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        });
    }

    /// <summary>§8.3 — 仅挑选带 [EffectOverride]/[AcceptDeviation] 的方法；其余忽略。
    /// §8.3.1 不变式(1)：[EffectOverride] reason 非空由 L1 <c>EffectOverrideAttribute</c> 构造子强制（fail-fast 抛），故编译期无需额外诊断（类型保障，非运行期可发点）。</summary>
    private static AnnotatedMethod? GetAnnotatedMethod(GeneratorSyntaxContext ctx)
    {
        var method = (MethodDeclarationSyntax)ctx.Node;
        bool hasOverride = false, hasAccept = false;
        foreach (var attr in method.AttributeLists.SelectMany(l => l.Attributes))
        {
            if (HasAttributeName(attr.Name, EffectOverrideName)) hasOverride = true;
            else if (HasAttributeName(attr.Name, AcceptDeviationName)) hasAccept = true;
        }
        // 取声明类型名（用于跨类同名消歧；嵌套/泛型类型取最近非方法声明名）。
        string typeName = "Global";
        for (var p = method.Parent; p is not null; p = p.Parent)
        {
            if (p is TypeDeclarationSyntax tds) { typeName = tds.Identifier.Text; break; }
        }
        return !hasOverride && !hasAccept
            ? null
            : new AnnotatedMethod(method.Identifier.Text, typeName, hasOverride, hasAccept);
    }

    /// <summary>按简单名匹配特性（含 `X` 与 `XAttribute` 两种写法）。等价于 Roslyn 常见 IsOrHasName 语义，本地实现以保证零依赖编译。</summary>
    private static bool HasAttributeName(NameSyntax name, string simpleName)
    {
        var text = name switch
        {
            IdentifierNameSyntax i => i.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            _ => null
        };
        return text == simpleName || text == simpleName + "Attribute";
    }

    /// <summary>§14 L2 — 生成「每标注方法 → 其效应 Signature 组合」代码。
    /// 生成代码真引用 L1：遍历 <c>GodotApiWhitelist.All</c>，按方法名规范化（去 `.`/`_` 小写）匹配白名单键，
    /// 用 <c>Signature.Union</c> 把匹配到的 <c>Claim</c> 组合进 <c>Compute{method}</c> 的返回签名。数学在 L1（§3.1–§3.3）。
    /// <param name="suffix">重名消歧后缀（非空仅当跨类同名，避免 hint/成员名冲突）；非重名为空串维持裸名契约。</param></summary>
    private static string GenerateMethodSignature(AnnotatedMethod m, string suffix)
    {
        // 转义关键字方法名（如 @class）的 Identifier.Text 含前导 '@'，直接拼接进生成代码会产出非法标识符
        // （Compute@class 被 Lexer 视为 Compute + @class 两段），且 AddSource 的 hint 名也不允许含 '@'。
        // 故生成成员名与白名单 canon 统一去掉前导 '@'（@class → class，Computeclass 合法）。
        var memberName = m.MethodName.TrimStart('@');
        var tags = (m.HasOverride ? "[EffectOverride] " : "") + (m.HasAccept ? "[AcceptDeviation] " : "");
        // 规范化方法名（单一真源：L1 GodotApiWhitelist.Canonical；memberName 已去前导 '@'，见上）。用于白名单键匹配（R2 #3 收敛）。
        var canon = GodotApiWhitelist.Canonical(memberName);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        // 生成代码引用 L1 中带可空标注的成员（如 GodotApiWhitelist 项的 string?）；消费工程可能关闭 #nullable，
        // 故显式禁用以避免 CS8632（TreatWarningsAsErrors 下会致 emit 失败，规模场景尤甚）。
        sb.AppendLine("#nullable disable");
        sb.AppendLine("// §L2 / §14 — EffectAlgebraGenerator 生成的「每方法效应 Signature 组合」代码（非桩）。");
        sb.AppendLine("// §L2 — 数学在 L1（Cosmos.EffectAlgebra）：本代码仅按方法名匹配 §7 白名单并 Union 出 Signature，");
        sb.AppendLine("// 不重算代数（net/Peak/Compatible 在运行期由 L1 完成，§3.3.1）。");
        sb.AppendLine("// L2 残差（honest）：方法体语义绑定靠「方法名↔白名单键」规范化匹配（§14 L2），");
        sb.AppendLine("// 真实调用级闭合由运行期 Σnet 判定（DO-9 近似见 L3）。");
        sb.AppendLine("public static partial class EffectAlgebraGenerated");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>§14 L2：方法 {m.MethodName}（{tags}）的效应签名 = baseSig ∪ §7 白名单中同名 API 的 Claims 组合（L1 数学在运行期 Σnet 权威）。</summary>");
        sb.AppendLine($"    public static global::Cosmos.EffectAlgebra.Signature Compute{memberName}{suffix}(global::Cosmos.EffectAlgebra.Signature baseSig)");
        sb.AppendLine($"        => global::Cosmos.EffectAlgebra.Signature.Union(baseSig, {memberName}{suffix}_Claims());");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>§7 / §14 L2 — 从 GodotApiWhitelist.All 取方法名（规范化后）匹配的 Claims，组合成该方法的 Signature（真委托 L1，非桩）。</summary>");
        sb.AppendLine($"    private static global::Cosmos.EffectAlgebra.Signature {memberName}{suffix}_Claims()");
        sb.AppendLine("    {");
        sb.AppendLine("        var s = global::Cosmos.EffectAlgebra.Signature.Empty;");
        sb.AppendLine("        foreach (var m in global::Cosmos.EffectAlgebra.GodotApiWhitelist.All)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (string.Equals(m.GodotApi.Replace(\".\", \"\").Replace(\"_\", \"\").ToLowerInvariant(), \"{canon}\", global::System.StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine("                foreach (var c in m.Claims) s = global::Cosmos.EffectAlgebra.Signature.Union(s, global::Cosmos.EffectAlgebra.Signature.Of(c));");
        sb.AppendLine("        }");
        sb.AppendLine("        return s;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed record AnnotatedMethod(string MethodName, string TypeName, bool HasOverride, bool HasAccept);
}
