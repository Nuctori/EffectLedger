# 性质约束实施分阶段工作分解（P0–P8）

日期：2026-09-15
状态：规划完成，所有实现工作包均未开始。
所属总计划：[behavior-contracts-plan.md](behavior-contracts-plan.md)
需求依据：[需求与保证边界](behavior-contracts-requirements.md)。执行各工作包时同时核对该文档第 8 节的需求追踪表；P0 固定语义与证据类别，P5 落实报告和门禁，P6 检查保证措辞及可信计算基础。前后置条件通用验证和 DSL 路线不因需求记录而自动纳入首版。

本文将总计划第 8 节展开成可以逐项执行、测试和交接的工作包。P0–P6 为首版；P7/P8 仅细化研究与后续实现路径，不提前增加首版义务。文中所有新类型、文件和命令都是目标设计，不代表已经存在。

## A. 执行规则与依赖图

```text
P0 语义/反例规格
 └─ P1 项目/角色识别/测试宿主
     └─ P2 操作摘要/调用闭包/分析引擎
         └─ P3 深层不可变值
             └─ P4 确定性计算
                 └─ P5 严格审核/报告/构建一致性
                     └─ P6 试点/性能/分发/独立评审
                         ├─ P7 所有权与资源责任（另行启动）
                         └─ P8 代数律与证据（另行启动）
```

默认串行。P4 的 BCL 目录设计可在 P3 时准备，但不能以尚未完成的不可变性分类结果验收。P7、P8 相互独立，不表示需要并发运行。

每个工作包遵循：确定预期 → 加最小失败或未知样例 → 实现 → 加合法对照 → 跑针对性回归 → 记录证据。一个工作包超过一次会话可可靠完成的范围时，沿输入/输出边界拆分，不沿随意文件数量拆分。

### A.1 状态文件

实施开始时创建 `audit/behavior-contracts-progress.md`，字段固定为：

```text
当前阶段/工作包：
当前基线提交：
已完成工作包及证据：
本次变更路径：
已执行命令、退出码、日志：
未通过样例/未决语义：
本次新增的未知或信任边界：
下一项精确动作：
```

所有工作包初始为 NotStarted。Working、Blocked、Verified 状态必须附事实；修改文件不等于 Verified。父阶段只有全部必需工作包和退出门通过才完成。文档阶段不声称跑过未来测试。

### A.2 每阶段共同退出门

1. 正例、反例、未知例都有固定预期，不用“失败或未知皆可”掩盖覆盖退化；只有明确标为不支持的形状预期 Unknown。
2. 不支持节点不能被默认分支当成无副作用。
3. 新规则有反例和合法对照；修复有回归。
4. 相同输入报告稳定，包含规则与摘要版本。
5. 改动不改变旧 EAA、L1 JSON、Runtime 和既有 CLI 契约。
6. 未执行的命令与环境限制如实记录。
7. 阶段关键实现完成后做一次独立只读审查，问题回填台账，修复后复核受影响项。无人值守审查逐个派发，派发时提供输出结构与验收清单。

## B. 对总计划的实施细化决策

这些条目收紧此前尚有歧义的部分，后续不能按更宽解释实现。

- **不直接信任 marker**：`T : IConstrained<ImmutableValue>` 不是 T 的实现已经验证。当前 compilation 内要检查实现；开放泛型约束本身不能证明任意 T 不可变，暂 Unknown。外部类型声明也只代表主张，须有可信摘要或实现对应证据。
- **静态类入口**：C# static class 不能实现接口。首版直接声明入口只接受非静态 sealed class、受支持的 struct/record；静态辅助类可被依赖分析。不为了支持直接声明静态类立刻增加第二套 attribute 入口。
- **继承边界**：首版 class/record class 必须 sealed，显式用户基类不支持。object/ValueType/Enum 等运行时基础按目录处理；接口实现本身不等于允许任意虚分派。
- **不可变不等于纯**：ImmutableValue 单独约束实例状态和稳定值访问，不禁止实例方法产生与状态无关的日志。该方法作为确定性计算依赖时仍会因日志违规；字段不可变不能把全体成员自动标成纯。
- **构造期合法赋值与逃逸分离**：允许构造赋值并不允许 this 被提前发布；init、对象初始化器与 record 复制需要各自建模。
- **冻结集合不是单一概念**：结构不变、元素不变、枚举顺序稳定是三个条件。某集合可满足 ImmutableValue，但其枚举不能因此直接满足确定性输出顺序。
- **效果自由与终止性分离**：递归固定点没有发现效应不等于函数一定返回。检查结果附角色保障范围，不输出 Total。
- **严格工具提前验证可行性**：P1 做编译获取技术探针，避免直到 P5 才发现 Roslyn/MSBuild 版本不能共同加载；正式 CLI 和报告仍在 P5 实现。
- **临时阶段不伪装成功**：P1–P4 未完成的角色检查应显式 NotImplemented/Unknown，仅供开发。未全部兑现角色前不得把半成品包装成可验证产品。

## P0 — 语义、观察范围与反例规格

### P0 目标与入口

目标：在实现前，让每种声明到底禁止什么、允许什么、未知什么有唯一答案。
输入：总计划、当前 C# 与 Roslyn 版本、旧项目兼容边界。
输出：ADR、规则表、语言覆盖矩阵和可执行 fixture 的规格索引。

### P0.1 明确角色的观察对象

步骤：
1. 为 ImmutableValue 列出对象字段图、公开值属性、构造/复制路径、外部别名与内部修改。
2. 为 DeterministicComputation 列出入参、receiver 配置、返回值、支持的异常结果、隐藏环境和外部写入。
3. 区分字段值稳定、方法纯、逻辑值相等、引用身份相等、枚举次序稳定。
4. 约定不包含墙钟时间、内存耗尽、恶意反射篡改、跨运行时版本一致性等保证。

落点：`docs/adr/behavior-contracts-001-semantics.md`（实施时创建）。
验收：同一个带不可变字段但会 Console.WriteLine 的类型，ImmutableValue 的实例状态检查与确定性方法检查得出不同且合理的结论。

### P0.2 固定第一版语言边界

建立表格，每行写“支持的形状、Known/Unknown 原因、测试编号”：
- sealed class、record class、struct、readonly struct、record struct；
- partial、嵌套、显式接口实现、别名引用、泛型及约束；
- 主构造函数、静态构造、字段/属性初始化器、init、with；
- getter/setter、indexer、event、自定义转换/运算符；
- lambda/local function、delegate、多播、async/yield；
- ref/out/in、ref return、Span/ref struct、unsafe/dynamic/PInvoke；
- 编译器隐式成员与生成器代码。

首版同步常规托管方法优先；async/yield/ref-like 等无法精确覆盖的边界预先标 Unknown，不凭名字宣布支持。
落点：`docs/behavior-contracts-coverage.md`。
验收：矩阵中的每一种形状要么有实现义务，要么有“不能静默通过”的未知义务。

### P0.3 固定组合与信任语义

步骤：
1. 定义满足、违反、未知三个结论及独立的 evidence/coverage 维度。
2. 定义同一根既有违规又有未知时如何汇总：显示所有原因，主结论为 Violated，未知计数不丢。
3. 确定 marker、源码摘要、BCL 摘要、用户摘要的区别。
4. 规定跨程序集及开放泛型不能仅凭 marker 得出 Satisfied。
5. strict 默认禁止未批准策略例外；允许的信任摘要仍显示来源及约束范围。

验收：把同一类由源码引用换成只有元数据引用，不会无理由保留“源码已验证”证据。

### P0.4 构造最小反例规格库

先写样例及预期，可先是文本 fixture，P1 接测试宿主：
- 正确价格计算；隐藏时钟；helper 隐藏时钟。
- 深层不可变值；readonly List；只读包装；浅复制可变元素。
- 局部列表累加；外部列表修改；回调保存捕获对象。
- 未执行的 IO lambda；执行 IO lambda；未知外部委托。
- 用户声明正确但实现错误；同名伪声明；外部只有声明无证据。
- 纯但不交换的加一/乘二；确定但可能异常的 checked 算术。

每条写输入源、声明位置、预期规则、定位片段、结论和信任前提；反例不得只写“应有一个 warning”。

### P0.5 API 与诊断决策审查

固定最小公共 API 名称、sealed 限制、单角色策略、诊断 ID 与可见性。确认角色 token 无需实例化；若构造函数不公开则在公共快照中钉住。
检查 EBC ID 冲突；明确 unsupported declaration 与正常业务越界的区别。
独立审查重点是“是否存在用户自然以为被保证、实际上被排除的情况”。

### P0 退出门、恢复点

退出产物：ADR、覆盖矩阵、至少上述反例族的完整预期、API 决策表、审查结论。
阻塞条件：开放泛型/外部 marker 是否可信等核心语义仍有两种解释。
恢复点：下次从未定稿的矩阵行或反例编号继续，不重新讨论已经有 ADR 的决策。

## P1 — 项目骨架、声明识别与真实消费入口

### P1 目标与入口

目标：用户声明被正确识别，新模块可独立消费，并具备可靠的编译测试基础。
输入：P0 通过的语义/API 规格。
本阶段不承诺不可变或确定性检查已完成。

### P1.1 建立包与依赖边界

落点：Contracts、Contracts.Analyzer、Contracts.Tests 三个 csproj 和 solution。
步骤：
1. Contracts 只放声明类型，目标 net8.0/net10.0；不引用 Godot、L1、Runtime 或 Roslyn。
2. Analyzer 按现有支持宿主建立自包含包；Roslyn 不作为消费者运行时依赖泄出。
3. Tests 固定与仓库一致的 xUnit/Roslyn 依赖和编译选项。
4. 新模块拥有自己的 API 快照；旧快照保持字节级不变。
5. 包描述明确实验状态；不修改全仓版本以迎合新模块。

验收：Contracts-only 可以编译类型声明，但文档明确“仅声明包不提供检查”；Analyzer 单独安装缺 Contracts 时不崩溃。

### P1.2 语义化 ProfileResolver

实现准确符号匹配和 role resolution：
- 使用 compilation 中目标契约类型符号及 SymbolEqualityComparer；
- 正确解析 using alias、全限定名、partial 多部分；
- 收集不同角色冲突，不把 UnknownProfile 当无声明；
- 嵌套类型不自动继承，角色所在成员范围明确；
- 不执行用户属性构造函数、静态初始化或 Spec getter。

落点：`Profiles/ProfileResolver.cs`、`Profiles/ProfileDefinition.cs`。
测试族：`ProfileResolutionTests`、`ProfileBoundaryTests`。
验收：伪造相同短名不误识别，实际合法接口引用不因别名而漏识别。

### P1.3 建立 Compilation 测试宿主

步骤：
1. 使用固定 reference assemblies/受控引用集，避免从测试进程随意搜 DLL。
2. 可指定 LangVersion、nullable、preprocessor symbols、TFM 引用。
3. 统一断言诊断 ID、span、additional locations、结论与 unknown reasons。
4. 支持多源文件、元数据外部库、生成源、编译错误输入。
5. 所有 fixture 先断言编译状态，防止“本来没编译成功导致零分析”假绿。

落点：`Testing/CompilationFixture.cs`、`Testing/ContractAssertions.cs`（测试工程内部）。
验收：同一规则样例在 syntax 存在但 symbol 无法绑定时返回明确未知或工具错误，不是成功。

### P1.4 配置入口和禁误接线

定义独立文件名 `effectledger.contracts.json`，只先支持 schema/version/policy 框架；摘要详细语义 P4 完成。
通过 AdditionalFiles 读取；重复文件、未知键、非法版本、大小超限报明确配置错误。
MSBuild 属性若用于选择模式，必须经 CompilerVisibleProperty 暴露并有消费测试，不能只在测试内手动塞 option。

验收：配置文件存在但没经 AdditionalFiles 接入的实际消费场景有测试；P5 的目标清单策略负责严格检测，P1 不声称尚未加载的分析器可以自检。

### P1.5 提前验证严格驱动技术可行性

做开发探针，不发布 CLI：
1. 用候选 MSBuildWorkspace/构建捕获方式获得一个 net8 和一个 net10 示例 compilation。
2. 核对 DefineConstants、引用、生成源及编译器版本。
3. 确认目标 SDK 的 Roslyn/MSBuild 依赖可以同进程加载，不按“看起来版本差不多”假设。
4. 记录 MSBuild 注册顺序、工作区失败事件、生成器是否执行。
5. 若工作区无法忠实获得 compilation，研究构建时捕获输入并重建的路线；P5 正式实施前必须有通过的技术路径。

输出：`audit/behavior-contracts-compilation-probe.md`，含命令和实际差异。
限制：项目加载可执行 MSBuild task/生成器，只处理用户授权的本地项目，不能把审核未信任工程宣传为安全读取。

### P1.6 第一个真实消费门

建立独立 fixture：无角色、合法角色、未知角色、同名伪角色。
打包/引用两种方式至少跑通源码引用，验证 Analyzer 实际加载。未完成角色以 Unknown(NotImplemented) 呈现，不能用总是通过的 evaluator 占位。

### P1 退出门、恢复点

证据：新项目 build、声明测试、真实消费者日志、编译获取探针、旧 API 快照对照。
阻塞条件：编译器宿主不加载分析器或严格驱动编译获取路线未验证。
恢复点：保存准确包版本/SDK/引用集和失败 fixture，不用清空整个缓存碰碰运气。

## P2 — 分析引擎、摘要和调用固定点

### P2 目标与入口

目标：形成一个不依赖具体角色的行为分析内核，已知与未知均能跨调用传播。
输入：P1 compilation 宿主、P0 操作矩阵。

### P2.1 定义抽象域及 Join

内部数据建议：

```text
AbstractLocation = Receiver | Parameter(i) | Static(symbol)
                 | Fresh(allocationSite) | Unknown
MethodSummary = Reads + Writes + HiddenInputs + ExternalEffects
              + ReturnAliases + Escapes + CallbackBehavior
              + UnknownReasons + EvidenceDependencies
```

明确集合并、别名合并和 unknown 的偏序；unknown 不能清除已知效果。解释路径不要无限塞入摘要固定点状态，单独维护有限 witness 图。
单测：Join 的幂等/交换/结合、状态单调性、未知保留、有限域/预算边界。
落点：`Analysis/AbstractDomain.cs`、`Analysis/MethodSummary.cs`、`Analysis/SummaryJoin.cs`。

### P2.2 枚举和处理操作节点

建立 dispatch 表，每个本期 OperationKind 有一条 transfer 规则或 Unsupported 原因。
处理常量/参数/局部变量、字段、属性、数组、转换、调用、new、赋值、条件、异常区域、delegate。
包含隐式转换/运算符调用、foreach 的 GetEnumerator/MoveNext/Current/Dispose、using 的 Dispose；不能仅遍历显式 Invocation。
编译器新节点或无法获得 operation 的代码，默认 Unknown，不默认 empty summary。

验收：属性读取隐藏 IO、隐式用户转换及 Dispose 有可追踪调用边。
落点：`Analysis/OperationSummaryBuilder.cs`、`Analysis/OperationCoverage.cs`。

### P2.3 CFG、局部数据流与异常边

步骤：
1. 将 Roslyn CFG 的 flow capture、分支/合流映射到抽象位置。
2. 对循环工作列表求固定点；不会证明的路径条件保守合并。
3. try/catch/finally/throw 保留可能执行的效应；不因正常路径返回而漏掉 finally。
4. 无法安全追踪 ref alias/ref return 等形状明确 Unknown。
5. 入口字段初始化和静态初始化建立依赖；不假设“构造完对象后就无需分析创建过程”。

测试：条件一边 IO、循环中逃逸、finally 日志、隐式初始化调用、条件 ref 别名。

### P2.4 调用图与符号实例化

- 记录 caller/callee、实参到形参位置映射、receiver、返回别名。
- 直接调用按 symbol 解析，扩展方法映射 receiver 参数。
- sealed receiver 的可确定目标可绑定；开放 interface/virtual 不闭世界猜测。
- 外部方法按目录查询，缺失则 Unknown(ExternalSummaryMissing)。
- 泛型 OriginalDefinition 只用于索引；应用摘要时必须替换实参类型和相关泛型条件。

测试：同名重载、泛型不同实例、显式接口实现、别名 namespace、metadata-only 依赖。

### P2.5 递归 SCC 与增量失效

Tarjan 或等价 SCC 分解，按依赖方向计算；递归摘要从底部单调增长，达到固定点或预算。
缓存归属 compilation，禁止全局静态 Dictionary<IMethodSymbol,...>。IDE 新 compilation 不复用旧 Symbol；需要缓存时以内容/选项指纹和精确依赖失效为前提，本期优先简单正确。
测试：自递归、互递归、递归一支 IO、纯递归、重复 compilation 改 helper、改 BCL 摘要配置。

### P2.6 委托与闭包

区分三个事件：创建函数值、执行函数体、函数值逃逸。
局部唯一目标 delegate 调用应用其摘要；传给已知立即调用的高阶函数依据摘要展开；未知接收者可能保存或调用，保守记逃逸和未知。
多播、异步 continuation、迭代器未支持时 Unknown，不能随意展开为普通同步调用。
测试：未调用 lambda 不计函数体 IO、捕获 mutable input 并返回委托、立即回调、存储回调后执行。

### P2.7 有界 witness 与资源预算

设置根数、可达方法数、CFG 步数、固定点迭代数和解释深度的可配置上限；默认值由规模探针决定并记录，不临时加大以掩盖算法问题。
取消：抛出符合宿主规范的取消，不转 Satisfied；独立驱动最终处理为取消/工具未完成。
Unknown(BudgetExceeded) 保留耗尽位置和根；解释路径截断要标“已截断”，不能看似完整。

### P2 退出门、恢复点

证据：所有 transfer 规则的正反测试，抽象域 law tests，递归/取消/失效测试，操作覆盖表和第一份规模数据。
特别门：已支持的转移规则不能接受“Unknown 也算通过测试”；需断言具体效果与位置。
恢复点：记录失败方法图、operation kind、摘要前后值；先修转移/Join，不在角色层添加特例掩盖。

## P3 — 深层不可变值

### P3 目标与入口

目标：在受支持的构造及访问模型下，检查实例可达状态不会被外部或自身偷偷改变。
输入：P2 引擎、摘要和逃逸基础。

### P3.1 类型分类与递归字段图

实现 ImmutableTypeClassifier：标量、string、可空支持值、用户已检查类型、支持的不可变集合、已知可变类型、未知类型。
用户 marker 触发验证，不作为验证结果。相互引用的不可变类型图用 SCC 检查：循环本身不等于可变，也不能因为“正在访问”就直接放行，必须检查 SCC 内所有字段边和写入规则。
泛型集合按元素分类；开放泛型不能只凭接口约束假定其所有实例都通过。
测试：两类型互指、节点指向 mutable payload、ImmutableArray<List<int>>、值类型包引用字段。

### P3.2 构造与初始化阶段

为实例建立 Construction / Published 阶段；处理构造链、字段初始值、主构造参数、对象初始化器和 init。
检查初始化前/期间 this 是否被发布：存静态、注册事件、传给未知方法、捕获进逃逸 delegate。
编译器合成 record copy constructor、with、init setter 需要测试，不只看显式 ctor。
第一版不支持的反序列化/反射写入路径明确不在模型内；正常受支持工厂调用仍需分析。

### P3.3 写入与暴露检查

- 找出构造后 this 的字段、数组元素、可达集合等修改。
- getter 返回内部可变对象，或包装成接口后返回，仍保留位置来源。
- 返回自有不可变子值合法；返回 deep-copy 是否合法取决于复制摘要与元素分类。
- 内部可变缓存先拒绝，不以 private 为由视作不可观察。

测试：private readonly List.Add、属性返回 List.AsReadOnly、IEnumerable 延迟暴露可变内部状态、对象转换后再返回。

### P3.4 拷贝、冻结与顺序属性分离

为少量受支持构造建立具体规则：
- 标量不可变元素的复制集合；
- ImmutableArray<T> 的创建/ToImmutable 与 T 条件；
- builder 不逃逸且冻结返回的受支持路径。

每条分别记录元素深度、容器可变性、是否保留输入别名、是否稳定顺序。FrozenDictionary 等不因“Frozen”名称直接进入白名单。
测试：ToArray 复制 int 与复制可变 Line 的差异、builder 冻结后继续修改、共享可变元素。

### P3.5 成员观察与 diagnostics

实例值 getter 要稳定；读时间/全局状态会违反稳定值观察规则。普通实例方法是否 IO 不由不可变性自动否决，但方法写状态仍违反不可变性。
诊断区分“内部对象能被改”“this 提前逃逸”“getter 的值来源不稳定”，不都归为字段 readonly 错误。
测试：纯计算 getter、时间 getter、只打印日志不修改状态的方法、打印并修改字段的方法。

### P3.6 对抗与用户修复路径

对每个错误提供最小合法改写示例：冻结输入、复制不可变元素、返回不可变视图数据、避免构造期注册。
不自动生成深拷贝 code fix，避免循环/身份语义被悄悄改变。
试点至少包含一个普通业务 snapshot，记录为了通过检查需要的额外规则和改写。

### P3 退出门、恢复点

证据：类型图、构造、别名、复制、访问稳定性五族测试；覆盖矩阵从计划值更新为实际值；独立审查结果。
阻塞条件：readonly/interface 包装等常见伪不可变形状能获得 Satisfied。
恢复点：保存别名链和构造阶段转移，不靠新增“禁 List 名称”替代根因。

## P4 — 确定性计算与依赖摘要

### P4 目标与入口

目标：保持普通同步计算代码可写，隔离隐藏输入和外部可观察修改。
输入：P2 方法摘要、P3 不可变分类。

### P4.1 入口与 receiver 条件

验证 public 入口参数/返回值、接收者配置是已知稳定值；构造 receiver 时的隐藏环境依赖也要被记录，不能把“构造时读取时钟后存 readonly”洗成无条件确定。
明确两种路径：调用方显式传快照后保存合法；类型自己读环境生成配置不合法。
方法间 private mutable 临时状态不能留到下一次调用形成隐式状态；字段级缓存首版不允许。
测试：显式 rules 注入、构造读时间、readonly 持有 mutable service、私有计数器跨调用。

### P4.2 角色效应判定

将摘要中的 HiddenInputs、ExternalWrites、IO、Unknown 映射到诊断；只要违规可达就报告。对保守路径导致的误报给准确说明，不假装已证明路径可达。
允许 fresh local 修改，要求返回过程不泄露可变对象并满足 P3 分类。
测试：foreach 局部求和、局部列表冻结、输入数组赋值、静态字段写、调用 helper 改 receiver。

### P4.3 BCL 最小目录

先从真实 sample 需要的精确 API 建目录，优先：
- 基础数值/decimal 运算、string 常用确定性操作；
- 明确 ordinal 或固定 culture 的受支持比较/格式化；
- 支持的不可变集合构造和局部 List 操作；
- 已知禁止的时钟、环境、Console/IO、共享 Random、Guid 随机生成。

每条摘要测试精确重载、泛型条件、回调调用次数/保存行为、receiver 写入、返回别名、异常和初始化依赖。
不从 PureAttribute 推断完整确定性，不默认 GetHashCode/默认 comparer/无序枚举可跨进程稳定。
测试不同重载不能误共用宽泛摘要。

### P4.4 用户摘要 schema 与匹配

配置字段至少包括 schemaVersion、assembly identity/version 条件、精确 symbol ID、effect/alias/callback 条件、reason、evidenceRef。
JSON 拒重复键/未知字段/冲突摘要，设置尺寸和条目数限额。
建立三步校验：语法合法 → 符号唯一匹配 → 摘要条件适用。任何一步失败都是配置诊断。
用户摘要不能静默覆盖内建禁止项；需要例外时独立策略项和审计记录，strict 默认不允许未批准例外。
摘要是 trust，不是 proof；内容指纹出现在报告和缓存键。

### P4.5 回调、延迟计算和环境依赖

对于高阶 API，只在摘要明确其同步执行与不逃逸时应用回调规则。返回 IEnumerable/Task 等不能仅审查创建时的行为；第一版不支持的 deferred/async 返回 Unknown。
引入 IClock/IRandom 不是自动显式值；即使参数显式，行为依赖仍需摘要和环境条件。
测试：Select 没枚举、Select 后 ToArray、delegate 存储、DateTimeOffset 值入参、IClock 调用、默认 culture 与明确 culture。

### P4.6 可读的修复示例和试点闭环

建立三份同业务语义样例：原始隐藏依赖版、显式输入版、故意错误摘要版。
要求合法修复不引入 Run/Behavior 等业务接口，不要求每个 helper 继续声明角色。
记录每个诊断的真正根因和建议；避免建议“加信任摘要即可”。

### P4 退出门、恢复点

证据：确定性入口、效果、局部修改、BCL、摘要配置、回调六族测试；支持 API 清单；无角色场景零新增约束噪声。
阻塞条件：构造时隐藏输入、delegate 或外部 metadata 声明能将副作用洗成纯。
恢复点：记录角色规则 vs 方法摘要的分界，修复应落在最早错误层。

## P5 — 严格审核、解释报告与构建一致性

### P5 目标与入口

目标：提供不能因 IDE 诊断被抑制而误认为成功的审核入口，同时忠实使用目标项目的真实编译语义。
输入：P1 编译获取探针、P2–P4 引擎和规则。

### P5.1 共享引擎部署

确定 Analyzer 与 Tool 使用同一套引擎源：推荐内部共享源项目/链接源，分别编入各自程序集，避免引擎 DLL 漏放到 analyzers ALC。规则目录和版本也只保留一个源。
测试与工具都调用统一 ContractAnalysisEngine，诊断呈现与退出策略是外围层。
不能工具一套 visitor、分析器另一套 visitor。

### P5.2 获取一致的 Compilation

按 P1 通过的路线实现：
1. 指定 project、configuration、TFM、SDK 选择；多目标逐个验收，不隐式只查第一个。
2. 收集源、生成源、引用、定义符号、nullable、LangVersion、AdditionalFiles。
3. 对 workspace/load/generator/compiler 错误 fail loud。
4. 为输入生成指纹；报告记录 TFM 和配置。
5. 用条件编译改变 API 调用的 fixture 校验 real build 与 tool 相同。

同一项目在 Debug/Release、不同 TFM 下可能不同，报告不能混为一个永久类型保证。
不对任意输入程序集执行反射加载来“验证类型”；只做授权构建/编译获取。

### P5.3 CLI 契约定稿

候选命令：

```text
effectledger-contracts check <project.csproj> --configuration Release --framework net10.0 --mode strict --report contracts-report.json --policy contracts-policy.json
```

实施时固定语法；候选退出码 0=按所选策略通过、2=契约/覆盖策略不通过、1=输入/构建/工具错误。虽然数字可与旧 CLI 相同，仍是独立命令契约，不能改变旧 CLI。
未知参数、缺值、报告写入失败、多 TFM 漏指定等必须有明确行为。advisory 返回策略单独定稿：建议仍按检查结果返回非零，迁移者显式配置哪些结论可接受，不让 mode 默默掩盖失败。
超时/取消不产生成功报告；报告可写入 partial 状态但退出非零。

### P5.4 不依赖可抑制诊断的判定

Tool 从内部检查结果决定结论，不从“Roslyn 最后显示了几条诊断”决定。
测试 #pragma、NoWarn、editorconfig severity=none 及诊断 suppressor；IDE 可以不显示，但 strict 原始规则结果仍失败。
不使用“不可绕过”措辞：用户仍能改工具/策略/CI，目标是防止无意退化。

### P5.5 目标清单、策略与未知聚合

策略文件记录预期项目/TFM/角色根或最低根数、是否允许用户摘要、预算、版本要求。根匹配以稳定类型标识，不只用文件名。
基线删除角色、类型改名、项目漏查必须产生目标差异，不能更新基线自动掩盖。
两种清单模式明确：精确根列表用于保护关键类型；最低数量只防全空，不声称能发现任意角色替换。
为 no-targets 设独立报告状态；strict 要求清单或显式允许空，不能默认“0 项全部通过”。

### P5.6 JSON/SARIF 与解释链

JSON 先作为稳定机器面，SARIF 如需使用直接复用标准位置格式，不把格式扩张变成额外规则引擎。
包含结论、规则依据、unknown reasons、trust dependencies、覆盖计数、配置/源指纹、engine/catalog 版本。
路径相对工程，绝对外部路径脱敏；输出不包含环境值或第三方凭据。
解释链稳定排序、截断标识，主诊断定位根因，additional locations 指向声明和调用路径。
测试同输入排序稳定、部分失败报告不自相矛盾、report 写失败退出错误。

### P5.7 真实故障注入

fixture：拔 analyzer 包、漏 AdditionalFiles、生成器报错、删角色、改 TFM、陈旧摘要、tool/build 条件编译不一致。
Tool 自己加载引擎可查出受约束代码，即使 analyzer 包没安装；打包消费门另验证 IDE/build 侧确实加载，不能混为同一保证。
复用已有 ChildProcessRunner 的有界 stdout/stderr 排空经验，测试进程超时与退出码。

### P5 退出门、恢复点

证据：CLI fixture、build/tool 一致性矩阵、诊断抑制反例、目标清单反例、报告 schema 与快照。
阻塞条件：两条编译路径来源不一致但仍可显示“通过”。
恢复点：保存 compilation 输入差异与工具依赖版本，不能简单吞 workspace failure。

## P6 — 试点、性能、分发与发布候选

### P6 目标与入口

目标：证明功能不仅在字符串编译单测中成立，还能被普通用户正确安装、理解和持续使用。
输入：P5 完整垂直切片。

### P6.1 无 Godot 业务样例

创建 `samples/BehaviorContracts`：
- OrderSnapshot / RulesSnapshot 不可变值；
- PriceCalculator 确定性逻辑；
- 外层读取时钟、数据库和日志，不声明为纯；
- 故意错误变体由单独 fixture 承载，不污染全绿 solution。

用户只加一条角色声明，普通 helper 自动分析；展示诊断链和显式输入的最小修复。
验收：从干净环境按 README 命令可复现正确通过、违规失败和未知报告。

### P6.2 仓库内小规模试点

先挑少量没有复杂依赖的计算函数/值类型，以测试夹具或非公开样例验证；不要为了演示给冻结公共类型添加接口，接口新增也是公共面变化。
记录每个类型接入前后：业务方法签名变更、额外声明数量、摘要数量、真实违规、误报、未知。
如果为了简单运算必须写大量 trust，回到 P4 缩小目录缺口或调整受支持范围，不宣称用户体验良好。

### P6.3 性能实验

固定生成语料：
1. 无角色大工程（验证 opt-in 成本）；
2. 100/1000 根、共享深 helper（验证摘要复用）；
3. 宽调用图和深链；
4. SCC 递归；
5. 大对象字段图及别名合流；
6. 大量未知外部 API（验证诊断输出成本）。

在同一固定机器记录无模块/有模块冷暖编译、至少五轮样本、中位数、极端值、峰值内存、摘要数、重算数。性能目标按总计划执行；测试重跑规则事先固定，不能只挑最快一轮。
超预算结果必须 Unknown，测试性能保护本身不会造成假绿。

### P6.4 真实 NuGet 消费矩阵

Contracts net8/net10 + .NET 10 SDK 编译宿主；Windows/Linux。
在临时隔离 feed 和 NUGET_PACKAGES 内验证：
- 声明包单装；
- 声明+Analyzer；
- Tool 严格审核；
- 与旧 EffectLedger.Analyzer 共存；
- 配置通过包 targets/AdditionalFiles 接线；
- 全新安装后不依赖源码仓库的 obj/bin。

检查包文件清单、依赖闭包、分析器运行时依赖、旧 Cosmos DLL 混入与版本身份。只清理本次创建的临时目录，不删除用户全局缓存。

### P6.5 CI 与文档

增加独立测试/消费/审核步骤，保留旧 ci.sh/ci.ps1/工作流门。
失败日志必须上传或可定位；退出失败不能被 shell 管道吃掉。不同平台脚本保持语义一致。
用户文档包括：安装两部分含义、角色选择、已支持/未知、trust、strict 的范围、常见修复、如何卸载/退出 opt-in。
提供 API/报告/诊断变更规则；任何未来降低检查覆盖的变化都需显式版本及迁移说明。

### P6.6 独立对抗审查与收敛

审查者基于承诺逐条挑战，不仅看代码风格：
- 是否能经 helper、元数据 marker、别名、委托、静态初始化骗过；
- 编译器生成代码、隐式调用是否遗漏；
- 未知/预算/抑制/删目标是否假绿；
- 是否有常用合法代码无法通过却只能 trust；
- 打包宿主是否与宣传一致。

每项发现包含 fixture、预期、实际、优先级、边界分类。支持范围内假绿必须修；范围外必须明示 Unknown 并钉住。修复后复审具体问题，不能只修改 README 改称“不保证”以无限逃避首版承诺。

### P6.7 发布候选裁定

候选证据包：阶段完成表、全回归日志、消费矩阵、性能报告、试点报告、未知清单、信任目录、审查台账。
只在证据完整时称 RC；公共发布需用户执行/授权，不自动 push 或发布 NuGet。
P7/P8 未开始不影响首版验收，但文档不可暗示已支持这些角色。

### P6 退出门、恢复点

证据：上述候选证据包完整，旧回归无意外变化，未处置支持范围内假绿为零。
恢复点：按未通过的具体矩阵单元继续，不重新跑全部已知无关步骤代替定位。

## P7 — 所有权与资源责任（后续阶段）

### P7 目标与入口

目标：研究并实现可检查的受限所有权纪律，避免把现有资源计数扩大宣传成独占/释放证明。
入口：P6 通过且明确启动后续范围。OwnedState 与 ResourceOwner 分开验收，不要求同时发布。

### P7.1 定义责任与状态机

分别定义：
- 对象状态的独占所有者、只读借用、可变借用、共享未知；
- 资源句柄的 Unacquired / Owned / Moved / Released / Unknown；
- acquire/transfer/release 的前置与后置；
- 生命周期、异常、中途失败、回调重入的含义。

逻辑资源身份来自具体分配/句柄关联，不以类型名、资源族或总净计数代表唯一对象。
产物：新 ADR 与手工模型；单独确认禁止/允许重复释放的资源种类。

### P7.2 受限 API 面与角色暴露

只有状态机可检查后才定角色：可能采用 IConstrained<OwnedState>、IConstrained<ResourceOwner>，配合少量明确责任转移 API。
如果普通 C# 签名不能表达借用/转移，允许集中式操作摘要或受控 wrapper，但记录用户需要的额外建模成本；不强求“一条 marker 足以表达全部协议”。
普通外部可变引用保存到 owned 对象应失败或未知，不能推测调用方已经放弃引用。

### P7.3 所有权数据流原型

先支持同步方法、局部对象、显式返回/参数转移、using/finally。分支合流将状态集合合并；只有所有可达退出路径都履行责任才成功。
循环资源聚合、异步释放、容器批量所有权、跨线程、复杂 ref alias 初期 Unknown。
测试：分支只释放一边、释放后再用、转移后再释放、异常前后资源责任变化。

### P7.4 OwnedState 别名纪律

复用 P3 逃逸分析但不混淆目标：OwnedState 允许内部修改，禁止外部存活别名无约束修改。
提供 immutable snapshot 输出；callback 持有 this、静态存储、内部集合返回均需检测。
线程封闭性不等于独占；即使只在一个线程，外部别名仍可破坏封装。
验收：同样内部 List 修改在 OwnedState 合法，在 ImmutableValue 不合法；外露 List 在两者都不能静默通过。

### P7.5 ResourceOwner 与 Runtime 对接

明确桥接记录何种真实获取/释放/转移，Runtime 观察覆盖不完整则不能作全程序权威。
保持旧 EAA 的近似语义不变；新责任分析用独立规则与资源实例 ID。
测试两个同类型资源仅释放一个、把 A 的 release 配给 B、释放回调抛异常、重复 dispose、重入。

### P7.6 验证与分步发布

对有限状态/短程序模板穷举，比较静态结论与小型执行模型。支持范围内静态成功却出现 use-after-release 或未归还责任的样例阻断。
OwnedState 与 ResourceOwner 各有独立退出门、性能测试和用户示例；任一仍未成熟不得通过另一个角色的测试宣布完成。

### P7 退出门、恢复点

阶段状态保持延后，直到受限模型、原型和用户成本均可接受。
恢复点：记录哪一种转移/借用无法表达或推导；必要时缩小支持子集，而不是借助计数凑出守恒。

## P8 — 代数律、性质测试与证明证据（后续阶段）

### P8 目标与入口

目标：让需要自定义合并/重排的用户明确表达定律，并将测试、构造规则和证明证据连接到使用点。
入口：P6 后另行启动；不依赖 P7，涉及效果型操作时可等待 P7 的责任模型。

### P8.1 定律绑定模型

定义 LawBinding：
- 操作精确符号及版本/实现指纹；
- carrier/input domain、前置条件；
- 等价关系；
- 观测投影（返回、状态、日志、失败）；
- 所需定律（结合、交换、幂等、单位元等）；
- evidence kind、验证器版本和结果。

交换性的对象可是一对操作，不能从某类“可交换”推导它与所有其他操作可交换。
等价关系本身也有义务：若只比较集合终态，必须明确忽略顺序；不能把日志差异偷偷漏出观测范围。

### P8.2 已知构造规则目录

先选两个小构造：受支持集合并、在明确全序下取 max。为参数条件建立规则：比较器须满足全序，元素等价须符合约定等。
组合得到性质必须有推导链，不按类型短名直接信任。测试规则前提失效时拒绝证据传播。
本项目 Interval.Merge 的 Dafny 证明可作为方法论参考，但不能给任意用户 Merge 同名方法发证。

### P8.3 自定义性质测试适配

接 FsCheck 或等价引擎时，用户明确 generator、合法域、operation 和 equality。
保存 seed、样本数、丢弃数、shrunk counterexample、实现/配置指纹。全部样本因前置条件被丢弃判 Inconclusive，不通过。
缩小反例过程中维持合法域；并保留 overflow/NaN/边界值等定向样本，不能依赖随机命中。
测试证据标签为 Tested，不能自动升级 Proven。

### P8.4 受限静态证明原型

选择一个精确子集，例如无循环、无 IO 的有限位宽整数表达式；明确 checked/unchecked 与异常语义。可借 SMT 检验两边不等的可满足性。
SAT 给反例；UNSAT 只在编码和前提正确的受限片段内标为证明；timeout/unknown/unsupported 不能通过。
限制执行预算和工具版本；记录 C# 到公式的编码对应和边界测试。不要把 C# decimal/float 当数学实数直接编码。

### P8.5 使用点需求检查

明确执行器要求的是哪种性质：fold 分组需要结合律；重排需要相关交换条件；重试需要完整观测下的幂等及失败模型；多线程还需要执行隔离/原子性。
仅满足计算确定性不能获并行许可；证据与操作实现指纹不匹配立即失效。
初期只诊断合法性，不自动改写执行顺序。

### P8.6 证据失效与报告

代码、依赖、比较器、域、观测范围或验证器版本变化时重新验证。来源无法绑定的证书为 Trusted/Unknown，不显示 Proven。
将 algebra evidence 接到报告独立分区，保留行为约束的原结论，不以“测试过交换律”覆盖隐藏 IO 违规。
测试：改一行运算、替换 equality、放宽域、换 checked 语义、空样本、过期证据。

### P8 退出门、恢复点

退出产物：绑定模型、两个可复用构造、反例测试工具、受限证明片段及使用点示例；每项支持程度独立标注。
阻塞条件：观测范围不清、实现证据无法关联、求解未知当成功。
恢复点：记录具体 LawBinding 和编码/反例，不回到给整个类型添加 bool 标签的捷径。

## C. 追踪矩阵与交接模板

### C.1 用户承诺 → 负责阶段

| 用户可见承诺 | 主工作包 | 验证方式 |
| --- | --- | --- |
| 一个类型声明、不改方法接口 | P1.2、P4.6、P6.1 | 普通业务 sample + 声明识别 |
| helper 内隐藏行为也能追踪 | P2.4–P2.6 | 跨调用、回调、递归 fixture |
| 构造/属性/隐式调用不漏掉 | P2.2–P2.3、P3.2 | 覆盖矩阵逐格测试 |
| readonly 不被误当深层不可变 | P3.1–P3.4 | 别名/复制正反例 |
| 局部修改不造成业务代码扭曲 | P2.3、P4.2 | fresh 集合合法样例 |
| 不确定的部分明确未知 | P2.7、P4.4、P5.5 | unknown-only strict 失败 |
| IDE 抑制不造成审核假绿 | P5.4 | pragma/NoWarn 故障注入 |
| 删除声明/漏项目可被发现 | P5.5 | 精确目标清单差异 |
| 实际包安装后真有诊断 | P1.6、P6.4 | 干净包消费矩阵 |
| 不夸大减少 bug 的幅度 | P6.2、P6.6 | 试点分类统计和审查 |
| 所有权/代数律不提前承诺 | P7/P8 独立退出门 | 分离发布与证据状态 |

### C.2 工作包完成记录模板

```text
工作包：P?.?
状态：NotStarted / Working / Blocked / Verified
依据：总计划条目 + 本文工作包
改动文件：
固定的语义/ADR：
新增或修改的测试名：
反例原先结果 / 本次结果：
实际验证命令、退出码、日志路径：
未验证环境：
新增 unknown/trust 边界：
对旧契约的影响：
审查发现与处置：
下一工作包与依赖满足情况：
```

本文定义的是未来执行义务，不以文档详细程度代替实现完成证明。阶段是否完成以真实测试、消费验证与审查结果为准。
