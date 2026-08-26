# Iter10a 独立审计
对象：`PDR_Effect_Cost_Algebra_v3_FINAL.md`（v3.0-FINAL-rA6，1100 行）。范围：§8.1 白名单/默认规则（L694–715）、§8.2 ED-001..ED-004（L721/747–749）、关联 §7（L600–688）、§8.3（L723–745）。仅基于该文档磁盘内容。

## 结论摘要
- **内部矛盾（确证）**：L697 声称白名单「约 100 个方法」，§7 实际仅 **38 个表行**（含 getter/setter 合并行，至多 ~44 个可区分 API）；L860 工作量表重复同一「100」声称。
- 默认规则经 iter44 修订后方向正确（mode=Unknown，fail-closed），但**机制不闭合**：kind=Unknown 越出 Def 3.1.1 定义域，且与 §3.1.4b 分桶、§3.3.1 的 ⊤ 兜底衔接未定义。
- Unknown 资源相等规则（Unknown=Unknown）造成**二次冲突放大**：n≥2 个未映射调用即产生 C(n,2) 对伪冲突候选。
- release-class 清单 7 项中 4 项（free/remove_from_group/cancel_free/free_children_in_group）无 §7 映射，违反自身 L703 强制条款。
- [EffectOverride] 信任边界收窄但仍以人工 reason 为唯一证据载体，机器不可校验部分为 open。

## 逐命题审计

### P1 白名单「约 100 个」声称
- **数学性质**：|Whitelist| = |§7 表行去重后的 API 集|；实测 38 行（L606–688），getter/setter 合并计 2 则 ≈ 42–44。
- 状态：**asserted 且与事实矛盾（discharged 为否定）**
- 论证：grep 计数 `^| \`` 共 38 行；L697/L860 两处「100」无任何枚举支撑。行号：L697、L860、L600–688。
- 判定：文档内部矛盾，须改为「约 40」或补齐枚举。

### P2 默认规则对未映射 API 不漏报 occupy/release
- **数学性质**：∀M 含未映射调用 u：net(S(M)) = ⊤（保守上界）⇒ DO-9/AUDIT002 必报。
- 状态：**open（条件 discharged）**
- 条件证明：若「Unknown 在 net 中计为 ⊤」（L316 注释）被落实为机械规则——即默认 Claim 触发聚合器返回 ⊤——则泄漏不可能静默（⊤ 与任何阈值比较均报警）。但该机制未落地：默认 emit 的 Claim（L698）kind=Unknown ∉ Sig_occupy 桶（§3.1.4b），net 只对 c.kind=occupy 求和（L310–314），该 Claim **永不进入 net**。L316 的 ⊤ 兜底与 L698 的默认 emit 是两套未接线的规则。
- 缺口：需规定「Signature 含任一 kind=Unknown Claim ⇒ net/Peak(S,·)=⊤」为公理级后置条件。

### P3 默认规则不给纯读 API 强加 write
- **数学性质**：默认 mode=Unknown 按 use 处理（§3.2.3 P4）⇒ Compatible(use,·) 恒真 ⇒ 无伪写冲突。
- 状态：**discharged（有代价）**
- 论证：L698–702 明确 mode=Unknown 最弱处理；纯读 API 不会被强加 write/create。代价：对真实占用资源的未映射 API，冲突检测**漏报**（A4 completeness 在含未映射调用的程序上失效），此为已知权衡而非矛盾。行号：L698–702、L1029。

### P4 Unknown 资源标识的冲突放大
- **数学性质**：Claim 相等规则规定 Unknown=Unknown（L188）⇒ n 个未映射调用的 create 类 Claim 判同资源 ⇒ 伪 CONFLICT 对数 = C(n,2)，随 n 二次增长。
- 状态：**asserted（反例构造成立）**
- 论证：两个互不相关的未映射 API 各 emit create(Unknown,·,create,scope)，按 3.1.4a 五元组逐字段相等判同一资源，落入 §3.2.3 CONFLICT={(create,create)} ⇒ 误报。反之若按 use 放行则回到 P3 的漏报侧。文档同时主张「Unknown=Unknown」（归一需要）与「fail-closed 需人工确认」（L701），未说明冲突判定走哪支。
- 缺口：需为 resource=Unknown 单列判定规则（如：Unknown 资源一律不参与自动 CONFLICT，改报 UNKNOWN_RESOURCE 人工诊断）。

### P5 [EffectOverride] 信任边界
- **数学性质**：override 后的 Signaturê 与真实效应 Signature 的一致性不可静态判定（依赖 reason 中外部证据）；形式化为：∀ov∈Override：Siĝ(ov(M)) = Sig(M) 非定理，仅为假设。
- 状态：**partially discharged / 边界 open**
- 论证：§8.3.1（L727–736）已封堵最危险通道——禁覆盖 kind（量纲不可篡改）、禁压制 DO-9 根因、禁跨 kind 豁免、CI 人工 approve。剩余信任缺口：(i) `target: Claim | resource` 粒度到 resource 时可一次覆盖多条 Claim，放大面未限；(ii) 「真实 release 路径证明」无格式规范；(iii) override 数量无上界，理论上可用 N 个 override 把白名单外 API 全部洗白，等价于重建黑名单逃逸通道。行号：L729–736。

### P6 ED-001「已收敛」真伪
- 状态：**asserted（收敛声明超前）**
- 论证：ED-001（L721）的收敛依赖三要素：白名单完整（P1 否证其声称规模）、默认规则接线（P2 open）、override 校验（P5 半开）。三要素均非 fully discharged，「已收敛」应降级为「条件收敛，依赖 PO-10a..c」。另注意 §8.2 表格结构破损：ED-002..004 行（L747–749）出现在 §8.3 定义块之后，markdown 表格已被 L723–745 的定义文本截断，属排版级矛盾。

### P7 ED-002 getter/setter 区分
- **数学性质**：属性访问点 p 的效应 = getter 映射 iff 语法角色为读。
- 状态：**discharged（设计层）**；论证：§7.2 显式分列 getter/setter（L617–621），L2 Generator 语法树区分可行且无歧义；测试矩阵未见对应正/反例，实现验证 open。

### P8 ED-003 回调/委托保守估计
- 状态：**open**。「动态委托保守估计」（L748）无定义：保守到什么 mode/size 未给；Connect 右侧方法分析（静态具名回调）与 λ 闭包捕获的效应归属规则缺失。§7.5 Connect 仅映射 callback 占用，不推导被连方法的 Signature 进入调用方。

### P9 ED-004 动态 Instantiate
- **数学性质**：size ∈ {[s,s]（显式 .tscn）, [1,⊤]（变量场景）}，⊤ 经 §3.1.5a 律传播至 net/Peak。
- 状态：**discharged**；论证：§3.1.5(c)（L213）+ §12.2 AUDIT002 文案（L950）已统一为 [1,⊤]。残留措辞矛盾：L749 仍写「标记为 ∞」，∞ 是已废弃载体（MA-002 收口后应为 ⊤），建议同步修订。λ 形式 `Finished += () => fx.QueueFree()`（L952）的跨语句控制流配对算法未给出，A1 判据（L1030）在该模式下 completeness 为 asserted。

## Proof Obligation 账本

| PO | 内容 | 来源 | 状态 |
| ---- | ------ | ------ | ------ |
| PO-10a | 默认 Unknown Claim ⇒ net/Peak 返回 ⊤ 的公理化接线 | P2 | open |
| PO-10b | Unknown 资源的 CONFLICT 判定专规（消除 C(n,2) 放大） | P4 | open |
| PO-10c | [EffectOverride] 按 resource 粒度的覆盖上限与证明格式规范 | P5 | open |
| PO-10d | release-class 4 项（free 等）的 §7 Claim 映射补全或显式豁免 | P6/§8.1 | open |
| PO-10e | 白名单计数修正（100→实际枚举数）及 §7 补齐至声称规模 | P1 | open |
| PO-10f | ED-003 动态委托的保守估计形式化 | P8 | open |
| PO-10g | L749 「∞」→「⊤」术语同步；§8.2 表格结构修复 | P6/P9 | open |
| PO-10h | λ 回调 QueueFree 配对的控制流分析规范（支撑 A1） | P9 | open |

## 新发现缺口清单
1. **L697/L860 与 §7 的 100-vs-38 数量矛盾**（P1，文档内部矛盾，最高置信）。
2. **L698 默认 emit 与 L316 net ⊤ 兜底未接线**（P2，机制断裂，DO-9 完备性悬空）。
3. **Unknown=Unknown 引起二次伪冲突放大**（P4，反例可机械构造）。
4. **release-class 自我违约**：清单要求「不得落入默认规则」但半数字段无映射（PO-10d）。
5. **同名重载歧义**：`Stop()` 在 §7.7（音频释放）与 §7.10（动画释放）两处映射不同资源集（L664/L687），白名单若按方法名索引则键冲突，需按 (类型, 方法) 键控。
6. **§8.2 表格被 §8.3 截断**：ED-002..008 行落在定义块之后，机器解析该表会失败（排版矛盾）。
