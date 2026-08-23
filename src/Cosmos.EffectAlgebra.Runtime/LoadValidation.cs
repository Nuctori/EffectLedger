// LoadValidation.cs — §3 step2b + §7.1 装载期校验（scale-closure + release-class 双重释放防护）+ §5 net 闭合接线。
using System.Collections.Generic;
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§3/§7.1/§5 — 装载期校验异常。</summary>
public sealed class LoadValidationException : Exception
{
    public LoadValidationException(string msg) : base(msg) { }
}

/// <summary>§3 step2b + §7.1 + §5 — 装载前静态校验：声明维度闭合（scope 闭合）+ 释放类双重释放防护（逆 Action 须是 release-class API）+ §5 per-Fiber net 收益闭合。</summary>
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

    /// <summary>R5-6（reviewer #185 blocker 1）— 双重释放防护的核心交叉判定：若某逆引用的资源 == 某 provider 的 Provides（即该逆释放的资源由另一 Fiber 提供），
    /// 则其 ReleaseApiTags 必须 ∈ ReleaseClass（真正的释放操作），否则视为"未真正逆"的裸释放 = 双重释放风险。
    /// 软边也在此自动派生：逆引用他 provider 资源的 Fiber 自动成为该 provider 的软依赖（由 AddDependency 路径调用）。</summary>
    public static void ValidateDoubleRelease(Fiber fiber, IReadOnlyCollection<Fiber> allFibers)
    {
        foreach (var inv in fiber.Inverses)
        {
            foreach (var other in allFibers)
            {
                if (other.Id == fiber.Id) continue;
                if (ResourceId.Normalize(inv.Resource) == ResourceId.Normalize(other.Coeffect.Provides))
                {
                    // 逆引用了他 provider 提供的资源 ⇒ 该逆必须是真正的 release-class 释放
                    if (inv.ReleaseApiTags == null || inv.ReleaseApiTags.Count == 0)
                        throw new LoadValidationException(
                            $"Fiber {fiber.Id}：逆 {inv.Resource} 释放了 provider {other.Id} 提供的资源，但未标注 ReleaseApiTags（R5-6 双重释放防护：须声明 release-class API）");
                    foreach (var tag in inv.ReleaseApiTags)
                        if (!ReleaseClass.IsRelease(tag))
                            throw new LoadValidationException(
                                $"Fiber {fiber.Id}：逆 {inv.Resource} 释放 provider {other.Id} 资源，但标签 '{tag}' 非 release-class（R5-6）");
                }
            }
        }
    }

    /// <summary>§5（reviewer #185 blocker 2）— per-Fiber net 收益闭合接线：对注册集合整体校验每 Fiber 在自身 Scope 内的有符号 net 是否含 0（生命周期闭合）。
    /// 接入装载/卸载路径：任一 Fiber 未闭合（含 ⊤ 或未记录）⇒ 抛异常，使 §5 闸门在运行时真正生效（非死代码）。</summary>
    public static void VerifyNetClosure(IEnumerable<Fiber> fibers)
    {
        var result = NetBenefitClosure.CheckAll(fibers);
        if (!result.Conserved)
        {
            var list = string.Join(", ", result.ViolatingResources.Select(r => r.ToString()));
            throw new LoadValidationException(
                $"§5 net 收益闭合失败：以下资源未闭合（生命周期不闭合）：[{list}]");
        }
    }

    /// <summary>§3/§7.1/§5 — 组合校验（调用方在 Load 前执行）。</summary>
    public static void ValidateForLoad(Fiber fiber, IReadOnlyCollection<Fiber> allFibers)
    {
        ValidateScaleClosure(fiber);
        ValidateReleaseClass(fiber);
        ValidateDoubleRelease(fiber, allFibers);
    }
}
