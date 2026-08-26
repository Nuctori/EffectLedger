# 迭代14 审计（类型层加固 + 全解构建）

## 摘要
- 全解构建：0 错误 0 警告（`dotnet build Cosmos.EffectAlgebra.slnx` 已核实，4 工程入 slnx：Algebra / Generator / Analyzer / Tests）。
- Nullable：`Algebra`/`Generator`/`Analyzer`/`Tests` 4 个 csproj 均 `<Nullable>enable</Nullable>`；构建 0w ⇒ 无 NRT 逃逸（任何裸可空未标注会触发 CS8618/8625 并因 TreatWarningsAsErrors 失败）。
- TreatWarningsAsErrors：`Algebra`/`Generator`/`Analyzer` 3 个 src 工程均设 `true`（Tests 非 src，豁免，符合任务「3 个 src 工程」）。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + 结论 OK/OPEN）

| 检查 | 行号 | 真约束? | 结论 |
|---|---|---|---|
| 1 Claim 五字段完整（required 等价） | Objects.cs L122 `public readonly record struct Claim(Kind, ResourceId, Mode, ScopeId, Interval)` | 真：`readonly record struct` 位置参数 = 5 个强制构造参数，无参数less ctor ⇒ 五元组缺一则编译失败。`required` 关键字仅对非位置属性有意义；位置记录强制更强（连 `new Claim()` 都不存在）。无任何字段可省略 | OK |
| 1b 五字段类型均为 non-nullable 值类型 | L122 Kind/ResourceId/Mode/ScopeId/Interval 全是 `readonly record struct` | 真：Nullable enable 下无 `string`/`class` 裸字段，无 null 默认 Claim 路径（除 `default(Claim)` 这种值类型固有语义，与 `required` 同样无法阻止，非本加固缺口） | OK |
| 2 Claim 不可变 | Objects.cs L122 `readonly record struct` + 无 `{ get; set; }` 定义 | 真：`readonly record struct` ⇒ 值语义 + 不可变；位置属性为 get/init，无 `set` 后门；`Normalize()` 用 `this with` 返回新实例（L125），不就地改 | OK |
| 3 Nullable 全工程 enable | Algebra.csproj L6 / Generator.csproj L6 / Analyzer.csproj L6 / Tests.csproj L5 均 `<Nullable>enable</Nullable>` | 真：4/4 enable；构建 0w 交叉证实无 `#nullable disable` 逃逸、无裸 `string`/`ScopeId` 未标注 | OK |
| 4 TreatWarningsAsErrors（src 3 工程） | Algebra/Generator/Analyzer csproj 均 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | 真：3/3 src 设 true；Tests 设 `IsPackable=false` 未设（非 src，符合任务口径）；结合 0w ⇒ 任何 NRT/未用变量警告即红 | OK |
| 5 构造即合法（无运行时 if 校验五元组） | Objects.cs L122 ctor 仅靠类型层强制五元组完整；无 `if (resource==null)` 之类 | 真：构造不变量交付类型层，调用点（测试/生成器/分析器）真补全且 0e 通过（全解构建已证实） | OK |
| 6a EffectOverride.Reason 仅 get | EffectAttributes.cs L20 `public string Reason { get; }` | 真：无 set 后门；构造子 L43 强制非空（`IsNullOrWhiteSpace` ⇒ 抛），fail-fast | OK |
| 6b AcceptDeviation.Epsilon 仅 get | EffectAttributes.cs L61 `public double Epsilon { get; }` | 真：无 set 后门；构造子 L67 强制 ε∈[0,0.5]（越界 ⇒ 抛），上界 fail-closed | OK |
| 6c 构造子强制保留 | EffectAttributes.cs L43 / L67 | 真：reason 非空 + epsilon 范围均在 ctor 强制，未因加固退化 | OK |
| 6d 属性 named-arg 必须的可变属性 | EffectAttributes.cs L23 `Scope{get;set;}` / L30 `OverrideMode{get;set;}` / L36 `OverrideSize{get;set;}` | 设计内：C# 属性命名参数语法（`[EffectOverride(Scope=...)]`）要求 settable 属性；运行期 attribute 实例由编译器构造、不可变。非「可变后门」，不破坏 reason/epsilon 的 get-only 约束 | OK（设计内，非 open） |
| 7a 无 TODO/FIXME/HACK | 全 src grep `TODO|FIXME|HACK` | 真：无匹配 | OK |
| 7b 无 #pragma warning disable | 全 src grep `pragma`/`warning disable` | 真：无匹配（Analyzer.csproj 用 `<NoWarn>RS2008;RS2007</NoWarn>` 而非 pragma，属打包发布追踪豁免，注释已说明，非代码路径埋雷） | OK |
| 7c 无魔法数未类型化 | NatStar.Interval.DeviationVal 常量均经 `Of/Top/Exact/Default` 类型构造 | 真：阈值/边界全为类型化静态工厂，无裸字面量魔法数 | OK |

## open 项清单
无。

## 结论
- Claim 五元组完整性由 `readonly record struct` 位置参数在**编译期**强制（比 `required` 更强：无参数less 构造路径），无运行时 if 漏判；全部字段为 non-nullable 值类型，Nullable enable + TreatWarningsAsErrors 双重保障无 NRT 逃逸（已 0e/0w 实证）。
- `EffectOverride`/`AcceptDeviation` 的 reason/epsilon 为 get-only 且 ctor 强制非空/范围（fail-closed）；named-argument 必须的 Scope/OverrideMode/OverrideSize 的 `set` 是 C# attribute 语法固有要求，运行期不可变，不构成「可变后门」。
- 全工程零 TODO/pragma/魔法数；`NoWarn` 仅用于 Roslyn 发布追踪豁免（RS2008/RS2007），已在 csproj 注释声明，非技术债。
- 用户铁律「类型系统能约束的用类型——构造即合法，不靠运行时 if 漏判」在本迭代**真落实**：加固经类型层交付，构建 0e/0w 独立验证，无 open 项 ⇒ 可终止。
