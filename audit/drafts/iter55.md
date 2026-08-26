# Iter55 独立审计

> 独立 PDR 形式审计员，全新隔离进程，与其他轮次无共享上下文。
> 被审文档：`D:/Godot/Cosmos/PDR_Effect_Cost_Algebra_v3_FINAL.md`（磁盘版本，1101 行，v3.0-FINAL-rA6 历史）。
> 本轮范围：**全文**（用户指令未附带子范围，按全文覆盖执行）。纪律：结论仅基于该文档磁盘内容。

---

## 一、范围与结论摘要

审计对象为 §1–§14 全部数学定义、组合律、派生度量、API 映射、推导规则、运行时与工具层判据。

**总结论**：rA2–rA6 修订确实闭合了此前账本中的多数「定义缺失」类缺口（Claim 相等、⊤ 代数、weight、Deviation 除零、Compatible 全函数性等——见 §四 discharged 项）。但本轮发现一个**贯穿全文的结构性阻塞缺口**：文档以 `Set<Claim>`（幂等并）作为唯一效应载体，而 net/Peak/S×ω/场景预算累加全部依赖**多重性（multiplicity）计数**——集合语义天然丢弃重复元素，使这些度量的算术在自身定义下不成立。此外发现 ⊔ 配对键自吞、ScopeId 偏序自相矛盾、mode 赋值策略掏空 Compatible 冲突检测三个高优先缺口，及若干中低优先问题。

统计（本轮 PO 账本，§三）：
| 状态 | 数量 |
| ------ | ------ |
| open（含 3 个阻塞级） | 10 |
| asserted（声明未证） | 2 |
| discharged（本轮确认已证/良定义） | 5 |

---

## 二、逐命题审计

### F1【阻塞】集合幂等性 vs 多重性计数的全局矛盾

| 项 | 内容 |
| ---- | ------ |
| 命题 | Signature := ImmutableHashSet<Claim>（L166）；(S₁;S₂)=S₁∪S₂ 且「∪ 幂等由 Claim 相等保证」（L252–256）；同时 AUDIT003 场景累加「20 个 Enemy → occupy{memory,[64,64]}×20 = [1280,1280]」（L936, L954–956）、S×ω := Σ_{i=1..ω} copy_i(S)（L293–297）、net(S)=Σ_{c∈S,...} c.size（L312, L317–318） |
| 数学性质 | 集合并幂等 ⇔ 重复元素只计一次。而 ×20 累加、循环副本求和、net 按条目求和均要求同一 (kind,resource,mode,scope,size) 的 Claim **按出现次数重复计入** |
| 状态 | **open（阻塞级）** |
| 反例 | 场景放置 20 个 Enemy（非代码循环，S×ω 不适用），各产生 occupy{Memory(uid="mem"),[64,64],create,Shell}。20 次 ∪ 后 Signature 仅含 1 条该 Claim（五元组逐字段相等，L169–172），net(S)=64 而非 AUDIT003 声称的 1280。**预算累加在文档自己的代数下不成立** |
| 行号 | L163–166, L169–192, L252–256, L293–301, L312–321, L936, L954–956 |

根因：v1.0「以 Set<Claim> 替代区间」解决了 MA-004 的概念混淆，但把**计数域**一并丢掉了。MA-004 表格行（L357「net 和 peak 是派生度量，非原语」）的收敛方案在多重资源场景下不真。最小补充：将效应载体升级为 `Multiset<Claim>`（或保留 Set 但给 Claim 增加 occurrence 维度/分配站点标识），并重新推导 ∪/⊔/Peak/net 的幂等性质——注意 multiset 并不再是幂等的，§3.2.1 的「统一组合律」表述需相应改写。

### F2【阻塞】S×ω 中 ω 实际不进入任何度量

| 项 | 内容 |
| ---- | ------ |
| 命题 | (S × ω) := Σ_{i=1..ω} copy_i(S)，copy_i 为 S 的副本（L295–297）；Peak(S,scope)=max_{i∈1..ω} Σ_{c∈copy_i(S),...} c.size（L327, L337） |
| 数学性质 | 若 copy_i 是同构副本（仅 scope 标注差异），则各 copy_i 的 size-和相等 ⇒ max_i 退化为单副本和，**ω 既不乘进 Peak 也不影响 net**。循环内每轮 Instantiate 不释放的真实峰值应为 i·s（随 i 增长），公式只能给出 s |
| 状态 | **open（阻塞级）**；DO-8（峰值检测）在此定义下无法成立 |
| 论证 | 设 s=[64,64]，ω=10，S 含一条 occupy create。copy_1..copy_10 的 Σ 均=64 ⇒ Peak=64。真实并发占用（若副本共存）为 640。max 算子选不出增长量；除非 copy_i 携带不同的 resource/scope 标识且定义「副本共存时如何聚合」，否则 ω 是死变量（仅 ⊤ 特判分支起作用，L301） |
| 行号 | L293–301, L326–338 |

与 F1 同根：缺少「副本即不同实例」的多重性语义。消解 F1（multiset 化）时须同步定义 copy_i 的区分键（如 Loop 迭代索引注入 scope/resource），否则 ω 仍不入账。

### F3【高】⊔ 配对键含 size 字段，merge_I 永不生效（自吞定义）

| 项 | 内容 |
| ---- | ------ |
| 命题 | ⊔ 按 **Claim 相等（3.1.4a）** 配对 S₁/S₂ 中同一 Claim，再对 size 区间取 merge_I（L286–289）；Claim₁=Claim₂ 要求 size 按 3.1.5b 区间相等（L172, L232–233） |
| 数学性质 | Claim= 包含 size ⇒ size 不同的两条跨分支 Claim **不相等 ⇒ 不被配对 ⇒ merge_I 输入永不存在**；能配上对的恰是 [s,s] 与 [s,s]（merge 结果仍 [s,s]），merge_I 退化为恒等。条件组合的核心功能（if 分支取 size 区间并集）在其自身定义下不可达 |
| 状态 | **open（高优先，但修复机械）** |
| 反例 | if b then occupy(memory,[64,64]) else occupy(memory,[128,128])：两 Claim 因 size 不等不配对，⊔ 输出为两条独立 Claim 而非声明的 [64,128] |
| 最小补充 | 配对键改为四元组投影 (kind,resource,mode,scope)，size 不参与配对、只参与合并 |
| 行号 | L282–291, L169–173, L232–236 |

### F4【高】ScopeId 偏序自相矛盾 + Shell 构造子缺席 + 嵌套作用域不可比

| 项 | 内容 |
| ---- | ------ |
| 命题 | 3.1.3b：Global ⊑_any X **且** X ⊑_any Global（L154–155），随后声称「⊑ 自反、反对称、传递 ⇒ ⊆* 为偏序；Global 为最大元」（L159） |
| 数学性质 | Global ⊑ Method(m) ∧ Method(m) ⊑ Global 而 Global ≠ Method(m) ⇒ **反对称被两条包含层次规则直接违反**。⊑ 至多是预序；「偏序 + Global 最大元」的声明为假（最大元要求 X⊑Global 且不反向）。若要保留双向可比，需取预序的商集（则 Global 与一切具体 scope 同伦，过滤谓词失去区分力）或改单向包含 |
| 附带缺口 (a) | rA2 新增 `Shell` 构造子（L136），但基础偏序表（L146–152）**未列 Shell ⊑ Shell**；(Shell,Shell) 对查表无定义，与「按上表机械查表即可，无未定义项」的声明矛盾。§7 全表所有 Claim 的 scope 都是 shell_scope ⇒ Shell（ST-04），即**当前全部 API 映射的 scope 过滤落在未定义对上** |
| 附带缺口 (b) | Loop(id) 与外层 Method(m)/Type(t) 不可比（跨标签不比较，L156）⇒ net(S,Method(m)) / Peak(S,Method(m)) 的过滤 c.scope⊆scope **排除方法体内循环里的 Claim** ⇒ DO-8/DO-9 在外层作用域聚合时系统性低估。需要嵌套闭包（如 Loop(id) 记录宿主链，或定义 Loop(id)⊑enclosing） |
| 附带瑕疵 | ⊆* := (c.scope⊑scope) ∨ (scope=Global) 中第二析取支冗余（X⊑_any Global 已蕴含） |
| 状态 | **open（阻塞级：Peak/net 的核心过滤谓词在主用例上未定义或不自洽）** |
| 行号 | L141–160, L128–138 |

### F5【高】mode 赋值策略掏空 Compatible 的冲突检测（A4 空转）

| 项 | 内容 |
| ---- | ------ |
| 命题 | CONFLICT := {(create,create),(move,move),(release,release)}（L273–274）；§7 全表中几乎所有写操作映射为 mode=**use**（如 Position setter `{write(self,"transform",use,shell_scope)}` L618，Rotation/Scale 同 L620–621，MoveAndSlide write(physics,...,use) L627 等） |
| 数学性质 | 两系统并行写同一 transform：双方 mode=use ⇒ use∨use ⇒ Compatible=true ⇒ 代数层判定无冲突。这与 §6.3 的调度器设计直接冲突：「MovementSystem writes Position / PhysicsSystem also writes Position → 自动标注 [RunAfter]」（L566–568）——同一情形在 §3.2.2 的并行约束下根本不触发 |
| 推论 | CONFLICT 三对中，§7 映射里 create/release 仅用于生命周期操作（AddChild/QueueFree/Play/Stop 等），move 已无任何映射使用（iter27 将 QueueFree 改 release 后全文无 move 实例）⇒ **A4「兼容冲突 COMPLETE」（L1031）在真实映射上几乎无可检对象**。MA-009 的「16 对全函数可机械验证」（L359）对模式代数本身为真（discharged），但对「检测真实写写竞争」这一隐含用途为空洞真 |
| 状态 | **open（高优先）**：需要么承认 Compatible 只管生命周期资源、写写竞争完全交给 L2 写集分析（则 §3.2.2 的并行约束对 §7 主表形同虚设应显式声明），要么修订 §7 的 mode 赋值策略（写操作 ≠ use，或引入 exclusive 权限档） |
| 行号 | L265–280, L618–621, L627, L566–568, L1031, L357–359 |

### F6【高】net 公式未实现其声明的 Unknown fail-closed

| 项 | 内容 |
| ---- | ------ |
| 命题 | §8.1 默认规则产 mode=Unknown claim，并声明「Unknown mode 在 net 中按 3.3.1 上界处理」「未知映射在 net 中计为 ⊤ 上界，触发需人工确认而非静默漏报」（L697–700, L320–321） |
| 数学性质 | net(S,scope) 只对 mode∈{create,move} 取正、mode=release 取负（L312–318）；mode=Unknown 的项**不在任何求和中出现，贡献恒为 0**，既非 ⊤ 也非人工确认标志。声明的 fail-closed 在公式层面未落地 ⇒ 未映射 API 的 occupy 仍静默漏报，DO-9 的双误修正只完成了一半（release-class 白名单侧闭合，默认规则侧未闭合） |
| 状态 | **open（高优先，修复机械）**：在 net 定义中显式加入分支「∃c: kind=occupy ∧ mode=Unknown ∧ c.scope⊆scope ⇒ net:=⊤」，或等价的哨兵传播规则 |
| 行号 | L309–321, L692–713 |

### F7【中】归一化常量 uid 抹平资源实例身份（跨信号/跨纹理互相抵消）

| 项 | 内容 |
| ---- | ------ |
| 命题 | 裸名缩写映射：memory ⇒ Memory(uid="mem")、callback ⇒ Callback("cb")（L185, L188） |
| 数学性质 | 所有回调占用坍缩到单一资源 Callback("cb")。反例：`Connect(sigA)` 产 occupy(Callback("cb"),[8,8],create,Shell)；随后无关的 `Disconnect(sigB)` 产 occupy(Callback("cb"),[8,8],release,Shell)。二者除 mode 外逐字段相等 ⇒ net(S,scope) = +8−8 = 0 ⇒ **sigA 的回调泄漏被 sigB 的正常释放精确抵消（假阴性）**。同理，两张各占 64MB 的纹理经 memory⇒Memory(uid="mem") 归一后互相抵消/合并 |
| 状态 | **open（中优先）**：缩写映射应参数化（callback ⇒ Callback(id=调用点哈希) 等），或在 Claim= 中保留分配站点维度 |
| 行号 | L185–191, L657–662 |

### F8【中】release-class 白名单含语义可疑条目，「源码核对」不可证

| 项 | 内容 |
| ---- | ------ |
| 命题 | release-class = { queue_free, free, remove_child, disconnect, remove_from_group, **cancel_free**, **free_children_in_group** }，标注「godotengine/godot 源码核对，2026-08-20 实测枚举」（L703–713） |
| 论证 | (a) `cancel_free()` 的引擎语义是**取消**挂起的 queue_free、使节点继续存活——把它归入 release-class 会 emit release claim，令占用被少计，恰好**掩盖**它所取消的那次释放对应的泄漏路径；方向性疑似写反（文档括注「释放 pending 占用」所指的资源在 Claim 代数中不存在）。(b) `free_children_in_group(String)` 不是 Godot 4.x 公开 Node API（Node 只有 remove_from_group/get_children 等），本审计员离线无法复核该「源码实测」；(c) remove_from_group 是否真的伴随可释占用（group 成员关系非堆分配）亦属 asserted |
| 状态 | **asserted（其中 cancel_free 为 suspected-wrong，建议降级为「待源码复核」并把 cancel_free 移出 release-class 或改标 neutral）** |
| 行号 | L703–713 |

### F9【低】Signature(b) 未定义；copy_i 的 scope 标注规则歧义

- `(if b then S₁ else S₂) = Signature(b) ∪ ...`（L284）与 `(while b do S) = Signature(b) ∪ (S×ω)`（L298）中的 **Signature(b)** 从未定义：b 是纯 Bool 谓词还是可能带效应的条件求值？定义域/值域均悬空。最小补充：Signature(b) := ∅（谓词纯）或给出条件求值的 Claim 规则。
- copy_i「scope 标注为所在 Loop(id) 或 Global」（L295–296）未说明是**替换**原 Claim.scope 还是叠加注记；两种读法下 net(S,scope) 的过滤结果不同。状态：open（低，修复机械）。

### F10【低】SizeVal 区间上的减法与序未定义，net 值域悬空

- net 是「正和 − 负和」（L312–318）：SizeVal 只定义了 +/×/max/min/compare 对 ⊤ 的扩张律（L221–231）与区间 merge_I（L232–236），**区间减法 [a,b]−[c,d]、负值表示、以及 net>0 判定所用的区间序（[lo,hi]>0? 按 lo 还是 hi？）均未定义**。compare 律（L229–230）只规定了与 ⊤ 的比较。AUDIT003 的 `[1280,1280] > GlobalBudget [512,512]`（L956）同样依赖未定义的区间全序。
- 状态：open（低-中；net 可为负这一点还与 ℕ\* 载体冲突，需引入 ℤ 或有符号区间）。

### F11【文档级】结构与同步缺陷（rA5/rA6 自述与磁盘不符处）

| 发现 | 证据 | 状态 |
| ---- | ------ | ------ |
| 双 `## 14` 标题仍并存：「编译期工具层完备性规范」（L1000）与「文档历史」（L1086）。rA6 自述「现仅剩 ## 14.文档历史」，实际是删了 `## 15` 但把文档历史留在 14 号，与工具层 14 号**编号冲突未解决** | L1000, L1086 | open（编辑类） |
| §8.2 审计发现表被 §8.3 正文从中间截断：ED-001 行之后插入整个 8.3 节，ED-002..ED-008 七行脱离表头悬浮（markdown 渲染为断裂表格） | L723–744, L745–752 | open（编辑类） |
| 术语表 ResourceId 行漏 rA4 新增的 Occupancy/Callback/Input 构造子，且仍列已被 SignalBus 归一的裸 "signal"；ScopeId 行漏 ST-04 新增的 Shell | L981–982 vs L112–124, L128–137 | open（编辑类） |
| ED-004 收敛方案仍写「动态 Instantiate 变量场景标记为 ∞」，未同步 §3.1.5 的 [1,⊤] 口径 | L750 | open（编辑类） |
| §3.1.2 定义块末尾连续两个代码围栏 ``` ```，渲染出空代码块 | L111–113 | open（编辑类） |

### F12【asserted】§14 工具层完备性判据的控制流前提未证

- A1「泄漏检测 COMPLETE」：Instantiate 后控制流无 release ⇒ 必报。事件回调配对（`fx.Finished += () => fx.QueueFree()`，L951/L1057）中「Finished 恰触发一次」、try/catch 与早退路径下的释放可达性，均超出直线控制流，文档未给出判据成立的程序类限制。状态：asserted（文档自己诚实标注「实现层测试全绿方视为已证」L1064–1066，与本审计一致：当前非 discharged）。
- A2「峰值检测 COMPLETE」：循环体内分配 ⇒ 必报。对 ω 有界且很小的循环同样报警 ⇒ 其 soundness（不冤枉）一侧反而存疑，文档未讨论。状态：asserted。
- A5 保守 SOUND 与 F6 相关：Unknown 落人工确认的前提是 net/Compatible 真的处理 Unknown（见 F5/F6），当前链条有一环缺失。

---

## 三、Proof Obligation 账本（本轮）

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ------ | ------ |
| PO-55-01 | 效应载体支持多重性计数（net/Peak/×20 累加可计重） | **open·阻塞** | Set<Claim> → Multiset<Claim> 或增加分配站点/occurrence 维度；重推 ∪/⊔ 幂等性质 | L166, L252, L936 |
| PO-55-02 | ω 进入度量的合法路径（DO-8 成立） | **open·阻塞** | 定义 copy_i 区分键（迭代索引注入 scope/resource）+ 副本共存聚合规则 | L293–301, L326–338 |
| PO-55-03 | ScopeId 偏序自洽且覆盖 Shell/嵌套 | **open·阻塞** | 改预序+商集或单向包含矩阵；补 Shell⊑Shell；定义 Loop(id)⊑enclosing 闭包 | L141–160 |
| PO-55-04 | ⊔ 能真正合并跨分支 size | open | 配对键改 (kind,resource,mode,scope) 投影 | L282–291 |
| PO-55-05 | Compatible 对真实写写竞争有区分力 | open | 修订 §7 mode 赋值（写≠use）或显式声明冲突检测移交 L2 写集分析 | L273, L618, L566 |
| PO-55-06 | net 对 mode=Unknown 实现 ⊤ fail-closed | open | net 定义加 Unknown 哨兵分支 | L312, L320 |
| PO-55-07 | SizeVal 减法/负值/预算比较序 | open | 引入 ℤ 或有符号区间；定义 [a,b]>[c,d] 判定 | L312, L956 |
| PO-55-08 | 归一化保持实例身份 | open | callback/memory 缩写映射参数化 | L185–188 |
| PO-55-09 | release-class 清单权威性（cancel_free 方向、free_children_in_group 存在性） | asserted | 重核 godotengine/godot node.cpp/Object.cpp 并给出函数签名级引用 | L703–713 |
| PO-55-10 | Signature(b) 良定义 | open | 定义为 ∅（纯谓词）或给出条件求值效应 | L284, L298 |
| PO-55-11 | copy_i scope 标注：替换 or 叠加 | open | 二选一并写明 | L295 |
| PO-55-12 | 文档结构：双 ##14、断表、术语表同步、ED-004 ∞ 残留、双围栏 | open | 纯编辑修复 | L1000, L1086, L745, L981, L750, L111 |
| PO-55-13 | A1/A2 完备性的程序类前提（事件单触发、异常路径、有界循环豁免） | asserted | 给出判据适用的控制流片段类定义，或降级为 partial-complete | L1029–1032 |
| PO-55-14 | ⊤ 扩张运算律（+/×/max/min/compare 闭合、0×⊤=⊤ 保守约定自洽） | discharged | —（定义完备，约定明确标注） | L221–231 |
| PO-55-15 | Compatible 作为纯 mode 代数：全函数 + 对称 + CONFLICT 补集刻画 | discharged | —（P1/P2 可机械验证；其实际检测力另见 PO-55-05） | L265–280 |
| PO-55-16 | weight : Kind×Kind→ℝ∪{⊥} 显式定义 + KIND_MIX 编译期联动 | discharged | —（同 kind=1/跨 kind=⊥ 与 3.1.4b 分桶一致） | L330–339 |
| PO-55-17 | Deviation 除零保护与 DeviationVal ⊤ 载体 | discharged | —（ε=1 下界 + 先判 ⊕ 再比 double 链路闭合；项级/总和的 Σ 歧义记为瑕疵不计 PO） | L239–247, L776–800 |
| PO-55-18 | Claim 五元组相等 + Unknown 等式（∪ 幂等的定义基础） | discharged | —（作为定义自洽；其多重性后果另见 PO-55-01） | L169–192 |

---

## 四、本轮新发现缺口清单（按严重度）

1. **[阻塞]** 集合幂等 vs 多重计数：AUDIT003 的 ×20、net 的逐条求和、S×ω 的副本求和在 `ImmutableHashSet` 语义下均不成立（PO-55-01）。
2. **[阻塞]** ω 是死变量：Peak 的 max-over-identical-copies 退化为单副本和，DO-8 无数学基础（PO-55-02）。
3. **[阻塞]** ScopeId ⊑ 双向包含违反其自身声称的反对称；(Shell,Shell) 未列于偏序表而 §7 全表 scope=Shell；Loop 内容被外层聚合过滤排除（PO-55-03）。
4. **[高]** ⊔ 以含 size 的 Claim= 作配对键，merge_I 永不生效（PO-55-04）。
5. **[高]** §7 把几乎所有 write 标成 mode=use，CONFLICT 集在真实映射上近不可实例化，A4 判据空转，与 §6.3 调度示例矛盾（PO-55-05）。
6. **[高]** net 公式未实现声明的 mode=Unknown → ⊤ fail-closed，默认规则侧漏报依旧（PO-55-06）。
7. **[中]** memory/callback 常量 uid 归一造成跨实例抵消，具体反例：Connect(sigA)+Disconnect(sigB) net=0 掩盖泄漏（PO-55-08）。
8. **[中]** cancel_free 入 release-class 方向疑似相反；free_children_in_group 存在性不可证（PO-55-09）。
9. **[低]** Signature(b)、copy_i 标注、区间减法/序、双 ##14、断表、术语表失同步（PO-55-10/11/07/12）。
10. **[记录]** §14 A1/A2 的 COMPLETE 目前是 asserted 而非 discharged——文档自身的「测试全绿方为已证」条款尚未满足，任何「工具层完备性已收口」的表述应继续保留「依赖实现」限定（与 rA4–rA6 历史行的说法一致）。

## 五、对「收敛声明」的总体裁定

rA 系列历史行声称「10 根因全部给出可机械执行的良性定义；数学层阻塞 0 个」（L1097–1098）。本轮裁定：**该声明不成立于「阻塞 0 个」部分**——PO-55-01/02/03 为数学层新识别的阻塞级缺口（其中 PO-55-01 是 v1.0 架构决策「Set<Claim>+∪」的内生代价，此前各轮审计聚焦定义缺失而未击中多重性问题这一根因）。其余 7 个根因的良性定义修补属实（discharged 项 PO-55-14..18 可复核）。

*审计员：ox-alpha（独立隔离进程）。报告完。*
