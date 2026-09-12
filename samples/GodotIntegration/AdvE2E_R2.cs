// AdvE2E_R2.cs — 对抗审计 Round 2：L3 分析器逃逸/豁免绕过（ADVERSARIAL）。
// 复用 IntegrationTests.MakeCompilation 驱动模式（GameSource 风格字符串 + WithAnalyzers + GetAnalyzerDiagnosticsAsync）。
// 断言全部可证伪： blanket exemption 静默泄漏 / [AcceptDeviation] 误豁免 / 跨方法配对漏报 / 空 reason / 误报 / 命名逃逸。
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using EffectLedger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using SampleGame.IntegrationTests;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R2
{
    // 与 IntegrationTests 相同的驱动；独立复制以避免跨文件依赖。
    private static CSharpCompilation MakeCompilation(string source)
    {
var refs = CompilationRefs.Lean(typeof(Claim).Assembly.Location);
        return CSharpCompilation.Create(
            "AdvR2Game",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var comp = MakeCompilation(source);
        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(AnalyzerTestLoader.LoadAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // P0-2 对齐（hickey-x3 F3）：桩类型放 Godot.Shapes 命名空间（分析器的 Godot 类型门），
    // 消费者经接收者调用 _n.AddChild(...)——与真实 Godot 用法一致；用户自有撞名方法不再被裸名定罪。
    private const string Header = @"
using EffectLedger;
namespace Godot.Shapes {
    public sealed class Node3D { public object? child; public void AddChild(object c) { } public void RemoveChild() { } }
    public sealed class SignalHub { public void Connect(object s, object c) { } public void IsConnected(object s) { } }
";
    private const string Footer = @"
}";

    /// <summary>R2-1 对抗：多个方法都标 [EffectOverride] 但每个都含真实 acquire 调用 ⇒ 不得静默漏报泄漏（§8.3.1(3) DO-9 仍报警）。
    /// 旧实现 hasEscape 跳过整个方法 ⇒ 全标 override 即零报警（静默泄漏）。修正后 EAA0901 仍须报到具体方法。</summary>
    [Fact]
    public async Task E2E_R2_OverrideDoesNotSuppressDO9Leak()
    {
        var src = Header + @"
    namespace AdvR2 {
    public sealed class AllTaggedLeak {
        private readonly Node3D _n = new();
        [EffectOverride(""known leak A"")]
        public void TagA() { _n.AddChild(new object()); }
        [EffectOverride(""known leak B"")]
        public void TagB() { _n.AddChild(new object()); }
        [EffectOverride(""known leak C"")]
        public void TagC() { _n.AddChild(new object()); }
    } }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("TagA"));
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("TagB"));
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("TagC"));
    }

    /// <summary>R2-2 对抗：[AcceptDeviation] 仅放宽运行期 Deviation 阈值（§8.3.2(2)），不得豁免编译期 DO 报警。
    /// 旧实现 hasEscape 包含 AcceptDeviation ⇒ 标 [AcceptDeviation] 即压制 EAA0901/EAA0303/EAA0304（误豁免）。修正后照报。</summary>
    [Fact]
    public async Task E2E_R2_AcceptDeviationDoesNotSuppressCompileTimeDO()
    {
        var src = Header + @"
    namespace AdvR2 {
    public sealed class DeviateLeak {
        private readonly Node3D _n = new();
        [AcceptDeviation(0.3)]
        public void MarkedA() { _n.AddChild(new object()); }
        [AcceptDeviation(0.3)]
        public void MarkedB() { _n.AddChild(new object()); _n.AddChild(new object()); }
    } }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("MarkedA"));
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("MarkedB"));
        // MarkedB 两次 AddChild 同 Tree(node.id) Create×2 ⇒ A4 冲突仍须报（不豁免）
        Assert.Contains(diags, d => d.Id == "EAA0304" && d.GetMessage().Contains("MarkedB"));
    }

    /// <summary>R2-3 对抗：[EffectOverride] 仅豁免 A3/A4 意图提示（EAA0303/EAA0304），不豁免 EAA0901。
    /// 验证 override 方法内 Connect↔IsConnected（信号跨 kind）不报 A3，但 acquire 无 release 仍报 EAA0901。</summary>
    [Fact]
    public async Task E2E_R2_OverrideSuppressesA3NotDO9()
    {
        var src = Header + @"
    namespace AdvR2 {
    public sealed class SignalOverride {
        private readonly Node3D _n = new();
        private readonly SignalHub _bus = new();
        [EffectOverride(""intent: paired signal + node lifecycle"")]
        public void Mixed() { _bus.Connect(new object(), new object()); _bus.IsConnected(new object()); _n.AddChild(new object()); }
    } }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303" && d.GetMessage().Contains("Mixed"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0304" && d.GetMessage().Contains("Mixed"));
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Mixed"));
    }

    /// <summary>R2-4 对抗：跨方法配对（A 方法 acquire，B 方法 release，同一类内）⇒ 静态近似漏报属已知边界（见类注释 OPEN-2）。
    /// 本测试锁定该近似行为：类整体 acquire+release 分散在两个未标注方法时，分析器可能不报 EAA0901（false negative，不误报）。
    /// 用途=回归基线：若未来改为 flow-sensitive，此断言应更新为「报」。当前仅断言不误报健康配对。</summary>
    [Fact]
    public async Task E2E_R2_CrossMethodPairing_NoFalsePositive()
    {
        var src = Header + @"
    public sealed class CrossMethodPair {
        private readonly Node3D _n = new();
        public void Acquire() { _n.child = new object(); }   // AddChild 风格 acquire
        public void Release() { _n.child = null; }           // RemoveChild 风格 release
        // 注意：方法名不命中 §7 白名单（Acquire/Release 非白名单键），不触发 DO-9
    }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }

    /// <summary>R2-5 对抗：[EffectOverride] reason 为空字符串 ⇒ 属性构造子在运行期反射抛（ArgumentException），但编译期不阻断；
    /// 故分析器须作为兜底仍报泄漏（fail-open，不形成静默逃逸）。</summary>
    [Fact]
    public async Task E2E_R2_EmptyReason_StillReportsLeak()
    {
        var src = Header + @"
    namespace AdvR2 {
    public sealed class EmptyReason {
        private readonly Node3D _n = new();
        [EffectOverride("""")]
        public void Marked() { _n.AddChild(new object()); }   // 空 reason：不形成静默逃逸
    } }" + Footer;
        var compilation = MakeCompilation(src);
        var diags = await RunAnalyzer(src);
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Marked"));
    }

    /// <summary>R2-6 对抗：误报 — 平衡类（AddChild 配对 RemoveChild）不应报 EAA0901；方法名撞白名单但语义平衡。</summary>
    [Fact]
    public async Task E2E_R2_NoFalsePositiveOnBalancedPair()
    {
        var src = Header + @"
    public sealed class Balanced {
        private readonly Node3D _n = new();
        public void AddChild(object c) { _n.child = c; }
        public void RemoveChild() { _n.child = null; }
        public void SpawnAndDespawn() { AddChild(new object()); RemoveChild(); }
    }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("Balanced"));
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("SpawnAndDespawn"));
    }

    /// <summary>R2-7 对抗：方法名撞白名单但啥也不做（如方法体为空）→ 无调用表达式 → 不报 EAA0901（无 acquire 调用）。</summary>
    [Fact]
    public async Task E2E_R2_NoopApiNamedMethod_NotFlagged()
    {
        var src = Header + @"
    public sealed class NamedButIdle {
        public void AddChild(object c) { }   // 名撞 §7 但无实际调用表达式
        public void QueueFree() { }
    }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("AddChild"));
    }

    /// <summary>R2-8 对抗：真实 alloc 方法命名不像任何 §7 API（如 CreateNode）→ 静态白名单匹配逃逸检测（known limitation）。
    /// 锁定行为：不误报（因不在白名单）。用途=诚实记录：非侵入式 name-match 的固有盲区（需运行期 Σnet 兜底）。
    /// 若未来扩展白名单，此断言应更新。</summary>
    [Fact]
    public async Task E2E_R2_ObfuscatedAlloc_EscapesStaticDetection_NoFalsePositive()
    {
        var src = Header + @"
    public sealed class Obfuscated {
        private readonly Node3D _n = new();
        public void CreateNode(object c) { _n.child = c; }   // 真实占用但名不命中白名单
        public void DestroyNode() { _n.child = null; }
    }" + Footer;
        var diags = await RunAnalyzer(src);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901");
    }
}

