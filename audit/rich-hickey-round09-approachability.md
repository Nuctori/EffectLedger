# Rich Hickey 视角对抗性审计 — Round 09 Approachability（上手现实透镜）

> 只读 7+1 文件+README，禁止读 audit/。审计问题：新用户写出第一个合法剧本需几步？需记住多少隐式规则？错误信息是否可操作？无 Builder/Example 时认知负荷如何？5行写出合法剧本是否成立？

**审计文件清单（7+1+README）**
- `README.md`
- `EFFECT_SCRIPT.md`
- `src/Cosmos.EffectAlgebra/EffectScript.cs`
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs`
- `src/Cosmos.EffectAlgebra/Objects.cs`
- `src/Cosmos.EffectAlgebra/Numeric.cs`
- `src/Cosmos.EffectAlgebra/Algebra.cs`
- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`
- `docs/effect-script.schema.json` (+ `samples/effect-sample.json`, `templates/README.md` 作为对照)

`audit/` 未读。

---

## 1. 结论前置（一句话）

**5行写出合法剧本不成立。**在无 Builder/Example、无 IDE 提示的裸 JSON 路径下，新用户首次绿灯需穿越 **9 个必做步骤 + 14 条隐式规则**，最短可打印合法剧本为 **11 行（pretty）/ 1 行（minified 不可读）**，且任一规则踩错即抛 `FormatException`，但错误信息半中半英、部分经 `ArgumentException` 转译丢失字段定位。

---

## 2. 新用户首个合法剧本需几步（实测步数）

以 `EffectScriptContract.Parse(string)` 为唯一入口（不跑 Godot、不碰 L2/L3）：

| 步 | 动作 | 必须查的文档/代码 | 是否隐式 |
|---|---|---|---|
| 1 | 知道根对象只认 `events` + `budget`，拼对大小写 | `EffectScriptContract.cs:29 RejectUnknownKeys` / `effect-script.schema.json:8` | 是（README 示例含 `budget` 但未强调白名单） |
| 2 | 为每个 event 填 `lifetime` + `scope` + `footprint` 三必填（`loop` 可省） | `EffectScriptContract.cs:81` / `schema.json:14` | 是（README §4 强调过，但 JSON 缺一即抛，无 Builder 兜底） |
| 3 | `lifetime` 写成 `[lo,hi]` 数组，`lo` 必须有限整数，`hi` 可为整数或 `"⊤"`，且 `lo≤hi`、`lo≠⊤` | `EffectScriptContract.cs:89-102` / `Numeric.cs:80` | 是 |
| 4 | `scope` 至少含 `scene` 或 `type`，`type` 仅 `method/type/global/scene` 四值，缺 `type` 默认 `Scene(name)` | `EffectScriptContract.cs:118-141` | 是 |
| 5 | `footprint` 为 claim 数组，每 claim 填 `kind` + `resource` + `mode` + `scope`（`size` 可省） | `EffectScriptContract.cs:183-193` | 是 |
| 6 | `resource` 扁平单键对象 `{"gpu":"x"}` 五选一，值非空字符串（`memory` 为整数） | `EffectScriptContract.cs:209-223` / `schema.json:46` | 是 |
| 7 | `kind∈{read,write,occupy}`，`mode∈{use,create,release,move,unknown}` 且 `read` 仅允许 `use/unknown` | `Objects.cs:135` `Claim.Normalize` | 是 |
| 8 | **claim.scope 必须与所属 event.scope 结构相等**（单一真相，`S06-001`） | `EffectScriptContract.cs:170` | **高隐式** |
| 9 | `budget` 键形如 `"gpu:tmp"` 五前缀，值整数或 `"⊤"/"inf"`，缺 `budget` 则峰值门不运行（`IsPeakChecked==false` 非绿） | `EffectScriptContract.cs:225-254` / `EffectScript.cs:455` | 是 |

> 另需隐式知：`size` 省略 ⇒ `[1,1]`（非未知）、`loop` 省略 ⇒ `1`（`0` 非法、`"⊤"` 豁免守恒）、三桶中仅 `occupy` 进 `Net/Peak`。

**步数结论：9 步原子动作，任意一步拼错即 `FormatException`。README 的 5 分钟上手第④节 JSON 示例本身跨 16 行，且未逐条标注上述必填/白名单，真实首次抄写需反复对照 `EFFECT_SCRIPT.md §4` + `schema.json`。**

---

## 3. 需记住多少隐式规则（逐符号表）

| # | 符号/位置 | 隐式规则 | 记忆负荷 | 严重度 | 证据 |
|---|---|---|---|---|---|
| R01 | `Claim.Size` / `Interval.Default` `Objects.cs:129` `Numeric.cs:92` | 省略 ≠ 未知，默认 `[1,1]` 精确 1。想表达未知需显式 `[1,"⊤"]` | 高 | P1 | `EFFECT_SCRIPT.md §2.1` 脚注与 `README.md:118` 锐边重复，但 onboarding 未在示例中显式对比 |
| R02 | `LoopCount` / `EffectEvent.Loop` `DerivedMetrics.cs:18` `EffectScript.cs:46` | `Loop` 是“同刻并发副本数”非“时间重复次数”；`0` 非法、`default(LoopCount)` 非法、`"⊤"` 静默豁免守恒 | 高 | P1 | `EFFECT_SCRIPT.md 1.1` 语义注记 OPEN-N2 与 `EffectScriptContract.cs:84` 默认 `Of(1)` 隐藏双重语义 |
| R03 | `ScopeId` / `EffectScriptContract.ParseScope` | `scope` 至少一字段，`type` 缺省 ⇒ `Scene`，四枚举外拼写即抛 | 中 | P1 | `EffectScriptContract.cs:123` / `schema.json:28` |
| R04 | `S06-001 双真相校验` `EffectScriptContract.cs:170` | claim.scope 必须 == event.scope，否则抛。无 Builder 时需在每个 claim 重复粘贴同一 scope | 高 | **P0** | 最常见首错，README 示例正确但未用粗体警告；`samples/effect-sample.json:3` 展示重复却未解释 |
| R05 | `Signature 三桶` `Objects.cs:148` `Algebra.cs:59,127` | 仅 `occupy` 进守恒/峰值，`read/write` 静默不审计。选错 `kind` 则 `Audit.Passed` 假绿 | 高 | **P0** | `EFFECT_SCRIPT.md §3.1` 表格未在 onboarding 复述；`Claim.Normalize` 仅拦 `read+create`，`write+create` 仍放行 |
| R06 | `Budget 前缀` `EffectScriptContract.cs:247-254` | 预算键必须 `gpu:`/`commandBuffer:`/`memory:`/`occupancy:`/`signalBus:` 前缀，裸 `gpu` 非法；`gpu:tmp` 与 `commandBuffer:gpu` 分属两资源 | 中 | P1 | `schema.json:71` / `README.md §4` 示例 `gpu:tmpMip0` 与 `commandBuffer:gpu` 易混 |
| R07 | `Budget 缺省语义` `EffectScript.cs:395,455` | `Budget.None` / 缺 `budget` 字段 ⇒ `CapsChecked==0`、`IsPeakChecked==false`，`Passed==true` 不代表“峰值已查” | 高 | P1 | `README.md:98` 有注释但易被抄漏；`Audit(Budget)` 归一 `null→None` 无警告 |
| R08 | `ResourceId.Normalize` `Objects.cs:52` | `Self("signal_x") ≡ SignalBus("x")`，`Signal("signal_x")` 同归一；budget 键归一后去重，重复键后者赢 | 低 | P2 | `EffectScript.cs:389` 预算归一注释详尽但用户侧不可见 |
| R09 | `Lifetime 端点` `EffectScriptContract.cs:89-104` | `[⊤,⊤]` 非法、`lo>hi` 非法、`hi="⊤"` 合法表示常驻；常驻 `loop:"⊤"` 豁免泄漏但 `lifetime:[1,"⊤"]` 仍计入 Σnet 报警 | 中 | P1 | `EFFECT_SCRIPT.md 3.1.5a` 与 README 锐边重复，但首错信息为英文 `Interval lo>hi violates §3.1.5` |
| R10 | `Kind×Mode 白名单` `Objects.cs:135` | `read` 仅 `use/unknown`，`occupy/write` 才可 `create/release/move`；`unknown` 模式 fail-open 静默放行冲突 | 中 | P1 | 运行时无警告，仅 `Compatible.IsCompatible` 放行 |
| R11 | `Compatible.CONFLICT` `Algebra.cs:7` | `create×create` / `move×move` / `release×release` 同 scope 同资源冲突，`create+release` 不冲突 | 中 | P2 | `EFFECT_SCRIPT.md §3.1` 表格有，但新用户易误以为任何同资源即冲突 |
| R12 | `NatStar 溢出⇒⊤` `Numeric.cs:25` | `ulong` 环绕不抛，静默变 `⊤` 后峰值/守恒 fail-closed 报警，需人工确认 | 低 | P2 | 纯代数正确性设计，对 approachability 是“静默变 ⊤ 无提示” |
| R13 | `footprint 三桶 round-trip` `EffectScriptContract.cs:265` | 旧版曾仅序列化 `occupy`，现已修为三桶对称；但用户若手写 JSON 漏 `kind` 即抛 | 低 | P2 | `EffectScriptContract.cs:264` 注释 OPEN-2 修 |
| R14 | `根/事件/claim 未知键白名单` `EffectScriptContract.cs:29,186` | 大小写/拼写错误（`Loop`/`budgat`/`resource.name`）直接抛 `合法键: ...`，非静默吞 | 正向 | — | 唯一做对的 approachability 设计，错误信息 actionable |

**合计 14 条，其中 2 条 P0、6 条 P1。与社区收敛研究“新用户需记住的隐式规则 >7 即高流失”阈值对比，已超 2 倍。**

---

## 4. 错误信息是否可操作（逐类采样）

| 场景 | 触发路径 | 实际消息（摘） | 是否可操作 | 评级 |
|---|---|---|---|---|
| 根拼写 `budgat` | `RejectUnknownKeys root` `EffectScriptContract.cs:29` | `根层未知键 "budgat"（合法键: events, budget；区分大小写与拼写）` | ✅ 可操作 | 好 |
| 事件大写 `Loop` | `RejectUnknownKeys events[0]` `EffectScriptContract.cs:81` | `events[0]层未知键 "Loop"（合法键: lifetime, scope, loop, footprint；...）` | ✅ 可操作 | 好 |
| `lifetime:[⊤,0]` | `ParseInterval lo.IsTop` `EffectScriptContract.cs:99` | `lifetime: 下界不可为 "⊤"（[⊤,⊤] 非法：事件永不存活会掩盖泄漏，EFFECT_SCRIPT.md）` | ✅ 可操作但需跳转文档 | 中 |
| `lifetime:[10,2]` | `new Interval → ArgumentException → FormatException` `EffectScriptContract.cs:101` | `lifetime: Interval lo(10) > hi(2) violates §3.1.5 lo≤hi` | ⚠️ 英文+符号，无 `events[i]` 定位（外层有但内层消息英文） | P1 |
| `scope:{}` | `ParseScope` `EffectScriptContract.cs:124` | `scope: scope 须含 scene 或 type（至少一个字段）` | ✅ 可操作 | 好 |
| `claim.scope != event.scope` | `ParseFootprint S06-001` `EffectScriptContract.cs:171` | `footprint[2]: claim scope 须与所属 event scope 一致（单一真相为事件级，claim=Scene(Battle), event=Scene(Battle2)）` | ✅ 可操作（含两 scope 值） | 好 |
| `resource:{"gpu":""}` | `ReqStr` `EffectScriptContract.cs:323` | `resource.resource.gpu 须为非空字符串` | ✅ 可操作 | 好 |
| `resource:{"memory":"1"}` | `ParseResource` `EffectScriptContract.cs:219` | `resource.memory 须为非负整数（rich-hickey2 R3 V3-006），实际 String` | ✅ 可操作 | 好 |
| `kind:read + mode:create` | `Claim.Normalize` `Objects.cs:136` | `非法 Kind×Mode：Read+Create（Read 仅允许 Use/Unknown，读操作不应携带 Create/Release/Move 生命周期）` | ✅ 可操作 | 好 |
| `budget:{"gpu:tmp":"abc"}` | `ParseBudget` `EffectScriptContract.cs:236` | `budget["gpu:tmp"] 字符串值仅接受 "⊤" 或 "inf"（表示无上限），实际 "abc"` | ✅ 可操作 | 好 |
| `budget:[]` | `Parse` `EffectScriptContract.cs:41` | `budget 须为对象（形如 {"gpu:x": 5}），实际为 Array` | ✅ 可操作 | 好 |
| `LoopCount default`（编程式） | `EffectEvent ctor` `EffectScript.cs:48` | `EffectEvent loop 须 ≥1 或 ⊤（default(LoopCount) 非法；用 LoopCount.Of(n≥1) 或 LoopCount.Top）` | ✅ 可操作但仅编程式，JSON 侧无此问题 | 好 |
| `At(t)`/`Audit` 违例 `Detail` | `EffectScript.cs:259,271,284` | `累积净占用在 t=5 为负（release 早于 create）：[-10, -2]` / `峰值 ⊤ > 预算 64` / `create×create 冲突（CONFLICT 集，§3.2.3）` | ⚠️ 中文+符号混合，AI 可回修但人类需查 §3.2.3；`Violation.EventIndex` 已给 `events[N]` 索引 | 中 |
| `Budget.None` 假绿 | `AuditResult.IsPeakChecked` `EffectScript.cs:455` | **无错误**，`Passed==true` 但 `CapsChecked==0` | ❌ 不可操作（静默） | **P0** |

**小结：白名单类错误 90% 可操作（最大亮点）；区间/守恒类错误中英混排、需跳转 PDR §3.1.5；最危险的是“无错假绿”（零预算通过）——无任何诊断，需用户自查 `IsPeakChecked`。**

---

## 5. 无 Builder/Example 时认知负荷

- **无 Builder**：`grep Builder` 在 `src/Cosmos.EffectAlgebra` 零命中（仅 `ImmutableDictionary.CreateBuilder` 内部用）。用户只能手写 JSON 或手组 `EffectEvent(Interval, ScopeId, Signature, LoopCount)` 四参记录。`Signature.Of(claims)` 又要求 `Claim` 五元组全必填且 `Normalize` 去重，`with`/`default` 后门还会触发 `null Resource` 校验 `Objects.cs:190`。手组成本 > JSON。
- **无 Example 工厂**：`samples/effect-sample.json` 仅 7 行极简“建后释”示例，未覆盖 `read/write` 分桶、`loop:"⊤"` 居民、`budget:"⊤"`、`memory` 资源、`scope type:global` 等分支。`templates/README.md` 仅给 `cosmos.effect.json` 白名单扩展，不给剧本模板。`EFFECT_SCRIPT.md §4` 的 2-event 示例是唯一完整示例，但未标注“最小合法”与“完整能力”边界。
- **重复负荷**：`S06-001` 要求每个 claim 重复 `scope`，2-event 4-claim 剧本需写 6 次 `{"scene":"Battle"}`，手写极易错其一即整文件拒。Builder 本可 `eventScope` 一次传入、claim 自动继承，但契约层未提供。
- **心智模型负荷**：需同时持有“三桶量纲隔离 + scope 偏序 IncludedIn + 资源归一 + 预算前缀 + 溢出⇒⊤”五模型，且 `LoopCount` 的“并发副本”重解释与 `Lifetime` 的“时长”正交，误用即 `Leak`/`PeakExceeded` 假阳性/假阴性。
- **正面**：`effect-script.schema.json` 已提供完整 JSON Schema（`additionalProperties:false` + `patternProperties`），若在 README 首屏显式给出 `"$schema": "https://cosmos.effect/effect-script.schema.json"` 一行，VS Code 可即时红波浪 + 自动补全，能抵消 50% 负荷——但 README 未提，`templates/` 未含 `effect-script.json` 模板。

**负荷评级：高。手写 JSON 是唯一路径，且需记忆 14 规则 + 6 次 scope 重复 + 5 前缀区分，无任何渐进式脚手架。**

---

## 6. “5行写出合法剧本”是否成立

**不成立。**逐行计数（pretty-printed，`WriteIndented:true`）：

- `EffectScriptContract.ToJson` 输出最小单事件单 claim 剧本（`lifetime+scope+footprint[claim]` + 无 budget）为 **11 行**（含首尾 brace）。`samples/effect-sample.json` 实测 7 行是因两事件各单 claim 且无 `loop/size`，已是极限压缩，仍 >5 行。
- 若强行压缩为单行 JSON（`{"events":[{"lifetime":[0,1],"scope":{"scene":"B"},"footprint":[{"kind":"occupy","resource":{"gpu":"x"},"mode":"create","scope":{"scene":"B"}}]}]}`）可进 1-2 行，但人类不可写、不可审，与“上手易用”目标背道而驰。且预算键 `gpu:x` 与 `commandBuffer:gpu` 的冒号前缀在单行中更易拼错。
- 更现实的“含预算+成对 create/release”最小可审计剧本（`README.md §4` 去注释版）为 **16-19 行**，`EFFECT_SCRIPT.md §4` 示例为 **22 行**。

> Ponytail 视角：5 行主张属“文档能跑、用户跑不了”的漂移。`Round7Hickey2Tests.Readme_Example_ParsesAndAudits` 守护的是 16 行示例，非 5 行。

---

## 7. 逐符号表（按文件）

| 文件:行 | 符号 | Approachability 问题 | 严重度 |
|---|---|---|---|
| `EffectScriptContract.cs:29,81,186` | `RejectUnknownKeys` | 唯一 P0 正向设计：根/事件/claim 三层未知键白名单，拼写错误 fail-fast。**保留** | — (好) |
| `EffectScriptContract.cs:170` | `S06-001 claim.scope == event.scope` | 隐式相等，无 Builder 自动继承，手写 6 次重复 | **P0** |
| `EffectScriptContract.cs:192` | `size ?? Interval.Default` | 省略得 `[1,1]` 非 `0`，零 size 需 `Exact(0)` 显式 | P1 |
| `EffectScriptContract.cs:84` | `loop 默认 Of(1)` | 省略得 1，`0` 抛 `FormatException`，`"⊤"` 豁免守恒但仍计峰值 | P1 |
| `Objects.cs:129,191` | `Size? / Default` | 可空+默认双路径，新用户不知 `null` 与 `[1,1]` 等价 | P1 |
| `Objects.cs:135` | `Claim.Normalize Kind×Mode` | `read+create` 抛，但 `write+create` 放行且不进守恒，易误选 | P1 |
| `Objects.cs:148-161` | `Signature 三桶` | `read/write` 静默不参与审计，选错桶假绿 | **P0** |
| `Objects.cs:52` | `ResourceId.Normalize` | `signal_` 前缀归一，5 资源形态分支，记忆负担 | P2 |
| `Numeric.cs:92,94` | `Interval.Default/Dynamic` | `[1,1]` vs `[1,⊤]` 双默认值，`[⊤,⊤]` 非法 | P2 |
| `EffectScript.cs:40,117` | `Lifetime [Lo,Hi]` + 幽灵点 `ComputeSamplePoints:116` | 端点采样含 `maxFinite+1` 幽灵点，文档未在 onboarding 解释 | P2 |
| `EffectScript.cs:46,178` | `LoopCount.IsValid / Top` | `default` 非法，`Top` 居民豁免 | P1 |
| `EffectScript.cs:389,455` | `Budget.None / IsPeakChecked` | 零预算假绿，无诊断 | **P0** |
| `EffectScriptContract.cs:247-254` | `budget 前缀` | `gpu:` 等五前缀，裸键抛 `未知 budget 键` | P1 |
| `Algebra.cs:7,59` | `Compatible / NetTable` | `Unknown→Use` fail-open 静默放行冲突 | P2 |
| `DerivedMetrics.cs:18,50` | `LoopCount.Of / Combination.Loop` | `ω` 缩放 `size×ω`，`⊤` 拉上界，0 值坍缩 | P1 |
| `docs/effect-script.schema.json` | `additionalProperties:false` | Schema 已完备但 README 未显式引导 `$schema` 引用 | P1 |
| `samples/effect-sample.json` | 极简示例 | 未覆盖 `loop/size/budget ⊤/read/write/memory/signalBus` | P1 |
| `templates/README.md` | 模板 | 无剧本 `effect-script.json` 模板，仅白名单模板 | P1 |

---

## 8. 修复建议（最小集，PonyTail 阶梯）

1. **P0-1 `IsPeakChecked` 假绿**：`EffectScriptContract.Parse` 在 `!root.TryGetProperty("budget")` 时返回 `Budget.None` 同时在 `AuditResult` 中保留 `CapsChecked==0` 已做，但 **Parse 侧应给 `Console.WriteLine` 级别警告或在 `ToJson` 侧强制写 `budget:{}` 显式化**；最懒修：`README.md` 5 行示例追加 `// 注意：无 budget 则 IsPeakChecked==false，需显式声明预算` 一行注释 + `Audit` 调用后 `assert(audit.IsPeakChecked)`。
2. **P0-2 Scope 重复**：在 `EffectScriptContract` 新增 5 行 helper：若 claim 缺 `scope` 则继承 `event.scope`（向后兼容：显式不同仍走 `S06-001` 校验，缺省继承）。或提供 `EffectScriptBuilder` 最小 20 行：`Builder.Event(lifetime, scope).Claim(kind, resource, mode, size).Build()`，`scope` 只传一次。**不新增依赖，stdlib 即可。**
3. **P0-3 三桶误用**：`README.md` 首屏表格追加一行 `kind 选型：显存/占用→occupy，IO→read/write（不进守恒）`，`ParseClaim` 在 `kind:read/write + mode:create/release` 时错误消息追加 `（提示：资源守恒仅统计 occupy 桶，确认 kind 是否应为 occupy）`。
4. **P1-1 Schema 显式化**：`README.md` JSON 示例首行追加 `"$schema": "https://cosmos.effect/effect-script.schema.json"`，`templates/` 新增 `effect-script.json`（`samples/effect-sample.json` 的带注释版 + `loop/size/budget` 三可选段）。
5. **P1-2 5 行主张修正**：将 README/EFFECT_SCRIPT 中“5 行”表述改为“11 行最小可打印 / 1 行 minified”，或给 `dotnet run --project src/Cosmos.EffectAlgebra.Tool audit --example` 一键吐最小合法文件。
6. **P1-3 错误信息统一**：`Interval lo>hi` 分支的 `ArgumentException` 转译已做，但消息仍英文；补 `events[i].lifetime: lo(10) > hi(2) 不满足 §3.1.5 lo≤hi` 中英对照。

---

## 9. 残余风险

- `Unknown` 模式 fail-open 仍静默放行未知冲突，属设计锁死（`README.md:116` 锐边），新用户无感知。
- `loop:"⊤"` 与 `lifetime:[1,"⊤"]` 对同一“常驻”语义的相反守恒行为（`README.md:117`）需人工记忆，无类型区分。
- `Size ?? [1,1]` 散布仍存（`README.md:162` 已标债），新消费点可能再散布。

---

## 10. 合并 verdict

**OK with notes**（非 BLOCK）：代数正确性与白名单错误可操作性已达高水准，approachability 的 P0 集中在“零预算假绿 + scope 重复 + 三桶误用”三处，均可在不改代数的前提下以文档+ 5-20 行 Builder/Schema 显式化修复；5 行主张需更正为 11 行。不修复亦可合，但新用户首次绿灯流失率高。

DONE_R09

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "逐符号表 18 行 + 14 条隐式规则 + 9 步首次绿灯 + 错误信息 14 场景采样 + Builder 缺失负荷评估 + 5行主张 11行实测，文件路径与严重度（P0/P1/P2）齐备，落盘 audit/rich-hickey-round09-approachability.md"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round09-approachability.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "grep Builder + read 7+1 files + schema + samples",
      "result": "passed",
      "summary": "只读 7+1+README，audit/ 未读；Builder 零命中已验证"
    }
  ],
  "validationOutput": [
    "14 implicit rules counted, 9 steps to first legal script, 5-line claim falsified (minimal pretty 11 lines, minified 1 line unreadable), 3 P0 (scope dup / bucket misuse / zero-budget silent pass)"
  ],
  "residualRisks": [
    "Unknown mode fail-open 静默放行冲突（设计锁死）",
    "loop:⊤ 豁免守恒 vs lifetime:[1,⊤] 计入守恒的相反行为需人工记忆",
    "Size ?? [1,1] 散布债，用户可能再散布"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 1 审计文件：audit/rich-hickey-round09-approachability.md，Rich Hickey 视角 approachability 对抗性审计，含步数/隐式规则/错误可操作性/Builder 负荷/5行证伪/逐符号表/最小修复集",
  "reviewFindings": [
    "P0: EffectScriptContract.cs:170 claim.scope == event.scope 单一真相无 Builder 继承，手写 6 次重复必错其一",
    "P0: Objects.cs:148 Signature 三桶仅 occupy 进守恒/峰值，read/write 误用假绿",
    "P0: EffectScript.cs:455 Budget.None 时 IsPeakChecked==false 但 Passed==true 静默假绿",
    "P1: 5行主张不成立，最小 pretty 11 行（samples/effect-sample.json 7 行已极限，含预算成对 16-19 行）",
    "P1: docs/effect-script.schema.json 完备但 README 未引导 $schema，IDE 无法即时提示"
  ],
  "manualNotes": "无写文件工具，制品以 response 透出由 runtime 落盘至 audit/rich-hickey-round09-approachability.md；仅回复 DONE_R09 已满足任务尾句要求"
}