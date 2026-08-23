# 空间维度运行时壳层设计（Godot 壳 + 插件）— 对抗审计靶标 v1

> 对齐论文 *A Programming Paradigm for Spatiotemporal Composability* (cordiverse/paper)。
> 本文档是「3 轮对抗性审计」的**靶标规格**：独立审计员据此找设计缺陷，每轮收敛。
> 不修改 L1/L2/L3 代数核心；仅新增一个**运行时层**补论文的 spatial 维度。

## 0. 术语对齐（论文 → Cosmos）

| 论文 (Cordis) | Cosmos 现有 | 本设计新增 |
| --- | --- | --- |
| effect（前向 setup 累积 `ctx.effect`） | `Claim` (R/W/O × Create/Move/Release) | — |
| coeffect（所需/所提供） | `ResourceId` / `ScopeId` | `Coeffect` 轻量封装 |
| 时间维度：effect 配对逆、LIFO 撤销 | `Mode.Release` 配对；`NetTable.IsConserved`；L3 EAA0901 | 运行时 `Inverses` 栈 |
| 空间维度：依赖拓扑、provider-active、dependent-first-teardown | ❌ 无 | `PluginRuntime` / `DependencyGraph` |
| 组件 = Fiber | — | `Fiber` 类型 |

- **时空统一效益** = 插件的 `Signature`（effect 净效应，时间维度配对）。
- **余效益** = 插件的 `NetTable` 余量（未闭合资源 = 运行时残留效应）。

## 1. 组件模型

```csharp
// 新增运行时层（默认放 samples/GodotIntegration 内壳 Demo，验证后晋升 src/Cosmos.EffectAlgebra.Runtime）
namespace Cosmos.EffectAlgebra.Runtime;

public enum FiberState { Inactive, Active, TearingDown, Dead }

public sealed record Coeffect(ResourceId Requires, ResourceId Provides);

public sealed class Fiber
{
    public FiberId Id { get; }
    public FiberState State { get; private set; }      // Inactive→Active→TearingDown→Dead
    public Signature Effect { get; }                   // 前向 setup 累积的 Signature（时空统一效益）
    public Coeffect Coeffect { get; }                 // 余效益：所需/所提供
    public ImmutableStack<Action> Inverses { get; }    // 时间维度：LIFO 逆操作栈
    public IReadOnlySet<FiberId> Dependents { get; }   // 空间维度：依赖本 Fiber 的 others
}
```

## 2. 生命周期状态机（provider-active 精化）

```
Inactive ──load()──▶ Active ──unload()──▶ TearingDown ──▶ Dead
   ▲                                               │
   └──────────── dependent-first ───────────────────┘
```

- `load()`：运行前向 setup → 累积 `Signature` 入 `Effect`；注册逆操作入 `Inverses`；置 `Active`。
- 依赖仅当 provider Fiber 处于 `Active` 才视为 available（论文 provider-active）。
- `unload()`：置 `TearingDown` → 先通知 dependents（provider-first-notify）→ 等 dependents 全 Dead → 回放自身 `Inverses`（LIFO）→ `Dead`。

## 3. 依赖图 + 拆除顺序（spatial 落地）

1. 插件 B 的 `Coeffect.Requires ⊇` 插件 A 的 `Coeffect.Provides` ⇒ B 依赖 A。
2. A 进入 `TearingDown` → **provider-first-notify**：通知所有依赖 A 的 B。
3. 每个 B 先于 A 完成自身 teardown（**dependent-first-teardown**），回放 B 的 `Inverses`（LIFO）。
4. 全部 B 到 `Dead` → A 才回放自身 `Inverses` → A 到 `Dead`。

## 4. 时间维度（复用 L1）

- 每个 effect 必须配对逆（论文：作者配 inverse，运行时排序）。
- 运行时 `Inverses` 栈在 `unload` 时 LIFO 回放，等价于 L1 `Mode.Release` 对 `Create` 的配对。
- 编译期已由 L3 EAA0901 静态近似；运行时为权威执行。

## 5. 余效益闭环（接 L1 `NetTable.IsConserved`）

每次 Fiber 装载/卸载后，运行时聚合所有 `Active` Fiber 的 `Signature` → `NetTable`：

- 任一资源 `IsConserved == false` ⇒ 该 Fiber 前向 setup 未闭合（effect 栈未配对）。
- **gate 决策（待定）**：未闭合时，是拒绝 `unload`（fail-closed）还是仅报警（fail-open）？
  - 论文倾向运行时追踪 + 正确逆序，隐含 fail-closed（逆序撤销保证闭合）。
  - 本设计暂定：**unload 时若聚合 NetTable 不闭合 ⇒ 拒绝卸载并抛 `ResidualBenefitUnclosedException`**（fail-closed，对应论文「正确逆序撤销」）。

## 6. 故障处理（对抗审计重点）

| 故障 | 处理 |
| --- | --- |
| 依赖环 A→B→A | 装载期检测环 ⇒ 拒绝装载环中任一（或拓扑排序失败抛异常） |
| provider 在 Active 中崩溃/消失 | 视为强制 `unload` 该 provider ⇒ 触发其 dependents 级联 teardown |
| 余效益未闭合（逆缺失） | gate 拒绝卸载（见 §5） |
| 动态重组合（论文 dynamic composition） | MVP 外；预留 `Reload()` 重新聚合 NetTable |

## 7. Godot 壳集成点

- Godot 宿主：加载插件程序集（`Assembly.Load`），每个插件 = 一个 `Fiber`。
- 场景树节点对应 `Fiber` 生命周期（`_Ready`→`load`，`_ExitTree`→`unload`）。
- 业务全在插件程序集；壳只驱动 `PluginRuntime`。

## 8. 待对抗审计的设计缺口（已知未决）

- G1：依赖粒度用 `ResourceId` 还是粗粒度「插件能力」？（用 `ResourceId` 最贴 L1）
- G2：运行时层放 `samples` Demo 还是正式 `src/Cosmos.EffectAlgebra.Runtime`？
- G3：MVP 仅 load/unload+teardown，还是含动态重组合？
- G4：§5 gate 选 fail-closed 还是 fail-open？（影响插件可卸载性）
- G5：`Coeffect.Requires/Provides` 如何与 L2 生成器的 `[EffectOverride]` 标注自动派生？（避免手写重复）
- G6：多 `ScopeId` 下 NetTable 聚合的 scope 选择（全局 vs 每作用域）？
