# Cosmos 一键接入（仅 C#）

## 任意 Godot C# 项目

```pwsh
dotnet add package Cosmos.EffectAlgebra
dotnet add package Cosmos.EffectAlgebra.Generator
dotnet add package Cosmos.EffectAlgebra.Analyzer
dotnet add package Cosmos.EffectAlgebra.Runtime
```

**门禁须自行接线**（NuGet 包不含 severity 策略，诊断默认 warning）：把主 README §② 的五行 `dotnet_diagnostic.EAA*.severity = error` 复制进你的 `.editorconfig`，build 即门禁。不接线时泄漏只出 warning——别把"有告警"当"已拦截"。

## 白名单扩展

`cosmos.effect.json` 放在项目根：

```json
{ "extraMappings": [{ "api": "MyPool.Spawn", "claims": [{ "kind": "occupy", "resource": { "memory": 1 }, "mode": "create", "scope": { "scene": "Battle" } }] }] }
```

> **诚实边界（当前未自动生效）**：该文件目前仅有 L1 加载 API（`CosmosEffectConfig.LoadExtra / AllWithExtra`）；L2 生成器与 L3 分析器**尚未**自动消费它——不写接线代码时自定义 API 不会进白名单，也就是"静默无保护"。自写 CI 严格门时请在调用方传 `LoadExtra(path, strict: true)` / `AllWithExtra(...)`（cosmos audit CLI 本身不消费该文件，也无 strict 开关）。

## AI 闭环

```pwsh
# 1. AI 产 JSON（符合 docs/effect-script.schema.json；templates/effect-script.json 为可直接 Parse 的样板）
# 2. 一键审计
dotnet run --project src/Cosmos.EffectAlgebra.Tool -c Release -- audit effect.json --out violations.json
# 3. violations.json 喂回 LLM 重投直至 passed
```

## CI

`.github/workflows/cosmos-audit.yml` 提供 build/test/audit/pack 四门（默认手动触发 workflow_dispatch；push/PR 门由 ci.yml 承担，二者不再重复跑）。Tool 项目已入 slnx，`dotnet pack -c Release` 会一并产出 `cosmos` .NET tool 包。
