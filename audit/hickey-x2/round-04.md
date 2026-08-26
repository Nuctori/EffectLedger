# Cosmos.EffectAlgebra 对抗性审计 · 第 4 轮 · Specs, Guards, and Correctness（Rich Hickey 视角）

> 纪律：本轮未读取 audit/ 下任何历史报告（含 hickey-x2/），核实矩阵以 PDR v3-FINAL 声称的数学性质为基准逐条裁决。所有「对抗性实证」均在仓库外临时工程中针对当前源码实际运行得出（net10.0，ProjectReference 直指源码），非纸面推演。

## 核实矩阵（PDR 声称 vs 代码实际）

| # | 声称（出处） | 裁决 | 证据 |
|---|---|---|---|
| V1 | §3.1.5a：溢出 ⇒ 保守 ⊤，「永不崩溃、永不 NaN」（Numeric.cs:8） | **部分修** | 仅 ℕ* 半边成立：NatStar.Add/Mul 有环绕检测（Numeric.cs:31-40，ScaleGuardTests.cs:134-135 已测）；**ℤ* 半边完全裸奔**——ZStar 加减零检查（SignedNet.cs:30-33），且 ToSigned/Negate 用 `(long)` 强转 ulong（Algebra.cs:74-75,82-83）。见 F1/F3 实证 |
| V2 | §3.2.4：⊔ 按同 Claim 配对取 merge_I，输出单条 `[min(lo),max(hi)]` | **误报为已实现（实为口号）** | `Signature.Join(a,b) => Union(a,b)`（Objects.cs:193）是纯别名；merge_I 配对逻辑不存在于任何 src 文件；全测试套件 0 处调用 Join。见 F2 实证 |
| V3 | §3.2.2：并行组合前置 `∀ 同资源对 Compatible` 检查 | **仍在（缺口）** | `Parallel(a,b) => Union(a,b)`（DerivedMetrics.cs:53），注释推给「L3 Analyzer 补」，但 Analyzer 只审用户 C# 方法体，从不审 Combination.Parallel 的直接调用者。见 F4 实证 |
| V4 | §3.2.3 P4 + README.md:56：Unknown 按 Use 处理 | **已修但规格语言自相矛盾** | 行为三处一致（Unknown→Use→恒兼容）；README 诚实称之为 **fail-open**，而 Algebra.cs:10 与 PDR §3.2.3 P4 称之为「fail-closed 最弱兼容」。同一行为两个相反的名字 = 没有规格。见 F8 |
| V5 | §5/DO-9：net 闭合闸门 fail-closed，装载前生效 | **部分修** | VerifyNetClosure 存在且被 PluginRuntime.cs:83 接线；但组合入口 ValidateForLoad（LoadValidation.cs:86-91）漏掉它，类文档（LoadValidation.cs:14）却声称含 §5。见 F5 |
| V6 | §3.1.4a：Claim 构造即合法，「不靠运行时 if 漏判」 | **部分修** | 构造子路径成立；但 Claim 是 struct，`default(Claim)` 携 null Resource 绕过一切校验流入 Signature.Of 不报错（实证 ADV-C）。见 F7 |
| V7 | §9.1：Deviation 分母 ε=1、⊤ 整体跳过 | **已修（数值边界除外）** | Deviation.cs:47-49 ε 下界正确；TryMid 判 ⊤ 正确；但有限端点的 long 中点溢出未建模（SignedNet.cs:87）。见 F3 |
| V8 | MA-002 收口：⊤ 在所有载体上封闭 | **部分修** | NatStar/DeviationVal 封闭；ZStar 算术可产生非法负值/环绕值而非 ⊤，破坏「上界标记不发散」承诺 |

## 新发现

### F1 · HIGH · ZStar 无溢出守卫 + `(long)` 重解释 ⇒ 泄漏闸门假阴性（静默放行真泄漏）
- **位置**：src/Cosmos.EffectAlgebra/SignedNet.cs:30-33（unchecked `a.Value + b.Value`）；src/Cosmos.EffectAlgebra/Algebra.cs:74-75, 80-83（Negate/ToSigned 的 `(long)s.X.Value` 强转）
- **判词**：NatStar 层把溢出做成保守 ⊤ 是好品味；到了有符号层同一份纪律消失了——ulong size ≥ 2⁶³ 经 `(long)` 静默变负，create 变 release，这不是边界处理，这是符号占卜。
- **对抗性实证**（均满足类型系统，无任何异常/报警路径）：
  - `create(2^64-1) + create(1)`，零次释放：`(long)(2^64-1) = -1`，net = `[-1,-1]+[1,1] = [0,0]` ⇒ **IsConserved = True**。两次巨型分配、一次不还，DO-9 闸门绿灯放行。
  - 单个 `release(2^63+1)`：Negate 得 `[+MaxLong,+MaxLong]`——释放被记成正贡献，净表符号整体翻转。
- **最小修复**：ToSigned/Negate 对 `s.X.Value > long.MaxValue` 返回 ZStar.Top（与 NatStar 溢出策略对齐）；ZStar +/- 用 `checked` 或环绕检测回退 Top。

### F2 · HIGH · Signature.Join 未实现 PDR §3.2.4 的 merge_I —— 文档代数与运行代数是两套
- **位置**：src/Cosmos.EffectAlgebra/Objects.cs:193 `public static Signature Join(Signature a, Signature b) => Union(a, b);`
- **判词**：PDR 花了一整节定义 ⊔ 的 join-semilattice 性质，代码用一行别名交卷。声称的结合律从未被实现，也就从未被证伪——这比没有定律更糟，它让读者以为自己在用被证明过的东西。
- **对抗性实证**：分支 A `create Mem [10,10]`，分支 B 同资源 `create [50,50]`。§3.2.4 要求输出单条 `[10,50]`；实测 Join 输出**两条并存 claim**，Peak = 60（求和）而非规格的 50，net = `[60,60]` 而非 `[10,50]`。条件分支的规模语义系统性失真。
- **最小修复**：Join 按 Normalize 后 Claim 键配对，size 取 `Interval.Merge`；补幂等/交换/结合性质测试（现有 PropertyTests 只测了 Interval.Merge 载体，没测 Signature 层）。

### F3 · MED · TryMid 中点计算 long 溢出 ⇒ Deviation 产出无意义数值且不报错
- **位置**：src/Cosmos.EffectAlgebra/SignedNet.cs:87 `mid = (Lo.Value + Hi.Value) / 2.0`
- **判词**：「先转 double 再除」写成了「先在 long 里加爆再转 double」。ε=1 防的是分母除零，却没人防分子先死。
- **实证**：`SignedInterval[MaxLong, MaxLong].TryMid` ⇒ mid = **-1**（真值 ≈ 9.22e18）。expected=MaxLong / actual=0 的 Deviation 报 100%，一个看似合理实则纯属巧合的数字。
- **最小修复**：`mid = Lo.Value/2.0 + Hi.Value/2.0`（或先转 double 再加）。

### F4 · MED · Combination.Parallel 无 Compatible 前置守卫，且集合去重抹掉冲突证据
- **位置**：src/Cosmos.EffectAlgebra/DerivedMetrics.cs:53；对照 PDR §3.2.2 与 CONFLICT 集（Algebra.cs:12）
- **判词**：前置条件写在注释里就是许愿。(create, create) 是 CONFLICT 集正式成员，Parallel 却把它们 Union 成一条 claim——冲突不仅没报错，连案发现场都被 ImmutableHashSet 清扫干净。
- **实证**：两分支各含 `create Memory(5)`，`Combination.Parallel` 正常返回，claims=1，无任何诊断。
- **最小修复**：Parallel 内对同 Normalize 资源跨桶断言 `Compatible.IsCompatible`，违约抛 `InvalidOperationException("PARA_CONFLICT …")`——让非法状态不可表示，而不是留给一个永远看不到这次调用的分析器。

### F5 · MED · ValidateForLoad 组合入口漏接 §5 net 闭合闸门
- **位置**：src/Cosmos.EffectAlgebra.Runtime/LoadValidation.cs:86-91（只调三项）；类文档 LoadValidation.cs:14 声称含「§5 per-Fiber net 收益闭合」
- **判词**：文档说这道门有四道锁，装了三道。PluginRuntime.cs:82-83 靠调用方记得再单独调一次 VerifyNetClosure 才补齐——正确的做法是把第四道锁装进门里，而不是要求每个路过的人背熟装配图。
- **最小修复**：ValidateForLoad 末尾追加 VerifyNetClosure（或重载接受集合版本），PluginRuntime 改调单一入口。

### F6 · MED · ScopeId ⊆* 退化为「相等 ∨ Global」，嵌套作用域不可表达；传递性测试空转
- **位置**：src/Cosmos.EffectAlgebra/Objects.cs:96-110（IncludedIn 仅 Equals 或 Global 两分支）；tests/Cosmos.EffectAlgebra.Tests/AlgebraLawsTests.cs:116-146
- **判词**：给一个只有单位元和最大元的序起名叫「偏序」，再把传递性测试写成四条自反链自证——测试在给注释作伪证。后果落在 NetTable.Compute（Algebra.cs:64）：`net(S, Scene("s"))` 对 Method-scoped claims 全部漏计，泄漏欠近似且无提示。
- **最小修复**：要么在 ScopeId 里显式建模包含链（如 Scene ⊒ Type ⊒ Method 的命名约定查询），要么在文档里承认 ⊆* 只有 {相等, ≤Global} 两级并删掉「传递性」措辞与空转测试。

### F7 · LOW · `default(Claim)` 携 null Resource 绕过「构造即合法」承诺
- **位置**：src/Cosmos.EffectAlgebra/Objects.cs:88（Normalize 的 `_ => r` 对 null 原样放行）、Objects.cs:121（承诺原文）；Claim 为 struct 无法阻止 default 构造
- **实证**：`Signature.Of(default(Claim))` 成功返回含 1 条 null-resource claim 的签名，错误延迟到后续哈希/分组才可能引爆。
- **最小修复**：Claim.Normalize 首行 `Resource is null ⇒ throw ArgumentException`，或 Signature.Add 处拒收。

### F8 · LOW · fail-open/fail-closed 术语精神分裂
- **位置**：README.md:56（「fail-open：未知资源冲突被静默放行」）vs Algebra.cs:10（「fail-closed 最弱兼容」）vs PDR §3.2.3 P4
- **判词**：行为只有一个（Unknown→Use→恒放行，即 open），名字有两个（open 和 closed）。当同一个词描述相反事实，两个文档都失去了规格资格。
- **最小修复**：统一为「permissive（放行）」，并在 Algebra.cs 注释删掉 fail-closed 错标。

### F9 · LOW · DeviationVal.Of 接受 NaN/±∞，NaN 永不报警
- **位置**：src/Cosmos.EffectAlgebra/Numeric.cs:125（无守卫）；Numeric.cs:131 ExceedsThreshold 对 NaN 恒 false
- **判词**：「double ∪ {⊤}」这个类型声明把 IEEE 的毒值也一并收编了。当前 Calculate 造不出 NaN，但公共构造面留着一根永远通着电的管子。
- **最小修复**：Of 内 `double.IsFinite(v) ?? throw`。

### F10 · LOW · InverseReplay 裸 catch 吞掉一切异常，Pending 无法区分「未尝试/半释放」
- **位置**：src/Cosmos.EffectAlgebra.Runtime/InverseReplay.cs:35-41
- **判词**：「尽量继续剩余逆」（R4-6）是正当策略，但 catch-all 把 OOM 也当成普通失败咽下去，且半释放的资源与根本没碰的资源在诊断里长一个样——上游按拓扑序二次释放时拿到的信息不足以自保。
- **最小修复**：诊断记录异常类型与阶段标记（attempted/partial），至少区分 `OperationCanceledException` 类致命信号。

### F11 · LOW · Peak 按 PDR §3.3.2 公式跨 kind 求和，与 DO-7/KIND_MIX 自相矛盾
- **位置**：src/Cosmos.EffectAlgebra/Algebra.cs:114-127（对全部非 release claim 的 hi 求和，read/write 缺省 [1,1] 也计入）；PDR §3.3.2 聚合公式 vs weight 定义「跨 kind ⇒ ⊥ 禁止混算」
- **判词**：代码忠实实现了规格，可惜规格自己打自己：一边宣布量纲隔离是铁律（DO-7），一边在 Peak 公式里把 read 次数和 memory MB 相加。忠实执行的错误仍然是错误。
- **最小修复**：Peak 过滤 `c.Kind == Kind.Occupy`（与 net 同口径），或按 kind 分桶返回多峰值。

## TOP-3

1. **F1** — 有符号层无溢出守卫：类型合法输入使 DO-9 泄漏闸门对真实泄漏报「守恒」（实证 IsConserved=True，零释放）。正确性叙事的核心承诺被一行 `(long)` 强转击穿。
2. **F2** — ⊔ 的 merge_I 从未实现：PDR 的条件分支代数是文学创作，实际 Peak/net 系统性偏离规格且零测试覆盖。
3. **F4** — Parallel 前置条件靠注释执行：CONFLICT 冲突被静默吞并并抹除证据，违反「非法状态不可表示」。

## 可用性裁决

载体层（NatStar/Interval/Merge/Compatible）是被规格化、被测试、值得信任的；但有符号聚合层（ZStar/ToSigned/Negate/TryMid）与组合层（Join/Parallel）的「代数律」停留在 markdown 里——本轮 11 条 finding 中 6 条属于「PDR 声称 X、代码做 Y」，其中两条 HIGH 都能被完全合法的类型输入触发且全程零报错。**结论：在 F1/F2 修复并有证伪性测试落位之前，本库不得对外宣称 DO-9 fail-closed 或「数学性质已被验证」。**

## 证据：读取过的文件

- README.md；EFFECT_SCRIPT.md（结构核对）；PDR_Effect_Cost_Algebra_v3_FINAL.md（§3.1.4-3.3.2、§9.1、修订记录）
- src/Cosmos.EffectAlgebra/：Algebra.cs、SignedNet.cs、Numeric.cs、Deviation.cs、DerivedMetrics.cs、Objects.cs、Cosmos.EffectAlgebra.csproj
- src/Cosmos.EffectAlgebra.Runtime/：LoadValidation.cs、NetBenefitClosure.cs、InverseReplay.cs、Fiber.cs
- tests/Cosmos.EffectAlgebra.Tests/：PropertyTests.cs、AlgebraLawsTests.cs、IntervalArithmeticTests.cs、ScaleGuardTests.cs、StabilityAuditTests.cs（节选）
- 对抗性验证脚本：仓库外临时工程（net10.0 ProjectReference），输出已在上文各实证条目引用
