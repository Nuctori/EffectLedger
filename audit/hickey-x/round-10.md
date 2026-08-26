# Hickey-X 第 10 轮 · 终审裁决书 —— 「这个 API 能用吗？」

> 视角：Rich Hickey 终审。裁决对象：本系列 R1–R9 约 60 条发现的收敛与修复排序。
> 纪律：终审前对本轮引用的全部载荷点重新实读当前磁盘（行号见各条），不采信任何未经本轮复核的旧结论。本轮只读，未改任何项目源文件。

---

## 一、总判（三层三个等级）

**数学内核（L1 代数：NatStar/Interval/ZStar/SignedNet/Signature/Algebra）——良。**
这是全项目最接近「simple」的一层：载体全部 `readonly record struct` 与封闭判别联合，构造即合法（R4-V9），溢出归 ⊤ 的律法统一（Numeric.cs:27-29），Signature 结构相等三桶 `SetEquals` 是真值语义（Objects.cs:186-218）。扣分项是边缘处的失约：ZStar 裸算术绕过溢出律（SignedNet.cs:30-33，R4-V3）、`Merge([⊤,⊤],[1,1])=[1,⊤]` 的格论瑕疵潜伏无消费（R2-N4，R3 已降级）、Sequence 仍名异实同（DerivedMetrics.cs:50）。内核值得信任，前提是你只用它的正门。

**契约面（EffectScriptContract + EFFECT_SCRIPT.md + samples）——中偏差。**
用户真正生活的地方，恰恰是最纠缠的一层：同一资源两种 JSON 方言（对象 vs `"commandBuffer:gpu"` 平面键，Contract:155-166 vs :187）；⊤ 在解析侧无拼法、在序列化侧被写成 0（Contract:175/:234）；15 个资源类型只有 5 个能过桥、8 个作用域只有 4 个能过桥（R1-MED-2/R3 仲裁：15 中抛 10，:199-206 区域）；官方旗舰示例照抄必炸（EFFECT_SCRIPT.md:139 嵌套 gpu 对象 vs Contract:161 `ReqStr` 要 string）；samples 至今没有任何剧本样例（ls 实证）。fail-fast 的口号写在文件头注释里（Contract:4），fail-soft 的后门开在根级键与可选字段里（Contract:25/:33）。这一层不是没努力——25 处 throw、五键报错列全合法集——但它把诚实花在了低频路径上，把静默留给了高频路径。

**运行时壳（Runtime：Fiber/DependencyGraph/PluginRuntime/InverseReplay/GodotShell）——良⁻。**
审计期间这层被实质加固过：PluginRuntime.Register 重复 Id 改抛、AddDependency 补同 Scope 校验、AddHardEdge 加 `ValidateSameScope`（DependencyGraph.cs:30/:41，R9 终审确认）——系列纪律真的换来了代码。残余的是老毛病：图本体 `Register` 仍 `_fibers[f.Id]=f` 静默覆盖（DependencyGraph.cs:24）、部分释放诊断裸 catch 丢根因（InverseReplay.cs:34-40）、公共可变三角 Graph/IsShuttingDown/OnSuspending 三条绕过协议的走廊仍在（R4-V5）。LoadValidation 消息全线带 Fiber.Id + § 引用是壳层最好的部分。

## 二、用户困难判定：会。而且会的是项目自己指定的那批用户

**AI 写剧本用户（README 承诺的核心场景）：第一分钟就会，且失败是静默的。**
证据链：照抄官方 §4 示例 ⇒ Parse 必炸且报错不指路（#3）；侥幸写对了，漏一个字母 `budgat`/大小写 `Budget`/类型错 `budget:[]` ⇒ 整个预算门无声消失，`Passed=true` 照常发放（#1，R7 实验 d1/d2/d3 全复现）；干脆不写 budget ⇒ gate(2) 一条都不跑，通过与否与峰值无关（#4）。三条路殊途同归：**用户得到的是一个说谎的绿灯**。对以「AI 写剧本、不跑游戏就审计」为存在理由的产品，没有比这更根本的可用性失败。

**C# 集成用户：中度困难，伤害集中在跨边界时刻。**
C# 合法的 ⊤ 预算经 ToJson→Parse 变成 0 并产生虚假 Leak（#2，R7-b 实测全链）；一半的类型在 ToJson 当场抛（#10）；Budget 构造函数把调用方字典变成远程位置，Audit 结果随外部变异漂移（#8）；公开查询 At 与审计 Audit 对同一剧本给出 1 vs 2 两个并发计数（#6，R7-f 实测）。这些用户的共同处境是：必须读实现才能预测行为——Hickey 判据里最贵的失败。

**Runtime 宿主作者：困难最少但最深。** CrashReport 只说哪里坏不说为什么（#9）；图不变量半数仍靠注释站岗。这类用户少，但他们坏的时机是生产事故现场。

结论一句话：**不会写不出程序，但会在不知情时拿到错误的审计结论**——对一个验证工具而言，「静默给出错误结论」就是「造成使用困难」的最重刑。

## 三、十大问题榜（按用户伤害排序）

| # | 发现 | 一句话 | P |
| --- | --- | --- | --- |
| 1 | R6-E1+E3（源 R2-N7） | 根级未知键/大小写/budget 类型错三路静默禁用整个预算门，绿灯照发（Contract:25/:33） | **P0** |
| 2 | R2-N1 | SerializeBudget 把 ⊤ 上限写成 0，round-trip 制造虚假 Leak（Contract:234 `kv.Value.Value`，R7-b 实测） | **P0** |
| 3 | R8-TOP1 | 官方 §4 旗舰示例无法通过自家 Parse：嵌套 gpu 对象 vs 要求 string（EFFECT_SCRIPT.md:139 vs Contract:161） | **P0** |
| 4 | R1-HIGH-3 | 零预算=恒真审计，AuditResult 无覆盖面维度，「查过通过」与「没查」不可区分（gate(2) 只遍历 cap.Caps） | **P0** |
| 5 | R1-HIGH-1 | 核心卖点（剧本 DSL）零上手路径：samples 无任何剧本样例，唯一形状文档是必炸的示例 | **P1** |
| 6 | R5-C2 | At 说 1 个并发副本、Audit 按 2 个计冲突与峰值，同一事实两套官方答案（EffectScript.cs:91 vs sweep） | **P1** |
| 7 | R2-N3+R3 合流 | `default(LoopCount)` 后门直通 `ulong.MaxValue / w.Value` 除零链（EffectScript.cs:200/:219；Of(0) 守卫可被绕过） | **P1** |
| 8 | R4-V1 | Budget 构造持有调用方可变字典，值区里的位置，None 单例底座亦可被强转污染（EffectScript.cs:353/:356） | **P1** |
| 9 | R6-E2 | InverseReplay 裸 catch 丢原始异常，CrashReport 永远只有「哪里」没有「为什么」（InverseReplay.cs:34-40） | **P1** |
| 10 | R1-MED-2 | 契约面≠API 面：10/15 资源、4/8 作用域 ToJson 即抛，子集边界藏在 switch 的 default 里 | **P1** |

（遗珠：V2 违例键未归一——一行修复但伤害窄，并入路线图 P1 首位顺带清单；HIGH-2 双轨方言并入 #1 同族根治。）

## 四、对 round-09 P0/P1/P2 的最终裁决

**大体背书，一处调整，一处补充。**

- **同意**：N1、E1、§4 示例三项定 P0 准确——每条 ≤10 行 + 回归测试，且都有 R7 实验背书，修复轮可以直接开工。
- **调整一处**：**HIGH-3 从 P1 升 P0**。R9 把它放在 P1 是按「修复行数」定价，但定价标尺应是用户伤害：它与 #1 合并成同一个故障模式——「Passed=true 不含信息量」。修法便宜到不像话（AuditResult 加 `CapsChecked` 计数，或 caps 为空时附一条 warning 类 Violation），却能把整个产品从「可能说谎的绿灯」升级为「诚实的范围声明」。性价比全榜第一，没有理由留在下一档。
- **补充一条方法论约束**：R3 的时间线发现必须写进修复轮的验收标准——**每个修复 PR 附「对应发现编号 + 当前行号」**，防止修 A 时顺手改了 B 导致后续轮次成片误报。本系列三次自纠（E4 数字强转夸大、R5-C5 窗口不可达、资源计数 14→15）全部源于行号漂移，这个成本不该再付一遍。
- **P2 维持**：N2 残余、V5 可变三角、E5 事件索引、CompareToFinite 改名等——真实但不急，记录在案即可。

**修复轮第一枪：先修 E1 家族（根级白名单 + budget 类型严判），不是 N1。**
理由有三：(a) 频率最高——任何拼写错误都触发，N1 只打 C#→JSON 往返用户；(b) 爆炸半径最大——它禁用的是整道 gate，N1 只是翻转一个预算；(c) 它是其他发现的放大器——修好白名单后，#4 的 CapsChecked 计数才有可信的输入前提。一个递归白名单辅助函数（与 ParseResource 五键报错同一标准）约 20 行，就能把契约层从「选择性诚实」拉回「系统性 fail-fast」。第二枪 N1（对称加 IsTop 分支），第三枪 §4 示例一行修正并把示例固化为契约测试——三枪打完，AI 用户的「第一分钟死亡谷」即告填平。

## 五、正面清单（真·优点 TOP-5，鞭子之外有骨架）

1. **值语义双轨制画线干净**：数学对象全 `readonly record struct`/封闭判别联合，运行时实体才是 mutable class（R4-V9 全仓普查）——「数学是值，生命是对象」这条线大多数项目画不出来。
2. **错误消息的尖子生够格当教材**：`LoopCount.Of(0)` 连「为什么 0 有害」都讲了（DerivedMetrics.cs:18）；LoadValidation 全线带 Fiber.Id + § 引用；Analyzer EAA 诊断内嵌修复动作。
3. **审计期间代码真的在动**：Join 从撒谎注释修成真 merge_I（Objects.cs:193-209）、[⊤,⊤] JSON 路径改 fail-fast（Contract:71-73 注释直接引用审计理由）、Parallel 补 PARA_CONFLICT 守卫（DerivedMetrics.cs:52-63）、PluginRuntime 堵重复 Id——对抗审计与开发形成了闭环，这在被审项目里罕见。
4. **溢出纪律有统一的法**：NatStar「溢出⇒⊤保守」贯穿加乘（Numeric.cs:27-29），采样完备性定理经 R5 推演成立——端点采样没有漏掉任何存活跳变点。
5. **最小剧本 round-trip 幂等实测成立**（R7-a 系列）：正门的序列化/反序列化是对称的，负数/超界 budget 的异常处理合格——契约层的地基是好的，坏的是边角。

## 六、方法论附言：十轮对抗设计有效吗？有效，且有数据

有效性证据不是感觉而是纠错率：本系列抓出自己的误报/夸大至少 3 起（R6-E4 数字 scene 强转被 R9 证伪、R5-C5 watchdog 二次入队被 R6 状态机分析推翻、R1 资源计数 14 被 R3 修正为 15），并识破一次外部陷阱（并行系列基于过期快照，其综合若被直接采信将产生成片误报——R3 时间线仲裁）。R7 的可执行探针把两个 HIGH 从静态推断升格为 17 行可复现事实，是全系列可信度的转折点。代价同样真实：工作树在审计窗口内持续演进导致行号反复漂移，每轮被迫重读——所以终审给修复轮的第一条建议不是代码，是流程：**发现编号随 PR 走**。

**最终一句判词**：这套代数配得上更好的门面——内核是真金，契约面是纸糊的；先把说谎的绿灯换成诚实的红灯，再谈别的。

---

## 证据：本轮实读文件清单

- 历史 9 轮全文：audit/hickey-x/round-01..09.md（+ round-09-source.md）
- 本轮复核读取（当前磁盘）：src/Cosmos.EffectAlgebra/EffectScriptContract.cs（:25/:33/:71-73/:155-166/:175/:187/:230-234）、EffectScript.cs（:59/:85-91/:114/:200-250 区域/:353/:356）、DerivedMetrics.cs（:10-18/:50-63）、Objects.cs、Numeric.cs、SignedNet.cs、Algebra.cs、ApiMapping.cs、Deviation.cs
- Runtime 层：DependencyGraph.cs（:18-41）、InverseReplay.cs（:28-45）、Fiber.cs、PluginRuntime.cs、LoadValidation.cs、GodotShell.cs、IHost.cs
- 文档与样例：EFFECT_SCRIPT.md（:130-150 §4 示例、:67 struct/class 谎言）、README.md、samples/ 目录树（实证无剧本样例）
- 未修改任何项目源文件；未运行构建/测试（基线沿用 R9 实测：95+311+73=479 通过）

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "id_note": "",
      "status": "satisfied",
      "evidence": "终审裁决书已写入 D:/Godot/Cosmos/audit/hickey-x/round-10.md：总判三层三级、用户困难判定（会，分三类用户）、TOP-10 问题榜、对 round-09 P0/P1/P2 的最终裁决（背书+HIGH-3 升 P0+第一枪指定 E1）、正面 TOP-5、方法论附言；全部载荷论断在本轮重新实读当前磁盘核过行号（如 Contract:234/:25/:33、EffectScript.cs:353/:91/:200、EFFECT_SCRIPT.md:139、DependencyGraph.cs:24/:30、InverseReplay.cs:34）"
    }
  ],
  "changedFiles": [
    "audit/hickey-x/round-10.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "grep -n / sed -n 对 EffectScriptContract.cs、EffectScript.cs、DependencyGraph.cs、InverseReplay.cs、DerivedMetrics.cs、EFFECT_SCRIPT.md 的载荷行复核；ls samples 树",
      "result": "passed",
      "summary": "确认 N1/E1/§4 示例/HIGH-3/V1/V2/C2/N3 仍存在，[⊤,⊤]/Join/Parallel 守卫已修，samples 无剧本样例"
    }
  ],
  "validationOutput": [
    "终审前抽验 12 个载荷点全部与报告陈述一致；未运行 dotnet test（基线沿用 R9 实测 479 通过）"
  ],
  "residualRisks": [
    "工作树在本系列期间持续演进，报告中的行号以本轮实读为准但可能在修复轮继续漂移",
    "SampleGame 测试套件存在 0~10 例并行抖动（R9 实测），修复轮回归判定应以三项目全绿为目标并先隔离该抖动"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增终审裁决报告 round-10.md（约 120 行），未触碰任何项目源文件",
  "reviewFindings": [
    "no blockers"
  ],
  "manualNotes": "收官轮为裁决书而非复核员角色：对 round-09 排序做了一处实质调整（HIGH-3 升 P0，理由是它与 E1 共同构成『说谎的绿灯』故障模式而修复极廉价）并指定修复第一枪为 E1 fail-soft 家族而非 N1（频率×爆炸半径×修复成本三维比较）。"
}
```
