# 打包与供应链攻击报告（p5-auditor-supplychain）

审计日期：2026-09-08 ｜ 审计员：打包与供应链攻击（P5 第 7 轮）
对象：Cosmos.EffectAlgebra 五包族（L1 / Analyzer / Generator / Runtime / Tool），净室 pack 于 `P:\Temp\p5sc`（仓库零写入；消费矩阵用隔离 `NUGET_PACKAGES=P:\Temp\p5sc\nugethome`，不触碰用户全局缓存）。

---

## 总评（供应链成熟度结论）

**结论：包本体成熟度高（净室路径全部防御生效），但供应链流程存在一个 HIGH 级真实缺陷——「修复历史缺陷时不升版本号」，导致本机全局缓存中 R2B-01 修复前与 P1-B5/honesty-13 修复前的两代旧 1.0.0 包仍在静默遮蔽修复后的新 1.0.0。历史缺陷 R2B-01（Generator 单装丢 L1）与 honesty-13（同 id+version 跨 TFM API 分歧）在存量机器上当场复活复现。**

分层判定：
- **净室层（新机器/干净缓存/nuget.org 首次消费者）**：五包 manifest、依赖闭包、TFM 矩阵、Analyzer ALC、确定性构建、Tool 端到端 **全部通过，零 pack 警告**。包本体达到可发布质量。
- **存量层（曾本地消费过旧 1.0.0 的机器）**：`P:\Caches\NuGet` 实测驻留 2026-09-04 的旧五包（无依赖组、带 net9.0 缩减切片、无 README），同 id+version 精确匹配优先于本地源新包 → **还原图静默使用旧包**。README ⓪ 的「版本一旦变更即新缓存键」防线设计正确但从未触发：**版本号在两次修复中都没有变更过**。PUBLISH-CHECKLIST 第 32 行的「干净缓存实验」只覆盖净机路径，对已被旧包毒化的机器（含本机与 CI runner 的常驻缓存）无覆盖。
- 补救：下一次发布**必须**升到 1.0.1（本地 1.0.0 已被烧毁）；存量机器清理 `P:\Caches\NuGet\cosmos.effectalgebra*` 五个目录。

---

## 攻击发现清单

| # | 严重度 | 攻击向量 | 实证命令与输出摘录 | 判定 |
|---|--------|----------|--------------------|------|
| 1 | **HIGH** | 同 id+version 全局缓存遮蔽：修复 R2B-01 时不升版本 → 旧 1.0.0 Generator（`SuppressDependenciesWhenPacking` 时代，nuspec **无** `<dependencies>`）驻留全局缓存，遮蔽修复后同版本新包，**Generator-only 还原不联装 L1，历史缺陷当场复活** | `cat /p/Caches/NuGet/cosmos.effectalgebra.generator/1.0.0/cosmos.effectalgebra.generator.nuspec`：**无 dependencies 节**，mtime `2026-09-04 13:28`（净室新包 mtime `2026-09-08 06:36`，nuspec 有 `net8.0`+`net10.0` 两组 L1 依赖）。用毒化缓存还原 GenOnly（只引 Generator 包）：`project.assets.json` → `targets net10.0: {"Cosmos.EffectAlgebra.Generator/1.0.0": {"type":"package"}}`——**deps 里没有 L1**，包文件仅 analyzer dll+nuspec | **实现缺陷**（发布流程：修缺陷不升版本；Directory.Build.props 注释声称的防线「版本一旦变更即新缓存键」因版本未变而永不触发；PUBLISH-CHECKLIST「干净缓存实验」不覆盖存量毒化机器） |
| 2 | **MED** | 同根因爆炸半径 #2：缓存中旧 L1 1.0.0 携带 **net9.0 缩减切片**（P1-B5/honesty-13 修复前三 TFM 时代）——被毒化机器的 net9 消费者拿到 API 面分歧的旧资产，「同一包 ID+版本在不同 TFM 暴露不同 API」复活 | `grep -E "targetFramework" /p/Caches/NuGet/cosmos.effectalgebra/1.0.0/cosmos.effectalgebra.nuspec` → `net8.0 / net9.0 / net10.0` 三组；`ls .../1.0.0/lib/` → `net8.0 net9.0 net10.0`。净室新包仅 `net8.0`+`net10.0` | **实现缺陷**（同 #1 根因：旧包不升版本即永久存活于存量缓存；csproj 注释宣称的「分歧类随之消失」仅对净缓存成立） |
| 3 | MED | 净缓存对照实验（R2B-01 回归，隔离 `NUGET_PACKAGES`）：Generator-only 还原是否联装 L1 | 换净缓存后 `dotnet restore GenOnly`：`target Cosmos.EffectAlgebra.Generator/1.0.0 deps: {'Cosmos.EffectAlgebra': '1.0.0'}`；`dotnet build` 成功——探测代码只经 Generator 传递引用 `ResourceId.Memory` 编译通过；生成器 emitted 的 `EffectAlgebraGenerated.ComputeSpawn` 在同次编译可解析且运行期返回 `Cosmos.EffectAlgebra.Signature` | **防御生效**（条件：净缓存——见 #1 的保留意见） |
| 4 | MED | 多 TFM 消费矩阵：net8.0 / net10.0 / net9.0 三消费者引用 L1+Runtime 包 | 三工程 `dotnet build`+`dotnet run` 全绿。net9 臂 assets 实证：`Cosmos.EffectAlgebra/1.0.0 → compile: lib/net8.0/Cosmos.EffectAlgebra.dll`（Runtime 同）——就近原则选 net8.0 资产；net10 臂运行期 `System.Runtime 10.0.0.0`，net8 臂 `8.0.0.0` | **防御生效** |
| 5 | MED | Analyzer 真实接线（`OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"`，包来源非 ProjectReference）CS8032 回归 | `dotnet build AnalyzerReal` → `已成功生成`；`Game.cs(18,21): warning EAA0901: 方法 'Spawn' 调用了 acquire 类 API（AddChild）但无对应 release-class 调用`——分析器在编译器隔离 ALC 中加载并真触发；配对释放的 `GodotBalanced` 零误报；全程无 CS8032 | **防御生效** |
| 6 | MED | 隔离 ALC 自包含的 PE 层结构验证（不信任行为测试，直接查 AssemblyRef） | `re.search(rb'Cosmos\.EffectAlgebra\x00', dll)`（#Strings 堆独立条目）：`Generator.dll → False`，`Analyzer.dll → False`（零 L1 程序集引用，ALC 无法解析失败即不可能发生）；正向对照 `Runtime.dll → True`（合法引用）、`L1.dll → True`（自身名） | **防御生效**（A2-06 源共享自包含在字节层面成立，非仅注释声称） |
| 7 | LOW | 包内容安全：不该发布的文件（.pdb/源码/临时文件） | 五包完整解包清单：L1/Analyzer/Generator/Runtime 均为 `nuspec + README.md + lib|analyzers 下仅 DLL`——**零 .pdb、零源码、零 build/.targets 注入面**；`grep -rl p5sc --include=*.dll` → 无匹配（临时绝对路径零泄漏）。唯一瑕疵：Tool 包 `tools/net10.0/any/` 含 `Cosmos.EffectAlgebra.pdb` 与 `Cosmos.EffectAlgebra.Tool.pdb`（dotnet tool 惯例可接受，但与 lib 四包「零 pdb」口径不一致，且全族无 snupkg） | **文档欠明确**（PUBLISH-CHECKLIST 未写明 tool pdb 是否有意保留） |
| 8 | LOW | 惰性供应链配置：`EmbedUntrackedSources`/`PublishRepositoryUrl` 已设但无符号包故事 | 五包均无 snupkg（pack 输出仅 5 个 nupkg）；SourceLink 映射存于 PDB 而 PDB 不随包发布 → 两属性在当前发布形态下无实际作用面。好消息：`github.com/Nuctori/Cosmos` 确已内嵌 DLL 程序集元数据（provenance 成立） | **文档欠明确**（要么补 snupkg 让属性生效，要么注释声明仅 provenance 用途） |
| 9 | LOW | API 文档缺位：L1 源码大量 `///` 注释但不随包发布 | 五包清单无任何 `.xml`；各 csproj 未设 `GenerateDocumentationFile`。nuget.org 消费者无 IntelliSense 文档（ Description 仅包级一句话） | **实现缺陷**（打包配置缺项，轻微：一行属性即可修复，但注意 `CS1573` 类警告会在 `TreatWarningsAsErrors` 下爆，需配套 `<NoWarn>` 决策） |
| 10 | LOW | 版本一致性 + 确定性构建复现 | 五净室 nuspec `<version>` 全 `1.0.0`（单一真源 `Directory.Build.props` 流出）；仓库既有 Release 五包同名 `1.0.0`；同工作区双 pack：L1 net10 DLL SHA256 `d02098c058e8db03 == d02098c058e8db03`（逐字节一致）；五包 pack 零警告（无 NU5039——README 正斜杠修复生效、无 NU5128 漏出） | **防御生效** |
| 11 | LOW | Tool 包端到端：本地源安装 → 命令可用性 | `dotnet tool install --add-source P:\Temp\p5sc\artifacts --tool-path P:\Temp\p5sc\tool Cosmos.EffectAlgebra.Tool --version 1.0.0` → 成功；`cosmos.exe --help` → `usage: cosmos audit <script.json> [--out violations.json]`；`cosmos audit templates/effect-script.json` → `{"passed": true, "events": 2, "violations": []}` exit=0 | **防御生效** |

> 注：#1/#2 在 nuget.org 公开发布场景下风险收窄（NuGet 上同 id+version 不可覆盖），影响面是**本地 pack→本地源/缓存消费**的工作流——这正是本仓库样例、模板与 PUBLISH-CHECKLIST 引导的工作流，故维持 HIGH 判级。

---

## 值得表扬

1. **确定性构建经真实验证**：`Deterministic=true` + 临时目录 pack → DLL SHA256 逐字节一致、零绝对路径泄漏（连 `P:\Temp\p5sc` 这种随机审计路径都查不到）。任何第三方可用任意路径复现位对位产物，供应链可审计性拉满。
2. **Analyzer/Generator 的 ALC 自包含是结构性的而非行为性的**：PE 层零 L1 程序集引用（发现 #6），使 CS8032 类缺陷在字节层面不可能发生；同时 R2B-01 又让 NuGet 依赖照常流转——「编译器隔离通道」与「NuGet 依赖通道」解耦得干净，注释与实现零偏差。
3. **包对外暴露面最小化**：无 `build/` 注入面（消费方构建零劫持点）、Generator 依赖带 `exclude="Build,Analyzers"`、全族单一版本真源、README/license/repository 全齐、nuget.org 页面直达——五包在「最小权限打包」上是一份范本。

---

## 审计合规声明

- 仓库内除本报告文件外零写入：pack 与全部消费矩阵均在 `P:\Temp\p5sc`（净室源码副本 + 隔离 NUGET_PACKAGES）完成；未删除/修改用户全局缓存中毒化的旧包（保留证据，处置建议见总评）。
- 临时工作区 `P:\Temp\p5sc` 已于报告完成后删除。
