# Runtime 生命周期攻击报告（P5 批次·Runtime 生命周期攻击审计员）

- 审计对象：`src/Cosmos.EffectAlgebra.Runtime/`（PluginRuntime / GodotShell / DependencyGraph / InverseReplay / ProviderCrashCascade / LoadValidation / Fiber / IHost）
- 审计方式：只读代码审计 + 临时目录独立探针工程实证（net10.0，直接引用 `bin/Release/net10.0` 既有 DLL，未向仓库写入任何构建产物；探针工程用毕即删）。探针输出以 `OBS[P*]` 标注逐条引用。
- 诚实边界参照：README #10（单线程/单场景/不可重注册）、#11（跨 scope Σnet 口径）、#17（异常方言表）、#19（QED-C2 有界增长「已解决」）。
- 审计日期：2026-09-08

## 总评（状态机/级联的鲁棒性结论）

**结论：核心状态机与级联机制对本清单中的非法转移、重入、看门狗连击、崩溃叠加攻击表现出成熟的多层防御，整体鲁棒性高；但生命周期「关路径/级联期」守卫存在一个系统性遗漏——`LoadAll` 是唯一没有 `IsShuttingDown`/级联期守卫的生命周期入口（`Register`/`AddDependency`/`RecomputeTopology` 均有），实证可造成「退出后残留永久 Active 纤程」的泄漏。** 其余发现集中在诊断一致性（`ProviderCrashCascade.Handle` 的 OnSuspending 无去重）、方言一致性（null 参数 NRE vs ArgumentNullException）与边界 #19 的表述强度（空队列早退路径跳过 `_netAccum` 剪除）。

五态状态机的转移矩阵经攻击验证无非法转移可被外部调用者打出：所有「回退/跳跃」路径（Dead→任何态、Active→Dead 直跳等）在 `Fiber` 中结构性不存在；`Unload` 对 Inactive 的 D4 直达 Dead 是防「释放未获取资源（双重释放类崩溃）」的正确设计。重入门方面，`ReplayInProgress`（R3-RT-04）+ `TeardownEnqueued` 单点成对入队（REG-01）+ 排空期 Dead-skip 陈旧任务防御三层叠加，在「逆 Action 重入 TickWatchdog + 嵌套 DrainTeardownBatch」的最恶劣组合下仍保证每条逆恰好执行一次（P7 实证）。看门狗三路径（强制/自愈/级联）在连击与崩溃叠加下无重复回放、无假崩溃报告（P11/P4 交叉验证）。唯一需要指出：`BeginTeardown` 级联递归深度与依赖链深线性相关（仓库钉 3000 深通过，10⁴ 以上未测，推理未实证，按规则不单列严重发现）。

## 攻击发现清单

| # | 严重度 | 攻击向量 | 实证方式 | 实际行为 | 判定 |
|---|--------|----------|----------|----------|------|
| 1 | **MED** | **逆 Action 中调用 `LoadAll`（关路径/级联期无守卫）**：SynchronousExitDrain 排空中某 fiber 的逆 Action 调 `rt.LoadAll()`（P8a）；或级联排空中调 `LoadAll()`（P8b）。`Register`/`AddDependency`/`RecomputeTopology` 均有关路径+级联期双守卫，`LoadAll` 一个都没有 | 临时工程实证（P8a/P8b） | 退出排空期间 Inactive fiber `g` 被 `Load()` 激活；排空完成后 `g.State=Active`、队列空、场景已死——**无任何路径再覆盖 g 的 teardown**（入队仅发生在 BeginTeardown/看门狗/退出补队三点，均已错过），逆永不回放=资源泄漏；级联期变体同样使 `h` 排空后残留 Active | **实现缺陷**（守卫不对称；D4/A3-10 的补队逻辑覆盖不了排空中途被激活的 fiber） |
| 2 | LOW | **`AttachShell(null)` 接线**：首次接线传 null | 临时工程实证（P1） | `ReferenceEquals(null, null)` 命中幂等守卫 ⇒ **静默 no-op 不抛**；`OnSuspending` 保持 null、退出 drain 未注册，宿主误以为接线成功；之后再接真壳仍成功，无痕掩盖 | **实现缺陷**（防御不对称：`Register(null)` 抛 ArgumentNullException，此处静默；违反「构造期 loud 拒绝」纪律，#17 方言表亦未涵盖） |
| 3 | LOW | **崩溃级联对已 Dead 依赖者重复触发 OnSuspending**：provider 逆回放部分失败/抛异常 ⇒ `ProviderCrashCascade.Handle` 对 `DependentsOf` 全量 `MarkSuspending`+`OnSuspending`，无 `!TeardownEnqueued` 去重（BeginTeardown/TickWatchdog 两分支同型处均有，#191 low 确立的纪律） | 临时工程实证（P4） | dependent-first 排空下 d 先 Dead，p 后崩 ⇒ `OnSuspending(d)` 触发 **2 次，序列=[Suspending; Dead]**——第二次发生在已 Dead 的依赖者上；壳侧 `DisableDispatch` 对已释放子树 id 重复/失效调用，运行时侧无观测差异（幂等） | **实现缺陷**（一致性缺口；低危——壳语义幂等兜底，但真实宿主对已释放节点做 ProcessMode 操作有越界风险面） |
| 4 | LOW | **OnSuspending 钩子内重入 BeginTeardown：被重入者自身钩子被跳过**：链 p←d1←d2，钩子 `OnSuspending(d1)` 内直接 `BeginTeardown(d2)` | 临时工程实证（P5） | d2 被重入路径推进 TearingDown+入队并正常 Dead，但外层级联以 `!TeardownEnqueued` 判定跳过 ⇒ **d2 的 OnSuspending 全程未触发**（钩子触发列表仅 [d1]）⇒ 壳 ProcessMode 永不禁用 d2 子树；派发安全仍由 `ShouldDispatch` 状态门兜住（仅 Active 派发） | **实现缺陷**（低危，有兜底；「钩子必达」不成立且无文档声明） |
| 5 | LOW | **对未注册（外来）Fiber 调 BeginTeardown**：`new Fiber(...)` + IVT `Load()` 后 `rt.BeginTeardown(foreign)`，其逆抛异常 | 临时工程实证（P3） | 逆回放**真执行**（外来 fiber 照常 TearingDown→入队→Dead），但两条升级路径均被 `_fibers.TryGetValue(providerId, ...)` 门吞掉 ⇒ **CrashReports=0、LastCrashReport=null，部分释放失败零诊断** | **实现缺陷**（诊断完整性缺口；公共 API `BeginTeardown(Fiber)` 接受任意实例，未校验属本 runtime） |
| 6 | LOW | **`_netAccum` 剪除豁口**：①`AccumulateNet` 喂入不存在 FiberId（表照收，P9a）；②旁路 `Unload`+`MarkDead` 直达 Dead 后走**空队列** SynchronousExitDrain（P9b）/空队列 DrainTeardownBatch——两方法空队列早退分支均**先于 `PruneDeadFiberAccumulations` 返回** | 临时工程实证（P9a/b/c） | 未知 id 条目与死纤程条目跨批次残留（P9b：entries=2）；直到**首个非空排空**才被兜底剪除（P9c：entries=0）。有界性依赖「宿主 id 空间有限 + 终会发生一次非空排空」，非结构性保证 | **文档欠明确**（诚实边界 #19 的「已解决」表述过强——剪除挂在「非空排空完成」单点，早退路径有豁口；内存仍受 distinct-id 数上界约束，故降为文档级） |
| 7 | LOW | **边界/方言攻击**：①`CheckPermanentFiberLeak(-1)` 负阈值；②`AccumulateNet(null)`/`TickWatchdog(null)`；③`isTimedOut` 委托抛异常（③推理未实证） | ①②临时工程实证（P10c/d/e），③代码推理 | ①acc=0 的 fiber 被误报为泄漏（`0 > -1` 恒真，零告警语义失真）；②抛 **NullReferenceException** 而非 ArgumentNullException——与 `Register` 的 `ArgumentNullException` 纪律及 #17 方言表不一致；③宿主委托异常直接逸出 TickWatchdog 至帧循环，无隔离（已处理者状态不受损） | **实现缺陷**（参数校验缺失/方言不一致；无内存安全问题） |
| 8 | LOW | **双 runtime 接线同一 GodotShell**：rt1、rt2 均 `AttachShell(s)`（A3-13 守卫只防「同 runtime 重复接线/跨壳」，不防多 runtime 共壳） | 临时工程实证（P2） | 均不抛；shell._exitDrains.Count=2；FlushExitDrain 两条 drain 各自独立排空、互不串扰（P2c 双双正确释放）；退出期 `_exitDraining` 共享——一 runtime 的退出会屏蔽另一 runtime 后续的壳级 Defer | **文档欠明确**（行为实际良性，但「多 runtime 共壳」拓扑是否受支持无任何文档声明；#10 单场景契约下属灰色地带） |
| 9 | LOW | **绕过 AddDependency 的图后门**：宿主经公共属性 `rt.Graph.AddHardEdge(d, p)` 直加边（R7-N5 只在图内补了同 Scope 校验，未回填 `provider.Dependents`）（推理未实证） | 代码推理（DependencyGraph.cs L28-32 vs PluginRuntime.cs L132-134） | 运行时级联（DependentsOf 读图）正常，但 Godot 壳 ProcessMode 级联遍历 `fiber.Dependents`（双账本）为空 ⇒ 壳级联静默 no-op；BeginTeardown 级联却照常推进——两套级联观测不一致 | **文档欠明确**（推理未实证，按规则由 MED 降 LOW；R2A-10 已把 Remove 收 internal，但加边后门仍在公共面） |
| 10 | — | **非法转移四连**（BeginTeardown(Dead)、LoadAll 后重复 Register 同名、Suspending 中二次 BeginTeardown、看门狗对 Suspending fiber 连续两帧触发） | 临时工程实证（P11/P12a-d） | ①Dead ⇒ 静默 no-op（TeardownEnqueued+State 双守卫）；②重复 Register ⇒ **InvalidOperationException**（R7-L1）；③Suspending ⇒ 正常推进 TearingDown+入队，二次调用不重复入队（Pending 恒 1）；④看门狗第 2 帧 Pending 不变（2→2）、逆执行恰 1 次 | **防御生效**（全矩阵与 #17 方言表吻合） |
| 11 | — | **重入三连**：OnSuspending 钩子内调 SynchronousExitDrain（P6）；逆 Action 内重入 TickWatchdog+嵌套 DrainTeardownBatch（P7）；teardown 排空中 Register/AddDependency | 临时工程实证（P6/P7）+ 代码推理 | P6：p/d 逆各执行 1 次、双双 Dead、IsShuttingDown 正常复位——无双重回放；P7：嵌套排空+看门狗自愈叠加下每条逆仍恰执行 1 次、无假 CrashReport、Pending 归零（REG-01 陈旧任务 Dead-skip 实证生效）；排空中 Register ⇒ InvalidOperationException（Any(TearingDown) 守卫），异常被逆回放 per-inverse catch 收敛为该 fiber 的部分失败升级，可观测 | **防御生效**（重入门三层防御在叠加攻击下成立） |
| 12 | — | **CrashReports 环形边界**：反射直击私有 `AddCrash` 恰好 64/65 条 + ResetDiagnostics 交互 | 临时工程实证（P13/P13b） | 64 条时 Length=64 首条=c1；第 65 条后 Length=64、首条=c2（精确丢最旧 1 条）、LastCrashReport=c65（last 语义保持）；Reset 后从零重建 | **防御生效**（#19 环形上限承诺精确成立；既有 QedP2C2 钉只测 66，本探针补齐 64/65 边界点） |
| 13 | — | **long.MinValue 累积与回绕**：acc=long.MinValue 直接判定；MaxValue+1 无检查回绕 | 临时工程实证（P10a/b） | MinValue 判定**无 OverflowException**（`acc > t \|\| acc < -t` 双侧比较如注释所声称）；MaxValue+1 静默回绕为负，仍被负侧捕获照常告警（符号翻转不漏报） | **防御生效**（注释声称的防溢出语义实证成立；加法本身 unchecked 回绕属可接受锐边——量级近 2⁶³ 的泄漏计数已无工程意义） |

## 值得表扬

1. **重入门三层防御的组合正确性**（`ReplayInProgress` + `TeardownEnqueued` 单点成对入队 + 排空期 Dead-skip）：P7 用「逆 Action 重入 TickWatchdog 自愈 + 嵌套 DrainTeardownBatch」的最恶劣叠加攻击实测，每条逆仍恰执行一次、零假崩溃报告——这正是 R3-RT-04/REG-01 两组修复声称的语义，且在组合场景下无隐式耦合破裂（此前 3/5 站点漏置位的失步 bug 已被结构性单点化根除）。
2. **D4「未 _Ready 也安全」+ A3-10 补队**的配套设计：Inactive fiber 经 Unload 直达 Dead 并自锁 `TeardownEnqueued`，使「宿主漏调 BeginTeardown 的假绿式退出」和「释放未获取句柄的双重释放崩溃」两类对称风险同时堵死（P12b/P6 交叉验证）；退出排空对旁路 Unload 的全量补队（A3-10）让关路径不依赖宿主自觉。
3. **诊断可观测性纪律的一致落实**：崩溃级联在四条路径（DrainTeardownBatch 整任务异常/部分失败、SynchronousExitDrain 对称两路）全部升级 `ProviderCrashCascade.Handle` 并保留原始异常为 InnerException（A3-08），CrashReports 环形上限让「不依赖宿主自觉 Reset」的内存安全成为结构性质（P13 精确边界实证）——#19 的收口方向正确（豁口仅剩发现 #6 的早退路径表述强度）。

## 附：实证工程说明

- 探针工程位于临时目录（`%TEMP%\p5audit`，程序集名借用 `Cosmos.EffectAlgebra.Runtime.Tests` 以获得 IVT internal 观测口 `PendingTeardownCount`/`NetAccumEntries`），仅引用仓库既有 `bin/Release/net10.0` DLL，未触发仓库内 obj/bin 写入；审计完成后已整体删除。
- 未实证发现仅 #9（按规则已降级）与 #7-③（并入 #7 不单列）；其余所有 `OBS[P*]` 均为运行时实证输出。
