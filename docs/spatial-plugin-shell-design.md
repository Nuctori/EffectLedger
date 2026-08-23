# 空间维度运行时壳层设计（Godot 壳 + 插件）— 对抗审计靶标 v4（收敛版）

> 对齐论文 *A Programming Paradigm for Spatiotemporal Composability* (cordiverse/paper)。
> 本文档是「3 轮对抗性审计」靶标。**v4 = 第1轮(D1–D10)+第2轮(N1–N5)+第3轮(Blocker+#1–#9)修正后的收敛版**。不修改 L1/L2/L3 代数核心；仅新增**运行时层**补论文 spatial 维度。
> 第3轮判定：修复 §2/§3/§7 的 Blocker + 3 项后，设计已可进入 MVP 实现。

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
- **已知边界（硬约束）**：本仓 L3 `EAA0901`（`EffectAlgebraAnalyzer.cs:44-56`）是**流不敏感、仅按白名单 API 调用点匹配的近似**（"运行期 net 为权威"，"may silently miss cross-method or cross-object pairings"）；本仓 `audit/effect-script-auditA.md` OPEN-2 已证 `IsConserved(At(t))` 是**瞬时**判定，会把合法临时占用误报。设计尊重这两条，不假装静态/瞬时手段可替代运行时权威。

## 1. 组件模型（D2/D5 修正：结构化逆 + 单一权威 Scope）

```csharp
namespace Cosmos.EffectAlgebra.Runtime;

public enum FiberState { Inactive, Active, Suspending, TearingDown, Dead }

// 余效益：所需/所提供。每个 Fiber 有**唯一权威 Scope**（#7），供 §5 聚合分组 + §3 provider-active 过滤。
public sealed record Coeffect(ResourceId Requires, ResourceId Provides, ScopeId Scope);

// 时间维度结构化逆：声明维度(ResourceId/ScopeId/Mode.Release)齐全，壳层可排序/可代数校验（D2）。
// Action 仅作执行载体；逆的「声明语义」以 InverseClaim.Resource/Scope 为准，Action 内部真实释放不在静态可证范围（D2残/N4）。
// InverseClaim.Scope 必须等于所属 Fiber 的 Coeffec.Scope（#7：不一致则装载期 error，非仅 warning）。
public sealed record InverseClaim(ResourceId Resource, ScopeId Scope, Action Execute);

public sealed class Fiber
{
    public FiberId Id { get; }
    public FiberState State { get; private set; }          // Inactive→Active→Suspending→TearingDown→Dead
    public ScopeId Scope => Coeffect.Scope;                // 唯一权威 Scope（#7）
    public Signature Effect { get; }                       // 前向 setup 累积（时空统一效益）
    public Coeffect Coeffect { get; }                     // 余效益
    public ImmutableStack<InverseClaim> Inverses { get; }  // 时间维度：LIFO 结构化逆栈
    public IReadOnlySet<FiberId> Dependents { get; }       // 空间维度：依赖本 Fiber 的 others
    public bool TeardownEnqueued { get; private set; }     // 防重复入队（Blocker 配套，§2/§3）
    // 逆诱导的隐式依赖边也纳入 DependencyGraph（见 §3，保守过近似，D5残）
}
```

## 2. 生命周期状态机（D3/D4/N1/Blocker/#1 修正）

```
Inactive ──load()──▶ Active ──provider-notify──▶ Suspending ──teardown begins──▶ TearingDown ──▶ Dead
   ▲                                                          │                                      │
   └──────────── dependent-first ─────────────────────────────┘                                      │
   └── unload() 对 {Suspending,TearingDown,Dead} 直接 return（Blocker 修复）─────────────────────────┘
```

- `load()` / `unload()` 以 `State` 为**幂等守卫**：`Active/Suspending/TearingDown` 时 `load()` 直接 return；**`unload()` 对 `{Suspending, TearingDown, Dead}` 三者均直接 return**（**Blocker 修复**：补上 `Suspending`，否则已 Suspending 的 Fiber 再被 unload 会二次置 TearingDown + 二次入队 → 双重释放）。`unload()` 重复调用另以 `TeardownEnqueued` 标志防二次入队（双保险）。
- `load()`：运行前向 setup → 累积 `Signature` 入 `Effect`；注册 `InverseClaim` 入 `Inverses`（携带 Resource/Scope，且 `Scope` 须等于 `Coeffect.Scope`，#7）；置 `Active`。
- 依赖仅当 provider Fiber 处于 `Active`（**且同 `Coeffect.Scope`**，N5）才视为 available（论文 provider-active）。
- **`Suspending` 子态（N1）**：当 provider A 进入 `TearingDown` 并 provider-first-notify 到 dependent B，B 立即转入 `Suspending`——**暂停 gameplay 逻辑**（壳强制拦截，见 §7），但保留于场景树并响应 teardown。消除「provider 正在释放、dependent 本帧仍在跑」的 use-after-free 窗口。
- **`Suspending` 看门狗（#1）**：`Suspending` 设最大超时；超时仍未 `Dead` → 强制 `TearingDown`（忽略 provider 通知延迟）+ 硬错误日志告警。防止上游卡住致 B 永久挂起、连带依赖 B 的其它 Fiber 永不收到通知。
- `unload()`：**不入同步等待**——置 `TearingDown`，把本 Fiber 的 teardown 任务**入帧安全调度队列**（见 §3，以 `TeardownEnqueued` 防重），由调度器异步完成；永不忙等（D3）。

## 3. 依赖图 + 拆除顺序（D3/D5/D7/N1/N5/Blocker/#2/#4 修正）

1. **显式边（hard）**：同 `Coeffect.Scope` 内，B 的 `Requires ⊇` A 的 `Provides` ⇒ B 依赖 A（硬边）。
2. **隐式边（soft，D5残）**：B 的某条 `InverseClaim.Resource` 引用了 A 提供的资源 ⇒ 自动派生 B→A 依赖边（逆所需 provider 不可早于逆回放死亡）。隐式边是**保守过近似**：即使 `Requires` 漏写 A，隐式边仍能捕获（不丢边），但可能多建边（过度约束，可接受）。
3. **两源交叉校验（D5残）**：装载期若 `InverseClaim.Resource` 集合与 `Coeffect.Requires` 声明不一致 ⇒ 装载期 warning（不阻塞，记入诊断）。
4. **环检测分 hard/soft（#2，防 Phantom-cycle 误拒）**：装载期仅对**硬边**做拓扑排序并拒绝环（`Requires`/`Provides` 语义环是真实死锁）；**软边（隐式逆边）单独成环只 warning + 允许装载**（过度约束可在动态组合阶段区间化缓解，D10），不硬拒。两路边合并做排序，但拒绝判定只基于硬边。
5. A 进入 `TearingDown` → **provider-first-notify**：通知所有依赖 A 的 B（硬边 + 软边），B 转入 `Suspending`（N1），并把 B 的 teardown 入队（以 `TeardownEnqueued` 防重）。
6. **帧安全调度器（D3残/#4）**：teardown 队列**每批对全部 pending 任务重新做拓扑排序**（按依赖 DAG 叶子优先 / dependent-first），**硬边成环即中止并告警**。整个 teardown 任务体包 **try/catch**：逆回放异常（§4）与非逆异常（#4 补全）均捕获 → 该 Fiber 标记 `Dead` + 向上游 waiter **级联传播**；调度器级 try/catch 隔离单任务故障，防 pending 任务孤儿化（#1 复合）。**环中止不无限重试**：跳过成环子集，把其余子集重新入队并升级告警（避免永久 Suspending）。
7. 全部 B 到 `Dead` → A 回放自身 `Inverses`（LIFO，作用于结构化逆，声明维度等价成立 D2）→ A 到 `Dead`。
8. **Godot 解耦（D7）**：`_ExitTree`/`_Ready` 回调只**入队请求**，不内联同步拆解；执行顺序与 Godot 回调顺序解耦。

## 4. 时间维度（D2 修正：结构化逆=声明等价，非行为等价）

- 每个 effect 配对结构化逆 `InverseClaim`（作者配 inverse，运行时排序）。
- `unload` 时 `Inverses` 栈 **LIFO 回放**，作用于 `InverseClaim`（`ResourceId`/`ScopeId`/`Mode.Release` 维度齐全）→ 在**声明维度**上**等价于** L1 `Mode.Release` 对 `Create` 的配对（D2：壳层代数等价成立）。
- **不声称行为等价（D2残/N4）**：`Action` 内部真实释放的资源由开发者保证与 `InverseClaim.Resource` 一致；L3 `EAA0901` 是流不敏感近似，仅按白名单调用点匹配，可能漏掉间接/委托/反射路径的 release。因此「逐Fiber 闭合」只证明**声明维度**结构闭合，**行为闭合（真实资源配对）以运行时为权威**（见 §5）。
- 逆回放异常（D5/#4）：捕获后标记该 Fiber `Dead` 并向其上游 waiter **级联传播**（有界，受 DAG 规模约束，非雪崩），禁止静默卡死。

## 5. 余效益闭环（D1/D9/N3/#5 修正：声明闸门 + 运行时权威 + 累积net监控）

- **卸载闸门 = 逐 Fiber、声明维度、完整性校验（非泄漏防护，#5 澄清）**：只校验「本 Fiber 的 `Inverses` 是否完整逆置其自身 `Effect`」的**结构闭合**（声明维度）。**不声称运行时恒为真**——若某插件逆经间接调用走 release（L3 白名单漏匹配），装载期闸门放行，真实泄漏由下方运行时监控兜底。保留 `ForceUnload()` / `Unload(ignoreResidual: true)` 逃生通道 + 告警升级。
- **全局聚合降级为 fail-open 监控（D1）**：**绝不作为卸载闸门**。按各 Fiber 自身 `ScopeId` **分组分别**校验（D9，绝不跨作用域合并）。
- **监控度量必须避免 OPEN-2 告警风暴（N3）**：全局监控**不得**用瞬时 `IsConserved(At(t))`（OPEN-2 已证会把合法临时占用全误报）。须用 OPEN-2 的**脚本级累积 net**（跨全部事件求和、Fiber 退出时判零），仅在 Fiber 永久退出后仍有正余量才告警（**永久存活 Fiber 不查，属 §8 已知缺口 #6**）。

## 6. 故障处理（D5/D6/D8/N2/#4 修正）

| 故障 | 处理 |
| --- | --- |
| 硬边依赖环 A→B→A（语义环） | 装载期检测硬边环 ⇒ 拒绝装载 |
| 软边（隐式逆）环 | 仅 warning + 允许装载（#2，防 Phantom-cycle 误拒） |
| provider 在 Active 崩溃/消失 | 视为强制 `unload` ⇒ 级联 teardown（§3，B 转 Suspending） |
| 逆回放异常 / teardown 任务任意异常（#4） | 整任务 try/catch → 标记 `Dead` + 向上游 waiter 级联传播，禁静默卡死/孤儿化 |
| 环中止 | 跳过成环子集，其余重入队 + 升级告警（不无限重试） |
| Suspending 看门狗超时（#1） | 强制 `TearingDown` + 硬错误日志 |
| 余效益未闭合（逆缺失/间接释放） | **逐 Fiber** 声明闸门放行 + 运行时监控兜底（§5） |
| 级联期间新装载依赖 TearingDown provider | 显式 reject + 告警，或延迟到该 provider 真正 `Dead` 后判定（D6） |
| `RecomputeTopology()` 调用时存在 `TearingDown/Suspending` Fiber（N2） | **守卫**：任一 Fiber 处于 `TearingDown/Suspending` 时拒绝/延迟执行，避免半死节点增删边致图不一致 |
| 动态重组合（论文 dynamic composition） | MVP = **静态组合**（假设 Fiber 全集在装载期已知，#9）；`RecomputeTopology()` 为动态组合前置钩子（受 N2 守卫）；`Reload()` 仅重算守恒不重算拓扑 |

## 7. Godot 壳集成点（D4/D7/N1/#3 修正：壳强制 State 门控）

- Godot 宿主：加载插件程序集（`Assembly.Load`），每插件 = 一个 `Fiber`。
- `_Ready`→`load()` 入队；`_ExitTree`→`unload()` 入队（**只入队，不内联拆解**，D7）。
- **壳强制 `State` 门控（#3，N1 前提）**：壳在 Godot `process`/`physics_process` 派发时**按 `State` 强制跳过**——仅 `State==Active` 才执行插件 gameplay 回调；`Suspending`（已暂停的 dependent）与 `TearingDown`（正在释放的 provider）**一律不派发**。这由壳保证，不依赖插件自觉检查标志位（否则非协作插件即突破 N1 的 use-after-free 防护）。
- `Suspending` 子态（N1）：Fiber 收到 provider 通知后由壳暂停 gameplay（上述门控），teardown 完成才释放。
- 处理 Godot 不对称/多次语义：`_ExitTree` 在从未 `_Ready` 时也安全（幂等守卫 D4/Blocker）；树重组「先 exit 再 re-enter」由状态机自然吸收。

## 8. 已知缺口（v4 收敛 + 硬约束 + 推迟项）

- G6 关闭：聚合**按 ScopeId 分组**，绝不跨作用域（D9）。
- G4 结论：逐 Fiber **声明闸门（完整性校验，非泄漏防护，#5）** + 全局 **fail-open 累积net监控**（D1/N3）。
- G3 补接缝：MVP 静态组合（#9 假设）+ `RecomputeTopology()` 钩子（受 N2 守卫，D8）。
- **硬约束**：全局监控必须用 OPEN-2 累积 net，禁用瞬时 `IsConserved(At(t))`（N3）。
- **推迟项（tracked，非阻断）**：§5 监控硬化（MVP fail-open 即可，#5）、§8 动态组合/`RecomputeTopology`（仅接缝）、多 Scope 处理（#7 已强制一致）、永久存活 Fiber 监控缺口（#6）、游戏中期 mod 加载破坏静态拓扑（#9）。

## 9. 审计发现总表

### 第1轮（D1–D10，已融入）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| D1 | 严重 | §5 全局聚合 fail-closed 闸门致几乎全部卸载被永久拒绝 | 逐Fiber声明闸门+全局fail-open监控 |
| D2 | 严重 | `Inverses` 不透明 `Action` ≠ 结构化逆，失 L1 等价 | `InverseClaim` 结构化逆 |
| D3 | 严重 | 无并发/重入模型，同步等 dependents 在单线程帧模型不可能 | 帧安全调度队列 |
| D4 | 严重 | 无幂等守卫，双重 `_ExitTree` 双重 free | `State` 守卫 |
| D5 | 中/高 | 逆闭包捕获跨Fiber状态对依赖图不可见 | 逆诱导依赖边入图+异常传播 |
| D6 | 中 | 级联中装载依赖 TearingDown provider 未定义 | 冻结新装载/reject |
| D7 | 中 | Godot exit 顺序与 dependent-first 冲突 | 回调只入队解耦 |
| D8 | 中 | 砍 dynamic composition 但静态拓扑冲突 | 静态组合+`RecomputeTopology()` 接缝 |
| D9 | 低/中 | NetTable 聚合缺 ScopeId→跨作用域误报 | 按 ScopeId 分组 |
| D10 | 低 | 静态依赖过度约束时序余量 | 动态组合阶段区间化缓解（记录） |

### 第2轮（D1/D2/D3/D5 残留 + N1–N5，已融入 v3/v4）

| 编号 | 严重度 | 残留/新增缺陷 | 修正 |
| --- | --- | --- | --- |
| D1-残 | 高 | 逐Fiber闸门仍假设「运行时恒真」，但 L3 流不敏感可能漏间接释放 | 闸门降级为声明闭合+运行时权威兜底；明确 L3 边界 |
| D2-残/N4 | 中 | `InverseClaim` 仍携不透明 `Action`，行为等价不可静态证 | §4 明确结构化逆仅声明等价，行为由运行时权威 |
| D3-残 | 中 | 调度 FIFO 而非每批 DAG 重排 → dependent-after-provider use-after-free | §3 强制每批重排+环即中止 |
| D5-残 | 中 | `InverseClaim.Resource` 与 `Requires` 不一致/Action 真实释放≠声明 | §3 两源交叉校验+隐式边声明为保守过近似 |
| N1 | 高 | TearingDown 窗口 dependent 仍在跑 gameplay → use-after-free | §2/§7 新增 `Suspending` 子态 + 壳强制门控 |
| N3 | 高 | 全局监控用瞬时 `IsConserved(At(t))` → 合法临时占用全误报告警风暴 | §5 改用 OPEN-2 累积 net；写入 §8 硬约束 |
| N2 | 低/中 | `RecomputeTopology()` 遇 TearingDown Fiber → 半死节点图不一致 | §6 加守卫：有 TearingDown/Suspending 时拒绝/延迟 |
| N5 | 低 | provider-active 跨 scope 可用性未隔离 | §3 明确以 `Coeffect.Scope` 过滤 |

### 第3轮（Blocker + #1–#9，已融入 v4）

| 编号 | 严重度 | 缺陷 | 修正 |
| --- | --- | --- | --- |
| Blocker | 严重 | `unload()` 守卫漏 `Suspending` → 二次 unload 二次入队 → 双重释放 | §2/§3 守卫补 `Suspending` + `TeardownEnqueued` 防重 |
| #1 | 中 | `Suspending` 无看门狗 → 上游卡住致永久挂起 | §2/§6 加 Suspending 超时→强制 TearingDown + 日志 |
| #2 | 中 | 隐式过近似边进硬环检测 → Phantom-cycle 误拒装载 | §3 硬/软边分离；仅硬边环拒，软边环仅 warning |
| #3 | 中 | §7 门控靠插件自觉检查标志位 → 非协作插件突破 N1 | §7 壳强制按 `State` 跳过 process（仅 Active 派发） |
| #4 | 中 | 调度器仅"环即中止"；非逆异常/重试/拓扑异常未隔离 → pending 孤儿化 | §3/§6 整任务 try/catch→Dead+级联；环中止不无限重试 |
| #5 | 低/中 | "闸门"措辞夸大，MVP 实为信任开发者+事后监控 | §5 澄清"声明完整性校验，非泄漏防护" |
| #6 | 低 | 永久存活 Fiber 永不触发累积 net 监控 | §8 列为已知缺口（fail-open 一致） |
| #7 | 低 | `Coeffect`/`InverseClaim` 各带 Scope 不一致仅 warning | §1/#7 强制 `InverseClaim.Scope == Coeffect.Scope`（error） |
| #8 | 低 | §2 行文/图省略 TearingDown 中间态、未反映 Blocker 非对称 | §2 图/文补 `Suspending→TearingDown→Dead` |
| #9 | 低 | MVP 静态假设未声明，游戏中期加载破坏拓扑 | §6/#9 显式 MVP 假设 Fiber 全集装载期已知 |

## 10. MVP 实现清单（第3轮收敛判定）

**必须落地（核心正确性）**：§1 Fiber 模型、§2 状态机 + 幂等守卫(**含 Blocker 修复**) + Suspending 看门狗(#1)、§3 拓扑 + 每批 DAG 重排 + notify + Suspending(**含硬/软边分离 #2**)、§4 结构化逆回放 + **整任务异常捕获(#4)**、§6 故障处理、§7 Godot 入队 **+ 壳强制 `State` 门控(#3)**。

**MVP 内建议落地**：Suspending 看门狗(#1)、调度器异常隔离(#4)。

**可推迟（tracked gaps）**：§5 监控硬化(fail-open 即可)、§8 动态组合/`RecomputeTopology`(仅接缝)、多 Scope(#7 已强制一致)、永久 Fiber 监控(#6)、游戏中期加载(#9)。

**收敛判定**：3 轮审计后，设计在 spatial 维度论题上高度收敛；Blocker + #1/#2/#3/#4 修复后**可进入 MVP 实现**。
