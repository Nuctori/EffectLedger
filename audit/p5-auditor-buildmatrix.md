# 构建矩阵与 NuGet 攻击报告

- 审计员：P5 构建矩阵与 NuGet 攻击审计员
- 日期：2026-09-08
- 基线：HEAD `79fa516fd05637e496991a87e666f976654fff62`（审计中途被并发会话推进至 `55aaa24`，仅改 `tests/Cosmos.EffectAlgebra.Tests/QedMaintFuzzTests.cs`，未触碰任何 src 源——对本报告全部字节级结论无影响；nuspec commit 指纹当场暴露了这次移动，见发现 #8）
- 环境：Windows 10，SDK 8.0.418 / 10.0.103，`NUGET_PACKAGES=P:\Caches\NuGet`（360 包，含 Microsoft.NETCore.App.Ref 9.0.13、Microsoft.CodeAnalysis.CSharp 4.12.0、xunit 2.9.3 等全量闭合）
- 约束遵守：仓库零修改（`git status --porcelain` 前后均为空）；临时工程/工具/包全部建于 `P:\Temp\p5attack`，审计结束已删除；dotnet tool 经 `--tool-path` 安装（未污染 `~/.dotnet/tools`）。

## 总评（构建/打包/供应链成熟度结论）

**结论：生产级。** 五包 pack 全绿零 NU 警告；nuspec 依赖组/TFM 标签/README/来源指纹全部正确；Deterministic=true 经 405 个二进制双重构建逐字节验证；空 source 离线还原 14 工程全闭合；ShareSource 命名空间改写在元数据层零泄漏；Tool 端到端可执行。R2B-01（Generator 依赖流转）静态与动态双层证据均达标。

扣分项集中在**增量 pack 的陈旧字节风险**（本审计以同 commit 实证打出过与确定性重建不一致的 DLL）、**同版本本地缓存遮蔽的活体存在**（缓存内残留 R2B-01 之前的旧 1.0.0 包族，且无自动化清除门）、以及 Analyzer csproj 一处**反斜杠路径与仓库自身 R3-CG-03 教训相悖**的跨平台隐患。供应链成熟度的主要缺口是无 lock 文件/中央包管理，离线闭合性完全依赖本地缓存状态而非机械保证。

## 发现清单

| # | 严重度 | 攻击向量 | 实证命令与输出摘录 | 判定 |
|---|--------|----------|--------------------|------|
| 1 | MED | Analyzer csproj 的 pack 路径用反斜杠（`<None Include="bin\$(Configuration)\$(TargetFramework)\$(AssemblyName).dll" .../>`），而 `Directory.Build.props` R3-CG-03 注释自证「反斜杠在 Linux MSBuild 按字面文件名解析恒 false ⇒ NU5039」；Generator 已改正斜杠（R3-CG-04），Analyzer 未改。Linux 上 pack 可能**静默产出缺 analyzer DLL 的"成功"包** | `src/Cosmos.EffectAlgebra.Analyzer/Cosmos.EffectAlgebra.Analyzer.csproj`：`bin\$(Configuration)\$(TargetFramework)\...`（反斜杠）vs Generator csproj：`bin/$(Configuration)/net10.0/...`（正斜杠）。Windows 本机 pack 正常（包内 `analyzers/dotnet/cs/Cosmos.EffectAlgebra.Analyzer.dll` 在），Linux 行为未能实证（本机仅 win32），但失效机制与仓库自己的 R3-CG-03 记载同源 | **实现缺陷**（跨平台潜在、静默失败面；同仓库双标写法即是证据） |
| 2 | MED | 同 commit 裸 `dotnet pack`（增量）可打出与确定性重建**字节不一致**的陈旧 DLL——JetBrains/CLI 一键 pack 若未先走 `--no-incremental`，发布的 nupkg 内容可属任意历史状态 | 先后两次 `dotnet pack -c Release` 同一 L1：`pkgs1` DLL sha256=`3e2d8b24…`，`pkgs2` DLL=`92ec3e8b…`（两者 nuspec commit 同为 79fa516，且 `55aaa24` 只改测试文件——差异只能来自会话前遗留的 bin/obj 增量状态）。随后 `dotnet build --no-incremental` 再 pack：DLL=`92ec3e8b…` 与 bin 逐字节一致，证明 pack 管道本身干净、污染源是增量信任 | **实现缺陷**（pack 目标缺机械防呆；PUBLISH-CHECKLIST 第 20 行有 `-warnaserror --no-incremental` 前置要求，但属程序约束非硬门） |
| 3 | LOW | 无 lock 文件/中央包管理：全仓无 `Directory.Packages.props`、无 `packages.lock.json`。直接依赖全部精确钉版（4.12.0/17.14.1/2.9.3/6.0.4/3.1.4），但**传递依赖**版本随时间/环境漂移，离线闭合性完全由本地缓存状态决定（本次实证闭合，不代表任意时点闭合） | `find` 全仓无 lock 文件；`dotnet restore --configfile <仅空目录 source>` 14 工程全过——靠的是 `P:\Caches\NuGet` 当下恰好齐备 | **实现缺陷**（轻度；建议 `RestorePackagesWithLockFile` + `--locked-mode` 门） |
| 4 | MED | 同版本缓存遮蔽活体：全局缓存现存 `cosmos.effectalgebra.generator/1.0.0` 为 **R2B-01 之前**（commit `b3d53ed`）的旧 nuspec——无依赖组。裸还原单装 Generator 解析到缓存旧包，还原图**无 L1**，消费代码编译 `CS0246`，与 PUBLISH-CHECKLIST 第 13-15 行描述的症状逐字命中；新版 1.0.0 nupkg 被静默遮蔽 | `grep '"Cosmos.EffectAlgebra' obj/project.assets.json` → 仅 `"Cosmos.EffectAlgebra.Generator/1.0.0"`；`dotnet build` → `error CS0246: 未能找到类型或命名空间名"Cosmos"`。缓存内旧 nuspec `<dependencies>` 节为空。换 `NUGET_PACKAGES=<隔离目录>` 重还原 → 联装 `"Cosmos.EffectAlgebra/1.0.0"`、编译 0 错（R2B-01 动态达标） | **防御生效**（清单明令「发布前删除 `cosmos.effectalgebra*` 缓存目录」且症状学准确），但**无自动化清除/检测门**——本机缓存至今仍是脏的，任何按文档走 `dotnet add package` 的外部消费者都会踩中 |
| 5 | LOW | nupkg 非逐字节可重现：相同输入两次 pack 的 nupkg SHA 不同（zip 条目时间戳所致；DLL 内容本身确定） | `sha256sum` pkgs1=`3456dc0e…` vs pkgs4=`a5f24626…`；`unzip -v` 条目时间 `08:02` vs `08:11`。对发布校验和/镜像比对构成小噪音 | **实现缺陷**（轻度；`SOURCE_DATE_EPOCH`/可重现 zip 可解，属行业通病） |
| 6 | LOW | 仓库根 `obj/` 含前期审计探针残留：`%TEMP%ProbeNum*.csproj.nuget.dgspec.json`（字面 `%TEMP%` 文件名）、指向 `src/Cosmos.EffectAlgebra.csproj` 的陈旧根级 `project.assets.json`、根 `bin/Debug`。以仓库根为 cwd 的任何 MSBuild 探针会误触这套状态 | `ls /d/Godot/Cosmos/obj` → `%TEMP%ProbeNum2ProbeNum2.csproj.nuget.dgspec.json` 等 6 件 + `project.assets.json`（内容为 net10.0 project 引用还原图） | 文档欠明确（gitignore 已盖住不碍 git，但对后续审计者是错误信号源；建议清理） |
| 7 | LOW | Analyzer 单 net9.0 的宿主矩阵陷阱：构建 net9.0 需要 `Microsoft.NETCore.App.Ref 9.x`，本机 `dotnet/packs` 只有 8.0.24/10.0.3，9.0.13 仅存在于 NuGet 缓存——即离线闭合性对**缓存中的 ref pack** 单点依赖；纯 net9 SDK（无 10.x 宿主）的 JetBrains 用户无法构建 Analyzer/Tool（Tool net10.0） | `ls "C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref"` → `8.0.24`、`10.0.3`；`ls $NUGET_PACKAGES/microsoft.netcore.app.ref` → `6.0.36`、`9.0.13`（离线 restore 全靠它闭合成功） | 文档欠明确（README R2B-02 已记宿主前提「.NET 10 SDK」；csproj 注释也写明 net9.0 选择理由，但「需 9.x ref pack 经 NuGet 流转」未言明） |
| 8 | INFO | 并发会话中途推进 HEAD（`79fa516`→`55aaa24`，2026-09-08 08:06 +0800）：同版本再次 pack 的 nuspec `commit` 指纹当场变位，暴露仓库正被并行写入。对审计本身是干扰，对打包溯源是**正面证据**——指纹机制活的 | pkgs1 nuspec `commit="79fa516fd05…"` vs pkgs4 nuspec `commit="55aaa246af…"`；`git log` 证实新提交仅 1 文件 +3/-1（测试 fuzz 语料） | 防御生效（`RepositoryUrl`+commit 溯源元数据可作「同版本不同字节」与并发提交的免费检测器） |
| 9 | INFO | Tool 包 nuspec 无依赖组（SDK `PackAsTool` 默认 `SuppressDependenciesWhenPacking`）——攻击「Tool 不声明 L1 依赖导致消费缺件」不成立：L1 DLL 实物随包（`tools/net10.0/any/Cosmos.EffectAlgebra.dll` + deps.json 双登记），`dotnet tool install --tool-path … --add-source <本地>` 成功，`cosmos audit samples/effect-sample.json` 端到端 `"passed": true`，退出码 0 | `DotnetToolSettings.xml`：`<Command Name="cosmos" EntryPoint="Cosmos.EffectAlgebra.Tool.dll" Runner="dotnet" />`；包类型 `<packageType name="DotnetTool" />` | 防御生效（自包含是比声明依赖更稳的 Tool 语义，杜绝全局工具解析时的版本漂移） |

补充核对（无发现项）：
- **五包清单**：L1/Runtime `lib/net8.0`+`lib/net10.0` 双资产齐；Analyzer/Generator 落 `analyzers/dotnet/cs/`；五包均含 `README.md`（R2B-03 达标）；版本单一真源生效（五包均 1.0.0，`<Version>` 仅在 Directory.Build.props）。
- **Generator R2B-01（静态）**：nuspec `net8.0`/`net10.0` 双依赖组，`<dependency id="Cosmos.EffectAlgebra" version="1.0.0" exclude="Build,Analyzers" />`；隔离缓存下 net8.0 单装 Generator 消费工程编译 0 错。
- **Analyzer ShareSource 泄漏**：PortableExecutable 元数据审计，`Cosmos.EffectAlgebra.Analyzer.dll` 共 23 个类型，PUBLIC 仅 `EffectAlgebraAnalyzer` + 21 个 `Cosmos.EffectAlgebra.Analyzer.Shared.*`（ResourceId/Signature/ApiMapping/GodotApiWhitelist/NetTable/CosmosEffectConfig/Interval/SignedNet 族等六源文件全数收编）——**零裸 `Cosmos.EffectAlgebra` 前缀公共类型**；Generator 侧同构（仅 `EffectAlgebraGenerator` + `Generator.Shared.*`）。CS0433 双类型攻击面关闭。
- **Analyzer 独立还原**：临时工程只引 Analyzer 包（`<clear/>` 仅本地源），还原图唯一库即 Analyzer，`dotnet build` 出 `warning EAA0901`（泄漏样例真触发）、零 CS8032——隔离 ALC 自包含闭环。
- **确定性**：`dotnet build --no-incremental -c Release Cosmos.EffectAlgebra.slnx` 连续两次，`src/tests/samples` 下 405 个二进制 sha256 全量 diff 为空；仅有的 3 条警告为样例工程设计内 EAA0901 演示（WarningsNotAsErrors 豁免）。
- **离线还原**：`--configfile`（`<clear/>` + 空目录 source）下 slnx 11 工程 + GateFixture 3 工程全部还原成功，退出码 0——预热缓存下离线可构建成立。
- **slnx**：`dotnet sln list` 11 项与磁盘 14 csproj 比对——slnx 无孤儿、无坏路径；`tests/GateFixture/{Leaky,Paired,ExtendedWhitelist}` 3 项**有意排除**（README 记载的变异门装置，由 `ProdAuditBatch4ToolingTests` 以真实 `dotnet build` 行使，非遗漏）。

## 值得表扬

1. **nuspec `repository commit` 溯源指纹 + Deterministic=true 的组合拳**：本次审计靠「同版本两包指纹变位」当场抓获陈旧增量产物（发现 #2）与并发提交（发现 #8）——来源元数据不是摆设，是可运行的篡改/漂移检测器。
2. **R2B-01 修复双层达标且症状学精准**：静态（nuspec 双 TFM 依赖组）与动态（隔离缓存单装联装 L1 + net8 消费编译零错）全过；PUBLISH-CHECKLIST 对缓存遮蔽的描述（「Generator-only 还原不联装 L1」）与实击产出逐字一致——文档与实况零偏差。
3. **ShareSource 命名空间改写纪律**：21 个 L1 派生公共类型在 Analyzer/Generator 两个 Roslyn 包内 100% 收进 `.Shared` 前缀（Copy 字节级副本 + `#if` 切换），元数据层零泄漏，CS0433 攻击面彻底关闭；且分析器单包独立行使 EAA0901 成功，隔离 ALC 自包含声明经得起黑盒验证。
