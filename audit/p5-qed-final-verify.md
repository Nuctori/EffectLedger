# QED 终态全维度验证报告

> 审计员：终态独立验证审计员（与前六轮审计无会话上下文共享，仅以仓库现状 + 自审报告为输入）。
> 任务：攻击 `audit/p5-qed-claim-audit.md` 的结论，对 P0–P5 全部声明做机器可复核的独立验证。
> 方法：只读审计 + 独立复现（自建临时反射工程对比快照、独立复跑 `dafny verify`、全量 `dotnet test`、
> 逐条核对文档声明与源码/测试的 file:line 对应）。仓库内未修改任何文件；临时工程已删除。
> 日期：2026-09-11。

## 判定

**QED 路线声明验证：通过（附保留意见）。**

理由：六项验证维度中，冻结完整性、Dafny 定律实质、测试执行、DIAGNOSTICS 一致性四项为**独立复现级 PASS**
（不是复述自审，而是本审计员用独立手段重新得出同样结果）；诚实边界与文档-代码一致性两项为
PASS-WITH-NOTES（存在 6 条新发现，全部为文档级/死代码级，无一是语义正确性或安全性缺口）。
自审报告的核心结论——「模型层形式化验证 + 实现抽样对照 = 工程级 QED 而非数学级 QED」——
经独立攻击后**未被推翻**，且其证据鸿沟矩阵与实际代码状态相符（无虚报）。

## 逐项验证表

| # | 验证维度 | 判定 | 证据 | 保留意见 |
|---|---------|------|------|---------|
| 1 | **冻结完整性**：快照 vs 实际导出 | **PASS** | 本审计员在系统临时目录自建独立反射工程（非复用 QedP1B1 测试代码），按同等机制渲染 L1 与 Runtime 全部导出类型及公共成员，与 `tests/Cosmos.EffectAlgebra.Tests/PublicApiSnapshot.Cosmos.EffectAlgebra.txt`（211 行）及 `tests/Cosmos.EffectAlgebra.Runtime.Tests/PublicApiSnapshot.Cosmos.EffectAlgebra.Runtime.txt`（131 行）**逐字节相等**；net8.0 与 net10.0 两个 TFM 的 DLL 渲染结果**彼此相等且均等于快照**（「全 TFM 同一公共面」B5 声明实测成立）。L1 导出类型恰 43 个、Runtime 恰 20 个（README「43 类型」声明精确）。逃逸检查：L1 非导出非嵌套类型仅 `Cosmos.EffectAlgebra.NodePathOrUnknown`（B4b 声明的 internal 超集，Objects.cs）+ 2 个编译器合成物；**零**「public 嵌套于 internal」隐藏类型；IVT 面 = {Cosmos.EffectAlgebra.Tests, Runtime.Tests, SampleGame}（`src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj:31-33`）+ Runtime→Runtime.Tests（Runtime.csproj:26），与 README #18 声明一致 | ①快照机制不覆盖 Analyzer/Generator/Tool——ROADMAP B1 有显式豁免记录且实测三程序集公共面各仅 1 个 Roslyn 入口类（EffectAlgebraAnalyzer.cs:50 / EffectAlgebraGenerator.cs:22），豁免合理但 README 头部「公共 API 面 43 类型快照钉死」措辞未披露此边界；②IVT 按程序集名匹配、无公钥，伪造友元可行——README #18 已诚实声明「文档性边界而非安全边界」，强命名决策仍挂起（属实） |
| 2 | **Dafny 定律实质** | **PASS** | 独立复跑 `dafny verify`（dafny 4.11 + z3 4.12.1，干净进程）：**89 verified, 0 errors**——与徽章/CI 门声明精确一致。`formal/CosmosEffectAlgebra.dfy` 46 条 lemma + `formal/CosmosSweepLine.dfy` 16 条 lemma = **62 条 lemma 声明，全部带非空 `ensures`**（共 69 行 ensures，含多 ensures lemma）；`assume` 关键字在两文件中**零出现**（脚本剥离注释后断言验证）。证明链自洽：每条 lemma 的证明或为函数定义展开的自动证明（空体），或调用前序 lemma（如 `MergePreservesValid`→`LeTotal`+`LeTransitive`，`FoldZSound`→`AllInRangeBounded`+递归，`SegmentViolationsCovered`→三 gate 覆盖 lemma，`SweepTotalMatchesNetAt`→`SweepAccMatchesNetAt`），依赖闭包终止于 datatype/function 定义，**无循环、无假设、无 `assume *`**。C# 侧对应实体现状核对：`Algebra.cs IsCompatible`（23-37 行）与 Dafny 模型逐行同构 | ROADMAP/README 的「89 条定律」口径实为 Dafny 的**验证义务数**（62 lemma + 32 顶层/成员函数谓词的适定性义务），非 89 条独立定律——自审报告未明确此口径差异，属措辞精度问题非虚报。另 D4 扫换线 22 条验证的是模型而非 C# Audit 实现（自审发现 #1 已如实披露，本审计确认该鸿沟真实存在：`QedP53SweepModelConformanceTests` 为阈值桥非直译） |
| 3 | **测试覆盖** | **PASS-WITH-NOTES** | 独立复跑三门：L1 **554 passed** / Runtime **128 passed** / SampleGame **73 passed** = **755 全绿 0 失败**（`dotnet test -c Release --no-build`，2026-09-11）。覆盖率攻击：独立反射枚举两公共程序集**全部 401 个公共方法/属性/构造器**，对测试+样例源码做引用统计：编译器合成 `Deconstruct` 与基础设施方法（ToString/GetHashCode/Equals）除外后，**零消费者公共成员 = 2 个**（`EffectOverrideAttribute.OverrideMode`/`OverrideSize`——见新发现 #1）；生产内部消费但无直接测试引用 ≈ 5 个（`PluginRuntime.TryGetFiber`→ProviderCrashCascade.cs:22 间接覆盖；`PartialReleaseDiagnosis.FailedIndex/FirstError`→PluginRuntime.cs:238,302 间接覆盖；IHost.DisableDispatch/EnableDispatch/IsInstanceValid 由测试 FakeHost 实现，编译期行使） | 「dotnet test 全绿」声明为真，但「无未测公共方法」若按名字级引用口径不成立——2 个零消费 + 5 个仅间接；且约 33 条 Dafny 义务（D3 SignedNet 后半 + D4 扫换线）无 C# 直译桥（自审发现 #1，本审计维持） |
| 4 | **诚实边界一致性**（20 条逐一） | **PASS-WITH-NOTES** | 逐条核对（详见下表）：20 条中 6 条已解决标记属实（#1 B3 删除 Sequence/Parallel——`src/` 零残留、快照仅 Union/Join；#13 B5——三包 `TargetFrameworks` 实测 `net8.0;net10.0`；#18 B4——NodePathOrUnknown 等实测 internal；#19 C2——`PluginRuntime.cs:48` `MaxCrashReports=64` 实证；#5/#12 改写与现行为一致）；14 条活跃声明的钉名**全部存在**：QedP0A1/A2/A3/A4/A7、QedP2C1a/C1b/C1c/C2、QedP4E1/E2、QedP53* 、ProdAuditR4AuditScaleTests、QedMaintFuzzTests、Round8Hickey2Tests(D08)、Round6Hickey2Tests(S06-001/R3-L1-07)。行为抽检：#6 peakReported 去重集实存（EffectScript.cs:193,305）；#11 Runtime ⊆* 过滤实存（Algebra.cs:57 `if (!c.Scope.IncludedIn(scope)) continue`）；#17 四族方言与 Program.cs/Runtime throw 点吻合（InverseReplay.cs:25,29 InvalidOperationException 族）；#2 `Size ?? Interval.Default` 散布点实存（DerivedMetrics.cs:53-57、EffectScript.cs:211,249） | ①E3 称「14 条活跃」而 P5.4a 称「13 条活跃项逐条【影响】标签」——实测 14 条活跃中 **#17 缺【影响：…】标签**（唯一的例外），且已解决的 #5 反而有标签，两处计数口径互相矛盾（新发现 #4）；②#14 的 R3-CG-07 仅为文档引用无测试钉（E3 措辞「测试钉/文档引用」二选一，勉强达标） |
| 5 | **文档-代码一致性** | **PASS-WITH-NOTES** | **EFFECT_SCRIPT.md §4 vs `EffectScriptContract.Parse`**：逐条比对 8 项契约声明全部吻合——"$schema" 读后丢弃（EffectScriptContract.cs:38-40）；⊤/"inf" 双形等价覆盖 lifetime.hi/loop/budget/size 端点（IsTopAlias:145，ParseTop/ParseLoop/ParseBudget 单一真源）；lifetime lo 拒 ⊤（:116）；size 允许 [⊤,⊤] 拒 [⊤,finite]（:136-137）；budget 键 memory 非负整数/余键非空 id（ParseResourceKey:311-325 + NonEmptyId）；重复键五层全拒（RejectUnknownKeys:68 + budget:292）；控制字符拒（ReqStr:435-437）；十进制字面量 TryGetUInt64（:151,195）。README ④ 的 JSON 示例受 doc-guard 钉守护（Round7Hickey2Tests.cs:34 `Readme_Example_ParsesAndAudits` 实存且绿）。**README ⑤ vs `Program.cs`**：退出码表逐行吻合——0=通过（:64 `result.Passed ? 0 : 2`）、2=存在违例（同上）、1=解析/IO/未知命令（:20,23,30,34-35,62）；`QedP0A4ContractFreezePins` 补钉 exit 0 子进程路径（:43,61）属实 | ①P5.2b 修的「资源 id 前后空白拒绝」（NoPad，EffectScriptContract.cs:442-447 + budget 侧 :337-338）**未写入 EFFECT_SCRIPT.md §4 契约要点、README ④、schema（无空白 pattern）**——HIGH 级方言行为游离在冻结契约文档之外（新发现 #3）；②Program.cs 的 `--help`/无参数返回 0（:17）未在退出码表中列出（良性，语义可辩护） |
| 6 | **DIAGNOSTICS 一致性** | **PASS** | L3 `SupportedDiagnostics` = 恰 6 ID：{EAA0901, EAA0303, EAA0304, EAA0801, EAA0802, EAA0701}（EffectAlgebraAnalyzer.cs:120-122）；L2 生成器全源仅 1 个 DiagnosticDescriptor = EAA0701/ConfigInvalid（EffectAlgebraGenerator.cs:79-80,120）。交集 = L2 集 = {EAA0701}；并集 = L3 集 = 6 ID。README ② severity 行 = 恰 6 行（.editorconfig:26-35 同款六行），与并集**一一对应、无多余无遗漏**。诊断面快照钉实存且断言精确到 6 ID 集合（QedP53HardeningPins.cs:43-55） | ci.yml:33 的计数门用 `head -1` 取第一个「N verified」而 ci.sh 用 awk 求和——当前 Dafny 对多文件输出**单一聚合行**（本审计实测仅 1 行）故两者等价；若 Dafny 未来改为按文件分行，ci.yml 会取到单文件计数（如 67 < 89）而**误红**——失败方向安全（fail-closed），非风险敞口 |

### 诚实边界 20 条逐条判定明细（维度 4 附件）

| # | 声明类型 | 核对结果 |
|---|---------|---------|
| 1 | 已解决(B3) | ✅ src 零 Sequence/Parallel 残留；快照仅 `Union`/`Join` |
| 2 | 活跃·文档锁 | ✅ 散布点实存于 DerivedMetrics.cs:53-57 / EffectScript.cs:211,249；「禁再散布」为策略声明，无机器门（与边界自述一致） |
| 3 | 活跃·等价钉 | ✅ D08-001/002 钉在 Round8Hickey2Tests.cs（grep 实存） |
| 4 | 活跃·YAGNI | ✅ 首贡献者归因与 EffectScript.cs:191-192 注释/实现吻合 |
| 5 | 改写(现行为) | ✅ `QedP0A2ProjectionContractTests` 实存；At 在场/Audit 计数双语义与实现在场语义吻合 |
| 6 | 活跃·冻结 | ✅ peakReported 问题集去重实存（EffectScript.cs:193,305）；NegativeDip/Conflict 逐采样点路径在 Audit 内 |
| 7 | 活跃·冻结 | ✅ Load=Mem create（ApiMapping.cs:156）与 QueueFree=release（:120）哨兵配对属实；「跨家族假配对」为声明的静态盲区 |
| 8 | 活跃 | ✅ EAA0303/0304 意图提示 + EffectOverride 不豁免 EAA0901（Analyzer:287-290） |
| 9 | 活跃 | ✅ L3 以方法声明为分析单元；构造期/属性不在范围（声明与实现一致） |
| 10 | 活跃·冻结 | ✅ Runtime 程序集零锁（grep lock/Monitor/Mutex/Interlocked 无命中）；单线程声明与实现一致 |
| 11 | 活跃·冻结 | ✅ ⊆* 过滤实存：Algebra.cs:57（Runtime 传 fiber.Scope 经 NetTable.Compute 过滤；L1 Audit 不过滤） |
| 12 | 改写(已接线) | ✅ QedP2C1b/C1a/C1c pins 全实存 + GateFixture/ExtendedWhitelist 构建门实存；L3/L2 双侧 EAA0701 实证 |
| 13 | 已解决(B5) | ✅ 三包 TargetFrameworks 实测 `net8.0;net10.0`（bin 下 net9.0 目录为 B5 前陈旧产物，csproj 已无） |
| 14 | 活跃·文档引用 | ⚠️ R3-CG-07 为审计文档引用非测试钉（E3 措辞允许；无机器门，属硬钉盲区） |
| 15 | 活跃·冻结 | ✅ 混 scope Parse 拒绝（Round6Hickey2Tests.cs:18 S06-001 + fuzz 语料：73） |
| 16 | 活跃·冻结 | ✅ `ProdAuditR4AuditScaleTests` 实存（含 spread 形状钉） |
| 17 | 活跃·冻结 | ⚠️ 四族方言表与代码吻合（FormatException/ArgumentException/LoadValidationException/InvalidOperationException throw 点实存；钉 QedP0A4ContractFreezePins:68）——但本条是 14 条活跃中**唯一缺【影响：…】标签**的条目 |
| 18 | 已解决(B4) | ✅ 超集实测 internal；IVT 无公钥的「文档性边界」自述与实际一致 |
| 19 | 已解决(C2) | ✅ MaxCrashReports=64（PluginRuntime.cs:48）+ 排空剪除 + QedP2C2DiagnosticsBoundPins 实存 |
| 20 | 活跃 | ✅ QedP0A7AliasFoldingPins 实存；JSON 契约面拒裸名的声明与 ParseResource 实现一致 |

## 新发现清单

| # | 严重度 | 发现 | 证据 |
|---|--------|------|------|
| 1 | **MED** | `EffectOverrideAttribute.OverrideMode` 与 `OverrideSize` 是**全仓库零消费者的公共死 API**：除声明处外，src/tests/samples 无任何读取（L3 分析器只消费 `Reason`，经 IsValidOverrideReason；无 NamedArguments 反射读取）。两属性却被 B1 快照冻结进 43 类型公共面——未来接线或删除都构成 semver major。且与 §8.3.1 叙事微冲突：文档强调「OverrideKind 根本不提供」防死检查，却提供了同样未接线的 OverrideMode/OverrideSize。属 P2「死特性处置」的漏网（P2 正式清单只覆盖 cosmos.effect.json 与 ResetDiagnostics 两项） | src/Cosmos.EffectAlgebra/EffectAttributes.cs:31,37（唯一出现点）；全仓 grep OverrideMode/OverrideSize 仅此两行；快照行 18-21 |
| 2 | **LOW** | README 末行验证脚注过时：声称 `-warnaserror` 构建为「AnalyzerConsumer 样例 **1 条** EAA0901 故意泄漏警告」，P5.0 加入 HelloEffect 后实测为 **3 条**（HelloEffect ×2 + AnalyzerConsumer ×1）。P5.0 更新了 README ⓪ 但漏更新验证脚注 | README.md:290 vs 本审计构建实测（3 个警告，0 错误）；HelloEffect.csproj:15 `WarningsNotAsErrors>EAA0901`（有意设计） |
| 3 | **LOW** | P5.2b 修复的 HIGH 方言（资源 id 前后空白拒绝，claim 侧 NoPad + budget 键侧 id!=id.Trim()）**未落入任何冻结契约文档**：EFFECT_SCRIPT.md §4 契约要点、README ④、docs/effect-script.schema.json（无对应 pattern 约束）、templates/README 均无记载。行为有钉（QedP53DialectHardeningPins 6 枚）且 fail 方向 loud，无静默风险，但「契约已冻结」的文档集对一条 HIGH 级失败路径保持沉默——按 EFFECT_SCRIPT 自身的「doc 即测试/三方同界」教义，这构成契约文档不完整 | EffectScriptContract.cs:337-338,442-447；EFFECT_SCRIPT.md §4（174-185 行契约要点无空白条款）；schema grep「空白」零命中 |
| 4 | **LOW** | 诚实边界元数据自相矛盾：E3 声明「6 条已解决 + **14 条活跃**」，P5.4a 声明「**13 条活跃项**逐条【影响】标签」。实测：活跃 14 条，【影响：…】标签共 14 枚但其中 1 枚贴在已改写的 #5 上，活跃 #17（异常方言表）反而无标签——两处声明均与事实不完全吻合（#17 缺标签是实际缺陷；「13 条」计数错误） | README.md:270-288 逐条标签比对；#17（:285）无【影响】前缀；ROADMAP E3/P5.4a 两处计数 |
| 5 | **LOW** | 公共接口 `IHost` 的 3 个方法（DisableDispatch/EnableDispatch/IsInstanceValid）与 `PluginRuntime.TryGetFiber`、`PartialReleaseDiagnosis.FailedIndex/FirstError` 在测试套件中**零按名引用**（IHost 三方法仅由测试 FakeHost 提供实现、TryGetFiber/FailedIndex/FirstError 仅被生产代码 PluginRuntime.cs:22,238,302 消费）——行为经间接路径覆盖，但没有任何直接契约钉。若 ProviderCrashCascade/退出路径重构，这些公共成员的语义漂移不会被现有测试直接捕获 | 反射 × 测试源码引用统计（本审计）；PluginRuntime.cs:34；InverseReplay.cs:14-15 |
| 6 | **INFO** | 「89 条定律」口径精度：Dafny 的 89 = 全部验证义务（62 条 lemma + 32 个函数/谓词的适定性义务，部分声明合并验证），非 89 条独立「定律」；README 徽章/正文多处将 89 用作定律计数。数学实质无虚报（89 verified 0 errors 可独立复现），但措辞在「义务」与「定律」间滑动——自审报告虽质疑了多种声明精度，唯独未质疑此口径 | 本审计独立复跑 dafny verify 输出「89 verified, 0 errors」；lemma 声明计数 46+16=62（脚本剥离注释后统计） |

## 最终建议

1. **可以按 PUBLISH-CHECKLIST 进入人类发布决策**：六维度中无一项 FAIL；全部可机器复核的硬声明（快照逐字节、dafny 89/0、755 测试、6 诊断 ID、退出码表）经独立复现全部为真。新发现无一触及语义正确性、守恒判定、冻结机制本身。
2. **发布前建议顺手处置（全部 ≤30 分钟）**：①删除或接线 `OverrideMode`/`OverrideSize`（若删除，此刻动手仍在 1.0.0 首发前，无 semver 代价——这是零成本窗口，错过则永久冻结死代码）；②README:290 脚注「1 条」改「3 条（含 HelloEffect 演示 2 条）」；③EFFECT_SCRIPT §4 补一行空白拒绝条款 + #17 补【影响】标签。
3. **维护模式记一笔**：快照机制覆盖面（Analyzer/Generator/Tool 豁免）在 README 正文的「三层冻结契约」话术旁加一句披露，防止读者高估冻结范围。
4. **重申自审报告结论仍成立**：严格数学意义 QED 仍未达成（模型与实现间是抽样桥）——本审计未发现任何能推翻该诚实定性的反证；相反，自审报告对自身弱点的披露（89 义务中 33 条无直译桥、oracle 同源、并发零定律）经查全部属实，无粉饰。

---

*验证手段留痕：独立反射工程（临时目录，已删除）渲染对比两快照 × 两 TFM；dafny verify 干净进程复跑；
401 公共成员 × 测试/样例源码引用交叉统计；lemma/ensure/assume 计数脚本（剥离注释后断言无 assume）；
20 条边界 × 钉名存在性批量 grep + 行为抽检 8 条。*
