# ISSUES — 缺陷与处置台账

> 单一事实来源：所有已复现缺陷、修复状态、回归钉与**明确接受的残余风险**都在此登记。
> 历史审计报告（`audit/` 其余文件）是当时的证据链快照，不回写改写；状态以本台账为准。
> 状态语义：`FIXED`（修复 + 回归钉在位）/ `OPEN`（活跃缺陷，附复现与影响）/ `ACCEPTED`（设计边界，理由与兜底，同步 README 诚实边界编号）。
> 登记纪律：缺陷须带可复现证据（复现脚本/最小用例/测试名）；修复须带回归钉（测试名 + 钉住的文件行号语义）；关闭状态只能由「修复提交 + 回归钉」达成。

## FIXED

> **处置记录（诚实留痕）**：O-2026-09-14-01 排查中途曾据"停滞宿主无测试工作线程"误判为 xUnit
> 集合并行调度停滞，短暂加过 `xunit.runner.json` 全串行配置；后续对第二只停滞样本抓栈，
> 定位到 `ChildProcessRunner.GetResult` 阻塞（孤儿句柄持有管道），根因改判，串行配置已撤销。

| ID | 日期 | 摘要 | 证据（复现） | 修复 | 回归钉 |
| -- | ---- | ---- | ------------ | ---- | ------ |
| I-2026-09-14-01 | 2026-09-14 | `TickWatchdog` 自愈分支二次调用宿主谓词 `isTimedOut(f)`，且在逐 fiber 异常隔离之外——谓词有状态（首次 true、再次抛）时异常逸出帧循环，同帧其余超时 fiber 失去回收机会，宿主谓词每帧被重复执行 | 最小复现：谓词第二次调用抛异常，异常逃逸 `TickWatchdog`，其余 Active fiber 未被强制 | `PluginRuntime.TickWatchdog` 自愈分支复用本帧已判定的 `timedOut`，每帧每 Fiber 恰调用谓词一次 | `RoiAudit202609Pins.Watchdog_PredicateCalledOncePerFiberPerTick` / `Watchdog_SecondPredicateCallWouldThrow_DoesNotEscape_OthersReaped` |
| I-2026-09-14-02 | 2026-09-14 | L3 分析器同站点 Claim 分桶按 `(kind,mode)` 去重后以 `First(...)` 取资源——同一 API 对多资源声明同 `(kind,mode)` 时后续资源被静默丢弃，跨 API 同资源冲突（EAA0304）在支持范围内漏报 | 最小复现：扩展白名单 API `Multi`（mem:111+mem:222 均 occupy+move）与 `Other`（mem:222 move）同方法调用 ⇒ 零诊断（应报 `Move+Move` 冲突） | `EffectAlgebraAnalyzer.AnalyzeKindMixAndCompat` 改按（归一资源, kind, mode）三元组分桶去重，每 Claim 归入自己的资源桶 | `MultiResourceClaimPins.ConflictOnLaterResource_Reported` / `ConflictReported_IndependentOfClaimOrder` / `SingleResourceConflict_StillReported_NoFalsePositiveOnExclusiveResource` |
| I-2026-09-14-03 | 2026-09-14 | `SynchronousExitDrain` 无重入门：退出排空期间逆 Action 重入触发嵌套调用，嵌套空队列路径把 `IsShuttingDown` 中途复位 ⇒ 逆 Action 内 `LoadAll` 守卫失效，预注册未装载 fiber 被激活，退出结束后永久 Active 且无 teardown 路径（`audit/p5-qed-claim-attack.md` 攻击 #2，本次复确认仍未闭合） | 最小复现：逆 Action 内嵌套调用 `SynchronousExitDrain` 后 `LoadAll` 成功，Inactive fiber 变 Active，CrashReports 为空 | `PluginRuntime` 增加退出排空深度门 `_exitDrainDepth`：嵌套调用 no-op、关闭态保持；仅最外层出口复位标志（`finally` 保证） | `RoiAudit202609Pins.NestedExitDrain_KeepsShuttingDown_LoadAllRejected_InactiveStaysInactive`（既有钉 `QedP53RuntimeR2HardeningPins` 钉 1/2 兼容保持） |
| I-2026-09-14-04 | 2026-09-14 | `GodotShell.FlushExitDrain` 的 `OnDrainFault` 观测回调自身抛异常时不被隔离——上报通道故障逸出，剩余 drain（含 `PluginRuntime` 退出排空）全部不执行、`_exitDrains` 不清 | 最小复现：drain 抛异常 + `OnDrainFault` 再抛 ⇒ 第二个 drain 执行 0 次，`FlushExitDrain` 向外抛出 | `OnDrainFault?.Invoke` 独立 try/catch 隔离；其余 drain 照跑、批只执行一次语义不变 | `RoiAudit202609Pins.FlushExitDrain_OnDrainFaultThrows_RemainingDrainsStillRun` |
| I-2026-09-14-05 | 2026-09-14 | `GodotShell.Defer` 先登记去重集合再调宿主入队，宿主抛异常不回滚——同 Action 重试被 `_deferred` 永久吞掉（一次可恢复宿主故障 = 该 Action 静默永不执行，保留闭包引用） | 最小复现：宿主首次入队抛、此后正常；重试同一 Action 后宿主收到 0 次调用、Action 执行 0 次 | 入队失败撤销本次去重登记并重抛原异常；契约明示：宿主入队成功即接管、抛异常即未入队（重试有效） | `RoiAudit202609Pins.Defer_HostEnqueueFails_RollbackDedup_RetrySucceeds` |
| I-2026-09-14-06 | 2026-09-14 | L2 生成器对 verbatim 转义类型名（如 `class @event`）失败：TypeName 带 `@` ⇒ `AddSource` hint 名含非法字符抛 `ArgumentException`（CS8785），生成器整体不生成（0 个文件）；`@` 进入生成成员名 `Compute{…}` 亦为非法标识符 | 最小复现：`class @event` 与 `class Other` 各含同名标注方法 ⇒ CS8785，`GeneratedTrees.Length==0` | `EffectAlgebraGenerator.SanitizeTypePart` 统一剥离 verbatim 前缀 `@`（`@x` 与 `x` 在 C# 同形，剥离不引入新撞名），类型段与命名空间段共用 | `ProdAuditBatch4ToolingTests.Generator_EscapedTypeName_EmitsCompilableOutput` |
| O-2026-09-14-01 | 2026-09-15 | L1 主测试工程（EffectLedger.Tests）的测试宿主在**含真实子进程 `dotnet build` 的测试**（GateFixture / R6 / R3-CLI 族）运行期间间歇性挂起（CPU 空转），被 vstest blame/hang 终止后即报"测试主机进程崩溃" | 2026-09-14/15 数据点：① slnx 全量执行 L1 时 >15min 无进展（testhost 存活、CPU 0ms/5s）；② blame 定位 `AnalyzerConsumer_Build_EmitsEaa0901` 运行中崩溃（前 555 项全过）；③ 事件日志无 testhost 原生崩溃记录 ⇒ "崩溃"=挂起被回收的表现。**根因（dotnet-stack 栈证据钉死）**：子进程 stdout/stderr 重定向管道的 EOF 被第三方持有者无限推迟——`dotnet build` 默认 nodeReuse=true 的驻留 MSBuild 节点继承管道句柄，节点被外部终止成为孤儿（会话清理/看门狗杀进程均制造孤儿）后管道永不 EOF，旧写法 `ReadToEnd()`/`ReadToEndAsync().GetResult()` 等待端永久阻塞；另叠加旧双同步 ReadToEnd 的经典双流死锁变体（stderr 灌满 ~4KB 管道缓冲） | 新增 `tests/EffectLedger.Tests/ChildProcessRunner.cs` 统一收编 4 处 spawn（R6/R3/QED-A4/Batch4）：①事件累加（BeginOutputReadLine）不依赖 EOF；②进程退出为唯一等待边界 + EOF 仅 5s 有界宽限；③超时杀整进程树（确定性红）；④注入 `MSBUILDDISABLENODEREUSE=1` 从源头消灭孤儿句柄持有者。门禁脚本（ci.yml/ci.sh/ci.ps1）测试步骤加 `--blame-hang --blame-hang-timeout 300s` 兜底（任何停滞转为有界失败） | `ChildProcessRunnerPins.Runner_StderrFlood_ReturnsCleanly_NoDeadlock`（4MB stderr 逐行灌流 ≫ 管道缓冲，旧写法死锁形态）/ `Runner_Timeout_KillsTree_AndThrows`（3s 超时杀树）；压测 5 轮 × 22 进程型测试全绿（此前同形态第 2 轮必挂） |
| O-2026-09-14-02 | 2026-09-14 | 打包消费链路此前无 CI 级验证（本周Workflow才有 pack 门，PR 门无 pack；`GodotReal` 样板实际只引 L1、未接 Analyzer/Generator）——包能否被独立工程经真实 NuGet 管线消费未被证明 | 本日新增 `tests/ConsumerSmoke/`（配对绿/泄漏红/Generator 单装闭包 + emit 落盘核对）并接入 `ci.yml`/`ci.sh`/`ci.ps1`，本机两轮 PASS | 三门同款烟测常驻；发布前「包级可用」主张由 CI 每次推送背书 | 观察 CI 首跑；若 ubuntu 上 pack --no-build 时序不稳，改为 pack 前显式 build |

## ACCEPTED（设计边界，非缺陷——修复即破坏冻结契约）

| ID | 边界 | 理由与兜底 | 详见 |
| -- | ---- | ---------- | ---- |
| A-01 | L3 静态近似盲区：跨方法/跨对象配对、构造期/属性形态、`#7` 哨兵跨 API 假配对、`#20` 常量实例保守合并 | 流不敏感近似是显式设计（误报洪水 vs 漏报的取舍）；权威判据 = 运行期 Σnet 闭合 | README 诚实边界 #7/#9/#20、`EffectAlgebraAnalyzer` 类注释 |
| A-02 | Runtime 单线程、单场景生命周期、同 FiberId 不可重注册 | 帧驱动零锁模型（#10 冻结契约）；场景重载新建 `PluginRuntime` | README 诚实边界 #10 |
| A-03 | 分析器/生成器仅在 .NET SDK `dotnet build` 宿主加载（VS/MSBuild Roslyn 宿主静默不加载） | Roslyn 宿主能力边界，README ① 显式声明（doc-guard 钉住） | `Readme_HostPrerequisite_Declared` |
| A-04 | `At(t)` 集合投影 vs `Audit` 计数语义双口径 | 多重性载体决策（QED-A5），非缺陷 | README 诚实边界 #5/#6 |
