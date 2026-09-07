# 性能规模与生成器攻击报告

审计员：性能规模与生成器攻击（P5）。日期：2026-09-08。
方法：只读攻击仓库 `D:\Godot\Cosmos`；全部实验在系统临时目录（`P:\Temp\p5perf`、`P:\Temp\p5gen`、`P:\Temp\p5gen3`、`P:\Temp\p5escape`，实验后已删除）。
测量口径：`BestMs` = 3 次取最小（与 `ProdAuditR4AuditScaleTests` 同去噪法），Release net10.0，直接引用 `src\Cosmos.EffectAlgebra\bin\Release\net10.0\Cosmos.EffectAlgebra.dll`；`alpha = log(t4/t1)/log 4`（1000→4000）。

## 总评（性能承诺可信度与生成器鲁棒性结论）

**性能面：承诺「可信但有量化盲区」。** 常规形状裕量极大：1000 事件 all-green 异构剧本 Parse+Audit 实测 16–39ms（README 硬墙钟钉 <5000ms ⇒ **>128 倍裕量**），CLI 端到端 0.26–0.33s；全绿 2000 事件 Audit 仅分配 6MB。诚实边界 #16 的存在、`O(S·D)` 定性与「恶化=semver major」冻结是对的，但 **「实测 4 倍数据 ≈6x」只在被钉住的那一种交错形状上成立**：

- 复现钉形状（`pin`，create[1,1]）得 t4/t1=5.3x，与文档 ≈6x 吻合（校准成功）；
- 同为逐事件互异 (resource,scope) 的 **spread 形状（Lo=i, Hi=N+i 双向交错）** 实测 t4/t1=**14.8x（alpha≈1.94，已过纯二次 16x 的门口）**，N=8000 时 t8/t1≈62x（alpha≈1.99，纯二次渐近确认）——**超出仓库自家曲线钉 12x 阈值**，即若以该形状补进 `ProdAuditR4AuditScaleTests` 当场红；
- **宽预算表是 #16 未记录的第三超线性乘子**：gate(2) 每采样点全扫 `cap.Caps` 并对每键 `ResourceId.Normalize`（`EffectScript.cs:301-303`），spread+逐资源预算使 N=4000 从 94.5ms 放大到 **3554ms（≈37x）**，N≈5000 即破 <5000ms 墙；
- 违例洪水（O(S·G)，#6/#16 已声明）实测绝对量惊人：N=1000 ⇒ **1,002,000 条 NegativeDip、Audit 1.0s、violations JSON 247MB**；CLI 800 事件端到端 **4.0s + 183MB stdout + 183MB --out 双写**，「violations 可喂回 LLM 重投」在该区间实际不可消费。

**生成器面：emit 正确性优秀，发现 1 个 HIGH 实现缺陷。** 全部敌意声明形状（泛型类/嵌套类/重载/接口声明+实现/partial/深命名空间/关键字名 `@class`/canonical 别名 `queue_free`/`QUEUEFREE`）34/34 生成、编译全绿、22 个同名 `Load` 变体签名逐一与 L1 白名单真值一致；A2-05 消歧后缀在极端重名下无 CS0111。但 **`LoadExtras` 对多个 `cosmos.effect.json` 的 Canonical 碰撞只做「逐文件 vs 基础表+本文件内」复核，跨文件碰撞静默 last-win 覆盖**（`EffectAlgebraGenerator.cs:88-114`），同一配置 L3 报 error EAA0701、L2 零诊断且 `ComputeFooBar` 拿到错误 claims——违反 README #12/QED-C1a 的 loud 契约，属「静默改写比报错更危险」教义自家违反。增量鲁棒：6 步连续小改（增/删/恢复/改名/坏配置/恢复）真实 `dotnet build` 全部符合预期、成员集与签名始终一致；#14 声明的 IDE 瞬态少生成在真实构建路径无法复现（与「全量构建恒正确」承诺一致）。

## 攻击发现清单

| # | 严重度 | 攻击向量 | 实测数据 | 判定 |
|---|--------|----------|----------|------|
| 1 | HIGH | **spread 形状超线性劣化超出已记录曲线**：E 个事件逐互异 (resource,scope)，Lo=i、Hi=N+i 双向交错（进入与退出各生成 N 个采样点，`net` 字典只增不减 ⇒ Σ\|net\|=Θ(N²)） | t4/t1=**14.8x，alpha≈1.94**（1000:6.4ms→4000:94.5ms）；N=8000 t8/t1≈62x，alpha≈1.99（6.4→396.7ms）＝纯二次。同族对照：文档钉形状仅 5.3x（校准吻合 ≈6x）；toploop(ω=⊤) 变体亦达 12.4x。**已超 `ProdAuditR4AuditScaleTests` 的 12x 断言阈值** | 文档欠明确（#16 的「≈6x」非该退化族的上界；建议把 spread 形状补进曲线钉，否则 12x 防线对更糟形状失守） |
| 2 | MED | **gate(2) 预算宽表 = 未记录的超线性乘子**：每采样点全扫 `cap.Caps` 且逐键 `ResourceId.Normalize`（`EffectScript.cs:301-303`）；#16 只点名 gate(1)/(3) 的 net/grp | spread+逐资源预算（Caps.Count=Θ(N)）：N=1000 355.9ms、N=2000 851ms、N=4000 **3554ms**（同形状无预算 94.5ms ⇒ **37.6x 放大**）；N≈5000 即破 README「1000 事件 <5000ms」绝对墙的等比外推 | 文档欠明确（O(S·D) 数学上勉强覆盖，但 #16 的乘子清单与 ≈6x 量化均不含 gate(2)；`peakReported` 去重使输出有界，纯性能面） |
| 3 | HIGH | **违例洪水 O(S·G) 的工具端绝对量**：release 早于 create 的 staggered 脚本 ⇒ 每资源×每采样点一条 NegativeDip（无去重，#6 声明的「时间序列语义」） | N=1000：**1,002,000 条**（≈N²+2N，含幽灵点，公式吻合）、Audit 1033ms、序列化 247MB/2684ms；N=2000：**4,004,000 条、Audit 3271ms、分配 1613MB**（≈400B/条）；CLI 端到端 N=800：**4.0s，stdout 183MB + --out 再写 183MB（双写）**，且 `resource` 字段为 `Gpu { BufferId = Rid { Value = tex0 } }` 调试格式放大体积 | 已知边界符合声明（#16 第二句已声明 O(S·G)；但「violations 喂回 LLM 重投」的工具契约在该区间实际不可用——建议 CLI 侧加条数上限/按 (Kind,Resource) 折叠选项，属工具层防御缺失，非库层违约） |
| 4 | HIGH | **生成器跨配置文件 Canonical 碰撞静默 last-win**：`LoadExtras` 逐文件 `MergedWith(extra)` 只复核「基础表+本文件」，累积 `builder[c]=m` 无跨文件碰撞检查（`EffectAlgebraGenerator.cs:94-113`） | 两个 `cosmos.effect.json`：a 定义 `FooBar`(Memory 7/create/Battle)，b 定义 `Foo_Bar`(Memory 9/release/Arena)，canonical 同键 "foobar"。L2（生产接法 OutputItemType=Analyzer，TreatWarningsAsErrors）：**构建全绿 0 警告，运行期 `ComputeFooBar` 返回 b 的 claims（Memory 9/release/Arena）**——a 的映射被静默吞掉；同一配置 L3（p5gen3 对照工程）：**error EAA0701「白名单扩展碰撞：'FooBar' 与 'Foo_Bar'」构建红**（`EffectAlgebraAnalyzer.cs:157-170` 注释明言「碰撞（vs 基础表/跨文件）」） | **实现缺陷**（违反 README #12/QED-C1a「扩展彼此碰撞 ⇒ EAA0701 + 整体弃用」loud 冻结契约；L2/L3 对同一配置分叉；生成器注释「MergedWith 碰撞复核：vs 基础表/扩展彼此 ⇒ 抛」在跨文件场景为假） |
| 5 | LOW | **`[EffectOverride(" ")]` 空白 reason 编译期逃逸（L2 单独接线时）**：attribute ctor 仅运行期执行，生成器照常 emit | p5escape 工程（只接 Generator）：`[EffectOverride("   ")]`/`("\t")` 构建绿、0 诊断、`EscapeProbe_Load.g.cs` 照常生成；生成器注释「reason 非空由构造子强制……类型保障，编译期无需额外诊断」与事实不符；L3 侧 `OverrideReasonRequired`（EffectAlgebraAnalyzer.cs:70-77）在编译期兜底拦截（其注释自己已承认 ctor 运行期才执行） | 文档欠明确（生成器注释的「类型保障」错误；防御在 L3 存在，仅 L2-only 接线时逃逸通道静默有产物） |
| 6 | LOW | **白名单/扩展双未命中的标注方法静默空签名**：`[EffectOverride] void UnknownThing()` | `ComputeUnknownThing(Signature.Empty)` 返回空 Signature，0 诊断——名字匹配残差（README §14 L2 已声明该 residual） | 已知边界符合声明（文档已诚实；列此存照） |
| 7 | GREEN | **A2-05 消歧后缀极端重名**：22 个同名 `Load`（泛型 `Outer`/`Outer<T>`、`GenOuter<T>.Inner`/`GenOuter<T1,T2,T3>.Inner`、嵌套 `G2.B.Cfg`、重载 ×3、partial ×2、深命名空间 `N1.N2.N3.Cfg`、NS00–NS09 十连同名类）+ `Spawn` 接口声明+实现 + 关键字名 `@class`/`@event` + canonical 别名 `QueueFree`/`queue_free`/`QUEUEFREE` | 34/34 个标注方法全部 emit，构建全绿（含 consumer 代码调用全部变体）；22 个 `Load` 变体签名 **distinct signatures: 1** 且与 L1 白名单 canonical 匹配真值逐一相等；`queue_free`/`QUEUEFREE` 折叠到 `queuefree` 同一 claims；跨工程场景（不同编译各自生成）结构上无碰撞面 | 防御生效 |
| 8 | GREEN | **增量鲁棒（R3-CG-07/#14 实证）**：连续 6 步小改后重建——①新增 NS10.Load ②删 NS09 ③恢复 ④`Preload`→`Pre_Load` ⑤config a 置坏 ⑥恢复 config+方法 | 每步构建 GREEN（⑤按契约 RED：单文件坏 JSON **loud error EAA0701**）、生成文件数 35→36→36→37→37→37 与标注方法集精确同步、每步 `Load` 变体签名 distinct=1、`FAILURES` 除已知 #3/#4 外恒 0。真实 build 路径未复现「短暂少生成」 | 防御生效（#14 声明的 IDE 瞬态未能复现于全量构建，与「CI 门不受影响」一致） |
| 9 | GREEN | **绝对耗时 vs README 硬墙钟**：1000 事件（异构互异 res+scope all-green） | 库内：Parse 12.6–35ms + Audit 3.5–4.1ms ⇒ **16–39ms（<5000ms 的 1/128）**；CLI 端到端 all-green 0.26s / spread 0.33s（含 dotnet 启动）；2000 事件 Parse+Audit 41–61ms | 已知边界符合声明（裕量巨大） |
| 10 | GREEN | **内存异常放大检查**：2000 事件 Audit 堆分配 | all-green staggered/spread：**alloc Δ=6MB**、Audit 12–41ms——无异常放大；对照 dipflood N=2000 alloc 1613MB 全部来自 #3 的违例对象/字符串（≈400B/条），非审计本体泄漏 | 已知边界符合声明（O(E·K) 内存本体健康；放大仅沿已声明的 O(S·G) 输出通道） |

**「实现缺陷」判定计数：1 项（#4，HIGH，生成器跨配置文件 Canonical 碰撞静默覆盖）。**

## 值得表扬（2-3 条）

1. **A2-05 消歧后缀极其健壮**：22 个同名标注方法（泛型/嵌套/重载/partial/深命名空间/十连同名类）零 CS0111、AddSource hint 零互相覆盖、且每个变体的 Signature 都与 L1 真值一致——「重名 ⇒ `_{FullType}_{idx}`」这条简单规则在所有我能构造的敌意形状下都成立。
2. **诚实边界的「存在 + 曲线钉方法论」本身是对的**：`ProdAuditR4AuditScaleTests` 用倍增比值而非绝对墙钟，我的复现（钉形状 5.3x vs 文档 ≈6x）证明其校准有效；正是这枚钉让 #1 的 spread 形状「一测即知超线」——问题只是覆盖面（一种形状）而非方法。
3. **常规路径性能承诺严重保守（好事）**：1000 事件 16–39ms 对 <5000ms 承诺有 >128 倍裕量、内存 6MB/2000 事件；L1 构造期守卫（Lo=⊤、default 旁路、空白 reason 的 L3 编译期兜底）使敌意输入大多在入口即 loud 拒绝，本轮未再发现新的假绿/漏报向量。

### 附：机制根因备忘（供修复参考）

- #1/#2 根因同源：`AuditAtSample`（`src\Cosmos.EffectAlgebra\EffectScript.cs:289-326`）每个采样点全量扫三字典——`net`（:291，只增不减，enter 累加于 :216，无 exit 回收）、`cap.Caps`（:301，逐键 Normalize）、`grp`（:315，随存活收缩）。S=Θ(E)、D=Θ(E) 时三者各贡献 Θ(E²)。低风险修法（不碰冻结语义）：net 负陷改为「仅在 enter/exit 时检查受影响资源的增量」或维护负值键子集；caps 预归一化一次（Budget 构造已归一键，Audit 侧 Normalize 冗余）。
- #4 修法：`LoadExtras` 把 `builder`（或累积 extra 集）纳入每文件碰撞复核——如 `MergedWith(builder.ToImmutable().AddRange(extra))` 后再赋值，即可与 L3 的「收集全部后单次 MergedWith」同界。
