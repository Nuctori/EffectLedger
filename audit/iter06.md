# Iter06 独立审计

审计对象：`PDR_Effect_Cost_Algebra_v3_FINAL.md`（磁盘内容，v3.0-FINAL-rA6，1100 行）
范围：§5 Shell 同态映射（定义 5.1.1–5.1.3，L459–496；审计表 SH-001..005，L500–506），及其与 §2.1（L44–52）、§3.1.3（L130–152）、§3.2、§4.1.6/4.1.7（L414–439）、§7.1（L605–611）、DO-10（L25）的交叉一致性。
纪律声明：本报告仅基于该文档自身内容；行号以磁盘文件为准。

## 结论摘要

1. **「Shell 作为函子」（L459–473）是比喻而非范畴论意义上的函子。** 文档未给出源/目标范畴的定义，未定义恒等态射与复合在两侧的对应，「函子律」（Shell(id)=id、Shell(g∘f)=Shell(g)∘Shell(f)）完全未被陈述或证明；且对象映射非良定（Shell(Component) 有两个像）、态射映射非全（4 个 Command 变体无像）。
2. **薄层五约束中仅约束 1 有指定机制（且机制不完备）；约束 2、4、5 在文档内无任何可执行判据。** 约束 3「无循环」与 Delta Sync 的应用必然含迭代存在文档内部张力。
3. **Delta Sync 只给了数据类型（L495），未给推导规则、前条件、不相交性证明与应用正确性定理。** 可在补充最小定义后给出条件证明（见 P5）。
4. **发现一个此前所有 iter 均未记录的新缺口：`ImmutableArray<T>` 字段的相等性是引用语义，破坏 §2.2「值比较」前提，进而使 Modified 集的组件级 diff 不完备——SH-003 的「扁平化已收敛」结论不成立。**
5. SH-001..005 中，仅 SH-005 接近真实消解；SH-001/SH-003 为「机制已指名但不完备」；SH-002/SH-004 为纯断言。

---

## 逐命题小节

### P1 「Shell 是 Domain → Godot 的函子」

| 项目 | 内容 |
| --- | --- |
| 命题 | 存在函子 Shell: Domain → Godot，按 L462–472 的对象/态射映射定义 |
| 数学性质 | 函子需：(i) 源范畴 C、目标范畴 D 明确；(ii) 对象函数 Ob(C)→Ob(D)；(iii) 态射函数 Hom_C(X,Y)→Hom_D(FX,FY)；(iv) F(id_X)=id_FX；(v) F(g∘f)=F(g)∘F(f) |
| 状态 | **open（比喻成立，函子不成立）** |

论证：

1. **无范畴结构。** L462 仅写 `Shell: Domain → Godot`。「Domain 范畴」的对象据 L464–467 混杂了三个层级（Entity、World、Component），不是同一范畴的 Ob 集合的自然成员列表；「态射」据 L469–472 是 Spawn/Destroy/SetComponent——这些按 §4.1.6/L417 是 Command（数据构造子），不是任意两个 Domain 对象间的态射。若强行解释为 World→World 的部分终态射，则 Shell(Entity)/Shell(Component) 两行超出该解释，映射定义域不自洽。
2. **对象映射非良定。** L467 `Shell(Component) = Godot.Node 的属性或子节点` 给出两个可能像，未给选择规则 ⇒ 不是函数。
3. **态射映射不全。** §4.1.7（L428–436）的 Command 有至少 7 个变体（SpawnEntity/DestroyEntity/SetComponent/EmitEvent/LoadResource/PlaySound/SpawnEffect + `| ...`），L470–472 仅覆盖前三个。EmitEvent/LoadResource/PlaySound/SpawnEffect 在 Shell 侧无像 ⇒ 即使承认其余结构，也只是部分函子。
4. **函子律不可满足的具体反例。** 取 f=SpawnEntity(e)、g=DestroyEntity(e.id)，g∘f 在 Domain 侧按 §3.2.1 语义等价于净零效应（net=0，实体从未被观察者可见地存在）。而 Shell 侧 L470–471 给出 Instantiate+AddChild 之后接 QueueFree——QueueFree 在 Godot 中是**延迟到帧末**执行（此事实恰被本文档 §8.1 L659 的 release-class 清单引用引擎源码所佐证），因此该复合在本帧内产生真实的树变更（节点短暂存在），Shell(g∘f) ≠ id。若改用 RemoveChild 则又是另一不同的像。无论选哪个实现，恒等律在该复合上失败。
5. 标题措辞自相矛盾：§5 标题为「同态映射」（L455），L459 称「函子」，同态与函子是不同强度的概念，文档未区分。

**消解所需最小补充**：放弃函子主张，改述为「带标注的执行翻译函数 translate: Command → (Godot API 序列)」并逐条给操作语义；或补齐两个范畴 + 全态射映射 + 复合保持律并处理延迟释放的时序模型。

### P2 薄层约束 1「无决策逻辑」可由 SH-001 机制保证

| 项目 | 内容 |
| --- | --- |
| 命题 | L479 约束 1 可由 L502 机制（sealed + L2 禁 if/else/for/while/switch/try-catch + L3 兜底）保证 |
| 数学性质 | 语法禁令集 B 对程序语法完备 ⇔ 程序 P 含决策 ⇒ P 的 AST 含 B 中某构造 |
| 状态 | **asserted（机制指名但不完备）** |

论证（证明缺口）：
- L502 禁令集遗漏了 C# 的全部其他条件构造：三元运算符 `?:`、null 传播 `?.`、null 合并 `??`/`??=`、switch 表达式、模式匹配 `is`/`case when`、逻辑短路 `&&`/`||`、`goto case`。以上任一均可承载决策而不触发禁令。
- 更根本的健全性缺口：AST 检查是**语法的**，不含跨方法调用闭包。生成代码调用任何辅助方法/委托/虚方法（其方法体含 if）即绕过禁令，而 L502 未限定「仅检查生成代码体、禁止一切外部调用」。SH-001 自己也承认「无法类型系统完全保证」（L502 发现栏原文），却把状态标为「已收敛」——发现栏与状态栏在同一行内自相矛盾。
- 禁止 try-catch 与 Godot API 会抛异常的事实冲突：生成的 Shell 代码对引擎异常无处理路径，文档未说明这是有意 fail-fast 还是疏漏。

**消解所需最小补充**：禁令集扩为封闭的条件构造清单（含表达式级）＋「生成代码体仅允许调用白名单 Godot API 与纯 DTO 转换」的调用闭包规则＋对 try-catch 缺席的显式语义决定。

### P3 薄层约束 2「无状态」与约束 4「确定性」

| 项目 | 内容 |
| --- | --- |
| 命题 | L480–482：无独立状态；相同 World 总产生相同 SceneTree 变更 |
| 数学性质 | 确定性需前条件：同步不变量 Inv(T,W)：「SceneTree 的受管子图恰为 Shell⟦W⟧ 且无外部突变」。在此不变量下，translate 是 World×Command→TreeDiff 的函数才可证 |
| 状态 | **open（约束 2）/ asserted（约束 4）** |

论证：
- 约束 2 的收敛方案（SH-002，L503）只给出了概念区分（「独立状态禁止 / 缓存覆盖写入允许」），**没有指名任何强制机制**（哪个 Analyzer 规则、哪层检查禁止 Shell 类新增字段？）。对比 SH-001 有具体机制、SH-005 有归属规则，SH-002 是五条中唯一零机制的。状态「已收敛」为纯断言。
- 约束 4 作为无条件命题为假：SceneTree 受引擎侧物理步进、动画、其他节点脚本、信号时序影响；相同的 World 输入配不同的当前树状态（如目标父节点已被外部删除）产生不同变更集甚至异常。故确定性至多是**相对不变量 Inv 的条件确定性**，而 Inv 本身需要一步同步归纳定理（见 P6），文档未陈述。
- 约束 4 还与 DO-10（L25）「双向同步」张力：若 SceneTree 可被引擎侧改变并回流入 Domain（输入方向 L492），则 Shell 不是单射——多个 SceneTree 对应同一 World（节点名、meta、UI/Camera 等非受管节点），「Shell(World)=SceneTree」（L466）作为对象等式不成立，只能是部分嵌入关系。DO-10 的「双向」范围未界定。

### P4 薄层约束 3「无循环」与 Delta Sync 应用的内部矛盾

| 项目 | 内容 |
| --- | --- |
| 命题 | L481「Shell 不执行循环」与 L489–495 的增量同步可同时成立 |
| 状态 | **文档内部矛盾（未消解）** |

论证：
- 应用一个 Delta（L495）必然要对 Spawned/Destroyed（Set）与 Modified（Map）做迭代写入——遍历集合即循环。要么 Shell 手写循环（违反约束 3），要么循环存在于 Generator 生成的代码中（则约束 3 的适用主体必须显式改为「手写 Shell 方法体」）。文档两处（L479–483、L502）均未做此区分。
- 连带缺口：SH-001 的禁令含 for/while 但**未列 foreach**；递归亦不在禁令内（递归方法体可不含任何循环关键字而实现迭代）。禁令集对「无循环」这一目标同样不完备。

### P5 Delta Sync：推导规则、三集合不相交性与幂等性

| 项目 | 内容 |
| --- | --- |
| 命题 | L495 的 Delta 类型足以支撑「增量一致」的同步 |
| 数学性质 | 设 K(W)=dom(entities)。自然推导：Spawned:=K(W')−K(W)；Destroyed:=K(W)−K(W')；Modified:={id↦{t∈types : W[id].components[t]≠W'[id].components[t]}}（id∈K(W)∩K(W')）。则： |
| 状态 | **discharged（在该推导定义下，条件成立）** |

条件证明（前提：Delta 按上述 diff 定义；World 含严格递增 version，L409；组件相等按 §2.2 值比较）：
1. **两两不相交**：Spawned⊆K(W')∖K(W)，Destroyed⊆K(W)∖K(W')，二者互斥；Modified⊆K(W)∩K(W')，与两者皆斥。∎（注意：这是 diff 定义下的构造性结论；若 Delta 由命令序列直接累积而非 diff 得出，「同帧 create 后 destroy」会同时进入 Spawned 与 Destroyed，不相交性失效。文档未规定必须用 diff 推导 ⇒ 该前提是必要前条件，须写明。）
2. **幂等性的正确表述**：Delta 的*应用*不是幂等操作（对同一树应用两次 Spawn 必失败）；幂等的正确对象是 *diff 计算*：diff(W,W)=空 Delta，且 diff 应用后再 diff 得空。文档未区分这两者，「幂等」一词在 §5 中实际未出现也未主张——此处代为给出应证命题。
3. **增量一致性的真判据（文档缺失的核心定理）**：One-Step Sync：若 Inv(T,W) 且 Apply(T, Delta(W,W'))=T'，则 Inv(T',W')。该定理依赖每个 Command 的树侧像与 Domain 侧语义一致（正是 P1 失败之处），故在现状下**不可证**；它是整个 §5 应证明而未证明的主引理。

### P6 SH-003 收敛真伪：扁平化不足以保证 Modified 完备（新缺口）

| 项目 | 内容 |
| --- | --- |
| 命题 | SH-003（L504）：嵌套变更遗漏由 COMP002 扁平化解决，状态「已收敛」 |
| 数学性质 | 组件级 diff 完备 ⇔ 组件值相等是结构相等 ∧ 结构相等等于语义相等 |
| 状态 | **asserted（反例存在）** |

论证（反例）：L386 允许字段类型 `ImmutableArray<T>`。C# 编译器为 readonly record struct 生成的相等性对 `ImmutableArray<T>` 字段使用 `EqualityComparer<T>.Default` 包装的比较——ImmutableArray 的默认相等是**底层数组引用比较**，非逐元素比较。于是：同一 Entity 的某组件内 ImmutableArray 内容被替换为新数组但元素相同 ⇒ 值语义上未变，diff 判为 Modified（误报）；反之若原地共享数组被外部修改（虽然 readonly 约束降低此风险，string 豁免同理引入别名），引用相等使 diff 判为未变（漏报）。两种情形都击穿 Modified 集的完备性，而 SH-003 的收敛方案（禁止嵌套对象）对此无效——问题不在嵌套而在相等谓词。这与 §2.2「值比较（readonly record struct 自动相等性）简单可靠」（决策表）直接冲突：自动相等性对 ImmutableArray 字段不是值比较。

### P7 SH-004 收敛真伪：Signature「合并」未定义

| 项目 | 内容 |
| --- | --- |
| 命题 | SH-004（L505）：Shell 效应签名与 Domain Command Signature 经「预算声明 vs 实际执行 + 运行时校准」合并，状态「已收敛」 |
| 状态 | **asserted** |

论证：文档给出的唯一签名组合算子是 §3.2.1/3.2.2 的集合并 ∪。若 Shell 执行 Claim 与 Domain 声明 Claim 按 ∪ 合并，同一资源会被计两次（Domain 声明 occupy(memory,create) ＋ Shell 实际 AddChild 的 occupy(tree,create) 资源尚不同；但 memory 类会重叠），net/Peak 将系统性双计；若不合并，则「合并」一词无所指。§9.1 的 Deviation 是 expected-vs-actual 数值校准，并非签名合并算子。收敛方案描述的是流程而非数学对象，无定义、无性质、无判据。

### P8 SH-005 收敛真伪：输入效应归属

| 项目 | 内容 |
| --- | --- |
| 命题 | SH-005（L506）：Claim 归属 Shell 的 GatherInput，Domain Input 零效应 |
| 状态 | **discharged（文档内部一致，机制有指名）** |

论证：与 §7.8（IsActionPressed 等 → read(input, action, use, shell_scope)）及 §3.1.3 ST-04（shell_scope ⇒ ScopeId.Shell，L138）一致；TS-005（L589）给出 L2+L3 强制机制。残余小缺口：GatherInput 自身仍受 P2 所列禁令不完备问题约束，但归属本身（本命题内容）在文档内自洽。注：shell_scope 到 ScopeId.Shell 的归一（ST-04）使 §7 全表作用域良定义，此项为 §5 与 §3 的正向闭环点。

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| --- | --- | --- | --- | --- |
| PO-SH-01 | Shell 满足函子律（恒等/复合保持） | open | 放弃函子措辞改定义翻译函数；或补两范畴+全态射映射+延迟释放时序模型后证复合保持 | L459–473 |
| PO-SH-02 | 态射映射全覆盖（EmitEvent/LoadResource/PlaySound/SpawnEffect 有 Shell 像） | open | 为 §4.1.7 全部变体补 Shell 侧像 | L428–436, L469–472 |
| PO-SH-03 | Shell(Component) 映射良定（属性 vs 子节点唯一选择） | open | 给出按 ComponentType 的确定性选择函数 | L467 |
| PO-SH-04 | 无决策禁令语法完备（?:、??、?.、switch 表达式、模式匹配、&&/\|\|）+ 调用闭包限制 | open | 封闭条件构造清单 + 白名单调用闭包规则 + try-catch 缺席的显式语义决定 | L479, L502 |
| PO-SH-05 | 无循环禁令完备（foreach、递归）+ 适用主体区分手写/生成代码 | open | 禁令加 foreach/递归；声明约束 3 仅限手写方法体或生成器豁免条款 | L481, L502 |
| PO-SH-06 | 无状态可强制（禁止 Shell 类新增字段的机制） | open | 指名 Analyzer 规则（如 SHELL00x：NodeShell 子类禁止非常量字段） | L480, L503 |
| PO-SH-07 | 确定性：同步不变量 Inv(T,W) 陈述 + 条件确定性 | open | 定义 Inv 并将约束 4 弱化为相对 Inv 的条件命题；界定 DO-10「双向」范围 | L482, L25 |
| PO-SH-08 | Delta 推导规则固定为 diff(W,W')（不相交性的前条件） | open | 在 §5.1.3 写明 Spawned/Modified/Destroyed 的 diff 定义 | L494–495 |
| PO-SH-09 | One-Step Sync 定理：Inv(T,W) ∧ Apply(T,Δ)=T' ⇒ Inv(T',W') | open | 以 P1 的翻译语义为基础证主引理（依赖 PO-SH-01/02 先消解） | L489–495 |
| PO-SH-10 | 组件值比较对 ImmutableArray 字段为结构相等 | open | 规定 ImmutableArray 字段用逐元素序列相等（自定义 Comparer）或禁止该字段类型进 diff 键 | L386, L504 |
| PO-SH-11 | Shell/Domain 签名合并算子的定义与双计排除 | open | 定义 merge 算子（如按 resource 归一取 max 或按 kind 分账），证 net/Peak 无双计 | L505 |
| PO-SH-12 | diff 幂等性（diff(W,W)=ε 且 Apply 后再 diff 为空） | discharged | P5-2 在 diff 定义下已条件证明；落文档需一句话声明 | L494–495 |

## 新发现缺口清单

1. **N-06-1（新，高）**：`ImmutableArray<T>` 字段相等性为引用语义，破坏 §2.2「值比较」前提与 SH-003 的 diff 完备性（P6，L386/L504）。此前 iter51/rA 系列修订均未触及。
2. **N-06-2（新，高）**：SH-001 禁令集遗漏 foreach、递归及全部表达式级条件构造；AST 检查无调用闭包限制，「已收敛」与其自身发现栏「无法类型系统完全保证」同行矛盾（P2/P4，L479/L481/L502）。
3. **N-06-3（新，中）**：约束 3「无循环」与 Delta Sync 应用必含迭代矛盾，文档未区分手写/生成代码的约束适用主体（P4，L481 vs L494–495）。
4. **N-06-4（新，中）**：DO-10「双向同步」未界定范围；Shell(World)=SceneTree 作为对象等式与非单射现实冲突（P3，L25/L466/L492）。
5. **N-06-5（新，低）**：术语混用——§5 标题「同态映射」vs L459「函子」，强度不同且均未给出形式定义（P1，L455/L459）。
