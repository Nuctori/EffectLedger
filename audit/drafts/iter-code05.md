# 迭代05 审计（§3.3 派生度量）

## 摘要
- 构建：父会话独立核实 **0 错误 0 警告**（本子代理 fork 环境无 pwsh/dotnet，未重跑，沿用交接状态）。
- open 项总数：**3**（可闭 3 / 设计 out-of-scope 0）。均为 §3.3.1/§3.3.2 数学语义未真落实，非注释类小修。
- 终止判定：**需继续（3）**——`LoopCount`/`Combination`/`Derived` 委托链、出处注释、⊤ 类型兜底均 OK，但 net 有符号语义被静默破坏（NegateCore 不取负）、守恒判定过严、Peak 漏 `mode≠release` 过滤。
- 总评：架构层（类型字段承载 ⊤、委托链清晰、注释 § 出处完整、cardinality 旧形式已废弃标注）落实良好；但 §3.3.1 的「减法使 net 可负/可零」核心不变量未进类型/代码，导致 DO-9 会产生**误报泄漏**。

## 逐条核对（回指行号 + PDR § + 结论）

| 检查 | 代码行 | PDR § | 类型真约束? | 结论 |
|---|---|---|---|---|
| 1 LoopCount 载体 | DerivedMetrics.cs L9 | §3.2.5 | 真：`readonly record struct`；`Count:NatStar`(L14)；`Of(ulong)`(L17)；`Top`(L20) | OK |
| 1 ω=⊤ 类型强制 | L20 `NatStar.Top` | §3.2.5 | 真：`ω=⊤` 经 `NatStar.IsTop` 字段检测，无魔法数 | OK |
| 2 Combination.Loop ω=⊤ | L69-71 `Scale` | §3.2.5 | 真：`if (w.IsTop) return new Interval(s.Lo, NatStar.Top)`（上界开放） | OK |
| 2 Loop 真用 ω.Count.IsTop | L69 `if (w.IsTop)` | §3.2.5 | 真：判定来自 `ω.Count.IsTop`，非常量 | OK |
| 2 Loop ω 有限等价 ω 次合并 | L46-49, L72 | §3.2.5 | 真：每 Claim `Size` 按 ω 缩放 `new Interval(s.Lo*w, s.Hi*w)`，再 ∪；与 Σ copy_i 等价 | OK |
| 2 Loop re-scope | L46-49 `Scope = loopScope` | §3.2.5 | 真：副本 scope 重标注为 `loopScope` | OK |
| 3 Sequence/Parallel | L52, L57 | §3.2.1/§3.2.2 | 真：均 `= Signature.Union`（半格并，幂等/交换/结合） | OK |
| 4 Derived.Peak 委托 | L99 | §3.3.2 | 真：`=> Peak.Compute(s,scope)`，未自造 | OK |
| 5 Derived.Net 委托 | L103 | §3.3.1 | 真：`=> NetTable.Compute(s,scope)` | OK |
| 5 Derived.IsConserved 委托 | L107 | §3.3.1 DO-9 | 真：`=> Net(s,scope).IsConserved(r)` | OK |
| 6 Peak.Compute 与 §3.3.2 | Algebra.cs L101-114 | §3.3.2 | **部分**：size 求和 + 任一 ⊤⇒⊤ 正确，但**未过滤 `mode≠release`** | OPEN-3 |
| 6 NetTable.Compute 仅 occupy | L57 `if (c.Kind != Kind.Occupy) continue;` | §3.3.1 | 真：量纲隔离落实 | OK |
| 6 cardinality 残留 | 全局 | §3.2.5 | 无：Peak 用 size 求和，旧 cardinality 仅注释标注废弃 | OK |
| 7 注释/出处 | 各方法类级 + 行尾 | §3.2.5/§3.3.1/§3.3.2 | 完整：LoopCount/Combination/Derived/Peak/NetTable 均带 § 出处 | OK |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | Algebra.cs L66-69 `NegateCore` | §3.3.1 定义 `net = Σcreate c.size − Σrelease c.size`（有符号，可为负/零）。但 `NatStar` 为 ℕ*（非负），`NegateCore` 实现 `new Interval(NatStar.Of(s.Hi.Value), NatStar.Of(s.Lo.Value))` 对 `[1,1]` 产出 `[1,1]`——**未取负**，仅转置，且因 ℕ* 无法存负数而恒为非负。注释写 `[−hi, −lo]` 与代码矛盾（与 iter-code01 采用的「注释/代码一致性」判据同标准，此处亦冲突）。后果：任意 `create+release` 配对 net 算成 `[1,1]`（而非 `[-1,1]`），net 永不为负 ⇒ DO-9 对**正确闭合的配对误报泄漏**（soundness 破坏）。 | 可闭（需新增有符号类型） | **高** | 引入有符号网值类型，如 `ZStar = long ∪ {⊤}` 及 `SignedInterval`，或 `NetTable` 内部用 `long` 累加（release 取负）；`NegateCore` 输出真正负值区间；使 `create[1,1]+release[1,1] ⇒ [-1,1]`，DO-9 可判为含 0（见 OPEN-2）。类型强制符号边界，避免 ℕ* 静默截断。 |
| OPEN-2 | Algebra.cs L85-86 `IsConserved` | 当前 `loLeZero = v.Lo.Value == 0; hiGeZero = v.Hi.Value == 0; return loLeZero && hiGeZero;`——要求 **lo 与 hi 同时精确等于 0** 才判守恒。但 §3.3.1/DO-9 的「无泄漏」判据应是「net 区间**含 0**」即 `lo ≤ 0 ≤ hi`（含 0 = 可能闭合）。即便修好 OPEN-1 后，平衡配对 `[-1,1]` 因 `lo=-1≠0` 仍被判 `false`（误报）。且对 `create[1,2]+release[1,1] ⇒ [-1,2]`（确可能闭合）同样误报。 | 可闭 | **高** | 改为 `bool containsZero = v.Lo.Value <= 0 && v.Hi.Value >= 0; return containsZero;`（在 `!IsTop` 分支内）。`⊤` 分支维持 `return false`（fail-closed，已 L84 正确）。注释同步「区间含 0 ⇒ 可能闭合，不报警」。 |
| OPEN-3 | Algebra.cs L104-113 `Peak.Compute` | §3.3.2 公式显式 `c.mode≠release`：`Peak = max_i Σ_{c, scope⊆scope, c.mode≠release} c.size`。但实现对所有 kind/mode 求和（`foreach (var c in sig.AllClaims())` 仅过滤 `Scope.IncludedIn`），**未排除 `Mode.Release`**。release 是释放、不贡献并发占用峰值，纳入会高估 Peak（甚至使本应触顶的 ⊤ 判定延迟）。 | 可闭 | 中 | 循环内加 `if (c.Mode == Mode.Release) continue;`（§3.3.2 `c.mode≠release`）。注意：kind 不限制（§3.3.2 公式未限 kind），故仅按 mode 过滤即可。 |

## 补充（非 open，已确认）
- `LoopCount`/`Combination`/`Derived` 三层委托链自洽：`Derived.Peak/Net/IsConserved` 均真委托 `Peak.Compute`/`NetTable.Compute`/`NetTable.IsConserved`，无重复实现、无魔法数。
- ω=⊤ 经 `NatStar.IsTop` 一路兜底：`Scale`→`[lo,⊤]`、`Peak.Compute`→`NatStar.Top`、`NetTable` 的 `NegateCore` ⊤ 分支→`[⊤,⊤]`→`IsConserved` 返回 false（fail-closed）。该链路类型强制，无 NaN/发散路径。
- 旧 cardinality 形式（`|{c∈copy_i}|` 计数 Peak）仅作为废弃注记出现，代码中无任何 claim-count 残留逻辑。
- §3.2.5 循环组合与 §3.3.2 Peak 的 ω 缩放一致：finite ω 缩放 size、⊤ 开放上界，Peak 求和即 `ω × Σ size`，与「ω × 作用域过滤 × size 求和」定义吻合。

## 结论
- 架构、类型边界载体（⊤ 进 `NatStar.IsTop`）、委托链、§ 出处注释、cardinality 废弃标注——均**真落实**，无假绿、无注释吹。
- 但 §3.3.1 的**有符号 net 不变式被静默破坏**：`NegateCore` 在 ℕ* 下无法取负（OPEN-1），连带 `IsConserved` 的「精确 0,0」判据（OPEN-2）与 `Peak` 漏 `mode≠release` 过滤（OPEN-3）共同使 DO-9 对正确闭合配对误报、Peak 高估。三处均可在 L1 内闭（OPEN-1 需新增 `ZStar`/有符号网值类型，属已批准类型边界范畴，非产品决策）。
- 终止判定：**需继续（3）**——修复 OPEN-1/2/3 后，§3.3 派生度量方达「类型约束有符号边界 + 数学公式真落实 + DO-9 不误报」严格标准。
