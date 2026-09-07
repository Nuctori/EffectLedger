# 产品门面复审报告（第二轮）

> 视角：第二次到访的挑剔用户，按 README → FAQ → 诚实边界 → CHANGELOG → EFFECT_SCRIPT → templates/PUBLISH-CHECKLIST 顺序复审上一轮 12 条发现修复后的整体状态。
> 方法：只读。对修复声明做源码/测试实证（快照类型数、Dafny 定律门、诊断 ID、EffectEvent 字段、schema 文件、GateFixture），引用格式 `file:line`。

## 总评

产品化成熟度 **7/10**（上一轮约 4/10）：第一屏（徽章/白话 tagline/文档地图/FAQ/CHANGELOG）已达到可发布库的门面水准，且多数修复经源码实证为真（43 类型快照、6 个诊断 ID、4 字段、schema 文件、EAA0701 进 .editorconfig 全部核实）。失分集中在「诚实边界」这一旗舰节的导航与计数：文档地图/FAQ/CHANGELOG 三处把「20 条」指向只有 4 条短笺的「已知语义锐边」，「6 条已解决」与实际 4 条划线不符且被本轮新增的 CHANGELOG 再次传播——这正是本项目品牌承诺（机器验证级精确）最不能容错的部位。另有 templates/README 漏改的悬空命令与 FAQ Q3 的误报/漏报方向混淆两处中等问题。

## 新发现清单

| # | 严重度 | 发现 | 证据（file:line） | 建议 |
|---|--------|------|-------------------|------|
| 1 | HIGH | 「6 条已解决」与实际 4 条划线不符（#1/#13/#18/#19），且本轮修复把该错误计数**新增传播**进 CHANGELOG（「6 条已在 QED 路线中解决」）——上一轮 #3(b) 未修反扩一处。被「E3 复核」封印的旗舰清单，主计数即错 | README.md:217（声明）vs README.md:269,281,286,287（实划 4 条）；CHANGELOG.md:28；PUBLISH-CHECKLIST.md:27 | 要么补划 2 条（若 #12/#17 算已解决则显式标注），要么把三处「6 条」改「4 条」；并把「划线条数==已解决计数」钉进 doc-guard 测试 |
| 2 | MED | 「20 条」的官方导航全部落空：文档地图「已知边界（20 条）→ 本页『已知语义锐边』」、FAQ 两处「见下节」、CHANGELOG「见主 README『已知语义锐边』」、EFFECT_SCRIPT §7 注「主 README 的 20 条『已知语义锐边』」——但 20 条实际在「诚实边界」节；「已知语义锐边」节只有 4 条短笺，其头注却称「下列 20 条」。EFFECT_SCRIPT 同文件 :182 又称「README 诚实边界 #17」，一文两名。上轮 #5 宣称「完整落地」未达成 | README.md:31,196,211,215,217,267；CHANGELOG.md:28；EFFECT_SCRIPT.md:209 vs :182 | 统一法定名（建议 20 条列表节名定为「诚实边界」，或把「已知语义锐边」改为其别名注记）；修 README:31 文档地图锚点与 FAQ「下节」措辞；把「已知语义锐边」节的 E3 头注移到 20 条列表头部 |
| 3 | MED | FAQ Q3 把 #7/#20 归入「保守方向的提示」（误报方向）例证，但边界 #7/#20 是**漏报/掩蔽**方向（Load+QueueFree 假配对抵消、Connect+Disconnect 折叠 net=0），与同句「漏报方向被结构性压制」自相矛盾；且 #7/#20 的兜底是运行期 Σnet 而非 `[EffectOverride]` | README.md:205 vs README.md:275(#7),288(#20) | Q3 拆成两向各举一例：误报→#8（[EffectOverride] 出口）；漏报→#7/#20（Runtime 权威兜底） |
| 4 | MED | templates/README.md:37 `cosmos audit effect.json --out violations.json` 仍指仓库不存在文件，照抄 exit 1——上轮 #2 只修了主 README（:168 已改 `templates/effect-script.json`），两文档命令现在不一致 | templates/README.md:37 vs README.md:168 | 改为与主 README 同一命令（或显式写「effect.json 为 AI 产物占位名」并给可跑样例 `samples/effect-sample.json`） |
| 5 | MED | 「按组件速查」只覆盖 18/20：#18/#19 缺席，而同为已解决的 #1/#13 在列——收录规则不一致；另 16 条活跃项中 #12/#17 无【影响】标签（承诺「逐条」） | README.md:221-223（速查全文）；README.md:280(#12),285(#17) 无标签 | 速查补「已解决不路由：#18/#19」一行或补全映射；#12 补「影响：配置错误会整体弃用扩展（loud）」、#17 补「影响：catch 面接错族会漏异常」 |
| 6 | LOW | EFFECT_SCRIPT 正文引用不存在的 §7.2（§7 改名后只有 §7.1），L2/L3 剧本生成实为 §7.1 内容 | EFFECT_SCRIPT.md:205 vs :221 | 「§7.2」改「§7.1」 |
| 7 | LOW | PUBLISH-CHECKLIST 两处陈旧：「nupkg × 5 + Tool 包」读作 6 个（上轮 #11 未动）；「Unknown 三维契约（#1）/双 ⊤ 两轴（#2 前身 A1）」的编号与现 README 错位（现 #1=Sequence≡Parallel、#2=Size 散布，Unknown/双⊤ 是「已知语义锐边」无编号短笺） | PUBLISH-CHECKLIST.md:19,26 vs README.md:269,270,225,226 | :19 改「五包（含 Tool）」；:26 改引「已知语义锐边短笺 1/2」或给 4 条短笺编号（S1–S4） |
| 8 | LOW | 模板 `$schema` 用仓库相对路径：复制到消费工程（模板的既定用途）后指向不存在的 `../docs/`；Parse 读后丢弃不受影响，但仓外 JSON 校验器场景悬空（上轮 #4 的替代方案残余；仓内校验链路已通） | templates/effect-script.json:2、templates/cosmos.effect.json:2 | 模板保留相对路径但加注释「复制出仓后可删此键或改 raw.githubusercontent 绝对 URL」 |
| 9 | LOW | 「89 条 Dafny 定律」口径未解释：formal/ 两文件 lemma 声明实为 46+16=62 个，89 是 CI 的 `dafny verify` verified 计数门（≥89）——对「每个数字可机械核对」的品牌，应写明口径 | formal/CosmosEffectAlgebra.dfy:46 声明、formal/CosmosSweepLine.dfy:16；ci.sh:14、.github/workflows/ci.yml:34 | README:14 补半句「89 = dafny verified 计数（CI 门 ≥89），含定律拆分条件」 |
| 10 | LOW | 术语残留：README 三处裸「§7 白名单」未注明指向 PDR §7，与 EFFECT_SCRIPT §7（范围外声明）撞号；EAA0304 有三种叫法（README「并发冲突模式」/CHANGELOG「兼容冲突」/分析器「全函数冲突」） | README.md:46,117,238；CHANGELOG.md:16；src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs:23 | 首次出现写「PDR §7 白名单」；EAA0304 统一「兼容冲突（Compatible 违约）」 |
| 11 | LOW | 打磨缺项：README:35/37 仍叠双标题（「### 核心能力」+「## 能做什么」，上轮 #9 半修——韩文字符已清）；无支持渠道（Issues/Discussions/CONTRIBUTING）；无最小可跑示例工程（samples/ 仍仅测试夹具）；Godot 版本要求（4.x C#）全库无文档。FAQ 可补的高频问：「支持哪个 Godot 版本」「分析器对构建时长影响多大」 | README.md:35-37；samples/ 清单；docs/spatial-plugin-shell-design.md:163（Godot 4.6.3 仅深埋于此） | 合并双标题；README 尾部补 Issue 链接；FAQ 增第 6/7 问或 README ③ 注明「Godot 4.x C#」 |

**实证记录**（本轮抽查全部通过项）：公共 API 快照恰 43 类型（`tests/Cosmos.EffectAlgebra.Tests/PublicApiSnapshot.Cosmos.EffectAlgebra.txt` 43 个顶层条目）✅；6 个诊断 ID EAA0901/0303/0304/0801/0802/0701 全部在分析器中 ✅；`EffectEvent` 恰 4 字段 Lifetime/Scope/Footprint/Loop（EffectScript.cs:26-39），EFFECT_SCRIPT.md:74「4 字段」已一致 ✅；`EffectScript` 为 sealed partial class 与 §2.2 注记一致（EffectScript.cs:71）✅；.editorconfig 恰 6 行 dotnet_diagnostic 含 EAA0701，与 README「六行」/templates/README「六行」三方一致 ✅；`docs/cosmos-effect-config.schema.json` 已存在且两 schema 均用标准 2020-12 dialect、version 1.0.0 与 Directory.Build.props:18 一致 ✅；CHANGELOG 白名单扩展声明的钉 `QedP2C1a/C1b/C1c` 三文件与 `tests/GateFixture/ExtendedWhitelist` 全部在位 ✅；模板被 Parse 接受有 doc-guard 实钉（ProdAuditBatch2DocGuardTests）✅。

## 上一轮修复验收速记

| 上轮 # | 主题 | 状态 | 速记 |
|---|------|------|------|
| 1 HIGH | 首条命令必败 + 无最小示例 | 部分 | ⓪ 已把源码引用提为默认路径 + .NET 10 SDK 前置（README.md:55）✓；samples/ 最小消费工程仍未提供 ✗ |
| 2 HIGH | CLI 命令指向不存在文件 | 部分 | 主 README ⑤ 已改 `templates/effect-script.json` + 文件名区分注记（README.md:168,170）✓；templates/README.md:37 漏改（新发现 #4）✗ |
| 3 HIGH | 数字漂移群 | 部分 | 六行 severity 三方一致 ✓；5→4 字段已修 ✓；**6-vs-4 划线未修且新增 CHANGELOG 一处**（新发现 #1）✗；数字机械钉进 CI 未做 ✗ |
| 4 MED | Schema 校验链 | 已解决 | config schema 文件已建、2020-12 dialect、模板 $schema 指仓库文件；残余仅仓外相对路径悬挂（LOW #8） |
| 5 MED | 术语/文档地图 | 部分 | EFFECT_SCRIPT §7 已改名「范围外声明」✓；但 README 双节并存、三文档仍以「已知语义锐边」指 20 条（新发现 #2）✗ |
| 6 MED | 边界台账→消费者文档 | 部分 | 14/16 活跃条目有【影响】标签、按组件速查已加 ✓；速查缺 #18/#19、#12/#17 无标签、票据代号仍内联（新发现 #5） |
| 7 MED | 黑话前置 | 已解决 | README.md:9 白话 tagline（AddChild/QueueFree→EAA* 警告、JSON 剧本机审）精准到位，第一屏信息层级清晰 |
| 8 MED | 缺 CHANGELOG/FAQ/示例/支持渠道 | 部分 | CHANGELOG.md 首发 ✓、FAQ 前 5 问 ✓（Q3 有方向性错误，见新发现 #3）；最小示例工程 ✗、支持渠道 ✗ |
| 9 LOW | 双标题/韩文字符 | 部分 | 「대」已清 ✓；双标题仍在（新发现 #11）✗ |
| 10 LOW | EFFECT_SCRIPT 状态行自相矛盾 | 已解决 | 状态行改「已闭环 30 轮…轨迹见 §10」（:11）✓；§8 规划残留改「已纳入迭代回归集」（:240）✓；新增仅 §7.2 悬空引用（LOW #6） |
| 11 LOW | 「五包 + Tool 包」 | 未动 | PUBLISH-CHECKLIST.md:19 原文保留（新发现 #7 并案） |
| 12 LOW | SDK 硬前置未声明 | 已解决 | ⓪ 顶部「前置：.NET 10 SDK，并以 dotnet build 构建」（README.md:55）✓ |

**合计**：已解决 4 ｜ 部分 7 ｜ 未动 1。
