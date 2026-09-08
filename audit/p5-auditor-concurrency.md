# 并发边界攻击报告（P5）

- **审计对象**：`src/Cosmos.EffectAlgebra`（L1 纯代数）、`src/Cosmos.EffectAlgebra.Analyzer`（L3）、`src/Cosmos.EffectAlgebra.Generator`（L2）全部公开 API 的线程安全声明
- **审计日期**：2026-09-08（只读攻击，零源码/文档/测试改动；攻击 harness 建于系统临时目录，验证后已删除）
- **被审计声明**：README 诚实边界 #10（README.md:278）「Runtime 非线程安全（帧驱动单线程模型，零锁）：全部调用须在宿主主线程……」——**该声明只覆盖 Runtime**；README Q5（README.md:209-210）仅以「剧本审计无这些约束」间接暗示 L1 无单线程限制，从未正面承诺 L1/L2/L3 并发安全。

---

## 总评（线程安全声明可信度）

**可信度：高（实测）。攻击未能找到任何可利用的线程竞态——L1/L2/L3 的并发安全是事实成立的。**

三条结构性证据：

1. **静态可变状态为零**。全仓 grep 证实 L1/L2/L3 中不存在任何可变 static 字段：没有 `Lazy<T>`、没有 `ConcurrentDictionary`、没有手写 double-checked locking、没有 `lock`/`volatile`/`Interlocked`。全部静态面要么是 `static readonly` 不可变值（`DiagnosticDescriptor`、`NatStar.Top`、`Signature.Empty`、`Budget.None` 等），要么是 CLR 类型初始化器保护的 getter-only 静态属性（`GodotApiWhitelist.All`、`ReleaseClass.Names`），要么是每次调用分配新数组的纯计算属性（`ReleaseClass.All`）。`Peak`/`NetTable`/`Compatible`/`Derived`/`SignatureDeviation` 的静态方法只触碰参数与局部变量。
2. **Analyzer 不是靠单线程编译侥幸**。`EnableConcurrentExecution()` 下，per-compilation 白名单被构建为不可变 `WhitelistLookup`（两个 `ImmutableDictionary`，EffectAlgebraAnalyzer.cs:208-244）并经闭包捕获；`AnalyzeMethod` 全局部变量；配置诊断列表 `configDiags` 为每次 `OnCompilationStart` 新建的局部 `List`，仅被同编译单次的 CompilationEndAction 消费。
3. **Generator 无实例状态**。`EffectAlgebraGenerator` 成员仅 const 与 `static readonly DiagnosticDescriptor`；管线全部 static lambda；`LoadExtras` 状态为方法局部 builder。生成器实例跨编译共享不泄漏（实证见 F7）。

**但必须指出：这份并发安全目前是「实现副产品」，不是「契约承诺」。** 文档没有任何一行正面声明 L1 纯函数可并发调用，测试套件没有任何并发回归钉。安全性完全悬在「未来维护者不引入 static 缓存、不给 `Signature` 加就地变异方法」这条隐性纪律上。实证方法与结论见发现清单。

---

## 实证方法（攻击 harness 摘要）

临时控制台工程（net10.0，引用仓库已构建的 L1 net10.0 / Analyzer net9.0 / Generator net10.0 DLL + Microsoft.CodeAnalysis.CSharp 4.12.0），运行三套攻击后删除：

- **L1 锤击**：12 线程 Barrier 同步首触三个静态面（`GodotApiWhitelist.All`/`ReleaseClass.Names`/`IsRelease`，构造类型初始化器并发首触竞态）后各 4000 轮，共享同一批只读 `Signature` 实例并发调用 `Of/Union/Join/Combination.Loop/Peak.Compute/NetTable.Compute/IsConserved/Compatible.IsCompatible/SignatureDeviation.Calculate/EffectScript.Audit`，与单线程参考结果逐位比对（`DeviationVal` 按 double 逐位、`AuditResult` 按 Passed+Violations 全字段展开比对）。
- **L3 并发**：同一 `EffectAlgebraAnalyzer` 实例并发跑 4 个独立编译（各 240 个含 Godot API 调用模式的方法 + 2 个 cosmos.effect.json AdditionalText，一合法一非法）→ 1442 条诊断零异常零串扰；同编译重复分析两遍诊断逐字节一致（85,670 字符逐条对齐）。
- **L2 泄漏**：同一 generator 实例按 A(40 方法)→B(15 方法)→A 顺序跨编译运行，输出逐字节一致；双线程并发运行同实例无漂移；非法配置 loud 产出 EAA0701。
- **附加取证**：反射枚举 `Signature` 非 readonly 实例字段；饱和算术非结合性探针（见 F5）。

**主套件结果：17/17 全部 PASS。**

```
PASS  L1.静态面首触竞态(All/Names/IsRelease)
PASS  L1.Signature.Of 并发确定  0/48000
PASS  L1.Signature.Union 并发确定  0
PASS  L1.Signature.Join 并发确定  0
PASS  L1.Combination.Loop 并发确定  0
PASS  L1.Peak/Net/IsConserved 并发确定  peak=0 net=0
PASS  L1.Compatible 并发确定  0
PASS  L1.SignatureDeviation 并发确定(逐位)  0
PASS  L1.EffectScript.Audit 并发确定  0
PASS  L1.Signature 非 readonly 实例字段清单（反射取证）  _read,_write,_occupy
PASS  L1.共享 Signature 在并发 Union 下未被变异
PASS  L3.同实例跨 4 编译并发分析(EnableConcurrentExecution)  1442 条诊断
PASS  L3.同编译重复分析诊断逐条确定  diagLen=85670
PASS  L2.同生成器实例跨编译(A→B→A)输出无泄漏  A1 bytes=60825, A2 逐字节一致=True
PASS  L2.同生成器实例双线程并发运行
PASS  L2.非法 cosmos.effect.json ⇒ EAA0701(loud)
PASS  L2.生成产物形状(@class 消歧存在)
```

---

## 发现清单

| # | 严重度 | 攻击向量 | 实证/推理 | 判定 |
|---|--------|----------|-----------|------|
| F1 | **MED** | 并发安全零声明：L1/L2/L3 全部公开 API 的线程安全模型没有任何文档承诺，README 诚实边界 20 条无一涉及 | README.md:278 的 #10 明文只钉 Runtime；Q5（README.md:210）仅说「剧本审计无这些约束」（弱暗示）。攻击实证当前实现并发安全，但这是实现副产品：任何未来 PR 引入一个 static 缓存字段（如 canonical 化 memo、白名单 Dictionary 缓存）都会无声破坏之，仓库内没有任何测试或分析器护栏能拦截 | **文档欠明确** |
| F2 | LOW | 静态初始化线程安全依赖 CLR 隐式语义：`GodotApiWhitelist.All`（ApiMapping.cs:105，`{ get; } = BuildValidated()`）、`ReleaseClass.Names`（ApiMapping.cs:248）均无显式锁/Lazy，靠 beforefieldinit 类型初始化器的 CLR 保证 | 12 线程 Barrier 同步首触竞态攻击 → 零异常、初始化恰一次。CLR 类型初始化器保证至多执行一次且异常被缓存（进程级），当前实现正确。风险在演化：若日后有人把 `All` 改成「手写 DCL + 可变缓存」即引入经典竞态，且无护栏报警 | **防御生效** |
| F3 | **MED** | `Signature` 伪不可变模式：三个实例字段 `_read/_write/_occupy` 非 readonly（Objects.cs:165-167），`Add`/`Union` 用「new 出新实例 → 构造路径上原地改字段 → 返回」实现（Objects.cs:196-212） | 反射取证确认恰 3 个非 readonly 私有字段。当前所有变更都发生在新实例发布给其他线程之前，且 .NET 内存模型下引用赋值带 release 语义 → 共享实例只读安全：8 线程 × 20000 次并发 `Union` 高频读 `refSig`，内容零变异。但这不是类型系统强制的——后续维护者加一个 mutating `Add`（public 或 init 通道）即把它变成共享可变热点，编译器不会拦 | **文档欠明确**（模式易碎：宜 readonly 字段 + 私有构造器收口，或注释钉死「字段仅构造期可写」契约） |
| F4 | LOW | 纯静态函数共享状态扫描：`Peak.Compute`（Algebra.cs:110-128）、`NetTable.Compute`（Algebra.cs:51-65，私有 `Dictionary` 仅 Compute 内构造、发布后只读）、`Compatible.IsCompatible`（Algebra.cs:24-34）、`Derived.*`、`SignatureDeviation.Calculate`（Deviation.cs:22-49） | 12 线程 × 4000 轮并发调用与单线程参考逐位一致（含 `DeviationVal` double 逐位比较、`AuditResult` 全字段展开比对），48000 次 `Signature.Of` 零次漂移。未找到任何共享可变状态 | **声明充分** |
| F5 | LOW | 确定性锐边（非竞态）：饱和算术非结合性 × `ImmutableHashSet` 枚举序。ZStar/NatStar 加法溢出 ⇒ 饱和 ⊤（SignedNet.cs:36-59），饱和加法不满足结合律；net/Peak 的聚合顺序取自 ImmutableHashSet 枚举 | 探针实证：`(long.MaxValue+1)+(-long.MaxValue)=⊤` 而 `long.MaxValue+(1+(-long.MaxValue))=1`——结果可翻转。缓解：同进程内同一 Signature 枚举序稳定（两种插入序探针枚举一致，net 结果相同）；但 .NET 字符串哈希按进程随机化 ⇒ 跨进程/跨机器，同一 claim 集合在溢出边缘的 net/Peak 可能 ⊤ 与有限值漂移。触发条件极窄（size 端点逼近 long 域 + 多 claim 抵消），无并发危害 | **实现缺陷**（数学实现锐边：饱和算术交换/结合注释未声明非结合性；修复方向=文档声明或聚合前排序） |
| F6 | LOW | Analyzer 并发模型：`EnableConcurrentExecution()`（EffectAlgebraAnalyzer.cs:199）下多线程诊断回调是否触碰共享可变状态 | 逐行审计：唯一共享静态 = 6 个不可变 `DiagnosticDescriptor` + 2 个 const FQN；per-compilation 合并白名单经 `WhitelistLookup.Build` 固化为 ImmutableDictionary 闭包快照（:175-176），`AnalyzeMethod` 及其辅助全为局部变量；CompilationEndAction 闭包捕获的 `configDiags` 为 per-compilation 局部 List 且每编译仅触发一次。攻击实证：同实例并发 4 编译 1442 条诊断零异常零串扰，重复运行逐字节确定。**不是单线程编译侥幸，是真并发安全** | **防御生效** |
| F7 | LOW | Generator 实例跨编译共享泄漏：`IIncrementalGenerator` 实例是否在多编译间共享并携带状态 | 审计：生成器类零实例字段；`LoadExtras`（EffectAlgebraGenerator.cs:88-124）全局部状态；`RegisterSourceOutput` lambda 内状态均为局部。攻击实证：同一实例 A→B→A 跨编译运行输出逐字节一致（无泄漏），双线程并发运行无漂移 | **声明充分** |
| F8 | LOW | 增量缓存/配置合并竞态：多文件 cosmos.effect.json 并发变更、`LoadExtras` 合并、缓存键 | `configTexts` 管线缓存键含配置文本（文本变更 ⇒ 管线重跑，代码注释 QED-C1c 明示）；`GetAnnotatedMethod` 返回 record（值相等）⇒ 缓存不持有 SyntaxNode，无跨编辑泄漏；Roslyn 串行执行 transform 用户委托。跨文件 Canonical 碰撞 ⇒ 整文件弃用 + EAA0701（实证非法 JSON → EAA0701 loud）。锐边：多文件碰撞时「弃用哪个文件」取决于 AdditionalFiles 顺序——结果是确定的但顺序敏感，且 EAA0701 消息只报 canonical 键不报文件名（EffectAlgebraGenerator.cs:111-112） | **防御生效**（附带：碰撞诊断不含文件名，多文件场景归因欠明确——F1 同源的文档锐边） |
| F9 | **MED** | 并发回归保护缺位：tests/ 全套件无任何线程安全测试钉 | grep `Task.Run`/`Parallel`/`Thread` 仅命中 obj/ 生成物；并发安全完全无回归保护。本次攻击 harness 的 17 项探针（类型初始化器首触竞态、共享只读实例锤击、Roslyn 并发分析、生成器跨编译泄漏）证明当前安全，但全部未沉淀进仓库——下一个重构者可以在零测试红灯的情况下无声拆掉这些性质 | **文档欠明确**（实为测试缺位；建议将 17 项探针收编为 `ConcurrencyTests`） |
| F10 | LOW | 共享单例变异通道：`Signature.Empty`/`NatStar.Top`/`Interval.Default`/`Budget.None` 等静态单例是否可被并发污染 | 全部为 `static readonly`；底座 ImmutableHashSet/ImmutableDictionary/readonly record struct；`Budget` 构造期防御拷贝（EffectScript.cs:421-430，外部字典改动不透传）。`ImmutableHashSet.Add` 为持久化结构，`Signature.Add` 并发安全。未找到变异通道 | **声明充分** |

> 严重度说明：无 HIGH——攻击未发现任何可利用竞态。MED 三条均为「安全性质无契约化/无回归保护」的结构性风险，而非现行缺陷。

---

## 值得表扬

1. **per-compilation 不可变快照设计（静态字典退役）**。Analyzer 把白名单合并视图固化为不可变 `WhitelistLookup`（ImmutableDictionary ×2，A2-07 预计算 canonical 键）并经闭包捕获，Generator 侧 `LoadExtras` 返回 `ImmutableDictionary`——把「共享可变缓存」这个 Roslyn 扩展最经典的竞态源整个消灭在结构层面，而不是靠加锁补救。
2. **RS1035 精确围栏**（CosmosEffectConfig.cs:83-110）。文件 IO 与 `Console.Error` 被显式钉在 CLI/CI 路径并局部 `#pragma` 说明；分析器侧一律走编译器供给的 `AdditionalText.GetText`——编译期零直接 IO，从根上排除了编译进程内的文件读写竞态面，且不靠项目级 NoWarn 粗放压制。
3. **L1「类型即边界」纪律使零锁安全成为自然结果**。载体全部 readonly record struct（NatStar/ZStar/Interval/Claim/LoopCount/Budget），集合底座全部 Immutable，`Budget` 构造期防御拷贝（S06-002 归一键），`Audit` 扫换线全部局部字典——不可变性不是为并发设计的，却让并发安全成为代数实现的免费副产品。

---

## 结论

- **L1 纯代数、L3 Analyzer、L2 Generator 在 Roslyn 编译器并发模型下实测线程安全**（17/17 攻击通过），且 L3 的安全来自结构性设计而非「单线程编译」侥幸。
- 判定「实现缺陷」仅 1 条（F5：饱和算术非结合性 × 枚举序的跨进程确定性锐边，LOW，触发条件极窄，非并发竞态）。
- 最有价值的产出不是发现的缺陷，而是缺口：**并发安全契约未声明（F1）、`Signature` 伪不可变模式易碎（F3）、并发测试钉缺位（F9）**。建议最小收口：README 诚实边界补一条「L1 纯函数/静态面并发安全（依 CLR 类型初始化器 + 不可变底座），L2/L3 编译期并发安全（依不可变 per-compilation 快照）」+ 将本报告 17 项探针收编为回归测试。
