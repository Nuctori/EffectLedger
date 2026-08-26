# 迭代01 审计（L1 代数核心）

## 摘要
- 构建状态：**0 错误 0 警告**（已独立核实 `dotnet build`，MSBUILD_EXE_PATH 置空以绕开 VS 坏 MSBuild）。
- open 项总数：**3**（可闭 3 / 设计层 out-of-scope 0）。均为代码与 PDR 不符或注释自相矛盾，可在 iter01 代码内机械修复。
- 终止判定：**需继续（3）**——非 L2/L3/测试类缺口（那些后续迭代职责，不计入），而是本层类型/注释真未对齐 PDR 的 3 处。
- 总评：类型边界主体落实良好（⊤ 律、⊆* 偏序、Compatible 全函数、结构相等、零 Godot 依赖均真进类型），但 net 量纲过滤、⊤ 守恒语义、Interval lo=⊤ 三处需收紧。

## 逐符号核对（每条回指代码行号 + PDR § + 结论）

| 符号 | 代码行 | PDR § | 类型是否真约束 | 注释是否完整 | 结论 |
|---|---|---|---|---|---|
| `NatStar` + / * / Max / Min | Numeric.cs L27,L32,L36,L41 | §3.1.5a | 真：`+`/`*` 用 `IsTop` 短路返回 `Top`；`Max`/`Min` 内嵌 ⊤ 律 | 完整（每行标 §3.1.5a） | OK |
| `NatStar` 0×⊤=⊤ | Numeric.cs L32 | §3.1.5a | 真：`*` 任一 IsTop 即 Top（覆盖 0×⊤） | 完整（注释明言保守） | OK |
| `NatStar.CompareToFinite` | Numeric.cs L54 | §3.1.5a | 真：先判 `IsTop` 再比数值，无 NaN | 完整 | OK |
| `Interval` lo≤hi 校验 | Numeric.cs L37 | §3.1.5 | **部分**：仅双侧有限时校验；`lo=⊤` 时跳过 → 允许 `[⊤,x]`（PDR 判非法） | 注释称“[⊤,x] 非法”但无代码强制 | OPEN-3 |
| `Interval.Default/Dynamic/Exact` | Numeric.cs L45,L48,L51 | §3.1.5 | 真：`[1,1]`/`[1,⊤]`/`[s,s]` 值正确 | 完整 | OK |
| `Interval.Merge` | Numeric.cs L54 | §3.1.5b | 真：`Lo.Min`/`Hi.Max` 内嵌 ⊤ 律（Min/Max 已实现） | 完整 | OK |
| `DeviationVal.ExceedsThreshold` | Numeric.cs L62 | §3.1.5c/§9.1 | 真：`!IsTop && Value>threshold`，先判 ⊤ 再比 | 完整（引 §9.1 修正） | OK |
| `ResourceId` 判别联合 | Objects.cs L18-31 | §3.1.2/§3.1.2b | 真：record 结构相等 = 构造子标签+字段 | 完整 | OK |
| `ResourceId.Normalize` | Objects.cs L46 | §3.1.4a/§3.1.2b | 真：`Self("signal_"+s)`、`Signal("signal_"+s)` ⇒ `SignalBus(s)`；其余原样 | 映射表注释含全部 §7 裸名（memory/disk/.../audio_channel...） | OK |
| `ScopeId.IncludedIn` (⊆*) | Objects.cs L83 | §3.1.3b | 真：`Equals`(自反) + `other is Global`(最大元) + 跨标签 false；构成偏序（自反/反对称/传递均成立） | 完整 | OK |
| `Claim.Normalize` | Objects.cs L114 | §3.1.1/§3.1.4a | 真：resource 走 Normalize + size 缺省 ⇒ Default | 完整 | OK |
| `Signature` 三桶 | Objects.cs L119-127 | §3.1.4b | 真：三独立 `ImmutableHashSet`，按 Kind 分流，类型层不相交 | 完整（注释明言跨桶须 Weight，L3 补） | OK |
| `Signature.Union` | Objects.cs L143 | §3.2.1/§3.2.2 | 真：`Add` 先 `Normalize` 再 `Add`（集合去重 ⇒ 幂等） | 完整 | OK |
| `Compatible` 全函数+对称 | Algebra.cs L17 | §3.2.3 | 真：`Resolve(Unknown⇒Use)`；use 放行；create+release/move 配对放行；CONFLICT=(C,C)/(M,M)/(R,R) 排除；对称（(a,b)/(b,a) 同式） | 完整（P1-P4 注释） | OK |
| `Weight.Of` | Algebra.cs L39 | §3.3.2b | 真：`a==b?1.0:NaN`；NaN 编码 ⊥ | 完整（声明 ⊥ 约定） | OK |
| `NetTable.Compute` | Algebra.cs L50 | §3.3.1 | **不符**：遍历 `AllClaims()` 未过滤 `c.kind=occupy`；PDR net 仅 occupy 参与 | 注释未声明放宽到全 kind | OPEN-1 |
| `NetTable.IsConserved` | Algebra.cs L73 | §3.3.1/DO-9 | **矛盾**：`⊤` 时 `loLeZero && hiGeZero` 均 true ⇒ 返回 **true（守恒）**，但注释明言“⊤ 不宣称守恒 / fail-closed 需人工界定”，PDR 亦要求 ⊤ 触发需人工确认而非静默 | 注释与代码冲突 | OPEN-2 |
| `Peak.Compute` | Algebra.cs L89 | §3.3.2/§3.2.5 | 真：`Hi.IsTop ⇒ Top`；size 求和（旧 cardinality 形式已废弃，注释声明） | 完整 | OK |
| 注释完整性（出处+不变式） | 全部公共类型 | 各 § | — | 每个公共类型/运算符均带 `(§x.y)` 出处；无“裸类型无注释” | OK |
| L1 零 Godot 依赖 | Objects.cs L1-9 | 设计约束 | 真：无 `using Godot`；`Rid`/`StringName` 为 `Cosmos.EffectAlgebra` 命名空间内 `readonly record struct` 原语别名 | 注释明言映射层 §7 负责转换 | OK |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | Algebra.cs L50-60 | `NetTable.Compute` 对 `sig.AllClaims()` 全量求和，未过滤 `c.kind == Occupy`；PDR §3.3.1 明确定义 `net` 仅 `c.kind=occupy` 的 create/move 与 release 抵消。代码把 read/write 也纳入净变化，与 PDR 量纲不符（read/write 不应进入 net 守恒判定）。 | 可闭 | 中 | 在循环内加 `if (c.Kind != Kind.Occupy) continue;`（或只对 occupy 桶枚举），与 §3.3.1 严格对齐；注释同步。 |
| OPEN-2 | Algebra.cs L73-80 | `IsConserved` 在 `v.Lo.IsTop || v.Hi.IsTop` 时 `loLeZero`/`hiGeZero` 均为 true ⇒ 返回 **true（守恒）**。但其注释与 PDR §3.3.1 DO-9 fail-closed 均要求 ⊤ 视为“需人工界定”，即**不应宣称守恒**（应返回 false 交由人工确认）。代码与注释/ PDR 自相矛盾，且若被 DO-9 直接采用会静默漏报 ⊤ 资源。 | 可闭 | 高 | 改为：`if (v.Lo.IsTop || v.Hi.IsTop) return false;`（未知 ⇒ 不守恒 ⇒ 触发人工确认），注释保留 fail-closed 说明。 |
| OPEN-3 | Numeric.cs L37-41 | `Interval` 构造子仅在双侧有限时校验 `lo≤hi`；`lo.IsTop` 时跳过，允许构造 `[⊤, x]`。PDR §3.1.5 与引擎事实注记均视 `[⊤,x]` 为非法（下界不可为 ⊤）。类型未约束该不变量。 | 可闭 | 低 | 构造子内加 `if (lo.IsTop) throw new ArgumentException("Interval lo=⊤ invalid (§3.1.5)");`（下界必须有限；上界可为 ⊤）。 |

## 结论
- 类型边界主体**真进类型**（非注释吹）：NatStar ⊤ 闭包、ScopeId ⊆* 偏序、Compatible 16 对全函数+对称、ResourceId/Claim 结构相等+归一、Signature 三桶不相交、DeviationVal 先判 ⊤、L1 零 Godot 依赖——均落实，注释完整带 § 出处。
- 3 处 open 均为**代码与 PDR 字面不符 / 注释自相矛盾**，非 L2/L3/测试职责（那些属后续迭代 out-of-scope，不计入）：OPEN-1（net 量纲应限 occupy）、OPEN-2（⊤ 应判不守恒）、OPEN-3（Interval lo=⊤ 应拒）。
- 终止判定：**需继续（3）**——修复上述 3 处后，iter01 L1 核心可达“每个符号类型约束 + 注释承载数学边界”的严格标准。
