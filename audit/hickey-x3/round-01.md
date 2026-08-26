# Cosmos.EffectAlgebra 对抗性 API 审计 · 第 1/10 轮 · Simple is not Easy（Rich Hickey 视角）

> 独立上下文轮次：未读取 `audit/` 下任何历史报告。核实对象为仓库自身文档（README / EFFECT_SCRIPT / DELIVERABLE）中**自我声明的结论**。
> 方法：通读 L1 全部公共 API 面 + 两个真实消费者样例；在仓库外建探针工程（`D:/Godot/hickey-x3-probe/`）实际运行 6 个误用场景；`dotnet build` 0 错误、`dotnet test` 391 通过作为基线佐证。

---

## 核实矩阵（仓库自declared 结论逐条裁决）

| # | 自declared 结论 | 出处 | 裁决 | 证据 |
|---|---|---|---|---|
| V1 | 「未命中白名单的 API 静默无保护、无警告」 | README.md:40 | **仍在**（诚实标注≠可接受） | ApiMapping.cs:57–58 仅按方法名 Canonical 匹配 38 条；L1/L3 无任何「未覆盖」信号通路 |
| V2 | 「Unknown 模式 = fail-open 静默放行（设计锁死）」 | README.md:42 | **仍在** | Algebra.cs:15 `Resolve(Unknown→Use)`；EffectScriptContract.cs:149 契约层仍接受 `"unknown"` 输入后即刻擦除 |
| V3 | 「Claim.Size 省略 ≠ 未知：`?? [1,1]`」 | README.md:45 | **仍在** | Objects.cs:131–135 `Normalize()` 把 null 膨胀为精确 1 |
| V4 | 「`[⊤,⊤]` 寿命视为非法输入——fail-fast 拒绝（OPEN-B4 修）」 | EffectScriptContract.cs:71–74 | **部分修** | 守卫只在 JSON 解析器：Numeric.cs:70–77 的 Interval 构造子仍接受 `[⊤,⊤]`；C# 直构 `EffectEvent` 可造出永不存活事件，探针场景 A 证实 `Audit().Passed == true`、0 violations（create-without-release 假绿）。guardrail 住错了层 |
| V5 | 「R10-F1：default(Budget).Caps == null 归一为无上限，不 NRE」 | EffectScript.cs:109–110 | **部分修** | `Audit` 路径已归一；但公共属性 `Budget.Caps`（EffectScript.cs:346）对 `default(Budget)` 仍返回 null（探针 E 证实）——消费方自遍历 Caps 即 NRE，归一没做在数据上，做在了一个调用点上 |
| V6 | 「Union 幂等/交换/结合（半格并）；Join 为 merge_I 合并」 | EFFECT_SCRIPT.md §1.1 / Objects.cs:180–215 | **仍在，且是新问题的根** | Union 以 `ImmutableHashSet<Claim>` 承载（Objects.cs:148–154），结构相等折叠重复 Claim ⇒ 并发多重性不可表示（见 F1 探针） |

---

## 新发现

### F1 · HIGH — 同一剧本，框架自己的两条公共路径给出两个不同的峰值答案
- **位置**：EffectScript.cs:175–201（Audit gate(2) 按事件逐个累加 `peakSum`） vs Objects.cs:148–154 + 180–189（`At(t)` 经 `ImmutableHashSet<Claim>` 折叠结构相等副本）+ Algebra.cs:113–125（`Peak.Compute`）。
- **证据（探针 B'，实际运行）**：两个完全相同的并发事件（occupy Memory:7 size[10,10]，lifetime [0,100]）：
  - `script.Audit(cap=15)` → `PeakExceeded: 峰值 20 > 预算 15`；
  - `Derived.Peak(script.At(50), Global)` → **10**（`At(50).OccupyClaims.Count == 1`，第二份副本被 set 吃掉）。
- **判词**：你把「多少份同时存在」交给一个不能数到 2 的容器——这不是抽象泄漏，是抽象撒谎。同一个问题，问门卫说 20，问前台说 10；程序员必须读实现才知道该信谁。这恰恰是 simple 的反面：不是难学，是**不可预测**。
- **最小修复**：二选一并锁死文档——(a) `At` 改多重集/带计数语义；(b) 明文规定「并发多重性只能经 ω 表达，重复 Event 属非法形状」并在 Parse/Audit 拒绝结构等价的同刻存活事件对。同时让 `Peak.Compute` 与 gate(2) 共享同一实现，消灭第三个口径。

### F2 · HIGH — `[⊤,⊤]` 的守卫住在 JSON 解析器里，不住在类型里
- **位置**：Numeric.cs:70–77（Interval 构造子允许 `[⊤,⊤]`）、EffectScript.cs:136–137（`Lo.IsTop ⇒ continue`，sweep 静默跳过）、EffectScriptContract.cs:73（唯一拒绝点）。
- **证据（探针 A，实际运行）**：C# 直构 `new EffectEvent(new Interval(NatStar.Top, NatStar.Top), scope, create-footprint)` → 构造成功，`Audit().Passed == True`，0 violations；同一形状走 `EffectScriptContract.Parse` → FormatException。
- **判词**：「非法状态不可表示」是这个项目自己写在每页文档里的信条，结果最危险的非法状态——永不存活却携带 footprint 的事件——只需要绕过一个解析函数就能表示。AI 消费者是 C# 和 JSON 双语者；守卫双语者中的一半等于没守卫。fail-open 还包装成了 fail-fast。
- **最小修复**：`EffectEvent` 构造子校验 `Lifetime.Lo.IsTop ⇒ throw`（一行），让类型兑现它自己的广告。

### F3 · HIGH — 白名单 miss：整个编译期承诺的缺省行为是 fail-open
- **位置**：README.md:40（自我承认）、ApiMapping.cs:55–58（Canonical 名字匹配是唯一入口）。
- **证据**：38 条白名单之外的一切调用——`PackedScene.Instantiate` 的真实方法链、用户自己的资源包装 helper——静默无 Claim、无诊断、无「此 API 未受保护」信号。样例 SampleGame.cs:26–33 同时展示了机制的两面：`HealthyEnemy.AddChild` 靠同名匹配获得保护，也意味着任何叫 `AddChild` 的无关方法都会被归因同样的 Claims。
- **判词**：guardrail 只有在你知道它在哪里时才是 guardrail；不知道的时候它是装饰，而装饰比没有更糟——它让人停止担心。一个以「编译期守恒审计」为卖点的系统，其最常见的失败模式（写了没被看见的代码）必须是显式事件，不能是缺省背景。
- **最小修复**：L3 对「疑似资源操作动词但未命中白名单」发最低级别诊断（覆盖率提示），或在 CI 导出「已保护 API 清单」与工程实际调用的差集。

### F4 · MED — `IsConserved` 谓词语义反转：从没碰过的资源「不守恒」
- **位置**：Algebra.cs:96–101：`if (!_net.ContainsKey(key)) return false; // 无净效应记录 ⇒ 未闭合`。
- **证据（探针 C，实际运行）**：`Signature.Empty.Net(Global).IsConserved(Memory(42)) == False`。
- **判词**：名字承诺的是数学性质（净效应跨 0），行为实现的是成员检查加报警语义。空签名在数学上 vacuously 守恒一切；这里它控告一切。调用者不读注释就无法预测——隐藏前置条件是把复杂度从代码搬进脑袋。
- **最小修复**：拆成 `HasOpenNet(r)`（成员+跨0）与调用侧「出现即须闭合」的策略判断，或至少改名为 `ClosesLifecycle`。

### F5 · MED — 「峰值」有三个口径：Peak.Compute 数 read/write，gate(2) 和 net 只数 occupy
- **位置**：Algebra.cs:117–122（`Peak.Compute` 遍历 `AllClaims()`，不过滤 Kind）vs Algebra.cs:59（net 仅 Occupy）vs EffectScript.cs:175（gate(2) 仅 OccupyClaims）。
- **证据（探针 D，实际运行）**：read[1,1] + occupy[5,5] 同 scope ⇒ `Peak.Compute == 6`；同签名的 `Net` 只有 1 个资源条目。
- **判词**：量纲隔离（DO-7）是这套代数的招牌，然后「占用峰值」这个量纲里悄悄混进了 read 的 size。三个函数、三种口径、零个名字上的区别。
- **最小修复**：`Peak.Compute` 加 `if (c.Kind != Kind.Occupy) continue;`，或把非占用口径显式命名为 `TouchedCount` 之类。

### F6 · MED — Mode.Unknown：契约层收进来，代数层抹掉，全程无痕
- **位置**：EffectScriptContract.cs:149（接受 `"unknown"`）→ Algebra.cs:15、22（`Resolve` 为私有 `Use` 别名）。
- **证据（探针 F，实际运行）**：`Compatible.IsCompatible(Mode.Unknown, Mode.Unknown) == True`；两个同刻存活的 unknown-mode 事件不产生任何 CompatibleConflict violation。审计结果里没有任何一条记录说明「这段剧本有未被审计的部分」。
- **判词**：fail-open 我可以接受——诚实的系统承认未知。但放行而不留痕是把「我不知道」伪造成「我查过了」。Unknown 是 spec 概念，不该在边界处被处决。
- **最小修复**：Audit 对含 Unknown mode 的事件追加一条 info 级 Violation（`UnauditedMode`），让 ⊤ 的诚实贯穿到产物。

### F7 · MED — 闭包守恒 gate 无视 scope 维度，「守恒」有两个聚合域
- **位置**：EffectScript.cs:289–296（closureNet 按归一化资源全局求和，无任何 scope 过滤）vs Algebra.cs:56–61（`NetTable.Compute` 严格执行 `⊆*` 过滤）。
- **证据**：代码路径直读——Scene A create、Scene B release 的剧本在脚本 Audit 里闭合成 [0,0]，经 `sig.Net(scopeA)` 则 release 不入表、报未闭合。同一概念、两个入口、两种答案。
- **判词**：作用域是这个代数的一等公民，直到最关键的闭合判定时它突然变成二等。跨 scope 抵消也许是对的策略，但它应该是一个决定，不是一个疏忽。
- **最小修复**：closure gate 按 `e.Scope` 分组判定（或至少在 Violation.Detail 中声明「全局聚合，忽略 scope」）。

### F8 · LOW — `Violation.Kind` 是裸字符串枚举
- **位置**：EffectScript.cs:377–381：`Leak | NegativeDip | PeakExceeded | CompatibleConflict` 靠注释约定。
- **判词**：整套系统的卖点是「类型即边界」，给 AI 回修循环的关键接口却是一个 string 字段。typo 一次，下游 switch 静默落入 default。
- **最小修复**：`enum ViolationKind`。

### F9 · LOW — 逃逸通道的一半边界是愿望而非类型
- **位置**：EffectAttributes.cs:35–37（`OverrideSize` 为 `double?`，「负值非法……本层不重复校验」——构造时可传入 `-5.0`）；EffectAttributes.cs:56（ε<0.2 「无意义」仅注释，构造成功且静默无效）。
- **判词**：`reason` 非空和 ε∈[0,0.5] 你们都用构造子强制了，偏偏最容易被 AI 写错的数值参数留给「调用方须保证」。逃逸通道恰恰是最需要 guardrail 的地方。
- **最小修复**：构造子校验 `OverrideSize >= 1`；ε<0.2 发 warning 或直接拒收。

### F10 · LOW — 预算有两个真源：`script.Budget` 与 `Audit(cap)` 参数互不知情
- **位置**：EffectScript.cs:99–103（构造时固定 Budget）与 EffectScript.cs:105–107（`Audit(Budget cap)` 可传任意 cap）；EffectScriptContract.cs 末尾 partial 的 `Audit()` 用自带预算。
- **判词**：构造时收下预算、审计时又允许换一个，两个入口各自合理，合起来就是一个「我到底审计的哪份合同」的问题。
- **最小修复**：删掉 `Audit(Budget)` 参数或让它必须等于 `Budget`（引用相等断言）。

---

## TOP-3

1. **F1 — 双答案问题**（HIGH）：`Audit` 说峰值 20、`At+Peak` 说 10，实测复现。框架内部对「并发」有两种互相矛盾的建模，使用者不可能在不读实现的情况下预测哪个生效。
2. **F2 — 守卫住错层**（HIGH）：`[⊤,⊤]` 假绿实测复现。JSON 有护栏、类型没有，对一个宣称「类型即边界」的系统这是自我否定。
3. **F3 — fail-open 的缺省面**（HIGH）：白名单外静默无保护。安全性质的缺省状态必须是「报」，不能是「装作无事」。

---

## usability 裁决

L1 代数核本身是 **simple** 的：值类型、全函数、⊤ 闭包、量纲分桶，这些做得认真且有测试背书（391 绿）。但边界层系统性地用 **easy** 换走了 simple：守卫放在解析器不在类型（F2/V4）、同一概念三套口径（F1/F5/F7）、未知被擦除不留痕（F6）、谓词名不符实（F4）。

对目标消费者（AI 编码代理 / 第一次使用的游戏开发者）的实测结论：**第一次用错的代价是错误结果而非显式失败**——探针 A 得到假绿、探针 B' 得到两个互相矛盾的答案。以「AI 产剧本 → 自动审计 → 回修闭环」这一核心承诺衡量，当前 API 面**尚不可安全使用**；修复 TOP-3 均为小改动（各约 1–10 行），修复后本裁决可翻案。

---

## 证据：读取过的文件

- README.md、EFFECT_SCRIPT.md、DELIVERABLE.md
- src/Cosmos.EffectAlgebra/: Algebra.cs、ApiMapping.cs、Objects.cs、Numeric.cs、SignedNet.cs、DerivedMetrics.cs、Deviation.cs、EffectAttributes.cs、EffectScript.cs、EffectScriptContract.cs、Cosmos.EffectAlgebra.csproj
- samples/GodotIntegration/SampleGame.cs、samples/AnalyzerConsumer/Game.cs
- （未读取 audit/ 下任何历史审计报告，遵守本轮隔离纪律）

探针工程：`D:/Godot/hickey-x3-probe/`（仓库外，6 个误用场景全部实际运行）。命令：`dotnet run`（探针）、`dotnet build Cosmos.EffectAlgebra.slnx`（0 错误）、`dotnet test Cosmos.EffectAlgebra.slnx`（391 通过 / 0 失败）。仓库源文件零改动（`git status` 中的 EFFECT_SCRIPT.md 修改与 audit/* 删除为本轮开始前的既有工作区状态）。
