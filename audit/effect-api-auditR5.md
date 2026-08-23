# EffectAlgebra 组合性 / 不可变值语义 独立对抗性审计 (auditR5)

**视角**：Rich Hickey「组合性 / 值语义 / 可预测性」。宁误报组合陷阱。
**严格独立**：仅依据以下源码推导，未读 `audit/` 历史文件：
`EffectScript.cs` `EffectScriptContract.cs` `Objects.cs` `Numeric.cs` `Algebra.cs` `SignedNet.cs` `DerivedMetrics.cs`。
**立场**：多数载体是优秀的不可变值对象；陷阱集中在**隐藏可变状态 (`Budget`)**、**闭包漏检未来事件**、**`At` 的 scope 投影与 `Parallel` 语义与实际实现错位**。

---

## 0. 独立声明（结论先行）

- 核心数值载体（`NatStar`/`Interval`/`ZStar`/`SignedInterval`/`LoopCount`/`Claim`/`EffectEvent`/`Violation`/`AuditResult`）全部为 `readonly record struct`，构造即全必填，**真·不可变值语义良好**。
- `Signature` 是 `sealed class` 但无公共 mutator，内部 `Add` 走"复制三桶 + 单桶 Add"的写时复制，**对外表现为值对象（effectively immutable）**，但有内部就地 `s._read = ...` 突变（非写时复制纯净版），属"伪装不可变"。
- **已证实的「可预测性陷阱」有三处**，按严重度：
  1. `EffectScript.Budget` 是可变属性（`get; init;`）——同一脚本实例可被赋予不同预算后再次 `Audit()`，组合结果依赖隐藏可变状态。**(严重)**
  2. `Audit` 闭包 `Leak` 检查静默排除 `Lo > maxFinite` 的"未来事件"，用户极易误以为"所有事件都进了守恒账"。**(中)**
  3. `Combination.Parallel` 与 `Sequence` 实现完全相同（都是 `Union`），但 §3.2.2 文本要求"跨调用点 Compatible 检查"，而此检查**根本不在 `Parallel` 内**，也未在 `Audit` gate(3) 中做真正的 `Compatible.IsCompatible` 配对——文档语义与实现错位。**(中/严重)**
- **疑似（无运行测试佐证，仅静态推导）**：`At(t)` scope 投影把每个事件的 claim 重新 scope 到 `e.Scope`（经 `Combination.Loop`），而 `Audit` gate(2)/gate(3) 直接用原始 footprint / `e.Scope`；二者对"某 scope 的峰值"可能给出不同数字（scope 投影差异）。`Audit` 的 `sweep.Sort` 非稳定排序，"同时同相位"瞬态 `grp` 集合顺序理论依赖事件索引，但因判定只数"≥2"而结果顺序无关，仅确定性靠"结果集不依赖顺序"保证而非严格的稳定排序。

---

## 1. 逐 API 表（值语义 / 组合性 / 判定 / 证据 / 严重度）

| API | 值语义 / 组合性 | 判定 | 证据 (file:line) | 严重度 |
|---|---|---|---|---|
| `NatStar` (`readonly record struct`) | 不可变 ℕ*∪{⊤}，+/*/Max/Min 内嵌 ⊤ 律，溢出保守 ⇒ ⊤，无 NaN。纯函数。 | PASS | `Numeric.cs:11-83` | 无 |
| `Interval` (`readonly record struct`) | 构造即校验 `lo≤hi`、禁止 `[⊤,有限]`；`Merge` 为 join-半格（幂等/交换/结合）。纯函数。 | PASS（fail-fast 良好） | `Numeric.cs:88-150` | 无 |
| `ZStar` / `SignedInterval` (`readonly record struct`) | 有符号网值，+ / − / Min / Max 内嵌 ⊤ 律；`Add` 为区间逐端相加（真·求和，非吞守恒的 Merge）；`ContainsZero` fail-closed。 | PASS | `SignedNet.cs:9-104` | 无 |
| `LoopCount` (`readonly record struct`) | 不可变 ω∈ℕ∪{⊤}。纯值。 | PASS | `DerivedMetrics.cs:9-30` | 无 |
| `Claim` (`readonly record struct`) | 五参位置记录，构造即全必填；`Normalize` 用 `with` 返回新值（不原地改）。 | PASS | `Objects.cs:108-125` | 无 |
| `ResourceId` / `ScopeId` (`record`) | 判别联合，结构相等；`Normalize` 幂等（已修正 `SignalBus` 二次剥前缀）；`ScopeId.IncludedIn` 自反+Global 最大元，但**跨标签返回 false ⇒ 非完整偏序格**（无 LUB）。 | PASS（注：非格） | `Objects.cs:30-95` | 低（注记） |
| `Signature` (`sealed class`) | **对外只读三桶 (ImmutableHashSet)**，无公共 mutator；`Union/Join/Of` 返回新实例 ⇒ 值语义合格。但 `Add` 内部 `s._read = s._read.Add(n)` 是"复制三桶引用 + 单桶突变"，属"伪装不可变"（非纯净写时复制，仅因新实例未被观测而安全）。 | PASS（有异味） | `Objects.cs:130-182` | 低 |
| `Signature.Union` / `Join` | 半格并：幂等、交换、结合，`Empty` 为单位元。满足组合定律。 | PASS | `Objects.cs:166-178` | 无 |
| `Combination.Loop(body,ω,scope)` | 纯函数：把每个 claim `with { Scope=loopScope, Size=Scale(...) }` 后 `Union`。`ω=⊤ ⇒ [lo,⊤]`。无原地修改。 | PASS | `DerivedMetrics.cs:33-55` | 无 |
| `Combination.Sequence` | `= Union`；满足半格律。 | PASS | `DerivedMetrics.cs:58-59` | 无 |
| `Combination.Parallel` | **实现 = `Union`**，与 `Sequence` 逐字节相同；但 §3.2.2 文本要求"跨调用点 Compatible 检查由 L3 Analyzer 补"——**该检查不在此函数内，也不在本仓库 `Audit` gate(3) 内**（gate(3) 只数同 mode 活跃事件数 ≥2，非逐对 `Compatible.IsCompatible`）。文档语义 ≠ 实现。 | FAIL（语义错位） | `DerivedMetrics.cs:62-63`；`EffectScript.cs` gate(3) `:171-186` | 中/严重 |
| `Compatible.IsCompatible` | 全函数 + 对称（P1）；`Unknown→Use` 最弱；CONFLICT={C×C,M×M,R×R}。纯函数、可预测。 | PASS | `Algebra.cs:14-44` | 无 |
| `NetTable.Compute` (net) | 按归一化资源分组、`create/move(+)` 与 `release(−)` 有符号 `Add` 求和（**正确：用 Add 非 Merge**，否则吞守恒）；`IsConserved` fail-closed（无记录或 ⊤ ⇒ false）。纯函数。 | PASS | `Algebra.cs:48-92` | 无 |
| `Peak.Compute` | 求和非 release 的 `Hi`；任一 ⊤ ⇒ 整体 ⊤（早退）。`NatStar+` 结合/交换 ⇒ 顺序无关。 | PASS | `Algebra.cs:96-112` | 无 |
| `EffectEvent` (`readonly record struct`) | 四参位置记录 + 默认 ω=1 重载；不可变。 | PASS | `EffectScript.cs:21-67` | 无 |
| `EffectScript` 构造 | `ImmutableArray` 存储；`IEnumerable` 入参被 `ToImmutableArray()` 复制 ⇒ 不保留外部可变集合引用。值语义良好。 | PASS | `EffectScript.cs:78-96` | 无 |
| `EffectScript.At(t)` | 折叠 `Union`，`Alive` 判定与事件顺序无关 ⇒ `At` 是**置换不变**的。但 `Combination.Loop(.., e.Scope)` 把每个事件 claim 重新 scope 到 `e.Scope` ⇒ `At(t)` 返回的 Signature 是**多 scope 混合体**；用户以特定 scope 取 `Net/Peak` 时结果依赖 `⊆*` 投影，易误读"为什么某资源峰值对不上"。 | PASS（注：scope 投影陷阱） | `EffectScript.cs:103-117`；`EffectScript.cs:296-305` (`Alive`) | 低（注记） |
| `EffectScript.Audit(cap)` | 扫换线：端点 `SortedSet`（确定性序）；`sweep.Sort` **非稳定**，但相位切分保证结果"≥2 计数"与顺序无关 ⇒ 结果确定性靠"判定不依赖顺序"而非稳定排序（脆弱但当前正确）。局部 `Dictionary` 在调用内被突变，但**不跨调用共享** ⇒ 重复 `Audit` 结果一致（幂等）。 | PASS（脆弱确定性） | `EffectScript.cs:124-290`；`sort` 在 `:155-158` | 低 |
| `Audit` gate(1) `NegativeDip` | 仅 enter 累加有限 ω 的 net；release 已在自身 Lo 带负向 ⇒ exit 不动 net。数学正确。 | PASS | `EffectScript.cs:162-180` | 无 |
| `Audit` gate(2) `PeakExceeded` | 直接用原始 `e.Footprint.OccupyClaims`（**不经 `Combination.Loop` 重 scope**），用 `e.Scope` 分组；与 `At(t)` 经 `Loop` 重 scope 的峰值**scope 口径不同**（投影差异，疑似）。 | PASS（注：与 At 口径不一致） | `EffectScript.cs:181-201` | 低（疑似） |
| `Audit` gate(3) `CompatibleConflict` | 按 `(res, e.Scope, mode)` 活跃事件集，同 mode 计数 ≥2 即报冲突；**等价于"同 mode 多于一个事件"**，≠ 文档所述"逐对 `Compatible.IsCompatible`"。`create×release` 等良性配对因不同 mode 不会同组，故不会误报；但"冲突"定义被**弱化为同 mode 多源**，与 §3.2.3 的 CONFLICT 集语义部分错位。 | PASS（语义弱化/错位） | `EffectScript.cs:171-186`,`203-214` | 低/中 |
| `Audit` 闭包 `Leak` | **静默排除 `e.Lifetime.Lo > closureT`(=maxFinite) 的事件**（`:256-258`）。数学上这些事件在 `maxFinite` 时尚未开始 ⇒ 不影响"之后净效应归零"判定，故**正确**；但用户极易误以为"所有事件都进了守恒账"，且若期望"全时间轴闭合"会被漏检未来事件。 | PASS（易误用） | `EffectScript.cs:250-262` | 中 |
| `Budget` (`readonly record struct`) | 载体不可变；但 `EffectScript.Budget` 是 `public Budget Budget { get; init; }`——**可变属性**。同一 `EffectScript` 实例可被赋不同预算后再次 `Audit()`，组合结果依赖隐藏可变状态（非纯函数式投影）。 | FAIL（隐藏可变状态） | `EffectScriptContract.cs:215` | 严重 |
| `EffectScript.Audit()`（无参） | 用自带 `Budget` 调 `Audit(Budget)`；因 `Budget` 可变，**同一实例两次 `Audit()` 可能因 Budget 被改而结果不同**（除非 Budget 从未改）。 | FAIL（依赖可变 Budget） | `EffectScriptContract.cs:218-220` | 严重 |
| `EffectScriptContract.Parse` | 纯搬运：逐字段校验、非法形状 `FormatException`（fail-fast）；返回新 `EffectScript`。无隐藏状态。 | PASS | `EffectScriptContract.cs:18-46` | 无 |
| `EffectScriptContract.ToJson` | 序列化 `Read/Write/Occupy` 三桶含 `size`；round-trip：Parse 按 `kind` 路由三桶、对称为"完整 footprint"。**无明显丢信息**（除非新增 kind 桶未同步——当前仅三桶，一致）。 | PASS | `EffectScriptContract.cs:135-145`,`55-72` | 无 |
| `EffectScriptContract` 早求值倾向 | 无 API 鼓励"先 ToJson 再 Parse 回修"；契约即不可变数据搬运，组合链为 `Parse→EffectScript→Audit`。 | PASS（无早求值陷阱） | 整体 | 无 |

---

## 2. 总评

**组合是否可预测？** 大部分可预测——数值载体、`Signature` 半格、`Combination.Loop`、net/Peak 都是纯函数且尊重组合（置换不变、顺序无关、幂等）。主要的可预测性破坏来自**一处真·隐藏可变状态**与**两处文档/实现语义错位**：

### Top 3 应改善的组合性 / 值语义点

1. **`EffectScript.Budget` 必须是不可变的一部分（严重）**
   - 现状：`public Budget Budget { get; init; } = Budget.None;`（`EffectScriptContract.cs:215`）。`init` 允许构造后通过对象初始化器改写；无参 `Audit()` 读取它。同一实例被改写 Budget 后再 `Audit()`，结果变 —— 这不是值语义，是"对象带可变配置"。
   - 改善：把 `Budget` 改为构造参数（`EffectScript(ImmutableArray<EffectEvent>, Budget = Budget.None)`），删除可写属性；或至少改为 `init`-only 且**禁止 Parse 后再 set**（目前 `Parse` 用 `new EffectScript(...) { Budget = ... }` 初始化器，可改为构造参数传入）。让 `EffectScript` 成为真正不可变值。

2. **`Audit` 闭包 `Leak` 检对未来事件的"静默排除"应明示（中）**
   - 现状：`:256-258` `if (e.Lifetime.Lo.CompareToFinite(closureT) > 0) continue;` 排除 `Lo>maxFinite` 的事件。数学正确，但 `AuditResult` 不告知"存在未纳入闭包账的未来事件"，用户误以为全量闭合。
   - 改善：当存在 `Lo>maxFinite` 的有限 ω 事件时，在 `AuditResult` 增加 `Info`/注意项（非 Violation），明示"闭包守恒仅覆盖 [0,maxFinite]，N 个未来事件未纳入"。或文档在 §3 显式标注此边界。

3. **`Combination.Parallel` 与 `At` 的 scope / Compatible 语义应与实现/审计对齐（中/严重）**
   - `Parallel` 实现与 `Sequence` 完全相同（`DerivedMetrics.cs:58-63`），但 §3.2.2 文本承诺"跨调用点 Compatible 检查"——该检查既不在 `Parallel` 内，也不在 `Audit` gate(3) 内（gate(3) 只数同 mode 活跃事件数 ≥2，见 `EffectScript.cs:171-186`）。用户调用 `Parallel` 会以为"已做并发冲突检查"，实际没有。
   - `At(t)` 经 `Combination.Loop` 把 claim 重 scope 到 `e.Scope`（`EffectScript.cs:114`），而 `Audit` gate(2)/gate(3) 直接用 `e.Footprint`/`e.Scope`（不经 `Loop` 重 scope）。二者对"某 scope 峰值/冲突"的口径可能因 scope 投影差异而不一致（疑似，需运行测试确认）。
   - 改善：`Parallel` 要么真的接入 `Compatible` 聚合（或显式返回"未检查"标记并改名），要么文档删除"由 L3 补"的承诺、明确"Parallel=Sequence=Union，兼容性由调用方负责"。同时统一 `At` 与 `Audit` 的 scope 投影来源（均用 `e.Scope` 经 `Loop`，或均用 claim 自带 `c.Scope`）以避免数字对不上。

### 次要 / 注记
- `Signature` 内部 `Add` 的"复制三桶后单桶就地突变"是"伪装不可变"（`Objects.cs:150-164`）：当前安全（新实例未被观测），但若将来有人复用旧实例即数据竞争风险；建议改为纯写时复制（三个桶全部 `.Add` 成新集合后整体赋新实例）。
- `ScopeId.IncludedIn` 跨标签返回 false（`Objects.cs:78-91`），故 scope 过滤不是完整格（无 LUB）；组合 `Net/Peak(scope)` 时两个不同标签 scope 互不相交，用户可能误以为有包含关系。
- `At(t)` 返回的 Signature 跨事件混合多 scope，取 `.Net(scope)` 依赖 `⊆*` 投影——可预测但易误读，建议在文档 §3 标注"`At` 是 scope-分段投影，按各事件自身 Scope 合并"。

---

## 3. 已证实 vs 疑似 对照

**已证实违反（有静态证据，无需运行）**
- F1 `EffectScript.Budget` 可变 ⇒ 同实例 `Audit()` 结果可变（值语义破坏）。证据 `EffectScriptContract.cs:215,218-220`。
- F2 `Audit` 闭包静默排除未来事件（正确但易误用）。证据 `EffectScript.cs:256-258`。
- F3 `Combination.Parallel` 实现 = `Sequence` = `Union`，且未做 §3.2.2 承诺的 Compatible 检查；`Audit` gate(3) 也不是逐对 `Compatible.IsCompatible`。证据 `DerivedMetrics.cs:58-63`、`EffectScript.cs:171-186`。

**疑似（仅静态推导，需运行测试确认）**
- S1 `At(t)`（经 `Loop` 重 scope 到 `e.Scope`）与 `Audit` gate(2)（原始 footprint / `e.Scope`）对某 scope 峰值数字可能不一致（scope 投影差异）。
- S2 `Audit` 的 `sweep.Sort` 非稳定（`EffectScript.cs:155-158`），"同时同相位"瞬态 `grp` 顺序依赖事件索引；当前因判定只数"≥2"而结果正确，但确定性脆弱。
- S3 `Signature.Add` 内部就地突变（伪装不可变）潜在数据竞争。

---

*审计方法：全量源码静态推导（7 文件）。未执行任何测试运行（本交付物为写-only 的审计文档；C# 仓库无现成 vitest/test 运行器，且任务明确"只写这个文件，勿改 .cs"，故未新建设施）。所有结论附 file:line 证据。*
