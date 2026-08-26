# Hickey-X R7: JSON 契约 round-trip 与 fail-soft 边界 —— 可执行实验验证

> 视角：用真实运行代替阅读。全部断言来自 rt-probe 控制台项目（net10.0，ProjectReference 引 L1 主项目）的实际输出。
> 说明：hickey-auditor 完成探针设计（9.3KB Program.cs，含 17 项断言）后超时；主会话补齐 csproj、修复路径/using 后本地执行取得完整结果。**probe 目录已按约删除**，仓库无残留。

---

## 一、实验矩阵（PASS = 与契约应有行为一致；FAIL = 发现契约违背）

| # | 实验 | 结果 | 实际观察 |
| --- | --- | --- | --- |
| a1 | 最小合法剧本 ToJson∘Parse 幂等 | **PASS** | `j2 == j3` 成立；事件语义相等；budget=5 保真 |
| a3 | 事件数 round-trip | **PASS** | 1→1 |
| b | **SerializeBudget ⊤→0 翻转（R2-N1 复核）** | **PASS（翻转证实）** | 序列化输出 `"gpu:bufTop": 0`；重解析得有限值 0；且后果链实测：重解析后 `Audit Passed=False, violations=1 [Leak@5]`——预算 ⊤ 变 0 直接产生虚假 Leak |
| c×4 | ScopeId.Loop/Shell/Conditional/Async → ToJson | **PASS×4** | 四者均抛 FormatException（消息含具体 scope 值）——「契约面≠API 面」实测确认 |
| d1 | 根级未知键 `'budgat'` | **FAIL（静默）** | Parse 成功、Caps.Count=0、零诊断 |
| d2 | 大写 `'Budget'` 键 | **FAIL（静默）** | 同上——大小写敏感 + 未知键忽略的组合拳 |
| d3 | `"budget": []` 类型错 | **FAIL（静默）** | 静默跳过 ⇒ gate(2) 整体失效 |
| e | `[⊤,⊤]` lifetime 事件 | **FAIL（静默）** | `Audit Passed=True, violations=0`——携带泄漏的事件从所有 gate 消失；At(0) 也看不见它（OccupyClaims=0） |
| f | **At vs Audit 并发计数分歧（R5-C2 复核）** | **PASS（分歧证实）** | 两事件相同 footprint 重叠窗口：`At(5).OccupyClaims=1`（set 去重）vs Audit 报 CompatibleConflict×2 + PeakExceeded(cap=1)×2（multiset 计数）——同一事实两套答案，实测坐实 |
| g | lifetime 负数端点 | **FAIL（异常类型不符）** | 抛的是裸 `FormatException: "One of the identified items was in an invalid format."`——.NET 原生消息，无字段名无期望形态（R6-E 族确认） |
| h1 | budget 负数 `-1` | **PASS** | 契约层 FormatException ✓ |
| h2 | budget 超 ulong | **PASS** | 契约层 FormatException ✓ |
| i | 序列化形态漂移 | **FAIL（部分）** | 输入缺 loop/type 键 → 输出补全显式 `"loop"`+scope 形态变化；hasType=False 说明 scene 缺省形态不写 type 字段——幂等性靠「规范化后再比」而非字面相等 |

## 二、结论

1. **R2-N1 / R5-C2 两个 HIGH 经可执行实验二次坐实**，不再是静态推断：
   - N1 的伤害链完整实测：C# 合法剧本（⊤ 预算）→ ToJson → Parse → **Audit 产生虚假 Leak**。修复优先级应排全系列第一。
   - C2 的分歧数值精确：同一时刻 At 说 1 个副本、Audit 按 2 个计冲突与峰值。
2. **fail-soft 三连（d1/d2/d3）全部复现**：拼写错误、大小写、类型错三条路都通向「预算门静默失效」。
3. **[⊤,⊤] 事件静默消失（e）实测**：Passed=true 且零违例——审计对不可判定窗口的选择是「假装不存在」。
4. **正面记录**：最小剧本 round-trip 幂等成立（a 系列）；budget 负数/超界的异常处理是契约层质量合格的两处（h 系列）；四个不可序列化 scope 的报错消息带具体值（c 系列）。

## 三、TOP-3

1. **N1 翻转的完整伤害链实测**（⊤ 预算 → 虚假 Leak）：从「类型层瑕疵」升格为「用户可见的错误审计结论」，P0 修。
2. **f 分歧实测**：At/Audit 双口径从代码推理变为 17 行 probe 可复现的事实，修复方案讨论有了共同地基。
3. **d/e/g 三族静默路径在真实运行下无一例外全部复现**：静态分析发现的 fail-soft 面没有任何一处在运行时被意外兜住——问题清单的可信度经实验背书。

## 文件清单

- 探针产物（已删除）：audit/hickey-x/rt-probe/{rt-probe.csproj, Program.cs}（Program.cs 全文存档于 round-07-source.md）
- 主会话修改：仅 rt-probe.csproj（新建，已删）、Program.cs 补 using（基于子代理稿，已删）
- 引用项目：src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj（只读构建）
- 运行环境：dotnet 10.0.103，Release 配置
