# R6-P 审计报告：打包 / NuGet 供应链 / 多包消费真实可用性

- 视角：独立审计 subagent（打包 / NuGet 供应链 / 多包消费）
- 日期：2026-09-05
- 仓库：D:\Godot\Cosmos（HEAD 68dcf7eb136b41faa9edd822755b1880972320d2，实验后 `git status --porcelain` 为空，未改动任何跟踪文件）
- 方法：只依据 README.md、csproj、pack 产物与亲手运行的实验；未读取 audit/ 下任何历史审计文件。
- 实验目录：pack 产物 `$TEMP/r6p-pkgs`（复用此前中断运行的 5 包，已重新 unzip 验证）与 `$TEMP/r6p-pkgs-fresh`（强制新产出）；本地源 `$TEMP/r6p-src`；消费工程 `$TEMP/r6p-consumer2`；探针 `$TEMP/r6p-probes`；分析器隔离宿主 `$TEMP/r6p-host`。
- 环境：Windows 10.0.19045 x64，Git Bash（/tmp → P:\Temp），.NET SDK 10.0.103。

---

## 1. pack 门（task 1）

命令：`dotnet pack Cosmos.EffectAlgebra.slnx -c Release -o $TEMP/r6p-pkgs` → **exit 0**。
增量运行对已存在的 r6p-pkgs 未重写 nupkg（mtime 保持 04:11、无「已成功创建包」行）——为严谨另跑全新目录：

`dotnet pack ... -o /tmp/r6p-pkgs-fresh` → **exit 0**，5 个包全部新建：

| nupkg | 体积 | 关键内容 |
|---|---|---|
| Cosmos.EffectAlgebra.1.0.0 | 118 KB | lib/net8.0 + net9.0 + net10.0 DLL，README.md |
| Cosmos.EffectAlgebra.Runtime.1.0.0 | 74 KB | lib/net8.0 + net9.0 + net10.0 DLL |
| Cosmos.EffectAlgebra.Analyzer.1.0.0 | 42 KB | analyzers/dotnet/cs/Cosmos.EffectAlgebra.Analyzer.dll（net9.0 构建） |
| Cosmos.EffectAlgebra.Generator.1.0.0 | 39 KB | analyzers/dotnet/cs/Cosmos.EffectAlgebra.Generator.dll（仅 net10.0 镜像，README 宿主前提一致） |
| Cosmos.EffectAlgebra.Tool.1.0.0 | 84 KB | packageType DotnetTool，tools/net10.0/any（内嵌 Cosmos.EffectAlgebra.dll） |

- 两次 pack 输出均**无任何 NU*/NUSPEND 警告行**，`已成功创建包` ×5。Generator csproj 以 `<NoWarn>NU5128</NoWarn>` 显式压制 analyzer-only 包的已知误报（有注释说明，可接受）。
- 所有包带 license expression MIT、readme、repository url+branch+commit（Directory.Build.props 单点注入）。

## 2. nuspec 依赖闭包 / TFM 一致性（task 2）

逐包 `unzip -p *.nuspec`（/tmp/r6p-pkgs-fresh）：

- **L1**：3 个空依赖组 net8.0/net9.0/net10.0 —— 与 csproj `TargetFrameworks` 完全一致（零外部依赖，符合「零 Godot」声明）。
- **Runtime**：lib 3 TFM + 3 个依赖组各含 `Cosmos.EffectAlgebra 1.0.0 (exclude Build,Analyzers)` —— 一致。
- **Generator**：**无 lib**（IncludeBuildOutput=false），analyzers/dotnet/cs 仅 net10.0 DLL；nuspec 声明 3 个 TFM 依赖组 → `Cosmos.EffectAlgebra 1.0.0`。与 csproj 多目标（R3-CG-04）形式一致。**但见 F1：这些依赖组在 restore 期不流动。**
- **Analyzer**：完全无 `<dependencies>`（csproj `SuppressDependenciesWhenPacking=true`）——设计如此：L1 白名单以「命名空间改写副本」编进分析器 PE，自包含（隔离宿主实验证实实例化与 SupportedDiagnostics 均正常）。
- **Tool**：无依赖组，tools/net10.0/any 自带 L1 DLL + deps.json —— 自包含一致。
- **TFM 组 vs csproj 多目标**：五包全部一致（Analyzer 单 net9.0、Tool 单 net10.0 为有意设计，均有注释背书）。
- **analyzer/generator 包内均无 buildTransitive/*.props、无 *.targets** —— 按 NuGet 分析器约定（`analyzers/dotnet/cs/`）这不是假包：消费实验证实 Csc 任务确实收到 `P:\Caches\NuGet\cosmos.effectalgebra.analyzer\1.0.0\analyzers\dotnet\cs\Cosmos.EffectAlgebra.Analyzer.dll`（diag 构建日志 Analyzers= 参数），EAA0901 真实触发（见 §3）。NuGet 对 analyzer 约定目录是自动接线，不需要 props/targets（props/targets 只在需要附加构建逻辑时才必要）。**结论：两包都是真包，非假包。**

## 3. 本地源消费实验（task 3）

`$TEMP/r6p-src` 放入 5 个 nupkg；消费工程 nuget.config 用 `<clear />` + 仅本地源 —— 还原全程不触 nuget.org，反证包集合无隐藏外部依赖（全绿）。

安装链（`dotnet add package --source`，逐包 `dotnet list package --include-transitive`）：

- 装 **Analyzer**（plain PackageReference）→ 仅 Analyzer 本身，无联装（nuspec 无依赖，符合设计）。
- 装 **Generator** → **无任何传递包**（F1）。
- 装 **Runtime** → 自动联装 `Cosmos.EffectAlgebra 1.0.0`（传递依赖真实流动，对照组成立）。
- 四包齐装 → 全部解析成功。

### 3a. EAA0901 从包路径触发 —— 成立（带重要边界，见 F2）

消费工程 Program.cs 用 `namespace Godot` 桩类型（Node.AddChild/QueueFree）模拟 Godot 工程语义：

- `Leaker.Spawn()`（AddChild 无配对 release）→ `warning EAA0901: 方法 'Spawn' 调用了 acquire 类 API（AddChild）但无对应 release-class 调用…`（来自包路径分析器，PackageReference 无任何 OutputItemType 手工接线）。
- `Balanced.Lifecycle()`（AddChild+QueueFree 配对）→ 不报（无误报）。
- 门禁升级测试：`.editorconfig` 写 `dotnet_diagnostic.EAA0901.severity = error` → **构建失败，exit 1**（`error EAA0901`）。README §② 的五行接线声明在包消费形态下真实成立。

排查记录（对归因重要）：最初用「用户自有 `AddChild` 方法 + 裸调用」的 README §③ 同形样例，两路（包 / ProjectReference OutputItemType=Analyzer）均 0 诊断；隔离宿主（$TEMP/r6p-host：`Assembly.LoadFrom` 包内 DLL + `Compilation.WithAnalyzers`）证实包内分析器实例化正常、五个 EAA 描述符在册、白名单 38 条与 L1 一致、Canonical("AddChild")="addchild"，但对全局命名空间的 `AddChild` 仍 0 诊断。根因在分析器 P0-2 门控 `IsGodotTypedInvocation`（EffectAlgebraAnalyzer.cs）：调用点符号可解析且命名空间非 `Godot.*` 时**有意**跳过（防同名误报）。换成 Godot 命名空间桩后立即触发。即：分析器逻辑正常，触发面被限定在「绑定到 Godot 命名空间类型」的调用。

### 3b. EffectScriptContract.Parse（L1 包）—— 成立

消费工程（net10.0）直接 `EffectScriptContract.Parse(json)`（templates/effect-script.json 的最小合法剧本）→ `PARSE_OK events=2`，程序运行到底。

### 3c. Generator 包实编译验证

带 `[Cosmos.EffectAlgebra.EffectOverride("...")]` 方法 + `EmitCompilerGeneratedFiles=true` →
`obj/Release/net10.0/generated/Cosmos.EffectAlgebra.Generator/.../GenProbe_Bang.g.cs` 生成，内容为
`public static global::Cosmos.EffectAlgebra.Signature ComputeBang(...)`（真引用 L1 类型，非桩），build exit 0 —— L1 在场时生成链路完全可用；同时坐实「生成代码硬引用 L1 类型」，是 F1 的后果放大器。

## 4. README 声明核对（task 4）

README ⓪（R3-DT-01）原文：「以下包**尚未发布到 nuget.org**（`dotnet add package` 会 NU1101）。发布前请用 ① 的源码引用接入；**包内容与依赖闭包已由 `dotnet pack` 门验证**。」

- 「尚未发布」——诚实、属实。
- 「包内容已验证」——与事实一致（§1/§2 全部核实）。
- 「依赖闭包已验证」——**对 Generator 不成立**：pack 只验证 nuspec 形式合法性，不验证依赖在 restore 期真实流动；实测 Generator 的三个 TFM 依赖组全部不流动（F1）。README 同段的「（依赖 L1，NuGet 自动联装）」为失实声明。
- README ①（源码引用）与 ②（severity=error 自接线）均经实验证实有效；「NuGet 包不含 severity 策略」的说明与包内无 props/targets 的事实一致（诚实）。
- NUSPEND/NU5 分级警告：两次 pack 均 0 条；唯一被压制的是 NU5128（Generator，显式 NoWarn，有注释）。

## 5. templates/（task 5）

templates/README.md：包名/接线与 pack 产物一致（L1/Generator/Analyzer/Runtime 四包名正确）；「门禁须自行接线」「cosmos.effect.json 目前未自动生效」的诚实边界与源码事实相符。小瑕疵：`dotnet tool install -g --add-source ./src/Cosmos.EffectAlgebra.Tool/bin/Release` 只在**不带 -o** 的 pack 下成立；按主 README ⓪ 惯例 `dotnet pack -o <dir>` 时 nupkg 在 <dir> 而非项目 bin/Release。

---

## Findings

### F1（HIGH）Generator 的 NuGet 依赖闭包不流动：「依赖 L1，NuGet 自动联装」失实
- 证据：`dotnet list package --include-transitive`（probeGen，仅 `PackageReference Cosmos.EffectAlgebra.Generator 1.0.0`）：net10.0 与 net8.0 消费者均**零传递包**；`obj/project.assets.json` 的 `libraries` 仅含 Generator（grep `Cosmos.EffectAlgebra/` 计 0），`targets` 节点无 dependencies 子项。对照：同形 nuspec 的 Runtime 包自动联装 L1（有 lib 资产的包依赖正常流动）。而 nuspec 文件本身确实声明了三组依赖（`unzip -p` 可见）。生成代码 `GenProbe_Bang.g.cs` 逐字引用 `global::Cosmos.EffectAlgebra.Signature`。
- 影响：只装 Generator 的消费工程还原**绿灯、零警告**；一旦使用 `[EffectOverride]/[AcceptDeviation]`（生成器唯一触发面），生成代码对 L1 类型的硬引用立即 CS0246。README ⓪ 的「自动联装」与 csproj R3-CG-04/R2B-01 注释前提（「net8/9 消费者还原期自动联装 L1」「单装 Generator 不再 CS0246」）在 NuGet 路线上均未达成——此前审计批次的修复只改了 nuspec 声明，未验证 restore 流动性。按 README ⓪ 四包全装的用户不受影响（L1 被显式安装），故为闭包断裂+声明失实而非当场不可用。
- 修复方向：(a) Generator 包补 build/ 多目标 lib 形态或改用 analyzer-asset 之外的资产承载依赖组；(b) 或在包内 buildTransitive targets 显式注入 L1；(c) 或至少把 README/csproj 注释改为如实说明「必须显式安装 L1」，并把「restore 后 L1 在图」加进 pack/CI 门（当前 pack 门验证不了这件事）。

### F2（MEDIUM）EAA0901 只对绑定到 `Godot.*` 命名空间的调用触发；README 快速上手样例在非 Godot 消费工程静默零诊断
- 证据：全局命名空间自有 `AddChild` + 裸调用的 README §③ 同形代码，包路径与 ProjectReference 路径均 0 诊断；`IsGodotTypedInvocation`（EffectAlgebraAnalyzer.cs P0-2）对可解析且非 Godot 命名空间的符号 return null。换成 `namespace Godot` 桩后立即触发（见 §3a）。
- 影响：门禁不会失效于真实 Godot 工程（设计初衷），但非 Godot 工程或用自有同名包装方法的团队拿到的是**静默零保护**而非报错；README §③「无需改游戏代码」的示例在控制台/纯 C# 消费中无法复现，易造成「接了包=有门」的错觉。建议 README 注明触发前提（调用点需绑定 `Godot`/`Godot.*` 命名空间方法）。

### F3（MEDIUM）仓库自带 samples/AnalyzerConsumer「EAA0901 真在编译期出现」门为绿灯偶然
- 证据：在 $TEMP 逐字复刻该样例（同 csproj 形状 + 同 Game.cs，Release）：`已成功生成，0 个警告，0 个错误`——其 `Leaker.AddChild` 绑定全局命名空间，被 F2 的 P0-2 门控跳过；csproj 注释声称「EAA0901 真在编译期出现（见 Game.cs）」，但门只断言 0 错误，无任何断言保证 EAA 必现。
- 影响：该样例作为「分析器接线验证工程」无法抓住分析器静默不加载/不触发的回归（本机包路径与源码路径都曾是 0 诊断也照样绿）。建议样例改用 Godot 命名空间桩并断言 EAA0901 出现（或加 WithAnalyzers 单测）。

### F4（NOTE）Analyzer/Generator 包无 buildTransitive props/targets —— 不是假包；severity 策略不随包分发
- 证据：Csc 任务 Analyzers= 含包路径 DLL；EAA0901 触发并可升级 error。props/targets 非分析器接线所必需；包不含 .editorconfig 策略，README「门禁须自行接线」说明如实。

### F5（NOTE）pack 门本身干净；增量 pack 不重写已存在输出目录
- 证据：两次 pack exit 0、0 NU 警告；对非空输出目录的首次 pack 未重写已有 nupkg（mtime 不变、无「已成功创建包」行）。CI 若复用缓存输出目录需注意；建议门内校验 nupkg mtime/存在性或总是干净目录。

### F6（NOTE）Tool 包自包含；templates 的 tool install 源路径与 `-o` 打包习惯不一致
- 证据：nuspec 无依赖组、tools/net10.0/any 内嵌 L1；templates/README.md 的 `--add-source ./src/.../bin/Release` 仅在无 `-o` pack 时成立。

## Verdict

**READY_WITH_RESERVATIONS** —— 按 README ⓪ 四包齐装的消费路线端到端可用（EAA0901 触发 + error 门禁生效 + Parse 可用 + Generator 产出可编译代码），包形态与元数据质量高；但 Generator 的依赖闭包声明在 restore 期断裂（HIGH，声明的「自动联装」不成立），且 EAA0901 的触发边界（Godot 命名空间绑定）与样例门的绿灯偶然需要在发布前修正与补门。

## 证据文件与命令

- 命令与 exit code（关键项）：
  - `dotnet pack Cosmos.EffectAlgebra.slnx -c Release -o $TEMP/r6p-pkgs` → 0
  - `dotnet pack ... -o /tmp/r6p-pkgs-fresh` → 0（5 包新建）
  - `dotnet add package ... --source P:/Temp/r6p-src` ×4 → 0
  - `dotnet build -c Release`（consumer，EAA0901 warning）→ 0；`dotnet run`（PARSE_OK events=2）→ 0
  - `.editorconfig` EAA0901=error 后 `dotnet build` → **1**（生成失败，门禁生效）
  - `dotnet build -c Release`（probeSample 复刻）→ 0，0 警告（绿灯偶然证据）
  - `dotnet restore -v diag` probeGen → 0；`dotnet list package --include-transitive`（probeGen net10/net8、probeRt、probeAna）→ 0
  - `dotnet run`（r6p-host 隔离宿主：包内分析器 WithAnalyzers）→ 0（SUPPORTED=EAA0901..EAA0802，TOTAL=0 → P0-2 门控定位）
  - `dotnet build Cosmos.EffectAlgebra.slnx -c Release`（收尾健康检查）→ 0
  - `git status --porcelain` → 空（0 条，未改动仓库）
- 证据目录：`P:\Temp\r6p-pkgs`、`P:\Temp\r6p-pkgs-fresh`、`P:\Temp\r6p-src`、`P:\Temp\r6p-consumer2`（Program.cs/.editorconfig/obj/project.assets.json/obj/generated/…/GenProbe_Bang.g.cs）、`P:\Temp\r6p-probes`（probeGen/probeRt/probeAna/probeProjRef/probeSample）、`P:\Temp\r6p-host`
- 关键日志：`P:\Temp\r6p-build-diag2.log`、`P:\Temp\r6p-sample-diag.log`、`P:\Temp\r6p-diag.log`、`P:\Temp\r6p-gen-build.log`、`P:\Temp\ana_pkg.dll`
