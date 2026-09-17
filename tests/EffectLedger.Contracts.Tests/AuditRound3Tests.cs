// AuditRound3Tests.cs — 多角度审计（假绿猎手）发现的 13 个家族的判别性回归。
// 全部断言"必须产生违规或 Unknown"，且对每个家族配合法对照，防止用过度保守（全部 Unknown）冒充修复。

using System.Linq;
using EffectLedger.Contracts.Tests.Testing;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class AuditRound3Tests
{
    private const string Header = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using EffectLedger.Contracts;
        """;

    /// <summary>
    /// 断言指定形状被检出，且**必须落到具体违规诊断**（不能靠 Unknown 蒙混）。
    /// 审计 F-10：此前只断言"有任何 EBC" —— 把所有违规都退化成 ExternalSummaryMissing
    /// 也能全绿，等于没钉住任何规则。
    /// </summary>
    private static void MustFlag(string body, string because, string expectedId)
    {
        var r = CompilationFixture.Run(Header + "\n" + body);
        Assert.True(r.Compiled, "fixture must compile — " + because);
        Assert.True(r.WithId(expectedId).Any(),
            $"应产生具体违规 {expectedId}（{because}）；实际诊断=[" +
            string.Join("; ", r.EbcViolations().Select(d => d.Id)) + "]");
    }

    // ── F1：公共属性 getter 曾是分析盲区 ──
    [Fact]
    public void F1_PublicPropertyGetter_ReadingClock_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public int Now => DateTime.Now.Day;
            }
            """, "公共属性 getter 读时钟", "EBC2001");

    [Fact]
    public void F1_PublicPropertyGetter_WithIO_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public int Side { get { Console.WriteLine("s"); return 1; } }
            }
            """, "公共属性 getter 产生 IO", "EBC2002");

    // F1 合法对照
    [Fact]
    public void F1_PurePublicPropertyGetter_Passes()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            private readonly int _x = 1;
            public int X => _x;
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // ── F2：索引器 ──
    [Fact]
    public void F2_IndexerGetter_ReadingClock_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public int this[int i] => DateTime.Now.Day;
            }
            """, "索引器 getter 读时钟", "EBC2001");

    [Fact]
    public void F2_Indexer_LeakingInternalMutable_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                private readonly List<int> _l = new();
                public List<int> this[int i] => _l;
            }
            """, "索引器暴露内部可变集合", "EBC1002");

    // ── F3：用户运算符 ──
    [Fact]
    public void F3_UserOperator_ReadingClock_IsFlagged() =>
        MustFlag("""
            public struct OX { public int V; public static int operator +(OX a, OX b) => DateTime.Now.Day; }
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public int M(OX a, OX b) => a + b;
            }
            """, "用户运算符读时钟", "EBC2001");

    // ── F4：装箱进 object 字段 ──
    [Fact]
    public void F4_BoxingMutableIntoObjectField_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                private readonly object _o;
                public C(List<int> x) { _o = x; }
                public object O => _o;
            }
            """, "把可变集合装箱存入 object 字段", "EBC1002");

    // ── F5：构造期 this 存入静态字段 ──
    [Fact]
    public void F5_ThisStoredIntoStaticField_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                public static object? Slot;
                public C() { Slot = this; }
            }
            """, "构造期把 this 存入静态字段", "EBC1003");

    // ── F6：值类型内含可变引用 ──
    [Fact]
    public void F6_ValueTypeHoldingMutable_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                public KeyValuePair<string, List<int>> P { get; }
                public C(List<int> v) { P = new KeyValuePair<string, List<int>>("k", v); }
            }
            """, "只读属性暴露含可变引用的值类型", "EBC1002");

    // ── F7：Span/Memory 暴露 ──
    [Fact]
    public void F7_SpanOverInternalArray_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                private readonly int[] _a = new int[4];
                public Span<int> S => _a;
            }
            """, "Span 暴露内部数组", "EBC9001");

    // ── F8：fixed 指针写入 ──
    [Fact]
    public void F8_FixedPointerWrite_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                private readonly int[] _a = new int[4];
                public unsafe int M() { fixed (int* p = _a) { *p = 1; return *p; } }
            }
            """, "fixed 指针写入内部数组", "EBC9001");

    // ── F9：指针入口参数 ──
    [Fact]
    public void F9_PointerEntryParameter_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public unsafe int M(int* p) => *p;
            }
            """, "指针入口参数不是稳定值", "EBC2003");

    // ── F10：公共 setter 破坏不可变 ──
    [Fact]
    public void F10_PublicSetterOnImmutable_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                public int X { get; set; }
            }
            """, "ImmutableValue 上的公共 setter", "EBC1001");

    // ── F11：确定性计算修改自身 receiver 状态 ──
    [Fact]
    public void F11_DeterministicMutatingOwnState_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                private int _c;
                public int M(int x) { _c = x; return _c; }
            }
            """, "确定性计算修改 receiver 字段", "EBC2002");

    [Fact]
    public void F11_DeterministicMutatingOwnCollection_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                private readonly List<int> _l = new();
                public void M() { _l.Add(1); }
            }
            """, "确定性计算修改 receiver 集合", "EBC2002");

    // ── F12：文化敏感插值 ──
    [Fact]
    public void F12_CultureSensitiveInterpolation_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public string M(decimal d) => $"{d:C}";
            }
            """, "文化敏感的插值格式化", "EBC2001");

    // ── F13：ref/out 参数写入 ──
    [Fact]
    public void F13_RefParameterMutation_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<ImmutableValue>
            {
                public void M(ref List<int> x) { x.Add(1); }
            }
            """, "经 ref 参数修改调用方状态", "EBC1002");

    [Fact]
    public void F13_OutParameterWrite_IsFlagged() =>
        MustFlag("""
            public sealed class C : IConstrained<DeterministicComputation>
            {
                public void M(out List<int> x) { x = new List<int>(); }
            }
            """, "out 参数写入", "EBC2003");

    [Fact]
    public void F13_MutatingPassedInObject_IsFlagged() =>
        MustFlag("""
            public sealed class Cnt { public int N; }
            public sealed class C : IConstrained<ImmutableValue>
            {
                public void M(Cnt c) { c.N++; }
            }
            """, "修改传入对象的状态", "EBC1001");

    // ── 合法对照：这些必须仍然通过，防止"一律报 Unknown"式假修复 ──
    [Fact]
    public void LegitPatterns_StillPass()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class Value : IConstrained<ImmutableValue>
        {
            private readonly int _x;
            public Value(int x) { _x = x; }
            public int X => _x;
        }

        public sealed class Calc : IConstrained<DeterministicComputation>
        {
            public int Sum(IReadOnlyList<int> xs)
            {
                var local = new List<int>();
                foreach (var v in xs) local.Add(v);
                int s = 0;
                foreach (var v in local) s += v;
                return s;
            }
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // ── 收敛轮：合法代码必须通过（反向假红钉）──

    [Fact]
    public void Legit_DefensiveCopy_Passes()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class C : IConstrained<ImmutableValue>
        {
            private readonly List<int> _items;
            public C(IReadOnlyList<int> input) { _items = new List<int>(input); }
            public int Count => _items.Count;
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    [Fact]
    public void Legit_Record_Passes()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed record Point(int X, int Y) : IConstrained<ImmutableValue>;
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    [Fact]
    public void Legit_RecordWithClockInitializer_IsFlagged()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed record Bad(int X) : IConstrained<DeterministicComputation>
        {
            public int Stamp { get; } = DateTime.UtcNow.Day;
        }
        """);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.EbcViolations());
    }

    [Fact]
    public void Legit_DeterministicIteratingOwnReadOnlyList_Passes()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            private readonly IReadOnlyList<int> _xs;
            public C(IReadOnlyList<int> xs) { _xs = xs; }
            public int Sum() { int s = 0; foreach (var x in _xs) s += x; return s; }
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    [Fact]
    public void Legit_HoldingDeclaredImmutableElements_Passes()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class Item : IConstrained<ImmutableValue>
        {
            private readonly int _v;
            public Item(int v) { _v = v; }
            public int V => _v;
        }

        public sealed class Bag : IConstrained<ImmutableValue>
        {
            private readonly IReadOnlyList<Item> _xs;
            // 防御性拷贝：持有外部集合的正确做法（文档：IReadOnlyList 包装不算冻结）。
            public Bag(IReadOnlyList<Item> xs) { _xs = xs.ToArray(); }
            public IReadOnlyList<Item> Items => _xs;
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // 收敛轮 BC-069：插值文化判定的精确化（双向）。
    [Fact]
    public void Interpolation_PlainStringConcat_Passes()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public string Greet(string name) => $"hello {name}";
            public string N(int v) => $"count={v}";
        }
        """);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    [Fact]
    public void Interpolation_DefaultDecimalFormat_IsFlagged()
    {
        var r = CompilationFixture.Run(Header + """

        public sealed class C : IConstrained<DeterministicComputation>
        {
            public string M(decimal d) => $"total {d}";
        }
        """);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC2001"));
    }
}
