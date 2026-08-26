# Iter11a 独立审计

## 范围 / 结论摘要
- 范围：§9.1 CalculateDeviation 良定义性（range=0 除零、SizeVal 载体一致性、Σ 索引对齐）；§9.2 Release 零开销（#if DEBUG vs [Conditional] 双机制；Mono.Cecil 剥离证明力）；RT-001..006 收敛真伪复核。
- 结论：除零已闭合（discharged，附条件）；**Σ 对齐规则缺失为最大 open 缺口**；§9.1 的 [Conditional] 机制与 §9.2/DO-6 的「完全剥离」断言构成**文档内部矛盾**；Mono.Cecil 扫描作为检测器可判定，但作为剥离证明不充分（扫描规范未定义）。

## 命题 1：range=0 除零防护
| 项 | 内容 |
| ---- | ---- |
| 命题 | 分母 `max(expectedᵢ_range, ε)`, ε=1（L784-786）对一切合法输入 > 0 |
| 数学性质 | 全函数性：∀interval [lo,hi], lo≤hi ⇒ range=hi−lo≥0 ⇒ max(range,1)=1..∞，分母∈[1,+∞)，无除零、无 NaN |
| 状态 | **discharged**（条件：size 为 §3.1.5 合法区间且 ε 固定为 1） |
| 论证 | lo≤hi 由 3.1.5 定义直接给出（L226）；range≥0 故下界 ε=1 恒生效。原缺陷（L780 自述 range=max−min 得 0）已被消除 |
| 行号 | L780-786, L825 |

## 命题 2：载体一致性（SizeVal ↔ DeviationVal）
| 项 | 内容 |
| ---- | ---- |
| 命题 | expected 端 SizeVal→(mid,range) 映射良定义；结果落在 DeviationVal=double∪{⊤}（L239-244） |
| 数学性质 | mid=(lo+hi)/2 要求运算域 ℝ；ℕ 上整数除法会截断（文档未声明）；⊤ 端由「任一端为 ⊤⇒项计 ⊤」短路（L785-786），与 3.1.5c 一致 |
| 状态 | **partially discharged**；两处 asserted/open：(a) mid 的数值域（ℝ vs 整数截断）未声明；(b) **L242 称「任一端」含 actual 端，但 actual 的载体（SampleActualResources 返回类型）全文未定义**——若 actual 非 SizeVal，「端为 ⊤」对其无意义 |
| 反例/张力 | L242 的「任一端」预设 actual 也是区间载体，与 §9.1 代码仅 expected 有区间的表述不一致 |
| 行号 | L239-244, L780-786, L772 |

## 命题 3：Σ 索引对齐规则
| 项 | 内容 |
| ---- | ---- |
| 命题 | Deviation = Σᵢ |actualᵢ − midᵢ| / max(rangeᵢ, ε) 的求和指标 i 良定义 |
| 数学性质 | i 的域未绑定（自由变量）：需指明 i 遍历 expected∩actual 按 Claim= 配对的集合；actual∖expected（多占资源）与 expected∖actual（少占资源）如何计价未定义 |
| 状态 | **open（本审计最高优先缺口）** |
| 论证 | 对比 §3.2.4 的 ⊔ 给出了显式按 3.1.4a Claim= 配对规则（L204-208），§9.1 无对应条款。缺配对规则时 Deviation 不可计算，RT-002「已收敛」过强 |
| 行号 | L784, L825；对照 L204-208 |

## 命题 4：Release 零开销——双机制语义差异
| 项 | 内容 |
| ---- | ---- |
| 命题 | 审计代码在 Release 中「完全剥离」（DO-6） |
| 数学性质 | 区分两个强度不同的命题：P1=无审计代码**被执行**；P2=无审计 IL**存在于产物**。[Conditional("DEBUG")]（L763）仅保证 P1：方法体 IL 仍编入程序集，仅直接调用点被编译器删除（且要求 void/无 out，反射/委托引用不剥除）。#if DEBUG 整类包裹（L797-801）可同时保证 P1+P2 |
| 状态 | **内部矛盾**：§9.1 EffectValidator 仅用 [Conditional]（L763），其 IL 必然存活于 Release 程序集 ⇒ 按 §9.2「审计代码存在则构建失败」（L803），CI 必失败；若 CI 为其开白名单，则 P2 剥离保证失效。DO-6「完全剥离」与 §9.1 实现不相容 |
| 行号 | L763（§9.1）, L795-803（§9.2）, DO-6（§1 表）, L826/L828 |

## 命题 5：Mono.Cecil IL 扫描的证明力
| 项 | 内容 |
| ---- | ---- |
| 命题 | CI 用 Mono.Cecil 扫描 IL 可验证剥离（L803, L828） |
| 数学性质 | 作为**判定器**：检查「具名类型/成员 ∉ 目标程序集」是可判定的机械性质（discharged，前提是给定扫描模式）。作为**证明器**不充分：①「审计代码」的匹配规范（类型名/命名空间/attribute/字符串字面量如 "[EffectAudit]"）未定义 ⇒ 谓词不可判定；②不覆盖传递闭包：Generator 内联生成、lambda 捕获、反射启动的后台采样线程（L758-761 异步线程）中的审计逻辑可换名存活；③扫描范围（主程序集 vs 全部引用链）未声明 |
| 状态 | **partially discharged / open**（规范缺失使「存在审计代码」非良定义谓词） |
| 行号 | L803, L826, L828 |

## Proof Obligation 账本
| PO# | 义务 | 来源 | 状态 |
| ---- | ------ | ------ | ------ |
| PO-11.1 | 分母 max(range,ε)>0 | §9.1 L784-786 | discharged（ε=1, lo≤hi） |
| PO-11.2 | mid 运算域声明（防整数截断） | §9.1 | open（小） |
| PO-11.3 | actual 载体定义 + 「任一端 ⊤」适用性 | L242 vs L772 | open |
| PO-11.4 | Σ 配对规则（按 Claim=，含 actual∖expected 计价方向） | §9.1 L784 | **open（阻塞级）** |
| PO-11.5 | 统一 §9.1 至 #if DEBUG 或重述 DO-6 为 P1（执行剥离而非 IL 剥离） | L763 vs L797/DO-6 | **open（矛盾待裁决）** |
| PO-11.6 | Cecil 扫描规范（pattern 集合、范围、传递闭包策略） | L803 | open |
| PO-11.7 | RT-001 性能声明需量化证据（采样开销上界） | L824 | asserted only |

## 新发现缺口清单
1. **N1（高）**：Deviation 的 Σ 对齐规则整体缺失——无 Claim= 配对、无单侧出现资源的计价语义；RT-002「已收敛」应降级为「分母防护已收敛、对齐未收敛」（L825）。
2. **N2（高）**：§9.1 [Conditional] 与 §9.2/#if + DO-6「完全剥离」三方矛盾；Cecil 扫描在该矛盾下无可满足的通过判据（L763/L797/L803）。
3. **N3（中）**：SampleActualResources / actual 载体未定义，导致 L242「任一端为 ⊤」对 actual 端悬空，且 DeviationVal 的 ⊤ 短路规则只对 expected 端可执行。
4. **N4（中）**：Cecil 扫描谓词「审计代码存在」未给匹配规范，PO-11.6 未清前 RT-005「已收敛」仅为 asserted。
5. **N5（低）**：mid=(lo+hi)/2 未声明实数运算域；ℕ 截断会使对称区间（如 [1,2]）的 mid 偏移，系统性低估偏差。
