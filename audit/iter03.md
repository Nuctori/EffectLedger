# Iter03 独立审计

- 审计对象：`PDR_Effect_Cost_Algebra_v3_FINAL.md`（磁盘版本 v3.0-FINAL-rA6）
- 范围：§3.3 派生度量（定义 3.3.1 Net / 3.3.2 Peak / 3.3.3 read/write），及其对 §3.1（SizeVal L203–236、Claim= L169、⊆* L141）、§3.2（组合律 L252–301）的依赖
- 纪律：结论仅基于该文档自身内容，引用行号；每命题标注 discharged / asserted / open

## 结论摘要

§3.3 的三个派生度量在「作用域过滤谓词」层面已由 §3.1.3b 良定义，但在**算术层**存在系统性缺口：
SizeVal 已从标量改为区间 `[lo,hi]`（L207），而 §3.1.5a 只为 ℕ\* 定义了 `+ × max min compare` 五种运算（L221–228），
**区间加法与区间减法从未定义**。net 的核心运算 `Σ c.size  −  Σ c.size`（L312–318）因此悬空：
求和算子未指定（区间算术加 `[a+c,b+d]`？还是 merge_I join？二者语义完全不同），
减法算子在 ℕ\*/Interval 上无定义，⊤ 参与减法的传播律缺失。此外发现一处**文档内部矛盾**：
Peak 公式（L327/L337）缺少 `c.kind=occupy` 过滤，与 §3.1.4b 分桶约束（L199）及 DO-7 直接冲突；
且 L337 中 `weight(c.kind,c.kind)` 恒等于 1，weight 函数在 Peak 公式内部**不可能**触发 KIND_MIX，
量纲隔离实际依赖未写出的桶级外层结构。

## 逐命题审计

### 命题 N1：net 是全函数（良定义性）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | net : Signature → D（某值域 D），对任意 S 唯一确定 |
| 状态 | **open** |
| 论证 | L312–318 定义 net 为两个 Σ 之差。(i) Σ 对 `c.size ∈ Interval` 求和：§3.1.5a 仅定义 ℕ\* 上的 +（L224），**Interval 上的加法未定义**——若取区间算术加 [a,b]+[c,d]=[a+c,b+d] 则需显式声明；若误用 merge_I（L235，min/max join）则 net 退化为非负包络，不再是「净变化」；(ii) 两 Σ 之间的 `−` 在 ℕ\* 与 Interval 上均无定义（L221–228 运算律表无减法）；(iii) ⊤ 进入被减项/减项时的行为（⊤−x ? x−⊤?）无任何定律。三者在文档中均为悬空符号。 |
| 行号 | L207–208, L221–228, L309–320 |

### 命题 N2：net(S, Global) = net(S)（基础定义与分组定义一致）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 全局聚合是作用域分组的最大元特例 |
| 状态 | **discharged（条件证明）** |
| 前提 | N1 的算术层已补齐（本命题只关涉过滤谓词） |
| 论证 | 按 §3.1.3b，`c.scope ⊆ Global := (c.scope ⊑ Global) ∨ (Global=Global)`；任一具体 scope 满足 X ⊑\_any Global（L150–152），故过滤器恒真，两组求和指标集相同 ⇒ 两式逐项相等。附带验证：⊆\* 的自反/反对称/传递经查表成立（Global⊆a ⇔ a=Global 保证传递性闭合），L157 的偏序断言成立。 |
| 行号 | L141–158, L312 vs L317 |

### 命题 N3：net 对 ∪ 线性 / 单半群同态

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | net(S₁∪S₂) = net(S₁) + net(S₂) 是否成立 |
| 状态 | **discharged（否定性结果 + 条件正结果）** |
| 论证 | **反例**：S₁ = S₂ = { occupy{Memory(u), create, s, [64,64]} }。net(S₁)=net(S₂)=64；但 ∪ 幂等（L252–256，Claim 相等合并）⇒ S₁∪S₂ = S₁ ⇒ net(S₁∪S₂)=64 ≠ 128。故 net **不是** (Signature,∪,∅) 到任意加法幺半群的单调半群同态；顺序组合 (S;S)=S 语义下「同一 Claim 重复分配两次」被计数一次。这是 Set 语义与资源计数的固有张力，文档未声明此限制（MA-004 L354 称「已解决」，仅指 net/peak 不再混淆，未覆盖幂等吞并重复分配问题）。**条件正结果**：若 S₁∩S₂ = ∅（按 Claim= 判），则 net(S₁∪S₂)=net(S₁)+net(S₂)，前提是 N1 的区间加法已定义且可交换结合。 |
| 行号 | L252–256, L354 |

### 命题 N4：Peak 良定义且量纲隔离（DO-7）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | Peak : Signature × ScopeId → SizeVal∪{⊤}；同一次 Peak 内不跨 kind 混算 |
| 状态 | **open（含内部矛盾）** |
| 论证 | (i) **kind 过滤缺失（内部矛盾）**：L327 与 L337 的求和指标仅为 `c.scope⊆scope ∧ c.mode≠release`，无 `c.kind=occupy`。read/write 桶的 claim（如 §7.2 的 read(self,"transform",use) size=[1,1]）全部混入占用峰值求和，直接违反 L199「peak/net/read/write 仅在同桶内聚合」与 DO-7。文字描述「并发占用 size 之和」（L326）暗示仅 occupy，公式未兑现——**规范文本与其形式化公式矛盾**。(ii) weight 失效：L337 的 `weight(c.kind,c.kind)` 由 L333 恒为 1（同 kind），单条 Peak 公式内永远不产生 ⊥；KIND_MIX 只能在「外部把 Peak\_read 与 Peak\_occupy 相加」时触发，而该外层结构未定义。L1032（A3 判据）声称基于 §3.3.2 weight 达成 SOUND+COMPLETE，证据链断裂。(iii) ω=⊤ 时 L327 的 `max_{i∈1..ω}` 类型非法（1..⊤ 无意义），靠 L301/L328 的兜底条款「返回 ⊤」全函数化，可接受但属公理式补丁而非推导。(iv) copy\_i 因 scope 统一标注为 Loop(id) 且其余字段相同，各副本 Claim 相同 ⇒ 在集合语义下坍缩为一个副本（见 N6）。 |
| 行号 | L196–200, L296, L301, L323–338, L1032 |

### 命题 N5：Peak ≥ net（峰值上界净变化）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | Peak(S,scope) ≥ net(S,scope)（资源守恒直觉：持有峰值不小于净增量） |
| 状态 | **asserted（ω=1 时条件可证；一般情形 open）** |
| 论证 | 文档从未陈述此关系。设 P = Σ(create,move)、R = Σrelease（均限 kind=occupy）。ω=1 且数值域为普通自然数时：net = P − R ≤ P ≤ Σ\_{mode≠release} = Peak，需 R ≥ 0（N1 补齐后平凡）且 Peak 不含负项。一般情形失败：Peak 取 max 于循环副本之上，而 net 按全部副本累计；由于副本集合坍缩（N6），net(S×ω)=net(S) ≠ ω·net(S)，两度量对 ω 的响应不一致，「Peak ≥ net」在多副本泄漏场景下不可判定。文档亦未给出区间版证明路径。 |
| 行号 | L312–318, L327, L354 |

### 命题 N6：(S×ω) 作为数学对象良定义

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | (S×ω) := Σ\_{i=1..ω} copy\_i(S) 应为 Signature 上的合法元素 |
| 状态 | **open** |
| 论证 | L296 用 Σ 记号定义集合构造，本身即记号滥用（集合上无 Σ）；copy\_i 仅 scope 标注 Loop(id)/Global（L296 注释），i 不进入 Claim 任何字段 ⇒ ∀i,j: copy\_i(S)=copy\_j(S)（按 Claim=），并集后 ω 个副本坍缩为一份。「上界开放的重复副本集合」（L297）不是集合也不是多重集，是无定义对象。后果：DO-8「循环内资源分配静态报警」的数值基础缺失——100 次迭代每次占 64MB 报 Peak=64MB 还是 6400MB，文档无法回答。 |
| 行号 | L293–301 |

### 命题 N7：peak 过滤谓词 c.mode≠release 的量纲合理性

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 过滤谓词应在维度（kind×mode×unit）上封闭 |
| 状态 | **open** |
| 论证 | 三重量纲问题：(i) mode 层面——谓词只排除 release，保留 use/create/move。对 kind=occupy 自洽（use=持续持有应计入），但对 read/write 无意义（见 N4-i）；谓词的正确性**隐式依赖 kind=occupy 前提，而前提未写进公式**。(ii) unit 层面——size 无单位维度：occupy{Memory,[64,64]}(MB) 与 occupy{Occupancy("audio"),[1,1]}(通道数) 在 Peak/net 中直接相加得 [65,65]，MB 与个数混算。§3.1.5 SizeVal 无单位参数，AUDIT003 的「VramMB 预算」（L958）要求按单位类分组聚合，文档未提供机制。(iii) resource 层面——不同资源的 size 求和语义未定义（全局总量？分资源账本？），泄漏判定 net>0 也因此无法定位到具体资源。 |
| 行号 | L207, L327, L337, L958 |

### 命题 N8：net 值域良定义（区间减法 / 负值 / ⊤）

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | 存在 codomain D 与封闭的减法运算使 net: Signature→D 总有定义 |
| 状态 | **open** |
| 论证 | (i) codomain 未声明：ℕ\* 排除负数，而 release 多于 create 时 net<0 必然出现（如先 QueueFree 后 Instantiate 的合法代码路径，或 §7.7 Stop() 单独出现）；候选 D = ℤ∪{⊤} 或整区间 [lo,hi], lo∈ℤ，均未定义。(ii) 区间点态减法 [a,b]−[c,d]=[a−d,b−c] 保持 lo≤hi 但可产生负端点；文档既未采用也未排除。(iii) ⊤−x 与 x−⊤ 的传播律缺位：create size=[1,⊤] 配 release [64,64] 时 net 结果未定义，L320 的 fail-closed 规则只覆盖 mode=Unknown 的 claim，**不覆盖 size=⊤ 的已知 claim**，存在静默漏报窗口。(iv) 泄漏判定的 `net(S,scope)>0`（L319）：`>` 只在 ℕ\* 上定义（L228），Interval 上与 0 的比较未定义（[lo,hi]>0 意为 lo>0？lo≥1 即可？），DO-9 的可判定性因此悬空。 |
| 行号 | L207–208, L224–228, L312–320 |

### 命题 N9：read/write 度量良定义

| 项目 | 内容 |
| ---- | ---- |
| 数学性质 | read/write : Signature → SizeVal∪{⊤} 全函数 |
| 状态 | **open（弱于 net/peak）** |
| 论证 | L343–344 同样使用未定义的 Interval 求和（承 N1-i）；且无 scope 参数，与 net(S,scope)/Peak(S,scope) 的接口不对称——无法回答「某方法内读量」，AUDIT001（L944，60/sec 读频报警）所需的 per-scope read 无法由此公式导出。不过不过滤 mode 对 read/write 语义可辩护（读释放仍是读），不算矛盾。 |
| 行号 | L341–344, L944 |

## Proof Obligation 账本表

| ID | 命题 | 状态 | 消解所需最小补充 | 行号 |
| ---- | ---- | ---- | ---- | ---- |
| PO-I3-01 | net 全函数性（区间和+差） | open | 新增 §3.1.5d：定义 Interval 加法 [a,b]+[c,d]=[a+c,b+d]（⊤ 律沿用 3.1.5a）与差值载体 NetVal := ℤ·Interval∪{⊤} 及其减法/比较律 | L207, L312 |
| PO-I3-02 | net(S,Global)=net(S) | discharged | （无；已证，依赖 PO-I3-01 落地后复核） | L141, L312 |
| PO-I3-03 | net 对 ∪ 的代数地位 | open | 文档显式声明：net 非同态，仅 Claim-不相交并可加；并声明 (S;S)=S 下重复分配计一次为已知保守限制 | L252, L354 |
| PO-I3-04 | Peak 量纲隔离 | open | Peak 公式加 `c.kind=occupy` 过滤或改写为 per-kind 参数化 Peak\_k；删除或重写恒真的 weight(c.kind,c.kind) 项，KIND_MIX 移到桶级聚合层定义 | L327, L337, L199 |
| PO-I3-05 | Peak ≥ net | asserted | PO-I3-01/04/06 落地后在 ℕ\* 数值域证明（ω=1 平凡；ω>1 需先解 PO-I3-06） | L312, L327 |
| PO-I3-06 | (S×ω) 良定义 | open | 将 copy\_i 的 i 编入 Claim（如 scope=Loop(id,i) 或新增 iteration 字段），或将 (S×ω) 显式定义为多重集并给 Peak/net 的多重集版本 | L293–301 |
| PO-I3-07 | peak 谓词量纲封闭 | open | SizeVal 增加单位维度 Unit（MB/count/…），Peak/net 强制按 (resource-class, Unit) 分组聚合；AUDIT003 预算比较限定同 Unit | L207, L958 |
| PO-I3-08 | net 值域与负值 | open | 同 PO-I3-01 载体；补 ⊤−x=x−⊤=⊤ 保守律（fail-closed）；明确 size=⊤ 的 occupy claim 使 net 项输出 ⊤ | L312–320 |
| PO-I3-09 | net>0 可判定 | open | 定义 Interval 与 0/预算的比较序（[lo,hi]>0 :⇔ lo>0，⊤ 参与 ⇒ 需人工界定） | L228, L319 |
| PO-I3-10 | read/write 良定义 | open | 承接 PO-I3-01 的区间加法；补 read(S,scope)/write(S,scope) 分组版本以支撑 AUDIT001 | L341–344 |

## 新发现缺口清单

1. **G-I3-01（高，内部矛盾）**：Peak 公式（L327/L337）无 kind 过滤，read/write claim 混入「占用峰值」，与 L199 分桶规则、DO-7、A3 判据（L1032）三方冲突；weight(c.kind,c.kind)≡1 使 weight 在公式内永不起作用。
2. **G-I3-02（高）**：SizeVal 升级为区间（L207）后，§3.1.5a 未同步升级——区间加法、区间减法、区间与 0 的序均未定义；net/read/write/Peak 四个度量的核心算子全部悬空（v3.0-FINAL 修订 C 声称「size 用 SizeVal 故返回 ⊤ 不 NaN」（L307），实际只处理了标量 ⊤，未处理区间载体）。
3. **G-I3-03（高）**：(S×ω) 副本坍缩（L296），ω 对 net/Peak 数值无效，DO-8「循环内资源分配报警」缺乏数值语义；「上界开放的重复副本集合」（L297）为无定义对象。
4. **G-I3-04（中）**：size 无单位/资源类维度，跨资源 size 求和（MB+个数）在 net/Peak 中发生，AUDIT003 的 VramMB 对账（L956–958）无机制支撑。
5. **G-I3-05（中）**：net 可为负但值域未声明；L320 fail-closed 只覆盖 mode=Unknown，不覆盖 size=⊤，存在静默低估窗口（例：release [64,64] 已知、create [1,⊤] 未知 ⇒ 净变化应为 ⊤ 而非 −63）。
6. **G-I3-06（低）**：L199 引用「见 3.3.2b」——全文不存在 §3.3.2b 小节，悬空引用。
7. **G-I3-07（低）**：read/write（L341–344）无 scope 参数化版本，与 net(S,scope)/Peak(S,scope) 接口不一致，AUDIT001（per-frame 读频）无法由 §3.3.3 导出。
8. **G-I3-08（低，记录性）**：Set 幂等语义吞并重复分配（N3 反例），MA-004「已解决」（L354）表述过强——解决的是 net/peak 混淆，未解决重复计数问题，建议降格表述并列为本体论限制。
