# Runtime 运行时壳层 MVP 实现规格（reviewer fc9af307，基于设计v7）

> 来源：独立 fresh-context reviewer 审计 docs/spatial-plugin-shell-design.md v7 + L1 源码，产出 MVP 实现规格。
> 用途：指导 Cosmos.EffectAlgebra.Runtime 的 TDD 实现与后续 10 轮审计细化。

## A. 对外接口草案（零 Godot 依赖，经 IHost 抽象）

```csharp
namespace Cosmos.EffectAlgebra.Runtime;
public sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope);
public sealed record InverseClaim(ResourceId Resource, ScopeId Scope, Action Execute,
    IReadOnlySet<string>? ReleaseApiTags = null);   // Action 无 API 名元数据，须补 tag
public enum FiberState { Inactive, Active, Suspending, TearingDown, Dead }
public sealed class Fiber {
    FiberId Id; FiberState State; ScopeId Scope; Signature Effect; Coeffect Coeffect;
    ImmutableStack<InverseClaim> Inverses; IReadOnlySet<FiberId> Dependents; bool TeardownEnqueued;
    bool Load(); void Unload(); void NotifyProviderTeardown();
}
public sealed class DependencyGraph {
    bool TryAddEdge(Fiber dependent, Fiber provider, EdgeKind kind);
    ImmutableArray<Fiber> TopoSortLeafFirst();
    CycleReport DetectHardCycles();   // 仅硬边成环中止；软边环降级 warning
}
public interface IHost {                       // Godot 唯一接触面
    void Defer(Action a); void SetProcessMode(FiberId id, bool disabled);
    void EnqueueExitDrain(Action drain); bool IsInstanceValid(object handle);
}
public sealed class PluginRuntime {
    Fiber Register(FiberSpec spec); LoadResult LoadAll(); UnloadResult UnloadAll();
    void RecomputeTopology(); bool IsShuttingDown;   // N2 守卫：关路径硬拒绝
}
```

## B. 可测纯逻辑边界 vs 须 Godot 运行时

- 纯逻辑（可单测）：状态机+幂等守卫(§2)、硬/软拓扑+环检测(§3)、逆 LIFO 回放+部分释放(§4)、装载期软边∩ReleaseClass(§3 step2b)、每批重拓扑+TeardownEnqueued 去重(§3)、门控判定 State==Active(§7)。
- 须 Godot（推迟）：ProcessMode=Disabled 级联、_Ready/_ExitTree 入队、Defer() 实现、节点树祖先不变量、IsInstanceValid 原生句柄判空。

## C. 与 L1 接线点

- §5 余效益：每 Fiber 累积 Signature（InverseClaim→Claim）→ NetTable.Compute(sig, fiber.Scope) → 逐资源 SignedInterval.ContainsZero 判闭合。禁用瞬时 IsConserved(At(t))、禁用 EffectScript.Audit（剧本级，非运行时 teardown）；Scope 仅分组不跨 Fiber 求和(R4-7)。
- §7.1：ReleaseClass.IsRelease(apiName)。软边 = ResourceId.Normalize(inv.Resource) == ResourceId.Normalize(provider.Coeffect.Provides)。
- §3 交叉校验：ResourceId.Normalize(InverseClaim.Resource) vs Coeffect.Requires。

## D. 测试清单（MVP，按设计章节）

1. §2 load()/unload() 幂等守卫（3 态 return）。
2. §2 notify if(State==Active) 幂等(R4-4)+看门狗帧计数强转 TearingDown(#1)。
3. §3 硬边环拒载 / 软边环 warning+装载(#2)。
4. §3 step2b 双重释放：逆引用他 provider 资源 ∩ ReleaseApiTags 命中 ReleaseClass → 装载 error(R5-6)。
5. §3 拓扑叶子优先 + TeardownEnqueued 去重。
6. §3 N2 IsShuttingDown 关路径硬拒 RecomputeTopology(R5-7)+级联期新装载拒载。
7. §4 LIFO 回放 + 栈中异常部分释放诊断(R4-6)。
8. §5 逐 Fiber net 零闸门 pass/fail + Scope 分组(R4-7)。
9. §6 provider 崩溃级联 teardown→全 Dead。
10. §7 门控：仅 Active 派发，Suspending/TearingDown 拦截（mock IHost）。

## E. §7.1 release-class 白名单实情与改造

- ApiMapping.cs 内无 release-class 集。实际是独立静态类 ReleaseClass（§8.1）：Names(7 项: queue_free/free/remove_child/disconnect/remove_from_group/cancel_free/free_children_in_group)、IsRelease(string)、All。
- 不符：设计 §7.1 称「ApiMapping 中既有」→ 实为 ReleaseClass，设计需改引用名。
- 缺口：InverseClaim.Action 是裸 Action 无 API 名元数据，§3 step2b 无法机械判定。最小改造：InverseClaim 加可选 ReleaseApiTags(IReadOnlySet<string>)，运行时取 软边 ∩ tags ∩ ReleaseClass.IsRelease。
