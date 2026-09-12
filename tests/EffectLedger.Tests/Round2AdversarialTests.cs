using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

// Round 2 对抗审计（L2 生成器 + L3 分析器 + 打包/集成）。
// 每一条测试都真实触发 L3 分析器（Roslyn WithAnalyzers），断言可证伪行为；不依赖 mock 自断言。
// 这些测试在当前 master 代码下应当 FAIL（红），对应真实缺陷；修复建议见各测试注释（文件:行）。
namespace EffectLedger.Tests;

public sealed class Round2AdversarialTests
{
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = DefaultRefs().ToArray();
        return CSharpCompilation.Create(
            "Round2AdvAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static System.Collections.Generic.IEnumerable<MetadataReference> DefaultRefs()
    {
        yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location);
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        foreach (var p in tpa)
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name == "System.Runtime" || name == "System.Collections.Immutable")
                yield return MetadataReference.CreateFromFile(p);
        }
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var compilation = MakeCompilation(source);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ── BUG A：完全限定 / global:: 限定的 [EffectOverride] 名不被识别 ─────────────
    // 分析器用 attr.Name.ToString() 仅比对 "EffectOverride"/"EffectOverrideAttribute"
    // （EffectAlgebraAnalyzer.cs:  IsEffectOverride 用 name==... 字面量比对），
    // 不解析 [EffectLedger.EffectOverride(...)] 或 [global::EffectLedger.EffectOverride(...)]，
    // 导致逃逸通道被静默忽略：合法标了 override 且确实意图混用 kind 的方法仍被报 EAA0303（误报）。
    //
    // 修复建议：EffectAlgebraAnalyzer.cs 中 IsEffectOverride/IsAcceptDeviation 应改用
    //   context.SemanticModel.GetTypeInfo(attr).Type 或 attr.Name 解析出的符号与
    //   typeof(EffectOverrideAttribute) 比较（用 ISymbol/INamedTypeSymbol 全名解析），
    //   而非字符串相等。或者在 GetAnnotatedMethod 处用语义模型取属性类型。
    //
    // 预期：带完全限定 [EffectOverride(...)] 且混用 Read/Write 同一资源的方法不应报 EAA0303。
    // 当前代码：方法名字符串为 "EffectLedger.EffectOverride" ≠ "EffectOverride" ⇒ hasValidOverride=false ⇒ 报 EAA0303 ⇒ 测试 FAIL。
    [Fact]
    public async Task BUG_A_FullyQualifiedEffectOverride_NotMisReportedAsA3()
    {
        // A2-15（生产审计批4 重_ARM）：Sample 迁入 Godot.Shapes——全局命名空间下调用点被类型门拦截，
        // EAA0303 恒不存在 ⇒ DoesNotContain 恒真（假覆盖）。过门后本测试才真正验证 override 识别。
        const string source = @"
using EffectLedger;
namespace Godot.Shapes
{
    public class Sample
    {
        public void Connect(object s, object c) { }
        public void IsConnected(object s) { }
        [EffectLedger.EffectOverride(""intent: paired signal read+write"")]
        public void SignalMixWithQualifiedOverride()
        {
            Connect(new object(), new object());
            IsConnected(new object());
        }
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303");
    }

    // ── BUG A（变种）：global:: 限定同样应被识别 ───────────────────────────────
    [Fact]
    public async Task BUG_A_GlobalQualifiedEffectOverride_NotMisReportedAsA3()
    {
        // A2-15：同上，过类型门（Godot.Shapes）后才真正验证 global:: 形态的 override 识别。
        const string source = @"
using EffectLedger;
namespace Godot.Shapes
{
    public class Sample
    {
        public void Connect(object s, object c) { }
        public void IsConnected(object s) { }
        [global::EffectLedger.EffectOverride(""intent: paired signal read+write"")]
        public void SignalMixWithGlobalOverride()
        {
            Connect(new object(), new object());
            IsConnected(new object());
        }
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0303");
    }

    // ── BUG B：整数字面量 [AcceptDeviation(0)] 被误报 EAA0802 ──────────────────
    // IsValidAcceptEpsilon 用 cv.Value is not double e 判断；
    // 但 Roslyn 对整数实参 0 的 GetConstantValue 返回 boxed int（不是 double），
    // 合法 epsilon=0 被当作“非常量/越界”误报 EAA0802。
    //
    // 修复建议：EffectAlgebraAnalyzer.cs IsValidAcceptEpsilon 改为
    //   var cv = context.SemanticModel.GetConstantValue(arg.Expression);
    //   if (!cv.HasValue) { report; return false; }
    //   double e;
    //   if (cv.Value is double d) e = d;
    //   else if (cv.Value is int i) e = i;      // 整数实参（如 0）合法
    //   else if (cv.Value is float f) e = f;
    //   else { report; return false; }
    //   if (e < 0.0 || e > 0.5) { report; return false; }
    //
    // 预期：标 [AcceptDeviation(0)] 的方法不应报 EAA0802（0 在 [0,0.5] 内，且 ctor 接受）。
    // 当前代码：cv.Value 为 int 0 ⇒ is not double 为真 ⇒ 报 EAA0802 ⇒ 测试 FAIL。
    [Fact]
    public async Task BUG_B_AcceptDeviationIntegerLiteral_NotReportedAsEAA0802()
    {
        const string source = @"
using EffectLedger;
public class Sample
{
    [AcceptDeviation(0)]
    public void TempHold() { }
}";
        var diags = await RunAnalyzer(source);
        Assert.DoesNotContain(diags, d => d.Id == "EAA0802");
    }

    // ── BUG D：release-class-only 调用抑制整方法 EAA0901（无关资源泄漏漏报）──────
    // AnalyzeMissingRelease 中：只要方法体内出现过“不在 §7 白名单的 release-class 调用”
    // （如历史上的 RemoveFromGroup/cancel_free/free_children_in_group，均仅释放部分资源；
    // 【QED-A9】三者已移出 release-class，该抑制面随之收窄，但机制本身的防护钉保留），
    // 就把 hasReleaseClassOnly 置真，进而 return 跳过整个方法的 EAA0901 报告。
    // 这会让“调用了 RemoveFromGroup（只释放组隶属）却仍 AddChild 占用 Tree 却从未释放 Tree”的
    // 真实泄漏被整段静默吞掉（false negative）。
    //
    // 修复建议：EffectAlgebraAnalyzer.cs AnalyzeMissingRelease 不应以“任一 release-class 调用”
    // 作为 blanket 抑制。改为按资源判定：仅当该被 acquire 的归一资源的 net>0 时，
    // 若存在可释放该资源的 release-class 调用才豁免；否则仍报 EAA0901。即去掉 hasReleaseClassOnly 的全局 return，
    // 改为 per-resource 的释放可见性判断（参见 R8 注释的逐资源计数思路，但需区分“释放的是哪种资源”）。
    //
    // 预期：M 内 RemoveFromGroup（仅释放组隶属）+ AddChild（占用 Tree 未释放）⇒ 应报 EAA0901。
    // 当前代码：RemoveFromGroup 规范化 = removefromgroup ∈ ReleaseApiNames 且不在白名单
    //          ⇒ hasReleaseClassOnly=true ⇒ 跳过全部 EAA0901 ⇒ 测试 FAIL（漏报）。
    [Fact]
    public async Task BUG_D_ReleaseClassOnlyCall_SuppressesUnrelatedLeak()
    {
        // A2-15（生产审计批4 重_ARM）：释放类调用改经 Godot.Shapes 接收者（Disconnect 过类型门、计入 release-class）——
        // 此前 RemoveFromGroup 在全局命名空间被类型门拦截 ⇒ release-class 计数为 0，blanket 抑制回归也不会让本测试变红。
        // Disconnect 只释放 Callback，不释放 Tree：若有人重新引入「任一 release-class 即整体豁免」，Tree 泄漏被吞 ⇒ 本测试红。
        const string source = @"
using EffectLedger;
namespace Godot.Shapes { public sealed class Node3D { public void AddChild(object c) { } public void Disconnect(object s, object c) { } } }
public class Sample
{
    private readonly Godot.Shapes.Node3D _n = new();
    public void M()
    {
        _n.AddChild(new object());                   // §7 acquire Tree(node.id)，无任何对应 Tree release
        _n.Disconnect(new object(), new object());   // §8.1 release-class：仅释放 Callback，不释放 Tree
    }
}";
        var diags = await RunAnalyzer(source);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("M"));
    }
}
