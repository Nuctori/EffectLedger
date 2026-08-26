# Iter13 审计 — §11-13 工作量/术语/社区发布/§14 一致性（独立审计 #13，hy3 单独进程，本轮重跑）

- **审计视角**：文档级一致性 / 元声明真伪 / 工作量与风险闭环（独立 pass #13，全新上下文）
- **范围**：§11 工作量（L623-635）、§12 社区发布（L639-738，重点 §12.1 阶段、§12.2 AUDIT001/002/003）、§13 术语表（L742-763，重点 ResourceId/ScopeId）、§14 文档历史（L767-775）；邻接 §3.1.2 ResourceId 构造子、§3.4 MA-002/006/009、§7.4 Instantiate、§3.1.1 size、Iter04（§14 矛盾）、Iter08/09（映射）、Iter11（Deviation）、Iter12（风险依赖）
- **结论摘要**：§14 v3.0「21 个开放问题全部收敛，0 个阻塞」与文档自身 §3.4（MA-002/006/009 open）及全文审计证据直接矛盾，属 PDR 级结论错误（高，open）。术语表 L748 把 §3.1.2 的 10 构造子 tagged union 平铺为 9 类别字符串，丢失结构（open）。§12.2 AUDIT002「occupy{memory}+10 per death」的 +10 无出处（§7.4 Instantiate 的 size 是 `scene.estimated_size` 估计值、非 10；MA-008 默认=1），AUDIT003「保守估计 64MB」与 MA-008「默认 size=1」矛盾（open）。§11 12-19 周未含本文已暴露 open 问题（Δ 正确性、L2/L3 完备、Deviation 修复、ScopeId⊆、QueueFree mode=move 等）的消解成本 ⇒ 低估（open）。§12 阶段1采用吸引力依赖 R-3 误报消解（Iter12 I12-03），阶段间「采用→反馈→修正」负反馈未建模（open）。结构性成立（阶段目标数字为声明、MIT 协议事实）给条件证明。

---

## M1. §14 v3.0「0 阻塞」与文档自身矛盾（核心，高）

**命题** §14（L774）：v3.0「21 个开放问题全部收敛，0 个阻塞，明确三层安全模型（L1/L2/L3）」；v3.0-FINAL 同样声称「21 个开放问题全部收敛」。

**数学性质 / 证明状态**：
- **(PO-I13-a) PDR 级结论错误（open，高）**：§14 的「0 阻塞」断言依赖「所有开放问题已收敛」，但文档内部即证伪：
  - §3.4 中 MA-002（∞ 代数性质，open）、MA-006（⊔ 非半环，open）、MA-009（Compatible 完备性，open）三条在 Iter04 已还原为 open；单 §3.4 内部即 ≥3 个未收敛项。
  - 全文自 Iter01 至 Iter12 暴露的 open 项（Delta Sync 正确性、L2/L3 完备性、Deviation 除零、QueueFree mode=move、ScopeId⊆ 未定义、默认规则漏报 occupy/release、[EffectOverride] 无校验、白名单覆盖率 <5% 等）均未被 §14 承认。
  - **(论证)** 「21 个问题全收敛」与「§3.4 自带 ≥3 open + §4-9 跨节 open」构成直接矛盾 ⇒ 该元声明为假，非可证性质。状态 = open（高，PDR 结论错误）。
- 交叉：Iter04 已点名「§14 声明与 §3.4 矛盾」，Iter20（总账）将回收此矛盾为文档级 blocker。

**文档行号**：§14（L767-775，L774）、§3.4（L176-190，MA-002/006/009）、Iter04（§14 矛盾论证）。

---

## M2. 术语表 ResourceId 平铺丢失 tagged union 结构（open）

**命题** §13（L748）：`ResourceId | 资源标识：tree, self, physics, memory, disk, signal, gpu, audio, network`（9 类别平铺）；§3.1.2（L88-99）定义 `ResourceId := Tree(path)|Self(component)|Physics(bodyId)|Memory(uid)|Disk(path)|Signal(name)|Gpu(bufferId)|AudioMixer(channelId)|Network(peerId,method)|Custom(name)`（**10 构造子**，带字段的 tagged union）。

**数学性质 / 证明状态**：
- **(PO-I13-b) 类型结构信息丢失（open）**：术语表把带字段的 tagged union 降级为无字段的 9 个裸字符串类别，且：① 漏列 `Custom(name)` 第 10 构造子；② 抹去每个构造子的**判别字段**（path/u64/RID/String 等）。后果：
  - 丢失「ResourceId 相等性基于判别字段」（Iter01 I1-02 / MA-010 的核心）——术语表读者无法从「tree」这个字符串推断相等需比较 `path`，致 `Tree(path)` vs `Tree(Unknown)` 的 Unknown⊤ 保守逻辑不可见。
  - 与 §7 映射（§7.4 `memory(scene.uid)`、§7.9 `network(self.id+"/"+method)` 用构造子字段造 id）无法对表——映射用字段，术语表无字段 ⇒ 文档内部表示不一致。
  - 状态 = open（文档一致性缺陷，非安全阻断但放大 I1-02 归一化缺口）。
- 附带：术语表 ScopeId 列 7 项（method/type/scene/global/loop/conditional/async），与 §3.1.3 `ScopeId := Method|Type|Scene|Global|Loop(id)|Conditional(...)|Async(...)` 基本对应（字段同样被抹），一致性较好；仅 ResourceId 侧结构性丢失。

**文档行号**：§13（L748）、§3.1.2（L88-99）、§7.4/§7.9 映射（L452-499）、Iter01 I1-02 / MA-010。

---

## M3. §12.2 AUDIT002(+10) 与 AUDIT003(64MB) 数值无出处（open）

**命题** §12.2（L662-672）：
- AUDIT002：`Instantiate<Explosion>() has no guaranteed QueueFree() path. Resource leak: occupy{memory} +10 per death, never released.`
- AUDIT003：`Texture2D AlbedoMap has no [Budget]. Using conservative estimate 64MB. Scene 'Level1' accumulated VramMB: 1280MB (default budget 512MB).`

**数学性质 / 证明状态**：
- **(PO-I13-c) +10 来源不明（open）**：§7.4 `Instantiate(scene)` 的 Claim 为 `occupy(memory, scene.estimated_size, create, shell_scope)`——size 是 `scene.estimated_size`（估计值，MA-005 open），**不是常数 10**。且 MA-008「默认 size=1（单位资源）」。AUDIT002 报告写死「+10 per death」与 §7.4 的 `estimated_size`、MA-008 的默认 1 均不对应 ⇒ 该报警数值是**作者手填示例**，非由 §7.4 映射规则推导。状态 = open（示例与机制脱节，削弱「自动推导」宣称）。
- **(PO-I13-d) 64MB 与 MA-008 默认 size=1 矛盾（open）**：AUDIT003「Texture2D → 默认 occupy{memory, 64MB}」；但 MA-008（L185）明定「默认 size = 1（单位资源），显存/内存等精确资源显式标注 size」。即同一文档内：显存/内存的默认 size 在 MA-008 是 1，在 §12.2 是 64MB ⇒ 直接矛盾。若按 MA-008 默认 1，则「20 个 Enemy × 1 = 20MB」而非「1280MB」，报警阈值逻辑全变。状态 = open（数值口径冲突，致 §12.2 的「保守估计」无法与 §3 代数对齐）。
- 附带：AUDIT003「默认预算 512MB」与 §12.1 阶段2「[GlobalBudget(VramMB=512)]」一致（自洽）；但 512 与 64MB 单纹理估计的组合未给推导。

**文档行号**：§12.2（L656-672，AUDIT002/AUDIT003）、§7.4（L458，Instantiate size）、MA-008（L185）、§12.1（L644）。

---

## M4. §11 工作量未计入已暴露 open 问题的消解成本（open）

**命题** §11（L623-635）：总计 12-19 周（3-5 人月），逐项列 Domain/Shell/SG/Analyzer/Interop/映射/运行时/测试。

**数学性质 / 证明状态**：
- **(PO-I13-e) 工作量低估（open）**：§11 的模块拆分未显式列出「证明义务消解」子项。本文（Iter01-Iter12）已暴露且未收敛的高影响 open 项需要额外工程/形式化投入，至少含：
  - Delta Sync 一致性判据设计与证明（Iter06 PO-I6-e，高，核心）；
  - L2/L3 检测完备性 soundness 证明（Iter07 PO-I7-b/c/d，高）；
  - Deviation 公式 range=0 除零修复 + 区间载体定义（Iter11 PO-I11-a/b/c，高）；
  - ScopeId⊆ 偏序定义与 Peak 跨 scope 并（Iter15 PO-I15-*，open）；
  - QueueFree mode=move → release 修正（Iter08 PO-I8-a / Iter17，高）；
  - 默认规则含 occupy/release 或强制白名单覆盖（Iter10 PO-I10-a，高）；
  - [EffectOverride]/[AcceptDeviation] 校验（Iter10 PO-I10-g / Iter11 PO-I11-g，安全）；
  - 白名单覆盖率从 <5% 提升至覆盖（Iter10 PO-I10-f）；
  - ResourceId 相等性/Unknown⊤ 数学对象定义（Iter01 I1-03）。
  - 这些均**不在** §11 的 8 个模块中（SG/Analyzer 仅列「实现」未列「完备性证明」；运行时仅列「偏差检测」未列「Deviation 良定义修复」）。故 12-19 周是「按现有（含 open）设计实现」的估时，非「收敛全部 open 问题」的估时 ⇒ 低估。状态 = open（工作量估算与风险/证明缺口脱钩）。

**文档行号**：§11（L623-635）、Iter06/07/10/11/15/17 对应 PO。

---

## M5. §12 阶段1采用吸引力依赖 R-3 误报消解，负反馈未建模（open）

**命题** §12.1（L641-657）：阶段1（Audit-only）目标「1000 下载，收集误报反馈」；阶段2-4 逐级侵入。

**数学性质 / 证明状态**：
- **(PO-I13-f) 采用吸引力依赖未证误报消解（open）**：阶段1核心卖点是「零改动 + 自动推导 + 报警」。但 Iter12 I12-03 指出 R-3（误报率高，高/高）的缓解依赖 L3 Analyzer 完备性（open）与白名单覆盖率（<5%，open）；即阶段1的「报警质量」建立在未证机制上。文档把「收集误报反馈」当阶段1成功指标，但**未建模**：若阶段1报警因漏报 occupy/release（Iter10 I10-01）而**漏报真实泄漏**（误报的反向——false negative），则「1000 下载」的采用吸引力反而建立在危险的不完整信号上 ⇒ 采用吸引力与机制可靠性正相关假设未证。状态 = open（采用策略风险未闭环）。
- **(PO-I13-g) 阶段间负反馈未建模（open，弱）**：§12.1 是线性四阶段推进，未定义「阶段1反馈 → 阶段2设计修正」的回写机制（如误报分布如何改变默认规则、白名单如何随反馈扩充）。Iter10 PO-I10-f（覆盖率<5%）若仅靠阶段1反馈扩充，其速率/成本未在 §11 工作量体现 ⇒ 与 M4 同源。状态 = open（弱）。

**文档行号**：§12.1（L641-657）、Iter12 I12-03、Iter10 PO-I10-f。

---

## M6. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若 §14 的「21 问题全收敛」改为「已实现机制层收敛，形式化证明 open 项见审计账本」，则 §14 与 §3.4/全文一致。证明：措辞弱化排除矛盾。前提 PO-I13-a（声明未修订）未立 ⇒ 条件，实际未消解。
- **P2（discharged，条件）**：若术语表 ResourceId 改回带字段 10 构造子（或注明「详见 §3.1.2」），则术语表与 §3.1.2/§7 映射一致。证明：结构对齐。前提 PO-I13-b（平铺未改）未立 ⇒ 条件。
- **P3（discharged，条件）**：若 AUDIT002 的 +10 改为 `scene.estimated_size`、AUDIT003 的 64MB 改为与 MA-008 默认 size=1 统一的口径，则 §12.2 报警与 §3/§7 代数一致。证明：数值口径对齐。前提 PO-I13-c/d（数值未修订）未立 ⇒ 条件。
- **P4（discharged）**：MIT 协议（§12.3）、阶段目标数字（1000/500/50/10 下载）为声明性事实，不依赖代数机制 ⇒ 结构性成立。证明：工程事实独立。

---

## Proof Obligation 账本（Iter13）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I13-a | §14「0 阻塞」与 §3.4/全文矛盾 | open(高) | 修订 §14 声明或补齐收敛证明 | L774, L176-190 |
| PO-I13-b | 术语表 ResourceId 丢 tagged union | open | 改回 10 构造子+字段 | L748, L88-99 |
| PO-I13-c | AUDIT002 +10 无出处 | open | 改 `scene.estimated_size` | L662, L458 |
| PO-I13-d | AUDIT003 64MB 与 MA-008 默认1 矛盾 | open | 统一 size 口径 | L667, L185 |
| PO-I13-e | §11 工作量未含 open 消解成本 | open | 补证明义务子项估时 | L623-635 |
| PO-I13-f | 阶段1采用依赖未证误报消解 | open | 建模漏报反向风险 | L641-657, Iter12 |
| PO-I13-g | 阶段间负反馈未建模 | open(弱) | 定义反馈回写机制 | L641-657 |

## 本轮新发现未消解缺口（I13- 前缀，全局唯一）
- **I13-01（高）**：§14 v3.0「21 开放问题全收敛，0 阻塞」与 §3.4（MA-002/006/009 open）及全文审计证据直接矛盾，是 PDR 级结论错误。
- **I13-02**：术语表 L748 把 §3.1.2 的 10 构造子 tagged union 平铺为 9 无字段类别，漏列 `Custom`，丢失相等性判别字段 ⇒ 与 §7 映射无法对表。
- **I13-03**：AUDIT002「+10 per death」与 §7.4 `scene.estimated_size`、MA-008 默认 1 均不对应，属手填示例，非机制推导。
- **I13-04**：AUDIT003「保守估计 64MB」与 MA-008「默认 size=1」矛盾，致 §12.2 数值无法与 §3 代数对齐。
- **I13-05**：§11 12-19 周未计入 Delta 正确性/L2-L3 完备/Deviation 修复/ScopeId⊆/QueueFree mode 等已暴露 open 的消解成本 ⇒ 低估。
- **I13-06**：§12 阶段1采用吸引力依赖 R-3 误报消解（未证机制），且漏报反向风险未登记；阶段间负反馈未建模。
- **I13-07**：§14 与 §3.4 矛盾需由 Iter20 总账回收为文档级 blocker（交叉 Iter04 / Iter20）。

---

一句话摘要：§14 v3.0「0 阻塞」与文档自身 §3.4（MA-002/006/009 open）及全文审计证据直接矛盾、属 PDR 级结论错误（I13-01，高）；术语表 ResourceId 平铺丢失 10 构造子 tagged union（I13-02）；AUDIT002 的 +10、AUDIT003 的 64MB 分别与 §7.4/MA-008 数值口径冲突（I13-03/04）；§11 工作量未含已暴露 open 的消解成本（I13-05）；§12 阶段1采用依赖未证误报消解且负反馈未建模（I13-06）。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter13.md，未读/改其它 audit 文件（仅本轮前已读的 iter04 作交叉引用，未再读新 audit 文件），聚焦 §11-14 文档一致性审计"}
  ],
  "changedFiles": ["audit/iter13.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md (offset 623, 152 lines)", "result": "passed", "summary": "读取 §11-14 真实文本确认工作量/发布/术语/历史"},
    {"command": "read PDR (offset 176, 30) + (offset 452, 12) + (offset 88, 18)", "result": "passed", "summary": "交叉确认 §3.4 MA-002/006/009、§7.4 Instantiate size、§3.1.2 ResourceId 10 构造子"},
    {"command": "write D:/Godot/Cosmos/audit/iter13.md", "result": "passed", "summary": "覆盖写入独立审计 #13"}
  ],
  "validationOutput": ["header 含「独立审计 #13（hy3 单独进程，本轮重跑）」", "共 M1-M6 六节 + Proof Obligation 账本 + 7 条 I13- 缺口", "交叉引用 §3.1.2/§3.4/§7.4/§14/MA-008/Iter04/Iter12 真实行号"],
  "residualRisks": ["未运行外部工具验证 §12.2 报警数值是否由 Analyzer 真实产出（仅基于文档文本比对 +10/64MB 与 §7.4/MA-008 口径）", "§3.3 Peak 公式 ω 语义未在本轮读取，依赖 Iter04/Iter18 交叉引用"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter13.md，独立审计 §11-14 文档级一致性缺口",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 §14「0 阻塞」为 PDR 级结论错误、术语表/AUDIT 数值口径冲突，需 PDR 侧修正"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；所有行号基于本轮 PDR 实际 read；iter04 为任务前已读，未再读其它 audit 文件"
}
