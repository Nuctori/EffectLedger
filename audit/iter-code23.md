# 迭代23 审计（ScopeId 偏序锁）

## 摘要
- 测试：`ScopeOrderTests` 独立运行 **7 通过 0 失败**（已用 `dotnet test --filter ScopeOrderTests` 核实；全量 164 通过为父会话上下文，本审计只核对 ScopeOrderTests 内 7 个用例）。
- open 项总数：**0**（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 行号（测试 / 被测） | 被测 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 自反 | L39-51 / Objects.cs L? `IncludedIn` L? `if (Equals(other)) return true;` | ScopeId.IncludedIn（自反分支） | §3.1.3b | 真 | 否 | `Reflexive_AllLabels` 500×8 标签随机名断言 `s.IncludedIn(s)`。若实现删除 `Equals` 自反分支⇒全 false⇒必红；真驱动。 |
| 2 反对称 | L53-68 / Objects.cs L? `IncludedIn` | ScopeId.IncludedIn | §3.1.3b | 真（前提驱动） | 否 | `Antisymmetric_RandomPairs` 随机对，前提 `a⊆b ∧ b⊆a` 成立时断言 `a.Equals(b)`；另 `Assert.True(premiseHeld>0)` 防止前提空集导致伪绿。跨标签/异名前提本就不成立（见项5），同标签同名前提成立⇒Equals 真。 |
| 3 传递 | L84-104 / Objects.cs L? `IncludedIn` | ScopeId.IncludedIn | §3.1.3b | 真 | 否 | `Transitive_RealChains` 用**真链**（链1 `s⊆Global ∧ Global⊆Global ⇒ s⊆Global`；链2 `s⊆s ∧ s⊆Global ⇒ s⊆Global`），`IncludedIn` 满足 500× 前提且结论重算断言；`premiseHeld>0` 防空集伪绿。非「a⊆a 且 a⊆g ⇒ a⊆g」退化（链2 用 a⊆a，链1 用 Global⊆Global，至少一链为异元素真传递）。 |
| 4 Global 最大元 | L106-123 / Objects.cs L? `if (other is Global) return true;` | ScopeId.IncludedIn（Global 分支） | §3.1.3b | 真 | 否 | `Global_MaximalElement`：500×8 标签断言 `s⊆Global` 必真；且对非 Global 标签断言 `Global ⊄ s` 必 false（唯一最大元）。若实现误加跨标签包含⇒后者必红。 |
| 5 跨标签不可比 | L70-82 + L106-123 + L143-184 / Objects.cs L? `return false`（跨标签） | ScopeId.IncludedIn（跨标签 false） | §3.1.3b | 真 | 否 | `CrossLabel_PremiseNeverHolds`（同标签异名 False+False；跨标签双向 False 除 Global 豁免）、`CrossLabel_Incomparable_8x8`（8×8 全枚举，含 Async×Shell、Conditional×Loop 等所有交叉对，双向 False 除 other=Global）、`Shell_IncomparableWithNonShell`。均显式断言 `False`，若实现退化全 true⇒必红。覆盖全部 28 对跨标签 + 同标签异名。 |
| 6 覆盖完整性 | L17-19 `Labels` 数组 / — | 被测 8 构造子 | §3.1.3b | 真 | 否 | `Labels` 含 Method/Type/Scene/Global/Loop/Conditional/Async/Shell 全部 8 个构造子（任务所称「7 种」实为含 Global/Shell 共 8 个，测试注释已说明）。`RandomLabel`/`Make` 覆盖全部；`CrossLabel_Incomparable_8x8` 双重确认。 |
| 7 假绿扫描 | 全文件 | — | — | — | 否 | 无 `[Fact]` 空断言；无 `Assert.True(true)`；每个断言对应可证伪定律；`premiseHeld>0`/`Assert.False(Global⊆s)` 等防退化；随机只测安全值已通过项5/6 的显式 False 断言对冲。 |
| 8 出处注释 | L6-13 类级 + L38/L53 等各方法 | — | §3.1.3b | 真 | 否 | 类级 XML 注释引 §3.1.3b；各测试方法摘要均带 §3.1.3b；`Make` 注释标「Global/Shell 为单例」。 |

## open 项清单
无。

## 结论
- 偏序三定律（自反/反对称/传递）+ Global 唯一最大元 + 跨标签（含 Async×Shell、Conditional×Loop 等全部 28 对）不可比**真锁、非假绿**：每个断言均可被实现退化（全 true / 全 false）证伪，且 `premiseHeld>0` / `Global ⊄ s` 等防止前提空集退化。
- 8 标签全覆盖（Method/Type/Scene/Global/Loop/Conditional/Async/Shell）；实现侧 `IncludedIn` 经 `Equals(other) → true`（自反）、`other is Global → true`（最大元）、其余 `false`（跨标签不可比），与测试断言逐条吻合。
- 无死断言、无恒真充数、§ 出处齐备。
- 终止判定：**可终止**（0 open）。
