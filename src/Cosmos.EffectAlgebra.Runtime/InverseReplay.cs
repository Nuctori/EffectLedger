// InverseReplay.cs — §4 结构化逆回放：LIFO、部分释放诊断、整任务 try/catch → Dead + 按拓扑序推进上游。
using System.Collections.Immutable;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§4 — 逆回放诊断：栈中异常的位置与已完成/未完成资源。</summary>
public sealed record PartialReleaseDiagnosis(
    bool AllCompleted,
    ImmutableArray<ResourceId> Completed,   // 已成功释放
    ImmutableArray<ResourceId> Pending,     // 未释放（异常前/后未执行）
    int FailedIndex);

/// <summary>§4 — 结构化逆栈回放器（声明维度等价；不声称行为等价，行为闭合由运行时权威 §5）。</summary>
public static class InverseReplay
{
    /// <summary>§4 — LIFO 回放 Inverses 栈；栈中任一 Action 抛异常 → 记录部分释放诊断并尽量继续剩余逆（R4-6），最终标记 Dead。</summary>
    public static PartialReleaseDiagnosis ReplayAndDead(Fiber fiber)
    {
        // reviewer #188 F3：逆回放前置于 TearingDown（设计假设「仅 TearingDown 态回放」；否则对 Active 纤程回放会遗贸 Active 且 MarkDead 成 no-op）。
        if (fiber.State != FiberState.TearingDown)
            throw new InvalidOperationException($"逆回放须于 TearingDown 态进行（当前 {fiber.State}）；禁止对 Active/Suspending 纤程回放");
        var completed = ImmutableArray.CreateBuilder<ResourceId>();
        var pending = ImmutableArray.CreateBuilder<ResourceId>();
        int failedIndex = -1;
        int i = 0;
        // 栈顶为最后压入（最内层声明），LIFO：从栈顶向栈底回放。
        foreach (var inv in fiber.Inverses)
        {
            try
            {
                inv.Execute();
                completed.Add(inv.Resource);
            }
            catch
            {
                // 部分释放诊断（R4-6）：标记失败位置，资源留待 pending，继续回放剩余逆（或要求 Action 自包含幂等）。
                if (failedIndex < 0) failedIndex = i;
                pending.Add(inv.Resource);
                // 不中断：尽量释放其余资源，避免上游 provider 按拓扑序释放时 dependent 仍部分持有致二次崩溃。
            }
            i++;
        }
        fiber.MarkDead(); // 仅 TearingDown → Dead（§2）
        return new PartialReleaseDiagnosis(failedIndex < 0, completed.ToImmutable(), pending.ToImmutable(), failedIndex);
    }
}
