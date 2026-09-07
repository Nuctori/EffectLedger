# Cosmos.EffectAlgebra

[![CI - Effect Cost Algebra Audit Gate](https://github.com/Nuctori/Cosmos/actions/workflows/ci.yml/badge.svg)](https://github.com/Nuctori/Cosmos/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue)
![Formal Verification](https://img.shields.io/badge/Dafny-89%20lemmas%20verified-brightgreen)

> 效应代数（Effect Algebra）——在**编译期**做**资源守恒 / 泄漏 / 峰值超预算 / 互斥冲突**的静态近似审计，并在**运行时**由 `Cosmos.EffectAlgebra.Runtime` 做权威闭合判定。

**为什么选它**：

- **编译期拦截，运行期权威**——L3 Roslyn 分析器在写代码时就报泄漏/冲突（EAA* 诊断），运行时 Σnet 闭合做最终裁决，两层互为印证。
- **89 条机器验证定律**——代数核心（ℕ∪{⊤} 闭合、区间半格、ScopeId 偏序、Compatible 全函数、SignedNet 守恒、扫换线采样充分性）由 Dafny 形式化验证并纳入 CI 门禁，`dafny verify` 0 errors 才放行。
- **三层冻结契约**——公共 API 面 43 类型快照钉死、JSON 契约面 schema version 1.0.0 冻结、异常方言/退出码冻结：升级兼容性可被机器断言。
- **AI 友好**——声明式剧本 JSON（6 资源 × 4 scope）可被 LLM 产出并直接机审，`cosmos audit` CLI 一键闭环。

**文档地图**：

| 想了解 | 去哪 |
| ------ | ---- |
| 5 分钟上手（安装/接线/诊断门禁） | 本页 ⓪–⑤ |
| 剧本 DSL 与 AI 闭环 | `EFFECT_SCRIPT.md`（§4 数据契约） |
| JSON Schema（AI 产出校验用） | `docs/effect-script.schema.json` + `templates/effect-script.json` |
| 形式化验证（89 条 Dafny 定律） | `formal/CosmosEffectAlgebra.dfy`、`formal/CosmosSweepLine.dfy` |
| 设计总纲（PDR v3.0-FINAL） | `PDR_Effect_Cost_Algebra_v3_FINAL.md` |
| 运行时壳层设计 | `docs/spatial-plugin-shell-design.md` |
| 发布流程（人类执行） | `PUBLISH-CHECKLIST.md` |
| QED 路线与维护模式 | `audit/qed/ROADMAP.md` |
| 历史交付快照 | `DELIVERABLE.md` |
| 已知边界（20 条，逐条附证据） | 本页「已知语义锐边」 |

---

### 能做什么

## 能做什么


| 能力          | 输入                                                                      | 产出                                                                                                                          | 形式化保障                                                                                                                      |
| ----------- | ----------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| **守恒/泄漏审计** | 一组 `EffectEvent`（`lifetime` × `scope` × `footprint` × `LoopCount`）      | `AuditResult.Violations`（`Kind`: Leak / NegativeDip / PeakExceeded / CompatibleConflict）                                              | Σnet(t) 含 ⊤ 保守律（MA-002），O(E·K·log E) 扫换线（等价端点采样，`audit/drafts/iter-effect26.md`）                                                          |
| **峰值预算**    | `EffectScript.Budget.Caps`（按归一化资源键）                                     | `Peak ≤ cap`，`IsPeakChecked`/`CapsChecked` 报告门是否实际运行（R1-HIGH-3, R5）                                                         | 值语义 `Budget: ImmutableDictionary` 归一底座（Self→SignalBus 等，R6 S06-002）与 `peakReported` 问题集语义（同资源只报首个）                         |
| **互斥冲突**    | `gate(3) Compatible`                                                    | `CompatibleConflict`（跨 scope×mode×归一化 ResourceId；CONFLICT 集见 §3.2.3）                                                                | `Compatible.IsCompatible` 对角律单一真源（CONFLICT 集 §3.2.3）                                                               |
| **声明式剧本**   | `EffectScriptContract.Parse(string) → EffectScript`；`ToJson` round-trip | 可证伪的 JSON 契约 + L1 类型承载                                                                                                      | fail-fast 白名单（根/事件/claim 层未知键拒，대 小写 Loop 静默退化 ⊤ 等）、`default(LoopCount)` 构造期封堵、`Scope{}`/`resource` 非空校验（R1-R2, R6 S06-001） |
| **编译期近似**   | L2 `Generator` + L3 `Analyzer`（`ApiMapping` 白名单；cosmos.effect.json 扩展经 AdditionalFiles 真接线，QED-C1b）                        | `EAA*` 诊断（EAA0901 泄漏 / EAA0303 量纲混用 / EAA0304 并发冲突 / EAA0801 EffectOverride reason 必填 / EAA0802 AcceptDeviation epsilon 越界 / EAA0701 白名单扩展配置错误） | 对抗测试族 co-driven（`samples/GodotIntegration/AdvE2E_*`：逃逸/豁免/误报对抗形状），白名单 §7 单点                                                |


---

## 5 分钟上手（静态审计 + 剧本 DSL 演示）

### ⓪ NuGet 安装（消费已发布的包；源码引用见 ①）

> **发布状态（R3-DT-01）**：以下包**尚未发布到 nuget.org**（`dotnet add package` 会 NU1101）。发布前请用 ① 的源码引用接入；包内容与依赖闭包已由 `dotnet pack` 门验证。

```pwsh
dotnet add package Cosmos.EffectAlgebra           # L1 代数核心（必需；Generator 产物硬引用其类型）
dotnet add package Cosmos.EffectAlgebra.Runtime   # 运行时权威闭合（仅运行期闭合需要；预算-only 可跳过）
dotnet add package Cosmos.EffectAlgebra.Analyzer  # L3 EAA* 诊断
dotnet add package Cosmos.EffectAlgebra.Generator # L2 每方法 Signature 生成（nuspec 依赖 L1，干净缓存下 restore 自动联装）
```

> **本地重打包陷阱（R6-P）**：NuGet 全局缓存（`~/.nuget/packages` 或 `$NUGET_PACKAGES`）按
> **id+version** 复用且不校验内容——包版本号固定 1.0.0 时，本地重打包后重装会静默拿到旧缓存
> （`dotnet list package --include-transitive` 可核对依赖是否真流动）。重打包后须先删除缓存中
> 对应包目录。CI/全新机器不受影响。

**门禁须自行接线**：诊断默认 warning，把 ② 的五行 severity=error 复制进你的 `.editorconfig`。JSon 剧本一键审计用 `cosmos` CLI（见 ⑤）。


### ① 接 L1 + L3 分析器 + L2 生成器（消费工程 `.csproj`）

```xml
<!-- L1 类型承载：普通引用（生成器 emit 的代码引用 Cosmos.EffectAlgebra.Signature 等）。
     路径按你的消费工程相对仓库位置自行调整（下为仓库内相对形状；仓库外工程改绝对/相对前缀）。 -->
<ProjectReference Include="..\src\Cosmos.EffectAlgebra\Cosmos.EffectAlgebra.csproj" />
<!-- 分析器/生成器必须以 OutputItemType="Analyzer" 接线才会被编译器加载；
     裸 ProjectReference 只是普通库引用，编译期一条 EAA 诊断都不会触发（静默假绿）。 -->
<ProjectReference Include="..\src\Cosmos.EffectAlgebra.Analyzer\Cosmos.EffectAlgebra.Analyzer.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<ProjectReference Include="..\src\Cosmos.EffectAlgebra.Generator\Cosmos.EffectAlgebra.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

> **宿主前提（R2B-02）**：分析器（net9.0）与生成器（net10.0）仅在 **.NET SDK 的 `dotnet build`**（Roslyn on .NET Core）下加载——Visual Studio / .NET Framework MSBuild 的 Roslyn 宿主暂不支持，会**静默不加载**（零诊断零生成）。CI/CLI 构建路径不受影响。

### ② 把 `EAA*` 诊断设为 error（否则只是 warning，门禁失效）

根 `.editorconfig` 已含；消费工程复制这几行：

```
[*.cs]
dotnet_diagnostic.EAA0901.severity = error   # 疑似资源泄漏（acquire 无配对 release）
dotnet_diagnostic.EAA0303.severity = error   # 同资源量纲混用
dotnet_diagnostic.EAA0304.severity = error   # 同资源并发冲突模式
dotnet_diagnostic.EAA0801.severity = error   # [EffectOverride] reason 必填
dotnet_diagnostic.EAA0802.severity = error   # [AcceptDeviation] epsilon 越界
dotnet_diagnostic.EAA0701.severity = error   # cosmos.effect.json 白名单扩展配置错误（扩展未生效，QED-C1b）
```

### ③ 写一个含 acquire/release 的方法（无需任何特性、无需 partial 类、无需改游戏代码）

```csharp
void SpawnEnemy()
{
    var node = AddChild(/* … */);   // acquire 类 API（白名单命中）
    // …
    node.QueueFree();                // release-class 配对 ⇒ 不报 EAA0901
}
```

若配对在跨方法/跨对象（静态近似盲区），以运行期 Σnet 为权威判据，并在 CI 基线中显式豁免 EAA0901（附证据）。

`[EffectOverride("证据")]` **不豁免** EAA0901——它仅豁免 A3/A4 意图提示；DO-9 静态泄漏近似永不抑制（防止全标 override 静默泄漏）。

> 注意：仅当 API 在 §7 白名单中才有保护；未命中白名单的 API **静默无保护、无警告**。自有/未映射 API 可经 `cosmos.effect.json` 扩展白名单（QED-C1b/C1c，L3+L2 均已真接线）：放在消费工程并在 `.csproj` 加 `<AdditionalFiles Include="cosmos.effect.json" />`，扩展 API 即参与 L3 泄漏/冲突分析、L2 也为其 emit 每方法 Signature（扩展-only 方法以字面量 Claims 内嵌——契约面类型全 public 可构造；基础表命中方法维持原 emit）。格式/schema/Canonical 碰撞错误报 **EAA0701**（该文件扩展整体弃用、基础白名单不受影响、绝不静默；L3/L2 两侧同契约 ID 各报一次）。模板见 `templates/cosmos.effect.json`。
>
> **触发前提（R6-P）**：调用的接收者须绑定 `Godot`/`Godot.*` 命名空间的类型（真实 Godot 工程天然满足；符号不可解析的裸语法编译保留回退判定）。自有类的同名方法（非 Godot 命名空间）按设计**零诊断**（A2-09 防同名误报）——包装层/非 Godot 工程不触发不是缺陷，但也没有保护。

### ④ 亮点：声明式剧本数据契约（JSON→L1 审计，零 Godot 依赖，AI/自动化可喂）

AI/脚本可直接产出视觉/音频/网络效果的“视觉剧本”JSON，并用 `EffectScriptContract` 不跑游戏即验证——这正是不能把 L1 审计藏在 Godot 回调里、而要把它做成“纯数据类型”的原因。

```csharp
// §4 数据契约：AI/动画工具产出的可审计数据 — claim 省 scope 继承 event scope（R10 Top1）
// 依赖引入：using Cosmos.EffectAlgebra;（L1 类型）+ using System.Linq;（下方 .Count()）
string json = $$"""
{
  "events": [
    { "lifetime":[0,6],  "scope":{"scene":"Battle"},
      "footprint":[{"kind":"occupy","resource":{"gpu":"tmpMip0"},"mode":"create","size":[256,256]}] },
    { "lifetime":[6,12], "scope":{"scene":"Battle"},
      "footprint":[{"kind":"occupy","resource":{"gpu":"tmpMip0"},"mode":"release","size":[256,256]}] }
  ],
  "budget": { "gpu:tmpMip0": 600, "commandBuffer:gpu": 600 }
}
""";
var script = EffectScriptContract.Parse(json);
// 失败路径：无效形状抛 FormatException（白名单/Loop 0/空 scope/资源坏值等全部 loud），且异常消息自带 events[i] 索引定位
// 成功后
var at5   = script.At(NatStar.Of(5));         // 单点投影：t=5 的总签名
var audit = script.Audit(script.Budget);      // 三道 gate：守恒/峰值/冲突（含居民层豁免 OPEN-4, IsPeakChecked 报告）
// 峰值门是否真实运行（别把“没查”当“全绿”，R10 Top2）：无 budget ⇒ IsPeakChecked==false，Passed 真但未查峰值
System.Diagnostics.Debug.Assert(audit.IsPeakChecked, "峰值门未运行：缺 budget 或空 Caps（CapsChecked==0）");
if (!audit.Passed)
    foreach (var v in audit.Violations)  Console.WriteLine($"{v.Kind} t={v.AtT} {v.Resource} scope={v.Scope}  {v.Detail}");
// round-trip：Parse(ToJson(script)).At(t) 与 script.At(t) 语义等价（资源归一 Brave/归一化键一致）
string back = EffectScriptContract.ToJson(script);
```

契约要点（已实现且可证伪）：claim 缺 scope 继承 event scope（R10 Top1），不一致仍抛（R6 S06-001）；`LoopCount.Of(0)` / `default(LoopCount)` 拒绝；预算按归一化键存储（R6 S06-002，`Self(signal_x)` ≡ `SignalBus(x)`）；异常类型统一为 `FormatException`（R4）。预算准绳：`Passed==true && IsPeakChecked==false` 表示“峰值门未运行”（假绿），模板 `templates/effect-script.json` 含 `$schema` + `budget` 样板可防。
> **doc 即测试**：上面这段 JSON 已被 `tests/Cosmos.EffectAlgebra.Tests/Round7Hickey2Tests.cs` 的 `Readme_Example_ParsesAndAudits` 抽取并守护——文档改一字、CI 立刻红，杜绝"文档能跑、代码不能跑"的漂移。对应可剪贴的 xUnit 断言：
>
> ```csharp
> var at5 = script.At(NatStar.Of(5));
> Assert.Equal(1, at5.OccupyClaims.Count());          // 单点投影：t=5 仅 alive 事件在
> Assert.True(audit.Passed);                            // 该示例自洽：无违例
> Assert.Equal(2, audit.CapsChecked);                   // 两资源峰值门实际运行（非零预算）
> // 注意：Budget.None 时 gate(2) 不运行 ⇒ audit.CapsChecked==0，IsPeakChecked==false（R4/R7-D07-006：别把"没查"当"全绿"）
> ```

---

### ⑤ `cosmos` CLI：剧本 JSON 一键审计（AI 闭环用）

```pwsh
dotnet run --project src/Cosmos.EffectAlgebra.Tool -c Release -- audit effect.json --out violations.json
```

`violations.json` 喂回 LLM 重投直至 `passed`。**退出码契约（R2B-06，注意与常见惯例不同）**：

| exit code | 含义 |
| --------- | ---- |
| `0` | 审计通过（`passed: true`） |
| `2` | **存在违例**（解析成功但 Audit 不通过；`--out` 落盘反例） |
| `1` | 解析/IO/未知命令错误 |

CI 接线示例：`cosmos audit x.json || exit 1` 会把违例当失败（2 非零、0 通过）——按需用 `if [ $? -eq 2 ]` 区分违例与错误。

> **退出码契约冻结（QED-A4，2026-09-06）**：0=通过 / 2=存在违例 / 1=解析或 IO 错误，自此刻语义冻结，变更=破坏性（semver major）。行为钉：`ProdAuditR3ToolingTests`（1/2 与 --out）+ `QedP0A4ContractFreezePins`（0 通过路径）。

---

## 参与者模型（你是谁，怎么用）

**AI（视觉剧本 JSON 生产者）**——瞄 `EFFECT_SCRIPT.md` §4 与 `EffectScriptContract.Parse(string)`：得到它的 R6-E1/E3 覆盖（未知键拒、budget 类型错抛、Loop 0 拒、空 scope 拒）、R2-N1 ⊤ 往返（budget/loop/lifetime ⊤ ↔ "⊤"/"inf"）与 `CapsChecked` 报告（R1-HIGH-3）。

**审计驱动（L1+L2+L3）**——瞄 `EffectScript.Audit`（CLI `cosmos audit` 一键门）与 `samples/GodotIntegration/`（IntegrationTests + AdvE2E_* 对抗族）：报告 Violation 载荷与诊断 EAA*，门禁接线见 ②/⑤。

**运行时集成者（Runtime 权威闭合）**——瞄 `Cosmos.EffectAlgebra.Runtime`：不动游戏代码，依 `docs/spatial-plugin-shell-design.md` 的套件—纤维工艺执行闭合；预算与泄漏的“权威判定”在运行关卡落地（TR-004 闭合，关卡改动只覆盖 `src/Cosmos.EffectAlgebra.Runtime`）。

---

## 已知语义锐边（设计锁死，非 bug；审计/测试/运行闭合中明示）

> **E3 复核（QED，2026-09-07）**：下列 20 条逐条复核完毕——6 条已解决（编号保留划线标记 +
> 修复证据），14 条活跃语义均附测试钉/文档引用。**编号即契约身份，永不重排**（代码与文档存在
> `#10`/`#11`/`#20` 等交叉引用）；每条的证据钉名内联可查。新增边界只在尾部追加新编号。

- `Unknown` 模式三维契约（QED-A3 定稿）：**net/peak 按占用 +size 保守计入**（未映射 API 的泄漏/峰值检测不静默——「Unknown 占用无 release」照常报 Leak）；仅 Compatible 维度 **fail-open**（按 Use 最弱兼容放行——未映射 API 是白名单工具的常态，逐一报冲突=警报洪水，用户会批量 [EffectOverride] 致工具失效）。生命周期冲突（CONFLICT 集）为 A4 权威域；写写数据竞争检测移交 L2 写集分析（F 轨）。钉：`QedP0A3UnknownSemanticsPins` + CompatibleMatrixTests 25 组合矩阵
- `loop:"⊤"`（population-⊤，种群轴）居民层豁免 gate(1) 守恒——判据是「配对 release **结构性不可枚举**」，非「语义常驻」；gate(2) 峰值仍审计（设有限 cap 即报）。`lifetime:[…,⊤]`（time-⊤，时间轴，ω 有限）是完整事件缺 release ⇒ Leak。两轴结论相反是契约而非矛盾——故意常驻请用 `loop:"⊤"` 声明（QED-P0-A1 定稿，对照钉 `QedP0A1SemanticDecisionTests`）。
- `Claim.Size` 省略 ≠ 未知：`?? [1,1]`（精确 1，既非未知 ⊤ 也非 0 预算）（§3.1.5a）。
- `Effects → Audit` 在“全 finite 结束后”走一次性闭包路径（而非端点增量模拟）对 Leak 的认定与“有限闭合”在有限性严整上等价。

---

## 分层


| 层      | 项目                                            | 职责                                                                                                               |
| ------ | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| L1 纯代数 | `src/Cosmos.EffectAlgebra`                    | `Claim/Signature/NetTable/Compatible/SignedNet`（零 Godot 依赖）+ `EffectScript`（含 Audit/At/Budget/AuditResult，非真环理性） |
| L2 生成器 | `src/Cosmos.EffectAlgebra.Generator`          | 按方法名匹配 §7 白名单，Union 出每方法 Signature                                                                               |
| L3 分析器 | `src/Cosmos.EffectAlgebra.Analyzer`           | 方法内 acquire/release 配对近似（EAA* 诊断）                                                                                |
| 剧本 DSL | `Cosmos.EffectAlgebra` `EffectScriptContract` | 声明式契约（`EFFECT_SCRIPT.md`）——`EffectScript` 从手工 JSON 构造，`EAA*` 诊断 + `AdvE2E` 对抗测试族对照             |
| 运行时    | `src/Cosmos.EffectAlgebra.Runtime`            | Fiber 状态机 / 依赖图 / 逆回放 / 退出期 drain（权威 Σnet 闭合），TR-004                                                             |


**预算-only 用户**只需 L1 + L2 + L3，完全不必接触 Runtime（Godot 进程树由 `IHost` 抽象隔离）。

---

## 测试与仪表航迹

```pwsh
dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror
dotnet test  Cosmos.EffectAlgebra.slnx -c Release --no-build
```

- **变异门**：`tests/GateFixture/`（Leaky 工程必红且含 EAA0901 / Paired 工程必绿），由 `ProdAuditBatch4ToolingTests` 以真实 `dotnet build` 行使——分析器接线被静默拔掉（裸 ProjectReference）即红。
- **性能钉**：`EffectScriptEdgeTests` 端点采样==密集扫描随机/对抗等价钉（`Iter26_SweepLine_EqualsBruteForce_*`）+ 硬墙钟钉（5000 粒子 <2000ms / 1000 事件 <5000ms）。
- **CLI 门**：`cosmos-audit.yml` 每周全量四门（build -warnaserror / test / `cosmos audit` 样本 / pack）；`ProdAuditR3ToolingTests` 以真实子进程钉退出码 0/1/2 契约。

---

## 运行期套件—纤维工艺（`docs/spatial-plugin-shell-design.md` 概要）

- **状态机**：`Fiber` 五态（Inactive → Active → Suspending → TearingDown → Dead），全转移幂等守卫（`Fiber.Unload` 契约：Inactive 直达 Dead 防双重释放、TearingDown no-op）。
- **闭合**：`InverseReplay` LIFO 逆回放（部分释放诊断 + 重入门）→ `DrainTeardownBatch` 按 dependent-first 拓扑序排空（硬环子集兜底回放 + CrashReport 可观测）→ `ProviderCrashCascade` fail-open 升级。
- **兜底**：`PluginRuntime.TickWatchdog` 三路径（Active/Suspending 强转入队 + 自愈入队 + 依赖者级联，全部 OnSuspending 钩子接线）；`GodotShell` 帧驱动（Defer 退出期丢弃、`FlushExitDrain` 单场景生命周期）。

## 诚实边界（故意留债 · 测试守住不漂移）

1. ~~`Sequence≡Parallel≡Union` 四名一实~~ **已解决（P1-B3）**：Sequence/Parallel 别名已从 `Combination` 删除，组合唯一入口 `Signature.Union`（幂等并，无时序/并行语义）；原 Parallel 的 PARA_CONFLICT 前置守卫随删——冲突检测权威 = `Audit` gate(3)；时序语义若未来需要归 F 轨
2. `Size ?? Interval.Default` 散布 — `§3.1.5a DO-1` 设计锁，新消费点禁再散布
3. `Audit` 内联 sweep 与 `NetTable`/`Peak` 两份物理代码 — `D08-001/002` 钉住等价，不做重构
4. `At(t)` 投影不带事件来源 / `EventIndex` 取首个贡献者非峰值最大者 — `R9` YAGNI
5. `At(t)` 是**集合投影**（在场语义：同刻逐字段相同的重复事件去重计 1，与事件个数无关）；并发计数/峰值语义以 `Audit` 扫换线为准（计数语义：net/Peak 逐事件累加）——双语义是多重性载体决策（QED-A5：集合刻意幂等 + 重复构造即拒，多重性走事件序列/size×ω）的直接后果而非缺陷：At 回答「t 时刻有哪些 claim 在场」，Audit 回答「各占多少」。自建核对脚本请勿用 `At`+`Derived.Peak` 对账峰值（对照钉 `QedP0A2ProjectionContractTests`）
6. `NegativeDip`/`CompatibleConflict` 逐采样点上报（时间序列语义），仅 `PeakExceeded` 做问题集去重（每资源首个反例）——三门去重口径不同是显式设计，喂 AI 回修前请自行按 `(Kind,Resource)` 去重
7. `EAA0901` 哨兵资源跨 API 假配对：`Load`（Mem create）+ `QueueFree`（Mem release）在同方法内按语法计数互相抵消——跨 API 家族的加载泄漏属静态近似盲区，以运行期 Σnet 为权威判据
8. `EAA0303/0304` 是意图提示而非数学缺陷：哨兵资源上惯用形态（`DrawRect`×2、`MoveAndSlide`+`GetSlideCollisionCount`）会触发，按需 `[EffectOverride("理由")]`（它们不豁免 EAA0901）
9. L3 仅分析**方法体**：构造函数、属性访问器、`using var` 形态不在注册范围；`Position.get/set` 等属性形态白名单条目对 L3 无效——构造期泄漏不在静态覆盖内
10. Runtime 非线程安全（帧驱动单线程模型，零锁）：全部调用须在宿主主线程；实例为单场景生命周期——场景重载请新建 `PluginRuntime`（Dead fiber 与图边不回收、同 FiberId 不可重注册，`R7-L1`）
11. Runtime `Σnet` 闸门按 `⊆*` 过滤：Effect claim 中 scope ⊄* fiber.Scope（如 Global）的资源**不参与**该 fiber 的守恒判定（L1 `EffectScript.Audit` 无此过滤）——两层口径差异，跨 scope 泄漏请以 L1 剧本审计为权威
12. `cosmos.effect.json` 白名单扩展 **L3 分析器已真接线（QED-C1b）**：`<AdditionalFiles Include="cosmos.effect.json" />` 后扩展 API 参与 L3 泄漏/冲突分析；解析/schema/Canonical 碰撞错误报 **EAA0701**（该文件扩展整体弃用 + 基础白名单不受影响，绝不静默；碰撞时合并集回退基础表；诊断 ID 公共契约面，2026-09-06 登记）。钉：`QedP2C1bAdditionalFilesPins`（5 枚）+ `tests/GateFixture/ExtendedWhitelist` 真实构建门。**L2 生成器亦已真接线（QED-C1c）**：扩展 API 的每方法 Signature 以字面量 Claims emit（扩展-only）；全链路（L3 诊断 + L2 emit）已受保护。合计钉：`QedP2C1bAdditionalFilesPins`（5）+ `QedP2C1aMergedWhitelistPins`（4）+ `QedP2C1cGeneratorAdditionalFilesPins`（3）+ `tests/GateFixture/ExtendedWhitelist` 真实构建门（接线被拔即红）
13. ~~L1 包 net9.0 TFM 仅含代数切片~~ **已解决（P1-B5）**：net9.0 缩减切片已删除（A2-06 分析器自包含后为残迹），包族（L1/Generator/Runtime）收敛 `net8.0;net10.0`——同包各 TFM 同一公共面（QedP1B1 快照唯一准绳）；net9 消费者按 NuGet 就近原则消费 net8.0 资产。分析器包保持 net9.0（编译器宿主对齐，自包含、非消费 TFM）
14. L2 生成器在 IDE 增量编辑的极端序列下可能短暂少生成（transform 内语义绑定不在缓存键，Roslyn 文档明示的受限模式）；全量 `dotnet build` 恒正确——CI 门不受影响（R3-CG-07）
15. `ToJson → Parse` 往返对 C# 手工构建剧本只在「claim scope == 所属 event scope」时闭合（JSON 契约的单一真相即事件级 scope）；C# API 允许构造混 scope 剧本（审计语义按 claim 各自 scope 生效），导出再解析会被拒——混 scope 请自留 C# 数据（R3-L1-07）
16. `O(E·K·log E)` 以「互异 (资源,scope) 组数 D 有界」为前提；逐事件独立 scope/resource 的脚本 gate(1)/(3) 每采样点扫 net/grp 字典 ⇒ 整体 O(S·D) 超线性（实测 4 倍数据 ≈6x，曲线钉 `ProdAuditR4AuditScaleTests`）。`NegativeDip`/`CompatibleConflict` 逐采样点上报在此区间输出可达 O(S·G) 条（R4-JD-05/06）
17. **Runtime 异常方言表**（R4-RH-05/15）：JSON 契约=`FormatException`；L1 参数违约=`ArgumentException`（含 `ArgumentOutOfRangeException` 子类）；Runtime 装载校验=`LoadValidationException`；Runtime 状态机/装配前置=`InvalidOperationException`。catch 面按表接，跨族混接会漏。**【QED-A4 冻结 2026-09-06】**自此刻四族方言为冻结契约（L1 族钉 `QedP0A4ContractFreezePins`，Runtime 族钉 Runtime.Tests 既有套件）；变更=semver major
18. ~~契约面子集~~ **已解决（P1-B4a/B4b）**：非契约面的 C# 超集本体（Resource 侧 Tree/Self/Physics/Disk/Signal/AudioMixer/Callback/Network/Input + NodePathOrUnknown；Scope 侧 Shell/Loop/Conditional/Async）已转 internal——外部消费者只能构造契约面（6 资源 × 4 scope），`ToJson` 抛异常的分叉不复存在。超集语义仍供 §7 白名单层内部使用（仓库测试/样例经 InternalsVisibleTo 授权）
19. ~~`CrashReports`/`_netAccum` 单实例有界增长~~ **已解决（QED-C2）**：`CrashReports` 环形上限恒保留最近 64 条（last 语义不变）；`_netAccum` 在每批次卸载/退出排空完成点自动剪除非 Active Fiber 条目（零语义损失：Active 过滤器永久跳过 + 同 FiberId 不可重注册）。`ResetDiagnostics` 降级为宿主可选显式出口（非内存安全义务）。钉：`QedP2C2DiagnosticsBoundPins`（3 枚，经 IVT 断言 internal 观测口）
20. §7 API 白名单层**常量实例保守合并**（QED-A7）：无身份差分资源族（`Callback("cb")`、`AudioMixer(0)`、裸名 memory 哨兵）跨调用点折叠到单一实例——`Connect(sigA)` + `Disconnect(sigB)` 在静态层 net=0（泄漏被掩蔽）。JSON 剧本契约面不受影响（显式 id 即身份，拒裸名，钉 `QedP0A7AliasFoldingPins`）；该盲区以运行期 Σnet 为权威判据（同 ⑦ 宪法）。参数化 alias 与 F1 流敏感化同窗评估

验证：`dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror` 0 错误（AnalyzerConsumer 样例 1 条 EAA0901 故意泄漏警告为设计——「分析器在真实编译路径活着」的可见证据，R6-P）；`dotnet test --no-build` 全绿（三测试工程：L1 主套件 + Runtime + SampleGame，总数以 CI 汇总为准；doc-guard 禁止硬编码会漂移的全量计数）。