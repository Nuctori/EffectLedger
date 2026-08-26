# 迭代06 审计（§8.3 属性）

## 摘要
- 构建：**0 错误 0 警告**（已独立核实 `dotnet build`，MSBUILD_EXE_PATH 置空绕开坏 MSBuild，实测 0 errors）。
- open 项总数：**0**（可闭 0 / 设计层 out-of-scope 0）。所有 §8.3 约束均真落实于类型/构造子，注释承载类型不可表达的残留（size≥1、epsilon<0.2 警告延迟到 L2/L3）。
- 终止判定：**可终止（0）**——reason 非空与 epsilon 上界靠构造子强制、kind 不可覆盖靠类型层缺省属性强制、注释完整带 § 出处与不变式。

## 逐条核对（回指行号 + PDR § + 类型真约束? + 结论）

| 检查 | 代码行 | PDR § | 类型真约束? | 结论 |
|---|---|---|---|---|
| 1 EffectOverride reason 非空强制 | L62-66 `if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(...)` | §8.3.1 | **真**：构造子 `throw`，非空强制靠运行时类型边界，非注释吹 | OK |
| 2 kind 禁止覆盖 | 类体 L33-58，属性仅 `Reason/Scope/OverrideMode/OverrideSize`；无 `OverrideKind`、无任何 kind setter | §8.3.1 | **真**：类型层即无 kind 属性 ⇒ read/write/occupy 互转在类型上不可能（含 L2/L3 反射也无法设 kind，因属性不存在） | OK |
| 3 可覆盖域受限（mode/size/scope） | `OverrideMode`(L42) `OverrideSize`(L53) `Scope`(L37)；L18/L40-44 注释明言「DO-9 根因不压制、DO-7 不豁免」 | §8.3.1 | **真**：仅三属性暴露；注释声明不提供「豁免 DO-9」/「跨 kind 豁免」开关（类型层无对应成员） | OK |
| 4 AcceptDeviation epsilon 上界 | L88-92 `if (epsilon < 0.0 \|\| epsilon > 0.5) throw new ArgumentOutOfRangeException(...)` | §8.3.2 | **真**：构造子抛，[0.0,0.5] 边界强制（double 无上界 ⇒ 构造子拦，符合注释 L77「类型边界：double 无上界，故构造子拦」） | OK |
| 5 AcceptDeviation 语义 | L77-80 / L82-84 注释：仅放宽运行时 Deviation>阈值(§9.1 基础0.2f)局部报警；不豁免编译期 DO；作用域=标注对象 scope，不跨 scope 传播 | §8.3.2 | 注释完整承载（语义为 L2/L3 消费行为，L1 仅承载属性数据；此属类型不可表达，注释声明） | OK |
| 6 L1 零 Godot 依赖 / 是 Attribute | 文件 L1-8 无 `using Godot`；`EffectOverrideAttribute`/`AcceptDeviationAttribute` 均 `: Attribute`，带 `[AttributeUsage(Method|Property|Field|Class)]` | 设计约束 / §8.3 | **真**：两属性可经 `[EffectOverride(...)]`/`[AcceptDeviation(...)]` 标注方法/属性/字段/类 | OK |
| 7 注释/出处 | 类级 L10-17、L73-83 均带 §8.3.1/§8.3.2；各属性 L33-58 带 § 出处 + 不变式 | §8.3 | 完整 | OK |

## 额外核对（非 open，确认无假绿）
- **无越界逃逸**：`EffectOverrideAttribute` 无 `void SuppressDo9()`/`ExemptDimension()` 之类方法；`AcceptDeviationAttribute` 无 `bool ExemptCompileDo` 字段。类型层确实无法静默豁免 DO，与注释一致（非仅注释吹）。
- **reason 空路径真触发**：`IsNullOrWhiteSpace(reason)` 在 reason="" / " " / null 时均抛，非空 reason 正常构造 ⇒ 强制有效，无魔法数。
- **epsilon 边界包含端点**：`epsilon==0.0`/`epsilon==0.5` 不抛（PDR `[0.0,0.5]` 闭区间），`epsilon==0.5001`/`epsilon==-0.0001` 抛 ⇒ 与 PDR 严格一致。
- **OverrideMode 不含 Unknown（设计收窄）**：L42 注释「Unknown 由默认规则处理，不得经 override 指定」——属对 PDR §8.3.1「可覆盖 mode」的安全收窄，与 §8.1 Unknown 回落默认规则一致，非矛盾；属类型/注释双约束的合理边界，不计入 open。
- **OverrideSize 负值残留（类型不可表达，注释承载）**：`double?` 无法在类型层表达 ℕ*（§3.1.5a），注释 L53 明言「负值非法，调用方须保证 ≥1，本层不重复校验（L1 仅承载属性数据）」——属用户铁律「约束不了的写注释」之典型，已诚实声明，非 open。
- **epsilon<0.2 编译警告（跨层延迟）**：L82-84 注释明言「编译警告由 L2/L3 触发，见 §8.3.2」——L1 仅属性数据，无法发编译警告，注释诚实延迟到 L2/L3，非 open。

## open 项清单
（无）

## 结论
§8.3 两个属性**真落实**用户铁律「类型约束数学边界，约束不了的写注释」：
- reason 非空、epsilon 上界 ∈[0,0.5] → **构造子 `throw` 强制**（非注释吹）；
- kind 不可覆盖 → **类型层无 `OverrideKind` 属性**（根本无法在类型上设 kind）；
- 可覆盖域仅 mode/size/scope → **仅三属性暴露**，DO-9/DO-7 无豁免开关；
- L1 零 Godot 依赖、两属性均 `: Attribute` 可标注；
- 注释完整带 §8.3.1/§8.3.2 出处 + 不变式，且诚实声明类型不可表达的残留（size≥1 调用方责任、epsilon<0.2 警告延迟 L2/L3）。
无任何 open 项，可终止。
