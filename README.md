# Cosmos.EffectAlgebra

效应守恒代数（Effect Cost Algebra）—— 在编译期对 Godot 资源操作做**守恒/泄漏**静态近似审计，并在运行期由 `Cosmos.EffectAlgebra.Runtime` 做权威闭合判定。

> 设计文档见 `PDR_Effect_Cost_Algebra_v3_FINAL.md`；声明式剧本 DSL 见 `EFFECT_SCRIPT.md`；运行时壳层设计见 `docs/spatial-plugin-shell-design.md`。

## 5 分钟上手（预算/泄漏检查）

1. **接 L3 分析器 + L2 生成器**（消费工程 `.csproj`）：

   ```xml
   <ProjectReference Include="..\src\Cosmos.EffectAlgebra.Analyzer\Cosmos.EffectAlgebra.Analyzer.csproj" />
   <ProjectReference Include="..\src\Cosmos.EffectAlgebra.Generator\Cosmos.EffectAlgebra.Generator.csproj" />
   ```

2. **把 EAA* 诊断设为 error**（否则只是 warning，门禁失效）——根 `.editorconfig` 已含，消费工程复制这几行：

   ```
   [*.cs]
   dotnet_diagnostic.EAA0901.severity = error   # 疑似资源泄漏（acquire 无配对 release）
   dotnet_diagnostic.EAA0303.severity = error   # 同资源量纲混用
   dotnet_diagnostic.EAA0304.severity = error   # 同资源并发冲突模式
   dotnet_diagnostic.EAA0801.severity = error   # [EffectOverride] reason 必填
   dotnet_diagnostic.EAA0802.severity = error   # [AcceptDeviation] epsilon 越界
   ```

3. **写一个含 acquire/release 的方法**（无需任何特性、无需 partial 类、无需改游戏代码）：

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

## 分层

| 层 | 项目 | 职责 |
| --- | --- | --- |
| L1 纯代数 | `src/Cosmos.EffectAlgebra` | Claim/Signature/NetTable/Compatible/SignedNet（零 Godot 依赖） |
| L2 生成器 | `src/Cosmos.EffectAlgebra.Generator` | 按方法名匹配 §7 白名单，Union 出每方法 Signature |
| L3 分析器 | `src/Cosmos.EffectAlgebra.Analyzer` | 方法内 acquire/release 配对近似（EAA* 诊断） |
| 剧本 DSL | `src/Cosmos.EffectAlgebra` `EffectScript` | 声明式守恒剧本（`EFFECT_SCRIPT.md`） |
| 运行时壳 | `src/Cosmos.EffectAlgebra.Runtime` | Fiber 状态机 / 依赖图 / 逆回放 / 退出 drain（权威 Σnet 闭合） |

**预算-only 用户**只需 L1 + L2 + L3，完全不必接触 Runtime（Godot 进程树细节由 `IHost` 抽象隔离）。

## 已知语义锐边（设计锁死，非 bug）

- `Unknown` 模式 = 最弱兼容 = **fail-open**：未知资源冲突被静默放行（§3.2.3 P4）。
- `loop:"⊤"` 居民层被**静默豁免**泄漏检测；`lifetime:[1,⊤]`（ω 有限）仍入 net 并报警——同一"常驻"语义，两种相反行为（MA-002）。
- `Claim.Size` 省略 ≠ 未知：`?? [1,1]`（精确 1，既非未知 ⊤ 也非 0 预算）（§3.1.5a）。

## 测试

```pwsh
dotnet test Cosmos.EffectAlgebra.slnx   # 453 passed：297 单元 + 69 E2E + 87 Runtime
```
