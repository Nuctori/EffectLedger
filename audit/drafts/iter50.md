# Iter50 审计 — 细粒度全局回收+总账+收口 AUDIT002/003 与 EffectOverride（独立审计 #50，hy3 单独进程）

- **审计视角**：50 轮细粒度审计的全局回收 + 总账量化 + 收口两项遗留（AUDIT002/003 数值口径、[EffectOverride]/[AcceptDeviation] 校验）（独立 pass #50，全新上下文）
- **范围**：§14 收敛声明（L738-742，「21 个开放问题全部收敛，0 个阻塞」）、§12.2 AUDIT002/AUDIT003（L717/L722）、§7/§8/§9 中 [EffectOverride]（L524 ED-001）/[AcceptDeviation]（L587 §9.3）、§3.1.1 size∈Nat?（L78-86）、§3.3.1 net（L163-165）、§3.3.2 peak（L167）、§9.1 CalculateDeviation（L547-561）；邻接 iter15/34（ScopeId⊆）、iter01/32（Claim 相等）、iter16/22/25（Compat 全函数）、iter18/35（ω/S×ω）、iter27（QueueFree）、iter37（net(scope)）、iter14/36（DO-7 分桶）、iter07/38/39（L2/L3 完备）、iter11/33（Deviation range）、iter19/20（量化总账）
- **结论摘要**：本审计把 iter21–50 的细粒度发现做全局回收，给出「阻塞依赖链」根因排序、量化总账，并收口 #61/#62 两项遗留。(A) 高优先根因按依赖链排序：① Claim 相等/归一（iter01/32）与 ② ScopeId⊆ 偏序（iter15/34）是两个最深根，喂给 Peak/peak/net(scope)/Compatible 过滤；③ ω 载体+S×ω（iter18/35）喂 Peak(L154) 收敛；④ QueueFree mode=release（iter27）喂 net 泄漏检测；⑤ Compatible 全函数+对称（iter16/22/25）喂 DO-9 并发与 L3 COMPAT；⑥ net(S,scope) 分组（iter37）依赖①⑤；⑦ DO-7 kind 分桶（iter14/36）喂 BUDGET001/AUDIT003；⑧ L2/L3 完备（iter07/38/39）喂所有工具层执行；⑨ Deviation range=0（iter11/33）喂运行时校准；⑩ size 多口径（iter02/26/46）喂 AUDIT002/003 数值统一。(B) 总账：§14 自称「21 问题收敛/0 阻塞」，但 iter19 量化 49 对象/combinator/API 仅 ~8% 完全良定义、~71% 悬空；iter20 账本 ~123 PO-I*（~0 履行/~12 宣称/~108 open）、~70 I- 缺口（~40 高）。实测与声明严重不符。(C) AUDIT002「+10 per death」(L717) 与 AUDIT003「64MB」(L722) 均为硬编码/启发式数字，与 §3.3.1 net(c.size,默认1,L163)、§3.3.2 peak(Σsize,L167)、§9.1 Deviation(range,L549) 的 size 载体**均不统一**（至少 5 种 size 口径），且「default budget 512MB」(L722) 在代数外 ⇒ 数值口径未统一(open,高)。[EffectOverride] 仅于 ED-001(L524) 作为「收敛方案允许修正」被提及，无校验规则（可覆盖哪些冲突、语法、是否可压制 DO-9/DO-7、未审查 ⇒ open,高，逃逸通道无约束）；[AcceptDeviation(0.3)] 仅于 §9.3(L587) 选项 C 提及，无规则（0.3 vs 报警阈值 0.2f L547 关系、是否压制 AUDIT、局部/全局、无上界 ⇒ open,高）。两项属性均未定义校验 ⇒ 仍是未消解缺口。诚实结论：无一项在本审计内消解，全部 open。

---

## 节A. 阻塞依赖链排序（高优先根因）

按「被依赖广度」排序（越靠前，下游越多 audit 卡在其上）：

**根 1（最深）— Claim 相等/归一规则未定义**（iter01 I1-02 / iter32 PO-I32-a）
- 喂给：∪ 幂等（§3.2.1）、resource 去重、§3.2.2 约束 `c₁.resource=c₂.resource`、§3.3 聚合按 resource、Deviation Σ 对齐（iter33 PO-I33-d）、net(scope) 分组、S×ω 副本去重（iter35 PO-I32）。
- 数学性质：未定义 `Claim₁=Claim₂` 五元组相等 ⇒ 集合代数 A1/A2/A3 无定义基础。状态 = open（高）。

**根 2 — ScopeId⊆ 偏序未定义**（iter15 I15-01 / iter34 PO-I34-a）
- 喂给：Peak(L154 `c.scope⊆scope`)、peak(L167 `c.scope⊆t`)、net(S,scope)(iter37)、Compatible 作用域过滤、Iter28 内存跨 scope 可并。
- iter34 给出 ⊆* 草案但未采纳 ⇒ 仍 open（高，条件草案）。

**根 3 — ω 载体 + S×ω 算子未定义**（iter18 I18-01/02 / iter35 PO-I35-a/b）
- 喂给：Peak(L154) 收敛（ω=∞ 发散）、循环组合 `(while b do S)=Signature(b)∪(S×ω)`。
- iter35 给 ω∈ℕ∪{⊤}+copy_j 草案但未采纳 ⇒ open（高，条件草案）。

**根 4 — QueueFree mode=release 未修正**（iter27 I27-01 / iter37 PO-I37-d）
- 喂给：net 泄漏检测（§3.3.1）、DO-9 判定、AUDIT002「never released」数值、net(scope) 有效性。
- 文档 §7.1 L429 仍 mode=move ⇒ net 漏算释放 ⇒ open（高）。

**根 5 — Compatible 全函数 + 对称**（iter16 I16-01/02 / iter22 / iter25 PO-I25-）
- 喂给：DO-9 并发安全判定、L3 COMPAT 规则（iter39 PO-I39-a）、平行 API 冲突。
- 现状：偏函数 + 非对称（create+release 良性未识别、use∧use 与 Unknown 短路不一致）⇒ open（高）。

**根 6 — net(S,scope) 分组**（iter37 PO-I37-a）
- 依赖：根1（Claim= 去重）+ 根2（⊆*）+ 根4（QueueFree）。
- 使 DO-9 作用域粒度可行，草案未采纳 ⇒ open（高，条件）。

**根 7 — DO-7 kind 分桶 Signature**（iter14 I14-01/02/06 / iter36 PO-I36-a）
- 喂给：BUDGET001 预算、AUDIT003 VramMB、peak 混加、L3 KIND_MIX。
- 草案 (R,W,O)+weight 未采纳 ⇒ open（高，条件）。

**根 8 — L2/L3 完备性**（iter07 I7-01..05 / iter38 / iter39）
- 喂给：根1–7 全部的工具层执行（若工具不覆盖，代数再良定义也无强制）。
- L2 仅类型安全非 effect 完备（iter38）、L3 五规则与代数脱节（iter39）⇒ open（高）。

**根 9 — Deviation range=0 退化**（iter11 I11-01 / iter33 PO-I33-a/b）
- 喂给：§9.1 运行时校准闭环（NaN 永不报警/∞ 恒真）。
- 区间载体无出处 + 无 range 下界 ⇒ open（高，运行期）。

**根 10 — size 多口径不统一**（iter02 I2-04 / iter26 / iter46）
- 喂给：AUDIT002/003 数值统一、peak/net 数值、Deviation 单位。
- §3.1.1 size∈Nat? 单值、ED-004 动态=∞、AUDIT002=+10、AUDIT003=64MB、Deviation=[min,max]、BUDGET=512MB 共 ≥6 种口径 ⇒ open（高）。

**依赖链图（箭头=「被依赖/下游卡住」）**：
根1,根2 → {peak, net(scope), Compat 过滤}；根3 → Peak(L154)；根4 → 根6；根5 → L3 COMPAT；根7 → BUDIT003/BUDGET；根8 横切全部执行；根9 → 运行时；根10 → AUDIT002/003。
⇒ 根1+根2 是「代数层最深处双根」，修复它们可解锁 Peak/peak/net/Compat 四类过滤；根4+根6 是 DO-9 泄漏主线；根7+根10 是数值主线（直接收口 #61）。

---

## 节B. 总账量化（open 高优先清单）

**文档自声称（§14 L738-742）**：v3.0/v3.0-FINAL「21 个开放问题全部收敛，0 个阻塞，明确三层安全模型（L1/L2/L3）」。
**实测（iter19/iter20 + 本轮 30 轮回收）**：

| 维度 | §14 声称 | 实测（iter19/20 量化） | 偏差 |
|------|---------|----------------------|------|
| 开放问题收敛 | 21 收敛 | 49 对象/combinator/API 仅 ~8% 完全良定义、~71% 悬空 | 严重不符 |
| 阻塞数 | 0 阻塞 | ~8 个高优先根因（节A 根1–10，其中根1/2/4/5/7/8/9/10 为 open 高）+ ~108 PO-I open + ~40 I- 高 | 严重不符 |
| Proof Obligation | 隐含全 discharged | ~123 PO-I*：~0 真正履行 / ~12 仅「asserted 宣称」/ ~108 open（iter20 账本） | 严重不符 |
| 三层完备 | L1/L2/L3 明确 | L2 仅类型安全(iter38)、L3 五规则与代数脱节(iter39)、L1 未独立证 ⇒ 完备性 open(iter07) | 严重不符 |

**总账 open 计数（保守下界）**：
- 高优先根因：节A 根1–10 中 8 个 open（根1/2/4/5/7/8/9/10）；根3/6 为「条件草案」依赖根1/2/4。
- PO-I* 账本：~108 open（iter20），其中高优先（根1–10 相关）~40。
- I- 缺口：~70（iter20），高 ~40。
- 文档 §3.4/§8/§9/§10 中「已收敛」标记（MA-001..010、ED-001..008、RT-001..005、R-1..12）大量为「asserted 宣称」而非 discharged（iter19/20/iter42/iter43 交叉）——至少 MA-002(∞ 闭包,iter45)、MA-006(⊔ 区间,iter46)、MA-007(权重,iter47)、ED-004(动态=∞,iter26)、RT-002(Deviation,iter33) 已被证伪为未收敛。

**结论**：§14「21 问题收敛/0 阻塞」与 30 轮细粒度实测矛盾——真实状态为「~8 高优先根因 open + ~108 PO open + ~40 高 I- 缺口 + 多层工具完备性 open」。文档收敛声明不实，需回写为「0 收敛、≥8 阻塞」。

---

## 节C. 收口 #61/#62 遗留

### C1. AUDIT002/AUDIT003 数值口径统一（#61）

**命题**（§12.2 L717 AUDIT002）：`Instantiate<Explosion>() has no guaranteed QueueFree() path. Resource leak: occupy{memory} +10 per death, never released.`
**命题**（§12.2 L722 AUDIT003）：`Texture2D AlbedoMap has no [Budget]. Using conservative estimate 64MB. Scene 'Level1' accumulated VramMB: 1280MB (default budget 512MB).`

审计数值口径冲突：
1. **AUDIT002 的「+10」**：硬编码字面量，无来源。代数侧 Instantiate 的 occupy 应取 size=∞（ED-004 L527 动态标记 ∞，iter26）或 .tscn 显式 size；§3.3.1 net 以 `c.size` 计（L163）。"+10" 既 ≠ ∞ 也 ≠ 代数 size ⇒ 与 net 公式不可对账。
2. **AUDIT003 的「64MB」**：启发式默认（§12.2 L713「Texture2D 字段 → 默认 occupy{memory,64MB}」），单位 MB；而 §3.1.1 size∈Nat? 默认 1（L82）、§3.3.2 peak 用 Σsize（L167）—单位/量级均不同。「accumulated VramMB: 1280MB」是 20×64MB 累加，但 peak/net 的 size 默认 1，二者无法在同一代数内相加。
3. **「default budget 512MB」(L722)**：独立于代数（§3.3 peak/net 无预算常量），是外部警戒线，未与 kind 分桶（iter36）或 weight 函数（iter47）挂钩 ⇒ 跨 kind 直接比 MB 违反 DO-7（iter14/36）。
4. **Deviation 单位（§9.1 L549）**：用 `range=max-min` 区间百分比，与 AUDIT002/003 的 MB/字面量数字又不同维度。

⇒ **至少 5 种 size/数值口径共存**：(a) 代数 size∈Nat 默认1 (L82/L163/L167)、(b) ED-004 动态=∞ (L527)、(c) AUDIT002 +10 字面量 (L717)、(d) AUDIT003 64MB 启发式 (L722/L713)、(e) Deviation [min,max] 区间 (L549)、(f) budget 512MB 外部常量 (L722)。**无任何归一规则** ⇒ AUDIT002/003 数值与代数不统一（open，高）。

**数学性质 / 证明状态**：
- **(PO-I50-a) AUDIT002/003 数值口径未统一（open，高）**：AUDIT002 的 +10 与 AUDIT003 的 64MB/512MB 均不源自 §3.3 的 size 代数，且与 Deviation 单位脱节 ⇒ 报警数字不可信、不可对账。状态 = open（高，交叉 iter02/26/46、iter14/36 DO-7）。
- 文档行号：§12.2（L713-722）、§3.1.1（L78-86）、§3.3.1（L163-165）、§3.3.2（L167）、§9.1（L547-561）、ED-004（L527）。

### C2. [EffectOverride] 校验规则（#62-1）

**命题**（§8 ED-001 L524）：「核心 API 白名单（100 个），其他默认最大效应，允许 [EffectOverride] 修正」— 仅此一句，无定义。
**缺口**：
- [EffectOverride] 的**语法/参数**未定义（覆盖整个 Signature？单 Claim？按 resource？）。
- **可覆盖哪些冲突**未定义：能否压制 DO-9 泄漏报警（AUDIT002）、DO-7 量纲混算（iter36）、Compatible 并发冲突（iter16/25）？若可无差别压制，则它是「静默豁免一切」的逃逸通道。
- **是否经任何校验/审查**未定义：无「覆盖需理由」「覆盖上限」「CI 复核」⇒ 开发者可随意 [EffectOverride] 关掉所有报警 ⇒ 审计系统形同虚设。
- **与 [AcceptDeviation] 关系**未定义（见 C3）。

⇒ [EffectOverride] 是未定义校验规则的逃逸属性（open，高）。

**数学性质 / 证明状态**：
- **(PO-I50-b) [EffectOverride] 无校验规则（open，高）**：属性被 ED-001 当作「收敛方案」但未形式化，是 unvalidated escape hatch，可压制任意 DO 报警。状态 = open（高，交叉 iter10/iter42 推导层 soundness）。
- 文档行号：§8 ED-001（L524）。

### C3. [AcceptDeviation] 校验规则（#62-2）

**命题**（§9.3 L587）：校准流程选项 C「接受偏差（标注 [AcceptDeviation(0.3)]）」— 仅此一句，无定义。
**缺口**：
- **阈值语义**：标注 0.3，但 §9.1 报警阈值是 `deviation > 0.2f`（L547）。0.3 是「允许偏差上限」还是「覆盖 0.2 报警」？二者关系未定义 ⇒ 0.3>0.2 看似「放宽到 30%」，但 Deviation 公式本身遇 range=0 已 NaN/∞（iter33），0.3 对 NaN 仍恒假（不报警）⇒ 标注在退化情形无意义。
- **作用域**：局部（单对象）还是全局？能否压制 AUDIT002/003 编译期报警（C1）？未定义。
- **无上界**：[AcceptDeviation(99)] 是否合法？无最大值约束 ⇒ 可豁免任意大偏差。
- **与 [EffectOverride] 分工**未定义（C2）。

⇒ [AcceptDeviation] 是未定义校验规则的偏差豁免属性（open，高）。

**数学性质 / 证明状态**：
- **(PO-I50-c) [AcceptDeviation] 无校验规则（open，高）**：属性被 §9.3 当校准出口但未形式化，阈值/作用域/上界全缺，且依赖已退化的 Deviation 公式（iter33）。状态 = open（高，交叉 iter11/33、§9.1 L547）。
- 文档行号：§9.3（L587）、§9.1（L547-561）。

---

## Proof Obligation 账本（Iter50）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
|----|------|------|----------------|------|
| PO-I50-a | AUDIT002/003 数值口径未统一(≥5种size) | open(高) | 统一 size 口径(根10)+budget 入代数 | L713-722, L78-86, L163-167, L549 |
| PO-I50-b | [EffectOverride] 无校验规则(逃逸通道) | open(高) | 定义语法/可覆盖域/CI 复核 | L524 |
| PO-I50-c | [AcceptDeviation] 无校验规则(无上界) | open(高) | 定义阈值/作用域/上限+Deviation 修 | L587, L547 |
| PO-I50-d | §14「21收敛/0阻塞」声明不实 | open(高) | 回写「0收敛/≥8阻塞」 | L738-742, iter19/20 |

## 本轮新发现未消解缺口（I50- 前缀，全局唯一）
- **I50-01（高）**：AUDIT002「+10 per death」与 AUDIT003「64MB/512MB」为硬编码/启发式数字，与 §3.3 size 代数、Deviation 单位均不统一，至少 5 种 size 口径共存无归一（收口 #61，交叉 iter02/26/46/14/36）。
- **I50-02（高）**：[EffectOverride]（L524）仅 ED-001 一句提及、无校验规则，是可压制任意 DO 报警的未约束逃逸通道（收口 #62-1）。
- **I50-03（高）**：[AcceptDeviation(0.3)]（L587）仅 §9.3 选项 C 提及、无校验规则，阈值/作用域/上界全缺且依赖已退化 Deviation 公式（交叉 iter33）。
- **I50-04（高）**：§14（L738-742）「21 问题收敛/0 阻塞」与 30 轮实测（iter19/20 ~8% 良定义/~108 PO open/~40 高 I-）严重矛盾，需回写为「0 收敛/≥8 阻塞」。
- **I50-05（中）**：节A 根1(Claim=)+根2(⊆) 为代数层最深双根，下游 Peak/peak/net/Compat 四类过滤皆卡其上；根4(QueueFree)+根6(net(scope)) 为 DO-9 泄漏主线；根7(分桶)+根10(size 口径) 为数值主线（直接收口 #61）。

---

一句话摘要：全局回收给出 10 个高优先根因的阻塞依赖链（根1 Claim= 与根2 ⊆ 为最深双根，喂 Peak/peak/net/Compat；根4 QueueFree+根6 net(scope) 为 DO-9 泄漏主线；根7 分桶+根10 size 口径为数值主线），总账实测 ~8 高优先根因 open + ~108 PO open + ~40 高 I- 缺口（§14「21 收敛/0 阻塞」不实，I50-04），并收口 #61：AUDIT002「+10」/AUDIT003「64MB/512MB」与 §3.3 size 代数、Deviation 单位均不统一（≥5 种 size 口径，I50-01，高）；收口 #62：[EffectOverride](L524) 与 [AcceptDeviation(0.3)](L587) 均无校验规则（逃逸通道/无上界，I50-02/03，高）——三项遗留全部 open，无一消解。

// acceptance-report
{
  "criteriaSatisfied": [
    {"id": "criterion-1", "status": "satisfied", "evidence": "仅覆盖写入 audit/iter50.md，未读/改其它 audit/*.md，聚焦全局回收+总账+AUDIT002/003 与 EffectOverride 收口，未 widening scope"},
    {"id": "criterion-2", "status": "satisfied", "evidence": "文件含 header「独立审计 #50（hy3 单独进程）」、节A(阻塞依赖链)/节B(总账量化)/节C(AUDIT002/003+EffectOverride/AcceptDeviation)、Proof Obligation 账本(PO-I50-a~d)、I50- 缺口列表；交叉引用真实行号(L524/L587/L713-722/L738-742/L78-86/L163-167/L547-561/L527) 并经 read 确认 §14/§12.2/§8/§9 真实文本"}
  ],
  "changedFiles": ["audit/iter50.md"],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {"command": "read PDR (offset 510, 90)", "result": "passed", "summary": "读取 §8 推导层与 §9 运行时层确认 [EffectOverride](L524)/[AcceptDeviation](L587) 仅一句提及无规则"},
    {"command": "read PDR (offset 700, 90)", "result": "passed", "summary": "读取 §14 收敛声明(L738-742)与 §12.2 AUDIT002/003(L713-722)确认数值口径冲突"},
    {"command": "read PDR (offset 160, 12)", "result": "passed", "summary": "读取 §3.3.1 net/§3.3.2 peak 确认 size 载体与 AUDIT 数字不统一"},
    {"command": "grep PDR EffectOverride|AcceptDeviation|AUDIT002|AUDIT003", "result": "passed", "summary": "定位属性与报警行号(L524/L587/L717/L722)"},
    {"command": "write D:/Godot/Cosmos/audit/iter50.md", "result": "passed", "summary": "覆盖写入独立审计 #50"}
  ],
  "validationOutput": ["header 含「独立审计 #50（hy3 单独进程）」", "节A 10 根因依赖链 + 节B 总账量化表(~8高优先根因/~108 PO open/~40高 I-) + 节C AUDIT002/003 与 EffectOverride/AcceptDeviation 收口", "交叉引用 §14/§12.2/§8/§9/§3.1/§3.3 真实行号"],
  "residualRisks": ["未读取 iter21-49 文件（依任务约束禁止），全局回收基于编号交叉引用与 §14/§12.2/§8/§9 实测，可能遗漏个别 iters 的细粒度 PO 计数", "AUDIT002「+10」的具体来源假设为硬编码字面量（基于 L717 文本，未运行源码验证）"],
  "noStagedFiles": true,
  "diffSummary": "覆盖写入 audit/iter50.md，独立审计 #50：细粒度全局回收(10 根因依赖链)+总账量化(§14 不实)+收口 AUDIT002/003 数值口径与 [EffectOverride]/[AcceptDeviation] 校验",
  "reviewFindings": ["blocker: 无——本文件为审计产物不修改 PDR；但发现 §14 收敛声明不实、AUDIT002/003 数值不统一、[EffectOverride]/[AcceptDeviation] 无校验规则，需 PDR 侧回写收敛状态并定义属性校验"],
  "manualNotes": "纯文档审计，未改动 PDR 正文；依任务约束未读其它 audit 文件，仅基于 PDR 真实文本与本任务给定编号作交叉引用；所有行号基于本轮 PDR 实际 read"
}
