// ImmutableValueTests.cs — P3 反例/正例/未知例回归。

using EffectLedger.Contracts.Tests.Testing;
using Xunit;

namespace EffectLedger.Contracts.Tests;

public class ImmutableValueTests
{
    private const string Header = """
        using System;
        using System.Collections.Generic;
        using EffectLedger.Contracts;
        """;

    // 正例：字段只读 + 标量稳定

    [Fact]
    public void ReadOnlyScalarField_Passes()
    {
        var src = Header + """

        public sealed class Snapshot : IConstrained<ImmutableValue>
        {
            private readonly int _total;
            public Snapshot(int total) { _total = total; }
            public int Total => _total;
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.Empty(r.EbcViolations());
    }

    // 反例：构造后修改字段

    [Fact]
    public void MutatingOwnField_AfterConstruction_Reports()
    {
        var src = Header + """

        public sealed class Mutable : IConstrained<ImmutableValue>
        {
            private int _count;
            public void Bump() { _count++; }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        Assert.NotEmpty(r.WithId("EBC1001"));
    }

    // 反例：readonly List 字段（伪不可变）

    [Fact]
    public void ReadOnlyListField_NotTreatedAsDeepImmutable()
    {
        var src = Header + """

        public sealed class HoldsList : IConstrained<ImmutableValue>
        {
            private readonly List<int> _items = new();
            public void Add(int x) { _items.Add(x); }
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        // 即使 _items 是 readonly，修改其元素仍违反深层不可变（接收者字段写入路径）。
        Assert.NotEmpty(r.EbcViolations());
    }

    // 反例：返回内部可变集合视图

    [Fact]
    public void ReturnsInternalList_NotFrozen()
    {
        var src = Header + """

        using System.Collections.ObjectModel;
        public sealed class Wrapper : IConstrained<ImmutableValue>
        {
            private readonly List<int> _inner = new();
            public IReadOnlyList<int> Items => _inner.AsReadOnly();
        }
        """;
        var r = CompilationFixture.Run(src);
        Assert.True(r.Compiled);
        // AsReadOnly 仍指向可变底层集合 -> 别名暴露（此处至少应产生 EBC1002 或 EBC9001）。
        Assert.NotEmpty(r.EbcViolations());
    }
}
