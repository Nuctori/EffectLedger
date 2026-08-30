# Rich Hickey 视角审计 — 2026-08-30（全量重读版）

> 审计员：Rich Hickey 透镜（本轮独立上下文，未复述既往结论，只认磁盘）
> 输入：`src/Cosmos.EffectAlgebra/**`、`src/Cosmos.EffectAlgebra.Analyzer/**`、`src/Cosmos.EffectAlgebra.Generator/**`、`src/Cosmos.EffectAlgebra.Runtime/**`、`EFFECT_SCRIPT.md`、`docs/effect-script.schema.json`、`README.md`
> 方法：Simple≠Easy / Values·Identity·State / Decomplect / Data·Contract·Maybe / Hammock命名 / Composability / Extensibility / Approachability 八透镜逐层推演；每条结论带 `path:line` 可复核。

---

## 0. 一句话总判

**L1 值域是 Simple 的典范；契约面与 Runtime 是 Easy 买来的 Complect。**
能正确表达、能正确审计；但在「怎么不写错」与「写错了怎么知道」两处，仍把本该由类型拒绝的事推给了运行时抛异常或文档警告。

> Hickey 判词：*Simple is objective — it is about lack of interleaving. Easy is relative — it is about proximity to what you already know.* 本仓 L1 做到了前者，L2/L3/契约面屡次为了后者把两件事编在一起（complect），然后用一份抛异常清单来还债。

---

## 1. Simple vs Easy — 哪里为短期的 Easy 牺牲了长期的 Simple

| # | 现象 | 证据 | Hickey 视角 |
| --- | ------ | ------ | ------------- |
| S1 | `Signature` 是 `ImmutableHashSet<Claim>` 集合，却被用来表达并发数量；数量坍缩后补 `Of` 重复抛 | `Objects.cs:156-166` 的 `Of` 抛 `重复 Claim`；`hickey-x3 F4` 探针 `Signature.Of(c,c)` 静默 1 份 | 把**多重集**硬塞进**集合**是最典型的 easy。正确是另给一等公民：`Combination.Loop` 已是正解，但类型仍允许用户走错路，靠运行时抛来纠偏（而非让错误不可表达）。Simple 的做法是让 `Signature` 根本不接受重复语义的输入——要数量就必须过 `Loop`。 |
| S2 | `EAA0901` 永不豁免 vs 诊断消息承诺可豁免，已改文档对齐为「不豁免」 | `EffectAlgebraAnalyzer.cs:136` 永不豁免；`README.md:46` 已改为诚实 | 曾为 easy 承诺一条逃逸通道，最后发现通道焊死。现改文档是 honest，但代价是真实工程里跨方法配对**无合法通过路径**——团队最终只能关分析器（门禁失效）。这是把 Easy 的承诺当 Simple 卖。 |
| S3 | 白名单按 `Canonical(methodName)` 字符串匹配，不看接收者类型 | `EffectAlgebraAnalyzer.cs:293-297,315-321`；`ApiMapping.cs:107 Load / 123 Connect` 易撞名 | 用「拼写相同 ⇒ 语义相同」是典型的 complected easy。用户自己的 `SaveSystem.Load` 被判泄漏，第一次正确使用就撞墙。Simple 要求区分「谁的方法」。 |

**裁决**：S1 已用 loud-fail 止血（Simple 方向正确）；S2/S3 仍是 easy 债——用户为 easy 付的税是「关掉门禁」。

---

## 2. Values / Identity / State — 值、身份、状态是否被拆开

**做得好的（Hickey 会点赞）：**

- `NatStar / ZStar / Interval / SignedInterval / DeviationVal` 全是 `readonly record struct` + 构造期 `lo≤hi` 校验 + `⊤` 闭包算术内嵌（`Numeric.cs:14-91`、`SignedNet.cs:16-58`）。溢出⇒⊤ 保守，不回卷翻转符号（R4-F1 已修）。这是**Values as values**的教科书实现，Hickey 原话会是 *"values are not places"*。
- `Signature / Budget` 以 `ImmutableHashSet/Dictionary` 为底座，`Equals/GetHashCode` 按内容（`Objects.cs:240-263`、`EffectScript.cs:394-414`）。`Budget(None)` 防御拷贝归一（`EffectScript.cs:378-386`）。值语义双轨制画线干净（数学是值，生命是对象）——`hickey-x synthesis` 正面清单 TOP1 属实且仍成立。
- `EffectScript` 从 `init` 改为构造期固定（`EffectScript.cs:52-62`），`AuditResult(Passed≡Violations.IsEmpty)` 守卫（`EffectScript.cs:440-441`）——消灭「同一身份先后看到不同值」的 state 幻觉。

**仍 complected 的：**

- `Fiber` 是**身份 + 可变状态**的 `class`（`Fiber.cs`），`State` 由 `PluginRuntime` 命令式驱动（`PluginRuntime.cs:51-167`）。这本身不是错——Hickey 的 Value/Identity/State 区分**要求**把「随时间变化的生命」显式做成 place。但缝是：`FiberState` 的合法转移靠 `if (State==Active)` 散落守卫，而非类型状态机。`IsShuttingDown` 曾是 `public bool {get;set;}` 公共旋钮（`F7`），现已收为 `RuntimePhase` 枚举+私有 setter（`PluginRuntime.cs:14-18`）——方向正确，但仍是「约定」而非「不可表示非法态」。
- `default(LoopCount) / default(Interval)` 仍可在 C# 构造路径绕过校验（`EffectEvent` 构造期 `!IsValid` 拦截是补丁 `EffectScript.cs:30-34`，但 `Interval` 的 `[⊤,⊤]` 仍可在类型层构造，`Numeric.cs:78-91` 只拒 `lo=⊤ hi有限`）。Hickey 会说：*if it can be constructed, it will be constructed*。`struct default` 是 C# 语言的 complect，Simple 的库要么用 `required` 要么用工厂私有化构造子——现状是靠每个消费点自律。

**裁决**：Values 层已 Simple；Identity/State 层靠纪律维持，类型未锁死。

---

## 3. Decomplect — 哪些本不该在一起的东西被编在了一起

| # | Complect | 证据 | 解法（Hickey 式：拆成两个 Simple 东西） |
| --- | ---------- | ------ | ------------------------------------------ |
| D1 | **时间 vs 数量**：`LoopCount` 既是「循环次数」又是「并发副本数」 | `EFFECT_SCRIPT.md §1.1` 语义注记 OPEN-N2 承认重新解释；`EffectScript.cs:128-137` 的 `Loop` 注释 | 保持现状但**命名即拆解**：类型已拆（`Lifetime` 管时长，`LoopCount` 管并发），只差把 `LoopCount` 更名为 `Concurrency`/`Multiplicity`，让错误选型在读时即被发现（现名诱导用户把「重复 10 次」写成 `Loop=10` 而非 `Lifetime` 循环）。 |
| D2 | **作用域的两个真相**：`Event.Scope` vs `Claim.Scope` 同值重复，靠运行时 `Equals` 强校验 | `EffectScriptContract.cs:170-171` 强校验；`Objects.cs:127 Claim(Scope)` 仍必填 | 最 Simple 的拆法是**单一真相**：`Claim` 不再携带 `Scope`，`ParseClaim` 缺 `scope` 即继承 `eventScope`（现已实现 `ParseClaim:192` 的继承分支，R10-Top1），下一步是把 `Claim` 的 `Scope` 字段直接移除，让 duplication 不可表达。当前是「允许重复 + 运行时拒绝不一致」——半拆。 |
| D3 | **序列化能力 vs 域模型能力**：`ScopeId` 8 构造子 vs 契约 4 分支；`ResourceId` 15 vs 6 | `Objects.cs:21-41` 15 资源 vs `EffectScriptContract.cs:209-302` 6 键；`ScopeId` 8 vs `SerializeScope:274-281` 4 分支 | 这是**最重的 complect**。域模型说「我能表达」，序列化器说「我不认」，用户在 `ToJson` 时才知被骗（`throw FormatException: 不可序列化`）。Simple 的拆法二选一：(a) 收窄域模型到契约可表达子集（删或文档化死分支）；(b) 扩契约到域模型（补 `tree/self/disk/callback` 等）。现两头都留着，类型可构造≠可往返——Hickey 会直接判 *incomplect*。 |
| D4 | **审计三门合一**：`Audit` 一次扫换线同时算 `net/peak/grp` | `EffectScript.cs:138-350` 单方法 200 行，三字典 + `peakReported` 去重 | 三门是三个独立的**值函数**（`NetTable.Compute`/`Peak.Compute`/`Compatible.IsCompatible` 已是独立值函数），却被编进同一个增量循环以换性能。性能是对的（`iter-effect26` O(E·K·logE) 论证成立），但 Hickey 会要求**先有三个 Simple 的独立审计函数，再用一个 Coordinated 的增量版本复用它们**——现增量版与朴素版是两份物理代码（`README 诚实边界 #3` 自认），靠 `D08` 定点钉住等价，属于用测试还 Complect 的债。 |
| D5 | **方法名 vs 语义**：`Canonical` 去 `.`/`_` 小写即判同 API | `GodotApiWhitelist.Canonical` + `Analyzer.Canonical` + `Generator` 内联三处 | 解法是**数据 > 字符串**：白名单应由 `Attribute` 或 `IHost` 宿主类型显式注册，而非字符串规范化。`CosmosEffectConfig.extraMappings`（`cosmos.effect.json`）已朝数据驱动迈了一步，但 Analyzer 仍只认字符串。 |

---

## 4. Data / Contract / Maybe — 数据即契约，Maybe 即诚实

**做得好的：**

- `EffectScriptContract` Fail-fast 白名单三层（根/事件/claim `RejectUnknownKeys`，`EffectScriptContract.cs:26-57,75-79`）、`[⊤,⊤] lifetime` 拒（`71-73`）、`Loop==0/default` 拒（`30-34`）、`resource/scope` 非空校验、`FormatException` 单一方言。AI 拼写错误不再静默禁用预算门（hickey-x P0 E1 已闭环）。
- `Budget` 预算门「没查 vs 查过全绿」用 `CapsChecked/IsPeakChecked` 显式化（`EffectScript.cs:429,449`），`README` 置顶 `Debug.Assert(IsPeakChecked)` 样板。`⊤` 往返对称（`SerializeBudget:308` 写 `"⊤"`，`ParseBudget` 认 `"⊤"/"inf"`）——hickey-x P0 N1 已闭环。
- `Interval` 的 `[1,⊤]` 动态语义、`Deviation` 的 `ε=1` 分母守卫、`AnyTop ⇒ Top` 整体跳过，均是**把 Maybe 显式化为值**而非异常/NaN。

**仍缺的（Hickey 会追问 "what about nil?"）：**

- `Budget.None` 的命名是经典 Maybe 误名——`None` 在 Hickey 语境里是「无值」，此处却是「无上限」（`Unbounded`）。虽已加 `Unbounded` 别名（`EffectScript.cs:391`），但主名仍是 `None`，新用户直觉是「零预算」而非「不检查」。Hickey 会要求**要么叫 `Unbounded` 要么用 `Budget?`（Maybe Budget）**——缺预算与零预算在类型上就应不同。
- `ScopeId.Global` 作为「最大元」被复用于两处语义：(a) 真正的全局作用域；(b) `ResolveNetScope` 回落的「未知归因」哨兵（`EffectScript.cs:168,343` 的 `?? new Global()`）。这是把 Maybe（无归因） complect 成一个具体值（Global）。Hickey 会要求用 `ScopeId?`（无归因 = null）而非伪造一个 Global 归因——AI 回修时 `scope=Global` 会误导它去全局 scope 找泄漏。
- 契约覆盖面 `5/15 → 6/15`（新增 `custom`）仍缺 `tree/self/disk/physics/network/input` 等 Godot 主角资源。EFFECT_SCRIPT 宣称「AI 不写 Godot 代码只产数据」却不让 AI 表达 `tree` 泄漏——数据契约的表达力决定了它能验证什么。Hickey 会说：*data is the contract; if your data can't say it, you can't reason about it.*

---

## 5. Hammock 命名 — 名字是思维的把手

| 原名 | Hickey 式追问 | 建议 |
| ------ | --------------- | ------ |
| `LoopCount` | Loop 什么？循环几次还是并发几份？ | `Concurrency` / `Multiplicity`（EFFECT_SCRIPT 已在注释里澄清，但类型名仍诱导错误选型） |
| `Signature` | 签名是什么的签名？资源占用的签名还是方法的签名？ | `ResourceSet` / `EffectSet` 更直接；但 `Signature` 已全仓统一，改名成本 > 收益，保留 + 文档置顶一句即可 |
| `Union` / `Join` / `Sequence` / `Parallel` | 四名一实，时序词假装有时序 | `Sequence` 已 `[Obsolete]` 正确；`Parallel` 应更名为 `UnionChecked`（`DerivedMetrics.cs:76` 已有 `PARA_CONFLICT` 守卫，名不副实）；`Join` 已补 `UnionWidening` 别名正确 |
| `Net` / `Peak` | Net 什么？Peak 什么？ | `NetOccupancy` / `PeakOccupancy` 更自解释；现 `NetTable`/`Peak` 已在 XML doc 置顶量纲说明，可接受 |
| `Budget.None` | None 是无还是无限？ | 主名改为 `Unbounded`，`None` 保留为 `[Obsolete]` 别名 |
| `IsShuttingDown` | 是状态还是动作？ | 已收为 `RuntimePhase` 枚举正确 |

**Hickey 原话**：*naming is the hardest part — but it is also where you pay or collect interest every time you read the code.* 本仓命名债已收大半，剩余是利息而非本金。

---

## 6. Composability — 能否像搭积木一样组合

**值的组合是 Simple 的：**

- `Signature.Union` 半格并（结合/交换/幂等，`Objects.cs:205-212`）+ `Join` 的 `Merge` 区间 widen（`Objects.cs:215-231`）+ `Combination.Loop` 的 `ω` 缩放（`DerivedMetrics.cs`）三者正交，`PropertyTests` 以 1000 组随机律背书。这是 Hickey 认可的 *composable values*。
- `At(t)` 纯函数（`EffectScript.cs:113-126` 同剧本同 t ⇒ 同签名）+ 端点采样定理（`EFFECT_SCRIPT.md §3`）使 AI 可符号探索、反例 100% 可复现——组合性的地基。

**过程的组合仍是 Easy 的：**

- `Generator` 按方法名生成 `ComputeX(baseSig)` 的「每方法签名」是**过程组合**（把多个方法的效应 Union 起来），但它只认 `[EffectOverride]/[AcceptDeviation]` 标注方法（`EffectAlgebraGenerator.cs:34-45`），而 Analyzer 却扫描**所有方法**的调用点。两者「谁该被组合」的判定分裂——一个看标注，一个看白名单命中。这是 complected 的组合边界。
- `PluginRuntime` 的依赖图 + 拓扑排序 + 帧安全调度（`PluginRuntime.cs:60-150`）是**时空组合**，设计已收敛（`docs/spatial-plugin-shell-design.md` v7），但 `RecomputeTopology()` 非关路径仍是 `no-op` 占位（`PluginRuntime.cs:163-166` 自认 deferred gap）。Hickey 会说：*a function that does nothing but is called as if it does something is the most dangerous kind of easy.*

---

## 7. Extensibility — 扩展时是否要改内核

- **白名单扩展**已从硬编码迈向数据驱动：`GodotApiWhitelist.All`（38 条）+ `CosmosEffectConfig.extraMappings`（`cosmos.effect.json` + `LoadExtraFromJson` 严格门）+ `Canonical` 单一真源 + `ValidateNoCollisions` 碰撞校验。Hickey 会认可「数据扩展而非代码扩展」的方向，但会追问：为何 `cosmos.effect.json` 的 `ParseClaim`（`CosmosEffectConfig.cs:60-90`）与 `EffectScriptContract.ParseClaim`（`EffectScriptContract.cs:183-195`）是两份独立的 resource/scope 解析器？这是 DRY 的 complect——同一契约两种方言，改一处易漏另一处。
- **Analyzer 的扩展点**是字符串白名单，而非类型/接口。Hickey 会更倾向 `IHost`/`EffectSource` 协议扩展（论文 spatial 维度的 `Coeffect` 方向），而非继续膨胀字符串表。现状是 practical 的 easy 选择，可接受但需在文档中诚实标注为「字符串扩展，非类型扩展」。

---

## 8. Approachability — 新人第一小时能否不踩坑

| 路径 | 现状 | Hickey 评语 |
| ------ | ------ | ------------- |
| AI 写 JSON 剧本 | `Parse` fail-fast + `Violation(EventIndex)` 反例 + `EffectScriptContract.Parse→Audit` 最短闭环，`README §4` 可剪贴 JSON 已被 `Round7Hickey2Tests.Readme_Example_ParsesAndAudits` 守护（doc 即测试） | **已 Simple**。这是本仓最 Hickey 的设计：数据先于行为，反例驱动回修，无需跑游戏。 |
| C# 方法内配对 | `AddChild + QueueFree` 同方法内可过 `EAA0901`，但跨方法（`_Ready Connect / _ExitTree Disconnect`）必红且无豁免；`Load/Connect` 裸名撞名误报 | **仍 Easy 陷阱**。第一小时即撞自家 `error` 门禁（hickey-x3 F1/F2/F3 合流）。Hickey 会说：*if your tool punishes correct code, people will turn off the tool, and then you have no tool.* |
| Runtime 集成 | `FiberSpec → Register → AddDependency（同 Scope 校验）→ LoadAll（硬环拒载 + net 闭合）→ BeginTeardown/DrainTeardownBatch` 链路清晰；`Graph/SoftCycles/CrashReports/OnSuspending` 可观测出口齐全 | **已 Approachable**。`FiberState` 幂等守卫 + `TeardownEnqueued` 去重 + 帧安全队列 + `SynchronousExitDrain` 关闭路径，Hickey 会认可「状态机显式、失败可观测」的纪律。 |

---

## 9. 优先级排序的修复清单（按 Hickey 式伤害排序：先修「让人关掉工具」的，再修「让人算错账」的）

| 优先级 | 问题 | 最小修复 | 行号 |
| -------- | ------ | ---------- | ------ |
| **P0-1** | Analyzer 裸方法名撞名误报（用户自有 `Load` 被判泄漏） | `FindWhitelistEntry` 加接收者类型判定：仅当接收者可判定为 Godot 类型（或至少非用户命名空间）才按裸名命中，否则要求 `fullName` 命中 | `EffectAlgebraAnalyzer.cs:293-321` |
| **P0-2** | `EAA0901` 无合法通过路径（跨方法配对正确代码无法豁免） | 二选一：(a) 让合法 `reason` 的 `[EffectOverride]` 豁免 `EAA0901`（与历史文档承诺一致）；(b) 保持永不豁免但提供 `SuppressMessage` + `Budget/NetTable` 权威判据的显式基线豁免文档与模板 | `EffectAlgebraAnalyzer.cs:136` + `README.md:46` |
| **P0-3** | `EAA0303` 对白名单内部 `Write+Occupy` 配对误报（`AddChild` 自带双桶） | 已修：`AnalyzeKindMixAndCompat` 仅跨调用站点聚合（`EffectAlgebraAnalyzer.cs:100-180` 护栏），但 `README` 仍把 `EAA0303=error` 教给新人——应降为 `suggestion` 或在 `§7` 单条 API 内天然跨桶处豁免 | `README.md:20-22` |
| **P1-1** | 契约表达力 6/15，AI 无法表达 `tree` 等主角资源 | `ParseResource/SerializeResource/ResourceKey/budget patternProperties` 补 `tree/self/disk` 等，或在 `EFFECT_SCRIPT.md §4` 显式声明覆盖面与替代建模 | `EffectScriptContract.cs:209-321` + `docs/effect-script.schema.json` |
| **P1-2** | `ScopeId/ResourceId` 可构造不可往返（`ToJson` 才抛） | 二选一：收窄域模型（删死分支）或扩契约（补分支）；并使非法态在构造期即不可表达（而非序列化期才 loud-fail） | `Objects.cs:21-41,90-99` |
| **P1-3** | `RecomputeTopology()` 非关路径 no-op 却名承诺重算 | 抛 `NotImplementedException` 或更名 `RecomputeTopologyPlaceholder` 并在 XML doc 顶层警告（hickey-x3 F8） | `PluginRuntime.cs:163-166` |
| **P2** | `LoopCount` 命名诱导错误选型；`Budget.None` 命名反直觉 | `LoopCount` → `Concurrency` 别名；`Budget.None` 主名改为 `Unbounded` | `DerivedMetrics.cs` / `EffectScript.cs:389-391` |
| **P2** | 双解析器分裂（`CosmosEffectConfig.ParseClaim` vs `EffectScriptContract.ParseClaim`） | 抽 `ClaimJsonCodec` 单一真源 | `CosmosEffectConfig.cs:60-90` vs `EffectScriptContract.cs:183-195` |

---

## 10. 值得保留的优点（Hickey 会要求原样保留）

1. **L1 值类型纪律**：`readonly record struct` + 构造期非法态拒绝 + 溢出⇒⊤ 保守 + 区间/有符号区间双轨闭包，配合 `PropertyTests` 随机律——*make illegal states unrepresentable* 的正确做法。
2. **数据先于行为**：`EffectScript : Time → Signature` 纯数据剧本 + `At(t)` 纯函数 + `AuditResult(Violation)` 反例，是 Hickey 最认可的 *data > function > macro* 分层。AI 可符号探索、100% 可复现，无需跑游戏。
3. **诚实的残差文化**：`Analyzer` 类注释逐条自陈近似边界与漏报方向、`README`「已知语义锐边」主动亮家丑、`Runtime` 每个非常规决策带 reviewer 编号溯源——注释与行为基本对表（F2 是唯一发现的承诺违约处，且已修）。
4. **可证伪的测试结构**：sweep-line vs 暴力参考实现的 Violation 集合相等断言、`HickeyX*FixTests` 每条对应一个实证过的错误行为、全量 `<2s` 全绿——回归防线是真的，不是装饰。

---

## 11. 最终裁决

**档位：B+ — 修完 P0-1~P0-3 后可用；修完 P1 后好用。**

- **今天可用**：L1 纯代数 + 契约 `Parse→Audit→Violation` 回修闭环 + Runtime 权威闭合，已满足「AI 写剧本不跑游戏即可验证」与「运行时 Σnet 权威判定」的核心承诺。AI 路径已 Simple。
- **修完 P0 才可把 L3 门禁设为 error**：否则团队会在第一周就关掉分析器（hickey-x3 的核心判词「让人关掉工具」仍成立）。
- **修完 P1 才可宣称「AI 可描述完整游戏」**：否则 Tree/Disk 等主角资源仍在契约外，AI 的调色板缺色。

> Hickey 会补一句：*Programming is not about typing, it is about thinking. Your L1 thinks clearly. Now make it hard to think unclearly.*

---

## 附：本轮读取文件清单

`src/Cosmos.EffectAlgebra/{Objects,Algebra,Numeric,SignedNet,DerivedMetrics,EffectScript,EffectScriptContract,Deviation,CosmosEffectConfig,EffectAttributes,ApiMapping}.cs` · `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs` · `src/Cosmos.EffectAlgebra.Generator/EffectAlgebraGenerator.cs` · `src/Cosmos.EffectAlgebra.Runtime/{PluginRuntime,Fiber,DependencyGraph,InverseReplay,LoadValidation,NetBenefitClosure}.cs` · `EFFECT_SCRIPT.md` · `docs/effect-script.schema.json` · `docs/spatial-plugin-shell-design.md` · `README.md` · `audit/hickey-x/synthesis.md` · `audit/hickey-x3/round-10.md` · `audit/rich-hickey-round10-synthesis.md`
