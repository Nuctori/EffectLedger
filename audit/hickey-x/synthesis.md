# Hickey-X 十轮 Rich Hickey 视角对抗性审计 · 收敛综合

> 系列：`audit/hickey-x/round-01..10.md`（本会话串行产出，ox 模型）。
> 方法：10 轮串行对抗——R1 上手面 / R2 类型正确性 / R3 对抗复核 / R4 值语义 / R5 组合性 / R6 错误信息 / R7 可执行实验验证 / R8 命名与概念完整性 / R9 终局复核+修复排序 / R10 终审裁决。
> 纪律：一切以当前磁盘为准；载荷行号由主会话独立钉死；R7 用真实运行背书静态结论。

---

## 一、总判（R10 裁决）

| 层 | 等级 | 一句话 |
| --- | --- | --- |
| L1 数学内核 | **良** | 值语义双轨制画线干净、溢出律统一、结构相等是真值语义；扣分在边缘失约（ZStar 裸算术、Sequence 名异实同） |
| 契约面（JSON/剧本层） | **中偏差** | 用户真正生活的地方最纠缠：双轨方言、⊤ 无拼法且被写成 0、15 资源只 5 个过桥、旗舰示例必炸、fail-soft 后门开在高频路径 |
| Runtime 壳 | **良⁻** | 审计期间被实质加固（重复 Id 抛/同 Scope 校验/PARA_CONFLICT）；残余图本体静默覆盖、裸 catch 丢根因 |

**用户困难判定：会。** AI 写剧本用户第一分钟就会（照抄官方示例必炸；拼写错误静默禁用预算门；零预算恒真绿灯）；C# 集成用户在跨边界时刻会（⊤ 预算翻转成虚假 Leak、At/Audit 双口径、Budget 别名泄漏）；Runtime 宿主最少但最深（CrashReport 无根因）。核心判词：**「不会写不出程序，但会在不知情时拿到错误的审计结论」——对验证工具这是最重刑。**

## 二、十大问题榜（R10 收敛，按用户伤害排序）

| # | 发现 | P | 最小修复 |
| --- | --- | --- | --- |
| 1 | 根级未知键/大小写/budget 类型错三路静默禁用整个预算门（E1+E3，R7-d 实测） | **P0** | 根级白名单递归辅助函数 ~20 行 |
| 2 | SerializeBudget ⊤→0 round-trip 制造虚假 Leak（N1，R7-b 全链实测） | **P0** | IsTop→"⊤" 分支 + Parse 接受 ⊤/inf |
| 3 | 官方 §4 示例无法通过自家 Parse（嵌套 gpu vs 要 string） | **P0** | 文档一行修正 + 示例固化为契约测试 |
| 4 | 零预算=恒真审计，「查过通过」与「没查」不可区分（HIGH-3） | **P0**（R10 从 P1 升格） | AuditResult 加 CapsChecked 计数 |
| 5 | 剧本 DSL 零上手路径（samples 无样例） | P1 | samples/EffectScript 两文件 |
| 6 | At 说 1 个并发副本、Audit 按 2 个计（C2，R7-f 实测） | P1 | 口径文档化或 AtMulti |
| 7 | default(LoopCount) 后门→除零链（N3 合流） | P1 | 归一入口拒 Count==0 |
| 8 | Budget 构造持有调用方可变字典（V1） | P1 | ToImmutableDictionary 一行 |
| 9 | InverseReplay 裸 catch 丢根因（E2） | P1 | Diagnosis 加 FirstException |
| 10 | 契约面≠API 面：10/15 资源、4/8 作用域 ToJson 即抛 | P1 | 扩契约或构造期拒绝 |

遗珠并入：V2 违例键未归一（一行）、HIGH-2 双轨方言（与 #1 同族根治）。

## 三、修复路线图（R9 排序 + R10 终裁）

**第一枪 E1 家族**（白名单+类型严判）→ **第二枪 N1**（对称 ⊤ 支持）→ **第三枪 §4 示例 + HIGH-3 CapsChecked**。方法论约束：每个修复 PR 附「发现编号 + 当前行号」，防行号漂移再造误报。

### 工作树已在审计期间自行演进的项（无需重修）

- Join 真 merge_I 化（Objects.cs:193-209）
- [⊤,⊤] lifetime JSON fail-fast（Contract:71-73）——C# 构造路径仍开放
- Parallel PARA_CONFLICT 守卫（DerivedMetrics.cs:52-63）
- PluginRuntime 重复 Id 抛 + AddDependency 同 Scope 校验

### 测试基线

479 用例（Runtime 95 + Tests 311 + SampleGame 73），末次全绿；SampleGame 分析器集成测试存在非确定性抖动（并行状态污染嫌疑），修复轮应先隔离（[Collection] 或禁并行）再动刀。

## 四、正面清单 TOP-5

1. 值语义双轨制画线干净（数学是值，生命是对象）
2. 错误消息尖子生够格当教材（LoopCount.Of(0) 讲后果；LoadValidation 带 Fiber.Id+§ 引用）
3. 审计期间代码真的在动——对抗审计与开发形成闭环
4. 溢出纪律有统一的法（溢出⇒⊤），端点采样定理经推演成立
5. 最小剧本 round-trip 幂等实测成立——正门是对称的

## 五、方法论附言

十轮对抗设计有效，证据是纠错率而非感觉：

- 自纠 ≥3 起：E4 数字 scene 强转证伪、R5-C5 watchdog 窗口不可达推翻、资源计数 14→15 修正
- 识破外部陷阱 1 起：并行会话系列基于过期快照，其「仍在」判定若被采信将成片误报（R3 时间线仲裁）
- R7 可执行探针把两个 HIGH 升格为可复现事实——全系列可信度转折点
- 成本同样真实：工作树持续演进致行号反复漂移，每轮被迫重读

## 六、产物清单

- `audit/hickey-x/round-01.md` … `round-10.md`：10 轮完整报告
- `*-source.md` / `round-02-source-thinking.md`：输出捕获失败轮次的子代理原始分析存档
- 本文件：收敛综合
- 修复轮入口：见第二节 P0 清单与第三节路线图
