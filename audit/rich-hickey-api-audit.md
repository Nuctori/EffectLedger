# Rich-Hickey 视角 API 设计审计 — 5 轮综合 (Cosmos.EffectAlgebra)

> 5 轮独立 fresh-context 审计（R1 表面面积/命名 · R2 跨子系统耦合 · R3 Simple-vs-Easy/锐边 · R4 值/状态/身份 · R5 上手现实）。
> 原则：区分**被证明义务/设计决策锁死**（不可改语义）与**可安全削减**（降认知负担且不丢表达力）。

## 一、共同结论（5 轮收敛）

- **核心代数语义（L1）健壮、被 iter-code 闭环测试锁死**，不是负担来源。
- 真实认知负担来自：(a) 几处**冗余别名/词冲**，(b) **值/身份混淆**（Signature 无结构相等），(c) **静默吸收锐边**（被代数锁死，只能补文档/诊断），(d) **上手入口缺失**（无 README、EAA 严重度不随包发、白名单静默盲区）。
- 多个 brief 预设摩擦点**经核实不存在**：无 `[Effect]` 特性、无 `IsExternalInit.cs`、无 partial-class 用户义务（生成类 partial 但用户无需声明）。init-only setter 已被 auditR5 F1 **主动删除**（防 Budget 可变）。

## 二、被锁死（不可改语义，仅可补文档/诊断）

- `Add`(∑端点求和) vs `Merge`(min/max join) 二元分置 — D-022（Merge 吞守恒 HIGH 缺陷锁）。
- `Unknown→Use`（§3.2.3 P4 / DO-10）— weakest-compat = **fail-open**（最宽松），真实冲突静默吞。只能文档直言。
- `IsConserved` ⊤⇒false（DO-9 含0判据）、`Size ?? [1,1]`（§3.1.5a DO-1）、`ω=⊤` 居民层静默豁免（MA-002）、`Deviation` ⊤⇒整表静默（§9.1）— 均 iter-code 锁。
- `[EffectOverride]` reason 必填 + CI approve（逃逸须带证据）、编译期 EAA→error 门禁（P1-4）、白名单强类型（L1 真源）。

## 三、可安全削减 / 修复（已落地 TDD 见 commit）

1. **Signature 缺结构相等**（R4 P1）：`class Signature` 无 `Equals/GetHashCode`，引用相等却看似值 → 潜伏 footgun。→ 补结构相等（`Objects.cs`）。
2. **EAA0901 诊断只报不导**（R5）：message 补"加 release-class 调用或标 `[EffectOverride]`"显式修复动作。
3. **缺 README 上手入口**（R5）：新增 10 行快速开始（接线 L2+L3、复制 5 行 `.editorconfig`、写一个含 acquire/release 的方法）。
4. **Canonical() 三处复制**（R2 #3）：Analyzer:101、Generator:107 内联、tests:14 各写一份 → 上提到 L1 `GodotApiWhitelist.Canonical`，单一真源。

## 四、评估后**拒改**（会破坏生成器/测试或撞设计锁）

- 删除 `Combination.Sequence/Parallel`：生成器 emit + `LoopCombinationTests` 消费，**删即破编**。
- `DependencyGraph.Register`→`AddFiber` / `Fiber.Effect`→`DeclaredEffect` / `GodotShell._deferred` 按值重键：breaking API + 测试涟漪。
- `EffectScript.Audit` 改复用 L1 原语：大重构，且与 D-037（`fiber.Dependents` 级联）冲突，收益不抵风险。
- 删除 `Signature.Join`：低价值（无外部消费证据但生成器可能 emit），安全保留。

## 五、文档化建议（非代码，留作后续）

- 一页 "values vs identities" 说明：Claim/ResourceId/ScopeId/Signature/EffectScript = 值；Fiber/PluginRuntime/GodotShell = 身份(状态)。
- 直书 `Unknown=fail-open`、"`loop:⊤` 静默豁免"、`Size 省略=精确[1,1]` 三处锐边语义，并建议默认规则对未知资源走 ⊤ 冲突（MA-010）。
- 白名单扩展流程（ApiMapping.cs 手改 + 重建）写入文档。
