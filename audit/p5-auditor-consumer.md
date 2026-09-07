# 外部新消费者审计报告

> 审计视角：从未接触过本项目的 Godot C# 开发者 / 技术选型者，按「README → EFFECT_SCRIPT.md → schema + templates → PUBLISH-CHECKLIST」的首次接触顺序评审。
> 方法：只读评审；对文档声明抽查 6 处去源码验证（诊断 ID、CLI 退出码、契约 API、测试钉、.editorconfig、类型形状）。所有引用格式 `file:line`。

## 总评

工程纪律和可证伪性是我见过的独立库里顶级的（README 示例被 CI 测试钉住、契约面冻结、抽查声明绝大多数真实存在），但**产品化门面明显欠打磨**：新用户照抄的第一条命令必然失败（NuGet 未发布 + `effect.json` 不存在），三处「文档声称 ≠ 文件/代码事实」的漂移直接损害其「机器验证级精确」的品牌承诺，缺 CHANGELOG/FAQ/最小示例工程。当前处于「内部审计完备、外部消费者体验未验收」的 pre-release 状态——诚实有余，消费者视角不足。

## 发现清单

| # | 严重度 | 发现 | 证据（file:line） | 建议 |
|---|--------|------|-------------------|------|
| 1 | HIGH | 「5 分钟上手」第一条命令 100% 失败：⓪ 的 4 条 `dotnet add package` 全部 NU1101（包未发布），实际路径要求 clone 仓库并手改 csproj 相对路径；无预打包的最小可跑示例工程兜底。披露是诚实的，但对每个新用户，「上手 = 从改路径开始」 | README.md:52-61, templates/README.md:5 | 在 samples/ 提供一个 clone 后 `dotnet build` 即出 EAA 诊断的最小消费工程；⓪ 顶部把「源码引用」提为默认路径，NuGet 降级为「发布后」 |
| 2 | HIGH | AI 闭环卖点第一跑就断：⑤ 与 templates/README 的 `cosmos audit effect.json` 引用的 `effect.json` 在仓库不存在，照抄得到 exit 1（read not found）；仓库根目录恰好有个形似的 `cosmos.effect.json`——但那是白名单扩展配置（用途完全不同），喂错会得到「根须含 events」的费解报错 | README.md:167, templates/README.md:37, cosmos.effect.json（根目录）, Program.cs:18-29 | 命令改为指向真实文件：`audit templates/effect-script.json`；并在文档显式区分「剧本 JSON」与「cosmos.effect.json 白名单配置」两个文件名 |
| 3 | HIGH | 「文档声称 ≠ 事实」漂移群（抽查 6 处，3 处不匹配）：(a) README ⓪ 称「② 的**五行** severity」，但 ② 列了**六行**，根 .editorconfig 实际只有五行（无 EAA0701），templates/README 又说「六行」——四种说法两种数；(b) E3 复核称 20 条中「**6 条已解决**」，列表只有 **4 条**删除线（#1/#13/#18/#19）；(c) EFFECT_SCRIPT 铁律称「**5 字段**位置记录」，同节草图是 3 字段，实际代码是 4 字段（Lifetime/Scope/Footprint/Loop） | README.md:68 vs :91-99 vs .editorconfig:26-34 vs templates/README.md:14；README.md:196-197 vs :244,256,261,262；EFFECT_SCRIPT.md:74 vs :56-64 vs src/Cosmos.EffectAlgebra/EffectScript.cs:26-39 | 以脚本（doc-guard 思路已存在）把这些可机械校验的数字钉进 CI：severity 行数、划线条数、字段数，杜绝手工漂移 |
| 4 | MED | Schema 校验链路对 AI 工具链不友好：schema 的 `$id` 与模板的 `$schema` 都指向 `https://cosmos.effect/...`（.effect 不是可解析 TLD），`cosmos-effect-config.schema.json` 在仓库内外都不存在；按惯例用 JSON Schema 校验器跑 templates/effect-script.json 的 AI 会直接「unknown meta-schema」报错。AI 校验闭环是头号卖点，这条边没打通 | docs/effect-script.schema.json:3, templates/effect-script.json:2, templates/cosmos.effect.json:2 | 模板 `$schema` 改用标准 dialect URL（`https://json-schema.org/draft/2020-12/schema`），config schema 要么补文件要么删引用；`$id` 保留作身份即可 |
| 5 | MED | 术语多套并存 + 文档地图指错路：同一份「20 条边界」在文档地图叫「已知边界」并指向「已知语义锐边」（该节只有 4 条 bullet），实际 20 条编号列表在下一节「诚实边界」；PUBLISH-CHECKLIST 又叫「README『诚实边界』20 条」；EFFECT_SCRIPT §7 还有第三个同名但内容不同的「诚实边界」。新用户无法确定「20 条」指哪份 | README.md:30 vs :194-203 vs :242；PUBLISH-CHECKLIST.md:27；EFFECT_SCRIPT.md:209 | 统一命名为一个（建议「已知边界」），文档地图指向精确锚点；EFFECT_SCRIPT §7 改名「范围外声明」 |
| 6 | MED | 已知边界 20 条的组织是「维护者债务台账」而非消费者文档：20 条通篇内部票据代号（R6-P、QED-C1b、A2-09、R7-L1、MA-002…），无按组件分组（剧本审计 / 静态分析器 / Runtime），无「对你意味着什么/何时需担心」的分层。内容诚实，但读完的第一印象是「边界多到不敢用」，而非「这些盲区我有 Runtime 兜底」 | README.md:242-263（如 #12 一条即 4 个钉名，:255） | 每条前置一行「影响：可能误报/漏报/不适用 X」；按「对剧本审计 / 对静态分析 / 对 Runtime」分三组；票据代号收进折叠或附录 |
| 7 | MED | 30 秒价值主张勉强及格但黑话前置：tagline 在第一段就使用 Σnet、闭合判定、L3 分析器等未定义术语；「为什么选它」4 条里 L1/L2/L3、ScopeId 偏序、 SignedNet 均未解释（分层定义在页面后部）。懂静态分析的人能猜到方向，普通 Godot 开发者需要读完整页才明白「装了它我的构建会发生什么」 | README.md:8-15 vs :207-217 | tagline 后加一句白话：「在 `dotnet build` 时对 AddChild/QueueFree 这类 API 报泄漏与冲突警告，JSON 特效剧本不跑游戏即可机审」 |
| 8 | MED | 缺产品级 README 常见项：无 CHANGELOG（契约冻结的项目尤其需要）、无 FAQ、无支持渠道（无 CONTRIBUTING/Issue 模板/Discussions 链接）、无最小可运行示例工程（samples/ 均为测试夹具，AnalyzerConsumer 是「故意泄漏」的门禁装置，直接跑会给新用户错误示范） | 根目录清单（仅 LICENSE/README/PDR 等）；samples/AnalyzerConsumer/Game.cs；PUBLISH-CHECKLIST.md:45-49（发布后同步清单亦不含补 CHANGELOG） | 补 CHANGELOG.md（1.0.0 首发）+ FAQ（前 5 问可直接从 20 条边界提炼）+ samples/HelloEffect 最小工程；Issue/Discussions 链接进 README 尾部 |
| 9 | LOW | 门面机械性瑕疵：README 出现连续两个「能做什么」标题（### 与 ## 各一）；「为什么选它」表格内混入韩文字符「대」（应为「大」）。「每个字符都被机器验证」的品牌承诺被这类瑕疵反噬 | README.md:34-36, README.md:44 | 清理；顺手把「能做什么」表前的空行/重复标题合并 |
| 10 | LOW | EFFECT_SCRIPT.md 状态行过时：头部称「设计冻结（**待** 3 轮独立数学家对抗性审计）」，§10 却记录了 30 轮迭代闭环；§8 末尾残留规划语言「测试（下一轮迭代补）」。状态自相矛盾让「冻结」二字打折 | EFFECT_SCRIPT.md:11 vs :250-265, EFFECT_SCRIPT.md:240 | 状态行改为「已冻结，审计轨迹见 §10」；删除规划残留 |
| 11 | LOW | PUBLISH-CHECKLIST 自身小矛盾：§2 称「五包走查成功（nupkg × 5 **+ Tool 包**）」（读作 6 个），§4 的五个 nupkg 清单里 Tool 已含在内。人类执行发布时会停下来核对 | PUBLISH-CHECKLIST.md:19 vs :31 | 删除「+ Tool 包」或改为「五包（含 Tool）」 |
| 12 | LOW | 快速上手未声明 SDK 硬前置：badge 写 .NET 8.0\|10.0，但分析器 net9.0 / 生成器 net10.0，「仅装 .NET 8 SDK / VS 默认 MSBuild」的用户按 ① 接线会静默零诊断；宿主限制在 ① 末尾注记中有提，但 ⓪/① 顶部没有一行「需要 .NET 10 SDK + dotnet build」 | README.md:5 vs :85, templates/README.md 未提 | ⓪ 顶部加「前置：.NET 10 SDK，且以 `dotnet build` 构建（VS 内置 MSBuild 宿主不支持）」 |

**抽查验证记录**（「文档声称 → 源码核实」）：诊断 ID EAA0901/0303/0304/0801/0802/0701 六个全部真实存在于分析器（EffectAlgebraAnalyzer.cs:59-112）✅；CLI 退出码 0/1/2 契约与 Program.cs:13-56 逐条一致 ✅；`EffectScriptContract.Parse/ToJson`、`FormatException` 方言、`LoadExtra(path, strict)`/`AllWithExtra(...)`、`OccupyClaims`、`IsPeakChecked/CapsChecked`、`Budget.None` 均真实存在 ✅；README 示例确实被 `Round7Hickey2Tests.Readme_Example_ParsesAndAudits`（Round7Hickey2Tests.cs:34-46）真实抽取执行 ✅；引用的钉 `QedP0A4ContractFreezePins`/`ProdAuditR3ToolingTests`/`QedP1B1PublicApiSnapshotTests`、`formal/*.dfy`、`audit/qed/ROADMAP.md`、两个 workflow、`tests/GateFixture/{Leaky,Paired,ExtendedWhitelist}` 全部在位 ✅；不匹配项见发现 #3。

## 值得表扬

1. **doc-as-test 是真功夫**：README ④ 的 JSON 示例不是贴完就完——`Round7Hickey2Tests.Readme_Example_ParsesAndAudits` 从 README 源码里抽取该 JSON 真实 Parse + Audit + 断言，「文档能跑、代码不能跑」这类漂移被 CI 结构性杜绝（README.md:152-160, tests/Cosmos.EffectAlgebra.Tests/Round7Hickey2Tests.cs:34）。绝大多数库做不到这一点。
2. **抽查命中率极高**：6 处声明抽查，诊断 ID、退出码、契约 API、测试钉、文件引用全部真实存在——对一个文档密度如此高的项目，这种「引用可追溯性」（每条边界附钉名、每条冻结附日期）本身就是竞争力，发现 #3 的三处漂移属于少数漏网。
3. **诚实披露不装**：未发布状态直接写在安装命令旁边并给出替代路径（README.md:54）、NuGet 缓存陷阱、退出码「与常见惯例不同」的显式警告（README.md:170）、分析器宿主静默不加载的限制（README.md:85）——没有一条是藏着掖着等用户踩坑的，PUBLISH-CHECKLIST 甚至把「发布后删掉这些临时注记」列为正式步骤（PUBLISH-CHECKLIST.md:45-49）。
