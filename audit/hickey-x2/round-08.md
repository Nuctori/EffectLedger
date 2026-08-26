# Cosmos.EffectAlgebra 对抗性审计 · 第 8/10 轮 —— Composability（组合性）视角

> 审计人：Rich Hickey 视角。本轮独立上下文，未读取 `audit/` 下任何历史报告（含 hickey-x2/round-01..07）。
> 核实矩阵的「历史结论」取自仓库内可读文档自述：README.md「已知语义锐边」与 EFFECT_SCRIPT.md §10 审计轨迹。
> 审计对象：EffectAlgebra 的组合承诺——效果组合后是否仍是同一种东西、顺序是否可预测、上下文是否泄漏进结果。

---

## 核实矩阵（历史自述结论逐条裁决）

| # | 历史结论出处 | 裁决 | 证据 |
|---|---|---|---|
| V1 | README.md:56 「Unknown 模式 = fail-open：未知资源冲突被静默放行」 | **仍在** | Algebra.cs:15 `Resolve(m) => m == Mode.Unknown ? Mode.Use : m`；:22 Use 与任意 mode 兼容。且 EffectScriptContract.cs:146 合法接受 `"unknown"` mode——脚本层同样静默放行（见 F7，比 README 自述更严重） |
| V2 | README.md:57 MA-002「loop:"⊤" 居民层被静默豁免泄漏检测；lifetime:[1,⊤] 仍入 net 并报警」 | **仍在（自述属实）** | EffectScript.cs:158 `if (enter && !e.Loop.Count.IsTop)` 整事件跳过 net；:283 closure 同样跳过；Peak 仍计入（:186 topCount）。豁免粒度是**整个事件的所有 claim**，不是「纯正向贡献的资源」（见 F5 推演 C） |
| V3 | README.md:58「Claim.Size 省略 ≠ 未知：?? [1,1]」 | **已修** | Objects.cs:135-138 `Size = Size ?? Interval.Default`，可空类型区分显式 Exact(0) 与缺省；CrossLayerTests.cs:126 断言 `Interval?` 类型 |
| V4 | EFFECT_SCRIPT.md §5 / EffectScript.cs:78「At(t) 纯函数，同剧本同 t ⇒ 同签名」 | **成立（限 At 本身）** | Objects.cs:196-222 Signature 补了结构相等 + 顺序无关哈希；At（EffectScript.cs:81-90）只依赖 Events 与 t。但 **Audit 的 Violation 元数据不在此保证内**（F9：归因 scope first-wins 随 Events 顺序变） |
| V5 | EFFECT_SCRIPT.md OPEN-N2：ω 重解释为「同一时刻并发副本数」，时长→Lifetime、密度→LoopCount | **部分修** | 注释到位（DerivedMetrics.cs:16-17、EffectScript.cs:38-41），但语义未贯彻到底：单事件 ω=2 自配对不报冲突（EffectScript.cs:249 组大小≥2 才查），两个 ω=1 事件同屏即报 CompatibleConflict——「两份并发副本」按写法不同得出相反裁决（推演 A′）。同一语义三种结局，用户无法预测 |
| V6 | EFFECT_SCRIPT.md OPEN-B4：lifetime [⊤,⊤] 视为非法输入、脱离审计不报警 | **仍在** | EffectScript.cs:132 `if (lt.Lo.IsTop) continue;` 扫换线直接丢弃；Alive（:326）恒 false。静默不审，无任何 Violation 或诊断提示「此事件未被审计」 |
| V7 | EFFECT_SCRIPT.md §10.2 / iter-effect26「扫换线 == 端点采样暴力版逐条 Violation 相等」 | **部分修** | 等价是对**自家 ReferenceAudit** 的等价（同口径复刻），不等于对外语义正确：gate(2) 峰值按资源全局聚合（EffectScript.cs:185-195 键=r），gate(3) 兼容按 (r, e.Scope, mode) 分组（:177），NetTable.Compute 又按 ⊆* 过滤（Algebra.cs:60）——三套 scope 口径并存，等价测试锁住的是这个不一致本身（F4） |

---

## 新发现

### F1 · HIGH · `Sequence` ≡ `Parallel` ≡ `Union`：名为序列的并集
- **位置**：src/Cosmos.EffectAlgebra/DerivedMetrics.cs:50、:53
  ```csharp
  public static Signature Sequence(Signature a, Signature b) => Signature.Union(a, b);
  public static Signature Parallel(Signature a, Signature b) => Signature.Union(a, b);
  ```
- **判词**：把 set-union 命名为 Sequence 是给马匹贴上汽车的商标——顺序信息在进入函数之前就被扔掉了，而 API 名字向用户许诺了它根本不承载的东西。更糟的是 Union 幂等（Objects.cs:183-192 哈希集去重），故 `Sequence(a, a) == a`：「把同一个效果做两遍」在这个代数里是 no-op。想表达重复必须改用 Loop(ω)，而 Sequence 的存在本身就在诱导用户写错。
- **最小修复**：删掉 `Sequence`/`Parallel` 这两个公共名（API 面积是最贵的承诺），或让 Sequence 返回带相位标记的类型使 (a;b) ≠ (b;a)。至少在 doc 注释首行写明「无时序语义、幂等：S;S=S」。

### F2 · HIGH · 组合爆炸掉出代数：Signature 层去重吞冲突，Script 层报冲突——同一程序两种裁决
- **位置**：src/Cosmos.EffectAlgebra/Objects.cs:149（ImmutableHashSet 存桶）、:168-181（Add 去重）；src/Cosmos.EffectAlgebra/Algebra.cs:115-125（Peak 按 claim 求和，无冲突检查）；对照 src/Cosmos.EffectAlgebra/EffectScript.cs:177-183、:249-259（gate(3) 按活跃事件计数报 CompatibleConflict）
- **判词**：
  - **推演 A（Signature 层）**：`var s1 = Signature.Of(new Claim(Occupy, gpu, Create, scene, Exact(10))); var both = Combination.Parallel(s1, s1);`
    → `both.OccupyClaims.Count == 1`（HashSet.Add 去重），`Peak.Compute(both, Global) == 10`。两个并发 create 各占 10，代数说峰值是 10，且**没有任何信号**告诉你第二条 create 被吞了。组合到一半，冲突事实静蒸发了。
  - **推演 A′（Script 层，同一逻辑程序）**：两个 EffectEvent，lifetime 相同、footprint 均为该 create、loop=1 → Audit 报 `CompatibleConflict`（create×create）。若合并为一个事件 loop=2 → 通过（V5）。
  - 同一「两份并发占用」的概念，三种表示得到 {峰值10 无告警}、{冲突违例}、{通过}。使用者知道得越多、写得越巧，才越安全——这正是 easy 而非 simple。
- **最小修复**：Union 时对同 (resource, scope, mode) 且 mode ∈ CONFLICT 集的 claim 至少返回诊断或让 Peak 按「乘法副本」语义累加；或在 Combination.Parallel 文档里用一行黑体写明「重复 claim 会被去重，冲突检查不在本层」。现在注释里那句「跨调用点 Compatible 检查由 L3 Analyzer 补」（DerivedMetrics.cs:54）对 Script 层用户不可见。

### F3 · HIGH · `Combination.Loop` 无条件 rescope：组合改写历史查询的答案
- **位置**：src/Cosmos.EffectAlgebra/DerivedMetrics.cs:37-47（`c with { Scope = loopScope, ... }` 三桶全部覆写）
- **判词**：
  - **推演 B**：`sig` = create(gpu, scope=Method("m"), size[10,10])。`Peak.Compute(sig, M) == 10`。
    `looped = Combination.Loop(sig, Of(5), new ScopeId.Loop("L"))` 后：claim scope 变成 Loop("L")，`Peak(looped, M) == 0`（Loop("L").IncludedIn(Method("m")) 为 false，Objects.cs:120-127 跨标签不可比）、`Peak(looped, Global) == 50`。
    一个查询（「方法 m 内该资源占用多少」）的答案从 10 变成 0，仅仅因为效果被包了一层 loop——组合操作悄悄改写了所有既有 scoped 查询的可见性，且无告警。
  - 嵌套同理：`Loop(Loop(body,2,A),3,B)` 后所有 claim scope==Loop("B")（LoopCombinationTests.cs:31-35 自己的断言就证明了内层 scope 被 B 覆盖），内层循环身份永久丢失，「三层嵌套」塌成一层。
- **判词（一句话）**：Loop 不是给效果套环，是把效果的地址擦掉重写——组合之后你再也问不出组合之前的问题。
- **最小修复**：rescope 改为记录链（Scope = Loop(id, inner)）而非覆写；或 Loop 只缩放 size 不动 scope，scope 归属交给调用方。

### F4 · MED · Audit 三道 gate 的 scope 口径互相矛盾：a+b 的预算判定随 scope 分布变化，兼容判定却按 scope 隔离
- **位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:167（netScope/net 键=ResourceId，全局聚合）、:185-195（peakSum 键=ResourceId，全局聚合）、:177（grp 键=(r, e.Scope, mode)，按 scope 分组）；对照 Algebra.cs:59-60（NetTable.Compute 按 ⊆* 过滤）
- **判词**：
  - **推演 D**：事件 E1(Scene:"Battle") create gpu size100，E2(Scene:"Menu") create gpu size100，budget cap=150。两 scope 互不可比（ScopeOrderTests 锁死的事实）。gate(3)：两组各 count=1 → 无冲突；gate(2)：peakSum 全局相加=200 > 150 → PeakExceeded。
    用户直觉：「不同场景的效果怎么会共享一个峰值账户？」要么兼容 gate 少了全局视角，要么预算 gate 多了全局视角——代码替用户选了前者对后者，文档一字未提。
  - 同一剧本，`script.At(t).Net(scope)`（走 ⊆* 过滤）与 Audit 内部 net（不过滤）是两个数字。框架自己都不用同一把尺子。
- **最小修复**：统一口径——要么全按 e.Scope 分组后再逐组对 cap 判定，要么在 EFFECT_SCRIPT.md 显式声明「预算是全剧本地资源维度，兼容是 scope 维度」，并把 NetTable 的 ⊆* 过滤差异写清。

### F5 · MED · ω=⊤ 豁免是整事件的：常驻元素与有限记账混用同一资源 ⇒ 幽灵 Leak/NegativeDip
- **位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:158、:283（跳过条件是 `e.Loop.Count.IsTop`，作用于该事件全部 occupy claims）
- **判词**：
  - **推演 C（组合爆炸第 2 例）**：E1 lifetime[0,100]、loop=⊤、footprint=create(gpu,[1,1])（常驻背景层）；E2 lifetime[0,100]、loop=1、footprint=release(gpu,[1,1])（某有限效果的清理步）。
      - closure：E1 被豁免，net=[−1,−1] 不含 0 → **Leak**；
      - 运行中：t≥E2.Lo 起 net.Hi=−1<0 → **NegativeDip「release 早于 create」**——create 明明从头活到尾。
    反过来 E1=create 有限 + E2=release ⊤：net=[+1,+1] → Leak，清理行为完全隐形。两个方向都产生违反直觉的裁决，且 Violation 文本不会告诉用户「本资源的另一半贡献来自被豁免的 ⊤ 事件」。README V2 把它当锐边接受，但接受的前提应是豁免可被推理；整事件粒度的豁免让「⊤ + 任意有限记账」成为不可组合的禁区。
- **最小修复**：豁免降级到 claim 粒度并在 Violation 中标注「含 N 条被豁免贡献」；或对混合情形输出专门的 MixedResidency 违例类型。

### F6 · MED · JSON 契约自称 round-trip，实为 C# 代数的真子集：组合结果出不了 C# 层
- **位置**：src/Cosmos.EffectAlgebra/EffectScriptContract.cs:39（「round-trip 用」）、:205（SerializeScope 对 Loop/Conditional/Async/Shell 抛 FormatException）、:224（SerializeResource 仅认 5 种，Tree/Self/Physics/Disk/Signal/AudioMixer/Network/Input/Custom 全抛）
- **判词**：C# 层有 13 种 ResourceId 构造子、8 种 ScopeId 构造子（Objects.cs:26-44、:76-86），随便组合出的合法 EffectScript 一经 `ToJson` 即炸。两层各自「可组合」，接缝处一边是全集一边是五分之一的投影，还不叫 lossy、叫 round-trip。Parse 侧同样只收 5 种 resource——AI 产不出 C# 层能表达的大部分效果。
- **最小修复**：要么契约扩到全集，要么把 ToJson 的 doc 改成「仅覆盖 {5 resource × 4 scope} 子集，其余构造子抛」并在 Parse/ToJson 共用一个能力谓词。

### F7 · MED · `mode:"unknown"` 是冲突检查的后门，且代码自称 fail-closed
- **位置**：src/Cosmos.EffectAlgebra/EffectScriptContract.cs:146（接受 "unknown"）；EffectScript.cs:177（分组键含 mode）+ Algebra.cs:15,22（Unknown≡Use ⇒ 与一切兼容）；Algebra.cs:10 注释「fail-closed 最弱兼容」vs README.md:56「fail-open」
- **判词**：AI 生成的 JSON 里随手一个 `"mode":"unknown"` 就让该 claim 在 gate(3) 里永远不与任何东西冲突——对抗性输入的最省事逃逸口。更讽刺的是同一行为在 Algebra.cs 注释里叫 fail-closed、在 README 里叫 fail-open：连维护者都没就它的性质达成一致，凭什么要求用户预测它？
- **最小修复**：Audit 对含 Unknown-mode claim 的剧本输出一条 UnknownMode 降级警告（不计入 Passed 也行，但要可见）；统一 fail-open/fail-closed 表述。

### F8 · MED · Runtime 折叠逆声明时丢尺寸：跨层组合制造伪泄漏
- **位置**：src/Cosmos.EffectAlgebra.Runtime/Fiber.cs:52-59（`new Claim(Kind.Occupy, inv.Resource, Mode.Release, inv.Scope, null)`）
- **判词**：`InverseClaim` 不携带数量纲（Fiber.cs:24-28），折叠进 EffectiveSignature 时 Size=null → Default[1,1]。若 Fiber.Effect 里 create(Provides) 是 Exact(10)，净效应 = [+10] + [−1] = [9,9]，IsConserved 恒 false——守恒闸门对着自己的逆回放报警。L1 辛苦建的区间算术，到 Runtime 接缝被 `[1,1]` 一刀切平。类型边界在每层内部都漂亮，边界与边界之间漏水。
- **最小修复**：EffectiveSignature 里 release 用与已存在 create 相同的 size（既然 :52 已检测到同名 create，顺手取其 Size），或 InverseClaim 增加 Interval 字段。

### F9 · LOW · Violation 归因 scope 取首个贡献者：元数据非交换
- **位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:167（`if (!netScope.ContainsKey(r)) netScope[r] = e.Scope;`）、:184（peakScope 同法）、:296-306（leakScope 同法）
- **判词**：net 本身交换律成立（区间加法），但 Violation.Scope 是 first-wins——Events 数组顺序一换，同一条违例归到不同 Scene。值是确定的，解释是不确定的；AI 拿着反例回修时会追错凶手。
- **最小修复**：归因改为「该资源全部贡献者的 scope 列表」或至少在 Detail 中列出。

### F10 · LOW · peakSum 的 ⊤ 粘滞：一次瞬态溢出污染其后整条时间线
- **位置**：src/Cosmos.EffectAlgebra/EffectScript.cs:194、:216-217（cur.IsTop ⇒ 永远 Top；exit 只做有限减法）
- **判词**：enter 溢出置 Top 后，即使该事件早已 exit，峰值永远 ⊤。保守方向没错，但「曾经大过」和「现在大过」在 gate(2) 里不可区分——时间轴上的组合性在这里断了。
- **最小修复**：exit 时对 ⊤ 记录溢出来源事件 id，全部退出后回落到有限重算。

### F11 · LOW · 文档漂移：EffectEvent 到底几个字段
- **位置**：EFFECT_SCRIPT.md:62「5 字段位置记录」 vs src/Cosmos.EffectAlgebra/EffectScript.cs:19「4 字段位置记录」（实际 4 个：Lifetime/Scope/Footprint/Loop）
- **判词**：小到不值一提，直到有人按文档逐字段对 JSON。

### F12 · LOW · Weight.Of 以异常表达偏函数：聚合路径上埋着跳出代数的出口
- **位置**：src/Cosmos.EffectAlgebra/Algebra.cs:38
- **判词**：`KIND_MIX` 该报，但报的方式是 InvalidOperationException 从纯代数函数里射出——调用方一次跨 kind 聚合就让整条组合管道崩掉而不是拿到一个「不可聚合」的值。非法状态应可表示为值（错误值/Result），而不是靠调用者记得 try。
- **最小修复**：返回 `Option<double>`/专用 Weight 结果类型，把 throw 留给真正的契约破坏。

---

## TOP-3（本轮最重要）

1. **F2 — Parallel/Sequence 去重吞冲突 + 两层裁决相反**：组合性的底线是「组合的结果仍属于同一代数、且语义不随表示层翻转」。Signature 层把两条并发 create 合并成一条还少算峰值，Script 层同样的逻辑程序报冲突——这是承诺（效果可组合）与现实（效果可组合，但你得挑对入口，挑错了答案会变）的直接背离。
2. **F3 — Loop 覆写 scope 使历史查询失忆**：嵌套 loop 是任务要求的组合爆炸形态之一，实测三层嵌套后内层身份全灭、scoped Peak 从 10 归零，全程零告警。组合不应有副作用，尤其是对「过去」的查询结果。
3. **F5 — ω=⊤ 整事件豁免污染共享资源的记账**：居民层 × 有限层的组合必然产出幽灵 Leak 或「release 早于 create」式的胡话归因，且 Violation 不披露豁免的存在。这是最典型的「隐式上下文改变 a+b 结果」。

---

## Usability 裁决

核心价值载体（NatStar/ZStar/Interval 的 ⊤ 律、Compatible 25 格矩阵、At(t) 纯函数）确实是简单的东西：单一职责、值语义、定律可测。但围绕它的组合面在三个方向上违背了「程序员无需读实现即可预测行为」：

1. **同名不同义**：Sequence/Parallel 是同一个并集，名字许诺的语义不存在（F1）；ToJson 叫 round-trip 实为有损投影（F6）。
2. **表示决定裁决**：同一概念程序在 Signature 层、Script 层、JSON 层得到三套不同答案（F2/V5/F6），用户必须先学「哪种表示会被认真对待」。
3. **组合改写过去**：Loop 擦 scope（F3）、⊤ 粘滞（F10）、first-wins 归因（F9）都让「先组合后查询」得不到「先查询后组合」能推出的结论。

**裁决：不可依赖（not dependable as-is）。** L1 数值核值得信任；任何涉及 Loop rescope、多事件同资源、⊤ 与有限混用、跨层序列化的组合，使用前必须读实现或写探针测试验证——而这正是好 API 应当免除的税。

---

## 证据清单（实际读取过的文件）

- README.md
- EFFECT_SCRIPT.md
- src/Cosmos.EffectAlgebra/Algebra.cs
- src/Cosmos.EffectAlgebra/Objects.cs
- src/Cosmos.EffectAlgebra/Numeric.cs
- src/Cosmos.EffectAlgebra/SignedNet.cs
- src/Cosmos.EffectAlgebra/DerivedMetrics.cs
- src/Cosmos.EffectAlgebra/EffectScript.cs
- src/Cosmos.EffectAlgebra/EffectScriptContract.cs
- src/Cosmos.EffectAlgebra.Runtime/Fiber.cs
- src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs
- tests/Cosmos.EffectAlgebra.Tests/LoopCombinationTests.cs
- tests/Cosmos.EffectAlgebra.Tests/CompatibleMatrixTests.cs
- tests/Cosmos.EffectAlgebra.Tests/ScopeOrderTests.cs
- tests/Cosmos.EffectAlgebra.Tests/CrossLayerTests.cs
