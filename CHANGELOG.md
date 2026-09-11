# Changelog

Cosmos.EffectAlgebra 的用户可见变更记录。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 计划首发（未发布——发布流程见 `PUBLISH-CHECKLIST.md`）

首个公开发布版本。契约面（6 资源 × 4 scope × kind 3 × mode 5）与公共 API 面（43 类型）
自本版本起**冻结**：任何破坏性变更 = semver major + PDR 决策记录。

#### L1 纯代数核心（零 Godot 依赖）

- ℕ∪{⊤} 保守闭合算术（NatStar）：溢出⇒⊤、⊤ 吸收律、全序 ≤。
- 有符号 ℤ* = ℤ ∪ {±⊤}（ZStar/SignedInterval）：区间加法、ContainsZero 即守恒判定。
- 区间 [lo,hi] 不变量（lo≤hi / Default[1,1] / Merge join-semilattice 幂等·交换·结合）。
- ScopeId 单向偏序（Global 唯一最大元、跨标签不可比、Shell ⊑ Shell 自反）。
- Compatible 全函数（25 组合矩阵穷举 + 对称律 + Unknown→Use fail-open）。
- SignedNet 有符号守恒判定（ContainsZero ⇔ DO-9 不报警，任一端 ⊤ fail-closed）。
- 效应代数 DSL：`EffectScriptContract.Parse/ToJson/Audit`——AI 产出 JSON 剧本零 Godot 机审。

#### L2 源生成器

- 每标注方法（[EffectOverride]/[AcceptDeviation]）生成 Signature 组合代码——真委托 L1 白
  名单 Claims（非桩代码），支持泛型/嵌套/重载/partial/深命名空间/关键字名全部形态。
- 白名单扩展真接线：`cosmos.effect.json` 经 AdditionalFiles 被生成器消费（跨文件 Canonical
  碰撞 loud EAA0701 拒绝，无部分生效）。

#### L3 Roslyn 分析器

- EAA0901 泄漏 / EAA0303 量纲混用 / EAA0304 兼容冲突 / EAA0801·0802 特性校验 /
  EAA0701 白名单扩展配置错误诊断。
- 白名单扩展真消费：`cosmos.effect.json` 经 AdditionalFiles 后扩展 API 参与 EAA* 分析
  （跨文件碰撞 EAA0701 loud，与 L2 同契约 ID）。

#### 运行时

- `PluginRuntime` 五态 Fiber 状态机（Inactive→Active→Suspending→TearingDown→Dead）。
- 依赖图拓扑排序（dependent-first teardown）+ 软环降级 + 硬环拒载。
- 逆回放（LIFO 逆序，恰好一次语义）+ 看门狗三路径（强制/自愈/级联）+ 崩溃级联 fail-open。
- 诊断有界：CrashReports 环形上限 64 条、_netAccum 自动剪除非 Active 条目。

#### CLI

- `cosmos audit <script.json> [--out violations.json]` 一键门——AI 闭环工具。
- 退出码契约：0=通过 / 2=存在违例 / 1=解析或 IO 错误。

#### 形式化验证

- 89 条 Dafny 定律（`formal/CosmosEffectAlgebra.dfy` + `formal/CosmosSweepLine.dfy`）：
  NatStar ⊤ 闭合与溢出⇒⊤ / Interval 半格 / ScopeId 单向偏序 / Compatible 对称 /
  SignedNet 守恒 / 扫换线采样充分性与增量等价。
- `dafny verify` 纳入 CI 门禁（verified ≥89 计数门，删减即红）。
- 实现对照性质测试 8 组（NatStar 算术/Interval 合并/ScopeId 偏序/Compatible 矩阵/
  SignedNet 加法/ZStar 饱和/NetTable 暴力求和/扫换线峰值阈值边界）。

#### 已知边界

20 条诚实边界见主 README「诚实边界」节（逐条附测试钉/文档引用 + 【影响】标签 + 组件速查；
6 条已在 QED 路线中解决划线标记）。完整决策链见 `audit/qed/ROADMAP.md`。
