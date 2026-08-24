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
        Assert.Throws<LoadValidationException>(() => LoadValidation.ValidateForLoad(f, System.Array.Empty<Fiber>()));
    }

    // ───────────── reviewer #185 blocker 1 (R5-6 双重释放交叉判定) ─────────────

    static Fiber Fiber(ImmutableStack<InverseClaim> inv, ScopeId scope, Coeffect coeffect)
        => new(new FiberId("f"), Signature.Empty, coeffect, inv);

    [Fact]
    public void ValidateDoubleRelease_Fails_WhenInverseReleasesOtherProvidersResource_WithoutTags()
    {
        var shell = new ScopeId.Shell();
        var provider = Fiber(ImmutableStack<InverseClaim>.Empty, shell);
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), shell));
        var releaser = new Fiber(new FiberId("r"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), shell), inv);
        var all = new[] { provider, releaser };
        Assert.Throws<LoadValidationException>(() => LoadValidation.ValidateForLoad(releaser, all));
    }

    [Fact]
    public void ValidateDoubleRelease_Pass_WhenInverseReleasesOtherProvidersResource_WithValidReleaseTag()
    {
        var shell = new ScopeId.Shell();
        var provider = Fiber(ImmutableStack<InverseClaim>.Empty, shell);
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), shell, "queue_free"));
        var releaser = new Fiber(new FiberId("r"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), shell), inv);
        var all = new[] { provider, releaser };
        LoadValidation.ValidateForLoad(releaser, all); // 合法 release-class 标签 ⇒ 通过
    }

    // ───────────── reviewer #185 blocker 2 (§5 net 收益闭合接线) ─────────────

    [Fact]
    public void VerifyNetClosure_Throws_WhenFiberNotConserved()
    {
        // §3.3.1：net 仅含 Kind.Occupy 桶；Write/Read 不进 net（量纲隔离）。
        // 正确构造“非闭合”纤维：提供 Memory(0)（Occupy/Create）但无任何释放 ⇒ net 正区间不跨 0 ⇒ 闸门必须抛。
        var shell = new ScopeId.Shell();
        var f = new Fiber(new FiberId("x"),
            Signature.Of(new Claim(Kind.Occupy, new ResourceId.Memory(0), Mode.Create, shell, Interval.Default)),
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), shell), ImmutableStack<InverseClaim>.Empty);
        Assert.Throws<LoadValidationException>(() => LoadValidation.VerifyNetClosure(new[] { f }));
    }

    [Fact]
    public void VerifyNetClosure_Pass_WhenInverseReleasesProvidedResource() // reviewer #188 F1：真实装载语义——提供 R 且有逆释放 R ⇒ net 0 ⇒ 闸门放通
    {
        var shell = new ScopeId.Shell();
        // 有效签名 = Effect ∪ create(Memory0) ∪ release(Memory0) ⇒ net 0 ⇒ 闭合。
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), shell, "queue_free"));
        var f = new Fiber(new FiberId("x"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), shell), inv);
        LoadValidation.VerifyNetClosure(new[] { f }); // 不抛 ⇒ §5 闸门对真实装载生效
    }

    [Fact]
    public void VerifyNetClosure_Throws_WhenProvidedResourceNeverReleased() // reviewer #188 F1：提供 R 但无逆释放 R ⇒ 泄漏 ⇒ 闸门拦截
    {
        var shell = new ScopeId.Shell();
        // 有效签名 = create(Memory0) 但无 release ⇒ net 正（泄漏）⇒ 闸门抛 LoadValidationException。
        var f = new Fiber(new FiberId("x"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), shell), ImmutableStack<InverseClaim>.Empty);
        Assert.Throws<LoadValidationException>(() => LoadValidation.VerifyNetClosure(new[] { f }));
    }

    [Fact]
    public void VerifyNetClosure_Pass_WhenEffectAlreadyCreatesProvidedResource() // reviewer #189 F4：Effect 已含 create(Provides) 时不重复折入，避免 fail-closed 过度拒绝
    {
        var shell = new ScopeId.Shell();
        // Effect 已声明 create(Memory0) + release(Memory0)（净 0）；EffectiveSignature 不应再折入第二份 create(Provides=Memory0)。
        var s = Signature.Of(
            new Claim(Kind.Occupy, new ResourceId.Memory(0), Mode.Create, shell, null),
            new Claim(Kind.Occupy, new ResourceId.Memory(0), Mode.Release, shell, null));
        var f = new Fiber(new FiberId("x"), s,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), shell), ImmutableStack<InverseClaim>.Empty);
        LoadValidation.VerifyNetClosure(new[] { f }); // 不抛 ⇒ 未重复折入导致过度拒绝
    }

    [Fact]
    public void ValidateDoubleRelease_Pass_WhenInverseReleasesOwnProvidedResource() // reviewer #187 F2：释放自身提供的资源是合法逆，不误判双重释放
    {
        var shell = new ScopeId.Shell();
        // fiber 提供 memory(0) 且逆释放 memory(0)（同一资源，自引用）——合法所有权逆，不应抛。
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), shell, "queue_free")); // 合法 release tag ⇒ ValidateReleaseClass 通过
        var f = Fiber(inv, shell); // Coeffect.Provides = Memory(0)（默认 Spec 提供同资源）
        LoadValidation.ValidateForLoad(f, new[] { f }); // 自引用释放（同资源）⇒ R5-6 不误判 ⇒ 通过
    }

    [Fact]
    public void ValidateDoubleRelease_Pass_WhenCrossScopeSameResource() // reviewer #187 F2：同资源但不同 Scope 不构成双重释放
    {
        var shell = new ScopeId.Shell();
        var scene = new ScopeId.Scene("s");
        // provider 在 shell 提供 memory(0)；另一 fiber 在 scene 逆释放 memory(0)——不同 Scope，不触发跨 provider 双重释放判定。
        var provider = Fiber(ImmutableStack<InverseClaim>.Empty, shell);
        var inv = ImmutableStack<InverseClaim>.Empty.Push(Inv(new ResourceId.Memory(0), scene, "queue_free")); // 合法 release tag
        var releaser = new Fiber(new FiberId("r"), Signature.Empty,
            new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), scene), inv);
        var all = new[] { provider, releaser };
        LoadValidation.ValidateForLoad(releaser, all); // provider 在 shell、releaser 在 scene（不同 Scope）⇒ R5-6 不误判 ⇒ 通过
    }
}
