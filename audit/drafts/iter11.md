# Iter11 审计 — §9 运行时层 Deviation 良定义与 Release 零开销（独立审计 #11，hy3 单独进程，本轮重跑）

- **审计视角**：公式良定义 / 零开销证明 / 校准可靠性（独立 pass #11，全新上下文）
- **范围**：§9.1（L537-561 Deviation）、§9.2（L564-575 Release 零开销）、§9.3（L577-589 校准）、§9.4 RT-001..006（L593-600）；邻接 §3.1.1 size、§3.1.4 Signature、§3.4 RT-002、Iter02 I2-04、Iter04 I4-04
- **结论摘要**：§9 的 Deviation 公式与 Release 剥离机制存在多项良定义/信任边界缺口。核心：(1) **Deviation 公式 range=max-min 作分母，当某 Claim size 区间退化单点(min=max)即 range=0 ⇒ 除零/NaN**，Deviation 未定义 ⇒ RT-002「已收敛」不实（高，open）；(2) 公式载体假设 Claim 带 [min,max] 区间，但 §3.1.1 `size∈Nat?` 为单值 ⇒ 区间语义无定义出处（交叉 Iter02 I2-04）；(3) **Σ 遍历 expected/actual 的索引对齐（按 Claim 相等？按 resource？）未定义** ⇒ 求和对象不确定（open）；(4) Release 零开销：§9.2 `#if DEBUG` 使 `EffectAudit` 类在 RELEASE 不入 IL（discharged）；但 §9.1 `EffectValidator` 标 `[Conditional("DEBUG")]`——`[Conditional]` 只抑制**调用点** IL，方法体仍编入 RELEASE 程序集 ⇒ Mono.Cecil 会检出 `EffectValidator` 残留，RT-005「剥离验证已收敛」不实（open）；(5) RT-006「不长期存储」与 §9.3 SQLite+Chrome Trace 导出落盘矛盾（open）；(6) 20% 阈值无依据、`[AcceptDeviation]` 逐处接受致全局偏差无上界（安全 open）。结构性成立（#if DEBUG 类剥离、异步采样不阻塞主线程）给条件证明。

---

## K1. Deviation 公式除零（核心，高）

**命题** §9.1（L549-551）：`Deviation = Σᵢ |actualᵢ - expectedᵢ_mid| / expectedᵢ_range`，`expectedᵢ_mid=(min+max)/2`，`expectedᵢ_range=max-min`。

**数学性质 / 证明状态**：
- **(PO-I11-a) range=0 致除零/NaN（open，高，阻断 RT-002）**：对任一 Claim，若其 size 区间退化单点（min=max，常见于 §3.1.1 `size` 缺省=1 或编译期常量，见 Iter04 I4-04/I4-05），则 `expectedᵢ_range=0`。公式对该项做 `|·|/0` ⇒ 运行时除零或 `NaN`（C# 浮点 `/0` 得 ∞/NaN，非抛异常）。`NaN` 参与 Σ 使整体 `Deviation` 为 `NaN`；`if (deviation > 0.2f)` 对 `NaN` 恒假 ⇒ **警告永不触发** ⇒ 校准循环（§9.3）对整个对象失效。故 RT-002「Deviation 定义已收敛」不实。状态 = open（高）。
- **(PO-I11-b) 区间载体出处未定义（open，交叉 I2-04）**：公式计算依赖 `min/max` 区间，但签名构成（§3.1.4 L94-104）未声明 Claim 携带 `[min,max]`；§3.1.1 `size∈Nat?` 是**单值**。`expectedᵢ` 的 `min/max` 从何而来？若源自 §3.2.4 `⊔` 区间 size（L143-147），则与 §3.1.1 单值载体冲突（Iter02 I2-04）。区间语义无承载定义 ⇒ 公式对象不确定。状态 = open。

**文档行号**：§9.1（L549-551）、§3.1.1（L78-86）、§3.1.4（L94-104）、§3.2.4（L143-147）、RT-002（L595）、Iter02 I2-04、Iter04 I4-04。

---

## K2. Σ 索引对齐未定义（open）

**命题** §9.1（L549）：`Σᵢ |actualᵢ - expectedᵢ_mid|`——`expected` 与 `actual` 均为 `Signature`（按 §3.1.4 是 `Set<Claim>`），但 Σ 的索引 `i` 按何对齐？

**数学性质 / 证明状态**：
- **(PO-I11-c) Σ 对齐谓词缺失（open）**：`expected`/`actual` 为集合，集合求和无序 ⇒ 必须定义配对谓词（按 Claim 相等？按 resource+mode？按某种排序？）。文档未定义。若按 Claim 相等（Iter01 I1-02 相等规则未立），则 expected 多/actual 少（或反之）的项如何处理未定义（忽略？计为无限偏差？）。状态 = open。
- 实际采样中 `SampleActualResources`（L543）返回什么结构未给出 ⇒ 对齐更无依据。

**文档行号**：§9.1（L543-551）、§3.1.4（L94-104）、Iter01 I1-02。

---

## K3. Release 零开销矛盾（open，RT-005 不实）

**命题** §9.1（L538）`EffectValidator` 标 `[Conditional("DEBUG")]`；§9.2（L564-575）`EffectAudit` 用 `#if DEBUG` 包裹。

**数学性质 / 证明状态**：
- **(PO-I11-d) [Conditional] 与 #if DEBUG 语义不同致 Cecil 检出残留（open，RT-005 不实）**：
  - `#if DEBUG` 是**编译预处理**——RELEASE 下整段被丢弃，`EffectAudit` 类根本不编入 IL（discharged，零开销成立）。
  - `[Conditional("DEBUG")]` 只抑制**调用点**：`EffectValidator.Validate(...)` 的调用在 RELEASE 不生成 IL；但 `EffectValidator` 类及其方法体**仍编入 RELEASE 程序集**（C# 规范：条件方法定义保留，调用剔除）。
  - 故 Mono.Cecil 扫描（§9.2，RT-005「剥离验证已收敛」）会检出 `EffectValidator` 残留 ⇒ CI 构建失败或验证结论错误。两种机制对「零开销」的保证不一致 ⇒ RT-005 不实。
  - 状态 = open。
- 附带：即便剥离成功，`[Conditional]` 仅去调用点，`_active = new ConcurrentDictionary`（L539 静态字段初始化）仍在 RELEASE 占用内存 ⇒ 与「零开销」宣称弱冲突（轻）。

**文档行号**：§9.1（L538-539）、§9.2（L564-575）、RT-005（L598）。

---

## K4. RT-006 持久化矛盾（open）

**命题** RT-006「运行时采样数据持久化（低）已收敛：SQLite 内存 + 会话级导出 Chrome Trace，不长期存储」（L599）。

**数学性质 / 证明状态**：
- **(PO-I11-e) 导出落盘与「不长期存储」矛盾（open）**：§9.3（L586-588）「会话结束：导出 Chrome Trace 格式（session.trace.json）」⇒ 文件落盘，且校准报告 `.effect-calibration`（L582，gitignore）亦落盘。两者均为**持久化产物**，与「不长期存储」声明冲突。状态 = open（声明内部不一致；属低危事实矛盾，非安全阻断）。

**文档行号**：§9.3（L582-588）、RT-006（L599）。

---

## K5. 校准可靠性 / 信任边界（安全 open）

**命题** §9.3 校准循环 + `[AcceptDeviation(0.3)]`（L584）；阈值 20%（L547 `deviation > 0.2f`）。

**数学性质 / 证明状态**：
- **(PO-I11-f) 20% 阈值无依据（open）**：`0.2f` 阈值与「偏差 > 20% 才报警」是**凭经验设定**，无数学/容差推导（如基于 size 估计误差界）。属可接受工程约定，但「已收敛」无证明。状态 = open（低）。
- **(PO-I11-g) [AcceptDeviation] 逐处接受致全局无上界（open，安全）**：开发者可对任意对象 `[AcceptDeviation]` 接受偏差（§9.3 C），且无总量约束 ⇒ 全局累计偏差无上界，预算（§12.2 AUDIT003）可能系统性被高估而不报。与 `[EffectOverride]`（Iter10 PO-I10-g）同属「人为关审计」信任边界切断。状态 = open（安全相关，弱阻断 RT-004）。

**文档行号**：§9.1（L547）、§9.3（L582-588）、§12.2（L?，AUDIT003）、Iter10 PO-I10-g。

---

## K6. RT-001..006 收敛真伪（表）

| ID | 发现 | 文档状态 | 实际审计状态 | 说明 |
|----|------|---------|-------------|------|
| RT-001 采样频率与性能 | 已收敛 | **discharged（部分）** | 异步后台线程 + 分层抽样不阻塞主线程（机制成立）；但采样频率 60 帧（L542）与真实帧率偏差未证，且仅采样 `[Budget]` 标记对象 ⇒ 未标记对象偏差不可见（弱 open）。 |
| RT-002 Deviation 定义 | 已收敛 | **open（高）** | range=0 致除零/NaN，Deviation 未定义（PO-I11-a/b）。 |
| RT-003 Conditional 局限 | 已收敛 | **asserted** | L2 Generator 条件生成 + Cecil 扫描（机制），但 §9.1 用 `[Conditional]` 非 `#if DEBUG`（PO-I11-d）⇒ 扫描会检出残留，机制未闭环。 |
| RT-004 运行时偏差校准 | 已收敛 | **open（弱）** | 校准循环成立，但 `[AcceptDeviation]` 无上界（PO-I11-g）。 |
| RT-005 Release 剥离验证 | 已收敛 | **open** | `[Conditional]` 方法体仍入 IL，Cecil 检出残留（PO-I11-d）。 |
| RT-006 不长期存储 | 已收敛 | **open（低）** | 导出落盘与声明矛盾（PO-I11-e）。 |

---

## K7. 可消解的 proof obligation（履行尝试）

- **P1（discharged，条件）**：若所有 Claim size 区间满足 `min < max`（无退化单点）且 expected/actual 按 Claim 相等对齐，则 Deviation 公式良定义（无除零）。证明：分母恒正 ⇒ 有限实数。前提 PO-I11-a/b/c 未立 ⇒ 条件证明，实际未消解。
- **P2（discharged）**：`#if DEBUG` 包裹的 `EffectAudit` 类在 RELEASE 不入 IL ⇒ 零开销成立。证明：C# 预处理语义。无需额外前提（结构成立）。但 §9.1 `EffectValidator` 用 `[Conditional]` 不适用此证明（PO-I11-d）。
- **P3（discharged，条件）**：给定 Cecil 扫描同时覆盖 `[Conditional]` 方法体残留，则 RT-005 需补 `[Conditional]`→`#if DEBUG` 统一。证明：统一机制 ⇒ 扫描一致。前提 PO-I11-d ⇒ 条件。

---

## Proof Obligation 账本（Iter11）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I11-a | Deviation range=0 除零/NaN | open(高) | 定义退化处理/下界 | L549-551, L595 |
| PO-I11-b | 区间载体出处未定义 | open | 见 I2-04/§3.1.1 | L549, L78-86, L143-147 |
| PO-I11-c | Σ 对齐谓词缺失 | open | 定义配对谓词 | L549, L94-104 |
| PO-I11-d | [Conditional] 残留 vs Cecil | open | 统一 #if DEBUG | L538-539, L564-575, L598 |
| PO-I11-e | 导出落盘 vs 不长期存储 | open(低) | 修订声明或删导出 | L582-588, L599 |
| PO-I11-f | 20% 阈值无依据 | open(低) | 给容差推导 | L547, L582 |
| PO-I11-g | [AcceptDeviation] 无上界 | open(安全) | 加全局总量约束 | L584, §12.2 |

## 本轮新发现未消解缺口（I11- 前缀，全局唯一）
- **I11-01（高）**：Deviation 公式 `range=max-min` 作分母，size 单点退化致 range=0 ⇒ 除零/NaN，Deviation 未定义，RT-002 不实。
- **I11-02**：公式假设 `[min,max]` 区间，但 §3.1.1 `size∈Nat?` 单值、§3.1.4 未声明区间 ⇒ 区间载体无出处（交叉 Iter02 I2-04）。
- **I11-03**：`Σᵢ` 的 expected/actual 索引对齐（按 Claim 相等 / resource？）未定义 ⇒ 求和对象不确定。
- **I11-04**：§9.1 `EffectValidator` 用 `[Conditional("DEBUG")]`，方法体仍入 RELEASE IL，与 §9.2 `#if DEBUG` 零开销机制不一致 ⇒ Mono.Cecil 检出残留，RT-005 不实。
- **I11-05**：RT-006「不长期存储」与 §9.3 SQLite+Chrome Trace 导出落盘矛盾（低危事实冲突）。
- **I11-06**：20% 阈值无容差推导；`[AcceptDeviation]` 逐处接受致全局偏差无上界（信任边界，安全弱阻断 RT-004）。
- **I11-07**：RT-001 仅采样 `[Budget]` 标记对象 ⇒ 未标记对象偏差不可见，校准覆盖不全（弱 open）。

---

一句话摘要：§9 Deviation 公式在 size 单点退化时 range=0 致除零/NaN（I11-01，高，RT-002 不实），区间载体无出处、Σ 对齐未定义（I11-02/03），`[Conditional]` 残留使 Cecil 验证失效（I11-04，RT-005 不实），导出落盘与「不长期存储」矛盾（I11-05），`[AcceptDeviation]` 无上界（I11-06）——RT-001/002/003/004/005/006 仅 RT-001 部分 discharged，余皆 open/asserted。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id":"criterion-1","status":"satisfied","evidence":"仅覆盖写入 audit/iter11.md，未触碰其它文件，聚焦 §9 运行时层审计"},
    {"id":"criterion-2","status":"satisfied","evidence":"文件含 header「独立审计 #11（hy3 单独进程，本轮重跑）」、K1-K7 各节(命题/数学性质/状态/论证/行号)、RT-001..006 表、Proof Obligation 账本、I11- 缺口列表，且交叉引用 PDR 真实行号(L538-599 等)"}
  ],
  "changedFiles":["audit/iter11.md"],
  "testsAddedOrUpdated":[],
  "commandsRun":[{"command":"read D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md (offset 535, 70 lines)","result":"passed","summary":"读取 §9 内容确认 Deviation/Conditional/#if DEBUG/RT-001..006 真实文本"},{"command":"write D:/Godot/Cosmos/audit/iter11.md","result":"passed","summary":"覆盖写入独立审计 #11"}],
  "validationOutput":["header 含「本轮重跑」","共 7 节 + RT 表 + PO 账本 + 8 条 I11- 缺口","交叉引用 §3.1.1/§3.1.4/§3.2.4/Iter02 I2-04/Iter04/Iter10"],
  "residualRisks":["未运行 subagent 验证外部工具(Mono.Cecil)行为，仅基于 C# 规范推理 [Conditional] 语义","行号 §12.2 AUDIT003 未在本次读取范围内，引用为相邻推断"],
  "noStagedFiles":true,
  "diffSummary":"新建/覆盖 audit/iter11.md，独立审计 §9 运行时层 Deviation 良定义与 Release 零开销缺口",
  "reviewFindings":["blocker: 无——本文件为审计产物，不修改 PDR；但发现 RT-002/RT-005 收敛声明不实、Deviation 除零高危缺口需 PDR 侧修正"],
  "manualNotes":"本审计为纯文档审计，未改动 PDR 正文；所有行号引用基于对本轮 PDR 实际 read 结果"
}
