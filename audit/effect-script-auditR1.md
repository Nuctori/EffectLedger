# 独立审计 R1（代数视角 / 视角 A：代数学·范畴论）

## 声明
- 审计轮次：R1，独立对抗性代数审计，立场「宁误报 open，不漏判」。
- **未读** `audit/` 下任何历史文件（`iter-*/effect-script-auditA.md` 等均未读取）。
- **所读源码清单（仅下列，未触碰其他）**：
  - `src/Cosmos.EffectAlgebra/EffectScript.cs`（目标文件）
  - `src/Cosmos.EffectAlgebra/EffectScriptContract.cs`（目标文件）
  - `src/Cosmos.EffectAlgebra/EffectScriptIo.cs`（目标文件）
  - `src/Cosmos.EffectAlgebra/Objects.cs`（支撑 L1）
  - `src/Cosmos.EffectAlgebra/Numeric.cs`（支撑 L1）
  - `src/Cosmos.EffectAlgebra/Algebra.cs`（支撑 L1）
  - `src/Cosmos.EffectAlgebra/SignedNet.cs`（支撑 L1）
  - `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`（支撑 L1）

## 审计方法
枚举 3 目标文件暴露的全部 public/readonly 符号，逐个质问「数学上是否良定义」：是否引用未定义/悬空量、是否依赖注释契约而非类型/代码保证、是否与前一遍定义矛盾、端点采样定理是否真的成立、三道 gate 的「扫换线」实现是否与「逐点全算 + 时间聚合」语义一致。凡非 OK 项均给可运行反例（JSON 或 C# 片段）。

---

## 逐符号表

### A. EffectEvent（EffectScript.cs:18-56）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `EffectEvent` record struct | :18 | 条件 | 4 字段全必填由构造子强制，数学边界由既有类型承载，OK。但 `Loop` 默认构造 `LoopCount.Of(1)`（:55）与 `EffectEvent(..., LoopCount loop)` 的语义「ω 表示同一时刻并发副本数」无类型强制，仅注释契约。 | LOW |
| 字段 `Lifetime: Interval` | :24 | 良定义 | 由 `Interval` 构造子强制 `lo≤hi`、拒绝 `[⊤, finite]`（Numeric.cs）。 | OK |
| 字段 `Scope: ScopeId` | :30 | 良定义 | 为 `At/Net/Peak` 的 scope 来源，修 OPEN-1 后已消除自由变量 `loopScope`。OK。 | OK |
| 字段 `Footprint: Signature` | :35 | 良定义 | 三桶量纲隔离由类型承载。OK。 | OK |
| 字段 `Loop: LoopCount` | :41 | 条件 | `ω∈ℕ∪{⊤}`，类型承载。但 `ω` 的「并发副本」语义与 `At` 用 `Combination.Loop` 缩放的耦合仅注释保证（见 At 项）。 | LOW |
| 构造子 `(lifetime,scope,footprint,loop)` | :48 | 良定义 | 全必填。OK。 | OK |
| 构造子 `(lifetime,scope,footprint)` | :55 | 良定义 | 委托 `:this(..., LoopCount.Of(1))`。OK。 | OK |

### B. EffectScript（EffectScript.cs:61-... / EffectScriptContract.cs:187-201）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `Events: ImmutableArray<EffectEvent>` | :67 | 良定义 | 纯数据。OK。 | OK |
| `.ctor(ImmutableArray)` | :70 | 良定义 | OK。 | OK |
| `.ctor(IEnumerable)` | :74 | 良定义 | `.ToImmutableArray()`。OK。 | OK |
| `At(t): Signature` | :73-81 | **条件（与 Audit 矛盾）** | `At` 用 `Combination.Loop(e.Footprint, e.Loop, e.Scope)`（:79）按 **Event.Scope** 作为 loopScope 重写每个 Claim 的 scope，并缩放 size。**矛盾点**：`Audit` 的 gate(3) 兼容判定用 `grp[key] = (r, c.Scope, (int)c.Mode)`（:166），key 用的是 **Claim 原始 scope `c.Scope`**，不是 `Combination.Loop` 改写后的 `e.Scope`。因此 `At` 的数学语义（scope 被重写为 Event.Scope）与 `Audit` 的 scope 来源（Claim.Scope）**不一致**：当某 Event 的 foot print 内 Claim.scope ≠ Event.scope 时，二者对「同一 (resource,scope) 组」的归类不同。`At` 的 doc 自陈「loopScope=Event.Scope，修 OPEN-1」与 Audit 实现矛盾。 | **MED（HIGH 风险来源）** |
| `Alive(lt,t)` | :283-85 | 良定义 | `lt.Lo.CompareToFinite(t)<=0 && (lt.Hi.IsTop \|\| t.CompareToFinite(lt.Hi)<=0)`。`Lo=⊤` ⇒ 永不存活，与 `At` 一致。OK。 | OK |
| `ScaleSize(s,w)` | :288-93 | 良定义 | `w.IsTop ⇒ [s.Lo,⊤]`；否则端点×ω（:291 `* w`）。与 `Combination.Scale` 同义。OK。 | OK |
| `ToZ/Negate` | :295-96 | 良定义 | 仅 finite→ZStar；⊤→ZStar.Top。OK。 | OK |
| `Budget` 属性（Contract 追加部分） | EffectScriptContract.cs:191 | 良定义 | `init` 属性，默认 `Budget.None`。OK。 | OK |
| `Audit()` 无参 | EffectScriptContract.cs:197 | 良定义 | 委托 `Audit(Budget)`。OK。 | OK |

### C. `Audit(Budget)`（EffectScript.cs:84-280）—— 三道 gate 扫换线实现
| 符号/子逻辑 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| 端点采样集合 `endpoints`/`samplePoints` | :97-117 | **未定义（定理破缺）** | 端点采样定理（doc :90-92）称「At 分段常数、仅有限 Lo/Hi 跳变，故采样全部有限端点=全量」。但 `Alive` 把 `Hi=⊤` 视为常驻尾段，而采样点仅取 `maxFinite+1`（:115）。**反例（HIGH）**：两个事件 `E1=[1,⊤]`、`E2=[2,3]`。在 `t=1` 采样时仅 E1 alive；`t=2` E1+E2；`t=4(=maxFinite=3+1)` E1 仍 alive（`Hi=⊤`）。本想用 `maxFinite+1` 代表「所有有限事件结束后」。但若有**纯常驻**脚本：`E=[0,⊤]`（单一事件，无有限端点），则 `anyFinite=false` ⇒ `maxFinite=0` ⇒ `samplePoints=[0]`（:116），`t=0` 处 Alive = true，OK。**真正破缺**：`maxFinite+1` 仅代表「有限事件结束」，不保证覆盖 `Hi=⊤` 事件在 `[maxFinite,∞)` 内的**潜在新跳变**——但 `Hi=⊤` 事件无更多有限跳变，故该尾段确为常驻。定理在「无 finite 端点」退化到 `t=0`，仍正确。→ 降级为 **MED：定理成立但 `maxFinite` 初值 0 在 `anyFinite=false` 时以 `t=0` 代表点，对 `Lo>0` 的纯常驻脚本漏采**（见下 `Leak/NegativeDip` 在 `t=0` 对 `Lo>0` 常驻事件误报）。 | **MED** |
| gate(1) 累积 net `net[r]=cur.Merge(contrib)` | :152-155 | **未定义（自相矛盾）** | `Step` 在 enter 时累积 net 用 **`Merge`（min/max 包络）**（:155）。但同一文件 `NetTable.Compute`（Algebra.cs:65）用 **`Add`（逐端求和）**，且 `NetTable` 注释明确「Merge 会吞掉守恒判定，漏报泄漏」。同一子系统对同一 `C(t)=Σ...` 语义给出**两种互不相容的实现**：扫换线用 Merge、NetTable 用 Add。**反例（HIGH）**：资源 `R`，事件 A `create size=[4,4]`，事件 B `create size=[4,4]`，两者时间重叠。逐点全算 `C(t)=+8`（应 ≥0，pass）；扫换线 Merge ⇒ `[4,4] Merge [4,4] = [4,4]`（仍为 4，恰好巧合相等）。换**异号**场景：A `create size=[2,2]`，B `release size=[10,10]` 重叠。逐点 Add ⇒ 净 `[-8,8]` 含 0（守恒，pass）；扫换线 Merge ⇒ `create [2,2]` 与 `release [-10,-2]` 的 Merge = `[-10,2]`，其 `Hi=2≥0`（gate(1) 只看 `Hi<0`，碰巧不报）。但改 A `create [5,5]`、B `release [3,3]` 不重叠（B 早于 A）：逐点 Add 按 Lo 时间序正确累加，扫换线同样时间序 enter 累加——此处 Merge 与 Add 因单 Claim 区间退化为点而**数值相同**，掩盖了矛盾。真正的判据失效在：**Multiple 重叠 create 的峰值被 Merge 压成单个区间**，导致 gate(1) 完全丧失了「求和」语义。反例 JSON：`E1=[1,9] create R size[100,100]`，`E2=[2,9] create R size[100,100]`，`E3=[3,9] release R size[150,150]`。Add：-100-100+150=-50 <0（应 NegativeDip）；Merge：create 区间经两次 Merge 仍 `[100,100]`（同号 Merge 不叠加！），release `[-150,-150]` Merge `[100,100]` = `[-150,100]`，Hi=100≥0 ⇒ **漏报 NegativeDip**。 | **HIGH** |
| gate(2) 峰值 `peakSum`/`topCount` | :162-187 | **条件（与 Peak.Compute 一致但语义窄）** | `peakSum` 累加 `size.Hi * ω`（:171），与 `Peak.Compute` 取 `Hi` 求和一致（Algebra.cs:78）。`top` 判定 `size.Hi.IsTop \|\| ω.IsTop`（:169,172）对应 `ω=⊤⇒⊤` 兜底（MA-002）。**但存在整数回卷漏洞**：`(c.Size??Default).Hi.Value * e.Loop.Count.Value` 为裸 `ulong*` 乘法（:171），`ulong` 环绕后 `peakSum` 再 `NatStar.Of`（:171 不触发 NatStar 的 `*`，故环绕不被 ⊤ 保守捕获）。**反例（MED）**：`size.Hi=2`, `ω=10_000_000_000`(0x...)，乘积 ≥2^64 环绕成小值 ⇒ 峰值被低估，可能漏报 `PeakExceeded`。C# `ulong*ulong` 静默环绕，无 `checked`。 | **MED** |
| gate(3) 兼容 `grp` | :164-181,200-220 | **条件** | `key=(r, c.Scope, mode)` 用 **Claim 原始 scope**（:166），与 `At` 的 `Combination.Loop(.., e.Scope)` 重写 scope **不一致**（见 At 项）。`Count>=2` 等价于跨事件同 mode 冲突对，单事件内 ω 份同 EventIdx 不触发（HashSet 存 `ei`）。数学等价于逐点两两枚举。但 `mode==Use` 不计入（:209 仅 Create/Move/Release），与 `Compatible.IsCompatible` 的「Use 与任意兼容」一致。→ 良定义，但 scope 源矛盾使其与 `At` 不同步。 | **MED** |
| 闭包 Leak 块 `closureNet[r]=cur.Merge(contrib)` | :246-260 | **未定义（与 NetTable 矛盾 + 与 gate(1) 同病）** | 闭包 net 复用 **Merge**（:254），而 `NetTable.Compute` 用 Add（Algebra.cs:65），且 `IsConserved` 判定 `ContainsZero`（:258, Algebra.cs:102）。同一 Leak 语义在 `NetTable.IsConserved` 与 `Audit` 闭包块给出**不同结果**：若 Merge 把净区间压成 `Hi≥0`（如上述重叠 create 场景），`ContainsZero` 可能因 `Lo>0` 返回 false ⇒ 误报 `Leak`；或在异号抵消场景 Merge 仍漏报。**矛盾与 gate(1) 同源。** | **HIGH** |
| sweep 相位排序 | :120-135,224-238 | 良定义 | enter@Lo 先于 exit@Hi（同时间），相位 1-4 顺序（:224-238）保证 `t=Lo` 处 enter 后审计、`t=Hi` 处审计后 exit，`Alive` 一致。OK。 | OK |

#### 端点采样定理（EFFECT_SCRIPT.md §3）成立性结论
- **定理字面**（At 分段常数、仅有限端点跳变）：对 `At` 本身成立 —— `At(t)` 确为分段常数（每 Event 的 Lifetime 端点跳变）。OK。
- **审计采样完整性**：定理被 `maxFinite+1` 代表点近似。`Hi=⊤` 事件无有限跳变，尾段常驻，故单点代表成立。**破缺点**：当脚本**无任何有限端点**（`anyFinite=false`）时，`maxFinite` 初值 0、`samplePoints=[0]`（:116）。若常驻事件 `Lo>0`（如 `E=[5,⊤]`），`t=0` 处 `Alive=false` ⇒ 该事件**完全不在任何采样点被审计**（gate1/2/3 在 t=0 不计入，t=maxFinite+1 因 anyFinite=false 未生成）。`Lo>0` 的纯常驻脚本被**静默跳过**，定理对该对象未定义像。| **MED** |

### D. Budget（EffectScript.cs:300-318）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `Budget` record struct | :300 | 良定义 | 载体 `IReadOnlyDictionary<ResourceId,NatStar>`。OK。 | OK |
| `Caps` | :306 | 良定义 | 缺省无上限（⊤ 语义由「不在 Caps 即不检查」承载，:204 注释）。OK。 | OK |
| `.ctor` | :310 | 良定义 | OK。 | OK |
| `None` | :315-17 | 良定义 | `new Budget(new Dictionary<...>())`。OK。 | OK |

### E. AuditResult（EffectScript.cs:321-337）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `AuditResult` | :321 | 良定义 | `Passed`= `violations.Count==0`（:280）。OK。 | OK |
| `Passed`/`Violations`/`.ctor` | :325-336 | 良定义 | OK。 | OK |

### F. Violation（EffectScript.cs:340-365）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `Violation` record struct | :340 | 良定义 | 5 字段全必填。OK。 | OK |
| `AtT/Resource/Scope/Kind/Detail`/`.ctor` | :344-364 | 良定义 | OK。但 `Scope` 在 gate(1)/gate(3) 中均填 `new ScopeId.Global()`（:161, :265），即**守恒/负陷违例不携带真实 scope**（与 `At` 的 Event.Scope 源不一致），AI 回修 JSON 时缺 scope 线索。仅 LOW。 | LOW |

### G. EffectScriptContract（EffectScriptContract.cs:13-...）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `Parse(json)` | :21 | 条件 | 校验根含 `events` 数组（:24）。fail-fast。OK。**但**：解析后 `caps = Budget.None.Caps`（:31）当无 budget 时。**契约格式冲突**：`Parse` 期望 budget 为「对象，键=资源、值=数字」（JSON 形状 `{gpu:rid: N}`），而 `EffectScriptIo.ParseBudget` 期望 `{caps:[{resource,cap}]}` 数组（见下）。**两个契约解析器对同一 `budget` 字段给出互不相容的形状约定** ⇒ 用 Contract.Parse 解析 Io 风格的 budget 会失败/反之。 | **MED** |
| `ToJson(script)` | :43 | 条件 | 序列化 `footprint` 仅取 `e.Footprint.OccupyClaims`（:181），**丢弃 read/write 桶**（footprint 含 read/write claim 时往返丢失）。反例 round-trip：`{"events":[{"lifetime":[0,1],"scope":{"scene":"S"},"footprint":[{"kind":"read","resource":{"memory":1},"mode":"use","scope":{"scene":"S"}}]}]}` ⇒ 序列化后 footprint 为空数组，`ToJson`→`Parse` 往返丢 read claim。 | **MED** |
| `ParseEvent` | :52 | 良定义 | 缺 `loop` ⇒ 默认 1（:57），缺 `scope`/`lifetime`/`footprint` 必抛。OK。 | OK |
| `ParseInterval` | :65 | 良定义 | 仅接受 `[lo,hi]` 数组；`hi="⊤"`。OK。 | OK |
| `ParseTop` | :76 | 良定义 | 数字或 `"⊤"`。OK。 | OK |
| `ParseScope` | :85 | 条件 | 无 `type` 默认 `Scene`（:93）；未知 type 回退 `Scene`（:92）。与 `EffectScriptIo.ParseScope` 的「未知 shape ⇒ 抛」不同（Io 更 strict）。同类契约分叉。 | LOW |
| `ParseLoop` | :96 | 良定义 | OK。 | OK |
| `ParseFootprint`/`ParseClaim` | :103-135 | 良定义 | 逐字段 fail-fast。OK。 | OK |
| `ParseKind`/`ParseMode` | :137-149 | 良定义 | 未知⇒抛。OK。 | OK |
| `ParseResource` | :151 | 条件 | 仅支持 gpu/commandBuffer/memory/occupancy/signalBus 5 种（:152-159）。**不支持** Tree/Self/Physics/Disk/Signal/Callback/Network/Input/Custom/AudioMixer（Objects.cs 定义的其余构造子）。即 Contract 解析器是 Io 解析器的**真子集**；用 Contract 解析含 `"tree"` 的 AI JSON 会抛，而 Io 可解析。两解析器资源覆盖不一致。 | MED |
| `ParseBudget(el)` | :157 | 条件 | 期望 `{key:number}`，`ParseResourceKey` 解析 `gpu:/commandBuffer:/...`（:165-178）。**与 Io 的 `caps:[{resource,cap}]` 形状冲突**（见 G.Parse）。 | MED |
| `ParseResourceKey` | :165 | 条件 | `memory:xxx` 用 `ulong.Parse`（:170）⇒ 非数字 memory key 抛；与 Io `memory⇒Memory(0)` 硬编码 uid=0 不同（Io:124）。**同一 `memory` 资源在两解析器产生不同 ResourceId**（`memory:42` vs `memory:0`）⇒ 归一化键不一致，审计时跨解析器不互通。 | MED |
| 序列化 `SerializeEvent/Scope/Claim/Resource/Budget/ResourceKey` | :180-237 | 条件 | `SerializeResource`/`ResourceKey` 缺 `else` 兜底时回退 `memory:0`（:225,231），与 `ParseResource` 的 5 种键不完备对称（如 `Tree` 类资源序列化回退 `memory:0` 破坏 round-trip）。但 EffectScript 仅含 5 种资源则 OK。LOW。 | LOW |

### H. EffectScriptIo（EffectScriptIo.cs:9-...）
| 符号 | 出处 | 良定义性 | 反例或理由 | 严重度 |
|---|---|---|---|---|
| `Parse(json)` | :13 | 条件 | 校验 `events` 存在且数组（:17-20）。OK。但默认 scope 用 `ScopeId.Scene("Default")`（:64）当缺 scope；与 Contract 必抛缺 scope 不同（分叉）。 | LOW |
| `ParseBudget(json)` | :33 | 条件 | 读 `budget` 节点（:35）。OK。 | OK |
| `ParseBudget(JsonElement)` | :42 | 条件 | 期望 `budget.caps` 为**数组**（:45）`[{resource,cap}]`。**与 Contract 的 `{key:number}` 形状冲突**。用 Io.ParseBudget 解析 Contract 风格 `{"gpu:1":100}` 会因缺 `caps` 数组⇒返回 `Budget.None` 而**静默吞掉预算**（不抛）。反之 Contract 解析 Io 风格 `{"caps":[...]}` 会因 `prop.Value.GetUInt64()` 对对象报错。 | **MED** |
| `ParseEvent` | :60 | 条件 | claim 的 scope 默认用 `Event.scope`（:108，`ParseClaim(c, scope)` 把未显式 scope 的 claim 归到事件 scope），而 Contract 的 `ParseClaim` 要求 claim **必须自带 scope**（Contract:131 `Require(c,"scope")` ⇒ 缺则抛）。**同一「claim 无 scope」在 Contract 抛、在 Io 静默采用事件 scope** ⇒ 两解析器对同一 JSON 产生不同 `Signature`（scope 不同），破坏 `At` 与 Audit 的 scope 一致性讨论前提。 | **MED** |
| `ParseInterval` | :75 | 条件 | 接受**单数字**（`Exact(v)`，:77）或 `[lo,hi]`。**Contract 的 ParseInterval 仅接受数组**（Contract:67-72 数组或抛）。单数字 lifetime 在 Contract 解析抛异常，Io 接受 ⇒ 分叉。 | LOW |
| `ParseBound` | :89 | 良定义 | `"⊤"/"top"` 或数字。OK（比 Contract 多 `top` 同义词，分叉但无害）。 | LOW |
| `ParseClaim` | :104 | 条件 | 用 `Enum.Parse<Kind>`/`Enum.Parse<Mode>`（:107,110）**大小写不敏感、含所有枚举值**（含 `Unknown`/`Use`）。Contract 用白名单 switch 仅接受特定串。**Io 接受 Contract 拒绝的字符串**（如 mode `"unknown"`、`"use"` 大小写），解析结果不同。 | LOW |
| `ParseResource` | :114 | 条件 | 支持 15 种资源（:124-141），超 Contract 5 种。**`memory⇒Memory(0)` 硬编码 uid=0**（:124），与 Contract `memory:xxx` 解析出的 `Memory(uid)` 不归一（见 G）。`Extract` 兼容字符串简写。OK 但覆盖不一致。 | MED |
| `ParseScope` | :142 | 条件 | 支持 8 种 scope（:145-154），含 Contract 无的 shell/loop/conditional/async；未知⇒抛。与 Contract 回退 Scene 不同。 | LOW |
| `Extract` | :157 | 良定义 | 字符串简写兼容。OK。 | OK |

---

## 具体反例（可运行）

### 反例 1（HIGH）— gate(1)/闭包 net 用 Merge 而非 Add，漏报负陷 / 与 NetTable 矛盾
JSON（喂 `EffectScriptContract.Parse` 或 `EffectScriptIo.Parse`）：
```json
{
  "events": [
    {"lifetime":[1,9], "scope":{"scene":"S"}, "footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"scene":"S"},"size":[100,100]}]},
    {"lifetime":[2,9], "scope":{"scene":"S"}, "footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"scene":"S"},"size":[100,100]}]},
    {"lifetime":[3,9], "scope":{"scene":"S"}, "footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"release","scope":{"scene":"S"},"size":[150,150]}]}
  ]
}
```
- 逐点全算（NetTable.Add 语义）：net = +100 +100 −150 = **−50 < 0** ⇒ 应为 `NegativeDip`。
- 扫换线实现：create 区间 `[100,100]` 经两次 `Merge` 仍为 `[100,100]`（同号区间 Merge=自身），release `[-150,-150]` Merge `[100,100]` = `[-150,100]`，`Hi=100≥0` ⇒ **Audit 不报 NegativeDip（漏报）**。
- 同时 `NetTable.IsConserved`（Algebra.cs:65 用 Add）会对同一脚本判 **不守恒**（−50 不含 0）。⇒ 两子系统对同一数学量给出相反判定。
- 证据：EffectScript.cs:155（`net[r]=...cur.Merge(contrib)`）vs Algebra.cs:65（`t._net[r]=...t._net[r].Add(signed)`）。

### 反例 2（MED）— 纯常驻脚本（Lo>0）被采样点遗漏
JSON：
```json
{ "events": [ {"lifetime":[5,"⊤"], "scope":{"scene":"S"}, "footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"scene":"S"},"size":[100,100]}]} ] }
```
- `anyFinite=false`（Hi=⊤ 无有限端点），`maxFinite=0`，`samplePoints=[0]`（EffectScript.cs:116-117）。
- `t=0`：`Alive([5,⊤],0)` = `Lo≤0?` 否 ⇒ 不存活。该常驻事件**不在任何采样点被审计**，gate(1)/(2)/(3) 全部跳过。定理对「Lo>0 纯常驻」对象未定义像。若它本应触发 `PeakExceeded` 也漏检。

### 反例 3（MED）— ToJson 往返丢失 read/write claim
C#：
```csharp
var s = new EffectScript(ImmutableArray.Create(
    new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(1)), new ScopeId.Scene("S"),
        Signature.Of(new Claim(Kind.Read, new ResourceId.Memory(1), Mode.Use, new ScopeId.Scene("S"), Interval.Default)))));
var json = EffectScriptContract.ToJson(s);
var back = EffectScriptContract.Parse(json);
// back.Events[0].Footprint.ReadClaims.Count == 0  ← 丢失（ToJson 只序列化 OccupyClaims）
```
证据：EffectScriptContract.cs:181（`e.Footprint.OccupyClaims.Select(...)`）。

### 反例 4（MED）— Contract 与 Io 对 `budget`/`memory`/`claim.scope` 形状冲突
- Contract 期望 budget 为 `{gpu:1: 100}`（EffectScriptContract.cs:157-160）；Io 期望 `{"caps":[{"resource":{...},"cap":100}]}`（EffectScriptIo.cs:45-58）。互不可解析。
- Contract `memory:42` ⇒ `Memory(42)`（EffectScriptContract.cs:170）；Io `memory` ⇒ `Memory(0)`（EffectScriptIo.cs:124）。同资源不同键 ⇒ 审计不互通。
- Contract claim 缺 scope ⇒ 抛（EffectScriptContract.cs:131）；Io 缺 scope ⇒ 静默用 Event.scope（EffectScriptIo.cs:108）。同 JSON 异结果。

### 反例 5（LOW）— At 与 Audit 的 scope 源矛盾
当某 Event 的 footprint 内 Claim.scope ≠ Event.scope：
```json
{"lifetime":[0,1],"scope":{"scene":"S"},
 "footprint":[{"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"scene":"OTHER"},"size":[1,1]},
              {"kind":"occupy","resource":{"memory":1},"mode":"create","scope":{"scene":"OTHER"},"size":[1,1]}]}
```
- `At(t)`（EffectScript.cs:79）经 `Combination.Loop(.., e.Scope="S")` 把两 claim 的 scope **改写为 S**，于是 `At` 在该 (memory,S) 组计 2 份。
- `Audit` gate(3)（EffectScript.cs:166）按 **Claim 原始 scope "OTHER"** 分组 ⇒ 在 (memory,OTHER) 报 `CompatibleConflict`（create×create 冲突）。
- `At` 与 `Audit` 对「同一时刻屏幕总签名 / 冲突组」的数学归类不一致 —— doc 自陈「修 OPEN-1：scope 来源 = Event.Scope」，但 Audit 实现未贯彻。

### 反例 6（MED）— peakSum 裸 ulong 乘法回卷
C#：
```csharp
// EffectScript.cs:171 (sweep Step) 等价于：
ulong hi = 2_000_000_000UL;
ulong w  = 10UL;
ulong prod = hi * w; // 静默环绕 < 2^64，不再触发 NatStar 的 * 保守 ⊤
// peakSum[r] = NatStar.Of(prod)  ⇒ 峰值被低估，可能漏报 PeakExceeded
```
无 `checked`，`NatStar.Of`（Numeric.cs:30）不检测回卷；与 `NatStar.*` 运算符的保守 ⊤ 行为（Numeric.cs:43-55）不一致。

---

## 端点采样定理（§3）总评
- `At(t)` 分段常数、仅有限端点跳变：**成立**（与 `Alive` 一致）。
- 审计采样「全部有限端点 + maxFinite+1 代表常驻尾段」：对含有限端点的脚本成立；对 `anyFinite=false` 退化为单点 `t=0`，**对 `Lo>0` 常驻脚本漏采**（反例 2）→ 定理对「纯常驻且 Lo>0」对象**未定义像**，严重度 MED。
- `maxFinite+1` 等价于「全量」只在「所有 `Hi=⊤` 事件确实常驻、无更多跳变」时成立，成立。

## 守恒/峰值/兼容三道 gate 与「逐点全算+时间聚合」一致性
- gate(2) 峰值、gate(3) 兼容：扫换线与逐点一致（数学等价成立）。
- gate(1) 守恒（含闭包 Leak）：**扫换线用 `Merge`（EffectScript.cs:155,254），逐点 `NetTable` 用 `Add`（Algebra.cs:65），二者数学语义互不相容**；对同一脚本可给出相反判定（反例 1）。这是**已证实的内部矛盾（HIGH）**，非疑似。

---

## 总评：本子系统是否「每符号良性定义」
- **否**。绝大多数 leaf 符号（record 字段、构造子、`At`/`Alive`/`ScaleSize`、`Budget`/`AuditResult`/`Violation`、`Peak` 解析）良定义。
- **关键阻塞（HIGH）**：
  1. **gate(1) 守恒 net 聚合算子矛盾**：`Audit` 用 `Merge`、L1 `NetTable` 用 `Add`，同一 `C(t)=Σ...` 语义两实现，导致 `Audit` 漏报负陷/与 `IsConserved` 结果相反（反例 1，证据 EffectScript.cs:155 vs Algebra.cs:65）。
  2. **闭包 Leak 块复用 Merge**（EffectScript.cs:254），同上矛盾，Leak 判定与 `NetTable.IsConserved` 不一致。
- **MED 阻塞/不完整**：
  - 端点采样对 `Lo>0` 纯常驻脚本漏采（:116-117，反例 2）。
  - `At` 的 scope 源（Event.Scope，:79）与 `Audit` gate(3) 的 scope 源（Claim.Scope，:166）矛盾（反例 5）。
  - Contract 与 Io 解析器对 `budget` 形状、`memory` 键、`claim.scope` 缺省、`read/write` 往返、`resource/scope` 枚举覆盖**互不相容**（反例 3、4）→ 同一 AI JSON 经两解析器产出不同 L1 对象，破坏「解析即良定义搬运」前提。
  - `peakSum` 裸 `ulong*` 回卷（:171，反例 6）。
- **LOW**：`EffectEvent.Loop` 语义仅靠注释；`Violation.Scope` 在 gate(1)/闭包填 Global 丢真实 scope；`ParseScope` 默认/回退策略分叉。

## 立场声明
宁误报 open：上述 HIGH/MED 均按「定义自相矛盾 / 未定义像 / 解析互不相容」开出，未因「恰好某些输入数值重合」而降级为 OK。Merge vs Add 矛盾为**代码级已证实违反**（两处源码直接对照），非注释推测。

## Acceptance Contract（独立审计交付）
```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "仅写入 audit/effect-script-auditR1.md，未改动任何 .cs 源码；覆盖 3 目标文件全部 public/read-only 符号 + 支撑 L1"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "逐符号表含 文件:行 证据；HIGH 矛盾给出代码级对照（EffectScript.cs:155 Merge vs Algebra.cs:65 Add）；6 个可运行反例（JSON/C#）附出处"
    }
  ],
  "changedFiles": [
    "audit/effect-script-auditR1.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "grep 锚点核对（仅读取源码，未执行构建）",
      "result": "passed",
      "summary": "在 effect-script/contract/io 及 algebra/objects 中定位 Merge/Add/采样点/budget 解析等行号证据"
    }
  ],
  "validationOutput": [
    "未在 audit/ 读历史；仅审列明 8 个源文件",
    "HIGH: gate(1)守恒与闭包用 Merge（EffectScript.cs:155,254）与 NetTable 用 Add（Algebra.cs:65）矛盾，反例1证明漏报 NegativeDip",
    "MED: 端点采样对 Lo>0 纯常驻漏采（:116-117）；At scope 源(:79)与 Audit gate3 scope 源(:166)矛盾；Contract/Io 解析器预算/ memory/claim.scope 互不相容；peakSum ulong 回卷(:171)",
    "OK: 多数 leaf 符号(readonly record 字段/构造子/Alive/ScaleSize/Budget/AuditResult/Violation/Peak)良性定义"
  ],
  "residualRisks": [
    "未运行 C# 构建/单测验证反例1的数值（仅静态推导 Merge 对同号区间=自身，故漏报）；建议后续以真实 EffectScript.Audit 跑反例1 JSON 复核",
    "EFFECT_SCRIPT.md §3 定理原文未在本次读取范围（仅依据源码 docstring 复述定理），必要时交叉核对"
  ],
  "noStagedFiles": true,
  "diffSummary": "新建独立代数审计 R1 报告，逐符号良定义性表 + 6 反例 + 总评；无源码改动",
  "reviewFindings": [
    "HIGH blocker: EffectScript.cs:155,254 — gate(1)守恒与闭包 Leak 用 SignedInterval.Merge，而 L1 NetTable.Compute (Algebra.cs:65) 用 Add；同一 Σ 语义两实现，导致 Audit 漏报 NegativeDip 且与 IsConserved 相反（反例1）",
    "MED: EffectScript.cs:116-117 — 纯常驻(Lo>0,无有限端点)脚本采样点退化为 t=0，事件不被审计",
    "MED: EffectScript.cs:79(At,scope=Event.Scope) vs :166(Audit gate3,scope=Claim.Scope) 矛盾",
    "MED: EffectScriptContract.cs:157-160(预算 {key:num}/memory:uid) 与 EffectScriptIo.cs:42-58,124(预算 caps[]/memory=>Memory(0)) 互不相容",
    "MED: EffectScriptContract.cs:181 ToJson 仅序列化 OccupyClaims，丢 read/write",
    "MED: EffectScript.cs:171 peakSum 裸 ulong* 回卷未被 NatStar 保守 ⊤ 捕获"
  ],
  "manualNotes": "本审计为只读 R1；所有 HIGH/MED 均开出 open。Merge-vs-Add 矛盾为源码级已证实，建议优先修复 Audit 的 gate(1)/闭包改用 Add 求和（与 NetTable 对齐），并统一两个 JSON 解析器的预算/资源/claim.scope 约定。"
}
```
