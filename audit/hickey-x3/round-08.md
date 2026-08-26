# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 8 轮 —— Composability（Rich Hickey 视角）

> 视角：组合性。「简单」的代数让你不看实现就能预测 `a+b`；本轮问一个问题：**把两个东西放一起之后，你拿到的还是不是同一个代数里的东西？你能不能在不读源码的前提下说出结果？**
> 方法：全量精读 L1/L1-script/Runtime 核心与指定测试；仓库外探针工程（`P:\Temp\hickey-probe`，引用 L1 net10.0，零源码改动）实测 14 个组合场景。本报告未读取 `audit/` 下任何历史报告；「历史结论」以仓库内 README/EFFECT_SCRIPT 及源码注释中的审计修复标记（R4-Fx/R10-Fx/OPEN-x）为准逐条核验。
> 基线：`dotnet test tests/Cosmos.EffectAlgebra.Tests` 306 passed / 0 failed（本轮实测复跑确认）。

---

## 核实矩阵（仓库内声明的历史结论 · 逐条裁决）

| # | 历史声明（出处） | 裁决 | 证据 |
| --- | --- | --- | --- |
| H1 | 「ω=⊤ 居民层静默豁免泄漏检测」（README 锐边 / OPEN-4） | **仍在（设计锁死）** | `src/Cosmos.EffectAlgebra/EffectScript.cs:161`（gate(1) 仅有限 ω 入 net）、`:286`（闭包 `if (e.Loop.Count.IsTop) continue`）。行为与文档一致 |
| H2 | R4-F2「Join 实现 merge_I：[10,10]⊔[50,50]⇒单条 [10,50]，Peak=50 非 60」 | **已修（有测试）** | `src/Cosmos.EffectAlgebra/Objects.cs:196-204`；`tests/.../HickeyX2FixTests.cs:40-51`；探针 P2 复现 Peak=50 |
| H3 | R4-F4「Parallel 冲突抛 PARA_CONFLICT 不静默吞并」 | **已修（有测试），但引入新洞** | `src/Cosmos.EffectAlgebra/DerivedMetrics.cs:55-63` + `HickeyX2FixTests.cs:73-79`；新洞见 F2/F3 |
| H4 | R10-F2「lifetime [⊤,⊤] 非法输入 fail-fast」 | **已修** | `src/Cosmos.EffectAlgebra/EffectScriptContract.cs:73` |
| H5 | OPEN-1「Event 自带 Scope，消除自由变量 loopScope」 | **部分修** | Event 确带 Scope（`EffectScript.cs:22-24,87`），但 JSON 契约仍强制每条 claim 写 scope，且该字段在 At/Audit 中被 `e.Scope` **静默改写**（见 F6，探针 P4 实测）——自由变量没消除，只是换了地方埋 |
| H6 | EFFECT_SCRIPT.md §5「Union 交换/结合/幂等，iter19 已证」 | **部分固化** | 幂等有测试（`tests/.../AlgebraLawsTests.cs:250-263`）；**交换律、结合律零测试**，纯注释承诺。Join 同理：幂等/交换有测（`HickeyX2FixTests.cs:53-59`），结合律无测（探针 Q4 抽查本轮成立，但无人锁它） |
| H7 | LoopCombinationTests.cs:93「嵌套等价 Loop(Loop(b,ω1),ω2)==Loop(b,ω1×ω2)」 | **部分修（只锁 Peak）** | 测试仅断言 Peak 相等；嵌套 loop 的 **scope 抹除**（内层 Loop("A") 被外层 Loop("B") 覆盖）无任何断言——探针 P3b 实测内层 scope 信息丢失 |

---

## 新发现

### F1 · [HIGH] Union 与 Join 同型不同义：组合产物不可从值本身预测
- **位置**：`src/Cosmos.EffectAlgebra/Objects.cs:184-194`（Union，Claim 集合并，不去重 size）、`Objects.cs:196-211`（Join，按 (Kind,Res,Mode,Scope) 合并 size 取 Merge 包络）
- **事实**：两条 create 同资源 size [10,10]、[50,50]：`Union` 后 `Peak.Compute`=**60**（求和），`Join` 后=**50**（包络）。二者返回**完全相同的类型** `Signature`，无任何标记区分来源；再喂给 `Combination.Loop(x,3)` 得 180 vs 150（探针 P2/P2b 实测）。哪个是「对」的取决于上游把两次占用解释为并发还是备选——这个解释**没有落在类型里，也没落在值里**。
- **判词**：你把两个数加起来，得到一个看不出是怎么加出来的数——这不是半格，这是信息碎纸机。
- **最小修复**：文档必须给出二者的选择判据（并发⇒Union/求和、条件分支⇒Join/包络）；更彻底的做法是给 `Signature` 附一个离散的构造来源标签，或在 `Compatible` 之外提供显式 `Alternatives(sig)` 包装，使「备选」成为可表示的状态而非隐式约定。

### F2 · [HIGH] 兼容判定三套口径：Parallel 对 Scope 全盲，Audit 按 event scope，NetTable 按 ⊆*
- **位置**：`src/Cosmos.EffectAlgebra/DerivedMetrics.cs:57-62`（PARA_CONFLICT 只比对归一化 ResourceId，**完全不比较 Scope**）；对照 `EffectScript.cs:180`（gate(3) 按 `(r, e.Scope, mode)` 分组）、`Algebra.cs:117-118`（Net/Peak 按 `IncludedIn(scope)` 过滤）
- **事实**（探针 P1a/P1b）：`Scene("A")` 与 `Scene("B")` 各一条 create 同一 GPU buffer——`Combination.Parallel` **抛 PARA_CONFLICT**（尽管两景永不同屏，逻辑上不可能冲突）；同一对 claim 放进 `EffectScript.Audit` 则**不报兼容冲突**（只报 Leak）。而同 scope 的 create×create 经 `Sequence` 又**静默通过**（P1c）。同一兼容矩阵在三个入口三种行为，使用者无法预测。
- **判词**：「这两个 create 冲突吗？」的正确答案是「看你怎么问」——一个谓词三个真值，代数就死了。
- **最小修复**：抽单一函数 `Conflict(c1,c2)` 内嵌 scope 可比性判断（如 `c1.Scope.IncludedIn(commonScope)` 或要求 scope 相等），Parallel/Audit/gate(3) 共用它。

### F3 · [HIGH] 「复制两份」有两种拼法两种答案；Sequence 蒸发冲突证据；x∥x 非幂等
- **位置**：`DerivedMetrics.cs:37-47`（Loop 缩放 size）、`DerivedMetrics.cs:49-50`（Sequence≡Union）、`Objects.cs:143-150,184-194`（ImmutableHashSet 去重）、`DerivedMetrics.cs:55-63`（Parallel 自反即抛）
- **事实**（探针 Q3）：`Loop(body,2)` ⇒ 单 claim size=[20,20]，Peak=20；`Union(body,body)` ⇒ 集合去重后**仍是 1 条 size=[10,10]**，Peak=10。同一条 `Sequence(create,create)` 把 create×create 的冲突证据**静默坍缩为一条 claim**（Q3c），下游 gate(3) 永远看不见。而 `Parallel(x,x)` 对任何含 CONFLICT-mode 的效果**必抛**（Q3d）——组合子对自己都不封闭，何谈结合律。`Loop(body,1,G)` 还不是恒等：scope 被改写为 G（P3a，`body.Equals(once)=False`），「什么都不做」也掉出代数。
- **判词**：ω 份副本在一个算子里是乘法，在另一个算子里是集合论消消乐；同一份代码，两种算术。
- **最小修复**：(a) 文档明示 Union 是**集合语义、重复无意义**，并把「计数」职责唯一地交给 Loop/ω；(b) `LoopCount.Of(1)` 时跳过 rescope 保持恒等；(c) Parallel 的自冲突（a∩b 同键同 claim）应视为幂等而非冲突。

### F4 · [HIGH] 审计不可组合：绿+绿⇒红，且剧本层没有任何组合算子或告警
- **位置**：`src/Cosmos.EffectAlgebra/EffectScript.cs:81-99,127`（公开面仅 `At`/`Audit`/`Audit`，反射证实，探针 P7c）；`EffectScript.cs:64-70`（Budget 构造即固定，无合并语义）
- **事实**（探针 Q2）：脚本 A（create[0,20]+release[0,20] size10，cap15）`Audit().Passed=True`；脚本 B 同样 `True`；手工 concat 两份 Events 后 **6 条违例**：PeakExceeded 20>15、create×create、release×release、Leak。框架既不提供 `Combine(a,b)`，也不在 Budget 上定义合并（交集？逐资源 min？未定义），更不会警告「两份各自通过的剧本拼起来会爆」。验证的代价随组合规模线性重付，且没有局部性原理可用。
- **判词**：一个卖「可组合」的框架里，审计是全有或全无的全局重启——你证明了零件合格，整机还是要重新上流水线。
- **最小修复**：至少提供 `EffectScript.Concat(a,b, BudgetMerge)` 显式算子 + 文档声明「Passed 不可组合」；理想态是按 (resource,scope) 分片审计使不相交分片的结果可拼接。

### F5 · [MED] Script.Audit 跨互不可比 scope 加总峰值，与 Peak.Compute 的 ⊆* 语义直接矛盾
- **位置**：`EffectScript.cs:186-206`（peakSum 对所有事件无条件累加，无任何 scope 过滤）vs `src/Cosmos.EffectAlgebra/Algebra.cs:117-119`（`if (!c.Scope.IncludedIn(scope)) continue`）
- **事实**（探针 P5）：Scene A、Scene B（跨标签不可比）各 create 同资源 size 10，cap 15：按 `Peak.Compute` 的 ⊆* 视角每景峰值 10 ≤ 15 应通过；`Audit` 却报 **PeakExceeded 峰值 20 > 预算 15**，且归因 scope 是 `Scene("A")`——仅仅因为它先写进脚本（`EffectScript.cs:154,178` 首写者归因）。同一个数据集，「峰值」这个词在两个入口是两个量纲。
- **判词**：预算到底是全局的还是每景的，代码用两个互相矛盾的答案替用户做了决定，然后连罚单都开给了无辜的 A。
- **最小修复**：Budget 键升级为 `(ResourceId, ScopeId)` 或至少文档锁死「Audit 峰值为全局求和」；Violation 归因改为聚合所有贡献者而非首写者。

### F6 · [MED] JSON 契约强制填写一个毫无效力的 claim.scope 字段（非法状态可表示）
- **位置**：`src/Cosmos.EffectAlgebra/EffectScriptContract.cs:135`（`Require(c, "scope")` 强制）；`DerivedMetrics.cs:41,43,45`（Loop 无条件 `with { Scope = loopScope }`）；`EffectScript.cs:87,180`（At/gate(3) 一律用 `e.Scope`）
- **事实**（探针 P4）：JSON 里 claim 写 `"scope": {"type":"method","scene":"SomeMethod"}`、event 写 `{"scene":"Battle"}` ⇒ 解析成功、`At(t)` 返回的 claim scope 是 `Scene{Battle}`——claim 级 scope 被静默丢弃，无告警。AI 契约（EFFECT_SCRIPT.md §4 示例同样携带该字段）诱导产出者相信它在起作用。源码注释自己承认了这一点（`EffectScript.cs:178-179`「此前用 claim 自带 c.Scope…导致分裂」，却保留了这个必填死字段）。
- **判词**：让用户填一张必填的表格，然后把表格丢进碎纸机——这不是容错，这是制度化的谎言。
- **最小修复**：Parse 阶段校验 claim.scope==event.scope 否则 FormatException；或干脆从契约中删除该字段（破坏性变更需版本化）。

### F7 · [MED] C# 层与 JSON 层值宇宙不对称：合法 C# 剧本无法往返
- **位置**：`EffectScriptContract.cs:202-209`（SerializeScope 仅支持 Scene/Method/Type/Global，其余 throw）；`:224-227`（SerializeResource 仅 5 种）；对照 `Objects.cs:60-77`（8 种 scope 构造子）、`Objects.cs:22-39`（14 种资源构造子）
- **事实**（探针 P8）：C# 侧完全合法的剧本（event scope=`Loop("L")`，claim resource=`Tree("/root")`）调用 `ToJson` 直接抛 FormatException。Parse 侧同样只认 4 scope×5 resource。两层各自内部自洽，但「C# 构建 → JSON 交换 → 再解析」这条最自然的组合管线在合法输入上断裂。
- **判词**：你的代数有 8×14 个音符，导出的乐谱只印 4×5 个——剩下的音符不是错音，是哑弹。
- **最小修复**：要么契约扩到全集（scope.type 增加 loop/conditional/async/shell，resource 增加 tree/physics/signal…），要么在 C# 构造侧就限制 EffectEvent 只接受契约可表达的子集类型（把边界做成类型，而不是做成运行时异常）。

### F8 · [LOW] 代数律固化清单不全：注释承诺多于测试锁定
- **位置**：`EFFECT_SCRIPT.md` §5（「Union 交换/结合/幂等 iter19 已证」）；`tests/.../AlgebraLawsTests.cs:250-263`（仅幂等）；`HickeyX2FixTests.cs:53-59`（Join 幂等/交换，无结合）；`LoopCombinationTests.cs:93`（嵌套仅锁 Peak）
- **事实**：交换/结合律、Loop 的 rescope 行为、`Loop(ω=1)≠恒等` 均无测试。本轮探针抽查 Join 结合律成立（Q4），但没有测试守着它，下次重构就是静默漂移。
- **判词**：写在注释里的定律是愿望，写在测试里的定律才是合同。
- **最小修复**：补 Union/Join 的结合+交换性质测试（PropertyTests 已有 fuzz 骨架可直接挂），加 `Loop(body,1,s) 与 s 的 scope 断言`（无论决定恒等还是改写，都应锁死）。

### F9 · [LOW] Violation 归因依赖字典首写序，组合顺序影响诊断文本
- **位置**：`EffectScript.cs:170`（`if (!netScope.ContainsKey(r)) netScope[r] = e.Scope;`）、`:178-179`（peakScope 同）、`:153-154`（Resolve* 回退 Global）
- **事实**：net/peak 按**归一化资源**全局聚合后，Violation 的 scope 取「第一个贡献者」。不影响 Passed 布尔值，但 AI 回修循环吃的是 Violation 文本——归因错误会把回修引向无关事件（P5 中 Scene B 的贡献被记在 Scene A 头上）。
- **最小修复**：归因收集全部活跃贡献者 scope 列表（grp 结构现成可查）。

### F10 · [LOW] Weight.Of 以异常表达偏函数：又一处「组合到一半掉出代数」
- **位置**：`src/Cosmos.EffectAlgebra/Algebra.cs:36-38`
- **事实**：跨 kind 聚合抛 `InvalidOperationException(KIND_MIX)`。签名宣称 `Kind×Kind→ℝ∪{⊥}`，实际是 `Kind×Kind→ℝ`＋throw。与 Parallel 的 PARA_CONFLICT 同款模式：组合失败不走值通道。
- **判词**：类型签名说「我总能给你个数」，实现说「除非我说不给」——诚实的做法是把 ⊥ 做成值。
- **最小修复**：返回 `double?`/`TryOf`，或提供 `TryWeight` 与抛错版并存。

---

## 对抗性实例（任务要求 ≥2，「组合爆炸下结果不可预测」）

**例 1 · 嵌套 loop + 跨 scope 峰值（探针 P3b + P5）**
`Loop(Loop(body,2,Loop("A")),3,Loop("B"))`：内层 loop scope `Loop("A")` 被 `DerivedMetrics.cs:41` 的无条件 rescope 覆盖为 `Loop("B")`——三层嵌套后你既不知道每层的 ω，也不知道中间层的 scope；嵌套等价测试（`LoopCombinationTests.cs:93`）对此零断言。叠加跨 scope：两个互不可比 Scene 各 create size10、cap15 ⇒ `Audit` 报 PeakExceeded「峰值 20 > 预算 15」并归因 Scene A（P5 实测输出）。使用者按 `Peak.Compute` 的 ⊆* 直觉预测「每景 10 应通过」，实测红——且换一下 events 数组顺序，罚单会开给别人。**无任何告警提示两种口径存在。**

**例 2 · Join 与 Union 混用 + Sequence 吞证据（探针 P2/Q3）**
同一对效果：`Union` ⇒ Peak 60，`Join` ⇒ Peak 50；各自再 `Loop(·,3)` ⇒ 180 vs 150。产物同为 `Signature`，后续一切度量（Peak/net/Audit）结果随上游选词翻转而无人能从值上看出。更进一步：`Sequence(create,create)`（同 scope 同资源）被 ImmutableHashSet 去重成**一条** claim（Q3c），create×create 冲突证据在组合第一步就蒸发；换成 `Parallel(create,create)` 则当场爆炸；换成 `Union` 则无声通过且峰值减半。三种拼法三种命运，全部符合各自签名——这就是「easy 而不 simple」的完整标本。

---

## TOP-3

1. **F2 · 兼容判定三套口径**（Parallel scope 盲 / Audit 按 event scope / Net 按 ⊆*）——同一谓词三个真值，是「能否预测 a+b」这一核心承诺的直接违背。
2. **F3 · 复制语义分裂 + 冲突证据蒸发**——Loop×ω、Union 去重、Parallel 自斥三种「放一起」互不相容，代数对自己的恒等元都不封闭。
3. **F4 · 审计不可组合且无剧本层组合算子**——绿+绿⇒红无告警、Budget 合并无语义，验证成本随组合线性重付，框架名不副实。

---

## usability 裁决

**不可预测（fail）。** 「效果可组合」在本框架只在最窄的意义上成立：`Signature.Union` 是干净的集合半格，`At(t)` 是纯函数。但围绕它的每一个组合入口都在悄悄换语义——兼容性有三套口径、峰值有两种量纲、「复制」有两种算术、审计不可拼接、JSON 契约强迫填写死字段并在合法值上断裂。使用者**必须**读完 `DerivedMetrics.cs`、`EffectScript.cs` 扫换线和 `EffectScriptContract.cs` 的每个 throw 才能安全地拼两个效果——这正是「程序员知道得越少越好」的反面。修复不需要重写代数内核（内核的 ⊤ 律和区间算术是扎实的），需要的是：一个共享的冲突谓词、一套写进类型的构造来源、一个显式的剧本组合算子，以及把注释里的定律搬进测试。

## 证据：本轮读取过的文件

- `README.md`、`EFFECT_SCRIPT.md`
- `src/Cosmos.EffectAlgebra/Algebra.cs`、`EffectScript.cs`、`EffectScriptContract.cs`、`SignedNet.cs`、`DerivedMetrics.cs`、`Objects.cs`、`Numeric.cs`
- `src/Cosmos.EffectAlgebra.Runtime/Fiber.cs`、`DependencyGraph.cs`、`LoadValidation.cs`（grep 定位）
- `tests/Cosmos.EffectAlgebra.Tests/LoopCombinationTests.cs`、`CompatibleMatrixTests.cs`、`ScopeOrderTests.cs`、`CrossLayerTests.cs`、`HickeyX2FixTests.cs`、`AlgebraLawsTests.cs`（节选）
- 探针工程 `P:/Temp/hickey-probe/{probe.csproj,Program.cs}`（仓库外，未改动仓库源文件）
- 命令：`dotnet build src/Cosmos.EffectAlgebra`（0 err/0 warn）、`dotnet test tests/Cosmos.EffectAlgebra.Tests`（306/306 通过）、探针 `dotnet run` 两轮共 14 场景
