// LoadValidation.cs — §3 step2b + §7.1 装载期校验（scale-closure + release-class 双重释放防护）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3/§7.1 — 装载期校验异常。</summary>
public sealed class LoadValidationException : Exception
{
    public LoadValidationException(string msg) : base(msg) { }
}

/// <summary>§3 step2b + §7.1 — 装载前静态校验：声明维度闭合（scope 闭合）+ 释放类双重释放防护（逆 Action 须是 release-class API）。</summary>
public static class LoadValidation
{
    /// <summary>§3 step2b — 每个逆的 Scope 须 == Fiber.Coeffect.Scope（声明维度闭合；否则逆回放时 scale 不匹配 = 死锁/越界释放风险）。</summary>
    public static void ValidateScaleClosure(Fiber fiber)
    {
        foreach (var inv in fiber.Inverses)
            if (inv.Scope != fiber.Scope)
                throw new LoadValidationException(
                    $"Fiber {fiber.Id}：逆 {inv.Resource} 的 Scope({inv.Scope}) != 系数 Scope({fiber.Scope})（§3 step2b scale 不闭合）");
    }

    /// <summary>§7.1 — 释放类双重释放防护：逆 Action 涉及 API 时须显式标注 ReleaseApiTags；
    /// 非空标签须全部 ∈ ReleaseClass（queue_free/free/...），否则逆声明的是非释放操作 = 未真正逆（裸 Action 须显式 opt-out 留空标签）。</summary>
    public static void ValidateReleaseClass(Fiber fiber)
    {
        foreach (var inv in fiber.Inverses)
        {
            if (inv.ReleaseApiTags == null) continue; // 显式 opt-out（裸 Action 自证为安全逆）
            if (inv.ReleaseApiTags.Count == 0)
                throw new LoadValidationException(
                    $"Fiber {fiber.Id}：逆 {inv.Resource} 标注了空 ReleaseApiTags（须列具体 release API 或留 null opt-out）");
            foreach (var tag in inv.ReleaseApiTags)
                if (!ReleaseClass.IsRelease(tag))
                    throw new LoadValidationException(
                        $"Fiber {fiber.Id}：逆 {inv.Resource} 的标签 '{tag}' 非 release-class（§7.1 须为 {string.Join("/", ReleaseClass.All)} 之一）");
        }
    }

    /// <summary>§3/§7.1 — 组合校验（调用方在 Load 前执行）。</summary>
    public static void ValidateForLoad(Fiber fiber)
    {
        ValidateScaleClosure(fiber);
        ValidateReleaseClass(fiber);
    }
}
