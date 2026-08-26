# Round05 — Maybe Not 透镜审计（Rich Hickey 视角：错误/缺席值处理）

- 审计员：ox-alpha（对抗性审计子代理，只读源码，未读 audit/ 目录）
- 日期：2026-02（round05）
- 范围：`src/Cosmos.EffectAlgebra/` 下 7 个核心源文件
  `Numeric.cs`、`Objects.cs`、`SignedNet.cs`、`Algebra.cs`、`Deviation.cs`、`EffectScript.cs`、`EffectScriptContract.cs`
  （另核对了 `ApiMapping.cs` 中 Memory 哨兵的定义处）
- 视角：《Maybe Not》核心论点——把"缺席"伪装成"某个普通值"（nil-punning / sentinel / NaN-as-value）是语言级谎言；
  缺席应当是显式类型（Maybe/⊤），且**宁可报错也不静默错**。本审计逐符号检查该原则的遵守与违背。

---

## 一、逐符号审计表

严重度定义：
- **Blocker** — 静默数据损坏 / 审计结果错误，必须修。
- **High** — fail-open 吞掉本应报错的情形，或哨兵值污染语义；应尽快修。
- **Medium** — 有文档的启发式缺省，但仍是"猜测代替缺席"，存在误报/漏报面。
- **Low** — 边角不一致、注释契约无强制，风险可控但值得记录。

| # | 符号 | 位置 | 判定 | 严重度 |
|---|------|------|------|--------|
| 1 | `NatStar.Top` | Numeric.cs:19 | **显式类型 ✅（有裂缝）** | Low |
| 2 | `Interval` 构造校验 | Numeric.cs:83–88 | fail-fast ✅ | — |
| 3 | `Interval.Default = [1,1]` | Numeric.cs:94 | 静默吸收（启发式缺省） | Medium |
| 4 | `ZStar.Top` | SignedNet.cs:21 | 显式类型 ⚠️ 运算不一致 | Medium |
| 5 | `SignedInterval.ContainsZero` / `IsConserved` | SignedNet.cs:72; Algebra.cs:96–102 | fail-closed ✅ | — |
| 6 | `DeviationVal.Top` + `ExceedsThreshold` | Numeric.cs:122,128 | **fail-open（报警被吞）** | High |
| 7 | `Weight.Of ⇒ double.NaN` | Algebra.cs:38 | **NaN 当 ⊥ 哨兵** | High |
| 8 | `Claim.Size : Interval?`（null Size） | Objects.cs:126,131–134 | 静默吸收（null→[1,1]）+ 全库重复 `??` | Medium |
| 9 | `Mode.Unknown` → 按 Use 处理 | Objects.cs:116; Algebra.cs:15,20–28 | **fail-open（Unknown-Use）** | High |
| 10 | `SerializeResource`/`ResourceKey` 默认臂 `_ => memory:0` | EffectScriptContract.cs:218,236 | **静默身份改写** | Blocker |
| 11 | `ParseScope` 缺 type 兜底 `Scene(name)` | EffectScriptContract.cs:92–95 | 静默吸收（兜底猜类型） | Medium |
| 12 | Memory 哨兵 `Mem() => Memory(0)` 等 | ApiMapping.cs:39–52 | 哨兵值合并不同资源 | Medium |
| 13 | `NetTable.Get` 缺省 ⇒ `SignedInterval.Zero` | Algebra.cs:91 | nil-punning（缺席=零区间） | Low |
| 14 | `NatStar` 溢出 ⇒ ⊤（Numeric.cs:26–46）vs `ZStar +/-` 无溢出检测（SignedNet.cs:30–33） | 两文件 | 不一致：ℤ* 静默回绕 | Medium |
| 15 | `(long)` 无 checked 下转：ToZ/Negate/ToSigned/Negate(EffectScript) | EffectScript.cs:305–306; Algebra.cs:74–75,82–83 | ulong>long.MaxValue 静默变负 | Medium |
| 16 | peakSum exit 路径下限钳 0 | EffectScript.cs:211 | 静默吸收账目漂移 | Low |
| 17 | 开放尾采样点 `maxFinite + 1` | EffectScript.cs:123 | ulong.MaxValue 时回绕为 0 | Low |
| 18 | `ParseTop`/`ParseLoop`/`ParseBudget` 用 `GetUInt64()` | EffectScriptContract.cs:81,109,167 | 异常类型不符契约（非 FormatException） | Low |
| 19 | 闭包块重复 `if (e.Lifetime.Lo.IsTop) continue;` | EffectScript.cs:272,274 | 死代码（无害） | Low |

---

## 二、逐符号详述

### 1. `NatStar.Top`（Numeric.cs:19）— 显式 ✅，Low

⊤ 是显式的 `IsTop` 标志位而非哨兵数值——这正是 Hickey 要求的形态。加法/乘法内嵌 ⊤ 律，
**ulong 回绕被检测并保守提升为 ⊤**（Numeric.cs:29–31、40–43），不静默低估。

**裂缝（Low）**：`Value` 字段恒可读，`IsTop==true` 时读 `.Value` 得到 0，编译器不拦——纯注释契约。
record struct 无法像 Hickey 的 Maybe 那样在类型层使"无效态不可表示"。同类问题存在于 `DeviationVal.Value`、`ZStar.Value`。
建议：提供 `TryGet(out ulong)` 并将 `Value` 私有化/改名 `UnsafeValue`，或至少在 Analyzer 层禁止直接读。

### 2. `Interval` 构造校验（Numeric.cs:83–88）— fail-fast ✅

`[⊤, x]`（x 有限）抛异常；`lo > hi` 抛异常。"构造即合法"，非法区间不可表示。这是全库最好的设计之一。
`SignedInterval` 构造同理（SignedNet.cs:60–64）。无需改动。

### 3. `Interval.Default = [1,1]`（Numeric.cs:93–94）— 静默吸收，Medium

PDR §3.1.5(a) 代数缺省。问题不在 `[1,1]` 本身，而在它被用来**顶替缺席信息**（见 #8）：
一个没写 size 的 occupy claim 被当成"恰好占用 1 个单位"参与 Peak/net 计算——峰值被静默低估。
若缺省语义真是"未知"，正确载体是 `[1,⊤]`（Dynamic）；若真是"1"，应由调用方显式写 `Exact(1)`。
当前选择是启发式（AUDIT002/003 有文档），但它是典型的"用编造的值掩盖不知道"。

### 4. `ZStar.Top`（SignedNet.cs:21）+ ℤ* 运算不一致 — Medium

⊤ 表示法本身显式 ✅。但对比：

- `NatStar operator+`：显式检测 ulong 回绕 ⇒ ⊤（Numeric.cs:29–31）。
- `ZStar operator+/−`：`(a.IsTop||b.IsTop) ? Top : Of(a.Value + b.Value)` —— **unchecked long 加减，回绕静默发生**（SignedNet.cs:30–33）。

同一个"未知上界"概念，ℕ* 保守升 ⊤、ℤ* 静默环绕出错的数。net 求和路径大量走 ZStar.Add
（EffectScript.cs:159→278、Algebra.cs:63），极端 size 可产生错误的"守恒"结论——这恰是审计器最不能犯的错。
修复：仿照 NatStar 做 `checked` + 回绕⇒`ZStar.Top`（未知即诚实）。

### 5. `ContainsZero` / `IsConserved`（SignedNet.cs:72；Algebra.cs:96–102）— fail-closed ✅

任一端 ⊤ ⇒ 不宣称守恒；资源不在 net 表 ⇒ `IsConserved` 返回 false（报警）而非默认闭合。
这是"宁可报错也不静默漏报"的正确实现，与 #13 的 `Get` 形成鲜明对照。✅

### 6. `DeviationVal.Top` + `ExceedsThreshold`（Numeric.cs:122,128）— **fail-open，High**

```csharp
public bool ExceedsThreshold(double threshold) => !IsTop && Value > threshold;
```

⊤（"偏差不可校准、需人工界定"）经由此 API 变成返回值 `false` —— 与"未超标"**不可区分**。
调用方 `SignatureDeviation.ExceedsThreshold`（Deviation.cs:52）原样转发。注释说"调用方视为需人工界定，
不触发普通数值报警（避免掩盖）"，但实际上没有任何机制强迫人工界定：布尔返回值把
「未知」punning 成了「通过」。Hickey 原话：把 absence 塞进 value channel，调用者必然忘记检查。
这正是 §8.3.2 想避免的"掩盖"，却被 API 形状亲手造成。

**修复方向**：返回三态 `enum ThresholdResult { Pass, Exceed, Unverifiable }`，或在 `AuditResult`/
诊断输出中把 ⊤ Deviation 单列为一条 Violation-kind（如 `ManualReviewRequired`），让"未知"可见而不是消失。

### 7. `Weight.Of ⇒ double.NaN`（Algebra.cs:33–38）— **NaN 当 ⊥ 哨兵，High**

```csharp
public static double Of(Kind a, Kind b) => a == b ? 1.0 : double.NaN;
```

⊥（跨 kind 无定义）编码为 NaN——Hickey 点名的经典反模式：NaN 是 `double` 的合法成员，
会沿算术传播、所有比较返回 false、`double.IsNaN` 忘记调就静默出错。与本库自己的
`DeviationVal`（bool IsTop + double）相比是明显倒退：同一份代码里已经有正确的 Maybe 形态却不复用。
更糟的是 KIND_MIX 报警完全依赖"L3 Analyzer"（注释契约），运行时零强制——
`Weight.Of(Kind.Read, Kind.Write) * anything` 今天就能静默返回 NaN 并流进任何下游 double。

**修复方向**：改为 `Weight?`（null=⊥）或专用 `readonly record struct WeightVal { bool IsBottom; double V; }`；
至少在聚合入口对 NaN fail-fast 抛 `InvalidOperationException("KIND_MIX")`，别等 Analyzer。

### 8. `Claim.Size : Interval?`（Objects.cs:126,133）— null Size 静默吸收，Medium

`Normalize()` 把 `Size == null` 膨胀为 `Interval.Default [1,1]`。可空类型区分了 null 与 `Exact(0)`
（修复过零 size 误报，好），但 null 的语义归宿是**编造的 [1,1]**（见 #3）。
且该 `?? Interval.Default` 兜底在 EffectScript.cs:159,179,184,198,204,278、Algebra.cs:63,121,122、
DerivedMetrics.cs:41–43、EffectScriptContract.cs:208 反复出现 ≥11 处——不变量没有收敛到单点，
任何新增消费点都可能忘记 `??` 而拿到 null 或各自兜底。Hickey："如果不变量重要，就把它放进构造函数。"
**修复方向**：`Claim` 构造时归一（或提供 `Claim.Create` 工厂保证 Size 非 null），消灭散布的 `??`。

### 9. `Mode.Unknown → Use`（Objects.cs:116；Algebra.cs:15,20–28）— **fail-open，High**

```csharp
private static Mode Resolve(Mode m) => m == Mode.Unknown ? Mode.Use : m;
// ... if (aa == Mode.Use || bb == Mode.Use) return true;
```

Unknown mode 被 punning 成最弱权限 Use，而 Use 与一切兼容 ⇒ **两个 Unknown-mode claim 永远判兼容，
冲突检测对它们整体失明**。注释自称 "fail-closed 最弱兼容"——命名反了：对"是否冲突"这个问题，
它是 fail-open（不确定 ⇒ 放行）。PDR §3.2.3 P4 确实如此规定，且 Parse 层接受字面 `"unknown"`
（EffectScriptContract.cs:142）意味着 AI 产出的 JSON 可以合法地把无法判断的模式标成 unknown 从而绕开 CONFLICT gate。
这与"宁可报错也不静默错"直接冲突：至少应在 AuditResult 里产出一条 `UnknownMode` 弱违例（需人工确认），
而不是让 Unknown 在 Compatible 全函数里无声蒸发。

### 10. `SerializeResource` / `ResourceKey` 默认臂（EffectScriptContract.cs:214–218, 230–236）— **Blocker**

```csharp
_ => new Dictionary<string, object?> { ["memory"] = 0 }   // SerializeResource
_ => "memory:0"                                            // ResourceKey
```

任何非 Gpu/CommandBuffer/Memory/Occupancy/SignalBus 的 ResourceId（Tree、Self、Physics、Disk、Signal、
AudioMixer、Callback、Network、Input、Custom、SignalBus 已覆盖……Tree/Self/Physics/Disk/Network/Input 均**未覆盖**）
序列化时被静默改写成 `Memory(0)`。后果链：

1. `ToJson(Parse(json))` round-trip 后 Tree("node.id") 变成 Memory(0) —— **身份被改写，无任何报错**；
2. Budget 序列化同理：`Tree(...)` cap 键变成 `"memory:0"`，与真 Memory(0) cap 合并/串键；
3. 直接违背本文件头部的自我声明（EffectScriptContract.cs:4 "非法形状…抛 FormatException（fail-fast，非静默漏报）"）。

这是全库最严重的"静默错"：不是拒绝服务，而是把错的数据递给下一环节。
ParseResource（155 行）同文件已经示范了正确做法（fail-fast throw）。
**修复**：两个 switch 的 `_` 臂改为 `throw new FormatException($"不可序列化的 resource: {r}")`，
并补齐 Tree/Self/Physics/Disk/Network/Input 等构造子的序列化形状。

### 11. `ParseScope` 缺 type 兜底（EffectScriptContract.cs:89–95）— Medium

缺 `type` 字段 ⇒ 默认 `Scene(name)`；连 `scene` 都没有 ⇒ `name=""` ⇒ **静默得到 `ScopeId.Scene("")`**。
已修过 "仅未知 type 才抛"，但"缺 type 即 Scene"仍是猜测式兜底：AI 少打一个字段，作用域语义就被改写，
且 `IncludedIn` 偏序下 Scene("") 与真实 Scene 的比较结果随之失真。
对照同文件 `Require`（259 行起）对其他字段的 fail-fast，此处标准不一致。
**修复方向**：缺 type 且缺 scene ⇒ throw；缺 type 但有 scene 至少保留现状并在文档标注（向后兼容）。

### 12. Memory 哨兵 `Memory(0)` 等（ApiMapping.cs:39–52）— Medium

`Mem()=>Memory(0)`、`CmdBuf()=>CommandBuffer("gpu")`、`Cb()=>Callback("cb")`、`AudioMx()=>AudioMixer(0)`：
多个不相干的运行时实体共享一个哨兵键。注释声明"真实 UID 由映射层运行时填入"——但目前 L1 白名单
产出的 Claim 全部带哨兵值，net/Peak 按 ResourceId 分组时会把**所有 Load/Preload/Instantiate 的内存占用
合并到同一键**（还都是 Global/Shell scope），峰值既可能虚高（不同资源的账算一起）也可能互相抵消
（create/release 配错对）。哨兵值 punning 是 Hickey 明令禁止的形态；正确做法是让 uid 成为必填参数
（类型强制），映射层拿不到 uid 时显式给 `[1,⊤]`/Dynamic 区间或 Unknown 资源，而不是伪造 id=0。

### 13. `NetTable.Get` 缺省 ⇒ `SignedInterval.Zero`（Algebra.cs:91）— Low

缺席资源 punning 成 `[0,0]`（天然含 0 = "守恒"）。当前唯一调用方 Deviation.Calculate（Deviation.cs:44）
用它做资源对齐是合理的（缺席侧 mid=0）。但这是一个公共 API：下一个调用方很可能误用它做守恒判定，
从而绕过 `IsConserved` 精心实现的 fail-closed（#5）。**建议**：改名为 `GetOrDefault` 或返回
`SignedInterval?`，把"缺席"还给调用方决定。

### 14–15. ℕ*/ℤ* 与 (long) 下转的静默回绕族 — Medium（合并 #14/#15）

- `ZStar +/-` unchecked 回绕（见 #4，SignedNet.cs:30–33）。
- `ToZ`/`Negate`（EffectScript.cs:305–306）与 `Negate`/`ToSigned`（Algebra.cs:74–75,82–83）：`(long)n.Value`
  在 `n.Value > long.MaxValue` 时静默变负。NatStar 的合法域是整个 ulong（加法还会产生接近 ulong.MaxValue 的中间值），
  一旦进入 net 路径，一个巨大的 size 会变成负贡献——可能凭空制造 NegativeDip 或掩盖 Leak。
  SignedInterval 构造器的 lo≤hi 校验只能碰巧拦截部分案例。
- **修复方向**：统一策略——凡 ulong→long 下转，`> long.MaxValue` ⇒ 对应端取 `ZStar.Top`（保守未知），一处 helper 收口。

### 16. peakSum exit 钳 0（EffectScript.cs:211）— Low

`sub <= curSum ? curSum - sub : 0`：退出扣减大于入场累加时静默钳 0。这只在 enter/exit 记账失衡（bug）时发生，
正确反应是暴露失衡而不是吸收。扫换线自平衡性目前靠事件成对入队保证，属防御深度不足，记录备查。

### 17. `maxFinite + 1` 回绕（EffectScript.cs:123）— Low

全部有限端点均为 ulong.MaxValue 时采样点回绕为 t=0。数学边角，实际剧本几乎不可能触达；记录即可。

### 18. `GetUInt64()` 异常类型不符（EffectScriptContract.cs:81,109,167）— Low

JSON 数值为浮点/负数时 `JsonElement.GetUInt64()` 抛 `FormatException`（.NET 此处恰好也是 FormatException，
但 InvalidOperationException/OverflowEventArgs 场景随 ValueKind 而异）且消息不含字段上下文。
与文件声明的"非法形状 ⇒ FormatException（fail-fast）"契约基本吻合但错误信息劣化，影响 AI 回修 JSON 的效率。低危。

### 19. 闭包块死代码（EffectScript.cs:272,274）— Low

`if (e.Lifetime.Lo.IsTop) continue;` 连续出现两次（272 与 274），无害但是合并补丁残留，顺手清理。

---

## 三、"宁可报错也不静默错"总评

**遵守得好的（fail-fast / fail-closed 正面清单）**：
- `Interval` / `SignedInterval` 构造器不变量校验（Numeric.cs:83–88；SignedNet.cs:60–64）
- JSON 契约解析的形状校验：未知 kind/mode/scope.type/budget 键、resource 值空串 ⇒ FormatException
  （EffectScriptContract.cs:102,135,142,151–157,179,262–264）
- `IsConserved`：缺席资源与 ⊤ 端点均判不守恒（Algebra.cs:96–102）
- `ContainsZero`：⊤ ⇒ false（SignedNet.cs:72）
- `NatStar` 加/乘回绕 ⇒ 保守 ⊤（Numeric.cs:29–31,40–43）
- Deviation 任一端 ⊤ ⇒ 整体 ⊤，不出 NaN/∞（Deviation.cs:40–47）
- `Budget.None` 显式表达"无上限"，与"未设 cap"区分清楚（EffectScript.cs:330–337）

**违背原则的（按危害排序）**：
1. **Blocker** — SerializeResource/ResourceKey 默认臂把任意资源静默改写为 `memory:0`（#10）。
2. **High** — `ExceedsThreshold` 把 ⊤（不可判定）折叠为 false=通过（#6）。
3. **High** — Unknown mode→Use 使冲突检测对该类 claim 失明（#9）。
4. **High** — Weight 以 NaN 编码 ⊥，KIND_MIX 无运行时强制（#7）。
5. **Medium** — null Size→[1,1]、ParseScope 缺 type→Scene("")、Memory(0) 哨兵、ZStar/(long) 静默回绕（#3,#8,#11,#12,#14–15）。

共性根因只有两条：(a) **布尔/双精度返回通道装不下三态**（通过/违反/不可判定），导致第三态被折叠；
(b) **switch 默认臂选择了"编造值"而非 throw**。两处的修法都是机械的：三态枚举 + 默认臂 fail-fast。
库内已有正确范本（DeviationVal 的 IsTop、ParseResource 的 throw、IsConserved 的 fail-closed），
本次整改不需要引入新抽象，只需要把既有范式推广到遗漏点。

## 四、残余风险

- `NatStar/ZStar/DeviationVal` 的 `.Value` 在无效态可读（编译器不可拦），依赖注释契约与代码评审；Analyzer 层若无检查则长期存在误用窗口。
- Mode.Unknown→Use 为 PDR P4 明文规定，改动属规范变更，需要上游（PDR §3.2.3）同步修订而非单方面改码。
- DerivedMetrics.cs / EffectAttributes.cs 未在本轮 7 文件范围内深审（仅 grep 到 `?? Interval.Default` 三处，已并入 #8 统计）。
