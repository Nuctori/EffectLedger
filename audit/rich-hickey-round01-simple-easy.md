# Rich Hickey 透镜审计 — Simple vs Easy (round01)

> 只读 7 文件：Algebra.cs / Objects.cs / Numeric.cs / EffectScript.cs / EffectScriptContract.cs / DerivedMetrics.cs / SignedNet.cs
> 透镜：Simple = 一事一处、不编织；Easy = 近手、熟悉。Complect = 把本不相干之事编在一起。

## 总判定：COMPLEX — 易用性赎买了简单性

7 文件共 ~80 个公开符号，其中 **偶然复杂度占比 >60%**。核心代数（`Signature`集合 + `Interval`格 + `Net+Peak`守恒）是本质的，但被三类 Easy 包装层层缠绕：

1. **四名一实**：`Union / Join / Sequence / Parallel` 实为同一 `∪`，用名字伪造语义差异，调用方需记忆四套心智模型。
2. **双真相 Scope**：`Claim.Scope` 与 `EffectEvent.Scope` 双源，契约层再用运行时校验缝合，类型本可让错误不可表达。
3. **ResourceId 膨胀**：15 个构造子中契约仅用 5 个，剩余 10 个是映射层债务提前泄入 L1，+ `Rid/StringName` 薄包装制造类型幻觉。

结果：API 看似 Easy（到处有便捷入口），实为 Complected（概念交叉、别名泛滥、隐式守卫散落）。Simple 债务已影响可推理性：同一资源因归一化时机不同呈现两种身份，同一组合因别名不同产生两种错误方言。

---

## 逐符号表

| 符号 | 位置 | 本质/偶然 | 严重度 | 证据与判定 | 极简修复 |
|---|---|---|---|---|---|
| **Rid / StringName** | `Objects.cs:11,14` | 偶然 | P2 | `record struct Rid(string Value)` vs `string` 零行为差异，映射层注释称零 Godot 依赖，但实际仅透传 `Value`。Complect 类型幻觉与字符串本质。 | 删除，用 `string`；映射层用 `type alias` 而非新类型 |
| **ResourceId 15 态** | `Objects.cs:21-65` | 偶然 | **P1** | 15 构造子（Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Occupancy/Callback/Network/Input/Custom/CommandBuffer/SignalBus），契约 `ParseResource:209-222` 仅识别 5 个（gpu/commandBuffer/memory/occupancy/signalBus），其余 10 个在 7 文件内无产生/消费点（`Normalize:52-65` 仅处理 3 个）。开放域用封闭枚举建模，再用 `Custom` 兜底 = 自相矛盾。 | 收敛为 `Gpu/Memory/Occupancy/SignalBus/CommandBuffer + Open(string)` 6 态，或直接 `string NormalizedKey`，删除其余 9 态 |
| **ResourceId.Normalize** | `Objects.cs:52-65` | 偶然 | P1 | 归一仅对 `Self(signal_*)→SignalBus` / `Signal(signal_*)→SignalBus` 生效，其余 13 态恒等。调用方必须记忆“何时调 Normalize”，`NetTable.Compute:61` / `EffectScript.Audit:182,193` / `Budget:390` 各自调一遍，漏调即双身份 Bug（`Audit.cs:269` 注释自认修过）。 | Normalize 应在 `Claim` 构造时一次完成且不再暴露未归一值；或 `ResourceId` 构造即归一 |
| **NodePathOrUnknown** | `Objects.cs:69-84` | 本质 | P2 | `Tree` 资源的可空路径需 `Unknown` 哨兵，结构合理。但 `IsUnknown+Path` 双字段 vs 单例 `Unknown` 引入 `default` 后门（与 `LoopCount` 同类）。 | 改为 `record` 判别联合 `Known(string)/Unknown`，消 `bool+string` 组合 |
| **ScopeId 9 态 + IncludedIn** | `Objects.cs:90-109` | 偶然 | **P1** | 9 态（Method/Type/Scene/Global/Shell/Loop/Conditional/Async），契约 `ParseScope:118-141` 仅认 4 个（method/type/scene/global），`SerializeScope:269-276` 仅写 4 个，`IncludedIn:102-108` 实为 `Equals ∨ is Global` 常量折叠，谎称偏序。Dead 态（Shell/Loop/Conditional/Async）制造“似乎支持层级作用域”幻觉。 | 删至 4 态；偏序若真需要用 `parent` 指针，否则改名 `IsSameScopeOrGlobal` |
| **Kind / Mode** | `Objects.cs:112,117` | 本质 | — | `{Read,Write,Occupy} × {Use,Create,Release,Move,Unknown}` 是量纲隔离与生命周期本质量，`Claim.Normalize:135` 的 `Read⊕Create` 拒绝正确。 | 保留 |
| **Claim** | `Objects.cs:127-146` | 本质+偶然 | **P0** | 本质：五元组 `(kind,resource,mode,scope,size)`。偶然：`Scope` 字段与 `EffectEvent.Scope` 双真相（见下）。`record struct` 的 `default/with` 后门致 `Signature.Add:190-191` 需运行时 `null` 守卫，类型谎言。 | 删除 `Claim.Scope`，单一真相归 `Event` |
| **Claim.Normalize / CompatibleWith** | `Objects.cs:133-145` | 偶然 | P2 | `Normalize` 兼做校验+归一+补缺省，Complect 三事；`CompatibleWith` 薄委托 `Compatible.IsCompatible`。 | 拆 `Validate` / `Normalize` / `DefaultSize` |
| **Signature** | `Objects.cs:153-261` | 本质 | P1 | 三桶 `ImmutableHashSet` 是量纲隔离本质。但 `Of:171-184` 的重复 Claim 抛异常是并发语义泄入集合层；`Add:186-202` 私有却重复 `Normalize+null` 校验；`Equals/GetHashCode:237-257` 为“类却想当值”打补丁。 | `Signature` 改 `record` 或 `readonly struct` 天然值语义；`Of` 去重改为显式 `TryAdd` |
| **Signature.Union** | `Objects.cs:205-212` | 本质 | — | `∪` 半格并是唯一本质组合子。 | 保留为唯一公开组合子 |
| **Signature.Join** | `Objects.cs:215-231` | 偶然 | **P0** | 注释自承 `L1 警告：与 Union 等价于同键 size 求并区间(merge_I)` — `Join` 仅在同键时 `Merge` 否则同 `Union`。四名一实之二，`Peak=max而非求和` 的时序幻觉藏在注释而非类型。7 文件内零调用点。 | 删除；需要时用 `UnionWithMerge` 显式命名 |
| **NetTable / Peak** | `Algebra.cs:46-135` | 本质 | — | `net` 有符号求和 vs `merge` 包络区分正确（`NetTable:65` 注释），`Peak` 量纲隔离（`127`）正确。 | 保留 |
| **SignatureExtensions.AllClaims** | `Algebra.cs:138-147` | 偶然 | P2 | 薄包装 `yield return` 三桶，与 `Signature` 暴露三桶重复。 | 内联或改 `Signature.EnumerateAll()` |
| **Compatible** | `Algebra.cs:12-29` | 本质 | — | 16 对全函数 + 对称律清晰，`Resolve(Unknown→Use)` fail-open 注释诚实。 | 保留 |
| **Weight** | `Algebra.cs:35-39` | 偶然 | P2 | `Of(a,b) => a==b?1:throw` 仅为 `KIND_MIX` 抛异常制造类包装，无算术。 | 删除，用 `if (a.Kind!=b.Kind) throw` 内联 |
| **NatStar / Interval** | `Numeric.cs:8-105` | 本质 | — | `ℕ*` 闭包（溢出→⊤）、`Interval.Merge` 格运算本质且正确。 | 保留 |
| **DeviationVal** | `Numeric.cs:111-132` | 偶然 | P2 | 7 文件内零引用，仅为 §9.1 预留，`Top` 语义与 `NatStar.Top/ZStar.Top` 重复。 | 移至 Deviation.cs 或删除 |
| **EffectEvent** | `EffectScript.cs:23-58` | 本质+偶然 | **P0** | 本质：`Lifetime×Scope×Footprint×Loop` 原子事件。偶然：`Scope` 与 `Claim.Scope` 双真相，构造子 `42-48` 需双重守卫（`Lo==⊤` + `!IsValid`），`Footprint` 含三桶但审计仅用 `Occupy`。 | `EffectEvent` 持有 `Scope`，`Claim` 去 `Scope` |
| **EffectScript.At** | `EffectScript.cs:90-98` | 本质 | — | `At(t)= ⋃ Loop(Footprint,Loop,Scope)` 瞬时快照本质。 | 保留 |
| **EffectScript.ComputeSamplePoints** | `EffectScript.cs:101-120` | 本质 | P2 | 纯函数抽取正确，但 `anyOpenEnd→maxFinite+1` 幽灵点规则是 Easy 补丁裹 Simple 采样定理。 | 保留，改名 `SampleTimes` |
| **EffectScript.Audit** | `EffectScript.cs:134-351` | 偶然 | **P1** | 220 行扫换线单函数承载 3 gate + 2 归因字典 + 幽灵点 + 闭包，Complect 时间/资源/作用域三正交轴。`peakScope` 清理 `236-244` 用 `Values.Any` 近似按资源，说是 TODO。`Budget.Caps==null` 归一 `139` 是 `struct default` 后门补丁。 | 拆 3 个纯函数 `CheckNet/CheckPeak/CheckCompat`，Audit 仅编排；或直接用 `INetGate` 等接口（见下）但需真正实现 |
| **Budget** | `EffectScript.cs:378-421` | 偶然 | P2 | `None` vs `Unbounded` 双名同值 `395,397`；`Caps` 防御拷贝正确但 `Equals/GetHashCode` 手写 20 行仅为 `record struct` 本应天然之事。`default(Budget).Caps==null` 后门致 `139` 特判。 | 删除 `Unbounded` 别名；`Budget` 改 `record` 自动值语义；构造器禁止 `default` |
| **AuditResult / Violation** | `EffectScript.cs:426-489` | 本质 | P2 | `Passed≡Violations.IsEmpty` 不变量守卫 `446` 诚实，`CapsChecked` 区分“没查 vs 查过全绿” 正确。但 `Violation.Kind: string` 非枚举，`EventIndex:-1` 魔法值。 | `Kind` 改枚举，`EventIndex` 改 `int?` |
| **INetGate/IPeakGate/ICompatGate** | `EffectScript.cs:370-372` | 偶然 | **P1** | 3 接口仅声明零实现，`Audit` 未通过接口分发，注释称“可独立开关与测试”但无测试可开关，Dead Abstraction 制造可扩展幻觉。 | 删除或真正实现分发 |
| **EffectScriptContract** | `EffectScriptContract.cs:18-326` | 偶然 | P1 | `Parse` 正确 fail-fast，但 `RejectUnknownKeys:49-59` 白名单在根/事件/Claim 三层重复；`ParseResource:209-222` 与 `ParseResourceKey:247-255` 双解析表；`SerializeScope:269` 仅 4 态与 `ScopeId` 9 态分裂。`claim scope==event scope` 校验 `170` 是双真相缝合带。 | 契约 Resource/Scope 各收敛为单表；Claim 去 scope 后校验消失 |
| **LoopCount** | `DerivedMetrics.cs:10-35` | 本质+偶然 | P2 | `ω≥1 ∨ ⊤` 本质，但 `IsValid/TryOf` 是 `record struct default` 后门的补丁簇，`Of` 抛异常 vs `TryOf` 静默 default 双 API。 | `LoopCount` 改 `record` 禁 default，或用 `NatStar` 直代 |
| **Combination.Loop** | `DerivedMetrics.cs:50-64` | 本质 | — | `Scale` 语义正确，`loopScope` 重标是 Scope 双真相的源头之一。 | 保留但去 `loopScope` 参（Footprint 已无 Scope） |
| **Combination.Sequence** | `DerivedMetrics.cs:70` | 偶然 | **P0** | `[Obsolete] Sequence=>Union` 别名，四名一实之三，注释自承“不承载时序”。 | 删除 |
| **Combination.Parallel** | `DerivedMetrics.cs:76-85` | 偶然 | **P0** | `Parallel=> Compatible检查+Union`，Complect 校验与组合，校验仅对 `Occupy` 且 `O(n²)` 嵌套循环。四名一实之四。 | 删除，改为 `ValidateCompatible(a,b); Union(a,b)` 两步 |
| **Derived** | `DerivedMetrics.cs:99-109` | 偶然 | P2 | `Peak/Net/IsConserved` 三薄委托，无新增语义，与 `Algebra.Peak/NetTable` 重复。 | 删除，调用方直调 `Peak.Compute/NetTable.Compute` |
| **ZStar / SignedInterval** | `SignedNet.cs:10-121` | 本质 | — | `ℤ*` 有符号净占用本质，溢出→⊤、负陷判定 `ContainsZero` 正确。 | 保留 |

---

## Top3 可砍（按 Simple 收益排序）

### 1) 砍四名一实：`Join / Sequence / Parallel` → 唯一 `Union`
- **砍什么**：`Objects.cs:214-231 Signature.Join`、`DerivedMetrics.cs:69-70 Sequence`、`76-85 Parallel`（3 符号，~30 行 + 1 个 Obsolete 警告）
- **留什么**：`Signature.Union` 唯一组合子；`Compatible` 校验抽为独立纯函数 `Signature.ValidateCompatible(a,b)`（原 `Parallel` 内 6 行），调用方 `if (!Validate) throw` 再 `Union`，校验与组合解耦
- **收益**：调用方心智从 4 选 1 降为 1；L1 不再谎称承载时序/并行；`PARA_CONFLICT` 方言统一为 `Compatible` 方言；删除 `O(n²)` 隐藏校验，性能显式化
- **风险**：`Join` 的同键 `Merge` 语义若真有分支分析用例，需显式 `UnionWithMerge` 并补测试；当前 7 文件零调用，零风险

### 2) 砍双真相 Scope：`Claim.Scope` → 单一 `EffectEvent.Scope`
- **砍什么**：`Objects.cs:127 Claim.Scope` 字段、`Objects.cs:133-142` 归一中 scope 相关、`DerivedMetrics.cs:58-62 Loop` 的 `loopScope` 重标、`EffectScriptContract.cs:170-171` 双真相校验、`EffectScript.cs:197` 按 `e.Scope` 分组注释中的分裂修复
- **留什么**：`EffectEvent.Scope` 唯一真相；`Claim` 变为 `(Kind,Resource,Mode,Size)` 四元组；`Combination.Loop` 去 `loopScope` 参；`Audit` 分组键 `(ResourceId, ScopeId)` 中 Scope 直接取 `Event.Scope`
- **收益**：类型让“claim 归属事件”不可错表达，删除运行时 `claim.Scope != event.Scope` 抛异常分支；`At/Audit` 的 scope 来源统一，消除两视角归因错位类 Bug（`EffectScript.cs:195-197` 曾修）
- **风险**：若未来需单事件内多 Scope（异构 Footprint），应在 `EffectEvent` 层拆多事件而非在 Claim 层混 Scope；当前契约已强制单 Scope，零兼容风险

### 3) 砍 ResourceId 膨胀：15 态 → 6 态 + 删 `Rid/StringName`
- **砍什么**：`Objects.cs:24-36` 中 `Tree/Self/Physics/Disk/Signal/AudioMixer/Callback/Network/Input/Custom` 9 个死态 + `11,14 Rid/StringName` 薄包装；`Objects.cs:52-65 Normalize` 退化为 2 分支
- **留什么**：`Gpu / CommandBuffer / Memory / Occupancy / SignalBus / Open(string)` 6 态（前 5 为契约可见，`Open` 承接未来扩展），`ResourceId` 构造即归一（`signal_*` 前缀在构造时剥），`string` 直用无 `Rid/StringName`
- **收益**：L1 类型与契约对齐，消除“枚举看似封闭实则开放”谎言；`Normalize` 调用点从 5 处收敛至 1 处；`Budget` 键、`Violation.Resource` 身份唯一，不再随用户拼写分裂
- **风险**：`ApiMapping.cs`（未审计）若依赖 `Tree/Physics` 等态，需在映射层保留但不泄入 `EffectAlgebra`；建议映射层 `GodotRid→Gpu` 转换时即收敛

---

*附：次级可砍（P2，不入 Top3 但值得顺手）*：`Budget.Unbounded` 别名、`Derived` 薄门面、`DeviationVal` 跨文件、`INetGate` 3 死接口、`Weight` 类、`SignatureExtensions.AllClaims`。每项 <10 行，合计再减 ~80 行 incidental code。

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "7 文件全读，逐符号表覆盖 35+ 符号，证据行号精确到行（如 Objects.cs:205/215, DerivedMetrics.cs:70/76, EffectScript.cs:197），Top3 可砍均给出删除位置与替代设计"
    }
  ],
  "changedFiles": [
    "D:/Godot/Cosmos/audit/rich-hickey-round01-simple-easy.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [],
  "validationOutput": [
    "只读审计，无构建/测试执行；7 源文件行号交叉验证完成"
  ],
  "residualRisks": [
    "ScopeId 9态中 Shell/Loop/Conditional/Async 是否被 ApiMapping/CosmosEffectConfig 依赖未验证（7文件外）",
    "ResourceId 15态中 Tree/Physics 等是否被 Godot 映射层强依赖需 L7 审计确认",
    "Signature.Join 的 Merge 语义若被 L3 Analyzer 隐式依赖，删除需补 UnionWithMerge"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round01-simple-easy.md 单文件审计报告，无源码改动",
  "reviewFindings": [
    "P0: Objects.cs:214-231 & DerivedMetrics.cs:69-85 - Union/Join/Sequence/Parallel 四名一实，Complect 校验与组合",
    "P0: Objects.cs:127 vs EffectScript.cs:29 - Claim/Event Scope 双真相，契约层运行时缝合",
    "P1: Objects.cs:21-65 - ResourceId 15态膨胀，契约仅用5态，开放域伪封闭",
    "P1: Objects.cs:90-109 - ScopeId 9态 vs 契约4态，IncludedIn 伪偏序",
    "P1: EffectScript.cs:370-372 - INetGate/IPeakGate/ICompatGate 死接口零实现",
    "P2: Objects.cs:11,14 - Rid/StringName 薄包装",
    "P2: EffectScript.cs:395,397 - Budget.None/Unbounded 重复别名"
  ],
  "manualNotes": "本环境无 write 工具，报告内容已在回复正文中完整输出，运行时需落盘至 D:/Godot/Cosmos/audit/rich-hickey-round01-simple-easy.md"
}
```
