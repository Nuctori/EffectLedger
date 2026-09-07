# AI 闭环方言攻击报告（v2 合并版）

- 审计对象：`EffectScriptContract.Parse/ToJson/Audit` 的 JSON 方言边界（LLM 产出剧本 → `cosmos audit` 机审闭环）
- 审计方式：只读黑盒攻击 + 实际投喂。全部反例经 `dotnet run --project src/Cosmos.EffectAlgebra.Tool -c Release -- audit <file>`（批量为等价的 `bin/Release/net10.0/Cosmos.EffectAlgebra.Tool.dll` 直调）真实执行，记录确切 stdout/stderr/退出码（0=Passed / 2=Violations / 1=FormatException·IO）
- 投喂规模：**约 45 个反例剧本、6 个攻击方向、50+ 次投喂**；临时剧本置于系统临时目录，审计后已删除；仓库源码/文档/测试零修改
- 日期：2026-09-08

> **基线漂移注记（诚实声明）**：本次审计进行期间，前一轮同题审计的 HIGH 发现（budget 键空白 id 幽灵预算）已被Commit `78b221b`（P5.2b）修复落库（源码 06:30、二进制 06:31）。本报告全部投喂均针对**修复后的当前基线**（HEAD `0316674`）执行，并复测确认：`"custom: residency"` 现被拒（`budget 键 "custom: residency" 资源 id 含前后空白（" residency"）…拒绝而非改写`）。本文件为两轮证据的合并收口，替代前一轮版本；所有沿承结论均已由本轮独立复测（含 od 逐字节验证）。

---

## 总评（LLM 产 JSON 的鲁棒性结论）

**解析边界（Parse）：硬。** 约 45 个恶意/边缘输入实测 **0 次静默改写**（唯一例外见 #3 global+scene）、**0 次 BCL 裸异常逃逸**、**0 次崩溃**。数值方言、别名大小写、Unicode 控制字符、深嵌套、重复键、形状混淆全部 fail-fast，且绝大多数失败带 `events[N][M]` 级可定位路径。`78b221b` 落地后，前后一轮的空白 id 幽灵预算向量已被堵死。

**审计边界（Audit）：数学内核确定、fail-closed 纪律无反例。** 溢出环绕⇒⊤、`ContainsZero` 对 ⊤ fail-closed、`loop:⊤` 豁免守恒但不豁免峰值——40+ 次投喂无一处静默漏报；1000 事件 250–378ms（含 dotnet 启动），远优于 §10.2 的 <5s 承诺；确定性经多载荷 3 连投喂**逐字节**验证成立。

**但工程外壳有 1 个 HIGH + 2 个 MED 必须先堵**：(1) Windows 控制台默认 GBK 编码把 `⊤` 与全部中文机审载荷打成 `?`/乱码——直接打击「LLM 读违例回修」的头号卖点；(2) gate(3) 冲突违例不去重，1000 事件剧本单组冲突膨胀 1999 行/518KB；(3) `{"type":"global","scene":"X"}` 静默丢弃 scene，违反仓库自身「拒绝而非改写」教义。语义层对 LLM 最危险的假绿是 **create/release 时序错配全绿**（gate(1) 只查净效应闭合，不查时序配对）。

**判定汇总：实现缺陷 6 条（HIGH 1 / MED 2 / LOW 3），文档欠明确 4 条（MED 2 / LOW 2），行为正确（含防线确认）11 组。**

---

## 攻击发现清单

| # | 严重度 | 攻击向量 | 投喂剧本（内联） | 实际行为 | 期望行为 | 判定 |
|---|---|---|---|---|---|---|
| 1 | **HIGH** | **控制台 GBK 编码摧毁机审载荷**：重定向捕获时 stdout/stderr 按 codepage 936 编码，`⊤`(U+22A4)→单字节 `?`(0x3F)，中文对 UTF-8 消费方整体乱码 | 任意含 ⊤ 的载荷。d1f（budget cap=Max + 双 size.hi=Max 事件）stdout detail 实义 `峰值 ⊤ > 预算 18446744073709551615`，实测字节 `22 3f 22`；stderr `端点须为非负整数或 "?"/"inf"`（od 可见 GBK 字节 `266 313…`） | LLM 读到的违例 detail 变 `峰值 ? > 预算 …`——`?` 无法区分「无上界」与字面问号；若按 UTF-8 解码则全部中文乱码，回修信号语义丢失 | Tool 入口钉 `Console.OutputEncoding = UTF8`（或对非 ASCII JSON-escape），保证重定向字节恒为 UTF-8 | **实现缺陷**（Program.cs 未设编码；UTF-8 终端不受影响，但本仓库主力平台 Windows 中文区默认即中招） |
| 2 | **MED** | **gate(3) 冲突违例不去重**：gate(2) 有 `peakReported` 按资源去重，gate(3) 无对应机制 | d4b2：1000 事件（500 create `[i,5000]` + 500 release `[1000+i,6000+i]`，同 `gpu:m` 同 scope） | 250ms、exit 2，但输出 **1999 行 CompatibleConflict / 518KB**（1499 个采样点，其中 500 点因 create 组与 release 组各报一行）——结构上是同一组资源冲突的时间序列 | 与 §3.2「反例：首个违例」口径一致：同 (resource,scope,mode) 组按首反例去重或截断，保 LLM 回修上下文干净 | **实现缺陷**（EffectScript.cs gate(3) 无去重集；注释只给 gate(2) 写了「时间序列不是问题集」理由） |
| 3 | **MED** | **`{"type":"global","scene":"X"}` 静默丢弃 scene**：与 `{"type":"global"}` 归并为同一 scope | v2：事件甲 scope `{"type":"global","scene":"Battle"}`、事件乙 `{"type":"global"}`，各 create 同一 `{"gpu":"s"}` | 两条 create 被归入**同一 Global 组**：exit=2 `CompatibleConflict …scope: "Global { }"`×2——证明 scene 被无声丢弃；且 `SerializeScope(Global)` 只输出 `{"type":"global"}`，ToJson round-trip 同样丢（代码只读结论） | 仓库教义是「拒绝而非改写」（R6-RB-04/R3-L1-03）：要么拒 `global+scene` 组合，要么文档钉死「global 忽略 scene」 | **实现缺陷**（ParseScope `"global" => new ScopeId.Global()` 无声吞字段） |
| 4 | **MED** | **create/release 时序错配 ⇒ 假绿**：交错窗与缺口窗均不可审计 | d5c：create `[0,120]` + release `[60,180]`（交错）；d5d：create `[0,100]` + release `[200,300]`（100 tick 悬空窗口） | 两者均 `passed:true`、exit 0 **全绿**。gate(1) 净额按 enter 累加且与顺序无关：交错对恰好抵消、缺口对闭包含 0；只有完全反向（release `[0,60]` 先于 create `[60,120]`）才报 `NegativeDip at t=0 [-1,-1]` | 行为与「守恒=净效应闭合」代数定义一致，非实现错误；但 §4 须向 LLM 声明「gate(1) 不审计 create/release 的时序配对」，否则全绿会被误读为时序合法 | **文档欠明确**（建议 §4 补时序方言注记） |
| 5 | **MED** | **occupy + `mode:"use"` 单独存在 ⇒ Leak** | v3：单事件 `{"kind":"occupy","resource":{"gpu":"g"},"mode":"use","size":[1,1]}` | exit=2 `Leak 生命周期未闭合（净效应不含 0）：[1,1]`。net 记账把非 release 全记 +，`use` 结束后 [1,1] 不含 0 | 记账是 SignedNet 设计（Use=+），但 EFFECT_SCRIPT §3.1 符号表只写「create/move=+、release=−」，`use` 未列——LLM 写 `use` 表达「临时借用」必吃 Leak 且报错不含正解 | **文档欠明确**（符号表必须补 use 行；报错可附「改用 create/release 对或 loop 居民声明」提示） |
| 6 | LOW | **read+create / 重复 claim 的报错缺 claim 索引** | d5b2：footprint 3 条 claim，第 3 条 read+create；v4：同 footprint 两条相同 create claim | 均 `events[0]:` 级（无 `[2]`）：`非法 Kind×Mode：Read+Create（…）`、`重复 Claim(Occupy,Gpu…)：Signature 是集合…须用 Combination.Loop…（P0-4）`——3 条 claim 中 LLM 须自查凶手 | EFFECT_SCRIPT R6-RB-03 承诺「claim/kind/mode 报错均带 events[N][M] 级定位」 | **实现缺陷**（ParseFootprint 把 `Signature.Of/Normalize` 的 ArgumentException 在 footprint 层统一捕获丢 cIdx；重复 claim 消息质量高但同样无索引） |
| 7 | LOW | **CLI 载荷缺 `capsChecked`**：budget 用 `"inf"`/缺省 ⇒ gate(2) 整体 no-op，输出不可区分「查过全绿」与「没查」 | d2e/d2k：`loop/hi/size/budget` 全 `"inf"`（或全 `"⊤"`）→ `passed:true` exit 0；d4c：`{"events":[],"budget":{"gpu:x":5}}` | CLI payload 仅 `{passed,events,violations}`；`AuditResult.CapsChecked`（R1-HIGH-3 专设字段）被 Tool 丢弃——`budget:"inf"` 等价于没设预算，闭环 AI 无从知情 | payload 增加 `capsChecked` 一行 | **实现缺陷**（工具面；库内字段存在） |
| 8 | LOW | **控制字符报错自身内嵌裸 NUL**：拒绝消息把原始 key 原样拼进文案 | d3e：`{"budget":{"gpu:a\u0000b":64}}`，od -c 逐字节检查 stderr | 拒绝正确（`…含控制字符 U+0000…`），但消息字节流内嵌裸 `\0`（od 实证：`g p u : a \0 b`）——恰是 R6-RB-06 要防的下游日志/NUL 场景 | 提示控制字符的消息应转义或省略回显 | **实现缺陷**（诊断面） |
| 9 | LOW | **`[0,⊤]` 的 Leak 文案撒谎**：`ContainsZero` 对 ⊤ 端 fail-closed（SignedNet.cs DO-9），但理由断言「净效应不含 0」 | d1f2：create/release 成对但 `size.hi=18446744073709551615`（> long.MaxValue ⇒ ToZ=⊤） | exit=2 `Leak …（净效应不含 0）：[0,⊤]`——0 明明 ∈ [0,⊤]；决策本身正确（⊤ 不可证闭合⇒人工确认），理由文案数学为假 | fail-closed 上报保留，文案改为「净效应含 ⊤，不可证闭合（人工确认）」 | **文档欠明确**（消息文案；行为正确。附锐边：size 任一端点 > 9223372036854775807 时成对 create/release 也必报 Leak，建议 §4 注记） |
| 10 | LOW | **`"∞"` 别名幻想**：§4「`"⊤"/"inf"`（∞）」括注诱导 LLM 尝试 ∞(U+221E) | v5：`{"lifetime":[0,"∞"],…}` | exit=1 `events[0].lifetime.hi: 端点须为数字或 "⊤"/"inf"` | 拒绝正确（方言冻结 QED-A4）；文档应删 `（∞）` 括注或消息点名 ∞ 不合法 | **文档欠明确** |
| 11 | — | **ulong 数值边界全家桶** | `hi=18446744073709551615`（d1a 收，全绿 exit 0）；`hi=18446744073709551616`（d1b 拒）；`lo=-0`（d1c 拒）；`size.lo=-0`（d1c2 拒）；`[0120,180]`（d1d 拒）；`hi=1e19`（d1e 拒）；`loop:1e+19`（d1e2 拒，R6-RB-05 钉的原案）；`hi=1e2`（d1e3 拒——整数值也须十进制字面量）；`memory:18446744073709551616`（d1g 拒） | 全部 FormatException + 精确路径：`events[0].lifetime.hi` / `events[0][0].size.lo` / `JSON 非法: Invalid leading zero before '1'. LineNumber: 0 | BytePositionInLine: 25` / `budget 键 "memory:…" memory 段须为非负整数` | 一致拒绝，无 BCL 裸异常 | **行为正确**（R6-RB-05/A1-06 有文档） |
| 12 | — | **溢出⇒⊤ 的保守上报** | d1f：budget cap=Max、两事件 size.hi=Max 叠加（峰值和环绕） | `PeakExceeded 峰值 ⊤ > 预算 18446744073709551615` + Leak `[2,⊤]`——环绕判定 `sum < a.Value` 生效，保守转 ⊤ 交人工 | — | **行为正确**（⊤ 呈现被 #1 编码缺陷损毁） |
| 13 | — | **⊤/inf 别名跨位置一致性 + 大小写变体** | d2a `"inf"` hi / d2b `"INF"` / d2c `"Inf"` / d2d lo=`"inf"` / d2e 四位置全 `"inf"` / d2k 四位置全 `"⊤"` / d2f budget `"INF"` / d2g loop `"Inf"` / d2h size `["⊤",5]` / d2i size `["inf",5]` / d2j 裸 `Infinity` | **零漂移**：exact `"⊤"`/`"inf"` 在 lifetime.hi、loop、size 端点、budget 值四位置全收（d2e/d2k 全绿）；`"INF"/"Inf"` 三位置全拒且带路径（budget 回显 `实际 "INF"`）；lo 位置双别名统一拒（专用消息）；size `["⊤",5]`/`["inf",5]` 同一条消息；裸 `Infinity` → `JSON 非法: 'I' is an invalid start of a value`。d2a 时间⊤+有限 ω 按 P0-A1 正确报 Leak | A1-07「多位置认双形」承诺成立 | **行为正确** |
| 14 | — | **Unicode/控制字符/超长串** | d3a `"m\u00e9sh"`（claim+budget 同形）；d3b `"m\u0000esh"`；d3c scene `"Ba\u001fttle"`；d3f `"m\u007fesh"`（DEL）；d3d 10240 字符 id（30948 字节文件） | `\u00e9` 收且 claim↔budget 解码一致全绿；`\u0000`→`events[0][0].resource.gpu 含控制字符 U+0000`；`\u001f`→`events[0].scope.scene 含控制字符 U+001F`（全路径+码点）；DEL 收（规则恰为 U+0000–U+001F，文档同口径）；10KB id 全绿 **163ms** 无长度上限无性能悬崖 | — | **行为正确**（消息回显缺陷另见 #8） |
| 15 | — | **结构攻击：深嵌套/类型混淆/重复键/空对象** | d4a scope `{"typo":{"deep":{"x":1}}}`；d4a2 `{"scene":{"a":1}}`；d4a3 events 值嵌 100 层数组；d4d `{"gpu":"a","gpu":"b"}`；d4d2 budget 同键双写；d4d3 根级 `"events"` 双写（一空一非空假绿向量）；d4f `scope:{}` | 全拒可定位：`events[0].scope层未知键 "typo"（合法键: scene, type…）`；`events[0].scope.scene 须为非空字符串`；`JSON 非法: The maximum configured depth of 64 has been exceeded…BytePositionInLine: 73`（无栈溢出）；`events[0][0].resource层重复键 "gpu"`；`budget 层重复键 "gpu:a"`；`根层重复键 "events"`；`scope 须含 scene 或 type`。**最毒的「events 一空一非空假绿」被根层重复键检测正面封死** | — | **行为正确** |
| 16 | — | **空数组语义** | d4c `"events":[]` + budget；d4e `"footprint":[]` | 空 events：`passed:true, events:0` exit 0（载荷显式带计数，R3-CG-08 诚实可见）；空 footprint：Parse 接受、audit 全绿——惰性事件无任何提示；与 `docs/effect-script.schema.json` 同界（footprint 无 minItems） | 三方同界 | **行为正确**（附 UX 注记：惰性事件可提示） |
| 17 | — | **1000 事件规模压测** | d4b：1000 事件自配对自资源（206KB）；d4b2：1000 事件同资源重叠 | **378ms** 全绿 / **250ms** + 1999 违例（均含 dotnet 启动约 150–200ms）——远优于 §10.2「1000 事件 <5s」 | 秒级内 | **行为正确**（违例噪声见 #2） |
| 18 | — | **scope 继承 × global × mismatch** | d5a：event scope `{"type":"global"}`、claim 省略 scope（继承）、两条 create 交错；d5a2：claim 显式 `{"type":"global"}` vs event `{"scene":"Battle"}` | d5a：继承成立，Global 组冲突上报（`scope: "Global { }"` + eventIndex 定位）；d5a2：`claim scope 须与所属 event scope 一致（…claim=Global { }, event=Scene { Name = Battle }）`——双侧归因 | — | **行为正确**（global 吞 scene 是另一回事，见 #3） |
| 19 | — | **kind×resource/mode 合法性** | d5b：`{"kind":"read","resource":{"gpu":"m"},"mode":"use"}` | Parse 收、audit 全绿——read 不进 gate(1)/gate(2)（量纲隔离），gate(3) use 自兼容。「读 GPU」合法且不触发守恒/峰值；read+create 越界被拒（见 #6） | — | **行为正确** |
| 20 | — | **确定性：同剧本 3 连 Audit** | d6_multi_violations（Leak+PeakExceeded+CompatibleConflict+NegativeDip 四种并存）；d4b2（1999 违例）；d1b（Parse 错误 stderr） | 三组各 3 连投喂：stdout sha256 全等（`7fbfb424…`×3 / `fc59910f…`×3）、stderr sha256 全等、exit code 全等，`diff` 逐字节通过。违例顺序稳定 = 采样点时序 + 组内 gate(1)NegativeDip→(2)Peak→(3)Conflict + Leak 闭包殿后 | 逐字节一致（含顺序） | **行为正确**（附注：顺序源自 Dictionary 迭代序+扫换线排序，同一 SDK 内稳定；如需跨运行时契约，建议工具侧显式排序后输出） |
| 21 | — | **回归确认：空白 id 幽灵预算已修复** | v1：`"budget":{"custom: residency":64}` + 正常 claim（前轮 HIGH 原案） | 当前基线 **exit=1 拒**：`budget 键 "custom: residency" 资源 id 含前后空白（" residency"）——拼写失配会让预算/审计静默失配，拒绝而非改写` | P5.2b（`78b221b`）承诺落地 | **行为正确**（修复验证通过；claim 侧同型守卫已加，`EffectScriptContract.cs:426-431`） |

---

## 值得表扬（防线撑住的部分）

1. **「静默改写比报错危险」教义基本全线落地**。重复键（根/resource/budget 三层实测，事件/claim 层同机制）、未知键白名单（含 scope 内嵌套对象值）、类型混淆（对象塞 scene、数字塞 gpu）全部 loud 拒绝；最毒的「根级 events 一空一非空假绿」向量被 `RejectUnknownKeys` 的 `seen.Add` 单点封死（#15 实测）。唯一漏网是 `global+scene` 静默吞字段（#3），恰是该教义需要补的最后一块。
2. **数值与别名方言单一真源，零漂移**。`TryGetUInt64` + `IsTopAlias` 两个单点覆盖 lifetime/size/loop/budget/memory 键段全部位置：`1e19`/`1e+19`/`1e2`/`-0`/前导零/超 ulong 一律 `FormatException` 带定位；`"⊤"/"inf"` 四处一致收、`"INF"/"Inf"/"∞"/Infinity` 一律拒（#11/#13）。溢出环绕⇒⊤ 的 fail-closed 在真实投喂中得到验证（#12）。
3. **fail-closed 纪律与性能承诺双双兑现**。`Lo=⊤` 拒、`ContainsZero` 对 ⊤ 恒否、`loop:⊤` 豁免守恒但**不**豁免峰值（d5e 实测 `峰值 ⊤ > 预算 64`）——「⊤ 一律交人工确认，不静默漏报」在 50+ 次投喂中无一处反例；1000 事件 250–378ms 远优于 <5s 承诺（#17），确定性逐字节成立（#20）。

---

## 附：方法与限制

- 每条「实际行为」均为本轮真实投喂记录（非推断）；退出码逐一核实；GBK/NUL 相关结论以 `od -c` 逐字节为准。
- `ToJson` 无法经 CLI 驱动（Tool 仅暴露 `audit`），涉及 ToJson 的两条结论（Global 丢 scene 的 round-trip、⊤ 以 `UnsafeRelaxedJsonEscaping` 原样输出）为代码只读结论，已注明。
- 基线漂移：审计中途 `78b221b`（空白 id 双侧拒绝）落库，本报告全部结论基于其后二进制；前轮 HIGH 经 v1 复测确认已修复（#21）。
- 临时剧本目录（系统临时区）已清空；仓库内除本报告外零写入。
