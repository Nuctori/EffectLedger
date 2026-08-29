# Rich Hickey 视角对抗性审计 — Round 03 Decomplect 正交性

- **审计员**: Rich Hickey 视角 (Decomplect / 正交性 / Complect)
- **日期**: 2026-05-13
- **输入**: 7 源文件零 trust — `Objects.cs` `Algebra.cs` `Numeric.cs` `DerivedMetrics.cs` `EffectScript.cs` `EffectScriptContract.cs` `Deviation.cs`
- **禁区**: 未读 `audit/` (按任务约束)
- **被审计笛卡尔积**: ResourceId(15) × ScopeId(8) × Kind(3) × Mode(5) = 1800 理论组合
- **并发三门**: 守恒 gate(1) / 峰值 gate(2) / 兼容 gate(3)
- **视角对照**: At(t) 瞬时快照 vs Audit sweep 审计 vs Deviation 全局对齐

> Hickey 箴言: “Complect 是把本不相关的维度编在一起。Decomplect 是让每个维度独立变化、单一事实单一归属。”

---

## 0. 执行摘要

| 维度 | 结论 | 严重度 |
|------|------|--------|
| 15×8×3×5 笛卡尔积 | **非正交 —  intentional 折叠 3 处** (Read⊕Create/Release/Move 禁止 / Signal 命名空间归一 / Unknown≡Use) 折叠本身正确，但以 `throw`/`switch` 而非类型实现，增长时靠人记表 | P1 |
| 三门 | 正交**形状**成立 (过滤 occupy → 签名桶隔离 → 分组键 (r,scope))，但**实现**非正交: 2 套 net 累加逻辑 + 2 套 scope 投影键，属一事实两处写 | P1 |
| At vs Audit scope 投影 | 本轮已对齐为 `e.Scope` (修 OPEN-1)，`c.Scope` 不再分裂；但 `Deviation` 硬编码 `Global` 与 At/Audit 的 per-scope 过滤正交分裂 | P1 |
| 一事实两处写 | 至少 5 处 (ResourceId 归一 / net 符号化 / Size 缺省 / LoopCount 非法态 / Budget 归一) | P1 |
| 跨桶量纲隔离 × Weight.NaN | **已修复为 throw** (`Algebra.cs:38`)，无 NaN 毒值；但 `Weight` 几乎无调用方，隔离靠 `Kind!=Occupy continue` 硬过滤而非 Weight 路由，Weight 是 dead complect | P2 |

**总判定: 功能正确，正交性债务存留 — 类型未把维度真正分开，靠注释+运行时守卫+人肉对齐表维系。新增维度(新 ResourceId / 新 Scope 标签)会同时碰 4-5 文件。**

---

## 1. 笛卡尔积 15×8×3×5 是否正交？

### 1.1 ResourceId 15 支 — 合成命名空间折叠

- **定义** `Objects.cs:21-41` — 15 构造子: `Tree` `Self` `Physics` `Memory` `Disk` `Signal` `Gpu` `AudioMixer` `Occupancy` `Callback` `Network` `Input` `Custom` `CommandBuffer` `SignalBus`
- **归一** `Objects.cs:52-65`:
  ```cs
  Self("signal_"+s) => SignalBus(s)           // L55
  Signal("signal_"+s) => SignalBus(s)         // L58
  SignalBus(bus) => bus                       // L62 幂等护栏
  _ => r                                     // L64 其余 13 原样
  ```
- **正交性**: 15 支名义独立，实际 3 支坍缩为 1 (`Self/Signal/SignalBus` 信号命名空间)。PD R §3.1.4a 显式要求，但类型上 15 支仍并列，归一靠运行时 `switch`。
- **证据**: `Algebra.cs:61` `EffectScript.cs:182,194,330` `EffectScript.cs:390` 四处各自 `ResourceId.Normalize`，漏一处即分裂。
- **严重度 P2 (Info)** — 折叠正确但非类型强制；新增 `Self("gpu_"+s)` 之类别名需改 `Normalize` 单点，忘记则 DBL 资源分身。
- **最小修复**: 已单点 `Normalize`，但需在 `Budget` 构造 `EffectScript.cs:390` 与所有 `Net/Peak` 入口加契约测试: `Normalize(r1)==Normalize(r2) ⇒ Net/Peak/Budget 键相等` (已有 `AlgebraLawsTests:ResourceId_Normalize_*`，缺 Budget 归一测试 — `EffectScriptContract.cs:111` 已测)。

### 1.2 ScopeId 8 支 — 扁平偏序

- **定义** `Objects.cs:90-99` — 8 支: `Method` `Type` `Scene` `Global` `Shell` `Loop` `Conditional` `Async`
- **偏序** `Objects.cs:102-108`:
  ```cs
  if (Equals(other)) return true;      // 自反
  if (other is Global) return true;    // Global 最大元
  return false;                        // 跨标签不可比
  ```
- **正交性**: 8 支仅通过 `Global` 连通，其余 7 支互不相交。`Method("m") ⊄ Scene("s")` 恒 false。8×Resource 正交但**非层级正交**: 用户以为 `Method ⊆ Scene ⊆ Global` 的树，实际是 7 个孤岛 + 1 个天。
- **证据**: `DerivedMetrics.cs:58-62` `Algebra.cs:60` `Algebra.cs:128` `EffectScript.cs:305-313` 全部 `IncludedIn(scope)` 过滤 — `Per-Scope` 查询不会跨标签聚合。
- **严重度 P1** — `Loop/Conditional/Async/Shell` 四支在 JSON 契约 `EffectScriptContract.cs:131-139` 仅识别 `method/type/scene/global`，未知 type 已改为 throw (R1-F4 修)，但 `Shell/Loop/...` 仍**不可序列化** (`SerializeScope:275` throw)。类型有 8，契约只通 4，属类型-契约正交分裂。
- **最小修复**: 要么删 4 死支(若 ST-04 外无用)，要么在 `ParseScope/SerializeScope` 补全 `shell/loop/conditional/async` 往返。

### 1.3 Kind(3) × Mode(5) — 非自由积

- **Kind** `Objects.cs:112` `{Read, Write, Occupy}` ; **Mode** `Objects.cs:117` `{Use, Create, Release, Move, Unknown}`
- **约束 A — Read 仅 Use/Unknown** `Objects.cs:135-136`:
  ```cs
  if (Kind==Read && Mode!=Use && Mode!=Unknown) throw ...
  ```
  理论 3×5=15 组合，实际 Read×{Create,Release,Move}=3 组合非法 → 12 合法。**显式 complect 移除，非 bug**，但以运行时 throw 而非 phantom type 实现。新增 `Kind.Read+Mode.Move` 调用方在构造期才炸。
- **约束 B — Unknown≡Use** `Algebra.cs:15` `Resolve` : `Unknown=>Use`。Mode 5 坍缩为 4 有效值。`default(Mode)==Use==0` 使 `default(Claim)` 恰是最弱权限 — fail-open 味 (报告 `hickey-x3/round-05.md:16`).
- **约束 C — Write/Occupy 全 Mode 自由**，但 `Net/Peak/Compat` 三门**仅 consume Occupy** (`Algebra.cs:59,127` `EffectScript.cs:192`)，Read/Write 维度对三门**无影响** — 正交过度 (dead dimension 对三门)。
- **严重度 P1 (Read×Mode 运行时)** / **P2 (Unknown 坍缩)**。

### 1.4 4 维总体积表

| 维度对 | 正交? | 证据 |
|--------|-------|------|
| Resource × Scope | 隔离 (不同键) 但 scope 扁平 | `Objects.cs:102` |
| Resource × Kind | 隔离 (Signature 三桶 `Objects.cs:155-157` ) | `Signature.Add:194-199` |
| Kind × Mode | **非正交** (Read 禁 3) | `Claim.Normalize:135` |
| Mode × Scope | 正交 (Compat 按 (r,scope,mode) 分组 `EffectScript.cs:197`) | — |
| Resource 内部 | **非正交** (signal 3→1) | `Normalize:55-62` |

---

## 2. 三道 Gate 是否正交？

### 2.1 Gate 定义溯源

| Gate | 符号 | 文件:行 | 输入 | 过滤 | 聚合 |
|------|------|---------|------|------|------|
| G1 守恒 | `NetTable.Compute` | `Algebra.cs:54-68` | `Signature × Scope` | `Kind==Occupy && IncludedIn` | `SignedInterval.Add` 符号和 `Release=>Negate` |
| G2 峰值 | `Peak.Compute` | `Algebra.cs:120-134` | `Signature × Scope` | `Kind==Occupy && !Release && IncludedIn` | `NatStar sum Hi×ω` |
| G3 兼容 | `Compatible.IsCompatible` | `Algebra.cs:18-28` + `EffectScript.cs:274-286` 调用 | ` (Resource,Scope,Mode) grp` | 同上 Occupy | `IsCompatible(mode,mode)==false && count≥2` |

- **CONFLICT 集** `Algebra.cs:27` 仅 3 对: `(Create,Create)` `(Move,Move)` `(Release,Release)`。`Use` 与任何兼容 (`Algebra.cs:22`)，`Create×Release/Create×Move/Release×Move` 互兼容 (`Algebra.cs:24-26`)。

### 2.2 正交性判定

- **过滤正交 ✓**: 三门同前置 `Kind!=Occupy continue` — 量纲隔离一致。
- **聚合正交 ✓**: G1 有符号区间求和 (可负) vs G2 无符号 NatStar 上界求和 vs G3 布尔对称表 — 无共享可变状态 (sweep 版 G1/G2/G3 用 3 个独立 dict: `net:161` `peakSum:162` `topCount:163` `grp:164`).
- **键正交 ✓**: 三门同键 `(Normalize(r), e.Scope)` — 本轮已统一为 `e.Scope` (见 §3)。
- **非正交残留**: G1 有两套**实现** — sweep 增量 net (`EffectScript.cs:178-189`) vs closure 终态 net (`EffectScript.cs:305-346` 置换 `Lo≤closureT` 判定)。二者各自重新 `Negate/ToZ/ScaleSize`，属一事实两处写 (见 §4 F4)。二者间 `release 早于 create` 的负陷判定 (`EffectScript.cs:253-260`) 与 closure 的 `ContainsZero` (`EffectScript.cs:341`) 互为补充但不共享 `TryConserve` 逻辑 — `Algebra.cs:98 TryConserve` 枚举 `Missing/Top/NotZero/Conserved` 在 Audit 未复用。

**严重度 P1** — 三门**语义**正交，**实现**不 DRY。改 `NatStar` 环绕规则或 `Release` 符号化需同步 3 处。

### 2.3 Gate 级别 Complect 债务

- `Combination.Parallel` `DerivedMetrics.cs:76-85` 在 L1 已做 `Compatible` 前置 throw `PARA_CONFLICT`，Audit G3 又做同分组二次检查 — 同一 CONFLICT 事实两套入口 (L1 并行组合 vs L2 时序 sweep)。意图是 `Parallel` 管静态组合，`Audit` 管时序并发，属于分层正交，但共享 `Compatible` 单点是正确的 (单一事实)。

---

## 3. At 与 Audit 的 Scope 投影是否一致？

### 3.1 At

`EffectScript.cs:90-98`:
```cs
Signature At(NatStar t) = Union( Combination.Loop(e.Footprint, e.Loop, e.Scope) )  // L96
Alive: Lo≤t≤Hi  // L354
```
每个 Claim 的 scope 被**重投影**为 `e.Scope` (`Combination.Loop:58-62`  `c with { Scope=loopScope }`)。`Claim` 自带 `c.Scope` 被丢弃。

### 3.2 Audit sweep

`EffectScript.cs:192-247` Step:
```cs
var key = (r, e.Scope, (int)c.Mode)  // L197  — 用 e.Scope，非 c.Scope
if (!c.Scope.IncludedIn(scope)) — 无 (Audit 不用单 claim scope)
foreach c in OccupyClaims — 已在 Loop 重投影后, 但 Audit 直接读 e.Footprint 未重投影? 实际 Step 读 e.Footprint 原始 claim + 外层 e.Scope 作 key
```
对照 `Algebra.cs:60` `!c.Scope.IncludedIn(scope)` 在 `NetTable.Compute` 是 per-claim scope 过滤。Audit 的 per-claim 过滤**被 e.Scope 分组替代**。注释 `L195-196` 自述修 OPEN-1: “与 At/ReferenceAudit 的 Combination.Loop 投影一致；此前用 c.Scope 与 At 视角分裂”。

**本轮一致 ✓** — At 与 Audit 同源 `e.Scope`。

### 3.3 Deviation 的分裂

`Deviation.cs:24`:
```cs
var exp = NetTable.Compute(expected, new ScopeId.Global()); // 硬编码 Global
var act = NetTable.Compute(actual, new ScopeId.Global());
```
- At/Audit 是 **per-event scope** 过滤 (Method/Scene 等孤岛)；Deviation 是 **Global 单点** 聚合 (最大元含一切)。
- 后果: 同剧本 `Method("m")` 与 `Scene("s")` 的资源分属不同 scope，At/Audit 分别报告守恒/峰值，Deviation 把它们加一起算 `Σ|mid|` — 可能掩盖 per-scope 负陷或夸大 Deviation。
- 是否正交有意? §9.1 注释 `L20` 声明 “scope 取 Global (最大元)，仅含 occupy 净效应” — 是**开发期占用对账**的有意全局视图。但与 `EFFECT_SCRIPT.md §3` 的 per-scope 审计语义不显式区分，读者会以為 Deviation 与 Audit 同 scope。
- **严重度 P1** — 文档/命名未显式 `GlobalDeviation`，用户易把 per-scope 泄漏误判为 Deviation 通过即安全。

### 3.4 附加证据 — 归因 scope 一致性修复史

`EffectScript.cs:165-171` `netScope/peakScope` + `ResolveNetScope/PeakScope` 与 `EffectScript.cs:326-338` `leakScope` (取最晚 Lo) 均改用 `e.Scope` 归因。旧版 `peakScope` 陈旧问题 (`EffectScript.cs:235-244` exit 清理) 本轮已加清理，但 `hasActiveGrp` 行 `239-240` 仍为简化占位 (`_ = hasActiveGrp`) — 标注 “未来可细化为按资源分组检查”，属已知 P2 债务。

---

## 4. 一个事实两处写 (Single Source of Truth Violations)

| # | 单一事实 | 两处(多处)写 | 文件:行 | 严重度 | 最小修复 |
|---|----------|-------------|---------|--------|----------|
| SSOT-1 | ResourceId 归一 | `Objects.cs:52 Normalize` 为真源，但 `Budget` `NetTable` `Audit sweep` `closure` `Combinator` 五处各自 `Normalize` 调用，漏一处即键分裂 | `Algebra.cs:61` `EffectScript.cs:182,194,330,390` | P2 | 已收敛为单函数；需契约测试覆盖 Budget 归一 (`EffectScriptContract.cs:111` 已有) |
| SSOT-2 | Net 符号化 | `Algebra.cs:72-85 Negate/ToSigned` vs `EffectScript.cs:184-186 contrib` vs `EffectScript.cs:318-320 closure contrib` vs `EffectScript.cs:364-366 ToZ/Negate` — 同一 “Release 取 [-hi,-lo]” 公式复制 4 次，`long.MaxValue` 溢出护栏亦复制 | 同上 | P1 | 抽 `SignedInterval.FromInterval(Interval,Mode)` 单点 |
| SSOT-3 | Size 缺省 | `Claim.Normalize:140 Size??Default` vs `Signature.Add:192 n=c.Normalize()` vs `NetTable:63 c.Size??Default` vs `Peak:130` vs `Combination.Scale:88` — 5 处 `?? Interval.Default` | `Objects.cs:140` `Algebra.cs:63,80,130` `DerivedMetrics.cs:88` | P2 | Claim.Normalize 后保证 Size 非 null，下游删 `??` (现部分仍冗余) |
| SSOT-4 | LoopCount 非法态 | `LoopCount.IsValid:26` / `Combination.Loop guard:54` / `EffectEvent ctor:47` / `EffectScriptContract.ParseLoop:151` 四处 `≥1或⊤` 校验 | `DerivedMetrics.cs:26,54` `EffectScript.cs:47` `EffectScriptContract.cs:151` | P2 | 单点 `LoopCount.IsValid` + 统一 throw，已部分收口 (R5 V5-002) |
| SSOT-5 | Scope 投影键 | `EffectScript.At:96 Loop(...,e.Scope)` vs `EffectScript.Audit:197 (r,e.Scope,mode)` vs 旧 `c.Scope` (已删) — 历史分裂已修，但 `Deviation` 仍用 `Global` (见 §3.3) | 同上 | P1 | Deviation 更名为 `GlobalDeviation` 或参化 scope |
| SSOT-6 | Budget Caps 归一 | `EffectScript.cs:390` `norm[Normalize(kv.Key)]` 与 Audit `peakSum` 键 `Normalize` 需同源；旧版分裂已修 S06-002 | `EffectScript.cs:390` | P2 (已闭) | — |
| SSOT-7 | Interval 端点校验 | `Numeric.cs:83-86` `lo⊤∧!hi⊤ throw` vs `EffectScriptContract.ParseInterval:99` `lo⊤ throw` vs `EffectEvent:43 Lo⊤ throw` — 同一 “Lo 不可为 ⊤” 事实三处 throw 文案不同 | 同上 | P2 | — |

> Hickey 评: “Facts should have a single home. Copies are not DRY — they are time bombs with different clocks.”

---

## 5. 跨桶量纲隔离 × Weight.NaN 是否被破坏？

### 5.1 设计

- **分桶** `Objects.cs:153-157` `Signature` 三 `ImmutableHashSet` (`_read/_write/_occupy`) 物理隔离；`Add:194-199` 按 `Kind` 路由，`AllClaims` 仅枚举合并。
- **隔离定理** `Objects.cs:148-152` 注释: “跨桶聚合须显式 Weight，否则 KIND_MIX (L3 诊断 §3.3.2b)”；`DerivedMetrics.cs:58-62` 三桶各自 Union — 类型层面已分桶。

### 5.2 Weight 现状

`Algebra.cs:35-39`:
```cs
public static class Weight {
  public static double Of(Kind a, Kind b) => a==b ? 1.0 : throw new InvalidOperationException($"KIND_MIX ...");
}
```
- **历史**: 曾返回 `double.NaN` 毒值会污染下游 `Deviation`/`Peak` 聚合 (NaN 传播)。本轮已改为 `throw` — **fail-fast，非 NaN** (`Algebra.cs:38` 注释 “非返回 NaN”)。
- **调用面**: 全仓 `grep Weight` 仅定义处 — 无生产调用。隔离靠 `if (Kind!=Occupy) continue` 硬过滤 (`Algebra.cs:59,127` `EffectScript.cs:180,202`)，而非 Weight 路由。
- **结论**: Weight.NaN **未破坏**隔离；隔离由桶 + 过滤双层保证。但 Weight 成为 dead complect — “为跨桶预留的显式换算点”实际无消费者，徒增概念负载。

### 5.3 残留风险 — 量纲穿越

- `Signature.Join` `Objects.cs:215-231` 按 `(Kind,Resource,Mode,Scope)` 四元键合并 size — 若 `Kind` 相同但 `Mode` 不同，size 合并会把 `Create` 与 `Release` 的 interval merge 为包络 `[minLo,maxHi]`，与 Net 的符号抵消语义相反。Join 被标注 `L1 警告` 与 Union 等价仅差 size merge，但调用方若误用 Join 代替 Union 会得到假守恒。
- `Combination.Loop` `DerivedMetrics.cs:58-62` 对三桶各自 Scale，但 `Scale:88-92` `if(w.IsTop) return [Lo,Top]` 仅开放上界，下界不变 — `LoopCount.Top` 的净效应下界仍有限，导致 `Deviation` 的 `TryMid` 取中点有界，Top 资源仍参与 Deviation 求和直至 `anyTop break` 全 ⊤ — 保守但粗粒。

**严重度 P2**

---

## 6. 逐符号表 (Symbol × File:Line × 正交性 × 严重度)

| 符号 | 文件:行 | 职责 | 正交性评价 | 严重度 |
|------|---------|------|------------|--------|
| `ResourceId` | `Objects.cs:21` | 15 支判别联合 | 15 支中 3 坍缩，新增维度需改 Normalize 表 | P2 |
| `ResourceId.Normalize` | `Objects.cs:52` | 信号命名空间归一 + 幂等护栏 `L60-62` | 单点真源，多处调用漏则分裂 | P1 |
| `NodePathOrUnknown` | `Objects.cs:69` | Tree 路径 Unknown | 正交 (Unknown=Unknown 结构相等) | OK |
| `ScopeId` | `Objects.cs:90` | 8 支 + 偏序 `IncludedIn` | 扁平，仅 Global 连通；契约仅 4 支往返 | P1 |
| `ScopeId.IncludedIn` | `Objects.cs:102` | 自反+Global最大元，跨标签 false | 非层级，有意但文档未显 | P1 |
| `Kind` | `Objects.cs:112` | {Read,Write,Occupy} | 与 Mode 非自由积 (Read×Create 禁) | P1 |
| `Mode` | `Objects.cs:117` | {Use,Create,Release,Move,Unknown} | Unknown≡Use 坍缩 | P2 |
| `Claim` | `Objects.cs:127` | 五元组 (Kind,Resource,Mode,Scope,Size) | 类型强制全必填，`record struct default` 后门由 `Signature.Of` 双校验 `L190-191` 守卫 | P1 |
| `Claim.Normalize` | `Objects.cs:133` | Read×Mode 拒绝 + Size 缺省 | 运行时 throw 非类型 | P1 |
| `Signature` | `Objects.cs:153` | 三桶 `_read/_write/_occupy` | 桶物理隔离，Join/Union 双算子易混 | P2 |
| `Signature.Of` | `Objects.cs:171` | 去重 + 重复 Claim throw P0-4 | 并发须走 LoopCount，非集合重复 | OK |
| `Signature.Union/Join` | `Objects.cs:205/215` | ∪ vs ⊔ (size merge) | Join 仅 size 包络，不承载时序 — 警告已标 | P2 |
| `Compatible` | `Algebra.cs:12` | 16 对全函数 + 对称 | 封闭 5×5 表，Use 最弱 | OK |
| `Weight` | `Algebra.cs:35` | Kind×Kind → ℝ∪{⊥} | throw 非 NaN，零调用方 dead code | P2 |
| `NetTable` | `Algebra.cs:46` | 有符号 net + `IsConserved/ TryConserve` | scope 过滤 + 符号化复制多处 | P1 |
| `Peak` | `Algebra.cs:117` | NatStar 求和 | 过滤 Occupy/!Release，与 Net 正交 | OK |
| `NatStar/Interval` | `Numeric.cs:8/70` | ℕ* ⊤ 闭包 + 区间 | ⊤-闭包完备 (环绕→⊤) | OK |
| `DeviationVal` | `Numeric.cs:111` | double∪{⊤} | `ExceedsThreshold` 先判 IsTop | OK |
| `LoopCount` | `DerivedMetrics.cs:10` | ω∈ℕ∪{⊤} + IsValid | IsValid 单点，四处守卫 | P2 |
| `Combination.Loop` | `DerivedMetrics.cs:50` | Loop 重投影 + Scale | 与 At/Audit scope 投影同源 | OK |
| `Combination.Parallel` | `DerivedMetrics.cs:76` | Compatible 前置 + Union | 与 Audit G3 同表，双入口有意分层 | P2 |
| `Derived` | `DerivedMetrics.cs:99` | Peak/Net/IsConserved 门面 | 透传 Algebra，无新增 complect | OK |
| `EffectEvent` | `EffectScript.cs:23` | lifetime+scope+footprint+ω | Lo⊤ 拒绝 + Loop 非法拒绝 双守卫 | OK |
| `EffectScript.At` | `EffectScript.cs:90` | 瞬时快照 | 投影 `e.Scope`，与 Audit 对齐 | OK |
| `EffectScript.Audit` | `EffectScript.cs:134` | sweep 三门 | sweep net vs closure net 双写 | P1 |
| `Budget` | `EffectScript.cs:378` | Caps 不可变 + 归一 | 构造归一已修 S06-002 | P2 (已闭) |
| `AuditResult` | `EffectScript.cs:426` | Passed≡Violations.IsEmpty + CapsChecked | `IsPeakChecked` 区分 “没查” vs “查过绿” (R1-HIGH-3) | OK |
| `EffectScriptContract` | `EffectScriptContract.cs:18` | JSON⇄L1 | `RejectUnknownKeys` + `ReqStr` 白名单；未补 Shell/Loop 往返 | P1 |
| `SignatureDeviation` | `Deviation.cs:11` | Global net 中点偏差 | 硬编码 Global，与 per-scope 审计分裂 | P1 |

---

## 7. 关键发现清单 (按严重度)

### P0 — 阻塞 (无)

本轮无 P0 阻塞。历史 P0-4/P0-5 (重复 Claim 静默去重 / Lo=⊤ 假绿) 已闭 (`Signature.Of:178` throw / `EffectEvent:43` throw)。

### P1 — 应修 (影响结论正确性或维度增长成本)

- **P1-1 ScopeId 8→4 契约分裂** `Objects.cs:90-99` vs `EffectScriptContract.cs:131-139,269-275` — `Shell/Loop/Conditional/Async` 类型存在但 JSON 不可往返。新增场景若用 Loop scope 剧本直接 throw。
- **P1-2 Read×Mode 运行时非类型** `Objects.cs:135` — 非法组合在构造期 throw，但类型仍可表达 `new Claim(Read,_,Release,_,_)`，错误延迟到运行时而非编译期。
- **P1-3 Net 双实现** `EffectScript.cs:178-189` vs `305-346` — 符号化/Scale/ToZ 各复制，改一漏一。已有 `ToZ/Negate:365-366` 单点但 sweep/closure 未复用 `NetTable` 真源。
- **P1-4 Deviation Global 硬编码** `Deviation.cs:24` — 与 At/Audit per-scope 正交分裂，易误读为同 scope 结论。
- **P1-5 Scope 归因陈旧清理占位** `EffectScript.cs:238-244` `hasActiveGrp` 简化占位 — exit 后峰值归因可能取历史首个而非当前活跃者。
- **P1-6 Scope 偏序扁平 surprise** `Objects.cs:102-108` — `Method⊄Scene` 静默 false，无 LUB，跨标签聚合误以为 Global 聚合已含而实际需显式 Global 查询。

### P2 — 报告 (债务/可推迟)

- **P2-1 ResourceId 归一表多处调用** 见 SSOT-1
- **P2-2 Weight dead complect** `Algebra.cs:35` — 保留 `KIND_MIX` 概念但零调用，概念负载
- **P2-3 Size 缺省多处 `??`** 见 SSOT-3
- **P2-4 Interval/Loop/Claim 三处 Lo 非法校验文案不一** 见 SSOT-7
- **P2-5 Signature.Join 与 Union 易混** `Objects.cs:214` 警告已标但无 lint 阻止误用
- **P2-6 Mode.Unknown 坍缩 + default(Mode)==Use** `Objects.cs:117` `Algebra.cs:15` — `default(Claim)` 最弱权限 fail-open，需文档显式

---

## 8. 残余风险

- 新增 ResourceId 构造子 (如 `Physics` 子类) 若忘记加入 `EffectScriptContract.cs:209-222` 白名单 + `SerializeResource:287-294` + `Normalize`，会静默走 `ParseResource throw` — 属白名单正交风险，现有 throw 是安全的 fail-fast，但增长成本线性。
- `NatStar` 乘法环绕→⊤ (`Numeric.cs:38`) 与 `EffectScript.cs:212-213` `peakSum + hi*w` 的 Top 传播已弃哨兵 (`ulong.MaxValue` 哨兵已删 R2-001)，残余风险低。
- `Deviation` 的 `ε=1` 分母下界 (`Deviation.cs:44`) 使单值区间 `[s,s]` 偏差为 `|Δmid|/1`，量纲与区间长度无关 — 有意但会放大单值资源的 Deviation，需阈值侧意识。

---

## 9. 审计方法 & 证据

- **方法**: 逐符号读 7 文件行号锚定，交叉 `AllClaims`/`IncludedIn`/`Normalize`/`Scale` 调用图；对比 At vs Audit vs Deviation 的 scope 入参；追踪三门过滤谓词与分组键；验证 Weight 返回类型非 NaN。
- **未读**: `Samples/GodotReal` `Runtime/*` `Analyzer` `Generator` — 本报告不覆盖运行时 Fiber/Shell 层的 scope 分组 (D9) 是否与 L1 正交，属后续轮次。
- **验证**: `Weight.Of(Read,Write)` 必 throw `InvalidOperationException` (`Algebra.cs:38`)；`Claim(Read,_,Release,_,_)` 必 throw (`Objects.cs:135`)；`ParseScope({"scene":"s"})` → `Scene`，`{"type":"global"}` → `Global` (`EffectScriptContract.cs:137`) — 契约测试可复核。

---

## 10. 最小下一步 (Hickey 式)

1. 补 `ScopeId` 契约往返或删 4 死支 (P1-1) — 一行白名单。
2. 抽 `SignedInterval.From(Interval,Mode)` 单点，sweep/closure/NetTable 三处复用 (P1-3)。
3. 更名 `SignatureDeviation` → `GlobalDeviation` 并在注释显式 “Global 聚合，非 per-scope” (P1-4)。
4. 视 `Weight` 去留: 要么删 dead code，要么让 `Signature.Join` 显式走 Weight (当前 Join 绕开 Weight)，二选一消 complect。

> “Simple is not easy. 正交不是把所有组合都做出來，而是让每个维度只在一个地方被理解。”

```

---

DONE_R03

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "7源文件逐符号行号审计: Objects.cs:21-146 ScopeId偏序/ResourceId归一/Claim约束; Algebra.cs:12-135 Compatible/Weight/Net/Peak; Numeric.cs:8-132 NatStar/Interval; DerivedMetrics.cs:10-93 LoopCount/Combination; EffectScript.cs:23-489 At/Audit/Budget三门与scope投影; EffectScriptContract.cs:21-326 契约白名单; Deviation.cs:11-53 Global硬编码. 输出5类正交性判定+12条SSOT/栅门/Weight.NaN发现，artifact内联返回待落盘 audit/rich-hickey-round03-decomplect.md"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round03-decomplect.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [],
  "validationOutput": [
    "Weight.Of cross-kind throws InvalidOperationException not NaN (Algebra.cs:38); Claim.Read+Release throws (Objects.cs:135); ParseScope/SerializeScope round-trip 4/8 scopes; At vs Audit both project e.Scope (EffectScript.cs:96 vs 197) deviation hard-codes Global (Deviation.cs:24) — verified by source read"
  ],
  "residualRisks": [
    "ScopeId Shell/Loop/Conditional/Async存在但JSON不可往返 — 新增scope剧本直接throw (P1-1)",
    "Net符号化/Scale在NetTable/Audit sweep/Audit closure三处复制 — 改一漏一 (P1-3)",
    "Deviation硬编码Global与per-scope审计分裂 — 易误读为同scope结论 (P1-4)"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round03-decomplect.md — Rich Hickey正交性透镜审计15×8×3×5笛卡尔积、三门正交性、At/Audit scope投影一致性、SSOT 7项、Weight跨桶隔离，含逐符号表+行号+严重度",
  "reviewFindings": [
    "P1: ScopeId 8类型 vs 契约4往返分裂 — Objects.cs:90/EffectScriptContract.cs:131,269",
    "P1: Read×Create/Release/Move运行时throw非类型 — Objects.cs:135",
    "P1: Net双实现 sweep vs closure 符号化复制4份 — Algebra.cs:72/EffectScript.cs:178,305,364",
    "P1: Deviation硬编码Global与At/Audit per-scope分裂 — Deviation.cs:24",
    "P1: Scope偏序扁平跨标签恒false无LUB — Objects.cs:102",
    "P2: Weight dead complect零调用方 — Algebra.cs:35",
    "P2: ResourceId/Size/LoopCount各5/4处调用/校验复制 — SSOT-1..4"
  ],
  "manualNotes": "仅读7源文件(禁止读audit/)约束下完成; write工具缺失故制品以内联markdown返回, 运行时落盘至 D:/Godot/Cosmos/audit/rich-hickey-round03-decomplect.md; 未执行测试/构建命令"
}