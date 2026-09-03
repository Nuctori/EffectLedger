// AdvE2E_R3.cs — 对抗审计 Round 3：L2 生成器 emit 代码鲁棒性残差。
// 目标：同名方法跨类/同类的成员重名与生成器 hint 重名碰撞、无匹配键的空 Signature、
// 零标注方法、转义关键字方法名、单一类多方法。全部可证伪，修复闭环（bug=同名方法跨类重名）。
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Cosmos.EffectAlgebra;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace SampleGame.IntegrationTests;

public sealed class AdvE2E_R3
{
    private static CSharpCompilation MakeCompilation(string source)
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Claim).Assembly.Location));
        return CSharpCompilation.Create(
            "IntegrationGameR3",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static (string GenText, bool EmitSuccess, Assembly Asm) Drive(string source)
    {
        var comp = MakeCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Cosmos.EffectAlgebra.Generator.EffectAlgebraGenerator());
        driver.RunGeneratorsAndUpdateCompilation(comp, out var output, out _);
        var gTrees = output.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => t.ToString())
            .ToList();
        var genText = string.Join("\n", gTrees);
        using var ms = new MemoryStream();
        var emit = output.Emit(ms);
        Assembly? asm = null;
        if (emit.Success)
        {
            // 在可收集 ALC 中加载，避免污染默认 AppDomain（harness 的 MakeCompilation 会扫描所有已加载程序集）。
            var alc = new AssemblyLoadContext("r3-" + Guid.NewGuid().ToString("N"), isCollectible: true);
            asm = alc.LoadFromStream(new MemoryStream(ms.ToArray()));
        }
        return (genText, emit.Success, asm!);
    }

    // 解析生成方法：同名方法跨类时生成器以 {MethodName}_{TypeName}_{idx} 消歧；
    // collideType/idx 非空时按消歧名查找，否则按原始 MethodName 查找。
    private static Signature Compute(Assembly asm, string methodName, Signature baseSig, string? collideType = null, int idx = 0)
    {
        var ident = collideType is not null ? $"{methodName}_{collideType}_{idx}" : methodName;
        var t = asm.GetTypes()
            .First(t => t.Name == "EffectAlgebraGenerated" && t.GetMethod("Compute" + ident) is not null);
        return (Signature)t.GetMethod("Compute" + ident)!.Invoke(null, new object[] { baseSig })!;
    }

    private static Signature EmptySig()
        => (Signature)typeof(Signature).GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    // ── R3-1：同一方法名出现在不同类（真实 Godot 工程常见：多个 Node 子类都 AddChild）──
    // 修复前：生成全局 partial class EffectAlgebraGenerated 含两份 ComputeAddChild/CS0101 + 重复 hint 名崩溃。
    [Fact]
    public void E2E_R3_SameMethodNameAcrossTypes_NoCollision()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class EnemyA {
        private readonly Node3D _n = new();
        [EffectOverride(""a"")]
        public void AddChild(object c) { _n.child = c; }
        [EffectOverride(""a"")]
        public void RemoveChild() { _n.child = null; }
    }
    public sealed class EnemyB {
        private readonly Node3D _n = new();
        [EffectOverride(""b"")]
        public void AddChild(object c) { _n.child = c; }
    }
}";
        var (genText, success, asm) = Drive(src);
        Assert.True(success, "同名方法跨类不应引发 CS0101 或生成器 hint 重名崩溃");
        Assert.Contains("GodotApiWhitelist.All", genText);
        // 跨类同名 AddChild → 生成器以 {MethodName}_{FullTypeName}_{idx} 消歧（A2-05：完全限定类型名参与，
        // NS1.Cfg 与 NS2.Cfg 不再同后缀），不再成员重名（CS0101）
        var a = Compute(asm, "AddChild", EmptySig(), "SampleGame_EnemyA", 0);
        var b = Compute(asm, "AddChild", EmptySig(), "SampleGame_EnemyB", 0); // A2-05：按 (方法,全限定类型) 各自从 0 起
        // 同时原始 ComputeAddChild 不应存在（已消歧）
        Assert.DoesNotContain(asm.GetTypes(), t => t.Name == "EffectAlgebraGenerated" && t.GetMethod("ComputeAddChild") is not null);
        Assert.NotEmpty(SignatureExtensions.AllClaims(a));
        Assert.NotEmpty(SignatureExtensions.AllClaims(b));
        Assert.Contains(SignatureExtensions.AllClaims(a), c => c.Kind == Kind.Occupy);
    }

    // ── R3-2：方法名无匹配白名单键 → 空 Signature，不抛、不编译错 ──
    [Fact]
    public void E2E_R3_UnmatchedMethod_EmitsEmptySignature()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Weird {
        private readonly Node3D _n = new();
        [EffectOverride(""nothing matches whitelist"")]
        public void DoSomethingCompletelyUnknown(object c) { _n.child = c; }
    }
}";
        var (_, success, asm) = Drive(src);
        Assert.True(success, "无匹配键方法必须可编译");
        var sig = Compute(asm, "DoSomethingCompletelyUnknown", EmptySig());
        Assert.Empty(SignatureExtensions.AllClaims(sig));
    }

    // ── R3-3：零标注方法 → 不 emit 任何 .g.cs，不崩溃 ──
    [Fact]
    public void E2E_R3_ZeroAnnotated_NoGeneratedType()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Plain {
        private readonly Node3D _n = new();
        public void AddChild(object c) { _n.child = c; }
    }
}";
        var (genText, success, asm) = Drive(src);
        Assert.True(success);
        Assert.DoesNotContain(".g.cs", genText);
        Assert.Equal(0, asm.GetTypes().Count(t => t.Name == "EffectAlgebraGenerated"));
    }

    // ── R3-4：转义关键字方法名（@class）→ 生成器拿到 Identifier.Text="class"，Computeclass 合法 ──
    [Fact]
    public void E2E_R3_EscapedKeywordMethodName_NoCrash()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Kw {
        private readonly Node3D _n = new();
        [EffectOverride(""keyword"")]
        public void @class(object c) { _n.child = c; }
    }
}";
        var (genText, success, asm) = Drive(src);
        Assert.True(success, "转义关键字方法名必须可生成/编译");
        Assert.Contains("Computeclass", genText);
        var sig = Compute(asm, "class", EmptySig());
        // "class" 规范化后 "class" 不匹配白名单任何键（白名单无 class）→ 空
        Assert.Empty(SignatureExtensions.AllClaims(sig));
    }

    // ── R3-5：单一类多标注方法，各自命中不同白名单键（AddChild=Create/Release Tree, RemoveChild=Release Tree）──
    [Fact]
    public void E2E_R3_TwoMethods_SameClass_DistinctKeys()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class One {
        private readonly Node3D _n = new();
        [EffectOverride(""add"")]
        public void AddChild(object c) { _n.child = c; }
        [EffectOverride(""remove"")]
        public void RemoveChild() { _n.child = null; }
    }
}";
        var (genText, success, asm) = Drive(src);
        Assert.True(success);
        Assert.Contains("ComputeAddChild", genText);
        Assert.Contains("ComputeRemoveChild", genText);
        var add = Compute(asm, "AddChild", EmptySig());
        var rem = Compute(asm, "RemoveChild", EmptySig());
        Assert.Contains(SignatureExtensions.AllClaims(add), c => c.Kind == Kind.Occupy && c.Mode == Mode.Create);
        Assert.Contains(SignatureExtensions.AllClaims(rem), c => c.Kind == Kind.Occupy && c.Mode == Mode.Release);
    }

    // ── R3-6：AddChild 与 RemoveChild 在相同 Tree 节点守恒（端到端协议不变量，回归既有的 E2E）──
    [Fact]
    public void E2E_R3_AddRemove_UniteConserved()
    {
        const string src = @"
using Cosmos.EffectAlgebra;
namespace SampleGame {
    public sealed class Node3D { public object? child; }
    public sealed class Bal {
        private readonly Node3D _n = new();
        [EffectOverride(""add"")]
        public void AddChild(object c) { _n.child = c; }
        [EffectOverride(""remove"")]
        public void RemoveChild() { _n.child = null; }
    }
}";
        var (_, success, asm) = Drive(src);
        Assert.True(success);
        var add = Compute(asm, "AddChild", EmptySig());
        var rem = Compute(asm, "RemoveChild", EmptySig());
        var union = Signature.Union(add, rem);
        var net = NetTable.Compute(union, new ScopeId.Global());
        var treeNode = new ResourceId.Tree(NodePathOrUnknown.Of("node.id"));
        Assert.True(net.IsConserved(treeNode), "AddChild+RemoveChild 在 Tree(node.id) 应守恒");
    }
}
