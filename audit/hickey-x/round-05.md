# Hickey-X R5: 组合性 —— A⊕B 的性质能从 A、B 推导出来吗？

> 视角：好的抽象可自由组合而不产生意外。组合子的代数律是否真实成立？组合的结论跨层一致吗？
> 来源说明：分析由 hickey-auditor 完成（输出捕获失败后从会话记录恢复），**载荷行号已由主会话逐一独立复核**。

---

## 一、组合子语义核验

Union/Join/Sequence/Parallel 四名一实已确证（Objects.cs:183 Union 实体；:193 Join=Union；DerivedMetrics.cs:50/:53 Sequence=Parallel=Union）——四者作为集合运算满足交换/结合/幂等 ✓。但本轮挖出的不是名字问题，而是**语义承诺与交付不符**：

### C1 [HIGH] `Signature.Join` 承诺「同 Claim 取 size merge_I」，交付的是裸 set-union——文档在撒谎

- **位置**：Objects.cs:192 注释「join-semilattice 合并……同 Claim 取 size merge_I」vs :193 实现 `=> Union(a, b)`。
- **判词**：merge_I 只对**结构完全相等**的 Claim 生效（ImmutableHashSet.Add 去重），两个 size 不同的同资源 Claim（如 create r[1,1] 与 create r[2,2]）不会被合并成 [1,2]，而是两条并存，下游 net/peak 各自**求和**得 [3,3]。承诺的 join（取 lub）实际是 sum——`net(Join(A,B)) = net(A)+net(B)` 而非 `merge(net(A),net(B))`。「半格」的名字许诺了 max 语义，代码交付 sum 语义。
- **加重项**：`SignedInterval.Merge`（SignedNet.cs:79，真 min/max join）**生产零消费**——grep 全仓只有测试与 string.Join 假阳性。真正的 join 运算存在但没人用；被到处用的 Union 却顶着「半格并」的名号。名实分离的最糟形态：正确的实现闲置，错误的解释上岗。
- **修复**：要么 Join 真做 size merge（对 kind/res/mode/scope 相同、size 不同的 Claim 调 Interval.Merge），要么注释如实降级为「结构去重并集，size 不合并」。二选一，别让注释替代码许愿。

### C2 [HIGH] At(t) 与 Audit 对同一剧本给出不同答案：set 口径 vs multiset 口径

- **位置**：EffectScript.cs:81-90 `At` 经 Signature.Union 结构去重（两事件相同 footprint ⇒ 快照只显示 1 份）；:177-181 sweep 的 grp 按 `(r, e.Scope, mode, ei)` 计数、peakSum 按事件累加（两事件 ⇒ 峰值 2）。
- **判词**：「屏幕上到底有几个并发副本」这个事实，公开查询 API 说 1，内部审计说 2。EffectScript.cs:103 的等价性声明（「三道 gate 与逐点全算版本逐条一致」）只在 sweep↔假想的 multiset-oracle 之间成立，**不覆盖 At 本身**。消费者按 API 邀请用 At 自查会得到系统性漏报的 false-pass。
- **最小复现**：两事件 lifetime 重叠、footprint 同为 occupy r[1,1] mode=create ω=1。`script.At(t).Footprint.OccupyClaims` 计 1 条；`Audit()` 报 CompatibleConflict + 峰值 2。
- **修复**：At 文档明示「快照为去重视图，冲突/峰值以 Audit 为准」，或给 At 增加 multiset 变体（如 `AtMulti`）。口径必须二选一地写进契约。

### C3 [MED] 冲突判定不满足重编码不变性：同一物理事实，编码方式决定判决

- **位置**：gate(3) 分组键含事件索引（EffectScript.cs:177-181）；单事件内 ω 副本自配对豁免（Loop 语义）。
- **判词**：「两个并发 creator」这一事实有三种编码：①两个重复事件 → Audit 报冲突；②单事件 ω=2 → 豁免不报；③Signature 层 Union → 结构去重直接消失。同一现实，三种判决——组合性质不满足「语义等价的编码得到等价结论」。① vs ② 有文档背书（自配对豁免），但 ② vs ③ 连文档都没对齐。
- **修复**：文档显式写明「multiplicity 的唯一合法通道是 Loop；重复事件表达的是逻辑上不同的元素」——把约定升格为契约，而不是让用户撞墙后自己悟。

## 二、守恒/兼容的闭包核验（正面记录 + 一条边界）

- IsConserved 在 Union 下**基本封闭**：零含区间的和仍零含（lo1≤0≤hi1 ∧ lo2≤0≤hi2 ⇒ lo1+lo2≤0≤hi1+hi2）✓；B 引入未抵消 create 时联合不守恒是正确语义（B 泄漏）✓。
- 兼容关系非传递但无传递性承诺，不构成缺陷 ✓。
- **C4 [MED] `Claim.CompatibleWith(Claim)` 是公共 API 层的谎言**（Objects.cs:137）：签名收两个 Claim，实际只比 Mode——`create(gpu:x).CompatibleWith(create(gpu:y))` 返回 false（create×create 冲突），哪怕两者是完全不相干的缓冲区。方法名承诺 claim 级相容，交付 mode 级相容。当前仓内无生产消费者（仅测试），属于「埋着的 API 雷」而非现行 bug。修复：改名 `ModeCompatibleWith` 或补 resource 相等前置。

## 三、Runtime 组合核验

- Σnet ≡ 各 Fiber 声明之和：设计上不做跨 Fiber 聚合（R4-7 Scope 仅分组），闭合闸门 per-Fiber 验自身 Provides ✓ 声明驱动的模型自洽。
- 拓扑序 leaf-first teardown 正确（Kahn provider-first 后反转）✓；FindCycle 栈输出环路径首尾重复节点（a→b→a 中 a 出现两次），仅诊断观感问题 LOW。
- **C5 [MED] TickWatchdog 缺二次入队守卫——可产生虚假 CrashReport**（PluginRuntime.cs:218-221）：状态守卫只排除 Dead/TearingDown，Suspending+TeardownEnqueued 已置位的窗口内（provider 先行 BeginTeardown 级联通知 dependent Suspending、watchdog 同帧触发）`:221` 无条件 `_teardownQueue.Add(...)` 再次入队。对照 BeginTeardown:109 有 `if (TeardownEnqueued) return;` 守卫。后果：DrainTeardownBatch 第二次执行 ReplayAndDead 时 state 检查先抛 InvalidOperationException → 被 catch → 写入虚假 CrashReport（声称部分释放失败，实际首次回放已成功）→ 污染宿主轮询的 LastCrashReport 语义、依赖者被再次通知。修复：`:221` 包一层 `if (!f.TeardownEnqueued)`。

## 四、跨层口径差异总表（R2-N5 的完整化）

| # | 概念 | L1 | 剧本层 sweep | Runtime |
| --- | --- | --- | --- | --- |
| 1 | net scope 过滤 | NetTable.Compute 按 c.Scope ⊆* 过滤（Algebra.cs:60） | **无过滤**，整桶 OccupyClaims 直接累加 | Compute(fiber.Scope)，inv.Scope 已验证等于 fiber.Scope |
| 2 | net 的 ω 缩放 | 无（调用方预缩放） | enter 时 ×ω，ω=⊤ 整事件豁免 | EffectiveSignature 折入逆 release，无 ω |
| 3 | peak 口径 | size.Hi 求和、c.Scope 过滤、无 ω | ×ω、e.Scope 归组、cap 键归一、Top 计数 | 复用 L1 |
| 4 | 冲突分组 | Compatible 两两对称（mode-only） | (res, e.Scope, mode) 同组才判 | 不涉及 |
| 5 | scope 可表达空间 | ScopeId 8 子类全开放 | SerializeScope 仅 4 子类，Shell/Loop/Conditional/Async ToJson 即抛（Contract:199-206） | 全开放 |

五条差异里没有一条在单一文档里对齐过。R2-N5 只发现了第 3 行；第 1 行（sweep 不过滤 scope）意味着「剧本审计的 net 从来不是 L1 定义的 net」——用户拿 Footprint.Net(scope) 手推的结果可以合法地与 Audit 结论相反。

## 五、采样完备性专项（Q3）

端点采样定理本身成立：Alive 的跳变点 ⊆ {有限 Lo, 有限 Hi} ∪ {maxFinite+1}，全部被采样；exit 在 tv 先审计后执行保证 Hi 端点含入 ✓。发现的缺口均已被前轮覆盖（N6 回绕、[⊤,⊤] 静默消失）。新确认一条保守方向的怪癖：

- peakSum[r] 一旦因溢出变 Top 就**永不恢复**（exit 减法遇 Top 保持 Top）——单次溢出污染整个剩余时间线，每个采样点都报 PeakExceeded。方向保守（fail-closed）不损 soundness，属误报放大器。LOW-MED。

## TOP-3

1. **C1 Join 名实分离 + 真 join 零消费**：文档承诺 merge_I、交付 set-union；唯一正确的 join 实现无人问津。这是「API 面积是最昂贵的承诺」的反面教材——最贵的不是面积，是撒谎的面积。
2. **C2 At/Audit 双口径**：同一剧本两个官方视图给出不同的并发计数，等价性声明的适用范围被注释夸大。
3. **C5 TickWatchdog 二次入队**：虚假 CrashReport 直击 Runtime 权威闭合层的可信度。

## 文件清单

- 主会话复核行号：Objects.cs（:137/:182-193）、DerivedMetrics.cs（:49-53）、EffectScript.cs（:76-90/:177-183/:189-191）、SignedNet.cs（:75-79）、PluginRuntime.cs（:106-118/:214-224）、EffectScriptContract.cs（:199-206）
- 子代理通读：上述全部 + Algebra.cs、Numeric.cs、ApiMapping.cs、Deviation.cs、DependencyGraph.cs、Fiber.cs、InverseReplay.cs、LoadValidation.cs、GodotShell.cs、IHost.cs、TickWatchdog 相关段
- 测试抽查：组合律相关测试文件（Union/Join 幂等性断言）
