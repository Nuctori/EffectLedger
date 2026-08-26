# Rich Hickey 视角 — Round 02 数值边界（除零/回绕/溢出/⊤ 传播）

> 审计员：hickey-auditor · 透镜：数值边界全链路 · 轮次：R2/10
> 判据：错误的数值是谎言的精确形状——宁可 ⊤ 不可错值；simple = 边界以类型显式化，非法状态不可表示

## 核实矩阵（历史结论 R1 + synthesis 锁死清单逐条裁决）

| 历史项 | 结论 | 本轮裁决 | 行号证据 |
|---|---|---|---|
| synthesis L：ℕ*→ℤ* 三份拷贝 + ZStar/(long) 回绕 | R1 部分已修（ToZ/Negate 带 Top 守卫） | **部分修** | `Algebra.cs:72-83` 现为 `s.Hi.Value > long.MaxValue ? ZStar.Top : ZStar.Of(unchecked((long)...))` 已保守；但三份 `ScaleSize/Scale` 仍分裂：`DerivedMetrics.cs:68-72` vs `EffectScript.cs:341-345` vs `EffectScript.cs:175/302` 内联 `ScaleSize` + `ToZ/Negate`，未收敛至单一 helper |
| synthesis B：loop=0 除零 / ScaleSize [0,0] 误报 | R1 已修 | **已修** | `DerivedMetrics.cs:18` `LoopCount.Of` 拒绝 0；`EffectScriptContract.cs:144-146` ParseLoop 拒绝 0；`EffectScript.cs:45-47` 构造期封堵 `loop.Count.Value==0` |
| synthesis H：Peak.Compute 跨桶聚合（量纲隔离击穿） | 仍在 | **仍在** | `Algebra.cs:116-126` `Peak.Compute` 遍历 `AllClaims()` 仅滤 `Release`，`Kind`（Read/Write/Occupy）不区分，直接 `sum + Hi`，与 `NetTable.Compute` 的 `Kind==Occupy` 隔离矛盾；`EffectScript.cs:192-210` 峰值路径按 `OccupyClaims` 隔离，二者语义分裂 |
| synthesis G：Signature.GetHashCode 顺序敏感 | synthesis 称仍在，api-audit 待核 | **已修** | `Objects.cs:236-252` 已改为 `XOR` 折叠，顺序无关 |
| synthesis E：Budget 可变字典泄漏 | 仍在 | **仍在**（非本轮透镜，记录） | `EffectScript.cs:355-363` `record struct Budget` 包 `IReadOnlyDictionary` 但 `None` 仍为 `new Dictionary<>` 可变底，`Equals` 仍为引用语义 |
| R1：default(LoopCount) 封堵 | 已修 | **已修** | `EffectScript.cs:45-47` 构造期拒绝；`DerivedMetrics.cs:18` 同理 |
| R1：序列化 round-trip 保真 / 未知键白名单 | 已修 | **已修**（非本轮透镜） | `EffectScriptContract.cs:32-33,82,163-165` 白名单校验 |
| R1：空 scope 拒绝 | 已修 | **已修** | `EffectScriptContract.cs:119-120` 拒绝零字段 scope |

## 新发现（数值边界专项）

> 动探针：`C:/temp/cosmos_probe/cosmos_probe.csproj`（引用 `src/Cosmos.EffectAlgebra`），.NET 10.0 实测；静态推断与实测对照标注

### R2-001 — HIGH — 峰值哨兵碰撞：合法有限峰值 `ulong.MaxValue` 被误判为 ⊤

- **位置**：`EffectScript.cs:205-207`（enter 路径）与 `EffectScript.cs:224-228`（exit 路径）
- **证据摘录**：
  ```csharp
  // :205
  var mul = (!hi.IsTop && !w.IsTop && hi.Value <= ulong.MaxValue / w.Value) ? hi.Value * w.Value : ulong.MaxValue;
  // :206
  peakSum[r] = (curSum.IsTop || mul == ulong.MaxValue || curSum.Value > ulong.MaxValue - mul) ? NatStar.Top : ...
  ```
  `ulong.MaxValue` 同时承载“溢出哨兵”和“合法乘积 1×Max”。`hi=1, w=Max` 时 `hi.Value <= Max/w` 为真，`mul = 1*Max = Max`，随后 `mul==Max` 恒真 ⇒ 误标 `Top`。
- **判词**：用值域内的合法值当哨兵，是把边界藏进值的 Hickey 反例——simple 要求哨兵在类型外，不在值内。
- **实测**：`hi=Exact(1), w=LoopCount.Of(Max), cap=Max` 预期 `Peak=Max ≤ cap` 无违例，实测 `PeakExceeded 峰值 ⊤ > 预算 18446744073709551615`（`C:/temp/cosmos_probe` ProbePeakSentinel）——假阳性，保守但错；`cap=Top` 时本应 `Top≤Top` 全绿却报峰值超限，属于“该 ⊤ 但给 ⊤”的误伤但暴露类型不干净。
- **最小修复**：弃哨兵，复用 `NatStar` 类型边界：`var scaled = hi * w; if (scaled.IsTop) => Top else if (curSum.IsTop || curSum.Value > Max - scaled.Value) => Top else Of(cur+scaled)`；exit 路径同理用 `NatStar` 运算替代 `mul==Max` 分支。零新增 API。
- **testHint**：`Budget cap=Max, single occupy Exact(1) with LoopCount.Of(Max) => Audit.Passed==true && no PeakExceeded`；反例 `cap=Max-1 => PeakExceeded`；`cap=Top vs Peak Top => no PeakExceeded`。

### R2-002 — HIGH — `ZStar.Min` 将 `Min(x,⊤)` 误为 ⊤，与 `NatStar.Min` 及 §3.1.5a 律矛盾

- **位置**：`SignedNet.cs:59`
- **证据摘录**：
  ```csharp
  // :59
  public ZStar Min(ZStar o) => (IsTop || o.IsTop) ? Top : Of(Math.Min(Value, o.Value));
  // 对比 :42-48 NatStar.Min 正确
  // Numeric.cs:47 return IsTop ? o : this; // min(x,⊤)=x
  // :56 ZStar.Max 正确：(IsTop||o.IsTop)?Top
  ```
  `NatStar.Min` 实现 `min(x,⊤)=x`（保守下界），`ZStar.Min` 却实现 `min(x,⊤)=⊤`（上界律误用到下界）。
- **判词**：Max/Min 对 ⊤ 的律是对偶的——把 `Max` 的“任一 ⊤ ⇒ ⊤”抄给 `Min`，是 complect 的精确形状。
- **实测**：`ZStar.Of(5).Min(ZStar.Top)` 实测 `⊤`，预期 `5`；`SignedInterval [5,10].Merge([⊤,⊤])` 实测 `[⊤,⊤]`，预期 `[5,⊤]`（`C:/temp/cosmos_probe` ProbeZStarMinConsequence）。`ContainsZero` 在两种结果下同为 `false` 掩盖了错误，但 `Merge` 的语义已坍缩，未来任何依赖 `Lo` 下界的守恒/范围判定都会静默取 ⊤。
- **最小修复**：`Min` 改为 ` (IsTop && o.IsTop) ? Top : IsTop ? o : o.IsTop ? this : Of(Min(...))`，与 `NatStar.Min` 对偶；`Max` 保持 `(IsTop||o.IsTop)?Top`。
- **testHint**：`Assert.Equal(5, ZStar.Of(5).Min(ZStar.Top).Value)`；`Assert.Equal(ZStar.Of(5), new SignedInterval(ZStar.Of(5),ZStar.Of(10)).Merge(new SignedInterval(ZStar.Top,ZStar.Top)).Lo)`。

### R2-003 — MED — `maxFinite + 1` 在 `ulong.MaxValue` 处回绕，未守卫

- **位置**：`EffectScript.cs:134`
- **证据摘录**：
  ```csharp
  // :134
  if (anyOpenEnd) samplePoints.Add(NatStar.Of(maxFinite + 1));
  // maxFinite: ulong，默认unchecked，Max+1 => 0
  ```
- **判词**：用算术给“尾段代表点”命名，却让算术在边界回卷——是让值去承载“无穷后一点”的 easy。
- **实测 vs 静态**：构造 `e1=[Max-1,Max], e2=[0,⊤]` 时 `maxFinite=Max`，`Max+1` 回绕为 0，`samplePoints` 额外加入 0（已存在），未抛但尾段代表点丢失；当前扫换线在 `Max` 已覆盖开区间（因 `Hi=Top => ulong.MaxValue`），故未衍生额外违例，属静默冗余而非当下误判，标记为锐边而非立崩。静态推断 + 动探（`ProbeMaxFinitePlusOne`）确认无异常但多一次 `t=0` 采样。
- **最小修复**：`if (anyOpenEnd) { if (maxFinite != ulong.MaxValue) samplePoints.Add(NatStar.Of(maxFinite+1)); /* else 尾段已由 Max 点覆盖 */ }`；或将 `maxFinite` 提升为 `NatStar` 并以 `checked` 守卫回绕时直接不增点。
- **testHint**：`Events: [Max,Max] + [0,Top] => Audit 不抛 OverflowException 且 Violations 与 Max!=Tail 时一致`；边界 `Events: [Max,Top] alone => Atomic` 断言 `samplePoints` 含 `Max` 且不含 0 重复（需反射或通过违例 `AtT` 间接观测）。

### R2-004 — MED — 三份 `Scale`/`ToZ` 拷贝未收敛，回绕守卫分散，漂移面

- **位置**：`DerivedMetrics.cs:68-72` `Scale`、`EffectScript.cs:341-349` `ScaleSize`+`ToZ`/`Negate`、`Algebra.cs:72-83` `Negate`/`ToSigned`
- **证据摘录**：
  ```csharp
  // DerivedMetrics.cs:70-71
  if (w.IsTop) return new Interval(s.Lo, NatStar.Top);
  return new Interval(s.Lo * w, s.Hi * w);
  // EffectScript.cs:343-344 同形重复
  // Algebra.cs:74 ZStar.Of(-unchecked((long)s.Hi.Value)) 依赖外层 >long.MaxValue 守卫
  ```
- **判词**：同一算术在三处各写一次 `>long.MaxValue ? Top : Of(unchecked((long)…))`——不是复用，是重复；重复是漂移的温床。
- **影响**：今日三处守卫一致（R1 后已修复 `>long.MaxValue` 分支），但 `DerivedMetrics.Scale` 溢出靠 `NatStar.*` 的 `Top`，`EffectScript.ScaleSize` 同理，而 `Algebra.ToSigned` 另起炉灶做 `(long)` 强转守卫；未来一处改阈值（例如切 `checked`）必有一处漏。数值边界应由单一 `NatStarToZStar` helper 承载。
- **最小修复**：抽 `static ZStar ToZSat(NatStar n)` 与 `static Interval ScaleSat(Interval s, NatStar w)` 至 `Numeric.cs` 或 `Algebra.cs` 单一处，三调用点收敛；保留注释 `// R4-F1 超域 => Top`。
- **testHint**：`Interval [Max,Max] * LoopCount.Of(2) => [⊤,⊤] == EffectScript.ScaleSize == DerivedMetrics.Scale` 的等价 property；`NatStar.Of(ulong.MaxValue).ToZ() == Top` 且 `NatStar.Of((ulong)long.MaxValue).ToZ() == Of(long.Max)`。

### R2-005 — MED — `Peak.Compute` 跨桶求和仍击穿量纲隔离（`Read`/`Write` 计入峰值）

- **位置**：`Algebra.cs:116-126` `Peak.Compute`
- **证据摘录**：
  ```csharp
  // :118-122
  foreach (var c in sig.AllClaims()) {
    if (c.Mode == Mode.Release) continue;
    if ((c.Size ?? Interval.Default).Hi.IsTop) return NatStar.Top;
    sum = sum + (c.Size ?? Interval.Default).Hi;
  }
  ```
  未按 `Kind` 过滤；`ReadClaims`/`WriteClaims` 同进求和。
- **判词**：`NetTable.Compute` 以 `Kind==Occupy` 守量纲，`Peak.Compute` 却以 `Mode` 守——同一资源两套“属于峰值”的定义，是让“量纲”在注释里而非在类型里。
- **影响**：`L1` 旧 `cardinality→size` 切换已废弃 `cardinality`，但 `Peak` 仍把 `read/write` 的 `size` 当并发占用峰值；`EffectScript` 侧 `gate(2)` 仅遍历 `OccupyClaims` 已隔离，二者语义分裂。对直接调 `Derived.Peak`/`Peak.Compute` 的调用方（`C:/temp` 未发现外部调用，但属公共 API）会高估峰值 ⇒ 假阳性 `PeakExceeded`。
- **最小修复**：与 `NetTable.Compute` 对齐，首行加 `if (c.Kind != Kind.Occupy) continue;` 或文档显式声明 `Peak` 仅含 `Occupy` 并补测试锁；若有意含 `Read`/`Write` 则需在 `Peak` 与 `EffectScript` 间抽单一真源。
- **testHint**：`Signature { Read(m=Create,size=10), Occupy(m=Create,size=5) } => Peak.Compute == 5`（隔离） vs 当前 `15`（击穿）；`EffectScript` 同签名 `Audit` 峰值应与 `Peak.Compute` 一致的等价断言。

### R2-006 — LOW — `ParseTop`/`ParseBudget` 的 `GetUInt64` 在负数/小数输入时漏出 BCL 异常而非 `FormatException`

- **位置**：`EffectScriptContract.cs:110` `el.GetUInt64()`、`220` `pv.GetUInt64()`、`197` `mem.GetUInt64()`（已包 `FormatException` 仅对非 Number）
- **证据摘录**：
  ```csharp
  // :144
  var v = el.GetUInt64(); // loop
  // :110 return NatStar.Of(el.GetUInt64()); // lifetime
  ```
  当 JSON 值是 `-1` 或 `1.5`（`ValueKind==Number` 但非 UInt64），`GetUInt64()` 抛 `InvalidOperationException`/`FormatException` 非本契约约定的 `FormatException("...须为数字或 ⊤...")`，违背 `EffectScriptContract.Parse` “非法形状 ⇒ FormatException fail-fast” 的 API 面积承诺。
- **判词**：把 JSON 数值形态的校验外包给 `JsonElement`，是让 BCL 的异常形状成为你的 API 形状。
- **最小修复**：`TryGetUInt64` 分支，失败则 `throw new FormatException($"{layer}: 须为非负整数或 \"⊤\"")`；对 `memory`/`budget` 同理。
- **testHint**：`Parse("{\"events\":[{\"lifetime\":[-1,5],...}]}") => throws FormatException` 而非 `InvalidOperationException`；`Parse("{\"budget\":{\"gpu:x\": -1}}") => FormatException`。

## TOP-3（本轮最需修）

1. **R2-001 峰值哨兵碰撞** — 唯一在合法输入域内将有限值误判为 ⊤ 的路径，直接致 `PeakExceeded` 假阳性；修为类型内 `NatStar.IsTop` 分支，零 API 拓宽。
2. **R2-002 `ZStar.Min` 对偶律错** — `Min(x,⊤)` 本应取 `x` 却给 ⊤，使 `SignedInterval.Merge` 坍缩；修一行对偶分支，守住“最小”语义。
3. **R2-005 `Peak.Compute` 跨桶击穿** — 公共 API `Peak` 与审计 `gate(2)` 两套峰值定义，量纲隔离在 `Net` 侧已修而峰值侧仍漏；收敛到 `Kind==Occupy` 单一真源。

## 证据清单（实际读取）

- `src/Cosmos.EffectAlgebra/Numeric.cs` 全文
- `src/Cosmos.EffectAlgebra/SignedNet.cs` 全文
- `src/Cosmos.EffectAlgebra/Algebra.cs` 全文
- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs` 全文
- `src/Cosmos.EffectAlgebra/EffectScript.cs` 全文（重点 `120-150,170-230,289-349`）
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` 全文（重点 `91-115,141-146,209-221`）
- `src/Cosmos.EffectAlgebra/Objects.cs` 全文
- `src/Cosmos.EffectAlgebra/Deviation.cs` 全文
- `audit/rich-hickey-round10-synthesis.md` 锁死清单
- 动探针 `C:/temp/cosmos_probe/cosmos_probe.csproj` + `Program.cs`（`ProbePeakSentinel`/`ProbeZStarMinConsequence`/`ProbeMaxFinitePlusOne`/`ProbeIntervalScalePollution` 实测，.NET 10.0 `dotnet run`）

> 注：本轮承诺“可写临时探针到 %TEMP%”，因仓库盘 `D:` 与系统 `C:` 分离，实测工程落于 `C:/temp/cosmos_probe`（等价临时区），不进仓库；报告内已注明实测 vs 静态推断。

