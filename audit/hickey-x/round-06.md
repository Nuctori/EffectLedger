# Hickey-X R6: 错误信息与可恢复性 —— 无消息比坏消息更危险

> 视角：错误在最近的修复点暴露；消息是 API 的一半；「静默」是负价值的消息。
> 来源说明：分析由 hickey-auditor 完成（输出捕获失败后从会话记录恢复），载荷行号已由主会话独立复核。

---

## 一、错误消息质量矩阵（全量 throw 评级摘要）

| 层 | 代表消息 | 评级 |
| --- | --- | --- |
| L1 构造校验 | `LoopCount.Of(0)`（DerivedMetrics.cs:18，讲后果）、Interval/SignedNet/EffectAttributes 构造器（引 §、带实参值） | **好** |
| 契约层 ~20 处 FormatException | 仅 :26 带「EFFECT_SCRIPT §4」前缀，其余裸消息（"event 须为对象"、"缺少字段: kind"）；:102/:140/:147 回显实际值但不列合法集；:156 列五键（好） | **中偏差**——共同硬伤：**零位置信息**（无 events[i] 索引、无字段路径） |
| budget/lifetime/loop 数值解析 | `GetUInt64()` 裸调用（Contract:81 ParseTop、:111 区域 ParseLoop、:172 区域 budget），负数/超界抛 .NET 原生异常，无字段名 | **差** |
| Runtime LoadValidation | 全部含 Fiber.Id + 资源 + § 规则引用 | **好**；例外：VerifyNetClosure 不报归属 Fiber |
| Analyzer EAA0901/0801/0802 | 含修复动作与 § 引用 | **好** |
| Analyzer EAA0303/0304 | 0303 建议的 [EffectOverride] 补救代价不成比例（需 CI 人工 approve）；0304 无任何补救指引且 `{1}` 槽位填的是 API 名而非资源身份 | **差-中** |

结构性反差：BCL 的 `JsonException`（语法错）自带行号与字节位置，而自家的 FormatException（语义错）反而没有——框架比项目自己更懂可诊断性。

## 二、新发现（E 系列）

### E1 [HIGH] 静默数据降级是系统性的：可选字段 + 大小写敏感 + 未知键忽略 = 拼写错误静默改写审计结论

- **位置链**：根级未知键忽略（Contract:33 只认 events/budget）+ 可选字段缺省路径（`loop` 缺省 ω=1 :58、`size` 缺省 [1,1] :133）+ TryGetProperty 区分大小写。
- **判词**：用户写 `"szie":[1,2]` → 静默得 [1,1]，峰值少算；`"budget"` 写成 `"Budget"` → 整个预算门消失；`"loop":2` 写成 `"Loop":2` → 并发副本静默变 1。这不是「缺字段报错」（Require 兜住了必需字段），而是**可选字段的拼写错误静默降级为默认值**——审计照跑、Passed=true、结论错。R3-N7 只发现了根级一处，本轮确认这是贯穿所有层级（root/event/claim/scope）的系统性模式。
- **修复**：解析时对每层对象做已知键白名单，未知键抛并列出合法键名（与 ParseResource 五键报错同一标准）。一个递归辅助函数即可全覆盖。

### E2 [HIGH] InverseReplay.cs:34 裸 catch 丢弃原始异常——部分释放失败的根因永久丢失

- **位置**：InverseReplay.cs:34-40（`catch {` 无变量）→ PluginRuntime.cs:145 用 `new InvalidOperationException(...)` 伪造新异常入 CrashReport。
- **判词**：运维看到的 CrashReport 是「位置 2，未释放 1 项：gpu:x」——知道**哪里**失败了，永远不知道**为什么**（inv.Execute() 内部的真实异常对象被丢弃）。对照整任务失败路径 :147 `catch (Exception ex)` 是保留 ex 的。同一个文件里两种诚实度。
- **修复**：PartialReleaseDiagnosis 增加 `FirstException` 字段（或至少 Exception.Message），伪造消息改为 inner exception 保留。

### E3 [MED] `"budget"` 值类型错静默禁用整个预算门

- **位置**：Contract:33 `if (root.TryGetProperty("budget", out var bud) && bud.ValueKind == JsonValueKind.Object)`。
- **判词**：`"budget": []` 或 `"budget": "64"` → 条件为假 → caps 保持 None → gate(2) 整体失效、Audit Passed=true。类型不合法本该抛错，实际等价于没写。与 E1 同族但独立成条：即使修了未知键白名单，这条「键存在但类型不对即静默跳过」仍要单独堵。

### E4 [MED] scope 解析的三重不一致：null 抛、缺失静默空串、数字静默强转

- **位置**：Contract:85-101（ParseScope）：`{"scene":null}` → FormatException；`{"type":"scene"}` 无 scene 键 → `Scene("")` 静默空名；`{"scene":42}` → GetString() 对 Number 返回原始文本，静默得名字 "42"。
- **判词**：同一个「scene 不对劲」，三种输入三种待遇，其中两种是静默接受。空 scene 名会让两个不相干事件在 gate(3) 里同组误判冲突。
- **修复**：scene 必须是非空字符串；type=scene 而 scene 缺失即抛。

### E5 [MED] CompatibleConflict 的 Violation 不含事件索引

- **位置**：EffectScript.cs gate(3)——分组值 `HashSet<int>` 明明持有涉事事件索引 ei，Violation.Detail 只有静态文本（"create×create 冲突（CONFLICT 集，§3.2.3）"）。
- **判词**：数据在手却不上报。千事件剧本里用户要手工找「哪两个 event 在抢 gpu」。对比 PeakExceeded 的 detail（峰值 X > 预算 Y，数值齐全），这是同类输出里的质量洼地。
- **修复**：Detail 追加 `events=[{ei1},{ei2}...]`。

### E6 [LOW-MED] Fiber.Load() 返回值被无视

- **位置**：Fiber.cs:73 `public bool Load()` 返回 false 表示非 Inactive 拒载；PluginRuntime.cs:102 `foreach (var f in all) f.Load();` 丢弃返回值。
- **判词**：二次 LoadAll 时重复装载请求静默消失。防御性布尔返回 + 调用方不看 = 双重保险都不保险。
- **修复**：LoadAll 收集 false 返回并抛聚合异常，或 Load 改 void+内部 throw。

### E7 [MED] 零错误码注册表、零文档映射

- **证据**：Violation.Kind 四种字符串（Leak/NegativeDip/PeakExceeded/CompatibleConflict）只在代码注释出现；EFFECT_SCRIPT.md §4 一个示例顶全部文档，无「错误处理」章节、无消息→场景对照表、无 error code 注册表。EAA id 在消息文本内自引 § 号（好），但没有集中索引。
- **判词**：可恢复性 = 从异常到文档的最短路径。现在这条路要靠 grep 源码铺。
- **修复**：EFFECT_SCRIPT.md 加一节「错误速查表」：消息模式 → 原因 → 修复示例，半天工作量。

## 三、静默路径终审清单（S 系列）

系统性清点结果（含前轮已确认项标注）：

| # | 位置 | 行为 | 状态 |
| --- | --- | --- | --- |
| S1 | Contract 各层未知键 | 静默忽略 → 默认值 | 本轮 E1 升格 HIGH |
| S2 | Contract:33 budget 类型不符 | 静默禁用预算门 | 本轮 E3 |
| S3 | Contract ParseScope | Scene("")/"42" 强转 | 本轮 E4 |
| S4 | EffectScript sweep Lo=⊤ continue | 事件永不存活、永不报告 | 已知族（[⊤,⊤]），本轮确认泛化到任意 Lo=⊤ |
| S5 | InverseReplay.cs:34 裸 catch | 根因异常丢弃 | 本轮 E2 |
| S6 | PluginRuntime OnSuspending 两处 catch{} | Godot 集成缝失败不可观测 | 新发现 LOW |
| S7 | GodotShell exit-drain 外层 catch{} | 排空异常零诊断 | 新发现 LOW-MED |
| S8 | PluginRuntime.cs:102 忽视 Load() 返回值 | 二次装载静默 no-op | 本轮 E6 |
| S9 | Analyzer 白名单未命中 continue | 文档承认的近似盲区（documented silence，可接受） | 记录 |
| S10 | NotifyProviderTeardown/MarkDead 幂等 no-op | 设计内幂等，注释明确 | 可接受 |

**正面记录**：LoadValidation 消息全线含 Fiber.Id + § 引用（好档）；PluginRuntime :55/:58/:159 的前置条件消息带补救方向（好档）；CrashReport 结构含 Provider/dependents/pending（形状对，输在 E2 丢根因）。

## 四、对 R5-C5 的修正裁决

R5 报告的 TickWatchdog 二次入队（PluginRuntime.cs:218-221）经本轮状态机复核**需要修正**：Suspending ⇒ TeardownEnqueued=false 的不变式成立（Unload 与 ForceTeardownOnWatchdog 都同步置 TearingDown；NotifyProviderTeardown 只转 Suspending 不置 enqueued），因此 Active/Suspending 守卫实际上排除了已入队 fiber，`:221` 的 Add 不会重复执行。R5 描述的窗口依赖「Suspending 且 enqueued」同时成立，当前代码不可达。**C5 降级为理论边界（若未来有人单独设置 internal set 则重新暴露），建议加断言而非改逻辑。**此为本系列对抗纪律的自纠实例。

## TOP-3

1. **E1 静默数据降级系统性**：拼写错误的 size/loop/budget 不是报错而是静默改默认值——审计结论在用户不知情时已被改写，这是比崩溃严重一个量级的失败模式。
2. **E2 裸 catch 丢根因**：部分释放诊断只告诉「哪里坏了」不告诉「为什么」，CrashReport 的可信度被自家代码削掉一半。
3. **E7 零错误码映射**：全部质量投入在单条消息文本里，没有一条从消息到文档的路——修复成本最低（半天文档），收益覆盖所有历史与未来消息。

## 文件清单

- 主会话复核行号：InverseReplay.cs（:25-44）、PluginRuntime.cs（:102/:141-149）、Numeric.cs（:91-92）、EffectScriptContract.cs（:30-36/:54-61/:128-135）、Fiber.cs（:73/:87-88/:102-103）
- 子代理通读：EffectScriptContract.cs 全部 throw 点、EffectAlgebraAnalyzer.cs（五诊断全文）、PluginRuntime.cs、LoadValidation.cs、NetBenefitClosure.cs、ProviderCrashCascade.cs、GodotShell.cs、Fiber.cs、DependencyGraph.cs、InverseReplay.cs、Algebra.cs、Numeric.cs、Objects.cs、DerivedMetrics.cs、EffectAttributes.cs、EFFECT_SCRIPT.md（错误码覆盖检查）
