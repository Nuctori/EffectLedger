# Hickey-X R8: 命名与概念完整性 —— 名字是 API 的用户界面

> 视角：一个概念一个名字，一个名字一个概念；文档与代码必须说同一种语言。
> 来源说明：分析由 hickey-auditor 完成（thinking 阶段中断，主会话代笔落盘），**全部载荷行号已独立复核**。

---

## 〇、重大更正（对前轮结论）

**R5-C1「Join 文档承诺 merge_I 实为 set-union」已修。** Objects.cs:193-209 现为真 merge_I 实现：按 (Kind, Normalize(Resource), Mode, Scope) 配对、`cur.Merge(size)` 取区间 join（:204），注释明确引用 R4-F2 与 PDR §3.2.4 规格。本系列报告时工作树再次演进——R3 的时间线警告持续有效，消费旧结论必须逐条对当前磁盘复核。（Sequence/Parallel=Union 的名异实同仍在：DerivedMetrics.cs:50/:53 未变。）

## 一、命名普查（按四类裁决）

### (b) 名字撒谎（承诺≠交付）

| 名字 | 位置 | 撒的谎 | 状态 |
| --- | --- | --- | --- |
| `Claim.CompatibleWith(Claim)` | Objects.cs:168 区域 | 收两个 Claim 只比 Mode——不同资源的 create×create 也报不兼容 | 仍在（R5-C4） |
| `NatStar.CompareToFinite(NatStar)` | Numeric.cs | 名字暗示「对有限值比较」，实为全序比较（另一参数可为 ⊤） | 仍在本轮确认；建议 CompareTotal |
| `Budget.Caps` 类型 | EffectScript.cs:344 区域 | 声明非空 `IReadOnlyDictionary` 但 default(Budget).Caps==null——类型撒谎（代码注释自认 R10-F1） | 仍在（与 R2-N3 同族） |

### (c) 同概念多名

| 名字群 | 状态 |
| --- | --- |
| Union/Join/Sequence/Parallel（Join 已真分化） | **部分改善**：Join 现在有独立语义 ✓；Sequence/Parallel 仍=Union（DerivedMetrics.cs:50/:53） |
| Signature.Net vs Derived.Net；Peak class vs Derived.Peak | 跨层同名方法/类，语义口径还不同（R2-N5/R5 口径表）——双重碰撞 |
| Violation.Kind（字符串枚举）vs Kind enum（read/write/occupy） | **本轮新确认**：同名两概念，前者是违例类别后者是量纲桶——grep "Kind" 的用户必然混淆 |

### (d) 同名多概念 / 碰撞

- **EffectEvent.Loop（属性，LoopCount）vs Combination.Loop（静态方法）vs ScopeId.Loop（record）vs LoopCount（类型）**——「Loop」一词四用：名词属性、动词方法、作用域标签、并发数类型。已知碰撞的完整版。
- Weight 用 ⊥ 表示未定义（抛异常），全仓其余用 ⊤ 表示未知——同一文档体系里两个相反的「无穷/未知」符号约定，数学上分属 bottom/top 传统，无一处对照说明。

### (a) 正面记录

- Parse/ToJson 成对对称 ✓；NatStar/Interval/ZStar/SignedInterval 载体命名清晰一致 ✓；EAA 诊断 ID→§ 引用内嵌消息 ✓。

## 二、概念完整性缺口

| 缺口 | 说明 |
| --- | --- |
| At 无 multiset 变体 | At(t) 是去重视图，Audit 内部是 multiset 计数（R7 实验坐实）——需要 AtMulti 或文档声明唯一权威口径 |
| SerializeScope 只覆盖 4/8 子类 | Shell/Loop/Conditional/Async 有 C# 身份无 JSON 身份（R7-c 实测抛）——「有类型无契约」的半截概念 |
| ParseBudget 无 ⊤ 表达 & SerializeBudget ⊤→0 | R7-b 实测翻转——契约层缺「无上限」这个概念的拼法 |
| Violation 无事件索引 | gate(3) 手握 HashSet<int> 不上报（R6-E5）——「归因」概念只做了一半 |
| AuditResult 无覆盖面维度 | Passed 无法区分「查过通过」与「没查」（R2 HIGH-3）——「审计」概念缺「范围」成分 |

## 三、术语对齐表（文档 vs 代码）

| 文档说法 | 代码现实 | 裁决 |
| --- | --- | --- |
| EFFECT_SCRIPT.md §2.2：「`public readonly record struct EffectScript`」 | EffectScript.cs:55 `sealed partial class` | **文档撒谎**（struct/class 之差直接影响复制语义预期） |
| §2.1 Event 字段清单 3 个（Lifetime/Footprint/Loop） | EffectScript.cs:38 四参构造含 Scope | 文档过期（OPEN-1 修复未回写 §2.1 清单；§225 表格倒是提了） |
| **§4 示例 `"resource": {"gpu": {"bufferId":"mesh1"}}`（嵌套对象）** | Contract:161 ReqStr 要求 string——`{"gpu":"mesh1"}` 才合法 | **文档的旗舰示例无法通过自家 Parse**（ReqStr 对 Object 抛 FormatException）。AI 照抄官方示例必炸，且错误消息不会告诉它「去掉一层嵌套」 |
| README「453 passed」 vs EFFECT_SCRIPT.md §10.3「279 passed」 | 测试数漂移 | 低危一致性噪音 |
| PDR merge_I/⊆*/DO-7 等符号 | 代码注释大量自引 ✓ | 好 |

## 四、中英混用

错误消息主体中文（"resource 须含 gpu/commandBuffer/…"）、异常类型与 .NET 原生消息英文、注释中文为主夹英文术语。检索性影响真实存在但可控（关键术语如 PeakExceeded/Leak 本身英文）。LOW，建议保持现状但在 EFFECT_SCRIPT.md 错误速查表中英并列。

## TOP-3

1. **§4 旗舰示例无法通过自家 Parse**（嵌套 gpu 对象 vs ReqStr 要求 string）：文档是 API 的第一次承诺，示例是承诺的样板间——样板间进不去门。修一行文档即可消除最大的「第一分钟失败」源。
2. **Kind 双关**（量纲桶 enum vs 违例类别字符串）：同名两概念横跨 L1 与 Audit 输出，任何跨层搜索/重构都会踩。
3. **§2.2 struct/class 文档谎言**：直接误导用户的复制语义预期——文档声称的类型签名与磁盘代码不符，属于「契约文档失效」而非笔误。

## 文件清单

- 主会话复核行号：Objects.cs（:168/:193-209）、Numeric.cs（CompareToFinite）、EffectScript.cs（:22/:38/:47/:55/:344 区域）、DerivedMetrics.cs（:49-53）、EffectScriptContract.cs（:157-165）、EFFECT_SCRIPT.md（§2.1/§2.2/§4:139-141/§10.3）
- 子代理通读：README.md、EFFECT_SCRIPT.md 全文、PDR 目录结构、Generator、Analyzer 命名辅助段
