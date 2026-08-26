# Iter12 审计 — §10 风险评估 R-1..R-12 与文档已证/未证状态的耦合（独立审计 #12，hy3 单独进程，本轮重跑）

- **审计视角**：风险管理一致性 / 缓解措施是否依赖未证机制（独立 pass #12，全新上下文）
- **范围**：§10（L604-620 R-1..R-12）；邻接 §1 DO-1..DO-11、§6（L2/L3 完备性，Iter07）、§8 默认规则（Iter10）、§9 Deviation（Iter11）、§7 映射（Iter08/09）
- **结论摘要**：12 条风险中，11 条缓解措施**直接依赖本审计已证为 open/asserted 的机制**（L2/L3 完备性、白名单覆盖率、Delta Sync 正确性、[AcceptDeviation]/[EffectOverride] 信任边界、Deviation 良定义）。即：文档一边声称「21 问题全收敛、0 阻塞」（§14），一边把高概率/高影响风险的缓解建立在未证机制上——风险登记与 §14 结论自相矛盾。R-3（误报率过高，概率高/影响高）的缓解含「[SuppressEffect] + 白名单机制」，但白名单覆盖率 <5%（Iter10 I10-04）、且误差方向（漏报 occupy/release，I10-01）恰与「泄漏检测」目标反向，故 R-3 缓解实际**可能加重** R 表中未列出的「漏报」风险。仅 R-7（跨平台构建）缓解为 CI 覆盖，与代数机制解耦，可 discharged。

---

## L1. 风险缓解对未证机制的依赖映射（核心）

| ID | 风险 | 概率/影响 | 缓解措施 | 缓解所依赖机制 | 该机制审计状态 | 结论 |
|----|------|----------|---------|---------------|---------------|------|
| R-1 | Godot C# 版本兼容 | 中/高 | 锁版本+CI 矩阵 | CI（与代数无关） | — | discharged（工程成立） |
| R-2 | SG 编译性能 | 中/中 | 增量生成/缓存 | SG 本身（Iter07 PO-I7-b 写集 soundness 未证，但性能无关正确性） | open（弱） | discharged（性能，非数学） |
| R-3 | Analyzer 误报率高 | **高/高** | 分级报警+[SuppressEffect]+白名单 | L3 Analyzer 完备性（Iter07 PO-I7-c/d）、白名单覆盖率（Iter10 I10-04） | **open/asserted** | **open（缓解建立在未证机制，且漏报方向反向）** |
| R-4 | 团队抗拒分层 | 高/高 | Audit-only 零改动 | Audit-only 静态分析正确性（Iter14） | open | open（弱） |
| R-5 | API 变动致映射失效 | 中/中 | 自动化测试+重扫 | 映射表（§7，Iter08/09 一致性缺口） | open | open（弱） |
| R-6 | struct 拷贝性能 | 低/中 | readonly record struct | L1（Iter07 F1） | discharged（语言保证） | discharged |
| R-7 | 跨平台构建 | 中/中 | CI 覆盖三平台 | CI（工程） | — | discharged |
| R-8 | EaD↔Scene 同步性能 | 中/高 | Delta Sync+批量 | Delta Sync 正确性（Iter06 PO-I6-e，核心 open） | **open（高）** | **open（高，缓解建立在未证机制）** |
| R-9 | ImmutableDictionary 瓶颈 | 中/高 | 评估自定义存储 | 性能（非数学） | — | discharged（工程） |
| R-10 | SG 代码可调试 | 中/中 | [GeneratedCode] | SG（工程） | — | discharged |
| R-11 | 培训成本 | 高/高 | Audit-only 零门槛 | 同 R-4 | open（弱） | open（弱） |
| R-12 | _Process 频率不确定 | 中/中 | 静态 60fps+采样校准 | 校准（Iter11 PO-I11-g 无上界） | open | open（弱） |

**核心论证**：R-3、R-8 为高影响项，其缓解分别依赖 L3 完备性（open）与 Delta Sync 正确性（open，高）。文档 R-表未区分「已证机制缓解」与「未证机制缓解」，与 §14「0 阻塞」矛盾。

---

## L2. R-3 缓解方向反向（关键新增发现）

**命题** R-3 缓解：「分级报警；允许 [SuppressEffect]；持续校准；白名单机制」。

**数学性质 / 证明状态**：
- **(PO-I12-a) 漏报方向未被 R-3 覆盖（open，高）**：文档 §8 默认规则（Iter10 I10-01）对未映射 API **漏报 occupy/release**——这是「漏报」（false negative），而 R-3 仅谈「误报率过高」（false positive）。文档风险评估**未登记「漏报泄漏/峰值」这一高影响风险**，且 R-3 的缓解（[SuppressEffect] 抑制报警）只会**增加**漏报。即缓解方向与真实风险方向部分反向。状态 = open（高，风险登记缺陷）。
- **(PO-I12-b) 白名单缓解建立在 <5% 覆盖率（open）**：R-3「白名单机制」依赖 §8 白名单，但覆盖率 <5%（Iter10 I10-04），白名单无法兜底绝大多数 API。状态 = open。

**文档行号**：R-3（L610）、§8（L511-518，Iter10）、Iter10 I10-01/I10-04。

---

## L3. R-8 缓解建立在未证 Delta Sync（open，高）

**命题** R-8「Entity-as-Data 与 Scene Tree 同步性能（中/高）」缓解：「增量同步（Delta Sync）；批量更新；避免每帧全量同步；Benchmark 验证」。

**数学性质 / 证明状态**：
- **(PO-I12-c) Delta Sync 正确性未证（open，高）**：增量同步的正确性（Iter06 PO-I6-e，Spawned/Modified/Destroyed 一致性判据缺失）是 R-8 缓解的根。若 Delta 三集合不相交性未证（Iter06 PO-I6-f），批量更新可能丢失变更或重复 ⇒ 同步性能优化的前提（同步正确）未立。状态 = open（高）。
- **(PO-I12-d) Benchmark 验证未附（open）**：「Benchmark 验证」无引用/无数据（同 Iter07 I7-06 TS-011），不可复现。状态 = open（弱）。

**文档行号**：R-8（L615）、§5.1.3（L306-315，Iter06）、Iter07 I7-06。

---

## L4. 可消解的 proof obligation（履行尝试）

- **P1（discharged）**：R-1/R-6/R-7/R-9/R-10 的缓解为纯工程措施（版本锁定、语言保证、CI、性能评估、生成属性），与代数机制解耦 ⇒ 风险缓解成立（不依赖未证数学）。证明：工程事实独立。
- **P2（discharged，条件）**：若 L3 Analyzer 完备性（Iter07 PO-I7-c/d）+ 白名单覆盖率（Iter10 I10-e/f）+ Delta Sync 正确性（Iter06 PO-I6-e）均成立，则 R-3/R-4/R-8/R-12 缓解成立。证明：机制链闭合 ⇒ 风险可控。前提均为 open ⇒ 条件，实际未消解。

---

## Proof Obligation 账本（Iter12）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I12-a | 漏报风险未登记/缓解反向 | open(高) | 补登漏报风险+限 Suppress | L610, L511-518 |
| PO-I12-b | 白名单覆盖率兜底不足 | open | 提覆盖率或降级默认 | L610, L513 |
| PO-I12-c | Delta Sync 正确性未证 | open(高) | 证一致性判据 | L615, L306-315 |
| PO-I12-d | R-8 Benchmark 未附 | open(弱) | 附数据 | L615, L? |
| PO-I12-e | R 表与 §14 矛盾 | open | 修订 §14 或 R 表 | L604-620, L774 |

## 本轮新发现未消解缺口（I12- 前缀，全局唯一）
- **I12-01（高）**：R-3 仅覆盖「误报」，未登记「漏报 occupy/release」高影响风险；其缓解（[SuppressEffect]）反而增加漏报，方向反向。
- **I12-02**：R-8 缓解依赖未证的 Delta Sync 正确性（Iter06 PO-I6-e，高），与「0 阻塞」矛盾。
- **I12-03**：12 条风险中 11 条缓解依赖本审计证为 open/asserted 的机制，风险登记与 §14「0 阻塞」自相矛盾。
- **I12-04**：R-3「白名单机制」建立在 <5% 覆盖率上（Iter10 I10-04），无法兜底。
- **I12-05**：R-12 校准缓解依赖 [AcceptDeviation] 无上界（Iter11 PO-I11-g），长期偏差窗口未被风险覆盖。

---

一句话摘要：§10 风险登记中 11/12 条缓解依赖本审计证为 open/asserted 的未证机制（L3 完备性、白名单、Delta Sync、校准），与 §14「0 阻塞」矛盾；R-3 仅谈误报、漏登漏报泄漏/峰值风险且其 [SuppressEffect] 缓解反向加重漏报（I12-01，高）；R-8 依赖未证 Delta Sync（I12-02，高）。
