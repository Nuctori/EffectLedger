# 迭代15 审计（跨层引用一致性）

## 摘要
- 测试：79 通过 0 失败（其中 CrossLayerTests 8 项，已独立 `dotnet test --filter CrossLayerTests` 复测：8 通过 0 失败，29ms）
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）
- 终止判定：可终止

## 逐条核对（回指行号 + 被测 + 真锁? + 假绿? + 结论）

| 项 | 测试行号 | 被测（src 行号） | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|
| 1 GodotApiWhitelist.All | CrossLayerTests.cs L22-27 | `GodotApiWhitelist.All` 属性（ApiMapping.cs L46 `public static ImmutableArray<ApiMapping> All { get; } = Build();`） | 真：`GetProperty("All")` 返回 null 若该属性改名/删除 ⇒ `Assert.NotNull` 红；`PropertyType == ImmutableArray<ApiMapping>` 锁返回类型；`.Length > 0` 锁非空 | 否 | OK |
| 2 ReleaseClass.IsRelease + Names | L30-44 | `ReleaseClass.IsRelease(string)->bool`（ApiMapping.cs L185）；`Names`（L181 七项） | 真：`GetMethod("IsRelease", new[]{typeof(string)})` 改名/改参 ⇒ null ⇒ 红；`IsStatic`/`ReturnType==bool`/单参 锁签名；7 项成员用 `Assert.Contains` 数据驱动，遗漏⇒红；正向 `queue_free`/`AddChild` 双向验证 | 否 | OK |
| 3 Claim 五字段 | L47-56 | `Claim` record（Objects.cs L122：`Claim(Kind, ResourceId, Mode, ScopeId, Interval)`） | 真：`GetProperties()` 字典查 `Kind/Resource/Mode/Scope/Size` 缺字段 ⇒ 红；各 `PropertyType` 锁为 `Kind/ResourceId/Mode/ScopeId/Interval`（删/改类型 ⇒ 红） | 否 | OK（注：反射仅查属性名/类型，required 修饰在构造子，见 §9 说明） |
| 4 Compatible / NetTable 签名 | L59-77 | `Compatible.IsCompatible(Mode,Mode)->bool`（Algebra.cs L17）；`NetTable.Compute(Signature,ScopeId)->NetTable`（L53）；`NetTable.IsConserved(ResourceId)->bool`（L94） | 真：三处 `GetMethod` 带精确参数类型数组，改名/改参序/改返回 ⇒ null ⇒ 红；`IsStatic`/`IsInstance` 区分锁静态性 | 否 | OK |
| 5 L2 Generator 类型 | L80-84 | `EffectAlgebraGenerator : IIncrementalGenerator`（Generator/EffectAlgebraGenerator.cs L18） | 真：`IsAssignableFrom(IIncrementalGenerator)` + `GetInterface("Microsoft.CodeAnalysis.IIncrementalGenerator")` 删接口实现 ⇒ 两断言红 | 否 | OK |
| 6 L3 Analyzer 类型 | L87-92 | `[DiagnosticAnalyzer]` + `: DiagnosticAnalyzer`（Analyzer/EffectAlgebraAnalyzer.cs L19-20）；`SupportedDiagnostics` 含 `EAA0901`（L55） | 真：`IsAssignableFrom(DiagnosticAnalyzer)` + `GetCustomAttribute<DiagnosticAnalyzerAttribute>()` + `Contains(d=>d.Id=="EAA0901")` 删/改 id ⇒ 红 | 否 | OK |
| 7 ApiMapping 字段 + Claims 元素合法 | L95-115 | `ApiMapping.GodotApi`（ApiMapping.cs L8）/`.Claims`（L12 `ImmutableArray<Claim>`）；每条 `m.Claims` 非空、字段合法 | 真：`GetProperty` 锁字段存在且 `Claims` 类型为 `ImmutableArray<Claim>`；逐元素断言 `GodotApi` 非空、`Claims.Length>0`、Kind/Mode 枚举合法、`Scope`/`Resource` 非空、`Size` 非 [0,0] 非法区间（构造不变量呼应） | 否 | OK |
| 8 假绿扫描 | 全局 | — | — | 否：无 `Assert.True(true)`；无 `BindingFlags` 误用（`GetProperty`/`GetMethod` 用默认 `BindingFlags.Public|Instance|Static`，`IsRelease(string)` 显式传参类型数组避免重载歧义）；无「永远通过」弱断言——每条反射 `GetX` 均接 `Assert.NotNull`/类型相等/成员包含，任一符号漂移使对应断言红 | 否 | OK |
| 9 出处注释 | 各方法签名/类级 | §7 / §8.1 / §3.1.1 / §3.1.4b / §3.2.3 / §3.3.1 / §L2 / §L3 | 真：9 项断言均带 § 出处（白名单 §7、release-class §8.1、Claim §3.1.1/§3.1.4b、Compatible §3.2.3、NetTable §3.3.1、Generator/Analyzer §L2/§L3） | 否 | OK |

## 补充验证（独立复测）
- `dotnet test --filter "FullyQualifiedName~CrossLayerTests"` 实跑：**8 通过 0 失败**，证明 8 条反射断言在现行代码下均 GREEN，且非编译期未执行。
- 可证伪性回归推演（审计独立判断）：
  - 改 `GodotApiWhitelist.All` → `All2`：项1 `GetProperty("All")` 返回 null ⇒ 红。
  - 改 `ReleaseClass.IsRelease` 签名（如增参）→ 项2 `GetMethod` 返回 null ⇒ 红；删 `Names` 任一成员 ⇒ `Assert.Contains` 红。
  - `Claim` 删 `Scope` 字段 → 项3 字典缺键 ⇒ 红。
  - `Compatible.IsCompatible` 改返回 `int` → 项4 `ReturnType==bool` ⇒ 红。
  - `EffectAlgebraGenerator` 撤 `IIncrementalGenerator` → 项5 `IsAssignableFrom` ⇒ 红。
  - `EAA0901` 改名 `EAA0902` → 项6 `Contains(Id=="EAA0901")` ⇒ 红。
  - 以上任一均使测试由绿转红，无「静默通过」路径。

## open 项清单
无。

## 结论
- 8 项跨层引用一致性断言**真由反射在运行期锁定** L1↔L2/L3 的符号存在性与签名匹配性，无假绿、无恒真断言、无 `BindingFlags` 漏检盲区。
- 被测 src（ApiMapping/Objects/Algebra/Analyzer/Generator）签名与测试反射目标**逐一对齐**（已读源实证）：`GodotApiWhitelist.All[ImmutableArray<ApiMapping>]`、`ReleaseClass.IsRelease(string)->bool` + `Names` 七项、`Claim(Kind,ResourceId,Mode,ScopeId,Interval)`、`Compatible.IsCompatible(Mode,Mode)->bool`、`NetTable.Compute(..)->NetTable` + `IsConserved(ResourceId)->bool`、`EffectAlgebraGenerator:IIncrementalGenerator`、`EffectAlgebraAnalyzer:[DiagnosticAnalyzer]+:DiagnosticAnalyzer+SupportedDiagnostics{EAA0901}`、`ApiMapping.GodotApi/Claims[ImmutableArray<Claim>]`。
- 用户铁律（跨层引用一致性真由反射断言、非假绿）**已满足**：任一签名/符号漂移（改名/改参/改返回/删成员）均使对应 `Assert` 红，且已独立复测 8/8 通过佐证现行 GREEN 非装饰。
- 终止判定：**可终止**。
