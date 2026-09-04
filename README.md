# Cosmos.EffectAlgebra

> 效应代数（Effect Algebra）——在**编译期**做**资源守恒 / 泄漏 / 峰值超预算 / 互斥冲突**的静态近似审计，并在**运行时**由 `Cosmos.EffectAlgebra.Runtime` 做权威闭合判定。
> 设计文档见 `PDR_Effect_Cost_Algebra_v3_FINAL.md`；声明式剧本 DSL 见 `EFFECT_SCRIPT.md`；运行时壳层设计见 `docs/spatial-plugin-shell-design.md`。
> 仪表层见 `DELIVERABLE.md`。

---

## 能做什么


| 能力          | 输入                                                                      | 产出                                                                                                                          | 形式化保障                                                                                                                      |
| ----------- | ----------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| **守恒/泄漏审计** | 一组 `EffectEvent`（`lifetime` × `scope` × `footprint` × `LoopCount`）      | `AuditResult.Violations`（type: Leak / NegativeDip / PeakExceeded / Conflict）                                              | Σnet(t) 含 ⊤ 保守律（MA-002），O(E·K·log E) 扫换线（等价端点采样，iter-effect26.md）                                                          |
| **峰值预算**    | `EffectScript.Budget.Caps`（按归一化资源键）                                     | `Peak ≤ cap`，`IsPeakChecked`/`CapsChecked` 报告门是否实际运行（R1-HIGH-3, R5）                                                         | 值语义 `Budget: ImmutableDictionary` 归一底座（Self→SignalBus 等，R6 S06-002）与 `peakReported` 问题集语义（同资源只报首个）                         |
| **互斥冲突**    | `gate(3) Compatible`                                                    | `Conflict / ParaConflict`（跨 scope×mode×归一化 ResourceId）                                                                | `Compatible.IsCompatible` 对角律单一真源（CONFLICT 集 §3.2.3）                                                               |
| **声明式剧本**   | `EffectScriptContract.Parse(string) → EffectScript`；`ToJson` round-trip | 可证伪的 JSON 契约 + L1 类型承载                                                                                                      | fail-fast 白名单（根/事件/claim 层未知键拒，대 小写 Loop 静默退化 ⊤ 等）、`default(LoopCount)` 构造期封堵、`Scope{}`/`resource` 非空校验（R1-R2, R6 S06-001） |
| **编译期近似**   | L2 `Generator` + L3 `Analyzer`（`ApiMapping` 白名单）                        | `EAA*` 诊断（EAA0901 泄漏 / EAA0303 量纲混用 / EAA0304 并发冲突 / EAA0801 EffectOverride reason 必填 / EAA0802 AcceptDeviation epsilon 越界） | HLIR/LLIR 双近似 + SpecDrive 探针-fuzzer（`samples/GodotIntegration/AdvE2E_*` co-driven），白名单 §7 单点                                                |


---

## 5 分钟上手（静态审计 + 剧本 DSL 演示）

### ⓪ NuGet 安装（消费已发布的包；源码引用见 ①）

```pwsh
dotnet add package Cosmos.EffectAlgebra           # L1 代数核心（必需；Generator 产物硬引用其类型）
dotnet add package Cosmos.EffectAlgebra.Runtime   # 运行时权威闭合（仅运行期闭合需要；预算-only 可跳过）
dotnet add package Cosmos.EffectAlgebra.Analyzer  # L3 EAA* 诊断
dotnet add package Cosmos.EffectAlgebra.Generator # L2 每方法 Signature 生成（依赖 L1，NuGet 自动联装）
```

**门禁须自行接线**：诊断默认 warning，把 ② 的五行 severity=error 复制进你的 `.editorconfig`。JSon 剧本一键审计用 `cosmos` CLI（见 ⑤）。


### ① 接 L1 + L3 分析器 + L2 生成器（消费工程 `.csproj`）

```xml
<!-- L1 类型承载：普通引用（生成器 emit 的代码引用 Cosmos.EffectAlgebra.Signature 等） -->
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

> 注意：仅当 API 在 `ApiMapping.cs` 白名单（§7）中才有保护；自定义资源操作需扩展白名单并重建（见该文件注释）。未命中白名单的 API **静默无保护、无警告**。

### ④ 亮点：声明式剧本数据契约（JSON→L1 审计，零 Godot 依赖，AI/自动化可喂）

AI/脚本可直接产出视觉/音频/网络效果的“视觉剧本”JSON，并用 `EffectScriptContract` 不跑游戏即验证——这正是不能把 L1 审计藏在 Godot 回调里、而要把它做成“纯数据类型”的原因。

```csharp
// §4 数据契约：AI/动画工具产出的可审计数据 — claim 省 scope 继承 event scope（R10 Top1）
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

CI 接线示例：`cosmos audit x.json || exit 1` 会把违例当失败（0/2 均非零）——按需用 `if [ $? -eq 2 ]` 区分违例与错误。

---

## 参与者模型（你是谁，怎么用）

**AI（视觉剧本 JSON 生产者）**——瞄 `EFFECT_SCRIPT.md` §4 与 `EffectScriptContract.Parse(string)`：得到它的 R6-E1/E3 覆盖（未知键拒、budget 类型错抛、Loop 0 拒、空 scope 拒）、R2-N1 ⊤ 往返（budget/loop/lifetime ⊤ ↔ "⊤"/"inf"）与 `CapsChecked` 报告（R1-HIGH-3）。

**审计驱动（L1+L2+L3）**——瞄 `EffectScript.Audit(*pretty)*` + `Analyzer/SpecDrive`：报告顶层 run summary（TR-001 形状、E2E co-run）与 HLIR/LLIR 布置。

**运行时集成者（Runtime 权威闭合）**——瞄 `Cosmos.EffectAlgebra.Runtime`：不动游戏代码，依 `docs/spatial-plugin-shell-design.md` 的套件—纤维工艺执行闭合；预算与泄漏的“权威判定”在运行关卡落地（TR-004 闭合，关卡改动只覆盖 `src/Cosmos.EffectAlgebra.Runtime`）。

---

## 已知语义锐边（设计锁死，非 bug；审计/测试/运行闭合中明示）

- `Unknown` 模式 = 最弱兼容 = **fail-open**：未知资源冲突被静默放行（§3.2.3 P4）。
- `loop:"⊤"` 居民层被**静默豁免**泄漏检测；`lifetime:[1,⊤]`（ω 有限）仍计入 Σnet 且报警——同一"常驻"语义，两种相反行为（MA-002）。
- `Claim.Size` 省略 ≠ 未知：`?? [1,1]`（精确 1，既非未知 ⊤ 也非 0 预算）（§3.1.5a）。
- `Effects → Audit` 在“全 finite 结束后”走一次性闭包路径（而非端点增量模拟）对 Leak 的认定与“有限闭合”在有限性严整上等价。

---

## 分层


| 层      | 项目                                            | 职责                                                                                                               |
| ------ | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| L1 纯代数 | `src/Cosmos.EffectAlgebra`                    | `Claim/Signature/NetTable/Compatible/SignedNet`（零 Godot 依赖）+ `EffectScript`（含 Audit/At/Budget/AuditResult，非真环理性） |
| L2 生成器 | `src/Cosmos.EffectAlgebra.Generator`          | 按方法名匹配 §7 白名单，Union 出每方法 Signature                                                                               |
| L3 分析器 | `src/Cosmos.EffectAlgebra.Analyzer`           | 方法内 acquire/release 配对近似（EAA* 诊断）                                                                                |
| 剧本 DSL | `Cosmos.EffectAlgebra` `EffectScriptContract` | 声明式契约（`EFFECT_SCRIPT.md`）——`EffectScript` 从手工 JSON 构造，`EAA*` 诊断 + `AdvE2E`（`performance-mode=off`）对照             |
| 运行时    | `src/Cosmos.EffectAlgebra.Runtime`            | Fiber 状态机 / 依赖图 / 逆回放 / 退出期 drain（权威 Σnet 闭合），TR-004                                                             |


**预算-only 用户**只需 L1 + L2 + L3，完全不必接触 Runtime（Godot 进程树由 `IHost` 抽象隔离）。

---

## 测试与仪表航迹

```pwsh
dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror
dotnet test  Cosmos.EffectAlgebra.slnx -c Release --no-build
```

- **跑盘门**：`a.StableHits==N && b.StableHits>=N && chance(b.AdvHits) ≥ chance(a.AdvHits)`（E2E 相对打点，而非绝对 gate）；跨切片大小/`maxSteps` 强性一致，不要求 `>=` 单袋子的绝对稳定性；`performance-mode=off` 下仍 co-run（含 `AdvE2E_P0` 的 LLIR=空/漏报链）。
- **单一关**：`performance-mode=off`（默认）→ `on` 时标 `Experimental` 并改 `MaxPlayers→auto(100)`。两种模式 `Audit`-led 的 `Advanced*` 事件与 `L2-L3 AdvStats` 同可枚举。
- **迁移/整理**：`EAA0205→EAA0801` 与 `effect-lint` → `effect-lint-eaa` 的迁移用 `SliceCheck`，CI 不改旧 gate 名；`split-slice-check` 并行化，不攫两批高携带的 EAA 查。

---

## 运行期套件—纤维工艺（`docs/spatial-plugin-shell-design.md` 概要）

- **正交**：`World`（`IHost` / 钩子管理） vs `Fiber`（布局身），发明转录存活单独于 `World`；GLUT 统揽 `Witr` OOP（ZX），对齐 `EffectScript` 退化为「不是层化的平淡」效果；`World↔Fiber.Contract` 压缩变种 SLO 加强。
- **迁移范畴**：逐屏/迁移生成的「落入单张画时预候状态化 + 已有技性裹府后上后`At`权」与 `GodotShell.IsSafeToInvoke + ProviderCrashCascade` 分责。
- **宿主合**：`ReasonOp` / `ScopeNested` / `ALL4` 闭合时 `GodotShell.IsSafeToInvoke` + 应对判据为唯一上「宿主还活+未屏」判层（单层），与 `EffectScript` 从 Godot 的 `OnHoverMove` 分离。

## 诚实边界（故意留债 · 测试守住不漂移）

1. `Sequence≡Parallel≡Union` 四名一实 — L1 无时序语义，`R5 V5-001` 已在 `Join` 置顶 `L1 警告`，生成器锁死
2. `Size ?? Interval.Default` 散布 — `§3.1.5a DO-1` 设计锁，新消费点禁再散布
3. `Audit` 内联 sweep 与 `NetTable`/`Peak` 两份物理代码 — `D08-001/002` 钉住等价，不做重构
4. `At(t)` 投影不带事件来源 / `EventIndex` 取首个贡献者非峰值最大者 — `R9` YAGNI
5. `At(t)` 是**集合投影**（同刻逐字段相同的重复事件去重计 1）；并发计数/峰值语义以 `Audit` 扫换线为准（逐事件累加）——自建核对脚本请勿用 `At`+`Derived.Peak` 对账峰值
6. `NegativeDip`/`CompatibleConflict` 逐采样点上报（时间序列语义），仅 `PeakExceeded` 做问题集去重（每资源首个反例）——三门去重口径不同是显式设计，喂 AI 回修前请自行按 `(Kind,Resource)` 去重
7. `EAA0901` 哨兵资源跨 API 假配对：`Load`（Mem create）+ `QueueFree`（Mem release）在同方法内按语法计数互相抵消——跨 API 家族的加载泄漏属静态近似盲区，以运行期 Σnet 为权威判据
8. `EAA0303/0304` 是意图提示而非数学缺陷：哨兵资源上惯用形态（`DrawRect`×2、`MoveAndSlide`+`GetSlideCollisionCount`）会触发，按需 `[EffectOverride("理由")]`（它们不豁免 EAA0901）
9. L3 仅分析**方法体**：构造函数、属性访问器、`using var` 形态不在注册范围；`Position.get/set` 等属性形态白名单条目对 L3 无效——构造期泄漏不在静态覆盖内
10. Runtime 非线程安全（帧驱动单线程模型，零锁）：全部调用须在宿主主线程；实例为单场景生命周期——场景重载请新建 `PluginRuntime`（Dead fiber 与图边不回收、同 FiberId 不可重注册，`R7-L1`）
11. Runtime `Σnet` 闸门按 `⊆*` 过滤：Effect claim 中 scope ⊄* fiber.Scope（如 Global）的资源**不参与**该 fiber 的守恒判定（L1 `EffectScript.Audit` 无此过滤）——两层口径差异，跨 scope 泄漏请以 L1 剧本审计为权威
12. `cosmos.effect.json` 白名单扩展当前仅提供 L1 加载 API（`CosmosEffectConfig.LoadExtra/AllWithExtra`），L2 生成器/L3 分析器尚未自动消费 `AdditionalFiles`——接线前该文件不生效（勿当作已受保护）
13. L1 包 net9.0 TFM 仅含代数切片（无 `EffectScript`/DSL 类型，为分析器源内嵌而设）；需要剧本 DSL 请引用 net8.0 或 net10.0 TFM

验证：`dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror` 0 警告 0 错误；`dotnet test --no-build` 全绿（95 Runtime + Tests + 73 SampleGame——Tests 计数随迭代增删，以 CI 汇总为准；文档硬编码总数已随漂移移除，见 doc-guard 测试）。