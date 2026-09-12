// QedP8LoadDeterminismPins.cs — 第八轮独立审计 CRITICAL（P5-8-01）Runtime 侧回归钉：
// LoadAll 内部经 NetBenefitClosure.Check（数学净和为 0 的资源应守恒放行）——
// 修复前 NetTable 按枚举顺序累加、ZStar 溢出⇒⊤ 吸收 ⇒ 约 22% 进程概率误拒合法 Fiber。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;
using Cosmos.EffectAlgebra.Runtime;
using Xunit;

namespace Cosmos.EffectAlgebra.Runtime.Tests;

public sealed class QedP8LoadDeterminismPins
{
    [Fact]
    public void LoadAll_OpposingHugeResources_NotRejected()
    {
        var rt = new PluginRuntime();
        var res = new ResourceId.Memory(11);
        var stack = ImmutableStack<InverseClaim>.Empty;
        stack = stack.Push(new InverseClaim(res, new ScopeId.Shell(), () => { }));
        var spec = new FiberSpec(new FiberId("p"), Signature.Empty,
            new Coeffect(res, res, new ScopeId.Shell()), stack);
        rt.Register(spec);

        rt.LoadAll(); // 修复前：枚举顺序不利时抛 LoadValidationException（数学净和实为 0）
        Assert.Empty(rt.CrashReports);
    }
}
