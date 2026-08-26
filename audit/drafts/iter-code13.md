# 迭代13 审计（PDR 出处注释完整性）

## 摘要
- 构建：0 错误 0 警告（已核实：`dotnet build src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj` → 已成功生成 / 0 个警告 / 0 个错误）
- 审计范围：8 个 L1 源文件（Numeric / Objects / Algebra / SignedNet / Deviation / ApiMapping / DerivedMetrics / EffectAttributes）。只读代码 + PDR；未读 `audit/` 任何文件。
- open 项总数：2（可闭 2 / 设计 out-of-scope 0）
- 终止判定：**需继续(2)** —— 闭 OPEN-1/OPEN-2（注释级，改 § 号即止）后即达「覆盖率 100% + 抽查 § 号均准确 + 无错挂」可终止标准。

## 逐文件 public 符号出处核对（回指行号 + 实际 § + 准确? + 结论）

### Numeric.cs
| 符号 | 行号 | 所引 § | PDR 标题（核对） | 准确? | 结论 |
|---|---|---|---|---|---|
| NatStar | L7 | §3.1.5a | 定义 3.1.5a（⊤ 在 +/×/max/min/compare 上的运算律） | 真 | OK |
| NatStar.IsTop / .Value | L10/L13 | §3.1.5a | 同上（⊤ 标记 + 有限值） | 真 | OK |
| NatStar.Top / .Of | L18/L21 | §3.1.5a | 同上 | 真 | OK |
| operator + / * | L24/L28 | §3.1.5a | 加/乘 ⊤ 律（x+⊤=⊤；x×⊤=⊤(x>0)；0×⊤=⊤ 保守） | 真 | OK |
| Max / Min | L31/L34 | §3.1.5a | max(x,⊤)=⊤；min(x,⊤)=x | 真 | OK |
| CompareToFinite | L? | §3.1.5a compare | compare：∀x, x<⊤；⊤=⊤ | 真 | OK |
| Interval | L59 | §3.1.5 / §3.1.5b | 定义 3.1.5（SizeVal：区间）/ 3.1.5b（区间相等与合并） | 真 | OK |
| Interval.Lo / .Hi | L62/L65 | §3.1.5 | SizeVal 上/下界 | 真 | OK |
| Interval ctor | L69 | §3.1.5 | lo≤hi 不变量（[⊤,x] 非法） | 真 | OK |
| Interval.Default | L81 | §3.1.5(d) | **错挂**（见 OPEN-2）：缺省 [1,1] 实为 §3.1.5(a)「代数缺省 → [1,1]」；(d) 是 AUDIT002/003 启发式注记 | **否** | **OPEN-2** |
| Interval.Dynamic | L84 | §3.1.5(c) | 动态 Instantiate 变量场景 → [1,⊤]（原 ED-004 的 ∞） | 真 | OK |
| Interval.Exact | L87 | §3.1.5 | 单值 s ⇔ [s,s]（(b) 显式精确） | 真 | OK |
| Interval.Merge | L90 | §3.1.5b | merge_I join-semilattice（min/max 内嵌 ⊤ 律） | 真 | OK |
| DeviationVal | L100 | §3.1.5c | 定义 3.1.5c（DeviationVal：偏差载体） | 真 | OK |
| DeviationVal.IsTop/.Value | L103/L106 | §3.1.5c | ⊤ 标记 + 有限偏差 | 真 | OK |
| DeviationVal.Top | L111 | §8.3.2/§9.1 | 类型定义 §3.1.5c，字段注引「不触发普通数值报警（§8.3.2/§9.1）」——相关章节，非错挂 | 真（相关） | OK |
| DeviationVal.Of | L114 | §3.1.5c | 有限偏差构造 | 真 | OK |
| DeviationVal.ExceedsThreshold | L117 | §9.1 | 先判 ⊤ 再比数值 | 真 | OK |

### Objects.cs
| 符号 | 行号 | 所引 § | PDR 标题 | 准确? | 结论 |
|---|---|---|---|---|---|
| Rid / StringName | L9/L12 | §7（映射层转换） | §7 Godot API 映射层 | 真（相关） | OK |
| ResourceId | L19 | §3.1.2 + §3.1.2b | 定义 3.1.2（ResourceId）/ 3.1.2b（合成资源命名空间） | 真 | OK |
| ResourceId.Tree/Self/Physics/Disk/Signal/Gpu/AudioMixer/Network/Custom | L22-L34 | §3.1.2 | 定义 3.1.2 资源构造子 | 真 | OK |
| ResourceId.Memory | L25 | §7 | 裸 'memory' ⇒ Memory(uid="mem")：**类型定义是 §3.1.2，注释仅引 §7 裸名映射**（§3.1.4a 亦有缩写表）。非错挂但 §3.1.2 主定义漏引（低严重度，见备注 R1） | 弱 | 备注 R1 |
| ResourceId.Occupancy / Callback / Input | L30/L31/L33 | §7 | 同上（audio_channel/animation_state/Connect callback/input 裸名）。主定义 §3.1.2 漏引 | 弱 | 备注 R1 |
| ResourceId.CommandBuffer / SignalBus | L37/L38 | §3.1.2 + §3.1.2b / §3.1.4a | 完整双引 | 真 | OK |
| ResourceId.Normalize | L? | §3.1.4a | 定义 3.1.4a（Claim 相等/归一化） | 真 | OK |
| NodePathOrUnknown | L64 | §3.1.2 + §3.1.4a | 路径/Unknown | 真 | OK |
| ScopeId | L85 | §3.1.3 + §3.1.3b | 定义 3.1.3b（ScopeId 偏序 ⊆*） | 真 | OK |
| ScopeId 七记录 + IncludedIn | L87-L97 | §3.1.3b | 各 scope 标签 + ⊆* 查表 | 真 | OK |
| **Kind** | **L107** | **§3.2.3** | **错挂（见 OPEN-1）**：kind ∈ {read,write,occupy} 来自 §3.1.1（Claim 五元组）/ §3.1.4b（按 kind 分桶）；§3.2.3 是「Compatible：全函数+对称」，论 *mode* 对，不定义 kind 枚举 | **否** | **OPEN-1** |
| Mode | L112 | §3.2.3（P4） | mode 五值与 Unknown 按 Use 处理（P4）均在 §3.2.3 显式枚举/治理；§3.1.1 仅列前四值。注释聚焦 P4，§3.2.3 属权威源 | 真（可证） | OK |
| Claim | L119 | §3.1.1 | 定义 3.1.1（Claim := (kind,resource,mode,scope,size?)） | 真 | OK |
| Claim.Normalize | L122 | §3.1.4a | 归一化 | 真 | OK |
| Claim.CompatibleWith | L129 | §3.2.3 | 全函数对称单元调用 | 真 | OK |
| Signature | L? | §3.1.4b | 定义 3.1.4b（按 kind 分三桶，DO-7 量纲隔离） | 真 | OK |
| Signature.Read/Write/OccupyClaims | L143-L145 | §3.1.4b | 三桶量纲隔离 | 真 | OK |
| Signature.Empty / .Of / Union | L? | §3.2.1 / §3.2.2 | 定义 3.2.1（顺序 ∪）/ 3.2.2（并行 ∥，均为 ∪） | 真 | OK |
| Signature.Join | L? | §3.2.4 ⊔ | 定义 3.2.4（⊔ join-semilattice） | 真 | OK |
| Signature.Net | L? | §3.3.1 | 定义 3.3.1（net(S,scope)） | 真 | OK |

### SignedNet.cs
| 符号 | 行号 | 所引 § | PDR 标题 | 准确? | 结论 |
|---|---|---|---|---|---|
| ZStar | L9 | §3.3.1 | 有符号网值 ℤ*（net=Σcreate−Σrelease 可负，不能复用 §3.1.5a 非负 ℕ*） | 真 | OK |
| SignedInterval | L49 | §3.3.1 | 有符号区间 [lo,hi]，lo≤hi，端点∈ZStar | 真 | OK |
| .ContainsZero / .Merge / .TryMid | L71/L74/L77 | §3.3.1 / §9.1 | DO-9 含 0 判定 / net 区间合并 / mid-range（§9.1 用） | 真 | OK |

### Algebra.cs
| 符号 | 行号 | 所引 § | PDR 标题 | 准确? | 结论 |
|---|---|---|---|---|---|
| Compatible | L11 | §3.2.3 | 定义 3.2.3（全函数+对称，CONFLICT 集，P4 Unknown→Use） | 真 | OK |
| Weight | L34 | §3.3.2b | weight: Kind×Kind → ℝ∪{⊥}（§3.3.2b/KIND_MIX） | 真 | OK |
| NetTable | L45 | §3.3.1 | 定义 3.3.1（净变化 Net，仅 occupy 桶） | 真 | OK |
| NetTable.Compute / .IsConserved | L53/L94 | §3.3.1 | net 分组 / DO-9 守恒（含 0 / ⊤ fail-closed） | 真 | OK |
| Peak | L109 | §3.3.2 | 定义 3.3.2（峰值 Peak，size 求和，ω=⊤⇒⊤，c.mode≠release） | 真 | OK |

### ApiMapping.cs / Deviation.cs / DerivedMetrics.cs / EffectAttributes.cs
| 符号 | 行号 | 所引 § | PDR 标题 | 准确? | 结论 |
|---|---|---|---|---|---|
| ApiMapping / GodotApiWhitelist | L12/L29 | §7 | 定义 §7（Godot API 的 Claim 映射，§7.1–§7.10） | 真 | OK（逐条 §7.x 注释齐备） |
| ReleaseClass | L178 | §8.1 | §8.1 自动推导规则（release-class 白名单，7 项与 PDR 一致） | 真 | OK |
| SignatureDeviation / .Calculate | L10/L21 | §9.1 | §9.1 开发模式验证（Deviation = Σ|Δmid|/max(range,ε)，ε=1） | 真 | OK |
| .ExceedsThreshold | L? | §9.1 / §3.1.5c | 复用 DeviationVal.ExceedsThreshold | 真 | OK |
| LoopCount | L9 | §3.2.5 | 定义 3.2.5（ω ∈ ℕ∪{⊤}） | 真 | OK |
| Combination | L28 | §3.2.5 / §3.2.1 / §3.2.2 | S×ω 循环组合 / 顺序∪ / 并行∪ | 真 | OK |
| Derived | L67 | §3.3 | 派生度量封装（net/Peak/守恒） | 真 | OK |
| EffectOverrideAttribute | L17 | §8.3.1 | 定义 8.3.1（[EffectOverride]，仅覆盖 mode/size/scope，禁覆盖 kind，reason 强制） | 真 | OK |
| AcceptDeviationAttribute | L58 | §8.3.2 | 定义 8.3.2（[AcceptDeviation(ε)]，ε∈[0,0.5] 构造子强制） | 真 | OK |

## 覆盖率结论
- **逐 public 类型/方法/属性/ctor 是否都带 §x.y？** 是。8 个文件全部 public 符号（含 record 构造子、enum、属性、方法、operator、static 字段）均带 § 出处注释；唯一软缺口 `DeviationVal.Top` 字段注引相关章节 §8.3.2/§9.1 而非 §3.1.5c，但类型主定义在 struct 级 summary 已引 §3.1.5c，不构成漏引。
- **遗漏清单**：无硬性遗漏（覆盖率 100%）。

## open 项清单
| # | 位置 | 问题 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|---|
| OPEN-1 | Objects.cs L107 `Kind` enum | 注释「§3.2.3 — 效应种类」错挂：`kind ∈ {read, write, occupy}` 来自 **§3.1.1**（Claim 五元组定义）与 **§3.1.4b**（Signature 按 kind 分三桶）；§3.2.3 是「Compatible：全函数 + 对称」，仅论 *mode* 对（16 对），根本不定义 kind 枚举。审阅者按 §3.2.3 查不到 kind 定义，出处失准。 | 可闭（注释改 § 号） | 低 | 改注释为「§3.1.1（Claim 的 kind ∈ {read,write,occupy}）/ §3.1.4b（按 kind 分桶量纲隔离）」。enum 穷举由 C# 类型本身保证，与 §3.1.4b DO-7 一致。 |
| OPEN-2 | Numeric.cs L81 `Interval.Default` | 注释「§3.1.5(d) 缺省 size ⇔ [1,1]」子标签错挂：PDR §3.1.5 取值来源统一中，(a)=「代数缺省 → [1,1]」、(d)=「AUDIT002/AUDIT003 启发式 → 仍用 (a)(b)」。缺省 [1,1] 属 (a) 而非 (d)。按 § 号检索会指向启发式注记，语义不对应。 | 可闭（注释改 § 号） | 低 | 改注释为「§3.1.5(a) 缺省 size ⇔ [1,1]」（或 §3.1.5 + (a)）。 |

## 备注（非 open，低严重度）
- **R1（完整性，非错挂）**：`ResourceId.Memory`(L25)/`Occupancy`(L30)/`Callback`(L31)/`Input`(L33) 注释仅引 §7 裸名映射，未引其主定义 §3.1.2。§7 确实论及这些裸名（memory/audio_channel/callback/input 在 §7 映射表出现），故非错挂，但 §3.1.2 是类型权威定义，建议补引「§3.1.2 + §7（裸名）/ §3.1.4a（缩写映射）」以与 CommandBuffer/SignalBus 同格式。不阻塞终止。

## 结论
- 覆盖率 100%：8 个 L1 文件的所有 public 符号均带 `§x.y` 出处（含 record 构造子、enum、operator、属性、方法、static 字段），无硬性遗漏；构建 0 错误 0 警告实证。
- 抽查 ≥10 个关键符号的 § 号均与 PDR 实际章节标题匹配且语义契合：NatStar(§3.1.5a)/Interval.Merge(§3.1.5b)/DeviationVal(§3.1.5c)/ScopeId(§3.1.3b)/Claim(§3.1.1)/Compatible(§3.2.3)/NetTable(§3.3.1)/Peak(§3.3.2)/Weight(§3.3.2b)/LoopCount(§3.2.5)/ApiMapping(§7)/ReleaseClass(§8.1)/EffectOverride(§8.3.1)/AcceptDeviation(§8.3.2)/SignatureDeviation.Calculate(§9.1) 均真落实。
- **无错挂（除 2 处子/标签错误）**：绝大多数注释同时承载「类型约束不了的语义」——fail-closed（Unknown→Use §3.2.3 P4、⊤ 不报警 §9.1/§3.1.5c、DO-9 含 0 判据 §3.3.1）、控制流近似（L2/L3 解析、运行期 Σnet 权威）、量纲隔离（§3.1.4b/§3.3.2b KIND_MIX），满足「类型约束数学边界 + 注释承载语义与 §x.y」铁律。
- 仅 2 处出处错挂（OPEN-1 Kind 引 §3.2.3→应为 §3.1.1/§3.1.4b；OPEN-2 Interval.Default 引 §3.1.5(d)→应为 §3.1.5(a)），均注释级、可闭、不影响编译与运行正确性。
- 终止判定：**需继续(2)** —— 修正 OPEN-1/OPEN-2 的 § 号后即达「覆盖率 100% + 抽查 § 号全准确 + 无错挂」可终止标准。
