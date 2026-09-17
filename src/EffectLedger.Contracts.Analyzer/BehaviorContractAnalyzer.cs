// BehaviorContractAnalyzer.cs — Roslyn 入口（P1.2/P5.4）。
// 只发根因诊断；计算全部委托给 ContractEngine（与 Tool 共用，保证两套入口同一语义）。

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Diagnostics;
using EffectLedger.Contracts.Analyzer.Engine;
using EffectLedger.Contracts.Analyzer.Profiles;

namespace EffectLedger.Contracts.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BehaviorContractAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        ContractDiagnostics.InvalidProfile,
        ContractDiagnostics.ImmutableMutatingWrite,
        ContractDiagnostics.ImmutableAliasEscape,
        ContractDiagnostics.ImmutableCtorEscape,
        ContractDiagnostics.DeterministicHiddenInput,
        ContractDiagnostics.DeterministicExternalWrite,
        ContractDiagnostics.DeterministicEntryCondition,
        ContractDiagnostics.UnknownDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(compilationContext =>
        {
            var compilation = compilationContext.Compilation;
            var resolver = new ProfileResolver(compilation);
            var engine = new ContractEngine(compilation, AnalysisBudget.Default);
            foreach (var decl in resolver.FindDeclarations())
            {
                var result = engine.Evaluate(decl);
                foreach (var d in result.Violations) compilationContext.ReportDiagnostic(d);
                // 未知：可见但不阻断（strict 由 Tool 判定）。
                foreach (var u in result.Unknowns)
                {
                    compilationContext.ReportDiagnostic(Diagnostic.Create(ContractDiagnostics.UnknownDependency,
                        decl.Location, decl.Type.Name, $"{u.Reason}: {u.Description}"));
                }
            }
        });
    }
}
