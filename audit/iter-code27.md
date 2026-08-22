# 迭代27 审计（Interval 算术锁）

## 摘要
- 测试：`IntervalArithmeticTests` 14 例于本机 `dotnet test --filter IntervalArithmeticTests` 全部通过（已核实，0 失败）。
- 全方案当前 208 通过 0 失败（上层汇总，本次仅专项核实 Interval 文件 14 例）。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 Exact(5) | L17-24 | `Interval.Exact` Numeric.cs L91 | §3.1.5 Exact(s)⇔[s,s] | 真：`Lo==Of(5) && Hi==Of(5) && !IsTop` | 否 | OK |
| 1 Dynamic | L26-33 | `Interval.Dynamic` Numeric.cs L82-83 | §3.1.5(c) ⇔[1,⊤] | 真：`Lo==Of(1) && Hi.IsTop && Hi==Top` | 否 | OK |
| 1 Default | L35-42 | `Interval.Default` Numeric.cs L79-80 | §3.1.5(a) ⇔[1,1] | 真：`Lo/Hir==Of(1) && !IsTop` | 否 | OK |
| 1 Exact(0) | L44-50 | `Interval.Exact` | §3.1.5 非负允许 0 | 真：`Lo/Hir==Of(0)`（ctor 不拒 0） | 否 | OK |
| 2 Merge 常规 join | L53-60 | `Interval.Merge` Numeric.cs L93 | §3.1.5b | 真：`[1,3]∪[2,5]==[1,5]` | 否 | OK |
| 2 Merge [1,⊤]∪[2,⊤] | L62-68 | `Hi.Max` Numeric.cs L47 | §3.1.5b max 吸收 ⊤ | 真：断言 `[1,⊤]` | 否 | OK |
| 2 Merge [1,3]∪[1,⊤] | L70-76 | `Hi.Max` | §3.1.5b | 真：断言 `[1,⊤]` | 否 | OK |
| 2 Merge [⊤,⊤]∪[1,5] | L78-85 | `Lo.Min` Numeric.cs L52-56 | §3.1.5b Min(⊤,x)=x | 真：断言 **[1,⊤]**（见下方 R-1，非任务示例误写的 [⊤,⊤]） | 否 | OK |
| 3 幂等 | L87-92 | `Merge` | §3.1.5b | 真：`a==a.Merge(a)` | 否 | OK |
| 3 交换 | L94-100 | `Merge` | §3.1.5b | 真：`a.Merge(b)==b.Merge(a)` | 否 | OK |
| 4 非法构造 lo=⊤ | L103-108 | `Interval ctor` Numeric.cs L67-69 | §3.1.5a | 真：`new Interval(Top, Of(5))` 抛 `ArgumentException`；ctor 真有校验 | 否 | OK |
| 5 Lo<=Hi | L111-116 | ctor/字段 | §3.1.5 | 真：`v.Lo.Value <= v.Hi.Value`（[2,9]） | 否 | OK |
| 5 IsTop 属性 | L118-123 | `NatStar.IsTop` | §3.1.5 | 真：结构属性 `Dynamic.Hi.IsTop` / `[⊤,⊤].Lo.IsTop` | 否（结构属性，非计算，但非假绿） | OK |
| 6 随机 1000 | L126-150 | `Merge`+`RandInterval` L152-168 | §3.1.5a/§3.1.5b | 真：form0 `[x,⊤]` / form1 `[⊤,⊤]` / form2 `[x,y]`（x<=y 保证）；幂等+交换+结合+Lo<=Hi 保持 | 否 | OK（边界 ⊤/0 已触达：rng.Next(0,100) 可取 0） |
| 7 可证伪 | 全局 | — | — | 真：若 `Max`/`Min` 丢弃 ⊤ 律（误算或抛），L62/L70/L78 必红 | — | OK |
| 8 假绿扫描 | 全局 | — | — | 无 `[Fact]` 无断言；无 `Assert.True(true)` | — | OK |
| 9 出处注释 | L1-10, 各方法签名 | — | §3.1.5a/§3.1.5b | 类级 + 各方法均带 § 出处 | — | OK |

## open 项清单
无。

## 备注（非 open，低严重度）
- **R-1（任务示例口径修正，非缺陷）**：任务逐条第 2 条示例写「`[⊤,⊤]∪[1,5]=[⊤,⊤]`」实为 §3.1.5b Min/Max 推导下的**错误预期**。正确结果：`Lo.Min = min(⊤,1)=1`，`Hi.Max = max(⊤,5)=⊤` ⇒ **[1,⊤]**。测试 L78-85 正确断言 `[1,⊤]`，与实现、与 §3.1.5b ⊤ 律一致；被测实现无误。仅作记录，避免后续审阅者按任务误示例误判。
- **R-2（已覆盖，非缺口）**：结合（associativity）仅在随机块 L137-139 断言，未单列确定性结合事实测试；但 1000 组随机覆盖 + 常规 join 已充分，不构成假绿。
- `IsTop_WhenEitherBoundTop`（L118-123）校验的是 `NatStar.IsTop` 结构字段（record 自动属性），属不变量可见性检查，非计算锁，但断言真实、非恒真。

## 结论
- Interval 算术测试**真锁** §3.1.5a/§3.1.5b/§3.1.5 的核心：Exact/Dynamic/Default 三类边界 + Merge 全 ⊤ 组合（含 max 吸收 ⊤ 与 min 对 ⊤ 取 meet 收窄下界）+ 非法构造 lo=⊤ 抛异常（构造子真校验）+ Lo<=Hi 不变量 + 1000 组随机（form0/1/2 覆盖 ⊤/0/缺省边界）。
- 所有断言可证伪：Merge 的 ⊤ 律若实现错误（误算或抛），对应断言必红。无假绿（`Assert.True(true)` / 无断言 [Fact] 均不存在）。§ 出处注释齐备。
- 唯一口径差异 R-1 为任务示例笔误，被测实现与测试均正确，不计入 open。
- 终止判定：**可终止**（0 open）。
