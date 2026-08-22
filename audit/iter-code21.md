# 迭代21 审计（端到端集成）

## 摘要
- 测试：本机 `dotnet test --filter FullyQualifiedName~EndToEndTests` ⇒ **5 通过 0 失败**（已核实，net10.0）。
- 全局：此前 146 通过 0 失败（父线程已核实）。
- open 项总数：**0**（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + 被测 + PDR § + 真闭环? + 假绿? + 结论）

| 项 | 行号 | 被测 | PDR § | 真闭环? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 balanced⇒过+守恒 | 测试 L115-118 / L125-153 | Generator.cs L74-99（GenerateMethodSignature 真 emit `Compute{m}`+`GodotApiWhitelist.All`+`Signature.Union`）；Analyzer.cs L116（hasAcquire&&!hasRelease 才报）；ApiMapping.cs L66-71（AddChild=occupy create `Tree("node.id")`、RemoveChild=occupy release `Tree("node.id")`） | §7.1 / §8.1 / §3.3.1 / §14 L2/L3 | 真：a) `Balanced_Body_NoAnalyzerWarning` 跑真 analyzer（`RunAnalyzer`）返回 0 个 EAA0901；b) `GeneratedSignature_Union_IsConservedViaL1` 经 `EmitWithGenerator` **反射运行生成代码**（`ComputeAddChild`/`ComputeRemoveChild`，非 grep），再 `Signature.Union` 后 `NetTable.Compute(..,Global()).IsConserved(Tree("node.id"))`=true。`EmitWithGenerator` L? 含 `Assert.Empty(diags)`+`Assert.True(emit.Success)` ⇒ 生成代码**真编译真运行**（非桩）。 | 否 | OK。守恒断言是真锁：若 §7 配对破坏或 L1 有符号 net 失效（iter05 OPEN-1 旧 bug 回归），此断言必红。 |
| 2 unbalanced⇒EAA0901 | 测试 L158-172 | Analyzer.cs L107-116（AcquireApiNames.Contains canon 且 !ReleaseApiNames.Contains ⇒ 报）；ApiMapping.cs L66-68（AddChild 有 Mode.Create ⇒ `addchild`∈AcquireApiNames） | §3.3.1 DO-9 | 真：`Leak()` 体内仅 `AddChild(new object())`（裸标识符 → `addchild` 命中 acquire，无 release）⇒ `Assert.Contains(EAA0901)` 命中。非 trivial（确实触发报告分支）。 | 否 | OK |
| 3 override 豁免 | 测试 L177-192 | Analyzer.cs L90-93（`hasEscape`=标 [EffectOverride]/[AcceptDeviation] ⇒ 提前 return 不报） | §8.3 / §8.3.1 | 真：`Temp()` 仅 `AddChild` 但标 `[EffectOverride("...")]`（reason 非空，L1 构造子强制）⇒ `hasEscape=true` 跳过 ⇒ `Assert.DoesNotContain(EAA0901)`。 | 否 | OK |
| 4 生成真委托 L1 | 测试 L198-218 / 辅助 L? `RunGenerator` | Generator.cs L96（`foreach (var m in GodotApiWhitelist.All)`）、L99（`Signature.Union(s, Signature.Of(c))`）、L89（`Compute{m}`） | §14 L2 | 真：`RunGenerator` 跑真 generator，`Assert.Contains("GodotApiWhitelist.All")`+`"Signature.Union"`+`"ComputeAddChild"`；再 `EmitWithGenerator` 反射 `ComputeAddChild` 返回非空 Signature 且 `AllClaims` 含 `Kind.Occupy`。生成代码确含 §7 白名单引用与 L1 代数委托，非桩。 | 否 | OK |
| 5 可证伪 | 跨测试 | — | — | 真：a) 若 generator 退化为桩（不 emit `GodotApiWhitelist.All`/`Signature.Union`）⇒ 测试 1/4 的 `Assert.Contains` 必红；b) 若 analyzer 不报 ⇒ 测试 2 `Assert.Contains(EAA0901)` 必红；c) 若 §7 配对断裂或 L1 net 符号抵消失效 ⇒ 测试 1 的 `IsConserved` 必红（iter05 已证此锁可捕获回归）。 | 否 | OK。无恒真断言，每断言对应可证伪定律。 |
| 6 假绿扫描 | 全局 | — | — | 测试用 stub 类型 `Enemy`/`Leaker`/`Intended` 方法体调用均为**裸标识符**（`AddChild(child)`/`RemoveChild()`），与 §7 白名单 canonical 键（`addchild`/`removechild`）对齐 ⇒ analyzer 真匹配、非静默漏报。无 `[Fact]` 无断言：5 个 `[Fact]` 均含断言（DoesNotContain/Contains/True/NotNull/NotEmpty）。无 `Assert.True(true)`。 | 否（stub 名与白名单一致，无假绿） | OK。测试头部 L11-32 已诚实声明「按方法名规范化匹配 §7 键；用 ComputeAddChild∪ComputeRemoveChild 验证守恒，而非虚构 Compute_SpawnAndDespawn（其名不匹配任何 §7 键会生成空签名致假绿）」——主动规避假绿设计已落地。 |
| 7 出处注释 | 测试 L1-33 / L114 / L124 / L157 / L176 / L197 | — | §7 / §8.1 / §3.3.1 / §14 | 真：类级摘要引 §14 L2/L3 + §3.3.1 + §7/§8.1；各 `[Fact]` 方法级 `/// <summary>` 均引 §14/§3.3.1/§8.3。 | 否 | OK |

## open 项清单
无（0 项）。

## 补充观察（越权越界说明，非 iter-21 open，不阻塞终止）
- **L3 Analyzer 接收者前缀 canonical 名不匹配（既存 L3 层问题，非本测试迭代范围）**：`EffectAlgebraAnalyzer.cs` L25-26 类注释声称「带接收者前缀（如 `node.QueueFree()`）与裸调用（如 `QueueFree()`）均被 canonical 名匹配」，但实现 `RawName`（L131-135）对 `node.QueueFree()` 返回 `node.QueueFree` → `Canonical` ⇒ `nodequeuefree`，而 `ReleaseApiNames` 由 `Canonical("QueueFree")` ⇒ `queuefree`；二者不等 ⇒ **带接收者前缀的 intra-method 调用实际不被匹配**（静默漏报 false negative）。此为 analyzer 内部 comment/code 不一致，且是 iter-09 OPEN-2「接收者前缀漏报未诚实注释」的遗留/回归——但 iter-21 测试源码均用裸标识符，未触发该路径，故**不影响本 5 个端到端测试的真闭环、不构成假绿**，按「只审计测试代码、不写测试」授权边界，不计入 iter-21 open。建议后续 L3 迭代修正 `RawName`：对 `MemberAccessExpressionSyntax` 仅取 `ma.Name.Identifier.Text`（去接收者）再 `Canonical`，并修正 L25-26 注释使其与实现一致。

## 结论
- 四项端到端目标**真闭环且非假绿**：balanced（acquire+release 同资源 `Tree("node.id")`）⇒ analyzer 不误报 + 生成 L1 Signature 经 `NetTable.IsConserved` 确守恒（反射运行生成代码，非 grep）；unbalanced（仅 acquire）⇒ analyzer 真报 EAA0901；`[EffectOverride]` 真豁免；生成代码真委托 L1（`GodotApiWhitelist.All`+`Signature.Union`，非空 Signature 含 Occupy）。
- 可证伪性充分：generator 退化/analyzer 不报/§7 配对断裂/L1 net 符号失效 任一回归均会使对应断言必红（无 `Assert.True(true)`、无空 `[Fact]`）。
- 测试头部诚实声明「按方法名规范化匹配 §7 键、用 ComputeAddChild∪ComputeRemoveChild 验证守恒而非虚构空签名方法」——主动规避了潜在假绿。
- 唯一越界观察为 L3 analyzer 接收者前缀 canonical 匹配缺陷（既存、未触发于本测试、非假绿），列为后续 L3 迭代建议，不阻塞 iter-21 终止。
- 终止判定：**可终止**。
