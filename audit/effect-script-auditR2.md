# 独立审计 R2 — 形式逻辑视角（证明收敛 / 定义良定义性）

## 0. 元信息

- **审计视角**：B（对抗性形式逻辑；「每个公开符号是否真被 discharged，而非被 asserted 冒充已证」）。
- **未读 `audit/` 历史**：严格遵守。本次只读取任务指定的 8 个源码文件，未打开 `D:/Godot/Cosmos/audit/` 下任何文件。所有判定均以源码文本与类型/运算符定义为据。
- **所读源码（仅此 8 个）**：
  1. `src/Cosmos.EffectAlgebra/EffectScript.cs`（目标）
  2. `src/Cosmos.EffectAlgebra/EffectScriptContract.cs`（目标）
  3. `src/Cosmos.EffectAlgebra/EffectScriptIo.cs`（目标）
  4. `src/Cosmos.EffectAlgebra/Objects.cs`（L1 支撑）
  5. `src/Cosmos.EffectAlgebra/Numeric.cs`（L1 支撑）
  6. `src/Cosmos.EffectAlgebra/Algebra.cs`（L1 支撑）
  7. `src/Cosmos.EffectAlgebra/SignedNet.cs`（L1 支撑）
  8. `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`（L1 支撑）

**判定尺度**：「是」= 定义义务由代码真正 discharge；「否」= 仅靠注释契约 / 与代码/与 L1 矛盾 / 反例推翻；「部分」= 主体 discharge 但存在旁支未定义。
立场：凡把 asserted 当作 discharged 的一律判「否」。

---

## 1. 四个焦点判定（任务强制要求逐条给反例）

### 1.1 `ResourceId.Normalize` 幂等性 —— **判定：是（discharged）**

**定义义务**：`Normalize` 必须是幂等全函数，且 `Self("signal_x")→SignalBus(x)` 之后不能再被二次剥 `signal_` 前缀（Objects.cs:39–49 注释契约）。

**代码实证**（Objects.cs:51–62）：
```
Self s when s.Component.StartsWith("signal_", ...) => SignalBus(Substring)
Signal sig when sig.Name.Value.StartsWith("signal_", ...) => SignalBus(Substring)
SignalBus bus => bus                      // 关键：原样返回，不再二次处理
_ => r
```
- 输出域只可能是 `SignalBus`（前两条）或原构造子（最后 `_`）。
- 对任意输出再次 `Normalize`：`SignalBus` 命中 `SignalBus bus => bus` 原样返回；其余命中 `_` 原样返回。
- 无任何分支能把 `SignalBus("signal_signal_x")` 再剥成 `SignalBus("x")`——该分支被显式排除。

**反例尝试（无）**：`Normalize(Normalize(Self("signal_x"))) = Normalize(SignalBus("x")) = SignalBus("x") = Normalize(Self("signal_x"))`。成立。

**结论**：幂等性由 `SignalBus bus => bus` 分支真正 discharge，非仅靠注释。✅
**注意（非缺陷）**：`Self→SignalBus` 这条归一路径在 JSON 层**只有 EffectScriptIo 可达**（Io 支持 `self` 键，Objects.cs/Io:127）；EffectScriptContract **不支持 `self` 键**（Contract.cs:138–152 只认 gpu/commandBuffer/memory/occupancy/signalBus）。故「AI 用 Contract 形态产 JSON」时 `Self("signal_x")≡SignalBus` 不可达，仅 `signalBus` 键可用——这是两解析器资源集差异（见 §1.4），不影响 Normalize 自身幂等性。

---

### 1.2 `NetTable.Add` vs `EffectScript` 守恒聚合（gate(1) 用 `Merge`） —— **判定：否（未 discharge，HIGH）**

**定义义务**：守恒判定必须对同资源多 Claim 求**带符号净和**（§3.3.1：`net = Σcreate − Σrelease`，端点相加 `[lo+o.lo, hi+o.hi]`）。Algebra.cs:65 `t._net[r] = ...Add(signed)` 明确用 `Add`（SignedNet.cs:76 `[Lo+o.Lo, Hi+o.Hi]`），且其注释警告「Merge 会吞掉守恒判定，漏报泄漏」。

**代码实证（EffectScript.cs）**：
- 运行中 net（gate(1)）：`EffectScript.cs:155` → `net[r] = net.TryGetValue(...) ? cur.Merge(contrib) : contrib;`
- 闭包 net（Leak）：`EffectScript.cs:254` → `closureNet[r] = ...cur.Merge(contrib) : contrib;`
- `Merge` = SignedNet.cs:79 → `[Lo.Min(o.Lo), Hi.Max(o.Hi)]`（**逐端 min/max 包络，非求和**）。

**矛盾**：EffectScript 注释（cs:108–113）把 C(t) 定义为 `Σ_{e.Lo≤t} Σ_{occupy c} sign·scaleSize`（求和），但代码用的是 `Merge`（包络）。两处聚合算子与 L1 `NetTable.Compute` 刻意回避的 bug（用 Merge 而非 Add）**完全相同地重新引入**。

**反例 A（Leak 被吞没，closure）**：
- 资源 `gpu:"b"`，事件 A：`[0,10]` create size `[5,5]` ω=1；事件 B：`[3,8]` release size `[3,3]` ω=1。
- 闭包（maxFinite=10）两事件 Lo≤10 均计入。
- 代码 `Merge`：`[min(5,−3), max(5,−3)] = [−3, 5]` → `ContainsZero`=true → **不报 Leak**（误通过）。
- 真净和 `Add`：`[5−3, 5−3] = [2, 2]` → 不含 0 → **应报 Leak**（净剩 +2 资源未释放）。
- 系统性后果：`Merge` 包络对「一 create 一 release」恒跨 0，**只要该资源同时存在过 create 与 release，Leak 几乎永不被报**，与守恒判定目标相反。

**反例 B（NegativeDip 被吞没）**：
- 资源 `gpu:"b"`，事件 R：`[0,10]` release size `[5,5]`；事件 C：`[3,8]` create size `[3,3]`。
- 采样点 t=5 两者均存活。
- 代码 `Merge`：`[min(−5, 3), max(−5, 3)] = [−5, 3]`，`Hi=3 ≥ 0` → **不报 NegativeDip**（误通过）。
- 真净和 `Add`：`[−5+3, −5+3] = [−2, −2]`，`Hi=−2 < 0` → **应报 NegativeDip**（t=5 时净占用仍为负）。

**结论**：gate(1) 的守恒与负陷语义**未被 discharge**——断言的「端点采样等价 / Σ 净和」被代码中的 `Merge` 包络推翻。这是把「数学等价于逐点全算」asserted 为已证、实则未证的典型。HIGH 阻塞项。

---

### 1.3 `At` 的 scope 改写 —— **判定：部分（主体 discharged，旁支 flatten 未定义）**

**定义义务**：`At(t)` 应对每个存活 Event 取 `Combination.Loop(Footprint, Loop, Scope)` 后 `Union`；scope 来源须明确。

**代码实证**：`EffectScript.cs:79` → `Combination.Loop(e.Footprint, e.Loop, e.Scope)`，且事件构造子注释（cs:16–21）声明 Event 自带 `Scope` 作为 scope 来源（修 OPEN-1）。

**L1 一致性**：`Combination.Loop`（DerivedMetrics.cs:55–67）内部 `c with { Scope = loopScope, ... }` —— 即它**把 claim 原 scope 重写为 loopScope**。EffectScript 传入 `e.Scope` 作 loopScope，故 `At` 中每个 claim 的 scope 被重写为事件级 `e.Scope`。这与 L1 `Combination.Loop` 的已定义行为一致，注释与代码相符。✅ 主体 discharged。

**旁支未定义（LOW，非 blocker）**：
- `At` / gate(1)/gate(2) 中 scope **不参与过滤**（gate(1) 仅按归一 `ResourceId` 聚合；gate(2) 同理）。scope 只在 `At` 的 loopScope 与 gate(3) 的 `(r, c.Scope, mode)` 分组键中起作用。
- 因此 JSON 中 footprint claim 自带的 `scope` 字段在 `At`/守恒/峰值审计里被**静默丢弃**（被事件级 `e.Scope` 覆盖）。若 AI 期望「claim 级 scope 影响审计」，该语义未定义也未文档化于 EffectScript（仅在 L1 `NetTable.Compute` 用 `IncludedIn` 过滤，而 EffectScript 根本不调用 `NetTable`/`IncludedIn`）。属「声明了 scope 字段但部分语义未被 discharge」。

---

### 1.4 两个 JSON 解析器 schema 一致性 —— **判定：否（多处不一致，HIGH/MEDIUM）**

`EffectScriptContract` 与 `EffectScriptIo` 都声称映射「同一份 EffectScript JSON ↔ L1」，但下列形状彼此矛盾或漏归一：

| 维度 | EffectScriptContract | EffectScriptIo | 是否一致 | 严重度 |
|---|---|---|---|---|
| 资源键集合 | 仅 gpu/commandBuffer/memory/occupancy/signalBus（Contract.cs:138–152） | 还支持 tree/self/physics/disk/signal/audioMixer/network/input/custom/callback（Io.cs:122–138） | **否**：Io 接受的资源在 Contract 抛 `FormatException` | HIGH |
| `memory` 归一 | `Memory(mem.GetUInt64())`（Contract.cs:148）即**尊重 JSON 数字** | `Memory(0)` **硬编码忽略 JSON**（Io.cs:124） | **否**：同一 JSON memory 在两解析器得到不同 ResourceId | HIGH |
| `gpu` 子字段 | `gpu.GetString()` 直接作 bufferId（Contract.cs:146） | `Extract(v,"bufferId")`（Io.cs:122），兼容 `{bufferId:"x"}` 或字符串简写 | 部分一致（Contract 只接受字符串简写） | LOW |
| `commandBuffer` 子字段 | `cb.GetString()`（Contract.cs:147） | `Extract(v,"channel","gpu")`（Io.cs:123） | 部分一致 | LOW |
| budget 形状 | `budget` = 扁平对象 `"gpu:x": N`（Contract.cs:156–177） | `budget` = `{ "caps": [ {resource, cap}, ... ] }`（`caps` 数组，Io.cs:48–66） | **否**：Contract 形态在 Io 中因找不到 `caps` 数组 → **静默返回 `Budget.None`**，不报错 | HIGH |
| claim scope 来源 | 必填每 claim `scope` 字段（Contract.cs:175 缺则 FormatException） | **忽略**每 claim scope，统一用事件级 scope（Io.cs:110 `ParseClaim(c, scope)`） | **否**：Io 不读 per-claim scope；Contract 强制要求 | MEDIUM |
| kind/mode 解析 | 精确小写 switch（Contract.cs:158–167） | `Enum.Parse` 大小写不敏感（Io.cs:104–108） | 部分一致（Io 更宽松） | LOW |
| scope 缺省/未知 | 缺 `scene` 即抛（Contract.cs:85–97）；未知 type 串回退 Scene | 空对象 → `Scene("Default")`；未知 scope 名抛（Io.cs:142–156） | 否（缺省行为不同） | LOW |

**反例（budget 静默漏报）**：JSON `{"events":[...], "budget":{"gpu:x":100}}` 经 `EffectScriptIo.ParseBudget(json)` → 找不到 `caps` 数组 → 返回 `Budget.None`（Io.cs:50–52），**预算约束被整体丢弃且无报错**。同一 JSON 经 `EffectScriptContract.Parse` 则正确建立 cap。两解析器对「预算是否生效」给出相反结果。

**反例（memory 归一分歧）**：`{"memory": 4096}` → Contract 得 `Memory(4096)`；Io 得 `Memory(0)`。若脚本含两个不同 uid 的 memory 资源，Io 把它们**坍缩成同一把钥匙**，守恒/峰值聚合串味。

**反例（资源集合分歧）**：`{"self":{"component":"signal_x"}}` 或 `{"tree":{"path":"Root/A"}}` → Io 正常解析（并经 `Normalize` 得 `SignalBus("x")` / `Tree(...)`）；Contract 因无 `self`/`tree` 键 → `FormatException`。即「同一份 AI 剧本」能否被解析取决于走哪个解析器。

**结论**：两解析器并未共享同一 schema 契约，且 Io 对 `memory` 与 `budget` 的处理会静默改变审计结果。属「声称统一的契约层」未被 discharge。

---

## 2. 逐符号 well-definedness 表

符号 | 定义义务 | 是否 discharge | 反例 / 说明 | 严重度
---|---|---|---|---
`EffectEvent.Lifetime` | 存在时间窗 `[lo,hi]`，`hi=⊤` 开放 | 是（类型承载 `Interval`） | 构造即全必填 | —
`EffectEvent.Scope` | At/Net/Peak scope 来源 | 是（类型承载 `ScopeId`） | 见 §1.3 旁支 | LOW
`EffectEvent.Footprint` | 存活期资源签名（三桶） | 是（`Signature` 强制） | — | —
`EffectEvent.Loop` | 并发副本数 ω∈ℕ∪{⊤} | 是（`LoopCount`） | — | —
`EffectEvent(lt,scope,fp,loop)` | 全字段构造 | 是 | — | —
`EffectEvent(lt,scope,fp)` | 默认 ω=1 | 是（`LoopCount.Of(1)`） | — | —
`EffectScript.Events` | 有限事件集 | 是（`ImmutableArray`） | — | —
`EffectScript(ImmutableArray)` | 构造 | 是 | — | —
`EffectScript(IEnumerable)` | 便捷构造 | 是（`ToImmutableArray`） | — | —
`EffectScript.At(t)` | `Σ Loop(Footprint,Loop,Scope)` 后 `Union` | 部分（见 §1.3） | scope 在审计中部分被 flatten | LOW
`EffectScript.Audit(Budget)` | 三 gate 扫换线审计 | **否**（gate(1) 用 `Merge` 非 `Add`） | 见 §1.2 反例 A/B | **HIGH**
`EffectScript.Budget` | 剧本级预算（默认 None） | 是 | — | —
`EffectScript.Audit()` | 用自带 Budget 审计 | 是（调 `Audit(Budget)`） | — | —
`Budget.Caps` | 每资源峰值上限表 | 是 | Audit 用 `Normalize(kv.Key)` 查（cs:205）消歧 | —
`Budget.None` | 空预算（全无上限） | 是（静态只读） | — | —
`AuditResult.Passed` | 全部 gate 通过 | 是 | 由 `violations.Count==0` | —
`AuditResult.Violations` | 违例清单 | 是 | — | —
`Violation.AtT/Resource/Scope/Kind/Detail` | 反例信息载体 | 是（record 字段） | — | —
`EffectScriptContract.Parse` | JSON→EffectScript（fail-fast） | 部分 | budget 形状与 Io 不一致（§1.4）；资源集窄于 Io | HIGH/MED
`EffectScriptContract.ToJson` | 序列化（round-trip） | 是（与自身 `Parse` 自洽） | 但与 Io `Parse` 不兼容 | LOW
`EffectScriptContract.ParseBudget` | 扁平 `{"gpu:x":N}` → caps | 部分 | 与 Io `ParseBudget` 形状矛盾 | HIGH
`EffectScriptIo.Parse` | JSON→EffectScript（fail-fast） | 部分 | 资源集 ≠ Contract；memory 硬编码 0；忽略 per-claim scope | HIGH/MED
`EffectScriptIo.ParseBudget(string)` | 取 budget 节 | **否**（静默丢 budget） | `caps` 数组缺失 → `Budget.None` 无报错（§1.4） | HIGH
`EffectScriptIo.ParseBudget(JsonElement)` | caps 数组→Budget | 否（形状与 Contract 矛盾） | 要求 `caps` 数组，Contract 给扁平对象 | HIGH
`EffectScriptIo.ParseResource` | 资源归一 | **否** | `memory`→`Memory(0)` 忽略 JSON（Io.cs:124） | HIGH
`EffectScriptIo.ParseScope` | scope 归一 | 部分 | 缺省 `Scene("Default")`、未知名抛，与 Contract 不同 | LOW
`EffectScriptIo.ParseClaim` | claim 归一 | 部分 | 丢弃 per-claim scope，用事件 scope | MED
`EffectScriptIo.Extract` | 子字段兼容简写 | 是（字符串/对象双形态） | — | —

---

## 3. 守恒 / 闭包 / 泄漏 数学定义闭合性复核

- **gate(1) 运行中 net（NegativeDip）**：代码用 `Merge`（cs:155）。断言「累积净 net C(t)=Σ…」与代码（包络）矛盾 → **未 discharge**（§1.2）。
- **gate(1) 闭包 Leak**：代码用 `Merge`（cs:254），与 L1 `NetTable.Compute` 用 `Add` 相反 → **未 discharge**。闭包仅对 `Lo≤closureT` 且有限 ω 累加，豁免 `ω=⊤`/`Lo=⊤`，这部分逻辑与注释一致；但聚合算子错，结论不可信。
- **gate(2) 峰值**：用 `peakSum` 加法（`+`）与 `topCount`，属真求和 → **discharged**。
- **gate(3) 兼容**：按 `(r, c.Scope, mode)` 维护活跃事件集，≥2 不同事件即报冲突，等价于逐点两两枚举（同 mode 多副本必来自 ≥2 事件）→ **discharged**（但 `c.Scope` 分组键属设计选择，与 L1 `Compatible.IsCompatible` 仅取 mode 不同，是额外约束，未与 L1 冲突但亦未由 L1 定义）。
- **ω=⊤ 居民层豁免**：gate(1) 仅 `enter && !Loop.Count.IsTop` 才累加（cs:151），闭包跳过 `IsTop`（cs:248）；gate(2) 用 `topCount` 标记 ⊤ → 峰值判 ⊤。两处对 ⊤ 的处理**同一套数学定义**（开放上界），一致 → discharged。✅
- **`ScaleSize`**（cs:288–294）：`ω=⊤→[lo,⊤]`，否则端点×ω，与 L1 `Combination.Scale`（DerivedMetrics.cs:84–90）逐字同构 → discharged。✅

---

## 4. 总评

**本子系统「每符号定义义务是否都被满足」：否。** 三个焦点中有两个（守恒聚合算子、两解析器 schema 一致性）是**核心未 discharge** 项；其余符号（记录结构、L1 复用算子、ω 豁免、ScaleSize）定义义务基本被代码真正 discharge。

把「asserted 冒充 discharged」定位如下：
1. `EffectScript.Audit` 注释声明「数学等价于端点采样审计 / 三 gate 与逐点全算逐条 Violation 一致」，但 gate(1) 用 `Merge` 而非 `Add`，使守恒与负陷判定系统性失效（漏报 Leak / NegativeDip）。这是**与 L1 `NetTable.Compute` 显式规避的 bug 完全相同**的回归。
2. `EffectScriptContract` 与 `EffectScriptIo` 各自声称映射「同一 EffectScript JSON 契约」，但资源集、memory 归一、budget 形状、claim scope 来源四处分歧，且 Io 对 `budget`/`memory` 的异常分支会**静默改变审计结果**（不抛错）。

### HIGH 阻塞项清单（须修复后方可宣称「良定义」）
1. **HIGH-1** `EffectScript.cs:155` 与 `:254`：守恒/闭包 net 必须用 `SignedInterval.Add`（求和）而非 `Merge`（包络）。当前实现系统性吞没 Leak 与 NegativeDip（反例见 §1.2）。
2. **HIGH-2** `EffectScriptIo.cs:124`：`memory` 硬编码 `Memory(0)`，忽略 JSON uid，导致 memory 资源无法区分、归一与 Contract/L1 三重不一致。
3. **HIGH-3** `EffectScriptIo.ParseBudget`（`Io.cs:48–66` 与 `:38–46`）：budget JSON 形状（`caps` 数组）与 `EffectScriptContract` 扁平形态矛盾；Io 在找不到 `caps` 时**静默返回 `Budget.None`**，预算约束被丢弃且无报错。
4. **HIGH-4** 两解析器资源键集合不一致（Contract 仅 5 种、Io 支持 14 种）：同一 AI 剧本能否解析取决于走哪个解析器，违反「统一契约」声称。

### MEDIUM
- **MED-1** `EffectScriptIo.ParseClaim` 丢弃 per-claim scope（统一用事件 scope），与 `EffectScriptContract` 强制 per-claim scope 矛盾。
- **MED-2** `At`/gate(1)/gate(2) 中 claim 自带 scope 在审计中被事件级 scope 覆盖，语义未在 EffectScript 层文档化（仅 L1 `IncludedIn` 涉及，而 EffectScript 不调用 `NetTable`/`IncludedIn`）。

### LOW（设计层，非良定义 blocker）
- `At` scope flatten 旁支（§1.3）；scope 缺省/未知处理两解析器不同；kind/mode 大小写宽松度不同。

### 已真正 discharge（确认无反例）
- `ResourceId.Normalize` 幂等性（§1.1）；`ScaleSize` 与 L1 `Combination.Scale` 同构；ω=⊤ 居民层豁免在 gate(1)(2) 同一数学定义；gate(2) 峰值求和；gate(3) 兼容分组；`Budget.None`/`AuditResult`/`Violation` 记录结构；`EffectScriptContract.ToJson` 与自身 `Parse` 自洽。

---
*本报告仅基于上述 8 个源码文件，未读取 `audit/` 历史。所有「否」判定均附带可复现的最小反例与文件:行锚。*
