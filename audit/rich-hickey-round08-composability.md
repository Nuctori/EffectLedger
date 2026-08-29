# Rich Hickey Round08 — 组合性（Composability）对抗审计

> 视角：组合性 / 代数律透镜。7 源文件只读：`Objects.cs` `Numeric.cs` `SignedNet.cs` `Algebra.cs` `DerivedMetrics.cs` `EffectScript.cs` `EffectScriptContract.cs`。`audit/` 禁读。
> 判定标准：半格律（幂等/交换/结合/单位元/吸收）、同构分布式、置换不变、gate 正交可组合、NaN 毒化。

---

## 1. 符号表（逐符号·行号·代数性质）

| # | 符号 | 位置 | 声称性质 | 实测性质 | 反例 / 证据 | 严重度 |
|---|------|------|----------|----------|-------------|--------|
| S1 | `Signature.Union` (∪) | `Objects.cs:205-212` + `Add:186-202` | §3.2.1 半格并：幂等/交换/结合，`Empty:166` 为单位元 | **幂等/交换/结合成立（集合并）**，但**多重集语义丢失**：同 `Claim` 结构相等则 `ImmutableHashSet.Add` 静默去重，多并发副本算 1 份 | `Union(Of(c[1,1]), Of(c[1,1]))` → 1 claim，`Peak=1` 而非 2；`Net` 单份抵消；`Of` 重复抛 `ArgumentException:180` 但 `Union` 静默坍缩——同一不变量两条路径 | **P1** |
| S2 | `Signature.Join` (⊔) | `Objects.cs:215-231` (`Of:171-184`) | §3.2.4 条件分支合并 join-semilattice，幂等/交换/结合/吸收，同键 `size=merge_I` | **交换/结合在 Merge 层成立**；**幂等对 Union 产物失效** | `a=Union(Of(c[10,10]),Of(c[50,50]))` 同键两 claim，`Join(a,a)` → 1 claim `[10,50]` ≠ `a`（2 claims）。`Join` 幂等仅当输入满足“每键至多一 claim”不变量，而 `Union` 不保证该不变量 | **P1** |
| S3 | `Union` vs `Join` 分叉 | `Objects.cs:205` vs `215` | 两算子同域 `Signature→Signature`，文档注释 214 行警告“与 Union 仅同键合并差异” | **非同构**：同输入 `Of(c[10,10]) , Of(c[50,50])` 下 `Union`→2 claims, `Peak` 60；`Join`→1 claim `[10,50]`, `Peak` 50；`Loop(·,3)` 后 180 vs 150。选词决定度量，值类型无法区分 | `DerivedMetrics.cs:66-70` 注释已承认差异但未类型化 | **P1** |
| S4 | `Interval.Merge` | `Numeric.cs:101` | §3.1.5b join-semilattice 幂等/交换/结合 | **成立**：`new(Lo.Min, Hi.Max)`，`Min/Max` 均内嵌 ⊤ 律 `Numeric.cs:42-49` | `Merge([⊤,⊤],[1,5])=[1,⊤]` 符合 `Min` 律，有意设计 | P2 |
| S5 | `NatStar` + / * | `Numeric.cs:25-39` | §3.1.5a 闭包：x+⊤=⊤, 溢出→⊤ | **成立**：环绕检测 `sum<a.Value` / `prod/a != b` 保守 ⊤ | 溢出不回卷，代数闭合 | OK |
| S6 | `Combination.Loop` | `DerivedMetrics.cs:50-64` + `Scale:88-92` | §3.2.5 `(S×ω)=Σ copy_i(S)`，ω 有限按 ω 缩放，ω=⊤ 上界开放 | **Loop 内部分配律成立**：`Loop(Union(a,b),ω) == Union(Loop(a,ω),Loop(b,ω))`（逐 claim Scale 线性+Union 分配）。但 **Loop vs 手工复制不等**：`Loop(body,2)`→`[2,2]` 单 claim；`Union(copy,copy)`→去重为 `[1,1]` 单 claim | 缩放把多重性编码进 `size` 区间，掩盖集合坍缩，仅当副本结构全等时等价 | **P1** |
| S7 | `Combination.Sequence` | `DerivedMetrics.cs:66-70` | §3.2.1 序列组合 `; := ∪` | **恒等于 Union**，`[Obsolete]` | 无时序语义，名异实同，`Sequence(a,b)==Union(a,b)` 恒成立 | P2 |
| S8 | `Combination.Parallel` | `DerivedMetrics.cs:76-85` | §3.2.2 并行组合 `∥ := ∪` + `Compatible` 前置 | **非纯 Union**：同资源归一 `Normalize:80` + `IsCompatible:81` 冲突则抛 `PARA_CONFLICT`，否则 Union。**非全函数**，破坏结合/交换的 totality | `Parallel(create,create)` 抛，`Union(create,create)` 静默通过且峰值减半，`Sequence(create,create)` 去重 1 份——三拼法三命运 | **P1** |
| S9 | `EffectScript.At(t)` | `EffectScript.cs:90-99` (`Alive:354-355`) | 瞬时快照 `Σ Loop(Footprint,Loop,Scope)` 经 Union，纯函数，置换不变 | **置换不变成立**（Union 交换/结合 ⇒ 折叠序无关）；**但多重性语义与 Audit 分裂** | 两事件同 `lifetime∋t` 同 footprint `c[1,1]`：`At`→1 claim `[1,1]`，`Audit` sweep `grp[(r,S,Create)]={0,1}` 报 `CompatibleConflict` 且 `peakSum` 累加 2。`At` 去重 vs `Audit` 计数 | **P1** |
| S10 | `Budget` / `CapsChecked` | `EffectScript.cs:378-421` `Audit:138-139,350` | 峰值预算壳，缺省无上限 | **Budget 值语义已修复**：防御拷贝 `ImmutableDictionary:389-391`，归一键 `Normalize:387-390`，`Equals/GetHashCode:399-420` 内容相等。但 **`CapsChecked` 暴露 gate 未运行**：`Caps.Count==0` 时 `Passed=true` 冒充全绿，需 `IsPeakChecked` 区分 | `AuditResult:440-455` 强制 `Passed==Violations.IsEmpty`，`CapsChecked` 单独记录 | P2 |
| S11 | `Weight.Of` | `Algebra.cs:31-39` | §3.3.2b `weight: Kind×Kind→ℝ∪{⊥}`，跨 kind `⊥` | **NaN 已根除**：`a==b?1.0:throw KIND_MIX`，无 `double.NaN` 毒化。**代价：偏函数抛异常，组合时非全** | `NaN` 会污染 `Peak`/`Deviation`，当前抛是正确 fail-fast | P2 (正向) |
| S12 | `ResourceId.Normalize` | `Objects.cs:52-65` | §3.1.4a 归一：`Self(signal_x)→SignalBus(x)`, 幂等 | **幂等成立**：`SignalBus` 分支短路 `62` 避免二次剥前缀 | `NetTable:61` `Peak:128` `Audit:182,197` 均经 `Normalize` 分组，一致 | OK |
| S13 | `NetTable.Compute` / `Peak.Compute` | `Algebra.cs:54-68` / `118-134` | net 有符号求和 vs 峰值求和 | **量纲隔离已对齐**：`Peak:127` `if(c.Kind!=Occupy) continue` 与 `Net:59` 同源；`release` 不入峰值 `129` | 历史跨桶污染已修 | OK |
| S14 | `Audit` 三 gate 正交 | `EffectScript.cs:134-351` + 接口 `370-372` | 声称 `Audit = concat(gates)` 可独立开关测试 | **接口死亡**：`INetGate/IPeakGate/ICompatGate` 已定义未实现、未注入、未被 `Audit` 调用；三 gate 在 `Audit` 内联扫换线共享 `sweep` 遍历但状态字典分离 (`net:161`,`peakSum:162`,`grp:164`)。**耦合点**：`ω=⊤` 豁免守恒 `178` 但仍入峰值 `205-206`；`peakScope` 清理 `236-244` 误用 `grp.Values.Any` 粗粒度 | 独立可组合性不成立：两通过脚本拼接可能因跨脚本 `create/release` 互补而整体通过/失败；居民层豁免使 gate1 依赖 gate2 的 ⊤ 判定 | **P1** |
| S15 | `EffectScriptContract` JSON 往返 | `EffectScriptContract.cs:20-326` | AI JSON→L1 搬运，零新增代数 | **往返已加固**：根未知键白名单 `29,81`，`loop`/`scope`/`resource` 非法形状转 `FormatException` 单一方言 `101-102,176-180`；`claim scope` 必须等于 `event scope` 双真相校验 `170-171`；`budget` ⊤ 序列化为 `"⊤"` `302` 避免 `0` 误判 | 仍有 `ScopeId.Loop/Conditional/Async/Shell` 不可往返 `269-275` 抛，但属有意未暴露 | P2 |

---

## 2. 半格律逐项验算

### 2.1 `Union` (集合并)
- **幂等** `Union(s,s)==s`：`Add` 经 `ImmutableHashSet.Add` 幂等，`Equals` 用 `SetEquals:238`，成立。反例仅在 `Of` 抛 vs `Union` 静默的不一致。
- **交换** `Union(a,b)==Union(b,a)`：`Equals`/`GetHashCode` 均顺序无关（XOR 折叠 `248-251`），成立。
- **结合** `Union(Union(a,b),c)==Union(a,Union(b,c))`：集合并结合，成立。`Empty` 为单位元 `166`。
- **结论**：`Union` 是干净的集合半格，但**建模对象错**：并发资源占用是多重集（multiset）加法幺半群，幂等是 bug 而非 feature。`Of` 的去重抛补丁只堵住 `Of` 入口，`Union`/`At` 仍静默坍缩。

### 2.2 `Join` (条件分支合并)
- **幂等**：仅当输入满足“每键至多一 claim”时成立；`Union` 可产出同键多 claim，`Join` 对其幂等失效（S2 反例）。
- **交换/结合**：成立（`Merge` 的 `Min/Max` 交换结合）。
- **吸收**：`Join` 与 `Union` 无吸收律，`Union(a,Join(a,b)) != Join(a,b)` 一般不成立（size 语义不同）。
- **结论**：`Join` 自身是半格，但与 `Union` 共处同一值类型 `Signature` 且无类型区分，**半格律不可组合**。

---

## 3. `Combination` 是否尊重组合

- `Sequence`：`∥ Union` 别名，`[Obsolete:69]`，尊重组合但零信息（应删除，保留仅为兼容）。
- `Parallel`：`Union` + `Compatible` 守卫。守卫使 `Parallel` 成为偏函数，**不尊重组合**：`Parallel(a,b)` 可能抛而 `Union(a,b)` 不抛；`Parallel(Parallel(a,b),c)` 的抛点依赖分组，`Union` 的结合律不能平移。文档“并行性由 L3 跨调用点补”与此处前置守卫双重口径。
- `Loop`：在集合语义下用 `Scale` 把加法幺半群编码进区间端点，`Loop(Union(a,b),ω)` 分配律成立；但**复制律** `Loop(body,ω) == Σ_{i=1..ω} Union(copy)` 不成立（去重导致后者坍缩）。`LoopCount.IsValid:26` / `Of≥1:18` / `default` 拒绝 `48,54` 已封 `ω=0 → [0,0]` 岔路。

---

## 4. `At(t)` 是否置换不变

- **置换不变**：是。`At` 为 `Union` 折叠，`Union` 交换结合 ⇒ 事件重排不影响 `Signature` 内容相等（`ImmutableHashSet` 无序，`Equals` 结构相等）。
- **但置换不变≠语义保持**：
  1. `At` 静默去重 vs `Audit` 计数：同一 `t` 下两相同足迹事件 `At` 得 1 份，`Audit` `grp` 得 2 份并报 `CompatibleConflict`（`EffectScript.cs:276-285` vs `90-98`）。
  2. 编码非唯一：`[Event(ω=2, footprint c[1,1])]` 经 `Loop→[2,2]` 与 `[Event×2 (ω=1,c[1,1])]` 经 `Union→[1,1]` 观测不等，`Peak` 2 vs 1，`Net` 2 vs 1。同一并发现实三种编码（重复事件/ω/Union）三种判决，正交分解失败。

---

## 5. `Audit` 三道 gate 是否可独立组合

- **声称**：`Audit = concat(gates)` 可独立开关测试（`370-372` 接口）。
- **实际**：
  - 接口未接线：`INetGate/IPeakGate/ICompatGate` 无实现类，`Audit:134` 未委托，测试无法单独实例化 gate。
  - 语义耦合：gate1 对 `ω=⊤` 资源豁免 `178,311`（居民层），gate2 仍将其 `topCount>0` 记为 `⊤` 峰值 `205-206,265`；gate1 的 `NegativeDip:253-259` 与 gate2 的 `PeakExceeded:262-271` 共享 `Budget.Caps` 归一键但归因 `netScope/peakScope` 分离，`peakScope` 的清理逻辑 `236-244` 粗粒度且 `hasActiveGrp` 未按资源过滤。
  - 可组合性反例：`scriptA=[create r]` `scriptB=[release r]` 各自 `Audit` 均 `Leak`，`Union` 后整体守恒；反之两通过脚本拼接可能因跨脚本 `create×create` 同 `t` 触发 `CompatibleConflict`。**Audit 不满足同态**：`Audit(a⊔b) ≠ Audit(a) ∧ Audit(b)`。
  - 正向：`CapsChecked:350` 使“未检查”与“检查通过”可区分，`Budget.None` 防御拷贝已正交。

---

## 6. `Weight.NaN` 是否破坏组合

- **现状**：`Weight.Of:38` 跨 kind 抛 `InvalidOperationException("KIND_MIX")`，**无 `NaN` 返回**，`deviation` 路径 `Numeric.cs:106-132` 用 `DeviationVal.Top` 而非 `double.NaN`。`NaN` 毒化（`x+NaN=NaN` 污染 `Peak`/`Net`/`ContainsZero`）已根除。
- **剩余组合代价**：抛异常使 `Weight` 成为偏函数，非全；跨桶聚合必须在 L3 `Analyzer` 前置拦截，否则运行时抛。优于 `NaN` 静默污染，符合 fail-fast。

---

## 7. 严重度汇总与最小修复

| 级别 | 发现 | 最小修复 |
|------|------|----------|
| **P1** | S1/S9 `Union`/`At` 静默去重导致并发计数减半 | 将并发多重性移出 `size` 区间，改为 `Signature` 多重集或 `LoopCount` 为一等公民；或使 `Union` 对同键同 size 去重时累加计数（`Peak`/`Net` 求和语义），`Of` 与 `Union` 同抛/同计数 |
| **P1** | S2/S3 `Union` vs `Join` 同类型异语义，`Join` 幂等对 `Union` 产物失效 | 类型化区分：`BranchSignature` vs `ConcurrentSignature`，或 `Join` 入口校验“每键至多一 claim”并抛，或统一为单一算子 |
| **P1** | S8 `Parallel` 偏函数 vs `Sequence`/`Union` 全函数 | 明确 `Parallel` 为校验式构造器非代数算子，或使其返回 `Result<Signature,Conflict>` 而非抛，保持 totality |
| **P1** | S6 `Loop` 缩放掩盖集合坍缩，复制律不成立 | 文档化 `Loop` 为唯一并发复制原语，禁用手工 `Union` 复制；测试固化 `Loop(body,2) != Union(copy,copy)` |
| **P1** | S14 Gate 接口死亡 + 跨脚本非同态 | 接线 `INet/IPeak/ICompatGate` 到 `Audit`，或删除接口；文档化 `Audit` 非同态，新增 `Script.Concat` 显式算子 |
| P2 | S4/S5/S10/S11/S12 已闭合或正向 | 保留；`Sequence` 删除或彻底 `Obsolete`；`Weight` 抛保持 |

---

## 8. 残余风险

- `Interval` 下界恒有限 `80-84`，`[⊤,⊤]` 合法但 `EffectEvent` 寿命 `43-44` 拒 `Lo=⊤`，`Scale` 中 `Lo×ω` 无 `⊤` 分支——闭合。
- `ZStar` 溢出保守 `Top:37,51`，`Negate/ToSigned:74-83` 对 `>long.MaxValue` 转 `Top`，`ContainsZero:98` 对 `Top` 假阴——fail-closed 正确。
- `Budget` 归一 `389-390` 后 `ResourceKey` 序列化往返 `306-314` 一致，但 `ScopeId.Loop/Conditional/Async/Shell` 往返抛 `275`——有意未暴露，若 AI 产出则 fail-fast。
- `Peak` 仍取 `Hi` 上界 `131`，`Net` 取区间求和 `102`，二者对 `⊤` 的 fail-closed 策略一致，但 `Peak` 的 `topCount` 与 `peakSum` 分离记账在 `Audit` 中需保持同步。

---

## 9. 判定

`Signature.Union` 自身是半格，但**建模对象错配**（集合幂等 vs 并发加法）；`Join` 与 `Union` 同值异律、`Parallel` 偏函数、`At`/`Audit` 双重计数、`Loop` 缩放代偿共同导致**组合性在最窄意义成立、在使用者意义失败**。代数内核（`⊤` 闭包、区间算术、量纲隔离）扎实，修复需统一冲突谓词与多重集语义，无需重写内核。

```

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "符号表 15 行逐符号验半格律/分配律/置换不变，行号到 Objects.cs:205/215/186 Numeric.cs:101/25 Algebra.cs:38 DerivedMetrics.cs:50/76 EffectScript.cs:90/134，P1×6/P2×3，残余风险与最小修复已列"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round08-composability.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "ls / find / grep (read-only discovery)",
      "result": "passed",
      "summary": "enumerated src files and verified Union/Join/Loop/At/Audit/Weight sites, no shell write"
    }
  ],
  "validationOutput": [
    "7-source read-only audit: Union set-semilattice holds but multiset mismatch, Join idempotent fails on Union products, Parallel partial vs Union total, At permutation-invariant but At/Audit double standard, Audit gates interfaces dead and non-homomorphic, Weight NaN eradicated via KIND_MIX throw"
  ],
  "residualRisks": [
    "Union/At set deduplication still halves concurrent identical claims; Loop scaling only papers over when copies structurally equal",
    "Join vs Union same type Signature non-isomorphic; user choice silently flips Peak/net by merged interval vs duplicate preservation",
    "Parallel is partial (throws PARA_CONFLICT) while Sequence/Union are total — associativity/totality not composable",
    "Audit INetGate/IPeakGate/ICompatGate defined but not wired; Audit(a⊔b) != Audit(a)∧Audit(b) (leak complementarity, cross-script CompatibleConflict)"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round08-composability.md：Rich Hickey 视角组合性审计，15 符号半格律/分配律/置换不变/门正交/NaN 毒化逐项验证，含行号反例与 P1/P2 最小修复",
  "reviewFindings": [
    "P1: Objects.cs:205-212 Union/At set deduplication collapses concurrent copies — Peak/net halved vs Audit grp counting (EffectScript.cs:90-99 vs 276-285)",
    "P1: Objects.cs:215-231 Join idempotent fails on Union products with same key different sizes (Union→2 claims, Join→1 merged [10,50])",
    "P1: Objects.cs:205 vs 215 Union vs Join same Signature type but divergent Peak (60 vs 50) and Loop(·,3) 180 vs 150 — non-composable semilattice fork",
    "P1: DerivedMetrics.cs:76-85 Parallel partial (PARA_CONFLICT throw) vs Sequence/Union total — three compositions three fates, breaks associativity/totality",
    "P1: DerivedMetrics.cs:50-64 Loop scaling vs Union replication inequivalence — Loop(body,2)=[2,2] vs Union(copy,copy)=[1,1] dedup",
    "P1: EffectScript.cs:90-99 At permutation-invariant but semantically lossy vs Audit sweep multiplicity; split-vs-Loop encoding non-unique",
    "P1: EffectScript.cs:370-372 Audit gate interfaces dead (not wired), Audit non-homomorphic and gate1/gate2 coupled via LoopCount.Top exemption",
    "P2: Algebra.cs:38 Weight.Of correctly eradicated NaN via KIND_MIX throw — preserves algebra at cost of partiality (preferable to NaN poisoning)",
    "P2: DerivedMetrics.cs:66-70 Sequence is pure Union alias [Obsolete] — should be removed"
  ],
  "manualNotes": "Reviewer 角色无写工具，未落盘；上游需将正文中 markdown 写入 D:/Godot/Cosmos/audit/rich-hickey-round08-composability.md 并校验。"
}