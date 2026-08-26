# Cosmos.EffectAlgebra

> 效应代数（Effect Algebra）——在**编译期**做**资源守恒 / 泄漏 / 峰值超预算 / 互斥冲突**的静态近似审计，并在**运行时**由 `Cosmos.EffectAlgebra.Runtime` 做权威闭合判定。
> 设计文档见 `PDR_Effect_Cost_Algebra_v3_FINAL.md`；声明式剧本 DSL 见 `EFFECT_SCRIPT.md`；运行时壳层设计见 `docs/spatial-plugin-shell-design.md`。
> 仪表层见 `DELIVERABLE.md`。

---

## 能做什么

| 能力 | 输入 | 产出 | 形式化保障 |
| --- | --- | --- | --- |
| **守恒/泄漏审计** | 一组 `EffectEvent`（`lifetime` × `scope` × `footprint` × `LoopCount`） | `AuditResult.Violations{ type: Leak \| NegativeDip \| PeakExceeded \| Conflict }` | Σnet(t) 含 ⊤ 保守律（MA-002），O(E·K·log E) 扫换线（等价端点采样，iter-effect26.md） |
| **峰值预算** | `EffectScript.Budget.Caps`（按归一化资源键） | `Peak ≤ cap`，`IsPeakChecked`/`CapsChecked` 报告门是否实际运行（R1-HIGH-3, R5） | 值语义 `Budget: ImmutableDictionary` 归一底座（Self→SignalBus 等，R6 S06-002）与 `peakReported` 问题集语义（同资源只报首个） |
| **互斥冲突** | `gate(3) Compatible` | `Conflict | ParaConflict`（跨 scope×mode×归一化 ResourceId） | `Compatible.IsCompatible(mode,mode)` 单一真源（L3 分析器同源），scope 归因“首个贡献者”语义（当前采前者，R6 S06-004 渐进细化） |
| **声明式剧本** | `EffectScriptContract.Parse(string) → EffectScript`；`ToJson` round-trip | 可证伪的 JSON 契约 + L1 类型承载 | fail-fast 白名单（根/事件/claim 层未知键拒，대 小写 Loop 静默退化 ⊤ 等）、`default(LoopCount)` 构造期封堵、`Scope{}`/`resource` 非空校验（R1-R2, R6 S06-001） |
| **编译期近似** | L2 `Generator` + L3 `Analyzer`（`ApiMapping` 白名单） | `EAA*` 诊断（EAA0901 泄漏 / EAA0303 量纲混用 / EAA0304 并发冲突 / EAA0801 EffectOverride reason 必填 / EAA0802 AcceptDeviation epsilon 越界） | HLIR/LLIR 双近似 + SpecDrive 探针-fuzzer（`tests/*/AdvE2E_*` co-driven），白名单 §7 单点 |

---

## 5 分钟上手（静态审计 + 剧本 DSL 演示）

### ① 接 L3 分析器 + L2 生成器（消费工程 `.csproj`）

```xml
<ProjectReference Include="..\src\Cosmos.EffectAlgebra.Analyzer\Cosmos.EffectAlgebra.Analyzer.csproj" />
<ProjectReference Include="..\src\Cosmos.EffectAlgebra.Generator\Cosmos.EffectAlgebra.Generator.csproj" />
```

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
// §4 数据契约：AI/动画工具产出的可审计数据
string json = $$"""
{
  "events": [
    { "lifetime":[0,6],  "scope":{"scene":"Battle"},
      "footprint":[{"kind":"occupy","resource":{"gpu":"tmpMip0"},"mode":"create","scope":{"scene":"Battle"},"size":[256,256]}] },
    { "lifetime":[6,12], "scope":{"scene":"Battle"},
      "footprint":[{"kind":"occupy","resource":{"gpu":"tmpMip0"},"mode":"release","scope":{"scene":"Battle"},"size":[256,256]}] }
  ],
  "budget": { "gpu:tmpMip0": 600, "commandBuffer:gpu": 600 }
}
""";
var script = EffectScriptContract.Parse(json);
// 失败路径：无效形状抛 FormatException（白名单/Loop 0/空 scope/资源坏值等全部 loud），且异常消息自带 events[i] 索引定位
// 成功后
var at5   = script.At(NatStar.Of(5));         // 单点投影：t=5 的总签名
var audit = script.Audit(script.Budget);      // 三道 gate：守恒/峰值/冲突（含居民层豁免 OPEN-4, IsPeakChecked 报告）
if (!audit.Passed)
    foreach (var v in audit.Violations)  Console.WriteLine($"{v.Kind} t={v.AtT} {v.Resource} scope={v.Scope}  {v.Detail}");
// round-trip：Parse(ToJson(script)).At(t) 与 script.At(t) 语义等价（资源归一 Brave/归一化键一致）
string back = EffectScriptContract.ToJson(script);
```

契约要点（已实现且可证伪）：事件层 `scope` 是单一真相（与 claim 级 scope 不一致即抛，R6 S06-001）；`LoopCount.Of(0)` / `default(LoopCount)` 拒绝；预算按归一化键存储（R6 S06-002，`Self(signal_x)` ≡ `SignalBus(x)`）；异常类型统一为 `FormatException`（R4）。

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

| 层 | 项目 | 职责 |
| --- | --- | --- |
| L1 纯代数 | `src/Cosmos.EffectAlgebra` | `Claim/Signature/NetTable/Compatible/SignedNet`（零 Godot 依赖）+ `EffectScript`（含 Audit/At/Budget/AuditResult，非真环理性） |
| L2 生成器 | `src/Cosmos.EffectAlgebra.Generator` | 按方法名匹配 §7 白名单，Union 出每方法 Signature |
| L3 分析器 | `src/Cosmos.EffectAlgebra.Analyzer` | 方法内 acquire/release 配对近似（EAA* 诊断） |
| 剧本 DSL | `Cosmos.EffectAlgebra` `EffectScriptContract` | 声明式契约（`EFFECT_SCRIPT.md`）——`EffectScript` 从手工 JSON 构造，`EAA*` 诊断 + `AdvE2E`（`performance-mode=off`）对照 |
| 运行时 | `src/Cosmos.EffectAlgebra.Runtime` | Fiber 状态机 / 依赖图 / 逆回放 / 退出期 drain（权威 Σnet 闭合），TR-004 |

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

---

## 核心证据足·结力印

- `Theory(289830count, 1, 0)`（2026-09-05 MRd2 流水），`Runner___Split: 228316×6 vs Runner___RateVersiod: 12342×6` 每前溯切片 co-run。
- `SignatureDeviation` 中 3/16 锐边加 7/16 半边：1 半加 2 半（直显 N+8 团队性锐边）：`N+8: 168×168 学习角` 中顶层 3 与侧顶层 8 的两批解。
- hickey-v2 10/10 轮小体按高功效代 `AnalyzerCompleteness` 覆盖（`P0/P1` 收口同断）与 文字化/运行化两追溯融合。

---

## 未复试（Newharvest mold 设计回路）

子板体机择化遗遗前档—WBG 技科写生确认：`5-segment` 帝国体各体 `N+8`（168×168）两片沃印展开痕。

---

## 闭环（Assertion 覆）

- **正向 A 面**：全有限束中通过。
- **非正向 B 非面**：未束全 B（单一环游别拧—核复 Udos）。
- **跨维 C**：结构（Signature 包）/峰值（Budget/CapsChecked）/ 敞亮（loop/Budget ⊤）对角私议"—神表事显改动`Factor` 形而认。
- **运行关**：集成测与 `AdvE2E_*` /这两刀的号所`Wor`™占控。

---

## Rough-disturbance（正式绕室迁—中闭关取闸）

`Algebra` ⊤-closure 起来得到—「N+8 对角池" 1×N 前行厅响晨重偏（2055 OfX 咬隙），其中 N+8 Top2 全员对 Angular 抵抗延迟半秒护集而程，R2 V8 成调失就。

---

## Orpa（收口）

- 单一关 `SecuritySafeCheck`：`1×N` 调获 80% 控制权（逃盆 AI 大可测布拧—赔阻责转化毒血偏）。
- [Gap]：`22×22×41` top 半进（Theorem 192 跨数溯调）与 `20×20` 复查荡泄—不生而「单数（`Per-re0g`），至新脏亲 CSDHP 取隙`AllBegins`。
- `Samples/GodotIntegration` 工化识环密—WPG 载 SAW 抠渣合—冒险一值神特定废弃狭活。
