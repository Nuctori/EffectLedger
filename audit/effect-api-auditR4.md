# EffectScriptContract — JSON 契约人体工学 / 数据形态简单性 独立审计（Rich Hickey 透镜）

> 审计员立场（独立）：JSON 契约视角，判断 AI 面向的 `EffectScriptContract` 是否**简单、无歧义、round-trip 稳定、不会让 AI 写错**。
> 原则：**宁误报「契约歧义」**。所有判定均锚定源码行号（D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScriptContract.cs 等）。
> 隔离声明：未读取 `D:/Godot/Cosmos/audit/` 下任何历史文件；仅依据四个指定源文件 + 实测测试代码静态推导。
> 执行说明：本机 MSBuild/NuGet 工具链损坏（`NuGet.Build.Tasks.WarnForInvalidProjectsTask` 无法加载 `Microsoft.Build.Utilities.v4.0`，与契约代码无关），无法实跑 xUnit。下列结论为**静态逐行推导**，关键 round-trip 判定已用源码对照（Parse↔ToJson）严格证明，并在残余风险中标注「需 CI 实跑复核」。

---

## 0. 契约形态速写（从源码推导）

AI 写出的 JSON（§4）由 `EffectScriptContract.Parse`（L187 起）读入，经类型安全搬运为内部 `EffectScript`，再 `Audit`。契约是**手写 JSON↔record 映射**，无 schema/JSON-Schema 文档，无注释写明的"AI 最小 schema"。

形状骨架（从 `SerializeEvent` L177-185 反推 AI 应写出的形状）：

```
root = { events: [ event ], budget?: { "<key>": <uint64> } }
event = {
  lifetime: [ lo, hi ],          // hi 可为 "⊤"
  scope:    { scene, type? },     // type∈{method,type,global,scene}，缺省=scene
  loop?:    number | "⊤",        // 缺省 1
  footprint: [ claim ]
}
claim = {
  kind:    "read"|"write"|"occupy",
  resource:{ gpu|commandBuffer|memory|occupancy|signalBus : <value> },
  mode:    "use"|"create"|"release"|"move"|"unknown",
  scope:   { scene, type? },      // 必须自带（事件 scope 不兜底）
  size?:   [ lo, hi ]             // 缺省 [1,1]
}
```

---

## 1. 逐字段人体工学判定表

格式：**字段 | 人体工学 | 歧义 / round-trip 证据（行锚）| 严重度**

> 严重度分级：**CRITICAL**（数据丢失 / round-trip 破坏 / 静默猜测，会让 AI 产出被无声改写）、**HIGH**（强歧义，AI 极可能写错）、**MEDIUM**（易错但可调）、**LOW/INFO**（健全）。

| # | 字段 | 人体工学 | 歧义 / round-trip 证据（行锚）| 严重度 |
|---|------|----------|-------------------------------|--------|
| 1 | `root.events` | 必需数组，缺失→`FormatException`："根须含 'events' 数组"（L39）。健全、清晰。 | 仅接受 `JsonValueKind.Array`；根非对象或非数组→抛。fail-fast 好。 | LOW（良好）|
| 2 | `root.budget` | 可选；有则必须是**对象**（L43-44：`bud.ValueKind == JsonValueKind.Object`）。 | 若 AI 写成数组/字符串→被当"没有 budget"（静默忽略，不报错）。非对称：Parse 接受值对象，ToJson 仅在 `Caps.Count>0` 输出（L52）。空 `budget:{}` 会被丢弃（L52 条件跳过）。 | MEDIUM |
| 3 | `event.lifetime` | 必须是 `[lo,hi]` 2 长度数组（L65-70）。`hi` 可为 `"⊤"`（∞）。 | 端点 `"⊤"` 仅 hi 语义合法；**lo 若写 `"⊤"`**→`ParseTop` L78 返回 `NatStar.Top`→lo=∞，但 `Alive`/sweep 中"Lo=⊤ 永不存活"（EffectScript.cs L276），事件被**静默排除**，无错误提示（AI 误写 lo=⊤ 整事件蒸发）。长度≠2→抛（L68）。| HIGH |
| 4 | `event.scope` | 对象 `{scene, type?}`。缺 `scene`→抛（L87）。 | `type` 缺省或未知→一律 `ScopeId.Scene(name)`（L95 默认分支）；`type:"global"`→`Global`（L93）。**Global 无法 round-trip**：`SerializeScope(Global)` 仅输出 `{type:"global"}` 无 `scene`（L192），而 `ParseScope` 要求必含 `scene`（L87）→`global` 事件/claim 经 ToJson→Parse **必抛**（召回即炸），且即便补 scene 也只映射回 `Scene` 而非 `Global`。**双缺陷**。| CRITICAL |
| 5 | `event.loop` | 可选，缺省 `LoopCount.Of(1)`（L56-57）。`"⊤"`=∞。 | 缺省 1 **静默**：AI 漏写 loop 即被当作单实例（可能非预期，但语义上"单实例"是合理的默认）。`loop:0`（测试 SampleJson 第 2 事件，L?测试）合法→ω=0（零副本，等价"不出现"），但契约注释未说明 0 语义，AI 易写出 `loop:0` 以为"瞬态"。 | MEDIUM |
| 6 | `event.footprint` | 必需，必须是 **claim 数组**（L108-110）。 | 非数组→抛。健全。每个 claim 经 `ParseClaim`（L116）。 | LOW（良好）|
| 7 | `claim.kind` | `"read"|"write"|"occupy"`（L126-129）。 | **大小写敏感**：`ParseKind` 仅匹配小写（L126）。AI 写 `"Occupy"`/`"OCCUPY"`→`FormatException:"未知 kind"`（L129）。ToJson 输出小写（L196 `ToLowerInvariant`）。非对称但单向宽容（只吃小写），AI 易错。**与 resource.value 缺省宽松形成反差**。| HIGH |
| 8 | `claim.resource` | **必须是对象** `{gpu|commandBuffer|memory|occupancy|signalBus : v}`（L141-145）。未知键（如 `"alien"`）→抛（L145 触发 L151 "resource 形状非法"；测试 `Json_UnknownResource_Throws` 覆盖）。 | **"memory 是数字还是对象"歧义实锤**：`resource` 整体必须是对象，但 `memory` 的值必须是 **uint64 数字**（L148：`mem.GetUInt64()`）——AI 看到 `{"gpu":"x"}`、`{"commandBuffer":"gpu"}` 都是字符串，容易把 memory 也写成 `{"memory":"0"}` 字符串→`GetUInt64` 抛 `FormatException`（数字期望不符），**错误信息来自 System.Text.Json 而非契约**，AI 难定位（错误不含"memory 须数字"）。| HIGH |
| 9 | `resource.gpu` | 字符串（Rid 值），L146。 | 缺值 `{"gpu":null}`→`?? ""` 得空串（L146），**静默**成 `Gpu("")` 而非报错。空串合法但语义模糊。 | MEDIUM |
| 10 | `resource.commandBuffer` | 字符串（channel），L147。 | **缺值静默默认 `"gpu"`**（L147：`cb.GetString() ?? "gpu"`）。AI 漏写 value→被静默改成 commandBuffer 通道 "gpu"，与 SampleJson 全剧 `{"commandBuffer":"gpu"}` 一致，但**任何拼写/漏写都被吞成 "gpu"**，无提示。 | CRITICAL |
| 11 | `resource.memory` | **数字** uid，L148。 | **类型容忍反向**：`mem.ValueKind != Number`→静默返回 `Memory(0)`（L148：`else 0`）。即 `{"memory":null}`/`{"memory":"x"}`/`{"memory":{}}` 全部**静默变 Memory(0)**。与 gpu/commandBuffer 缺值取字符串默认不同，memory 缺值取 0 且不报。极隐蔽的静默改写。| CRITICAL |
| 12 | `resource.occupancy` | 字符串（channel），L149。 | 缺值 `?? ""`（L149）→`Occupancy("")`。静默。 | MEDIUM |
| 13 | `resource.signalBus` | 字符串（StringName），L150。 | 缺值 `?? ""`→`SignalBus("")`。静默。 | MEDIUM |
| 14 | `resource.*` 多键 / 未知键 | 只取第一个命中的键（L146-150 顺序 if）；未命中→L145 抛。 | 多键（如同时 `gpu` 和 `memory`）**安静取第一**（gpu 优先），多余键被忽略，无警告。未知键（非 5 种）→L145 触发抛（L151）。 | MEDIUM |
| 15 | `claim.mode` | `"use"|"create"|"release"|"move"|"unknown"`（L132-136）。 | 大小写敏感（L132 仅小写）。`"Use"`→`FormatException:"未知 mode"`（L136）。ToJson 输出小写（L197）。与 kind 同问题。 | HIGH |
| 16 | `claim.scope` | **必须自带**（L121：`Require(c,"scope")`），事件 scope **不兜底**。 | 缺→`FormatException:"缺少字段: scope"`（L234）。**比事件 scope 更 strict**：事件 scope 缺则抛（L57），但 claim scope 缺也抛——**双重要求**。AI 自然以为"事件有 scope 就行"，会在 claim 漏写 scope→被拒。与 ToJson 一致（每个 claim 都输出 scope，L200），故此处"意外 strict"**不是 round-trip 破坏**，而是**与 AI 直觉冲突的隐藏必填**。| HIGH |
| 17 | `claim.size` | 可选 `[lo,hi]`，缺省 `[1,1]`（L122-123，经 `Interval.Default`）。 | 缺省 `[1,1]` 是**双侧默认**：AI 漏写 size 即被当作精确 1 单位。对 occupy 守恒（net）影响重大——漏写 size 的 create 仍贡献 net=1，可能引入 AI 想不到的 Leak/Peak。非对称：Parse 宽松（缺省 1），ToJson 总输出（L201）。| MEDIUM |
| 18 | `budget` 键 | **秘密语法**：`"gpu:..."` / `"commandBuffer:..."` / `"memory:..."` / `"occupancy:..."` / `"signalBus:..."`（L165-172）。值为 uint64（L160）。 | 键是**带前缀的字符串**，与 claim 里 `resource` 是**对象**的形状**完全不一致**：AI 在 claim 写 `{"gpu":"mesh1"}`，在 budget 却要写 `"gpu:mesh1"`。两处表达同一资源，形态天差地别（对象 vs 前缀字符串），且无文档。**最易出错的隐藏语法**。未知前缀→抛（L172）。| HIGH |
| 19 | `budget` 值 | uint64（L160 `GetUInt64`）。 | 非数字→System.Text.Json 原生 `FormatException`（无契约友好信息）。 | MEDIUM |
| 20 | `scope.scene` | 字符串 name，L89。 | 缺→L88 抛。健全。 | LOW |
| 21 | `scope.type` | 可选，∈`method|type|global|scene`，缺省 `scene`（L91-95）。 | 未知 type→**静默降级为 Scene**（L95 `_ => new ScopeId.Scene(name)`），无警告。AI 拼写 `type:"scne"` 不会报错，只被当 scene。 | HIGH |
| 22 | `scope` 的 global 表示 | 见 #4。 | ToJson：`{type:"global"}`（无 scene）；Parse：要求 scene 且 type=`global`→Global，但**Global 永远无法被 Parse 重建**（无 scene 抛；有 scene 也只回 Scene）。 | CRITICAL |

### 魔法字符串 / 符号专项

| 符号 | 含义 | 人体工学 | 证据 | 严重度 |
|------|------|----------|------|--------|
| `"⊤"` | 上界 ∞（lifetime.hi / loop / size 端点）| **Unicode 数学符号（U+22C4）**，非 ASCII。AI/编辑器/剪贴板极易写成 `top`/`"inf"`/`"∞"`/`"*"`。 | `ParseTop` L78、`ParseLoop` L102 仅匹配精确字符串 `"⊤"`；写 `"top"`→抛（L82/L104）。测试 SampleJson 用 `"⊤"`（loop），但**无任何文档说明必须是该字符**。ToJson 输出 `"⊤"`（L178/L180/L201）。 | HIGH |
| `"gpu"/"commandBuffer"/"memory"/"occupancy"/"signalBus"` | resource 键 | 5 个固定键，拼写敏感；缺/错→抛或静默（见上）。`commandBuffer` 是 camelCase，其余也是，但 AI 易写 `command_buffer`（§7 白名单用 `command_buffer`，契约用 `commandBuffer`——**两套命名并存，跨层歧义**）。 | L143-145、`Objects.cs` Normalize 注释提到 `command_buffer ⇒ CommandBuffer("gpu")`（§7 形态），与契约 camelCase 不一致。 | HIGH |

---

## 2. 错误信号（fail-fast 评估）

**整体：fail-fast 基调好，但存在"静默猜测"违规点。**

- 形状错误（根非对象、events 非数组、event 非对象、lifetime 长度≠2、resource 非对象、未知 kind/mode/resource/budget 键、缺必填字段）→一律 `FormatException`（L39/L55/L68/L73/L82/L104/L129/L136/L145/L151/L172/L234）。**干净、可定位**。
- **静默改写（违反 fail-fast 原则）的点**（会让 AI 以为写对了，实际被改）：
  1. `commandBuffer` 缺值→静默 `"gpu"`（L147）。
  2. `memory` 类型错→静默 `Memory(0)`（L148 `else 0`）。
  3. `gpu/occupancy/signalBus` 缺值→静默空串（L146/149/150 `?? ""`）。
  4. `scope.type` 未知→静默降级 Scene（L95）。
  5. `lifetime.lo = "⊤"`→静默永不存活（无报错，事件蒸发）。
  6. 多 resource 键→静默取第一（L146-150）。
  7. 空 `budget:{}` 或 `budget` 为非对象→静默忽略（L43-44/L52）。
- **错误信息不够 AI 回修**（要求 #4）：多数 `FormatException` 文案是中文短语（"resource 须含...之一"、"scope 须为..."），但 **`memory` 数字期望失败、`budget` 值数字失败**走的是 `System.Text.Json` 原生异常（`GetUInt64` / `GetString`），不含契约字段名与"应写数字/字符串"的指引 → AI 难定位（证据：L148/L160 无 try-catch 包装）。

---

## 3. round-trip 专项（ToJson → Parse 无损性）

已知"read/write/occupy 三桶"已在 auditR3b 修复（L181-185 注释 OPEN-2），三桶均序列化，Parse 按 `claim.kind` 路由回三桶 → **三桶对称，已收敛**。

**仍然丢字段 / 破坏 round-trip 的点：**

1. **CRITICAL — `scope == Global` 不可 round-trip（双缺陷）**：
   - `SerializeScope(Global)`（L192）仅输出 `{ "type": "global" }`，**无 `scene`**。
   - `ParseScope`（L87）要求 `scene` 必含，否则抛 → 任何含 Global 的脚本 ToJson 后再 Parse **必抛 FormatException**。
   - 即便放宽（补 scene），`ParseScope` 的 `type:"global"` 分支（L93）确实产 Global；但 `SerializeScope` 不输出 scene，且 Global 与 Scene 在 `SerializeClaim` 都走同一字典构造 → **Global 经 ToJson 实际上无法被还原为 Global**（缺 scene 直接炸；即使修 scene，SerializeGlobal 也不输出 scene，需同时改两处）。
   - 这是契约内**唯一的结构性 round-trip 破坏**（非静默丢，是炸）。

2. **CRITICAL — `resource` 缺值/类型错→静默改写，round-trip 不等价**：
   - `commandBuffer` 缺值→`"gpu"`；`memory` 类型错→`0`；gpu/occ/signalBus 缺值→`""`。这些经 ToJson 会**原样输出**被改写后的值（如 `"commandBuffer":"gpu"`、`"memory":0`），Parse 再读得到的是"改写后"的资源，与 AI 原始意图不同。**无损性被破坏（静默而非炸）**。

3. **MEDIUM — `budget` 空对象被丢弃（L52 条件 `Caps.Count>0`）**：`Budget.None`（空）不输出 budget；Parse 空 `budget:{}` 也被当 None。单向无害，但 `budget:{}` 显式意图（"有预算但全无上限"）与 `budget` 缺失不可区分。

4. **其余字段 round-trip 对称**：lifetime/scope(scene/method/type)/loop/kind/mode/resource 正常键/size 均双向一致（ToJson 输出即 Parse 所吃）。

---

## 4. 总评

### 契约是否简单、无歧义？
**否。** 契约在"形状严格性"上**不一致**：
- 对 `kind/mode/scope.type/budget 键` 极度严格（大小写敏感、缺则炸）；
- 对 `resource 的值`（gpu/commandBuffer/memory/occupancy/signalBus 缺值或类型错）却**静默猜测**（默认串/0/空串），且 `memory` 在"resource 必须是对象"的大前提下要求值是**数字**——这是最反直觉的坑（"memory 是数字还是对象？"的担心**已成事实**：memory 的值必须是数字，且类型错还被静默吞成 0）。
- `commandBuffer` 在 §7 白名单叫 `command_buffer`、契约叫 `commandBuffer`，**两套命名并存**。
- 上界符 `"⊤"` 是 Unicode 数学符号，无 ASCII 别名，AI 极易拼错。
- `budget` 键是**前缀字符串语法**（`gpu:mesh1`），与 claim 内 `resource` 的**对象语法**（`{"gpu":"mesh1"}`）表达同一资源却形态迥异。
- **唯一硬 round-trip 破坏**：`scope == Global` 双缺陷（Serialize 不输出 scene → Parse 必炸；且 Global 永不还原）。

结论：**契约会让 AI 写错**——尤其在 resource 值类型、commandBuffer 默认、memory 静默 0、budget 键前缀、`⊤` 符号、claim scope 必填、Global 不可往返这 7 处。

### Top 3 应改的契约点（按"让 AI 写错"的风险排序）

1. **[CRITICAL] 修复 `scope == Global` round-trip 破坏**：`SerializeScope(Global)` 必须输出可被 `ParseScope` 重建的形态（建议 Global 同时输出 `scene` 占位或显式 `{"type":"global"}` 且让 `ParseScope` 在 `type:"global"` 时不强制 scene）。当前任何含 Global 的剧本 ToJson→Parse 必炸，是契约级正确性 bug。
2. **[CRITICAL] 消除 resource 值的静默猜测**：`commandBuffer` 缺值默认 `"gpu"`（L147）、`memory` 类型错静默 `0`（L148）、gpu/occ/signalBus 缺值 `""`（L146/149/150）全部改为 **fail-fast**（`FormatException` 指明"resource.X 须为字符串/数字"）。静默改写比报错更危险——AI 以为写对了。
3. **[HIGH] 统一 resource 表达形态 + 提供「最小 schema 文档」**：`budget` 键的前缀字符串语法（`gpu:...`）与 claim 内 `resource` 对象语法不一致；`commandBuffer` 跨层命名（`command_buffer` vs `commandBuffer`）不一致；`memory` 值须数字却藏在"resource 必为对象"规则下；`⊤` 无 ASCII 别名。**强烈建议给 AI 一份最小 schema 文档**（含：每个字段类型、必填/可选、允许枚举值、上界符 `⊤` 约定、resource 各键的值类型表、budget 键前缀规则、claim scope 必须自带）。该文档可消除约 70% 的"AI 写错"面。

### 是否该给 AI 一份最小 schema 文档？
**是，强烈建议。** 当前契约是"代码即文档"，但对 AI 而言缺少：
- 字段类型表（尤其 `resource` 是对象但 `memory` 值是数字、`budget` 键是前缀字符串）；
- 枚举白名单（`kind`/`mode`/`scope.type` 仅小写，无 `top`，无 `command_buffer`）；
- 上界符 `"⊤"` 的明确约定（Unicode U+22C4，非 `top`/`inf`/`∞`）；
- `claim.scope` 必须自带（事件 scope 不兜底）；
- `loop:0`、`size` 缺省 `[1,1]`、`memory` 静默 0 等隐藏默认语义。
一份 1 屏的 JSON Schema / 示例 + 字段表，能把契约从"会写错"降至"基本可写对"。

---

## 5. 证据锚（源码行号）

- 根/events：L39；budget 可选对象：L43-44, L52。
- lifetime 数组与 `⊤`：L65-82；lo=⊤ 永不存活：EffectScript.cs L276。
- scope 解析与 Global 缺口：L85-95（要求 scene，type:global→Global）；SerializeScope Global：L192（无 scene）。
- loop 默认 1 / `⊤`：L56-57, L102-104。
- footprint 数组：L108-110。
- kind 小写敏感：L126-129；mode 小写敏感：L132-136。
- resource 必对象+5 键：L141-145；gpu `?? ""` L146；commandBuffer `?? "gpu"` L147；memory 数字/`else 0` L148；occupancy `?? ""` L149；signalBus `?? ""` L150；多键取第一 L146-150。
- claim.scope 必填（事件不兜底）：L121, L234。
- size 默认 [1,1]：L122-123（`Interval.Default`）。
- budget 键前缀语法 + uint64：L159-172, L160。
- 三桶 round-trip 修复（OPEN-2）：L181-185 注释。
- ToJson 输出（含 scope/resource/claim/size 对称）：L177-227。
- 跨层命名不一致：`command_buffer`（Objects.cs Normalize 注释 §7）vs `commandBuffer`（契约 L143/L222）。

---

## Acceptance 证据（审计产物）

- 交付文件：仅 `D:/Godot/Cosmos/audit/effect-api-auditR4.md`，**未改任何 .cs**（只读审计）。
- 测试：因本机 MSBuild/NuGet 工具链损坏（`NuGet.Build.Tasks.WarnForInvalidProjectsTask` 加载失败，与契约代码无关），**未能实跑** `EffectScriptContractTests`；结论为四源静态逐行推导，关键 round-trip（Global 双缺陷、resource 静默改写）已按 Parse↔ToJson 源码对照严格证明。
- 残余风险：需在健康 CI 上实跑 `EffectScriptContractTests` + 新增一个"Global scope round-trip"用例与一个"resource 缺值/memory 类型错应抛而非静默"用例以坐实 CRITICAL 判定。
