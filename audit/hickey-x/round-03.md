# Hickey-X R3: 对抗复核轮 —— 抽验 R2、仲裁冲突、双系列交叉收敛

> 视角：审计审计者。裁决对象：本系列 R1/R2 + 并行会话系列（rich-hickey-round01..10）。
> 来源说明：分析由 hickey-auditor 子代理完成（输出捕获再次失败，从会话记录恢复，主会话代笔落盘）。

---

## 一、R2 载荷发现抽验表

| R2 发现 | 裁决 | 抽验证据 |
| --- | --- | --- |
| N1 SerializeBudget ⊤→0 | **成立** | EffectScriptContract.cs:231 `d[ResourceKey(kv.Key)] = kv.Value.Value`；NatStar.Top.Value==0（Numeric.cs:19）；Parse 侧 ：172 只有 GetUInt64 无 ⊤ 表达。最小复现：C# 构造 Budget{res:⊤} → ToJson → Parse → Audit ⇒ peak>0 即 PeakExceeded。注意 ToJson 仅在 Caps.Count>0 时写 budget（:47），翻转发生在 C#→JSON→Parse 全链。 |
| N2 AddHardEdge 不验不变式 / Register 静默覆盖 | **成立（带精化）** | DependencyGraph.cs:24 `_fibers[f.Id]=f` 静默覆盖；:27-28 仅 `_hard.Add`，注释承诺的「B.Requires ⊇ A.Provides」无任何检查点（LoadValidation 验 scale/release/net，唯独不验边语义）；Coeffect.Requires 全仓零消费（幽灵字段模式重演）。**精化**：DependencyGraphTests.cs:28-29 钉住了 AddHardEdge「返回是否新增」契约——修复须保留 bool 返回或连测试同改。Fiber internal set 属程序集内可达但当前无人滥用，降 LOW。 |
| N4 Merge([⊤,⊤],[1,1])=[1,⊤] 声音性 | **行为属实，降级 MED→LOW-MED** | 数学上确为 unsound（未知下界被替换为精确 1，声称了输入不蕴含的精度）。**但**：(a) 测试钉住该语义（IntervalArithmeticTests.cs `Merge_FullyUnknownNarrowsLowerBound` 断言精确结果并附格论理由）；(b) Merge 在生产代码零消费者（仅测试调用）。定性改为「格论语义争议 + 潜伏雷」：要么改律并同步测试，要么文档显式声明 [⊤,⊤] 的 meet 解释。 |
| N6 maxFinite+1 回绕 | **成立（LOW-MED）** | EffectScript.cs:123 裸 ulong 加法；复现：事件 A lifetime [ulong.MaxValue,ulong.MaxValue]，事件 B 开放尾段 ⇒ maxFinite+1=0 追加到采样表尾部，破坏有序性，产生「t=0 的伪末态样本」，违规时间戳错乱。触发极端但真实；非内存不安全，属可观测性污染。绕过 NatStar 溢出律的指控成立。 |
| N7 根级未知键静默忽略 | **成立** | Parse 只查 events（Contract:25 区域）与可选 budget（:33），其余根键无视。最小复现：合法 events + `"Budget"`（大写 B）+ gpu cap ⇒ Parse 成功、caps 空、Audit Passed=true。「拼写错误静默禁用整个预算门」与 HIGH-3 叠加后爆炸半径最大。 |

### 补充裁决：R2 HIGH-2 补强例的算术纠错

R2 称 claim `Self("signal_signal_x")` 与 budget 键 `signalBus:signal_x` 错位——**方向搞反了**：Self("signal_signal_x") 归一为 SignalBus("signal_x")，恰与示例键一致（匹配）。真实的踩雷方向是反过来的：claim `Self("signal_x")` → SignalBus("x")，而用户按原始信号名写 budget 键 `signalBus:signal_x` → SignalBus("signal_x") ≠ SignalBus("x") ⇒ 查找恒 miss ⇒ 预算静默失效。机制成立、示例需修正。

---

## 二、冲突仲裁

1. **资源子类计数（R1 说 14 / R2 说 15）**：权威数字 **15**。Objects.cs:23-39 实数：Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Occupancy/Callback/Network/Input/Custom（13，:23-35）+ CommandBuffer（:38）+ SignalBus（:39）。SerializeResource 覆盖 5 种（Contract:216-224），抛 10 种（:224）。ScopeId 8 子类中 SerializeScope 覆盖 4、抛 Shell/Loop/Conditional/Async（:199-205）。R2 正确，R1 基数漏计。
2. **行号漂移**：R1 引用的 Objects.cs:17-30 等已漂移到 :23-39——两轮之间工作树被修过（见下）。

---

## 三、重大时间线发现（本轮最高价值 meta-finding）

**并行会话系列（rich-hickey-round01..10，17:23–18:24 产出）审计的是 HEAD 快照（d3de1ca）；当前工作树在其之后收到大量未提交修复（src mtime 18:53–19:21+）。**

证据：

- 并行 round10 综合的核心根因 A：「序列化兜底臂静默吞（仍在：EffectScriptContract.cs:199,218,236）」——当前磁盘这三处全是 `_ => throw new FormatException(...)`（实际行号 ：205/:224/:242），其 Top#1 修复建议描述的正是**已存在**的代码。
- 并行 B：「EFFECT_SCRIPT.md:144 仍写 loop":0"」——当前文档 ：137 是 `"loop": 1`，且 LoopCount.Of(0) 在 DerivedMetrics 抛。
- 结论：并行系列的多数「仍在」判定相对工作树**已过期**；直接采信其综合会产生成片误报。消费并行系列必须逐条对当前磁盘复核。

## 双系列交叉收敛清单（两边独立命中的问题 = 最高置信）

| 收敛问题 | 本系列出处 | 并行系列出处 | 当前状态 |
| --- | --- | --- | --- |
| 四名一实 Union/Join/Sequence/Parallel | R1 LOW-3 | parallel round01 | 仍在（DerivedMetrics 名异实同） |
| scope 分裂 / claim.scope 幽灵字段 | R1 C1 + R2 加强 | parallel round01/03 | 部分（gate3 已对齐 e.Scope；Net/Peak 仍 claim scope；剧本层 sweep 干脆无过滤） |
| read/write 桶进 Signature 无消费者 | R1 C3 | parallel | 仍在（唯一隐性消费者是 Peak.Compute 的可疑口径，见 R2-N5） |
| JSON 契约 fail-soft 家族 | R1 C4/R2 N7/N9 | parallel round04 | 大部分已修；残余：⊤ 别名缺失、根级未知键、budget 值裸 GetUInt64 |
| default(T) 后门 | R2 N3 | （未直接命中，但其 loop:0 分析触及同一通道） | 仍在且新证：default(LoopCount).Count==0 可达 sweep 除零（EffectScript.cs:193 `ulong.MaxValue / w.Value`），绕过 Of(0) 守卫——跨系列合流的完整攻击链 |
| 上手第一公里缺失 | R1 HIGH-1 | parallel round09 approachability | 未复核（两系列视角一致，均判缺失） |

## 新角度扫描（前两轮未碰）

- **N3-Runtime 链**：`new EffectEvent(..., default(LoopCount))` 编译通过，ω=0 直达 `ulong.MaxValue / 0` DivideByZeroException——default 后门从「可表示非法状态」升级为「运行时崩溃链」。修复优先级随之上调。
- 并行系列的独有贡献（经抽验仍成立的）：O10 ApiMapping 白名单哨兵合并资源账目；Violation 归因硬编码 Global 的历史问题（当前已是 ResolveNetScope/ResolvePeakScope 局部函数推断，部分修复）。
- 未及扫描（留待后续轮）：并发线程安全、性能特征、测试自身质量。

---

## TOP-3（本轮）

1. **时间线仲裁**：并行系列综合基于过期快照，其收敛结论不可直接引用——这改变整个审计程序的消费方式。
2. **N1 SerializeBudget ⊤→0**（二次确认成立）：序列化器主动制造语义翻转，仍是全部发现中最锋利的一个。
3. **default(LoopCount) 除零链**：R2-N3 与并行-B 合流后的完整崩溃链，把「类型后门」从中危提到高危。

## 文件清单

- 抽验读取：EffectScriptContract.cs、Numeric.cs、Objects.cs、Algebra.cs、EffectScript.cs、DerivedMetrics.cs、DependencyGraph.cs、Fiber.cs、PluginRuntime.cs、LoadValidation.cs、DependencyGraphTests.cs、IntervalArithmeticTests.cs、EFFECT_SCRIPT.md
- 对抗输入：round-01.md、round-02.md、rich-hickey-round01..10（10 份）
- 工具核验：git status/diff（时间线）、grep/awk（死代码、重复行）
