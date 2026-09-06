# Cosmos 一键接入（仅 C#）

## 任意 Godot C# 项目

> **发布状态（R3-DT-01）**：以下包尚未发布到 nuget.org（`dotnet add package` 会 NU1101）。发布前请从 Cosmos 仓库源码引用（见主 README ①）。

```pwsh
dotnet add package Cosmos.EffectAlgebra
dotnet add package Cosmos.EffectAlgebra.Generator
dotnet add package Cosmos.EffectAlgebra.Analyzer
dotnet add package Cosmos.EffectAlgebra.Runtime
```

**门禁须自行接线**（NuGet 包不含 severity 策略，诊断默认 warning）：把主 README §② 的六行 `dotnet_diagnostic.EAA*.severity = error` 复制进你的 `.editorconfig`，build 即门禁。不接线时泄漏只出 warning——别把"有告警"当"已拦截"。

## 白名单扩展

`cosmos.effect.json` 放在消费工程并接入 AdditionalFiles（可直接复制的样板见 `templates/cosmos.effect.json`）：

```json
{ "extraMappings": [{ "api": "MyPool.Spawn", "claims": [{ "kind": "occupy", "resource": { "memory": 1 }, "mode": "create", "scope": { "scene": "Battle" } }] }] }
```

```xml
<!-- .csproj：AdditionalFiles 是 L3 分析器与 L2 生成器共同消费本文件的唯一通道 -->
<AdditionalFiles Include="cosmos.effect.json" />
```

> **接线状态（QED-C1b/C1c，全链路已通）**：L3 分析器与 L2 生成器均已自动消费该文件——经 AdditionalFiles 接入即生效（扩展 API 参与 EAA* 诊断，触发前提仍是接收者绑定 Godot 命名空间，见主 README ③；L2 为扩展 API emit 每方法 Signature，扩展-only 方法以字面量 Claims 内嵌）；解析/schema/Canonical 碰撞错误编译期报 **EAA0701**（该文件扩展整体弃用、基础白名单不受影响；L3/L2 同契约 ID 各报一次）。自写 CI 严格门时请在调用方传 `LoadExtra(path, strict: true)` / `AllWithExtra(...)`（cosmos audit CLI 审计的是 effect-script 契约剧本，不消费本文件）。

## AI 闭环

```pwsh
# 1. AI 产 JSON（符合 docs/effect-script.schema.json；templates/effect-script.json 为可直接 Parse 的样板）
# 2. 一键审计（消费工程：安装 .NET tool，勿用仓库相对路径 R3-CG-09）
dotnet tool install -g Cosmos.EffectAlgebra.Tool   # 包发布前：在 Cosmos 仓根 dotnet pack 后安装。源路径二选一：无 -o 打包用 --add-source ./src/Cosmos.EffectAlgebra.Tool/bin/Release；dotnet pack -o <dir> 用 --add-source <dir>（R6-P）
cosmos audit effect.json --out violations.json
# 3. violations.json 喂回 LLM 重投直至 passed（退出码 0=passed / 2=违例 / 1=错误；载荷含 events 审计计数）
```

## CI

`.github/workflows/cosmos-audit.yml` 提供 build/test/audit/pack 四门（默认手动触发 workflow_dispatch；push/PR 门由 ci.yml 承担，二者不再重复跑）。Tool 项目已入 slnx，`dotnet pack -c Release` 会一并产出 `cosmos` .NET tool 包。
