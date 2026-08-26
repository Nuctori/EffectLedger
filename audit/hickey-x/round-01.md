# Rich Hickey 对抗性审计 · 第 1 轮 —— Simple vs Easy：从 README 到第一个正确剧本

> 视角：新用户第一印象。判据：simple = 不纠缠、一个职责、值语义；easy = 顺手但埋雷。API 面积是最昂贵的承诺。
> 本轮为 10 轮串行第 1 轮。历史基线：`audit/effect-api-audit-synthesis.md`（C1–C6 根因）与 `audit/rich-hickey-api-audit.md`。所有结论均以本轮实读行号为准。

---

## 核实矩阵（C1–C6 逐条裁决）

| # | 历史结论 | 裁决 | 行号证据 |
| --- | --- | --- | --- |
| C1 | **scope 分裂**：event scope vs claim scope，两条计算路径口径不一 | **部分修** | ✅ gate3 冲突分组已改按 `e.Scope`（`EffectScript.cs:177` `var key = (r, e.Scope, (int)c.Mode)`，注释自证修 auditR3b TC7）；`At(t)` 经 `Combination.Loop` 把每个 claim 的 scope 重写到 event scope（`DerivedMetrics.cs:41-45` `c with { Scope = loopScope … }`）。❌ 但 `NetTable.Compute` / `Peak.Compute` 仍按 **claim 自带 scope** 过滤（`Algebra.cs:60`、`Algebra.cs:119` `if (!c.Scope.IncludedIn(scope)) continue;`）——同一个 Signature，`At/Audit` 一个口径、`script.Footprint.Net(scope)` 另一个口径，分裂仍在。❌ 且契约仍**强制** claim.scope 必填（`EffectScriptContract.cs:132` `ParseScope(Require(c, "scope"))`），而该值被剧本层全部计算忽略——幽灵字段未除。 |
| C2 | **资源类型爆炸 + 静默兜底**（Memory(0)、`commandBuffer→"gpu"`、类型错→0） | **大部分已修** | ✅ resource 对象路径 fail-fast：五键之外抛 `FormatException` 并列全五个合法键（`EffectScriptContract.cs:146-147`）；值类型错/空串抛（`:160` memory 非数字即抛、`:254` `ReqStr` 拒空串），不再静默改写。❌ 残余 1：`Memory(0)` 哨兵仍在 **budget 键**路径——`"memory:"` 后跟空串静默得 `Memory(0)`（`EffectScriptContract.cs:181` `.Length > 0 ? ulong.Parse(...) : 0`），与对象路径的自家标准相矛盾。❌ 残余 2：14 个 ResourceId 子类（`Objects.cs:17-30`）契约只暴露 5 个（`EffectScriptContract.cs:146`），其余 9 个在 C# 里合法、在 JSON 里不可表达、`ToJson` 直接抛（`:224`）——见新发现 MED-2。 |
| C3 | **Mode×Kind 组合爆炸**：15 组合约 12 种无意义却被接受 | **仍未修** | `Mode` 5 值（`Objects.cs:116`）×`Kind` 3 值（`Objects.cs:111`）依旧自由组合，构造期无任何约束。更实质的证据：read/write 两桶进了 Signature 却**没有任何剧本层消费者**——net/gate(1)/gate(2)/gate(3)/闭包全部只遍历 `Footprint.OccupyClaims`（`EffectScript.cs:160,172,286,304`；`Algebra.cs:57-58` `if (c.Kind != Kind.Occupy) continue;`）。用户写 `"kind":"write", "mode":"create"` 得到的全部反馈是沉默。`move` 在 net 上仍硬编码等同 create（`EffectScript.cs:164-166` 三元只特判 `Mode.Release`），契约不拒、文档不提。 |
| C4 | **JSON 契约 fail-soft**：Global round-trip 必炸、值静默猜测、⊤ 无别名、缺 schema 文档 | **大部分已修（三项中两项半）** | ✅ Global round-trip 已修：序列化输出 `{"type":"global"}`（`EffectScriptContract.cs:98`），Parse 对 global 不再要求 scene（`:93-95`）。✅ 资源值静默改写已改 fail-fast（同 C2）。❌ 残余 1：⊤ 唯一字面量仍是 U+22B4 字符 `"⊤"`（`:79`、`:108`），无 `inf`/`*`/`top` 等 ASCII 别名——AI 产 JSON 时编码事故高发点。❌ 残余 2：`lifetime.lo` 仍接受 `"⊤"`（`:69` `ParseTop(items[0])` 与 hi 共用同一函数），产出永不存活的事件，见新发现 MED-1。❌ 残余 3：budget 值直接 `prop.Value.GetUInt64()`（`:172`），非数字抛的是 System.Text.Json 原生 `InvalidOperationException`/`FormatException`，无字段名、无自定义消息——与同文件其它错误的质检标准不一致。schema 文档仍缺（EFFECT_SCRIPT.md §4 一个例子顶全部文档）。 |
| C5 | **隐藏可变状态**：`EffectScript.Budget { get; init; }` | **已修** | `Budget` 为 `{ get; }` 只读属性（`EffectScript.cs:61`），仅构造参数注入，且构造器把 `default` 规范化为 `Budget.None`（`:64-67`）；便捷重载 `Audit()` 复用自带 Budget（`EffectScriptContract.cs:261`）。值语义恢复，同实例重复 `Audit()` 结果确定。此项历史结论兑现。 |
| C6 | **认知表面无导航**：无 Builder/Example、命名碰撞 | **部分修** | ✅ README 新增三步快速开始（`README.md:7-34`）+ 三条锐边直书（`:54-58`）——L3 分析器路径的第一公里补上了。❌ 但**剧本 DSL 路径零导航**：samples/ 下只有 `AnalyzerConsumer/Game.cs`（方法名配对演示）与 `GodotIntegration/AdvE2E_R*.cs`，没有任何 EffectScript/JSON 样例；唯一形状文档是 EFFECT_SCRIPT.md §4 内嵌 JSON。❌ 命名碰撞原样保留：`ScopeId.Loop`（循环作用域，`Objects.cs:96`）vs `LoopCount`（并发副本 ω，`DerivedMetrics.cs:11`）；kind `occupy`（`Objects.cs:111`）vs 资源 `Occupancy`（`Objects.cs:31`）。 |

---

## 新发现（C1–C6 之外）

### HIGH-1 · 剧本 DSL 没有「第一公里」——README 的 5 分钟承诺不覆盖项目自己的核心卖点
- **位置**：`README.md:7-34`（快速开始只有 L3 分析器路径）；`samples/` 目录（无任何 EffectScript 样例）；对照 `EFFECT_SCRIPT.md:§4`（唯一 JSON 形状文档）。
- **判词**：项目文档把「AI 写剧本、不跑游戏就审计」当作存在理由（`EFFECT_SCRIPT.md:§1`），可是新用户照着 README 走完三步，得到的是一个**和剧本毫无关系的泄漏检查器**；想写出第一个剧本只能去读 391 行 `EffectScript.cs` 和 262 行 `EffectScriptContract.cs` 反推 JSON 形状——这不是上手路径，这是考古。
- **最小修复**：`samples/EffectScript/` 放两个文件：`minimal.json`（10 行，含一个 Leak 反例）+ 3 行 `Program.cs`（`Parse→Audit→打印 Violations`）。README 分层表加一行链接。

### HIGH-2 · budget 键语法与事件内 resource 语法是两套方言，写错时报错不给活路
- **位置**：`EffectScriptContract.cs:146`（resource = 嵌套对象 `{"commandBuffer":"gpu"}`）vs `EffectScriptContract.cs:177-188`（budget 键 = 平面字符串 `"commandBuffer:gpu"`）；错误出口 `:189` `throw new FormatException($"未知 budget 键: {key}")`。
- **判词**：同一个资源在同一个 JSON 文件里要写两种形状——这是把一种概念纠缠成两套记法，教科书级的 accidental complexity；而写错时用户得到的全部信息是一个回显的键名，五个合法前缀、正确写法示例一概欠奉。EFFECT_SCRIPT.md §4 示例恰好用了 `"commandBuffer:gpu"`，但从未有一句话告诉读者「这里不是对象、前缀拼写区分大小写、`CommandBuffer:` 不合法」。
- **最小修复**：错误消息附上五个合法前缀和一个正例；中期把 budget 也统一成对象形态或至少在文档给键语法表。

### HIGH-3 · Budget 缺省 = 该资源根本不检查：「审计通过」在没有预算时是恒真命题
- **位置**：`EffectScript.cs:225-232` gate(2) `foreach (var kv in cap.Caps)`——只遍历**用户声明的**上限；`:24` 注释直言「资源不在 Caps ⇒ 不检查」；`EFFECT_SCRIPT.md:§2.3` 却说缺省 = ⊤，暗示存在一次「≤ ⊤」的比较。
- **判词**：L1 的哲学是 ⊤ ⇒ fail-closed 报警交人工（`Algebra.cs:96-103` IsConserved 对 ⊤ 返回 false），到了剧本层的预算门，⊤ 却变成**静默跳过**——同一个符号，两层相反的诚实度。新用户写完剧本、忘了写 budget、看到 `Passed=true`，自然以为显存峰值审过了；实际上 gate(2) 一条都没跑。「5 分钟上手」最危险的静默失败不在 README 里，在这里。
- **最小修复**：`AuditResult` 增加 `CapsChecked` 计数（或 0-cap 时附加 warning 类 Violation），让「什么都没查」成为可观测事实。

### MED-1 · `lifetime.lo = "⊤"` 被接受，产出永远不存在的元素，审计静默少查一个事件
- **位置**：`EffectScriptContract.cs:69`（lo 与 hi 共用 `ParseTop`，接受 `"⊤"`）；后果点 `EffectScript.cs:132`（扫换线 `if (lt.Lo.IsTop) continue;`——该事件永不出现在任何 gate）。
- **判词**：设计注记自己写过「`[⊤,⊤]` 视为非法输入而非错误项」（EFFECT_SCRIPT.md §2.1 边界注记 OPEN-B4），代码却选择了第三条路：既不拒绝也不审计，而是收下然后遗忘。非法状态不仅可表示，还表示得悄无声息。
- **最小修复**：`ParseInterval` 对 `items[0]` 单独校验有限性，报 `"lifetime.lo 必须为有限数字"`。

### MED-2 · C# 合法剧本可能无法序列化：契约面 ≠ API 面
- **位置**：`EffectScriptContract.cs:204-205`（`SerializeScope` 对 `Shell/Loop/Conditional/Async/Method 之外的…` 即八分之五的 ScopeId 抛 FormatException）、`:223-224`（`SerializeResource` 对 14 个资源子类中的 9 个抛）。
- **判词**：`new ScopeId.Shell()` 或 `ResourceId.Tree(...)` 在 C# 类型系统里完全合法，`ToJson` 却当场爆炸——「纯数据契约」承诺的是 AI 与 C# 两侧同构，实际只覆盖了一个真子集，且子集边界只藏在 switch 的 default 里。用户预测不了哪些值能过桥，就必须读实现——这正是 Hickey 判据里最贵的那种失败。
- **最小修复**：要么契约扩到全覆盖（scope 至少补 shell/loop/conditional/async 四个字符串标签），要么在 `EffectEvent`/`Claim` 构造期就拒绝不可序列化的 scope/resource——边界选一头，别悬在中间。

### MED-3 · claim.scope：必填、序列化往返、零消费——被制度化的幽灵字段
- **位置**：必填 `EffectScriptContract.cs:132`；序列化 `:213`（每个 claim 都带 scope 往返）；消费侧为零（`EffectScript.cs:160/172/286/304` 全部只用 `e.Scope`；`Algebra.cs:60/119` 的 claim scope 过滤在剧本路径上够不着）。
- **判词**：让用户每次为一个不被读取的字段负责，等于训练用户「必填≠有用」，下一批真正必填的字段也会被他们瞎填。C1 的病根不在哪条 gate 用了哪个 scope，而在**两个字段表达一个概念**这件事本身。
- **最小修复**：契约把 claim.scope 改为可选（缺省继承 event scope），文档标注「预留字段，当前恒被 event scope 覆盖」；或者干脆删。

### MED-4 · `mode:"unknown"` 可经契约写入，且 unknown×unknown 比 README 承诺的还要宽容
- **位置**：`EffectScriptContract.cs:125`（接受 `"unknown"`）；`Algebra.cs:15`（Resolve: Unknown→Use）+ `:22`（Use 与一切兼容）⇒ gate(3) 中 unknown 同组必然放行（`EffectScript.cs:247-249` `IsCompatible(mode,mode)` 经 Resolve 得 true）。
- **判词**：README 把「Unknown=fail-open」列为已知锐边（`README.md:55`），但那说的是「静态判定不了所以放行」；这里是用户**主动写下**「我不知道」，系统照样放行——自报无知还能免检，逃逸通道不该开在数据面上。
- **最小修复**：剧本契约拒绝 `"unknown"`（静态分析器的输入才需要它），报「请明确 use/create/release/move」。

### LOW-1 · ⊤ 是键盘打不出来的字面量
- **位置**：`EffectScriptContract.cs:79,108`（仅接受 U+22B4）。
- **修复**：加 `"inf"` / `"*"` / `"top"` 别名，序列化保持 ⊤。一行 switch 的事。

### LOW-2 · `memory:""` budget 键静默得 Memory(0)，自家对象路径同场景是 fail-fast
- **位置**：`EffectScriptContract.cs:181` vs `:160`。同一份代码两种诚实度。修复：空串抛，与 C2 已立的标准对齐。

### LOW-3 · `Sequence` / `Parallel` 名异实同，贩卖虚假心智模型
- **位置**：`DerivedMetrics.cs:50-53`（两者都 = `Signature.Union`）。
- **判词**：两个名字承诺两种时间结构，交付的却是同一个无序并集；名字越语义化，误导越深。旧审计建议过合并，因生成器引用暂缓——那至少该在 XML 注释第一行写「二者恒等，仅为兼容保留」，而不是让注释继续假装它们有区别。

### 正面记录（错误信息的对的部分）
`ReqStr` 报错带字段名与期望形状（`EffectScriptContract.cs:254`）；`ParseResource` 列全五个合法键（`:146`）；`LoopCount.Of(0)` 的报错连**为什么**都讲了（`DerivedMetrics.cs:18`「0 会使规模缩放为 [0,0] 致守恒误报」）；`Violation.Detail` 含时刻、数值 vs 上限（`EffectScript.cs:233-235,312-315`），确实可供 AI 回修。短板集中在：所有 FormatException 均无 JSON 路径/行号，多事件文档定位靠数花括号；budget 值解析（`:172`）是唯一漏网的裸 `GetUInt64()`。

---

## 四个关注点的直答

1. **「5 分钟」实际几步？** 三步成立，但仅限 L3 泄漏检查路径。三个静默失败点：(a) 步骤 2 忘复制 `.editorconfig` 片段 ⇒ 门禁退化为 warning，编译测试全绿（`README.md:16` 自己承认「否则门禁失效」）；(b) 步骤 3 API 未命中白名单 ⇒ 静默无保护（`README.md:40` 自认）；(c) 本轮新增：走完三步的用户离剧本 DSL 依然为零距离——真正的核心功能不在「5 分钟」里。
2. **最小合法剧本几个概念？** JSON 路径：events、lifetime、scope、footprint、claim{kind,resource,mode,**scope**,size}、loop、budget 键语法 ≈ **10 个概念**才够跑第一次 Audit；C# 路径最少 5 个类型（Interval/Signature.Of/Claim/EffectEvent/EffectScript）。更小的入口不存在——没有 Builder、没有 minimal example、没有一个能 `Parse("{}")` 都不炸的最小文档样例。
3. **写错时得到什么？** 形状类错误：FormatException 带字段名（合格，缺 JSON 定位）；budget 键错误：裸键名回显（不合格）；budget 值类型错：库的原生异常（不合格）。**语义类错误**（Leak/PeakExceeded/冲突）：Violations 质量好，含反例三元组，可自行恢复。总体呈「语法层半盲、语义层优秀」的倒挂——用户最容易卡住的地方恰恰是反馈最弱的地方。
4. **API 面积 vs 常用面？** L1 公共表面约 23 个类型 + EffectScript 子系统 6 个 + 特性 2 个；而 80% 的目标用户（AI 写剧本）只需要 3 个成员：`EffectScriptContract.Parse` / `script.Audit()` / `AuditResult.Violations`，外加 6 个 JSON 关键词。常用面不足公共面的 15%，README 首屏却把四层全部铺开——表面积本身没错，错在没有为这 15% 划一条直径入口。

---

## TOP-3（本轮最重要）

1. **HIGH-1 剧本 DSL 无第一公里**：README 的快速开始与项目的核心价值主张（AI 剧本审计）完全脱节，第一个正确剧本的最低成本路径是读实现源码。修一处 samples 目录即可消除最大的一块上手复杂度。
2. **HIGH-3 零预算 = 恒真审计**：gate(2) 只查用户声明的资源，「Passed=true」在没写 budget 时不含任何峰值信息，与 L1 的 fail-closed 哲学在同一产品内自相矛盾，且无任何可观测信号。
3. **HIGH-2 budget 键双轨方言**：同一资源同一文档两种写法 + 出错时不给合法形式——语法层反馈最弱处正好是语法最怪异处。

---

## 证据：本轮实读文件

- `README.md`
- `EFFECT_SCRIPT.md`
- `src/Cosmos.EffectAlgebra/Objects.cs`
- `src/Cosmos.EffectAlgebra/Numeric.cs`
- `src/Cosmos.EffectAlgebra/Algebra.cs`
- `src/Cosmos.EffectAlgebra/DerivedMetrics.cs`
- `src/Cosmos.EffectAlgebra/EffectScript.cs`
- `src/Cosmos.EffectAlgebra/EffectScriptContract.cs`
- `src/Cosmos.EffectAlgebra/EffectAttributes.cs`
- `src/Cosmos.EffectAlgebra.Analyzer/EffectAlgebraAnalyzer.cs`（诊断 ID 清单，grep 核对 EAA0901/0303/0304/0801/0802）
- `samples/AnalyzerConsumer/Game.cs`（确认 samples 现状：无剧本样例）
- `audit/effect-api-audit-synthesis.md`（历史基线 C1-C6）
- `audit/rich-hickey-api-audit.md`（历史基线）

*只读审计：未修改任何项目源文件。*
