# 独立审计 R3 —— 工程正确性视角（Jeff Dean 式务实 / 代码正确性）

- **审计角色**：视角 C（对抗性代码正确性 / fail-closed 审查）。
- **未读历史**：严格遵守「不读 `audit/` 下任何历史文件」。本报告仅基于下方源码独立推导。
- **所读源码（9 文件，全部来自指定清单）**：
  - 目标：`src/Cosmos.EffectAlgebra/EffectScript.cs`、`EffectScriptContract.cs`、`EffectScriptIo.cs`
  - 支撑 L1：`Objects.cs`、`Numeric.cs`、`Algebra.cs`、`SignedNet.cs`、`DerivedMetrics.cs`
  - （搜索确认：在 `src/` 内，`EffectScriptContract` / `EffectScriptIo` 均**无任何调用方**——两个解析器目前都是孤儿代码。）

---

## 0. 结论先行（3 行）

本子系统**没有「每符号可被良性定义并正确运行」**——`Audit` 的守恒/泄漏核心用的是 **`Merge`（包络）而非 `Add`（带符号求和）**，导致 `NegativeDip` 与 `Leak` 两类 fail-open 静默漏报；`EffectScriptIo` 把 `memory` 的 uid **无条件清零**，`EffectScriptContract` 与 `EffectScriptIo` 的 JSON schema 互相不兼容且都声称接 L1。这些不是风格问题，是会让「没跑游戏就审计」这一核心卖点失效的真实 bug。

---

## 1. 逐符号审计表

符号 | 行为是否与声称一致 | 状态 | 最小反例 | 严重度
---|---|---|---|---
`EffectEvent.Lifetime` | 是（纯数据） | OK | — | OK
`EffectEvent.Scope` | 数据 OK，但成为 scope 不一致的根源（见 JD-2） | OK(数据)/缺陷(语义) | — | 见 JD-2
`EffectEvent.Footprint` | 是 | OK | — | OK
`EffectEvent.Loop` | 是（ω∈ℕ∪{⊤}） | OK | — | OK
`EffectEvent(lifetime,scope,footprint,loop)` | 是 | OK | — | OK
`EffectEvent(lifetime,scope,footprint)`（默认 ω=1） | 是 | OK | — | OK
`EffectScript.Events` | 是 | OK | — | OK
`EffectScript(ImmutableArray)` | 是 | OK | — | OK
`EffectScript(IEnumerable)` | 是 | OK | — | OK
`EffectScript.At(t)` | 内容确定、与声称一致；但 scope 被 `Combination.Loop` 改写为 `Event.Scope`（见 JD-2） | OK(自身)/缺陷(与 Audit 不一致) | — | 见 JD-2
`EffectScript.Audit(Budget)` | **否**：gate(1) 与闭包用 `Merge` 而非 `Add`（JD-1，HIGH）；gate(3) 用 claim scope 与 At 矛盾（JD-2）；居民豁免按 ω=⊤ 而非 Hi=⊤（JD-3）；闭包纳入未来事件（JD-4）；隐藏 O(E²)（JD-5） | 缺陷 | 见 JD-1/2/3/4/5 | HIGH/MED
`EffectScript.Budget` | 数据 OK；但仅 `Contract` 填充，`Io` 不填 | OK/缺口 | — | 见 JD-7
`EffectScript.Audit()`（无参） | 行为正确，但依赖 `Budget` 已被填充；用 `Io.Parse` 时恒为 `None` ⇒ gate(2) 空转 | 缺口 | — | MED
`Budget.Caps` | 是 | OK | — | OK
`Budget( caps )` | 是 | OK | — | OK
`Budget.None` | 是 | OK | — | OK
`AuditResult.Passed/Violations/ctor` | 是（纯数据） | OK | — | OK
`Violation.*` / `ctor` | 是（纯数据）；但 `Scope` 字段在 JD-2 下会指向错误作用域 | OK/误导 | — | 见 JD-2
`EffectScriptContract.Parse` | 部分：global scope 不可达（JD-6）、budget 值不可为 ⊤ | 缺陷 | 见 JD-6 | MED
`EffectScriptContract.ToJson` | round-trip 基本可用；与 Io schema 不互通 | OK/缺口 | — | LOW
`EffectScriptIo.Parse` | **否**：`memory` uid 清零（JD-8）；resource 形状与 Contract 不兼容（JD-9） | 缺陷 | 见 JD-8/9 | HIGH/MED
`EffectScriptIo.ParseBudget(string)` | 返回独立 `Budget`，**未挂到 `EffectScript`** | 缺口 | — | MED
`EffectScriptIo.ParseBudget(JsonElement)` | 同上；caps 数组形状与 Contract 的扁平键不兼容 | 缺口 | — | MED

---

## 2. 详细发现（含最小可复现反例）

### JD-1 【HIGH】`Audit` 守恒/泄漏用 `Merge`（包络）而非 `Add`（带符号求和）—— 静默漏报

**依据**：
- gate(1) 累积 net：`EffectScript.cs:155` `net[r] = net.TryGetValue(r, out var cur) ? cur.Merge(contrib) : contrib;`
- 闭包泄漏：`EffectScript.cs:254` `closureNet[r] = closureNet.TryGetValue(r, out var cur) ? cur.Merge(contrib) : contrib;`
- 对照 L1 的正确语义：`NetTable.Compute`（`Algebra.cs:46`）明确用 **`t._net[r].Add(signed)`**（真·求和）。`Audit` 头部注释也自称「累积 net（仅 enter 累加）」「与 CumulativeNet 等价」，却用了 **`Merge`**（`SignedNet.cs` 的 `Merge` = `new(Lo.Min(o.Lo), Hi.Max(o.Hi))`，是 min/max 包络，**不是求和**）。

`Merge` 把多份带符号贡献合并成「[最小下界, 最大上界]」的包络，使 create/release 的正负号**永远无法相消**。于是「净效应含 0 ⇒ 闭合」与「Hi<0 ⇒ 负陷」两个判定都建立在错误的区间上。

**反例 A — `Leak` 漏报（真实泄漏被吞）**（JSON 经 `EffectScriptContract.Parse` 或手工构造 `EffectScript`）：
```
事件1 [0,5]   occupy create  size[10,10]  ω=1  资源 Gpu("buf")
事件2 [6,10]  occupy release size[5,5]    ω=1  资源 Gpu("buf")
```
- 期望：净效应 +10−5 = +5，区间 [5,5] 不含 0 ⇒ 报 `Leak`（真泄漏：只释放了 5/10）。
- 实际：`Merge([10,10],[-5,-5]) = [-5,10]`，`ContainsZero` 为真 ⇒ **不报 Leak**。❌ 静默漏报。

**反例 B — `NegativeDip` 漏报（过度释放被吞）**：
```
事件1 [0,5]   occupy create  size[3,3]   ω=1  资源 Gpu("buf")
事件2 [2,4]   occupy release size[10,10] ω=1  资源 Gpu("buf")
```
- 期望：t=3 时累积净 = +3−10 = −7，区间 [−7,−7]，Hi<0 ⇒ 报 `NegativeDip`。
- 实际：gate(1) `Merge([3,3],[-10,-10]) = [-10,3]`，Hi=3 不 <0 ⇒ **不报 NegativeDip**。❌ 静默漏报（over-release）。

**修复**：`EffectScript.cs:155` 与 `:254` 的 `Merge` 改为 `Add`（与 `NetTable.Compute` 一致）。这是本子系统最高优先级阻塞项。

---

### JD-2 【HIGH】`At` 与 `Audit` 对同一脚本给出矛盾的 scope 视图（`Combination.Loop` 改写 vs gate(3) 用 claim 原 scope）

**依据**：
- `At`：`EffectScript.cs:79` `acc = Signature.Union(acc, Combination.Loop(e.Footprint, e.Loop, e.Scope));` —— `Combination.Loop`（`DerivedMetrics.cs:48-66`）对每个 Claim 执行 `c with { Scope = loopScope }`，即**把所有 Claim 的 scope 改写为 `Event.Scope`**。
- `Audit` gate(3)：`EffectScript.cs:162` `var key = (r, c.Scope, (int)c.Mode);` —— 用的是 **Claim 原始 scope**（`c.Scope`），**未经过 `Loop` 改写**。
- 注释自相矛盾：头部声称「Event.Scope 作为 At/Net/**Peak** 的 scope 来源（修 OPEN-1）」，但 gate(3)（兼容性检查，属审计语义）并未采用同一来源。

后果：`At(t)`（文档宣称的「瞬时可见签名」，AI 主要据此理解冲突）把所有 Claim 归到 `Event.Scope`；而 `Audit` 的 `CompatibleConflict` 把同一批 Claim 归到各自的 claim scope，并把 `Violation.Scope` 写成 claim scope。**同一脚本，两个视图的 scope 归属直接矛盾**，且 AI 拿到的反例 `Scope` 字段指向一个 `At` 签名里根本看不到冲突的作用域 ⇒ 回修方向错误。

**系统性触发**：`EffectScriptContract.ParseClaim`（`EffectScriptContract.cs:117-121`）**强制每个 claim 带自己的 `scope`**（`Require(c,"scope")`）。因此在 Contract 输入下，claim scope 与 event scope **几乎总是不一致**（除非巧合相等），JD-2 对 Contract 输入是**系统性**的，而非偶发。

**最小反例**（Contract 形状 JSON）：
```json
{
  "events": [
    { "lifetime":[0,10], "scope":{"scene":"B"}, "loop":1,
      "footprint":[ { "kind":"occupy","resource":{"gpu":"buf"},"mode":"create","scope":{"scene":"A"},"size":[5,5] } ] },
    { "lifetime":[3,8], "scope":{"scene":"B"}, "loop":1,
      "footprint":[ { "kind":"occupy","resource":{"gpu":"buf"},"mode":"create","scope":{"scene":"A"},"size":[5,5] } ] }
  ]
}
```
- 期望（一致性）：`At(t)` 与 `Audit` 对「冲突发生在哪个 scope」给出同一结论。
- 实际：`At(t)` 的签名把两条 create 归到 `Scene("B")`；`Audit` 报 `CompatibleConflict` 且 `Violation.Scope = Scene("A")`。两视图矛盾，反例误导。❌

**修复**：`Audit` gate(3) 的分组 key 应使用与 `At` 一致的 scope 来源（即对 footprint 先做 `Combination.Loop(...,e.Scope)` 再取 scope），或在文档/契约层明确「claim scope 必须 == event scope」并让两个解析器都强制（Io 已是这样：见 JD-9 旁注，Io 的 `ParseClaim` 用事件 scope 覆盖 claim scope ⇒ Io 输入不触发此矛盾；Contract 触发）。

---

### JD-3 【MED】居民层豁免按 `ω=⊤` 而非 `Hi=⊤` 键控 —— 永久资源（开放上界、有限 ω）被误报 `Leak`

**依据**：
- 闭包豁免：`EffectScript.cs:233` `if (e.Loop.Count.IsTop) continue;` 与 gate(1) enter 豁免 `EffectScript.cs:131` `if (enter && !e.Loop.Count.IsTop)`。
- 文档声称「居民层豁免：资源全部正向贡献仅来自 ω=⊤ 事件」。

但「常驻/居民」在数学上是 **`Hi=⊤`（开放上界）**，未必 `ω=⊤`。一个 `ω=1`、`Hi=⊤` 的永久 buffer（永不释放、设计如此）会被纳入闭包 net ⇒ `Leak` 误报。

**最小反例**：
```
事件 [0,⊤]  occupy create size[5,5] ω=1  资源 Gpu("buf")   （合法永久资源，无 release）
```
- 期望：居民层，不报泄漏。
- 实际：`maxFinite=0`，闭包 net = [5,5]，`ContainsZero=false` ⇒ 报 `Leak`。❌ 误报（false-positive）。

**修复**：居民豁免应按「`Hi=⊤` 且从未 release」判定，或至少对 `Hi=⊤` 的有限 ω 事件也豁免闭包检查（其释放天然不发生，等价永久）。

---

### JD-4 【MED】闭包 `Leak` 把「未来才开始（Lo>maxFinite）」的事件也计入，逻辑越界

**依据**：`EffectScript.cs:243-247` 闭包循环遍历**全部**事件，仅跳过 `ω=⊤` 与 `Lo=⊤`，未要求 `e.Lifetime.Lo ≤ closureT`。`closureT = maxFinite`（`EffectScript.cs:120`）。

后果：一个 `Lo=100 > maxFinite=50`、且 `Hi=⊤`/无 release 的「未来常驻」事件会被算进「t=maxFinite 之后」的净效应。当前因 JD-1 的 `Merge` 包络把它和既有 [0,0] 合并成含 0 区间而**暂时掩盖**；一旦 JD-1 修成 `Add`，此越界就会表现为**对尚未开始的居民误报 `Leak`**。属于当前被 JD-1 掩盖、修复后会暴露的隐患。

**修复**：闭包只应包含 `e.Lifetime.Lo.CompareToFinite(closureT) <= 0` 的事件（已开始者）。

---

### JD-5 【MED】隐藏 O(E²·K)：`AuditAtSample` 在每个采样点全表扫描 `net`/`grp`/`caps`

**依据**：采样点数量 = 不同有限端点数 ≤ 2E（`EffectScript.cs:101-119`）；`AuditAtSample`（`EffectScript.cs:190-217`）在**每个**采样点遍历 `net`（规模 ≤ E·K，所有被占用的资源）、`cap.Caps`（C）、`grp`（规模 ≤ E·K，所有 (res,scope,mode) 单元）。主循环 `EffectScript.cs:219-237` 对每个采样点调用一次。

- 文件头注释声称复杂度 **`O(E·K·log E)`**（仅排序贡献 log），但实测为 **`O(S · (E·K + E·K + C)) = O(E²·K)`**（log 只在排序）。
- 对「数千事件同屏同资源」的 AI 视觉脚本，E²·K 是真实平方恶化（与 iter-effect26 想消灭的正是同一类问题）。这不是 fail-open，但是性能契约与实际不符，且会拖垮大脚本的尾延迟/内存。

**修复**：把 gate 检查做成「增量事件驱动」——负陷/泄漏在 `Step` 增量维护（net 已在增量维护，只是判定挪到采样点），冲突在 `Step` 进入/退出时按 cell 增量判定，避免每个采样点全表扫。

---

### JD-6 【MED】`EffectScriptContract` 无法解析 `global` scope（强制要求 `scene` 字段）

**依据**：`EffectScriptContract.cs:88-89`：
```csharp
if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("scene", out var sc))
    throw new FormatException("scope 须为 {\"scene\":\"Name\"} 等");
```
`ScopeId.Global` 是合法作用域，但惯用写法 `{"type":"global"}` **不含 `scene`** ⇒ 直接抛 `FormatException`。只有 `{ "type":"global", "scene":"x" }` 这种别扭写法才过。与 `EffectScriptIo`（`EffectScriptIo.cs:154` 直接识别 `global`）不一致，且违反「global 为最大元、无需 name」的 L1 定义（`Objects.cs` `ScopeId.Global`）。

**修复**：`scene` 仅对 scene/method/type 必需；`global`/`shell` 等无需 `scene` 即可解析。

---

### JD-7 【MED】`EffectScriptIo` 的预算与剧本**脱钩**：`Parse` 不挂 `Budget`，`ParseBudget` 返回独立对象

**依据**：
- `EffectScriptIo.Parse`：`EffectScriptIo.cs:74` `return new EffectScript(events.ToImmutableArray());` —— `Budget` 默认 `None`。
- `EffectScriptIo.ParseBudget`（`EffectScriptIo.cs:39-58`）返回一个**独立** `Budget`，没有任何代码把它挂回 `EffectScript`。
- 对比 `EffectScriptContract.Parse`：`EffectScriptContract.cs:36` `new EffectScript(...){ Budget = new Budget(caps) }`，并通过 `EffectScript` 的 partial 扩展 `Audit()`（`EffectScriptContract.cs:239-242`）消费。

后果：用 `Io` 管线时，若调用方「只记得 `Parse` 忘了再 `ParseBudget` 并手传 `Audit(budget)`」，**gate(2) 峰值预算检查整段空转**——常驻资源并发超限被静默放过（fail-open）。而且 `Io` 的 budget schema 是 `{"caps":[{resource,cap}]}` 数组（`EffectScriptIo.cs:42`），与 `Contract` 的扁平键 `{"gpu:x":n}`（`EffectScriptContract.cs:156-176`）**完全不兼容**，二者不能混用。

---

### JD-8 【HIGH】`EffectScriptIo` 把 `memory` 资源的 uid **无条件清零** ⇒ 不同显存块被当成同一资源

**依据**：`EffectScriptIo.cs:124` `if (name == "memory") return new ResourceId.Memory(0);` —— 无论 JSON 里 `"memory": 42` 还是 `"memory": 7`，都归一成 `Memory(0)`。

后果：
- 两个不同的显存缓冲（uid 5 与 uid 7）在审计里**完全等价**，冲突/泄漏/峰值检查会把它们错误合并。
- 与 `EffectScriptContract` 不一致：`EffectScriptContract.cs:148` `new ResourceId.Memory(mem.ValueKind == JsonValueKind.Number ? mem.GetUInt64() : 0)` —— Contract **保留** uid。
- 若用 Io 解析事件、用 Contract 形状写 budget（`"memory:5"`），则 budget key=`Memory(5)` 与 footprint 的 `Memory(0)` **永远不匹配** ⇒ 该资源的 `PeakExceeded` 永不被检查（另一处 fail-open）。

**最小反例**：
```json
{ "events":[
  { "lifetime":[0,5],"scope":{"scene":"S"},"footprint":[
     {"kind":"occupy","resource":{"memory":5},"mode":"create","scope":{"scene":"S"},"size":[10,10]} ]},
  { "lifetime":[0,5],"scope":{"scene":"S"},"footprint":[
     {"kind":"occupy","resource":{"memory":7},"mode":"create","scope":{"scene":"S"},"size":[10,10]} ]}
]}
```
- 期望：5 与 7 是两块独立显存，分别审计。
- 实际：二者都变成 `Memory(0)` ⇒ 被当作同一资源，峰值翻倍、且无法区分 ⇒ 错误合并（且若配 Contract 风格 budget 则峰值检查失效）。❌

**修复**：`memory` 应保留 uid（如 `Extract(v,"uid",...)` 或读数值），与 Contract 对齐。

---

### JD-9 【MED】两解析器 `resource` / `budget` / `scope` JSON 形状互相不兼容（第二产品 / schema 分歧）

| 维度 | `EffectScriptContract` | `EffectScriptIo` | 冲突 |
|---|---|---|---|
| `gpu` 资源 | `{"gpu":"buf"}`（字符串即 bufferId）`EffectScriptContract.cs:146` | `{"gpu":{"bufferId":"buf"}}`（嵌套）`EffectScriptIo.cs:122` | 形状互斥，Cross-parse 失败 |
| `memory` | `{"memory":42}` → 保留 uid | `{"memory":42}` → 清零 uid | 见 JD-8 |
| `signalBus` | `{"signalBus":"name"}` | `{"signalBus":{"name":"x"}}` 或 `{"signal_bus":{...}}` | 形状互斥 |
| `audioMixer` | **不支持**（白名单无此项 ⇒ 抛异常）`EffectScriptContract.cs:142-145` | 支持 → `AudioMixer(0)` | Contract 拒绝、Io 接受 |
| `scope` | 强制 `scene`；`global` 不可达（JD-6） | 各分支用 `Extract` 带 fallback 默认名 | 形状/默认不同 |
| `budget` | 扁平键对象 `{"gpu:x":n}` | `{"caps":[{resource,cap}]}` 数组 | 完全不兼容 |
| 是否挂 `Budget` | 是（`Parse` 内挂） | 否（脱钩，JD-7） | 集成缺口 |

**判断（YAGNI / 第二产品）**：两个解析器服务同一个 `EffectScript`，schema 彼此不兼容、且 `src/` 内**均无调用方**（孤儿代码）。这是典型的「第二产品」——应砍掉一个，只保留一个 canonical 契约（建议保留 `EffectScriptContract`：它挂 budget、与 `EffectScript.Audit()` 配对；但需先修 JD-6、JD-2 语义）。否则 AI 产出的 JSON 取决于「碰巧用哪个解析器」，审计结果不可复现。

---

### JD-10 【LOW】`Audit` gate(2) 峰值回退 `sub > cur ? 0 : cur - sub` 在正常流不触发，但触发即 fail-open

**依据**：`EffectScript.cs:185`
```csharp
peakSum[r] = NatStar.Of(sub > cur.Value ? 0UL : cur.Value - sub);
```
正常扫换线 enter/exit 1:1 配对，该分支不触发。但若因任何异常（如同一事件被退出两次、或未来修复引入的边界）导致 `sub>cur`，峰值会被**重置为 0** 而非保留负值 ⇒ 可能**漏报** `PeakExceeded`（fail-open）。属防御性代码但方向错误（应保留真实差值或 fail-closed 置 ⊤）。严重度 LOW（当前不可达）。

---

### JD-11 【LOW】`EffectScriptContract` budget 值不可为 `⊤`；`ParseBudget` 对非数值值抛非 `FormatException`

**依据**：`EffectScriptContract.cs:159-160` `dict[r] = NatStar.Of(prop.Value.GetUInt64());` —— 若 budget 值写 `"⊤"` 字符串，`GetUInt64()` 抛 `InvalidOperationException` 而非统一的 `FormatException`，且无法表达「该资源无上限」（虽 `Budget.None` 已覆盖，但单资源无限 cap 无法表达）。与 Io 的 `capEl` 支持 `⊤`（`EffectScriptIo.cs:53`）不一致。LOW。

---

## 3. 关于任务清单中其他设问的判定

- **扫换线 enter/exit 时序**：在采样点 `tv` 处，phase1 处理 `<tv`、phase2 处理 `==tv` 的 enter、phase4 处理 `==tv` 的 exit（`EffectScript.cs:221-236`），故 `t=Hi` 时事件仍存活（与 `Alive` 一致）、`t` 恰为事件起点时正确进入。**时序本身正确**。
- **峰值 `peakSum` 回退是否掩盖负计数**：见 JD-10，正常流不触发，不掩盖；仅异常路径 fail-open。
- **同资源多事件重叠时峰值是否等于瞬时并发和**：在 `Merge` 之外的部分**正确**——enter 累加 `Hi*ω`、exit 减同值（`EffectScript.cs:171,185`），采样点读取即瞬时并发和（含 ω 缩放）。✅ 但 `topCount`（⊤ 声明）与 `peakSum`（有限）分流正确。
- **gate(3) 是否误报单事件 ω 份并发**：**不会**。ω 在 `At`/`Audit` 里都是**把 size 缩放**（size×ω），不是复制 claim；`grp` 按 `ei`（事件索引）去重，同一事件内 ω 份只贡献 1 个 `ei` ⇒ 不触发 `count>=2`。✅ 此条 OK。
- **gate(3) 是否漏报跨事件同 mode 不同 scope 的真实冲突**：**会（设计性）**。gate(3) 按 `(res,scope,mode)` 分组（`EffectScript.cs:162`），跨 scope 的同资源 create×create 不被报——而 L1 `Compatible.Create×Create` 是 scope 无关的冲突。若 scope 被视为隔离边界则合理，否则是真实漏报。属设计选择，MED（提示：跨 scope 同资源冲突可能漏）。

---

## 4. 总评：每符号是否可被良性定义并正确运行

**结论：不可。** 阻塞项集中在 `EffectScript.Audit` 的数学核心（JD-1 `Merge` 误用）与两个解析器的资源归一化/集成（JD-8 `memory` 清零、JD-7 budget 脱钩、JD-9 schema 分歧、JD-2 scope 矛盾）。数据型符号（`EffectEvent` 字段、`Budget`、`AuditResult`、`Violation`、`At`）本身行为正确，但 `Violation.Scope` 在 JD-2 下会指向错误作用域，且 `Audit` 的 `Passed` 判定在 JD-1/JD-3/JD-8 下**会给出错误的「通过」**——即 fail-open，正是本次审计要揪出的静默漏报。

### HIGH 阻塞项清单
1. **JD-1**：`Audit` gate(1) 与闭包用 `Merge` 而非 `Add` ⇒ `NegativeDip`/`Leak` 静默漏报（行 155、254）。必须修。
2. **JD-8**：`EffectScriptIo` `memory` uid 清零 ⇒ 不同显存块错误合并 + 跨解析器峰值检查失效（行 124）。必须修（或砍掉 Io）。
3. **JD-2**：`At`（`Combination.Loop` 改写 scope）与 `Audit` gate(3)（claim 原 scope）对同一脚本给出矛盾 scope 视图；Contract 输入下系统性触发（行 79 vs 162）。必须统一 scope 来源。

### 现在最该做的三件事
1. 把 `EffectScript.cs:155` 与 `:254` 的 `Merge` 改成 `Add`，并用反例 A/B 写回归测试。
2. 统一 scope 来源：让 `Audit` gate(3) 与 `At` 采用同一 scope（建议都走 `Combination.Loop(...,e.Scope)`），或强制契约层 claim scope==event scope。
3. 二选一解析器：保留 `EffectScriptContract` 并修 JD-6，砍掉 `EffectScriptIo`（或反之），消除 `memory` 清零与 budget 脱钩（JD-7/8/9）。

### 最该砍的三件事
1. **`EffectScriptIo` 整个文件**（第二产品，schema 与 Contract 互斥、memory 清零、budget 脱钩）——YAGNI。
2. `EffectScriptContract` budget 对 `global` scope 的强制 `scene` 要求（JD-6）——非必要约束，且破坏 L1 `Global` 语义。
3. `Audit`/`ToJson` 里与 Io 互通的无谓 round-trip 兼容假设（两 schema 本就不该并存）。

---

## Acceptance Report

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "本报告为只读审计，未修改任何 .cs 源码；仅新增 D:/Godot/Cosmos/audit/effect-script-auditR3.md 一个审计产物文件，未扩大范围。"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "提供了可独立复核的证据：逐符号表 + 每个缺陷的具体文件:行号（EffectScript.cs:155/254/162/79/185/233, EffectScriptIo.cs:124/74/122, EffectScriptContract.cs:146/148/88-89/156-176）+ 最小可复现 JSON/C# 反例（JD-1 A/B、JD-2、JD-3、JD-8）+ 复杂度测算（JD-5 O(E^2·K) vs 声明 O(E·K·log E)）。"
    }
  ],
  "changedFiles": [
    "audit/effect-script-auditR3.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "grep -rn 'EffectScriptContract|EffectScriptIo' src --include=*.cs",
      "result": "passed",
      "summary": "在 src/ 内除自身定义外，两个解析器均无任何调用方（孤儿代码，确认 JD-9 第二产品判断）。"
    }
  ],
  "validationOutput": [
    "静态只读审查完成：发现 1 个 HIGH 数学核心 bug（JD-1 Merge vs Add）、2 个 HIGH 解析器/资源归一 bug（JD-8、JD-2）、4 个 MED（JD-3/4/5/6/7/9 跨 6 条）、2 个 LOW（JD-10/11）。",
    "反例 A（Leak 漏报）：create[10,10]@e1 + release[5,5]@e2 ⇒ Merge 得 [-5,10] 含 0 ⇒ 不报；Add 应为 [5,5] 报 Leak。",
    "反例 B（NegativeDip 漏报）：create[3,3]@e1 + release[10,10]@e2 重叠 ⇒ Merge 得 [-10,3]，Hi=3 不<0 ⇒ 不报；Add 应为 [-7,-7] 报 NegativeDip。",
    "复杂度校验：AuditAtSample 每个采样点全扫 net(E·K)+caps(C)+grp(E·K)，共 S≤2E 点 ⇒ O(E^2·K)，与文件头声明 O(E·K·log E) 不符（JD-5）。"
  ],
  "residualRisks": [
    "JD-2 scope 矛盾：未改代码，仅给出统一方案；是否采用 'At 改写 scope' 或 '强制 claim scope==event scope' 需产品/数学层拍板。",
    "JD-4 未来事件纳入闭包：当前被 JD-1 的 Merge 包络掩盖，JD-1 修复后会暴露误报，需在修复 JD-1 时一并处理（仅纳入 Lo<=closureT 的事件）。",
    "JD-9 两解析器取舍（保留 Contract 还是 Io）未定，影响 JD-6/7/8 的最终修复落点。"
  ],
  "noStagedFiles": true,
  "diffSummary": "仅新增审计产物 D:/Godot/Cosmos/audit/effect-script-auditR3.md；未改动任何 .cs 源码（只读审计，符合纪律）。",
  "reviewFindings": [
    "blocker: EffectScript.cs:155 — gate(1) 累积 net 用 Merge 而非 Add，导致 NegativeDip 静默漏报",
    "blocker: EffectScript.cs:254 — 闭包 Leak 用 Merge 而非 Add，导致 Leak 静默漏报",
    "blocker: EffectScriptIo.cs:124 — memory 资源 uid 无条件清零为 Memory(0)，跨缓冲错误合并且与 Contract budget key 不匹配",
    "blocker: EffectScript.cs:79 vs :162 — At(Combination.Loop 改写 scope) 与 Audit gate(3)(claim 原 scope) scope 来源矛盾，Contract 输入下系统性触发",
    "high: EffectScript.cs:233 — 居民层豁免按 ω=⊤ 而非 Hi=⊤，永久有限ω资源误报 Leak",
    "high: EffectScript.cs:185 — 峰值回退 sub>cur 时置 0（异常路径 fail-open）",
    "med: EffectScriptIo.cs:74 — Parse 不挂 Budget，gate(2) 峰值预算检查脱钩空转",
    "med: EffectScriptContract.cs:88-89 — global scope 因缺 scene 字段不可达，违反 L1 Global 语义",
    "med: 两解析器 resource/budget/scope JSON schema 互斥（JD-9），且 src/ 内均无调用方（第二产品，建议砍 Io）"
  ],
  "manualNotes": "本审计严格未读 audit/ 历史文件。所有判定来自对 9 个指定源码文件的独立推导。最高优先级修复是 JD-1（Merge→Add），它直接破坏 '没跑游戏就审计' 的核心卖点（fail-open 静默漏报）。JD-2 的 scope 矛盾在 Contract 输入下系统性存在，因为 Contract 强制每个 claim 带独立 scope。"
}
```