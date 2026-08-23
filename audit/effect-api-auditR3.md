# effect-api-auditR3 — 认知负荷 / 上手成本 / 人体工学审计（Rich Hickey 视角 + Jeff Dean 务实）

## 独立声明（立场：宁误报「难用」）

本报告**仅**基于以下 7 个源文件独立推导，未读取 `audit/` 下任何历史文件：
`EffectScript.cs`、`EffectScriptContract.cs`、`Objects.cs`、`Numeric.cs`、`Algebra.cs`、`SignedNet.cs`、`DerivedMetrics.cs`。

**结论先行：** 这个子系统的「数学正确性」与「类型护栏」做得相当扎实（record struct、⊤ 闭包、fail-fast JSON），
但**面向用户的认知表面极大且不一致**，新手（AI 或人类）写对一个合法 `EffectScript` 需要同时记住约 13 条隐式规则，
而且其中多条规则之间存在**静默不一致**（scope 分裂、peak 语义分裂、JSON 资源 5 个 vs 类型 14 个）。
我会把它们都当作 HIGH 先报，再下调，不替用户脑补宽容度。

---

## 逐概念表（概念 | 认知负荷 | footgun | 严重度）

| # | 概念 | 认知负荷 | footgun 证据（文件:行） | 严重度 |
|---|------|----------|--------------------------|--------|
| 1 | **公共类型总数量**（~23 个） | 高：EffectScript/EffectEvent/Budget/AuditResult/Violation/Contract/Claim/Kind/Mode/ResourceId(+14 子类)/ScopeId(+8 子类)/Signature/Interval/NatStar/ZStar/SignedInterval/LoopCount/Combination/Compatible/Weight/NetTable/Peak/Derived/DeviationVal 全部公开。无统一入口/命名空间分区。 | 用户第一眼不知道「从哪开始」；没有 `EffectScript.Builder` 或 Examples。 | HIGH |
| 2 | **Kind{Read,Write,Occupy}** | 中：`occupy` 是领域黑话（"持有资源"），与 read/write（瞬时 I/O）维度不同，但命名上 `occupy` 易与 `use`/`Occupancy` 资源混淆。 | `occupy`(Kind) vs `Occupancy`(ResourceId，audio_channel/animation_state) 同名不同物 —— `Objects.cs:31,111`。 | MED |
| 3 | **Mode{Use,Create,Release,Move,Unknown}** | 高：5 个值，且**与 Kind 构成 3×5=15 种组合**，绝大多数语义无意义却都被静默接受（无类型级 guard）。create vs move 仅在 *兼容表* 上不同，在 *net/peak* 上完全等价（都 +符号）。 | `NetTable.Compute` 仅 release 取负，create/move 同为正 —— `Algebra.cs:63`。用户易以为 move=「中性再定位（净零）」，**实际与 create 一样 +size**。 | HIGH |
| 4 | **Claim 五元组位置 record** `(Kind,Resource,Mode,Scope,Interval?)` | 高：位置式 5 参，易把 `Mode` 与 `Scope`（二者都是 record/enum）写反；`Size` 为可空 Interval，漏写 → 默认 `[1,1]`（见 #10），**不报错**。 | `Objects.cs:126` 位置参数；`Objects.cs:130` Normalize 把 null size 补成 `Interval.Default=[1,1]`。 | HIGH |
| 5 | **EffectEvent 4 字段位置 record** `(Lifetime,Scope,Footprint,Loop)` + 3 参便捷构造 | 中：两个构造器前 3 参相同，3 参版默认 `ω=1`（`EffectScript.cs:47`），掩盖 `Loop`/ω 语义；位置顺序 lifetime→scope→footprint→loop 需背。 | `EffectScript.cs:38`/`47`。缺省 ω=1 让新手以为「loop 不重要」。 | MED |
| 6 | **ScopeId 8 子类 vs JSON 4 子类** | 高：类型有 Method/Type/Scene/Global/Shell/Loop/Conditional/Async 共 8 种，但 JSON 解析只认 scene/type/method/global（`EffectScriptContract.cs:85-96`），**未知 type 静默回退成 Scene**（`_ => new ScopeId.Scene(name)`）。Shell/Loop/Conditional/Async 在 JSON 契约里**根本无法表达**。 | `EffectScriptContract.cs:96` 静默回退；`Objects.cs:89-110` 8 子类。 | HIGH |
| 7 | **ScopeId.Loop（作用域） 与 LoopCount ω（并发副本数）命名碰撞** | 高：`ScopeId.Loop` 是「循环区域作用域」，`LoopCount` 是「瞬时并发副本数 ω」，两者都叫 "loop"。新手极易把「给事件设个 loop 作用域」和「设 ω=⊤ 常驻」当成一件事。 | `Objects.cs:107`(ScopeId.Loop) vs `DerivedMetrics.cs:18-21`(LoopCount.Top)。 | HIGH |
| 8 | **scope 分裂：event scope vs claim scope** | 高（核心 footgun）：JSON claim 自带 `scope`，但 `At`/`Net`/`Peak` 经由 `Combination.Loop(..., e.Scope)` **把每个 claim 的 Scope 覆写成 Event.Scope**（`DerivedMetrics.cs:41-45`，`EffectScript.cs:79`）。结果：claim 上写的 scope **对数学计算无效**，却又是 Parse 必填字段（`EffectScriptContract.cs:121`）。 | `DerivedMetrics.cs:41` `c with { Scope = loopScope }`；`EffectScript.cs:164` 注释承认「此前用 c.Scope 与 At 视角 scope 分裂」。 | HIGH |
| 9 | **Algebra 级 Net/Peak 与 EffectScript 级 Net/Peak scope 语义不一致** | 高：直接 `Derived.Net(sig, scope)`/`Peak` 用 **claim 自带 scope** 做 `IncludedIn` 过滤（`Algebra.cs:60,119`）；而 `EffectScript.Audit` 的 peak 按 **resource 全局** 计（key 仅 `r`，无 scope，`EffectScript.cs` peakSum[r]）、conflict 按 `e.Scope`。同一 Signature 走两条路径得到不同结论。 | `Algebra.cs:60` vs `EffectScript.cs` `peakSum[r]`（gate2 仅按资源）。 | HIGH |
| 10 | **Interval 缺省 [1,1]（Default）/ 显式 Exact(0)=[0,0]** | 中：漏写 size → 占 1 单位；想表达「零占用」必须显式 `Exact(0)`。隐式不变量「size 缺省 [1,1]」不直觉。 | `Numeric.cs:92` Default=[1,1]；`Objects.cs:129` 注释「null 与 Exact(0) 区分」。 | MED |
| 11 | **NatStar ⊤ 哨兵**（"⊤" 字符串） | 中：JSON 里 `hi:"⊤"` / `loop:"⊤"` 表示上界开放（非 IEEE ∞）。手敲 ⊤ 易错（全角/半角/拼写）；`NatStar.Of(0)` 是有限 0 不是 ⊤。任一 ⊤ 参与 Peak → 整体 ⊤ → 若有 cap 则 PeakExceeded。 | `EffectScriptContract.cs:77-79,100-102`；`Numeric.cs` 算术遇 ⊤ 即 ⊤。 | MED |
| 12 | **LoopCount ω=⊤ ⇒ 居民层豁免守恒检查** | 高：设 `loop:⊤` 会让该事件**免 Leak 检查**（`EffectScript.cs:263` `if (e.Loop.Count.IsTop) continue;`）。用户排查「为什么没报 Leak」必须知道这条隐藏豁免。 | `EffectScript.cs:263,90`（居民层豁免注释）。 | HIGH |
| 13 | **ω 语义="瞬时并发副本" 而非"时间重复"** | 高：ω 解释（`EffectScript.cs:34`：「同一时刻有多少个该元素并发存在」）反直觉；多数用户会把 loop 理解成「循环 N 次（时间上）」。 | `EffectScript.cs:34` 注释 vs 直觉。 | HIGH |
| 14 | **ResourceId 14 子类 vs JSON 5 键** | 高：类型有 14+ 资源子类，JSON 只认 gpu/commandBuffer/memory/occupancy/signalBus（`EffectScriptContract.cs:139-157`）；写 "tree"/"physics"/"signal" 直接 `FormatException`。Serialize 兜底 `_ => memory:0`（`EffectScriptContract.cs:229`）与 Parse 兜底 `未知 budget 键` 抛错（`EffectScriptContract.cs:172`）**两边不一致**。 | `Objects.cs:20-49`(14 子类) vs `EffectScriptContract.cs:139`。 | HIGH |
| 15 | **ResourceId.Normalize 幂等边界** | 低-中：`SignalBus("signal_signal_x")` 不再二次剥前缀的幂等修复说明了映射易错；归一规则只写在注释（`Objects.cs:44-48`），无编译期保证。 | `Objects.cs:44-48`。 | LOW |
| 16 | **JSON 嵌套深度与 "⊤" 哨兵** | 高（AI 编写成本）：events→lifetime[lo,hi]→scope{type,scene}→footprint[claim{kind,resource{memory:..},mode,scope,size[lo,hi]}]。resource 必须是**只含 1 个**特定键的对象；budget 键 `"gpu:xxx"`。全手写 JSON 极啰嗦、易漏字段（缺字段即 FormatException）。 | `EffectScriptContract.cs` 全篇 `Require`/`FormatException`。 | HIGH |
| 17 | **lifetime Lo=⊤ ⇒ 永不存活** | 中：用户写 `lo:"⊤"` 想表达「从开始」会得到**静默死亡事件**（被跳过，`EffectScript.cs:286` 注释）。 | `EffectScript.cs:286`；sweep `if (lt.Lo.IsTop) continue;`。 | MED |
| 18 | **Budget/Peak 默认无上限** | 中：默认 `Budget.None`（所有资源无 cap），Peak 门**恒过**；用户可能以为「峰值被约束了」其实没有。C# 直接构造 `EffectScript` 时 `Budget` 也静默默认 None（`EffectScriptContract.cs:243`）。 | `EffectScriptContract.cs:243`；`EffectScript.cs` Budget.None。 | MED |
| 19 | **守恒是「区间含 0」而非「size 相等」** | 中：create[1,10]+release[1,10] → net[-9,9] 含 0 → 视为闭合（不报 Leak），即便 size 不等。新手以为必须「对称相等」才能闭合。 | `SignedNet.cs` `ContainsZero`；`Algebra.cs:63`。 | MED |
| 20 | **Combination.Sequence/Parallel 都=Union** | 低-中：命名暗示「时序/并行」差异，但实现都是 ∪，无时序保证。用户误以为 Sequence 强制先后。 | `DerivedMetrics.cs:48,53`。 | LOW |
| 21 | **Conflict 判定「同 (res,scope,mode)≥2 事件」** | 中：单事件内 ω 份副本**不**触发冲突（按 EventIdx 分组），跨事件同 mode 才冲突；且 create+release 跨事件兼容。`CompatibleConflict` 语义需读 §3.2.3 才懂。 | `EffectScript.cs:164`+gate3 注释。 | MED |
| 22 | **Mode.Unknown / "unknown" 折叠为 Use** | 低：`unknown` 与 `use` 等价，冗余且易让用户以为「unknown 是占位/待定」其实已按最弱兼容处理。 | `Algebra.cs` `Resolve`；`EffectScriptContract.cs:136`。 | LOW |
| 23 | **At(t) 重算 vs Audit 扫换线** | 中：用户需理解 `At(t)`（快照）与 `Audit`（端点采样 + 闭包检查）是两套入口，闭包 Leak 检查独立于扫换线（`EffectScript.cs:259-280`）。 | `EffectScript.cs:259`。 | LOW |

---

## 上手成本量化（从零写一个合法 EffectScript 需记住的规则）

1. `EffectEvent` = 4 位置字段（Lifetime, Scope, Footprint, Loop）；3 参便捷版默认 ω=1。
2. `Lifetime` = `Interval[lo,hi]`；`hi:"⊤"`=常驻上界开放；**`lo` 不能为 ⊤**（否则事件永不存活）。
3. `ScopeId` JSON：`{scene}` 默认 Scene；`{type:"method",scene}`/`{type:"type",scene}`/`{type:"global"}`；未知 type 静默回退 Scene；其余 4 种作用域 JSON 不可表达。
4. `Footprint` = `Signature` = claim 数组。
5. `Claim` = 5 位置（kind, resource, mode, scope, size?）；kind∈{read,write,occupy}；mode∈{use,create,release,move,unknown}。
6. **claim 上的 scope 在 At/Net/Peak 路径被 Event.Scope 覆写**——写了也白写（除非直接调 `Derived.Net/Peak`）。
7. resource JSON 必须是对象且**只含** gpu/commandBuffer/memory/occupancy/signalBus 之一。
8. size 缺省 `[1,1]`；要零占用用 `Exact(0)`。
9. `LoopCount`：`Of(n)` 或 `Top`（常驻，**免 Leak**）。
10. occupy 的 create 必须配对称 release（ω=⊤ 除外），否则 Leak；守恒看「区间含 0」非 size 相等。
11. `⊤` 哨兵在 JSON 里是字符串 `"⊤"`。
12. Budget 默认无上限（Peak 门恒过），要约束须显式 `budget`。
13. 直接 C# 构造 `EffectScript` 时 `Budget` 静默 = None。

→ **约 13 条隐式规则**，其中 #6、#9、#12 是「静默行为」，最易踩。

---

## 总评

**这个项目会造成用户（尤其 AI）使用困难。** 不是因为数学错，而是因为：

- **认知表面过大且无导航**（#1，~23 个公共类型、无 Builder/Example）。
- **存在多处静默不一致**（#8 scope 分裂、#9 peak/Net scope 语义分裂、#14 resource 类型/契约不对称、#18 默认无上限），用户无法从单一心智模型推断全貌，只能靠读源码注释（且注释分散在 7 个文件、引用 §3.x 外部文档）。
- **隐式不变量不直觉**（#10 size 默认 1、#12 ω=⊤ 免 Leak、#19 区间含 0 即守恒）。
- **易混淆命名**（#7 loop 双重含义、#2 occupy vs Occupancy）。

### Top 3 应降低的认知负荷

1. **消除 scope 分裂（#8/#9）：** 统一「claim scope 与 event scope 谁说了算」，并让 `EffectScript.Audit` 的 Peak/Net 与 `Derived.Peak/Net` 语义一致（要么都按 event scope 重写，要么都按 claim scope，并显式文档化）。当前两 API 对同一 Signature 给出不同结果，是最高危的认知陷阱。
2. **收敛 Mode×Kind 组合爆炸（#3）：** 用判别联合或构造器约束，让「非法但被静默接受」的 15-3≈12 种组合在编译期不可表达（例如 `OccupyClaim(Mode,Scope,Size)` 与 `IoClaim(Kind,Scope)` 分开），并明确 move 在 net 上等同 create。
3. **给 JSON/契约一个可达的入口与最小示例（#1/#16）：** 提供 `EffectScript.Builder` 或一份「5 行写出合法剧本」的样例 + 把「14 子类资源 → 5 JSON 键」「8 作用域 → 4 JSON 作用域」的映射从注释提升为可见常量/报错信息，消除「写了 type 变 Scene」「写 tree 直接炸」的静默/突兀行为。

> 备注：本报告刻意以「宁误报难用」为口径；#22/#20/#23 等 LOW 项即使实际影响有限也保留以便用户自行判断。所有判定均锚定源码行号，未引用任何外部审计文档。
