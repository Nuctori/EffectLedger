# Rich Hickey Round02 — Value / Identity / State 审计
> 透镜：Hickey 价值语义 — 值是不可变且以内容相等比较；身份是随时间变化的实体；状态是身份在时间轴上的快照。Place (位置) 可变，Value (值) 不可变。
> 范围：仅 `src/Cosmos.EffectAlgebra` 7 源文件，禁止读 `audit/`。逐符号判定 + 行号 + 严重度。

## 0. 执行摘要

| 维度 | 结论 |
|---|---|
| 真值载体 | `Rid`, `StringName`, `NodePathOrUnknown`, `NatStar`, `ZStar`, `Interval`, `SignedInterval`, `DeviationVal`, `LoopCount`, `Violation`, `AuditResult`, `ResourceId`族, `ScopeId`族 — 均为 `readonly record struct` / `abstract record` 且重写结构相等 |
| 伪装值 (Disguised Value) | `Claim` (readonly record struct 但 `default` 产非法 null), `Budget` (readonly record struct 但 `default.Caps==null`), `Interval`/`NatStar` default 与命名 sentinel 混淆 |
| 伪值/身份冒充值 | `Signature` (sealed class + 后补 Equals), `NetTable` (sealed class 无 Equals), `EffectScript` (sealed class 无 Equals) — 三者是代数核心却用引用身份承载值语义 |
| 隐藏可变状态 | `NetTable._net: Dictionary`, `Signature._read/_write/_occupy` 非 readonly 字段 + 局部 `Dictionary/HashSet/List` 在 Audit/Join 中可变别名，`EffectScript.Events` 的 `ImmutableArray` default 陷阱 |
| 总体 | L1 已有意識朝值语义修复（R3/R5 引入 Budget 防御拷贝、Signature Equals、EffectEvent Loop 守卫），但三大聚合根仍是 class 身份，与 Hickey "值不可变、身份显式" 相悖 |

---

## 1. 逐符号判定总表

### 1.1 Algebra.cs — `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Algebra.cs`

| 符号 | 类型形态 | 行 | 判定 | 证据 | 严重度 |
|---|---|---|---|---|---|
| `Compatible` | `static class` 无状态 | 12-29 | **真值工具** | 无字段；`Resolve` + `IsCompatible` 纯函数对称；无可变 | — |
| `Weight` | `static class` | 35-39 | **真值工具** | 纯函数 `Of` 抛 `InvalidOperationException` 表 partial，非 NaN 毒值 | — |
| `NetTable` | `sealed class` | 46-110 | **伪装值 / 身份** | `private readonly Dictionary<ResourceId,SignedInterval> _net` 可变字典 (L49)；class 无 `Equals/GetHashCode/==`；`Resources => _net.Keys` 暴露 live `KeyCollection` (L88)；`Compute` 返回新 heap 身份，内容相等但 `==` 为引用相等 | **P0** |
| `Peak` | `static class` | 117-135 | **真值工具** | 纯 `Compute`；量纲隔离已修 `if(c.Kind != Occupy) continue` L127 | — |
| `SignatureExtensions.AllClaims` | 扩展 | 138-147 | **真值工具** | `yield return` 三桶枚举，纯、无状态 | — |

### 1.2 Objects.cs — `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Objects.cs`

| 符号 | 类型形态 | 行 | 判定 | 证据 | 严重度 |
|---|---|---|---|---|---|
| `Rid` | `readonly record struct(string)` | 11 | **真值** | 结构相等，`string Value` 不可变 | — |
| `StringName` | `readonly record struct(string)` | 14 | **真值** | 同上 | — |
| `ResourceId` | `abstract record` + 15 sealed record | 21-66 | **真值** | `record` 结构相等标签+字段；`Normalize` 单一真相 L52 幂等；`SignalBus` 分支防二次剥离 L62 | — |
| `NodePathOrUnknown` | `readonly record struct` | 69-84 | **真值** | `IsUnknown+Path` 值字段；`Unknown` 单例 `new(true,"")` L80；私有构造收口 | — |
| `ScopeId` | `abstract record` + 8 sealed | 90-109 | **真值** | 偏序 `IncludedIn` 用 `Equals` 自反 + Global 最大元；跨标签 false | — |
| `Kind` | `enum` | 112 | **真值** | 穷举 | — |
| `Mode` | `enum` | 117 | **真值** | 穷举 | — |
| `Claim` | `readonly record struct(Kind,ResourceId,Mode,ScopeId,Interval?)` | 127-146 | **伪装值** | `with`/`default` 可产 `null Resource/Scope` 与 `0` 值；`Normalize` 抛 `ArgumentException` 守卫 Read 非 Use；但类型自身未封 `default`，依赖 `Signature.Of/Add` 边界 fail-fast (L190-191) | **P1** |
| `Signature` | `sealed class` | 153-261 | **伪装值 / 身份** | 三桶 `ImmutableHashSet<Claim>` 字段 **非 readonly** L155-157；`Empty` 单例 class；虽补 `Equals/SetEquals` + `GetHashCode` XOR + `==/!=` L237-260，但仍为 heap 身份；`Add` 用对象初始化器 `new Signature{_read=...}` 拷贝；`Join` 内 `Dictionary<(Kind,ResourceId,Mode,ScopeId),Interval>` 局部可变 L217 | **P0**  |

### 1.3 Numeric.cs — `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Numeric.cs`

| 符号 | 类型形态 | 行 | 判定 | 证据 | 严重度 |
|---|---|---|---|---|---|
| `NatStar` | `readonly record struct` | 8-63 | **真值** | `IsTop+Value`；`default` = `Of(0)` 合法；`+/*` 无 `checked` 环绕→Top 保守 L29/L38；`Max/Min/CompareToFinite` 内嵌 ⊤ 律 | — (P2 备注见 §3) |
| `Interval` | `readonly record struct(NatStar, NatStar)` | 70-105 | **真值（带 default 陷阱）** | 构造子校验 `lo.IsTop && !hi.IsTop` 抛 + `lo>hi` 抛 L83-85；但 `default(Interval)` 绕过构造 = `[0,0]` != `Default[1,1]`；`Default`/`Dynamic`/`Exact` 命名 sentinel 与 default 混淆 | **P2** |
| `DeviationVal` | `readonly record struct` | 111-132 | **真值** | `IsTop+double Value`；`default`=0 合法；`ExceedsThreshold` 先判 `IsTop` L128 | — |

### 1.4 EffectScript.cs — `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs`

| 符号 | 类型形态 | 行 | 判定 | 证据 | 严重度 |
|---|---|---|---|---|---|
| `EffectEvent` | `readonly record struct` | 23-58 | **伪装值（值内含身份）** | 字段 `Lifetime Interval`+`Scope ScopeId`+`Loop LoopCount` 均为真值，但 `Footprint Signature` 为 class 身份；record struct 相等会委托 `EqualityComparer<Signature>.Default` → 击中 `Signature.Equals` 值相等，表面值相等但内部共享引用；构造子守卫 `lifetime.Lo.IsTop` 抛 L43 + `!loop.IsValid` 抛 L47 | **P1** |
| `EffectScript` | `sealed partial class` | 64-367 | **伪装值 / 身份** | `ImmutableArray<EffectEvent> Events` + `Budget Budget` 本应为值；但 class **无 Equals/GetHashCode/==**；全等剧本 `==` 为 false；`Budget` 构造 `budget.Caps!=null ? budget : Budget.None` L76 处理 default null；可变性已修 `Budget {get;}` 非 `{get;init;}` L70 | **P0** |
| `Budget` | `readonly record struct : IEquatable<Budget>` | 378-421 | **真值（带 default 陷阱）** | 字段 `IReadOnlyDictionary<ResourceId,NatStar> Caps` 实际 `ImmutableDictionary` 防御拷贝 L389；`Normalize` 按归一键分组 L390；`Equals` 内容比较含 `null=>Empty` L403；`GetHashCode` 内容哈希 L411；但 `default(Budget).Caps==null` 绕过构造 L138 需归一 `Budget.None` | **P1** |
| `AuditResult` | `readonly record struct` | 426-456 | **真值** | `Passed`+`ImmutableArray<Violation>`+`CapsChecked`；构造子强制 `passed==IsDefaultOrEmpty` L446 fail-fast；派生 `IsPeakChecked` L455 | — |
| `Violation` | `readonly record struct` | 459-489 | **真值** | 6 字段纯数据；无可变 | — |
| `EffectScript.Audit` 局部可变 | 方法内 Place | 161-302 | **隐藏可变状态（局部）** | `net Dictionary`, `peakSum Dictionary`, `topCount Dictionary`, `grp Dictionary<...,HashSet<int>>` 内含可变 `HashSet<int>` 别名 L164；`sweep List<(ulong,int,bool)>` L145 排序；均为方法局部，不外泄，但 `grp` 的 HashSet 共享可变违反 Place 隔离理想 | **P1** |

### 1.5 EffectScriptContract.cs

| 符号 | 行 | 判定 | 证据 |
|---|---|---|---|
| `EffectScriptContract` (static class) | 18-326 | **真值工具** | 无字段；`Parse`/`ToJson` 纯搬运；`RejectUnknownKeys` 白名单 L49；`ParseBudget` 接受 `"⊤"/"inf"` L232；`Budget` 防御拷贝后交 `new Budget(caps)` |

局部可变 `List<EffectEvent>` L31, `Dictionary<ResourceId,NatStar>` L227 等均为方法内临时 place，不构成跨调用状态，符合 Hickey 局部 place 可变。

### 1.6 DerivedMetrics.cs — `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/DerivedMetrics.cs`

| 符号 | 行 | 判定 |
|---|---|---|
| `LoopCount` | 10-35 | **真值典范** `readonly record struct` 包 `NatStar`；`Of(n)` 拒 0 L18；`IsValid` 派生 `IsTop\|\|Value>=1` L26 统一守卫；`TryOf` 返回 `default` 非法值让 `IsValid` 显式化 |
| `Combination` | 42-93 | **真值工具** `Loop/Parallel/Sequence` 纯；`Loop` 守卫 `default(LoopCount)` 抛 L54；`Scale` 纯 |
| `Derived` | 99-109 | **真值工具** 纯转发 `Peak.Compute/NetTable.Compute` |

### 1.7 SignedNet.cs — `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/SignedNet.cs`

| 符号 | 行 | 判定 |
|---|---|---|
| `ZStar` | 10-69 | **真值** `readonly record struct IsTop+long`；`+`/`-` Top 传播 + 同号/异号溢出→Top L37/L50；`Min` 已修 R2-002 L59 |
| `SignedInterval` | 76-121 | **真值** `readonly record struct ZStar×ZStar`；构造子 `lo>hi` 抛 L87；`ContainsZero` fail-closed on Top L98；`Add` 逐端相加 L102 vs `Merge` min/max L105；`default` = `[0,0]` 合法但绕过校验 |

---

## 2. 隐藏可变状态清单

| 位置 | 文件:行 | 形态 | 是否外泄 | Hickey 评价 |
|---|---|---|---|---|
| `NetTable._net` | Algebra.cs:49 | `Dictionary<ResourceId,SignedInterval>` 可变 | 否（private），但 `Resources=>_net.Keys` 暴露 live view L88 | Place 误放入 Value 对象；应 `ImmutableDictionary` |
| `Signature._read/_write/_occupy` | Objects.cs:155-157 | 字段非 `readonly`，类型 `ImmutableHashSet` 不可变但引用可重绑 | 否（private） | 身份对象的可重绑字段破坏“值无身份” |
| `Signature.Join.merged` | Objects.cs:217 | `Dictionary<...,Interval>` | 局部 | 允许（ephemeral place）但与 `ImmutableDictionary` 混用风格不一致 |
| `EffectScript.Audit` locals | EffectScript.cs:161-167 | 6 个 `Dictionary` + `HashSet<int>` 别名 + `List` | 局部 | 局部 place 可变符合 Hickey，但 `HashSet<int>` 共享别名是引用陷阩的微型复刻 |
| `EffectScript.Events` default | EffectScript.cs:67 | `ImmutableArray` default = `IsDefault` | 构造后不可变 | 值类型的非法 default 未在构造期拒绝 |
| `Budget.Caps` default null | EffectScript.cs:381 | `IReadOnlyDictionary` null | 通过 `Budget.None` 归一 | 经典 struct default 后门，需 `IsValid` 类似 `LoopCount` |

> 规则：**Place**（局部变量、临时字典）可变无罪；**Value**（`NetTable`/`Signature`/`EffectScript` 实例字段）可变有罪。本轮有罪项为前三者。

---

## 3. 引用相等陷阱

| 陷阱 | 文件:行 | 场景 | 后果 |
|---|---|---|---|
| `NetTable` 引用相等 | Algebra.cs:46 | `var a=NetTable.Compute(sig,scope); var b=NetTable.Compute(sig,scope); a==b => false` | 缓存、去重、测试 `Assert.Equal` 失效；需 `IsConserved` 间接比较 |
| `Signature` 引用 vs 值相等 | Objects.cs:236-260 | 已补 `Equals/SetEquals` 但仍是 class；`ReferenceEquals(a,b)` 与 `a==b` 分裂；`==` 重载静态 `Equals(a,b)` 会处理 null 但 `a.Equals(b)` 与 `a==b` 语义仍依赖虚派发 | 调用方若用 `Dictionary<Signature, ...>` 依赖 `GetHashCode` XOR 虽可用但不同引用同内容可命中，行为分裂 |
| `EffectScript` 无值相等 | EffectScript.cs:64 | `new EffectScript(events)==new EffectScript(events) => false` | AI 产出 JSON round-trip 后 `Parse(ToJson(x)) != x` 按引用判不等；去重/缓存失效 |
| `EffectEvent` 值内含身份 | EffectScript.cs:32 | `EffectEvent.Footprint` 为 class；`event1==event2` 因 `Signature.Equals` 值比较而为 true，但 `event1.Footprint` 与 `event2.Footprint` 是不同 heap 引用，`ReferenceEquals` 仍 false | 混淆值与身份的边界；`with` 拷贝共享同一 `Signature` 引用 |
| `Budget.Caps` 接口别名 | EffectScriptContract.cs:251 | `ulong.Parse` 未用 `TryParse` 直接抛；`ImmutableDictionary` 经 `IReadOnlyDictionary` 暴露，可被 `as IDictionary` 强转？已修为 `ImmutableDictionary` 单例 `None` 但接口仍允许 `as` 尝试 | 已通过不可变单例缓解 |
| `ImmutableArray` default | EffectScript.cs:67 | `default(EffectScript).Events.IsDefault==true` 与 `Empty` 语义不同 | 未校验时 `At` 遍历空但 `ComputeSamplePoints` 加 `NatStar.Of(0)` 兜底 |

---

## 4. 严重度汇总与最小修复

### P0 — 阻断合并

1. **NetTable 伪值** `Algebra.cs:46` — 转 `sealed class` → `readonly record` 或 `sealed class : IEquatable<NetTable>` + `ImmutableDictionary<ResourceId,SignedInterval>` + `Equals/GetHashCode` + `sealed` 且 `Resources` 返回 `IReadOnlyCollection` 拷贝。
2. **Signature 身份** `Objects.cs:153` — 字段加 `readonly`，考虑 `sealed record` 或显式 `IReadOnlySet<Claim>` 暴露；`GetHashCode` XOR 已够但建议 `HashCode.Combine` 顺序无关折叠已满足。
3. **EffectScript 身份** `EffectScript.cs:64` — 补 `Equals/GetHashCode/==` (按 `Events` 序列相等 + `Budget` 值相等) 或改为 `record`；校验 `Events.IsDefault` → `ImmutableArray.Empty`。

### P1 — 发布前应修

4. **Claim/Budget default 后门** `Objects.cs:127`/`EffectScript.cs:381` — 为 `Budget` 加 `IsValid` 类似 `LoopCount`，`EffectScript` 构造拒 `Events.IsDefault`。
5. **NetTable.Resources live view** `Algebra.cs:88` — 改 `=> _net.Keys.ToImmutableArray()` 或 `IReadOnlyCollection`。
6. **Audit grp HashSet 共享可变** `EffectScript.cs:164` — 注释 `// place: grp value is set, mutated only via Step` 或改为 `ImmutableHashSet<int>` 每次 `Add/Remove` 产生新集（性能权衡，当前局部可接受但需显式 place 标注）。
7. **LoopCount/SignedInterval/Interval default 混淆** `Numeric.cs:70`/`SignedNet.cs:76` — 文档化 `default` 合法性或加 `IsValid` 派生。

### P2 — 报告留存

- `Budget.GetHashCode` 遍历 `Caps` 顺序依赖 `ImmutableDictionary` 迭代序但 XOR 交换律已缓解。
- `Signature.Join` 与 `EffectScript.Audit` 的局部 `Dictionary` ephemeral place 可变符合 Hickey，不阻塞。
- `Peak`/`Weight`/`Compatible` 已是纯函数典范，无需改动。

---

## 5. 残余风险 (Hickey 视角)

- 即使修 P0，`Claim` 的 `with` 仍可产非法 `null`，需 Roslyn Analyzer 禁 `with` 产非法或运行时 `where` 约束；当前靠 `Signature.Of/Add` 边界守卫是最小可用。
- `ImmutableArray<EffectEvent>` 的 `default` 与 `Empty` 分裂未完全收口，若 AI 侧反序列化绕过 `EffectScript` 构造直接 `default`，`Audit` 仍按 `Endpoints` 空集处理为 `[0]` 采样点，非崩溃但语义偏移。
- `ZStar`/`NatStar` 的 `Top` 传播已保守，但 `Budget` 的 `Top` budget 键在 `ParseBudget`/`SerializeBudget` 往返为 `"⊤"` 字符串，需确保 AI 提示词不产 `"inf"` 以外别名（已接受 `inf`）。

## 6. 判定方法

- 每文件 `Read` 全量源码 + 行号锚定
- 符号级 `IsTop/IsValid/default` 探针
- `Dictionary`/`HashSet` 可变别名 grep（本轮为人工逐行，未用 shell）
- 未读 `audit/` 目录

---

*审计员：Rich Hickey 透镜 — 值不可变、身份显式、状态受控。P0 三项不改，值语义即谎言。*
```

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "7/7 源文件逐符号判定，产出 audit/rich-hickey-round02-value-identity-state.md，含 3×P0 (NetTable:46, Signature:153, EffectScript:64) + 4×P1 + P2 行号锚定，附隐藏可变状态与引用相等陷阱清单"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round02-value-identity-state.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [],
  "validationOutput": [
    "Read 7 source files via default.read; no audit/ reads; markdown artifact returned for runtime persistence"
  ],
  "residualRisks": [
    "Claim with/default 仍可在类型层面产 null Resource/Scope，依赖 Signature.Of/Add 边界守卫，需 Analyzer 补强",
    "EffectScript/NetTable/Signature 三大聚合根仍为 class 身份，P0 未改前缓存与去重将误判引用不等",
    "Budget/Interval default 与命名 sentinel 分裂，需 IsValid 或构造期拒绝 default(ImmutableArray) 才能彻底封闭"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 Rich Hickey value/identity/state 轮次审计报告，覆盖 7 文件 30+ 符号的真值/伪装值/身份判定与可变状态清单",
  "reviewFindings": [
    "P0: Algebra.cs:46 - NetTable sealed class 无 Equals，_net 为可变 Dictionary，Resources 暴露 live Keys",
    "P0: Objects.cs:153 - Signature sealed class 伪装值，_read/_write/_occupy 非 readonly，虽补 Equals 仍 heap 身份",
    "P0: EffectScript.cs:64 - EffectScript sealed class 无 Equals/ GetHashCode，值语义剧本用引用相等",
    "P1: Objects.cs:127 - Claim readonly record struct default/with 可产 null Resource/Scope",
    "P1: EffectScript.cs:381 - Budget readonly record struct default.Caps==null 需归一 None",
    "P1: Algebra.cs:88 - NetTable.Resources 返回 Dictionary.Keys live view",
    "P1: EffectScript.cs:164 - Audit grp Dictionary<...,HashSet<int>> 共享可变 HashSet 别名",
    "P2: Numeric.cs:70 - Interval default [0,0] 绕过构造校验与 Default[1,1] 混淆",
    "P2: SignedNet.cs:76 - SignedInterval default [0,0] 同型混淆，Budget GetHashCode 顺序依赖"
  ],
  "manualNotes": "无写入工具，markdown 内容已在回复中内联，运行时需持久化到 D:/Godot/Cosmos/audit/rich-hickey-round02-value-identity-state.md"
}