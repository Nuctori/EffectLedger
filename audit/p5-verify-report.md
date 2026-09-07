# 修复验收报告

> P5.2 对抗审计「立即修」批次修复验收（2026-09-08）。审计对象：HEAD（6d0cb0b）工作区相对三份
> 审计文件（`audit/p5-auditor-consumer.md`、`audit/p5-auditor-redteam.md`、`audit/p5-adversarial-meeting.md`）
> 处置表中「立即修」批次的落实质量。方法：只读核对 + git 历史比对 + 本机 Dafny 4.11.0 实证。

## 验收结论

**PASS-WITH-NOTES**

- 12 项「立即修」中 11 项落实（多数超额：H1 双侧诊断通路、M4 计数门本机实证 89 verified、M5 四类违例全数常驻）。
- 1 项明确未落实：消费#11（PUBLISH-CHECKLIST「五包 + Tool 包」措辞矛盾原句仍在）——LOW 级文档行，不阻断。
- 修复质量总体高：未发现破坏性新问题；新发现均为 schema/解析器约束缝隙群（双向皆 loud 或方向安全）与残留措辞，无「静默死亡」类回归。

## 逐条验收表

| 处置项 | 落实状态 | 证据（file:line） | 新引入问题 |
| ------ | -------- | ----------------- | ---------- |
| 红队 H1：extraMappings 根键拼错静默死亡 | ✅ 落实 | 根层白名单 `src/Cosmos.EffectAlgebra/CosmosEffectConfig.cs:31-44`（只认 `$schema`/`extraMappings`，其余键 loud FormatException）；typo 钉 `tests/Cosmos.EffectAlgebra.Tests/QedP2C1aMergedWhitelistPins.cs:49-57`（三种拼写键 InlineData + 断言消息含「根键」）+ 钉 4b `$schema`-only 合法形态（:59-64）。**「真红过」成立**：父提交 16d5e06 的旧实现为 `if (!root.TryGetProperty("extraMappings", out var arr)) return ImmutableArray<ApiMapping>.Empty;`——三种拼写语料在旧代码下静默返回空、不抛，`Assert.Throws<FormatException>` 必红（钉与修同属提交 6b3abf7，TDD 先红后绿的红半边由旧代码逻辑实证）。EAA0701 自动覆盖双侧在位：分析器 `EffectAlgebraAnalyzer.cs:151→154→192`（catch FormatException → ConfigDiagnostic）、生成器 `EffectAlgebraGenerator.cs:100→108-110` | 无（$schema 值类型不校验，见新发现 N2e） |
| 消费#2：audit 命令指向不存在文件 | ✅ 落实（一处残留） | 主 README ⑤ 改指真实文件 `README.md:168`（`audit templates/effect-script.json`），两文件名显式区分 `README.md:170` + FAQ Q4（:207-208）+ ③ 白名单段（:117）。`templates/effect-script.json` 真实存在且形状合法：剧本解析器根白名单含 `$schema`（`EffectScriptContract.cs:36-40`），模板可解析+自洽被 DocGuard 钉死（`ProdAuditBatch2DocGuardTests.cs:27-41`，含「修改前根级 $schema 被拒」的回归注释）；README 命令串与文件存在性亦被钉（同文件 ：154-155） | 无（templates/README.md:37 占位名残留，见新发现 N6b） |
| 消费#3a：severity 行数漂移 | ✅ 落实 | 四处统一「六行」：README ⓪ `README.md:69`（六行+根同款）、② 六行代码块（:92-100，含 EAA0701）、根 `.editorconfig:26-35`（恰 6 条 severity 行）、`templates/README.md:14`（六行）。git diff 确认 ⓪ 由「五行」改「六行」 | 无 |
| 消费#3b：「6 条已解决」计数漂移 | ✅ 落实（口径修正于 ROADMAP，措辞有残留歧义） | 精确口径落在 `audit/qed/ROADMAP.md:348`（M9 清理）：「6 条已解决就地标记（4 条划线：#1/#13/#18/#19；2 条改写扩充：#5→A2 / #12→C1b,c）——原文『划线标记』措辞以此为准」；README:217-218 保留「6 条已解决」总计数（6+14=20 算术自洽） | N5（README:217/PUBLISH-CHECKLIST:28 措辞与 ROADMAP 新口径仍有歧义/不一致） |
| 消费#3c：EFFECT_SCRIPT 字段数漂移 | ✅ 落实 | `EFFECT_SCRIPT.md`（§2.1 末类型强制行）：「**4 字段**位置记录（Lifetime/Scope/Footprint/Loop——上方草样省略 Scope，实际形状见 R4-RH-16 注记）」；与 `src/Cosmos.EffectAlgebra/EffectScript.cs:26-42` 实际 4 字段（Lifetime/Scope/Footprint/Loop）一致；草图偏差由形状注记（R4-RH-16）显式声明 | 无 |
| 红队 M4：CI 安装不钉版本 / curl 无 -f / ps1 无默认值 | ✅ 落实 | `.github/workflows/ci.yml:23`（`--version 4.11.0`）、:25（`curl -fL --retry 2`）、:33-34（verified ≥ 89 计数门，另覆盖 H3 计数钉）；`ci.ps1:10`（DAFNY_Z3 默认值与 ci.sh 对称）。**计数门本机实证**：dafny 4.11.0 对两 dfy 联合 verify 输出**单行程序级汇总** `Dafny program verifier finished with 89 verified, 0 errors`（exit 0）——ci.yml `head -1` 与 ci.sh awk 求和均得 89≥89，门有效且当前树恰为 89 | N3（先前已存在：ci.ps1 的 dafny 退出码被后续 test 覆盖、ps1 无 0-errors/计数断言） |
| 红队 M5：fuzz 自我驯化 | ✅ 落实 | `QedMaintFuzzTests.cs:65-74` 畸形语料扩至 7 类，首轮 4 类违例全数常驻：case 3 memory 字符串值、case 4 Read+Create、case 5 size lo>hi、case 6 claim scope≠event scope（:64 注明 P5.2-M5）；AtT 断言在确定性不变量（:133）与往返闭合（:141）双处，注释「投影点漂移也要红」。量级语料按会议裁决归扩充项（处置原文「量级语料归扩充项」）——按范围验收通过 | 无 |
| 红队 M6：config 无 schema | ✅ 落实 | `docs/cosmos-effect-config.schema.json` 存在（2020-12 dialect、`version: 1.0.0`、x-contract-frozen 冻结声明 ：2-5）；模板指向仓库内可解析相对路径 `templates/cosmos.effect.json:2`（`../docs/cosmos-effect-config.schema.json`，同时落实消费#4）。逐字段对照：根键 {$schema, extraMappings}/资源 6 键且至多一键/scope 形状（scene 或 type，type 四枚举）/kind 3 枚举/mode 5 枚举——与 `CosmosEffectConfig.cs` 解析白名单一致（:39、:163-171、:189-204、:147-157） | N2（约束缝隙群：schema 根层 required 比解析器严、size 缺 lo≤hi 与 ⊤,finite 规则等，详见新发现） |
| 消费#9：双标题 + 韩文字符 | ✅ 落实 | 「### 能做什么」改为「### 核心能力」（`README.md:35`），重名消除；Unicode Hangul 扫描（AC00–D7AF）README/EFFECT_SCRIPT/templates README/PUBLISH-CHECKLIST 零命中 | N6a（空「### 核心能力」标题残留） |
| 消费#10：EFFECT_SCRIPT 状态行过时 | ✅ 落实 | 状态行改为「设计冻结（已闭环 30 轮迭代对抗审计，轨迹见 §10…）」（`EFFECT_SCRIPT.md:10`）；「下一轮迭代补」规划残留全库零命中；§7 已改名「范围外声明」（:209，兼落实消费#5 的改名半项） | 无 |
| 消费#11：五包 + Tool 包措辞矛盾 | ❌ **未落实** | `PUBLISH-CHECKLIST.md:19` 原句「五包走查成功（nupkg × 5 + Tool 包）」原样保留；§4「五个 nupkg」清单（:31）已含 `.Tool`，读作 6 个的矛盾未消除。该文件在修复提交（6b3abf7）之前已入库（2c095f9），属修复批次遗漏 | —— |
| 消费#12：缺 .NET 10 SDK 硬前置 | ✅ 落实 | `README.md:55` ⓪ 顶部：「前置：.NET 10 SDK，并以 `dotnet build` 构建（VS 内置 MSBuild 宿主对分析器静默不加载，见 ① 注记）」 | 无 |
| M8 短期：行为级冻结标记 | ✅ 大部分落实 | 【冻结：…变更=semver major】内联标记：#6（README:274）、#10（:278）、#11（:279）、#15（:283）、#16（:284）+ ③ 一般冻结（:113）；5/6 到位 | N4（#7 无内联冻结标记） |
| H2 短期：#18 internal 非安全边界声明 | ✅ 落实（超额） | `README.md:286`：「**边界定性：internal 面是文档性边界而非安全边界**——IVT 按程序集名匹配、无公钥，同名程序集可伪造友元访问 internal（红队 P5.2-H2，强命名决策挂 P5.3 队列）」；后续提交 6d0cb0b 无争议加固：SampleGame 友元程序集命名空间化 | 无 |
| 附：消费#1 部分（⓪ 源码引用提为默认路径） | ✅ 落实 | `README.md:55`：「发布前的默认接入路径是 ① 的源码引用」 | 无 |
| 附：消费#8 部分（CHANGELOG 首发 + 清单补步骤） | ⚠️ 半落实 | `CHANGELOG.md` 存在且为 1.0.0 计划首发内容（冻结声明+新增分组）；但 `PUBLISH-CHECKLIST.md` §7「发布后同步」无 CHANGELOG 维护步骤（全文件 changelog 零命中） | N7 |

### 「特别警惕」项专项核查结果

1. **根层白名单是否误伤 $schema-only 合法形态**：否。钉 4b 显式钉为合法（`QedP2C1aMergedWhitelistPins.cs:59-64` 返回 Empty=回落内置表）；模板形态（$schema+extraMappings）同过。
2. **EAA0701 进根 .editorconfig 是否影响本仓库自身构建**：否。全仓仅 `tests/GateFixture/ExtendedWhitelist` 经 AdditionalFiles 消费 cosmos.effect.json，其配置与仓库根 `cosmos.effect.json` 均只含 `extraMappings` 单一根键——新白名单下解析通过，EAA0701=error（`.editorconfig:35`）不会触发；三个 fixture 各有自身 .editorconfig，主 src 工程无配置文件注册。
3. **config schema 与解析器的严松缝隙**：存在，见新发现 N2（两处「schema 过但 Parse 拒」+ 一处「Parse 过但 schema 拒」+ 一处解析器侧微小静默点）。

## 新发现问题

| # | 严重度 | 发现 | 证据 |
| - | ------ | ---- | ---- |
| N1 | LOW | **消费#11 遗漏**（唯一未落实项，已列入上表）：`PUBLISH-CHECKLIST.md:19` | 处置表明文「立即修」，原句未动 |
| N2 | LOW | **config schema 与解析器约束缝隙群（M6 修复引入）**：(a) schema 根层 `required:["extraMappings"]` 比解析器**严**——`$schema`-only 文件 Parse 通过（钉 4b 认可为合法空扩展）但 schema 校验拒，两种官方工具给出矛盾信号；(b) schema 的 size 只约束「两项、各为非负整数或"⊤"」，**缺 lo≤hi 与「lo=⊤ 而 hi 有限非法」**——`["⊤",5]`、`[7,5]` schema 过但 Parse loud 拒（解析器 `CosmosEffectConfig.cs:131-132`）；(c) 字符串 `minLength:1` 弱于解析器的 `IsNullOrWhiteSpace`（纯空白 api/gpu 名 schema 过 Parse 拒）；(d) memory 无上限（>2^64-1 schema 过、`TryGetUInt64` 拒）。方向均安全（两侧皆拒/loud），但 schema 不是契约的精确镜像，「按 schema 校验通过 ⇒ Parse 必过」不成立 | `docs/cosmos-effect-config.schema.json:9,54-63` vs `CosmosEffectConfig.cs:44,131-132,55` |
| N2e | LOW | 解析器对根级 `$schema` **值不校验类型**：`{"$schema": 5}` 被静默当合法空扩展回落内置表（无 EAA0701）；对照 effect-script 契约侧同键校验「须为字符串」loud（`EffectScriptContract.cs:38-39`）——H1「绝不静默」在本通道残留一个微小静默点 | `CosmosEffectConfig.cs:35-44` |
| N3 | LOW | **ci.ps1 dafny 门盲区（先前已存在，M4 只补了默认路径）**：`dafny verify` 的非零退出码被后续三次 `dotnet test` 的 `$LASTEXITCODE` 覆盖，dafny 失败+测试绿 ⇒ 门误报 PASS；且 ps1 无「0 errors」/verified 计数断言，与 ci.sh（awk 求和 ≥89）/ci.yml（head -1 ≥89）不对称 | `ci.ps1:11-17` |
| N4 | LOW | **M8 冻结标记 5/6**：#7（EAA0901 哨兵假配对）有 `[影响：…]` 标签但无内联【冻结】标记，与 #6/#10/#11/#15/#16 不一致；仅由 README:113 的一般性「Σnet 权威判据」冻结间接覆盖，会议回填日志声称六条全含 #7 | `README.md:275` vs :274,278,279,283,284 |
| N5 | LOW | **计数口径残留不一致**：`PUBLISH-CHECKLIST.md:28`「6 已解决划线 + 14 活跃附证据」与 ROADMAP:348 新口径（4 划线 + 2 改写扩充）冲突；`README.md:218`「编号保留划线标记」对 #5/#12（改写扩充、无划线）表述有歧义 | 三处互证 |
| N6 | LOW | **门面残留**：(a) `README.md:35-37` 空「### 核心能力」标题直接叠在「## 能做什么」上（无内容、层级倒置）；(b) `templates/README.md:37` 仍用占位名 `cosmos audit effect.json`——步骤 1「AI 产 JSON」语境下可读为用户文件，但照抄且无该文件即 exit 1（原发现 #2 在此文件的原始引用点未改） | 同左 |
| N7 | LOW | **消费#8 半项遗漏**：发布后同步清单未补「CHANGELOG 维护」步骤（处置明文「发布后清单补 CHANGELOG 步骤」） | `PUBLISH-CHECKLIST.md` §7（37-40 行） |

### 验收说明

- 「真红过」判定方法：钉与修复同属提交 6b3abf7，无法从提交序列直接观察先红后绿；改以**父提交 16d5e06 的旧实现逻辑实证**——旧代码对三种拼写根键均静默返回空数组（不抛），钉断言 `Assert.Throws<FormatException>` 在旧代码下必红，红半边成立。
- M4 计数门有效性以**本机 dafny 4.11.0 实测**背书（与 ci.yml 钉死的 --version 4.11.0 同版本）：两 dfy 文件作为单程序验证，输出单行汇总 89 verified 0 errors，`head -1` 与 awk 两种取法均正确；不存在「每文件一行导致 ci.yml 只数第一个文件（67<89 永久红）」的风险。
- 自构建影响（EAA0701 入根 .editorconfig）：全仓 AdditionalFiles 消费面仅 ExtendedWhitelist fixture，其配置与根目录 cosmos.effect.json 均合规，无自身构建破坏路径。
