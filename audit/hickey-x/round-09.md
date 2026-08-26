# Hickey-X R9: 终局对抗复核 —— 全 HIGH 清单终审、伪问题清扫、修复排序

> 视角：修复轮开工前的最后一道过滤。一切以当前磁盘为准（工作树在本系列期间持续演进，已三次修正旧结论）。
> 来源说明：复核由 hickey-auditor 完成（超时后从会话恢复），主会话补钉行号并实测测试基线。

---

## 一、工作树演进速报（本系列审计期间的修复）

| 项 | 状态 |
| --- | --- |
| [⊤,⊤] lifetime JSON 路径 | **已修**：Contract:71-73 fail-fast 拒绝 lo=⊤（注释引 R10-F2「假绿」理由）。**但 C# 构造路径仍可造出 Lo=⊤ 的 Event 且 sweep 静默跳过**——半修 |
| Join 名实分离（R5-C1） | **已修**：Objects.cs:193-209 真 merge_I（按 Kind/Resource/Mode/Scope 配对取 Interval.Merge） |
| Sequence/Parallel 名异实同（R1 LOW-3/R8） | **部分修**：Parallel 现有 PARA_CONFLICT 跨分支冲突守卫（DerivedMetrics.cs:52-62）；Sequence 仍=Union（:50） |
| PluginRuntime 重复 FiberId | **已修**：Register 前抛（DependencyGraph.Register 本体仍 `_fibers[f.Id]=f` 静默覆盖，经公共 Graph 属性仍可达） |
| AddDependency 同 Scope 校验 | **已加**（PluginRuntime 演进）；AddHardEdge 本体的 Requires⊇Providers 校验仍未做 |

## 二、HIGH 发现终审表

| 发现 | 终审裁决 | 当前证据 |
| --- | --- | --- |
| N1 SerializeBudget ⊤→0 | **P0 成立** | SerializeBudget 仍 `kv.Value.Value` 无 IsTop 分支；ParseBudget 仍无 ⊤ 表达。R7 实验完整伤害链（虚假 Leak）未变 |
| E1 静默数据降级（根级未知键/大小写/类型错） | **P0 成立** | Parse 仍只认 events/budget；d1/d2/d3 三路径 R7 实测全静默 |
| §4 文档示例不可解析（嵌套 gpu 对象） | **P0 成立** | EFFECT_SCRIPT.md:139 `{"gpu":{"bufferId":"mesh1"}}` vs Contract:161 ReqStr 要 string——官方示例必炸且报错不指路 |
| HIGH-3 零预算=恒真审计 | **P1 成立** | gate(2) 仍只遍历 cap.Caps；AuditResult 仍无覆盖面维度；R7-d3 实测 budget 类型错即整体失效 |
| V1 Budget 别名泄漏 | **P1 成立** | Budget 构造函数仍直接持有调用方字典引用；None 单例可变底座未变 |
| V2 Violation 键未归一 | **P1 成立** | gate(2) 查找用 nk、上报用 kv.Key——一行修复 |
| C2 At/Audit 双口径 | **P1 成立** | At 仍 set 去重 vs sweep multiset 计数；R7-f 实测 1 vs 2 |
| N2 Runtime 图不变量 | **P2 降级** | PluginRuntime 层已补同 Scope 校验+重复 Id 抛；残余为 DependencyGraph 本体（经 Graph 公共属性可达）与 AddHardEdge 语义校验缺失——攻击面收窄 |
| N3 default 后门 | **P1 成立（收窄）** | default(Budget).Caps 已被 EffectScript 构造器归一（R10-F1 注释）；default(Claim)/default(LoopCount)→除零链仍在（sweep `ulong.MaxValue / w.Value`） |
| E2 裸 catch 丢根因 | **P1 成立** | InverseReplay.cs:34 未变 |
| E5 CompatibleConflict 无事件索引 | **P2**（数据在手不上报，修复便宜但伤害中低） |
| HIGH-1 上手第一公里 / HIGH-2 双轨方言 | **P1/P2**（samples 缺剧本样例未变；budget 键方言未变） |

### 误报清扫（本轮点名降级）

- **R6-E4 数字 scene 静默强转**：`JsonElement.GetString()` 对 Number 抛 InvalidOperationException 而非返回原始文本——第三子句夸大；成立的是「缺失→Scene("") 静默」与「异常类型不一致」两个弱化版。
- **R5-C5 TickWatchdog 二次入队**：R6 已自纠（状态机不变式使窗口不可达），维持降级。
- **R2-N4 Merge 声音性**：维持 R3 降级（测试钉住 + 生产零消费）。

## 三、修复排序

### P0（立即修，每条 ≤10 行 + 1 个回归测试）

1. **N1**：SerializeBudget 加 IsTop→`"⊤"` 分支；ParseBudget 接受 `"⊤"`/`"inf"` ⇒ NatStar.Top。测试：C# ⊤ 预算 round-trip 后 Audit 不再产生虚假 Leak（复用 R7-b 场景）。
2. **E1 根级白名单**：Parse 对根对象枚举已知键，未知抛 FormatException 列合法键。测试：budgat/Budget/budget:[] 三输入均抛。
3. **§4 示例修正**：EFFECT_SCRIPT.md:139 改 `"resource": {"gpu":"mesh1"}`（一行文档修复）。测试：文档示例 JSON 可 Parse（把示例固化为契约测试）。

### P1（本迭代）

4. V2：gate(2) Violation 改传 nk。测试：budget 键写别名时 Violation.Resource 为归一形态。
2. V1：Budget 构造函数 `caps.ToImmutableDictionary()`。测试：外部改原字典后 Audit 结果不变。
3. E2：PartialReleaseDiagnosis 增加 FirstException 字段，PluginRuntime 伪造消息保留 inner ex。测试：模拟 inv.Execute 抛错断言 CrashReport 含根因消息。
4. C2：EffectScript.Audit 头注释明示 multiset 口径、At 明示 set 口径（文档级）；或提供 AtMulti。最小：文档。
5. N3 收尾：LoopCount 归一入口拒 Count.Value==0（防 default 链除零）。测试：default(LoopCount) 事件进 Audit 得 FormatException 而非 DivideByZeroException。

### P2（记录，暂不修）

- N2 残余（DependencyGraph 本体校验）、V5 公共可变三角、E5 事件索引、C3 重编码不变性文档化、C6 中英混排、CompareToFinite 改名（breaking）、Sequence=Union 文档声明。

## 四、测试基线（修复轮起点）

`dotnet test` 连跑 5 次：**Runtime.Tests 95 通过恒定；Tests 311 通过恒定；SampleGame 73 个在 0~10 失败间抖动，末次全绿（95+311+73=479 全过）**。抖动集中在 Analyzer 集成测试（P0_*/Override_* 家族）——疑似并行执行下的分析器状态污染，非确定性失败。修复轮以「三项目全绿」为目标；建议顺手排查 SampleGame 测试隔离问题（[Collection] 或禁并行）。

## 五、文件清单

- 主会话实测：dotnet test ×5（基线数据）、Contract:68-74、DerivedMetrics.cs:49-62、Objects.cs:193-209
- 子代理通读（当前磁盘）：EffectScriptContract.cs、EffectScript.cs、Objects.cs、Numeric.cs、Algebra.cs、DerivedMetrics.cs、SignedNet.cs、ApiMapping.cs、DependencyGraph.cs、Fiber.cs、PluginRuntime.cs、InverseReplay.cs、LoadValidation.cs、round-01..08 全部
