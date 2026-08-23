// Fiber.cs — 空间可逆插件系统核心类型（MVP 骨架）。
// 来源：docs/spatial-plugin-shell-design.md v7 §1/§2 + audit/runtime-mvp-spec.md。
// 零 Godot 依赖：Godot 交互经 IHost 抽象（§7）。本文件仅定义纯数据/状态类型。
namespace Cosmos.EffectAlgebra.Runtime;

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

/// <summary>
/// §1 — 结构化逆：声明维度(ResourceId/ScopeId/Mode.Release 等价)齐全；Scope 须 == Coeffect.Scope。
/// ReleaseApiTags：逆 Action 涉及的释放类 API 名（§7.1/§3 step2b 装载期双重释放判定用）；
/// 因 Action 是裸委托无 API 名元数据，须显式标注（audit/runtime-mvp-spec.md E）。
/// </summary>
public sealed record InverseClaim(
    ResourceId Resource,
    ScopeId Scope,
    Action Execute,
    IReadOnlySet<string>? ReleaseApiTags = null);

/// <summary>§1 — 插件组件（Fiber）。MVP 骨架：纯数据 + 状态字段；逻辑在后续 TDD 轮次填充。</summary>
public sealed class Fiber
{
    public FiberId Id { get; }
    public FiberState State { get; internal set; } = FiberState.Inactive;
    public ScopeId Scope => Coeffect.Scope;
    public Signature Effect { get; }
    public Coeffect Coeffect { get; }
    public ImmutableStack<InverseClaim> Inverses { get; }
    public IReadOnlySet<FiberId> Dependents { get; internal set; } = ImmutableHashSet<FiberId>.Empty;
    public bool TeardownEnqueued { get; internal set; }

    public Fiber(FiberId id, Signature effect, Coeffect coeffect, ImmutableStack<InverseClaim> inverses)
    {
        Id = id;
        Effect = effect;
        Coeffect = coeffect;
        Inverses = inverses;
    }

    /// <summary>§2 — 装载（幂等守卫：Active/Suspending/TearingDown 直接 return）。</summary>
    public bool Load() => State == FiberState.Inactive && (State = FiberState.Active) == FiberState.Active;

    /// <summary>§2 — 卸载（幂等守卫：Suspending/TearingDown/Dead 直接 return；TeardownEnqueued 防二次入队）。</summary>
    public void Unload()
    {
        if (State is FiberState.Suspending or FiberState.TearingDown or FiberState.Dead) return;
        if (TeardownEnqueued) return;
        TeardownEnqueued = true;
        State = FiberState.TearingDown;
    }

    /// <summary>§2 — provider 通知 dependent 进入 Suspending（幂等：仅 Active → Suspending）。</summary>
    public void NotifyProviderTeardown()
    {
        if (State == FiberState.Active) State = FiberState.Suspending;
    }
}
