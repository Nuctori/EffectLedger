# effect-api-auditR6-RB — 对抗输入健壮性 / 失败方言一致性审计（R6-RB）

- 审计视角：**对抗输入健壮性 / fail-fast 契约（FormatException 方言）与 CLI exit code 方言**
- 日期：2026-09-05
- 审计员：独立 subagent（R6-RB）。**独立性声明**：未读取 `audit/` 下任何历史审计文件；依据仅限 `EFFECT_SCRIPT.md`、`src/Cosmos.EffectAlgebra/EffectScriptContract.cs`、`src/Cosmos.EffectAlgebra.Tool/` 源码、相邻载体源码（Numeric.cs / Objects.cs / EffectScript.cs / SignedNet.cs / DerivedMetrics.cs）与本人实跑实验。
- 语料：`P:\Temp\r6rb-corpus\`（46 个对抗 JSON）；直连 Parse 探针工程：`P:\Temp\r6rb-harness\`（console，ProjectReference 指向 Cosmos.EffectAlgebra.csproj）。
- 双路验证：(a) `dotnet run --project D:\Godot\Cosmos\src\Cosmos.EffectAlgebra.Tool -c Release -- audit <file>`（记录 exit code / stderr / stdout 摘录）；(b) harness 直接调 `EffectScriptContract.Parse(File.ReadAllText(file))`（记录异常类型 / 消息前 200 字 / 耗时）。

---

## 0. 契约基线（源码核实）

| 项 | 源码位置 | 内容 |
| --- | --- | --- |
| Parse 失败方言 | `EffectScriptContract.cs:22-23`（XML doc）、`:29`、`:113`、`:133`、`:216` | 非法形状/语法错误 ⇒ `FormatException`（JsonException/ArgumentException 均翻译收口） |
| CLI exit code 表 | `Tool/Program.cs:3` 注释 | `0=Passed, 2=Violations, 1=FormatException/IO`；另有 usage/help=0、unknown command=1（`:13-18`） |
| CLI 兜底 | `Tool/Program.cs:28-30` | `catch(FormatException)` 与 `catch(Exception)` 双兜底均转 stderr 单行消息 + exit 1（无崩溃堆栈）；`--out` 写失败 exit 1（`:50-55`） |
| 已知薄弱点（源码阅读预判，后经实测证实） | `EffectScriptContract.cs:220-228` | `ParseClaim` 未对 claim 的 `ValueKind != Object` 设防；`ParseFootprint` 只 `catch(ArgumentException)` ⇒ `JsonElement.TryGetProperty` 的 `InvalidOperationException` 可泄出方言 |
| 已知薄弱点 | `EffectScriptContract.cs:93-95` | `ParseEvent` 未把 `layer` 传入 `ParseInterval/ParseScope/ParseLoop`（用默认层名）⇒ 事件级消息缺 `events[N]` 定位 |
| 已知薄弱点 | `Tool/Program.cs:32` | `script.Audit(script.Budget)` 无 try/catch —— 若 Audit 抛异常即崩溃堆栈（实测未找到经 Parse 可达的触发输入，见 §2 正面确认） |

---

## 1. 语料 × 双路结果总表

图例：✅ 合规（FormatException 且消息含定位）/ ⚠️ 合规但消息差 / ❌ 违约。CLI ec = exit code；P = harness 直连 Parse 结果。

### G1 顶层形态

| 语料 | 内容 | CLI ec | P：异常 | 消息摘录（≤200 字） | 判定 |
| --- | --- | --- | --- | --- | --- |
| t01_deep1000.json | 1000 层嵌套数组 | 1 | FormatException | `JSON 非法: The maximum configured depth of 64 has been exceeded… LineNumber: 0 \| BytePositionInLine: 74.` | ✅（JsonException→FormatException 翻译生效，含字节偏移定位） |
| t02_bigobject_100kclaims.json | 单事件 10 万 claim（8.3MB） | 2（9.4s） | PARSE_OK 1392ms | passed=false, 1 Leak, events=1 | ⚠️ 合规但贴阈值（见 R6RB-02） |
| t03_empty.json | 空文件 | 1 | FormatException | `JSON 非法: The input does not contain any JSON tokens…` | ✅ |
| t04_bomonly.json | 仅 UTF-8 BOM | 1 | FormatException | 同 t03（ReadAllText 吃掉 BOM 后为空串） | ✅ |
| t05_rootarray.json | 根为数组 | 1 | FormatException | `EFFECT_SCRIPT §4：根须含 'events' 数组` | ✅ |
| t06_rootstring.json | 根为字符串 | 1 | FormatException | 同 t05 | ✅ |

### G2 events / lifetime / loop

| 语料 | 内容 | CLI ec | P：异常 | 消息摘录 | 判定 |
| --- | --- | --- | --- | --- | --- |
| t07_events_empty.json | `"events":[]` | 0 | PARSE_OK | `{passed:true, events:0, violations:[]}` | ✅（payload 带 events=0，非不可见全绿，R3-CG-08） |
| t08_missing_lifetime | 缺 lifetime | 1 | FormatException | `events[0]: 缺少字段: lifetime` | ✅ |
| t09_missing_scope | 缺 scope | 1 | FormatException | `events[0]: 缺少字段: scope` | ✅ |
| t10_missing_footprint | 缺 footprint | 1 | FormatException | `events[0]: 缺少字段: footprint` | ✅ |
| t11_lifetime_reversed | `[6,0]` | 1 | FormatException (inner ArgumentException) | `lifetime: Interval lo(6) > hi(0) violates §3.1.5 lo≤hi` | ⚠️ 消息缺 `events[0]`（R6RB-03） |
| t12_lifetime_negative | `[-1,5]` | 1 | FormatException | `lifetime.lo: 端点须为非负整数或 "⊤"/"inf"` | ⚠️ 缺 `events[0]`（R6RB-03） |
| t13_lifetime_inf_lo | `["inf","⊤"]` | 1 | FormatException | `lifetime: 下界不可为 "⊤"/"inf"（[⊤,⊤] 非法…）` | ⚠️ 缺 `events[0]`；语义正确（双别名均拒） |
| t14_loop_minus1 | `loop:-1` | 1 | FormatException | `loop: 须为非负整数或 "⊤"/"inf"` | ⚠️ 缺 `events[0]`（R6RB-03） |
| t15_loop_zero | `loop:0` | 1 | FormatException | `loop: 必须 ≥1（0 无意义）或 "⊤"/"inf"` | ⚠️ 缺 `events[0]` |
| t16_loop_top | `loop:"⊤"` | 0 | PARSE_OK（loop=⊤） | passed=true | ✅ |
| t17_loop_1e19 | `loop:1e+19` | 1 | FormatException | `loop: 须为非负整数或 "⊤"/"inf"` | ✅（NOTE R6RB-05：指数形式整被 System.Text.Json 拒） |
| t17b_loop_1e20 | `loop:1e+20` | 1 | FormatException | 同 t17 | ✅ |
| t17c_loop_ulongmax | `loop:18446744073709551615` | 0 | PARSE_OK（=2^64−1 全接受） | passed=true | ✅（边界钉：2^64−1 收、2^64（1e20）拒） |

### G3 footprint

| 语料 | 内容 | CLI ec | P：异常 | 消息摘录 | 判定 |
| --- | --- | --- | --- | --- | --- |
| t18_kind_unknown | kind:"zap" | 1 | FormatException | `未知 kind: zap` | ⚠️ 无任何 events[N]/footprint[i] 定位（R6RB-03） |
| t19_mode_unknown | mode:"destroy" | 1 | FormatException | `未知 mode: destroy` | ⚠️ 同上 |
| t20_resource_empty | `resource:{}` | 1 | FormatException | `resource 须含 gpu/commandBuffer/memory/occupancy/signalBus/custom 之一` | ⚠️ 无定位（R6RB-03） |
| t21_resource_multikey | `{"gpu":"a","memory":1}` | 1 | FormatException | `events[0][0].resource: resource 至多含一键…实际命中 2 键` | ✅ |
| t22_size_reversed | size `[5,1]` | 1 | FormatException (inner ArgumentException) | `events[0][0].size: Interval lo(5) > hi(1) violates §3.1.5 lo≤hi` | ✅ |
| t23_size_negative | size `[-3,2]` | 1 | FormatException | `events[0][0].size.lo: 端点须为非负整数…` | ✅ |
| t24_size_nan | size `["nan","inf"]` | 1 | FormatException | `events[0][0].size.lo: 端点须为数字或 "⊤"/"inf"` | ✅ |
| **t24b_claim_nonobject** | footprint:`[[0,1]]`（claim 为数组） | 1 | **System.InvalidOperationException** | P：`The requested operation requires an element of type 'Object', but the target element has type 'Array'.`；CLI：`parse: The requested operation…`（catch(Exception) 兜底） | **❌ 库侧违约（R6RB-01，MEDIUM）**：非 FormatException；CLI 兜底保住 ec=1 但消息为裸英文 BCL、零定位 |
| t24c_kind_number | `kind:42` | 1 | FormatException | `resource.events[0][0].kind 须为非空字符串` | ⚠️ 含定位但 `resource.` 前缀错误（kind 非 resource 字段；ReqStr 硬编码前缀，R6RB-03） |
| t24d_footprint_nonarray | footprint 为对象 | 1 | FormatException | `events[0]: footprint 须为 claim 数组` | ✅ |

### G4 budget

| 语料 | 内容 | CLI ec | P：异常 | 消息摘录 | 判定 |
| --- | --- | --- | --- | --- | --- |
| t25_budget_nocolon | 键 `"gpu"`（无冒号） | 1 | FormatException | `未知 budget 键: gpu` | ✅（键名即定位） |
| t26_budget_unknownprefix | 键 `"unknown:x"` | 1 | FormatException | `未知 budget 键: unknown:x` | ✅ |
| t27_budget_negative | `{"gpu:a":-5}` | 1 | FormatException | `budget["gpu:a"] 须为非负整数或 "⊤"/"inf" 字符串，实际为 Number` | ✅ |
| t28_budget_string | `{"gpu:a":"5"}` | 1 | FormatException | `budget["gpu:a"] 字符串值仅接受 "⊤" 或 "inf"…实际 "5"` | ✅ |
| t29_budget_1e30 | `{"gpu:a":1e+30}` | 1 | FormatException | 同 t27 | ✅ |
| **t30_budget_dupkey** | `{"gpu:a":5,"gpu:a":10}` | 0 | **PARSE_OK** | budget 载荷 `Gpu{a}=10` —— 值 5 被静默丢弃（last-win） | **❌ 静默接受歧义输入（R6RB-04，LOW）** |
| **t30b_root_dup_events** | `{"events":[ev],"events":[]}` | 0 | **PARSE_OK** | `passed:true, events:0` —— 含事件剧本被静默当空剧本审计 | **❌ 静默假绿向量（R6RB-04，LOW）** |

### G5 resource id 对抗字符串

| 语料 | 内容 | CLI ec | P | 判定 |
| --- | --- | --- | --- | --- |
| t31_rid_unicode_rtl | 含 Unicode+RTL 覆写符（U+202E/U+202D） | 0 | PARSE_OK | ✅ NOTE R6RB-06（身份不透明为设计；RTL 可伪装审计报告文本） |
| t32_rid_control | 含控制字符 U+0000/U+0007 | 0 | PARSE_OK | ✅ NOTE R6RB-06（NUL 可破坏下游日志/原生互操作；契约未定义字符集边界） |
| t33_rid_100k | 10 万字符 id（97KB） | 0 | PARSE_OK 0ms | ✅ 无感 |

### G6 规模（10 万事件；测时间/内存温和性，非要求拒绝）

| 语料 | 形状（大小） | CLI ec / 耗时 | Parse 耗时 | 判定 |
| --- | --- | --- | --- | --- |
| t34_events_100k_staggered | 10 万交错 `[i,i+10]`（13.9MB） | 0 / 2.9s | 617ms | ✅ 温和 |
| t35_events_100k_overlap | 10 万同窗 `[0,1e6]` 异构（15.2MB） | 2 / 9.1s | 756ms | ⚠️ 贴 10s 阈值 |
| **t36_events_100k_ovl_distinct** | 10 万三角重叠 ` [i,1e6−i]` 异 scope（15.5MB） | **124（45s timeout 击杀）**；放任跑完 **exit 2 / 138s**（输出 10 万条 violations，载荷正确） | — | **❌ 超 10s 阈值 13.8 倍（R6RB-02，MEDIUM）**：O(S·D) 超线性（自认边界，EffectScript.cs 头注释 R4-JD-05），但作为 AI 回修门的 CLI 对合法规模输入实际不可用 |

### CLI 参数方言（附加核实）

| 调用 | ec | 输出 |
| --- | --- | --- |
| 无参数 | 0 | usage 两行 |
| `audit`（缺文件名） | 1 | `audit 需要 <script.json>` |
| `audit 不存在.json` | 1 | `read …: Could not find a part of the path…`（单行，无堆栈） |
| `bogus` | 1 | `unknown command: bogus (only 'audit')` |
| `audit … --out Q:\no-such-dir\v.json` | 1 | `write Q:\…: Could not find a part of the path…`（R3-CG-05 生效） |

编码核实：stderr 重定向字节为合法 UTF-8（hexdump `e9 87 8d`=「重」），t40 重复 claim 消息为 `events[0]: 重复 Claim(Read,Gpu{a},Use,Scene{S})…`——中途观察到的乱码系本审计捕获层显示解码所致，**非被审工具缺陷，不记 finding**。

### 边界探针（Audit 崩溃面尝试，均未击穿）

| 语料 | 内容 | CLI ec | 结果 |
| --- | --- | --- | --- |
| t37_size_top_top | occupy create+release size `["⊤","⊤"]` | 2 | Leak，正常 |
| t38_loop_top_release | `loop:"⊤"` + release | 0 | 居民层豁免，正常 |
| t39_lifetime_ulongmax | lifetime `[2^64−2, 2^64−1]` | 2 | Leak @ t=18446744073709551615，无溢出崩溃 |
| t40_dup_claims | 同一 footprint 两条完全相同 claim | 1 | FormatException 带 `events[0]` 定位（P0-4 经 ArgumentException 翻译生效） |

---

## 2. Findings

### R6RB-01 · MEDIUM · 非对象 claim 泄出 `InvalidOperationException`，违反 FormatException 方言
- **Evidence**：`t24b_claim_nonobject.json`（`"footprint":[[0,1]]`）。直连 Parse：`System.InvalidOperationException: The requested operation requires an element of type 'Object', but the target element has type 'Array'.`（1ms）。CLI：`parse: The requested operation…`（经 `Program.cs:30` 的 `catch(Exception)` 兜底），ec=1。
- **Expected**：契约明文「非法形状 ⇒ FormatException（fail-fast）」（EffectScriptContract.cs:22）。`ParseEvent` 对 event 已做 `ValueKind != Object` 守卫（:90），claim 层漏同型守卫；`ParseFootprint` 仅 `catch(ArgumentException)`（:214）。
- **Actual**：库侧按文档 `catch(FormatException)` 的调用方（README 推荐的 AI 闭环接入方式）会漏接崩溃；CLI 侥幸保住 exit code 方言，但消息为裸英文 BCL 文本、零定位。
- **修复建议**：`ParseClaim` 入口加 `if (c.ValueKind != JsonValueKind.Object) throw new FormatException($"{layer}: claim 须为对象");`（一行，与 ParseEvent 同型）。

### R6RB-02 · MEDIUM · 10 万事件合法剧本审计 138s（>10s 阈值 13.8 倍）
- **Evidence**：`t36_events_100k_ovl_distinct.json`（10 万事件、三角重叠、各异 (resource,scope)：S≈20 万采样点 × D≈10 万活跃组）。45s timeout 击杀（ec=124）；放任跑完 138s、ec=2、载荷正确（10 万 Leak）。对照组：t34 交错布局 2.9s 全绿；t35 同窗 9.1s；t02 单事件 10 万 claim 9.4s（纯审计 ≈8.5s）。
- **Expected**：任务口径「10 万条时间/内存应温和（>10s 记 finding）」；本工具定位是 AI 闭环门，AI 产出 10 万事件剧本是现实规模。
- **Actual**：O(S·D) 乘子（EffectScript.cs 头注释 R4-JD-05 与 README 诚实边界 16 自认）在异构重叠形状下把 CLI 变成准挂起（用户体验即 hang）。内存全程温和、无 OOM；结果正确性无损。
- **修复建议**：gate(1)/(3) 的逐采样点全字典扫描改为随扫换线增量维护（负陷/冲突组按 enter/exit 事件记账），或对 D·S 设预警输出（payload 附 `superlinear: true` 提示）。

### R6RB-03 · LOW · 失败消息定位性不均（多处缺 `events[N]`；一处前缀错）
- **Evidence**：
  - 无任何定位：t18 `未知 kind: zap`、t19 `未知 mode: destroy`、t20 `resource 须含 gpu/…之一`（ParseKind/ParseMode/ParseResource hitCount==0 分支未接 `layer`）。
  - 缺事件索引：t11/t12/t13 `lifetime: …`、t14/t15 `loop: …`——`ParseEvent`（:93-95）调 `ParseInterval/ParseScope/ParseLoop` 时未传 `layer`，10 万事件剧本中同类消息无法定位到条目，直接削弱「violations 喂回 LLM 回修」闭环。
  - 前缀错误：t24c `resource.events[0][0].kind 须为非空字符串`——`ReqStr` 硬编码 `resource.` 前缀（:393），套在 kind/mode/scope 字段上产生误导（定位信息本身还在）。
- **Expected**：fail-fast 契约的「消息可定位」应全路径一致（同文件内 t21/t22/t23 已是 `events[0][0].xxx` 金标准）。
- **Actual**：同层校验两种口径并存。

### R6RB-04 · LOW · 重复 JSON 键静默 last-win（含根级 `events` 重复 ⇒ 假绿）
- **Evidence**：t30 `{"gpu:a":5,"gpu:a":10}` → PARSE_OK，cap=10，值 5 无声丢弃；t30b `{"events":[…1 事件…],"events":[]}` → PARSE_OK，`passed:true, events:0`，ec=0——一个非空剧本被静默当空剧本「全绿」。
- **Expected**：与仓库自身教义一致——R3-L1-03/REG-02 为多键 resource 拒绝时的理由是「静默改写数据比报错更危险」；重复键是同一威胁面。
- **Actual**：System.Text.Json `JsonDocument` 对重复键默认放行、`TryGetProperty` 取最后者；RFC 8259 仅 SHOULD-unique，故评 LOW 而非 HIGH。但根级 `events` 重复路径是结构性假绿向量（与 auditR4 修的 Lo=⊤ 假绿同型），建议 `EnumerateObject` 逐层查重拒绝。

### R6RB-05 · NOTE · 指数形式整数被拒（方言合规，语义可辨）
- `loop: 1e+19`（数学上是 ≤2^64−1 的整数）被 `TryGetUInt64` 拒绝：`loop: 须为非负整数或 "⊤"/"inf"`，ec=1。整数形式 `18446744073709551615` 接受、`1e+20`/2^64 拒绝。fail-fast 方言无违约；建议 EFFECT_SCRIPT.md §4 注明「整数须十进制字面量」。

### R6RB-06 · NOTE · resource id 无字符集边界（NUL/RTL/10 万字符均收）
- t31（RTL 覆写符）、t32（U+0000/U+0007）、t33（10 万字符）全部 PARSE_OK、ec=0、~0ms。身份不透明是设计选择；但 NUL 可打断下游日志/原生互操作，RTL 可伪装报告文本。建议至少拒 U+0000–U+001F。（budget 键侧已有空 id 拒绝口径 A1-12，claim 侧无对应字符级口径。）

### R6RB-07 · NOTE · exit code 表已定义但仅存在于源码注释
- `Program.cs:3` 注释 `0=Passed, 2=Violations, 1=FormatException/IO` + usage 文本；46 条语料 + 5 条参数方言 + 写失败路径共 51 次运行全部吻合，无第 4 种码出现。表本身已定义（按任务口径不记 finding）；建议升格进 EFFECT_SCRIPT.md/README 的机器可读章节。

### R6RB-08 · NOTE · 正面确认清单（对抗面通过项）
- 51 次投喂中：**零崩溃堆栈、零 OOM、零未处理异常逃出 CLI、零挂起致死**（t36 为有界慢，非死锁）；所有坏输入 ec=1 + 单行可读消息，所有合法输入 ec=0/2 符合审计结果。
- JsonException→FormatException 翻译（R3-L1-01）在深嵌套/空文件/BOM 全部生效；ArgumentException→FormatException 翻译（rich-hickey2 R4-001）在 [6,0]、[5,1]、重复 claim 全部生效；Parse 唯一漏网即 R6RB-01 一处。
- 数字边界钉：2^64−1 收、2^64 拒、负数/小数/字符串数字/1e30 全拒；`"⊤"`/`"inf"` 双别名在 lifetime.lo/loop/size/budget 四处行为一致；`["⊤","⊤"]` size 合法、`["⊤",n]` 拒、`["inf","⊤"]` lifetime.lo 拒——与 EFFECT_SCRIPT.md §4 要点逐条吻合。
- 根级未知键/事件级未知键/scope 未知键/budget 未知键白名单（R6-E1/R1-F2/R3-L1-04）实测全拦截，拼写错误无一静默。
- `Tool/Program.cs:32` 的未包裹 `script.Audit()`：40+ 形状/边界探针未能使其抛异常（构造期守卫 ValidateEvent + 溢出⇒⊤ 策略有效），本次未升级为 finding；建议补 `try/catch` 使 exit code 表对 Audit 阶段也闭合。

---

## 3. Verdict

**READY_WITH_RESERVATIONS**。

fail-fast 方言在 51 次对抗投喂中 50 次精确命中（FormatException + 可读消息 + ec=1/0/2 语义正确），CLI 层无一崩溃、无一静默接受非法形状。保留点有二：(1) 库侧唯一方言漏洞 R6RB-01（非对象 claim 漏 InvalidOperationException，一行守卫可修）；(2) 10 万事件异构重叠剧本审计 138s（R6RB-02），对 AI 回修闭环是现实可触的性能墙。其余为消息定位性（LOW）与文档口径（NOTE）。

---

## 4. 证据清单（文件路径与 exit code）

- 语料目录：`P:\Temp\r6rb-corpus\`（t01…t36 + t37…t40，46 个 JSON；生成器 `C:\Users\Nuctori\AppData\Local\Temp\gen_corpus.py`）
- 直连 Parse 探针工程：`P:\Temp\r6rb-harness\`（r6rb-harness.csproj + Program.cs）
- 双路原始输出：
  - `P:\Temp\r6rb-parse-results.txt` — harness 直连 Parse 全量结果（41 文件）
  - `P:\Temp\r6rb-cli-results.txt` — CLI 全量结果（41 文件，含 ec 与 stdout/stderr 摘录）
  - `P:\Temp\t36-out.json`（t36 跑完的 violations 载荷，ec=2）、`P:\Temp\t36-timing.txt`（`t36 exit=2 elapsed=138s`）
  - `P:\Temp\t40-err.bin` / `P:\Temp\t08-err.bin` — stderr UTF-8 字节取证（hexdump 见 §1 编码核实）
- 关键 exit code 摘录：坏输入全部 ec=1（t03/t04/t05/t06/t08-t15/t17/t17b/t18-t26/t27-t29/t40…）；合规通过 ec=0（t07/t16/t17c/t30*/t31-t33/t34/t38）；违例剧本 ec=2（t02/t35/t36/t37/t39）；t36 限时击杀 ec=124（45s timeout），放行后 ec=2/138s；CLI 参数方言 ec=0/1/1/1/1（无参/缺文件名/文件不存在/未知命令/--out 写失败）。
