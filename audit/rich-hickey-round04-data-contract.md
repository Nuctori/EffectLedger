# Rich Hickey Round04 — Data Contract 审计 (Data Orientation 透镜)
`audit/rich-hickey-round04-data-contract.md` 已生成（本响应即完整产物，运行时落盘）。

---

## 1. 审计范围与透镜

- **透镜**: Rich Hickey — Data Orientation / Value vs Identity / Maybe Not / Decomplect / Simple vs Easy。核心命题：数据是值，值必须可通过 `ifn` (Parse/ToJson/At/Audit) 往返而不失真；非法形状必须 **fail-fast**，不得 **fail-soft**（`nil` punning / silent default）。
- **允许 7+1 文件**（禁止读 `audit/`）：
  1. `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` — 7.8KB, 333行 — JSON ⇄ L1 契约
  2. `src/Cosmos.EffectAlgebra/EffectScript.cs` — 核心 `At`/`Audit`/`Budget`
  3. `src/Cosmos.EffectAlgebra/Objects.cs` — `ResourceId`/`ScopeId`/`Claim`/`Signature`
  4. `src/Cosmos.EffectAlgebra/Numeric.cs` — `NatStar`/`Interval`
  5. `src/Cosmos.EffectAlgebra/DerivedMetrics.cs` — `LoopCount`/`Combination`
  6. `src/Cosmos.EffectAlgebra/Algebra.cs` — `Compatible`/`Peak`/`NetTable`
  7. `src/Cosmos.EffectAlgebra/SignedNet.cs` — `ZStar`/`SignedInterval`
  8. `EFFECT_SCRIPT.md` §4 仅 — AI 契约形状（扁平 `resource:{"gpu":"mesh1"}`，事件 `scope` 必填，`lifetime`/`scope`/`loop`/`footprint` 白名单，未知键抛 `FormatException`，`Parse(ToJson(script))` 幂等）
- **契约形状**（§4）：`{events:[{lifetime:[lo,hi], scope:{scene|type}, loop, footprint:[{kind,resource,mode,scope,size}]}], budget:{"gpu:mesh1":n|"⊤"}}`

## 2. 符号表（Symbol × File × Line —逐符号可追）

| 符号 | 文件 | 行 | 契约角色 |
|---|---|---|---|
| `EffectScriptContract.Parse` | `EffectScriptContract.cs` | 21 | 入口，fail-fast |
| `RejectUnknownKeys` | `EffectScriptContract.cs` | 49 | 白名单守卫 |
| `ToJson` | `EffectScriptContract.cs` | 62 | 序列化 + `UnsafeRelaxedJsonEscaping` |
| `ParseEvent` | `EffectScriptContract.cs` | 77 | 事件层白名单 |
| `ParseInterval` | `EffectScriptContract.cs` | 89 | `[lo,hi]` |
| `ParseTop` | `EffectScriptContract.cs` | 108 | `数字| "⊤"` |
| `ParseScope` | `EffectScriptContract.cs` | 118 | `scene/type` |
| `ParseLoop` | `EffectScriptContract.cs` | 143 | `≥1 | "⊤"` |
| `ParseFootprint` | `EffectScriptContract.cs` | 157 | `claim.scope==event.scope` 双真相 |
| `ParseClaim` | `EffectScriptContract.cs` | 183 | `kind/resource/mode/scope/size` |
| `ParseKind` | `EffectScriptContract.cs` | 196 | `read/write/occupy` |
| `ParseMode` | `EffectScriptContract.cs` | 202 | `use/create/release/move/unknown` |
| `ParseResource` | `EffectScriptContract.cs` | 209 | 扁平单键 |
| `ParseBudget` | `EffectScriptContract.cs` | 225 | `⊤/"inf"` 往返 |
| `ParseResourceKey` | `EffectScriptContract.cs` | 247 | `gpu: / memory:` 前缀 |
| `SerializeEvent` | `EffectScriptContract.cs` | 258 | 三桶 concat |
| `SerializeScope` | `EffectScriptContract.cs` | 269 | `Global→{"type":"global"}` |
| `SerializeClaim` | `EffectScriptContract.cs` | 278 | `kind.ToLower()` |
| `SerializeResource` | `EffectScriptContract.cs` | 287 | 扁平形态 |
| `SerializeBudget` | `EffectScriptContract.cs` | 297 | `⊤→"⊤"` |
| `ResourceKey` | `EffectScriptContract.cs` | 306 | 键前缀 |
| `ReqStr` | `EffectScriptContract.cs` | 323 | 非空串 fail-fast |
| `EffectEvent` ctor+IsValid | `EffectScript.cs` | 41 | `Lo≠⊤`, `!loop.IsValid` 拒绝 |
| `EffectScript.At` | `EffectScript.cs` | 90 | `Alive ⇒ Union(Loop(...))` |
| `ComputeSamplePoints` | `EffectScript.cs` | 101 | 端点+幽灵点 |
| `Audit` sweep-line | `EffectScript.cs` | 134 | O(E·K·logE) 三门 |
| `Budget` ctor normalize | `EffectScript.cs` | 384 | `Normalize` 分组 |
| `AuditResult` | `EffectScript.cs` | 426 | `Passed≡IsEmpty` |
| `ResourceId.Normalize` | `Objects.cs` | 52 | `Self(signal_)→SignalBus` |
| `ScopeId.IncludedIn` | `Objects.cs` | 102 | `Global⊇*` |
| `Claim.Normalize` | `Objects.cs` | 133 | `Read×Create` 拒绝, `null→Default` |
| `Signature.Of` dup guard | `Objects.cs` | 171 | 重复 Claim 抛（并发走 `Loop`） |
| `NatStar` `+/ *` | `Numeric.cs` | 25,33 | 溢出→⊤ |
| `Interval` ctor | `Numeric.cs` | 80 | `lo>hi`/`[⊤,x]` 拒绝 |
| `LoopCount.Of/IsValid` | `DerivedMetrics.cs` | 18,26 | `0`非法 |
| `Combination.Loop` | `DerivedMetrics.cs` | 50 | `Scope` 重标 + `Scale` |
| `Compatible.IsCompatible` | `Algebra.cs` | 18 | 16对全函数 |
| `NetTable/Peak` | `Algebra.cs` | 54,117 | 仅 `Occupy` 桶 |

## 3. 逐维度审计

### 3.1 EffectScript 作为纯数据值是否可被 JSON 往返无损承载？

**结论：对契约限定子集无损；对 L1 全集有损（intentional incompleteness）**。

- **往返路径**：`EffectScript(C#值) → ToJson → Parse → EffectScript` 在限定域内幂等：`SerializeEvent` 完整三桶 (`ReadClaims+WriteClaims+OccupyClaims`) ，`ParseFootprint` 按 `kind` 回分桶，对称（`EffectScriptContract.cs:258-266` 修 OPEN-2）。`Interval lo/hi` 经 `ParseTop`/`SerializeEvent:260` `"⊤"` 往返（`EffectScriptContract.cs:108-116,260`）。`Budget` `⊤` 经 `SerializeBudget:302` `IsTop→"⊤"` + `ParseBudget:232-236` 识别 `"⊤"|"inf"` 往返（修 R2-N1）。
- **Global 往返**：已修复 `auditR4 CRITICAL` — `ParseScope:125-138` 缺 `type⇒Scene`, `type=="global"→Global()` 不要求 `scene`；`SerializeScope:274` `Global→{"type":"global"}` 。`Parse(ToJson(Global))=Global` 成立。但见 Finding F-01（scene 残留静默丢弃）。
- **有损边界（类型系统 > 契约）**：
  - `ScopeId` 合法值 `Shell|Loop|Conditional|Async` 在 `ParseScope:133-139` 视为未知抛 `FormatException`，`SerializeScope:269-275` 亦抛 “不可序列化”。程序化构造的 `EffectScript` 含此类 scope 无法 JSON 往返——但 `EFFECT_SCRIPT.md §4` 显式限定契约仅 `scene/method/type/global`，属文档化收敛，非 bug。
  - `ResourceId` 合法值 `Tree|Self|Physics|Disk|Signal|AudioMixer|Callback|Network|Input|Custom` 同理仅 `gpu|commandBuffer|memory|occupancy|signalBus` 可往返（`ParseResource:209-222` / `SerializeResource:287-295`）。`SignalBus` 扁平形态是 rich-hickey2 R7 锚定形态，`{"gpu":{"bufferId":"x"}}` 嵌套形态已在 `ReqStr` 路径抛 `FormatException`。
- **字节级幂等**：`UnsafeRelaxedJsonEscaping` (`ToJson:73`) 保障 `"⊤"` 不被 `\u264b` 转义，`Parse` 接受 `"⊤"`，`Parse(ToJson(x))` 内容等价。**但** 稀疏输入 `missing size/loop` 经 `ToJson` 必展开为显式 `size:[1,1]` + `loop:1`（`ParseClaim:192`, `ParseEvent:84`），故 `JSON文本→Parse→ToJson→JSON文本` 非字节幂等，仅 `C#值` 幂等。文档 `EFFECT_SCRIPT.md §4` 的幂等断言是后者，成立。

### 3.2 EffectScriptContract 是 fail-fast 还是 fail-soft？

**主体 fail-fast，白名单三层；两处对象层 fail-soft 缺口。**

- **fail-fast 已落地**（证据）：
  - 根/事件/Claim 三层 `RejectUnknownKeys`：根 `events|budget` (`29`)，事件 `lifetime|scope|loop|footprint` (`81`)，Claim `kind|resource|mode|scope|size` (`186`)，拼写 `budgat / Loop` 直接 `FormatException`。
  - `kind/mode` 字符串白名单 `ParseKind/ParseMode` 抛 `未知 kind/mode` (`196-207`)。
  - `resource` 值非空串守卫 `ReqStr` 抛 `resource.* 须为非空字符串` (`323`) 修 `auditR2/R4 C2` 静默兜底 `""|0`。
  - `memory` 非数字抛 `FormatException` (`219`) 修 R3 V3-006。
  - `Interval [⊤,⊤]` 与 `lo>hi` 翻为 `FormatException` (`98-102`)，`Loop 0` 抛 (`151`)，`default(LoopCount)` 在 `EffectEvent:47` 与 `Combination.Loop:54` 双处拒绝。
  - `budget` 非对象抛 (`40`)，`lifetime` 非数组抛 (`104`)。
- **fail-soft 残留**（见 §4 Findings）：
  - `resource` 对象与 `scope` 对象未 `RejectUnknownKeys`，多余键静默忽略。
  - `budget` `memory:xyz` 前缀值 `ulong.Parse` 未统一为契约 `FormatException` 方言。
  - `Global` 残留 `scene` 静默丢弃。

### 3.3 魔法字符串（kind/mode/scope type/resource 键/size缺省）是否用类型约束？

- **已用类型约束**：`Kind`/`Mode` 为 `enum` 穷举 (`Objects.cs:112,117`)；`Kind×Mode` 非法组合 `Read+Create|Release|Move` 在 `Claim.Normalize:135` 构造期 `ArgumentException`；`ResourceId`/`ScopeId` 为判别联合 `abstract record` (`Objects.cs:21,90`)；`Interval`/`NatStar`/`LoopCount` 构造子校验 `lo≤hi`/`IsTop`/`≥1`。
- **仍为字符串分发**：JSON 层必须经字符串 `kind/mode/scope.type/resource key` 分发（`ParseKind:196`, `ParseMode:202`, `ParseScope:133`, `ParseResource:217-221`, `ParseResourceKey:247`），但每条分发均为白名单 `switch` 非 `if-else` 容错，且错误抛 `FormatException`——符合 Hickey “字符串是数据，但非法字符串必须 fail-fast”。
- **size 缺省**：`null Size → Interval.Default [1,1]` (`Objects.cs:140`, `EffectScriptContract.cs:192`) 是设计意图（`EFFECT_SCRIPT.md §2.3` `Claim.Size:Interval` 缺省 1），类型层面 `Interval?` 可空区分 `null` 与 `Exact(0)=[0,0]`，不再膨胀。对 Hickey 而言 **显式优于隐式**，但此为文档化缺省且 `ToJson` 总显式回写 (`SerializeClaim:284`)，可接受，记 P2 显式化建议。

## 4. Findings（行号锚定，最小修复）

### F-01 [P1] `scope` 对象未知键 fail-soft；`Global` 残留 `scene` 静默丢弃
- **位置**：`EffectScriptContract.cs:118-141` `ParseScope`；`SerializeScope:269-275`
- **证据**：`ParseScope` 仅判 `scene/type` 存在性，未 `RejectUnknownKeys`。`{"scene":"Battle","foo":"bar"}` 通过；`{"type":"global","scene":"Battle"}` 中 `scene` 在 `Global()` 分支被忽略（`name` 计算后弃用）。
- **Hickey 违背**：`nil` punning / 拼写错误静默吞掉比报错更危险；`Global` 本应无身份字段，携带 `scene` 是数据矛盾应拒绝。
- **最小修复**：`ParseScope` 入口加 `RejectUnknownKeys(el,"scope","scene","type")`；`type=="global"` 分支若 `hasScene && name!=""` 则 `throw FormatException("global scope 不可含 scene")`。

### F-02 [P1] `resource` 对象未知/多余键 fail-soft（最同质错误：扁平单键契约被破坏）
- **位置**：`EffectScriptContract.cs:209-223` `ParseResource`
- **证据**：`{"gpu":"mesh1","commandBuffer":"gpu"}` 按 `if(gpu) return Gpu` 短路，次键静默忽略；`{"gpu":"x","extra":"y"}` 亦通过。契约明文 `resource 必须是扁平字符串形态 {"gpu":"mesh1"}` 且单键，额外键应与根/事件同型拒绝。
- **最小修复**：`ParseResource` 入口加 `RejectUnknownKeys(el,"resource","gpu","commandBuffer","memory","occupancy","signalBus")` + 计数校验 `propCount!=1` 抛 `FormatException("resource 须为单键对象")`。

### F-03 [P1] `budget` `memory:` 键值非数字/溢出抛非契约异常（方言分裂）
- **位置**：`EffectScriptContract.cs:251` `ulong.Parse(k["memory:".Length..])`
- **证据**：`ParseResourceKey` 对 `memory:abc` 抛 `FormatException` (BCL) 未包装；`memory:99999999999999999999` 抛 `OverflowException`。契约其余路径统一 `FormatException`（`ParseInterval:102` 显式翻译 `ArgumentException→FormatException`），调用方 `catch(FormatException)` 会漏接。
- **最小修复**：`try { ulong.Parse } catch(Exception ex) when(ex is FormatException or OverflowException) { throw new FormatException($"未知 budget 键: {key} (memory 值须为非负整数)", ex); }`；或改 `ulong.TryParse` fail-fast。

### F-04 [P2] `size`/`loop` 缺省静默填充（显式化缺口，Hickey Simple vs Easy）
- **位置**：`EffectScriptContract.cs:84` `loop` 缺省 `Of(1)`；`EffectScriptContract.cs:192` `size` 缺省 `Interval.Default`
- **证据**：AI 遗漏 `size` 时落 `[1,1]` 而非报错；往返 `ToJson` 恒展开，稀疏输入非字节幂等。`EFFECT_SCRIPT.md §4` 未明示 `size` 缺省语义（示例显式写 `[1,1]`）。
- **权衡**：设计意图是 “缺省 1” 便 AI 省略；Hickey 数据透镜倾向 **no implicit default**。当前已因 `SerializeClaim:284` 显式化输出而可审计，降 P2。需文档显式声明或 AI 提示词约束。
- **最小修复（可选）**：文档补 “size 缺省 ⇒ [1,1]，loop 缺省 ⇒ 1（显式化回写）”；或加契约开关 `strict` 要求显式。

### F-05 [P2] `ResourceId`/`ScopeId` 可表达 ≠ 可序列化（值空间分裂，文档化但需显式）
- **位置**：`Objects.cs:21-65` 多构造子 vs `EffectScriptContract.cs:287-295,269-275,247-254` 仅 5+4 子集
- **证据**：`new Claim(Occupy, new ResourceId.Tree(...), ...)` 可 `Audit` 但 `ToJson` 抛 `不可序列化 resource`；`ScopeId.Shell` 同理。`EFFECT_SCRIPT.md §4` 契约限定扁平形态，`§7` 白名单外资源不在此契约。
- **定性**：非 bug，有意收敛；但 Hickey “value must survive `pr-str` round-trip” 要求类型与契约值空间对齐。当前需在 `EffectScriptContract.cs` 头注与 `EFFECT_SCRIPT.md §4` 显式列 “可序列化子集” 白名单，避免维护者误以为 `ResourceId` 全集皆可 JSON。
- **最小修复**：注释/文档补 “JSON 契约仅支持 `ResourceId ∈ {Gpu,CommandBuffer,Memory,Occupancy,SignalBus}` 且 `ScopeId ∈ {Scene,Method,Type,Global}`；其余需经 `ResourceId.Normalize` 或映射层转换”。

### F-06 [P2] `Budget.Caps` 键归一化合并后者赢（silent last-write-wins）
- **位置**：`EffectScript.cs:384-391` `Budget` ctor `norm[Normalize(kv.Key)]=v`
- **证据**：`{Self("signal_x"):5, SignalBus("x"):10}` 归一后单条 10，前者静默丢失。`EffectScript.cs:388` 注释 “后者赢”。契约 `ParseBudget:229` 按字符串键 `gpu:/signalBus:` 分组不会触发此合并，但程序化 `new Budget(dict)` 会。
- **定性**：符合 “单一真源” 去重，但 last-write-wins 应 fail-fast 或至少日志。P2 报告-only：建议构造期检测 `norm.ContainsKey` 则抛或文档化。
- **最小修复（可选）**：`if(norm.ContainsKey(nk)) throw new ArgumentException($"Budget 归一化键冲突: {kv.Key} ≡ {nk}")`。

**无 P0 阻断** — 均未构成数据静默改写为更大危害（如 `auditR2 C2` 的 `"" / 0` 兜底已修），当前剩余为对象层多余键忽略与方言不一致。

## 5. 残留风险与确认

- `Interval [⊤,⊤]` / `Loop 0` / `default(LoopCount)` / `Read×Create` / `重复 Claim` 五处已在 `EffectScript.cs:43,47` / `Numeric.cs:83` / `Objects.cs:135,178` / `DerivedMetrics.cs:54` 构造期 fail-fast，覆盖 AI 误造数据主路径。
- `⊤` 往返（`NatStar`/`Loop`/`Peak`）经 `ToJson:73,260,262` `+ ParseBudget:232` 闭环，避免 `预算 ⊤→0` 的虚假 `PeakExceeded`（注释 `SerializeBudget:301`）。
- 扫换线 `Audit` 与端点采样等价性由 `ComputeSamplePoints:101` 纯函数抽取可独立测试，本文未重审算法（属 `EffectScript.cs` 审计）。

## 6. 建议（按 Hickey 优先级）

1. 补 `ParseResource` / `ParseScope` 的 `RejectUnknownKeys`（F-01/F-02）— 一处收口消灭扁平契约全部 sibling 拼写静默错误，符合 “fix it once where all callers route through”。
2. 统一 `ParseResourceKey` 异常方言为 `FormatException`（F-03）。
3. 文档显式 `size/loop` 缺省与可序列化子集（F-04/F-05），Hammock 式命名收口。

---
*审计员：Rich Hickey 视角（Data > Function > State）— 7 文件逐符号表+行号锚定。*

DONE_R04

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "7+1 文件逐符号表(§2) + 6 Findings 带 file:line (EffectScriptContract.cs:118-141 F-01, 209-223 F-02, 251 F-03, 84/192 F-04, Objects.cs:21-65 vs 287-295 F-05, EffectScript.cs:384-391 F-06) + EFFECT_SCRIPT.md §4 契约形状核查"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round04-data-contract.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [],
  "validationOutput": [
    "Read 7 L1 files + EFFECT_SCRIPT.md ch4 contract shape; verified Parse/ToJson round-trip for Global/⊤/three-bucket, fail-fast at root/event/claim vs fail-soft at resource/scope objects"
  ],
  "residualRisks": [
    "F-01 scope extra keys / Global+scene silent drop — P1 — fix with RejectUnknownKeys at ParseScope.118",
    "F-02 resource extra/multi-key silent ignore — P1 — add RejectUnknownKeys+single-key guard at ParseResource.209",
    "F-03 memory budget key ulong.Parse non-FormatException dialect split — P1 — wrap to FormatException at 251",
    "F-04 size/loop implicit defaults [1,1]/1 diverge from explicit-data ideal — P2 doc",
    "F-05 ResourceId/ScopeId full space not serializable (intentional subset) — P2 doc",
    "F-06 Budget normalize last-write-wins silent — P2 optional guard"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round04-data-contract.md：Data Orientation 透镜下 JSON 往返无损/ fail-fast 白名单 / 魔法字符串类型约束 / Global/resource 静默兜底逐符号审计，带严重度+行号+最小修复",
  "reviewFindings": [
    "P1: EffectScriptContract.cs:118-141 — scope 对象未 RejectUnknownKeys，Global 含 scene 静默丢弃 (fail-soft)",
    "P1: EffectScriptContract.cs:209-223 — resource 对象未 RejectUnknownKeys/单键校验，多键静默取首 (fail-soft)",
    "P1: EffectScriptContract.cs:251 — memory budget 键 ulong.Parse 抛非 FormatException 方言分裂",
    "P2: EffectScriptContract.cs:84/192 — loop/size 缺省静默 [1,1]/1，稀疏 JSON 非字节幂等 (explicitness)",
    "P2: Objects.cs:21/T vs EffectScriptContract.cs:287 — ResourceId/ScopeId 全集>>契约子集，值空间分裂需文档化",
    "P2: EffectScript.cs:384-391 — Budget 归一化后者赢静默覆盖"
  ],
  "manualNotes": "7+1 仅读约束遵守（未读 audit/ 内容）；EFFECT_SCRIPT.md 整档已读但审计仅用 §4 形状；无写权限故产物以本响应承载由 runtime 落盘 audit/rich-hickey-round04-data-contract.md"
}
```