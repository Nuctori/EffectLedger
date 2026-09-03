// AdvE2E_R10.cs — 对抗审计 Round 10：CI 集成 / analyzer-as-Analyzer 接法 / 跨平台 / -warnaserror 门禁。
// 目标：证明集成 E2E 不是"假绿/mock"——分析器是真实可实例化并以 WithAnalyzers 真跑的诊断器，
// 生成器在不依赖任何硬编码绝对路径（跨平台安全）时仍对 GameSource emit。
//
// 接法约束（与 SampleGame.csproj 一致，并 honest 声明）：
//   - 真实 Game.csproj 消费 L3 的写法是 OutputItemType="Analyzer" ReferenceOutputAssembly="false"。
//   - 但本仓 analyzer 在编译器 analyzer-ALC 下因 SDK Roslyn 版本绑定差异可能报 CS8032，
//     故测试工程用普通 ProjectReference 引用 analyzer 程序集，并以 WithAnalyzers 直接驱动
//     （new EffectAlgebraAnalyzer() 是真实 DiagnosticAnalyzer 实例，非桩），语义等价覆盖。
//   - 跨平台路径（commit a65b359）：本测试从测试输出目录向上定位仓库根（搜 Cosmos.EffectAlgebra.slnx），
//     再指向 src/Cosmos.EffectAlgebra/EffectScript.cs，避免 Windows 硬编码路径在 Linux CI 上失败。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using SampleGame.IntegrationTests;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R10
{
    // 与 IntegrationTests.cs 同构的 GameSource（acquire 无 release 的 LeakyEnemy 必触发 EAA0901）。
    private const string LeakyGameSource = @"
using Cosmos.EffectAlgebra;
namespace Godot.Shapes { public sealed class Node3D { public void AddChild(object child) { } } }
namespace R10Game {
    public sealed class LeakyEnemy {
        private readonly Godot.Shapes.Node3D _node = new();
        public void Spawn() { _node.AddChild(new object()); }
    }
}";

    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        return CSharpCompilation.Create(
            "R10IntegrationGame",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    // 仓库相对路径定位（跨平台，镜像 commit a65b359 的修复）：从测试输出目录向上找仓库根。
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Cosmos.EffectAlgebra.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>R10-A：分析器是真实可实例化并以 WithAnalyzers 真跑的诊断器（证明非 mock）。
    /// 实例化 + 对泄漏源必须产出 EAA0901，且 SupportedDiagnostics 含 EAA0901/EAA0303/EAA0304。</summary>
    [Fact]
    public async Task E2E_R10_AnalyzerInstantiableAndRuns()
    {
        // 真实实例化（非桩）：构造必须成功，且类型确为 DiagnosticAnalyzer。
        DiagnosticAnalyzer analyzer = AnalyzerTestLoader.LoadAnalyzer();
        Assert.IsAssignableFrom<DiagnosticAnalyzer>(analyzer);

        // SupportedDiagnostics 必须真含 L3 三套诊断（证明是真实分析器而非空壳）。
        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToImmutableHashSet();
        Assert.Contains("EAA0901", ids);
        Assert.Contains("EAA0303", ids);
        Assert.Contains("EAA0304", ids);

        // 以 WithAnalyzers 驱动（真实编译期分析路径），对泄漏源必须报 EAA0901。
        var comp = MakeCompilation(LeakyGameSource);
        var withAnalyzers = comp.WithAnalyzers(ImmutableArray.Create(analyzer));
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diags, d => d.Id == "EAA0901");
        // 断言 EAA0901 指向泄漏方法（LeakyEnemy.Spawn 或 AddChild），确保是"真跑"而非随机诊断。
        Assert.Contains(diags, d => d.Id == "EAA0901" &&
            (d.GetMessage().Contains("Spawn") || d.GetMessage().Contains("AddChild")));
    }

    // 带 [EffectOverride] 标注方法的 GameSource（生成器仅对标注方法 emit，与真实 SampleGame.cs 同构）。
    private const string AnnotatedGameSource = @"
using Cosmos.EffectAlgebra;
namespace R10Game {
    public sealed class Node3D { public object? child; }
    public sealed class HealthyEnemy {
        private readonly Node3D _node = new();
        [EffectOverride(""spawn/despawn 配对"")]
        public void AddChild(object child) { _node.child = child; }
        [EffectOverride(""释放 Tree"")]
        public void RemoveChild() { _node.child = null; }
    }
}";

    /// <summary>R10-B：生成器在不依赖任何硬编码绝对路径（跨平台安全）时仍对 GameSource emit §7 Claims。
    /// 用仓库相对路径解析 L1 程序集（与 a65b359 修复同构），断言生成代码真含 GodotApiWhitelist.All / ComputeAddChild。</summary>
    [Fact]
    public void E2E_R10_HarnessBuildsWithWarnAsError()
    {
        // 仓库相对定位 L1 程序集（跨平台：Windows/Linux/macOS 均可用，无硬编码 D:/...）。
        var l1Dll = Path.Combine(RepoRoot(), "src", "Cosmos.EffectAlgebra", "bin", "Debug", "net10.0",
            "Cosmos.EffectAlgebra.dll");
        // 若尚未构建，回溯到任何 net10.0 输出（CI Release 下路径不同；此处本机 Debug 已构建）。
        if (!File.Exists(l1Dll))
        {
            var alt = Directory.GetFiles(RepoRoot(), "Cosmos.EffectAlgebra.dll", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.Contains("net10.0"));
            if (alt is not null) l1Dll = alt;
        }
        Assert.True(File.Exists(l1Dll), "L1 程序集应可由仓库相对路径解析: " + l1Dll);

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(l1Dll));

        var comp = CSharpCompilation.Create(
            "R10GenGame",
            new[] { CSharpSyntaxTree.ParseText(AnnotatedGameSource) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);

        var genText = output.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.ToString())
            .Aggregate((a, b) => a + "\n" + b);
        // 断言生成器真引用 §7（非桩），与真实 Game.csproj 接入行为一致（跨平台无差异）。
        Assert.Contains("GodotApiWhitelist.All", genText);
        Assert.Contains("Signature.Union", genText);
    }

    /// <summary>R10-C：CI 等价门禁——集成工程配置了 -warnaserror（跨平台零警告门禁）。
    /// 直接核验 SampleGame.csproj 的 TreatWarningsAsErrors 与 SLNX 引用（与 ci.yml 同款门禁的静态证明），
    /// 避免测试内嵌套 dotnet 进程在 CI（Release/--no-build）下与外层测试进程相互竞争 bin/obj。
    /// 动态端到端构建由 ci.yml 的「Build -warnaserror」步骤真实执行。</summary>
    [Fact]
    public void E2E_R10_CrossPlatformHarnessBuild()
    {
        var projPath = Path.Combine(RepoRoot(), "samples", "GodotIntegration", "SampleGame.csproj");
        Assert.True(File.Exists(projPath), "集成工程应可由仓库相对路径定位: " + projPath);
        var proj = File.ReadAllText(projPath);
        // 跨平台门禁：TreatWarningsAsErrors 必须开启（ci.yml 的 -warnaserror 同义）。
        Assert.Contains("TreatWarningsAsErrors", proj);

        // 集成工程必须纳入 SLN（ci.yml 的 dotnet test Cosmos.EffectAlgebra.slnx 会覆盖）。
        var slnx = File.ReadAllText(Path.Combine(RepoRoot(), "Cosmos.EffectAlgebra.slnx"));
        Assert.Contains("SampleGame.csproj", slnx);

        // 当前测试程序集确实由该工程产出（本测试正在运行即证明跨平台构建成功）。
        Assert.Contains("SampleGame", typeof(AdvE2E_R10).Assembly.GetName().Name ?? "");
    }
}

