# Round 06 — Rich Hickey 视角命名审计（Hammock 即设计透镜）

- 审计视角：Hammock-driven development——名字是承诺。名字必须揭示本质（what it is），而非暴露实现或许诺不存在的区分。"Simple is not easy"：一个需要读注释才能用对的名字，就是没做设计的名字。
- 范围：只读 7 个核心文件 + README；**未读取 audit/ 目录**（避免锚定前轮结论）。
  - `src/Cosmos.EffectAlgebra/Objects.cs`、`Numeric.cs`、`Algebra.cs`、`DerivedMetrics.cs`、`SignedNet.cs`、`EffectScript.cs`、`EffectScriptContract.cs`、`README.md`
- 判据：用户能否**仅凭名字**推断正确用法与语义边界？
- 严重度：🔴 Blocker / 🟠 Major / 🟡 Minor / ⚪ Info

---

## 符号级发现总表

| # | 符号 / 词簇 | 位置 | 严重度 | 一句话诊断 |
|---|---|---|---|---|
| F1 | `ScopeId.Loop` vs `LoopCount` vs `EffectEvent.Loop` vs `Combination.Loop` | Objects.cs:96; DerivedMetrics.cs:10,37; EffectScript.cs:35 | 🟠 Major | "Loop" 一词四义：作用域标签、迭代计数、并发实例数、组合算子 |
| F2 | `Kind.Occupy` vs `ResourceId.Occupancy` | Objects.cs:111,31 | 🟠 Major | 动词桶名与名词资源构造子仅差 4 个字母，正交轴被词形相似掩盖 |
| F3 | `Union` / `Join` / `Sequence` / `Parallel` | Objects.cs:182,192; DerivedMetrics.cs:50,53 | 🟠 Major | 四个公开名字，一个行为；名字许诺了本层不存在的区分 |
| F4 | `Net` vs `Peak`（含 `NetTable`/`Peak`/`Derived.Net`/`Derived.Peak`） | Algebra.cs:46,111; DerivedMetrics.cs:68–74 | 🟡 Minor | 姊妹度量同名风格却返回不同形状；同一计算有两个入口 |
| F5 | `Budget` vs `Caps` vs 参数 `cap` | EffectScript.cs:61,105,311–320 | 🟡 Minor | budget/cap/Caps 三个同义词在 10 行内轮换；"Budget" 名字超载其语义 |
| F6 | `Violation.Kind : string` 遮蔽 `enum Kind` | EffectScript.cs:349; Objects.cs:111 | 🟡 Minor | 同名异型：核心 API 里两个 `Kind` 一个是枚举一个是魔法字符串 |
| F7 | `Violation.AtT` | EffectScript.cs:340 | ⚪ Info | 神秘 T 后缀泄露实现顾虑，不传达本质 |
| F8 | `Interval.Default` = [1,1] | Numeric.cs:92 | 🟡 Minor | "Default" 掩盖「缺省≠未知」这一 README 明示的语义锐边 |
| F9 | `NatStar/ZStar/DeviationVal` 的 `Top`/`IsTop` | Numeric.cs:11; SignedNet.cs:13 | ⚪ Info | 认识论（未知）与序论（最大元）两种语义压在同一个 ⊤ 上 |
| F10 | 注释词 "fail-closed 最弱兼容" vs README "fail-open" | Algebra.cs:10; Objects.cs:114; README.md:56 | 🟡 Minor | 同一规则两处贴了相反的行话标签，读者无法从名字推断安全姿态 |
| F11 | `Weight.Of(Kind,Kind)` 以 NaN 编码 ⊥ | Algebra.cs:38 | 🟡 Minor | 隐藏编码 + 工厂式命名，部分函数看起来像全函数 |
| F12 | 文件自述 "fail-fast" vs `_ => {"memory":0}` 兜底 | EffectScriptContract.cs:218（对照头部注释） | 🟠 Major | 自我描述撒谎：序列化兜底静默改写数据，直接摧毁名字/注释的可信度 |

---

## 逐符号详析

### F1 🔴🟠 "Loop" 一词四义 — 🟠 Major

四个公开符号共享词根，含义各不相同：

| 符号 | 位置 | 实际本质 |
|---|---|---|
| `ScopeId.Loop(string Id)` | Objects.cs:96 | 循环体的**作用域标签**（偏序域中一个元素） |
| `LoopCount`（类型，ω∈ℕ∪{⊤}） | DerivedMetrics.cs:10 | 循环/副本的**数量** ω |
| `EffectEvent.Loop`（属性，类型 `LoopCount`） | EffectScript.cs:35 | 该事件的**并发实例数** |
| `Combination.Loop(body, ω, loopScope)`（方法） | DerivedMetrics.cs:37 | **循环组合算子** |

调用点 `Combination.Loop(e.Footprint, e.Loop, e.Scope)`（EffectScript.cs:87）里 Loop 出现两次、语义不同，只能靠位置区分。

更糟的是本质错位：`LoopCount` 在剧本语境下文档自己承认它**不是时间重复**而是**并发实例数**——"ω 表示『同一时刻有多少个该元素并发存在』（并发副本，非时间重复）"（EffectScript.cs:33–34）。名字说 count-of-loops（时间迭代次数），语义是 resident-count（瞬时并发数）；`LoopCount.Top` 实际含义是"常驻层"（README MA-002）。读者凭名字会做出错误推断——这正是 Hickey 所说的"名字撒谎比没有名字更糟"。建议：剧本侧改名 `Concurrency`/`Instances`（保留 `LoopCount` 于纯代数侧亦可，但至少属性名应揭示本质）。

### F2 occupy vs Occupancy — 🟠 Major

- `enum Kind { Read, Write, Occupy }`（Objects.cs:111）：claim 的**种类桶**（动词，占用行为）。
- `ResourceId.Occupancy(string Channel)`（Objects.cs:31）：**资源身份**构造子（名词，audio_channel / animation_state 归一目标，Objects.cs Normalize 表）。

两条完全正交的轴（"什么类型的 claim" × "哪种资源"）共用近同词形。用户写出 `Claim(Kind.Occupy, new ResourceId.Occupancy("audio"), …)` 时，两个词几乎同义反复，无法从名字看出它们分属不同维度。JSON 契约进一步放大：kind 序列化为 `"occupy"`，resource 键为 `"occupancy"`（EffectScriptContract.cs ParseKind/ParseResourceKey）。加上 ApiMapping 层的 `Oc`/`Occ` 缩写（ApiMapping.cs:45,52，grep 证据），碰撞面持续扩大。建议：资源构造子改为领域真名 `AudioChannel`/`AnimationState` 或统一为 `Channel`，把 "occupancy" 这个词还给 Kind 桶。

### F3 Union / Join / Sequence / Parallel：四个名字一个行为 — 🟠 Major

- `Signature.Join(a,b) => Union(a,b)`（Objects.cs:192）——字面别名，零增量。
- `Combination.Sequence(a,b) => Signature.Union(a,b)`（DerivedMetrics.cs:50）。
- `Combination.Parallel(a,b) => Signature.Union(a,b)`（DerivedMetrics.cs:53）。

Sequence 与 Parallel 的差异（跨调用点 Compatible 冲突检查）被委托给 L3 Analyzer，在本层**不存在**——但公开 API 的名字许诺了区别存在。用户在 L1 看到 `Parallel` 会预期某种并发语义（如 size 相加而非去重合并），实际是幂等并集：两个 `Parallel(S,S)` 返回一份 S。Join 别名则纯粹增加词汇负担：半格术语（join-semilattice）与集合术语（union）同时挂在同一函数上，读者必须猜是否有细微差别——没有。

Hickey 判语：名字是承诺。要么让 Parallel 在本层就做对并发该做的事（size×2？），要么承认这是同一个 join 并砍掉冗余入口/在名字上标注层界（如 `UnionOnly_ParallelChecksDeferred` 不可取，但至少 XML doc 必须置顶于签名而非埋注释）。现状是注释承载了名字该承载的全部信息——每个使用点都要付一次阅读税。

### F4 Net vs Peak — 🟡 Minor

姊妹度量，形状不对称：
- `Derived.Net(S,scope) → NetTable`（按资源的**区间表**，Algebra.cs:46；DerivedMetrics.cs:74）
- `Derived.Peak(S,scope) → NatStar`（**标量**上界，Algebra.cs:111；DerivedMetrics.cs:71）

名字风格一致（都是单数物理量词），返回类型却一个是可查表一个是有穷标量——用户无法从名字预判 API 形状，只能试编译。另外同一计算有两个入口（`Peak.Compute` 与 `Derived.Peak`；`NetTable.Compute` 与 `Signature.Net`/`Derived.Net`），词汇在 `net`（小写，注释与局部变量）/`Net`/`NetTable` 间漂移。属一致性债，非正确性问题。

### F5 Budget vs Caps vs cap — 🟡 Minor

EffectScript.cs 三处词汇轮换：
- 属性 `public Budget Budget { get; }`（:61）
- 方法签名 `public AuditResult Audit(Budget cap)`（:105）——参数降格为同义词 `cap`
- 方法体内 `cap.Caps[r]`（:226）——budget→cap→Caps 三级转译
- 类型定义 `Budget.Caps`（:311–320）

且 **"Budget" 名字超出其实际语义**：gate(1) 守恒检查完全不消费 Budget；Budget 只承载 gate(2) 的每资源峰值上限（软约束，缺省=无上限）。叫 Budget 让人以为包含净额守恒预算（spend ≤ budget），实际只是 peak caps。诚实的名字是 `PeakCaps`/`Limits`。若未来加入真正的守恒预算，此名必然被迫分裂。

### F6 Violation.Kind : string 遮蔽 enum Kind — 🟡 Minor

- `enum Kind { Read, Write, Occupy }`（Objects.cs:111）——全库核心枚举。
- `Violation.Kind` 却是 `string`，取魔法值 `"Leak" | "NegativeDip" | "PeakExceeded" | "CompatibleConflict"`（EffectScript.cs:349 及各 Violation 构造点 :223,:231,:243,:287）。

同名异型：读过 Claim.Kind（枚举）的用户会对 violation.Kind 做出错误的类型假设；字符串魔法值也无编译期穷举保障（与本库"enum 保证穷举"的自家信条相悖，Objects.cs:110）。建议改名 `ViolationType` 并做成 enum。

### F7 Violation.AtT — ⚪ Info

EffectScript.cs:340。`AtT` 的 T 后缀是内部实现顾虑（避免与 `EffectScript.At` 方法联想混淆？）泄漏到公共契约。本质是"违例时刻"，`Time` 或 `AtTime` 更诚实。信息级。

### F8 Interval.Default = [1,1] — 🟡 Minor

Numeric.cs:92。README 明示锐边："Claim.Size 省略 ≠ 未知：?? [1,1]（精确 1，既非未知 ⊤ 也非 0 预算）"。但载体名字叫 **Default**——通用词汇暗示"安全回退值"，恰恰诱导用户把它当"未指定/无所谓"理解。本质是 `ExactOne`（精确单份）。名字应该携带"这是一个有语义的选择"这一事实。

### F9 Top / IsTop 的双重语义 — ⚪ Info

`NatStar.IsTop`/`ZStar.IsTop`/`DeviationVal.IsTop`（Numeric.cs:11,114; SignedNet.cs:13）三处模式一致（好）。但 ⊤ 同时承担：(a) 认识论的"上界未知"（IsTop 注释原话）与 (b) 序论的最大元（`CompareToFinite` 中 ⊤ 最大，Numeric.cs:60–63；ScopeId.Global 也是"最大元"却不用 Top 词族）。对可为负的 ℤ*，"⊤ 是最大值"是编码选择而非数学事实。一致地用 `Unknown`/`Unbounded` 词系会更诚实。信息级，不要求改。

### F10 "fail-closed 最弱兼容" vs "fail-open" — 🟡 Minor

同一规则（Unknown→Use 放行）在三处贴了相反标签：
- Algebra.cs:10 & Objects.cs:114："Unknown 按 Use 处理（**fail-closed** 最弱兼容，P4）"
- README.md:56："Unknown 模式 = 最弱兼容 = **fail-open**：未知资源冲突被静默放行"

README 的用法是对的（放行=open）。源码注释把"最弱权限"误写成"fail-closed"，方向恰好相反。行话标签错了比没有标签危险——安全审计读者扫到 fail-closed 会误判防护姿态。

### F11 Weight.Of 以 NaN 编码 ⊥ — 🟡 Minor

Algebra.cs:38：`Of(a,b) => a==b ? 1.0 : double.NaN`。工厂式名字 `Of` 看似全函数，实际是偏函数且用 NaN 当哨兵值（NaN 会在下游算术中静默传播污染，正是本库其他地方用类型拼命防住的失败模式——ulong 环绕都兜成 ⊤ 了，这里却放 NaN 出门）。注释已自认"约定为 ⊥ 编码，注释契约"。与全库"类型即边界"的方法论不一致。

### F12 文件自述 "fail-fast" vs memory:0 兜底 — 🟠 Major（越界发现，但关乎命名可信度）

EffectScriptContract.cs 头部声明："非法形状抛 FormatException（**fail-fast，非静默漏报**）"。但：
- `SerializeResource` 默认分支 `_ => new Dictionary<string, object?> { ["memory"] = 0 }`（:218）
- `SerializeBudget`/`ResourceKey` 默认分支 `_ => "memory:0"`

任何未被 switch 覆盖的 ResourceId（Tree/Disk/Signal/Self/Input/Network…，即 Objects.cs:27–41 的大部分构造子）会被**静默改写**为 `memory:0` 后输出——ToJson→Parse round-trip 直接损坏数据且无异常。这与文件自我描述直接矛盾。名字/注释是契约；契约撒谎一次，全部注释的可信度都要打折。（本轮为命名审计，仅记录为高优先残留风险；修复归属行为审计轮次。）

顺带（⚪ Info）：closure 块内 `if (e.Lifetime.Lo.IsTop) continue;` 重复出现两次（EffectScript.cs:272 与 :274），复制粘贴残迹，无害但说明该区域缺乏 hammock 式打磨。

---

## 正面清单（Correct）

- `NatStar`/`ZStar`/`DeviationVal` 的 `IsTop`+`Value` 对：三载体同一模式，名字稳定可迁移。
- `CompareToFinite`（Numeric.cs:58）：诚实命名——明确"只与有限值可比"，规避了比较符重载的假全序。
- `SignedInterval.Add` vs `Merge`（SignedNet.cs:88,92）：求和与包络两个易混操作名字清晰分离，且注释互相引用辨析——本库最好的命名实践样本。
- `ContainsZero`（SignedNet.cs:81）：谓词直陈本质，DO-9 规则内嵌进名字。
- `NodePathOrUnknown`（Objects.cs:68）：类型名即联合体文档。
- `Compatible.IsCompatible` / `Claim.CompatibleWith`（Objects.cs:139）：读起来就是其定义。

## 结论

核心 L1 代数（Numeric/SignedNet/Algebra）的命名质量明显高于剧本层（EffectScript*）。最大的三处债务集中在**一词多义**（F1 Loop 四义）、**许诺不存在的区分**（F3 四名一实）、以及**自我描述与行为相悖**（F12）——三者都会让用户"凭名字用错"。修复优先级：F12 > F1 > F3 > F2 > F6/F10。

## Residual risks

- F12 的 memory:0 兜底是数据损坏路径，需行为轮确认影响面（哪些 ResourceId 构造子实际流入 ToJson）。
- 本轮按任务约束只读了 7+1 文件，ApiMapping.cs / Deviation.cs / EffectAttributes.cs 仅经 grep 取证（Oc/Occ 缩写、fail-closed 标签、OverrideKind 命名未深审）；Runtime 层（Fiber/InverseReplay/NetBenefitClosure 等）命名未覆盖。
