// SummaryBuilder.cs — P2.2/P2.3 方法摘要构建（IOperation 驱动，语义分析，非字符串扫描）。
// 核心：遍历方法体的 IOperation 树，区分 read/write、隐藏输入、外部效果、回调、逃逸、未知。
// 不依赖方法名匹配（不可妥协要求 2）。未建模的 operation kind 走 UnsupportedOperation => Unknown。

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffectLedger.Contracts.Analyzer.Analysis;

/// <summary>
/// 单个方法的摘要计算。调用点的跨方法传播由 <see cref="CallGraphAnalyzer"/> 负责；
/// 本类只处理"这一个方法体内直接可见"的语义。
/// </summary>
public sealed class SummaryBuilder
{
    private readonly Compilation _compilation;
    private readonly AnalysisBudget _budget;
    private int _steps;

    private readonly ContractConfig _config;

    public SummaryBuilder(Compilation compilation, AnalysisBudget budget)
        : this(compilation, budget, ContractConfig.Empty) { }

    /// <summary><paramref name="config"/>：P4.4 用户摘要——外部符号命中摘要时以摘要替代 Unknown。</summary>
    public SummaryBuilder(Compilation compilation, AnalysisBudget budget, ContractConfig config)
    {
        _compilation = compilation;
        _budget = budget;
        _config = config;
    }

    /// <summary>构建一个方法（或其构造函数/访问器）的直接摘要。</summary>
    public MethodSummary Build(IMethodSymbol method, ContractProfileKindHint hint)
    {
        _steps = 0;
        var summary = MethodSummary.Empty;
        if (method.IsAbstract || method.PartialImplementationPart is not null) return summary;
        // 编译器合成成员（record 位置属性的 get/set、<Clone>$ 等）：
        // 只读写自身 backing field，无用户代码。此前对 get_Y 报"操作树未覆盖"Unknown
        // （审计第十轮 FP3），并对 Equals/Clone 产生噪声。
        if (method.IsImplicitlyDeclared) return summary;

        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            var semanticModel = GetSemanticModel(node);
            if (semanticModel is null) continue;

            // 自动属性访问器（`public int X { get; }`）无方法体：只读写自身字段，无外部效应。
            // 它们是语言合成形状而非"未覆盖语法"——报 Unknown 会污染每个普通不可变值（审查 F-03 回归）。
            if (node is AccessorDeclarationSyntax { Body: null, ExpressionBody: null }) continue;

            // record 声明（含主构造函数）：`GetOperation` 对 RecordDeclarationSyntax 返回 null。
            // 主构造函数的体由 record 声明的属性初始化构成——属性初始化表达式需单独遍历，
            // 而不是当作"未覆盖形状"报 Unknown（那会让最常见的不可变类型无法使用）。
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax recordDecl)
            {
                summary = summary.With(WalkRecordDeclaration(recordDecl, method, semanticModel));
                continue;
            }

            var operation = ResolveOperation(method, node, semanticModel);
            if (operation is null)
            {
                summary = summary.WithUnknown(UnknownReason.UnsupportedOperation,
                    $"无法获取 '{method.Name}' 的操作树（语法形状未覆盖）", GetLocationKey(node));
                continue;
            }
            summary = summary.With(Walk(operation!, method, semanticModel));
        }

        // 构造函数与初始化器标记：ImmutableValue 的构造期语义需要区分阶段。
        if (method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor)
            summary = summary.With(new MethodSummary { IsConstructorLike = true });

        return summary;
    }

    private SemanticModel? GetSemanticModel(SyntaxNode node)
    {
        if (node.SyntaxTree is null) return null;
        return _compilation.GetSemanticModel(node.SyntaxTree);
    }

    private static IOperation? ResolveOperation(IMethodSymbol method, SyntaxNode node, SemanticModel model)
    {
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax)
            return model.GetOperation(node);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ConstructorDeclarationSyntax)
            return model.GetOperation(node);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax p)
            return model.GetOperation(p);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.IndexerDeclarationSyntax i)
            return model.GetOperation(i);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.OperatorDeclarationSyntax o)
            return model.GetOperation(o);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ConversionOperatorDeclarationSyntax c)
            return model.GetOperation(c);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.AccessorDeclarationSyntax a)
            return model.GetOperation(a);
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ArrowExpressionClauseSyntax arrow)
            return model.GetOperation(arrow);
        return model.GetOperation(node);
    }

    /// <summary>
    /// 遍历 record 声明中会执行的代码：主构造函数参数默认值、属性初始化表达式。
    /// record 的 `with`/Deconstruct 等合成成员由编译器生成，不含用户逻辑。
    /// </summary>
    private MethodSummary WalkRecordDeclaration(
        Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax decl,
        IMethodSymbol method, SemanticModel model)
    {
        var summary = MethodSummary.Empty;
        var callees = ImmutableHashSet.CreateBuilder<IMethodSymbol>(SymbolEqualityComparer.Default);
        void WalkAndCollect(SyntaxNode expr)
        {
            var op = model.GetOperation(expr);
            if (op is null) return;
            summary = summary.With(Walk(op, method, model));
            // Walk 只做逐节点分类；用户方法调用必须登记给调用图传播
            // （审计第九轮 #4：`= Counter.Bump();` 初始化器中的静态写入此前漏检）。
            var stack = new Stack<IOperation>();
            stack.Push(op);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur is IInvocationOperation inv
                    && inv.TargetMethod.DeclaringSyntaxReferences.Length > 0
                    && !inv.TargetMethod.IsVirtual && !inv.TargetMethod.IsAbstract
                    && inv.TargetMethod.ContainingType?.TypeKind != TypeKind.Interface)
                    callees.Add(inv.TargetMethod);
                foreach (var c in cur.ChildOperations) stack.Push(c);
            }
        }
        foreach (var param in decl.ParameterList?.Parameters
            ?? default(Microsoft.CodeAnalysis.SeparatedSyntaxList<Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax>))
        {
            if (param.Default is null) continue;
            WalkAndCollect(param.Default.Value);
        }
        foreach (var member in decl.Members)
        {
            if (member is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax prop
                && prop.Initializer is not null)
            {
                WalkAndCollect(prop.Initializer.Value);
            }
        }
        var collected = callees.ToImmutable();
        if (!collected.IsEmpty)
            summary = summary.With(new MethodSummary { RequiresPropagation = collected });
        return summary;
    }

    private static string? GetLocationKey(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return $"{System.IO.Path.GetFileName(span.Path)}:{span.StartLinePosition.Line + 1}";
    }

    private static string? LocationKey(IOperation op) => op.Syntax is null ? null : GetLocationKey(op.Syntax);

    private MethodSummary Walk(IOperation root, IMethodSymbol method, SemanticModel model)
    {
        var summary = MethodSummary.Empty;
        var stack = new Stack<IOperation>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            if (_budget.ConsumeOperation(ref _steps) == false)
            {
                summary = summary.WithUnknown(UnknownReason.BudgetExceeded,
                    $"'{method.Name}' 的操作预算耗尽（步数上限 {_budget.MaxOperationsPerMethod}）", null);
                break;
            }
            var op = stack.Pop();

            // 局部函数 / lambda 的**定义**不是执行（不可妥协要求 4）：
            // 其函数体不在本方法执行路径上，除非被调用（调用点经 IInvocationOperation 传播）。
            if (op is ILocalFunctionOperation or IAnonymousFunctionOperation)
                continue;

            // with 表达式子树整体按 Fresh 克隆处理（见 Classify 的 IWithOperation 注记）。
            if (op is IWithOperation)
                continue;

            summary = summary.With(Classify(op, method, model));
            foreach (var child in op.ChildOperations)
                stack.Push(child);
        }
        return summary;
    }

    private MethodSummary Classify(IOperation op, IMethodSymbol method, SemanticModel model)
    {
        switch (op)
        {
            case IInvocationOperation inv:
                return ClassifyInvocation(inv, method, model);

            case IPropertyReferenceOperation prop:
                return ClassifyProperty(prop, method, model);

            case IFieldReferenceOperation field:
                return ClassifyField(field, method);

            case ISimpleAssignmentOperation assign:
                return ClassifyAssignment(assign, method, model);

            case ICompoundAssignmentOperation compound:
            {
                // `a += b`：目标写入分类 + 用户运算符体传播（审计第九轮 #6）。
                var byTarget = ClassifyWriteTarget(compound.Target, "复合赋值", method, model);
                var byOp = compound.OperatorMethod is { } cm && cm.DeclaringSyntaxReferences.Length > 0
                    ? MethodSummary.Empty.With(new MethodSummary
                    {
                        RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, cm),
                    })
                    : MethodSummary.Empty;
                return MethodSummary.Merge(byTarget, byOp);
            }

            case IIncrementOrDecrementOperation incdec:
            {
                var byTarget = ClassifyWriteTarget(incdec.Target, "自增/自减", method, model);
                var byOp = incdec.OperatorMethod is { } im && im.DeclaringSyntaxReferences.Length > 0
                    ? MethodSummary.Empty.With(new MethodSummary
                    {
                        RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, im),
                    })
                    : MethodSummary.Empty;
                return MethodSummary.Merge(byTarget, byOp);
            }

            // 事件订阅/退订（+= / -=）修改 receiver 或参数的状态：按写目标分类。
            // 此前未建模 ⇒ 落入 default 空摘要，是"静默绕过路径"（审查 F11）。
            case IEventAssignmentOperation evt:
                return ClassifyEventAssignment(evt, method, model);

            // 返回值逃逸（审查 F-07/F-09）：返回输入参数别名或返回 this。
            case IReturnOperation ret:
                return ClassifyReturn(ret, method);

            // using / using 声明：Dispose 是隐式调用，必须传播（审查 F-05）。
            case IUsingOperation usingOp:
                return ClassifyUsing(usingOp, method, model);

            // with 表达式（审计第十轮 FP3）：`this with {...}` 是 clone-then-init，
            // 对克隆体的属性赋值不是对既有状态的修改。
            case IWithOperation:
                return MethodSummary.Empty;

            // 插值字符串（审计第三轮 F12 的精确化，收敛轮 BC-069 修正）：
            // handler 调用是编译器降级产物，未降级 IOperation 树中只有 InterpolatedString，
            // 因此必须在 IInterpolatedStringOperation 上判定：
            //   有格式符（{x:C}）⇒ 文化敏感；
            //   无格式符但表达式类型默认 ToString 受文化影响（decimal/double/DateTime）⇒ 敏感；
            //   string/int/char 等拼接 ⇒ 纯连接，不报（避免对 $"hello {name}" 误报）。
            case IInterpolatedStringOperation interpolated:
                return ClassifyInterpolatedString(interpolated);

            // foreach：GetEnumerator / MoveNext / Current 是隐式调用，
            // 用户自定义枚举器若不被传播，其中的隐藏 IO 会静默漏报（审查 F-04）。
            case IForEachLoopOperation forEach:
                return ClassifyForEach(forEach, method, model);

            case IObjectCreationOperation creation:
                return ClassifyCreation(creation, method, model);

            // 用户运算符（+ - * / == 等）：运算符方法体是可执行代码，
            // 未被传播时其中的隐藏 IO 会静默漏报（审计第三轮 F3）。
            case IBinaryOperation bin when bin.OperatorMethod is { } binOp && binOp.DeclaringSyntaxReferences.Length > 0:
                return MethodSummary.Empty.With(new MethodSummary
                {
                    RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, binOp),
                });
            case IUnaryOperation un when un.OperatorMethod is { } unOp && unOp.DeclaringSyntaxReferences.Length > 0:
                return MethodSummary.Empty.With(new MethodSummary
                {
                    RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, unOp),
                });

            // 指针操作（fixed / 地址取用 / 解引用）：与文档声明的 unsafe 策略一致 ⇒ Unknown，
            // 绝不静默当作无副作用（审计第三轮 F8）。
            // 语法级判定：`fixed` 语句与 `&`/`*` 指针表达式在语法树中形状稳定。
            case var _ when op.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.FixedStatementSyntax:
                return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                    "fixed 固定语句涉及指针访问，未建模", LocationKey(op));

            case var _ when op.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.PrefixUnaryExpressionSyntax pfx
                && pfx.OperatorToken.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsteriskToken)
                && pfx.Operand is Microsoft.CodeAnalysis.CSharp.Syntax.PrefixUnaryExpressionSyntax inner
                && inner.OperatorToken.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AmpersandToken):
                return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                    "指针解引用写入未建模", LocationKey(op));

            case IDelegateCreationOperation:
            case IAnonymousFunctionOperation:
                // 委托创建本身不执行函数体（不可妥协要求 4）；体在执行时单独传播。
                return MethodSummary.Empty;

            case IFunctionPointerInvocationOperation:
                return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                    "函数指针调用不支持分析", LocationKey(op));

            case IDynamicInvocationOperation or IDynamicMemberReferenceOperation:
                return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                    "dynamic 分派不支持分析", LocationKey(op));

            // 用户转换（显式/隐式一律）：转换运算符体是可执行代码
            // （审计第九轮 #5：此前只拦隐式，`(int)m` 显式形态静默放行）。
            case IConversionOperation conv when conv.OperatorMethod is not null:
                return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                    $"用户转换 '{conv.OperatorMethod.Name}' 使用未建模路径", LocationKey(op));

            default:
                return MethodSummary.Empty;
        }
    }

    private MethodSummary ClassifyInvocation(IInvocationOperation inv, IMethodSymbol method, SemanticModel model)
    {
        var target = inv.TargetMethod;
        // 目录键以泛型**定义**为规范形态；构造实例（List<int>/int）需归一化，
        // 否则最常见的安全成员（List<T>.ToArray / int.ToString）会查不到而落 Unknown。
        var typeName = CanonicalTypeName(target.ContainingType);
        var memberName = target.Name;
        var loc = LocationKey(inv);

        // 基类/object 构造函数：建立对象状态，不是外部可观察修改，视为已知安全。
        if (target.MethodKind == MethodKind.Constructor && typeName is "System.Object" or "object")
            return MethodSummary.Empty;

        // 委托执行（不可妥协要求 4）：`d()` / `d.Invoke()` 执行传入的函数值。
        // 记为 ExecutesCallback —— 它表明"函数体在此处被执行"，与"仅创建未调用"区分开。
        // 委托体在编译期通常不可归属到具体目标（形参/字段来源）⇒ 必须报 Unknown：
        // 执行未知代码不能被当作"无副作用"。
        if (target.MethodKind == MethodKind.DelegateInvoke)
        {
            var executed = inv.Instance;
            if (executed is IDelegateCreationOperation { Target: IMethodReferenceOperation mref }
                && mref.Method.DeclaringSyntaxReferences.Length > 0)
            {
                return MethodSummary.Empty.With(new MethodSummary
                {
                    ExecutesCallback = true,
                    RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(
                        SymbolEqualityComparer.Default, mref.Method),
                });
            }

            return new MethodSummary { ExecutesCallback = true }
                .WithUnknown(UnknownReason.UnsupportedOperation,
                    "执行来源不可静态解析的委托：其副作用无法确认", loc);
        }

        // 已知可变集合修改器：按接收者来源分类（改参数/改 receiver = 越界；改 fresh 局部 = 允许）。
        var canonical = CanonicalTypeName(target.ContainingType);
        if (canonical is not null && BclCatalog.IsKnownMutator(canonical, memberName))
        {
            var recv = inv.Instance;
            // 集合初始化器（new List<int> { ... }）与 fresh 局部允许。
            var recvKind = ClassifyInstanceTarget(recv, method);
            // ref/out 参数作为接收者：别名指向调用方状态 ⇒ 修改等同修改输入（审计第三轮 F13）。
            if (recv is IParameterReferenceOperation rp2 && rp2.Parameter.RefKind != RefKind.None)
            {
                return MethodSummary.Empty.WithWrite(WriteTargetKind.Parameter,
                    $"经 ref/out 参数 '{rp2.Parameter.Name}' 修改集合 {memberName}", loc);
            }

            // 存入 receiver 集合的委托实参：捕获分析（审计第九轮 #9 的 List.Add 形态，
            // `_hooks.Add(() => input.Clear())` 使本类型经字段可达地持有修改调用方状态的入口）。
            MethodSummary byDelegateArg = MethodSummary.Empty;
            foreach (var arg in inv.Arguments)
            {
                if (arg.Value is IAnonymousFunctionOperation or IDelegateCreationOperation)
                    byDelegateArg = MethodSummary.Merge(byDelegateArg, ClassifyDelegateCapture(arg.Value, method));
            }
            var mutSummary = recvKind switch
            {
                WriteTargetKind.Fresh => MethodSummary.Empty,
                WriteTargetKind.Parameter => MethodSummary.Empty.WithWrite(WriteTargetKind.Parameter,
                    $"修改输入参数的集合 {memberName}", loc),
                WriteTargetKind.Receiver when IsConstructorContext(method) => MethodSummary.Empty,
                WriteTargetKind.Receiver => MethodSummary.Empty.WithWrite(WriteTargetKind.Receiver,
                    $"修改 receiver 的集合 {memberName}", loc),
                WriteTargetKind.Static => MethodSummary.Empty.WithEffect(ExternalEffectKind.StaticWrite,
                    $"修改静态集合 {memberName}", loc),
                _ => MethodSummary.Empty.WithWrite(WriteTargetKind.Unknown,
                    $"修改来源不可确定的集合 {memberName}", loc),
            };
            // 构造期把 this 作为实参交给集合 mutator（`StaticBag.Items.Add(this)`）：
            // 这是注册期逃逸的典型形态，而 mutator 分支会提前 return，
            // 故必须在这里一并判定（用户视角审计：此前只拦直接静态字段赋值，漏掉本形态）。
            if (IsConstructorContext(method) && inv.Arguments.Any(a => ArgumentCarriesReceiver(a.Value))
                && recvKind is WriteTargetKind.Static or WriteTargetKind.Unknown)
                mutSummary = MethodSummary.Merge(mutSummary, new MethodSummary { EscapesReceiver = true });

            return MethodSummary.Merge(byDelegateArg, mutSummary);
        }

        // 构造期 this 逃逸：任何实参直接或间接来源为 this（receiver）时，receiver 被传出。
        if (IsConstructorContext(method) && inv.Arguments.Any(a => ArgumentCarriesReceiver(a.Value)))
            return new MethodSummary { EscapesReceiver = true };

        // 0) 实参敏感的文化/确定性判定（审计 FP5/FP7）：
        //    目录按成员名登记（无法区分重载），故凡是"传了明确的确定性实参"的调用，
        //    一律放行——用户在代码里显式写了 InvariantCulture / 序数比较器，
        //    正是文档要求的做法，不应反被拒。
        if (HasExplicitDeterministicCultureArg(inv) || HasExplicitDeterministicComparerArg(inv))
            return MethodSummary.Empty;
        // 往返/ISO 标准格式符（"O"/"o"/"R"/"r"/"s"/"u"）在 BCL 契约里就是文化无关的，
        // 显式传它们等同于显式声明"与当前文化无关"（第三轮用户视角审计 MINOR-6）。
        if (HasRoundTripFormatSpecifier(inv))
            return MethodSummary.Empty;

        // 1) BCL 精确目录。
        if (typeName is not null && BclCatalog.TryLookup(typeName, memberName, out var entry))
        {
                if (entry.IsKnownSafe) return MethodSummary.Empty;
                var s = MethodSummary.Empty;
                if (entry.HiddenInputs != HiddenInputKind.None)
                    s = s.WithHidden(entry.HiddenInputs, $"{typeName}.{memberName}", loc);
                if (entry.ExternalEffects != ExternalEffectKind.None)
                    s = s.WithEffect(entry.ExternalEffects, $"{typeName}.{memberName}", loc);
                return s;
        }

        // 2) 用户代码（同 compilation 有源码）→ 由调用图传播（此处只记调用关系，不重复展开）。
        //    外部符号：先查 P4.4 用户摘要；命中则以摘要为准（trust），否则 Unknown（不默认纯）。
        if (target.IsExtern || target.DeclaringSyntaxReferences.IsEmpty)
        {
            if (TryUserSummary(typeName, memberName, loc, out var viaSummary))
                return viaSummary;

            if (IsSystemNamespace(typeName))
            {
                return MethodSummary.Empty.WithUnknown(UnknownReason.ExternalSummaryMissing,
                    $"外部依赖 {typeName}.{memberName} 无行为摘要（未在目录中登记）", loc);
            }
            return MethodSummary.Empty.WithUnknown(UnknownReason.ExternalSummaryMissing,
                $"外部程序集依赖 {typeName}.{memberName} 无行为摘要", loc);
        }

        // 3) 开放动态派发：接口/虚方法且目标不闭合 → Unknown。
        if (IsOpenDispatch(target))
        {
            return MethodSummary.Empty.WithUnknown(UnknownReason.OpenDispatch,
                $"开放分派 {typeName}.{memberName}（虚/接口，目标未闭合）", loc);
        }

        return MethodSummary.Empty;
    }

    private MethodSummary ClassifyProperty(IPropertyReferenceOperation prop, IMethodSymbol method, SemanticModel model)
    {
        var target = prop.Property;
        // 目录键以**泛型定义**为规范形态（`List<T>`），构造实例（`List<int>`）需归一化，
        // 否则 `_list.Count` 这类最常见读取会查不到而落到 Unknown。
        var typeName = CanonicalTypeName(target.ContainingType);
        var memberName = target.Name;
        var loc = LocationKey(prop);

        // 静态可写属性读取 = 隐藏共享状态。
        if (target.IsStatic && !target.IsReadOnly && target.SetMethod is not null)
        {
            return MethodSummary.Empty.WithHidden(HiddenInputKind.GlobalState,
                $"静态可写属性 {typeName}.{memberName}", loc);
        }

        if (typeName is not null)
        {
            // 索引器的符号名是 "this[]"，目录按 get_Item 登记（审计第十轮 FP10）。
            var catalogMember = memberName == "this[]" ? "get_Item" : "get_" + memberName;
            if (BclCatalog.TryLookup(typeName, catalogMember, out var entry))
            {
                if (entry.IsKnownSafe) return MethodSummary.Empty;
                var s = MethodSummary.Empty;
                if (entry.HiddenInputs != HiddenInputKind.None) s = s.WithHidden(entry.HiddenInputs, $"{typeName}.{memberName}", loc);
                if (entry.ExternalEffects != ExternalEffectKind.None) s = s.WithEffect(entry.ExternalEffects, $"{typeName}.{memberName}", loc);
                return s;
            }
        }

        // 有源码的 getter：交给调用图按符号传播（审查 F-01：属性 getter 曾是静默绕过路径）。
        // 这里只登记"需传播的 getter 符号"，效果在 CallGraphAnalyzer 中合并。
        if (target.GetMethod is { } getter && getter.DeclaringSyntaxReferences.Length > 0)
        {
            // 递归属性（getter 读自身）不在此展开，交由调用图的递归守卫处理。
            return MethodSummary.Empty.With(new MethodSummary
            {
                RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, getter),
            });
        }

        // 外部属性 getter 无摘要 → Unknown（保守，不默认纯）。
        if (IsSystemNamespace(typeName) || target.GetMethod is { DeclaringSyntaxReferences.Length: 0 })
        {
            return MethodSummary.Empty.WithUnknown(UnknownReason.ExternalSummaryMissing,
                $"外部属性 {typeName}.{memberName} 无行为摘要", loc);
        }
        return MethodSummary.Empty;
    }

    private MethodSummary ClassifyField(IFieldReferenceOperation field, IMethodSymbol method)
    {
        var f = field.Field;
        if (f.IsStatic && !f.IsReadOnly && !f.IsConst)
        {
            return MethodSummary.Empty.WithHidden(HiddenInputKind.GlobalState,
                $"静态可变字段 {f.ContainingType?.ToDisplayString()}.{f.Name}", LocationKey(field));
        }
        return MethodSummary.Empty;
    }

    private MethodSummary ClassifyAssignment(ISimpleAssignmentOperation assign, IMethodSymbol method, SemanticModel model)
    {
        // 目标写入分类（构造后 receiver/参数/静态写入）。
        var byTarget = ClassifyWriteTarget(assign.Target, "赋值", method, model);

        // 值侧逃逸分类（审查 F1）：把参数/receiver 派生值存入自身字段，即保留外部可变别名。
        var byValue = ClassifyAssignmentValue(assign, method);

        // 委托/函数值存入字段 = 逃逸（之后可被任意时刻调用，不可妥协要求 4）。
        var byDelegate = ClassifyDelegateStorage(assign, method);

        return MethodSummary.Merge(MethodSummary.Merge(byTarget, byValue), byDelegate);
    }

    /// <summary>
    /// 委托存入字段/属性 = 逃逸。委托的**创建**不执行其函数体，但**存储**使其可能在
    /// 任意时刻、任意上下文被调用，故必须与"仅创建"区分开（不可妥协要求 4）。
    /// </summary>
    private MethodSummary ClassifyDelegateStorage(ISimpleAssignmentOperation assign, IMethodSymbol method)
    {
        if (!IsDelegateType(assign.Value?.Type)) return MethodSummary.Empty;
        // 持久化存储目标：字段/属性（审计第九轮 #9 追加）数组元素与索引器
        // （`_hooks[0] = cb;` 同样让委托在任意时刻可被调用）。
        if (assign.Target is not (IFieldReferenceOperation or IPropertyReferenceOperation
            or IArrayElementReferenceOperation))
            return MethodSummary.Empty;

        // lambda/匿名函数/方法组：捕获分析是权威判定（#10 —— 此前 origin=Fresh 导致逃逸判定形同虚设）。
        if (assign.Value is IAnonymousFunctionOperation or IDelegateCreationOperation)
            return ClassifyDelegateCapture(assign.Value, method) is var cap
                ? MethodSummary.Merge(new MethodSummary { StoresCallback = true }, cap)
                : new MethodSummary { StoresCallback = true };

        // 其它委托值（字段/属性/调用结果）：按来源保守判定。
        var origin = ClassifyValueOrigin(assign.Value, method);
        return new MethodSummary
        {
            StoresCallback = true,
            EscapesParameters = origin is WriteTargetKind.Parameter or WriteTargetKind.Static,
            EscapesReceiver = origin == WriteTargetKind.Receiver,
        };
    }

    /// <summary>
    /// 赋值**值侧**分类：把输入参数或 this 派生的引用存入 receiver 字段 = 别名保留（ImmutableValue 违反）。
    /// 此前只分类目标、不分类值，导致"把调用方的 List 存进 readonly 字段"完全不可见。
    /// </summary>
    private MethodSummary ClassifyAssignmentValue(ISimpleAssignmentOperation assign, IMethodSymbol method)
    {
        // 自动属性赋值的目标是 IPropertyReferenceOperation（编译器映射到 backing field），
        // 此前只认字段 ⇒ `private List<int> Items { get; } ... Items = input;` 完全漏检
        // （审计第九轮 #14）。两者统一按"写入 receiver 自身存储"处理。
        // get-only 自动属性在构造函数中赋值时 SetMethod 为 null（编译器特权写入），
        // 但仍是"把值存入自身存储"——不能以 SetMethod 缺失为由放行（审计第九轮 #14 实测）。
        if (assign.Target is IPropertyReferenceOperation propTarget
            && propTarget.Instance is IInstanceReferenceOperation)
        {
            return ClassifyAliasRetention(propTarget.Property.IsStatic, assign, method);
        }
        if (assign.Target is not IFieldReferenceOperation targetField) return MethodSummary.Empty;
        if (targetField.Instance is not IInstanceReferenceOperation
            && !targetField.Field.IsStatic) return MethodSummary.Empty;
        return ClassifyAliasRetention(targetField.Field.IsStatic, assign, method);
    }

    /// <summary>
    /// 赋值值侧的别名保留判定（字段与自动属性共用；审计第九轮 #14 抽取）。
    /// </summary>
    private MethodSummary ClassifyAliasRetention(bool isStaticTarget, ISimpleAssignmentOperation assign, IMethodSymbol method)
    {
        // 静态目标：receiver（this）来源的逃逸不受类型可变性门控（审计第三轮 F5）。
        if (isStaticTarget)
        {
            var staticOrigin = ClassifyValueOrigin(assign.Value, method);
            if (staticOrigin == WriteTargetKind.Receiver)
                return new MethodSummary { EscapesReceiver = true };
            // 参数或共享静态状态存入静态位置 = 外部别名逃逸。
            if (staticOrigin is WriteTargetKind.Parameter or WriteTargetKind.Static)
                return new MethodSummary { EscapesParameters = true };
            return MethodSummary.Empty;
        }

        // 实例目标：receiver 存自身字段不是逃逸。
        // 只有存储"可携带可变别名"的值才构成别名保留；标量/string 是合法深层不可变建模。
        // 【审计 N1】只读视图接口**不能**仅凭元素不可变就放行：静态类型是 IReadOnlyList<T>
        // 时 IsMutableReferenceType 会因元素不可变而返回 false，从而在来源判定前就早退——
        // 于是 `_xs = input`（input 是调用方的 List）被静默接受。故作例外继续往下判来源。
        var isReadOnlyView = IsReadOnlySafeView(assign.Value.Type);
        if (!isReadOnlyView && !IsMutableReferenceType(assign.Value.Type)) return MethodSummary.Empty;

        var valueSource = ClassifyValueOrigin(assign.Value, method);

        if (valueSource == WriteTargetKind.Parameter)
        {
            // 只读视图是否"真的冻结"取决于**来源**（审计 N1）：
            //   `_xs = input.ToList()`            ⇒ 防御性拷贝，调用方无法再触及 ⇒ 放行
            //   `_xs = input`（静态类型 IReadOnlyList<T> 而实参是 List<T>）⇒ 调用方仍可变 ⇒ 必须拒
            // 文档明确写着"`IReadOnlyList` 包装不算冻结"，故按来源而非按类型放行。
            if (IsDefensiveCopy(assign.Value, method)) return MethodSummary.Empty;
            // 不可变集合类型（ImmutableArray/ImmutableList/Frozen*）：其值本身不提供修改入口，
            // 且元素不可变时调用方也无法经元素改写 ⇒ 放行。
            // 注意：**只读视图接口**（IReadOnlyList 等）不在此列——视图可能只是调用方 List
            // 的包装，调用方仍持有原引用（审计 N1；文档明写"包装不算冻结"）。
            if (IsImmutableCollectionType(assign.Value.Type) && IsImmutableElementView(assign.Value.Type))
                return MethodSummary.Empty;
            return new MethodSummary { EscapesParameters = true };
        }
        // 共享静态状态存入实例字段：对象的可观察数据随进程级状态漂移（审计第九轮 #12）。
        if (valueSource == WriteTargetKind.Static)
            return new MethodSummary { EscapesParameters = true };
        if (valueSource == WriteTargetKind.Unknown)
            return new MethodSummary { EscapesParameters = true };
        return MethodSummary.Empty;
    }

    /// <summary>
    /// 值是否为"可变引用类型"：值类型（标量/enum/struct）不算别名；
    /// 已知可变集合/数组算；用户自定义引用类型按未知不可变处理（保守但不误报标量）。
    /// </summary>
    /// <summary>
    /// 类型是否"可携带可变别名"。统一实现（审计第三轮）：分析侧与角色判定侧共用同一判据，
    /// 避免两处白名单漂移导致假绿/假红。
    /// 详见 <see cref="MutableType.IsMutableCarrier"/>。
    /// </summary>
    private static bool IsMutableReferenceType(ITypeSymbol? type) => MutableType.IsMutableCarrier(type);

    /// <summary>
    /// 扫描方法体内对该局部的所有后续赋值，返回最污染的来源类别。
    /// Fresh 表示全部赋值源均为新建/字面量；Unknown 表示存在无法解析的源（保守）。
    /// </summary>
    private WriteTargetKind FindLocalReassignmentTaint(ILocalSymbol local, IMethodSymbol method)
    {
        var worst = WriteTargetKind.Fresh;
        foreach (var declRef in local.DeclaringSyntaxReferences)
        {
            var tree = declRef.SyntaxTree;
            var model = _compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var assign in root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax>())
            {
                var targetOp = model.GetOperation(assign.Left);
                if (targetOp is not ILocalReferenceOperation lr
                    || !SymbolEqualityComparer.Default.Equals(lr.Local, local))
                    continue;
                var valueOp = model.GetOperation(assign.Right);
                var kind = valueOp is null ? WriteTargetKind.Unknown : ClassifyValueOrigin(valueOp, method);
                if (kind == WriteTargetKind.Unknown) return WriteTargetKind.Unknown;
                // Parameter/Receiver/Static 任一出现即污染；取非 Fresh 的最严判。
                if (kind != WriteTargetKind.Fresh) worst = kind;
            }
        }
        return worst;
    }

    /// <summary>
    /// P4.4：查用户摘要。命中 ⇒ 按摘要返回（trust 级证据，报告须标注）；
    /// 未命中/策略禁止 ⇒ false，由调用方落 Unknown。
    /// 匹配为**精确符号 ID**（`命名空间.类型::成员`），不做短名匹配（防误命中）。
    /// </summary>
    private bool TryUserSummary(string? typeName, string memberName, string? loc, out MethodSummary summary)
    {
        summary = MethodSummary.Empty;
        if (!_config.Policy.AllowUserSummaries || _config.Summaries.IsDefaultOrEmpty || typeName is null)
            return false;

        var symbolId = typeName + "::" + memberName;
        UserSummary? hit = null;
        foreach (var s in _config.Summaries)
            if (string.Equals(s.SymbolId, symbolId, StringComparison.Ordinal)) { hit = s; break; }
        if (hit is null) return false;

        var desc = $"用户摘要 {hit.SymbolId}（trust；依据 {hit.EvidenceRef}）";
        switch (hit.Effect)
        {
            case "pure":
            case "none":
                break;
            case "hidden-time":
                summary = summary.WithHidden(HiddenInputKind.Time, desc, loc); break;
            case "hidden-random":
                summary = summary.WithHidden(HiddenInputKind.Random, desc, loc); break;
            case "hidden-ambient":
                summary = summary.WithHidden(HiddenInputKind.Ambient, desc, loc); break;
            case "hidden-culture":
                summary = summary.WithHidden(HiddenInputKind.Culture, desc, loc); break;
            case "io-console":
                summary = summary.WithEffect(ExternalEffectKind.Console, desc, loc); break;
            case "io-file":
                summary = summary.WithEffect(ExternalEffectKind.File, desc, loc); break;
            case "io-network":
                summary = summary.WithEffect(ExternalEffectKind.Network, desc, loc); break;
            case "io-static-write":
                summary = summary.WithEffect(ExternalEffectKind.StaticWrite, desc, loc); break;
        }
        if (hit.ReturnsAlias) summary = MethodSummary.Merge(summary, new MethodSummary { ReturnsInputAlias = true });
        if (hit.StoresCallback) summary = MethodSummary.Merge(summary, new MethodSummary { StoresCallback = true });
        if (hit.ExecutesCallback) summary = MethodSummary.Merge(summary, new MethodSummary { ExecutesCallback = true });
        return true;
    }

    /// <summary>
    /// 调用是否传入了 BCL 定义的文化无关**往返/标准格式符**常量（"O","o","R","r","s","u"）。
    /// `DateTimeOffset.ToString("O")` 的输出与 CurrentCulture 无关（BCL 契约）。
    /// </summary>
    private static bool HasRoundTripFormatSpecifier(IInvocationOperation inv)
    {
        // 仅对**接收格式串的成员**成立（ToString/ToString(format)/Parse/ParseExact/TryParse 族）。
        // 早期版本对所有调用扫描字符串常量 ⇒ `Console.WriteLine("s")` 因实参恰为 "s"
        // 被误判为往返格式符，进而整条调用被放行（实测导致 IO 漏报的回归）。
        var name = inv.TargetMethod.Name;
        if (name is not ("ToString" or "Parse" or "ParseExact" or "TryParse" or "TryParseExact"))
            return false;
        foreach (var arg in inv.Arguments)
        {
            if (arg.Value.ConstantValue is { HasValue: true, Value: string fmt })
            {
                var t = fmt.Trim();
                if (t is "O" or "o" or "R" or "r" or "s" or "u") return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 调用是否显式传入了确定性的文化实参（`CultureInfo.InvariantCulture` 或等价物）。
    /// 传了就与流程区域无关 ⇒ 文化敏感目录条目不应命中（审计 FP5）。
    /// </summary>
    private static bool HasExplicitDeterministicCultureArg(IInvocationOperation inv)
    {
        foreach (var arg in inv.Arguments)
        {
            var v = arg.Value;
            while (v is IConversionOperation c) v = c.Operand;
            if (v is IPropertyReferenceOperation p
                && p.Property.Name is "InvariantCulture" or "InvariantCultureIgnoreCase")
                return true;
        }
        return false;
    }

    /// <summary>
    /// 调用是否显式传入了确定性比较器（`StringComparer.Ordinal` / `...IgnoreCase`）。
    /// 传了就与当前文化无关 ⇒ OrderBy/GroupBy 等不应落 Unknown（审计 FP7）。
    /// </summary>
    private static bool HasExplicitDeterministicComparerArg(IInvocationOperation inv)
    {
        foreach (var arg in inv.Arguments)
        {
            var v = arg.Value;
            while (v is IConversionOperation c) v = c.Operand;
            if (v is IPropertyReferenceOperation p
                && p.Property.Name is "Ordinal" or "OrdinalIgnoreCase"
                && p.Property.ContainingType?.Name == "StringComparer")
                return true;
        }
        return false;
    }

    /// <summary>未指定格式时的默认 ToString 受 CurrentCulture 影响的基元类型（元数据全名）。</summary>
    private static readonly HashSet<string> CultureSensitiveDefaultFormat = new()
    {
        "System.Decimal", "System.Double", "System.Single",
        "System.DateTime", "System.DateTimeOffset", "System.TimeSpan",
    };

    /// <summary>
    /// 插值字符串的文化敏感性判定（逐 hole 精确化）。
    /// </summary>
    private MethodSummary ClassifyInterpolatedString(IInterpolatedStringOperation interpolated)
    {
        var summary = MethodSummary.Empty;
        foreach (var part in interpolated.Parts)
        {
            if (part is not IInterpolationOperation hole) continue;
            var desc = $"插值格式化（{hole.Expression?.Type?.ToDisplayString()}）";
            // FormatClause 在操作接口上不可用（本 Roslyn 版本），取语法树的等价成员：
            // hole.Syntax 即 InterpolationSyntax，其 FormatClause 形状稳定。
            var hasFormatClause = hole.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.InterpolationSyntax ips
                && ips.FormatClause is not null;
            if (hasFormatClause)
            {
                // 格式符只在**文化敏感类型**上才构成文化依赖：
                // `$"{d.Year:0000}"`（int 补零）与 `$"{code:X4}"` 都是文化无关的
                // （第三轮用户视角审计 MINOR-7：此前按"有格式符即敏感"误报）。
                var ft = hole.Expression?.Type;
                var fname = CanonicalTypeName(ft as INamedTypeSymbol) ?? ft?.ToDisplayString();
                var cultureSensitiveFormatted = fname is not null
                    && (CultureSensitiveDefaultFormat.Contains(fname)
                        || ft?.IsValueType == false);   // 引用类型（string 除外）走 IFormattable，默认受文化影响
                if (ft?.SpecialType == SpecialType.System_String) cultureSensitiveFormatted = false;
                if (cultureSensitiveFormatted)
                    summary = summary.WithHidden(HiddenInputKind.Culture,
                        "文化敏感的插值格式化（显式格式符）", LocationKey(part));
                continue;
            }
            var t = hole.Expression?.Type;
            var name = CanonicalTypeName(t as INamedTypeSymbol) ?? t?.ToDisplayString();
            if (name is not null && CultureSensitiveDefaultFormat.Contains(name))
            {
                summary = summary.WithHidden(HiddenInputKind.Culture, desc, LocationKey(part));
                continue;
            }
            // 用户自定义类型的插值会隐式调用其 ToString()（审计第九轮 #11）：
            // ToString 体可能读时钟/IO，必须登记传播（`$"{w}"` 与 `w.ToString()` 同罪）。
            if (t is INamedTypeSymbol user && t.SpecialType == SpecialType.None)
            {
                var ts = user.GetMembers("ToString").OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.Parameters.Length == 0 && m.DeclaringSyntaxReferences.Length > 0);
                if (ts is not null)
                    summary = summary.With(new MethodSummary
                    {
                        RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, ts),
                    });
            }
        }
        return summary;
    }

    /// <summary>是否为"只读视图"类型（IReadOnly*/IEnumerable/Immutable*）——不提供修改能力。</summary>
    private static bool IsReadOnlySafeView(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol g) return false;
        var isView = g.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IReadOnlyList<T>"
            or "System.Collections.Generic.IReadOnlyCollection<T>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            or "System.Collections.Generic.IEnumerable<T>"
            or "System.Collections.Immutable.ImmutableArray<T>"
            or "System.Collections.Immutable.ImmutableList<T>";
        // 容器只读还不够：元素必须同样不携带可变别名
        // （审计第九轮 #1：`IEnumerable<MutableThing>` 可经元素修改 / 调用方下转型改回）。
        return isView && IsImmutableElementView(type);
    }

    /// <summary>
    /// 该值是否为**防御性拷贝**（新对象，与调用方不再共享存储）：
    /// `.ToList()/.ToArray()`、拷贝构造 `new List&lt;T&gt;(src)`、`ToImmutable*`、`Array.Clone`。
    /// 这是"把外部集合安全存入不可变值"的标准做法（审计 N1 的放行依据）。
    /// </summary>
    private static bool IsDefensiveCopy(IOperation value, IMethodSymbol method)
    {
        var v = value;
        while (v is IConversionOperation c) v = c.Operand;
        if (v is not IInvocationOperation inv) return false;
        if (IsKnownCopyProducer(inv)) return true;
        var name = inv.TargetMethod.Name;
        return name is "ToImmutableArray" or "ToImmutableList" or "ToImmutableDictionary"
            or "ToImmutableHashSet" or "ToFrozenSet" or "ToFrozenDictionary" or "Clone";
    }

    /// <summary>
    /// 是否为**真正的不可变集合类型**（不是只读视图接口）：
    /// `ImmutableArray/ImmutableList/ImmutableDictionary/ImmutableHashSet/Frozen*`。
    /// 这些类型没有修改 API，持有其引用即持有冻结值。
    /// </summary>
    private static bool IsImmutableCollectionType(ITypeSymbol? type) =>
        MutableType.CanonicalKey(type) is
            "System.Collections.Immutable.ImmutableArray`1"
            or "System.Collections.Immutable.ImmutableList`1"
            or "System.Collections.Immutable.ImmutableDictionary`2"
            or "System.Collections.Immutable.ImmutableHashSet`1"
            or "System.Collections.Frozen.FrozenSet`1"
            or "System.Collections.Frozen.FrozenDictionary`2";

    /// <summary>只读视图的元素是否同样不可变（元素可变时视图仍可被间接改写）。</summary>
    private static bool IsImmutableElementView(ITypeSymbol? type) =>
        type is INamedTypeSymbol g && !g.TypeArguments.Any(t => MutableType.IsMutableCarrier(t));

    /// <summary>
    /// （已由 MutableType 单一真源取代——保留说明避免再次复制实现。）
    /// </summary>

    /// <summary>该类型是否直接实现 IConstrained&lt;ImmutableValue&gt;（精确符号比对）。</summary>
    private static bool DeclaresImmutableValueContract(ITypeSymbol type)
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

    /// <summary>剥离隐式转换/括号后，该值是否为 this 本身（`return this;` 常带 object 装箱转换）。</summary>
    private static bool IsThisValue(IOperation? value) => value switch
    {
        null => false,
        IInstanceReferenceOperation => true,
        IConversionOperation conv when conv.IsImplicit => IsThisValue(conv.Operand),
        IParenthesizedOperation paren => IsThisValue(paren.Operand),
        _ => false,
    };

    /// <summary>判断一个表达式值的来源类别（用于别名保留判定）。</summary>
    private WriteTargetKind ClassifyValueOrigin(IOperation? value, IMethodSymbol method)
    {
        if (value is null) return WriteTargetKind.Unknown;
        return value switch
        {
            IParameterReferenceOperation => WriteTargetKind.Parameter,
            IInstanceReferenceOperation => WriteTargetKind.Receiver,
            // 静态字段读取 = 进程级共享可变状态（审计第九轮 #12：此前落入 Fresh 放行）。
            IFieldReferenceOperation f when f.Field.IsStatic => WriteTargetKind.Static,
            IFieldReferenceOperation f =>
                f.Instance is IInstanceReferenceOperation ? WriteTargetKind.Receiver : WriteTargetKind.Unknown,
            IPropertyReferenceOperation p =>
                p.Instance is IInstanceReferenceOperation ? WriteTargetKind.Receiver : WriteTargetKind.Unknown,
            // 局部变量：沿声明初始化器与**全部后续赋值**解析（#2/#3：链式别名、分支重赋值）。
            ILocalReferenceOperation local => ClassifyLocalAlias(local.Local, method),
            IConversionOperation conv => ClassifyValueOrigin(conv.Operand, method),
            IParenthesizedOperation paren => ClassifyValueOrigin(paren.Operand, method),
            // lambda/方法组：来源由捕获分析单独判定（#10），此处不按 Fresh 放行。
            IAnonymousFunctionOperation or IDelegateCreationOperation => WriteTargetKind.Unknown,
            _ => WriteTargetKind.Fresh, // 字面量/新建对象不携带外部别名
        };
    }

    /// <summary>
    /// 捕获分析（审计第九轮 #10）：lambda/委托体内是否捕获了本方法的参数或 this。
    /// 捕获参数 ⇒ 存储后调用方可经委托修改其指向的状态；捕获 this ⇒ receiver 逃逸。
    /// </summary>
    private MethodSummary ClassifyDelegateCapture(IOperation? delegateValue, IMethodSymbol method)
    {
        if (delegateValue is not (IAnonymousFunctionOperation or IDelegateCreationOperation))
            return MethodSummary.Empty;

        // 方法组（`input.Clear` 作为委托）：绑定目标本身就捕获 receiver/参数。
        if (delegateValue is IDelegateCreationOperation { Target: IMethodReferenceOperation mref })
        {
            var ms = new MethodSummary { StoresCallback = true };
            if (mref.Instance is IParameterReferenceOperation)
                ms = MethodSummary.Merge(ms, new MethodSummary { EscapesParameters = true });
            else if (IsThisValue(mref.Instance))
                ms = MethodSummary.Merge(ms, new MethodSummary { EscapesReceiver = true });
            return ms;
        }

        // 取匿名函数体做捕获扫描。
        // 委托创建包裹形态（赋值/实参处的 lambda 都编译为 IDelegateCreationOperation
        // → Target=IAnonymousFunctionOperation）——漏掉这一层会让捕获扫描永远空跑
        // （审计第九轮 #10 实测：数组/集合存储形态全部 [OK] 的根因）。
        IOperation? body = delegateValue switch
        {
            IAnonymousFunctionOperation anon => anon.Body,
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation inner } => inner.Body,
            _ => null,
        };
        if (body is null) return MethodSummary.Empty;

        bool capturesParam = false, capturesReceiver = false;
        var stack = new Stack<IOperation>();
        stack.Push(body);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            switch (cur)
            {
                case IParameterReferenceOperation pr
                    when pr.Parameter.ContainingSymbol is IMethodSymbol owner
                         && SymbolEqualityComparer.Default.Equals(owner, method)
                         && pr.Parameter.Ordinal != -1:
                    capturesParam = true;
                    break;
                case IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }:
                    capturesReceiver = true;
                    break;
            }
            foreach (var c in cur.ChildOperations) stack.Push(c);
        }

        var sum = new MethodSummary { StoresCallback = true };
        if (capturesParam) sum = MethodSummary.Merge(sum, new MethodSummary { EscapesParameters = true });
        if (capturesReceiver) sum = MethodSummary.Merge(sum, new MethodSummary { EscapesReceiver = true });
        return sum;
    }

    /// <summary>
    /// 事件订阅/退订（+= / -=）：修改订阅列表，属 receiver（或参数）状态写入。
    /// </summary>
    private MethodSummary ClassifyEventAssignment(IEventAssignmentOperation evt, IMethodSymbol method, SemanticModel model)
    {
        var loc = LocationKey(evt);
        // EventReference 的静态类型是 IOperation；实际为 IEventReferenceOperation。
        var target = evt.EventReference as IEventReferenceOperation;
        var instance = target?.Instance;
        var kind = ClassifyInstanceTarget(instance, method);
        var eventName = target?.Event.Name ?? "<event>";

        return kind switch
        {
            WriteTargetKind.Fresh => MethodSummary.Empty,
            WriteTargetKind.Parameter => MethodSummary.Empty.WithWrite(WriteTargetKind.Parameter,
                $"订阅/退订输入参数的事件 {eventName}", loc),
            WriteTargetKind.Receiver when IsConstructorContext(method) => MethodSummary.Empty,
            WriteTargetKind.Receiver => MethodSummary.Empty.WithWrite(WriteTargetKind.Receiver,
                $"订阅/退订 receiver 的事件 {eventName}", loc),
            WriteTargetKind.Static => MethodSummary.Empty.WithEffect(ExternalEffectKind.StaticWrite,
                $"订阅/退订静态事件 {eventName}", loc),
            _ => MethodSummary.Empty.WithWrite(WriteTargetKind.Unknown,
                $"订阅/退订来源不确定的事件 {eventName}", loc),
        };
    }

    /// <summary>写目标分类（P2.3）：Fresh 允许，Parameter/Receiver/Static 违规，Unknown 保守。</summary>
    private MethodSummary ClassifyWriteTarget(IOperation target, string what, IMethodSymbol method, SemanticModel model)
    {
        var loc = LocationKey(target);
        while (true)
        {
            switch (target)
            {
                case IFieldReferenceOperation f:
                {
                    var field = f.Field;
                    if (field.IsStatic)
                    {
                        return field.IsReadOnly || field.IsConst
                            ? MethodSummary.Empty
                            : MethodSummary.Empty.WithEffect(ExternalEffectKind.StaticWrite,
                                $"{what}静态字段 {field.ContainingType?.ToDisplayString()}.{field.Name}", loc);
                    }
                    // 实例字段：区分 receiver 自身 / 参数 / fresh。
                    var kind = ClassifyInstanceTarget(f.Instance, method);
                    // ref/out 参数经别名指向调用方状态：其字段写入等同修改输入（审计第三轮 F13）。
                    if (kind == WriteTargetKind.Parameter && f.Instance is IParameterReferenceOperation rp
                        && rp.Parameter.RefKind != RefKind.None)
                    {
                        return MethodSummary.Empty.WithWrite(WriteTargetKind.Parameter,
                            $"{what}经 ref/out 参数 '{rp.Parameter.Name}' 的字段 {field.Name}", loc);
                    }
                    // 参数对象的字段写入同样修改调用方可见状态（审计第三轮 F13）：
                    // `c.N++` / `c.N = v` 修改的是调用方持有的对象。
                    if (kind == WriteTargetKind.Parameter && f.Instance is IParameterReferenceOperation pp)
                    {
                        return MethodSummary.Empty.WithWrite(WriteTargetKind.Parameter,
                            $"{what}输入参数 '{pp.Parameter.Name}' 的字段 {field.Name}", loc);
                    }
                    return kind switch
                    {
                        WriteTargetKind.Fresh => MethodSummary.Empty,
                        WriteTargetKind.Parameter => MethodSummary.Empty.WithWrite(WriteTargetKind.Parameter,
                            $"{what}输入参数的字段 {field.Name}", loc),
                        // 构造期对自身字段赋值是合法初始化（不可变值正是在此建立状态）。
                        // 仅构造完成后的写入才违反构造后稳定性。
                        WriteTargetKind.Receiver when IsConstructorContext(method) => MethodSummary.Empty,
                        WriteTargetKind.Receiver => MethodSummary.Empty.WithWrite(WriteTargetKind.Receiver,
                            $"{what}receiver 状态字段 {field.Name}", loc),
                        _ => MethodSummary.Empty.WithWrite(WriteTargetKind.Unknown,
                            $"{what}来源不可确定的字段 {field.Name}", loc),
                    };
                }
                case IPropertyReferenceOperation p:
                    if (p.Property.IsStatic)
                    {
                        // 构造期写静态属性也是外部状态写入（与实例字段不同，静态不是"自身初始化"）。
                        return MethodSummary.Empty.WithEffect(ExternalEffectKind.StaticWrite,
                            $"{what}静态属性 {p.Property.Name}", loc);
                    }
                    else
                    {
                        var propKind = ClassifyInstanceTarget(p.Instance, method);
                        // 构造期给自身属性赋值（含 get-only 自动属性 / init）是合法初始化。
                        if (propKind == WriteTargetKind.Receiver && IsConstructorContext(method))
                            return MethodSummary.Empty;
                        return MethodSummary.Empty.WithWrite(
                            propKind switch
                            {
                                WriteTargetKind.Fresh => WriteTargetKind.Fresh,
                                WriteTargetKind.Parameter => WriteTargetKind.Parameter,
                                WriteTargetKind.Receiver => WriteTargetKind.Receiver,
                                _ => WriteTargetKind.Unknown,
                            },
                            $"{what}属性 {p.Property.Name}", loc);
                    }

                case IArrayElementReferenceOperation arr:
                    target = arr.ArrayReference;
                    continue;

                case ILocalReferenceOperation:
                case IParameterReferenceOperation:
                    // 局部变量赋值本身不是外部修改（Fresh 分析在默认分支处理对象内容）。
                    return MethodSummary.Empty;

                case IInvocationOperation or IPropertyReferenceOperation:
                    return MethodSummary.Empty; // 由对应节点自身分类

                default:
                    return MethodSummary.Empty;
            }
        }
    }

    /// <summary>
    /// 判断实例来源：方法内新分配且未逃逸（Fresh）→ 允许修改。
    /// 本期为保守近似：局部变量持有 new 且未被传出 → Fresh；参数 → Parameter；其余 → Receiver/Unknown。
    /// </summary>
    private WriteTargetKind ClassifyInstanceTarget(IOperation? instance, IMethodSymbol method)
    {
        if (instance is null) return WriteTargetKind.Receiver;

        switch (instance)
        {
            case IParameterReferenceOperation p:
                // this 参数即 receiver。
                return p.Parameter.Ordinal == -1 ? WriteTargetKind.Receiver : WriteTargetKind.Parameter;
            case IInstanceReferenceOperation:
                return WriteTargetKind.Receiver;

            case ILocalReferenceOperation local:
                return ClassifyLocalAlias(local.Local, method);

            case IFieldReferenceOperation f when f.Field.IsStatic:
                return WriteTargetKind.Static;

            case IFieldReferenceOperation:
                return WriteTargetKind.Receiver;

            case IObjectCreationOperation:
                return WriteTargetKind.Fresh;

            case IInvocationOperation inv when inv.TargetMethod.ReturnType.IsReferenceType:
                // 工厂返回的对象：所有权不明 → Unknown（保守，不按 Fresh 放行）。
                return WriteTargetKind.Unknown;

            case IConversionOperation conv:
                return ClassifyInstanceTarget(conv.Operand, method);

            case IParenthesizedOperation paren:
                return ClassifyInstanceTarget(paren.Operand, method);

            default:
                return WriteTargetKind.Unknown;
        }
    }

    /// <summary>
    /// 局部变量的来源分类（审查 F-12）：
    ///   `var alias = _items;`   ⇒ Receiver（别名指向 receiver 的可变字段）
    ///   `var alias = input;`    ⇒ Parameter
    ///   `var x = new List<int>()` ⇒ Fresh（允许修改）
    /// 未识别的初始化形状 ⇒ Unknown（保守，不按 Fresh 放行）。
    /// </summary>
    private WriteTargetKind ClassifyLocalAlias(ILocalSymbol local, IMethodSymbol method)
    {
        // 后续重赋值扫描（审计第九轮 #3）：`var a = new List<int>(); if (flag) a = input; a.Add(99);`
        // —— 只要存在任何来自参数/receiver/静态/未知的重赋值，就不得保留 Fresh 资格。
        var tainted = FindLocalReassignmentTaint(local, method);
        if (tainted is not WriteTargetKind.Fresh) return tainted;

        foreach (var declRef in local.DeclaringSyntaxReferences)
        {
            var decl = declRef.GetSyntax();
            if (decl is not Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax v) continue;
            if (v.Initializer?.Value is null) return WriteTargetKind.Unknown; // 未初始化，来源不明

            // 用语义模型解析初始化表达式的来源符号（不用名称匹配）。
            var semanticModel = _compilation.GetSemanticModel(v.SyntaxTree);
            var initOp = semanticModel.GetOperation(v.Initializer.Value);
            if (initOp is null) return WriteTargetKind.Unknown;

            switch (initOp)
            {
                case IObjectCreationOperation:
                    return WriteTargetKind.Fresh;
                case IParameterReferenceOperation p:
                    return p.Parameter.Ordinal == -1 ? WriteTargetKind.Receiver : WriteTargetKind.Parameter;
                case IInstanceReferenceOperation:
                    return WriteTargetKind.Receiver;
                case IFieldReferenceOperation f:
                    return f.Field.IsStatic ? WriteTargetKind.Static : WriteTargetKind.Receiver;
                case IPropertyReferenceOperation pr:
                    return pr.Property.IsStatic ? WriteTargetKind.Static : WriteTargetKind.Receiver;
                case ILocalReferenceOperation innerLocal:
                    // 局部链：递归解析（A = B; C = A;），带深度上限防环。
                    return ClassifyLocalAlias(innerLocal.Local, method);
                case IConversionOperation conv:
                    return conv.Operand is null ? WriteTargetKind.Unknown : ClassifyLocalAliasFromOperation(conv.Operand, method);
                case IInvocationOperation inv2 when IsKnownCopyProducer(inv2):
                    // `input.ToList()/ToArray()`/拷贝构造 产出新对象 ⇒ Fresh（审计第十轮 FP11：
                    // `var copy = input.ToList(); copy.Sort();` 此前把 copy 判为 Unknown，
                    // 再把 Sort 报成"修改来源不可确定的集合"）。
                    return WriteTargetKind.Fresh;
                default:
                    return WriteTargetKind.Unknown;
            }
        }
        return WriteTargetKind.Unknown;
    }

    /// <summary>调用是否为已知"拷贝产生器"（返回与源不共享存储的新集合）。</summary>
    private static bool IsKnownCopyProducer(IInvocationOperation inv)
    {
        var type = CanonicalTypeName(inv.TargetMethod.ContainingType);
        var member = inv.TargetMethod.Name;
        if (type == "System.Collections.Generic.List`1"
            && member is "ToList" or "ToArray") return true;
        if (type == "System.Linq.Enumerable"
            && member is "ToList" or "ToArray") return true;
        // 拷贝构造：new List<T>(src) / new Dictionary<K,V>(src)
        if (inv.TargetMethod.MethodKind == MethodKind.Constructor
            && inv.Arguments.Length == 1
            && (type == "System.Collections.Generic.List`1"
                || type == "System.Collections.Generic.Dictionary`2"
                || type == "System.Collections.Generic.HashSet`1"))
            return true;
        return false;
    }

    /// <summary>操作树来源分类（局部链递归用；深度受调用方控制）。</summary>
    private WriteTargetKind ClassifyLocalAliasFromOperation(IOperation op, IMethodSymbol method) => op switch
    {
        IObjectCreationOperation => WriteTargetKind.Fresh,
        IParameterReferenceOperation p => p.Parameter.Ordinal == -1 ? WriteTargetKind.Receiver : WriteTargetKind.Parameter,
        IInstanceReferenceOperation => WriteTargetKind.Receiver,
        IFieldReferenceOperation f => f.Field.IsStatic ? WriteTargetKind.Static : WriteTargetKind.Receiver,
        IPropertyReferenceOperation pr => pr.Property.IsStatic ? WriteTargetKind.Static : WriteTargetKind.Receiver,
        _ => WriteTargetKind.Unknown,
    };

    private MethodSummary ClassifyCreation(IObjectCreationOperation creation, IMethodSymbol method, SemanticModel model)
        => MethodSummary.Empty;

    /// <summary>
    /// 返回值逃逸分类（审查 F-07/F-09）：
    /// 返回输入参数的可变引用 = 保留调用方可变别名；返回 this = receiver 逃逸。
    /// </summary>
    private MethodSummary ClassifyReturn(IReturnOperation ret, IMethodSymbol method)
    {
        if (ret.ReturnedValue is null) return MethodSummary.Empty;

        // 返回不可变标量/string（或其字段读取）不是任何形式的逃逸。
        // 例如 `public int X => _x;` —— _x 是 this 的字段，但 int 不携带别名。
        if (!IsMutableReferenceType(ret.ReturnedValue.Type))
        {
            // 例外：返回 this 本身（object 等引用类型）仍按下面的 origin 判定处理。
            if (ret.ReturnedValue is not IInstanceReferenceOperation) return MethodSummary.Empty;
        }

        var origin = ClassifyValueOrigin(ret.ReturnedValue, method);

        // 返回 this 本身 = receiver 逃逸（把自身引用交出破坏构造后封装）。
        // 但返回 this 的**不可变字段**（如 `public int X => _x;` / `public IReadOnlyList<Imm> L => _l;`）
        // 不是逃逸：该字段的类型已保证不携带可变别名（审计第三轮回归修正）。
        if (origin == WriteTargetKind.Receiver)
        {
            // 仅"返回 this 本身"在此判定为 receiver 逃逸。
            // 返回 this 的**字段/属性**（`public IReadOnlyList<X> L => _l;`）的可变性判定
            // 需要完整类型知识（元素是否已声明不可变），由引擎的对外表面检查统一负责，
            // 此处若重复判定会因缺少符号解析而产生假红（审计第三轮回归修正）。
            if (IsThisValue(ret.ReturnedValue))
                return new MethodSummary { ReturnsInputAlias = true, EscapesReceiver = true };
            return MethodSummary.Empty;
        }

        // 返回输入参数的可变引用 = 调用方仍持有同一可变对象（审查 F-07）。
        if (origin == WriteTargetKind.Parameter && IsMutableReferenceType(ret.ReturnedValue.Type))
            return new MethodSummary { ReturnsInputAlias = true, EscapesParameters = true };

        return MethodSummary.Empty;
    }

    /// <summary>
    /// using / using 声明：对被释放对象调用 Dispose（隐式调用点，审查 F-05）。
    /// 登记 Dispose 供调用图传播；无法解析时记 Unknown，绝不静默当作无副作用。
    /// </summary>
    private MethodSummary ClassifyUsing(IUsingOperation usingOp, IMethodSymbol method, SemanticModel model)
    {
        var loc = LocationKey(usingOp);
        var disposedType = ResolveUsingResourceType(usingOp.Resources);

        if (disposedType is null)
        {
            return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                "using 资源类型无法确定，Dispose 行为未分析", loc);
        }

        var dispose = disposedType.GetMembers("Dispose")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length == 0);

        // 显式接口实现（`void IDisposable.Dispose()`）不在普通成员表里：经接口映射解析到实现。
        if (dispose is null && disposedType is INamedTypeSymbol named)
        {
            foreach (var iface in named.AllInterfaces)
            {
                var ifaceDispose = iface.GetMembers("Dispose").OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.Parameters.Length == 0);
                if (ifaceDispose is null) continue;
                dispose = named.FindImplementationForInterfaceMember(ifaceDispose) as IMethodSymbol;
                if (dispose is not null) break;
            }
        }
        if (dispose is null)
        {
            return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                $"using 资源 {disposedType.ToDisplayString()} 未找到 Dispose，行为未分析", loc);
        }

        if (dispose.DeclaringSyntaxReferences.Length > 0)
            return MethodSummary.Empty.With(new MethodSummary
            {
                RequiresPropagation = ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, dispose),
            });

        // 外部 IDisposable 实现：无摘要 ⇒ Unknown（不默认无副作用）。
        return MethodSummary.Empty.WithUnknown(UnknownReason.ExternalSummaryMissing,
            $"using 外部资源 {disposedType.ToDisplayString()}.Dispose 无行为摘要", loc);
    }

    /// <summary>
    /// foreach 的隐式调用传播（审查 F-04）：
    /// 枚举模式需要 GetEnumerator()（或 IEnumerable.GetEnumerator）、MoveNext()、Current。
    /// 三种都登记为需传播成员；无法解析时记 Unknown，绝不静默当作无副作用。
    /// 注意：BCL 集合（List/数组等）的枚举器在目录中不登记，但它们是安全的，
    /// 故仅在**用户自定义**枚举形状上强制传播，避免给常见循环制造噪声。
    /// </summary>
    private MethodSummary ClassifyForEach(IForEachLoopOperation forEach, IMethodSymbol method, SemanticModel model)
    {
        var loc = LocationKey(forEach);
        // 数组 foreach 的集合呈现为非泛型 IEnumerable（隐式转换）——解包到真实类型，
        // 否则 int[] 等最常见循环全部落 Unknown（审计第十轮 FP1）。
        var collectionRaw = forEach.Collection;
        while (collectionRaw is IConversionOperation fc) collectionRaw = fc.Operand;
        var collectionType = collectionRaw?.Type;
        if (collectionType is null)
        {
            return MethodSummary.Empty.WithUnknown(UnknownReason.UnsupportedOperation,
                "foreach 集合类型无法确定，枚举器行为未分析", loc);
        }

        // 已知 BCL 集合/数组：枚举器无用户效应，不产生噪声（也不声称做了额外检查）。
        if (IsKnownSafeEnumerable(collectionType))
            return MethodSummary.Empty;

        var toPropagate = ImmutableHashSet.CreateBuilder<IMethodSymbol>(SymbolEqualityComparer.Default);
        var unresolved = new List<string>();

        var enumerator = FindEnumeratorMethod(collectionType);
        if (enumerator is null)
        {
            unresolved.Add($"{collectionType.ToDisplayString()}.GetEnumerator");
        }
        else
        {
            // GetEnumerator 自身有源码 ⇒ 一律传播（其调用点效应可见）。
            if (enumerator.DeclaringSyntaxReferences.Length > 0)
                toPropagate.Add(enumerator);
            else if (enumerator.ContainingType?.TypeKind != TypeKind.Interface)
                unresolved.Add($"{collectionType.ToDisplayString()}.GetEnumerator（外部无摘要）");


            // GetEnumerator 返回 BCL 接口 ⇒ 实际枚举器是用户实现（其 MoveNext/Dispose
            // 可能携带效应），静态无法闭合目标 ⇒ 追加 Unknown（审计第九轮 #15：
            // `IEnumerator<int> GetEnumerator() { return new Inner(this); }` 的 Inner.Dispose）。
            // 注意：这只**追加**未知，不得吞掉 GetEnumerator 自身已传播的效应。
            if (enumerator.ReturnType is INamedTypeSymbol enRet
                && enRet.TypeKind == TypeKind.Interface
                && !IsKnownSafeEnumerable(enRet))
                unresolved.Add($"foreach 枚举器 {enRet.ToDisplayString()} 为接口，实际实现不可静态闭合");
        }

        if (enumerator is not null && enumerator.ContainingType?.TypeKind == TypeKind.Interface)
        {
            // 接口方法：解析到集合类型的实现（含显式接口实现）。
            if (collectionType is INamedTypeSymbol named
                && named.FindImplementationForInterfaceMember(enumerator) is IMethodSymbol impl
                && impl.DeclaringSyntaxReferences.Length > 0)
                toPropagate.Add(impl);
            else
                unresolved.Add($"{collectionType.ToDisplayString()}.GetEnumerator（接口实现不可解析）");
        }
        // 模式式枚举器（非接口）已在上面分流：有源码 ⇒ 已传播；无源码 ⇒ 已记 Unknown。
        // 此处**不得**再无条件补一条 Unknown —— 那会让每个用户自定义枚举器都凭空多一条
        // 噪声（用户视角审计实测：E.GetEnumerator 明明有源码却报"外部无摘要"）。

        // 枚举器元素类型上的 MoveNext/Current（模式式枚举器返回自身类型）。
        // Current 通常以**属性**形态出现（审计第九轮 #13：只搜方法恒为空）。
        var enumeratorType = enumerator?.ReturnType ?? collectionType;
        foreach (var memberName in new[] { "MoveNext", "Current" })
        {
            var member = enumeratorType.GetMembers(memberName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Parameters.Length == 0);
            if (member is not null)
            {
                if (member.DeclaringSyntaxReferences.Length > 0) toPropagate.Add(member);
                continue;
            }
            // 属性形态：登记其 getter。
            var propMember = enumeratorType.GetMembers(memberName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();
            var getter = propMember?.GetMethod;
            if (getter is { DeclaringSyntaxReferences.Length: > 0 }) toPropagate.Add(getter);
        }

        // foreach 结束时必然调用枚举器的 Dispose（审计第九轮 #15）：
        // 模式式（公共 Dispose 方法）或 IDisposable 实现（含显式接口实现）。
        var enumeratorDispose = enumeratorType.GetMembers("Dispose")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length == 0);
        if (enumeratorDispose is null && enumeratorType is INamedTypeSymbol en)
        {
            foreach (var iface in en.AllInterfaces)
            {
                var id = iface.GetMembers("Dispose").OfType<IMethodSymbol>()
                    .FirstOrDefault(x => x.Parameters.Length == 0);
                if (id is null) continue;
                enumeratorDispose = en.FindImplementationForInterfaceMember(id) as IMethodSymbol;
                if (enumeratorDispose is not null) break;
            }
        }
        if (enumeratorDispose is { DeclaringSyntaxReferences.Length: > 0 })
            toPropagate.Add(enumeratorDispose);

        var summary = MethodSummary.Empty.With(new MethodSummary { RequiresPropagation = toPropagate.ToImmutable() });
        foreach (var u in unresolved)
        {
            summary = summary.WithUnknown(UnknownReason.ExternalSummaryMissing,
                $"foreach 枚举器 {u} 无行为摘要", loc);
        }
        return summary;
    }

    /// <summary>查找枚举方法：优先模式式 `GetEnumerator()`，否则 IEnumerable/IEnumerable&lt;T&gt; 实现。</summary>
    private static IMethodSymbol? FindEnumeratorMethod(ITypeSymbol collectionType)
    {
        var direct = collectionType.GetMembers("GetEnumerator")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length == 0);
        if (direct is not null) return direct;

        foreach (var iface in collectionType.AllInterfaces)
        {
            if (iface.SpecialType is SpecialType.System_Collections_IEnumerable
                || (iface.IsGenericType && iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
            {
                var m = iface.GetMembers("GetEnumerator").OfType<IMethodSymbol>()
                    .FirstOrDefault(x => x.Parameters.Length == 0);
                if (m is not null) return m;
            }
        }
        return null;
    }

    /// <summary>
    /// 是否为已知安全的可枚举（数组、BCL 集合）。这些类型的枚举器不携带用户副作用，
    /// 不登记传播以免对每个 `foreach` 都产生噪声；这**不是**声称做了额外检查。
    /// </summary>
    private static bool IsKnownSafeEnumerable(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol) return true;
        // 非泛型 IEnumerable 是数组/字符串 foreach 的呈现形态（审计第十轮 FP1）。
        if (type.OriginalDefinition.ToDisplayString() is "System.Collections.IEnumerable") return true;
        // Dictionary 值/键集合（审计第十轮 D19：Values foreach 曾落"外部无摘要"）。
        if (type.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.Dictionary<TKey, TValue>.ValueCollection"
            or "System.Collections.Generic.Dictionary<TKey, TValue>.KeyCollection") return true;
        var name = type.OriginalDefinition?.ToDisplayString();
        return name is "System.Collections.Generic.List<T>"
            or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.IEnumerable<T>"
            or "System.Collections.Generic.ICollection<T>"
            or "System.Collections.Generic.IReadOnlyList<T>"
            or "System.Collections.Generic.IReadOnlyCollection<T>"
            or "System.Collections.Generic.Dictionary<TKey, TValue>"
            or "System.Collections.Generic.HashSet<T>"
            or "System.Collections.Generic.Queue<T>"
            or "System.Collections.Generic.Stack<T>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            or "System.String";
    }

    /// <summary>
    /// 解析 using 资源的静态类型。Resources 的 Type 常为 null——
    /// `using (var d = new D())` 得到 VariableDeclarationGroup，需下钻到声明符/初始值。
    /// </summary>
    private static ITypeSymbol? ResolveUsingResourceType(IOperation? resources) => resources switch
    {
        null => null,
        _ when resources.Type is not null => resources.Type,
        IVariableDeclaratorOperation d => d.Symbol.Type,
        IVariableDeclarationOperation decl => decl.Declarators.FirstOrDefault()?.Symbol.Type,
        IVariableInitializerOperation init => init.Value?.Type,
        _ => resources.ChildOperations
                .Select(ResolveUsingResourceType)
                .FirstOrDefault(t => t is not null),
    };

    /// <summary>类型是否为委托/函数值（用于"存入字段 = 逃逸"判定）。</summary>
    private static bool IsDelegateType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Delegate };

    /// <summary>该方法是否为构造函数/静态构造（构造期对自身字段赋值是合法初始化）。</summary>
    private static bool IsConstructorContext(IMethodSymbol method) =>
        method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor;

    /// <summary>该实参的值是否直接或间接来源于 this（构造期 this 逃逸判据）。</summary>
    private static bool ArgumentCarriesReceiver(IOperation? argValue)
    {
        if (argValue is null) return false;
        return argValue switch
        {
            IInstanceReferenceOperation => true,
            IFieldReferenceOperation f => f.Instance is null || ArgumentCarriesReceiver(f.Instance),
            IPropertyReferenceOperation p => ArgumentCarriesReceiver(p.Instance),
            IArgumentOperation a => ArgumentCarriesReceiver(a.Value),
            IParenthesizedOperation paren => ArgumentCarriesReceiver(paren.Operand),
            IConversionOperation conv => ArgumentCarriesReceiver(conv.Operand),
            _ => false,
        };
    }

    /// <summary>
    /// 目录键的类型规范名：**元数据全名**（`System.Int32`）而非 C# 关键字别名（`int`），
    /// 且用泛型定义（`List<T>`）而非构造实例（`List<int>`）。
    /// 两者不一致会让最常见的安全成员（`List<T>.ToArray` / `int.ToString`）查不到而落 Unknown。
    /// </summary>
    private static string? CanonicalTypeName(INamedTypeSymbol? type)
    {
        if (type is null) return null;
        var def = type.OriginalDefinition;
        var ns = def.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? def.MetadataName : ns + "." + def.MetadataName;
    }

    private static bool IsSystemNamespace(string? typeName) =>
        typeName is not null && (typeName.StartsWith("System.", System.StringComparison.Ordinal)
            || typeName.StartsWith("System", System.StringComparison.Ordinal));

    /// <summary>虚/接口调用且无法在源码内闭合 → 开放分派。</summary>
    private static bool IsOpenDispatch(IMethodSymbol target)
    {
        if (target.IsVirtual || target.IsAbstract) return true;
        var containing = target.ContainingType;
        if (containing is null) return false;
        if (containing.TypeKind == TypeKind.Interface) return true;
        return false;
    }
}

/// <summary>构建摘要时用于区分方法种类（构造期语义等）。</summary>
public readonly record struct ContractProfileKindHint(bool IsConstructorContext);
