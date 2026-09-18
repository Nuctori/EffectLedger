// EleganceTests.cs — 用户视角审计（正确性与优雅）：结构不变式的可执行钉。
// 纪律：每条断言都要求**具体证据类别**，防止"靠别的路径凑过"。

using System.Linq;
using EffectLedger.Contracts.Analyzer.Analysis;
using EffectLedger.Contracts.Tests.Testing;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace EffectLedger.Contracts.Tests;

public class EleganceTests
{
    private readonly ITestOutputHelper _out;
    public EleganceTests(ITestOutputHelper o) => _out = o;

    private static ITypeSymbol TypeOf(string src, string typeName)
    {
        var r = CompilationFixture.Run("using System; using System.Collections.Generic;\n" + src);
        Assert.True(r.Compiled, "fixture must compile");
        return r.Compilation.GetTypeByMetadataName(typeName)!;
    }

    // 单一真源：不可变容器（含元素）不应被 MutableType 判为可变承载。
    [Fact]
    public void ImmutableContainers_AreNotMutableCarriers()
    {
        var src = """
        public class C
        {
            public int[] Ints = new int[1];
            public List<int> IntList = new();
            public System.Collections.Immutable.ImmutableArray<int> Imm = default;
            public System.Collections.Immutable.ImmutableArray<List<int>> ImmOfMutable = default;
        }
        """;
        var c = TypeOf(src, "C");
        bool Mutable(string field) => MutableType.IsMutableCarrier(
            c.GetMembers(field).OfType<IFieldSymbol>().Single().Type);

        Assert.True(Mutable("IntList"), "List<int> 是可变承载");
        Assert.True(Mutable("ImmOfMutable"), "ImmutableArray<List<int>> 元素可变 ⇒ 可变承载");
        // 数组元素不可变时，可变性来自"数组本身可被替换元素/写入"，
        // 因此 int[] 仍算可变承载（数组是可写容器）；此处钉住该语义而非元素递归。
        Assert.True(Mutable("Ints"), "数组是可写容器 ⇒ 可变承载");
        Assert.False(Mutable("Imm"), "ImmutableArray<int> 元素不可变 ⇒ 非可变承载");
    }

    // 口径一致性：MutableType 判定为"不可变承载"的类型，EBC2003 也应视为稳定入口值。
    // 两条规则若漂移，就会出现"建了不可变容器却无法作为确定性入口"的矛盾。
    [Fact]
    public void StableEntryAgreesWithMutableType_OnImmutableContainers()
    {
        var r = CompilationFixture.Run("""
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using EffectLedger.Contracts;

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Sum(ImmutableArray<int> xs)
            {
                int t = 0;
                foreach (var v in xs) t += v;
                return t;
            }
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.WithId("EBC2003"));   // 不可变容器必须可作确定性入口
    }

    // 数组作为确定性入口被拒（设计立场）：钉住该语义，防止无意放宽或无意收紧。
    [Fact]
    public void MutableArrayEntry_IsRejected_ByDesign()
    {
        var r = CompilationFixture.Run("""
        using EffectLedger.Contracts;

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Sum(int[] xs) => xs.Length;
        }
        """);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC2003"));   // 调用方可并发改写数组
    }

    // F-1（优雅审计 MAJOR）：两个**已声明 ImmutableValue** 的类型组合，
    // 存储路径必须与引擎的暴露检查同口径（都承认声明契约），否则主要用例被误红。
    [Fact]
    public void ComposingDeclaredImmutableValues_Passes()
    {
        var r = CompilationFixture.Run("""
        using EffectLedger.Contracts;

        public sealed class Item : IConstrained<ImmutableValue>
        {
            private readonly int _v;
            public Item(int v) { _v = v; }
            public int V => _v;
        }

        public sealed class Bag : IConstrained<ImmutableValue>
        {
            private readonly Item _i;
            public Bag(Item i) { _i = i; }
            public int V => _i.V;
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());   // 声明契约的元素不得被当作可变别名
    }

    // F-3（优雅审计 MAJOR）：文档推荐的迁移目标是 ImmutableArray，
    // 读它的成员必须有据可查，否则"照文档改"会从假红掉进 Unknown 红。
    [Fact]
    public void ImmutableArrayMembers_AreKnownSafe()
    {
        var r = CompilationFixture.Run("""
        using System.Collections.Immutable;
        using EffectLedger.Contracts;

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Len(ImmutableArray<int> xs) => xs.Length;
            public int At(ImmutableArray<int> xs, int i) => xs[i];
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // F-16（优雅审计 MODERATE）：用户枚举器**有源码**时不得再附赠 ExternalSummaryMissing。
    [Fact]
    public void UserEnumeratorWithSource_ProducesNoSpuriousUnknown()
    {
        var r = CompilationFixture.Run("""
        using System.Collections.Generic;
        using EffectLedger.Contracts;

        public sealed class E
        {
            public E GetEnumerator() => this;
            public bool MoveNext() => false;
            public int Current => 1;
        }

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public int Go() { int s = 0; foreach (var x in new E()) s += x; return s; }
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.WithId("EBC9001"));   // 可解析的枚举器不该报 Unknown
    }

    // 假阴（用户视角审计）：构造期把 this 交给**静态集合的 mutator** 同样是注册期逃逸，
    // 文档明确承诺"构造期写入静态位置"会被检出——只拦直接字段赋值不足以兑现。
    [Fact]
    public void ThisIntoStaticCollection_IsDetected()
    {
        var r = CompilationFixture.Run("""
        using System.Collections.Generic;
        using EffectLedger.Contracts;

        public static class StaticBag { public static readonly List<object> Items = new(); }

        public sealed class Registers : IConstrained<ImmutableValue>
        {
            public Registers() { StaticBag.Items.Add(this); }
        }
        """);
        Assert.True(r.Compiled);
        Assert.True(r.EbcViolations().Any(),
            "构造期把 this 交给静态集合应被检出为 receiver 逃逸");
    }

    // BLOCKER-1（第三轮用户视角审计）：角色经**继承**获得时，基类的可变状态与修改方法
    // 必须并入派生根——否则把 mutator 上移一层即可逃逸 ImmutableValue（假绿）。
    [Fact]
    public void InheritedRole_DoesNotEvadeContract()
    {
        var r = CompilationFixture.Run("""
        using System.Collections.Generic;
        using EffectLedger.Contracts;

        public class BaseRepo : IConstrained<ImmutableValue>
        {
            protected readonly List<string> Rows = new();
            public void AddRow(string r) { Rows.Add(r); }
        }
        public sealed class DerivedRepo : BaseRepo { }
        """);
        Assert.True(r.Compiled);
        // 关键：判据必须落到**派生根**上——只报基类等于允许"把 mutator 上移一层"逃逸。
        var derivedDiags = r.EbcViolations()
            .Where(d => d.GetMessage().Contains("DerivedRepo", StringComparison.Ordinal))
            .ToList();
        Assert.True(derivedDiags.Any(),
            "派生类经继承获得 ImmutableValue 后修改基类状态，必须报在派生类上；" +
            "实际诊断=[" + string.Join("; ", r.EbcViolations().Select(d => d.Id + ":" + d.GetMessage())) + "]");
    }

    // BLOCKER-2：ImmutableValue 下的静态可变状态写入必须与确定性角色同判（此前完全静默）。
    [Fact]
    public void StaticMutableStateUnderImmutableValue_IsDetected()
    {
        var r = CompilationFixture.Run("""
        using System.Collections.Generic;
        using EffectLedger.Contracts;

        public sealed class Counter1 : IConstrained<ImmutableValue>
        {
            private static int _n;
            public int Next() { _n++; return _n; }
        }
        public sealed class Registry : IConstrained<ImmutableValue>
        {
            private static readonly List<string> _names = new();
            public void Register(string x) { _names.Add(x); }
        }
        """);
        Assert.True(r.Compiled);
        Assert.True(r.WithId("EBC1001").Any(),
            "静态可变状态写入应报 EBC1001（与确定性角色的同款判定对齐）");
    }

    // P1.4/P4.4：用户摘要配置入口（解除跨工程 Unknown 死局——审计判定的最大采纳障碍）。
    // 用真实存在但**不在 BCL 目录**的外部符号（Convert.ToInt32(string) 走 CurrentCulture）：
    // 无摘要 ⇒ Unknown；有摘要 ⇒ 解析为具体违规（trust，标注 reason/evidenceRef）。
    [Fact]
    public void UserSummary_ResolvesExternalSymbol_ToSpecificViolation()
    {
        const string json = """
        {
          "schemaVersion": "1.0.0",
          "policy": { "allowUserSummaries": true },
          "summaries": [
            { "symbolId": "System.Convert::ToInt32",
              "effect": "hidden-culture",
              "reason": "字符串解析受 CurrentCulture 影响，已确认",
              "evidenceRef": "docs/adr/0012-convert-culture.md" }
          ]
        }
        """;
        var result = CompilationFixture.EvaluateWithConfig("""
namespace Probe;
public sealed class Calc : EffectLedger.Contracts.IConstrained<EffectLedger.Contracts.DeterministicComputation>
{
    public int Go(string s) => System.Convert.ToInt32(s);
}
""", json);
        Assert.True(result.Violations.Count > 0, "用户摘要须把外部符号解析为具体违规");
        Assert.Contains("EBC2001", result.Violations.Select(v => v.Id));
        // 注意：Assert.Contains(子串, IEnumerable<string>) 检查的是**元素相等**而非子串，
        // 必须用谓词重载（实测此处踩过：消息含"用户摘要"但元素不相等 ⇒ 假红）。
        Assert.Contains(result.Violations.Select(v => v.GetMessage()),
            m => m.Contains("用户摘要") && m.Contains("trust"));
    }

    // 对照：无摘要时同一符号保持 Unknown（绝不静默当纯）。
    [Fact]
    public void SameExternalSymbol_WithoutSummary_StaysUnknown()
    {
        var r = CompilationFixture.Run("""
namespace Probe;
public sealed class Calc : EffectLedger.Contracts.IConstrained<EffectLedger.Contracts.DeterministicComputation>
{
    public int Go(string s) => System.Convert.ToInt32(s);
}
""");
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC9001"));
    }

    [Fact]
    public void ConfigWithDuplicateSymbol_ReportedAsInvalid()
    {
        const string json = """
        {
          "schemaVersion": "1.0.0",
          "summaries": [
            { "symbolId": "A::B", "effect": "pure", "reason": "r", "evidenceRef": "e" },
            { "symbolId": "A::B", "effect": "pure", "reason": "r", "evidenceRef": "e" }
          ]
        }
        """;
        var cfg = ContractConfigParser.Parse(json);
        Assert.False(cfg.IsValid);
        Assert.Contains(cfg.Errors, e => e.Contains("冲突摘要") || e.Contains("重复"));
    }

    [Fact]
    public void ConfigMissingReason_IsRejected()
    {
        const string json = "{ \"schemaVersion\": \"1.0.0\", \"summaries\": [ { \"symbolId\": \"A::B\", \"effect\": \"pure\" } ] }";
        var cfg = ContractConfigParser.Parse(json);
        Assert.False(cfg.IsValid);
        Assert.Contains(cfg.Errors, e => e.Contains("reason"));
    }

    // BC-138（计划 §2.1）：同一类型声明多个互斥角色 ⇒ EBC0001，不得双双放行。
    [Fact]
    public void DualRoleDeclaration_IsRejected()
    {
        var r = CompilationFixture.Run("""
using EffectLedger.Contracts;
namespace T;
public sealed class Both : IConstrained<ImmutableValue>, IConstrained<DeterministicComputation>
{
    public int X => 1;
}
""");
        Assert.True(r.Compiled);
        Assert.Contains(r.EbcViolations(), d =>
            d.Id == "EBC0001" && d.GetMessage().Contains("2 个互斥角色"));
    }

    // BC-139（计划 §2.2）：受约束 class 须 sealed。
    [Fact]
    public void UnsealedConstrainedClass_IsRejected()
    {
        var r = CompilationFixture.Run("""
using EffectLedger.Contracts;
namespace T;
public class NotSealed : IConstrained<ImmutableValue>
{
    public int X => 1;
}
""");
        Assert.True(r.Compiled);
        Assert.Contains(r.EbcViolations(), d =>
            d.Id == "EBC0001" && d.GetMessage().Contains("sealed"));
    }

    // 对照：sealed 的合法声明不受影响。
    [Fact]
    public void SealedConstrainedClass_Passes()
    {
        var r = CompilationFixture.Run("""
using EffectLedger.Contracts;
namespace T;
public sealed class Ok : IConstrained<ImmutableValue>
{
    public int X => 1;
}
""");
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }
}
