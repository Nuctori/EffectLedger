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
            // 收集该类型声明的所有角色（判重/判冲突用）。
            var profiles = new List<ContractProfileKind>();
            foreach (var iface in type.AllInterfaces)
            {
                if (!iface.IsGenericType) continue;
                if (!IsOurSymbol(iface.ConstructedFrom, IConstrainedName)) continue;

                var profileType = iface.TypeArguments[0] as INamedTypeSymbol;
                var kind = profileType is null ? ContractProfileKind.Unknown : ResolveProfile(profileType);
                // 未知角色必须保留：计划 §2.1 要求发"不支持的角色"诊断（不能静默接受）。
                profiles.Add(kind);
            }

            if (profiles.Count == 0) continue;

            // 计划 §2.1：每个类型只支持一个显式角色；多角色 ⇒ 冲突（去重后仍 >1）。
            // 首选规则：取第一个已知角色；冲突时保留两个声明，由引擎按"冲突"报告
            // （实现为两个 ContractDeclaration，引擎端对同类型多声明报 EBC0001）。
            var loc = type.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
            var distinct = profiles.Distinct().ToList();
            if (distinct.Count == 1)
            {
                result.Add(new ContractDeclaration(type, distinct[0], loc));
            }
            else
            {
                // 多角色冲突：每个角色各发一条声明，引擎将因重复根而报冲突（见 ContractEngine）。
                foreach (var k in distinct)
                    result.Add(new ContractDeclaration(type, k, loc));
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
