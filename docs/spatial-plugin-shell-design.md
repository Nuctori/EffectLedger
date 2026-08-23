# 空间维度运行时壳层设计（Godot 壳 + 插件）— 对抗审计靶标 v5（收敛版）

> 对齐论文 *A Programming Paradigm for Spatiotemporal Composability* (cordiverse/paper)。
> 本文档是「3 轮 + 迭代3轮 对抗性审计」靶标。**v5 = v4 + 第4轮(R4-1~R4-10 实现可行性盲区)修正**。不修改 L1/L2/L3 代数核心；仅新增**运行时层**补论文 spatial 维度。
> 第4轮判定：修复 R4-1 Blocker + 2 高 + 7 中低后，设计在「真能跑起来」维度收敛。

## 0. 术语对齐（论文 → Cosmos）

| 论文 (Cordis) | Cosmos 现有 | 本设计新增 |
| --- | --- | --- |
| effect（前向 setup 累积 `ctx.effect`） | `Claim` (R/W/O × Create/Move/Release) | — |
| coeffect（所需/所提供） | `ResourceId` / `ScopeId` | `Coeffect` 轻量封装 |
| 时间维度：effect 配对逆、LIFO 撤销 | `Mode.Release` 配对；`NetTable.IsConserved`；L3 EAA0901 | 结构化逆 `InverseClaim` 栈 |
| 空间维度：依赖拓扑、provider-active、dependent-first-teardown | ❌ 无 | `PluginRuntime` / `DependencyGraph` |
| 组件 = Fiber | — | `Fiber` 类型 |

- **时空统一效益** = 插件的 `Signature`（effect 净效应，时间维度配对）。
- **余效益** = 插件的 `NetTable` 余量（未闭合资源 = 运行时残留效应）。
- **已知边界（硬约束）**：本仓 L3 `EAA0901`（`EffectAlgebraAnalyzer.cs:44-56`）是**流不敏感、仅按白名单 API 调用点匹配的近似**；本仓 `audit/effect-script-auditA.md` OPEN-2 已证 `IsConserved(At(t))` 是**瞬时**判定。设计尊重这两条，不假装静态/瞬时手段可替代运行时权威。

## 1. 组件模型（D2/D5/#7 + R4-3 修正：结构化逆 + 单一权威 Scope + 跨 Fiber 所有权契约）

```csharp
namespace Cosmos.EffectAlgebra.Runtime;

public enum FiberState { Inactive, Active, Suspending, TearingDown, Dead }

public sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope);

// 时间维度结构化逆：声明维度(ResourceId/ScopeId/Mode.Release)齐全。
// InverseClaim.Scope 必须等于所属 Fiber 的 Coeffect.Scope（#7：不一致装载期 error）。
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
    // **禁止 free 共享资源**（否则 provider 自身逆再 free → 双重释放）。
    // 原生句柄（Godot Object 经 QueueFree 后的裸引用）访问须在 Action 内判空；
    // 壳 try/catch 隔离不了原生崩溃，属开发者契约。
}
```

## 2. 生命周期状态机（D3/D4/N1/Blocker/#1/R4-4/R4-9 修正）

```
Inactive ──load()──▶ Active ──provider-notify──▶ Suspending ──teardown begins──▶ TearingDown ──▶ Dead
   ▲                                                          │                                      │
   └──────────── dependent-first ─────────────────────────────┘                                      │
   └── unload() 对 {Suspending,TearingDown,Dead} 直接 return（Blocker 修复）─────────────────────────┘
```

- `load()` / `unload()` 以 `State` 为**幂等守卫**：`Active/Suspending/TearingDown` 时 `load()` 直接 return；`unload()` 对 `{Suspending, TearingDown, Dead}` 三者均直接 return（Blocker 修复）。`unload()` 另以 `TeardownEnqueued` 防二次入队。
- **notify 转移幂等守卫（R4-4）**：`provider-first-notify` 将 dependent B 转 `Suspending` 时须 `if (State == Active) State = Suspending;` 其它态 no-op，防看门狗(#1)超时强制 `TearingDown` 后 provider 仍重复 notify 造成 `TearingDown↔Suspending` 振荡。
- `load()`：运行前向 setup → 累积 `Signature` → 注册 `InverseClaim`（Scope==Coeffect.Scope）→ 置 `Active`。
- 依赖仅当 provider Fiber 处于 `Active`（**且同 `Coeffect.Scope`**，N5）才 available（provider-active）。
- **`Suspending` 子态（N1）**：provider A 进 `TearingDown` 并 notify 到 B → B 转 `Suspending`（壳强制暂停 gameplay，见 §7）。
- **`Suspending` 看门狗（#1）+ 时钟源（R4-9）**：时钟以**帧计数**为准（非墙钟；app 后台化帧冻结时墙钟超时但无帧可执行 teardown → 强制落空）。超时未 `Dead` → 强制 `TearingDown` + 硬错误日志；强制路径须确保 teardown 任务仍在队列/可调度，极端后台态降级为「恢复帧时优先处理」。
- `unload()`：置 `TearingDown`，本 Fiber teardown 任务入帧安全调度队列（§3，以 `TeardownEnqueued` 防重）；永不忙等（D3）。

## 3. 依赖图 + 拆除顺序（D3/D5/D7/N1/N5/Blocker/#2/#4/R4-1/R4-5 修正）

1. **显式边（hard）**：同 `Coeffect.Scope` 内，B 的 `Requires ⊇` A 的 `Provides` ⇒ B 依赖 A。
2. **隐式边（soft，D5残）**：B 的某 `InverseClaim.Resource` 引用了 A 提供的资源 ⇒ 派生 B→A 边（保守过近似）。
3. **两源交叉校验（D5残）**：装载期 `InverseClaim.Resource` 与 `Coeffect.Requires` 不一致 ⇒ warning。
4. **环检测分 hard/soft（#2）**：装载期仅对**硬边**拓扑排序并拒环；**软边环只 warning + 允许装载**。
5. A 进 `TearingDown` → **provider-first-notify**（R4-4 幂等）：B 转 `Suspending`，B teardown 入队（`TeardownEnqueued` 防重）。
6. **帧安全调度器（D3残/#4）**：teardown 队列每批对全部 pending **重新拓扑排序**（叶子优先/dependent-first）；排序前对合并图做环检测，**仅硬边成环才中止并告警**，纯软边环降级告警并**退化为该软边不约束**（拓扑排序忽略软边环分量，R4-5，避免合并排序失败致死锁）。整任务 try/catch：逆/非逆异常均捕获 → 该 Fiber 标记 `Dead` + 按拓扑序推进上游 provider teardown（R4-10 术语统一：无显式 waiter 对象）；环中止跳过成环子集、其余重入队 + 升级告警（不无限重试）。
7. 全部 B 到 `Dead` → A 回放自身 `Inverses`（LIFO）→ A 到 `Dead`。
8. **Godot 解耦（D7）** + **关闭/退出路径约束（R4-1 Blocker）**：`_ExitTree`/`_Ready` 只入队。但 Godot 在 `_ExitTree`/应用退出时**同步销毁**节点树，调度器「每帧一批」若无后续帧则队列不排空 → 拓扑拆除静默退化、重新引入 use-after-free。**约束（R4-1，二选一/组合）**：
   - (a) 壳调度器节点须存活于所有业务 Fiber 之后，并在自身 `_ExitTree` 前**同步排空**队列（与「永不忙等」在退出路径折中：退出路径允许同步排空）；或
   - (b) 明文声明：关闭/退出路径**不保证拓扑序**，退化为 Godot GC 兜底，且 provider 资源须对依赖方仍存活做防御（R4-3 所有权契约），该路径写入 §8 已知弱化点。

## 4. 时间维度（D2 修正 + R4-3/R4-6 修正）

- 每个 effect 配对结构化逆 `InverseClaim`（声明维度等价 L1 `Mode.Release`）。
- `unload` 时 `Inverses` 栈 **LIFO 回放**（声明维度等价成立 D2）。**不声称行为等价（D2残/N4）**：`Action` 内部真实释放由开发者保证与 `InverseClaim.Resource` 一致；行为闭合以运行时权威（§5）。
- **逆回放异常 + 部分释放诊断（R4-6）**：回放记录执行索引；异常若发生在栈中间，已回放部分释放、未回放部分未释放即标记 `Dead` → 记载「部分释放」诊断并尽量继续剩余逆（或要求每个 `InverseClaim.Action` 自包含幂等/可重入），避免上游 provider 按拓扑序释放自身资源时 dependent 仍部分持有致二次崩溃/泄漏。原生句柄崩溃壳隔离不了（R4-3）。
- 异常后标记 `Dead` + 按拓扑序推进上游（R4-10）。

## 5. 余效益闭环（D1/D9/N3/#5/R4-7 修正：声明闸门 + 运行时权威 + 累积net监控）

- **卸载闸门 = 逐 Fiber、声明维度、完整性校验（非泄漏防护，#5）**：只校验 `Inverses` 结构闭合；真实泄漏由运行时监控兜底。保留 `ForceUnload()` / `Unload(ignoreResidual: true)` 逃生通道。
- **全局聚合降级 fail-open 监控（D1）**：绝不作为卸载闸门。按各 Fiber 自身 `ScopeId` **分组**（D9）。
- **监控粒度（R4-7）**：累积 net 为**逐 Fiber**粒度——每 Fiber 累积 net、退出时判零；`ScopeId` 仅作分组维度、**不跨 Fiber 求和**（避免 A 的泄漏被 B 的负余量抵消）。禁用瞬时 `IsConserved(At(t))`（N3，防告警风暴）。**永久存活 Fiber 不查** → 属 §8 已知缺口 #6（对该类放弃运行时泄漏检测，fail-open 一致）；可选硬化：周期快照阈值告警。

## 6. 故障处理（D5/D6/D8/N2/#4/R4-3 修正）

| 故障 | 处理 |
| --- | --- |
| 硬边依赖环 | 装载期拒载 |
| 软边（隐式逆）环 | 仅 warning + 允许装载（#2） |
| provider 崩溃/消失 | 强制 `unload` ⇒ 级联 teardown（B 转 Suspending） |
| 逆回放异常 / teardown 任意异常（#4） | 整任务 try/catch → `Dead` + 按拓扑序推进上游；部分释放诊断（R4-6） |
| 环中止 | 跳过成环子集，其余重入队 + 升级告警（不无限重试） |
| Suspending 看门狗超时（#1/R4-9 帧计数） | 强制 `TearingDown` + 硬错误日志 |
| 跨 Fiber 双重释放（R4-3） | provider 拥有资源生命周期，dependent 逆仅 detach 禁止 free 共享；原生句柄访问开发者自判空 |
| 余效益未闭合 | 逐 Fiber 声明闸门放行 + 运行时监控兜底 |
| 级联期新装载依赖 TearingDown provider（D6，**MVP must-land**） | 显式 reject + 告警，或延迟到该 provider 真正 `Dead` 后判定 |
| `RecomputeTopology()` 遇 TearingDown/Suspending（N2，**MVP must-land**） | 守卫：拒绝/延迟执行 |
| 动态重组合 | MVP = 静态组合（#9 假设）；`RecomputeTopology()` 为动态组合前置钩子（受 N2 守卫） |

## 7. Godot 壳集成点（D4/D7/N1/#3/R4-2 修正：壳强制 State 门控 + 机制明确）

- Godot 宿主：`Assembly.Load` 加载插件程序集，每插件 = 一个 `Fiber`。
- `_Ready`→`load()` 入队；`_ExitTree`→`unload()` 入队（只入队，D7）。
- **门控机制（R4-2，明确）**：壳对每个 Fiber 子树设 `process_mode = Disabled` 作为 `Suspending`/`TearingDown` 的暂停手段（`process`/`physics_process` 级联不派发）。仅 `State==Active` 派发 gameplay；`Suspending`/`TearingDown` 一律不派发。
- **回调覆盖边界（R4-2，诚实声明）**：Godot **无 API 禁用 signal / `await` continuation / `SceneTreeTimer` / `Tween.finished` / `call_deferred`**。`area_entered`、`body_entered`、自定义 signal、`await`、定时器在 `Suspending` 期间仍会执行并访问 provider 资源 → use-after-free。**壳无法强制门控这些回调**，属**开发者契约 + fail-open**（插件须经壳注册异步回调、在回调内自判 `State`）。该限制写入 §8。
- `Suspending` 子态（N1）：Fiber 收 notify 后由壳暂停（上述 `process_mode=Disabled`）。
- 不对称/多次语义：`_ExitTree` 未 `_Ready` 也安全（D4/Blocker）；树重组由状态机吸收。

## 8. 已知缺口（v5 收敛 + 硬约束 + 推迟/弱化项）

- G6 关闭：聚合按 ScopeId 分组（D9）。
- G4 结论：逐 Fiber 声明闸门 + 全局 fail-open 累积 net 监控（D1/N3），逐 Fiber 粒度（R4-7）。
- G3 接缝：MVP 静态组合（#9）+ `RecomputeTopology()` 钩子（N2 守卫）。
- **硬约束**：全局监控用 OPEN-2 累积 net，禁用瞬时 `IsConserved(At(t))`（N3）。
- **已知弱化点（R4-1）**：关闭/退出路径不保证拓扑序，退化为 Godot GC 兜底（provider 资源须对依赖方仍存活防御，§1 所有权契约）。
- **已知限制（R4-2）**：信号/`await`/定时器/Tween 回调壳无法强制门控，属开发者契约 + fail-open。
- **推迟项**：§5 监控硬化（fail-open 即可）、§8 动态组合/`RecomputeTopology`（仅接缝）、多 Scope（#7 已强制一致）、永久 Fiber 监控缺口（#6）、游戏中期 mod 加载（#9）。

## 9. 审计发现总表

### 第1–3轮（D1–D10 / 残留 / Blocker+#1–#9，已融入 v4）

（详见 git history 与 v4；v5 仅增量记录第4轮 R4-1~R4-10。）

### 第4轮（R4-1~R4-10，实现可行性盲区，已融入 v5）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| R4-1 | 高(Blocker) | 关闭/退出路径 Godot 同步销毁节点树、调度器无后续帧排空 → 拓扑拆除静默退化重引 use-after-free | §3 关闭路径约束：调度器节点后存活并同步排空，或明文退化为 GC 兜底 + 所有权防御 |
| R4-2 | 高 | §7 门控机制未定 + 仅覆盖 process，信号/await/定时器无法壳强制门控 → N1 被绕过 | §7 明确 `process_mode=Disabled` 级联；枚举回调全集；承认非 process 回调为开发者契约+fail-open |
| R4-3 | 中/高 | §1/§4 跨 Fiber 资源所有权/生命周期契约缺位 → 双重释放/原生崩溃 | §1/§4 规定 provider 拥有生命周期、dependent 逆仅 detach、禁 free 共享、原生句柄自判空 |
| R4-4 | 中 | notify 转移无源态守卫 → TearingDown↔Suspending 振荡 | §2 notify 加 `if(State==Active)` 幂等守卫 |
| R4-5 | 中 | 软边自环使合并排序失败、行为未定义 | §3 排序前环检测，仅硬边中止，纯软边环降级忽略该边 |
| R4-6 | 中 | 逆栈中间异常→部分释放即 Dead→上游不一致 | §4 记录索引、部分释放诊断、尽量继续剩余逆/要求 Action 幂等 |
| R4-7 | 低/中 | §5 累积 net 粒度歧义（逐 Fiber vs 按 Scope 求和） | §5 明确逐 Fiber 粒度、Scope 仅分组不求和 |
| R4-8 | 低 | §10 MVP 漏点名 N2 守卫 + 级联期新装载 reject | §10 显式点名 must-land |
| R4-9 | 低 | Suspending 看门狗时钟未定义（墙钟 vs 帧计数） | §2 明确帧计数时钟 + 强制路径可调度 |
| R4-10 | 低 | 「向上游 waiter 级联」与异步调度器术语漂移 | §4/§6 统一为「按拓扑序推进上游」 |

## 10. MVP 实现清单（第4轮收敛判定）

**必须落地（核心正确性）**：§1 Fiber 模型 + 跨 Fiber 所有权契约(R4-3)、§2 状态机 + 幂等守卫(Blocker) + notify 幂等(R4-4) + Suspending 看门狗帧计数(#1/R4-9)、§3 拓扑 + 每批 DAG 重排 + notify + Suspending(硬/软边分离 #2) + **关闭路径约束(R4-1)** + 软边环排序处理(R4-5)、§4 结构化逆回放 + 整任务异常捕获(#4) + **部分释放诊断(R4-6)**、§6 故障处理(**含 N2 守卫 + 级联期新装载 reject 显式 must-land，R4-8**)、§7 Godot 入队 + **`process_mode=Disabled` 门控(#3/R4-2) + 回调边界声明**。

**MVP 内建议落地**：Suspending 看门狗、调度器异常隔离、关闭路径同步排空。

**可推迟（tracked gaps/弱化）**：§5 监控硬化(fail-open)、§8 动态组合/`RecomputeTopology`(仅接缝)、多 Scope(#7)、永久 Fiber 监控(#6)、游戏中期加载(#9)、关闭路径 GC 退化(R4-1)、非 process 回调门控(R4-2)。

**收敛判定**：4 轮审计后，设计在 spatial 维度论题上**完全收敛**——安全/死锁/实现可行性三维均闭环。修复 R4-1 Blocker + R4-2/R4-3 高项后可进入 MVP 实现。
