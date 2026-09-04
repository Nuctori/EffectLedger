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

/// <summary>§1 — 插件组件（Fiber）。MVP：状态机 + 幂等守卫 + 看门狗转移。
/// A3-06（生产审计批3）线程契约：本类型【非线程安全】（零锁）——全部状态迁移须在宿主主线程（帧循环）内调用；
/// 跨线程调用无可见性保证，属未定义行为（Godot 场景帧驱动模型即此假设）。</summary>
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
    /// <summary>R3-RT-04（三轮审计）：逆回放进行中标志——重入回放 loud 拒绝（双释放/多重重放守卫）；
    /// 看门狗自愈条件亦以此跳过「正在回放」的 fiber（排空中队列已清空，仅凭不在队列判定会误判）。</summary>
    public bool ReplayInProgress { get; internal set; }

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
            // R7-N5/P0-4 配套：内部聚合走 Union（显式集合语义），不经带重复检测的 Of——
            // A3-14（生产审计批3 注释正名）：此处机制是【同形 Claim 经 ImmutableHashSet 去重】，非"按资源聚合"——
            // Effect 已含不同 size/scope 的 release(Provides) 时两 claim 并存，net 可能多减 ⇒ fail-closed 误拒（已知锐边）。
            // 多个逆释放同一 Provides 合法的前提是各逆经 Union 折叠为同形 claim。
            var sig = Effect;
            foreach (var c in claims)
                sig = Signature.Union(sig, Signature.Of(c));
            return sig;
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

    /// <summary>§2 — 卸载（A3-01/A3-02，生产审计批3 契约修正）：
    /// Inactive（从未装载）⇒ 直达 Dead——D4「未 _Ready 也安全」：无已获取资源，逆回放会释放从未获取的句柄（双重释放类崩溃），跳过；
    /// Active/Suspending ⇒ TearingDown（级联 teardown 推进）；TearingDown/Dead 幂等 no-op。
    /// 本方法只做状态迁移，【不再置 TeardownEnqueued】——标志位语义收紧为「逆回放任务已入调度器队列」，
    /// 由 PluginRuntime.BeginTeardown / SynchronousExitDrain / TickWatchdog 真正入队时置位。
    /// 此前置位说谎：直接 Unload 后无人入队 ⇒ 永卡 TearingDown + Register 永久锁死（A3-01）。</summary>
    public void Unload()
    {
        if (State == FiberState.Dead) return;
        if (State == FiberState.Inactive)
        {
            State = FiberState.Dead;
            TeardownEnqueued = true; // teardown 语义已完结（无事可回放），防调度器再入队
            return;
        }
        if (State == FiberState.TearingDown) return;
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
