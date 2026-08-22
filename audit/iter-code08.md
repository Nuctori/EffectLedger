# 迭代08 审计（L2 Source Generator 骨架）

## 摘要
- 构建：`dotnet build`（SDK 路径，`MSBUILD_EXE_PATH=` 清空）0 错误 0 警告（已核实：`已用时间 00:00:00.88`，无 error/warning 行）。
- open 项总数：1（可闭 1 / 设计 out-of-scope 0）。
- 终止判定：**需继续(1)** —— 修掉 1 处死代码 EAG000 描述符后即达「薄翻译层 + 数学全在 L1 + 残差诚实 + 零 Godot」严格标准。

## 逐条核对（回指行号 + PDR § / LANDING_PLAN § + 结论）

| 检查 | 代码行 | PDR/LANDING § | 结论 |
|---|---|---|---|
| 1 是 IIncrementalGenerator | EffectAlgebraGenerator.cs L17 `[Generator]` + L18 `: IIncrementalGenerator`；L43-50 `Initialize` 用 `context.SyntaxProvider.CreateSyntaxProvider` 收集带特性方法 | PDR §14.2 / LANDING §1 | 真：标注 + 增量生成器 + 语法收集，标准形态。OK |
| 2 依赖 L1、不引 Godot | .csproj L14-19 `ProjectReference`→`..\Cosmos.EffectAlgebra\Cosmos.EffectAlgebra.csproj`；无 `Godot.NET.Sdk`（L12-13 注释明言本机未装）；生成桩 `Cosmos.EffectAlgebra.Signature`（L95-99） | LANDING §1（L1 零 Godot，L2 经 L1） | 真：依赖仅 Roslyn + L1；零 Godot 引用。OK |
| 3 只翻译不重算 | `GenerateStub` L86-104 仅产出 `return baseSig;` 透传桩，注释 L84/L101「运行期注入见 LANDING_PLAN §4，此处不重算代数」；无 net/Peak/Compatible 调用 | PDR §14.2 S1；LANDING §1「数学全在 L1」 | 真：生成代码不重算代数，只搬运语法位置；全部数学留 L1。OK（残差诚实，非假绿） |
| 4 诚实残差 | L1 类注释 L7-10「§7 白名单是 Godot 运行时 API 数据，编译期无法枚举实例，故仅生成调用桩并显式注释」；桩注释 L88-92「编译期无法取得实例 Claims，返回 baseSig 透传；运行期注入见 LANDING_PLAN §4」 | LANDING §4「类型约束不到的部分诚实写注释」 | 真：残差明言、无 TODO 埋雷、无 `// TODO` 占位、无静默假绿。OK |
| 5 Attribute 识别 | L58-79 `GetAnnotatedMethod` + `HasAttributeName`（IdentifierName/QualifiedName → `X` 或 `XAttribute`）；仅当两特性命中才产出，否则 `null` 被 `.Where` 过滤 | PDR §8.3 / §14.2 S2 | 真：按简单名过滤 `EffectOverrideAttribute`/`AcceptDeviationAttribute`，等价于 `IsOrHasName`；普通方法不会命中（谓词仅收集 `AttributeLists.Count>0` 再精确名匹配，不会误判）。OK |
| 6 reason 强制由 L1 构造子 | L33-34 注释「reason 非空由 L1 构造子强制，编译期无需诊断」；已回查 EffectAttributes.cs L79-82 `EffectOverrideAttribute(string reason)` ctor `if (string.IsNullOrWhiteSpace(reason)) throw` | PDR §8.3.1 不变式(1) | 真：L1 ctor 强制非空（已读 EffectAttributes.cs 实证），生成器不重发；注释与实现一致。OK |
| 7 csproj 元数据 | L9 `EnforceExtendedAnalyzerRules=true`、L10 `IncludeBuildOutput=false`、L14 `Microsoft.CodeAnalysis.CSharp` 4.12.0 `PrivateAssets=all`、L15 ProjectReference L1；`ls` 确认无 `Class1.cs`（目录仅 csproj + EffectAlgebraGenerator.cs + bin/obj） | LANDING §1 / PDR §14.2 | 真：analyzer 元数据合理、Roslyn 引用正确、无脚手架遗留。OK |
| 8 零 Godot / 零魔法数 | 全文件无 `using Godot`；生成桩不硬编码 Claim（仅注释引 ApiMapping.Claims 注入点，L84） | LANDING §1「零 Godot 依赖」 | 真：无 Godot 命名空间、无硬编码 Claim 魔法数。OK |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | EffectAlgebraGenerator.cs L36-42 | `ReasonGuaranteedByCtor`（`private static readonly DiagnosticDescriptor`）声明后**从未被 `spc.ReportDiagnostic` 调用**——死代码。注释称「无运行期可发点」，既如此应删除该字段，而非保留未使用的描述符（属轻微技术债，与用户「不留技术债」铁律冲突）。 | 可闭（死代码） | 低 | 删除 `ReasonGuaranteedByCtor` 字段及其注释块；reason 强制由 L1 ctor 保证的事实保留在 `GetAnnotatedMethod`/类注释即可。 |

## 结论
- L2 Generator 骨架**真为 L1 之上的薄翻译层**：仅识别 `[EffectOverride]`/`[AcceptDeviation]` 标注方法并生成委托 L1 `Signature` 的桩；数学（net/Peak/Compatible）全在 L1，生成代码零重算；依赖经 `ProjectReference` 到 Cosmos.EffectAlgebra，零 Godot 引用（csproj 已显式排除 Godot.NET.Sdk，因本机未装）。
- 残差诚实：`§7` 白名单是运行期 Godot API 数据、编译期不可枚举实例 Claims，已在类注释与桩注释两处明言、引用 LANDING_PLAN §4 运行期注入点；无 TODO 埋雷、无假绿、无静默漏报。
- Attribute 识别正确（按名匹配 `X`/`XAttribute`，等价于 `IsOrHasName`），普通方法不会误判；`reason` 非空由 L1 `EffectOverrideAttribute` 构造子强制（已读 EffectAttributes.cs 实证），生成器不重发、注释一致。
- csproj 元数据（`EnforceExtendedAnalyzerRules`/`IncludeBuildOutput=false`/Roslyn 引用/无 Class1.cs）均合规，构建 0 错误 0 警告。
- 唯一 open（OPEN-1）为**死代码**：未使用的 `EAG000` 描述符。不阻塞数学正确性，但属可闭轻微技术债，删之即达严格标准。
- 终止判定：**需继续(1)** —— 删除 OPEN-1 死描述符后即「薄翻译层 + 数学全在 L1 + 残差诚实 + 零 Godot + 零技术债」可终止。
