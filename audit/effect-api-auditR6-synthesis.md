# effect-api-auditR6-synthesis — 第六轮独立审计合成（门禁完整性 × 对抗健壮性 × 打包供应链 × 易用性）

- 日期：2026-09-05
- 方法：1 项门禁完整性前置核查（主代理）+ 3 份范围隔离的独立 subagent 审计（各自只写自己的报告、互不读取、独立实验）。
- 本轮审计报告：`effect-api-auditR6-RB.md`（对抗健壮性，51 次投喂）、`effect-api-auditR6-P.md`（打包/供应链）、`effect-api-auditR6-U.md`（fresh-consumer 逐字上手）。
- 收口方式：全程 TDD——每项可钉发现先落红测试再修实现；不可钉项（文档/证伪）附证据链。

---

## 0. 最重要的系统级发现：权威门禁曾连续三红而本地假绿

- GitHub CI（干净机、`-warnaserror` 全量构建）自 `b3d53ed`（audit-batch5）起**连续 3 次失败**：
  1. batch5：`Dependencies lock file is not found`（setup-dotnet 误开 cache）→ 已由 R2A-03 修复；
  2. r2 推送：`CS0219`（未用变量）→ 已由 1ae2183（r3-gate）修复；
  3. r4-R 推送：`xUnit2002`（`Assert.NotNull` 用在值类型 `ImmutableArray` 上，r3-R `0268b2a` 引入）→ **本轮修复**。
- 与此同时本地增量构建**跳过 csc ⇒ 分析器不运行 ⇒ 红代码假绿**，提交信息「字节级确认」与 CI 实况矛盾达两天。
- 收口（`2b9f0a8`）：xUnit2002 断言语义化（`["⊤","inf"]` 须归一 `[⊤,⊤]`，比"不抛即过"更锐利）；
  `ci.sh`/`ci.ps1` 加 `--no-incremental` 与 CI 干净机同语义。**修复后 CI 转绿并保持**（33914689151 起）。

## 1. 发现台账与处置

| ID | 视角 | 严重度 | 处置 | 证据 |
| --- | --- | --- | --- | --- |
| 门禁三红/本地假绿 | 主代理 | **CRITICAL（系统级）** | ✅ 收口 `2b9f0a8`，CI 转绿 | CI run 33914150148(红)→33914689151(绿) |
| R6RB-01 非 object claim 泄 `InvalidOperationException` 违反 FormatException 方言 | RB | MEDIUM | ✅ TDD 收口 `68dcf7e`：ParseClaim 加与 ParseEvent 同型 ValueKind 守卫（3 测试钉） | ProdAuditR6ContractTests.Parse_NonObjectClaim_* |
| R6RB-04 重复 JSON 键 last-win（根级 events 双写=非空剧本静默变空=结构性假绿向量） | RB | LOW→按 R3-L1-03 教义收口 | ✅ TDD 收口 `68dcf7e`：RejectUnknownKeys 单遍枚举内嵌重复键检测 + ParseBudget 单点补齐（3 钉） | Parse_Duplicate*Key_IsRejected |
| R6RB-03 失败消息定位不均（kind/mode 无 events[N]；`resource.` 前缀撒谎；lifetime/loop 缺索引） | RB | LOW | ✅ TDD 收口 `68dcf7e`：ParseKind/ParseMode 带 layer、子路径传入子解析器、ReqStr 去硬编码前缀（4 钉） | Parse_UnknownKind_* 等 |
| R6RB-06 资源/scope 身份串收 C0 控制字符（NUL 破坏日志/互操作） | RB | NOTE→按 A1-12 同口径收口 | ✅ TDD 收口 `68dcf7e`：ReqStr 单点 choke + NonEmptyId budget 键同界（3 钉） | Parse_*ControlChar_IsRejected |
| R6RB-02 10 万异构重叠事件审计 138s（O(S·D) 边界） | RB | MEDIUM | ✅ 已有文档（README 诚实边界 #16：前提、实测曲线钉 `ProdAuditR4AuditScaleTests`）——审计员视角盲区，双向核实后无需改码 | README.md:216 |
| R6RB-05 整数须十进制字面量 / R6RB-07 exit-code 表「仅源码注释」 | RB | NOTE | ✅ 前者补 EFFECT_SCRIPT.md §4；后者**部分证伪**（README ⑤ 已有退出码表+CI 接线示例） | EFFECT_SCRIPT.md:169-172；README.md:133-141 |
| R6P-01 Generator 包依赖不流动、单装+注解即 CS0246 | P | HIGH | ❌ **证伪**（见 §2）→ 真缺陷转为文档面：README ⓪ 补「本地重打包缓存陷阱」 | NUGET_PACKAGES 干净缓存实验 |
| R6P-03 AnalyzerConsumer「EAA0901 真在编译期出现」为绿灯偶然 | P | MEDIUM | ✅ TDD 收口 `92cfd66`：样例加 namespace Godot stub + GodotLeaker 故意泄漏形状；`ProdAuditR6ToolingTests.AnalyzerConsumer_Build_EmitsEaa0901` 真实子进程 build 硬钉「必须触发」 | 先红（0 诊断）后绿 |
| R6P-02 EAA0901 的 Godot.* 命名空间前提未文档化 | P | MEDIUM(文档面) | ✅ README ③ 补「触发前提」（A2-09 设计如此：防同名误报，包装层无保护） | README.md:89 |
| R6P-06 templates tool-install 源路径与 `-o` 打包习惯不一致 | P | NOTE | ✅ templates/README.md 补双分支说明 | templates/README.md:31 |
| R6P-04 无 buildTransitive 仍自动接线（NuGet 约定成立，非假包）/ R6P-05 pack 门干净、`-o` 不重写为 NuGet 原生行为 | P | NOTE | ✅ 确认，无需改码 | r6p-build-diag 日志 |
| R6U-01 ①接线 XML 相对路径无提示 | U | LOW | ✅ README ① 补「路径按消费工程位置调整」 | README.md:47 |
| R6U-02 §4 代码块缺 using | U | LOW | ✅ 代码块补 using 注记（Round7 doc-test 只抽 JSON，不受影响） | README.md:97 |
| R6U-03~07（editorconfig 行内注释实测可用/CLI 样本路径/诚实声明/门控双向实证等） | U | NOTE | ✅ 全部为**正向确认**，无需改码 | r6u-verify/*.log |
| R6-X1 性能曲线钉偶红（单次墙钟采样 12.3x 越线 12x，健康基线 6x） | 收敛复审计 | MEDIUM（偶发假红族） | ✅ TDD 收口：`AuditMsBest` 取 3 次重复最小值（墙钟微基准标准去噪）；阈值 12x 不动，无放松 | ProdAuditR4AuditScaleTests |
| R6-X2 门禁内两个 spawn-build 测试并发触碰同一 src/* obj ⇒ CS2001 偶红（GeneratedMSBuildEditorConfig 竞态） | 收敛复审计 | MEDIUM（偶发假红族） | ✅ TDD 收口：`[Collection("SerialDotnetBuild")]` 串行化 ProdAuditR6ToolingTests 与 ProdAuditBatch4ToolingTests | 两测试文件头部注记 |

## 2. 证伪记录（审计质量的双向核实）

**R6P-01（HIGH）不成立。** 审计员实验受本机全局 NuGet 缓存毒化污染：
- 全局缓存 `cosmos.effectalgebra.generator/1.0.0` 的提取 nuspec **无依赖组**、repository commit=`b3d53ed`（batch5 时代旧构建）；
- 该缓存与 `r6p-src`、`pkgC` 等**所有**后续重打包（同 id+version）哈希均不一致（sha512 逐一比对），NuGet 对本地源命中缓存**不校验内容**直接复用；
- 决定性实验：`NUGET_PACKAGES=<空目录>` 下单装 Generator → `dotnet list package --include-transitive` 显示 `Cosmos.EffectAlgebra 1.0.0` 正常联装，`[EffectOverride]` 方法编译通过。
- 结论：R2B-01 的依赖流转修复有效；「NuGet 对 analyzer-only 包不流动 nuspec 依赖」的机制归因错误。
- 保留价值：暴露了真实运维陷阱——**同 id+version 本地重打包会静默拿到旧缓存**。已文档化（README ⓪）。

## 3. 门禁与测试演进

- 测试：649 → **667**（+17 ProdAuditR6ContractTests、+1 ProdAuditR6ToolingTests；478+116+73，0 失败）。
- 构建：全量 `-warnaserror --no-incremental` 0 错误；AnalyzerConsumer 1 条 EAA0901 警告为样例设计（门禁回显与 README 已如实更新）。
- CI：`2b9f0a8` / `68dcf7e` / `92cfd66` 三连绿（33914689151 / 33917282793 / 33921729198）。
- 契约文档：EFFECT_SCRIPT.md §4 新增 R6-RB 契约要点四条（重复键拒/控制字符拒/十进制字面量/消息定位）。

## 4. 生产就绪判定（本轮口径）

- **正确性**：失败方言单一路径恢复（FormatException 全覆盖）、重复键/控制字符假绿向量封死、门禁自身三红清零——审计工具"自身不能假绿"的元要求首次全链路成立。
- **易用性**：README 五分钟上手全部硬承诺经第三方逐字实测成立（接线/触发/error 化/JSON 契约/CLI 退出码表）；余 2 LOW 文档摩擦已收口。
- **诚实边界**：NuGet 未发布（README 声明）、O(S·D) 规模边界（#16）、L3 触发前提（③）——全部文档化且有测试背书。
- **保留**：包未发布到 nuget.org（消费走源码引用，README 首段诚实声明）；大规模异构脚本性能边界已声明。

**判定：READY_WITH_RESERVATIONS → 就绪保留项仅为「包未发布」这一外部动作，代码/文档/门禁层面本轮无未收口 HIGH/MEDIUM。**
