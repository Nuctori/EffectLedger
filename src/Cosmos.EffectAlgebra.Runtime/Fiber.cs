// Fiber.cs — §1/§2 Fiber（状态机 + 幂等守卫 + 看门狗转移）+ 逆声明（InverseClaim）+ 协效应（Coeffect）。
using System.Collections.Immutable;
using Cosmos.EffectAlgebra;

namespace Cosmos.EffectAlgebra.Runtime;

/// <summary>§1 — Fiber 标识。</summary>
public readonly record struct FiberId(string Value);

/// <summary>§2 — Fiber 生命周期状态机（五态）。</summary>
public enum FiberState
{
    Inactive,    // 未装载
    Active,      // 已装载、provider 可用
    Suspending,  // provider 通知拆除中（壳强制暂停，§7）
    TearingDown, // teardown 任务已入队
    Dead         // 已完成逆回放，资源释放
}

/// <summary>§1 — 协效应：所需/所提供资源（同 Scope 内构成显式依赖边）。</summary>
public sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope);

/// <summary>§1 — 结构化逆：声明维度(ResourceId/ScopeId)齐全；Scope 须 == Coeffect.Scope（装载期校验）。
/// ReleaseApiTags：逆 Action 涉及的释放类 API 名（§7.1/§3 step2b 装载期双重释放判定用）；
/// 因 Action 是裸委托无 API 名元数据，须显式标注（audit/runtime-mvp-spec.md E）。
/// </summary>
public sealed record InverseClaim(
    ResourceId Resource,
    ScopeId Scope,
    Action Execute,
    IReadOnlySet<string>? ReleaseApiTags = null);

/// <summary>§1 — 插件组件（Fiber）。MVP：状态机 + 幂等守卫 + 看门狗转移。</summary>
public sealed class Fiber
{
    public FiberId Id { get; }
    public FiberState State { get; internal set; } = FiberState.Inactive;
    public ScopeId Scope => Coeffect.Scope;
    public Signature Effect { get; }
    public Coeffect Coeffect { get; }
    public ImmutableStack<InverseClaim> Inverses { get; }
    public ImmutableHashSet<FiberId> Dependents { get; internal set; } = ImmutableHashSet<FiberId>.Empty;
    public bool TeardownEnqueued { get; internal set; }

    /// <summary>§5（reviewer #188 F1 / #189 F4）— 装载期有效签名：Effect ∪ create(Provides)（仅当 Effect 未含同名 create 时）∪ 「释放自身 Provides」的逆 release。
    /// 仅折入释放【自身 Provides】的逆：跨 Fiber 借用资源的逆（释放他人 Provides）不计入自身生命周期 net，由提供方 net 守恒（R4-7 Scope 仅分组、绝不跨 Fiber 求和）。
    /// 折入逆声明使 §5 net 闭合闸门对真实装载生效（否则 Effect 恒 Empty、闸门恒真不拦截泄漏）；若用户 Effect 已含 create(Provides) 则不重复折入（避免 fail-closed 过度拒绝）。</summary>
    public Signature EffectiveSignature
    {
        get
        {
            bool effectAlreadyCreatesProvides = Effect.OccupyClaims.Any(c =>
                ResourceId.Normalize(c.Resource) == ResourceId.Normalize(Coeffect.Provides) && c.Mode == Mode.Create);
            var claims = new List<Claim>();
            if (!effectAlreadyCreatesProvides)
                claims.Add(new Claim(Kind.Occupy, Coeffect.Provides, Mode.Create, Coeffect.Scope, null));
            foreach (var inv in Inverses)
                if (ResourceId.Normalize(inv.Resource) == ResourceId.Normalize(Coeffect.Provides))
                    claims.Add(new Claim(Kind.Occupy, inv.Resource, Mode.Release, inv.Scope, null));
            return Signature.Union(Effect, Signature.Of(claims.ToArray()));
        }
    }

    public Fiber(FiberId id, Signature effect, Coeffect coeffect, ImmutableStack<InverseClaim> inverses)
    {
        Id = id;
        Effect = effect;
        Coeffect = coeffect;
        Inverses = inverses;
    }

    /// <summary>§2 — 装载（幂等守卫：非 Inactive 直接 return false）。</summary>
    public bool Load()
    {
        if (State != FiberState.Inactive) return false;
        State = FiberState.Active;
        return true;
    }

    /// <summary>§2 — 卸载（幂等守卫：TearingDown/Dead 直接 return；Suspending→TearingDown 允许（级联 teardown）；
    /// TeardownEnqueued 防二次入队）。</summary>
    public void Unload()
    {
        // Suspending 已进入“待卸载”态：级联显式 teardown 须推进到 TearingDown（不阻断）。
        if (State is FiberState.TearingDown or FiberState.Dead) return;
        if (TeardownEnqueued) return; // 防止重复入队（Suspending 经 NotifyProviderTeardown 后也走此路径）
        TeardownEnqueued = true;
        State = FiberState.TearingDown;
    }

    /// <summary>§2 — provider 通知 dependent 进入 Suspending（幂等：仅 Active → Suspendeding，其它态 no-op，防 R4-4 振荡）。</summary>
    public void NotifyProviderTeardown()
    {
        if (State == FiberState.Active) State = FiberState.Suspending;
    }

    /// <summary>§2 R4-9 — 看门狗帧计数强转 TearingDown（超时未 Dead 兜底；仅 Active/Suspending 可转）。</summary>
    public void ForceTeardownOnWatchdog()
    {
        if (State is FiberState.Active or FiberState.Suspending)
        {
            if (!TeardownEnqueued) TeardownEnqueued = true;
            State = FiberState.TearingDown;
        }
    }

    /// <summary>§4 — 标记完成逆回放（dead）。仅 TearingDown → Dead。</summary>
    public void MarkDead()
    {
        if (State == FiberState.TearingDown) State = FiberState.Dead;
    }
}
