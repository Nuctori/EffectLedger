# Changelog

Cosmos.EffectAlgebra 的用户可见变更记录。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 计划首发（未发布——发布流程见 `PUBLISH-CHECKLIST.md`）

首个公开发布版本。契约面（6 资源 × 4 scope × kind 3 × mode 5）与公共 API 面（43 类型）
自本版本起**冻结**：任何破坏性变更 = semver major + PDR 决策记录。

#### 新增

- **L1 纯代数核心**（零 Godot 依赖）：ℕ∪{⊤} 保守闭合算术、区间半格、ScopeId 单向偏序、
  Compatible 全函数（Unknown→Use fail-open）、SignedNet 有符号守恒判定。
- **L2 源生成器 / L3 Roslyn 分析器**：EAA0901 泄漏 / EAA0303 量纲混用 / EAA0304 兼容冲突 /
  EAA0801·0802 特性校验 / EAA0701 白名单扩展配置错误诊断。
- **剧本 DSL**：`EffectScriptContract.Parse/ToJson/Audit`——AI 产出 JSON 剧本零 Godot 机审，
  CLI `cosmos audit` 退出码契约 0/2/1。
- **运行时**：`PluginRuntime` 五态 Fiber 状态机、依赖图、逆回放、退出排空、看门狗级联。
- **形式化验证**：89 条 Dafny 定律（NatStar/Interval/ScopeId 偏序/Compatible 代数/SignedNet
  守恒/扫换线采样充分性与增量等价），`dafny verify` 纳入 CI 门禁；实现对照性质测试 5 组。
- **白名单扩展**：`cosmos.effect.json` 经 AdditionalFiles 被 L3/L2 真消费（扩展 API 全链路
  受保护；配置错误 EAA0701 loud）。

#### 已知边界

20 条诚实边界见主 README「诚实边界」节（逐条附测试钉/文档引用；6 条已在 QED 路线中解决）。
