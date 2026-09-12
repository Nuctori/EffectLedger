# 空间维度运行时壳层设计（Godot 壳 + 插件）— 对抗审计靶标 v7（收敛版）

> 对齐论文 *A Programming Paradigm for Spatiotemporal Composability* (cordiverse/paper)。
> 本文档是「3 轮 + 迭代3轮 对抗性审计」靶标。**v7 = v6 + 第6轮(最终收敛审计)修正**。不修改 L1/L2/L3 代数核心；仅新增**运行时层**补论文 spatial 维度。
> 第6轮判定：架构收敛，仅 2 项收尾（§7.1 白名单落位 + §7 ProcessMode 抑制范围纠错），无结构重构 → **v7 为最终收敛版**。
> v6 标题「v6（收敛版）」因第6轮发现 2 项必须修复项，升级至 v7。

## 0. 术语对齐（论文 → EffectLedger）

| 论文 (Cordis) | EffectLedger 现有 | 本设计新增 |
| --- | --- | --- |
| effect（前向 setup 累积 `ctx.effect`） | `Claim` (R/W/O × Create/Move/Release) | — |
| coeffect（所需/所提供） | `ResourceId` / `ScopeId` | `Coeffect` 轻量封装 |
| 时间维度：effect 配对逆、LIFO 撤销 | `Mode.Release` 配对；`NetTable.IsConserved`；L3 EAA0901 | 结构化逆 `InverseClaim` 栈 |
| 空间维度：依赖拓扑、provider-active、dependent-first-teardown | ❌ 无 | `PluginRuntime` / `DependencyGraph` |
| 组件 = Fiber | — | `Fiber` 类型 |

- **时空统一效益** = 插件的 `Signature`（effect 净效应，时间维度配对）。
- **余效益** = 插件的 `NetTable` 余量（未闭合资源 = 运行时残留效应）。
- **已知边界（硬约束）**：本仓 L3 `EAA0901` 是**流不敏感、仅按白名单调用点匹配的近似**；本仓 `audit/effect-script-auditA.md` OPEN-2 已证 `IsConserved(At(t))` 是**瞬时**判定。设计尊重这两条，不假装静态/瞬时手段可替代运行时权威。
- **Godot 销毁语义（硬约束，R4-1/R5 核实）**：Godot 无追踪 GC；`Object` 经 `queue_free`/引用计数死亡。节点销毁顺序确定——`propagate_exit_tree` **子先于父**（child `_ExitTree` 先于 parent）。`ProcessMode.Inheritor` 默认使父 `Disabled` 级联禁整子树 `_Process`/`_PhysicsProcess`。无全局开关禁用 signal/`await`/`SceneTreeTimer`/`Tween`/`call_deferred`。

## 1. 组件模型（D2/D5/#7 + R4-3 + R5 修正）

```csharp
namespace EffectLedger.Runtime;

public enum FiberState { Inactive, Active, Suspending, TearingDown, Dead }

public sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope);

// 结构化逆：声明维度(ResourceId/ScopeId/Mode.Release)齐全；Scope 须 == Coeffect.Scope（#7 error）。
// 静态不变量（R5/R4-3）：逆 Action 不得 free 另一 provider 提供的资源——装载期由软边+
// §7.1 release-class 白名单校验拒绝（见 §3 step 2b）。
public sealed record InverseClaim(ResourceId Resource, ScopeId Scope, Action Execute);

public sealed class Fiber
{
    public FiberId Id { get; }
    public FiberState State { get; private set; }
    public ScopeId Scope => Coeffect.Scope;
    public Signature Effect { get; }
    public Coeffect Coeffect { get; }
    public ImmutableStack<InverseClaim> Inverses { get; }
    public IReadOnlySet<FiberId> Dependents { get; }
    public bool TeardownEnqueued { get; private set; }

    // ── 跨 Fiber 资源所有权契约（R4-3，硬约束）──
    // provider 拥有其 Provides 资源的生命周期；dependent 逆只 detachment/停止引用，
    // 禁止 free 共享资源（构造期已静态拒绝，见 §3 step 2b；运行时仍属开发者契约）。
    // 原生句柄（Godot Object 经 QueueFree 后的裸引用）访问须在 Action 内 IsInstanceValid 判空；
    // 壳 try/catch 隔离不了原生崩溃。
}
```

## 2. 生命周期状态机（D3/D4/N1/Blocker/#1/R4-4/R4-9 修正）

```
Inactive ──load()──▶ Active ──provider-notify──▶ Suspending ──teardown begins──▶ TearingDown ──▶ Dead
   ▲                                                          │                                      │
   └──────────── dependent-first ─────────────────────────────┘                                      │
   └── unload() 对 {Suspending,TearingDown,Dead} 直接 return（Blocker 修复）─────────────────────────┘
```

- `load()` / `unload()` 以 `State` 为**幂等守卫**：`Active/Suspending/TearingDown` 时 `load()` 直接 return；`unload()` 对 `{Suspending, TearingDown, Dead}` 三者均直接 return（Blocker）。`unload()` 另以 `TeardownEnqueued` 防二次入队。
- **notify 转移幂等守卫（R4-4）**：`provider-first-notify` 转 dependent B 为 `Suspending` 须 `if (State == Active) State = Suspending;` 其它态 no-op，防看门狗超时强制 `TearingDown` 后 provider 仍重复 notify 致 `TearingDown↔Suspending` 振荡。
- `load()`：前向 setup → 累积 `Signature` → 注册 `InverseClaim`（Scope==Coeffec.Scope）→ 置 `Active`。
- 依赖仅当 provider 处于 `Active`（**且同 `Coeffect.Scope`**，N5）才 available。
- **`Suspending` 子态（N1）**：provider A 进 `TearingDown` 并 notify B → B 转 `Suspending`（壳强制暂停，§7）。
- **`Suspending` 看门狗（#1）+ 帧计数时钟（R4-9）**：以**帧计数**为准（非墙钟；后台化帧冻结时墙钟超时但无帧可执行 teardown → 强制落空）。超时未 `Dead` → 强制 `TearingDown` + 硬错误日志；强制路径须确保 teardown 任务仍在队列/可调度，极端后台态降级为「恢复帧时优先处理」。
- `unload()`：置 `TearingDown`，teardown 任务入帧安全调度队列（§3，`TeardownEnqueued` 防重）；永不忙等（D3）。

## 3. 依赖图 + 拆除顺序（D3/D5/D7/N1/N5/Blocker/#2/#4/R4-1/R4-5/R5 修正）

1. **显式边（hard）**：同 `Coeffect.Scope` 内，B 的 `Requires ⊇` A 的 `Provides` ⇒ B 依赖 A。
2. **隐式边（soft，D5残）**：B 的某 `InverseClaim.Resource` 引用了 A 提供的资源 ⇒ 派生 B→A 边（保守过近似）。
   - **2b 静态双重释放拒绝（R5/R4-3）**：装载期若 B 的 `InverseClaim.Resource` 引用 A 提供资源 **且** 该 `Action` 含 release-class 操作（`§7.1` free/dispose 白名单）→ 装载期 error（复用软边 + `ApiMapping` release-class 集）。关闭「逆释放他 provider 资源」的构造期双重释放类。
3. **两源交叉校验（D5残）**：`InverseClaim.Resource` 与 `Coeffect.Requires` 不一致 ⇒ warning。
4. **环检测分 hard/soft（#2）**：仅对**硬边**拓扑排序并拒环；软边环仅 warning + 允许装载。
5. A 进 `TearingDown` → **provider-first-notify**（R4-4 幂等）：B 转 `Suspending`，B teardown 入队（`TeardownEnqueued` 防重）。
6. **帧安全调度器（D3残/#4）**：每批对全部 pending **重新拓扑排序**（叶子优先/dependent-first）；排序前环检测，**仅硬边成环才中止并告警**，纯软边环降级告警并**退化为该软边不约束**（R4-5）。整任务 try/catch → `Dead` + 按拓扑序推进上游（R4-10）；环中止跳过成环子集、其余重入队 + 升级告警（不无限重试）。
7. 全部 B 到 `Dead` → A 回放自身 `Inverses`（LIFO）→ A 到 `Dead`。
8. **Godot 解耦（D7）+ 关闭/退出路径约束（R4-1/R5 核实）**：`_Ready`→`load()` 入队、`_ExitTree`→`unload()` 入队（只入队，D7）。Godot 退出同步销毁节点树、调度器无后续帧则队列不排空 → 拓扑拆除静默退化。
   - **结构不变量（R5）**：**所有业务 Fiber 节点必须是调度器节点的后代**（Godot `propagate_exit_tree` 子先于父，故调度器 `_ExitTree` 最后触发，可在**自身 `_ExitTree` 处理内**（非「之前」）同步排空队列——排空循环执行各 Fiber 的 `InverseClaim` 释放 Action 按 dependent-first 顺序，而节点实际 `queue_free` 顺序由 Godot 决定，二者不一致由 §8 fail-open 缓解）。若业务 Fiber 与调度器为 root 下**兄弟**，销毁顺序不保证 → 静默失败。该不变量为硬约束。
   - 同步排空循环须**快照/可重入安全**（Action 重入队仅 append；循环处理固定快照），且退出路径 teardown `Action` **须纯同步**（无 `await`/Tween/Timer continuation，否则 `_ExitTree` 期间 continuation 不恢复 → 该 Fiber 挂起 fail-open）。
   - 退化路径（R5 措辞修正，非「GC 兜底」）：关闭/退出路径**不保证拓扑序**；真实保护仅为 refcount（`RefCounted`/`Resource`）+ §1 所有权契约；**非引用计数共享原生句柄在不确定退出序下仍 fail-open**（仅 R4-3 判空缓解）。写入 §8 弱化点。

## 4. 时间维度（D2 + R4-3/R4-6 修正）

- 每个 effect 配对结构化逆 `InverseClaim`（声明维度等价 L1 `Mode.Release`）。
- `unload` 时 `Inverses` 栈 **LIFO 回放**（声明维度等价成立 D2）。**不声称行为等价（D2残/N4）**：`Action` 内部真实释放由开发者保证与 `InverseClaim.Resource` 一致；行为闭合以运行时权威（§5）。
- **逆回放异常 + 部分释放诊断（R4-6）**：回放记录执行索引；异常在栈中间 → 已释部分/未释部分即标记 `Dead` → 记载「部分释放」诊断并尽量继续剩余逆（或要求 `Action` 自包含幂等/可重入），避免上游 provider 按拓扑序释放时 dependent 仍部分持有致二次崩溃。原生句柄崩溃壳隔离不了（R4-3）。
- 异常后标记 `Dead` + 按拓扑序推进上游（R4-10）。

## 5. 余效益闭环（D1/D9/N3/#5/R4-7/R5 修正）

- **卸载闸门 = 逐 Fiber、声明维度、完整性校验（非泄漏防护，#5）**：只校验 `Inverses` 结构闭合；真实泄漏由运行时监控兜底。保留 `ForceUnload()` / `Unload(ignoreResidual: true)` 逃生通道。
- **全局聚合降级 fail-open 监控（D1）**：绝不作为卸载闸门。按各 Fiber 自身 `ScopeId` **分组**（D9）。
- **监控粒度（R4-7）**：累积 net 为**逐 Fiber**——每 Fiber 累积 net、退出时判零；`ScopeId` 仅分组维度、**不跨 Fiber 求和**（避免 A 泄漏被 B 负余量抵消）。禁用瞬时 `IsConserved(At(t))`（N3）。
- **永久存活 Fiber（#6，R5 升级为 MVP 建议项）**：逐 Fiber 退出判零对全程存活插件永久不触发 → 泄漏盲点。MVP **建议**落地周期快照阈值告警（如每 N 帧对活跃 Fiber 累积 net 超阈值告警），而非仅依赖退出判零。

## 6. 故障处理（D5/D6/D8/N2/#4/R4-3/R5 修正）

| 故障 | 处理 |
| --- | --- |
| 硬边依赖环 | 装载期拒载 |
| 软边（隐式逆）环 | 仅 warning + 允许装载（#2） |
| 逆释放他 provider 资源（R5/R4-3） | 装载期 error（软边+release-class 白名单，§3 step 2b） |
| provider 崩溃/消失 | 强制 `unload` ⇒ 级联 teardown（B 转 Suspending） |
| 逆回放异常 / teardown 任意异常（#4） | 整任务 try/catch → `Dead` + 按拓扑序推进上游；部分释放诊断（R4-6） |
| 环中止 | 跳过成环子集，其余重入队 + 升级告警（不无限重试） |
| Suspending 看门狗超时（#1/R4-9 帧计数） | 强制 `TearingDown` + 硬错误日志 |
| 跨 Fiber 双重释放（R4-3） | provider 拥有生命周期、dependent 逆仅 detach 禁 free 共享；装载期静态拒绝（§3 step 2b）+ 原生句柄自判空 |
| 余效益未闭合 | 逐 Fiber 声明闸门放行 + 运行时监控兜底 |
| 级联期新装载依赖 TearingDown provider（D6，**MVP must-land**） | 显式 reject + 告警，或延迟到该 provider 真正 `Dead` 后判定 |
| `RecomputeTopology()` 遇 TearingDown/Suspending（N2，**MVP must-land**，**关闭路径硬拒绝**） | `IsShuttingDown` 标志：非关闭路径延迟执行；**关闭路径（无后续帧）硬拒绝**（非 delay，delay 在关闭路径=静默丢弃） |
| 动态重组合 | MVP = 静态组合（#9）；`RecomputeTopology()` 为动态组合前置钩子（受 N2 守卫） |

## 7. Godot 壳集成点（D4/D7/N1/#3/R4-2/R5 修正）

- Godot 宿主：`Assembly.Load` 加载插件程序集，每插件 = 一个 `Fiber`。
- `_Ready`→`load()` 入队；`_ExitTree`→`unload()` 入队（只入队，D7）。
- **门控机制（R4-2，明确）**：壳对每个 Fiber 子树设 `ProcessMode = Disabled` 作为 `Suspending`/`TearingDown` 暂停手段。该枚举实际抑制 **`_Process`/`_PhysicsProcess`/`_Input`/`_UnhandledInput`/`_UnhandledKeyInput`/`_PhysicsInterpolation*` 以及 pause 相关 `_Notification`（`NOTIFICATION_PAUSED`/`VISIBILITY_CHANGED`/`TRANSFORM_CHANGED`/`LOCAL_TRANSFORM_CHANGED` 等）** 的派发（级联）。**不抑制** `NOTIFICATION_READY`/`NOTIFICATION_EXIT_TREE`/`NOTIFICATION_PREDELETE` 等内部通知（这些与对象生命周期绑定，与 `ProcessMode` 无关）——故 `Suspending` 期间 `_ExitTree` 仍触发（见 §3 step 8 同步排空即发生于 `_ExitTree` 内）。仅 `State==Active` 派发 gameplay；`Suspending`/`TearingDown` 一律不派发 process/physics/input/暂停类回调。
- **§7.1 释放类操作白名单（R5-6 装载期双重释放拒绝的数据源）**：壳以 `ApiMapping` 中既有的 **release-class 白名单**（`free`/`dispose`/`queue_free`/`destroy`/`Close`/`Release`/`Dispose` 等释放语义 API，见 `ApiMapping` release-class 集）为判据：装载期若某 Fiber 的 `InverseClaim.Action` 引用了另一 provider 提供的资源 **且** 该 Action 命中 release-class 白名单 → **装载期 error**（拒绝「逆释放他 provider 资源」）。该白名单复用 L3 `EAA0901` 同款 release-class 分类，避免重复定义；软边派生（§3 step 2）提供「逆引用他 provider 资源」的保守过近似，二者交集即为装载期拒绝集。
- **回调覆盖边界（R4-2/R5，诚实声明）**：Godot **无 API 禁用 signal / `await` continuation / `SceneTreeTimer` / `Tween.finished` / `call_deferred`**（这些不受 `ProcessMode` 控制）。`area_entered`、`body_entered`、自定义 signal、`await`、定时器、`call_deferred` 在 `Suspending` 期间仍执行并访问 provider 资源 → use-after-free。
  - **壳级缓解（R5）**：提供壳 `Defer()` 包装替代 `call_deferred`（注册入门控队列，Suspending 时丢弃/延后），并建议硬 lint 规则禁止插件直接调用 `call_deferred`；signal/`await`/Tween/Timer 仍属**开发者契约 + fail-open**（插件经壳注册异步回调、回调内自判 `State`）。
  - 该限制写入 §8。
  - 该限制写入 §8。
- `Suspending` 子态（N1）：Fiber 收 notify 后由壳暂停（`ProcessMode=Disabled`）。
- 不对称/多次语义：`_ExitTree` 未 `_Ready` 也安全（D4/Blocker）；树重组由状态机吸收。

## 8. 已知缺口（v6 收敛 + 硬约束 + 推迟/弱化项）

- G6 关闭：聚合按 ScopeId 分组（D9）。
- G4 结论：逐 Fiber 声明闸门 + 全局 fail-open 累积 net 监控（D1/N3），逐 Fiber 粒度（R4-7）。
- G3 接缝：MVP 静态组合（#9）+ `RecomputeTopology()` 钩子（N2 守卫）。
- **硬约束**：全局监控用 OPEN-2 累积 net，禁用瞬时 `IsConserved(At(t))`（N3）。
- **已知弱化点（R4-1/R5）**：关闭/退出路径不保证拓扑序；真实保护仅为 refcount + §1 所有权契约；**非引用计数共享原生句柄在不确定退出序下 fail-open**（非「Godot GC 兜底」——Godot 无追踪 GC）。调度器节点须为所有业务 Fiber 祖先（结构不变量）。
- **已知限制（R4-2/R5）**：signal/`await`/Timer/`Tween`/`call_deferred` 壳无法强制门控，属开发者契约 + fail-open（壳提供 `Defer()` 缓解 `call_deferred`）。
- **推迟项**：§5 监控硬化（fail-open 即可）、§8 动态组合/`RecomputeTopology`（仅接缝）、多 Scope（#7 已强制一致）、永久 Fiber 监控(#6，已升级 MVP 建议周期快照)、游戏中期 mod 加载（#9）。

## 9. 审计发现总表

### 第1–3轮（D1–D10 / 残留 / Blocker+#1–#9，已融入 v4）

（详见 git history 与 v4；v5/v6 仅增量记录第4/5轮。）

### 第4轮（R4-1~R4-10，实现可行性盲区，已融入 v5）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| R4-1 | 高(Blocker) | 关闭/退出路径调度器无后续帧排空 → 拓扑拆除静默退化重引 use-after-free | §3 关闭路径约束（调度器祖先+同步排空）或退化为 refcount+契约兜底 |
| R4-2 | 高 | §7 门控机制未定 + 仅覆盖 process | §7 `ProcessMode=Disabled` 级联；承认非 process 回调为开发者契约+fail-open |
| R4-3 | 中/高 | 跨 Fiber 资源所有权契约缺位 | §1/§4 provider 拥有生命周期、dependent 逆仅 detach 禁 free、原生句柄自判空 |
| R4-4 | 中 | notify 无源态守卫 → 振荡 | §2 notify `if(State==Active)` 幂等 |
| R4-5 | 中 | 软边自环排序失败 | §3 软边环降级忽略该边 |
| R4-6 | 中 | 逆栈中间异常部分释放 | §4 部分释放诊断+尽量继续 |
| R4-7 | 低/中 | 监控粒度歧义 | §5 逐 Fiber 粒度、Scope 仅分组 |
| R4-8 | 低 | §10 MVP 漏点名 | §10 点名 N2+级联 reject must-land |
| R4-9 | 低 | 看门狗时钟未定义 | §2 帧计数时钟 |
| R4-10 | 低 | 术语漂移 | §4/§6 统一「按拓扑序推进上游」 |

### 第5轮（R5-1~R5-7，Godot 4.6.3 API 可行性 + 措辞硬化，已融入 v6）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| R5-1 | 高 | §8「Godot GC 兜底」技术错误（Godot 无追踪 GC） | §3/§8 改为 refcount + 所有权契约 + 非引用计数句柄 fail-open |
| R5-2 | 中 | R4-1(a) 缺结构不变量：业务 Fiber 须为调度器后代 | §3 加硬不变量（子先于父销毁序） |
| R5-3 | 中 | 退出路径 teardown Action 含 await 永不完成 | §3 声明退出路径 Action 纯同步 |
| R5-4 | 中 | `call_deferred` 无壳门控 → use-after-free | §7 壳 `Defer()` 包装 + lint 建议 |
| R5-5 | 低/中 | §7 未列 `_Notification` 类/`_Input` | §7 枚举 `ProcessMode` 实际抑制全集 |
| R5-6 | 中 | R4-3 未用静态信息 | §3 step 2b 装载期拒逆释放他 provider 资源 |
| R5-7 | 中 | N2「delay」在关闭路径=静默丢 | §6 关路径 `IsShuttingDown` 硬拒绝；#6 升级 MVP 周期快照 |

### 第6轮（R6-1~R6-3，最终收敛审计，已融入 v7）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| R6-1 | 中(Blocker) | §1/§3/§6 引用 `§7.1 release-class 白名单` 但该节不存在 → R5-6 装载期拒绝无落点 | 新增 §7.1 定义释放类白名单（复用 ApiMapping release-class 集 + 软边交集） |
| R6-2 | 中(Blocker) | §7 误称 `ProcessMode=Disabled` 抑制「所有 _Notification」 | 限定抑制范围(process/physics/input + pause 类通知)，显式豁免 READY/EXIT_TREE/PREDELETE |
| R6-3 | 低(非阻塞) | §3:82「_ExitTree 前」措辞不准 | 改「自身 _ExitTree 内同步排空」+ dependent-first 顺序说明 |

### L1 对抗审计收尾（第7轮代码审计，治理双解析器债务，已落地 HEAD=ec772fc）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| C7-1 | 高 | `EffectScriptIo.cs` 是与 `EffectScriptContract.cs` 矛盾的**第二个** JSON 解析器（budget：caps 数组 vs 扁平键；资源：14 vs 5 种）→ 同一 AI 剧本按走哪个解析器结果不同，违反「统一契约」 | 删除 `EffectScriptIo.cs`（`git rm`），4 个 JSON 契约测试收敛到单一 `EffectScriptContract` 解析器（SampleJson 改用 Contract 资源/scope/loop 形状，budget 扁平 `commandBuffer:gpu` 键，断言 `FormatException`）；csproj 移除其 net9.0 `Compile Remove` 行 |
| C7-2 | 中 | `memory` uid 硬编码 `Memory(0)`（B1） | 随 `EffectScriptIo` 删除一并移除冗余实现，仅保留 Contract 单一口径（`Memory(GetUInt64())`） |
| C7-3 | 注 | 决策审计 B4（gate3 scope vs At Event.Scope）：**误报** | `Combination.Loop`（DerivedMetrics.cs:31-47）已将每 Claim scope 改写为 `e.Scope`；`gate(3)` 分组 `key=(r, c.Scope, mode)` 中 `c` 即已改写后的 claim ⇒ 两处 scope 来源一致 |

## 10. MVP 实现清单（第6轮收敛判定 → v7 最终）

**必须落地（核心正确性）**：§1 Fiber 模型 + 跨 Fiber 所有权契约(R4-3) + **装载期逆释放拒绝(R5-6)**、§2 状态机 + 幂等守卫(Blocker) + notify 幂等(R4-4) + 看门狗帧计数(#1/R4-9)、§3 拓扑 + 每批 DAG 重排 + notify + Suspending(硬/软边分离 #2) + **关闭路径(调度器祖先+同步排空+Action纯同步, R4-1/R5-2/R5-3)** + 软边环排序(R4-5)、§4 结构化逆回放 + 整任务异常捕获(#4) + 部分释放诊断(R4-6)、§6 故障处理(**含 N2 守卫+关路径硬拒绝(R5-7) + 级联期新装载 reject must-land**)、§7 Godot 入队 + `ProcessMode=Disabled` 门控(#3/R4-2) + **`Defer()` 包装(R5-4) + 回调边界声明**、§8 结构不变量(调度器祖先)。

**MVP 内建议落地**：Suspending 看门狗、调度器异常隔离、关闭路径同步排空、**#6 永久 Fiber 周期快照阈值告警(R5-7 升级)**。

**可推迟（tracked gaps/弱化）**：§5 监控硬化(fail-open)、§8 动态组合/`RecomputeTopology`(仅接缝)、多 Scope(#7)、游戏中期 mod 加载(#9)、关闭路径非引用计数句柄 fail-open(R4-1/R5-1)、signal/await/Timer/Tween 壳门控(R4-2/R5-4)。

**收敛判定（v7 最终）**：6 轮（+3 迭代）审计后，设计在 spatial 维度论题上**完全收敛**——安全/死锁/实现可行性/Godot API 可行性四维闭环。第6轮仅 2 项收尾（§7.1 白名单落位 + §7 ProcessMode 抑制范围纠错）+ 1 项措辞，无结构重构。**v7 为最终收敛版**，可进入 MVP 实现。
