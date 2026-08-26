# Rich Hickey Round 10 — 总收敛审计（Synthesis 透镜）

- 审计员：ox-alpha（总收敛审计员）
- 输入：`audit/rich-hickey-round01..09*.md`（9 轮）+ `audit/rich-hickey-api-audit.md`（前 5 轮 API 综合）+ 7 个 L1 源文件
- 方法：9 轮结论交叉比对 + 对当前磁盘源码逐点复核（行号以 2026-02 工作区为准）。所有引用的代码事实均经 grep/sed 二次验证，非转录自轮次报告。

---

## 一、结论先行

1. **核心代数载体（Numeric/SignedNet 的 ⊤-闭环类型、Signature 半格并、Interval 构造校验）在 9 轮中零争议地被确认为真值**——这是本库的承重结构，不是负担来源。
2. **会造成用户使用困难的共同 HIGH 根因共 6 条**（≥3 轮点名，见第三节交叉表），按危害排序：① 序列化静默兜底（6 轮）② loop=0 文档示例即崩/错报（3 轮，含 2 个独立 Blocker）③ scope 双份真相 + 违例归因伪造 Global（4 轮）④ Weight.NaN 毒值死代码（5 轮）⑤ Budget 可变字典泄漏（3 轮）⑥ 四名一实别名（3 轮）。
3. **明确回答"是否造成用户使用困难"：是。** 其中 ①② 是**当下即可触发**的用户路径损伤：复制 EFFECT_SCRIPT.md §4 旗舰示例第一次跑 Audit 即失败或崩溃；任何携带 Tree/Disk/Signal 等 10 类资源的脚本 ToJson 后数据被无声改写。③–⑥ 是高概率误用陷阱。
4. 约 60% 的 HIGH 根因属**可安全削减**（机械修复、不丢表达力）；其余为**被证明义务/设计锁死**（PDR/iter-code 锁），只能补文档与诊断。
3 条 Top 削减见第五节，均给出最小 diff 方向。
5. 环境注记：本轮尝试运行测试套件复核基线，但本机 MSBuild/VS 安装损坏（`hostpolicy.dll` / `Microsoft.Build.Utilities.v4.0` 加载失败），**非仓库问题**；验证计划中的测试命令需在健康工具链上执行。

---

## 二、9 轮交叉表

图例：R1=simple-easy, R2=value-identity-state, R3=decomplect, R4=data-contract, R5=maybe-not, R6=naming-hammock, R7=extensibility, R8=composability, R9=approachability。

| 共同根因 | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 | R9 | 轮数 | 当前状态（源码复核） |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A. SerializeResource/ResourceKey `_=>memory:0`、SerializeScope `_=>global` 静默兜底 | 高 | M3 | — | H1/H2 | Blocker#10 | F12🟠 | CRITICAL×2 | — | — | **6** | 仍在：EffectScriptContract.cs:199,218,236 |
| B. `loop:0` 语义陷阱（文档示例自身触发崩溃/Leak；ω 名实不符） | — | — | — | B1 Blocker | — | F1🟠(词根) | — | — | F1 Blocker | **3** | 仍在：EffectScript.cs:187,206 除零未守卫；EFFECT_SCRIPT.md:144 仍写 `"loop":0` |
| C. scope 双份真相 / Violation 归因伪造 Global | 高(双scope) | N/A | F7 | M3 | — | — | — | N2(scope压平) | F3 HIGH+F6 LOW | **4** | 仍在：EffectScript.cs:223,231,287 硬编码 `new ScopeId.Global()`；claim 级 scope 在剧本路径从不参与判定 |
| D. Weight.Of 以 NaN 编码 ⊥（死代码/毒值） | 低 | — | F4 MAJOR | — | #7 High | F11🟡 | INFO | M4 | — | **5** | 仍在：Algebra.cs:38；全 src 零调用点 |
| E. Budget.Caps 可变字典门面 + Budget.None 可变单例 + default(Budget) NRE 面 | 低 | H1/H2/H3 HIGH | — | — | (N4同根) | F5🟡 | — | — | F4#budget | **3** | 仍在：EffectScript.cs:311–320，`None = new(new Dictionary<>())` |
| F. 四名一实 Union/Join/Sequence/Parallel | 高(Top3#1) | — | — | — | — | F3🟠 | — | N1/M1 | — | **3** | 仍在：Objects.cs:192; DerivedMetrics.cs:50,53（api-audit 判"生成器 emit 锁死，拒删"，待核） |
| G. Signature.GetHashCode 顺序敏感（Eq/Hash 契约违约） | — | H4 HIGH | — | — | — | — | — | M2 Med-High | — | 2 | 仍在：Objects.cs:204–211（潜伏，现无哈希容器消费） |
| H. Peak.Compute 跨桶聚合（量纲隔离击穿） | 中 | — | F1 BLOCKER | — | — | — | — | — | — | 2 | 仍在：Algebra.cs:118–126 仅滤 Release |
| I. CONFLICT 集两处写（gate(3) 二次硬编码 vs IsCompatible） | 中(近亲) | — | F3 MAJOR | — | — | — | HIGH(mode#4) | — | — | 2–3 | 仍在：EffectScript.cs:237–243 与 Algebra.cs:18–28；今日恰好等价 |
| J. Unknown→Use fail-open（冲突检测失明） | — | — | F5 | — | #9 High | F10🟡(标签反) | — | — | — | 3 | 锁死（PDR §3.2.3 P4）；且 Algebra.cs 注释把 fail-open 误标为 "fail-closed" |
| K. `Size ?? [1,1]` 缺省静默吸收（≥11 处散布兜底） | — | — | — | — | #3/#8 Medium | F8🟡 | — | — | F4#1 | 3 | 锁死（§3.1.5a DO-1）；兜底散布未收口 |
| L. ℕ*→ℤ* 转换/缩放三份拷贝 + ZStar/(long) 静默回绕 | 高 | M4 | F6/N18(S18) | — | #14/15 Medium | — | — | — | — | 3 | 仍在：Algebra.cs:72–90 / EffectScript.cs:299–306 / DerivedMetrics.cs:57–62；SignedNet.cs:30–33 unchecked |
| M. 死代码：EffectScript.cs:272/274 逐字重复行 | 低 | — | N4物证 | Low表 | #19 | Info | — | L2 | — | 5 | 仍在：EffectScript.cs:272,274 |

低于 ≥3 轮阈值但值得留档：G（潜伏雷）、H（唯一 BLOCKER 级语义缺陷）、I（漂移定时炸弹）、ResourceKey 双编码（R4/R9 提及）。

---

## 三、共同 HIGH 根因（≥3 轮点名，按跨轮次数排序）

> 排序 = 轮数 × 当下可触发性 × 危害类型（静默错误 > 崩溃 > 认知税）。

### A. 序列化静默兜底臂（6 轮）— **确认造成用户困难**
`SerializeScope _ => global`（EffectScriptContract.cs:199）、`SerializeResource _ => {memory:0}`（:218）、`ResourceKey _ => "memory:0"`（:236）。15 个 ResourceId 构造子只覆盖 5 个；Shell/Loop/Conditional/Async 四种 scope 全部降级 Global。**同一文件 Parse 侧全部 throw（:102,135,142,179），Serialize 侧全兜底——失败模式不对称**，且与文件头 "fail-fast，非静默漏报" 自述矛盾。后果：round-trip 后资源身份被无声改写 → net/Peak 对错资源计算，审计器输出看似成功的错误结论。这是全库最危险的一类失败（fail-silent 数据损坏 > fail-loud 崩溃）。

### D. Weight.NaN 毒值死代码（5 轮）
Algebra.cs:38。违反本库自己的 "永不 NaN" 铁律（Numeric.cs:5、Deviation.cs:18）。今日零调用点故未实际污染，但它是公开 API：第一个跨 kind 聚合者拿到 NaN，错误延迟到下游某个比较恒 false 处才显形。R3 补充关键证据：KIND_MIX 的运行时守卫根本不存在（Weight 未接线），量纲隔离实际靠各消费点自觉维持——这直接解释了 H（Peak 击穿）为何能发生。

### C. scope 双份真相 + 违例归因伪造 Global（4 轮）
Claim.Scope 必填却在剧本审计路径从不参与判定（gate 全用 e.Scope）；gate(1)(2) 与 Leak 三处违例硬编码 `new ScopeId.Global()`（EffectScript.cs:223,231,287）。直接削弱 R9 点名的 "AI 回修需要充分反例信息" 承诺：多作用域剧本的 Leak/PeakExceeded 丢失发生层级。Combination.Loop 还会压平嵌套 scope（R8 N2）——scope 维度整体不可组合。

### B. `loop:0` 语义陷阱（3 轮，含两个独立推导的 Blocker）
两轮给出了不同但相容的失败路径：R4 — EffectScript.cs:187/206 `ulong.MaxValue / w.Value` 在 w=0 时 DivideByZeroException（守卫只查 IsTop 不查 0）；R9 — 闭包路径 ScaleSize(size,0)=[0,0] ⇒ 净效应不含 0 ⇒ Leak 误报。**EFFECT_SCRIPT.md §4 第二个事件今天仍写着 `"loop": 0`（EFFECT_SCRIPT.md:144，本轮复核确认）**。根因是 `LoopCount` 类型不拒绝 0 却让下游假设 ω≥1，加上名字叫 Loop 实义为并发副本数（R6 F1）。新用户复制文档第一段示例即翻车——上手路径上的实弹。

### E. Budget 可变字典（3 轮）
`readonly record struct Budget` 包 mutable Dictionary（EffectScript.cs:311–320）：相等退化为引用比较；`Budget.None` 单例指向可变字典，一次 cast+写入即污染全程序预算语义；构造函数 `budget.Caps != null ? budget : Budget.None` 只堵 null 壳不防御拷贝（R2 H3）。修复成本极低（换 ImmutableDictionary），是"伪装值"的教科书案例。

### F. 四名一实（3 轮）
Union/Join/Sequence/Parallel 同一实现四个名字（Objects.cs:192; DerivedMetrics.cs:50,53）。名字许诺了 L1 不存在的区分（Parallel 无并行性、Join 无 merge_I——R8 M1 证实 Interval/SignedInterval.Merge 零调用点）。api-audit 判定"生成器 emit + 测试消费，删即破编"——**该锁死声明未经本轮独立核实**（生成器代码不在 7 文件范围），列为开放缺口。

---

## 四、设计锁死（不可改语义，仅补文档/诊断）vs 可安全削减

### 4.1 被证明义务 / 设计锁死

| 项 | 锁的来源 | 只能做的 |
|---|---|---|
| `Unknown→Use` fail-open | PDR §3.2.3 P4 / DO-10（api-audit 明示） | 文档直言 fail-open；AuditResult 增列 `UnknownMode` 弱违例（加法性诊断，不改既有判定）；修正 Algebra.cs 注释中 "fail-closed" 的错误标签（R6 F10） |
| `Size ?? [1,1]` 缺省 | §3.1.5a DO-1，iter-code 锁 | 文档 + 把 `Interval.Default` 改名方向记为建议（ExactOne 更诚实，R6 F8）；新增消费点禁止再散布 `??`，收口到 Claim 构造归一 |
| `Sequence≡Parallel≡Union` 于 L1 | 生成器 emit + LoopCombinationTests 消费（api-audit 拒改判定） | XML doc 置顶声明"L1 无时序/并行区分，Compatible 由 L3 补"；前提是先核实在场缺口 O6（生成器确实 emit） |
| `Add`(∑) vs `Merge`(min/max) 分置 | D-022（Merge 吞守恒 HIGH 缺陷锁） | 保持分置，文档互引（现状注释已是正面样本） |
| ω=⊤ 居民层豁免守恒但仍计峰值 | MA-002 / OPEN-4，iter-code 锁 | 文档（README 已有锐边节）；gate(1) 结果依赖全局 ω 分布这一事实需在 Audit doc 声明（R8 N3） |
| Deviation ⊤ ⇒ 整体跳过 | §9.1 iter-code 锁 | `ExceedsThreshold` 可增三态返回或 `ManualReviewRequired` 违例种类（加法性，不改 Pass/Exceed 语义）（R5 #6） |
| Join 注释承诺 merge_I 但实现为裸 Union | 改实现 = 改 Signature 相等语义（破坏半格并的集合论基础） | 改注释对齐实现，删除 merge_I 承诺（一行修，R8 M1） |

### 4.2 可安全削减（不丢表达力）

| 项 | 为什么安全 |
|---|---|
| 兜底臂 `_ =>` → `throw FormatException`（A） | Parse 侧同文件已示范；序列化从"静默错"变"响亮错"，无合法输入受影响（受影响输入本来就是无法表达的构造子） |
| `Signature.Add` switch 补 default throw / 去 `_` 得 CS8509（R7） | 第 4 个 Kind 今天会被静默丢出三桶；补护栏纯增益 |
| GetHashCode 改顺序无关折叠（G） | 一行级修复，语义不变（修 bug 非改行为） |
| `LoopCount` 构造拒绝 0 或除零点守卫转 ⊤（B） | 当前 loop=0 只有崩溃/误报两种结局，无合法语义可破坏 |
| Budget → ImmutableDictionary + 结构相等（E） | 只消灭变异路径（本身是 bug），API 形状不变 |
| 删死代码：EffectScript.cs:272/274 重复行、恒假 closureT 过滤（M） | 5 轮点名，零风险 |
| Weight.Of 删除或改 `WeightVal`/抛 InvalidOperationException（D） | 零调用点（grep 多轮一致确认），删除无涟漪 |
| ZStar/(long) 下转加 checked→⊤、三份 scale/sign 拷贝收敛为单一 helper（L） | 方向保守（回绕⇒⊤ 与 NatStar 同律）；收敛是重构，需测试护航，排后 |
| gate(3) 改调 `Compatible.IsCompatible`（I） | 今日两处恰等价（快照等价），替换后行为不变、消除漂移面 |
| Violation.Scope 填真实 e.Scope（C） | 从伪造 Global 变为真实值，纯信息增益；Violation.Kind string→enum 属破坏性面，另案 |

---

## 五、Top 3 可安全削减（按降低用户困难 ROI）

### #1 序列化兜底臂改 fail-fast（根因 A）— ROI 最高
**理由**：6 轮共同点名；唯一成类的"静默给下游递错数据"路径；修复是 3 行机械 diff；直接兑现文件头自己的承诺。
**最小 diff 方向**（EffectScriptContract.cs）：
```csharp
// :199  _ => new Dictionary<string, object?> { ["type"] = "global" }
_ => throw new FormatException($"不可序列化的 scope: {s}")
// :218  _ => new Dictionary<string, object?> { ["memory"] = 0 }
_ => throw new FormatException($"不可序列化的 resource: {r}")
// :236  _ => "memory:0"
_ => throw new FormatException($"不可序列化的 budget 键资源: {r}")
```
配套：`Objects.cs Signature.Add` 的 switch 补 default throw（或转 switch expression 吃 CS8509）；补 round-trip 测试断言 `Parse(ToJson(Tree(...)))` 抛 FormatException。

### #2 `loop:0` 双修：文档示例 + LoopCount 收口（根因 B）
**理由**：上手第一公里实弹——旗舰示例复制即崩/误报；2 轮各自独立推出 Blocker；diff 极小。
**最小 diff 方向**：
1. `EFFECT_SCRIPT.md:144`：删除 `"loop": 0`（缺省即 1）——一行文档修，立即消除复现路径。
2. `DerivedMetrics.cs LoopCount.Of`：`if (count == 0) throw new ArgumentOutOfRangeException(...)`（fail-fast，契约层 ParseLoop 同步拒绝），或在 EffectScript.cs:187/206 除法守卫追加 `w.Value != 0 &&` 并将 w=0 视为保守 ⊤。二选一：类型层拒绝更符合本库"构造即合法"纪律。

### #3 违例归因真实化 + CONFLICT 单一真源（根因 C + I 合并修）
**理由**：一次小 diff 同时解决"AI 拿不到反例层级"（4 轮）与"CONFLICT 集漂移定时炸弹"（2–3 轮）；不改任何判定结果，只改信息的真与来源的唯一性。
**最小 diff 方向**（EffectScript.cs）：
```csharp
// :223/:231/:287 三处  new ScopeId.Global()  →  e.Scope
// :237–243 gate(3)：组内改为对 kv.Value 两两调 Compatible.IsCompatible((Mode)m1,(Mode)m2)
//   （或至少抽 Compatible.ConflictModes 单一静态数据源供两侧引用）
```
配套测试：多作用域剧本断言 Leak 的 Violation.Scope == 事件 scope；gate(3) ↔ 逐点 IsCompatible 的等价性 property test（锁住"快照等价"为"设计保证"）。

次优先（Top 3 之外但顺手）：GetHashCode XOR 化（G）、删 272/274 死行（M）、Budget→ImmutableDictionary（E）——合计 <20 行。

---

## 六、仍开放的 proof gap / 锐边

| # | 缺口 | 性质 |
|---|---|---|
| O1 | Peak.Compute 跨桶聚合（H）的实际影响面未知：是否有调用方给含 read/write size 的签名调过 Derived.Peak？PDR §3.3.2 对 read/write 是否计入峰值的意图未对照原文裁决 | 语义意图 gap；若是 bug 则为现存唯一 BLOCKER 级语义缺陷 |
| O2 | `loop:0` 两条失败路径（除零崩溃 / ScaleSize Leak 误报）均为静态推演，未运行复现 | proof gap；需一条最小 JSON 用例固化 |
| O3 | At(t) ⇔ Audit 等价仅靠注释断言，无交叉验证律测试（R8：At 正确 ⇏ Audit 正确） | 测试缺口 |
| O4 | Signature.GetHashCode：确认今日全 src 无哈希容器消费，但 samples/tests/Generator 未穷尽排查 | 潜伏雷边界未封 |
| O5 | SerializeScope global 降级是否被现有 round-trip 测试覆盖未知（各轮均未读 tests/ 取证） | 测试覆盖 gap |
| O6 | api-audit "生成器 emit Sequence/Parallel/Join，删即破编" 的锁死声明未独立核实（Generator/Analyzer 不在本轮 7 文件范围） | 锁死判定依据待验 |
| O7 | ZStar unchecked 回绕与 `(long)` 下转变负：可达输入域（size 接近 ulong.MaxValue）未测 | 边界锐边 |
| O8 | L3 Analyzer KIND_MIX 启发式与 L1 缺口（Weight 未接线）的互补程度未验证 | 层间 gap |
| O9 | 本机 MSBuild/VS 损坏致测试套件未能运行复核基线（环境问题，非仓库）；收敛判定的"测试绿灯"证据链悬空 | 环境 gap，需健康工具链补跑 |
| O10 | 锐边存档（锁死后仅文档可解）：`[⊤,⊤]` lifetime 静默脱离审计（fail-open）；`"memory:"` 空后缀撞 Mem() 哨兵 uid=0；ApiMapping 白名单哨兵合并不同资源账目；JSON budget 键与 footprint 资源双编码；重复 JSON 键容忍取末值；Violation 列表序随事件置换变化 | 各 1–2 轮点名，记录备查 |

---

## 七、验证计划

前置：在健康 .NET 工具链上跑 `dotnet test tests/Cosmos.EffectAlgebra.Tests` 记录基线绿灯数（O9）。

| 步骤 | 验证项 | 命令/手段 | 通过判据 |
|---|---|---|---|
| V1 | Top#1 | 新增 round-trip 测试：`ToJson(new ResourceId.Tree("a"))` 断言抛 FormatException；`Parse∘ToJson∘Parse` 对 5 个支持构造子仍幂等 | 新测试绿 + 原测试零回归 |
| V2 | Top#2 | 最小 JSON 用例（§4 示例原样）`Parse(...).Audit()`：修前应复现 O2（崩溃或 Leak）；修文档+LoopCount 后应 Passed | 复现→消除，前后证据留存 |
| V3 | Top#3 | 多作用域剧本断言 Violation.Scope == e.Scope；gate(3)↔逐点 IsCompatible property test（随机 mode 组合 × 事件排列） | 等价性由测试锁定 |
| V4 | 顺手项 | Signature 哈希一致性 property test（随机 Union 序列，Equals⇒HashCode 相等）；编译告警零新增 | property 绿 |
| V5 | O1 | grep samples/tests 对 `Derived.Peak` 调用点的 claim kind 分布；对照 PDR §3.3.2 原文出裁决记录 | 影响面有书面结论 |
| V6 | O6 | 读 Generator emit 逻辑确认是否产出 Join/Sequence/Parallel 调用 | 锁死声明证实或推翻 |
| V7 | 全量回归 | `ci.ps1` / `dotnet test` 全套件 | 与基线持平或更绿 |

---

## 八、残留风险

- 本综合为静态审计 + 源码二次取证；O2/O7 等动态行为未运行复现（工具链受损）。
- Generator/Analyzer/Runtime 层未精读，涉及它们的锁死判定（O6）沿用 api-audit 结论。
- 行号随后续提交漂移；本文引用以 2026-02 工作区快照为准。
