# Iter08 独立审计

## 范围 / 结论摘要

- **范围**：§7.1–§7.3（场景树 / 属性 / 物理 API 的 Claim 映射，L602–L631），及其对 §3.1（Claim/ResourceId/ScopeId 定义）、§3.2（Compatible）、§3.3（net/Peak）的依赖一致性。审计仅基于 `PDR_Effect_Cost_Algebra_v3_FINAL.md` 磁盘内容（v3.0-FINAL-rA6）。
- **结论摘要**：§7.1–§7.3 存在 **3 处文档内部矛盾**（QueueFree 的 kind 非法且使 iter27 收口注释失实；body_id/parent_path 违反文档自身的 Unknown 判定规则；⊆* 反对称性公理被包含层次规则否定）、**2 处高危证明缺口**（QueueFree 与 AddChild/RemoveChild 资源标识不一致致 DO-9 配对失败；Memory(uid="mem") 全局单桶致并行释放/创建误报 CONFLICT）。常规 Godot 合法操作（顺序重复 AddChild、MoveChild 与 AddChild 并行、每帧 GetNode）经核查**不产生误报**，但该"无误报"结论依赖 mode=use 的普遍化，使 §7.2/§7.3 的冲突检测能力**恒空**（vacuous completeness）。

---

## 逐命题小节

### P-08-1 §7 表记法与 Claim 五元组定义不合

| 项 | 内容 |
| --- | --- |
| 命题 | §7.1–§7.3 每个 API 的 Claim 集合是 §3.1.1 定义的五元组集合 |
| 数学性质 | 良构性（well-typedness）：Claim := (kind, resource, mode, scope, size?) 仅 5 个槽位 |
| 状态 | **open（记法歧义）** |
| 论证 | 表项如 `{ read(tree, path, use, shell_scope) }`（L606）含 5 个显式 token：kind=read、resource=tree、**path**、mode=use、scope=shell_scope。第三个位置 `path` 在 Def 3.1.1（L74–L82）中无对应槽位。唯一自洽读法是将 `path` 折入 resource（即 resource=Tree(path)），且 L189 的缩写映射 `tree ⇒ Tree(path)` 支持此读法；但文档未显式声明「表中第 2、3 token 合并为 resource 字段」的消解规则。`GetTree()` 的 `"root"`（L607）、`AddChild` 的 `node.id`（L608）同理。 |
| 行号 | L74–L82, L189, L606–L611 |

### P-08-2 QueueFree 的 kind=release 非法，iter27 收口注释失实（**内部矛盾**）

| 项 | 内容 |
| --- | --- |
| 命题 | QueueFree 映射为 `{ release(tree, self.id, release, …), release(memory, self.size, release, …) }`（L610），且「改 release 后 net 正确计入 −size」（L610 注释） |
| 数学性质 | 类型合法性 + net 的可计算性 |
| 状态 | **discharged（否证）——文档内部矛盾** |
| 论证 | (a) kind 枚举为 {read, write, occupy}（Def 3.1.1, L75；分桶 3.1.4b 仅设 Sig_read/Sig_write/Sig_occupy，L198）。L610 两条 Claim 的第一 token 均为 `release`，不在枚举内 ⇒ **ill-typed**，落不进任何桶。(b) net(S) 定义只聚合 `c.kind=occupy ∧ c.mode=release`（L309–L317）。即使善意地将 L610 第一 token 读作 kind 缺省、第三 token 读作 mode=release，其 kind 仍非 occupy ⇒ **net(S,scope) 对 QueueFree 的贡献严格为 0**。故 L610 注释「net 正确计入 −size」按字面为假；对比同节 RemoveChild 的正确写法 `{ write(…release), occupy(…release) }`（L609）可见 QueueFree 行偏离了本节自身范式。**正确形式应为 `occupy(memory, self.size, release, shell_scope)`（及 tree 维度同理）**。 |
| 行号 | L75, L198, L309–L317, L609, L610 |

### P-08-3 QueueFree 与 AddChild/RemoveChild 的资源标识不一致 ⇒ DO-9 配对断裂

| 项 | 内容 |
| --- | --- |
| 命题 | AddChild 创建的占用能与 QueueFree 的释放在 net(S,scope)/泄漏判定中按 resource 配对 |
| 数学性质 | 配对可行性要求：create 侧与 release 侧 resource 经 §3.1.4a 归一后相等 |
| 状态 | **open（缺口，当前不成立）** |
| 论证 | AddChild/RemoveChild 使用 `tree` 维度 ⇒ 归一为 Tree(node.id)（L189）；QueueFree 使用 `self.id` ⇒ 按 L189 `self ⇒ Self(component)` 归一为 Self("id")。Tree(p) ≢ Self(s)（构造子标签不同，L176–L177 无二者互等规则）⇒ **occupy(Tree(child), create) 与 release(Self(id)) 永不配对**，net 守恒与 DO-9 泄漏判定（L318–L320）在此路径上失效。附带类型问题：Tree 构造子字段为 `path: NodePath \| Unknown`（L96），而 `node.id`/`self.id` 是 EntityId/U64（Def 4.1.1），字段类型不符。最小补充：QueueFree 资源改为 Tree(self.path)，或增加归一规则 Self(id) ≡ Tree(id_of_node)。 |
| 行号 | L96, L100, L176–L190, L608–L610, L318–L320 |

### P-08-4 Memory(uid="mem") 全局单桶 ⇒ 并行合法操作的 CONFLICT 误报

| 项 | 内容 |
| --- | --- |
| 命题 | §7 中所有 memory 维度 Claim 归一后两两可区分到实例粒度，且并行约束不冤枉合法程序 |
| 数学性质 | 归一映射 L185 将裸名 `memory ⇒ Memory(uid="mem")`：**常元函数**，丢弃 size/上下文信息 |
| 状态 | **discharged（存在反例）** |
| 论文 | 反例 1：S₁ = QueueFree(A)、S₂ = QueueFree(B) 并行。二者各 emit release(Memory("mem")) ⇒ 同 resource、mode=(release,release) ∈ CONFLICT（L270–L272）⇒ 报警。但 Godot 中并行 free 两个不同节点完全合法 ⇒ **误报**。反例 2：并行两个 Instantiate 各 emit occupy(memory, ·, create)（L636）⇒ (create,create) ∈ CONFLICT ⇒ 误报。根因：uid 字段被缩写映射固化为常量 "mem"，违反了 ResourceId 设计意图（Memory(uid: U64)，L98——uid 本应区分实例）。注：单桶使 net 全局守恒**可**成立，但代价是泄漏定位粒度退化为全局且引入上述假阳性。最小补充：memory 缩写改为 Memory(uid=静态可得的实例标识)，或规定 memory 维度豁免 CONFLICT 判定、仅参与 net/Peak。 |
| 行号 | L98, L185, L265–L275, L636 |

### P-08-5 QueueFree 的 mode=release 与 Compatible 的一致性

| 项 | 内容 |
| --- | --- |
| 命题 | 修正 kind 后（见 P-08-2），mode=release 的选择与 Compatible/net 语义一致 |
| 数学性质 | Compatible(m₁,m₂) ⇔ use∨use∨(m₁,m₂)∉CONFLICT（L270–L274）；net 对 mode=release 取负项（L310–L312） |
| 状态 | **asserted（方向正确，条件成立）** |
| 论证 | 条件前提：(i) P-08-2 的 kind 修正已完成；(ii) P-08-3 的资源统一已完成。在前提供备下：mode=release 使 net 计入 −size（L310–L312）✓；release 与 AddChild 的 create 配对 Compatible=create∧release ∉ CONFLICT（L270–L271）✓ 不误报「先加后删」生命周期；release-class 白名单（L841 起）将 queue_free 列入强制 emit 清单 ✓。**残留开放点**：QueueFree(parent) || AddChild(child_into_parent) 判 compatible（create+release 良性配对），而 Godot 语义下向正在释放的父节点添加子节点是错误用法——代数层将其放行属保守欠近似，是否可接受文档未论证。 |
| 行号 | L270–L274, L310–L312, L608–L610 |

### P-08-6 self 资源标识统一性

| 项 | 内容 |
| --- | --- |
| 命题 | `self.X` 到 Self(component: String)（L100）的投影在全表统一且良定义 |
| 数学性质 | 要求存在全函数 proj: self表达式 → String，使归一后相等当且仅当语义同一资源 |
| 状态 | **open（多义复用，无投影规则）** |
| 论证 | §7.2 用 Self("transform")（属性名，L617–L621）；QueueFree 用 Self(self.id)（节点身份，L610）；§7.5 Connect 用 Self("signal_"+s)（且被 ST-02 强制归一到 SignalBus(s)，L180）。三种语义（属性槽 / 节点本体 / 信号归属）共用同一构造子，且 L189 的缩写映射只说 `self ⇒ Self(component)`，未定义 `.id`、`.size`、`.body_id` 如何落入 component:String。后果：不同节点的同名属性槽坍缩为同一 ResourceId（如任意两个 Shell 的 write(Self("transform"), use) 同资源）——因全为 mode=use 故暂不触发误报，但任何未来对 Self 维度引入 create/release 的映射都会立即产生跨节点假阳性（§7.5 SignalBus 归一已是现实先例）。 |
| 行号 | L100, L180, L189, L610, L617–L621 |

### P-08-7 重复 AddChild 等 Godot 合法操作不误报

| 项 | 内容 |
| --- | --- |
| 命题 | 顺序重复 AddChild、reparent（RemoveChild→AddChild）、MoveChild‖AddChild、每帧 GetNode 均不被误报 |
| 数学性质 | 顺序组合为纯集合并（3.2.1, L252–L255，幂等）；并行约束仅在 resource 相等且双 mode ∈ CONFLICT 时报警（L258–L263） |
| 状态 | **asserted（在本节映射下成立）** |
| 论证 | (a) 顺序 AddChild(child₁); AddChild(child₂)：resource 分别为 Tree(child₁.id)、Tree(child₂.id)，不相等 ⇒ ∪ 后并存，net 各计 [1,1] ✓；(b) reparent 序列为顺序组合，无并行约束检查 ✓；(c) MoveChild = write(Tree(node.id), use)（L611）与 AddChild 的 create 并行：m₂=use ⇒ Compatible ✓；(d) _Process 每帧 GetNode：read(Tree(path), use) 幂等合并为一条，频率惩罚由 AUDIT001 启发式（L946–L949）单独处理而非冲突误报 ✓。**边界**：向同一父节点重复 AddChild 同一 child 在 Godot 非法，代数层因幂等合并恰好也不报——漏报一个真实 bug，属可接受的欠近似但未被文档记录。 |
| 行号 | L252–L263, L608, L611, L946–L949 |

### P-08-8 transform 资源粒度

| 项 | 内容 |
| --- | --- |
| 命题 | Position/Rotation/Scale 共享单一资源 Self("transform") 不损害审计性质 |
| 数学性质 | 资源合并是保守方向（扩大冲突面）；但本节全部 mode=use ⇒ CONFLICT 判别力恒为空 |
| 状态 | **asserted（无害但无用）+ 一处矛盾** |
| 论证 | 因 §7.2 所有读写均标 mode=use（L617–L621），CONFLICT={(create,create),(move,move),(release,release)} 永不在 §7.2 触发 ⇒ 该节对竞态/冲突检测的贡献为空集（sound 但 vacuously incomplete）。粒度合并的实际损害仅在 Deviation Σ 按资源对齐处（L191）：Position/Rotation/Scale 的实际采样值混入同一对齐键，校准信号失真——程度未量化。另 GlobalPosition getter 断言 `read(tree, parent_path, use, …)`（L619）：parent_path 依赖运行期场景拓扑，静态通常不可判定，按 L182 自家规则应降级 Unknown，表却给出具体值 ⇒ **与 Unknown 纪律矛盾**（同 P-08-9）。 |
| 行号 | L182, L191, L270–L272, L617–L621 |

### P-08-9 物理维度 body_id 的静态可判定性（**内部矛盾**）

| 项 | 内容 |
| --- | --- |
| 命题 | §7.3 各条目的 `Physics(self.body_id)` 是合法的具体 resource 标识 |
| 数学性质 | 文档自定规则（L182）：「resource 字段为 Unknown 当且仅当该字段静态不可判定」 |
| 状态 | **discharged（否证：表与规则矛盾）** |
| 论证 | RID 由物理服务器在运行期分配（实例化后方确定），编译期静态分析原则上不可判定其值 ⇒ 按 L182 必须写 Unknown。但 L627–L631 五个条目均断言具体 `self.body_id`。两种修法：(i) 全部降级 Unknown(resource)——此时 MoveAndSlide 与 ApplyForce 仍可配对（Unknown=Unknown，L183），保守可行，但与 §7.4 Instantiate 的 scene.uid 等同样不可判定的字段需统一处理；(ii) 定义「符号 RID」机制（按持有者节点参数化 Physics(body_id=owner)）并写入归一规则。文档两者皆未做。附带：MoveAndSlide 的 `read(tree, "collision_shapes", use, …)`（L627）中 `"collision_shapes"` 不是任何声明的 NodePath，来源不明。 |
| 行号 | L182–L183, L627–L631 |

### P-08-10 ScopeId ⊆* 的偏序性质（被 §7 的 scope 使用直接依赖）

| 项 | 内容 |
| --- | --- |
| 命题 | ⊑ 自反、反对称、传递 ⇒ ⊆* 为偏序（L159）；§7.1–§7.3 的 shell_scope ⇒ Shell（L138）在该偏序下过滤良定义 |
| 数学性质 | 反对称性：a ⊑ b ∧ b ⊑ a ⇒ a = b |
| 状态 | **discharged（否证：公理集不自洽）** |
| 论证 | L154–L155 同时断言 Global ⊑_any X 与 X ⊑_any Global。取 X=Shell（≠Global）：得 Global ⊑ Shell ∧ Shell ⊑ Global 而 Global ≠ Shell ⇒ **反对称性被否定**，L159 的性质声明为假。⊆* 实为**预序**（preorder）；Global 与 Shell 在商集中等同。工程后果：§7.4 的 global_scope 占用（L634–L636）进入 Shell 聚合可能正是本意，但形式化必须改写——或将「Global ⊑_any X」降格为聚合侧规则（net/Peak 过滤器特判）而非序关系公理，或承认预序并在商偏序上陈述性质。此缺陷影响 §7 全部条目在 net(S,scope)/Peak(S,scope) 中的过滤语义。 |
| 行号 | L138, L141–L160, L634–L636 |

### P-08-11 Shell scope 与循环标注的合成

| 项 | 内容 |
| --- | --- |
| 命题 | 循环内的 §7.1–§7.3 API 调用能获得唯一确定 scope 以供 Peak(S, scope) 过滤 |
| 数学性质 | scope 标注须为单值全函数 |
| 状态 | **open** |
| 论证 | §7 表统一给 scope=shell_scope ⇒ Shell（L138）；而 §3.2.5 规定 copy_i(S) 的 scope「标注为所在 Loop(id) 或 Global」。位于 `_Process` 循环体内的 MoveAndSlide（典型每帧调用）同时命中两条标注规则，合成优先级未定义 ⇒ Peak 过滤输入不确定。 |
| 行号 | L138, §3.2.5（L296–L308）, L627 |

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| --- | --- | --- | --- | --- |
| PO-08-01 | QueueFree 的 Claim 集合适型（kind ∈ {read,write,occupy}）且 net 计入 −size | **open** | 将 L610 两条 Claim 第一 token 改为 occupy（对齐 L609 范式）；或在 §3.1.1 正式扩 kind 枚举并重推 net/分桶 | L610, L75, L309–L317 |
| PO-08-02 | QueueFree 与 AddChild/RemoveChild 资源标识可配对 | **open** | QueueFree 资源改 Tree(self.path)，或新增归一规则 Self(id) ≡ Tree(path_of(id)) 并补 NodePath/U64 字段类型 | L96, L608–L610 |
| PO-08-03 | memory 维度不产生并行 CONFLICT 误报 | **open** | memory 缩写按实例参数化 Memory(uid=…) 或规定 memory 维度豁免 CONFLICT 仅参与 net/Peak；补反例测试（并行双 QueueFree / 双 Instantiate） | L185, L265–L275, L636 |
| PO-08-04 | node.id / self.id / body_id / scene.uid / parent_path 的静态可判定性判定规则 | **open** | 增加「运行期分配标识 ⇒ Unknown 降级」条款或定义符号标识机制；同步修订 L627–L631、L619 | L182–L183, L619, L627–L631 |
| PO-08-05 | ⊆* 的序性质陈述与包含层次规则自洽 | **open** | 二选一：删除 Global ⊑_any X 公理改为过滤器特判；或声明 ⊑ 为预序、在商偏序上重述 L159 | L153–L159 |
| PO-08-06 | §7 表 5-token 记法到五元组的形式消解 | **open** | 在 §7 开头加一句：「表中第 2、3 token 经 §3.1.4a 缩写映射合并为单一 resource 字段」 | L606–L631, L189 |
| PO-08-07 | Shell × Loop scope 合成规则 | **open** | 规定优先级（如 Loop(id) 覆盖 Shell，Shell 为默认兜底）写入 §3.2.5 或 §7 | L138, L296–L308 |
| PO-08-08 | transform 单资源合并下 Deviation 对齐的精度损失可接受 | asserted | 给出量化论证或按 Position/Rotation/Scale 拆分 component 键 | L191, L617–L621 |
| PO-08-09 | QueueFree ‖ AddChild(create+release 放行) 的欠近似被记录 | asserted | 在 §3.2.3 注明该良性配对的已知欠近似场景清单 | L270–L271 |

---

## 新发现缺口清单

1. **【高】QueueFree ill-typed（P-08-2 / PO-08-01）**：iter27 收口注释与 net 定义直接矛盾，DO-9 主路径（Instantiate→QueueFree）在当前文本下仍不可证守恒——rA 系列宣称「§7.1 QueueFree mode=release 已收口」仅完成了 mode 修正，kind 维度遗漏。
2. **【高】资源标识三元分裂（P-08-3 / PO-08-02）**：tree/node.id、self.id、memory 单桶三种标识方案在 §7.1 内部混用，配对断裂；连带 Tree 构造子的 NodePath 字段类型违规。
3. **【高】Memory 常元坍缩（P-08-4 / PO-08-03）**：`memory ⇒ Memory(uid="mem")` 把实例维度抹平，制造确定性 CONFLICT 误报（并行双 free / 双 instantiate 反例），与 R-3「Analyzer 误报率过高」风险直接冲突。
4. **【中】Unknown 纪律选择性执行（P-08-9 / PO-08-04）**：文档自定义了 Unknown 判定规则却在 §7.2/§7.3 对运行期标识（parent_path、body_id）不执行，形成规范-映射矛盾。
5. **【中】⊆* 非偏序（P-08-10 / PO-08-05）**：公理集不自洽，Global 与 Shell 互相蕴含；影响全部 §7 条目的聚合过滤语义基础。
6. **【低】§7.2/§7.3 冲突检测恒空**：mode=use 普遍化使这两节的 Compatible 判别力为空集——不是错误，但「映射表覆盖冲突检测」的隐含预期应显式降级说明。
7. **【低】记法与合成规则缺口**：5-token 记法未消解（PO-08-06）、Shell×Loop scope 合成未定义（PO-08-07）、重复 AddChild 同一 child 的漏报未记录（P-08-7 边界）。
