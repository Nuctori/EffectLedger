# Iter41 审计 — §10 缓解措施 R-1..R-12 的「自身正确性」：每条缓解依赖的机制是否已证（独立审计 #41，hy3 单独进程）

- **审计视角**：§10 缓解措施的自证性——缓解所依赖的机制是 discharged（已证）/open（未证）/asserted（声称），若依赖 open 公设则该缓解建立在未证机制上、自身失效（独立 pass #41，全新上下文，未读任何其它 audit 文件）。
- **范围**：§10 R-1..R-12（L600-630）；邻接依赖机制：§9.1 Deviation（L547-561，Iter33 除零）、§8 推导（L511-518，Iter10 默认规则漏报）、§6 L2/L3（L355-398，Iter38/39 完备性 open）、§5 Delta Sync（L306-315，Iter40 三集合互斥/终止 open）、§5.1 Shell 同态（L283-315，Iter06 SH-001..005）、§4 EA-005 System 并行（L263-298）、§3 代数（Compatible/量纲 open，Iter14/16/25）。交叉引用 iter01-40 编号，仅作引用、未读其文件。
- **结论摘要**：逐条审查 R-1..R-12 发现——6 条（R-2/R-3/R-8/R-9/R-11/R-12）的缓解核心机制在文档内**未证（open/asserted）**，其中 R-3、R-8、R-12 直接依赖已被前期审计证明为 open 的代数/校准/同步机制，故这三条缓解**建立在未证机制上、自身无效或弱化**；其余 R-1/R-4/R-5/R-6/R-7/R-10 为工程/流程性缓解（CI/培训/属性标注），非形式化可证，按"外部断言"计、无法在文档内判定(discharged)。文档 §10/§14 声称这些风险"已收敛"对该子类混淆了"声称收敛"与"机制已证"。

---

## V1. 命题：R-3（Analyzer 误报率过高）依赖的校准机制 = open

**命题**（§10 L603）：R-3 缓解 =「分级报警；允许 [SuppressEffect]；持续校准；白名单机制」。
**依赖机制**：「持续校准」依赖 §9.1 `CalculateDeviation`（L547-561）。但该公式 `range=max-min` 作分母，当 size 单点退化 range=0 ⇒ 除零/NaN（Iter33 I33-01，高，open）；且 `deviation>0.2f` 对 NaN 恒假（永不报警）、对 ∞ 恒真（每帧报警）（Iter33 I33-02，高，open）。即校准闭环本身失效。
**数学性质 / 证明状态**：
- **(PO-I41-a) R-3 校准机制 open ⇒ 误报抑制部分失效（open，高）**：[SuppressEffect]/白名单属标注层（依赖人工），而"持续校准"的自动阈值判定建立在 Deviation 公式上，该公式 open（Iter33）→ 自动误报抑制机制未证。状态 = open（高，交叉 Iter33 I33-01/I33-02、Iter11 I11-01）。
- **缓解是否有效**：部分无效（标注层可用，自动校准层建立在未证公式上）。
- 文档行号：§10（L603）、§9.1（L547-561）、Iter33。

---

## V2. 命题：R-8（EaD↔Scene 同步性能）依赖的 Delta Sync 正确性 = open

**命题**（§10 L610）：R-8 缓解 =「增量同步（Delta Sync）；批量更新；避免每帧全量同步；Benchmark 验证」。
**依赖机制**：Delta Sync（§5.1.3 L306-315）产出 Spawned/Modified/Destroyed 三集合，但其**互斥性/优先级未定义**（Iter40 I40-01，高，open）、**同步后不变式 `SceneTree≡project(EaD)` 与终止性未证**（Iter40 I40-02，高，open）、**反向 function 性质未证**（Iter40 I40-03，高，open，交叉 Iter06 SH-001..005）。
**数学性质 / 证明状态**：
- **(PO-I41-b) R-8 增量同步正确性 open ⇒ 性能缓解建立在未证机制（open，高）**：若三集合不互斥/同步无终止不变式，则"增量同步避免全量"的**正确性**未证——即使性能上省了全量，结果可能重复/遗漏节点。Benchmark（性能）≠ 正确性证明。状态 = open（高，交叉 Iter40 I40-01/02/03、Iter06 SH-001）。
- **缓解是否有效**：建立在未证机制上（一致性判据缺失，Iter40 I40-05 已指 R-8 缓解本身依赖未证机制）。
- 文档行号：§10（L610）、§5.1.3（L306-315）、Iter40、Iter06。

---

## V3. 命题：R-12（_Process 频率不确定）依赖的静态 60fps 假设 + 运行时校准 = open

**命题**（§10 L630）：R-12 缓解 =「静态分析假设 60fps；运行时采样校准实际帧率；[TargetFrameRate] 属性」。
**依赖机制**：ED-006（§8 L531「_Process 调用频率对效应累加…已收敛…静态保守假设 60fps，运行时采样校准」）+ §9 校准（open，Iter33）。即 R-12 的"运行时采样校准"重走 Deviation 公式（open）。
**数学性质 / 证明状态**：
- **(PO-I41-c) R-12 校准依赖 open Deviation ⇒ 频率修正未证（open，中）**：静态 60fps 是保守假设（可声明但不保证贴合真实帧率），而"运行时校准"依赖 §9 Deviation（open，Iter33）→ 频率偏差的自动修正机制未证。状态 = open（中，交叉 Iter33、ED-006）。
- **缓解是否有效**：弱化（静态假设可用但保守、自动校准层 open）。
- 文档行号：§10（L630）、§8 ED-006（L531）、§9.1（L547-561）、Iter33。

---

## V4. 命题：R-2（Source Generator 编译性能下降）依赖的增量生成/缓存 = asserted

**命题**（§10 L602）：R-2 缓解 =「增量生成；缓存语法树；限制扫描范围」。
**依赖机制**：C# Source Generator 的增量生成（IIncrementalGenerator）是 Roslyn 框架特性（外部事实），文档未证"本项目的 Generator 确实增量"——仅声称。
**数学性质 / 证明状态**：
- **(PO-I41-d) R-2 缓解为外部框架特性声称、未在本 PDR 内证（asserted，中）**：机制（增量生成）是 Roslyn 已有能力，但"项目 Generator 已采用且对 100 API + 字段白名单扫描仍满足性能预算"未在文档内给出 benchmark/证明。状态 = asserted（非 discharged，亦非 open 公设——属外部事实引用，但本项目落地未证）。
- **缓解是否有效**：依赖外部框架，文档未证本项目落地 ⇒ 弱（无法在文档内判 discharged）。
- 文档行号：§10（L602）、§6.3（L355-372）。

---

## V5. 命题：R-9（ImmutableDictionary 性能瓶颈）依赖的 Benchmark 评估 = 流程性 asserted

**命题**（§10 L614）：R-9 缓解 =「评估切换到自定义 Archetype 分组存储；Benchmark 对比」。
**依赖机制**：属"未来评估"动作，无当前机制可证。
**数学性质 / 证明状态**：
- **(PO-I41-e) R-9 为待办评估、无已证机制（asserted/流程，中）**：缓解是"将评估"，非"已解决"。状态 = asserted（流程），文档内无可证对象。
- **缓解是否有效**：流程性，非代数正确性缓解，不适用 discharged 判定（标 asserted）。
- 文档行号：§10（L614）。

---

## V6. 命题：R-11（培训成本）依赖的 Audit-only 零门槛 = 依赖系统整体可用性（间接 open）

**命题**（§10 L617）：R-11 缓解 =「Audit-only 模式零门槛；渐进式培训；文档示例」。
**依赖机制**：Audit-only「零代码改动即可用」依赖审计系统本身能跑出有意义结果。但 [Budget] opt-in 致未标记组件盲区（Iter38 I38-02，高，open）、默认规则漏报 occupy/release（Iter10 I10-01，open）、L3 与 §7 映射语义脱节（Iter39 I39-04，高，open）→ 即"零门槛入口"产出的审计结论本身可能大面积漏报，培训价值建立在漏报系统上。
**数学性质 / 证明状态**：
- **(PO-I41-f) R-11 零门槛依赖系统可用性，而系统漏报 open ⇒ 缓解建立于弱基（open，中）**：培训/文档是流程，但"Audit-only 即有用"隐含系统已能捕获主要 effect，而捕获机制 open（Iter10/38/39）。状态 = open（中，交叉 Iter10/Iter38/Iter39）。
- **缓解是否有效**：流程层有效（确能零改动接入），但接入后结论可靠性依赖 open 捕获机制 ⇒ 弱化。
- 文档行号：§10（L617）、§11（L643-660 Audit-only）、Iter10/Iter38/Iter39。

---

## V7. 命题：R-1/R-5/R-7（版本/API/跨平台）为 CI 流程性缓解 = 外部断言

**命题**（§10 L601/R-1、L605/R-5、L607/R-7）：
- R-1：锁定 Godot 4.2+/.NET 8+ + CI 矩阵多版本测试。
- R-5：自动化测试覆盖核心 API + 版本升级重新扫描 + 社区贡献。
- R-7：CI 覆盖 Linux/Windows/macOS + Docker 统一环境。
**依赖机制**：均为外部 CI/流程动作，非文档内可证代数命题。
**数学性质 / 证明状态**：
- **(PO-I41-g) R-1/R-5/R-7 为 CI 流程、非文档内可证（asserted/不适用，低）**：无 open 公设依赖、无形式化命题，属工程保障。状态 = asserted（流程），不计入 open 公设缺口。
- **缓解是否有效**：流程有效，不适用 discharged 判定。
- 文档行号：§10（L601/L605/L607）。

---

## V8. 命题：R-4/R-6/R-10（架构抗拒/struct 拷贝/SG 可调试）为设计/属性缓解 = asserted

**命题**（§10 L604/R-4、L606/R-6、L612/R-10）：
- R-4：渐进式采用 + Audit-only 零改动 + 迁移工具 + 培训（流程）。
- R-6：readonly record struct + 状态对象 <128 bytes + 大 Component 引用包装（C# 编译器优化，外部事实，仅声称）。
- R-10：生成代码 [GeneratedCode]/[DebuggerNonUserCode] + Source Link（编译器/IDE 特性，外部事实，仅声称）。
**依赖机制**：R-4 流程；R-6/R-10 为语言/工具特性声称，文档未证"本项目确遵循 <128 bytes 约束"或"Source Link 已配置"。
**数学性质 / 证明状态**：
- **(PO-I41-h) R-4 流程、R-6/R-10 外部特性声称未证落地（asserted，低）**：非代数命题。状态 = asserted（流程/外部）。
- **缓解是否有效**：流程/外部特性有效，文档内未证落地但无 open 公设依赖。
- 文档行号：§10（L604/L606/L612）。

---

## V9. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 §9 Deviation 公式补 range 下界 + IsFinite 分支（Iter33 P1/P2），则 R-3「持续校准」的自动误报抑制机制良定义。证明：公式有限 ⇒ 阈值可判。前提 Iter33 PO-I33-a/b 未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 Delta Sync 补三集合互斥不变式 + 每帧后 `SceneTree≡project(EaD)` 终止不变式（Iter40 P1/P2），则 R-8「增量同步正确性」可证。证明：不变量 ⇒ 一致性。前提 Iter40 PO-I40-a/b 未立 ⇒ 条件。
- **P3（discharged，条件）**：若 §9 Deviation 校准（open）被修，则 R-12 频率自动修正机制良定义。证明：同 P1。前提 Iter33 未立 ⇒ 条件。
- **P4（discharged）**：R-1/R-4/R-5/R-6/R-7/R-9/R-10 属流程/外部特性，其"有效性"无法在文档内形式化判据（属工程保障），按 asserted 计，不构成 open 公设缺口。

---

## Proof Obligation 账本（Iter41）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I41-a | R-3 校准依赖 open Deviation ⇒ 误报抑制部分失效 | open(高) | 修 §9(Iter33) | L603, L547-561, I33 |
| PO-I41-b | R-8 增量同步正确性 open ⇒ 性能缓解建未证机制 | open(高) | 补 Delta 不变量(Iter40) | L610, L306-315, I40/I06 |
| PO-I41-c | R-12 校准依赖 open Deviation ⇒ 频率修正未证 | open(中) | 修 §9(Iter33) | L630, L531, I33 |
| PO-I41-d | R-2 增量生成为本项目落地未证(asserted) | asserted(中) | 补 SG 性能 benchmark | L602, L355-372 |
| PO-I41-e | R-9 为待评估、无已证机制(asserted) | asserted(中) | 执行评估 | L614 |
| PO-I41-f | R-11 零门槛依赖系统可用性(漏报 open) | open(中) | 补捕获机制(Iter10/38/39) | L617, I10/I38/I39 |
| PO-I41-g | R-1/R-5/R-7 CI 流程(asserted/不适用) | asserted(低) | 无(流程) | L601/L605/L607 |
| PO-I41-h | R-4 流程/R-6/R-10 外部特性声称(asserted) | asserted(低) | 无(流程/外部) | L604/L606/L612 |

## 本轮新发现未消解缺口（I41- 前缀，全局唯一）
- **I41-01（高）**：R-3「持续校准」自动误报抑制依赖 §9 Deviation 公式，该公式 range=0 除零/NaN 致校准闭环失效（Iter33）→ R-3 自动层建立在未证机制、部分失效。
- **I41-02（高）**：R-8「增量同步避免全量」依赖 Delta Sync 正确性，而三集合互斥/终止不变式/反向 function 全 open（Iter40/I06）→ R-8 一致性判据缺失、建立在未证机制。
- **I41-03（中）**：R-12「运行时采样校准」重走 open Deviation 公式（Iter33）→ 频率自动修正未证。
- **I41-04（中）**：R-11「Audit-only 零门槛有用」隐含系统已捕获主要 effect，但捕获机制（[Budget] opt-in 盲区/默认规则漏报/L3 脱节，Iter10/38/39）open → 零门槛接入结论可靠性弱。
- **I41-05（低）**：§10/§14 将 R-1..R-12 标「已收敛」混淆了「流程性声称收敛」与「依赖机制已证」——其中 R-3/R-8/R-12 依赖 open 公设、R-2/R-9/R-11 为未证落地/间接 open，严格说仅 R-1/R-4/R-5/R-6/R-7/R-10 属流程性断言（非可证对象）。文档「已收敛」声明对可证子类不实。

---

一句话摘要：逐条审查 R-1..R-12 自证性——R-3（校准依赖 open Deviation，I41-01 高）、R-8（增量同步正确性 open，I41-02 高）、R-12（校准重走 open Deviation，I41-03 中）三条缓解直接建立在已被证 open 的代数/校准/同步机制上、自身失效或弱化；R-11（零门槛依赖 open 捕获机制，I41-04 中）间接弱；R-2/R-9 为未证落地/待评估（asserted）；R-1/R-4/R-5/R-6/R-7/R-10 为流程/外部特性（asserted，非可证对象）。§10/§14「已收敛」声明对可证子类不实，需区分「流程声称」与「机制已证」。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter41.md，未读/改其它 audit 文件，聚焦 §10 缓解措施自证性，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含标题(独立审计 #41, hy3 单独进程)、V1-V9 各节(命题/依赖机制/状态/论证/真实行号)、Proof Obligation 账本(PO-I41-a..h 八项)、I41- 缺口列表(5 条)；行号均基于本轮真实 read：§10(L600-630)/§9.1(L547-561)/§8 ED-006(L531)/§5.1.3(L306-315)/§6.3(L355-372)/§5.1.1(L283-315)/§4 EA(L263-298)，并经 read 确认"}
  ],
  "changedFiles": ["audit/iter41.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 595, 50)", "result": "passed", "summary": "读取 §10 R-1..R-12 与 §9 RT-001..006 真实文本"},
    {"command": "read PDR (offset 283, 40) + (offset 329, 70) + (offset 537, 55)", "result": "passed", "summary": "读取 §5.1 Shell/Delta、§6 L1/L2/L3、§9.1 Deviation 确认各缓解依赖机制真实文本"},
    {"command": "read PDR (offset 511, 20) + (offset 263, 35)", "result": "passed", "summary": "读取 §8 推导 ED-006、§4 EA-005 确认 R-12/R-8 依赖机制"},
    {"command": "write D:/Godot/Cosmos/audit/iter41.md", "result": "passed", "summary": "覆盖写入独立审计 #41"}
  ],
  "validationOutput": ["标题含「独立审计 #41，hy3 单独进程」", "共 V1-V9 九节 + Proof Obligation 账本(PO-I41-a..h 八项) + 5 条 I41- 缺口", "逐条覆盖 R-1..R-12，区分 discharged/open/asserted"],
  "residualRisks": ["R-3/R-8/R-12 依赖的 open 机制判定基于 Iter33/Iter40/Iter06 编号引用，本轮未重读那些审计文件（按规则禁止读）", "CI 流程性缓解的实际有效性非形式化可证，按 asserted 计可能存在低估"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter41.md，独立审计 §10 R-1..R-12 缓解措施自证性（依赖机制 discharged/open/asserted 判定）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 R-3/R-8/R-12 三条缓解建立在 open 机制上、§10/§14「已收敛」声明对可证子类不实，需 PDR 侧区分流程声称与机制已证"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件；仅以 iterNN 编号交叉引用前期审计"
}
