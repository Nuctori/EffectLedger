// SanityTests.cs — 脚手架冒烟测试（TDD 起点）。后续轮次填充 §2–§7 真实用例。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public class SanityTests
{
    [Fact]
    public void Runtime_Scaffold_Compiles()
    {
        var id = new FiberId("f1");
        Assert.Equal("f1", id.Value);
        Assert.Equal(FiberState.Inactive, new Fiber(id, Signature.Empty, new Coeffect(new ResourceId.Memory(0), new ResourceId.Memory(0), new ScopeId.Shell()), ImmutableStack<InverseClaim>.Empty).State);
    }
}
