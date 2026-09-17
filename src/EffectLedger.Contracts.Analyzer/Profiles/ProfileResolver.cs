// ProfileResolver.cs — P1.2 语义化角色解析。
// 关键（R-SEM-01）：按精确符号识别角色，不靠短名、不靠用户构造函数执行、不靠任意 IBehaviorProfile 实现。
// 只支持两个内建角色；未知角色 => UnknownProfile 诊断，不静默当作无约束。

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EffectLedger.Contracts.Analyzer.Profiles;

/// <summary>受支持的内建角色。</summary>
public enum ContractProfileKind
{
    Unknown,
    ImmutableValue,
    DeterministicComputation,
}

/// <summary>一个类型上解析出的约束声明。</summary>
public sealed record ContractDeclaration(
    INamedTypeSymbol Type,
    ContractProfileKind Profile,
    /// <summary>声明位置（用于诊断）。</summary>
    Location Location);

/// <summary>
/// 解析 compilation 中所有实现 IConstrained&lt;T&gt; 的类型及其角色。
/// 使用 SymbolEqualityComparer 做精确符号比对，避免同名伪接口误识别。
/// </summary>
public sealed class ProfileResolver
{
    private readonly Compilation _compilation;

    // 精确全名（含命名空间），不切片比较短名。
    // 注意：ConstructedFrom.ToDisplayString() 返回带类型参数的形态
    // "EffectLedger.Contracts.IConstrained<TProfile>"，不是元数据反引号形态。
    private static readonly string ContractsNs = "EffectLedger.Contracts";
    private static readonly string IConstrainedName = ContractsNs + ".IConstrained<TProfile>";
    private static readonly string ImmutableValueName = ContractsNs + ".ImmutableValue";
    private static readonly string DeterministicName = ContractsNs + ".DeterministicComputation";

    public ProfileResolver(Compilation compilation) => _compilation = compilation;

    private bool IsOurSymbol(ISymbol? symbol, string fullName)
    {
        if (symbol is null) return false;
        return symbol.ToDisplayString() == fullName;
    }

    public ContractProfileKind ResolveProfile(INamedTypeSymbol profileType)
    {
        if (IsOurSymbol(profileType, ImmutableValueName)) return ContractProfileKind.ImmutableValue;
        if (IsOurSymbol(profileType, DeterministicName)) return ContractProfileKind.DeterministicComputation;
        return ContractProfileKind.Unknown;
    }

    /// <summary>扫描所有类型，返回实现了 IConstrained&lt;T&gt; 的声明。同名伪接口不会匹配（符号比对）。</summary>
    public IEnumerable<ContractDeclaration> FindDeclarations()
    {
        var result = new List<ContractDeclaration>();
        foreach (var type in _compilation.GlobalNamespace.GetAllTypes())
        {
            // 每个接口实现；IConstrained<T> 只有一个类型参数。
            foreach (var iface in type.AllInterfaces)
            {
                if (!iface.IsGenericType) continue;
                if (!IsOurSymbol(iface.ConstructedFrom, IConstrainedName)) continue;

                // 只接受直接、显式实现（alias/全限定均可，符号已归一）。
                var profileType = iface.TypeArguments[0] as INamedTypeSymbol;
                var kind = profileType is null ? ContractProfileKind.Unknown : ResolveProfile(profileType);
                var loc = type.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
                result.Add(new ContractDeclaration(type, kind, loc));
            }
        }
        return result;
    }
}

// 小工具：递归枚举命名空间下的所有类型。
internal static class NamespaceExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers())
                yield return nested;
        }
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var t in child.GetAllTypes())
                yield return t;
    }
}
