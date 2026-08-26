# Iter16 独立审计

## 范围

跨章形式审计：mode 组合的 Compatible 关系（§3.2.3，L265–279）与其唯一调用点并行组合 §3.2.2（L258–263），含：
1. 16 有序对判定矩阵的穷举复核（完备性 P2、对称性 P1）；
2. 用 §7 真实 Godot API→Claim 映射（L604–689）检验 CONFLICT 集在真实映射上的可实例化性，重点核查「写操作标 mode=use」的后果；
3. §8.1 默认 Unknown 规则（L698–702）与 §3.2.3 P4 的交互。

仅依据 `PDR_Effect_Cost_Algebra_v3_FINAL.md` 磁盘内容；行号以 Lxxx 标注。

## 结论摘要

- **矩阵本身完备且自洽**：16 对全覆盖、CONFLICT 刻画与定义等价、P1/P2 可消解（本报告直接给出机械枚举证明）。文档在此层面**无内部矛盾**。
- **但存在一个被掩盖的闭式**：Compatible 等价于 `(m₁=use) ∨ (m₁≠m₂)`——即「非 use 且不同即兼容」。§3.2.3 用生命周期配对语言包装的这一极宽规则，是后续全部真实映射反例的根源。
- **核心反例成立**：§7 大量纯变更 API（Position setter L618、ApplyForce L628、SetVolumeDb L665、Seek L688、MoveChild L611、SetMaterialOverride L657、MoveAndSlide 写分量 L627）全部标 `mode=use`，使 CONFLICT 对这类资源**不可实例化**：任意两个写-写竞争均判兼容。CONFLICT 仅在生命周期类 create/release claim 上可达。
- **P4 与 §8.1 构成类型层 + 语义层双重矛盾**：`Unknown ∉ mode` 声明域（L90 vs L279）；且 Unknown 按 use 处理对冲突检测是 fail-open，与 §8.1 自称的 fail-closed（L701）叙事冲突。
- **MA-009 的「已收敛/可机械验证」（L359）表述过强**：可机械验证的只是 4×4 表格本身，而非表格相对并发语义的正确性。

---

## 逐命题小节

### 命题 C1（矩阵覆盖 / P2 全函数）

- **命题**：Compatible 定义（L268–272）覆盖 {use,create,release,move}² 全部 16 个有序对，无未定义项。
- **数学性质**：全函数性（totality）：∀(m₁,m₂)∈M×M，Compatible(m₁,m₂) ∈ 𝔹 可判定。
- **状态**：**discharged**
- **论证**：穷举。use 行（4 对）与 use 列（另 3 对）由子句 `(m₁=use)∨(m₂=use)` 覆盖，共 7 对；剩余 {create,release,move}² 共 9 对中，显式配对子句列出 (create,release),(release,create),(create,move),(move,create),(release,move),(move,release) 共 6 对；剩对角 3 对 (create,create),(release,release),(move,move) 由 CONFLICT（L273）显式排除。7+6+3=16 ✓。
- **行号**：L268–277。

### 命题 C2（对称性 / P1）

- **命题**：∀m₁,m₂，Compatible(m₁,m₂)=Compatible(m₂,m₁)。
- **数学性质**：Compatible 为 M×M 上的对称二元关系。
- **状态**：**discharged**
- **论证**：子句 `(m₁=use)∨(m₂=use)` 关于变元交换对称；三个配对子句均成对双向书写（L270–271）；CONFLICT 为对称集（仅含对角对，对角对自动对称）。各析取支对称 ⇒ 整体对称。
- **行号**：L268–274。

### 命题 C3（CONFLICT 刻画的等价性 / 闭合式）

- **命题**：L274 的刻画 `Compatible ⇔ (m₁=use)∨(m₂=use)∨((m₁,m₂)∉CONFLICT)` 与 L268–272 的展开定义在 M×M 上逐点相等。
- **数学性质**：两公式的外延相等；进一步有极简闭式 **Compatible(m₁,m₂) ⇔ (m₁=use) ∨ (m₁≠m₂)**。
- **状态**：**discharged**（等价性）；闭式为本次审计新证定理
- **论证**：对 16 对逐一验证两公式同真值（由 C1 枚举直接读出）。闭式：若 m₁≠m₂ 且双方均非 use，则 (m₁,m₂) 是 6 个异色非 use 对之一 ⇒ 兼容；若 m₁=m₂≠use 则 ∈CONFLICT ⇒ 不兼容；任一为 use ⇒ 兼容。∎
- **审计含义（关键）**：闭式揭示 §3.2.3 实质上是「**异即兼容**」规则，(create,release)/(create,move)/(release,move) 三组生命周期配对的语义辩护是**修辞性的**——任何假想的第四种非 use 模式 X 也自动与 create/release/move 兼容。文档未陈述此闭式，构成呈现层面的缺口（见缺口 N1）。
- **行号**：L268–274。

### 命题 C4（CONFLICT 在 §7 白名单映射上的可实例化性——部分成立）

- **命题**（待审假设）：「write 操作标 mode=use 使 CONFLICT 集在真实映射上不可实例化」。
- **数学性质**：设 Inst(§7) := {c.mode | c 出现于 §7 某 Claim 集}。CONFLICT 可实例化 :⇔ ∃API 对 (A,B) 与归一后同资源 claim c₁∈Sig(A), c₂∈Sig(B)，使 (c₁.mode,c₂.mode) ∈ CONFLICT。
- **状态**：**open → 裁定为部分反例（假设过强，但暴露真实缺口）**
- **论证**：
  - **CONFLICT 可达的部分**（反例于强命题）：DrawRect ×2 并行（L656）经 §3.1.2b 归一同为 `write(CommandBuffer("gpu"), command_buffer, create)` ⇒ (create,create)∈CONFLICT ⇒ §3.2.2 报冲突；同理 Play 同通道 ×2（L663）、EmitSignal 同信号 ×2 并行（L646）、动画 Play ×2（L688 前一行区域）。故 CONFLICT 非空可实例化。
  - **不可达的部分（真实缺口）**：以下 API 的写效应全部标 `mode=use`：Position setter（L618）、MoveAndSlide 写分量（L627）、ApplyForce/ApplyImpulse（L628–629）、MoveChild（L611）、SetMaterialOverride（L657）、SetVolumeDb（L665）、Seek（L688）。对这些资源的任意 claim c′：Compatible(use, c′.mode)=true（C3 闭式）⇒ §3.2.2 约束对这些 claim **恒真空洞**。典型反例：系统 S₁ 与 S₂ 并行各自执行 Position setter，两条 `write(Self("transform"), "transform", use)` 同资源且兼容——并发写竞争静默通过；更严重的组合：`write(self,…,use)`（任意 setter）∥ QueueFree 的 `release(tree,self.id,release)`（L610）——**对已释放节点写入**亦判兼容。
  - **结论**：CONFLICT 的实际覆盖域 = 生命周期类 create/release claim；对状态变异类（mutate）claim 覆盖为零。这是检测能力的不对称缺口，不是表格逻辑错误。
- **行号**：L258–263、L273、L604–689（上列具体行）。

### 命题 C5（生命周期良性配对在并行语境下的误用）

- **命题**：(create,release) 等配对在 ‖ 组合中判兼容是正确的。
- **数学性质**：Compatible 是单一全局关系，而其正确性依赖组合语境：顺序生命周期配对（`;`）良性 ≠ 真并发（`||`）良性。
- **状态**：**open（发现反例，文档内部张力）**
- **论证**：P3（L278）自述其动机是修正 iter23 的「良性生命周期误判」，即为**顺序** create→release 序列翻案。但 Compatible 唯一消费点是 §3.2.2 的**并行**约束（L262）。反例：S₁ = AddChild(n)（emit write(tree,n,create)+occupy(tree,n,create)，L608），S₂ = RemoveChild(n)（emit 对应 release，L609）；S₁‖S₂ 同资源、(create,release) 配对 ⇒ 兼容 ⇒ 无报警——而对同一节点的并发增删是真实竞争。**顺序语境需要的宽松关系被无参数化地用于并发语境**。最小修复：拆分 Compat_seq / Compat_∥，后者将 (create,release)、(create,move)、(release,move) 列入冲突。
- **行号**：L258–263、L278、L608–610。

### 命题 C6（mode=move 在真实映射上的死值）

- **命题**：CONFLICT 中 (move,move) 及 move 相关 6 个兼容对在 §7 白名单上有实例。
- **数学性质**：Inst(§7) ⊆ {use, create, release}（待证）。
- **状态**：**asserted（本审计断言）+ open（需文档回应）**
- **论证**：全文检索 §7 各表，rA 系列修订后唯一曾 emit move 的 QueueFree 已改为 release（L610 明注「原 mode=move…改 release」）；现存 §7 无任何 `move` claim。故 move 仅能经 §8.3.1 [EffectOverride] 的 mode 覆盖通道进入系统。后果：(a) CONFLICT 的 (move,move) 分量在白名单上映射下不可实例化（vacuous）；(b) 6 个 move 兼容对的「良性转移」语义无任何映射证据支撑。文档应要么删除 move、要么给出至少一个权威 move 映射。
- **行号**：L90、L273、L610、L719–723（EffectOverride 可覆盖 mode）。

### 命题 C7（P4 的类型违法与 fail-open 矛盾）

- **命题**：「mode=Unknown 按 use 处理」（L279）与定义域声明及 §8.1 一致。
- **数学性质**：若 Unknown 进入求值域，则 mode 域为 |M|=5，有序对总数 25 ≠ 16；P2 的「16 对全部覆盖」在扩展域上为假。
- **状态**：**open（文档内部矛盾，明确指出）**
- **论证**：
  1. **类型矛盾**：Def 3.1.1（L90）声明 `mode ∈ {use, create, release, move}`；P4 与 §8.1 默认规则（L698: `{ Unknown(unknown, Unknown, Unknown, scope) }`，第三槽为 mode=Unknown）都使用了域外值。Compatible 的 16 对矩阵在 5 元域上未定义 9 个对（含 (Unknown,create) 等），P4 以「按 use 处理」一刀切补齐，但这使 L274 的 CONFLICT 闭合刻画失效（如 (Unknown,create) 归入 use 支而非 ∉CONFLICT 支，刻画不再等价）。
  2. **fail-open 矛盾**：P4 注释自称「最弱兼容，fail-closed 为保守兼容」（L279）——把未知模式映射为**与一切兼容**恰是冲突检测意义上的 fail-open；§8.1（L701）的 fail-closed 承诺是「需人工确认」，但 §3.2.2 中并行的双 Unknown claim 直接静默合成，无任何人工确认路径。同一文档对 Unknown 的处理哲学自相矛盾。
- **行号**：L90、L273–279、L698–702。

### 命题 C8（单 Signature 内部一致性未约束）

- **命题**：CONFLICT 检查覆盖所有同资源 claim 对。
- **状态**：**open**
- **论证**：§3.2.2 仅量化 ∀c₁∈S₁,c₂∈S₂（跨集合）；单个 Command 携带的 Signature 内部同时含 create 与 release 同资源 claim（畸形签名）不受任何检查。低危但属完备性缺口。
- **行号**：L258–263、L446 附近（Command 携带 Signature）。

### 命题 C9（MA-009 收敛声明的强度）

- **命题**：「§3.2.3 给出 16 对全函数 + 冲突集 CONFLICT，可机械验证」⇒ MA-009（Compatible 完备性）已收敛（L359）。
- **状态**：**open（声明过强，构成文档内部张力）**
- **论证**：「可机械验证」仅对 C1/C2 层面（表格封闭性）成立；MA-009 标题为「Compatible 的**完备性**」，其应有语义是「所有真实并发违规均可由 CONFLICT 捕获」——由 C4/C5 反例，该语义层面**不成立**。收敛声明混淆了「枚举封闭」与「语义可靠」。A4 判据（L1033）继承同一问题：它只保证 CONFLICT 命中必报，不保证应报皆命中。
- **行号**：L359、L1033。

---

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ------ | ------ | ------ | ------ |
| PO-I16-1 | P2 全函数（16/16 覆盖） | discharged | （本报告 C1 枚举即证） | L277 |
| PO-I16-2 | P1 对称性 | discharged | （本报告 C2 即证） | L276 |
| PO-I16-3 | L268 定义 ≡ L274 刻画；闭式 use∨≠ | discharged | （本报告 C3 即证；建议文档收录闭式） | L268–274 |
| PO-I16-4 | ‖ 语境下 (create,release)/(create,move)/(release,move) 兼容的合理性 | open | 引入语境参数化 Compat_∥（上述三对改判冲突），并为 AddChild∥RemoveChild 反例补测试 | L262, L270–271, L278 |
| PO-I16-5 | use 标注的写操作不漏报写写竞争 | open | 细分 mode（如 read/mutate 二分 use），或将 Compatible 提升为 kind×mode 上的关系；至少对 Position setter ∥ Position setter、setter ∥ QueueFree 补反例测试 | L618, L611, L627–629, L657, L665, L688 |
| PO-I16-6 | mode=move 存在非空真实映射实例 | open | 给出 ≥1 条权威 §7 move 映射，或删除 move 并收缩 CONFLICT | L90, L273, L610 |
| PO-I16-7 | Unknown ∈ mode 的类型合法化 + fail-closed 语义落地 | open | 将 Def 3.1.1 mode 域扩至含 Unknown 并重述 25 对矩阵；规定 (Unknown,·∉{use}) 判冲突或强制人工确认标记，消除与 §8.1 的哲学矛盾 | L90, L279, L698–702 |
| PO-I16-8 | 单 Signature 内部 CONFLICT 自检 | open | 在 §3.2.1 或 Command 校验处补 ∀c₁≠c₂∈S 同资源 ⇒ Compatible 约束 | L255–263 |
| PO-I16-9 | MA-009/A4 的「完备性」措辞与实际保证一致 | open | 将 L359/L1033 措辞降级为「CONFLICT 封闭性已证；对真实映射的检测完备性见 PO-I16-4/5（open）」 | L359, L1033 |

## 新发现缺口清单

1. **N1（呈现缺口）**：Compatible 的闭式 `(m₁=use)∨(m₁≠m₂)`（C3）未被文档陈述；生命周期配对的三组子句是对该极宽规则的冗余修辞，误导读者以为配对经过语义论证。
2. **N2（检测盲区）**：状态变异类 API（≥7 个，见 C4 行号清单）因 mode=use 完全逃逸 CONFLICT 检测；这是 §14 A4「兼容冲突 COMPLETE」判据的真实漏洞边界——A4 只对生命周期资源 complete。
3. **N3（语境无参数化）**：Compatible 未区分顺序/并行语境，导致 iter23 的顺序生命周期修正反向制造了并行语境的假阴性（C5 反例 AddChild∥RemoveChild）。
4. **N4（死模式）**：move 为白名单映射上的死值，CONFLICT(move,move) vacuous（C6）。
5. **N5（域外值）**：Unknown 作为事实上的第五个 mode 值游离于 Def 3.1.1 类型声明之外，16 对矩阵在实现域上实为 25 对欠定义；且其 use 化处理与 §8.1 fail-closed 叙事矛盾（C7）。
6. **N6（相邻发现，超出本轮范围仅记录）**：§3.2.1/3.2.2 均以幂等集合并实现，重复 claim（如同帧两次 EmitSignal 同信号）在去重后计数信息丢失，影响 Peak/net 口径——建议后续迭代单独审计 ∪ 幂等与频次敏感度量的相互作用。
