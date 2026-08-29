# Rich Hickey Round 06 — Naming Hammock Audit

> **Lens:** Hammock 命名即设计 — 名字是否揭示本质（what it is）而非用法/实现/隐喻？用户能否仅凭名字推断正确用法与失败模式？
> **Scope:** 7+1 文件 = `Algebra.cs` / `Objects.cs` / `Numeric.cs` / `SignedNet.cs` / `DerivedMetrics.cs` / `EffectScript.cs` / `EffectScriptContract.cs` + `README.md`。禁读 `audit/`。
> **Date:** 2026-08-29

## 图例
- **P0** 阻塞合并：名字直接导致错误用法且编译期不拦截或静默错误语义
- **P1** 发布前必改：高频误用，推理成本高，需重命名或别名废弃
- **P2** 记录债：可通过文档/注释缓解，但违背“值语义”原则

## 总表 — 逐符号 Hammock 判定

| # | 符号 | 位置 | 本质 | 名字揭示？ | 用户推断失败 | 严重度 | 最小修复 |
|---|------|------|------|-----------|-------------|--------|----------|
| N-01 | `ScopeId.Loop` vs `LoopCount` | `Objects.cs:97` vs `DerivedMetrics.cs:10`, `EffectScript.cs:36` | 前者是**地点**（scope tag, 值 `string Id`），后者是**数量**（ω∈ℕ∪{⊤}, 标量） | 否 — 同词干 `Loop` 复用 | `new ScopeId.Loop("battle")` vs `LoopCount.Of(3)` vs JSON `"loop":3` 三者在代码/JSON 中同拼 `loop`，心智模型坍缩为“循环”一词，误以为 `ScopeId.Loop` 携带计数或 `LoopCount` 携带作用域 | **P1** | `ScopeId.Loop` → `ScopeId.LoopScope` / `IterationScope`；保留 `ScopeId.Loop` 为 `[Obsolete]` 别名 1 版本 |
| N-02 | `Kind.Occupy` vs `ResourceId.Occupancy` | `Objects.cs:112` vs `Objects.cs:32` | 前者是**量纲桶标签**（三桶之一），后者是**资源身份**（`audio_channel`/`animation_state` 归一后的 channel 名） | 否 — 动词 `occupy` vs 名词 `Occupancy` 仅差后缀 | 用户推断 `Occupancy("audio")` 必然进入 `Kind.Occupy` 桶，或反之 `Kind.Occupy` 的资源必为 `Occupancy` 类型；实则正交：`Occupancy` 资源也可被 `Kind.Read/Write` 声明（类型不拦），而 `Kind.Occupy` 桶承载 `Gpu/Memory/...` 等任意资源；`Algebra.cs:59,127` 的 `c.Kind != Kind.Occupy` 过滤与资源类型无关的事实被名字掩盖 | **P1** | `ResourceId.Occupancy` → `ResourceId.Channel` / `ResourceId.OccupancyChannel`；或 `Kind.Occupy` → `Kind.Residency` 以拉开动词/名词距离 |
| N-03 | `Signature.Union` / `Signature.Join` / `Combination.Sequence` / `Combination.Parallel` | `Objects.cs:205` / `Objects.cs:215` / `DerivedMetrics.cs:69-70` / `DerivedMetrics.cs:76` | 四者签名同为 `(Signature,Signature)->Signature`，值域均为集合；仅 `Join` 在同键上 `Merge` 区间，`Parallel` 前置 `Compatible` 抛 `PARA_CONFLICT` | 否 — 时序隐喻 (`Sequence`/`Parallel`) 与格论术语 (`Union`/`Join`) 混用，且 L1 无时序语义（`README:163 诚实边界#1` 自承） | 用户无法从名字推断：`Sequence`==`Union`（已 `Obsolete` 但仍可调）、`Parallel` 会抛而 `Union` 不抛、`Join` 不是集合并而是 `merge_I`（`[10,10]⊔[50,50]⇒[10,50]` Peak=max 非 sum）；并行性误以为由 `Parallel` 承载，实为 `Compatible` 守卫 + L3 | **P0** | 删除 `Sequence`（已 Obsolete，下一大版本移除）；`Parallel` 更名为 `UnionChecked` / `UnionWithConflictCheck` 并文档置顶；`Join` 更名为 `MergeByInterval` / `UnionWidening`（揭示 widen 非叠加） |
| N-04 | `Net` / `NetTable` / `Peak` | `Algebra.cs:46,117` / `DerivedMetrics.cs:102,105` / `Objects.cs:234` / `EffectScript.cs:254-265` | `Net` 是**有符号求和** `Σ±size`（`SignedInterval`, 可负，`ContainsZero` 判守恒）；`Peak` 是**无符号上界求和** `Σ hi`（`NatStar`, 忽略 `Release`, 任一 ⊤⇒⊤） | 部分 — `Net` 过短（口语“净” vs “网络”），且 `Peak` 未显资源维度 | 用户误将 `Net` 当 `Peak`（或反之）用于预算门；`Signature.Net(scope)` / `Derived.Net(s,scope)` / `NetTable.Compute` 三名一实，`Derived.Peak` vs `Peak.Compute` 二名一实，增加检索成本；`Peak` 未揭示“每资源每 scope 求和、release 不计”的关键不变量（`Algebra.cs:129`） | **P1** | 统一入口：`Signature.Net` 保留，`Derived.Net` 标 `Obsolete` 转发；`Peak` → `PeakOccupancy` / `ConcurrentPeak`；`NetTable` → `NetLedger` / `ConservationTable`（揭示守恒账本） |
| N-05 | `Budget` vs `Caps` vs `CapsChecked` / `IsPeakChecked` | `EffectScript.cs:378,381,395,397,435,455` / `EffectScriptContract.cs:36,69,225` | `Budget` 是**壳**（`IReadOnlyDictionary<ResourceId,NatStar>`），`Caps` 是壳内表，`CapsChecked` 是**门是否运行计数** | 否 — `Budget`（预算，隐含“钱”）与 `Caps`（上限，隐含“帽”）混喻；`Budget.None` 语义为“无上限”（Unbounded）而非“零预算” | 用户将 `Budget.None` 误读为“零容忍”，将 `Passed==true && CapsChecked==0` 误读为“全绿”（`README:99` 已警告但名字未揭示）；`Audit(Budget cap)` 单数 vs `Budget.Caps` 复数、`SerializeBudget` 键为 `gpu:tmpMip0` 而 `ResourceId.Gpu` 构造子为 `Gpu(Rid)` 的映射不透明 | **P1** | `Budget` → `PeakLimits` / `ResourceCaps`（与 `Caps` 同词），`Budget.None` 保留别名但主名 `Unbounded` / `NoLimits`，`CapsChecked` → `LimitsCheckedCount`，`IsPeakChecked` 保留但文档强调“0 ⇒ 门未运行” |
| N-06 | `At` vs `Audit` | `EffectScript.cs:90` vs `EffectScript.cs:134,332` / `Violation.AtT:462` | `At(t)` 是**点投影**（`Signature` 快照，`Alive` 判定 `Lo≤t≤Hi`），`Audit` 是**扫换线验证**（三门 + 闭包，`O(E·K·logE)`） | 否 — 同前缀 `At`/`Audit`，2 字符差，且 `At` 为介词非动词 | 用户误将 `At` 当轻量 `Audit`，或在循环中逐点 `At` 代替 `Audit`（性能/语义错：`At` 无 `NegativeDip`/`Leak` 闭包）；`Violation.AtT` 进一步复用 `At` 词干表示“发生时刻” | **P2** | `At` → `SnapshotAt` / `ProjectAt`；`Audit` → `Verify` / `CheckAllGates`；`Violation.AtT` → `Violation.When` / `Violation.SampleTime` |
| N-07 | `Footprint` | `EffectScript.cs:32` / `EffectScriptContract.cs:85,265` | 值是 `Signature`（三桶集合），非几何面积 | 否 — 隐喻（脚印） | 用户推断 `Footprint` 为 `Interval Size` 而非 `Signature`；与 `Claim.Size` / `Interval` 混淆 | **P2** | `Footprint` → `Effects` / `Signature` / `Claims`（与 `Signature` 同词） |
| N-08 | `NatStar` / `ZStar` / `Top` / `Star` | `Numeric.cs:8,70` / `SignedNet.cs:10,76` | `NatStar`=`ℕ∪{⊤}` 非负上界，`ZStar`=`ℤ∪{⊤}` 有符号上界，`Top`=`⊤` 未知，`Star` 本意 Kleene 星 | 否 — `Star` 常规意为 Kleene 闭包，非 ⊤ | 用户误将 `IsTop` 当“栈顶”，`Star` 当“星号通配”；`NatStar.Top` vs `Interval.Dynamic=[1,⊤]` vs `LoopCount.Top` 三 `Top` 同名不同载体 | **P2** | `NatStar` → `NatTop` / `BoundedNat`，`ZStar` → `IntTop` / `BoundedInt`，`Top` 保留但类型前缀区分已足够；文档统一“`*` = ⊤-closed” |
| N-09 | `Interval.Default` / `Interval.Dynamic` / `Claim.Size ?? Interval.Default` | `Numeric.cs:92,95` / `Objects.cs:129-141` / `Algebra.cs:63,131` | `Default=[1,1]`（缺省精确 1），`Dynamic=[1,⊤]`（实例化不确定），`null⇒Default` 为归一 | 否 — `Default` 未揭示“1” | 用户将缺省当“未知”或“0”，`README:118` 已设锐边但名字未揭示；`Size ?? Interval.Default` 散布 4 处（`Algebra.cs:63,64,130,131` 等） | **P2** | `Default` → `One` / `SingleUnit`，`Dynamic` → `OneToTop`，或保留但追加注释 `// = [1,1]` 已有 |
| N-10 | `ResourceId.Normalize` vs `Claim.Normalize` | `Objects.cs:52,133` | 前者纯归一（`Self("signal_x")→SignalBus`），后者归一+缺省+校验（`Kind×Mode` 非法抛） | 否 — 同名不同契约 | 用户对 `Claim.Normalize()` 期望幂等纯函数，实则含校验抛 `ArgumentException`（`Objects.cs:135`）；`Signature.Of` 内双重 `Normalize` 易重复 | **P2** | `Claim.Normalize` → `Claim.Normalized` / `Claim.EnsureValid`，或拆为 `Normalize`（纯）+ `Validate` |
| N-11 | `Compatible` / `IsCompatible` / `CompatibleWith` | `Algebra.cs:12,18` / `Objects.cs:145` | 16 对全函数对称，`Unknown→Use` fail-open | 部分 — 双重命名 | 用户在 `Claim.CompatibleWith` 与 `Compatible.IsCompatible` 间犹豫；`Unknown` 静默放行（`Algebra.cs:15,22`）与名字“Compatible”隐含的“兼容”一致但与安全直觉相反（fail-open 非 fail-closed） | **P2** | 统一为 `Compatibility.IsCompatible` 单入口，`Claim.CompatibleWith` 标 `Obsolete` 转发 |
| N-12 | `LoopCount.Count` / `LoopCount.IsValid` / `LoopCount.Top` | `DerivedMetrics.cs:13,21,26` | `Count` 是 `NatStar` 载体，`IsValid` 派生合法性（≥1或⊤） | 否 — `Count` 未揭示 `NatStar` | 用户 `loop.Count.Value` 二跳取值，或 `default(LoopCount)` 误为合法（`EffectScript.cs:47,54` 已封堵但名字未揭示） | **P2** | `Count` → `Value` / `Nat`，保留 `Count` 别名；`IsValid` 已为最小修复（`R5 V5-002`） |
| N-13 | `Weight.Of` / `DeviationVal` | `Algebra.cs:35` / `Numeric.cs:111` / `Deviation.cs` 未审 | `Weight` 跨 kind 抛 `KIND_MIX`，`DeviationVal` 偏差阈值 | 否 — `Of` 工厂名与 `NatStar.Of` 复用但语义为偏函数 | 用户推断 `Weight.Of(a,b)` 总成功，实则跨 kind 抛 | **P2** | `Weight.Of` → `Weight.For` / `Weight.RequireSameKind` |

## 碰撞深钻（按任务清单）

### ScopeId.Loop vs LoopCount
- **证据** `Objects.cs:97` `ScopeId.Loop(string Id)` 注释“循环作用域”；`DerivedMetrics.cs:10-13` `LoopCount { NatStar Count }`；`EffectScript.cs:36` `LoopCount Loop`；JSON 层 `EffectScriptContract.cs:84` `"loop"` 键。
- **Hammock 违背** 地点（where）与数量（how many）同词，complect。
- **推断失败** 编译期类型可拦，但 JSON 心智与代码心智分裂：AI 产 `"loop": "⊤"` 时不知对应 `LoopCount.Top` 还是 `ScopeId.Loop`。
- **最小修复** 重命名 scope 侧为 `LoopScope`，保留兼容别名。

### occupy vs Occupancy
- **证据** `Objects.cs:112` `Kind.Occupy`；`Objects.cs:32` `ResourceId.Occupancy(string Channel)`；`Algebra.cs:59,127` 量纲隔离仅按 `Kind`。
- **违背** 动词/名词同根，暗示绑定实则正交。
- **失败** 误以为 `Occupancy` 资源自动落 `Occupy` 桶，错配 `Read/Write` 桶的 `Occupancy` 声明。
- **修复** 资源侧更名 `Channel`。

### Union/Join/Sequence/Parallel
- **证据** 4 方法签名一致，`Objects.cs:205` 正文“并集按Normalize去重”、`215` “条件分支合并...与Union等价...仅同键merge_I”、`DerivedMetrics.cs:70` `Obsolete("仅为别名")`、`76` `PARA_CONFLICT` 守卫。
- **违背** 用时序词命名值运算，违背“数据即值”原则；`README:163` 自承 `Sequence≡Parallel≡Union 四名一实`。
- **失败** `Parallel` 静默等价 `Union` 的假阳性/假阴性；`Join` 被当 `Union` 用导致 `Peak` 高估/低估。
- **修复** 见上表 P0。

### Net vs Peak
- **证据** `Algebra.cs:46-68` 有符号和、`117-134` 无符号和；`EffectScript.cs:132-133` 注释三门定义。
- **违背** `Net` 未揭示有符号账本本质。
- **失败** 预算门错用 `Net`。
- **修复** 更名 `ConservationLedger` / `ConcurrentPeak`。

### Budget vs Caps
- **证据** `EffectScript.cs:378-421` `Budget` 壳含 `Caps`，`395` `None=Empty`，`440` `Passed≡Violations.IsEmpty`，`455` `IsPeakChecked=>CapsChecked>0`；`README:99` `CapsChecked` 报告。
- **违背** 壳与内容异名，空预算语义反直觉。
- **失败** `Passed==true` 被当“峰值全绿”。
- **修复** 壳更名 `ResourceCaps`，空值更名 `NoLimits`。

### At vs Audit
- **证据** `EffectScript.cs:90` `At(NatStar t)` 点投影、`134` `Audit(Budget)` 扫换线、`462` `Violation.AtT`。
- **违背** 介词作动词，审计动词与投影介词同茎。
- **失败** 逐点 `At` 代替 `Audit` 漏 `Leak`/`NegativeDip`。
- **修复** `SnapshotAt` / `Verify`。

## 残余风险
- `ScopeId` 偏序 `IncludedIn` 仅同标签相等或 `Global` 最大元（`Objects.cs:102-108`），跨标签永远 `false` 却命名“包含”，可能误期待层级包含（`Method ⊆ Type ⊆ Scene`），现有设计故意扁平但名字暗示层级 — P2 文档债。
- `Weight` / `DeviationVal` 未在 7 文件主路径高频，但 `Weight.Of` 跨 kind 抛与 `DeviationVal.ExceedsThreshold` 的 `IsTop=>false` 静默不报警（`Numeric.cs:128`）延续“Top 即不报警”语义，需在调用点显式处理。

---
*审计员：Rich Hickey 视角（Hammock）— 名字是设计，错名即错设计。*

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "逐符号表 13 项，含文件路径与行号（Objects.cs:32,97,112 等）、严重度 P0×1/P1×4/P2×8，覆盖全部清单词冲"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round06-naming-hammock.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [],
  "validationOutput": [
    "7+1 文件已读：Algebra.cs Objects.cs Numeric.cs SignedNet.cs DerivedMetrics.cs EffectScript.cs EffectScriptContract.cs + README.md；未读 audit/"
  ],
  "residualRisks": [
    "ScopeId.IncludedIn 命名暗示层级包含但实现为扁平相等 — 文档债 P2",
    "Weight.Of 跨 kind 抛与 DeviationVal.IsTop 不报警语义需调用点显式处理"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round06-naming-hammock.md：Hammock 命名审计，13 行符号表+6 词冲深钻+最小更名修复",
  "reviewFindings": [
    "P0: DerivedMetrics.cs:76 Parallel vs Objects.cs:205 Union 四名一实但前者抛 PARA_CONFLICT — 名字掩盖守卫",
    "P1: Objects.cs:97 ScopeId.Loop vs DerivedMetrics.cs:10 LoopCount 地点/数量同词干",
    "P1: Objects.cs:32 Occupancy vs Objects.cs:112 Kind.Occupy 动词/名词同根但正交",
    "P1: EffectScript.cs:378 Budget vs Caps 壳/内容异名且 Budget.None 语义反直觉",
    "P1: Algebra.cs:46 NetTable vs Algebra.cs:117 Peak 有符号/无符号求和短名易混",
    "P2: EffectScript.cs:90 At vs 134 Audit 前缀碰撞介词/动词混用",
    "P2: EffectScript.cs:32 Footprint 隐喻非值语义"
  ],
  "manualNotes": "受限环境无 write 工具，markdown 全文已在回复中给出，runtime 需落盘至 D:/Godot/Cosmos/audit/rich-hickey-round06-naming-hammock.md"
}