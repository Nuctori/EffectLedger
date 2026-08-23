# Effect Algebra 类型系统对抗审计（Rich Hickey「illegal state unrepresentable」透镜）

**审计视角**：Rich Hickey 类型驱动正确性 —— 目标不是「能表达所有合法状态」，而是「**无法构造任何非法状态**」。
**立场（任务要求）**：宁误报「类型放过」。本报告对每个类型/符号给出「非法状态可表达性」判定。
**范围**：仅 `src/Cosmos.EffectAlgebra/` 下 7 个文件（`EffectScript.cs` / `EffectScriptContract.cs` / `Objects.cs` / `Numeric.cs` / `Algebra.cs` / `SignedNet.cs` / `DerivedMetrics.cs`）。
**方法**：全量通读源码自行推导；未读取 `audit/` 下任何历史报告。

---

## 0. 独立声明（总立场）

本子系统的类型系统**在「代数式载体」层面做得相当好**：`NatStar`/`ZStar`/`Interval`/`SignedInterval` 把 `⊤`、负值、越界都做成**类型可见的字段**（`IsTop`），并把 `lo≤hi` 不变量放进构造子抛异常。这是真·非法状态不可表达的好样本。

**但**在「**领域语义层**」（资源同质隔离、作用域偏序、量纲隔离、Claim 合法性、归一化不变量）类型系统**大量地把错误推迟到了 Audit 运行时或人工期**：

1. `ResourceId` 的 `record` 标签隔离是**形状隔离**，不是**同质隔离**——跨构造子可携带同义/可混淆字段（`Memory(0)` 哨兵、`CommandBuffer("gpu")` 常量、所有 `Normalize` 塌缩），类型无法阻止「同质资源被当成异质」或「异质资源被当成同质」。
2. `ScopeId` 偏序 `IncludedIn` 是**运行时布尔**，不是类型；跨标签比较「返回 false 兜底」，非法比较**可表达且不报错**。
3. `Signature` 三桶是**运行时分桶 + 注释契约**，跨桶聚合（`Weight`）用 `double.NaN` 约定编码 `⊥`，类型无法阻止 `read+occupy` 被当成可加。
4. `Claim` 的**归一化后条件**（`Resource == Normalize(Resource)`、`Size != null`）类型无法表达；原始五元组可构造出「非规范/无意义」状态而静默等到审计期才暴露等价塌缩。
5. `NatStar.Of(ulong)` **不检测回卷**——回卷检测靠 `operator+/*` 的事后比较完成；在裸 `ulong` 算术路径（如 `maxFinite + 1`）类型完全放手，回卷静默发生。

**结论（总评见 §3）**：类型系统在**算术载体**上足够阻止错误；在**领域语义**上不足，约 **9 个**「类型放过、只能在 Audit 运行时/人工期才发现」的隐患，其中 2 个（资源同质混淆、归一化不变量）会**静默破坏审计结论的正确性**而非仅仅报错。

---

## 1. 逐类型 / 符号表

判定列：**可表达** = 该非法状态可被类型系统构造出来（类型放过）；**不可表达** = 构造子/类型强制阻止。

| # | 类型 / 符号 | 非法状态是否可表达 | 证据（文件:行 / 行为） | 严重度 |
|---|---|---|---|---|
| 1 | `ResourceId`（判别联合） | **可表达**（同质不可混用未被强制） | `Objects.cs:20-43` 14 个构造子，仅做**形状**判别；`ParseResource`（`EffectScriptContract.cs:141-152`）把 `memory` 缺省成 `Memory(0)`、`commandBuffer` 缺省成 `CommandBuffer("gpu")`，常量硬编码而非类型区分；`Normalize`（`Objects.cs:45-72`）把 `Self("signal_x")`/`Signal("signal_x")` 塌缩成 `SignalBus(x)`。两不同构造子可表示「同一资源」（混淆），同质可混用。 | **High** |
| 2 | `ResourceId.Memory(ulong)` + `Memory(0)` 哨兵 | **可表达**（哨兵与真实 UID 0 不可区分） | `ApiMapping.cs:39` `Memory(0)` 作为「通用内存哨兵」；`EffectScriptContract.cs:148` JSON `memory` 无数字也 → `Memory(0)`；`Objects.cs:229/230` 序列化回退 `memory:0`。类型上 `Memory(0)` 既是「哨兵」又是「uid=0 的真实块」，用户无法在类型层区分「泛指内存」与「uid=0 块」。 | **High** |
| 3 | `ResourceId.CommandBuffer(string Channel)` | **可表达**（常量 `"gpu"` 硬编码，同质忽略） | `EffectScriptContract.cs:147` `new CommandBuffer(cb.GetString() ?? "gpu")`；`ParseResource` 注释称 `gpu / command_buffer ⇒ CommandBuffer("gpu")`（§3.1.2b）。所有裸 gpu 通道塌缩到同一个 `"gpu"` 字符串，类型无任何约束保证通道名有意义。 | **Medium** |
| 4 | `ResourceId.AudioMixer(int ChannelId)` | **可表达**（忽略 uid / 任意 int 合法） | `Objects.cs:34` 构造子接受任意 `int`；`ParseResource`（`EffectScriptContract.cs:142-152`）**根本没有** `audioMixer` 分支（只认 gpu/commandBuffer/memory/occupancy/signalBus），故 JSON 无法直接构造 `AudioMixer`；但代码层 `new AudioMixer(anyInt)` 完全合法，且 ChannelId 无界、无校验。 | **Medium** |
| 5 | `ScopeId` + `IncludedIn` 偏序 | **可表达**（非法跨作用域比较返回 false 兜底） | `Objects.cs:101-112` `IncludedIn`：`Equals(other)→true`；`other is Global→true`；否则 `return false`。偏序**不是类型**；`Method("a")` 与 `Scene("b")` 的比较**合法调用且静默返回 false**（不可比较），类型不报错。所有 net/Peak 过滤（如 `Algebra.cs:60,119`）靠此布尔，非法比较不会被类型阻止。 | **High** |
| 6 | `Signature` 三桶 `Read/Write/Occupy` | **可表达**（量纲隔离靠注释+运行时，非类型） | `Objects.cs:152-178` 三 `ImmutableHashSet` 字段；分桶在 `Add` 的 `switch(Kind)` 运行时完成；跨桶聚合 `Weight.Of`（`Algebra.cs:33-38`）用 `double.NaN` 约定编码 `⊥`（`a==b?1.0:double.NaN`）。类型**暴露三桶但无法阻止**把 read 桶当 occupy 桶喂进 net（net 靠 `if (c.Kind != Kind.Occupy) continue` 运行时过滤，`Algebra.cs:58`）。`NaN` 约定可被静默传播。 | **Medium** |
| 7 | `NatStar` / `Interval` / `SignedInterval` | **不可表达（载体层优秀）** | `Numeric.cs:17-95`：下界有限、lo≤hi 不变量在 `Interval` 构造子抛 `ArgumentException`（`Numeric.cs:57-62`）；`[⊤,x]` 非法被构造子拒绝。`SignedInterval`（`SignedNet.cs:62-66`）lo≤hi 同样构造子校验。负值由 `ZStar.Value:long` 承载，非运行时 if。`NatStar` 的 `⊤` 由 `IsTop` 字段承载。 | **None（载体层通过）** |
| 8 | `NatStar.Of(ulong)` 回卷检测 | **可表达**（构造子不检测，回卷靠运算符事后比） | `Numeric.cs:22` `Of(v)=>new(false,v)` 是纯构造；回卷检测在 `operator+`（`Numeric.cs:33-37`：`sum<a.Value?Top`）与 `operator*`（`Numeric.cs:44-50`：`prod/a!=b?Top`）。**裸 ulong 路径未被保护**：`EffectScript.cs:115` `NatStar.Of(maxFinite + 1)` 是原生 `ulong +`，若 `maxFinite==ulong.MaxValue` 静默回卷成 0 并作为开放尾段采样点；`EffectScriptContract.cs:81,103,160` `NatStar.Of(el.GetUInt64())` 直接信任 JSON 数值。类型层无法阻止 `ulong` 回卷流入。 | **Medium**（边缘；仅 `maxFinite+1` 路径真实可达） |
| 9 | `Claim` 五元组 `(Kind,Resource,Mode,Scope,Size?)` | **可表达**（构造即全必填≠构造即合法） | `Objects.cs:126` 位置记录确保 5 字段全填，但**不强制**：
(a) `Resource` 归一化后条件 `Resource==Normalize(Resource)` 类型无法表达——`new Claim(kind, new Self("signal_x"), ...)` 合法但非规范键，破坏 `Normalize` 去重/合并（`Objects.cs:120-124` 注释承认须先 Normalize）；
(b) `Size` 为 `Interval?` 可 null；`null` 与 `Exact(0)=[0,0]` 在**语义不同**但类型同形，靠 `Normalize`（`Objects.cs:118`）把 null→Default 兜底，用户可构造「null size」状态；
(c) 非法 mode 组合（read+release、move+read）类型**完全允许**：`Mode`/`Kind` 是独立 enum，`new Claim(Kind.Read, r, Mode.Release, s, null)` 合法，错误只在 Audit 运行时（gate(3) 只查 occupy 同 mode 冲突）才暴露，且 read+release 这种**跨桶**矛盾根本不被任何 gate 检查。 | **High**（(a)/(c)）+ **Medium**（(b)） |
| 10 | `LoopCount` / `ω=⊤` | **不可表达（边界可见）** | `DerivedMetrics.cs:14-33` `Count: NatStar` 承载 `⊤`；`Loop`（`DerivedMetrics.cs:36-58`）对 `ω=⊤` 把 size 上界拉到 `⊤`，类型强制检测，无魔法数。 | **None（通过）** |
| 11 | `Compat.IsCompatible(Mode,Mode)` | **不可表达（穷举全函数）** | `Algebra.cs:18-32` 16 对全函数，`Unknown→Use` 解析，无未覆盖对，**编译期穷举** enum，无运行时「未知 mode」分支。 | **None（通过）** |
| 12 | `EffectEvent`（`Lifetime,Scope,Footprint,Loop`） | **可表达**（重复 scope 字段，claim.scope 被静默忽略） | `EffectScript.cs:18-46` `EffectEvent` 自带 `Scope`，且 Audit 的 gate(3) 与 `At` 的 `Combination.Loop` 都用 `e.Scope`（`EffectScript.cs` OPEN-1 修复），**忽略** `Footprint` 内各 `Claim.Scope`（`EffectScriptContract.cs:138` 仍解析并存储 claim.scope）。一个 `Claim` 带与事件 scope 冲突的 scope 在类型层完全合法，且被静默丢弃，无报错。 | **Medium** |
| 13 | 三桶键冲突：`ResourceId` 相等 vs `SetEquals` | **可表达**（跨桶无法用类型约束「同资源」） | `Objects.cs:152` `Signature` 三桶各自 `ImmutableHashSet<Claim>`，Claim 相等用 record 结构相等（含 `Kind`/`Mode`/`Scope`/`Size`）。`Self("mem")` 与 `Memory("mem")` 结构**不相等**但经 `Normalize` 语义相等；任何未 Normalize 的 Claim 集合运算（`NetTable.Compute` `Algebra.cs:53`）用 `ResourceId.Normalize` 重键，若上游漏调则同资源被当异质。类型无法强制「集合运算前必 Normalize」。 | **Medium** |
| 14 | `EffectScript.Audit` 闭包 `Lo=⊤` 跳过 | **可表达**（`Lifetime` 含 `Lo=⊤` 永不存活，类型允许构造） | `EffectScript.cs:100-104` `if (lt.Lo.IsTop) continue;` —— `new Interval(NatStar.Top, NatStar.Top)` 合法（未知区间，`Numeric.cs:57` 仅禁 `[⊤,有限]`），但 `Lo=⊤` 的事件**永远不存活**、永不参与任何 gate。用户写出 `lifetime:[⊤,⊤]` 在类型层合法，却在审计中被静默完全忽略（零违例、零警告），制造「我写了但系统没看」的隐性困难。 | **Low/Medium** |

---

## 2. 关键缺失：只能在 Audit 运行时 / 人工期才报的错误（类型系统放过）

按「用户写完才发现」的隐性困难排序：

1. **资源同质混淆（表 #1/#2/#3）**：`Memory(0)` 哨兵、`CommandBuffer("gpu")` 常量、`Normalize` 塌缩——用户把「泛指内存」和「uid=0 块」、把不同命名空间当成同一/不同资源时，**类型不阻止也不警告**，只有审计结论出错（守恒/峰值按错误的归一键聚合）或人工对照 `§3.1.4a` 映射表才发现。这是**静默破坏正确性**级隐患。
2. **Claim 归一化后条件（表 #9a/#13）**：`new Claim(Kind.Occupy, new Self("signal_x"), ...)` 构造合法但非规范键，直到 `NetTable.Compute` 因未 Normalize 而把它当新资源——泄漏/守恒判定**漏报或误报**，无类型层护栏。
3. **跨桶非法 Claim 组合（表 #9c）**：`read+release`、`move+read` 等跨 kind 的矛盾在类型层完全合法；gate(1)(2)(3) 只查 occupy 桶内同 mode 冲突，**跨桶语义矛盾无任何检查**，用户写完才发现「我同时 read 又 release 了同一资源」从未被质疑。
4. **作用域非法比较（表 #5）**：`Method("a").IncludedIn(Scene("b"))` 静默 `false`，net/Peak 过滤因此**漏算或错算**跨作用域聚合，类型不报错。
5. **`Lifetime` `Lo=⊤`（表 #14）**：事件被静默完全忽略，零反馈。
6. **`NatStar.Of` 裸 ulong 回卷（表 #8）**：仅 `maxFinite+1` 路径真实可达，极端 `ulong.MaxValue` 端点时回卷成 0 采样点，类型放手。
7. **`double.NaN` 编码 `⊥`（表 #6）**：跨 kind 聚合的「未定义」用 NaN 约定，若上层未拦截，NaN 会静默传播进任何数值运算而不抛异常——类型无法阻止把 `⊥` 当实数使用。
8. **`EffectEvent` 与 `Claim` 双 scope（表 #12）**：claim 自带 scope 被审计忽略，用户写了无效 scope 无反馈。

---

## 3. 总评

**类型系统是否足够阻止错误？** —— **分化**：
- **算术载体层（ℕ*/ℤ*/Interval/SignedInterval/LoopCount/NatStar）**：✅ 足够。`⊤`、负值、越界都是**类型字段**，`lo≤hi` 是**构造子不变量**，`ω=⊤`/负值由类型强制检测，无魔法数、无运行时 if 漏判。这是 Hickey 式「边界即类型」的好实现。
- **领域语义层（ResourceId 同质隔离、ScopeId 偏序、Signature 量纲、Claim 归一化与合法性、跨桶约束）**：❌ 不足。错误被推迟到 Audit 运行时或人工对照映射表；其中 #1/#2/#9a/#13 会**静默破坏审计结论正确性**（非仅报错），是真正的「写完才发现」隐性困难来源。

**Top 3 应改成「非法状态不可表达」的点**（按收益/风险排序）：

1. **资源同质隔离（#1/#2/#3）**：把 `Memory(0)` 哨兵、`CommandBuffer("gpu")` 常量、`Normalize` 塌缩从「运行时约定」提升为**类型层区分**——例如引入 `ResourceId.GeneralMemory`（哨兵构造子）与 `ResourceId.MemoryBlock(ulong uid)` 两个**不同**标签，使「泛指」与「具体块」无法混用；把 `Normalize` 的塌缩结果类型化为独立的规范构造子，或要求 `Claim` 构造子**强制调用 Normalize**（在构造子内归一，而非靠注释「须先 Normalize」）。消除 `Memory(0)`/`CommandBuffer("gpu")` 可混淆常量。
2. **Claim 归一化 + 合法性不变量（#9）**：让 `Claim` 构造子**立即 Normalize**（`Resource=Normalize(Resource)`、`Size=null→Default`），并引入**类型层区分** `NullSize`(缺省) 与 `ExactZero`(显式 0)，使 `read+release`/`move+read` 这类跨桶矛盾在构造时被拒绝（例如用不同的 `Kind`×`Mode` 合法矩阵做判别联合，或构造子内断言 `!(Kind==Read && Mode==Release)`）。至少把「未归一 Claim」变成不可构造。
3. **ScopeId 偏序类型化（#5）**：把 `IncludedIn` 的「返回 false 兜底」改成**不可比较即编译期/构造期错误**（例如用 Phantom 类型或把 `IncludedIn` 拆成 `TryIncludedIn` 返回 `Option<bool>`，强制调用方处理「不可比较」分支，而非静默 false）；并在 `NetTable`/`Peak` 过滤处对 `false`（不可比较）与「真的不包含」做语义区分，避免跨作用域聚合被静默漏算。

**剩余（非 Top3 但应修）**：`NatStar.Of` 在裸 `ulong` 路径（尤其 `maxFinite+1`，`EffectScript.cs:115`）应改用 `NatStar` 运算符以触发回卷→`⊤`；`Signature` 跨桶聚合的 `double.NaN` 编码应提升为显式 `Option<double>`/`Result` 类型；`Claim.Scope` 与 `EffectEvent.Scope` 双 scope（`#12`）应去冗余——事件 scope 单源，claim 不再携带会被忽略的 scope。

---

## 4. 判定汇总（severity 计数）

- **None（类型通过）**：#7 载体层、#10 LoopCount、#11 Compatible —— 3 项。
- **High（静默破坏正确性）**：#1 ResourceId 同质、#2 Memory(0) 哨兵、#5 ScopeId 偏序兜底、#9 Claim 归一化/跨桶矛盾 —— 4 项。
- **Medium**：#3 CommandBuffer 常量、#4 AudioMixer 任意 int、#6 三桶 NaN、#8 NatStar.Of 回卷、#12 双 scope、#13 跨桶键冲突 —— 6 项。
- **Low/Medium**：#14 Lifetime Lo=⊤ 静默忽略 —— 1 项。

**核心结论**：类型系统把「数怎么算」做对了，把「业务上什么算合法」推迟给了运行时与人工；要使系统「illegal state unrepresentable」，优先把 Top3 的领域语义不变量**编码进类型本身**，而非依赖 `Normalize`/`IncludedIn`/`Weight.NaN` 的运行时约定与注释契约。
