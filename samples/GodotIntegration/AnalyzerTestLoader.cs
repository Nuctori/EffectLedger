using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleGame.IntegrationTests;

/// <summary>
/// 加载 L3 分析器用于 WithAnalyzers 驱动（E2E 测试）。
/// 分析器以 OutputItemType="Analyzer" 接入（ReferenceOutputAssembly=false），其类型不在测试编译单元内可见，
/// 故通过反射加载其 DLL 并实例化 —— 与真实编译期加载路径一致（非桩）。
/// 注意：分析器内嵌的 L1 副本位于 EffectLedger.Analyzer.Shared 命名空间，与 L1 本体的 EffectLedger
/// 不冲突，故加载进默认 ALC 不会引发 ResourceId/Signature 同名 (CS0433)。
/// </summary>
internal static class AnalyzerTestLoader
{
    public static ImmutableArray<DiagnosticAnalyzer> LoadAnalyzers(string language = "C#")
    {
        var asm = typeof(AnalyzerTestLoader).Assembly;
        var dir = Path.GetDirectoryName(asm.Location)
                  ?? throw new InvalidOperationException("cannot resolve test assembly directory");
        var analyzerDll = Path.Combine(dir, "EffectLedger.Analyzer.dll");
        if (!File.Exists(analyzerDll))
            throw new FileNotFoundException("L3 analyzer assembly not found in output dir", analyzerDll);

        var analyzerAsm = Assembly.LoadFrom(analyzerDll);
        var analyzers = analyzerAsm.GetTypes()
            .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!)
            .ToImmutableArray();
        if (analyzers.IsEmpty)
            throw new InvalidOperationException("no DiagnosticAnalyzer found in L3 assembly");
        return analyzers;
    }

    public static DiagnosticAnalyzer LoadAnalyzer() => LoadAnalyzers().First();
}
