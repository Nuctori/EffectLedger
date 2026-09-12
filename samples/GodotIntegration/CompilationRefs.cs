// CompilationRefs.cs — 分析器测试的共享轻量引用集（R3 审计：12 份「AppDomain 全量扫描」拷贝收敛到单一真源）。
// 全量扫描把 testhost 已加载的 Roslyn/xunit/runner 等上百个程序集逐个做成 MetadataReference，
// 内存放大 1~2 个数量级——solution 级多 testhost 并发叠加机器内存压力时曾把 commit 内存压到 OOM
//（MetadataReference.CreateFromFile 抛 OutOfMemoryException，28 个偶发假红）。
// 此处与 tests/EffectLedger.Tests 现有驱动同款的 TPA 精选模式：只挂编译所需的最小集合。
using System;
using System.Collections.Generic;
using System.IO;
using EffectLedger;
using Microsoft.CodeAnalysis;

namespace SampleGame.IntegrationTests;

internal static class CompilationRefs
{
    /// <summary>分析器驱动源码所需的最小引用集；extra 追加特殊引用（如独立解析的 L1 DLL、本项目 Godot 桩程序集），按路径去重。</summary>
    public static List<MetadataReference> Lean(params string[] extra)
    {
        var refs = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string path)
        {
            if (seen.Add(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }
        Add(typeof(object).Assembly.Location);
        Add(typeof(Claim).Assembly.Location);
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name is "System.Runtime" or "System.Collections.Immutable" or "System.Linq" or "System.Collections" or "netstandard")
                Add(p);
        }
        foreach (var e in extra)
            Add(e);
        return refs;
    }
}
