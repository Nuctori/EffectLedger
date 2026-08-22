Wrote `audit/iter47.md` (独立审计 #47, MA-007 权重函数).

覆盖行号真实回指：
- §3.4 MA-007 L186（`已解决` +「转换需显式权重函数」——但全文 grep `weight`/`权重` 仅 L186 提及，**函数从未定义**）
- §3.3.2 peak L167（跨 kind 直接 `Σ c.size`，无 weight 介入 ⇒ DO-7 在峰值层被破坏）
- §1 DO-7 L19（read/write/occupy 不可混算）
- §12.2 预算 L651-655 / L703-725（AUDIT003 1280MB vs 512MB、BUDGET001 单 kind(memory) 累加 ⇒ weight 从未被调用）
- §3.1.1 kind L80-82

**高优先 open 发现（4 个）：**
- I47-01（高）：MA-007 标「已解决」却依赖一个全文未定义的 weight 函数 ⇒ 假已解决（交叉 Iter19 标称失真、Iter14 I14-06）
- I47-02（高）：peak 跨 kind 直接 Σsize 混算，无 weight/分桶 ⇒ DO-7 在峰值层被破坏（交叉 Iter14 I14-02 / Iter36 Q1）
- I47-03（中）：§12.2 预算比较仅单 kind 内累加，weight 从未被调用，跨 kind 统一预算口径缺位
- I47-04（中）：文档从未给 weight 查表/定义域/值域/调用点

给出 `weight: Kind×Kind→ℝ∪{⊥}` 草案（同 kind=1、跨 kind=⊥）使 DO-7 可落地，但需 PDR 侧真定义 weight + 采纳 Iter36 分桶 + 补跨 kind 统一预算口径方能使 MA-007 由假已解决转真 resolved。