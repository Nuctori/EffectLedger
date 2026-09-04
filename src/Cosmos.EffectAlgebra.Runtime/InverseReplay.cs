// InverseReplay.cs — §4 结构化逆回放：LIFO、部分释放诊断、整任务 try/catch → Dead + 按拓扑序推进上游。
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§4 — 逆回放诊断：栈中异常的位置与已完成/未完成资源。
/// A3-08（生产审计批3）：FirstError 保留首个失败逆的原始异常（ExceptionDispatchInfo 捕获，含原始堆栈）——
/// 崩溃级联升级时作为 InnerException 传递，根因类型/堆栈不得在重新合成异常时丢失。</summary>
public sealed record PartialReleaseDiagnosis(
    bool AllCompleted,
    ImmutableArray<ResourceId> Completed,   // 已成功释放
    ImmutableArray<ResourceId> Pending,     // 未释放（异常前/后未执行）
    int FailedIndex,
    Exception? FirstError = null);

/// <summary>§4 — 结构化逆栈回放器（声明维度等价；不声称行为等价，行为闭合由运行时权威 §5）。</summary>
public static class InverseReplay
{
    /// <summary>§4 — LIFO 回放 Inverses 栈；栈中任一 Action 抛异常 → 记录部分释放诊断并尽量继续剩余逆（R4-6），最终标记 Dead。</summary>
    public static PartialReleaseDiagnosis ReplayAndDead(Fiber fiber)
    {
        // reviewer #188 F3：逆回放前置于 TearingDown（设计假设「仅 TearingDown 态回放」；否则对 Active 纤程回放会遗贸 Active 且 MarkDead 成 no-op）。
        if (fiber.State != FiberState.TearingDown)
            throw new InvalidOperationException($"逆回放须于 TearingDown 态进行（当前 {fiber.State}）；禁止对 Active/Suspending 纤程回放");
        // R3-RT-04（三轮审计）：重入守卫——逆 Action 内重入 runtime API（TickWatchdog 自愈条件在排空中为真/
        // 宿主直接调本方法）曾致整栈被反复回放（多重重放=双释放类）。重入 loud 抛，由调用方 try/catch 升级崩溃报告。
        if (fiber.ReplayInProgress)
            throw new InvalidOperationException($"Fiber {fiber.Id} 逆回放进行中禁止重入回放（R3-RT-04：重入=双释放/多重重放）");
        fiber.ReplayInProgress = true;
        try
        {
            return ReplayCore(fiber);
        }
        finally { fiber.ReplayInProgress = false; }
    }

    static PartialReleaseDiagnosis ReplayCore(Fiber fiber)
    {
        var completed = ImmutableArray.CreateBuilder<ResourceId>();
        var pending = ImmutableArray.CreateBuilder<ResourceId>();
        int failedIndex = -1;
        Exception? firstError = null;
        int i = 0;
        // 栈顶为最后压入（最内层声明），LIFO：从栈顶向栈底回放。
        foreach (var inv in fiber.Inverses)
        {
            try
            {
                inv.Execute();
                completed.Add(inv.Resource);
            }
            catch (Exception ex)
            {
                // 部分释放诊断（R4-6）：标记失败位置，资源留待 pending，继续回放剩余逆（或要求 Action 自包含幂等）。
                // A3-08：捕获原始异常对象（EDI 保留堆栈），供升级路径作 InnerException——裸 catch 曾把根因永久丢弃。
                if (failedIndex < 0) { failedIndex = i; firstError = ExceptionDispatchInfo.Capture(ex).SourceException; }
                pending.Add(inv.Resource);
                // 不中断：尽量释放其余资源，避免上游 provider 按拓扑序释放时 dependent 仍部分持有致二次崩溃。
            }
            i++;
        }
        fiber.MarkDead(); // 仅 TearingDown → Dead（§2）
        return new PartialReleaseDiagnosis(failedIndex < 0, completed.ToImmutable(), pending.ToImmutable(), failedIndex, firstError);
    }
}
