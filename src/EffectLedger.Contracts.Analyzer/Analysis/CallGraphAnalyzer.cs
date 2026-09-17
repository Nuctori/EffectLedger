// CallGraphAnalyzer.cs — P2.4/P2.5 跨方法摘要与递归固定点。
// 行为沿调用关系传播（不可妥协要求 3）。
//   - 同 compilation 有源码的方法：递归合并（SCC 用向上固定点）。
//   - 外部/开放分派/不支持：保留摘要中的 Unknown（不默认纯）。
// 缓存归属 compilation；不做跨 compilation 静态 Dictionary（P2.5）。

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace EffectLedger.Contracts.Analyzer.Analysis;

public sealed class CallGraphAnalyzer
{
    private readonly Compilation _compilation;
    private readonly AnalysisBudget _budget;
    private readonly Dictionary<IMethodSymbol, MethodSummary> _memo = new(SymbolEqualityComparer.Default);
    private readonly HashSet<IMethodSymbol> _inProgress = new(SymbolEqualityComparer.Default);
    private int _nodeCount;

    public CallGraphAnalyzer(Compilation compilation, AnalysisBudget budget)
    {
        _compilation = compilation;
        _budget = budget;
    }

    /// <summary>解析（必要时递归）一个方法的完整摘要。</summary>
    public MethodSummary Resolve(IMethodSymbol method)
    {
        if (_memo.TryGetValue(method, out var cached)) return cached;

        if (_inProgress.Contains(method))
        {
            // 递归边：返回当前已知（可能为空），避免无限展开；固定点会收敛。
            return _memo.GetValueOrDefault(method) ?? MethodSummary.Empty;
        }

        if (_nodeCount >= _budget.MaxCallGraphNodes)
            return MethodSummary.Empty.WithUnknown(UnknownReason.BudgetExceeded,
                $"调用图节点预算耗尽（上限 {_budget.MaxCallGraphNodes}）", null);

        _inProgress.Add(method);
        _nodeCount++;

        var builder = new SummaryBuilder(_compilation, _budget);
        var self = builder.Build(method, new ContractProfileKindHint(
            method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor));
        _memo[method] = self;

        // 传播：语法调用点 + 隐式调用点（属性 getter / Dispose 等，审查 F-01/F-05）。
        foreach (var callee in DirectCallees(method, self))
        {
            var calleeSummary = Resolve(callee);
            // receiver 写入只在被调方与调用方**同类型实例方法**时才指向同一个 this；
            // 调用局部 helper 对象（`b.Add(v)` 写 b._total）的 receiver 写入若照单全收，
            // 会被误报成根的"修改自身状态"（审计第十轮 FP2）。
            var sameTypeInstance = !callee.IsStatic
                && SymbolEqualityComparer.Default.Equals(callee.ContainingType, method.ContainingType);
            self = MethodSummary.Merge(self, PropagateCallee(calleeSummary, sameTypeInstance));
            _memo[method] = self;
        }

        _inProgress.Remove(method);
        return self;
    }

    /// <summary>
    /// 本方法的被调方法集合：语法调用点（IInvocation）∪ 隐式需要传播的成员
    /// （属性 getter、using 的 Dispose 等）。后者不经语法调用点，必须显式并入。
    /// </summary>
    private IEnumerable<IMethodSymbol> DirectCallees(IMethodSymbol method, MethodSummary self)
    {
        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        foreach (var callee in CollectDirectCalls(method))
            if (seen.Add(callee)) yield return callee;

        foreach (var implicitCallee in self.RequiresPropagation)
            if (implicitCallee is not null && seen.Add(implicitCallee)) yield return implicitCallee;
    }

    /// <summary>
    /// 被调方法摘要并入调用方：已知违规与未知一律保留（未知绝不被清除）。
    /// Fresh 类局部写入不逃逸到调用方，故不上升；其余维度全量并集。
    /// </summary>
    private static MethodSummary PropagateCallee(MethodSummary calleeSummary, bool sameTypeInstance) => new()
    {
        HiddenInputs = calleeSummary.HiddenInputs,
        ExternalEffects = calleeSummary.ExternalEffects,
        Unknowns = calleeSummary.Unknowns,
        // 局部（Fresh）写入不跨方法上升；其余写入目标语义仍然有效——
        // 但 Receiver 写入仅在跨类型边界丢弃（FP2），Unknown 写入保守保留。
        Writes = calleeSummary.Writes
            .Where(w => w.Target != WriteTargetKind.Fresh)
            .Where(w => w.Target != WriteTargetKind.Receiver || sameTypeInstance)
            .ToImmutableHashSet(),
        ReturnsInputAlias = calleeSummary.ReturnsInputAlias,
        EscapesParameters = calleeSummary.EscapesParameters,
        EscapesReceiver = calleeSummary.EscapesReceiver,
        StoresCallback = calleeSummary.StoresCallback,
        ExecutesCallback = calleeSummary.ExecutesCallback,
        IsConstructorLike = calleeSummary.IsConstructorLike,
    };

    /// <summary>枚举方法体内直接调用（IInvocationOperation.Invocation）的被调方法符号。</summary>
    private IEnumerable<IMethodSymbol> CollectDirectCalls(IMethodSymbol method)
    {
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            var model = _compilation.GetSemanticModel(node.SyntaxTree);
            var operation = model.GetOperation(node);
            if (operation is null) continue;
            var stack = new Stack<IOperation>();
            stack.Push(operation);
            while (stack.Count > 0)
            {
                var op = stack.Pop();
                if (op is IInvocationOperation inv)
                {
                    var target = inv.TargetMethod;
                    // 仅传播可解析的源码目标；外部/虚/接口目标在 SummaryBuilder 中已记为 Unknown。
                    if (target.DeclaringSyntaxReferences.Length > 0 && !target.IsVirtual && !target.IsAbstract
                        && target.ContainingType?.TypeKind != TypeKind.Interface)
                    {
                        yield return target;
                    }
                }
                foreach (var child in op.ChildOperations) stack.Push(child);
            }
        }
    }
}
