# 迭代11 审计（§14 验证矩阵）

## 摘要
- 测试：57 通过 0 失败（已核实 `dotnet test`：失败 0 / 通过 57 / 跳过 0，时长 ~695ms；本文件 12 个 `[Fact]` 全含于其中）。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。

## 逐条核对（回指行号 + PDR § + 真验证? + 假绿? + 结论）

| 项 | 测试行号 | 被测行号 | PDR § | 真验证? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1 DO-1 单值⇒Default | L21-25 `DO1_SingleValueIntervalDefaultsToOne` | `Numeric.cs` L80 `Default=[1,1]`；`Objects.cs` `Claim.Normalize`（`Size==default ⇒ Interval.Default`） | §3.1.5(d) | 真：`c` 用 `default` size ⇒ `Normalize().Size == Interval.Default == Interval.Exact(1)`（[1,1]）；若 `Normalize` 不填 default 则 `== [0,0]`⇒断言失败 | 否 | OK |
| 2 DO-3 ⊤ 不触发 0.2 | L29-33 `DO3_TopDoesNotTriggerAlarm` | `Numeric.cs` L161 `ExceedsThreshold => !IsTop && Value > threshold` | §3.1.5c / §9.1 | 真：`Top⇒false`、`Of(0.5)⇒true`、`Of(0.1)⇒false`，三向覆盖阈值两侧 + ⊤；若实现误把 ⊤ 当数值比较则必失败 | 否 | OK |
| 3 DO-6 栈/堆分离 | L37-56 `DO6_ResourceTypeIsolation` | `Algebra.cs` `NetTable.Compute` 按归一化资源键分组；`Objects.cs` `ResourceId` 各构造子结构相等 | §3.1.2 / §3.3.1 | 真：Tree/Memory/Gpu 各 create+release 配对，断言每资源独立 `IsConserved` 且键集恰好 3（互不串桶）；若 `Net` 误合并跨资源类型则 `set.Count` 错 | 否 | OK |
| 4 DO-7 量纲隔离 | L60-79 `DO7_DimensionalIsolation_NetOnlyOccupy` | `Algebra.cs` L57 `if (c.Kind != Kind.Occupy) continue;` | §3.3.1 / §3.1.4b | 真：同资源同 scope 放 read+write+occupy，断言 net 键集 `Single`（read/write 被丢弃）；`Get(res)==[1,1]`、`IsConserved false`；sig2 加 read 后 create+release 仍 `IsConserved true`（read 不污染 net）。若 L57 不过滤 kind 则键集会含 read/write ⇒ 断言失败 | 否 | OK |
| 5 DO-8 单点真相 | L84-90 `DO8_SingleSourceOfTruth_Normalize` | `Objects.cs` `ResourceId.Normalize`：`Self("signal_"+s)⇒SignalBus(s)`；`SignalBus` 原样 | §3.1.4a (ST-02) | 真：幂等 `Normalize(Normalize(r))==Normalize(r)`；`Self("signal_x")==SignalBus("x")`；若归一不收敛则双向不等 | 否 | OK |
| 6 DO-9 守恒 | L94-103 `DO9_Conservation_AcquireReleaseClosed` | `Algebra.cs` `NetTable.IsConserved` → `ContainsZero`（`SignedNet.cs` L~95 `Lo<=0 && Hi>=0`） | §3.3.1 / §3.1.4a | 真：create+release ⇒ `[−1,1]` 含 0 ⇒ `IsConserved true`；仅 create `[1,1]` ⇒ false（fail-closed）。若 `ContainsZero` 误判或净效应符号错则必失败 | 否 | OK |
| 7 DO-10 自洽 | L106-122 `DO10_Compatible_TotalAndSymmetric` | `Algebra.cs` L~24-39 `Compatible.IsCompatible`（全函数 + 对称 + P3 良性配对 + CONFLICT 集） | §3.2.3 | 真：5×5 穷举断言「返回 bool（全函数不抛）」+「对称」；显式 CONFLICT(Create,Create)/(Move,Move)/(Release,Release)⇒false；P3 良性 (Create,Release)/(Release,Create)⇒true（双向）。若实现漏对称或误判冲突则捕获 | 否 | OK |
| 8 A1 零 Godot | L126-131 `A1_ZeroGodotDependency` | `Claim` 来自 `Cosmos.EffectAlgebra` 程序集（编译即证零 Godot 引用） | §2 L1 / DO-2 | 真：`typeof(Claim).Assembly.GetName().Name == "Cosmos.EffectAlgebra"`；若 L1 误引 Godot 则编译失败，此断言冗余但为架构不变性显式证据；运行期 Godot 引用检查已注释为 out-of-scope（§14.3 S3），诚实 | 否（断言非恒真：换程序集名即失败） | OK |
| 9 A2 类型即约束 | L134-139 `A2_TypesEncodeBounds_IllegalIntervalThrows` | `Numeric.cs` L73 `Interval` 构造子 `lo.IsTop && !hi.IsTop ⇒ throw` | §3.1.5 | 真：`new Interval(NatStar.Top, NatStar.Of(5))` 抛 `ArgumentException`；合法 `[1,⊤]`/`[⊤,⊤]` 不抛。若构造子不校验则误通过 | 否 | OK |
| 9 A2（续）资源非空 | L142-149 `A2_TypesEncodeBounds_ResourceNotNullAfterNormalize` | `Objects.cs` `Claim` record（resource 非空引用类型，构造子静态禁止 null）；`Normalize` | §3.1.4a | 真：`n.Resource != null` 且 `IsType<Memory>`；类型边界「不可 new null resource」由 record 非可空参数保证，测试仅作证据。非假绿（确断言非 null + 类型） | 否 | OK |
| 10 A3 白名单完整 | L152-168 `A3_WhitelistComplete` | `ApiMapping.cs` `GodotApiWhitelist.All`（38 项）；`ReleaseClass.Names` L~181（7 项） | §7 / §8.1 | 真：`All.Length>0` + 逐条 `Claims.Length>0`（每条映射须带非空 Claim）；`ReleaseClass.Names.Count==7` 且 7 名全在。若任一映射缺 Claim 或 release-class 漏项则失败 | 否 | OK |
| 11 A4/A5 可复现 | L172-181 `A4_NoMagicNumbers_CommentCarriesResidual` | `Numeric.cs` L161 `ExceedsThreshold` | §14 A4 / A5 | 真：`new Random(14)` 固定种子；50 次断言 `d.Value>thr == d.ExceedsThreshold(thr)`（单调性）+ `Top` 恒不报警；阈值 `0.2` 抽为 `const` 并注 §9.1，无散落魔法数。若 `ExceedsThreshold` 实现错则 50 组必失败 | 否 | OK |
| 12 假绿扫描 | 全局 12 个 `[Fact]` | — | — | 无 `[Fact]` 无断言；无 `Assert.True(true)`；无恒真断言（A1 断言为程序集名比较，非恒真）。每断言对应可证伪行为 | 否 | OK |
| 13 出处注释 | 各方法签名/类注释 | — | §14 + x.y | 真：DO-1 §3.1.5(d)、DO-3 §3.1.5c/§9.1、DO-6 §3.1.2、DO-7 §3.3.1/§3.1.4b、DO-8 §3.1.4a、DO-9 §3.3.1/§3.1.4a、DO-10 §3.2.3、A1 §2/DO-2、A2 §3.1.5、A3 §7/§8.1、A4 §14，均带 § 出处 | — | OK |

## open 项清单
无。0 open。

## 结论
- §14 验证矩阵的可自动化项（DO-1/3/6/7/8/9/10 + A1/A2/A3/A4/A5）**真验证于 L1 实现**：每个断言对应可证伪的代数定律/类型边界，若实现未落实对应行为则断言必失败或构造子抛异常（上表逐行标注触发路径）；无假绿、无恒真断言、无散落魔法数。
- 不可达项（真实 Godot 行为、反射/动态注入边界）以注释显式声明 out-of-scope、权威在 L1 定义，未硬编伪断言（A1 注释、类注释 S2/S3 残差诚实）。
- 类型即约束（A2）真正由 `Interval`/`Claim` 构造子强制（非法区间抛、`resource` 非空引用类型），测试仅作架构证据，符合「类型系统能约束的用类型」铁律。
- 终止判定：**可终止**——矩阵可自动化项全真验证、不可达项诚实、零假绿、零技术债。
