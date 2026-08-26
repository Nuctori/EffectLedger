# 迭代22 审计（属性边界锁）

## 摘要
- 审计对象（仅测试代码 + 被测实现，未读 `audit/` 任何文件）：
  - `tests/Cosmos.EffectAlgebra.Tests/AttributeBoundaryTests.cs`
  - `src/Cosmos.EffectAlgebra/EffectAttributes.cs`
- 测试运行：独立 `dotnet test --filter "FullyQualifiedName~AttributeBoundaryTests"` → **11 通过 0 失败**；
  父级全量报告 157 通过 0 失败（11 个属本文件，一致）。
- open 项总数：**0**（可闭 0 / 设计 out-of-scope 0）
- 终止判定：**可终止**

## 逐条核对（回指行号 + 被测 + PDR § + 真锁? + 假绿? + 结论）

| 项 | 行号 | 被测 | PDR § | 真锁? | 假绿? | 结论 |
|---|---|---|---|---|---|---|
| 1a reason 空串 | L20-23 `EffectOverride_EmptyReason_Throws` | impl L55-56 `if (string.IsNullOrWhiteSpace(reason)) throw ArgumentException` | §8.3.1 | 真：`""` 必抛，断言 `Throws<ArgumentException>` 捕获 | 否 | OK |
| 1b reason null | L26-29 `EffectOverride_NullReason_Throws` | 同上（IsNullOrWhiteSpace 含 null） | §8.3.1 | 真：`null!` 必抛 | 否 | OK |
| 1c reason 空白 | L32-35 `EffectOverride_WhitespaceReason_Throws` | 同上（"   " 触发） | §8.3.1 | 真：`"   "` 必抛 | 否 | OK |
| 1d reason 合法 | L38-41 `EffectOverride_ValidReason_DoesNotThrow` | impl L57 `Reason = reason` | §8.3.1 | 真：不抛且 `Assert.Equal("真理由", attr.Reason)` | 否 | OK |
| 2a epsilon 超上界 | L44-47 `AcceptDeviation_AboveUpperBound_Throws` | impl L88-89 `epsilon > 0.5` 抛 `ArgumentOutOfRangeException` | §8.3.2 | 真：`0.6` 必抛 | 否 | OK |
| 2b epsilon 超下界 | L50-53 `AcceptDeviation_BelowLowerBound_Throws` | 同上 `epsilon < 0.0` | §8.3.2 | 真：`-0.1` 必抛 | 否 | OK |
| 2c epsilon=1.0 | L56-59 `AcceptDeviation_EqualsOne_Throws` | 同上 `> 0.5` 覆盖 1.0 | §8.3.2 | 真：`1.0` 必抛 | 否 | OK |
| 2d epsilon 合法 | L62-68 `AcceptDeviation_WithinBounds_DoesNotThrow` | impl L90 `Epsilon = epsilon` | §8.3.2 | 真：`0.0/0.5/0.2` 不抛且 `Assert.Equal(epsilon, attr.Epsilon)` | 否 | OK |
| 3 kind 不可覆盖 | L72-79 `EffectOverride_HasNoOverrideKindMember` | impl 无 `OverrideKind` 属性（§8.3.1 类型层禁止） | §8.3.1 | 真：反射 `GetProperty("OverrideKind", Public|Instance)` → `Assert.Null` | 否 | OK |
| 4 可证伪性 | 全局 | — | — | 真：去 `EffectOverrideAttribute` 抛异常→1a/1b/1c 红；去 `AcceptDeviationAttribute` 抛异常→2a/2b/2c 红；加 `OverrideKind` 属性→3 红 | 否 | OK（见下） |
| 5 假绿扫描 | 全局 | — | — | 真：无 `Assert.True(true)`；无 `[Fact]` 无断言；合法用例亦带 `Assert.Equal` 值校验，非仅构造 | — | OK |
| 6 出处注释 | 类 L9-12 + 各方法 | — | §8.3.1/§8.3.2 | 真：类级 summary + 每方法注释均带 § 号 | — | OK |

## open 项清单
无。

## 可证伪性说明（第4项展开）
- 若实现删除 `EffectOverrideAttribute` 构造子内 `IsNullOrWhiteSpace` 抛异常分支 ⇒ `EffectOverride_EmptyReason_/NullReason_/WhitespaceReason_Throws` 三条 `Assert.Throws` 全部失败 ⇒ 必红。
- 若实现删除 `AcceptDeviationAttribute` 构造子内 `epsilon<0.0 || epsilon>0.5` 抛异常分支 ⇒ `AboveUpperBound_/BelowLowerBound_/EqualsOne_Throws` 三条必红。
- 若在 `EffectOverrideAttribute` 上新增 `public Mode? OverrideKind { get; set; }`（即破坏 §8.3.1「kind 不可覆盖」）⇒ `EffectOverride_HasNoOverrideKindMember` 反射断言 `Assert.Null(prop)` 失败 ⇒ 必红。
- 三条边界均由 L1 构造子 fail-fast 强制，测试非恒真、可证伪，无假绿。

## 非 open 备注（不阻塞终止）
- 被测实现 `Mode? OverrideMode`（EffectAttributes.cs L43）注释称「不含 Unknown」，但类型为 `Mode?` 含 `Unknown` 枚举值，类型层未禁止 `OverrideMode = Mode.Unknown`；该约束属「类型系统约束不了的写注释」范畴（符合 LANDING_PLAN §2 约定），且不在本次审计的三类边界（reason/epsilon/kind）范围内 ⇒ 不记为 open，仅记录。
- `Reason`/`Epsilon` 均为 `get`-only 只读属性，构造子外不可篡改 ⇒ 边界在类型构造即锁定，与测试断言一致。

## 结论
三类约束（§8.3.1 reason 非空、§8.3.2 epsilon∈[0,0.5]、§8.3.1 kind 不可覆盖）**真由 L1 构造子强制**，对应测试全部可证伪、无恒真断言、无假绿、出处注释齐备。迭代22 审计：**可终止**。
