# AI 闭环方言攻击报告

- 审计对象：`EffectScriptContract.Parse/ToJson/Audit` 的 JSON 方言边界（LLM 产出剧本 → `cosmos audit` 机审闭环）
- 审计方式：只读黑盒攻击 + 实际投喂。全部反例经 `dotnet run --project src/Cosmos.EffectAlgebra.Tool -c Release -- audit <file>` 真实执行，记录确切 stdout/退出码（退出码契约：0=Passed / 2=Violations / 1=FormatException·IO）
- 投喂规模：40+ 个反例剧本，覆盖 6 个攻击方向；临时剧本置于系统临时目录，审计后已删除
- 对照实现：`CosmosEffectConfig.ParseClaim`（第二份物理解析器，代码只读对照）
- 日期：2026-09-08

---

## 总评（LLM 产 JSON 的鲁棒性结论）

**结论：防线整体扎实，可以支撑「LLM 产 JSON → 机审」闭环，但有 1 个 HIGH 级旁路必须先堵。**

- **解析边界（Parse）非常硬**：40+ 次攻击中没有任何一次让 BCL 异常（InvalidOperationException/NRE/OverflowException）漏出 `FormatException` 方言；所有失败消息带 `events[N][M]` 级定位；重复键、未知键、控制字符、数值方言四道防线全部实测撑住。这是本仓库宣称的核心卖点，实测名实相符。
- **审计边界（Audit）对恶意/退化输入诚实**：溢出环绕⇒⊤ fail-closed、`[⊤,⊤]` lifetime 拒收、P0-A1 双 ⊤ 语义与文档钉逐条一致；1000 事件剧本 1.1–1.4s（含 dotnet 启动），秒级达标；确定性经 38 次跨进程运行逐字节验证成立。
- **但「身份串卫生」有一处系统性缺口**：budget 键 id 只拒空串和控制字符，**不拒空白字符**。一个空格拼写习惯（`"custom: residency"` vs `"custom:residency"`）就能让 gate(2) 对真实资源**静默失效**，且 CLI 载荷看不到 `CapsChecked`，闭环 AI 无从发现「查了个幽灵」。这是 A1-12（空 id 拒）防线没有覆盖到的变体，与 R6-E1「拼写键静默禁用预算门」同教训。
- **文档对 LLM 有两处误导面**：`"⊤"/"inf"（∞）` 括注诱导 ∞ 别名幻想；§3.1 符号表漏 `use=+1` 导致 occupy+use 剧本被莫名判 Leak，LLM 会陷入回修循环。

**判定汇总：实现缺陷 5 条（HIGH 1 / MED 2 / LOW 2），文档欠明确 4 条，行为正确（含确认防线有效）16 条。**

---

## 攻击发现清单

| # | 严重度 | 攻击向量 | 投喂剧本（内联） | 实际行为 | 期望行为 | 判定 |
|---|---|---|---|---|---|---|
| 1 | **HIGH** | **空白 id 幽灵预算**：budget 键 id 带前导空格（LLM 书写习惯 `"custom: residency"`），永不匹配 claim，gate(2) 对真实资源静默失效 | `{"events":[{"lifetime":[0,100],"loop":1,"scope":{"scene":"A"},"footprint":[{"kind":"occupy","resource":{"custom":"residency"},"mode":"create","size":[1,100]},{"kind":"occupy","resource":{"custom":"residency"},"mode":"release","size":[1,1]}]}],"budget":{"custom: residency":64}}` | **EXIT=0 passed=true**，零违例；同剧本改用正确键 `"custom:residency":64` 则 EXIT=2 `PeakExceeded 峰值 100 > 预算 64` | 空白身份应与空 id 同拒（A1-12 同教义：幽灵条目虚增 CapsChecked 制造「已查」假象），或至少告警 | **实现缺陷**（A1-12/R6-RB-06 防线未覆盖空白变体） |
| 2 | MED | **`type:"global"` 静默丢弃 scene 值**：`{"type":"global","scene":"Battle"}` 的 scene 被 Parse 无声丢弃，与 `{"type":"global"}` 归并为同一 scope | 两个事件分别用 `{"type":"global","scene":"Battle"}` 与 `{"type":"global"}`，各 create 同一 `{"gpu":"s"}` | EXIT=2：两条 create 被归入**同一冲突组**，报 `CompatibleConflict create×create 冲突`×2 + Leak——证明两 scope 实为同一个 | 仓库自身教义是「拒绝而非改写」（R6-RB-04/R3-L1-03）；要么拒绝 `global+scene` 组合，要么文档钉死「global 忽略 scene」。现状是静默改写数据，且 `SerializeScope(Global)` 只输出 `{"type":"global"}`，round-trip 也丢 | **实现缺陷** |
| 3 | MED | **occupy + `mode:"use"` ⇒ Leak**：net 记账把非 release 全按 +1，`use` 结束后净额 [1,1] 不含 0 | `{"events":[{"lifetime":[0,10],"loop":1,"scope":{"scene":"A"},"footprint":[{"kind":"occupy","resource":{"gpu":"g"},"mode":"use","size":[1,1]}]}]}` | EXIT=2：`Leak 生命周期未闭合（净效应不含 0）：[1,1]` | 行为是 SignedNet 代数设计（Use 记 +），但 EFFECT_SCRIPT §3.1 符号表只写「create/move=+、release=−」，`use` 未提——LLM 写 `use` 表达「临时借用」必然吃 Leak 且不知所措 | **文档欠明确**（行为正确，符号表必须补 use 行） |
| 4 | MED | **双解析器方言漂移**：契约侧 `ReqStr` 只拒空串+控制字符（`EffectScriptContract.cs:413-420`），配置侧 `CosmosEffectConfig.ReqStr` 用 `IsNullOrWhiteSpace` 且**不查控制字符**（`CosmosEffectConfig.cs:206-219`） | （代码只读对照；CLI audit 仅走契约侧，无法经命令行投喂配置侧）`" "`（纯空格 id）：契约侧收（实测，见 #1 链路），配置侧拒；`"\u0000"`：契约侧拒（实测 #11），配置侧**不拒** | 同一字符串在两条解析路径一收一拒 | README/schema 宣称同界（「与 EffectScriptContract 同口径/同界」），漂移本身违承诺；控制字符在 cosmos.effect.json 侧可入身份串破坏白名单分组日志 | **实现缺陷**（漂移；附实测：契约侧 `" "` id 剧本 passed=true） |
| 5 | LOW | **报错消息回显裸 NUL**：拒绝控制字符的消息把原始 key 原样拼进文案 | `{"budget":{"gpu:\u0000x":64}}` | EXIT=1，消息正确指出 `含控制字符 U+0000`，但 `od -c` 逐字节验证消息本身内嵌裸 `\0` 字节（`budget 键 "gpu:\0x" …`） | 提示控制字符的消息自身不应含控制字符（正是 R6-RB-06 要防的下游日志/NUL 场景）；应转义或省略回显 | **实现缺陷**（诊断面） |
| 6 | LOW | **CLI 载荷缺 `CapsChecked`**：`AuditResult.CapsChecked`（R1-HIGH-3 专设的「没查 vs 查过全绿」判别字段）未被 `cosmos audit` 序列化 | 任意带 budget 剧本（如 #1）的 stdout 载荷 | 载荷只有 `passed/events/violations`，AI 闭环无法从输出区分「预算门没运行」「运行了但匹配到幽灵键」「真查过」 | 工具面输出 `capsChecked`，与 #1 复合（幽灵预算假绿不可见） | **实现缺陷**（工具面；库内字段存在，仅 CLI 未透出） |
| 7 | LOW | **[0,⊤] 的 Leak 文案撒谎**：`ContainsZero` 对 ⊤ 端恒 false（`SignedNet.cs:104` fail-closed），但消息断言「净效应不含 0」 | `{"events":[{"lifetime":[0,"⊤"],"loop":3,"scope":{"scene":"A"},"footprint":[{"kind":"occupy","resource":{"gpu":"m"},"mode":"create","size":[1,9223372036854775807]},{"kind":"occupy","resource":{"gpu":"m"},"mode":"release","size":[1,1]}]}],"budget":{"gpu:m":64}}` | EXIT=2：`Leak …（净效应不含 0）：[0,⊤]`——0 明明 ∈ [0,⊤] | 报告本身 fail-closed 正确（⊤ 不可证闭合 ⇒ 人工确认），但理由文案数学上为假，会误导回修方向 | **文档欠明确**（消息文案；行为正确） |
| 8 | LOW | **`"∞"` 别名幻想**：EFFECT_SCRIPT §4 写「`"⊤"/"inf"`（∞）」，括注诱导 LLM 以为 ∞（U+221E）合法 | `{"events":[{"lifetime":[0,"∞"],…}]}` | EXIT=1：`events[0].lifetime.hi: 端点须为数字或 "⊤"/"inf"` | 拒绝正确（方言冻结），但文档应删掉 `（∞）` 括注或消息里显式点名 ∞ 不合法 | **文档欠明确** |
| 9 | LOW | **`dotnet run` 偶发空输出**（tooling 观察，非契约违约）：快速循环投喂时 8 次中 2 次 stdout 为 0 字节文件；改直接调 dll 后 15/15 + 12/12 正常 | 同一 30-Leak 剧本循环 8 次 `dotnet run … > out.json` | 2 次空文件（stderr 当时被丢弃未捕获原因）；复跑 15 次带 stderr 捕获 0 复发 | 闭环脚本应：调 publish 的 exe/dll、校验 stdout 非空 + 退出码 ∈ {0,1,2}，异常则重试 | 行为正确（环境一次性抖动，建议闭环侧防御） |
| 10 | LOW | **create/release lifetime 不相交（僵尸持有）**：create [0,10] + release [20,30]，中间 10 年缺口无任何存活持有者 | `{"events":[{"lifetime":[0,10],…create gpu:g…},{"lifetime":[20,30],…release gpu:g…}]}` | **EXIT=0 passed=true**。净额 [0,10]=+1（正，非负陷）、闭包 +1−1=0（守恒）、缺口期无 gate 覆盖 | 净额代数只看符号闭合，设计如此；反向（release 先于 create）会正确报 NegativeDip。但 LLM 可借缺口期「无主持有」骗过机审 | **行为正确**（建议补进文档「已知锐边」：缺口期无存活持有者不报） |
| 11 | LOW | **10KB 超长资源 id**：单 id 10240 字符 | python 生成 `{"gpu":"xxxx…(10KB)"}` create+release 成对剧本 | EXIT=0 passed=true，总耗时 1.2s（含启动）。无长度上限、无截断、无消息定位问题（因为不拒绝） | 可接受（JSON 内存模型天然有界）；如担心日志爆炸可加 maxLength，非必需 | **行为正确** |
| 12 | — | **ulong 边界值**：lifetime.hi=18446744073709551615（收）/ 18446744073709551616（拒） | `[0,18446744073709551615]` 与 `[0,18446744073709551616]` | 前者 EXIT=0 passed=true（MaxValue 端点采样/闭包幽灵点规则均正确处理）；后者 EXIT=1 `events[0].lifetime.hi: 端点须为非负整数或 "⊤"/"inf"` | — | **行为正确** |
| 13 | — | **超大指数记法**：`1e19`（lifetime）、`1e+19`（loop）、`18446744073709551616`（budget 值/memory 键段） | 四个位置分别投喂 | 全部 EXIT=1，消息带定位（`events[0].lifetime.hi` / `events[0].loop` / `budget["gpu:m"] … 实际为 Number` / `memory 段须为非负整数（实际 "…"）`） | R6-RB-05「整数须十进制字面量」四位置口径一致 | **行为正确** |
| 14 | — | **负零与小数**：lifetime.lo=`-0`；budget 值=`64.5` | `[-0,10]` / `{"gpu:m":64.5}` | 均 EXIT=1（TryGetUInt64 方言拒绝），消息带定位 | 负数/小数不漏 BCL 异常（rich-hickey2 R2-006） | **行为正确** |
| 15 | — | **前导零**（JSON 语法层）：`[0120,10]` | 原始文本含 `0120` | EXIT=1：`JSON 非法: Invalid leading zero before '1'. LineNumber: 0 | BytePositionInLine: 25` | 外层翻译进 FormatException 方言（R3-L1-01），行列可定位；错误正文为 STJ 英文原文属可接受 | **行为正确** |
| 16 | — | **⊤/inf 别名跨位置一致性**：`lifetime.hi`、`size` 端点、`loop`、budget 值四处分别用 `"⊤"` 与 `"inf"` | `{"lifetime":[0,"inf"],"size":["⊤","⊤"],"loop":"⊤","budget":{"gpu:m":"inf"}}` 等组合 | 四处**全部一致接受**（单一真源 `IsTopAlias`）；`lifetime.hi="⊤"` 绿、`size ["⊤","⊤"]` 绿（未知区间合法） | A1-07「三处均认双形」承诺实测成立 | **行为正确** |
| 17 | — | **大小写/位置变体**：`"INF"`/`"Inf"` 在 lifetime.hi、loop、budget 值三处 | 三处分别投喂 | 三处**全部一致拒绝**（大小写敏感），消息各带定位 | 方言冻结：仅小写 `"inf"`；拒绝口径三处一致 | **行为正确** |
| 18 | — | **lo 位置 ⊤**：`lifetime.lo="inf"`；`size ["⊤",5]` | 两剧本 | 分别 EXIT=1：`下界不可为 "⊤"/"inf"（[⊤,⊤] 非法…）` 与 `[⊤,5] 非法（…[⊤,⊤] 表示未知区间，合法）`——两者规则不同且消息各自说清 | lifetime 拒 lo=⊤（防假绿）、size 允许 [⊤,⊤] 拒 [⊤,x]（A1-01） | **行为正确** |
| 19 | — | **Unicode 转义 id**：`\u00e9`（café） | scope `{"scene":"Caf\u00e9"}`、id `caf\u00e9-mesh` | EXIT=0 passed=true，非 ASCII 身份串合法 | 身份串只禁控制字符，Unicode 放行正确 | **行为正确** |
| 20 | — | **控制字符身份串**：`\u0000`（resource id）、`\u001F`（scope name） | `{"gpu":"a\u0000b"}` / `{"scene":"hud\u001F"}` | 均 EXIT=1：`…含控制字符 U+0000/U+001F（身份串不可含 U+0000–U+001F）`，带 `events[0][0].resource.gpu` / `events[0].scope.scene` 全路径 | R6-RB-06 落地（消息回显缺陷见 #5） | **行为正确**（除 #5 消息污染） |
| 21 | — | **深层嵌套**：`scope.scene` 塞 6 层嵌套对象；`events` 元素为数字；claim 为数字；`events` 为对象；`lifetime` 为对象/单元素数组；`loop` 为字符串 `"1"`；`resource.gpu` 为数字；`resource.memory` 为字符串 `"5"`；空 scope `{}`；budget 值字符串 `"5"`；`$schema` 非字符串；根缺 events；尾逗号 | 13 个独立反例 | 13/13 EXIT=1，全部 FormatException 且消息带 `events[N]`/字段级定位，无一条裸 BCL 异常 | fail-fast 白名单（R6-E1/R6-RB-01/R3-L1-03 等）实测全数撑住。小瑕疵：`events[0][0].resource层重复键` 的「resource层」连读略怪（纯文案） | **行为正确** |
| 22 | — | **重复键四层**：根 `events` 双写、resource `gpu` 双写、claim `kind` 双写、budget `gpu:a` 双写 | 四个剧本 | 四层全部 EXIT=1 `…层重复键 "…"（last-win 会静默丢弃前值，拒绝而非改写）`——含最危险的「一空一非空 events 假绿」向量 | R6-RB-04 实测全层覆盖 | **行为正确** |
| 23 | — | **规模与耗时**：1000 事件错峰自守恒剧本；1000 事件同资源同 scope 重叠 create | 两个 1000 事件剧本（后者预算 cap 2000） | 分别 1.4s / 1.1s 总耗时（含 dotnet 启动约 1.0s），扫换线秒级达标；违例正确产出 | iter-effect26 性能收口承诺（1000 事件 <5s） | **行为正确** |
| 24 | — | **空数组**：`{"events":[]}` | 空剧本 | EXIT=0 `passed:true, events:0`——载荷显式携带事件数，非不可见假绿（R3-CG-08） | — | **行为正确** |
| 25 | — | **claim scope 继承 + type=global**：claim 省略 scope 继承事件级 `{"type":"global"}` | create+release 成对、claim 无 scope | EXIT=0 passed=true（R10 Top1 继承语义工作正常）；反向用例：claim 显式 `{"scene":"B"}` vs 事件 `{"scene":"A"}` ⇒ EXIT=1 `claim scope 须与所属 event scope 一致（…claim=Scene { Name = B }, event=Scene { Name = A }）` | — | **行为正确** |
| 26 | — | **kind×resource/mode 矩阵**：`read+gpu+use`；`read+create`；同 Signature 重复 claim | 三个剧本 | `read gpu use` EXIT=0（read 桶无守恒义务，读 GPU 合法）；`read create` EXIT=1 `非法 Kind×Mode：Read+Create（Read 仅允许 Use/Unknown…）`；重复 claim EXIT=1 `重复 Claim(Occupy,Gpu…)：Signature 是集合…并发表达须用 Combination.Loop…（P0-4）` | kind×resource 全组合放行、kind×mode 由 `Claim.Normalize` 把关、重复 claim 拒绝且消息直接给出正解 | **行为正确** |
| 27 | — | **P0-A1 双 ⊤ 语义**：`loop:"⊤"` 居民 create-only + cap；`lifetime.hi="⊤"` 有限 ω create-only + cap | 两剧本（budget `{"gpu:g":1}`） | 前者 EXIT=2 **仅** `PeakExceeded 峰值 ⊤ > 预算 1`（守恒豁免、峰值照查）；后者 EXIT=2 `PeakExceeded 峰值 2 > 预算 1` **且** `Leak`——两侧结论相反，与文档钉逐字一致 | P0-A1/QedP0A1SemanticDecisionTests 承诺 | **行为正确** |
| 28 | — | **确定性**：13 违例混合剧本（4 Leak×不同资源类型 + 2 组 create×create 冲突 + 2 组 NegativeDip + PeakExceeded）与 30-Leak 剧本 | 各投喂 5/8/12/15 次（跨进程） | **38+ 次运行输出逐字节一致**（含违例顺序，md5 唯一）。顺序由 Dictionary entries 插入序决定（实测非桶序；`grp` 的 Remove/重插走 free-list 仍确定）；字符串哈希按进程随机化（`"abc".GetHashCode()` 5 次运行 5 个不同值）但**不影响** .NET Dictionary 枚举序 | 当前运行时上确定性成立。但顺序属实现细节而非契约——.NET 未来若改枚举策略即破；建议工具侧对 Violations 显式排序把顺序钉进机器契约 | **行为正确**（附加固建议） |

---

## 值得表扬（防线撑住的部分）

1. **「静默改写比报错危险」教义全线落地，且实测无死角**。重复键（根/事件/claim/resource/budget/scope 六层）、未知键白名单（含 resource 的 `{已知键+拼写键}` 组合）、`type:"global"` 之外的 scope 降级、多键 resource——全部 loud 拒绝。最毒的「根级 events 一空一非空假绿」向量被 `RejectUnknownKeys` 的 `seen.Add` 单点封死（#22 实测）。
2. **数值与别名方言单一真源，40 次投喂零漂移**。`TryGetUInt64` + `IsTopAlias` 两个单点覆盖 lifetime/size/loop/budget/memory 键段全部位置：`1e19`/`1e+19`/`-0`/`64.5`/前导零/超 ulong 一律 `FormatException` 带定位，`"⊤"/"inf"` 四处一致收、`"INF"/"∞"` 一律拒，无一处漏 BCL 裸异常。溢出 `hi×ω` 环绕⇒⊤ 的 fail-closed（`峰值 ⊤ > 预算 64`）在真实投喂中得到验证。
3. **失败消息的可定位性是真实的，不是宣传**。20+ 条 FormatException 无一例外携带 `events[N][M]`/字段全路径（含 `claim=…, event=…` 双侧对照这种高质量归因）；重复 claim 的报错甚至直接教 LLM 正确写法（「并发表达须用 Combination.Loop…」）。对「AI 拿报错自回修」的闭环场景，这个消息质量直接决定回修成功率。

---

## 附：审计方法与可复现性

- 每条「实际行为」均为真实投喂记录（非推断）；退出码逐一核实（早期两轮投喂的管道 `EXIT=$?` 取的是 `head` 的返回值，发现后已全部改为命令替换捕获重跑）。
- 确定性结论曾两度自我纠错：8 次循环中 2 个空文件初判为「顺序不确定」，排查后（a）空文件是 `dotnet run` 环境抖动且 15/15 复测无复发；（b）「顺序异常」是本报告 grep 正则未匹配含空格的 `Custom { Name = ck1 }` 所致——最终以「字符串哈希按进程随机化 + Dictionary 枚举序仍逐字节一致」的实证组合收口（#28）。
- 对照解析器漂移（#4）为代码只读结论（CLI 无投喂配置侧入口），已注明行号；其余 27 条均有投喂证据。
