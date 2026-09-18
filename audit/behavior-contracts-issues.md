# Behavior Contracts — 发现与处置台账

日期：2026-09-15
状态：实施中。每条发现附复现、影响、处置与回归证据。

类别：DESIGN(语义决策) / FIXED(修复+钉) / ACCEPTED(已接受边界) / ENV(环境限制) / OPEN(阻塞)

| ID | 日期 | 摘要 | 类别 | 证据 | 处置 | 回归 |
| -- | ---- | ---- | ---- | ---- | ---- | ---- |
| BC-001 | 2026-09-15 | `ProfileResolver` 用元数据反引号形态 ``IConstrained`1`` 比对，实际 `ConstructedFrom.ToDisplayString()` 返回 `IConstrained<TProfile>` ⇒ 声明解析恒为 0，引擎完全空转（假绿） | FIXED | 探针实测 `declarations found: 0`；改为 `<TProfile>` 形态后为 1 | 修正比对常量 | `DeterministicComputationTests` 全部（此前全部假绿通过式失败） |
| BC-002 | 2026-09-15 | `SummaryBuilder.GetSemanticModel` 是早期占位实现（恒返回 null）⇒ 方法摘要恒为空，所有效果/隐藏输入均漏报 | FIXED | 探针 `Stamp summary: hidden=0`；注入 Compilation 后 `hidden=1` | `Build` 经 Compilation 取 model | `DirectClockRead_ReportsHiddenInput` 等 |
| BC-003 | 2026-09-15 | BCL 目录属性键写作 `Type::UtcNow`，但属性引用走 `get_UtcNow` ⇒ 时钟/环境等属性全部落入 `ExternalSummaryMissing`（降级为 Unknown 而非具体违规） | FIXED | 探针显示 `UNK ExternalSummaryMissing: System.DateTime.UtcNow` | 属性键统一改 `get_` 形态 | `DirectClockRead_ReportsHiddenInput` |
| BC-004 | 2026-09-15 | 已知可变集合修改器键用 `::` 分隔而判定用 `.` ⇒ `List.Add` 等永不命中，修改输入漏报（漏为 Unknown） | FIXED | 探针 `UNK ExternalSummaryMissing: List<int>.Add` | 统一为 `.` 分隔 | `MutatingInputParameter_ReportsExternalWrite` |
| BC-005 | 2026-09-15 | 构造期对自身字段赋值被误判为「构造后稳定性违反」⇒ 任何不可变值都无法通过（假红） | FIXED | `ReadOnlyScalarField_Passes` 实测报 EBC1001 | 构造上下文中的 receiver 字段写入视为合法初始化 | `ReadOnlyScalarField_Passes` / `ReadOnlyValue_NoEscape_Passes` |
| BC-006 | 2026-09-15 | `object..ctor`（基类构造）被当作无摘要外部依赖 ⇒ 每个构造类型都附带 EBC9001 噪声 | FIXED | 探针 `UNK ExternalSummaryMissing: object..ctor` | `System.Object` 构造视为已知安全 | `ReadOnlyScalarField_Passes`（断言无诊断） |
| BC-007 | 2026-09-15 | 局部函数/lambda 的**定义**被当作执行 ⇒ 未调用的局部 IO 函数误报（违反「创建≠执行」） | FIXED | `UncalledLocalFunction_DoesNotTrigger` 实测报 EBC2001 | 遍历时跳过 `ILocalFunctionOperation`/`IAnonymousFunctionOperation` 的函数体 | `UncalledLocalFunction_DoesNotTrigger` |
| BC-008 | 2026-09-15 | 构造期 `Sink(this)` 未识别为 this 逃逸（EBC1003 从不触发） | FIXED | `CtorEscapesThis_Reports` 实测无诊断 | 新增 `ArgumentCarriesReceiver` 实参溯源 | `CtorEscapesThis_Reports` |
| BC-009 | 2026-09-15 | `EffectLedger.Contracts.csproj` 多目标 `net8.0;net10.0` 在本机 SDK 还原报 NU1105（无效框架） | FIXED | `dotnet test` NU1105 | 收敛为首版单目标 `net10.0`（多目标回 P6 打包阶段，需独立验证） | 测试工程还原成功 |
| BC-010 | 2026-09-15 | 严格工具重建编译缺少 SDK 隐式框架引用与项目引用输出 ⇒ 恒报「目标工程编译存在错误」，无法验证 | FIXED | 实测 CS0518（System.Object 未定义）、CS0246（EffectLedger 未找到）、CS0009（.Native.dll 无元数据） | 补齐运行时 BCL 引用（跳过 `.Native`）+ dump 前常规构建 + 补项目输出 DLL | 严格工具实跑：合法工程 exit 0 |
| BC-011 | 2026-09-15 | 环境无外网：NuGet 审计源 `api.nuget.org` 不可达 ⇒ `NU1900` 使 4 个**既有**真实构建门测试失败（`GateFixture_Leaky/Paired/ExtendedWhitelist`、`AnalyzerConsumer_Build_EmitsEaa0901`） | ENV | 失败子工程为 `src/EffectLedger.Analyzer`（本次未改动，`git status` 为空）；错误为服务索引加载失败 | 记录为环境限制；本次所有新模块构建/测试统一以 `-p:NuGetAudit=false` 运行 | 592/596 既有测试通过；4 项失败均为 NU1900 |
| BC-012 | 2026-09-15 | `Microsoft.CodeAnalysis.Workspaces.MSBuild` 无法离线取得 ⇒ MSBuildWorkspace 路线不可用 | ENV | `dotnet restore` NU1301（连接被拒） | 改用 MSBuild dump 目标抽取真实编译输入（P5.2 备选路线），已实跑通过 | 严格工具在合法/违规/抑制三工程实跑 |
| BC-013 | 2026-09-15 | 严格工具参数解析把 `check` 子命令当作工程路径 | FIXED | 实测 MSB1009「项目文件不存在：check」 | 解析时跳过前导 `check` 动词 | 工具实跑 exit 0/2 |
| BC-014 | 2026-09-15 | MSBuild dump 目标产物为手写 JSON，Windows 路径反斜杠未转义 ⇒ JSON 解析失败 | FIXED | 实测 `JsonReaderException: 'G' is an invalid escapable character` | 改为 `.meta/.src/.ref` 三文件逐行清单，规避 JSON 转义 | 工具实跑通过 |

## 独立对抗审查（2026-09-15）

审查者：独立子代理（只读），目标为证伪 10 条不可妥协要求。共 12 项发现，逐条**实测复核**如下。

| ID | 审查发现 | 复核结论 | 处置 |
| -- | -------- | -------- | ---- |
| BC-015 | F4：`List<T>.Add` 键永不匹配，修改输入实为 Unknown 而非已知写入 | **证伪**（审查推理错误）：新增判别测试 `MutatingInput_IsKnownWrite_NotUnknown` 实测产生 `Parameter` 类型已知写入 | 加判别测试钉住；键已按 `OriginalDefinition.ToDisplayString()` 实测形态对齐 |
| BC-016 | F1：赋值值侧不分类、返回值从不分类 ⇒ 存入可变别名 / 暴露内部集合不可见 | **成立** | 新增 `ClassifyAssignmentValue`（含 `IsMutableReferenceType` 门控）+ `EvaluateExposure` + `ReturnsViewOverMutableField`；回归 `StoringInputAliasIntoField_IsDetected`/`ExposingInternalMutableCollection_IsDetected`/`ReturnsInternalList_NotFrozen` |
| BC-017 | F2/F10：仅分析 public 成员，覆盖不全且无覆盖计数 | **成立（部分）** | 改为分析全部非隐式成员（跳过属性/事件访问器）；覆盖计数上报仍待补（记入进展） |
| BC-018 | F11：事件订阅/退订未建模，`+=` 静默 | **成立** | 新增 `IEventAssignmentOperation` 分类；回归 `SubscribingEventOnReceiver_IsDetected` |
| BC-019 | F7：缺工程参数恒退出 0；advisory 有违规也退 0 | **成立** | 缺工程 → exit 1；advisory 有违规/未知 → exit 2 |
| BC-020 | F6：dump 目标失败被忽略（可能沿用残留文件） | **成立** | 检查 dump 退出码，非 0 即失败 |
| BC-021 | F3：`StoresCallback`/`ExecutesCallback` 等字段无写入点；`EBC2003` 从不报告 | **成立** | 记入未实现边界；`BehaviorProfiles.cs` 已只声明实际检查的义务；后续实现或移除字段 |
| BC-022 | F5：工具源集合可能与真实构建不一致（生成源等） | **成立（风险）** | 记为已知边界；当前 dump 走真实 MSBuild `@(Compile)`；生成源一致性未验证 |
| BC-023 | F8/F9/F12：BCL 目录过窄、局部函数调用点解析为 null、Fresh 判定基于语法 | **成立（保守方向）** | 记为已知边界；局部函数路径不产生假成功（落 Unknown），非假绿 |

## 独立对抗审查 第二轮（2026-09-15）

审查者：独立子代理（只读，可构建）。目标：证伪并寻找**新**问题（含第一轮修复是否引入回归）。
16 项发现，逐条以可执行回归复核后处置如下（`tests/EffectLedger.Contracts.Tests/AuditRound2Tests.cs` 9 枚，修复前**全部实红**）。

| ID | 发现 | 严重度 | 复核 | 处置 |
| -- | ---- | ------ | ---- | ---- |
| BC-024 | F-01 属性 getter 不传播 ⇒ 经自身/静态属性读时钟静默通过 | HIGH | 成立（EBC2001 缺失） | `ClassifyProperty` 登记 `RequiresPropagation`，`CallGraphAnalyzer.DirectCallees` 并入；2 枚回归 |
| BC-025 | F-02 `ReturnsViewOverMutableField` 忽略返回类型 ⇒ `int Count => _items.Count` 误报 EBC1002 | HIGH（回归，第一轮引入） | 成立 | 仅当返回类型为引用且非 string 才判视图；回归 `ScalarAccessorOverMutableField_IsNotFlagged` |
| BC-026 | F-03 get-only 自动属性在构造器赋值被误判 EBC1001 + 自动访问器报 Unknown | HIGH（回归，第一轮引入） | 成立 | 自动访问器（无体）不再报 Unknown；属性写入在构造上下文合法；回归 `GetOnlyAutoProperty_AssignedInCtor_Passes` |
| BC-027 | F-04 `foreach` 用户可枚举不传播 GetEnumerator/MoveNext/Current | HIGH | 成立（未修复） | 记为已知边界，文档明示 |
| BC-028 | F-05 `using` 用户 IDisposable 不传播 Dispose | HIGH | 成立 | `ClassifyUsing` 解析资源类型（含 VariableDeclarationGroup 下钻与显式接口实现映射）并传播 Dispose；回归 `UsingOverUserDisposable_PropagatesDispose` |
| BC-029 | F-06 生成源对 Tool 不可见 ⇒ 工具报 0 根 exit 0 而 Analyzer 报违规（破坏单引擎/R-GATE-02） | HIGH | 成立 | 生成器引用 + 无 obj/ 生成产物 ⇒ fail-closed（exit 1），`--allow-generators` 显式放行；避免对仅引用分析器的普通工程误伤 |
| BC-030 | F-07 `ReturnsInputAlias` 无产生点 ⇒ 返回调用方可变引用静默通过 | HIGH | 成立 | `ClassifyReturn` 产生 `ReturnsInputAlias`；回归 `ReturningInputAlias_IsDetected` |
| BC-031 | F-08 参数存入静态字段不算逃逸 | MED-HIGH | 成立 | 静态字段存储按值来源判参数/receiver 逃逸；回归 `StoringParameterIntoStaticField_IsDetected` |
| BC-032 | F-09 返回 this 不算逃逸 | MED | 成立 | `ClassifyReturn` 对 receiver 来源一律记逃逸；回归 `ReturningThis_IsDetected` |
| BC-033 | F-12 经局部别名的 receiver 集合修改记为 Unknown 而非 Receiver | MED | 成立 | `ClassifyLocalAlias` 递归解析局部初始化来源（含变量声明组）；回归要求 Receiver 类别 |
| BC-034 | F-10/F-11 测试断言 `NotEmpty` 可被无关违规满足（测试质量） | MED | 成立 | 新增判别测试要求**具体证据类别**；原弱断言保留但不再作为唯一证据 |
| BC-035 | F-13 诊断全部指向类型声明行，丢失违反站点 | MED | 成立 | 诊断消息附站点 `file:line`（实测 `站点 Violations.cs:8`） |
| BC-036 | F-14 advisory 零根退出 0；`--allow-empty` 使 strict 零根通过 | LOW-MED | 成立 | 零根默认一律 exit 2（与模式无关），须显式 `--allow-empty` |
| BC-037 | F-15 报告 Unknown 计数虚高且无原因文本 | LOW | 成立 | 按 (原因,描述,站点) 去重并在报告输出具体原因 |
| BC-038 | F-16 `EBC2003` 在 SupportedDiagnostics 但无产生点 | LOW | 成立 | 记为已知未实现（文档已列"未实现项"） |

结论：第一轮修复确实引入了两处回归（BC-025/BC-026，均为**假红**方向，已修）；
本轮新增 6 处**假绿**通道（BC-024/028/029/030/031/032）已修，F-04 记为明示边界。
全部修复均有先红后绿的回归；合约测试 21 → 30 枚。

## 第三轮（自验）：清理临时产物后暴露的烟测缺陷

| ID | 发现 | 类别 | 证据 | 处置 |
| -- | ---- | ---- | ---- | ---- |
| BC-039 | `tests/ContractsConsumerSmoke/nuget.config` 指向 `artifacts/feed`，而 `run-smoke.sh` 打包到 `artifacts/contracts-feed` ⇒ 在**真正干净**的环境（无残留 feed）下还原必然 NU1301 失败。此前"PASS"是因为手工 pack 留下的 `artifacts/feed` 恰好存在，掩盖了该缺陷 | FIXED | 删除 `artifacts/` 后复跑：`error NU1301: 本地源"…\artifacts\feed"不存在` | 配置路径改为与脚本一致的 `../../artifacts/contracts-feed`；从干净状态复跑 PASS |

## 第四轮：关闭剩余已知假绿边界

| ID | 发现 | 类别 | 证据 | 处置 |
| -- | ---- | ---- | ---- | ---- |
| BC-040 | F-04 `foreach` 用户自定义枚举器不传播 ⇒ 枚举器内隐藏 IO 静默漏报 | FIXED | 先红后绿：`ForeachOverUserEnumerable_PropagatesEnumeratorEffects` / `ForeachOverUserIEnumerable_PropagatesGetEnumerator` 修复前实红 | 新增 `ClassifyForEach`：解析 `GetEnumerator`（模式式 / `IEnumerable<T>` / 显式接口实现）并登记 `MoveNext`/`Current` 传播；BCL 集合与数组不产生噪声 |
| BC-041 | F-03 `StoresCallback`/`ExecutesCallback` 无产生点（声明与实现不符） | FIXED | grep 确认仅被读取/合并，从无写入 | 实现三个区分：创建不计执行；委托调用记 `ExecutesCallback`（不可解析来源报 Unknown）；存入字段/属性记 `StoresCallback` + 逃逸维度；2 枚回归 |

## 第五轮：CI 接线补齐（跨平台路径）

| ID | 发现 | 类别 | 证据 | 处置 |
| -- | ---- | ---- | ---- | ---- |
| BC-042 | `ci.ps1` 缺少合约测试与消费烟测步骤（只改了 `ci.sh`）⇒ PowerShell 门禁不覆盖新模块 | FIXED | 对比 `ci.sh`/`ci.ps1` 步骤差异 | `ci.ps1` 补两步并把退出码纳入 `$gateExit` 聚合（逐项检查，防被后续步骤掩盖） |
| BC-043 | `.github/workflows/ci.yml`（ubuntu-latest）未运行合约消费烟测 ⇒ 跨平台与包链门未覆盖新模块 | FIXED | 工作流仅含旧 `ConsumerSmoke` | 追加 `bash tests/ContractsConsumerSmoke/run-smoke.sh Release` 步骤；测试工程已随 slnx 自动纳入 |
| BC-044 | 包纯度核对：新包不应携带改名前的 `Cosmos.*` 身份（与既有 `PackageIdentityPurityPins` 同类风险） | 已核验 | 解包检查：两个 nupkg 均无 `Cosmos.` 条目；Analyzer 包 `analyzers/dotnet/cs/` 落位正确 | 无需改动；烟测脚本每一步已含结构门 |

## 第六轮：多角度审计（假绿猎手视角）——13 个家族

审查者：独立子代理，专责寻找"零违规零 Unknown 却明显违反角色"的形状（最严重失效模式）。
全部以真实探针工程实测。**根因是单一过滤缺陷**（`ContractEngine` 排除属性访问器），
导致整个公共属性/索引器表面完全不参与分析。

| ID | 家族 | 严重度 | 复核 | 处置 |
| -- | ---- | ------ | ---- | ---- |
| BC-045 | F1 公共属性 getter 不是分析根 ⇒ `public int Now => DateTime.Now.Day;` 假绿 | HIGH | 实测 exit 0 | 分析集合纳入属性/索引器访问器；回归 `F1_*` 2 枚 + 合法对照 |
| BC-046 | F2 索引器同上，且不受 EBC2003 覆盖 | HIGH | 实测 exit 0 | 同上；索引器访问器纳入 EBC2003；回归 `F2_*` 2 枚 |
| BC-047 | F3 用户运算符未传播 | HIGH | 实测 exit 0 | `IBinaryOperation`/`IUnaryOperation` 登记 `RequiresPropagation`；回归 |
| BC-048 | F4 装箱可变对象进 `object` 字段 | HIGH | 实测 exit 0 | 可变性判定递归：`object`⇒可能可变；回归 |
| BC-049 | F5 构造期 `this` 存入静态字段 | HIGH | 实测 exit 0 | receiver 来源的静态存储不受类型门控；回归 |
| BC-050 | F6 值类型承载可变引用（`KeyValuePair<string,List<int>>`/元组/struct） | HIGH | 实测 exit 0 | `MutableType.IsMutableCarrier` 递归检查值类型字段；回归 |
| BC-051 | F7 `Span<T>`/`Memory<T>` 暴露内部数组 | MED-HIGH | 实测 exit 0 | 视为别名承载；回归 |
| BC-052 | F8 `fixed` 指针写入未建模 | MED-HIGH | 实测 exit 0 | 语法级 `fixed` 检测 ⇒ Unknown；回归 |
| BC-053 | F9 指针入口参数被判"稳定值" | MEDIUM | 实测 exit 0 | `TypeKind.Pointer/FunctionPointer` 判为不稳定；回归 |
| BC-054 | F10 公共 setter 破坏不可变 | HIGH | 实测 exit 0 | `EvaluateImmutableSurface` 检出公共 setter；回归 |
| BC-055 | F11 确定性计算修改自身 receiver 状态 | MED-HIGH | 实测 exit 0 | `Receiver`/`Unknown` 写入计入 EBC2002；回归 2 枚 |
| BC-056 | F12 文化敏感插值 `$"{d:C}"` | MEDIUM | 实测 exit 0 | 语法级插值检测 + BCL 目录补 `string.Format`/`ToLower` 等；回归 |
| BC-057 | F13 `ref`/`out` 参数写入、修改传入对象状态 | HIGH | 实测 exit 0 | ref/out 别名与参数对象字段写入记 `Parameter`；回归 3 枚 |
| BC-058 | **单一真源缺失**：可变性判定在分析侧与角色侧各有一份白名单，会漂移 | HIGH（结构） | 两处实现不一致（`object`/`KeyValuePair` 判定相反） | 抽出 `Analysis/MutableType.IsMutableCarrier` 单一真源，两侧委托 |
| BC-059 | 修复过程引入的假红：`List<T>.Count` 落 Unknown、`=> _x` 被误判逃逸、只读视图被误判可变 | HIGH（回归，自引入） | 全量回归实红 | 补 BCL 安全属性、收窄 return-receiver 判定、只读视图按元素递归；回归守住 |

合计：13 个假绿家族全部修复（20 枚判别性回归，修复前**全部实红**）；合约测试 40 → 63 枚。
文档同步诚实化：新增"已支持"清单，并明确"未声明不可变的用户类型按可变处理（请声明契约而非期待猜测）"。

## 第七轮：收敛复审（13 家族清零 + 反向假红清零）

审查者：主代理自验（子代理派发失败，改为独立探针工程实测）。
方法：用**变体探针**（非照抄测试文件）复验 13 家族，再用合法代码探针反向找假红。

| 阶段 | 结果 |
| -- | ---- |
| Task A 13 家族复验（变体） | **13/13 全部被标记**（12 VIOLATED + 1 UNKNOWN[f8 fixed 指针按文档策略]）；此前全部 exit 0 |
| Task B 反向假红 | 初测发现 **3 处**：record 合成 `Deconstruct` out 参数被误判；`List<T>.ToArray`/`int.ToString` 落 Unknown；record 主构造函数报"未覆盖形状" |
| Task C 端到端 | 示例 4 根 OK；消费烟测 PASS；合约测试 62/62 |

处置：

| ID | 发现 | 类别 | 处置 |
| -- | ---- | ---- | ---- |
| BC-060 | A13 `ref`/`out` 在 `DeterministicComputation` 侧未覆盖（仅 ImmutableValue） | 假绿 | 抽出 `EvaluateByRefParameters` 两角色共用；回归后 A13 VIOLATED |
| BC-061 | record 合成 `Deconstruct` 的 `out` 参数被误判为违反（假红） | 回归 | `IsImplicitlyDeclared` 与 `<...>` 合成名排除；`out` 只写不构成对调用方的双向别名 |
| BC-062 | record / 主构造函数报 `UnsupportedOperation` Unknown ⇒ 最常见的不可变类型不可用 | 覆盖率 | `WalkRecordDeclaration` 处理主构造参数默认值与属性初始化器；合法 record 通过、违规 record 精确定位 |
| BC-063 | 编译器合成成员（`Equals`/`<Clone>$`）污染 EBC2003 | 噪声 | 入口稳定性排除 `IsImplicitlyDeclared` 与 `<` 前缀名 |
| BC-064 | 目录键用 C# 别名（`int`）与构造实例（`List<int>`）⇒ 常见安全成员查不到落 Unknown | 可用性 | 引入 `CanonicalTypeName`（元数据全名 + 泛型定义）；`List<T>.ToArray` 等转 OK |
| BC-065 | 工具控制台只打印 `[UNKNOWN]` 不打印原因 | UX | 控制台输出 Unknown 原因（此前仅 JSON 报告有） |

残留（明示边界，非假绿）：`int.ToString` 等少数未登记 BCL 成员仍落 Unknown —— 保守方向、strict 下失败而非静默通过，用户可据原因识别。

### 收敛轮补充（BC-066..BC-068）

| ID | 发现 | 类别 | 处置 |
| -- | ---- | ---- | ---- |
| BC-066 | 目录键与查询键口径不一：查询侧改用元数据全名后（`System.Int32`/`List\`1`），目录仍为 C# 形态（`int`/`List<T>`）⇒ 常见安全成员全部转 Unknown，**合法工程被 strict 拒** | 回归（自引入） | 目录键与 mutator 键统一为元数据形态；`CanonicalTypeName` 单一出口 |
| BC-067 | 前一轮 `CanonicalTypeName` 补丁因锚点不匹配**静默未生效**（构建仍成功） | 过程缺陷 | 本次以断言校验出现次数后再替换；已实测生效 |
| BC-068 | 合法工程因未登记 BCL 成员而 strict 失败 ⇒ 工具对真实项目不可用 | 可用性 | 补 `List<T>.ToArray/Count/Add`、`int.ToString`、`Math.*`、`string.*` 等常用安全成员；合法探针 7/7 通过 |

**收敛判据（双向）**：
- 假绿侧：4 个违规探针（13 家族变体 / 原假绿 5 家族 / EBC2003 / 消费烟测 Leaky）**全部 rc=2**。
- 假红侧：7 个合法探针（防御性拷贝 / 已声明不可变元素集合 / record / 私有缓存 / 只读集合遍历 / 委托 / 原生类型格式化）+ 示例工程 **全部 rc=0**。
- 合约测试 62 → **67 枚**（新增 5 枚"合法必须通过"反向钉，防止后续修复再次引入假红）。

## 第八轮：修复遗留 + 启动新一轮多角度审计

本轮先行修复（审计前自纠）：

| ID | 发现 | 类别 | 处置 |
| -- | ---- | ---- | ---- |
| BC-069 | `$"hello {name}"`（无格式符、string 值）被误报文化敏感 ⇒ 假红 | 回归（自引入，F12 修复的副产品） | 改为在 `IInterpolatedStringOperation` 上逐 hole 判定：显式格式符 ⇒ 敏感；无格式符但类型默认 ToString 受文化影响（decimal/double/DateTime/TimeSpan）⇒ 敏感；纯拼接 ⇒ 放行。关键教训：**AppendFormatted 调用是编译器降级产物，未降级 IOperation 树中不存在**，必须处理 InterpolatedString 本体 |
| BC-070 | `IsKnownMutableCollectionType` 无任何调用者（死代码） | 清理 | 删除 |
| BC-071 | 死代码删除脚本的切片边界错误，把文件头重复拼进中段（构建 30 错误） | 过程缺陷 | 截断修复；教训：对结构化文件禁用盲切片，改用整体重写或带断言的精确替换 |

新增回归：`Interpolation_PlainStringConcat_Passes` / `Interpolation_DefaultDecimalFormat_IsFlagged`（69 枚）。
两个后台审计已派发：视角 A = 新增代码路径假绿（foreach/运算符/record/EBC2003/插值/MutableType 递归）；视角 B = 假红 + 门禁完整性 + 文档漂移。

## 第九/十轮：双审计并行（新增路径假绿 + 假红/门禁/文档）与收敛

审计 A（新路径假绿，51 根探针）：**15 个新假绿家族**，全部 RAN 实证。
审计 B（假红/门禁/文档）：**1 个门禁头条 + 10 假红 + 6 门禁 + 8 文档漂移**，全部 RAN 实证。

### 修复（审计 A 的 15 家族）

| ID | 家族 | 处置 |
| -- | ---- | ---- |
| BC-072 | #1 IsReadOnlySafeView 不递归元素 ⇒ `IEnumerable<Mutable>` 存字段漏检 | 元素经 `IsElementMutable` 递归（并识别已声明 ImmutableValue 的元素） |
| BC-073 | #2/#3 值侧 origin 不解析局部链/分支重赋值 | `ClassifyValueOrigin` 走 `ClassifyLocalAlias` + `FindLocalReassignmentTaint` 全赋值扫描 |
| BC-074 | #4 record 属性初始化器中的用户调用不传播 | `WalkRecordDeclaration` 收集调用入 `RequiresPropagation` |
| BC-075 | #5 显式用户转换静默 | 去掉 IsImplicit 门槛，一律 Unknown |
| BC-076 | #6 复合赋值/自增的用户运算符不传播 | 与 IBinaryOperation 同款登记 |
| BC-077 | #7 公共方法/索引器暴露内部可变状态完全未查 | `EvaluateExposure` 扩展：`ReturnsReceiverMutableField`；`ref` 返回单独判 |
| BC-078 | #8 EBC2003 对 by-ref 返回直接跳过 | 改查被引用元素稳定性 |
| BC-079 | #9 委托存入数组元素/集合漏检 | 目标扩展 + mutator 实参捕获分析 |
| BC-080 | #10 lambda 捕获分析缺失 ⇒ 委托逃逸判定死代码 | `ClassifyDelegateCapture`：参数/this 捕获 ⇒ 逃逸；方法组绑定 receiver 同判；**体提取漏掉 IDelegateCreationOperation 包裹层**（实测定位） |
| BC-081 | #11 插值 hole 为用户类型 ⇒ ToString 不传播 | 非 BCL 类型登记其 ToString |
| BC-082 | #12 静态字段读取被归 Fresh | origin 加 Static 分支；消费端 Static/Unknown ⇒ 别名保留 |
| BC-083 | #13 foreach Current 以属性形态恒解析失败 | 属性 getter 解析 |
| BC-084 | #14 自动属性赋值目标不接受 | 关键发现：get-only 自动属性 SetMethod 为 null 且目标就是 PropertyReference（非 backing field）——放宽为 Instance 检查 |
| BC-085 | #15 foreach 枚举器 Dispose 不传播 | 解析 Dispose（含显式接口实现）；枚举器经接口返回不可闭合 ⇒ 追加 Unknown（不吞 GetEnumerator 自身效应） |

### 修复（审计 B）

| ID | 发现 | 处置 |
| -- | ---- | ---- |
| BC-086 | **门禁头条**：dump 单目标调用跳过 ResolveAssemblyReferences/GenerateGlobalUsings ⇒ 引用为空 + 全局 using 缺失 ⇒ 真实工程全被误判"编译错误" exit 1 | target 加 `DependsOnTargets`；引用为空 loud 拒绝而非静默 bin 扫描 |
| BC-087 | 生成器检测恒 false（死代码）、`--allow-generators` 无效 | 按路径识别 SDK 内置（Sdks 目录 + microsoft.netcore.app.ref 包——ComInterfaceGenerator 实测住后者）；非内置 ⇒ fail-closed |
| BC-088 | CLI：未知 flag/缺值/坏 mode 静默或 exit 127；报告写失败 exit 127；多 TFM 无 -f 混乱 | 全部 loud：usage error exit 1 / 报告失败 exit 1 / 多目标必须 -f |
| BC-089 | advisory 与 strict 退出码相同（mode 摆设） | advisory ⇒ exit 0（可见不阻断），文档写明；strict 维持失败语义 |
| BC-090 | 嵌套两层类型不可见（假绿） | GetAllTypes 递归 |
| BC-091 | FP：foreach 数组 Unknown | 解包转换 + 非泛型 IEnumerable/数组安全集 |
| BC-092 | FP：调用局部 helper 的 receiver 写入被记为根"修改自身状态" | PropagateCallee 仅在**同类型实例方法**间传播 receiver 写入 |
| BC-093 | FP：record with 不可用（clone 写入误报 + 入口拒绝 + get_Y Unknown） | IWithOperation 按 Fresh 跳过；record 为稳定入口；隐式合成成员短路 |
| BC-094 | FP：防御性拷贝视图被拒 | readonly 字段视图放行（非 readonly 仍拒） |
| BC-095 | FP：in 参数被拒 | RefKind.In 豁免 |
| BC-096 | FP：FrozenSet 误报 + ToFrozenSet/Contains Unknown | MutableType 白名单 + 目录（含实测属主 FrozenSet 本身） |
| BC-097 | FP：索引器目录键恒不命中（this[] vs get_Item） | 映射 |
| BC-098 | FP：`var copy = input.ToList(); copy.Sort()` 误报 | 拷贝产生器（ToList/ToArray/拷贝构造）⇒ Fresh |
| BC-099 | FP：基元 GetHashCode/CompareTo Unknown；LINQ 纯子集/string.Join/Guid.ToString/StringBuilder.ToString/Values 集合 Unknown | 目录扩充；OrderBy/Min/Max（走文化敏感默认比较器）**刻意不登记** |
| BC-100 | FP：record 类型在 MutableType 中被当未知可变 ⇒ 暴露误报 | record 按 init-only 容器递归属性 |
| BC-101 | 文档 8 处漂移 | 逐条同步（入口稳定性设计立场、foreach 边界、with/record、退出码、生成器语义、editorconfig 片段补 EBC2003/EBC9001） |

### 收敛证据（双向）

- **假绿侧**：审计 A 51 根探针 48 洞全部标记、仅剩 3 个 OK 对照；ProbeMin 6/6 违规；我的 4 个违规探针 rc=2。
- **假红侧**：审计 B 剩余标记逐一甄别——全部为**设计内**（数组/List 入口 EBC2003、文化格式化、可变 setter、object 入口）；10 个假红家族全部放行。合法探针 rc=0。
- 测试 69/69；示例 4 根 OK；消费烟测 PASS；Runtime 139/139；Release -warnaserror 0 警告。
- 设计决策（明示）：**数组/List 作确定性入口被拒**（迁移 IReadOnlyList/ImmutableArray）——这是入口稳定性的直接推论，文档已写明，非缺陷。

## 第十一轮：用户视角审计（正确性与优雅）——进行中

派发两个用户视角审计（普通 C# 使用者视角 + 代码优雅/架构视角），后台并行。
趁审计期间，主代理自查并修复了三个结构性缺陷：

| ID | 发现 | 类别 | 证据 | 处置 |
| -- | ---- | ---- | ---- | ---- |
| BC-102 | `MutableType` 数组分支写作 `ElementType is null \|\| recurse \|\| true` —— 恒 true，前半段是**死代码**，且对 `int[]` 的语义表达错误 | 死代码 + 表达错误 | 代码阅读；断言 `Ints` 仍应为可变承载（数组是可写容器） | 简化为 `if (type is IArrayTypeSymbol) return true;` 并注明理由——元素可变性不影响该结论 |
| BC-103 | **白名单口径分裂（根因）**：`ToDisplayString()` 对 `ImmutableArray<>` 返回**无命名空间、无参数名**形态（实测 `ImmutableArray<>`），而白名单写 `System.Collections.Immutable.ImmutableArray<T>` ⇒ **永不匹配**；`MutableType` 与 `IsStableEntryValue` 各维护一份 | 正确性（假绿/假红共同根因） | 探针实测：`display='ImmutableArray<>'`、`meta='<global namespace>.ImmutableArray\`1'` | 全量改用 **MetadataName** 作唯一口径（`ImmutableArray\`1`），新增 `MutableType.MetadataNameOf`；去命名空间匹配（实测该类型在 global namespace） |
| BC-104 | 测试夹具 `BuildReferences` 缺 `System.Collections.Immutable` 等引用 ⇒ `ImmutableArray<T>` 解析为 **TypeKind.Error**，测试比较的是错误类型（测出假结论） | 测试基础设施缺陷 | 探针实测 `typeKind=Error` | 补齐 System.Collections / Immutable / Linq / Runtime.Extensions 引用 |

新增结构不变式钉（`EleganceTests`，3 枚）：
- `ImmutableContainers_AreNotMutableCarriers`：不可变容器与元素可变性的判定边界
- `StableEntryAgreesWithMutableType_OnImmutableContainers`：**两条规则必须一致**（漂移即红）
- `MutableArrayEntry_IsRejected_ByDesign`：数组入口被拒是设计立场，钉住防无意放宽/收紧

测试 69 → 72 枚。

## 第十一轮（续）：用户视角双审计处置完毕

两份用户视角审计（普通 C# 使用者 + 代码优雅/架构）共 **约 30 项发现**，逐条处置：

### MAJOR（已修，全部先红后绿）

| ID | 发现 | 处置 |
| -- | ---- | ---- |
| BC-105 | F-1：存储路径不承认用户声明的 `ImmutableValue` 契约 ⇒ 组合两个已声明不可变类型被误拒 | 契约维度下沉到 `MutableType.IsMutableCarrier(..., allowDeclaredContracts)`，**全部调用点自动同口径**；回归 `ComposingDeclaredImmutableValues_Passes` |
| BC-106 | F-3（**两份审计的共同头条**）：`ImmutableArray`/`IReadOnlyList` 等文档推荐的迁移目标**零目录条目** ⇒ "照文档改"从假红掉进 Unknown 红 | 补 IReadOnlyList/Collection/Dictionary、Immutable\*、数组、string/decimal 高频成员约 60 条；回归 `ImmutableArrayMembers_AreKnownSafe` |
| BC-107 | 口径分裂根因：`ToDisplayString()` 对部分泛型返回无命名空间形态，白名单永不匹配 | 统一为 `MutableType.CanonicalKey`（命名空间 + MetadataName），目录键与白名单同一出口 |
| BC-108 | F-16：foreach 对**有源码**的用户枚举器凭空附赠 `ExternalSummaryMissing` | 删除无条件补报分支；回归 `UserEnumeratorWithSource_ProducesNoSpuriousUnknown` |
| BC-109 | FP5：显式 `CultureInfo.InvariantCulture` 仍被报文化敏感（文档明说"显式传入即允许"） | 新增实参敏感判定 `HasExplicitDeterministicCultureArg` |
| BC-110 | FP7：显式 `StringComparer.Ordinal` 仍落 Unknown | 同上 `HasExplicitDeterministicComparerArg` |
| BC-111 | FP8/FP9：`string.IsNullOrWhiteSpace`/`Trim`、`decimal.Round/TryParse` 等骨干成员落 Unknown | 入目录（`decimal.Parse` 按文化敏感登记） |
| BC-112 | 假阴：`IReadOnlyList<T>` 包装调用方 `List<T>` 被判冻结（文档明说"包装不算冻结"） | **按来源判定**：防御性拷贝（`ToArray`/`ToList`/拷贝构造/`ToImmutable*`/`Clone`）或真不可变集合类型才放行；并修正早退守卫（元素不可变曾使来源判定被跳过） |
| BC-113 | 假阴：构造期 `this` 交给**静态集合的 mutator** 未被检出（`StaticBag.Items.Add(this)`） | 与 mutator 分支合并判定；回归 `ThisIntoStaticCollection_IsDetected` |

### MODERATE/MINOR（已修）

| ID | 发现 | 处置 |
| -- | ---- | ---- |
| BC-114 | F-10 测试质量：19 枚断言只看"有任意 EBC" ⇒ 任何规则退化成 Unknown 也能全绿 | `MustFlag` 改为要求**具体诊断 id**；收紧后**当场暴露 4 枚测试此前在靠错误理由通过**（已按实际（同样有效的）诊断修正期望） |
| BC-115 | F-5/F-7 死代码：`HasForbiddenEffects`/`Covers`/`IsUnresolved` 零引用；`AnalysisBudget` 两字段从未读取 | 全部删除 |
| BC-116 | EBC9003 在 `SupportedDiagnostics` 但**无任何产生点**（配置功能未接线） | 描述符与注册一并移除（"声明但不兑现"是本模块明令禁止的形态） |
| BC-117 | 文档 8 处漂移（门禁片段漏 EBC9001/EBC2003 —— 审计实测该门禁对 Unknown **无效**；冻结语义；advisory 语义；BCL 覆盖如实说明） | 逐条同步；**实测验证**新片段使构建真的失败（`error EBC2003`） |
| BC-118 | 示例与回归钉与新冻结规则不一致（持有外部视图未拷贝） | 示例改防御性拷贝（与文档一致）；测试同步 |

### 收敛证据

- 合约测试 **77/77**；Release `-warnaserror` 0 警告；示例 4 根 OK；消费烟测 PASS；Runtime 139/139。
- 双向探针：违规 5 探针 rc=2；合法 3 探针 rc=0（其中 fp 探针经修正为符合文档的防御性拷贝后转绿——**证明修复是按文档语义收敛，而非放宽**）。
- 新增回归钉：`EleganceTests` 6 枚（含"两条规则必须一致"的漂移钉）。

## 第十二轮：完成度审计（对照目标 15 项标准）

对目标逐条核验，发现两处**此前未如实记录**的完成度缺口，并取得一项关键证据：

| ID | 发现 | 类别 | 证据 | 处置 |
| -- | ---- | ---- | ---- | ---- |
| BC-119 | **`ci.sh` 在本机失败（exit 1）** —— 第一步 `dotnet restore` 即被 `NU1900` 挡住（nuget.org 不可达）。此前我以"逐个工程加 `-p:NuGetAudit=false`"绕过，**但从未跑过完整门禁**，也未在进度文件里说明"完整 CI 未通过" | 完成度缺口（诚实性） | 实测 `bash ci.sh` → `生成失败 … 8 个错误`，全部 NU1900；**在基线提交 65fa89e 的独立 worktree 上复现同样失败**（且失败工程含 4 个我从未改动的既有工程）⇒ 环境所致，非本次回归 | 记录为**环境阻塞**；同时构造 `ci.sh` 的离线等价变体（唯一差异 `-p:NuGetAudit=false`）取得真实门禁结论 |
| BC-120 | `EffectLedger.Contracts.csproj` 为多目标声明收敛为单目标 net10.0（早期 NU1105 处置），但**计划文档仍写 net8.0;net10.0** | 文档与实现漂移 | 对读 csproj 与 docs/behavior-contracts-plan.md §5 | 以下一轮同步（或在文档明示"首版单目标"） |
| BC-121 | 性能复测：新增目录条目与策略后仍为亚线性 | 正向证据 | 100 根 585 ms / 1000 根 2911 ms = **4.98x**（10 倍根数） | 记录；未设硬阈值以免制造假绿 |

**并行验证**（本机可用的最强手段）：
- `dafny` 工具链本机可用 ⇒ 离线 CI 变体可包含形式化门（89 定律 0 errors）。
- 80 个包已在本地缓存 ⇒ `-p:NuGetAudit=false` 下 restore 全绿。

## 第十三轮：第三份用户视角审计（"真实采纳者"）处置

第三份审计以"要不要在生产仓库采纳它"为标准，写了约 40 个类型 / 12 个探针工程。
发现 **2 个 BLOCKER + 5 MAJOR + 4 MINOR**，逐条处置：

| ID | 发现 | 严重度 | 处置 |
| -- | ---- | ------ | ---- |
| BC-122 | **BLOCKER-1 继承洞**：角色经继承获得时，基类的可变状态与 mutator **不被并入派生根** ⇒ 把 mutator 上移一层即可逃逸 `ImmutableValue`（实测 `DerivedRepo` 报 OK） | BLOCKER（假绿） | 引擎改为枚举 `类型 + 基类链`（`EnumerateSelfAndBases`）合并成员；实测 `BaseRepo`/`DerivedRepo` 均检出，**合法继承（基类只读）不误报** |
| BC-123 | **BLOCKER-2**：`ImmutableValue` 下**静态可变状态写入**完全静默（`private static int _n; _n++`、`static List.Add`），而同款写在 `DeterministicComputation` 下会被检出 —— 两角色判定不一致 | BLOCKER（假阴） | ImmutableValue 分支新增 `StaticWrite` ⇒ EBC1001；回归 `StaticMutableStateUnderImmutableValue_IsDetected` |
| BC-124 | MAJOR-3：跨工程 helper 调用一律 Unknown（不跟进 ProjectReference），真实分层代码库 strict 必失败 | MAJOR | 记录为**已知边界**并写入文档（需经 `effectledger.contracts.json` 提供摘要——该通道尚未接线，文档必须明说） |
| BC-125 | MAJOR-4：EBC2003 要求每个 DTO 都声明 `ImmutableValue`，采纳成本高且未在快速开始中说明 | MAJOR | 文档显著位置补充该成本与迁移路径 |
| BC-126 | MAJOR-5：报告无法说明"哪些文件/类型**没有**被分析"（无法观测静默跳过） | MAJOR | 记录为**设计缺口**（未在探针中复现到实际漏分析，但缺可观测性） |
| BC-127 | MINOR-6：`d.ToString("O")`（BCL 契约的文化无关往返格式符）被误报文化敏感 | MINOR（假红） | 新增 `HasRoundTripFormatSpecifier`，**限定于接收格式串的成员** |
| BC-128 | MINOR-7：`$"{d.Year:0000}"`（格式作用在 int 上）被误报文化敏感 | MINOR（假红） | 插值判定改为**按表达式类型**：仅文化敏感类型（decimal/double/DateTime 等）才判 |
| BC-129 | MINOR-9：`OrderBy`/`decimal.CompareTo`/`DateOnly.AddDays`/`KeyValuePair.Key` 等惯用成员落 Unknown | MINOR | 目录补齐约 40 条（LINQ 排序族、数值/时间比较与取字段、`Array.Empty`） |
| BC-130 | （自引入回归）`HasRoundTripFormatSpecifier` 早期版本**扫描所有调用的字符串常量** ⇒ `Console.WriteLine("s")` 因实参恰为 `"s"` 被整条放行，导致 **IO 漏报** | 回归（HIGH） | 限定为 `ToString/Parse/TryParse` 族；回归套件当场捕获（`F1_PublicPropertyGetter_WithIO_IsFlagged` 转红） |
| BC-131 | （自引入回归）目录 `Dictionary` 初始化器出现 **8 个重复键** ⇒ 静态构造抛异常会静默废掉整个目录 | 回归 | 逐键去重 |

**教训（记入台账）**：BC-130 说明"按实参放行"的判据必须**同时限定成员族**，否则常见常量会意外命中；
该回归被自身的"合法对照 + 具体诊断 id"测试当场捕获——正是前一轮收紧测试质量的直接收益。

## 第十五轮：独立差距审计（subagent，commit a5764a6）——确认并扩充缺口

独立审计员（只读、跑探针、独立复核我的台账声明）结论与主代理自查**一致**，并**新增 4 类缺口**。
其最关键贡献是**证伪了 progress 文件的"15 项全部满足"**（该结论已更正，见 progress 文件）。

### 新增缺口（主代理先前未发现）

| ID | 发现 | 计划依据 | 证据 | 类别 |
| -- | ---- | -------- | ---- | ---- |
| BC-138 | **多角色声明未报配置诊断**：`class X : IConstrained<ImmutableValue>, IConstrained<DeterministicComputation>` 被判为两个根、双双 OK、exit 0 | 计划 §2.1"每个类型本期只支持一个显式角色；**不同角色组合报配置诊断**" | 审计员实测探针 → exit 0，无 EBC0001 | 计划明文规则未实现（此前标为"待实现"，实为**未实现且未声明**） |
| BC-139 | **受约束 class 未强制 sealed**：非 sealed class 声明角色后判 OK | 计划 §2.2/§B"第一版受约束 class/record class **要求 sealed**" | 审计员实测探针 → `[OK]` | 同上 |
| BC-140 | **三个计划点名的文件不存在**：`docs/adr/behavior-contracts-001-semantics.md`（P0.1）、`docs/behavior-contracts-coverage.md`（P0.2）、`audit/behavior-contracts-compilation-probe.md`（P1.5） | 三个工作包的"落点"原文 | `ls docs/adr` 不存在 | 计划点名的交付物缺失 |
| BC-141 | **新模块无公共 API 快照**（P1.1 步骤 4 明确要求"新模块拥有自己的 API 快照"） | P1.1 | `tests/EffectLedger.Contracts.Tests/` 无 `PublicApiSnapshot.*` | 计划点名的交付物缺失 |
| BC-142 | **契约程序集单目标 net10**，计划要求 `net8.0;net10.0`；且 CI 仅 ubuntu（计划 P6.4 要求 Windows/Linux 矩阵） | P1.1 / P6.4 | csproj 仅 net10.0；`grep windows-latest .github/workflows/ci.yml` = 0 | 范围缩减，BC-120 记录但未修 |
| BC-143 | **P2.1 抽象域 Join 律测试缺失**、**P2.7 无取消令牌**、**P3.6/P4.6 无语义变体示例** | P2.1"单测：Join 的幂等/交换/结合…"；P2.7"取消：抛出符合宿主规范的取消"；P4.6"三份同业务语义样例" | grep `ControlFlowGraph`/`CancellationToken`/`public.*Join` 均 0 | 计划明文要求未实现 |

### 审计员独立复核通过的声明（未发现夸大）

站点定位、pragma/NoWarn 不可绕过（实测 build 0 警告 + tool exit 2）、78 枚测试无虚报、
零根与 advisory 退出码语义、防假绿设计——均符号实际行为。
唯一被证伪的是"15 项全部满足"这一总体结论（已更正）。

### 修正后的交付判定

- **可用性**：单工程场景**可用**（审计员探针：happy path 与惯用业务代码均 exit 0；违规被正确检出）。
- **完整版**：**未达成**。缺口集中在 P1.4/P4.4/P5.5/P5.6/P6.3 五个工作包 + 上述计划点名的交付物。
- **用户侧最大障碍**：`effectledger.contracts.json` 未接线 ⇒ **任何跨工程调用永久 Unknown**、
  strict 必失败且**无补救途径**（审计员用 probe2/probe3 实证：加了配置也无效）。这是 P4.4 范围内的缺口。

## 第十六轮：继续交付（"没做好就继续做"）——缺口收口

| ID | 工作包 | 处置 | 证据 |
| -- | ------ | ---- | ---- |
| BC-132 | P1.4 配置入口 | **已交付**：`effectledger.contracts.json` 严格解析器（`ContractConfigParser`：schema 白名单/未知键/重复键/大小与条目限额/policy 框架）；分析器经 AdditionalFiles 接线（EBC9002 配置诊断）；Tool 从真实构建导出的 `.addl` 清单加载；配置错误一律 loud |
| BC-133 | P4.4 用户摘要 | **已交付**：`UserSummary`（符号 ID/effect/returnsAlias/executesCallback/storesCallback/reason/evidenceRef）；`TryUserSummary` 精确符号匹配 ⇒ trust 级摘要替代 Unknown（effect 映射到具体隐藏输入/IO 类别）；重复符号拒绝；`allowBuiltinOverride` 默认 false |
| BC-134 | P5.5 目标清单 | **已交付**：`--baseline <path>`（裸数组或 `{"expectedRoots":[…]}`）；缺失/新增逐条报告；不一致 exit 2 |
| BC-135 | P5.6 报告完整度 | **已交付**：`CoverageReport`（源文件数/受约束类型数/评估根数/分类计数）、`SourceFingerprint`、`BclCatalog.Version`、每根 `RuleBasis` 与 `TrustDependencies`、`ConfigSources` 指纹 |
| BC-138 | 多角色声明未报诊断 | **已交付**：解析器收集全部角色，同类型多角色 ⇒ EBC0001 冲突（CompilationEnd 标记）；只评估首个（互斥角色双双评估只会产生误导噪声） |
| BC-139 | 受约束 class 未强制 sealed | **已交付**：class 且非 sealed ⇒ EBC0001（值类型天然 sealed 不适用） |

端到端验证：
- 摘要探针（外部程序集 `Vendor.Opaque::Next` + 摘要 hidden-random）⇒ 具体 EBC2001（消息含 trust 与证据引用），替代了原来的永久 Unknown；
- 无配置探针 ⇒ 仍 Unknown（绝不静默当纯）；
- 基线匹配 rc=0 / 删根 rc=2（新增/缺失逐条报告）；
- 多角色 ⇒ EBC0001"2 个互斥角色"；非 sealed ⇒ EBC0001"sealed"；sealed 合法声明零诊断。

测试 82 → **85 枚**（+3：摘要解析/缺 reason/重复符号）。

### 仍未交付（如实在案）

- P6.3 性能实验完整度（六类语料/五轮/内存指标）——已有两点基线，剩余为统计完备性；
- P6.7 发布候选证据包（部分由 progress/台账承载）；
- BC-142 net8 目标与 Windows CI 矩阵；
- 跨工程调用的**自动**摘要生成（现由用户摘要配置人工登记——这是计划 P4.4 的设计形态，非缺陷）。

## 第十六轮（补）：Linux CI 首跑 — 既有性能断言偶红

推送 49af706 后 Linux CI 首跑失败：`EffectScriptEdgeTests.Property_Random300_AuditDeterministicAndTerminates`
——"300 随机脚本审计应 <1s，实际 1217ms"。该断言为**既有 L1 性能墙钟钉**（本模块未触碰 L1 代码），
GitHub 共享 runner 变慢即可能超 1s（仓库先例：R12"伸缩性曲线钉共享 runner 假红收口"）。
**同提交 `gh run rerun --failed` 后转绿** ⇒ 偶红（runner 方差），非确定性回归。
与既往处置一致，建议后续把该墙钟钉改为"Linux runner 阈值放宽或按 runner 环境判据"（既有 L1 维护事项，不属本模块范围）。
