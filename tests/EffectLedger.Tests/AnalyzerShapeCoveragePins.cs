// AnalyzerShapeCoveragePins.cs — P5-10-05 / P5-10-06 实证钉：L3 语法形状覆盖面的**当前行为**快照。
// 背景：L3 只注册 MethodDeclarationSyntax ⇒ 仅方法体内语句级调用参与分析。README 诚实边界 #9
// 曾只声明「构造函数/属性访问器」，实测漏报面更宽（表达式体属性/索引器/字段初始化器/运算符重载/
// 转换运算符/事件访问器）——本文件把覆盖面钉成可证伪的契约，任何一侧变化（补覆盖 或 声明漂移）即红。
// 注意：这些是【已声明的静态近似边界】（README #9），非缺陷；钉的目的是让边界不静默漂移。
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EffectLedger.Tests;

public class AnalyzerShapeCoveragePins
{
    // Godot 形状替身 + 各语法形状各含一处【相同】的未配对 acquire（AddChild）调用。
    private const string Source = """
        namespace Godot { public class Node { public Node AddChild(Node n) => n; public void QueueFree() { } } }
        public class Shapes
        {
            Godot.Node _n = new Godot.Node();
            Godot.Node _field = new Godot.Node().AddChild(new Godot.Node());          // 字段初始化器
            public void MethodBody() { _n.AddChild(new Godot.Node()); }                // 方法体语句级（唯一被覆盖）
            public Godot.Node ExprBodiedProp => _n.AddChild(new Godot.Node());         // 表达式体属性
            public Godot.Node this[int i] => _n.AddChild(new Godot.Node());            // 索引器
            public static Shapes operator +(Shapes a, Shapes b) { a._n.AddChild(new Godot.Node()); return a; } // 运算符重载
            public static explicit operator Godot.Node(Shapes s) => s._n.AddChild(new Godot.Node());          // 转换运算符
            event System.Action? E { add { _n.AddChild(new Godot.Node()); } remove { } }                     // 事件访问器
            public Shapes() { _n.AddChild(new Godot.Node()); }                          // 构造函数
            public Godot.Node PropAccessor { get { return _n.AddChild(new Godot.Node()); } } // 属性访问器
        }
        """;

    private static async System.Threading.Tasks.Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var refs = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(EffectLedger.Claim).Assembly.Location),
        };
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator);
        foreach (var p in tpa)
            if (System.IO.Path.GetFileNameWithoutExtension(p) is "System.Runtime" or "System.Collections.Immutable")
                refs.Add(MetadataReference.CreateFromFile(p));
        // 事件/委托需要 System.Runtime 的 Action；再补一组常用门面以保编译无错
        var compilation = CSharpCompilation.Create("ShapeCoverage",
            new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EffectLedger.Analyzer.EffectAlgebraAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ── 钉 1：方法体语句级被覆盖（唯一被分析的形状）——分析器失效的哨兵。 ──
    [Fact]
    public async System.Threading.Tasks.Task MethodBody_IsCovered()
    {
        var diags = await RunAnalyzer(Source);
        Assert.Contains(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains("MethodBody"));
    }

    // ── 钉 2：其余 7 种形状均【未覆盖】（漏报面 = README #9 扩写后的声明集）。 ──
    //    任何一条将来被补上覆盖，此钉变红 ⇒ 同步更新 README #9 与 ROADMAP P5-10-05。
    [Theory]
    [InlineData("ExprBodiedProp")]   // 表达式体属性
    [InlineData("Item")]             // 索引器（元数据名 Item）
    [InlineData("op_Addition")]      // 运算符重载
    [InlineData("op_Explicit")]      // 转换运算符
    [InlineData("add_E")]            // 事件访问器
    [InlineData(".ctor")]            // 构造函数
    [InlineData("get_PropAccessor")] // 属性访问器
    public async System.Threading.Tasks.Task NonMethodBodyShapes_AreNotCovered(string memberName)
    {
        var diags = await RunAnalyzer(Source);
        // 该形状未产生 EAA0901（方法体那条会带着 'MethodBody' 出现，故按成员名过滤）
        Assert.DoesNotContain(diags, d => d.Id == "EAA0901" && d.GetMessage().Contains(memberName));
    }
}
