iter14.md 写入成功（独立审计 #14，本轮重跑）。

验证：header 含「独立审计 #14（hy3 单独进程，本轮重跑）」；N1-N6 六节覆盖 DO-7 与 Set<Claim> 单集合 ∪ 混合 kind 矛盾、peak 混加、工具缺职责、量纲单位、MA-007 权重函数缺失；Proof Obligation 账本 6 条（PO-I14-a~f）；I14- 缺口 7 条。

核心发现（数学性质+状态）：
- **PO-I14-a（高，open）**：DO-7「read/write/occupy 不可混算」与 §3.1.4 单 `Set<Claim>` + §3.2 无差别 ∪ 直接矛盾——组合层即静默混算，目标从未实现。
- **PO-I14-b（open）**：§3.3.2 `peak = max Σ c.size` 仅按 `mode≠release` 过滤、不按 kind 分离，三类 size 同数值相加，违反 DO-7。
- **PO-I14-c（open）**：grep 全文「量纲」仅 L19/L186，§6 三层工具（L329-398）均未执行量纲检查 ⇒ DO-7「编译期报错」无机制负责。
- **PO-I14-d/e（open）**：peak 量纲单位未定义；§12.2 512MB 预算与混算 peak 比较失真。
- **PO-I14-f（open, partial）**：MA-007「转换需显式权重函数」全文未定义，且 kind 字段未被任何运算符消费 ⇒ 「已解决」不实。

结构化验收报告已附于文件末尾（`acceptance-report` JSON，含 changedFiles/commandsRun/residualRisks/noStagedFiles）。铁律遵守：仅写 audit/iter14.md，未触碰其它 audit 文件。