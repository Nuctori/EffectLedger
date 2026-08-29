# Cosmos 一键接入（仅 C#）

## 任意 Godot C# 项目

```pwsh
dotnet add package Cosmos.EffectAlgebra
dotnet add package Cosmos.EffectAlgebra.Generator
dotnet add package Cosmos.EffectAlgebra.Analyzer
dotnet add package Cosmos.EffectAlgebra.Runtime
# .editorconfig 已含 EAA*=error（泄漏/量纲/兼容/逃逸参数），build 即门禁
```

## 白名单扩展

`cosmos.effect.json` 放在项目根：

```json
{ "extraMappings": [{ "api": "MyPool.Spawn", "claims": [{ "kind": "occupy", "resource": { "memory": 1 }, "mode": "create", "scope": { "scene": "Battle" } }] }] }
```

## AI 闭环

```pwsh
# 1. AI 产 JSON（符合 docs/effect-script.schema.json）
# 2. 一键审计
dotnet run --project src/Cosmos.EffectAlgebra.Tool -c Release -- audit effect.json --out violations.json
# 3. violations.json 喂回 LLM 重投直至 passed
```

## CI

`.github/workflows/cosmos-audit.yml` 已含 build/test/audit/pack 四门。
