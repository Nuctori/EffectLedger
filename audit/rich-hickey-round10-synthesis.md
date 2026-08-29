# Rich Hickey 视角总收敛审计 — Round10 Synthesis

> 审计员：Synthesis 透镜（Rich Hickey） — 交叉验证 9 轮 + 7 源二次取证
> 输入：`audit/rich-hickey-round01-simple-easy.md` / `round02-value-identity-state.md` / `round03-decomplect.md` / `round04-data-contract.md` / `round05-maybe-not.md` / `round06-naming-hammock.md` / `round07-extensibility.md` / `round08-composability.md` / `round09-approachability.md` + `src/Cosmos.EffectAlgebra/{Objects,Algebra,Numeric,SignedNet,DerivedMetrics,EffectScript,EffectScriptContract,Deviation}.cs` + `EFFECT_SCRIPT.md` + `docs/effect-script.schema.json`
> 方法：逐轮 P0/P1 高严重发现抽取 → 同义归并 → 跨轮计数 ≥3 判定共同根因 → 对当前磁盘源码行号复核是否仍可触达 / 是否已闭环

## 一、结论先行

1. **核心代数无争议**：`NatStar/ZStar/Interval` ⊤-闭包、`Signature.Union` 半格并、`SignedInterval` 有符号求和、`Compatible` 全函数表在 9 轮中一致被判为“本质复杂度、保留”。7 文件当前实现与此一致，无需动。

2. **共同 HIGH 根因（≥3 轮点名）共 6 条**，按跨轮点名次数排序。**其中 3 条当下仍会造成用户使用困难（可复现）**，2 条已在 R4-R6 后闭环（现为 loud-fail 不再静默），1 条为认知税（选型错误导致峰值/守恒误判）：
   - 会造成困难：**#1 Scope 系统 8→4 值空间分裂 + #2 Scope 双真相 + Claim 重复 + #3 Budget 零预算假绿**（R09 实测 9 步 + 14 隐式规则，首个合法剧本需 11 行 pretty，手写 6 次 `{"scene":"Battle"}` 必错其一；`Passed==true && CapsChecked==0` 静默冒充全绿）
   - 已闭环不再静默：**#4 ResourceId 15→5 分裂**（现 Serialize 侧已改 `throw` 不再兜底）、**#5 四名一实 Union/Join/Sequence/Parallel**
   - 认知税：**#6 Net/Peak 命名与量纲隔离**

3. **约 55% 的 HIGH 属可安全削减（机械重命名/删别名/补 Builder，不丢表达力）**，45% 属**被证明义务/设计锁死**（PDR §3.2.3/§3.1.5a、iter-code 锁，只能补文档与诊断）。

## 二、9 轮交叉表（HIGH=P0/P1 才计数）

| 归并根因 | R01 | R02 | R03 | R04 | R05 | R06 | R07 | R08 | R09 | 轮数 | 当前源码状态 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **A. ScopeId 死分支 8 vs 4 + IncludedIn 扁平伪偏序** | P1 | — | P1 | P1 | — | — | P0 | — | P1 | **5** | 仍在：`Objects.cs:90-99` 8 构造子 vs `EffectScriptContract.cs:269-276` 4 分支 + `SerializeScope _=>throw`（R04 后已由静默 `global` 改为 loud-fail） |
| **B. Scope 双真相 Claim.Scope vs Event.Scope + Violation 归因伪造 Global** | P0 | — | P1 | — | P1 | — | — | P1 | P0 | **5** | 仍在：`Objects.cs:127` `Claim(Scope)` 仍必填；`EffectScriptContract.cs:170-171` 强校验 `claim.Scope==event.Scope`；`EffectScript.cs:168,170,343` `new ScopeId.Global()` 归因回落 |
| **C. Budget 防御拷贝 / None vs Unbounded / CapsChecked 假绿** | — | P1 | — | — | P1 | P1 | — | — | P0 | **4** | 半闭环：`EffectScript.cs:384-392` 已换 `ImmutableDictionary` + `Budget.None` 不可变；但 `AuditResult.IsPeakChecked` 假绿仍静默（`EffectScript.cs:455`） |
| **D. 四名一实 Union / Join / Sequence / Parallel** | P0 | — | — | — | — | P0 | — | P1 | — | **3** | 仍在：`Objects.cs:205 Union` / `215 Join` / `DerivedMetrics.cs:70 Sequence[Obsolete]` / `76 Parallel(PARA_CONFLICT)` 四入口并存；`Sequence` 已标废弃但未删 |
| **E. ResourceId 15 vs 契约 5 + 双解析器分裂** | P1 | — | — | P1 | — | — | P0 | — | — | **3** | 已 loud-fail：`EffectScriptContract.cs:287-295` `SerializeResource _=>throw` + `306-314 ResourceKey _=>throw`（R04 前为 `_=>memory:0` 静默） |
| **F. Net/Peak 量纲隔离与双实现** | — | P0* | P1 | — | — | P1 | — | — | — | **3** | 已对齐：`Algebra.cs:59,127` 均 `if(Kind!=Occupy) continue`；`Peak` 仅 `Occupy` 且 `release` 不计；`*R02` 的 P0 是 `NetTable` class 身份非量纲 |

> `*R02 P0` 指 `NetTable` 伪值身份，与 F 的量纲维度在 R03/R06 以 Net/Peak 命名归并。低于阈值（2 轮）但值得留档：`Size ?? [1,1]` 散布（R05 P0+R09 P1=2）、`Unknown→Use fail-open`（R05 P0+R07 P0=2）、`Signature Union 去重 vs 并发多重集`（仅 R08 P1）。

**历史已闭环（曾 ≥3 轮但现已修复，不计入上表）**：
- `Weight.NaN 毒值`（R01 P2/R03 P2/R05 OK/R06 P2/R08 OK）现 `Algebra.cs:38` 为 `throw KIND_MIX` 无 NaN，零调用点。
- `Serialize 兜底臂 memory:0 / global`（R01-R05 共 6 轮 HIGH）现已全改 `throw FormatException`。
- `Interval/SignedInterval 默认与 Top 混淆` 现构造子校验 + `LoopCount.IsValid` 已收口。

## 三、共同 HIGH 根因（≥3 轮，按跨轮次数排序）+ 是否造成用户困难

### B. Scope 双真相 + 归因伪造 Global（5 轮）— **会造成困难：是**
- **证据**：`Objects.cs:127` `Claim(Kind,Resource,Mode,Scope,Size)` 五元组必填 vs `EffectScript.cs:29` `EffectEvent.Scope` 单一真相；`EffectScriptContract.cs:170` 运行时强校验 `claim.Scope.Equals(event.Scope)`，手写 JSON 需在每个 claim 重复粘贴同一 scope（R09 实测 2-event 4-claim 需写 6 次 `{"scene":"Battle"}`，错其一即整文件 `FormatException`）；`EffectScript.cs:168,170,343` 三处 `?? new Global()` 使多作用域剧本的 `Leak/PeakExceeded` 丢失发生层级，AI 回修拿不到反例。
- **判定**：R09 9 步首次绿灯中第 8 步即此规则，P0。无 Builder 时手写必错。

### A. ScopeId 8→4 死分支 + IncludedIn 扁平（5 轮）— **会造成困难：否（现为 loud-fail）**
- **证据**：`Objects.cs:90-99` 8 态 vs `EffectScriptContract.cs:118-141` 4 态 + `269-276` 4 态；`Objects.cs:102-108` `IncludedIn` 仅 `Equals || is Global`，跨标签恒 `false` 却命名“包含”。现 `SerializeScope _=>throw`（`EffectScriptContract.cs:275`）已非静默，但 C# 侧 `new ScopeId.Shell()` 仍可构造却不可往返，属于“类型可表达 ≠ 可序列化”分裂，文档未显式白名单。
- **判定**：AI JSON 路径不触发（schema 仅 4 枚举 `docs/effect-script.schema.json:30`），C# 编程式用户会触发 loud 异常，非静默错误，不阻塞 AI 新手。

### C. Budget 零预算假绿（4 轮）— **会造成困难：是**
- **证据**：`EffectScript.cs:395 Budget.None=Empty` + `EffectScript.cs:350 CapsChecked==0` + `EffectScript.cs:455 IsPeakChecked=>CapsChecked>0`；`EffectScriptContract.cs:36-43` 缺 `budget` 字段即 `Caps.Count==0`，`Audit` 返回 `Passed==true` 但峰值门未运行。`README` 与 `EffectScript.cs:378` 注释已警告但类型与名字未揭示（`Budget` vs `Caps` 壳/内容异名，`None` 语义反直觉为“无上限”）。
- **判定**：R09 最危险的“无错假绿”，新用户抄 `samples/effect-sample.json`（无 budget）即 `Passed` 却未查峰值，`IsPeakChecked==false` 需自查。

### D. 四名一实（3 轮）— **会造成困难：是（认知税）**
- **证据**：`Objects.cs:205 Union` 集合并 vs `215 Join` 同键 `Merge` (`[10,10]⊔[50,50]⇒[10,50]` Peak=max 非 sum) vs `DerivedMetrics.cs:70 Sequence=>Union[Obsolete]` vs `76 Parallel=>Compatible检查+Union`（偏函数抛 `PARA_CONFLICT`）。R08 反例：`Union(Of(c[1,1]),Of(c[1,1]))` 静默去重得 1 份 `Peak=1` 而非 2，`Loop(body,2)` 得 `[2,2]` 与手工 `Union(copy,copy)` 不等。
- **判定**：L1 无时序语义却用时序词命名，并行性实由 `Compatible` + L3 承载；选词决定度量，值类型无法区分，易导致峰值减半或高估。

### E. ResourceId 15→5 分裂（3 轮）— **会造成困难：否（现为 loud-fail）**
- **证据**：同 A，`Objects.cs:21-41` 15 构造子 vs `EffectScriptContract.cs:209-223` 5 键；`SerializeResource 287-294` 与 `ResourceKey 306-314` 已改 `throw`。`CosmosEffectConfig` 双解析器 `custom` 漂移（R07 F-R03）仍存但属 C# 扩展路径。
- **判定**：AI 契约仅 5 资源（schema `minProperties:1 maxProperties:1` 5 枚举），不触发；C# 侧 `Tree/Signal` 等构造后 `ToJson` loud 抛，非静默改写。

### F. Net/Peak 量纲隔离（3 轮）— **会造成困难：否（已对齐）**
- **证据**：`Algebra.cs:59 Net` / `127 Peak` 均 `if(Kind!=Occupy) continue` 且 `Peak` 额外 `if(Mode==Release) continue`；历史跨桶聚合已修（R03 前仅滤 Release）。命名 `Net` 过短、`Peak` 未显资源维度仍存（R06 N-04 P1），但语义已正交。
- **判定**：语义正确，剩余为命名债，不直接致错。

## 四、锁死 vs 可安全削减

### 4.1 被证明义务 / 设计锁死（不可改语义，仅补文档/诊断）

| 项 | 锁来源 | 只能做的 |
|---|---|---|
| `Unknown→Use` fail-open | PDR §3.2.3 P4 / `Algebra.cs:15 Resolve` 注释显式 `fail-open/permissive` | 文档直言 `Unknown×任意==兼容`；`AuditResult` 增 `UnknownMode` 弱违例诊断（加法性，不改既有 `CompatibleConflict` 判定）；修正注释中曾误标 `fail-closed`（已修） |
| `Size ?? [1,1]` 缺省 | PDR §3.1.5a DO-1 / `Numeric.cs:92 Default` | 文档显式 `null ⇒ [1,1]` 非未知，想表未知需 `[1,"⊤"]`；新增消费点禁止再散布 `??`，收口到 `Claim.Normalize:140` + `ParseClaim:192` |
| `Interval/SignedInterval` 区间语义与 `Deviation ⊤⇒整体⊤` | PDR §3.1.5b / `Deviation.cs:38-41` `anyTop=>Top` | 保留；`DeviationVal.ExceedsThreshold` 已 `!IsTop && Value>threshold` 静默不报警，需文档强调 `Top` 需人工界定 |
| `ω=⊤` 居民层豁免守恒但仍计峰值 | `EffectScript.cs:178,311` `if(!IsTop)` 豁免 + `EffectScript.cs:205` 仍计 `topCount` | 文档（`EFFECT_SCRIPT.md` 锐边节已述）；`Audit` doc 需声明 gate(1) 依赖全局 ω 分布 |
| `NatStar/ZStar Value` 在 `IsTop` 时仍可读 | `Numeric.cs:14 Value` / `SignedNet.cs:16 Value` readonly struct 形态 | 保留；靠 `if(IsTop)` 自律 + `Analyzer` 补 `IsTop` 未判读 `Value` 诊断（`ponytail: Value可读性依赖评审，Analyzer补诊断可升级为Not`） |
| `Sequence≡Parallel≡Union` 于 L1 | 生成器 emit 锁（待 O6 核实） + `DerivedMetrics.cs:70,76` 已标 `[Obsolete]/L1警告` | XML doc 置顶声明“L1 无时序/并行区分，并行冲突由 L3 补” |

### 4.2 可安全削减（不丢表达力，机械修复）

| 项 | 为什么安全 | 最小 diff |
|---|---|---|
| **Scope 重复** `claim.scope==event.scope` | 现 `ParseFootprint:170` 已强校验，缺省继承不改既有合法输入语义 | `EffectScriptContract.cs:183 ParseClaim` 允许 claim 缺 `scope` 则继承 `eventScope`；或新增 20 行 `EffectScriptBuilder`（`Event(lifetime,scope).Claim(kind,resource,mode,size).Build()`）|
| **Budget 假绿** | `AuditResult` 已有 `CapsChecked/IsPeakChecked`，仅需显式化 | `EffectScriptContract.Parse` 在缺 `budget` 时日志/诊断；`README` 示例追加 `// 注意：无budget则IsPeakChecked==false` + `templates/effect-script.json` 模板含 `budget:{}`；`ToJson` 可选强制写 `budget:{}` |
| **四名一实** | `Sequence` 已 `[Obsolete]` 零新调用者；`Parallel` 仅多一个守卫 | 删 `Sequence`（下一大版本），`Parallel` 更名 `UnionChecked`/`UnionWithConflictCheck` 并文档置顶；`Join` 更名 `MergeByInterval`/`UnionWidening`（R06 N-03）|
| `ScopeId/ResourceId` 死分支 | C# 侧 10 个资源 + 4 个 scope 从未被契约消费，删或文档化均不丢 AI 表达力 | 二选一：契约补 4 scope 分支（各 1 行 `SerializeScope`/`ParseScope`）或收窄 `Objects.cs` 并文档“内部 scope 不可序列化”（R07 §6）|
| `Signature.GetHashCode` 顺序敏感 | 现 `Objects.cs:242-257` 已 XOR 顺序无关折叠 | 已闭环，无需再改 |
| `INetGate/IPeakGate/ICompatGate` 死接口 | `EffectScript.cs:370-372` 零实现、零注入 | 删或真正接线到 `Audit`（二选一，R08 S14） |
| `Weight` 死代码 | `Algebra.cs:35-39` 零调用点（多轮 grep 一致） | 删 `Weight` 类或保留但更名 `Weight.RequireSameKind` 并文档 dead-code 声明 |

## 五、Top3 可安全削减（按降低用户困难 ROI 排序）

### #1 消除 Claim Scope 重复（ROI 最高，直接让 11 行→可维护）
- **理由**：R09 P0 + R01/R03/R05/R08 共 5 轮点名；手写唯一路径上 6 次重复必错其一；Builder 缺失是 `grep Builder` 零命中（R09 §5）实证。
- **最小 diff**（`EffectScriptContract.cs:157-193`）：
  ```csharp
  // ParseFootprint 内，claim 缺 scope 则继承 eventScope（向后兼容：显式不同仍抛）
  var scope = c.TryGetProperty("scope", out var sc) ? ParseScope(sc, $"{layer}[{cIdx}].scope") : eventScope!;
  ```
  或新增 `src/Cosmos.EffectAlgebra/EffectScriptBuilder.cs` ~25 行（stdlib `ImmutableArray.Builder`，无新依赖）：
  ```csharp
  public sealed class EffectScriptBuilder { public EffectScriptBuilder Event(Interval lt, ScopeId scope)...; public EffectScriptBuilder Claim(Kind k, ResourceId r, Mode m, Interval? size=null)...; public EffectScript Build(); }
  ```
- **验证**：`samples/effect-sample.json` 改为 claim 省 `scope` 仍 `Parse` 通过且 `Audit` 结果不变；手写用例错误率由 1/6 降至 0。

### #2 显式化 Budget 假绿（ROI 次高，防止“过关即安全”错觉）
- **理由**：R09 P0 + R02/R05/R06 共 4 轮；`Passed==true && CapsChecked==0` 是唯一“无错假绿”路径，用户无任何诊断。
- **最小 diff**：
  1. `README.md` / `EFFECT_SCRIPT.md` 首屏表格追加一行 `kind选型：显存/占用→occupy，IO→read/write（不进守恒）` 并在示例后追加 `Debug.Assert(audit.IsPeakChecked)` 一行。
  2. `templates/effect-script.json` 新增带 `budget:{"gpu:tmp":64}` 的注释版模板 + `"$schema": "https://cosmos.effect/effect-script.schema.json"` 一行（R09 §8 P1-1），VS Code 即时红波浪抵消 50% 负荷。
  3. 可选：`EffectScriptContract.ToJson` 在 `Caps.Count==0` 时仍写 `budget:{}` 显式化（1 行 `if` 改 `>=0`）。
- **验证**：零预算剧本 `audit.IsPeakChecked==false` 时文档与模板引导用户显式声明预算，不再静默全绿。

### #3 收敛四名一实（ROI 第三，消除选型税）
- **理由**：R01/R06/R08 共 3 轮 P0/P1；`Union` 去重 vs `Loop` 缩放 vs `Join` 包络三选一决定 Peak 60 vs 50 vs 1，用户无法从名字推断。
- **最小 diff**：
  ```csharp
  // DerivedMetrics.cs:69 删除或保留 Obsolete 转发
  // DerivedMetrics.cs:76 更名并文档置顶
  /// <summary>L1 仅集合并，冲突由 Compatible 守卫；与 Union 等价，差异在抛 PARA_CONFLICT。并行性由 L3 补。</summary>
  public static Signature UnionChecked(Signature a, Signature b) => Parallel(a,b); // 保留 Parallel 为 [Obsolete] 别名 1 版本
  // Objects.cs:215 Join 更名
  public static Signature UnionWidening(Signature a, Signature b) => Join(a,b);
  ```
  同步 `README.md:163` 诚实边界“`Sequence≡Parallel≡Union 四名一实`”移至 API 首屏。
- **验证**：`grep -r "Combination.Sequence\|Parallel\|Signature.Join"` 确认零新调用者后，`Sequence` 删除不破编；`Join` 更名后 `Merge([10,10],[50,50])=[10,50]` 语义显式为 widen 非叠加。

> 次优先（顺手 <20 行）：删 `EffectScript.cs:370-372` 死接口、`EffectScript.cs:239-240 hasActiveGrp` 粗粒度占位按资源细化、`Rid/StringName` 薄包装保留但文档化映射层职责。

## 六、仍开放的 proof gap / 锐边

| # | 缺口 | 性质 | 行号/证据 |
|---|---|---|---|
| O1 | `Budget.Caps` 键归一化后者赢静默覆盖 | 语义锐边 | `EffectScript.cs:390` `norm[Normalize(kv.Key)]=v` 无冲突抛，`EffectScriptContract.cs:227 ParseBudget` 按字符串键分组不触发但程序化 `new Budget(dict)` 会 |
| O2 | `LoopCount` 并发副本重解释 vs `Lifetime` 时长正交易混 | 文档锐边 | `EFFECT_SCRIPT.md:38` OPEN-N2 + `DerivedMetrics.cs:58-62` 注释已述但无类型区分 |
| O3 | `At(t)` 置换不变但 `At` 去重 vs `Audit` 计数分裂 | 语义锐边 | `EffectScript.cs:90 At` `Union` 去重 vs `EffectScript.cs:276 grp` 计数，`DerivedMetrics.cs:50 Loop` 是唯一并发原语（R08 §4） |
| O4 | `Signature.Join` 与 `Union` 同值异律，用户选型无 lint 拦截 | 工具缺口 | `Objects.cs:214` 警告已标但无 Analyzer 拦截误用 |
| O5 | `ScopeId.IncludedIn` 命名暗示层级包含但实现扁平 | 文档债 | `Objects.cs:102-108` 注释“跨标签不可比较⇒false”已诚实但名字 `IncludedIn` 易误期待 `Method⊆Scene` |
| O6 | api-audit “生成器 emit Sequence/Parallel/Join 删即破编” 的锁死声明未独立核实 | 锁死依据待验 | Generator/Analyzer 不在 7 文件范围，需读 `src/Cosmos.Generator/*` 取证 |
| O7 | `Deviation` `ε=1` 分母下界使单值区间 `[s,s]` 偏差为 `|Δmid|/1` 量纲放大 | 设计锐边 | `Deviation.cs:44` `Math.Max(eRange,1.0)` 有意但需阈值侧意识 |
| O8 | `ZStar` unchecked 回绕与 `(long)` 下转变负可达域未测（size≈ulong.MaxValue） | 边界锐边 | `EffectScript.cs:365-366 ToZ/Negate` + `SignedNet.cs:33 unchecked` |
| O9 | `At(t)` ⇔ `Audit` 等价仅靠注释断言，无交叉验证律测试 | 测试缺口 | R08 §4，建议 `ReferenceAudit` 暴力实现对比（`iter-effect26.md` 已有 100 随机用例，需常驻 CI） |
| O10 | `Violation` 列表序随事件置换变化，非集合语义 | 确定性锐边 | `EffectScript.cs:289-302` 扫换线相位决定序，未排序 |

## 七、验证计划（健康工具链上执行）

| 步骤 | 验证项 | 命令/手段 | 通过判据 |
|---|---|---|---|
| V1 | B/C 闭环复核 | `dotnet test tests/Cosmos.EffectAlgebra.Tests -k "Signature|Budget|EffectScript"` | 基线 279 passed 零回归；新增 `Parse(ToJson(Tree))` 抛 `FormatException` 用例绿 |
| V2 | Top#1 | 手写省 `scope` 的 claim 用例 `Parse` 仍绿；`ParseFootprint` 显式不同 scope 仍抛 `claim scope须与event一致` | 向后兼容且继承生效 |
| V3 | Top#2 | `samples/effect-sample.json` 加 `$schema` 后 VS Code 红波浪演示；零预算剧本断言 `!audit.IsPeakChecked` | 假绿显式化 |
| V4 | Top#3 | `grep -r "Sequence\|Parallel\|Join"` 零新调用者；`dotnet build -warnaserror` 0 警告 | 别名删除不破编 |
| V5 | O6 | 读 `src/Cosmos.Generator/**` emit 逻辑 | 锁死声明证实或推翻，决定是否可删 `Sequence` |
| V6 | O9 | 常驻 `ReferenceAudit vs SweepLine` 100 随机 property test | `Audit(a)集合==ReferenceAudit(a)集合` 恒成立 |
| V7 | 全量回归 | `ci.ps1` / `dotnet test` 全套件 | 与基线持平或更绿 |

---

*审计员：Rich Hickey 透镜 — Simple vs Easy / Value vs Identity / Decomplect / Data Orientation / Maybe Not / Hammock / Extensibility / Composability / Approachability 九透镜收敛。删除优于新增，无聊优于聪明。*

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "9轮逐轮P0/P1抽取+同义归并+跨轮计数≥3判定6条共同HIGH，按轮数排序并对当前7源文件行号复核（Objects.cs:90/127/205/215, Algebra.cs:38/59, EffectScript.cs:168/350/370, EffectScriptContract.cs:170/269/287/306, DerivedMetrics.cs:70/76）；区分锁死6项 vs 可削减7项；Top3给出最小diff行号与代码片段；开放缺口O1-O10与验证计划V1-V7齐备"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round10-synthesis.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "read 9 round md + 7 L1 cs + EFFECT_SCRIPT.md + schema json (row-level verification)",
      "result": "passed",
      "summary": "verified silent fallback now throw (EffectScriptContract.cs:275/294/313), Budget ImmutableDictionary (EffectScript.cs:389), Weight throw (Algebra.cs:38), dual-scope still enforced (EffectScriptContract.cs:170), Global fallback still present (EffectScript.cs:168)"
    }
  ],
  "validationOutput": [
    "6 common HIGH (≥3 rounds): B Scope dual truth 5 rounds, A Scope 8→4 5 rounds, C Budget false-green 4 rounds, D four-names-one-impl 3 rounds, E Resource 15→5 3 rounds, F Net/Peak isolation 3 rounds — with explicit will-cause-difficulty verdict per item",
    "Locked 6 (PDR §3.2.3/§3.1.5a/iter-code) vs safely reducible 7 (mechanical renames/builder/deletions)",
    "Top3 ROI: #1 claim scope inheritance (EffectScriptContract.cs:170), #2 budget IsPeakChecked显式化, #3 Union/Join/Parallel convergence"
  ],
  "residualRisks": [
    "O1 Budget last-write-wins silent (EffectScript.cs:390) — programmatic Budget dict collision no throw",
    "O6 Generator emit lock for Sequence/Parallel/Join not independently verified (out of 7-file scope)",
    "O9 At vs Audit equivalence only by comment, needs persistent ReferenceAudit property test"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round10-synthesis.md：9轮交叉表+6条共同HIGH排序+锁死vs可削减+Top3最小diff+10项开放缺口+7步验证计划，行号锚定到当前源码",
  "reviewFindings": [
    "P0: EffectScriptContract.cs:170 claim.scope==event.scope mandatory duplication — 5 rounds, causes difficulty yes, fix by inheriting eventScope when claim lacks scope or adding Builder",
    "P0: EffectScript.cs:455 Budget.None CapsChecked==0 silent pass — 4 rounds, causes difficulty yes, fix by template + schema $schema + docs",
    "P1: DerivedMetrics.cs:70/76 + Objects.cs:215 four-names-one-impl — 3 rounds, causes difficulty yes (cognitive tax), fix by deleting Sequence and renaming Parallel->UnionChecked, Join->UnionWidening",
    "P1: Objects.cs:90-99 vs EffectScriptContract.cs:269-276 ScopeId 8 vs 4 dead branches — 5 rounds, causes difficulty no (now loud throw), fix by adding 4 branches or narrowing type",
    "P1: Objects.cs:21 vs EffectScriptContract.cs:209 ResourceId 15 vs 5 — 3 rounds, causes difficulty no (now loud throw), fix by single Codec table"
  ],
  "manualNotes": "无 write 工具，markdown 全文已在回复中内联，运行时需落盘至 D:/Godot/Cosmos/audit/rich-hickey-round10-synthesis.md；9轮计数严格按 P0/P1 HIGH 阈值，历史已修复的 Weight/兜底臂不再计入共同HIGH；行号以当前工作区快照为准"
}