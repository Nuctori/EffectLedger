# 迭代09 审计（L3 Roslyn Analyzer 骨架）

## 摘要
- 构建：0 错误 0 警告（已核实 Analyzer 工程 `dotnet build`，MSBUILD_EXE_PATH 已清空走 SDK MSBuild）
- open 项总数：2（可闭 2 / 设计 out-of-scope 0）
- 终止判定：需继续(2)

## 逐条核对（回指行号 + PDR § + 结论）
| 检查 | 代码行 | PDR § | 结论 |
|---|---|---|---|
| 1 是 DiagnosticAnalyzer | L19 `[DiagnosticAnalyzer(LanguageNames.CSharp)]` + L20 `: DiagnosticAnalyzer` + L52-53 `SupportedDiagnostics` | — | 真：三类要件齐备，描述符数组 `ImmutableArray.Create(MissingReleaseForAcquire, EffectOverrideKindForbidden)` |
| 2 依赖 L1 / 零 Godot | csproj `ProjectReference Cosmos.EffectAlgebra` + 无 `Godot.NET.Sdk`；L60/L69 `GodotApiWhitelist.All`（§7）、L73 `ReleaseClass.Names`（§8.1）；无 `using Godot` | §7 / §8.1 | 真：判定数据全取 L1 白名单/ReleaseClass，无硬编码 Claim；构建 0e/0w 证实零 Godot 引用 |
| 3 DO-9 近似泄漏 | L30-39 描述符 EAA0901（§3.3.1 DO-9）；L126-145 检测 `hasAcquire && !hasRelease && !hasEscape` 报 Warning；类注释 L8-12 明言「控制流近似、运行期 Σnet 权威、不得声称数学已验证」 | §3.3.1 / §8.1 | 部分：检测逻辑真（取 §7 `Mode.Create` acquire + §8.1 release-class），近似性已注释；但**限定调用（receiver 前缀）漏报未诚实注释**（见 OPEN-2） |
| 4 §8.3.1 kind 禁止 | L34-48 描述符 EAA0801（Info）；L96-104 检测 `[EffectOverride]` 命名参数 `kind`/`OverrideKind` | §8.3.1 | 部分：L1 `EffectOverrideAttribute` 确无 `OverrideKind` 属性（已读 EffectAttributes.cs 实证）→ 良性代码无法写出该命名参数（编译器 CS0117 先错）；L3 重检类型已禁止的约束，属冗余护栏（见 OPEN-1） |
| 5 DO-7 量纲混算 | 全局：Analyzer 无 DO-7 重算逻辑；类注释 L13「绝不重算代数（net/Peak/Compatible 全在 L1）」 | §8.2 | 真：L3 不触碰量纲隔离，权威在 L1 运行期 `NetTable.Compute` 仅 occupy 桶，与 §3.3.1 一致；无重算、无静默 |
| 6 诊断 id/出处 | L30 `EAA0901`（"09"=§3.3.1，"01"=DO-9）；L34 `EAA0801`（"08"=§8.3，"01"）；L101/L134 均 `ReportDiagnostic` 真调用 | §3.3.1 / §8.3.1 | 真：双描述符带 § 出处 id 且均被调用，无死描述符 |
| 7 死代码/魔法数 | 无 `using Godot`（grep 空）；L60/L69/L73 取 L1 数据无硬编码 Claim；模式判定用 `Mode.Create`/`Mode.Release` 枚举无魔法数 | — | 真：无死描述符（两描述符皆用）、无硬编码 Claim、零 Godot；`Canonical` 归一（去 `.`/`_`）对 `Audio.Play`↔`audioplay`、`queue_free`↔`queuefree`、`FreeChildrenInGroup`↔`freechildrengroup` 一致 |
| 8 残差诚实 | 类注释 L8-12（控制流近似 + 运行期权威）+ L13（数学全在 L1）+ EAA0901 description（"L3 仅做调用级近似；真实闭合由运行期 net 判定"） | — | 部分：近似性两处明言；唯 receiver-前缀漏报未点明（见 OPEN-2） |

## open 项清单
| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | EffectAlgebraAnalyzer.cs L34-48（EAA0801）、L96-104 | §8.3.1 kind 禁止已由 L1 `EffectOverrideAttribute` **类型层强制**（无 `OverrideKind` 属性，EffectAttributes.cs 实证）。良性 C# 无法写出 `kind`/`OverrideKind` 命名参数（编译期 CS0117 先报错），故 EAA0801 对可编译代码永不可达，仅对"语法合法但语义非法"的属性噪声报警。用户铁律「类型系统能约束的用类型」「L3 只发根因诊断」「不留技术债」——L3 重检类型已禁止的约束属冗余技术债。 | 可闭（冗余护栏） | 低 | 删除 EAA0801 描述符及其 L96-104 分支；§8.3.1 kind 禁止纯由 L1 类型强制，注释保留于 EffectAttributes.cs 即可（与 OPEN-1@iter-code08 删除死描述符同标准）。 |
| OPEN-2 | EffectAlgebraAnalyzer.cs L113-121 `RawName` / `CanonicalOfInvocation` | `RawName` 对成员访问返回 `ma.Expression.ToString() + "." + ma.Name.Identifier.Text`（L113），故 `this.AddChild(...)`/`tree.GetNode(...)` 等**带 receiver 的限定调用**归一为 `"thisaddchild"`/`"treegetnode"`，与白名单键 `"addchild"`/`"getnode"` 不匹配 → DO-9 acquire 检测**静默漏报**。该漏报属控制流近似的真实边界，但类注释 L8-12 仅泛称「调用级近似」，未点明"限定调用不识别"这一具体漏报面。用户铁律「控制流近似必须诚实注释」。 | 可闭（近似诚实） | 中 | 二选一（推荐前者，最小改动）：(a) 在类注释或 EAA0901 description 显式补「限定调用 `obj.AddChild` 不识别，仅裸方法名 `AddChild` 匹配」；(b) 精炼匹配：对 `MemberAccess` 取末位方法名 `ma.Name.Identifier.Text` 归一，并对 `Audio.Play`/`Anim.Play` 这类 disambiguated 白名单键保留「receiver 末段 = Audio/Anim」的特判（避免 `Play` 误撞两类）。注意 (b) 需同步白名单键策略，属小改但涉及 §7 匹配语义，建议先 (a) 诚实注释、后续迭代再精炼。 |

## 结论
- L3 Analyzer 真为 L1 之上的薄静态根因层：`[DiagnosticAnalyzer]`+`SupportedDiagnostics` 齐备；依赖经 `ProjectReference` 到 Cosmos.EffectAlgebra，零 Godot 引用（csproj 显式排除 Godot.NET.Sdk），acquire/release 判定数据全取 §7 `GodotApiWhitelist.All` 与 §8.1 `ReleaseClass.Names`，无硬编码 Claim、无魔法数。
- DO-9 近似泄漏真实现（EAA0901 = acquire 无 release + 未标 `[EffectOverride]` ⇒ Warning），控制流近似与「运行期 Σnet 权威」在类注释 + description 两处明言，无 TODO 埋雷、无假绿。
- DO-7 量纲混算不在 L3 重算，权威留 L1 运行期，符合「数学全在 L1」。
- 两处可闭 open：OPEN-1 EAA0801 重检类型已禁止的 kind 约束（冗余护栏，删之即无技术债）；OPEN-2 限定调用接收者前缀致 DO-9 静默漏报未诚实注释（补注释即达近似透明）。
- 终止判定：**需继续(2)** —— 闭 OPEN-1/OPEN-2 后，L3 即达「根因诊断 + 数学全在 L1 + 近似诚实 + 零 Godot + 零技术债」严格标准。
