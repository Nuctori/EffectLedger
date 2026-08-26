# Iter12 独立审计

**审计对象**：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（v3.0-FINAL-rA6，1100 行）
**范围**：仅 §10 风险评估 R-1..R-12（L833–848）及其缓解措施在全文中的锚点；不读取 audit/ 下其他文件，结论全部基于本文档独立核验。
**方法**：逐条核查「缓解措施是否命中根因」，区分 症状层缓解 / TODO 兜底 / 根因消除；对结构性成立者给条件证明并写明前提，对缺口给最小补充方案。

---

## 结论摘要

- **总体**：R-1/R-5/R-7/R-10 属流程型风险，缓解措施与根因对齐，状态为条件 discharged（前提是 CI/Benchmark 真实运行——文档未证）。R-2/R-6 缓解部分悬空（无量化预算 / 无强制机制）。
- **高概率×高影响四项均存在缓解薄弱处**：
  - **R-3**：四项缓解中 `[SuppressEffect]` 是**未定义属性**（全文唯一出现在 L839），与 §8.3.1 已收口的 `[EffectOverride]`（L727–735）构成文档内部矛盾——一个无约束的 suppress 属性正是 iter50 #62 要封死的逃逸通道。且误报率无量化验收指标。
  - **R-4**：「迁移工具」被声称但 §11 工作量表只有「迁移教程」（L863），无工具条目、无规格 → asserted。
  - **R-11**：与 R-4 共享同一核心缓解（Audit-only 零门槛），培训 2–4 周无资源/验收锚点 → asserted。
  - **R-8/R-9**：缓解均为「Benchmark 验证」= 延迟到实现的 TODO 兜底；且发现 **§2.2 决策（SoA + swap-remove 可变存储，L70/L403）与 §4.1.5 规范定义（ImmutableDictionary 不可变 World，L394/L409）内部矛盾**——R-9 的根因（不可变结构每写路径复制 O(log n)）仍写在规范层，缓解只是"评估切换"。
  - **R-12**：静态 60fps 假设 + 运行时校准，但校准通道是 `Conditional("DEBUG")`（§9.2），**Release 下校准闭环不存在**，帧率偏差误差无界 → open。
- **新发现具体反例（N-1）**：§7.1 `QueueFree()` 的 release Claim 资源标识为 `memory, self.size`（L610），而 §7.4 `Instantiate(scene)` 的 create Claim 为 `memory, scene.uid`（L639）。按 §3.1.4a 逐字段相等判定，`Memory(uid=self.size)` ≢ `Memory(uid=scene.uid)` ⇒ **修复示例（§12.2 L951 "加 QueueFree 后泄漏消解"）按规范机械执行不成立**，将产生 AUDIT002 误报——这正是 R-3 担心的误报率的一个规范性来源。

---

## 逐命题小节

### R-1 Godot C# 模块版本兼容性（L837）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 锁定 Godot 4.2+ / .NET 8+ + CI 矩阵可消除版本兼容风险 |
| 数学性质 | 无代数性质可言；属流程型命题。相关形式对象：§7 映射表为 API→Claim 的全函数（每行给出完整 Claim 集），版本变动破坏的是该函数的定义域覆盖 |
| 状态 | **discharged（条件证明）** |
| 论证 | 条件证明：若 (i) CI 矩阵实际包含所有声明的版本组合并运行 §14.4 测试矩阵，(ii) 任一 API 行为变化导致 Claim 映射失效时测试必红（映射表测试化，见 R-5），则版本漂移在合并前被检出。前提 (i)(ii) 文档仅声明未证（TS-002 L583 仅文档化最低版本）。锁版本本身直接命中根因（消除不确定性来源而非事后补救） |
| 行号 | L837；L26（DO 验收）；L583（TS-002） |

### R-2 Source Generator 编译性能下降（L838）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 增量生成 + 缓存语法树 + 限制扫描范围可将编译开销控制在可接受范围 |
| 数学性质 | 增量生成的正确性依赖 Roslyn IncrementalValuesProvider 的缓存键等价性（工程性质，非本文档形式系统内对象） |
| 状态 | **asserted** |
| 论证 | 三项措施是 Roslyn 社区标准实践，方向正确，但：(i) 「可接受」无量化定义（无编译时间预算上限，如 ≤X% 基线）；(ii) DO 验收表（§1）不含性能验收条目。缓解措施命中手段但缺停止判据——无法判定何时算"缓解成功"。最小补充：在 §14 增加 S-perf 判据「全量 build 增量 ≤ N% vs 无 Generator 基线」，纳入 CI |
| 行号 | L838 |

### R-3 Analyzer 误报率过高【高概率×高影响】（L839）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 分级报警 + [SuppressEffect] + 持续校准 + 白名单机制可将误报率控制在团队可容忍水平 |
| 数学性质 | 相关形式基础：(i) Compatible 全函数+对称（§3.2.3 P1/P2，L249–260）使冲突报警可机械判定；(ii) weight 同 kind=1/跨 kind=⊥（§3.3.2，L341–344）使 KIND_MIX 判定完备；(iii) Unknown fail-closed（§8.1 默认规则，L701–706）保证未知调用不冤枉。三者均为 R-3 提供了**报警侧的 soundness 结构支撑** |
| 状态 | **open（部分缓解，一处缓解指向不存在的对象）** |
| 论证 | (a) 「分级报警」命中根因：L68 决策 warning 默认 + CI 可配 error，DO-1 L16 支持。(b) 「白名单机制」命中根因：§7 全表 + §8.1 release-class 权威清单（L712）+ 未入白名单回落 Unknown fail-closed（L713）。(c) 「持续校准」命中症状→反馈环：§9.3 校准流程闭合。(d) **`[SuppressEffect]` 未定义**：全文唯一出现于 L839；§8.3.1 定义的是 `[EffectOverride]`（L727–735，含 reason 强制、CI approve、禁止压制 kind/DO-9）。二者并存构成内部矛盾：若 R-3 允许 SuppressEffect，则 §8.3.1 的「不得本地私自豁免」审查约束可被绕过，等于重新打开 iter50 #62 封死的逃逸通道。**内部矛盾明确指出**。(e) 误报率无量化目标：阶段1 目标「1000 下载，收集误报反馈」（L878）只是反馈渠道，非速率界。另见新缺口 N-1：QueueFree/Instantiate 资源标识不对齐本身就是一条规范级误报路径。Proof obligation 见 PO-R3-a/b |
| 行号 | L839；L16；L68；L249–260；L341–344；L701–713；L727–735；L878 |

### R-4 团队抗拒分层架构【高概率×高影响】（L840）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 渐进式采用 + Audit-only 零改动入口 + 迁移工具 + 培训可化解架构抗拒 |
| 数学性质 | 不适用（组织性风险）。形式侧对应物：阶段1 Audit-only 的报警正确性完全由编译期数学层承载（§3 + §14.3 A1–A5），不需要用户改代码，故「零改动入口」在结构上成立 |
| 状态 | **open（阶段1 缓解 discharged，阶段3/4 缓解 asserted）** |
| 论证 | 抗拒的根因 = 采用成本。Audit-only（DO-11 L26，§12.1 阶段1 L873–878）确实把阶段1 边际成本降为零，命中阶段1 根因。但抗拒的实际发生点是阶段3/4（重写 Entity/System，L889–897）：此处的缓解只剩「培训」「迁移工具」，而 (i) 「迁移工具」在 §11 工作量表中**不存在对应条目**（仅 L863「迁移教程」2–3 人周），工具无规格、无人时；(ii) 培训无验收判据。即：最重的两个阶段的缓解是 TODO 兜底。最小补充：或删除「迁移工具」改为如实写「教程+手工迁移」，或在 §11 增列工具人时并给出输入/输出规格 |
| 行号 | L840；L26；L863；L873–897 |

### R-5 Godot API 变动导致映射表失效（L841）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 自动化测试覆盖核心 API + 版本升级重扫描 + 社区贡献可使映射表失效被及时检出/修复 |
| 数学性质 | 映射表是 API→Signaturê 的查表函数；其完备性边界已在 §14.2 S2 给出（直接调用 complete / 反射 incomplete，L1023） |
| 状态 | **discharged（条件证明）** |
| 论证 | 条件：release-class 白名单（L697–713）已枚举 7 个释放类 API 并要求「任一未入白名单 ⇒ 回落默认 Unknown 规则（fail-closed），不静默漏报」——这使映射失效的后果退化为「需人工确认」而非错误放行，即失效模式是有界的（fail-closed 性质，结构性成立，前提是 §8.1 默认规则实现忠实）。剩余前提「自动化测试覆盖核心 API」与 R-1 共享。「社区贡献」无治理机制定义（谁审核、如何回归）→ 该子项 asserted |
| 行号 | L841；L697–713；L1023 |

### R-6 struct 拷贝性能问题（L842）

| 项 | 内容 |
| ---- | ---- |
| 命题 | readonly record struct + <128 bytes 上限 + 大组件引用包装可控制拷贝开销 |
| 数学性质 | EA-007 已限 16 Component/Entity（L451）⇒ Entity 尺寸上界有结构来源；Component 字段类型白名单（§4.1.2，unmanaged/string/ImmutableArray/IComponent，L396–404）保证字段尺寸可静态求值（unmanaged 封送尺寸确定），故 sizeof(Component) 可计算 |
| 状态 | **asserted（上限存在但无强制机制）** |
| 论证 | (i) 「readonly record struct 编译器优化」不是保证：C# 中 readonly struct 消除防御拷贝是语言保证（此点成立），但 record 生成器对大结构的 == / GetHashCode 仍是逐字段比较，非零成本——措辞夸大。(ii) **<128 bytes 上限全文无规则 ID、无检查机制**：L2 字段白名单（§6.3 GEN001 类）查类型不查总尺寸，EA-007 的 16 个上限是数量不是字节。(iii) 「大 Component 用引用包装」与 §4.1.2 「禁止 class/嵌套对象」直接冲突，未说明经何豁免通道。最小补充：新增 Analyzer 规则 GEN00x「sizeof(Component)>128 ⇒ error」，并在 §4.1.2 写明引用包装的合法形态（如 ImmutableArray<T> 包装） |
| 行号 | L842；L396–404；L451 |

### R-7 跨平台构建差异（L843）

| 项 | 内容 |
| ---- | ---- |
| 命题 | CI 覆盖三平台 + Docker 统一环境可控制构建差异 |
| 数学性质 | 不适用 |
| 状态 | **discharged（条件证明）** |
| 论证 | 流程型标准做法；前提同 R-1（CI 实跑）。Docker 统一环境与「CI 覆盖 Linux/Windows/macOS」有轻微张力（macOS 无原生 Docker），工程上以 macOS runner 解决，不影响结论。Source Generator/Analyzer 本身跨平台（纯 .NET），差异面窄 |
| 行号 | L843 |

### R-8 Entity-as-Data 与 Scene Tree 同步性能【中概率×高影响】（L844）

| 项 | 内容 |
| ---- | ---- |
| 命题 | Delta Sync + 批量更新 + 避免每帧全量同步 + Benchmark 可保证同步性能 |
| 数学性质 | Delta := (Spawned, Modified, Destroyed)（§5.1.3，L494–496）。所需关键性质：**增量同步成本 ∝ |Delta| 而非 |World|**。成立前提是存在 per-entity/per-component 的脏标记或版本比较通道；World 定义仅有全局 version: U64 单调递增（L410），**无 per-entity 版本**，故从 (World, version) 到 Delta 的推导在最坏情况下需 diff 两棵实体树 = O(|World|)。Shell 约束「无循环」（L481）进一步限制 Shell 内做 diff 的合法性 |
| 状态 | **open** |
| 论证 | 文档声称 Delta Sync 但未定义 Delta 如何计算：System 是纯函数返回全新 World'，无变更记录随行；Command[] 只携带效应请求不携带「哪些 entity 变了」的完备清单（EmitEvent/Spawn/Destroy 有，SetComponent 有——部分可从 Command 推导，但直接改 World 的路径未排除）。Benchmark 验证 = 延迟兑现（TODO 兜底），TS-011 称「原型 Benchmark 验证已收敛」（L595）但原型不在本 PDR 交付范围内，收敛声明超前。Proof obligation PO-R8-a：补充「Delta 从 Command[] + World 版本比较推导，复杂度 Θ(|Commands| + |Destroyed|)」的定义并证明 |
| 行号 | L844；L47–48；L481–482；L410；L494–496；L595 |

### R-9 ImmutableDictionary 性能瓶颈【中概率×高影响】（L845）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 评估切换自定义 Archetype 存储 + Benchmark 对比可消除 ImmutableDictionary 瓶颈 |
| 数学性质 | 逻辑层需要持久化语义：World 是纯函数返回值 ⇒ 更新后旧 World 必须仍可用（观测不变性）。ImmutableDictionary 满足（路径复制 O(log n)）；SoA + swap-remove（§2.2 决策，L70；§4.1.4 L403）是**可变**存储，不满足，除非引入 epoch/version 间接层 |
| 状态 | **open（且缓解措施与规范定义矛盾）** |
| 论证 | **文档内部矛盾明确指出**：§2.2 关键决策表写「World 存储 = Archetype 分组 SoA，O(1) 查询，swap-remove」（L70），§4.1.5 规范定义写「World := (entities: ImmutableDictionary<...>, version)」（L409）。二者不能同时为真：swap-remove 使旧 World 失效，违反 System 纯函数语义（同一 World 输入两次须得同一输出，§4.1.6）。R-9 的缓解「评估切换」恰恰是在这两个已互相矛盾的声明之间摇摆——缓解没有命中根因（根因是规范层存储模型二义性），而是复述了矛盾。最小补充：定义双层模型——逻辑 World（持久化视图，供纯函数语义）+ 物理 World（SoA + epoch GC，供性能），并证明物理实现对逻辑视图的观测等价（读旧 World 得旧值）。在此之前 TS-011「已收敛」应降级为 open |
| 行号 | L845；L70；L394；L403；L409；L419–423（§4.1.6 纯函数约束）；L595 |

### R-10 Source Generator 生成代码可调试性（L846）

| 项 | 内容 |
| ---- | ---- |
| 命题 | [GeneratedCode] + [DebuggerNonUserCode] + Source Link 保证可调试性 |
| 数学性质 | 不适用 |
| 状态 | **discharged（条件证明，附措辞修正）** |
| 论证 | 三项是 C# 生态标准做法。[GeneratedCode] 影响分析器跳过（正确方向）；注意 [DebuggerNonUserCode] 的效果是**默认不步入**生成代码——这与「可调试性」目标相反半步：开发者怀疑生成代码出错时需手动启用显示。建议补一句「调试生成代码时关闭 Just My Code」。Source Link 对生成源码的意义有限（生成码非源仓库文件），真正有效的是 `#line` pragma 控制——文档未提。整体方向正确，残余为工程细节 |
| 行号 | L846 |

### R-11 团队函数式思维培训成本【高概率×高影响】（L847）

| 项 | 内容 |
| ---- | ---- |
| 命题 | Audit-only 零门槛 + 渐进式培训(2-4周) + 文档示例 + 代码审查可控制培训成本 |
| 数学性质 | 不适用（组织性）。形式侧：阶段1 报警全部可由 §14.4 测试矩阵承载（A1↔AUDIT002 等），用户无需理解 Claim 代数即可受益——「零门槛」的结构依据 |
| 状态 | **asserted** |
| 论证 | 与 R-4 高度重叠：两条高风险共用的实质缓解只有一个（Audit-only 渐进入口，§12.1 四阶段目标数 1000/500/50/10 是下载漏斗指标，非培训成效指标）。「2-4 周」无依据来源（无试点数据引用）；「代码审查」是通用手段未说明审查什么（EffectOverride reason？Signature 正确性？）。缓解薄弱处：若阶段3/4 采用停滞（与 R-4 同点），培训成本的根因（函数式范式转换）没有任何机制性缓解，只有人力投入承诺。最小补充：给出培训验收判据（如「受训者在示例项目上独立写出带 Signature 的 Command 通过 §14.4 反例测试」） |
| 行号 | L847；L863；L873–897 |

### R-12 Godot _Process 调用频率不确定（L848）

| 项 | 内容 |
| ---- | ---- |
| 命题 | 静态假设 60fps + 运行时采样校准 + [TargetFrameRate] 可界定帧率不确定性的影响 |
| 数学性质 | 效应累加与帧率的量纲关系：若每帧效应为 e，则每秒效应 = fps·e。静态假设 fps₀=60 时估计误差 = \|fps−60\|/60 相对误差，线性传播进 net/Peak 的秒归一量。size 载体 SizeVal 区间（§3.1.5）可容纳该误差为区间放大，但文档未做此建模 |
| 状态 | **open（Debug 闭环 / Release 开环）** |
| 论证 | (i) ED-006（L751）与 R-12 同文重复，缓解一致。(ii) 运行时校准通道位于 EffectValidator，整类被 `[Conditional("DEBUG")]`（§9.2）剥离 ⇒ **Release 构建下校准不存在**，静态 60fps 假设在真实 30fps 或 144fps 设备上的误差（50%~140%）无任何检出路径。开发机 Debug 帧率 ≠ 目标机 Release 帧率是常态，故校准环采到的样本本身有系统性偏差。(iii) 采样每 60 帧（L768）进一步压缩样本量，对突发峰值（Peak 型违规）检出概率低。(iv) `[TargetFrameRate]` 属性全文无语法定义（对比 §8.3 对两个属性给了完整定义）→ 悬空对象。最小补充：(a) 定义 [TargetFrameRate] 语法/作用域/默认值；(b) 在 net/Peak 的秒归一公式中显式引入 fps 因子并用区间 [fps_min, fps_max] 建模；(c) 说明 Release 下接受开环的理由或提供可选 Release 采样 |
| 行号 | L848；L751；L768；L793–801（§9.2 Conditional DEBUG 剥离） |

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ------ | ------ | ------ | ------ | ------ |
| PO-R2-a | Generator 编译开销可控 | open | §14 增 perf 判据：全量 build 时间 ≤ 无 Generator 基线的 (1+N%)，入 CI | L838 |
| PO-R3-a | [SuppressEffect] 合法性 | open | 二选一：删除 L839 的 [SuppressEffect] 改引 [EffectOverride]（继承 §8.3.1 全部审查约束）；或正式定义 [SuppressEffect] 并赋予同等 reason/approve 约束。当前状态为内部矛盾 | L839; L727–735 |
| PO-R3-b | 误报率有界 | open | 定义基准语料库（含 §14.4 全部正反例 + 真实项目样本）+ precision 量化目标（如 ≥95%），纳入阶段1验收替代「1000 下载」软指标 | L839; L878 |
| PO-R4-a | 「迁移工具」存在且可行 | open | 或在 §11 增列工具人时+输入输出规格；或改口为「迁移教程+手工迁移」。二选一，消除声称与资源的落差 | L840; L863 |
| PO-R6-a | Component ≤128B 可强制 | open | 新增 Analyzer 规则：sizeof(Component) > 128 ⇒ error（unmanaged 字段尺寸可静态求值，可行性由 §4.1.2 字段白名单保证）；同时消解「引用包装」与「禁止嵌套/class」的冲突表述 | L842; L396–404 |
| PO-R8-a | Delta 成本 ∝ \|Delta\| 非 \|World\| | open | 定义 Delta 推导规则：Delta = f(Command[], World.version, SceneTree 镜像)，给出复杂度上界 Θ(\|Commands\|+\|Destroyed\|)；或引入 per-component 版本号。当前全局单 U64 version 不够 | L844; L410; L494–496 |
| PO-R9-a | 存储模型观测等价 | open | 双层模型：逻辑 World（持久化语义）/ 物理 World（SoA+epoch），证观测等价（持旧 World 引用读取得旧值）。同时解决 §2.2(L70) 与 §4.1.5(L409) 的规范矛盾，TS-011 由「已收敛」降级 | L70; L409; L595 |
| PO-R12-a | 帧率误差有界 | open | 定义 [TargetFrameRate] 语法；net/Peak 秒归一显式含 fps∈[min,max] 区间因子；声明 Release 开环的可接受性理由 | L751; L793–801 |
| PO-R11-a | 培训成效可判定 | open | 给出验收判据（受训者独立完成 Signature 化示例并通过 §14.4 反例测试），替代无出处的「2-4 周」 | L847 |

已消解（条件 discharged，不需文档改动，仅需 CI 事实）：PO-R1-a（CI 矩阵实跑）、PO-R5-a（API 测试覆盖 + fail-closed 回落）、PO-R7-a（三平台实跑）、PO-R10-a（Just My Code 细节）。

---

## 本轮新发现缺口清单

| ID | 缺口 | 证据 | 影响 | 建议 |
| ------ | ------ | ------ | ------ | ------ |
| N-1 | **QueueFree 与 Instantiate 的 memory 资源标识不对齐**：§7.4 Instantiate emit `occupy(memory, scene.uid→Memory(uid=scene.uid), create)`（L639），§7.1 QueueFree emit `release(memory, self.size)` 即 Memory(uid=self.size)（L610）。按 §3.1.4a 五元组逐字段相等，二者 resource 字段不等 ⇒ net(S,scope) 中 create/release 无法配对抵消 | L610; L639; §3.1.4a | §12.2 修复示例「加 fx.QueueFree 后泄漏消解」（L951）与反例测试 NoFalsePositive_QueueFree_Present（§14.4）按规范机械执行**不成立** ⇒ AUDIT002 规范性误报，直接恶化 R-3 最担心的误报率 | 定义生命周期绑定规则：同一 Node 的 create/release memory Claim 使用同一 uid（如 Memory(uid(node))），QueueFree 映射改为 `release(memory, node_uid, release)`，size 取 Instantiate 时的 estimated_size |
| N-2 | **存储模型二义性矛盾**：§2.2 决策「SoA + swap-remove（可变）」vs §4.1.5「ImmutableDictionary（不可变）」互斥 | L70 vs L409 | R-8/R-9 的缓解在这两个矛盾声明间摇摆，无法命中根因；TS-011「已收敛」声明超前 | 见 PO-R9-a 双层模型方案 |
| N-3 | **[SuppressEffect] 幽灵属性**：R-3 引用全文唯一的该名称，§8.3 收口的却是 [EffectOverride] | L839 vs L727–735 | 若按 R-3 实现 suppress，将绕过 §8.3.1 的 reason/approve 审查，重开逃逸通道 | 见 PO-R3-a |
| N-4 | **[TargetFrameRate] 悬空**：ED-006/R-12 两处引用，无语法定义（对照 §8.3 为另两属性给出了完整定义） | L751; L848 | 实现者无从得知作用域/默认值/与 60fps 静态假设的关系 | 见 PO-R12-a |
| N-5 | **Release 下校准开环未被风险评估收录**：§9.2 Conditional("DEBUG") 剥离使 R-12 的「运行时采样校准」在发布形态不存在，R-12 表述暗示闭环恒在 | L848 vs L793–801 | 帧率假设误差在最终运行环境无检出路径 | R-12 缓解列增补「Release 采样可选开关」或明示接受开环及理由 |

---

*审计员：iter12 独立进程。除目标文档外未读取任何其他项目文件；所有行号以 v3.0-FINAL-rA6（1100 行）为准。*
