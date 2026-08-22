# 迭代28 审计（全代码残差技术债）

## 摘要
- 全解构建：0 错误 0 警告（`dotnet build Cosmos.EffectAlgebra.slnx`，已核实，无 warning/error 行输出）。
- 测试：208 通过 0 失败（`dotnet test`，net10.0）。
- open 项总数：0（可闭 0 / 设计 out-of-scope 0）。
- 终止判定：**可终止**。
- 审计范围：src 下 8 个 L1 文件 + L2 Generator + L3 Analyzer；只读测试目录确认覆盖广度（不逐行）；PDR §3.x/§7/§8.x/§9.1/§14 核对。

## 逐文件残差核对（回指行号 + 结论）

### Numeric.cs（§3.1.5a/§3.1.5b/§3.1.5c）
| 符号 | 行 | 数学边界 | 类型强制 | 注释真实 | 结论 |
|---|---|---|---|---|---|
| NatStar | 7 | IsTop/Value 字段即 ⊤ 边界；+/×/max/min/CompareToFinite 内嵌 ⊤ 律 | ✓ readonly record + 私有 ctor；+/* 溢出 → Top 保守（无 checked，环绕检测） | ✓ §3.1.5a 标注 | OK |
| operator + / * | 24/28 | 溢出 → ⊤ 保守（MA-002），不 NaN 不发散 | ✓ 运算符内嵌，无运行时 if 漏判 | ✓ | OK |
| Min | 38 | `min(⊤,x)=x` 由 `IsTop?o:this` 实现 | ✓ | ✓ | OK |
| Interval | 59 | ctor 拦 `[⊤,x]` 非法 + lo>hi | ✓ 构造子抛异常，类型不可表达故注释契约 | ✓ | OK |
| Interval.Default/Dynamic/Exact | 81/84/87 | [1,1] / [1,⊤] / [s,s] 边界显式 | ✓ static readonly | ✓ 现引 §3.1.5(a)/(c)（iter13 修） | OK |
| Interval.Merge | 90 | join-semilattice，min/max 内嵌 ⊤ 律 | ✓ | ✓ §3.1.5b | OK |
| DeviationVal | 100 | IsTop + ExceedsThreshold 先判 IsTop | ✓ readonly record；ExceedsThreshold 类型安全 | ✓ §3.1.5c/§9.1 | OK |

### Objects.cs（§3.1.1/§3.1.2/§3.1.3b/§3.1.4a/§3.1.4b）
| 符号 | 行 | 数学边界 | 类型强制 | 注释真实 | 结论 |
|---|---|---|---|---|---|
| Rid/StringName | 13/16 | 内部原语别名，零 Godot 依赖 | ✓ readonly record | ✓ | OK |
| ResourceId (+16 构造子) | 19-40 | 判别联合；结构相等即标签完整性；Normalize 单点真相 | ✓ abstract record + sealed record，类型给不了归一故注释承载 | ✓ §3.1.2/§3.1.4a | OK |
| ResourceId.Normalize | 42 | Self/Signal/SignalBus "signal_" 前缀归一；Unknown 不混 | ✓ 穷举 switch | ✓ §3.1.4a | OK（iter25 修 SignalBus 前缀剥离） |
| NodePathOrUnknown | 49 | IsUnknown + Path；Unknown=Unknown 由结构相等 | ✓ | ✓ §3.1.4a | OK |
| ScopeId (+8 构造子) | 55 | ⊆* 偏序：自反/Global 最大元/跨标签不可比 | ✓ IncludedIn 内嵌查表 | ✓ §3.1.3b | OK |
| Kind | 91 | enum{Read,Write,Occupy} 穷举（非 §3.2.3） | ✓ 现引 §3.1.1/§3.1.4b（iter13 修） | ✓ | OK |
| Mode | 95 | enum；Unknown 按 Use（P4） | ✓ | ✓ §3.2.3 | OK |
| Claim | 101 | 位置记录五字段全必填；Normalize 归一 + size 缺省→Default | ✓ readonly record 无缺省构造；类型强制无漏字段 | ✓ §3.1.1/§3.1.4a | OK |
| Signature | 117 | 三不相交桶（DO-7）；跨桶聚合须 weight 注释契约 | ✓ 三 ImmutableHashSet 桶；Add 按 kind 分流 | ✓ §3.1.4b | OK |
| Signature.Union/Join/Net | 147/161/175 | ∪ 幂等（结构相等）；Net 仅 occupy 桶 + ⊆* 过滤 | ✓ | ✓ §3.2.1/§3.3.1 | OK |

### Algebra.cs（§3.2.1/§3.2.3/§3.3.1/§3.3.2）
| 符号 | 行 | 数学边界 | 类型强制 | 注释真实 | 结论 |
|---|---|---|---|---|---|
| Compatible.IsCompatible | 22 | 16 对全函数 + 对称；P3 良性配对 + CONFLICT | ✓ static，无未覆盖对 | ✓ §3.2.3 | OK |
| Weight.Of | 38 | Kind×Kind→ℝ∪{⊥}，跨 kind→NaN(⊥) | ✓ partial 函数，注释契约「⊥ 编码」 | ✓ §3.3.2b（见备注 R1） | OK |
| NetTable.Compute | 53 | 仅 occupy 桶 + ⊆* 过滤；release 有符号取负 | ✓ | ✓ §3.3.1 | OK |
| NetTable.IsConserved | 94 | 区间含 0 即守恒；无记录/⊤→false fail-closed | ✓ | ✓ §3.3.1 DO-9 | OK |
| Peak.Compute | 109 | size 求和；release 不计入（§3.3.2）；⊤→⊤ | ✓ | ✓ §3.3.2/§3.2.5 | OK |

### SignedNet.cs（§3.3.1）
| ZStar / SignedInterval | 9/42 | 有符号 + ⊤；Lo≤Hi 构造子校验；ContainsZero 类型安全 | ✓ readonly record + 私有 ctor | ✓ §3.3.1 | OK |

### Deviation.cs（§9.1）
| SignatureDeviation.Calculate | 21 | 分母 ε=1 防除零；⊤ 整体跳过；资源按 Normalize 对齐 | ✓ | ✓ §9.1（现注释明确「仅 occupy 桶净效应」，iter04 修） | OK |
| ExceedsThreshold | — | 复用 DeviationVal 类型安全 | ✓ | ✓ §9.1 | OK |

### ApiMapping.cs（§7/§8.1）
| GodotApiWhitelist.All | 29 | 38 条映射逐格对应 §7.1–§7.10；来源注释齐备 | ✓ 强类型数据 | ✓ | OK |
| ReleaseClass.Names | 178 | 7 项与 PDR §8.1 严格一致 | ✓ ImmutableHashSet，零 Godot | ✓ §8.1 | OK |
| 裸名哨兵 Mem()/AudioMx()/CmdBuf()/Occ()/Cb()/Inp() | — | 真实 UID 运行时填（注释明言） | ✓ | ✓ §3.1.4a/§7 | OK |

### DerivedMetrics.cs（§3.2.5/§3.3）
| LoopCount | 9 | ω∈ℕ∪{⊤}；IsTop 类型强制 | ✓ readonly record | ✓ §3.2.5 | OK |
| Combination.Loop/Sequence/Parallel | 28 | ×ω 缩放（⊤→上界开放）；序列/并行=∪ | ✓ | ✓ §3.2.1/§3.2.2/§3.2.5 | OK |
| Derived.Peak/Net/IsConserved | 67 | 真委托 L1，无重算 | ✓ | ✓ §3.3 | OK |

### EffectAttributes.cs（§8.3.1/§8.3.2）
| EffectOverrideAttribute | 17 | reason 非空构造子强制；无 OverrideKind（kind 禁止覆盖） | ✓ 类型强制 + ctor 抛 | ✓ §8.3.1 | OK |
| AcceptDeviationAttribute | 58 | ε∈[0,0.5] 构造子强制越界抛 | ✓ ctor 拦（double 无上界故 ctor 补） | ✓ §8.3.2 | OK |

### L2 Generator（§14 L2）
| EffectAlgebraGenerator | — | 识别 [EffectOverride]/[AcceptDeviation]；生成每方法 Compute 真委托 L1（GodotApiWhitelist + Signature.Union） | ✓ IIncrementalGenerator | ✓ 残差诚实（方法名↔白名单键规范化匹配，运行期 Σnet 权威） | OK |
| HasAttributeName/GenerateMethodSignature | — | X 与 XAttribute 双写匹配；canon 去 `.`/`_` 小写 | ✓ | ✓ §14 L2 | OK |

### L3 Analyzer（§8.3.1/§3.3.1 DO-9）
| EffectAlgebraAnalyzer | — | 仅发 DO-9 近似根因；零 Godot；数据取 §7/§8.1 | ✓ DiagnosticAnalyzer + SupportedDiagnostics | ✓ 控制流近似边界（跨方法/跨对象漏报）两处明言 | OK |
| AcquireApiNames/ReleaseApiNames | — | canonical 名集合预计算 | ✓ | ✓ §3.3.1/§8.1 | OK |

## 测试覆盖广度（读目录确认，17 测试文件 2810 行）
- 覆盖 src 8 文件：AlgebraLaws/Property/NetTableSigned/ScaleGuard/ResourceNormalization/BucketIsolation/ScopeOrder/LoopCombination/IntervalArithmetic/AttributeBoundary/CompatibleMatrix/CrossLayer/CrossTable/StabilityAudit/VerificationMatrix/EndToEnd/Tooling。
- §3.x 主要不变量：ℕ* ⊤ 律、Interval join、ScopeId 偏序/反对称/传递、Compatible 全表 25×25+随机、Normalize 幂等/SignalBus 等价、Deviation ε=1/⊤跳过、Net 有符号守恒、Peak ω=⊤、Loop ω 缩放、三桶隔离、§8.3 构造子边界、§7↔§8.1 跨表一致、§14 端到端 L2→L1→L3。
- 无核心符号明显未覆盖。

## open 项清单
无。全库已达「类型约束数学边界 + 注释承载类型约束不了的语义（控制流近似/运行期权威/fail-closed/§出处）+ 零技术债」。

## 备注（非 open，已确认）
- **R1（Weight 编码）**：`Weight.Of` 跨 kind 返 `double.NaN` 编码 ⊥，注释已明言「⊥ 编码，注释契约」。KIND_MIX 由类型层分桶 + 注释声明（§3.1.4b/§3.3.2b），未实现为独立 Analyzer 诊断——但 PDR §3.1.4b 三不相交桶 + Signature.Union 按 kind 分流已使跨桶混算在结构层不可能；本层未做独立 NaN 检查，属 PDR 侧既有设计（LANDING_PLAN §3 注释契约），非本代码技术债。
- **R2（0.2f / 0.5 字面量）**：`DeviationVal.ExceedsThreshold(0.2)` 与 `AcceptDeviation(0.5)` 阈值均以注释引 §9.1/§8.3.2 出处，且 ε 上界由构造子强类型拦截；0.2 为 §9.1 报警阈值（注释引用），非裸魔法数。
- **R3（Mode.Unknown 在 ApiMapping）**：§7 白名单无 API 标 Unknown（全为 Use/Create/Release/Move），与 §3.2.3 P4「Unknown 按 Use」一致；无 MissingDefault 隐患。

## 结论
- 全库满足用户铁律：每个 public 符号均有明确数学边界（构造不变量 / ⊤ 处理 / 量纲 / 偏序）+ §x.y 出处注释；类型能约束的（非空/required-等价位置记录/enum/readonly record/构造子抛/IsTop 字段）一律由类型强制，无「类型漏判靠运行时 if」；类型约束不了的（控制流近似、运行期 Σnet 权威、fail-closed ⊤ 不报警、归一前置条件、跨桶须 weight）均写在注释且带 § 出处。
- 无注释吹（注释称「类型保证」处类型确保证，如 EffectOverride 无 OverrideKind、AcceptDeviation ctor 拦边界）、无 TODO/FIXME/pragma 埋雷、无未引 § 的裸魔法数。
- L2/L3 真委托 L1，残差（编译期拿不到运行期实例 Claim、控制流近似漏报）诚实注释；跨层 L1 API 调用全部存在（GenerateMethodSignature 引 GodotApiWhitelist/Signature.Union/Of 均真实存在）。
- 测试 208 覆盖全部 8 文件 + §7/§8/§3.x 主要不变量，无核心未覆盖符号。
- 终止判定：**可终止（0 open）**。
