# Runtime 生命周期攻击报告 R2（P5 批次 · QED-P5.3 加固后复验 + 新攻击向量）

- 审计对象：`src/Cosmos.EffectAlgebra.Runtime/`，审计基线 = **HEAD `39a7073`**（含 QED-P5.3 加固五钉：LoadAll 关闭期守卫 / AttachShell(null) / AccumulateNet(null) / TickWatchdog(null) / CheckPermanentFiberLeak 负阈值）。
- 与 R1 报告的关系：R1（审计员丁，`audit/p5-auditor-runtime.md`，同 commit 入库）审计于加固前树。本报告为独立第二轮：①复验 R1 修复在加固后树上真实生效；②提交 R1 未覆盖的新攻击向量（重入关路径链 / 新守卫绕过 / Graph 公共突变口 / 看门狗谓词逃逸）。
- **路径说明**：任务指定的输出路径 `audit/p5-auditor-runtime.md` 已被 R1 报告占用且已提交入库（commit 信息引用）。为不覆盖已入库文档（仓库惯例 auditR1/R2 版本化并存），本报告写至 `-r2` 后缀路径，未动任何源代码/文档/测试。
- 审计方式：只读代码审计 + 临时目录独立探针工程实证（net10.0 控制台，ProjectReference 直接构建仓库源码；internal 观测口经反射访问；探针工程用毕即删，未向仓库写入任何产物）。探针输出以 `P*` 编号逐条引用。除单列「推理未实证」条目外，全部发现均经临时工程实证。
- 诚实边界参照：#10（单线程/单场景/不可重注册）、#17（异常方言四族）、#19（QED-C2 有界增长）。审计日期：2026-09-08。

## 总评（状态机/级联的鲁棒性结论）

**结论：五态状态机的转移面与级联机制在攻击下依然无一条非法转移可被外部调用者打出；重入门对「双释放」这一最致命危害的防御（`ReplayInProgress` + `TeardownEnqueued` 单点成对 + 排空 Dead-skip 三层）在最恶劣重入链下实证未被穿透。但本轮发现一个 R1 未覆盖的系统性缺口——`SynchronousExitDrain` 无重入门：逆 Action 重入关路径会把「正在回放中」的 fiber 经自愈分支再入队 → R3-RT-04 loud 抛 → fail-open 提前打 Dead → **顺带复位 `IsShuttingDown`**，从而使 QED-P5.3 刚落地的 LoadAll 关闭期守卫与 Register 级联期守卫**双双被绕过**，退出排空可假绿返回（残留 Active fiber + 假崩溃报告污染 §6 诊断）。**

三点结构判断：

1. **状态机本身（Fiber.cs）是全仓防御最密的面**：五态间不存在可从外部打出的回退/跳跃边，D4（Inactive 直达 Dead 防释放未获取资源）与幂等守卫全部按文档行为。攻击未找到任何状态机违例。
2. **防御的薄弱点集中在「调度器的排空循环」而非「状态机」**：TickWatchdog 的自愈分支有 `ReplayInProgress` 检查（R3-RT-04），而 `SynchronousExitDrain` 的 A3-10 补队循环**没有**同型检查——同一条审计纪律（重入排除）只落实了一半。这是本轮 MED 发现的根因，属一行级修复面（补 `!f.ReplayInProgress` + 排空重入门标志），但当前树上确实可被宿主层 misuse 打穿。
3. **R1 的五钉修复经复验全部真实生效**（P08/P13/P14 实证异常方言），且 CrashReports 环形 64/65 边界、long.MinValue 防溢出注释、剪除双点无重复/漏剪等 QED-C2 承诺在本轮独立探针下精确成立。R1 遗留的 4 条 LOW 残项（级联 OnSuspending 去重 / 重入钩子跳过 / 外来 fiber 零诊断 / 剪除空队列早退豁口）本轮未复验，仍以 R1 报告为准归 P5.3 后续队列；其中「剪除早退豁口」与本轮 P13/P16 观测一致（幽灵条目存活至首个非空排空，有界、零语义损失，文档级）。

## 攻击发现清单

| # | 严重度 | 攻击向量 | 实证方式 | 实际行为 | 判定 |
|---|--------|----------|----------|----------|------|
| 1 | **MED** | **逆 Action 重入 `SynchronousExitDrain`（无重入门）**：fiber f 回放期间其逆 Action 调 `rt.SynchronousExitDrain()`（外层为 DrainTeardownBatch 或另一层 SynchronousExitDrain）。A3-10 补队循环只查「State==TearingDown 且不在队列」——排空中队列已清空 ⇒ 正在回放（`ReplayInProgress=true`）的 f 被再入队 | 临时工程实证（P10/P11） | 内层对 f 再回放 → `ReplayAndDead` 按 R3-RT-04 loud 抛 → `ProviderCrashCascade.Handle` **fail-open 把仍在执行逆的 f 提前打 Dead** 并级联通知依赖者 + 写入一条假崩溃报告（`重入…禁止重入回放`）；内层结束时 `IsShuttingDown` **在外层排空中途被复位**（P10 实证 mid-drain=False）；f 剩余逆在「已 Dead」状态下继续执行 | **实现缺陷**（重入门只护回放单点，未护入队点；TickWatchdog 自愈分支有 `ReplayInProgress` 排除而 A3-10 循环没有——同纪律半落实。双释放本身未发生，见发现 #8） |
| 2 | **MED** | **QED-P5.3 新 LoadAll 守卫被同一手段绕过**：同发现 #1 的重入链，逆 Action 在内层 `SynchronousExitDrain` 复位 `IsShuttingDown` 后调 `rt.LoadAll()`；另：mid-drain `Register` 成功注册「幸存者」fiber | 临时工程实证（P11b/P11） | P11b：`LoadAll(mid-exit)=SUCCESS`，事先保持 Inactive 的 fiber h 被激活，**外层退出排空返回后 `h.State=Active`**——A3-10「退出即净」承诺被打破（假绿退出复活），h 的逆此后无自动回放路径（仅剩宿主若接线的看门狗兜底）；P11：mid-exit `Register=SUCCESS`，幸存者逃过退出补队 | **实现缺陷**（新守卫只看 `IsShuttingDown` 布尔，重入复位即失效；根因同 #1——关路径缺排空进行中门。注：触发前提是逆 Action 主动调 runtime 关路径 API，属已声明的重入攻击面，故 MED 而非 HIGH） |
| 3 | LOW | **看门狗 `isTimedOut` 谓词抛异常直接逸出**（null 谓词已被 QED-P5.3 加固为 ArgumentNullException，但「抛异常的谓词」无隔离）：同帧多个 fiber 超时，谓词对第一个抛 | 临时工程实证（P07） | 异常逃出 `TickWatchdog` 至宿主帧循环；同帧其余超时 fiber（b）**未被处理**（仍 Active）；已处理 fiber 状态不受损、下帧可续——部分迭代但无状态腐蚀 | **实现缺陷**（与 `OnSuspending` 钩子 try/catch 隔离纪律不对称；无内存安全问题，#17 方言表未提及宿主委托异常归属） |
| 4 | LOW | **公共 `Graph.NotifyDependents` 突变后门**：宿主绕过调度器直接 `rt.Graph.NotifyDependents(activeProvider)` | 临时工程实证（P09a） | 依赖者 d 被推进 **Suspending 后无人接手**：不入队（`TeardownEnqueued=false`）、`OnSuspending` 钩子不触发（壳 ProcessMode 不禁用）、`ShouldDispatch(d)=false`——僵尸 Suspending，直至看门狗或手动 BeginTeardown 才回收 | **实现缺陷**（低危：突变语义公开但级联职责不随行；R2A-10 以「双账本失步」为由把 `Remove` 收 internal，同险的 NotifyDependents/Register 仍公开——口径不一致） |
| 5 | LOW | **公共 `Graph.Register` 同 id 覆盖 → 双账本失步**：`rt.Graph.Register(ghost)`（同 FiberId 新实例）后正常 BeginTeardown | 临时工程实证（P09b） | 图内账本指向 ghost（`MarkSuspending` 打在 Inactive ghost 上，**永不推进**）；真 fiber 跳过 Suspending 直达 TearingDown；级联最终仍经 runtime 自有账本完成（真 d=Dead、壳钩子以真 fiber 触发）——爆炸半径限于 Suspending 通知语义丢失 + ghost 永滞 | **实现缺陷**（低危；`PluginRuntime.Register` 对同 id loud 抛（R7-L1），`DependencyGraph.Register` 却是静默 `_fibers[id]=f` 覆盖——同名操作两种语义，且正落在 R2A-10 声明的危害类上） |
| 6 | — | **非法转移四连**：BeginTeardown(Dead)；LoadAll 后/死后重复 Register 同名；Suspending 态二次 BeginTeardown；看门狗对 Suspending fiber 连续两帧触发 | 临时工程实证（P01/P02/P03/P05） | ①Dead ⇒ 静默幂等 no-op（文档明示「防二次入队/已终结」），零入队零报告；②重复 Register ⇒ InvalidOperationException（R7-L1），图账本仍指原实例（P02 反射验证）；③Suspending ⇒ 正常推进 TearingDown+入队，Pending 恒 1，排空至 Dead；④两帧触发 Pending 1→1→0（Dead 后），无重复回放 | **防御生效**（全矩阵与 #17 方言表吻合；静默 no-op 而非抛属文档明示的幂等契约） |
| 7 | — | **重入三连（钩子/嵌套排空/级联期装载）**：OnSuspending 钩子内调 SynchronousExitDrain；钩子内 BeginTeardown(provider)；级联期 Register（守卫） | 临时工程实证（P04/P12/P10 对照） | P04：钩子内调关路径 ⇒ fail-open 全量安全收场（p/d 双双 Dead、CrashReports=0、IsShuttingDown 正常复位、外层级联以 TeardownEnqueued 幂等跳过）；P12：钩子内 BeginTeardown(provider) ⇒ 幂等 no-op；级联期 Register/AddDependency ⇒ InvalidOperationException（既有钉） | **防御生效**（这两条钩子重入形态均安全；与发现 #1 的差异恰说明缺口特定于「排空循环中途的重入」而非钩子/级联本身） |
| 8 | — | **崩溃叠加与恰好一次语义**：看门狗触发 + 逆抛异常；重入回放的双释放防线 | 临时工程实证（P06/P10） | P06：看门狗强转+入队后排空抛异常 ⇒ 恰 1 条崩溃报告（fail-open Dead），再次看门狗+排空 Δ=0 不重复；P10 恶劣重入链下 f 的每条逆**恰好执行一次**（内层再回放被 ReplayInProgress 在执行任何逆之前拦截） | **防御生效**（「多重重放=双释放」这一最致命危害在全部攻击形态下未被穿透；发现 #1 的危害是诊断污染与提前 Dead 收口，非双释放） |
| 9 | — | **诊断有界性边界**：CrashReports 恰 64/65 条；AccumulateNet 幽灵 FiberId；long.MinValue 累积；剪除双点连发；threshold=0/负数/MinValue | 临时工程实证（P15/P13/P16/P14） | 65 条时 Length 恒 64、精确丢最旧 1 条、LastCrashReport 恒最新、ResetDiagnostics 交互正常；幽灵 id 永不进入告警（Active 过滤器）且首个非空排空即被剪除；MinValue 累积无 OverflowException（注释声称成立；两次相加回绕为 0 属静默诊断损耗，量级无工程意义）；Drain/ExitDrain 剪除双点连发无重复剪除/漏剪可观测差异；threshold=0 ⇒ 仅非零累积告警、负数/MinValue ⇒ ArgumentOutOfRangeException（QED-P5.3 新钉实证生效） | **防御生效**（#19/QED-C2 承诺逐点精确成立；P16 同时确认空队列早退路径不剪但亦无新死亡——R1 发现 #6 的「早退豁口」实态为有界延迟剪除） |
| 10 | — | **R1 修复复验（QED-P5.3 五钉）**：AttachShell(null)；CheckPermanentFiberLeak(-1)；AccumulateNet(null)/TickWatchdog(null)；双实例共壳 | 临时工程实证（P08/P13/P14）+ 源码确认 | AttachShell(null) ⇒ ArgumentNullException（首次/二次皆 loud，不再静默 no-op）；CheckPermanentFiberLeak 负阈值 ⇒ ArgumentOutOfRangeException；两处 null 委托/字典 ⇒ ArgumentNullException（PluginRuntime.cs L65/L322/L341）；双实例 AttachShell 同一 shell ⇒ 均不抛、两条 drain 各自独立排空互不串扰（benign，R1 #8 同判） | **防御生效**（R1 发现 #2/#7①② 的修复在 HEAD 树上实证落地；5 钉 QedP53RuntimeHardeningPins 与探针观测一致） |

## 值得表扬

1. **「恰好一次」防线的工程强度**：`ReplayInProgress`（R3-RT-04）+ `TeardownEnqueued` 单点成对入队（REG-01）+ 排空快照 Dead-skip 三层叠加，在本轮最恶劣的重入链（排空中途重入关路径触发自愈再入队）下仍保证每条逆恰好执行一次、零双释放——Runtime 最不可承受的危害类型被结构性堵死，且 R3-RT-04 的拦截时机（执行任何逆之前）选得精准。
2. **诊断环形上限的边界精度**：`AddCrash` 的 `RemoveRange(0, len-64)` 在 64/65 恰好边界、last 语义、与 ResetDiagnostics 的正交性上全部精确成立（既有钉只测 66，本轮 P15 补齐 64/65 边界点亦无偏差）——「结构性内存安全不依赖宿主自觉」的承诺经得起逐点攻击。
3. **审计-修复-回归闭环质量**：几乎每个守卫携带审计编号与「先红后绿」回归钉，本轮对 QED-P5.3 五钉的独立复验全部通过；R1→修复→R2 的回路在同日内闭环，且 R1 报告按编号入库可溯——这套流程本身就是对「文档欠明确/实现缺陷」两类的最有效防腐剂。

---
*探针工程（临时目录，用毕已删）：net10.0 控制台 + ProjectReference 仓库源码；探针编号 P01–P16/P11b 与上表一一对应；internal 观测口（`NetAccumEntries`/`PendingTeardownCount`/`_exitDrains`/`_fibers`）经反射访问，未利用 IVT 伪造。*
