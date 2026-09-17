// MutableType.cs — 类型可变性判定的单一真源（审计第三轮：此前分析侧与角色侧各有一份白名单，会漂移）。
// 判据：该类型的值在传递/保存后，是否可能被他人观察到改变。
//   - 标量/string/已知不可变集合（元素亦不可变）⇒ 不携带别名
//   - object / 接口 / 未知引用类型 ⇒ 可能承载任意可变状态（保守）
//   - 值类型 ⇒ 本身按值传递，但**可能承载**可变引用（KeyValuePair/元组/用户 struct）⇒ 递归
//   - Span/Memory ⇒ 指向内部存储的窗口 ⇒ 视为别名
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EffectLedger.Contracts.Analyzer.Analysis;

public static class MutableType
{
    /// <summary>
    /// 该类型是否直接实现 <c>IConstrained&lt;ImmutableValue&gt;</c>（按符号精确比对，非字符串短名）。
    /// </summary>
    public static bool DeclaresImmutableValue(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named) return false;
        foreach (var iface in named.AllInterfaces)
        {
            if (!iface.IsGenericType) continue;
            if (iface.ConstructedFrom.ToDisplayString() != "EffectLedger.Contracts.IConstrained<TProfile>") continue;
            if (iface.TypeArguments[0] is INamedTypeSymbol prof
                && prof.ToDisplayString() == "EffectLedger.Contracts.ImmutableValue")
                return true;
        }
        return false;
    }

    /// <summary>
    /// 类型的规范判据键：**MetadataName**（`List`1` / `ImmutableArray`1`）。
    /// 刻意不带命名空间——实测 `ImmutableArray&lt;T&gt;` 位于 global namespace，
    /// 任何"System.Collections.Immutable.*"全名比较都匹配不上（历轮漂移的最终根因）。
    /// </summary>
    public static string? MetadataNameOf(ITypeSymbol? type)
        => (type as INamedTypeSymbol)?.OriginalDefinition.MetadataName;

    /// <summary>
    /// 目录键与白名单共用的规范名：`命名空间 + "." + MetadataName`（如 `System.Collections.Generic.List`1`）。
    /// 与 `SummaryBuilder.CanonicalTypeName`、`BclCatalog` 条目三者必须同口径——
    /// 历轮 bug 全部源于口径分裂（display `<T>` / 纯 MetadataName / 带命名空间）。
    /// </summary>
    public static string? CanonicalKey(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named) return null;
        var def = named.OriginalDefinition;
        var ns = def.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? def.MetadataName : ns + "." + def.MetadataName;
    }

    /// <summary>
    /// 该类型的值传递/保存后是否可能被他人观察到改变。
    /// <paramref name="allowDeclaredContracts"/>：是否承认用户显式声明的
    /// <c>IConstrained&lt;ImmutableValue&gt;</c> 契约（"声明即义务"，其稳定性由该类型自身的检查负责）。
    /// 全部调用点共用本方法 ⇒ 不存在"某条路径忘了排除声明契约"的口径漂移。
    /// </summary>
    public static bool IsMutableCarrier(ITypeSymbol? type, int depth = 0, bool allowDeclaredContracts = true)
    {
        if (type is null) return false;
        if (depth > 4) return true;                       // 深链保守
        // 声明契约的用户类型：本模块的核心立场是"声明产生义务、义务由被声明类型自己兑现"，
        // 故组合这些类型时不重复判其可变性（审计：此前仅暴露检查承认契约，存储路径不承认）。
        if (allowDeclaredContracts && DeclaresImmutableValue(type)) return false;
        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer) return true;
        if (type is IPointerTypeSymbol or IFunctionPointerTypeSymbol) return true;

        if (type.SpecialType == SpecialType.System_String) return false;
        if (type.SpecialType == SpecialType.System_Object) return true;   // 装箱可藏任意可变对象

        // 基元/枚举：纯值语义，不承载任何引用（用户视角审计：此前走"值类型扫字段"路径，
        // 误把 Int32 等判为可变承载，导致 ImmutableArray<int> 被当可变 —— 全局白名单漂移的根因）。
        if (type.SpecialType is not SpecialType.None) return false;
        if (type.TypeKind == TypeKind.Enum) return false;
        // decimal/DateTime/Guid/TimeSpan 等结构化值类型：按值语义，不含可变引用。
        if (type is INamedTypeSymbol { IsValueType: true } vt
            && vt.ContainingNamespace?.ToDisplayString() == "System"
            && vt.GetMembers().OfType<IFieldSymbol>().All(f => f.IsStatic || !f.Type.IsReferenceType))
            return false;

        // 统一口径：**元数据形态**（System.Collections.Generic.List`1）。
        // ToDisplayString() 对部分泛型返回 `ImmutableArray<>`（无命名空间、无参数名），
        // 与之比较的 display 形态白名单永远匹配不上——历轮假绿/假红的共同根因。
        var name = CanonicalKey(type);

        if (name is "System.Span`1" or "System.ReadOnlySpan`1" or "System.Memory`1" or "System.ReadOnlyMemory`1")
            return true;

        // 数组是可写容器（元素可被替换/写入）⇒ 恒为可变承载。
        // 元素是否可变不影响该结论，故此分支无需（也不应）递归元素——
        // 此前写作 `... || true` 让前半段成为死代码（用户视角审计发现）。
        if (type is IArrayTypeSymbol) return true;

        if (name is "System.Collections.Generic.List`1" or "System.Collections.Generic.Dictionary`2"
            or "System.Collections.Generic.HashSet`1" or "System.Collections.Generic.Queue`1"
            or "System.Collections.Generic.Stack`1"
            or "System.Text.StringBuilder" or "System.Collections.ArrayList" or "System.Collections.Hashtable")
            return true;

        // 只读视图接口：本身不提供修改能力。容器不可变；元素可变性仍需递归确认
        // （`IReadOnlyList<List<int>>` 仍可经由元素修改内部状态）。
        if (name is "System.Collections.Generic.IReadOnlyList`1" or "System.Collections.Generic.IReadOnlyCollection`1"
            or "System.Collections.Generic.IReadOnlyDictionary`2" or "System.Collections.Generic.IEnumerable`1")
        {
            return type is INamedTypeSymbol rov
                && rov.TypeArguments.Any(a => IsMutableCarrier(a, depth + 1, allowDeclaredContracts));
        }

        if (name is "System.Collections.Immutable.ImmutableArray`1" or "System.Collections.Immutable.ImmutableList`1"
            or "System.Collections.Immutable.ImmutableDictionary`2" or "System.Collections.Immutable.ImmutableHashSet`1"
            or "System.Collections.Frozen.FrozenSet`1" or "System.Collections.Frozen.FrozenDictionary`2")
        {
            // 容器不可变，但元素仍可能可变。
            return type is INamedTypeSymbol g && g.TypeArguments.Any(a => IsMutableCarrier(a, depth + 1, allowDeclaredContracts));
        }

        if (type is INamedTypeSymbol { IsRecord: true } rec)
        {
            // record：属性 init-only ⇒ 本类型无法替换成员；
            // 但若属性类型本身可变（record 里塞 List），仍然经由它可变 ⇒ 递归属性。
            return rec.GetMembers().OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && p.GetMethod is not null)
                .Any(p => IsMutableCarrier(p.Type, depth + 1, allowDeclaredContracts));
        }

        if (type.IsValueType)
        {
            // 值类型本身无别名，但可能承载可变引用（KeyValuePair<string,List<int>> 等）。
            return type.GetMembers().OfType<IFieldSymbol>()
                .Any(f => !f.IsStatic && IsMutableCarrier(f.Type, depth + 1, allowDeclaredContracts));
        }

        // System.Type/MethodInfo 等元数据对象视为不可变承载（不携带业务可变状态）。
        if (name is "System.Type" or "System.Reflection.MethodInfo" or "System.Reflection.PropertyInfo") return false;

        return true;   // 其余引用类型保守：可能可变
    }
}
