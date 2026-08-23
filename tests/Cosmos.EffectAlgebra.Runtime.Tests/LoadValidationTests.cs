// LoadValidationTests.cs — §3 step2b + §7.1 装载期校验 TDD（reviewer spec D#9）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class LoadValidationTests
{
    static Fiber Fiber(ImmutableStack<InverseClaim> inv, ScopeId scope)
        => new(new FiberId("f"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), scope), inv);

    // 注意：空数组 → 空 ImmutableHashSet（非 null），用于测试“空标签须报错”；显式 null 走 opt-out 路径需直接构造。
    static InverseClaim Inv(ResourceId r, ScopeId s, params string[] tags)
        => new(r, s, () => { }, tags.ToImmutableHashSet());

    static InverseClaim InvOptOut(ResourceId r, ScopeId s)
        => new(r, s, () => { }, null); // 显式 opt-out

    [Fact]
    public void ValidateScaleClosure_Pass_WhenScopeMatches()
    {
        var s = new ScopeId.Shell();
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), s));
        var f = Fiber(inv, s);
        LoadValidation.ValidateScaleClosure(f); // 不抛
    }

    [Fact]
    public void ValidateScaleClosure_Fails_WhenScopeMismatch()
    {
        var shell = new ScopeId.Shell();
        var other = new ScopeId.Scene("other");
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), other));
        var f = Fiber(inv, shell); // 系数 scope=shell，逆 scope=scene → 不闭合
        Assert.Throws<LoadValidationException>(() => LoadValidation.ValidateScaleClosure(f));
    }

    [Fact]
    public void ValidateReleaseClass_Pass_WhenTagsAreReleaseClass()
    {
        var s = new ScopeId.Shell();
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), s, "queue_free"));
        var f = Fiber(inv, s);
        LoadValidation.ValidateReleaseClass(f); // queue_free ∈ ReleaseClass
    }

    [Fact]
    public void ValidateReleaseClass_Fails_WhenTagIsNonRelease()
    {
        var s = new ScopeId.Shell();
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), s, "spawn_monster"));
        var f = Fiber(inv, s); // spawn_monster ∉ ReleaseClass → 未真正逆
        Assert.Throws<LoadValidationException>(() => LoadValidation.ValidateReleaseClass(f));
    }

    [Fact]
    public void ValidateReleaseClass_Pass_WhenNullTags_OptOut()
    {
        var s = new ScopeId.Shell();
        var inv = ImmutableStack<InverseClaim>.Empty.Push(InvOptOut(new ResourceId.Memory(0), s)); // null tags = opt-out
        var f = Fiber(inv, s);
        LoadValidation.ValidateReleaseClass(f); // 不抛
    }

    [Fact]
    public void ValidateReleaseClass_Fails_WhenEmptyTags()
    {
        var s = new ScopeId.Shell();
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), s, new string[0])); // 空集 = 错误
        var f = Fiber(inv, s);
        Assert.Throws<LoadValidationException>(() => LoadValidation.ValidateReleaseClass(f));
    }

    [Fact]
    public void ValidateForLoad_Combines_BothChecks()
    {
        var shell = new ScopeId.Shell();
        var other = new ScopeId.Scene("other");
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), other, "queue_free"));
        var f = Fiber(inv, shell); // scope 不闭合 +（tag 合法但仍 scope 错）
        Assert.Throws<LoadValidationException>(() => LoadValidation.ValidateForLoad(f));
    }
}
