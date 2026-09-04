# Cosmos.EffectAlgebra 独立审计报告 — R6-U（易用性 / 开箱即用 fresh-consumer onboarding）

- 审计日期：2026-09-05
- 审计视角：第一次接触本仓库的 .NET 游戏开发者，逐字执行 README.md「5 分钟上手」
- 独立性：未读取 `audit/` 下任何文件；依据 README.md、EFFECT_SCRIPT.md（未用到）、源码与本人运行的实验
- 实验工作区：`P:\Temp\r6u-verify\`（临时工程，均为 ProjectReference 源码引用，未装任何 NuGet 包）
- README 版本：仓库工作区当前版本（git 工作树）

---

## ⓪ NuGet 诚实声明与缓存陷阱注记（注释判定，未真跑装包）

**README 原文**：① "以下包**尚未发布**到 nuget.org（`dotnet add package` 会 NU1101）。发布前请用 ① 的源码引用接入"；② "本地重打包陷阱（R6-P）：NuGet 全局缓存按 id+version 复用且不校验内容——包版本号固定 1.0.0 时，本地重打包后重装会静默拿到旧缓存……重打包后须先删除缓存中对应包目录。CI/全新机器不受影响。"

**判定**：与实际一致。
- 实验全程未安装包（ProjectReference），restore 零网络取包、零 NU 警告——README "发布前请用 ① 的源码引用接入" 的指引可执行。
- 仓库各 csproj 版本固定 1.0.0（`grep Version` 于 `src/*/ *.csproj`），与注记描述的毒化前提吻合；该注记与上一轮审计实证教训一致，属诚实且有操作建议（删缓存目录 / `dotnet list package --include-transitive` 核对）。
- "门禁须自行接线：诊断默认 warning" —— 与 ①/③ 实验一致：默认 EAA0901 以 **warning** 出现，不设 .editorconfig 时 build 绿（见下）。
- NU1101 未真跑（按任务允许，注释判定）。

## ① 接线 L1+L2+L3（README §① XML 逐字，路径改绝对）

命令/文件：`P:\Temp\r6u-verify\Probe\Probe.csproj`（net10.0 console，README XML 原文含注释逐字，仅 `..\src\...` → `D:\Godot\Cosmos\src\...` 绝对路径）。

```
$ cd /p/Temp/r6u-verify/Probe && dotnet build -c Debug
EXIT=0；输出：Cosmos.EffectAlgebra -> bin/Debug/net10.0/*.dll；Generator -> net10.0；Analyzer -> net9.0
警告：仅 1 条 EAA0901（来自我自己写的 Godot-stub 未配对形状，属预期诊断）；0 条 CS/MSBuild/NU 警告
```

- README 承诺 "build 成功、诊断触发需 OutputItemType="Analyzer"" —— 成立。restore/build 干净，无多余警告。
- 唯一摩擦（LOW）：XML 中相对路径 `..\src\Cosmos...` 仅在"把工程放进仓库内"时可用；仓库外的消费工程必须手改为绝对/正确相对路径，README 未提示这一点（注：README 场景本就是"仓库内源码引用"，扣 LOW 而非 MEDIUM）。
- 宿主前提注记（分析器 net9.0 / 生成器 net10.0、仅 `dotnet build` Roslyn-on-Core 加载）与实际构建输出吻合（Analyzer -> net9.0）。

## ③ 示例形状与 Godot 命名空间门控 A2-09（两个方向都验）

### ③-A 自有类同名方法（非 Godot 命名空间），AddChild 无配对 —— README 注记称"按设计零诊断"

```
文件：Probe/Program.cs → Probe.OwnNode.AddChild(OwnNode) + OwnUnpaired() 无配对调用
$ dotnet build -c Debug → EXIT=0
该文件/方法零 EAA 诊断（build 全程仅 1 条警告，定位在 Godot stub 形状，行号 41）
```
**与 README「触发前提（R6-P）」注记一致：自有同名方法零诊断，确认 A2-09 门控真实存在。**

### ③-B namespace Godot stub 形状（显式接收者 + README ③ 原文裸调用两种）

```
文件：Probe/Program.cs → namespace Godot { StubNode.AddChild/QueueFree } + StubUnpaired()（p.AddChild(...) 无配对）
$ dotnet build -c Debug → EXIT=0，1 warning：
  Program.cs(41,28): warning EAA0901: 方法 'StubUnpaired' 调用了 acquire 类 API（p.AddChild）但无对应 release-class 调用……
文件：Probe/BareCall.cs → class MyScene : Godot.StubNode，README ③ 原文形状
  SpawnEnemy(): var node = AddChild(...); node.QueueFree();   → 无诊断
  SpawnLeak():  var node = AddChild(...);                     → EAA0901（见 ②）
```
**README ③ 示例形状（裸 `AddChild` + `QueueFree` 配对不报）成立；配对缺失时触发；诊断消息文本与 README 承诺一致（含 "[EffectOverride] 不豁免本诊断" 提示）。**

## ② 五行 severity=error .editorconfig → 门禁生效 / 配对恢复绿

```
文件：Probe/.editorconfig = README §② 五行逐字（含行内 "# 中文注释"）
$ dotnet build -c Debug（SpawnLeak 未配对在場）
→ EXIT=1（build 红）
  BareCall.cs(12,21): error EAA0901: 方法 'SpawnLeak' ...
$ 补 node.QueueFree() 配对后重建
→ EXIT=0；已成功生成。0 个警告 0 个错误
```

- README 承诺"复制这几行后门禁生效（error）、补配对恢复绿"——**逐字成立**（含 exit≠0 判定）。
- NOTE：五行的行内 `# 注释` 严格说不在 .editorconfig 官方规范担保内（部分解析器会把它并进 value），但 Roslyn/dotnet 实测正确剥离并应用 severity——逐字复制可用，无需改写。
- 补充验证：裸调用形状（`this` 绑定 Godot 命名空间基类）同样 error 化——门控不受调用形式影响。

## ④ EffectScriptContract.Parse + Audit + ToJson round-trip（README §④ JSON 原文）

```
文件：P:\Temp\r6u-verify\DslProbe\（net10.0 console，仅 ProjectReference L1）
$ dotnet run -c Debug → EXIT=0
[readme]   Passed=True IsPeakChecked=True CapsChecked=2 Violations=0
[readme]   at5 OccupyClaims=1
[roundtrip] Parse(ToJson) ok; Passed=True CapsChecked=2 Vio=0 At5Equal=True At5StrEqual=True
[leak-neg] Passed=False IsPeakChecked=True CapsChecked=1
[leak-neg] PeakExceeded t=0 Gpu{leak0} scope=Scene{Battle} 峰值 256 > 预算 100
[leak-neg] Leak t=100 Gpu{leak0} scope=Scene{Battle} 生命周期未闭合（净效应不含 0）：[256,256]
[bad-loop] FormatException: events[0].loop: 必须 ≥1（0 无意义）或 "⊤"/"inf"
```

- README 断言逐条对照：Parse 成功 ✔；`Passed==true` ✔（README xUnit 片段 `Assert.True(audit.Passed)`）✔；`CapsChecked==2`（两资源峰值门真实运行）✔；`At(5).OccupyClaims.Count()==1` ✔；round-trip `Parse(ToJson(script)).At(t)` 语义等价 ✔。
- "Leak/峰值违规真实报出" 用自构负例证实：PeakExceeded + Leak 同时报出，Detail 可读、带 eventIndex（CLI 侧）。
- "异常消息自带 events[i] 索引定位" 证实：`events[0].loop`。
- 小摩擦（LOW）：README §④ 代码块不含 `using Cosmos.EffectAlgebra;` / `using System.Linq;`（`.Count()` 需 Linq 命名空间），仓库外新建文件直接粘贴会先吃两个 CS 编译错。属常见文档省略，扣 LOW。
- NOTE：代码块中 `System.Diagnostics.Debug.Assert(audit.IsPeakChecked, "峰值门未运行…")` 一行在本例（有 budget）下断言为真、无碍运行，但其注释语境属"无 budget"分支，逐字照抄的读者可能误以为该 Assert 是通用必需行——文案歧义，非功能问题。

## ⑤ `cosmos` CLI 退出码契约（0/2/1）与可读性

样本 `D:\Godot\Cosmos\samples\effect-sample.json` **存在**（README 泛写的 `effect.json` 对应真实样本，路径差已如实记录：README 示例名是占位符，真实样本在 samples/ 下）。

```
$ dotnet run --project D:\Godot\Cosmos\src\Cosmos.EffectAlgebra.Tool -c Release --no-build -- audit D:\Godot\Cosmos\samples\effect-sample.json
→ EXIT=0；stdout: { "passed": true, "events": 2, "violations": [] }        # 符合 0=passed

$ … audit /p/Temp/r6u-verify/leak.json        （自构 create-无-release + budget 100）
→ EXIT=2；violations[] 含 PeakExceeded(atT=0) 与 Leak(atT=100)，各带 resource/scope/eventIndex/detail  # 符合 2=违例

$ … audit /p/Temp/r6u-verify/bad.json         （非法 JSON）
→ EXIT=1；stderr: parse FormatException: JSON 非法: 'b' is an invalid start of a value…  # 符合 1=解析错误

$ … audit …/nope.json                          （文件不存在）
→ EXIT=1；read …: Could not find file …                                                  # 符合 1=IO 错误

$ … audit leak.json --out violations.json      （README §⑤ 原文命令形态）
→ EXIT=2；violations.json 落盘（566 字节）                                                 # --out 行为成立
```

- README ⑤ 退出码表（0/2/1）**全部实测吻合**，包括"违例与错误可区分"的 CI 接线提示。
- 输出为单行/缩进 JSON，`kind/atT/resource/scope/eventIndex/detail` 载荷自描述，可读性良好，可直接喂回 LLM 修复闭环（README 的用途声明成立）。
- NOTE：任务指定的 `--no-build` 形态要求 Tool 先行构建过；README 原文形态（无 `--no-build`）对全新消费者反而更稳（自动构建）。非缺陷。

---

## Findings 汇总

| ID | 严重度 | 摘要 |
|----|--------|------|
| R6U-01 | LOW | README §① 接线 XML 用 `..\src\...` 仓库相对路径，仓库外消费工程必须手改且无提示 |
| R6U-02 | LOW | README §④ 代码块缺 `using Cosmos.EffectAlgebra;`/`using System.Linq;`，逐字粘贴先吃编译错 |
| R6U-03 | NOTE | README §② 五行含行内 `#` 注释（非 .editorconfig 规范担保写法），实测 Roslyn 正确应用 severity，逐字可用 |
| R6U-04 | NOTE | README §④ `Debug.Assert(audit.IsPeakChecked, …)` 行语境属"无 budget"分支，照抄者可能误读（本例有 budget 断言为真，无碍） |
| R6U-05 | NOTE | README §⑤ `effect.json` 为占位名；真实样本 `samples/effect-sample.json` 存在且 passed=true |
| R6U-06 | NOTE | ⓪ "NuGet 未发布 / NU1101 / 1.0.0 缓存陷阱" 注记与仓库实况（版本固定 1.0.0、纯源码引用可零包接入）一致，诚实声明成立 |

**无 HIGH / MEDIUM**：README「5 分钟上手」全部承诺（接线即触发、门控双向行为、error 化门禁、DSL Parse/Audit/round-trip 断言、CLI 退出码表）逐项实测成立。

## 证据文件与 exit code

- `P:\Temp\r6u-verify\Probe\Probe.csproj` + `Program.cs` + `BareCall.cs` + `.editorconfig`
  - `dotnet build -c Debug`（①/③，未配对）：**exit 0**，1 warning EAA0901 → `P:\Temp\r6u-verify\probe-build1.log`
  - `dotnet build -c Debug`（② editorconfig error 化，SpawnLeak 在场）：**exit 1** → `P:\Temp\r6u-verify\probe-build2-editorconfig.log`
  - `dotnet build -c Debug`（② 补 QueueFree 配对）：**exit 0**，0 警告 0 错误 → `P:\Temp\r6u-verify\probe-build3-paired.log`
  - `dotnet build -c Debug`（③ 裸调用形状 SpawnLeak）：**exit 1**（error EAA0901）→ `P:\Temp\r6u-verify\probe-build4-barecall.log`
- `P:\Temp\r6u-verify\DslProbe\`（④）：`dotnet run -c Debug` **exit 0** → `P:\Temp\r6u-verify\dsl-run.log`
- ⑤ Tool Release 构建：**exit 0** → `P:\Temp\r6u-verify\tool-build.log`
  - audit samples/effect-sample.json：**exit 0** → `tool-run1.log`
  - audit leak.json：**exit 2** → `tool-run2.log`；`--out` 落盘 `violations.json`（566B）→ `tool-run5.log`
  - audit bad.json / nope.json：**exit 1** → `tool-run3.log` / `tool-run4.log`
