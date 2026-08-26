# Iter43 审计 — 验证/测试层覆盖域与有效性：测试验证的是代码还是规范？核心公设 open 致「只能验实现、不能验代数」（独立审计 #43，hy3 单独进程）

- **审计视角**：§11/§12 验证与测试条目的「覆盖域与有效性」——这些测试能否证立/证伪 PDR 核心不变量（∪ 幂等、net 守恒、Peak 收敛、Compatible 一致性）？因核心公设自身 open，测试是否退化为「只验已实现、规范悬空」的伪测试？（独立 pass #43，全新上下文）
- **范围**：§11 工作量估算（L623-638，无测试条目）、§12.2 Audit-only 报警 AUDIT001/002/003（L720-736，`warning`/`error` 为工具行为非不变量）、§9.1 开发模式验证/EffectValidator（L537-551，Deviation 采样）、§9.3 校准（L577-589）、§9.4 RT 表（L597-606，「已收敛」无测试代码）、MA-009（L188「单元测试覆盖」）、§3.1-3.3 代数核心不变量（L100-171）、DO-4（L16「80% 测试 dotnet test」）、§14（L623「21 问题收敛 0 阻塞」）；邻接 Iter07（L2/L3 完备性全 open）、Iter19（量化：组合子/对象层律全缺）、Iter32（Claim=未定义致 ∪ 幂等悬空）、Iter33（Deviation 除零/NaN）、Iter16/Iter22（Compatible 偏函数+非对称）、Iter18（ω 载体未定义致 Peak 发散）、Iter34（ScopeId⊆ 未定义致 Peak/peak 过滤悬空）、Iter37（net 无 scope）
- **结论摘要**：PDR **不存在**针对核心代数不变量（∪ 幂等、net 守恒、Peak 收敛、Compatible 一致性）的任何单元测试/形式化测试条目。唯一可称为「代数测试」的声明是 MA-009「枚举 16 种组合，单元测试覆盖」，但其被测对象 `Compatible` 是偏函数+非对称（Iter16/Iter22），枚举本身未形式化、与 DO-9 冲突判定悬空；§9.1 的 `EffectValidator` 测的是 Deviation 采样实现，而 Deviation 公式 range=0 致 NaN/∞（Iter33）尚未收口；§12.2 的 AUDIT001/002/003 是 Analyzer 工具告警（泄漏/预算），属「代码行为检查」非「不变量证立」。结构性结论：**因 ScopeId⊆/Compatible/ω/Claim= 四类公设 open，任何对「已实现解释」的测试只能验证该解释的内部一致性，无法证立规范正确性——规范层无 reference 实现可对照，测试是循环（验证实现↔实现，非实现↔规范）**。PDR §14「0 阻塞」与测试缺位直接矛盾（open，高）。

---

## V1. 命题：PDR 无任何核心不变量测试条目（§11/§12 实为估算与发布策略）

**命题**（§11 L623-638）：标题「工作量估算」，罗列 L1/L2/L3/Tools/DTO/API 映射/运行时验证/测试文档的周数，**无任何一行列出「代数不变量测试」或「不变量证立」条目**。
**命题**（§12 L639-736）：标题「社区发布策略」，§12.1 四阶段、§12.2 Audit-only 代码与 AUDIT001/002/003 告警、§12.3 渠道——均非测试套件。
**命题**（DO-4 L16）：「80% 测试在 dotnet test 完成，不启动 Godot」——指测试**运行环境**（不启 Godot），不指测试**对象**（测什么不变量未列）。

后果：核心不变量 ∪ 幂等（Iter32 PO-I32-a）、net 守恒（Iter37）、Peak 收敛（Iter18 I18-02/Iter34）、Compatible 一致性（MA-009）**全文无对应 test target 声明** ⇒ 文档声称的「测试」与代数正确性脱钩。

**数学性质 / 证明状态**：
- **(PO-I43-a) 无核心不变量测试条目（open，高）**：PDR 未将任一核心代数不变量列为测试对象 ⇒ 「测试覆盖代数」无从谈起。状态 = open（高）。
- 文档行号：§11（L623-638）、§12（L639-736）、DO-4（L16）。

---

## V2. 命题：MA-009「单元测试覆盖」测的是已选实现的 Compatible，非规范正确性

**命题**（MA-009 L188）：「Compatible 的完备性 … 已收敛 … 枚举定义 16 种组合，单元测试覆盖，不追求形式化证明」。
**命题**（§3.2.3 L131-138）：`Compatible(m₁,m₂)` 仅列 4 条兼容规则（use∧use / create∧use / release∧use / move∧use），注释「不兼容：create+create, move+move, use+write(同资源)」。
**命题**（Iter16/Iter22）：Compatible 是**偏函数**（use+write 未定义、mode 超出 4 枚举未定义）+ **非对称**（create∧use 兼容但 use∧create 顺序未保证 ⇒ `||` 交换律依赖 mode 对称，Iter22 PO-I22-b）——即枚举**未穷尽 11 种 (use/create/release/move/write?) 组合的对称闭包**，实际远不止 16 种（若含 write 种类则更多）。

后果：MA-009 的「16 种组合单元测试」只能验证「该实现按这 16 条枚举返回 true/false」，**不能验证枚举本身是否=规范正确 Compatible**（因为规范 Compatible 的真值表未形式化、偏函数/非对称未闭合）。即测试是 **伪规范测试**：验证实现↔实现选定枚举，非实现↔规范（规范不存在合法全表）。且 Iter16 已证 create+release 良性对（如 create 后 release 同资源）被 Compatible 误判冲突 ⇒ 测试还会把「误判」固化进实现。

**数学性质 / 证明状态**：
- **(PO-I43-b) MA-009 单元测试是伪规范测试（open，高）**：被测 Compatible 规范未闭合（偏函数+非对称），测试仅验实现选定枚举 ⇒ 无法证立兼容性正确性，且可能固化误判。状态 = open（高，交叉 Iter16/Iter22/Iter25）。
- 交叉：Iter16（I16-01 偏函数）、Iter22（I22-02 非对称破 `||` 交换）、Iter25（C* 全对称函数草案未采纳）。
- 文档行号：MA-009（L188）、§3.2.3（L131-138）、Iter16/Iter22。

---

## V3. 命题：§9.1 EffectValidator 测 Deviation 实现，公式本身 open（Iter33）

**命题**（§9.1 L537-551）：`EffectValidator.Validate` 每 60 帧采样 `actual`，调 `CalculateDeviation(expected, actual)`，若 `>0.2f` 报警。其测试对象是「采样+偏差计算」**实现流程**。
**命题**（Iter33 PO-I33-a/b/c/d）：Deviation 公式 `range=max-min` 作分母，size 单点退化 range=0 ⇒ 除零/NaN；NaN 使 `>0.2f` 恒假（永不报警），∞ 使恒真（每帧报警）；区间载体无出处（§3.1.1 size 单值 vs §3.2.4 [min,max]）；Σ 对齐谓词缺失。

后果：即便为 `EffectValidator` 写单元测试，也只能验证「在给定有限 range 输入下返回有限偏差」——而文档**未定义** range=0 / NaN / ∞ 时 `CalculateDeviation` 的契约（L537 仅注释公式、无契约）。测试若用「安全输入」通过，仍不证立真实运行期（含单点 size 的 Claim）的正确性 ⇒ 测试覆盖的是「理想路径实现」，规范边界（Iter33）悬空。且 §9.3 校准循环（L577-589）依赖该偏差 ⇒ 校准闭环在 NaN 情形失效，无测试能覆盖（因失效由未定义规范引起）。

**数学性质 / 证明状态**：
- **(PO-I43-c) Deviation 测试仅覆盖理想路径、边界未定义（open，高）**：`CalculateDeviation` 契约（range 下界、IsFinite 分支）未定义 ⇒ 测试无法证立边界行为，且 PDR 称 RT-002「已收敛」（L597）不实。状态 = open（高，交叉 Iter33）。
- 文档行号：§9.1（L537-551）、§9.3（L577-589）、§9.4 RT-002（L597）、Iter33（PO-I33-a/b/c/d）。

---

## V4. 命题：§12.2 AUDIT001/002/003 是工具告警，非不变量证立

**命题**（§12.2 L720-736）：AUDIT001（每帧 GetNode 告警）、AUDIT002（Instantiate 无 QueueFree → 泄漏 error）、AUDIT003（无 [Budget] 保守 64MB 估算、预算累加）。这些是 **Analyzer 对开发者代码的 lint 行为**，触发条件是「代码模式识别」（每帧调用 / 无 QueueFree 路径 / 无 Budget 标注）。
**命题**（Iter27 I27-01 / Iter37 I37-04）：AUDIT002 的「泄漏」判断基于 `Instantiate→occupy(create)`、`QueueFree→mode=move`（Iter27）——因 QueueFree mode=move 致 net 不识别释放，AUDIT002 的「泄漏」语义本身依赖未修正的 mode 映射；且 §3.3.1 `net(S)` 无 scope（Iter15 I15-06/Iter37），AUDIT003 的「场景累加 1280MB」基于全局混算伪值（含 DO-7 量纲违规，Iter14）。

后果：AUDIT002/003 验证的是「Analyzer 按既定（可能错的）映射产生告警」，属**工具行为检查**，不证立「net 守恒」「Peak 收敛」「DO-7 量纲隔离」等规范。且其底层映射（QueueFree=move、size 默认 64MB 保守、全局累加）本身是 open/误判项 ⇒ 测试把误判固化。

**数学性质 / 证明状态**：
- **(PO-I43-d) AUDIT 告警是工具检查非规范证立（open，高）**：AUDIT002/003 触发依赖 mode 映射与 net(scope) 两项 open 公设，测试只验工具行为 ⇒ 伪规范测试。状态 = open（高，交叉 Iter27/Iter37/Iter14/Iter38）。
- 文档行号：§12.2（L720-736）、§3.3.1（L163-165）、Iter27（I27-01）、Iter37（I37-04）、Iter14（I14-02/05）。

---

## V5. 命题：核心公设 open ⇒ 无 reference 实现可对照 ⇒ 测试循环（实现↔实现）

**命题**（Iter32 PO-I32-a）：`Claim=` 相等律未定义 ⇒ ∪ 幂等性悬空（HashSet 去重依赖相等）。
**命题**（Iter34 PO-I34-a）：ScopeId⊆ 偏序未定义 ⇒ Peak/peak 的 `c.scope⊆t` 过滤悬空。
**命题**（Iter18 I18-02）：ω 载体未定义 ⇒ Peak 计数发散。
**命题**（Iter16/Iter22）：Compatible 偏函数+非对称。

结构性结论：PDR 无「规范 reference 实现」（因规范本身含 4 类 open 公设）。任何工程实现（§6 L2/L3、§9 EffectValidator、§12 Analyzer）必须**先选定对 open 公设的解释**（如 ∪ 去重按引用相等、Peak 按 Global 单窗口、Compatible 按 16 枚举）。测试该实现时：
- 测「实现↔自选解释」：恒通过（内部一致），但**不证立规范正确性**（规范无合法全表）。
- 测「实现↔规范」：不可行（规范无 reference）。
⇒ 测试是 **循环验证**（验证实现与其自身选定的解释一致），无法发现「解释本身违反未写明的代数律」（如 ∪ 应幂等但引用相等下 `S∪S` 仍含两引用 ⇒ 实际不幂等，Iter32）。即「测试只能验证实现、不能验证代数正确性」成立（open，高）。

**数学性质 / 证明状态**：
- **(PO-I43-e) 公设 open 致测试循环、无法证立规范（open，高）**：无 reference ⇒ 测试退化为实现↔实现，核心不变量正确性不可测。状态 = open（高，交叉 Iter32/Iter34/Iter18/Iter16）。
- 文档行号：§3.1-3.3（L100-171）、Iter32/Iter34/Iter18/Iter16。

---

## V6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 PDR 把 4 类公设（Claim=、⊆*、ω∈ℕ∪{⊤}、Compatible 全对称表，Iter32/34/35/25）补为**形式化 reference 定义**，并写「不变量测试」针对 ∪ 幂等（`S∪S=S`）、net 守恒（配对 release）、Peak 收敛（有限 ω 上界）、Compatible 对称闭包，则测试可从伪测试升级为真·规范测试。证明：有 reference 可对照。前提 PO-I43-e（公设未立）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若 MA-009 的「16 组合」改为「全对称 Compatible 表（Iter25 C*）+ 良性对（create+release, Iter16）」并写测试，则 Compatible 测试证立兼容性。证明：表闭合。前提 Iter25 未立 ⇒ 条件。
- **P3（discharged）**：在「测试仅作回归守护（防实现回退）、不声称证立规范」弱定位下，现有测试有效。证明：定位降级。但 PDR §14「21 问题收敛 0 阻塞」声称规范已证 ⇒ 定位与声明冲突（V1/§14）。

---

## Proof Obligation 账本（Iter43）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I43-a | 无核心不变量测试条目 | open(高) | §11/§12 补不变量测试 | L623-638, L639-736, L16 |
| PO-I43-b | MA-009 单元测试是伪规范测试 | open(高) | Compatible 形式化全表(Iter25) | L188, L131-138, Iter16/22 |
| PO-I43-c | Deviation 测试仅理想路径、边界未定义 | open(高) | range 下界+IsFinite(Iter33) | L537-551, L597, Iter33 |
| PO-I43-d | AUDIT 告警是工具检查非规范证立 | open(高) | 修正 mode 映射+net(scope) | L720-736, I27-01, I37-04 |
| PO-I43-e | 公设 open 致测试循环、无法证立规范 | open(高) | 补 4 类 reference(Iter32/34/35/25) | L100-171, Iter32/34/18/16 |

## 本轮新发现未消解缺口（I43- 前缀，全局唯一）
- **I43-01（高）**：PDR §11/§12 实为目标估算与发布策略，**无任何核心代数不变量（∪ 幂等/net 守恒/Peak 收敛/Compatible 一致）的测试 target 声明**——测试与代数正确性脱钩（交叉 Iter19 量化）。
- **I43-02（高）**：MA-009「16 组合单元测试覆盖」测的是已实现 Compatible 的选定枚举，而 Compatible 是偏函数+非对称、枚举未闭合（Iter16/Iter22）⇒ 伪规范测试，且可能固化 create+release 误判（Iter16）。
- **I43-03（高）**：§9.1 EffectValidator 测 Deviation 采样实现，但 Deviation 公式 range=0→NaN/∞ 边界未定义（Iter33）⇒ 测试仅覆盖理想路径，RT-002「已收敛」不实。
- **I43-04（高）**：§12.2 AUDIT002/003 是 Analyzer 工具告警（泄漏/预算），依赖 QueueFree=move 误映射（Iter27）与 net 无 scope 混算（Iter37/Iter14）⇒ 工具行为检查非规范证立，固化误判。
- **I43-05（高）**：4 类核心公设（Claim=/⊆*/ω/Compatible）open ⇒ 无 reference 实现 ⇒ 任何测试退化为「实现↔自选解释」循环验证，无法发现解释违反代数律（如 ∪ 实际不幂等，Iter32）⇒ 「测试只能验实现、不能验代数」成立。
- **I43-06（中）**：§14 称「21 问题全部收敛、0 阻塞」与 V1-V5 的测试缺位/伪测试直接矛盾——若测试真能证立规范，§14 应列出不变量测试条目；缺位反证收敛声明不实（交叉 Iter19/Iter20 账本）。

---

一句话摘要：PDR §11/§12 实为工作量估算与发布策略、**无任何核心不变量测试条目**（I43-01，高），MA-009「16 组合单元测试」测的是偏函数+非对称 Compatible 的选定枚举属伪规范测试（I43-02，高，交叉 Iter16/22），§9.1 EffectValidator 仅覆盖 Deviation 理想路径、range=0 NaN 边界未定义（I43-03，高，交叉 Iter33），§12.2 AUDIT002/003 是依赖误映射的工具告警非规范证立（I43-04，高，交叉 Iter27/37），四类公设 open 致无 reference、测试退化为实现↔实现循环（I43-05，高），§14「0 阻塞」与测试缺位矛盾（I43-06，中）——结论：现有测试只验证实现、不能证立代数正确性，需补形式化 reference + 不变量测试方能升级为真·规范测试。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter43.md，未读/改其它 audit 文件，聚焦验证/测试层覆盖域与有效性，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #43（hy3 单独进程）」、V1-V6 各节(命题/数学性质/状态/论证/行号)、Proof Obligation 账本、I43- 缺口列表；交叉引用真实行号(L623-638/L639-736/L537-551/L188/L720-736/L100-171) 并经 read 确认 §11/§12/§9.1/§3.2.3/MA-009/§14 真实文本"}
  ],
  "changedFiles": ["audit/iter43.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 630, 90) + (offset 700, 40)", "result": "passed", "summary": "读取 §11 工作量估算 / §12 发布策略与 AUDIT001/002/003 告警确认无代数测试条目"},
    {"command": "read PDR (offset 537, 70) + (offset 320, 80)", "result": "passed", "summary": "读取 §9.1 EffectValidator 与 §6 L2/L3 工具确认测试对象为实现行为"},
    {"command": "read PDR (offset 100, 75) + (offset 184, 10)", "result": "passed", "summary": "读取 §3.1-3.3 代数不变量与 MA-009 确认 Compatible 偏函数+16 组合声明"},
    {"command": "write D:/Godot/Cosmos/audit/iter43.md", "result": "passed", "summary": "覆盖写入独立审计 #43"}
  ],
  "validationOutput": ["header 含「独立审计 #43（hy3 单独进程）」", "共 V1-V6 六节 + Proof Obligation 账本(V5 项) + 6 条 I43- 缺口", "交叉引用 §11/§12/§9.1/§3.2.3/§3.3.1/MA-009/§14/Iter07/Iter19/Iter32/Iter33/Iter16/Iter22/Iter34/Iter18/Iter27/Iter37/Iter14/Iter25 真实行号"],
  "residualRisks": ["未运行任何 Analyzer/EffectValidator 源码验证告警实际触发条件（仅基于 PDR 文本推导）", "Compatible「16 组合」具体枚举未全文展开，按 §3.2.3 四条规则+Iter16/Iter22 偏函数/非对称推导"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter43.md，独立审计验证/测试层覆盖域与有效性（测试验实现非规范）",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 PDR 无核心不变量测试条目、MA-009/AUDIT 为伪规范测试、§14「0 阻塞」与测试缺位矛盾，需 PDR 侧补形式化 reference + 不变量测试"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；未读其它 audit 文件"
}
