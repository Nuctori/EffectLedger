// BuildProbe.cs — P5.2 从真实构建抽取编译输入。
// 实现：调用 dotnet build 执行自定义 MSBuild 目标 EffectLedgerDumpCompileInputs，
// 由该目标把真实编译器看到的源文件/引用/预处理符号/语言版本写到临时 JSON。
// 这样工具使用的是**真实构建输入**，不是目测或另建一份文件清单（R-GATE-02）。

using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EffectLedger.Contracts.Tool;

internal sealed class BuildProbe
{
    /// <summary>目标工程引用的分析器/生成器程序集（生成源一致性判定用）。</summary>
    internal IReadOnlyList<string> GeneratorsReferenced { get; private set; } = Array.Empty<string>();

    /// <summary>工程的 TargetFrameworks 列表（多目标且未指定 -f 时工具必须拒绝）。</summary>
    internal IReadOnlyList<string> MultiTargetFrameworks { get; private set; } = Array.Empty<string>();

    public CompileInputs? GetCompileInputs(string project, string configuration, string? framework)
    {
        var outFile = Path.Combine(Path.GetTempPath(), "effectledger-contracts-" + Guid.NewGuid().ToString("N") + ".json");
        var targetsFile = Path.Combine(AppContext.BaseDirectory, "DumpCompileInputs.targets");
        if (!File.Exists(targetsFile))
        {
            Console.Error.WriteLine($"error: 缺少 MSBuild 目标文件 {targetsFile}");
            return null;
        }

        // 先常规构建（让依赖工程 DLL 落盘，ReferencePath 才完整），再执行 dump 目标。
        var buildArgs = new List<string> { "build", project, "-c", configuration, "-nologo", "-v:quiet", "-p:NuGetAudit=false" };
        if (framework is not null) { buildArgs.Add("-f"); buildArgs.Add(framework); }
        var (bOut, bErr, bExit) = RunDotnet(buildArgs);
        if (bExit != 0)
        {
            Console.Error.WriteLine("error: dotnet build 失败，无法在真实构建状态下验证。");
            if (!string.IsNullOrWhiteSpace(bOut)) Console.Error.WriteLine(bOut.Trim());
            if (!string.IsNullOrWhiteSpace(bErr)) Console.Error.WriteLine(bErr.Trim());
            return null;
        }

        var args = new List<string>
        {
            "build", project,
            "-c", configuration,
            "-t:EffectLedgerDumpCompileInputs",
            $"-p:EffectLedgerDumpFile={outFile}",
            "-p:CustomAfterMicrosoftCommonTargets=" + targetsFile,
            "-p:NuGetAudit=false",
            "-nologo", "-v:quiet",
        };
        if (framework is not null) { args.Add("-f"); args.Add(framework); }

        var (stdout, stderr, dumpExit) = RunDotnet(args);
        if (dumpExit != 0)
        {
            // dump 目标失败：不得沿用上一次的残留文件当本次输入（审查 F6）。
            Console.Error.WriteLine($"error: 编译输入 dump 目标失败（exit {dumpExit}）。");
            if (!string.IsNullOrWhiteSpace(stdout)) Console.Error.WriteLine(stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr.Trim());
            return null;
        }

        var metaFile = outFile + ".meta";
        var srcFile = outFile + ".src";
        var refFile = outFile + ".ref";
        var genFile = outFile + ".gen";
        if (!File.Exists(metaFile) || !File.Exists(srcFile) || !File.Exists(refFile))
        {
            Console.Error.WriteLine("error: 构建未产生编译输入（MSBuild 失败或目标未执行）。");
            if (!string.IsNullOrWhiteSpace(stdout)) Console.Error.WriteLine(stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr.Trim());
            return null;
        }

        // 先读取，再清理（顺序反了会删掉待读文件）。
        var sources = File.ReadAllLines(srcFile).Where(l => l.Length > 0).ToList();
        var refs = File.ReadAllLines(refFile).Where(l => l.Length > 0).ToList();
        var meta = File.ReadAllText(metaFile);

        // 生成源一致性（审查 F-06）：分析器能看到生成器产出的类型，而 dump 的 @(Compile)
        // 未必包含生成文件 ⇒ 工具可能报"0 个受约束根"而分析器报违规（两套答案）。
        // 注意：@(Analyzer) 同时含纯分析器（如 SDK 内置 NetAnalyzers），它们不产生源；
        // 只有真正含 ISourceGenerator/IIncrementalGenerator 的程序集才引入该风险，
        // 否则工具对每个现代工程都 fail-closed，等于不可用。
        GeneratorsReferenced = File.Exists(genFile)
            ? File.ReadAllLines(genFile).Where(l => l.Length > 0).Where(ContainsSourceGenerator).ToList()
            : new List<string>();

        // 审计第十轮（门禁头条）：RAR 未跑时 @(ReferencePath) 为空 ⇒ 重建编译必然满屏 CS0246，
        // 被误报为"目标工程编译错误"。现在必须 loud 失败，而不是静默降级到 bin 扫描。
        if (refs.Count == 0)
        {
            Console.Error.WriteLine("error: 编译输入中引用列表为空（ResolveAssemblyReferences 未执行）——拒绝在降级输入上给出结论。");
            return null;
        }

        // ReferencePath 不含 SDK 隐式引入的框架引用（System.Private.CoreLib 等）。
        // 真实构建由 SDK 解析；工具侧补齐运行时 ref 程序集目录，使重建编译与真实构建一致。
        refs.AddRange(DiscoverFrameworkReferences());

        // 目标工程的 ProjectReference 输出（如 EffectLedger.Contracts.dll）在 dump 目标单独调用时
        // 可能未进入 ReferencePath；显式补充同次常规构建已落盘的依赖 DLL。
        refs.AddRange(DiscoverBuiltProjectReferences(project, configuration));

        try
        {
            File.Delete(outFile); File.Delete(metaFile); File.Delete(srcFile); File.Delete(refFile); File.Delete(genFile);
        }
        catch { /* 临时文件清理失败不影响结论 */ }

        var defines = Array.Empty<string>();
        string? langVersion = null;
        var nullable = NullableContextOptions.Disable;
        foreach (var part in meta.Split(':'))
        {
            if (part.StartsWith("lang=")) langVersion = part["lang=".Length..];
            else if (part.StartsWith("nullable=")) nullable = part["nullable=".Length..] == "enable" ? NullableContextOptions.Enable : NullableContextOptions.Disable;
            else if (part.StartsWith("defines=")) defines = part["defines=".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries);
            else if (part.StartsWith("tfms=")) MultiTargetFrameworks = part["tfms=".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        LanguageVersion lv = LanguageVersion.Latest;
        if (langVersion is not null && LanguageVersionFacts.TryParse(langVersion, out var parsed))
            lv = parsed;

        var parse = new CSharpParseOptions(
            languageVersion: lv,
            documentationMode: DocumentationMode.None,
            preprocessorSymbols: defines);

        return new CompileInputs(sources, refs, parse, nullable, defines, GeneratorsReferenced, MultiTargetFrameworks);
    }

    private static (string Stdout, string Stderr, int Exit) RunDotnet(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (stdout, stderr, p.ExitCode);
    }

    /// <summary>
    /// 补齐框架引用：ReferencePath 只含工程/包引用，不含 SDK 隐式的 BCL 引用。
    /// 从当前运行时目录解析（工具自身在 net10.0 上运行），使重建编译能解析 System.*。
    /// 与真实构建共用的包/工程引用仍来自 MSBuild dump（未被本方法替代）。
    /// </summary>
    /// <summary>
    /// 常规构建已让依赖工程 DLL 落盘；补回 dump 目标单独调用时未进入 ReferencePath 的项目引用输出。
    /// 只接受与目标工程同目录树下的 bin 输出，避免纳入无关程序集。
    /// </summary>
    private static IEnumerable<string> DiscoverBuiltProjectReferences(string project, string configuration)
    {
        var result = new List<string>();
        try
        {
            var projectDir = Path.GetDirectoryName(Path.GetFullPath(project))!;
            var binDir = Path.Combine(projectDir, "bin", configuration);
            if (Directory.Exists(binDir))
                foreach (var dll in Directory.EnumerateFiles(binDir, "*.dll", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    if (name.Contains("EffectLedger", StringComparison.Ordinal))
                        result.Add(dll);
                }
        }
        catch { /* 探测失败不影响主流程 */ }
        return result;
    }

    private static IEnumerable<string> DiscoverFrameworkReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir is null) yield break;
        foreach (var dll in Directory.EnumerateFiles(runtimeDir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            // 只补 BCL 面，避免把工具自身的 Roslyn/依赖误当目标引用；
            // PE 原生映像（.Native.dll）无托管元数据，跳过避免 CS0009。
            if (!name.EndsWith(".Native", StringComparison.Ordinal)
                && (name.StartsWith("System.", StringComparison.Ordinal)
                    || name is "System" or "netstandard" or "mscorlib" or "Microsoft.CSharp"))
                yield return dll;
        }
    }

    /// <summary>
    /// 程序集是否为源生成器程序集。离线且无 MetadataLoadContext 时无法可靠区分
    /// "含生成器"与"仅引用 ISourceGenerator 类型的分析器"，故**不**以此判定（避免对每个现代工程误报）。
    /// 生成源的一致性改由 GenerateSourcePresence 以"源文件中是否含 obj/ 生成产物"精确判定。
    /// </summary>
    // .NET SDK 随每个工程隐式注入的分析器/生成器（非用户引入，不构成"生成源不可见"风险）。
    private static readonly HashSet<string> SdkBuiltinAnalyzerAssemblies = new(StringComparer.Ordinal)
    {
        "Microsoft.CodeAnalysis.NetAnalyzers",
        "Microsoft.Interop.SourceGeneration",
        "System.Text.Json.SourceGeneration",
        "System.Text.RegularExpressions.Generator",
        "System.Text.Composition.SourceGeneration",
        "Microsoft.CodeAnalysis.CSharp.NetAnalyzers",
        "Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers",
        // LibraryImport/JSImport/UnmanagedCallersOnly 等 interop 生成器同样随 SDK 注入。
        "Microsoft.Interop.JavaScript.JSImportGenerator",
        "Microsoft.Interop.LibraryImportGenerator",
        "Microsoft.Interop.ComSourceGenerator",
        "Microsoft.Interop.UnmanagedCallersOnlyGenerator",
    };

    private static bool ContainsSourceGenerator(string assemblyPath)
    {
        // 实装（审计第十轮）：此前恒 false ⇒ fail-closed 分支为死代码。
        // 判据（路径优先于名单）：SDK 内置分析器/生成器统一位于
        // `<sdk>/Sdks/Microsoft.NET.Sdk/analyzers/`，路径含该目录即内置
        // ——覆盖现在与未来所有内置项，无需逐个枚举（ComInterfaceGenerator 教训）。
        var norm = assemblyPath.Replace('\\', '/');
        // 内置来源有两处：SDK 的 Sdks 目录，以及目标平台包 microsoft.netcore.app.ref
        // （interop 系列生成器实际住在这里——审计实测）。两者都随平台提供，非用户引入。
        if (norm.Contains("/Sdks/Microsoft.NET.Sdk/analyzers/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (norm.Contains("microsoft.netcore.app.ref/", StringComparison.OrdinalIgnoreCase)
            && norm.Contains("/analyzers/", StringComparison.OrdinalIgnoreCase))
            return false;

        var name = Path.GetFileNameWithoutExtension(assemblyPath);
        if (SdkBuiltinAnalyzerAssemblies.Contains(name)) return false;
        if (name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)) return false;
        // 其余未知程序集按"可能是生成器"处理（fail-closed 方向），--allow-generators 显式放行。
        return true;
    }

    /// <summary>
    /// 目标编译的源集合中是否存在生成产物（位于 obj/ 下）。
    /// 生成器产物落在 intermediate 目录；@(Compile) 若已包含它们则工具看得见生成源。
    /// 若工程引用了分析器但我们一个 obj/ 源都没看到，无法断言生成源已被覆盖 ⇒ 交由调用方 fail-closed。
    /// </summary>
    internal static bool HasGeneratedSourcesIn(IReadOnlyList<string> sources) =>
        sources.Any(p =>
        {
            var norm = p.Replace('\\', '/');
            return norm.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
        });
}
