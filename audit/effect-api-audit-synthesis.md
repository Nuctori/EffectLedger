# EffectAlgebra API 设计审计 — 5 轮 Rich Hickey 透镜收敛综合

> 5 份独立审计（R1 简单性 / R2 类型正确性 / R3 认知负荷 / R4 JSON 契约 / R5 组合性），各自独立只读 7 个源文件、未读 audit 历史。本文件做收敛：是否收敛于共同的 HIGH 级 API 设计缺陷（用户难度根因），并给出「这个项目会不会造成用户使用困难」的明确结论。

## 0. 结论先行（用户难度判定）

**会，且概率不低。** 5 轮审计在「数学正确性 / 类型算术载体」上高度一致地给出好评（NatStar/Interval/SignedInterval/LoopCount/Compat 把 ⊤、负值、越界做成类型字段，`lo≤hi` 进构造子不变量），但在「面向用户的认知表面」上收敛出 **6 个共同根因**。用户（AI 或人类）写对一个合法剧本需要同时记住约 13 条隐式规则，其中多条之间存在**静默不一致**。

**5 轮收敛的共同 HIGH 根因（按跨轮被点名次数排序）：**

| # | 根因 | 被哪些轮点名 | 是否造成「写完才发现」的静默错误 | 已修？ |
| --- | ------ | ------------ | ------------------------------- | -------- |
| C1 | **scope 分裂**：event scope vs claim scope，At/Audit 用 e.Scope 重写，但 `Derived.Net/Peak` 用 claim scope；claim.scope 在 Parse 必填却被计算忽略 | R1,R2(#12),R3(#8/#9),R5 | 是（同一 Signature 两条路径结论不同） | **部分**（gate3 已对齐 e.Scope，但 `Derived.Net/Peak` 仍 claim scope） |
| C2 | **资源类型爆炸 + 静默兜底**：ResourceId 14 子类但契约仅 5 键；`Memory(0)` 哨兵、`commandBuffer→"gpu"`、`memory` 类型错→0 全部静默改写 | R1,R2(#1-3),R3(#14),R4(#2) | 是（守恒/峰值按错误归一键聚合，静默错） | 否 |
| C3 | **Mode×Kind 组合爆炸**：5×3=15 组合，约 12 种无意义却被静默接受；`move` 在 net 上等同 `create` 反直觉；跨桶矛盾（read+release）无任何 gate 检查 | R1,R2(#9c),R3(#3) | 是（跨桶矛盾写完才发现） | 否 |
| C4 | **JSON 契约 fail-soft**：Global round-trip 必炸、resource 值静默猜测、`⊤` 无 ASCII 别名、缺最小 schema 文档 | R3(#16),R4(全篇) | 是（AI 反复写错/回修） | 否（OPEN-2 三桶已修，Global 未修） |
| C5 | **隐藏可变状态**：`EffectScript.Budget { get; init; }` 使同实例 `Audit()` 结果依赖可变配置 | R5(F1) | 否（但破坏值语义承诺） | 否 |
| C6 | **认知表面无导航**：~23 个公共类型、无 Builder/Example、命名碰撞（ScopeId.Loop vs LoopCount ω、occupy vs Occupancy） | R1,R3(#1/#7/#2) | 是（不知从何下手） | 否 |

> 共识：**数学内核是健康的，API 表面（资源/作用域/契约/组合爆炸/入口）才是造成用户困难的根因。** 这与 Rich Hickey「把边界做成类型、砍偶然复杂度」的纲领直接对应。

## 1. 各轮独立结论摘要

- **R1（简单性 / ct-expert）**：内核本质且简单；外围显著 complect。HIGH×6：ResourceId 膨胀、Normalize 别名、Claim/Event scope 冗余、Weight.NaN、Audit 纠缠、Contract 静默 `"memory:0"`。Top3 砍：合并 Union/Join/Sequence/Parallel 为单一 Union；Weight.NaN→类型化 ⊥；收窄 ResourceId、让 Claim.Scope 从 EffectEvent 派生。
- **R2（类型正确性 / formal-convergence）**：算术载体层 ✅（边界即类型）；领域语义层 ❌。4 个静默破坏正确性的 HIGH：资源同质混淆、Memory(0) 哨兵、ScopeId 偏序 false 兜底、Claim 归一化/跨桶矛盾。Top3：资源同质隔离类型化、Claim 构造即 Normalize+ 拒绝跨桶矛盾、ScopeId.IncludedIn 改 TryIncludedIn。
- **R3（认知负荷 / jeffdean）**：~13 条隐式规则，3 条静默最易踩（scope 分裂、ω=⊤ 免 Leak、默认无 Budget 上限）。HIGH×N。Top3：消除 scope 分裂、收敛 Mode×Kind 组合、给契约最小示例/入口。
- **R4（JSON 契约 / formal-convergence）**：契约形状严格性不一致（kind/mode 严格，resource 值静默猜测）。3 个 CRITICAL：Global round-trip 破坏、resource 值静默改写、缺最小 schema 文档。
- **R5（组合性 / ct-expert）**：值语义总体良好（readonly record struct）；3 个可预测性陷阱：Budget 可变（严重）、闭包静默排除未来事件（中）、Parallel=Sequence=Union 但文档承诺 Compatible 检查未做（中/严重）。

## 2. 是否已造成用户困难 — 明确结论

**是。** 不是因为数学错，而是因为：

1. 认知表面过大且无导航（C6）；
2. 多处静默不一致（C1 scope 分裂、C2 资源/契约不对称、C4 默认无上限）；
3. 隐式不变量不直觉（size 默认 [1,1]、ω=⊤ 免 Leak、区间含 0 即守恒）；
4. 易混淆命名（ScopeId.Loop vs LoopCount ω、occupy vs Occupancy）；
5. JSON 契约 fail-soft 让 AI 反复写错回修（C4）。

## 3. 收敛修复优先级（Top N，按「降低用户困难」ROI）

### P0 — 修即消除静默错误（正确性级）

1. **C4-Global round-trip**（R4 CRITICAL）：`SerializeScope(Global)` 必须输出 `ParseScope` 可重建形态（如 `{"type":"global"}` 且 Parse 不强制 scene）。当前任何含 Global 的剧本 ToJson→Parse 必炸。
2. **C2 资源静默兜底→fail-fast**（R1/R2/R4 共识）：`commandBuffer` 缺值默认 `"gpu"`、`memory` 类型错→0、gpu/occ/signalBus 缺值 `""` 全部改为 `FormatException` 指明资源。静默改写比报错更危险。
3. **C5 Budget 不可变**（R5 F1）：把 `EffectScript.Budget { get; init; }` 改为构造参数（`EffectScript(ImmutableArray<EffectEvent>, Budget = Budget.None)`），删可写属性，使 `EffectScript` 成真值对象。

### P1 — 消除认知陷阱（API 表面）

4. **C1 残余 scope 分裂**：统一 `Derived.Net/Peak` 与 `EffectScript.Audit` 的 scope 口径（都按 e.Scope 或都按 claim scope），并文档化；claim.scope 若被忽略则不要 Parse 必填（或二者统一单源）。
2. **C3 收敛 Mode×Kind**：引入判别联合或构造器约束，使「read+release」「move+read」等跨桶矛盾在构造期不可表达；明确 `move` 在 net 上等同 `create`。
3. **C6 入口 + 命名**：提供 `EffectScript.Builder` 或「5 行写出合法剧本」样例；消除 ScopeId.Loop vs LoopCount ω 命名碰撞（重命名其一）。

### P2 — 文档 / 契约人体工学

7. **C4 最小 schema 文档**（R4 强烈建议）：1 屏 JSON Schema + 字段表（resource 各键的值类型、⊤=U+22C4、claim.scope 必带、loop/size 默认语义、budget 键前缀语法）。可消除约 70% 的 AI 写错面。
2. **R1 合并 Union/Join/Sequence/Parallel** 为单一 `Union`，消除「名异实同」诱导的错误心智模型。

## 4. 已闭环项（本审计前已修，记入）

- **OPEN-1**（scope gate3 对齐 e.Scope）：本轮 R3/R5 复核对齐已做，残余在 `Derived.Net/Peak`（C1-P1）。
- **OPEN-2**（ToJson 三桶 round-trip）：R5 确认无损。
- **双解析器收敛**（删 EffectScriptIo）：R1/R2 认可的 YAGNI 收敛。

## 5. 验证计划（进入 #12 开发修复时）

- 每修一项配回归测试（AGENTS.md 铁律）；
- 基线：`dotnet test` 当前 362 passed；
- P0 三项修完跑全测 + 新增 Global round-trip / 资源 fail-fast / Budget 不可变 测试；
- 不引入新依赖、不新增文件，最小 diff。

---
*收敛方法：R1–R5 五份独立报告交叉对照；不依赖任一单轮，取共同 HIGH 根因。*
