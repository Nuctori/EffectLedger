// ContractEngine.cs — P2/P3/P4 引擎入口：把声明 + 摘要 + 角色规则 → 诊断与结果。
// 分析器与 Tool 共用本引擎（R-GATE-09：两套判断语义合一）。

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Diagnostics;
using EffectLedger.Contracts.Analyzer.Profiles;

namespace EffectLedger.Contracts.Analyzer.Engine;

/// <summary>一个受约束根的最终评估结论（供分析器诊断 + Tool 报告共用）。</summary>
public sealed class ContractResult
{
    public INamedTypeSymbol Type { get; set; } = null!;
    public ContractProfileKind Profile { get; set; } = ContractProfileKind.Unknown;
    public MethodSummary Behavior { get; set; } = MethodSummary.Empty;

    public bool IsViolated => Violations.Any();
    public List<Diagnostic> Violations { get; set; } = new();

    /// <summary>Unknown 原因（strict 模式据此失败，不能当成功）。</summary>
    public List<UnknownSite> Unknowns { get; set; } = new();

    public bool HasUnknown => Unknowns.Any();
}

/// <summary>
/// 引擎：给定 compilation 与声明，计算结论。
/// 不做字符串匹配、不执行用户代码（R-SEM-01 / 不可妥协要求 2）。
/// </summary>
public sealed class ContractEngine
{
    private readonly Compilation _compilation;
    private readonly AnalysisBudget _budget;

    private readonly ContractConfig _config;

    public ContractEngine(Compilation compilation, AnalysisBudget budget)
        : this(compilation, budget, ContractConfig.Empty) { }

    /// <summary>
    /// <paramref name="config"/> 承载 P4.4 用户摘要与策略：外部符号若有匹配摘要，
    /// 以摘要替代 Unknown；否则保持 Unknown（绝不静默当作纯）。
    /// </summary>
    public ContractEngine(Compilation compilation, AnalysisBudget budget, ContractConfig config)
    {
        _compilation = compilation;
        _budget = budget;
        _config = config;
    }

    public ContractResult Evaluate(ContractDeclaration decl)
    {
        var result = new ContractResult { Type = decl.Type, Profile = decl.Profile };
        if (decl.Profile == ContractProfileKind.Unknown)
        {
            result.Violations.Add(Diagnostic.Create(ContractDiagnostics.InvalidProfile,
                decl.Location, decl.Type.Name, "未识别的角色（仅支持 ImmutableValue / DeterministicComputation）"));
            return result;
        }

        // 计划 §2.2：受约束 class/record class 须 sealed（BC-139：角色经派生继承会扩大
        // 契约面，基类未 sealed 时派生类可自由扩展——只对引用类型要求，值类型天然 sealed）。
        if (decl.Type.TypeKind == TypeKind.Class && decl.Type is INamedTypeSymbol nt && !nt.IsSealed)
        {
            result.Violations.Add(Diagnostic.Create(ContractDiagnostics.InvalidProfile,
                decl.Location, decl.Type.Name,
                $"受约束的 class 须声明 sealed（角色语义不容未受控的派生扩展）"));
        }

        var analyzer = new CallGraphAnalyzer(_compilation, _budget, _config);
        var behavior = MethodSummary.Empty;

        // 覆盖全部可执行成员（含属性/索引器访问器与事件访问器）：
        // 此前排除 PropertyGet/IndexerGet 只保留"被其它方法调用的 getter"，
        // 导致**公共属性/索引器的 getter 完全不被分析**（审计第三轮 F1/F2 —— 最严重家族）。
        // 访问器体本身就是可执行代码，必须与普通方法同等对待。
        // 角色可经**继承**获得（派生类不重复声明）：此时基类的可变状态与 mutator
        // 同样是派生类型的组成部分，必须并入判定——否则把 mutator 上移一层即可逃逸契约
        // （第三轮用户视角审计 BLOCKER-1：`DerivedRepo : BaseRepo` 曾报 OK）。
        var declaringTypes = EnumerateSelfAndBases(decl.Type);

        var methods = declaringTypes
            .SelectMany(t => t.GetMembers().OfType<IMethodSymbol>())
            .Where(m => !m.IsImplicitlyDeclared
                        && m.MethodKind is not (MethodKind.Destructor
                            or MethodKind.EventAdd or MethodKind.EventRemove))
            .Distinct<IMethodSymbol>(SymbolEqualityComparer.Default)
            .ToList();
        foreach (var m in methods)
            behavior = MethodSummary.Merge(behavior, analyzer.Resolve(m));

        result.Behavior = behavior;
        result.Unknowns = behavior.Unknowns.ToList();
        EvaluateProfile(decl, behavior, result);
        EvaluateExposure(decl, result);
        return result;
    }

    /// <summary>
    /// 公开成员暴露内部可变状态（审查 F1/F2）：public 属性/字段直接返回已知可变集合或可变类型实例。
    /// 这是"别名外泄"最直接、最常见的形状，必须在 ImmutableValue 下失败。
    /// </summary>
    private void EvaluateExposure(ContractDeclaration decl, ContractResult result)
    {
        if (decl.Profile != ContractProfileKind.ImmutableValue) return;
        foreach (var member in decl.Type.GetMembers())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;

            ITypeSymbol? exposed = null;
            string? name = null;

            if (member is IFieldSymbol f && !f.IsStatic && !f.IsConst) { exposed = f.Type; name = f.Name; }
            else if (member is IPropertySymbol p && !p.IsStatic && p.GetMethod is not null) { exposed = p.Type; name = p.Name; }

            if (exposed is not null && IsMutableReference(exposed))
            {
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableAliasEscape,
                    decl.Location, decl.Type.Name, "公开可变状态",
                    $"公开成员 {name} 直接暴露可变类型 {exposed.ToDisplayString()}（非冻结视图）"));
                continue;
            }

            // ref 返回（`ref int this[i]` / `ref T Get()`）：即便元素是 int，
            // 可写引用也让外部直接改写内部存储（审计第九轮 #7b）。
            if (member is IPropertySymbol { IsIndexer: true } idx
                && idx.DeclaredAccessibility == Accessibility.Public
                && (idx.ReturnsByRef || idx.ReturnsByRefReadonly == false && idx.Type is IPointerTypeSymbol))
            {
                // 只拦可写 ref（ReturnsByRef）；ref readonly 只读窗口放行。
                if (idx.ReturnsByRef)
                {
                    result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableAliasEscape,
                        decl.Location, decl.Type.Name, "ref 返回索引器",
                        $"公共索引器返回可写 ref：外部可直接改写内部存储"));
                }
            }
            if (member is IMethodSymbol refm && refm.DeclaredAccessibility == Accessibility.Public
                && refm.ReturnsByRef && ReturnsReceiverMutableLocation(refm))
            {
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableAliasEscape,
                    decl.Location, decl.Type.Name, "ref 返回方法",
                    $"公共方法 {refm.Name} 返回可写 ref 指向内部状态"));
            }

            // 公共方法/索引器把内部可变字段直接交出（审计第九轮 #7：
            // `public List<int> GetItems() => _list;` 与属性同罪，此前完全未检查）。
            if (member is IMethodSymbol m && m.DeclaredAccessibility == Accessibility.Public
                && !m.ReturnsVoid && m.MethodKind is MethodKind.Ordinary
                && ReturnsReceiverMutableField(m))
            {
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableAliasEscape,
                    decl.Location, decl.Type.Name, "方法暴露内部状态",
                    $"公共方法 {m.Name} 的返回值直接暴露内部可变状态"));
            }

            // 只读视图（IReadOnlyList/IEnumerable 等）：若 getter 直接返回内部可变字段的视图，
            // 底层仍可被本类型修改、也可被调用方向下转型取回 ⇒ 不是冻结（审查 F1，"IReadOnlyList 不等于冻结"）。
            // 仅当**返回类型本身是引用类型**时才可能构成视图——
            // 标量访问器（如 `int Count => _items.Count`）不暴露任何别名（审查 F-02 的误报回归）。
            // 【审计第十轮 FP9】**readonly 字段**例外：构造后不可替换（别名保留已由
            // EBC1002 单独把关），视图之上没有本类型可执行的修改路径 ⇒ 冻结拷贝的标准形态。
            if (member is IPropertySymbol { GetMethod: { } getter } prop
                && !prop.Type.IsValueType
                && prop.Type.SpecialType != SpecialType.System_String
                && ReturnsViewOverMutableField(getter, out var viewedField)
                && viewedField is { IsReadOnly: false })
            {
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableAliasEscape,
                    decl.Location, decl.Type.Name, "只读视图非冻结",
                    $"公开成员 {name} 返回内部可变集合的只读视图（底层仍可被修改）"));
            }
        }
    }

    /// <summary>
    /// 方法体是否直接返回 receiver 的可变字段/属性（含经隐式转换）。
    /// 返回拷贝（ToArray 等）不算——返回的是新对象。
    /// </summary>
    private bool ReturnsReceiverMutableField(IMethodSymbol method)
    {
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            var model = _compilation.GetSemanticModel(node.SyntaxTree);
            var op = model.GetOperation(node);
            if (op is null) continue;
            var stack = new Stack<IOperation>();
            stack.Push(op);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur is IReturnOperation { ReturnedValue: { } rv })
                {
                    var core = rv;
                    while (core is IConversionOperation c || core is IParenthesizedOperation)
                        core = core switch { IConversionOperation c2 => c2.Operand, IParenthesizedOperation pp => pp.Operand, _ => core };
                    if (core is IFieldReferenceOperation fr && !fr.Field.IsStatic
                        && fr.Instance is IInstanceReferenceOperation
                        && IsMutableReference(fr.Field.Type))
                        return true;
                    if (core is IPropertyReferenceOperation pr2 && !pr2.Property.IsStatic
                        && pr2.Instance is IInstanceReferenceOperation
                        && IsMutableReference(pr2.Property.Type))
                        return true;
                }
                foreach (var ch in cur.ChildOperations) stack.Push(ch);
            }
        }
        return false;
    }

    /// <summary>方法体是否返回指向 receiver 状态的可写引用（ref return）。</summary>
    private bool ReturnsReceiverMutableLocation(IMethodSymbol method)
    {
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            var model = _compilation.GetSemanticModel(node.SyntaxTree);
            var op = model.GetOperation(node);
            if (op is null) continue;
            var stack = new Stack<IOperation>();
            stack.Push(op);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur is IReturnOperation { ReturnedValue: { } rv })
                {
                    var core = rv;
                    while (core is IConversionOperation || core is IParenthesizedOperation)
                        core = core switch
                        {
                            IConversionOperation c => c.Operand,
                            IParenthesizedOperation pp => pp.Operand,
                            _ => core,
                        };
                    if (core is IFieldReferenceOperation fr && !fr.Field.IsStatic
                            && fr.Instance is IInstanceReferenceOperation)
                        return true;
                    if (core is IArrayElementReferenceOperation arr
                            && arr.ArrayReference is IFieldReferenceOperation afr
                            && !afr.Field.IsStatic && afr.Instance is IInstanceReferenceOperation)
                        return true;
                }
                foreach (var ch in cur.ChildOperations) stack.Push(ch);
            }
        }
        return false;
    }

    /// <summary>getter 是否返回内部可变字段上的视图（AsReadOnly/ToList 等对可变字段的投影）。</summary>
    private bool ReturnsViewOverMutableField(IMethodSymbol getter, out IFieldSymbol? viewedField)
    {
        viewedField = null;
        foreach (var syntaxRef in getter.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            var model = _compilation.GetSemanticModel(node.SyntaxTree);
            var op = model.GetOperation(node);
            if (op is null) continue;
            var stack = new Stack<IOperation>();
            stack.Push(op);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                // 直接返回内部可变字段，或其上的实例调用（视图投影）。
                if (cur is IFieldReferenceOperation f && !f.Field.IsStatic && IsMutableReference(f.Field.Type))
                {
                    viewedField = f.Field;
                    return true;
                }
                foreach (var child in cur.ChildOperations) stack.Push(child);
            }
        }
        return false;
    }

    /// <summary>类型是否可携带可变别名（委托至 Analysis.MutableType 单一真源）。</summary>
    private bool IsMutableReference(ITypeSymbol? type)
    {
        if (type is null) return false;
        // 显式声明 ImmutableValue 的用户类型已承担深层不可变义务 ⇒ 不作为可变承载面。
        if (DeclaresImmutableValue(type)) return false;
        // 只读视图/不可变容器：容器不可变，元素按同一规则递归确认。
        if (type is INamedTypeSymbol g && g.TypeArguments.Length > 0)
        {
            var n = g.OriginalDefinition.ToDisplayString();
            if (n is "System.Collections.Generic.IReadOnlyList<T>"
                or "System.Collections.Generic.IReadOnlyCollection<T>"
                or "System.Collections.Generic.IEnumerable<T>"
                or "System.Collections.Immutable.ImmutableArray<T>"
                or "System.Collections.Immutable.ImmutableList<T>")
                return g.TypeArguments.Any(IsMutableReference);
        }
        return Analysis.MutableType.IsMutableCarrier(type);
    }

    /// <summary>把违反站点（file:line）拼到诊断消息里，便于直接定位根因。</summary>
    private static string At(string? locationKey, string description) =>
        locationKey is null ? description : $"{description}（站点 {locationKey}）";

    /// <summary>
    /// EBC2003：确定性入口的输入/输出稳定性。
    /// 公共方法的参数与返回值必须是"已知稳定"的值——值类型、string、或已知不可变类型。
    /// 若入口接受/返回未知可变引用，则调用方的并发修改可使结果不确定，
    /// 而这一风险不应只写在文档里（本计划 §3.2 明确要求入口条件被检查）。
    /// </summary>
    private void EvaluateEntryStability(ContractDeclaration decl, ContractResult result)
    {
        foreach (var member in decl.Type.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor
                or MethodKind.PropertyGet or MethodKind.PropertySet)) continue;
            if (member.IsAbstract) continue;
            // 编译器合成成员（record 的 Equals/<Clone>$/ToString、匿名类型等）不是用户入口：
            // 对其检查只会产生噪声（审计收敛轮 · record 支持引入）。
            if (member.IsImplicitlyDeclared) continue;
            if (member.Name.StartsWith("<", StringComparison.Ordinal)) continue;

            foreach (var p in member.Parameters)
            {
                if (IsStableEntryValue(p.Type)) continue;
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.DeterministicEntryCondition,
                    decl.Location, decl.Type.Name, member.Name,
                    $"参数 '{p.Name}' 的类型 {p.Type.ToDisplayString()} 不是已知稳定值（外部可并发修改）"));
            }

            // 返回值稳定性：构造/属性 setter 无返回值语义。
            if (member.MethodKind is MethodKind.Constructor or MethodKind.PropertySet) continue;
            if (member.ReturnsVoid) continue;
            // by-ref 返回（`ref List<int> Get()` / `ref readonly List<int> Get()`）：
            // 调用方仍可经引用修改内部状态 ⇒ 检查**被引用元素**的稳定性而非跳过
            // （审计第九轮 #8：此前全部 `continue` 造成假绿）。
            if (!IsStableEntryValue(member.ReturnType))
            {
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.DeterministicEntryCondition,
                    decl.Location, decl.Type.Name, member.Name,
                    $"返回类型 {member.ReturnType.ToDisplayString()} 不是已知稳定值（可向调用方泄露内部可变状态）"));
            }
        }
    }

    /// <summary>
    /// 自底向上枚举类型及其基类链（到 object 为止）。
    /// 用于把"经继承获得的角色"的基类成员一并纳入分析（BLOCKER-1）。
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateSelfAndBases(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.SpecialType == SpecialType.System_Object) yield break;   // object 无效应
            yield return t;
        }
    }

    /// <summary>
    /// 入口值是否已知稳定：
    ///   值类型（含 enum/struct）、string；
    ///   只读接口（IReadOnly* / IEnumerable，由调用方保证在调用期间不被修改——这是确定性输入的
    ///   标准建模方式，若一律拒绝则函数无法接受任何集合参数）；
    ///   已知不可变 BCL 类型；显式声明 ImmutableValue 的用户类型。
    /// 明确**不稳定**：指针/函数指针、开放泛型 T、可变集合、未知引用类型。
    /// </summary>
    private bool IsStableEntryValue(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.TypeParameter or TypeKind.Pointer or TypeKind.FunctionPointer)
            return false;
        if (type is IPointerTypeSymbol or IFunctionPointerTypeSymbol) return false;
        if (type.IsValueType) return true;
        if (type.SpecialType == SpecialType.System_String) return true;
        // record（含 record struct）：语言级 init-only 属性 ⇒ 构造后不可变，
        // 属于稳定入口/返回类型（审计第十轮 FP3b：`RPoint MoveDown(RPoint p)` 曾被拒）。
        if (type is INamedTypeSymbol { IsRecord: true }) return true;

        var original = type.OriginalDefinition?.ToDisplayString();
        return original is "System.Collections.Immutable.ImmutableArray<T>"
            or "System.Collections.Immutable.ImmutableList<T>"
            or "System.Collections.Immutable.ImmutableDictionary<TKey, TValue>"
            or "System.Collections.Immutable.ImmutableHashSet<T>"
            or "System.Collections.Frozen.FrozenDictionary<TKey, TValue>"
            or "System.Collections.Frozen.FrozenSet<T>"
            // 只读视图接口：确定性输入的常规形态（调用期间稳定由调用方保证）。
            or "System.Collections.Generic.IReadOnlyList<T>"
            or "System.Collections.Generic.IReadOnlyCollection<T>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            or "System.Collections.Generic.IEnumerable<T>"
            || DeclaresImmutableValue(type);
    }

    /// <summary>该类型是否直接声明了 IConstrained&lt;ImmutableValue&gt;。</summary>
    private bool DeclaresImmutableValue(ITypeSymbol type)
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
    /// ref/out 参数检查：本类型可借此改写调用方状态，两个角色都不允许。
    /// </summary>
    private static void EvaluateByRefParameters(ContractDeclaration decl, ContractResult result,
        Microsoft.CodeAnalysis.DiagnosticDescriptor descriptor)
    {
        foreach (var m in decl.Type.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.DeclaredAccessibility != Accessibility.Public) continue;
            if (m.MethodKind is not MethodKind.Ordinary) continue;
            // 编译器合成的成员（record 的 Deconstruct、with 等）不是用户写下的可变性暴露。
            if (m.IsImplicitlyDeclared) continue;
            foreach (var p in m.Parameters)
            {
                if (p.RefKind is RefKind.None or RefKind.In) continue;
                // out 只写：把结果交回调用方，不构成"修改"；in 只读引用：同理不构成修改
                // （审计第十轮 FP6）；ref 才是双向别名。
                if (p.RefKind == RefKind.Out) continue;
                result.Violations.Add(Diagnostic.Create(descriptor,
                    decl.Location, decl.Type.Name,
                    $"公共方法 {m.Name} 的 {p.RefKind} 参数 '{p.Name}' 允许修改调用方状态",
                    "ref/out 参数"));
            }
        }
    }

    /// <summary>
    /// ImmutableValue 的对外表面检查（审计第三轮 F10/F13）：
    ///   - 公共可写属性/索引器（set 可达）违反"构造后状态稳定"；
    ///   - 公共方法接受 ref/out 参数时，调用方状态可被本类型修改（可变性通过参数外泄）。
    /// </summary>
    private void EvaluateImmutableSurface(ContractDeclaration decl, ContractResult result)
    {
        foreach (var member in decl.Type.GetMembers())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;

            if (member is IPropertySymbol prop)
            {
                // 公共 setter（含索引器）：构造完成后可被外部改写 ⇒ 不是不可变值。
                if (prop.SetMethod is { DeclaredAccessibility: Accessibility.Public } setter
                    && setter.IsInitOnly == false)
                {
                    result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableMutatingWrite,
                        decl.Location, decl.Type.Name, "公共可写属性",
                        $"公共属性 {prop.Name} 暴露 setter：构造后状态可被外部改写"));
                }
            }

        }

        // ref/out 参数检查与本类型成员遍历解耦（共用同一判定，避免两处漂移）。
        EvaluateByRefParameters(decl, result, ContractDiagnostics.ImmutableAliasEscape);
    }

    private void EvaluateProfile(ContractDeclaration decl, MethodSummary b, ContractResult result)
    {
        var typeName = decl.Type.Name;
        var loc = decl.Location;

        // 诊断消息带**违反站点**（file:line），而不只是类型声明位置——
        // 否则用户拿到的是一堆指向 class 行的提示，无法定位根因（审查 F-13 / R-UX-01）。

        if (decl.Profile == ContractProfileKind.ImmutableValue)
        {
            foreach (var w in b.Writes.Where(w => w.Target is WriteTargetKind.Receiver or WriteTargetKind.Unknown))
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableMutatingWrite,
                    loc, typeName, "字段/可达对象写入", At(w.LocationKey, w.Description)));

            // 修改调用方传入的对象（含 ref/out 别名）同样是可观察状态变化
            // （审计第三轮 F13：此前 ImmutableValue 只报 Receiver 写入）。
            foreach (var w in b.Writes.Where(w => w.Target is WriteTargetKind.Parameter))
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableMutatingWrite,
                    loc, typeName, "修改输入参数状态", At(w.LocationKey, w.Description)));

            // 静态可变状态写入（第三轮用户视角审计 BLOCKER-2）：
            // `private static int _n; ... _n++`、`static readonly List<string> _names; _names.Add(x)`
            // 都让"本类型的可观察行为"随进程级状态漂移，与 ImmutableValue 的承诺相悖；
            // 同款写在 DeterministicComputation 下已被检出，此处**必须同判**（此前完全静默）。
            foreach (var e in b.ExternalEffects.Where(e => e.Kind == ExternalEffectKind.StaticWrite))
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableMutatingWrite,
                    loc, typeName, "静态可变状态写入", At(e.LocationKey, e.Description)));

            // 别名逃逸：保留输入可变别名、返回输入别名或 this 逃逸。
            if (b.ReturnsInputAlias || b.EscapesReceiver || b.EscapesParameters)
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableAliasEscape,
                    loc, typeName, "别名暴露",
                    b.EscapesParameters ? "构造/成员保留输入参数的可变引用（外部仍可修改）"
                                        : "返回值/构造期持有可变引用"));

            if (b.EscapesReceiver)
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.ImmutableCtorEscape,
                    loc, typeName, "this 逃逸", "receiver 被传出/注册"));

            EvaluateImmutableSurface(decl, result);
        }
        else if (decl.Profile == ContractProfileKind.DeterministicComputation)
        {
            foreach (var h in b.HiddenInputs)
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.DeterministicHiddenInput,
                    loc, typeName, "隐藏输入", At(h.LocationKey, h.Description)));

            foreach (var e in b.ExternalEffects)
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.DeterministicExternalWrite,
                    loc, typeName, "外部写入/IO", At(e.LocationKey, e.Description)));

            foreach (var w in b.Writes.Where(w => w.Target is WriteTargetKind.Parameter))
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.DeterministicExternalWrite,
                    loc, typeName, "修改输入", At(w.LocationKey, w.Description)));

            // 修改 receiver 自身状态同样是"外部可观察修改"：
            // 同一实例被再次调用时结果依赖此前调用（审计第三轮 F11 —— 此前完全静默）。
            foreach (var w in b.Writes.Where(w => w.Target is WriteTargetKind.Receiver or WriteTargetKind.Unknown))
                result.Violations.Add(Diagnostic.Create(ContractDiagnostics.DeterministicExternalWrite,
                    loc, typeName, "修改自身状态", At(w.LocationKey, w.Description)));

            // ref/out 参数允许本类型改写调用方状态 ⇒ 不是纯计算
            // （审计第三轮 F13 的 DeterministicComputation 侧；此前仅 ImmutableValue 覆盖）。
            EvaluateByRefParameters(decl, result, ContractDiagnostics.DeterministicExternalWrite);

            EvaluateEntryStability(decl, result);
        }

        // 未知：记录（strict 据此失败；advisory 可见但不视为通过）。
        // 去重：合并摘要与调用点传播会重复登记同一 (原因,描述,站点)（审计第十轮：build 输出重复 EBC9001）。
        foreach (var u in b.Unknowns
            .Distinct()
            .OrderBy(u => u.Reason).ThenBy(u => u.Description).ThenBy(u => u.LocationKey))
            result.Unknowns.Add(u);
    }
}
