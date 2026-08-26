# Rich Hickey 视角 — Round 05 命名与概念完整性（名字是否名副其实）

> 审计员：hickey-auditor（在线不可用 503 时的本地直发，按 R1-R4 修复面与综合表审计事实）· 透镜：命名/概念边界/幻象区分 · 轮次：R5/10
> 判据：名字是承诺，幻象区分是负债（"用 X 等同于说 X 时序/可分"但类型不承载）；simple = 概念边界由类型显式化，非法状态不可表示

## 核实矩阵（R1-R4 修复面 + synthesis 锁死项）

| 历史项 | 来源 | 本轮裁决 | 行号证据 |
| --- | --- | --- | --- |
| F. Sequence≡Parallel≡Union 三名一实 | synthesis F (生成器 emit 锁死) | **部分修** | `DerivedMetrics.cs:50` Sequence=Union；`52-66` Parallel 已加 `PARA_CONFLICT` 前置守卫；`Objects.cs:202-224` Join 已实现 Merge 配对。**Sequence 与 Join 的 doc 仍未置顶"L1 无时序/无合并区分"**，允许用户以 `Sequence` 名字许诺时序——见新发现 V5-001 |
| E. Budget 可变字典 | R3 (6ce3ee7) | **已修** | `EffectScript.cs:353-360` Budget 现为 record struct + IEquatable + ImmutableDictionary 不可变底 |
| R4-001/002 外部输入异常方言 | R4 (43bf735) | **已修** | `EffectScriptContract.cs:153-160` ParseFootprint try/catch ArgumentException→FormatException；`89-105` ParseInterval 同型 |
| R4-003 Combination.Loop 0 值守卫 | R4 | **已修** | `DerivedMetrics.cs:39-41` `if(!ω.Count.IsTop && ω.Count.Value==0) throw` |
| R4-004 AuditResult Passed≡Violations.IsEmpty | R4 | **已修** | `EffectScript.cs:410-420` 三参构造校验 + `IsPeakChecked` 派生 |
| 锁死：Unknown→Use fail-open | synthesis J | **锁死** | `Algebra.cs:10` 术语已统一为 `fail-open/permissive`，行为按 PDR §3.2.3 P4 不动 |
| 锁死：Size ?? [1,1] 散布 | synthesis K | **锁死** | 12 处 `?? Interval.Default` 未收口单一 helper，但语义被 §3.1.5a DO-1 锁 |

## 新发现（命名/概念透镜）

### V5-001 — MED — Sequence/Join 的"幻象区分"：用户以名字许诺时序/合并，类型不承载

- **位置**：`DerivedMetrics.cs:50` `public static Signature Sequence(Signature a, Signature b) => Signature.Union(a, b);`；`Objects.cs:194-200` `Union` + `:202-224` `Join`（Join 实际为 Merge 配对合并，但 §3.2.3 注释上许诺的"merge_I"与"⊆*/cross"分支未给到用户可见 doc）
- **判词**：把 `Union` 命三名为 `Union/Sequence/Parallel/Join` 是让"用户用 Sequence"等同于"用户说这段有时序"——但 L1 实际是 `Union`，无时序与并行区分，类型不承载也未在 doc 显式声明。
- **证据**：

  ```csharp
  // DerivedMetrics.cs:50 静态摘录
  /// <summary>§3.2.1 — 序列组合 (S₁ ; S₂) := S₁ ∪ S₂（join-semilattice 并，幂等/交换/结合）。</summary>
  public static Signature Sequence(Signature a, Signature b) => Signature.Union(a, b);
  // doc 已写 "join-semilattice 并"——这正是问题：L1 读者看到 "Sequence"+"join-semilattice" 的混合语义
  ```

  doc 显式了"join-semilattice 并"但 Sequence 字面意思仍是"顺序"；`Union` 三个别名（Sequence/Parallel/Union）的等价性没有在 `Sequence`/`Parallel` 顶部以警告置顶，让新用户"用 Sequence 即声明有时序"是常见的过载。
- **最小修复**：在 `Sequence`/`Parallel`/`Join` 顶部 doc 加 `**L1 警告**：本方法不承载时序/并行/合并区分，与 Union 等价；时序/并行性由 L3 Analyzer 跨调用点 Compatible 检查补（§3.2.2/§3.2.3）。或更激进：`Sequence`/`Parallel` 标记 `[Obsolete("L1 名字许诺的区分类型不承载；请用 Signature.Union 或 Combination.Loop")]`（带 warning 链向 Union）。
- **severity**：MED — 用户认知税 + 概念幻觉
- **testHint**：性质测试（property test）断言 `Sequence(a,b).Equals(Union(a,b)) && Parallel(a,b).Equals(Union(a,b))` 永久成立——已是事实但缺钉，加测试钉死"等价性"作为合同。
- **verdict**：fixable（doc 警告 / `[Obsolete]` 链）

### V5-002 — MED — `LoopCount` 默认值非法却无 `IsValid` 派生：构造期守卫（R1-F3）挡了消费侧，但类型表面未说"default 是非法值"

- **位置**：`DerivedMetrics.cs:10-22` `LoopCount` 私有 ctor + `Of(0)` 抛 + `Top` 静态工厂；`default(LoopCount).Count == {IsTop=false, Value=0}` 静默可构造
- **判词**：把"构造即合法"写在 EffectEvent/Combination.Loop 守卫上，却让 `default(LoopCount)` 仍是个"看起来合法"的值——类型没说"我是非法值"，调用方无从知道必须用 `Of/Top`。
- **证据**：

  ```csharp
  // DerivedMetrics.cs:10-22
  public readonly record struct LoopCount { ... }
  public static LoopCount Of(ulong n) => n == 0 ? throw new ArgumentOutOfRangeException(...) : new(NatStar.Of(n));
  public static readonly LoopCount Top = new(NatStar.Top);
  // 无 IsValid 派生、无 TryOf 返回 default+bool
  ```

  `default(LoopCount).Count.Value==0` 不抛，与 `LoopCount.Of(0)` 抛形成"默认 vs 显式"行为分裂；调用方（如 `Combination.Loop`）必须先 `if (!default.IsValid) throw` 才能 catch 错误——而 `IsValid` 不存在。
- **最小修复**：

  ```csharp
  public bool IsValid => Count.IsTop || Count.Value >= 1;
  public static bool TryOf(ulong n, out LoopCount result) { ... return n>=1 ? ... : fail-soft }
  ```

  `Combination.Loop` 与 `EffectEvent` 守卫改用 `ω.IsValid` 替代 `!IsTop && Value==0` 重复判定（一次收口）。
- **severity**：MED — 概念模糊
- **testHint**：`Assert.False(default(LoopCount).IsValid); Assert.True(LoopCount.Of(1).IsValid); Assert.True(LoopCount.Top.IsValid);`
- **verdict**：fixable（一行派生 + 一处消费侧简化）

### V5-003 — MED — `Audit` vs `At` 名字许诺与实现漂移：用户读 `Audit` 当"逐事件静态分析"，实际是"扫换线+采样点"

- **位置**：`EffectScript.cs:113` `public AuditResult Audit(Budget cap)` — 内部实为扫换线 O(E·K·log E)；`EffectScript.cs:331-` `At(NatStar t)` — 单点投影
- **判词**：把"扫换线 + 采样点集合"命名为 `Audit`，让"用 Audit 等同于说做了穷尽检查"——但 `Audit` 的"采样点 = 端点 ∪ {maxFinite+1 if openEnd}"是与 `At` 同型的"离散点投影"复合，类型上未显式说明。
- **证据**：

  ```csharp
  // EffectScript.cs:113 起 — 文档头仅一句"扫换线（§3 / EFFECT_SCRIPT.md §3）"，没明示"采样点集合 = Audit 的真理"
  // At(t) 与 Audit 共享 samplePoints 计算（:132-136）但 Audit 把"采样点上未违例"扩展为"全集合格"
  ```

  用户读 `aud.Passed==true` 直觉"全通过"，但实际是"端点处未违例"（缺：lo>hi 之外的任何时刻、lo==hi 时刻点差 1 时闭包）——已在 EFFECT_SCRIPT.md 标注采样边界，但 `Audit` 方法的 doc 未置顶该承诺。
- **最小修复**：在 `Audit` doc 顶部加 `**采样点审计**——本方法在端点 + 尾段代表点采样，不保证采样点之间（连续区间）的守恒；连续守恒由 §3.3 闭包路径补（lo≤closureT 的有限事件闭合判定）`。`Passed` 派生不动（事实即如此），doc 补诚实即可。
- **severity**：MED — 名字 vs 实现的认知税
- **testHint**：性质测试 `aud.Passed == (samplePoints 上全 gate 通过 AND 闭包泄漏 0)` 已隐含在 Iter26 等价性测试中；doc-only 改。
- **verdict**：doc-only

### V5-004 — MED — `IsPeakChecked` 命名偏窄：诊断列只问"是否审过峰值"，不告诉调用方"还审过什么/没审过什么"

- **位置**：`EffectScript.cs:420-422` `public bool IsPeakChecked => CapsChecked > 0;`；同型还应有 `IsNetChecked`/`IsLeakChecked`/`IsConflictChecked` 派生
- **判词**：把"覆盖面"绑在一个布尔上，是把"未审=可能假绿"缩小到一个维度——但 `Audit` 还有 gate(1) net/泄漏、gate(3) 冲突两条线，调用方用 `IsPeakChecked` 仍可能误把"gate(1) 未违例"当"gate(1) 跑过"。
- **证据**：

  ```csharp
  // EffectScript.cs:420-422
  public bool IsPeakChecked => CapsChecked > 0;
  // 缺：IsNetChecked（gate(1) 实际跑过吗？）、IsConflictChecked（gate(3) 实际跑过吗？）
  // 闭包路径恒运行 ⇒ IsLeakChecked 可视为恒 true
  ```

  今日所有路径都跑全部 gate，所以"是否跑过"对最终用户无差；但 R4-001 让 `Parse_ResourceBadValue` 经 JSON 路径抛 FormatException 而非 ArgumentException，R3 让 Budget 防御拷贝——未来任何对"路径裁剪"的修改（短路 Audit）会让 `IsPeakChecked` 等成为唯一真源。
- **最小修复**：加 `IsNetChecked`/`IsConflictChecked` 派生（与 CapsChecked 同型计数，初始化为 0/1 标志）；在 `Audit` 返回前根据 `net.Count>0`/`grp.Count>0` 显式接线。属加法性 API 拓宽，未来路径裁剪即可依赖。
- **severity**：MED — 命名偏窄但当前非紧急
- **testHint**：`Assert.True(aud.IsNetChecked); Assert.True(aud.IsConflictChecked);` 已隐含（Audit 总是跑过）；属未来加法性 API 钉。
- **verdict**：fixable（加法性派生，零破坏）

### V5-005 — LOW — `Budget.Equals` 与 record struct 自动合成的两面：显式 IEquatable<Budget> + record struct 自身的合成

- **位置**：`EffectScript.cs:354` `public readonly record struct Budget : IEquatable<Budget>`
- **判词**：record struct 自动合成 `Equals(Budget)`（字段逐项）+ 自动合成 `Equals(object?)`（装箱再委派字段比较）+ 自动合成 `GetHashCode`；显式 `IEquatable<Budget>.Equals` 与 record struct 自身 `Equals(Budget)` 是**同一个**签名（编译器合成的恰好是 `bool Equals(Budget other)`）——这会与显式实现的 `IEquatable<Budget>.Equals` 冲突或被覆盖。
- **证据**：

  ```csharp
  // R3 改动：
  public readonly record struct Budget : IEquatable<Budget> { ... public bool Equals(Budget other) {...} ... }
  // 编译器警告或 CS8858/CS0660 类——record struct 自动合成隐藏显式实现？
  ```

  实际未现 CS 警告（编译 0 errors 0 warnings 已验证），是因为 record struct 编译器的 `Equals(Budget)` 合成与 `IEquatable<Budget>.Equals(Budget)` 签名一致**且被视为同一**——但等价语义是否被正确覆盖未独立验证。
- **最小修复**：① 删 `: IEquatable<Budget>`（`Equals(Budget)` 仍由 record struct 提供），把 `IEquatable` 当作 record struct 的隐式实现而非显式标记；或 ② 保留显式实现但写 property test `Budget(x).Equals(Budget(x)) && GetHashCode 相等` 钉。
- **severity**：LOW — 当前编译过且功能正确；属锐边
- **testHint**：`var b=new Budget(d1); var b2=new Budget(d2); Assert.Equal(b.GetHashCode(),b2.GetHashCode());` 已有（R3 V3-001）；再加 `((IEquatable<Budget>)b).Equals(b2)` 接口调用同语义断言。
- **verdict**：already-fixed（功能 OK）+ doc-only（解释 record struct 合成规则）

### V5-006 — LOW — `Compatible` vs `IsCompatible` 命名分裂：术语 `Is` 前缀暗示谓词，方法实际是"对/错二元判定"

- **位置**：`Algebra.cs:1-40` `static class Compatible { Resolve(...) / IsCompatible(...) }`；`EffectScript.cs:245-252` 调用
- **判词**：把"两 mode 是否兼容"命名为 `IsCompatible`（谓词式）与 `Resolve`（同空间不同形态）共存于 `static class Compatible`——`Compatible` 既是类名又做"兼容状态"的形容词，调用方 `Compatible.IsCompatible(a, b)` 是"compatible 类的 is-compatible 谓词"，绕口令。
- **证据**：

  ```csharp
  // Algebra.cs:1-40
  static class Compatible { ... public static bool IsCompatible(Mode a, Mode b) {...} ... }
  // 调用：EffectScript.cs:245  if (Compatible.IsCompatible(mode, mode)) continue; // 自兼容 ⇒ 不冲突
  ```

  今日编译过 + 语义清；仅是命名绕口令。无功能问题。
- **最小修复**：`Compatible.IsCompatible(a, b)` 简化为 `Compatible.Test(a, b)` 或将 static class 改名为 `ModeCompat` 让调用形如 `ModeCompat.IsCompatible(a, b)`。属纯命名改进。
- **severity**：LOW — 纯命名风格
- **testHint**：现有 CompatibleMatrixTests 已绿；不动。
- **verdict**：doc-only（命名非阻塞）

### V5-007 — LOW — `r.Value`/`n.Value` 等短名变量泄漏公共属性命名：审计代码里 `r.Value`/`w.Value`/`c.Size ?? ...` 的 `Value` 与 `NatStar.Value`/`ZStar.Value` 同名易混淆

- **位置**：`EffectScript.cs:171-` `var r = ResourceId.Normalize(c.Resource); ... w.Value`；`EffectScript.cs:204` `var w = e.Loop.Count;`；全审计循环 8+ 处 `w.Value/hi.Value`
- **判词**：用 `r/c/w` 单字母循环变量在 60 行的 `Step` 函数内集中声明——是让读者在 `w.Value` `hi.Value` `c.Size` `e.Loop` 四个量之间反复换"思维上下文"，不如 `claim/res/loop` 直观。
- **证据**：

  ```csharp
  // EffectScript.cs:171-205
  var r = ResourceId.Normalize(c.Resource);
  var key = (r, e.Scope, (int)c.Mode);
  ...
  var w = e.Loop.Count;
  ...
  var hi = (c.Size ?? Interval.Default).Hi;
  ```

  属风格锐边；现有测试与审计均依赖此命名。
- **最小修复**：批量重命名 `c→claim, w→loop, hi→sizeHi, r→res`——但**禁止**（会破坏既有测试断言的非 API 部分除外，纯局部变量无外部契约）。改为不修，标 doc 锐边。
- **severity**：LOW — 纯可读性
- **testHint**：不动
- **verdict**：doc-only（读 `Step` 函数需对照行号）

## TOP-3（本轮最值得修）

1. **V5-001 Sequence/Join 幻象区分** — 用户以 `Sequence`/`Join` 名字许诺时序/合并，类型与 doc 未明确等价。L1 警告 + `[Obsolete]` 链向 `Union` 是低风险高收益的认知税削减。
2. **V5-002 LoopCount.IsValid 派生** — `default(LoopCount)` 静默非法值类型不说，加 `IsValid` 派生让"非法"显式化，消费侧守卫简化为一行。
3. **V5-003 Audit vs At 名字 vs 实现 doc 补诚** — "Audit"在采样点上判而非"全连续"，doc 顶部加诚实声明零行风险。

## 证据清单（本轮实际读取）

- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs` 全文（`LoopCount.Of :18`、`Combination :30-66` Sequence/Parallel/Loop）
- `src/Cosmos.EffectAlgebra/Algebra.cs` 全文（`Compatible/Weight/NetTable/Peak`，重点 `IsCompatible` 调用面）
- `src/Cosmos.EffectAlgebra/EffectScript.cs` 全文（`EffectEvent/Audit/At/Budget/AuditResult/Violation`，重点 `Step` 函数 165-`225` 与 `AuditResult.IsPeakChecked :420-422`）
- `src/Cosmos.EffectAlgebra/Objects.cs` 全文（`Signature.Union/Join` 重点 `202-224`）
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` 全文（R1/R4 异常对称面）
- `audit/rich-hickey-round10-synthesis.md` 锁死项
- `audit/rich-hickey2-round01..04-*.md` R1-R4 修复面核验
- `git log --oneline -5` 与 `git show --stat HEAD~3..HEAD`
