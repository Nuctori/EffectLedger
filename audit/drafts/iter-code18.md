# 迭代18 审计（§7↔§8.1 跨表一致性）

## 摘要
- 测试：实测 `dotnet test --filter FullyQualifiedName~CrossTableTests` → **4 通过 / 0 失败**（已于本机独立运行核实，非引述父摘要）。
- 被测：`src/Cosmos.EffectAlgebra/ApiMapping.cs` 的 `GodotApiWhitelist.All`（L44–170）与 `ReleaseClass.Names`（L183）。
- open 项总数：**1**（可闭 1 / 设计 out-of-scope 0）。
- 终止判定：**需继续(1)** —— 闭 OPEN-1（KnownReleaseSemanticsCanon 重列 7 字符串与自身注释/测试名自相矛盾）后即达「数据驱动派生、无重复、无假绿」严格标准。

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 测试行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 §8.1⇒§7 release-class 在白名单者必含 Release | L32–58 | ReleaseClass.Names (L183)；QueueFree(L72-74) RemoveChild(L69-71) Disconnect(L120-122) | §8.1 / §7.1 / §7.5 | **真（部分）**：对白名单确有条目的 3 个（`queue_free`/`remove_child`/`disconnect`）→ 真 `Assert.Contains(Claims, Mode.Release)`；若误标 Create 必红。其余 4 个（`free`/`remove_from_group`/`cancel_free`/`free_children_in_group`）在 §7 白名单无条目 → 落软约束分支（注释明言「Analyzer 按名匹配」），不红，合理 | 软分支含 `Assert.True(true)`/`Assert.All(missing,_=>{})` 但均**已注释为软约束**，非充数 | OK（除 OPEN-1 派生缺口见下） |
| 2 §7⇒§8.1 白名单 Release 孤儿=0 | L60–79 | 遍历 GodotApiWhitelist.All，凡 `Mode==Release` 者 canonical 须 ∈ ReleaseClassCanon ∪ KnownReleaseSemanticsCanon | §7 / §8.1 | **真**：实枚举白名单 Release 型 5 个（removechild/queuefree/disconnect/audiostop/animstop）逐一比对；`Assert.Empty(orphans)` 真断言，若新增未解释 Release API 必红 | 否：`orphans` 由真实枚举填充，非空遍历 | OK |
| 3 QueueFree 收口 mode=release | L81–86 | GodotApi=="QueueFree" (L72)，Claims L73-74 含 Mode.Release | §7.1 / iter27 | **真**：`First(m=>GodotApi=="QueueFree")` + `Assert.Contains(Release)`；若误改 Create 必红 | 否 | OK |
| 4 无魔法数 / 不重复 7 字符串 | L88–97 | ReleaseClass.Names (L183)；KnownReleaseSemanticsCanon (L24-29) | §8.1 | **部分**：`Assert.Equal(7, ReleaseClass.Names.Count)` 读真实数据仅比基数（非重列 7 串），OK；但 `KnownReleaseSemanticsCanon` **自身手写了 7 个 release-class 串 + Audio.Stop + Anim.Stop**（L24-29），与自身注释「由该集合 union … 派生」及本测试名「NoHardcodedSevenStrings」**自相矛盾**——见 OPEN-1 | 否（Contains 真断言） | **OPEN-1** |
| 5 假绿扫描 | 全局 | — | — | 无恒真 `[Fact]` 空断言；唯一 `Assert.True(true)` 在 L50/L57 软约束分支，已注释 | — | OK（软约束已声明） |
| 6 出处注释 | L32/L60/L81/L88 各方法 XML | — | §7.1 / §8.1 / iter27 | 真：均带 § 出处 | — | OK |

## open 项清单

| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | CrossTableTests.cs L24–29 `KnownReleaseSemanticsCanon` | 注释称「§8.1 Names 与 §7 显式释放方法派生 / 由该集合 union 派生」，测试名 `NoHardcodedSevenStrings` 亦宣称「不许手写重复 7 字符串」。但实现是**手写的 9 串字面量数组**，其中 7 个（`queue_free`/`free`/`remove_child`/`disconnect`/`remove_from_group`/`cancel_free`/`free_children_in_group`）与 `ReleaseClass.Names`(L183) **逐字重复**。`ReleaseClassCanon`(L19-21) 已正确从 `ReleaseClass.Names.Select(Canonical)` 派生，二者处理不一致。后果：若 §8.1 release-class 增删改名，`KnownReleaseSemanticsCanon` 会**静默漂移失同步**，而本迭代声称守护的「跨表一致性」恰恰依赖该集合与 §8.1 同源——自相矛盾、构成潜在技术债（用户铁律「不留技术债」）。 | 可闭（数据派生） | 中 | 改为从权威源派生：`ReleaseClass.Names.Select(Canonical).ToImmutableHashSet().Union(new[]{"Audio.Stop","Anim.Stop"}.Select(Canonical))`；删除手写的 7 串字面量。语义集合不变（仍是 9 个），但消除重复、与 `ReleaseClassCanon` 同源、与注释/测试名一致。 |

## 结论
- 跨表一致性**核心守护为真**：§8.1⇒§7（有白名单条目的 3 个 release-class 强制含 `Mode.Release`）、§7⇒§8.1（`Assert.Empty(orphans)` 真锁无孤儿 Release API）、QueueFree 收口（`Mode.Release` 硬断言）、§ 出处注释齐备、无恒真充数断言。非假绿。
- 唯一 open（OPEN-1）为**测试自身数据派生缺陷**：`KnownReleaseSemanticsCanon` 重复手写 7 串，与自身注释及 `NoHardcodedSevenStrings` 测试名自相矛盾，且会在 §8.1 变更时静默失同步。不影响当前运行时正确性（集合内容当下一致），但属应闭技术债。
- 终止判定：**需继续(1)** —— 闭 OPEN-1（派生而非手写）后即「数据驱动派生 + 无重复 + 无假绿 + § 出处精准」可终止。
