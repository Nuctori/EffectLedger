# 迭代04 审计（§9.1 Deviation 公式）

## 摘要
- 构建：**0 错误 0 警告**（已独立核实 `MSBUILD_EXE_PATH=` 置空绕开坏 VS MSBuild，`dotnet build -clp:ErrorsOnly`）。
- `ScopeId.Global` 嵌套类型确认存在于 Objects.cs L90（`public sealed record Global : ScopeId`）。
- open 项总数：**1**（可闭 1 / 设计 out-of-scope 0）。
- 终止判定：**需继续（1）**——数学公式、⊤ 不崩溃、ε=1 防除零、资源对齐均正确（类型真约束，非注释吹）；唯一 open 为代码注释与实现不符（"包含全部 Claim" 实际仅 occupy 桶），属可闭文档缺陷。

## 逐条核对（回指行号 + PDR § + 结论）

| 检查 | 代码行 | PDR § | 类型真约束? | 结论 |
|---|---|---|---|---|
| 1 公式正确性 | Deviation.cs L33-50 | §9.1 L784-785 | 真：分子 `Math.Abs(aMid - eMid)` = \|actual_mid − expected_mid\|（减法对称）；分母 `Math.Max(eRange, 1.0)` = max(expected_range, ε)；`mid=(lo+hi)/2`(L73)、`range=hi−lo`(L74) 真算 | OK |
| 2 分母下界 ε=1 | Deviation.cs L48 | §9.1 L786 / iter33 | 真：`Math.Max(eRange, 1.0)`，单值 [s,s] range=0 ⇒ denom=1，无 0/0 或 x/0 路径 | OK |
| 3 ⊤ 跳过 / 整体 ⊤ | Deviation.cs L40-43, L52 / TryMid L70-71 | §3.1.5c / §9.1 L787 / MA-002 / ST-03 | 真：`TryMid` 在 `Lo.IsTop \|\| Hi.IsTop` 时返回 false（由 `NatStar.IsTop` 类型字段驱动，非 NaN 比较），`anyTop` ⇒ `DeviationVal.Top`；⊤ 分支直接 break，不进入除法，故无 NaN/∞ | OK |
| 4 资源对齐 | Deviation.cs L29-34 / Algebra.cs L54, L72 | §3.1.4a / §3.3.1 | 真：键 = `ResourceId.Normalize(...)`；缺失侧 `NetTable.Get` ⇒ `Interval.Default [1,1]`（保守） | OK（但见 OPEN-1：仅 occupy 桶） |
| 5 ExceedsThreshold | Deviation.cs L81-82 → Numeric.cs L120 | §9.1 L773-774 / §3.1.5c | 真：委托 `DeviationVal.ExceedsThreshold` = `!IsTop && Value > threshold`；⊤ ⇒ false 不报警 | OK |
| 6 类型边界（mid/range 的 ±） | TryMid L70-76 | §3.1.5a | 真：`x.Lo.Value + x.Hi.Value` / `(double)x.Hi.Value − x.Lo.Value` 仅在 `!IsTop` 分支内执行（L70 已拦 ⊤）；无 NatStar ± 运算参与；denom≥1 ⇒ 无 NaN/∞ 路径 | OK（ulong 加法理论溢出不计，规模不可达） |
| 7 注释/出处 | Deviation.cs L1-15, L22-30, L61-72, L80-82 | §9.1 / §3.1.5c / §3.1.5a / MA-002 / ST-03 | 完整：类级 + 三方法均带 § 出处、ε=1、⊤ 整体跳过声明 | OK（除 OPEN-1 处误述） |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | Deviation.cs L31 | 注释称「scope 取 Global（§3.1.3b 最大元），**包含全部 Claim**（与 §9.1 开发期全量对账一致）」，但实现经 `NetTable.Compute`，而 `NetTable.Compute`（Algebra.cs L57 `if (c.Kind != Kind.Occupy) continue;`）**仅含 occupy 桶**。即 Deviation 实际只比对 create/move/release 的净占用，read/write 不进对账。注释与代码矛盾，会使审阅者误以为 read/write 偏差已被验证。 | 可闭（文档/注释） | 低 | 二选一（推荐前者，避免未经批准的扩域决策）：(a) 修正注释为「仅含 occupy 桶净效应（net，§3.3.1），与开发期占用对账一致」；(b) 若产品意图确为全量对账，则改走三桶合并枚举（SignatureExtensions.AllClaims）而非 NetTable——但属未批准产品决策，不默认采用。 |

## 结论
- §9.1 数学公式**真落实于类型/代码**（非注释吹）：分子对称性正确、分母 ε=1 防除零、mid/range 实算、⊤ 经 `NatStar.IsTop` 类型字段检测后整体返回 `DeviationVal.Top`（无 NaN/∞ 可达）、资源按 `ResourceId.Normalize` 键对齐、缺失侧保守 [1,1]、`ExceedsThreshold` 复用类型安全方法（⊤ 不报警）。
- 唯一 open（OPEN-1）为**注释与实现不符**："包含全部 Claim" 误述，实际仅 occupy 桶净效应对账。不阻塞数学正确性，但属可闭文档缺陷，避免虚假收敛（审阅者误判 read/write 偏差已覆盖）。
- 终止判定：**需继续（1）**——修正 OPEN-1 注释后即达「公式真落实 + ⊤ 不崩溃靠类型强制 + ε=1 防除零 + 资源对齐」严格标准。
