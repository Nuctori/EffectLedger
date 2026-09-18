// Program.cs — P5 严格审核 CLI（effectledger-contracts check）。
// 关键（R-GATE-01/02/09）：复用与分析器完全相同的 ContractEngine；
// 通过真实构建获取编译输入（不再依赖 IDE 显示的诊断），按覆盖策略判定。
// 目标编译失败时（含生成器失败）直接失败，不假装得到编译对象。

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Analyzer.Diagnostics;
using EffectLedger.Contracts.Analyzer.Engine;
using EffectLedger.Contracts.Analyzer.Profiles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EffectLedger.Contracts.Tool;

public static class Program
{
    public static int Main(string[] args)
    {
        var opt = ParseArgs(args);
        if (opt.ShowHelp) { PrintHelp(); return 0; }
        if (opt.InvalidUsage) { PrintHelp(); return 1; }
        if (opt.Project is null)
        {
            // 缺工程参数是用法错误，不是成功（审查 F7："未检查"绝不能退出 0）。
            Console.Error.WriteLine("error: 缺少 <project.csproj> 参数。用法见 --help。");
            PrintHelp();
            return 1;
        }

        var probe = new BuildProbe();
        var inputs = probe.GetCompileInputs(opt.Project, opt.Configuration, opt.Framework);
        if (inputs is null)
        {
            Console.Error.WriteLine("error: 无法从真实构建获取编译输入（构建失败或 MSBuild 不可用）。");
            return 1;
        }

        var compilation = BuildCompilation(inputs, out var compileErrors);
        if (compileErrors)
        {
            Console.Error.WriteLine("error: 目标工程编译存在错误，无法在真实构建状态下验证约束。");
            foreach (var d in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(15))
                Console.Error.WriteLine("  " + d);
            return 1;
        }

        // 生成源一致性（审查 F-06）：若工程引用了源生成器、而本次 dump 的源集合里**没有任何
        // obj/ 生成产物**，则无法保证生成源已被纳入 ⇒ 存在"工具报 0 根而 build 报违规"的假绿通道。
        // 精确判据（不误伤只引用分析器的普通工程）：存在生成器产物即视为已覆盖；
        // 引用了生成器却一个都没看到 ⇒ fail-closed，须用 --allow-generators 显式确认。
        var hasGeneratedSources = BuildProbe.HasGeneratedSourcesIn(inputs.Sources);
        if (inputs.GeneratorsReferenced.Count > 0 && !hasGeneratedSources && !opt.AllowGenerators)
        {
            Console.Error.WriteLine(
                $"error: 工程引用了 {inputs.GeneratorsReferenced.Count} 个源生成器，但本次编译输入中未见生成产物（obj/）。" +
                "生成源可能声明受约束类型而工具看不到 ⇒ 拒绝给出通过结论。");
            Console.Error.WriteLine(
                "       确认生成源确不含受约束类型后，用 --allow-generators 显式放行。");
            foreach (var g in inputs.GeneratorsReferenced)
                Console.Error.WriteLine("       - " + Path.GetFileName(g));
            return 1;
        }

        // 多目标工程未指定 -f ⇒ 明确拒绝（审计第十轮 B2：帮助文本承诺"默认取首项"从未实现）。
        if (inputs.MultiTargetFrameworks.Count > 1 && opt.Framework is null)
        {
            Console.Error.WriteLine(
                "error: 工程为多目标（" + string.Join(";", inputs.MultiTargetFrameworks) +
                "），必须用 -f 指定要审核的目标框架。不同 TFM 的源码/引用可能不同，隐式选择会误导结论。");
            return 1;
        }

        // P1.4/P4.4：加载 effectledger.contracts.json（重复文件/未知键/非法版本/缺 reason ⇒ 明确错误）。
        // 配置错误一律 exit 1——静默按"无配置"继续会让用户以为摘要已生效。
        var contractConfig = LoadContractConfig(inputs.AdditionalFiles);
        foreach (var e in contractConfig.Errors)
            Console.Error.WriteLine("error: effectledger.contracts.json 无效：" + e);
        if (!contractConfig.IsValid) return 1;

        var resolver = new ProfileResolver(compilation);
        var engine = new ContractEngine(compilation, AnalysisBudget.Default, contractConfig);
        var decls = resolver.FindDeclarations().ToList();

        var report = BuildReport(opt, compilation, decls, engine, contractConfig);
        if (opt.ReportPath is not null)
        {
            try
            {
                File.WriteAllText(opt.ReportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: 报告无法写入 '{opt.ReportPath}'：{ex.Message}");
                return 1;
            }
        }

        PrintSummary(report, opt);

        // 零目标：默认一律不通过（审查 F-14）——"没检查"不能当作"通过"，
        // 与 strict/advisory 无关；确需空目标的项目须显式 --allow-empty。
        if (report.RootCount == 0 && !opt.AllowEmpty)
        {
            Console.Error.WriteLine(
                "error: 未找到任何受约束类型。零目标不能视为通过（若确属预期请显式传 --allow-empty）。");
            return 2;
        }

        // 基线核对（P5.5）：提供 --baseline 时，删除角色/改名/漏项目必须显式更新基线，
        // 否则 exit 2——防止"静默丢根"让门禁悄悄缩水。
        if (opt.BaselinePath is not null)
        {
            var missing = CheckBaseline(opt, report.Roots);
            if (missing is { Count: > 0 } || opt.BaselineMismatch)
            {
                Console.Error.WriteLine("error: 与基线不一致（详见上方输出）。");
                return 2;
            }
        }

        if (opt.Mode == "strict")
        {
            if (report.FailedRoots > 0) return 2;
            if (report.UnknownRoots > 0) return 2;
            return 0;
        }

        // advisory：可见但不阻断 —— 违规/未知打印在输出中，退出码 0；
        // 阻断语义请用 strict。二者退出码自此不同（审计第十轮 B2："mode 是摆设"）。
        return 0;
    }

    // ── 报告与输出 ──

    private sealed record RootReport(string Type, string Profile, bool Violated, bool Unknown,
        int ViolationCount, int UnknownCount, IReadOnlyList<string> Diagnostics,
        IReadOnlyList<string> UnknownReasons,
        IReadOnlyList<string> RuleBasis, IReadOnlyList<string> TrustDependencies);

    /// <summary>覆盖计数（P5.6）：让"没查清"可观测。</summary>
    private sealed record CoverageReport(int SourceFiles, int ConstrainedTypesFound,
        int RootsEvaluated, int RootsWithViolations, int RootsWithUnknown, int RootsClean);

    private sealed record AuditReport(int RootCount, int FailedRoots, int UnknownRoots,
        string Mode, string EngineVersion, IReadOnlyList<RootReport> Roots,
        CoverageReport Coverage, string SourceFingerprint, string CatalogVersion,
        IReadOnlyList<string> ConfigSources)
    {
        public bool Passed => FailedRoots == 0 && UnknownRoots == 0;
    }

    /// <summary>
    /// 从真实构建导出的 AdditionalFiles 中加载 effectledger.contracts.json（P1.4）。
    /// 多于一个 ⇒ 拒绝（生效来源不确定）；缺失 ⇒ 空配置；解析错误 ⇒ 由调用方 loud 处理。
    /// </summary>
    private static ContractConfig LoadContractConfig(IReadOnlyList<string> additionalFiles)
    {
        var matches = additionalFiles
            .Where(f => string.Equals(Path.GetFileName(f), "effectledger.contracts.json",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) return ContractConfig.Empty;

        if (matches.Count > 1)
            return new ContractConfig(
                System.Collections.Immutable.ImmutableArray<UserSummary>.Empty,
                "none",
                System.Collections.Immutable.ImmutableArray.Create(
                    $"发现 {matches.Count} 个 effectledger.contracts.json（{string.Join(", ", matches)}）；" +
                    "重复配置会让生效来源不确定，故拒绝"),
                PolicyOptions.Default);

        string text;
        try { text = File.ReadAllText(matches[0]); }
        catch (Exception ex)
        {
            return new ContractConfig(
                System.Collections.Immutable.ImmutableArray<UserSummary>.Empty,
                "none",
                System.Collections.Immutable.ImmutableArray.Create($"无法读取 {matches[0]}：{ex.Message}"),
                PolicyOptions.Default);
        }
        return ContractConfigParser.Parse(text);
    }

    private static AuditReport BuildReport(Options opt, Compilation compilation,
        IReadOnlyList<ContractDeclaration> decls, ContractEngine engine, ContractConfig contractConfig)
    {
        var roots = new List<RootReport>();
        int failed = 0, unknown = 0;
        foreach (var d in decls)
        {
            var res = engine.Evaluate(d);
            var diags = res.Violations.Select(v => v.Id + ": " + v.GetMessage()).ToList();

            // 同一 (原因, 描述, 站点) 去重：合并摘要与调用点传播会重复登记同一站点，
            // 导致报告计数虚高（审查 F-15）。Exit code 不受影响，但报告必须如实。
            var unknownReasons = res.Unknowns
                .Select(u => $"{u.Reason}: {u.Description}" + (u.LocationKey is null ? "" : $"（站点 {u.LocationKey}）"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            int violationCount = res.Violations.Count;
            int unknownCount = unknownReasons.Count;
            if (violationCount > 0) failed++;
            if (unknownCount > 0) unknown++;

            // 规则依据与信任依赖（P5.6）：报告必须能回答"这个结论凭什么"。
            var ruleBasis = res.Violations.Select(v => v.Id)
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var trust = new List<string> { "builtin-bcl-catalog" };
            if (contractConfig.Summaries.Length > 0) trust.Add("user-summaries");

            roots.Add(new RootReport(d.Type.Name, d.Profile.ToString(), violationCount > 0, unknownCount > 0,
                violationCount, unknownCount, diags, unknownReasons, ruleBasis, trust));
        }

        // 覆盖计数（P5.6）：只报"N 根全 OK"无法区分"查过且干净"与"根本没查到"。
        var coverage = new CoverageReport(
            SourceFiles: compilation.SyntaxTrees.Count(),
            ConstrainedTypesFound: decls.Count,
            RootsEvaluated: roots.Count,
            RootsWithViolations: roots.Count(r => r.Violated),
            RootsWithUnknown: roots.Count(r => r.Unknown),
            RootsClean: roots.Count(r => !r.Violated && !r.Unknown));

        return new AuditReport(roots.Count, failed, unknown, opt.Mode,
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0", roots,
            coverage, SourceFingerprint(compilation), BclCatalog.Version,
            contractConfig.Fingerprint == "none"
                ? Array.Empty<string>()
                : new[] { "effectledger.contracts.json:" + contractConfig.Fingerprint });
    }

    /// <summary>源指纹（P5.6）：按文件名+长度摘要；报告可比对"两次审核是否同一份源码"。</summary>
    private static string SourceFingerprint(Compilation compilation)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var sb = new System.Text.StringBuilder();
        foreach (var tree in compilation.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
            sb.Append(Path.GetFileName(tree.FilePath)).Append(':').Append(tree.Length).Append('\n');
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// 基线核对（P5.5）：期望根清单（JSON 数组或 {expectedRoots:[…]}）。
    /// 删除角色/改名/漏项目都会产生差异 ⇒ 缺失与多余都要报告（不能更新基线自动掩盖）。
    /// </summary>
    private static List<string>? CheckBaseline(Options opt, IReadOnlyList<RootReport> roots)
    {
        if (opt.BaselinePath is null) return null;
        List<string> expected;
        try
        {
            // 支持两种形态：裸字符串数组，或 {"expectedRoots":[…]} 对象。
            var doc = JsonDocument.Parse(File.ReadAllText(opt.BaselinePath));
            expected = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().Select(e => e.GetString()!).ToList()
                : doc.RootElement.GetProperty("expectedRoots")
                    .EnumerateArray().Select(e => e.GetString()!).ToList();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: 基线无法读取 '{opt.BaselinePath}'：{ex.Message}");
            opt.BaselineMismatch = true;   // 视为与基线不一致（fail-closed）
            return null;
        }
        var found = roots.Select(r => r.Type).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(e => !found.Contains(e)).OrderBy(x => x).ToList();
        var extra = roots.Select(r => r.Type).Where(t => !expected.Contains(t, StringComparer.Ordinal))
            .OrderBy(x => x).ToList();
        if (missing.Count > 0 || extra.Count > 0)
        {
            Console.Error.WriteLine("error: 受约束根与基线不一致（删除/改名/漏项目必须显式更新基线）：");
            foreach (var m in missing) Console.Error.WriteLine("  缺失（基线有、本次未发现）：" + m);
            foreach (var e in extra) Console.Error.WriteLine("  新增（基线无、本次出现）：" + e);
            opt.BaselineMismatch = true;
        }
        return missing;
    }

    private static void PrintSummary(AuditReport r, Options opt)
    {
        Console.WriteLine($"effectledger-contracts: {r.RootCount} 个受约束根, 模式={r.Mode}");
        foreach (var root in r.Roots)
        {
            var tag = root.Violated ? "VIOLATED" : root.Unknown ? "UNKNOWN" : "OK";
            Console.WriteLine($"  [{tag}] {root.Type} ({root.Profile})");
            foreach (var d in root.Diagnostics) Console.WriteLine($"      {d}");
            // Unknown 原因必须在控制台可见：只显示 [UNKNOWN] 而不说为什么，用户无法处置。
            foreach (var u in root.UnknownReasons) Console.WriteLine($"      未知：{u}");
        }
        if (r.RootCount == 0)
            Console.WriteLine("  注意：未找到受约束类型（opt-in：无声明不新增行为约束）。");
    }

    // ── 编译重建 ──

    private static Compilation BuildCompilation(CompileInputs inputs, out bool hadErrors)
    {
        var refs = new List<MetadataReference>();
        foreach (var r in inputs.References)
            if (File.Exists(r)) refs.Add(MetadataReference.CreateFromFile(r));
        var trees = inputs.Sources.Where(File.Exists)
            .Select(s => CSharpSyntaxTree.ParseText(File.ReadAllText(s), inputs.ParseOptions, path: s))
            .ToArray();

        // define 属于 CSharpParseOptions（预处理符号），此处已由 BuildProbe 注入，不再传给 CompilationOptions。
        var compilation = CSharpCompilation.Create(
            "ContractsAudit_" + Guid.NewGuid().ToString("N"),
            trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: inputs.NullableOptions,
                allowUnsafe: true));

        hadErrors = compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);
        return compilation;
    }

    // ── 参数 ──

    private sealed class Options
    {
        public string? Project;
        public string Configuration = "Release";
        public string? Framework;
        public string Mode = "strict";
        public string? ReportPath;
        public bool AllowEmpty;
        public bool AllowGenerators;
        public bool ShowHelp;
        public bool InvalidUsage;
        public string? BaselinePath;
        public bool BaselineMismatch;
    }

    private static Options ParseArgs(string[] args)
    {
        var o = new Options();
        // 支持 "check <project>" 与直接 "<project>" 两种形态。
        var rest = args.Length > 0 && args[0] == "check" ? args.Skip(1).ToArray() : args;
        args = rest;
        var errors = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? Next(string flag)
            {
                if (i + 1 >= args.Length) { errors.Add($"{flag} 缺少值"); return null; }
                return args[++i];
            }
            switch (a)
            {
                case "--project": o.Project = Next(a) ?? o.Project; break;
                case "-c": case "--configuration": o.Configuration = Next(a) ?? o.Configuration; break;
                case "-f": case "--framework": o.Framework = Next(a) ?? o.Framework; break;
                case "-m": case "--mode": o.Mode = Next(a) ?? o.Mode; break;
                case "--report": o.ReportPath = Next(a) ?? o.ReportPath; break;
                case "--allow-empty": o.AllowEmpty = true; break;
                case "--allow-generators": o.AllowGenerators = true; break;
                case "--baseline": o.BaselinePath = Next(a) ?? o.BaselinePath; break;
                case "-h": case "--help": o.ShowHelp = true; break;
                default:
                    if (a.StartsWith("-"))
                        errors.Add($"未知参数 '{a}'（拼写错误会静默改变门禁语义，故 loud 拒绝）");
                    else if (o.Project is null) o.Project = a;
                    else errors.Add($"多余的位置参数 '{a}'");
                    break;
            }
        }
        if (o.Mode is not ("strict" or "advisory"))
            errors.Add($"--mode 须为 strict 或 advisory，实际 '{o.Mode}'");
        if (errors.Count > 0)
        {
            foreach (var e in errors) Console.Error.WriteLine("error: " + e);
            o.InvalidUsage = true;
        }
        return o;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("usage: effectledger-contracts check <project.csproj> [options]");
        Console.WriteLine("  -c, --configuration <name>   构建配置（默认 Release）");
        Console.WriteLine("  -f, --framework <tfm>         多目标工程必须指定；单目标可省略");
        Console.WriteLine("  -m, --mode <strict|advisory>  严格模式 strict 下任何 violation/unknown 失败");
        Console.WriteLine("  --report <path>                JSON 报告输出路径");
        Console.WriteLine("  --allow-empty                  目标清单允许 0 个受约束类型");
        Console.WriteLine("  --allow-generators             确认生成源不含受约束类型（默认对生成器工程 fail-closed）");
        Console.WriteLine("  --baseline <path>              期望根清单（JSON 字符串数组）；与本次结果不一致即失败");
    }
}

/// <summary>真实构建抽取的编译输入（P5.2：与 dotnet build 一致的源/引用/预处理符号/语言版本）。</summary>
internal sealed record CompileInputs(
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> References,
    CSharpParseOptions ParseOptions,
    NullableContextOptions NullableOptions,
    IEnumerable<string> Defines,
    IReadOnlyList<string> GeneratorsReferenced,
    IReadOnlyList<string> MultiTargetFrameworks,
    /// <summary>P1.4：消费方 AdditionalFiles（含 effectledger.contracts.json）。</summary>
    IReadOnlyList<string> AdditionalFiles);
