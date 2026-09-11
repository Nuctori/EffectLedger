# QED 声明攻击报告

- 审计对象：`D:\Godot\Cosmos`（commit 工作区，2026-09-11 只读快照）
- 被攻击声明：**89 条 Dafny 定律 + 739 测试 + 43 类型快照 + 六层冻结 ⇒ 可承诺「一次发布永远不用更新」**
- 审计方法：全量只读源码审读（L1 `EffectScript*.cs`/`Numeric.cs`/`SignedNet.cs`/`Objects.cs`/`Algebra.cs`、Runtime 全部 8 文件、`formal/*.dfy` 558+330 行、`ci.yml`/`ci.sh`、schema/快照/测试钉）+ 临时工程实测（.NET 10.0.103 + Dafny 4.11.0/Z3 4.12.1，仓库零修改，临时工程已删除）。所有「实测」结论均由真实进程输出背书，非静态推断。

## 总评（QED 声明的可信度）

**声明不成立。** 六层冻结中有两层被**实测打穿**（CI 计数门、行为级），一层被**实测腐蚀**（Runtime 状态机，绕过 QED-P5.3 自称封死的加固），其余各层均找到至少一个可复现的假绿/失真向量。核心问题不是某一处 bug，而是**声明结构与实现结构错配**：

1. 「89 条定律」的守恒/采样充分性只覆盖 `[0, long.MaxValue]` 的精确算术世界，而 `Parse` 接受域是 `[0, ulong.MaxValue]`——定律域 ⊊ 输入域，地带内行为无任何定律背书，且与 Dafny 模型的 gate 级结论**方向性分歧**（实测 A2a/A2b）。
2. CI 形式验证门是一个 `grep -q "0 errors"` 子串匹配——**实测**在注入 10 条 `ensures false` 的坏定律后双门全绿（`100 verified, 10 errors` 通过）。形式验证的"红"从未被机器真正强制过。
3. 「测试全绿」与行为正确之间有一条实测走廊：预算别名静默改写（A1）、round-trip 崩溃（A3）在**当前全部测试绿**的状态下存在。
4. 「一次发布永远不用更新」被自家冻结条款**自锁**：A3/A5 是非破坏性必须修复的 bug，但任何修复路径都触碰 QED-E2「契约面任何变更 = semver major」——修一个崩溃 bug 要发 major，或者违反自己的冻结纪律。冻结把「永不更新」变成了「永不修复」。

信任度评级：**不可信（claim rejected）**。库存质量本身不低（防御生效条目见失败清单，扫换线相位、⊤ 闭包、Unknown 正号计入、重复 Claim 拒绝等攻击均被有效防御），但「QED 级可承诺」的元声明被三层实测反证。

## 攻击成功清单

| # | 严重度 | 攻击向量 | 具体输入/步骤 | 预期 vs 实际 | 判定 |
|---|--------|----------|----------------|----------------|------|
| 1 | **Critical** | **CI Dafny 门 grep 子串假绿 + 计数门充数**（六层之 CI 计数门） | 仓库 `ci.yml`/`ci.sh` 同款管线：`grep -q "0 errors"` + `verified ≥ 89`。在 `formal/CosmosSweepLine.dfy` 副本注入 10 条 `lemma BrokenN() ensures false { }` + 11 条 `lemma GateFillerN() ensures true { }`（临时目录，仓库未动），`dafny verify` 输出 `Dafny program verifier finished with 100 verified, 10 errors`，喂入原版门禁管线 | 预期：「P3-H3 计数门：定律被删减即红」「0 errors 才放行」。实际：**双门 PASS**——`"10 errors"` 含子串 `"0 errors"`；count=100 ≥ 89（充数 stub 即可抬计数）。10 条被证伪的定律在树上，门禁全绿 | **攻击成功（实测）** |
| 2 | **High** | **Runtime 公共 API 重入腐蚀：退出排空中 `IsShuttingDown` 被复位 ⇒ 永久 Active 泄漏**（维度 2） | 序列（全部主线程公共 API）：`Register(P)` → `LoadAll()` → `Register(X)`（X 保持 Inactive）→ `SynchronousExitDrain()`。P 的逆 Action 内：①重入 `SynchronousExitDrain()`——P 处于 `TearingDown+ReplayInProgress` 被跳过、X 为 Inactive 被跳过 ⇒ `_teardownQueue.Count==0` ⇒ **早退路径执行 `IsShuttingDown=false`**；②随即调 `LoadAll()`——QED-P5.3 装载守卫已被拆，`X.Load()` 把 Inactive 的 X 激活 | 预期：QedP53RuntimeHardeningPins 声称「逆 Action 重入装载 ⇒ 永久 Active 泄漏」已封死（红队 r2#1 只堵了 ReplayInProgress 异常复位路径）。实测：退出排空「完成」后 **X.State=Active、ShouldDispatch(X)=true、CrashReports 为空（零诊断）**——死场景里一个派发门常开、无任何 teardown 路径覆盖的 fiber。与诚实边界 #10「场景重载请新建实例」无关：腐蚀发生在单次退出排空内部 | **攻击成功（实测）** |
| 3 | **High** | **`Parse→ToJson→Parse` round-trip 崩溃**（维度 1/5；六层之 dialect/schema 层） | schema 1.0.0 合法输入：event `scope:{"type":"method"}`（schema `anyOf: [required scene, required type]` 放行）。`Parse` 接受并构造 `ScopeId.Method("")`（无 scene ⇒ name 落空串）；`ToJson` 产出 `{"type":"method","scene":""}`；再 `Parse` ⇒ `FormatException: events[0].scope.scene 须为非空字符串` | 预期：README ④「`Parse(ToJson(script)).At(t)` 语义等价」、诚实边界 #18「`ToJson` 抛异常的分叉不复存在」。实测：**纯契约面、无混 scope 的剧本，导出后再解析必崩**。ToJson 是 AI 回修闭环的出口——闭环在合法输入上断裂 | **攻击成功（实测）** |
| 4 | **Med-High** | **冻结 schema 与 Parse 不同界：超域整数**（维度 4/5） | `lifetime:[0,1180591620717411303424]`（2^70）。schema（frozen 1.0.0）`integer, minimum:0` **无 maximum** ⇒ 合法输入；`Parse`（`TryGetUInt64`）⇒ FormatException。同类：budget 值、size 端点、loop 值 | 预期：schema 描述「与 Parse 同界（R2A-04）」且契约已冻结。实测：**frozen 契约面自身左右互搏**。修复路径（Parse 收敛 ⊤ / schema 加 `maximum`）任一都触发 QED-E2「任何变更=semver major」——一个边界对齐 bugfix 被自家冻结逼成 major 或违纪 | **攻击成功（实测）** |
| 5 | **Med** | **守恒律的域缺口：完美配对的巨尺寸剧本被 C# 判 Leak**（维度 3） | create+release 两个事件、`size:[2^63,2^63]`（= long.MaxValue+1，Parse 接受）。C#：`ToZ(2^63)=⊤`、`Negate(2^63)=⊤` ⇒ net=[⊤,⊤] ⇒ `ContainsZero=false` ⇒ **Leak@10**。Dafny：`CosmosSweepLine.dfy` `contrib:int` 精确算术 ⇒ +2^63−2^63=0 ⇒ 守恒无违例；`ExactPairingContainsZero` 显式 `requires b.n ≤ LONG_MAX` | 预期：「89 条机器验证定律」背书 Audit 数学性质。实测：定律守恒域 `[0,2^63]` ⊊ Parse 接受域 `[0,2^64)`；地带内「数学上完美守恒」的剧本被实现报 Leak（假阳性），且无任何定律覆盖该地带行为 | **攻击成功（实测）** |
| 6 | **Med** | **NegativeDip gate 被静默吞掉：D4a 采样充分性引理失真**（维度 3） | 仅一个事件：occupy `mode:release, size:[2^63,2^63]`，`[0,10]`。C#：release 贡献 = `[⊤,⊤]` ⇒ `net.Hi=⊤` ⇒ gate(1) 判 `!Hi.IsTop` 直接跳过 ⇒ **采样点零 NegativeDip**，仅闭包 Leak@10（不同 Kind/AtT/detail）。Dafny：`contrib=−2^63` ⇒ `NetAt(0)<0`，D4a `NetAtSampleCoversSegment` 结论「NegativeDip 段首采样必可见（不漏报）」 | 预期：D4a 引理保证 gate(1) 在采样点精确可见。实测：实现与已验证模型在 gate 级**方向性分歧**——「不漏报」仅靠闭包 Leak 兜底，违例分类/时刻/归因与模型全不同。喂 AI 回修的 Violation 载荷（冻结契约 #6）因此失真 | **攻击成功（实测）** |
| 7 | **Med** | **预算别名静默改写 = 测试全绿下的假绿**（维度 1/4） | 同一剧本（峰值 10）两份 budget：对照 `{"memory:7":5}` ⇒ PeakExceeded（红）；攻击 `{"memory:7":5, "memory:007":1000000}` ——两键均为 schema `^memory:\d+$` 合法键，`Parse` 的重复键检测按**原始字符串**去重（`memory:7`≠`memory:007`）不触发，归一后同为 `Memory(7)`，文档顺序后者静默胜出 ⇒ `Caps 条目=1、生效 cap=1000000、Passed=true、CapsChecked=1` | 预期：R6-RB-04 教义「重复键 last-win 是结构性假绿向量——拒绝而非改写」；A1-12 曾以同款理由杀掉空 id 幽灵条目。实测：**别名拼写完整绕过该教义**——cap 5 被静默改成 1000000，红变绿；CapsChecked 还从声明 2 条缩水为 1 条（恰是 A1-12 要杜绝的"已查假象"反向版）。诚实边界 #5/#6 均未覆盖此向量 | **攻击成功（实测）** |
| 8 | **Med-Low** | **「89 条定律」计数通胀**（声明保真度） | 实测 `dafny verify`（钉定版本 4.11.0+Z3 4.12.1）输出 `89 verified, 0 errors`；但逐条清点 `formal/*.dfy`：**lemma/theorem 合计 62 条**（46+16），其余 27 个"verified"是 datatype/function/predicate 的构造合法性义务 | 预期：README 徽章「Dafny 89 lemmas verified」= 89 条定律。实际：定律 62 条，89 是**声明计数**（这也是计数门只查 `≥89` 而从未查"哪些"的原因——门与修辞共用一个谁都不知道含义的数字） | **攻击成功（实测）** |
| 9 | **Med-Low** | **快照层覆盖缺口 + 重生成后门无 CI 反作弊**（六层之快照层） | ①快照仅覆盖 L1（43 类型）+Runtime（20 类型）两程序集——**Analyzer/Generator/Tool 三个发布程序集无任何公共面快照**（诊断 ID、CLI 退出码仅靠分散测试钉）；②`QedP1B1PublicApiSnapshotTests` 支持 `QED_REGEN_API_SNAPSHOT=1` 就地重写快照并转绿，而 `ci.yml` **无 dirty-tree 检查步骤**——PR 在 workflow 中注入该环境变量即可让"公共面变更必红"在单次 CI 中变绿且不留快照 diff（代码审阅须人肉补跑重生成） | 预期：「公共 API 面 43 类型快照钉死、升级兼容性可被机器断言」。实际：43 只钉住 5 个程序集中的 1 个；机器断言可被 1 个环境变量拆掉且 CI 无法发现 | **攻击成功（机制核实）** |
| 10 | **Med-Low** | **供应链层：形式验证门的 solver 无完整性钉**（六层之供应链层） | `ci.yml` L25：`curl -fL https://github.com/Z3Prover/z3/releases/download/z3-4.12.1/...zip`——**无 sha256/签名校验**，二进制在 CI 运行时从 GitHub release 拉取；`actions/checkout@v4` 等亦为 tag 非 SHA 钉。z3 是 Dafny 判定"0 errors"的唯一权威 | 预期：「`dafny verify` 0 errors 才放行」是机器权威。实际：被验证的"数学"最终由一个未校验完整性的运行时下载二进制裁决——release tag 重写/CDN 污染即可让形式门输出任何结果。配合 #1 的 grep 缺陷，形式层是双软门 | **攻击成功（静态核实）** |
| 11 | Low | **「一次发布永远不用更新」被冻结自锁证伪**（维度 5 汇总） | #3（round-trip 崩溃）、#4（schema/Parse 不同界）、#5（守恒域缺口）均为**非破坏性、必须修复**的缺陷；但其修复均需触碰冻结面：dialect（Parse 拒绝形状变化）、schema（加 maximum ⇒ version 递增）、Violation 语义（#5 的修复会改变 gate 输出）⇒ 按仓库自订 QED-E2/A4 均为 semver major | 预期：「一次发布永远不用更新」。实际：存在至少 3 个发布后**必须**发版的场景，且冻结条款使修复成本最大化（major 或违纪二选一）——「永不更新」的真义被反转为「不许好修」 | **攻击成功（推演+实测输入）** |
| 12 | Low | **`At`/`Audit` 同刻双口径与重复 Claim 防御的残余面**（维度 1 长枪） | C# 直构两事件各持同形 `create Memory(1) [5,5]`，`t=5` 并存：`At(5)` 投影 **1 份**（ImmutableHashSet 集合语义），`Audit` peak 计 **10**、且 create×create 报 CompatibleConflict | `Signature.Of` 正确拒绝重复（P0-4 ✓）；At/Audit 分歧为诚实边界 #5 明文冻结行为 | **防御生效（实测）** |
| 13 | Low | **`ulong.MaxValue` 幽灵点抑制、⊤ 混合端点扫描**（维度 1 长枪） | `[0, ulong.MaxValue]` 有限 hi + `[10,⊤]` open-end 混合剧本（`anyOpenEnd && maxFinite==MaxValue` ⇒ 幽灵点被抑制的极端形），cap=3 | 预期（攻击方）：幽灵点缺失产生漏检。实测：PeakExceeded 正确报出、闭包守恒正确 | **防御生效（实测）** |

## 攻击失败清单

| 攻击 | 失败原因（实测/核实） |
|------|------------------------|
| **Unknown+Unknown 对消掩蔽 Leak**：两个 `mode:"unknown"` occupy 试图伪装 create/release 配对 | `Audit` 的 net 贡献只对 `Mode.Release` 取负，Unknown 恒正 ⇒ +10+10=+20 ⇒ Leak 照报（实测）。README「Unknown 占用无 release 照常报 Leak」成立 |
| **`DrainTeardownBatch` 重入腐蚀**（逆 Action 内再调排空） | 队列与 `_queuedProviders` 在任务执行**前**清空，重入时 `Count==0` 早退；REG-01 陈旧任务防御 + `ReplayInProgress` 守卫拦住二次回放（代码路径核实） |
| **`BeginTeardown` 双重入队/对 Dead fiber 回放** | `TeardownEnqueued` 标志 + `State==Dead` 早退 + REG-01 丢弃防御成立（代码路径核实） |
| **重复 `Register` / 级联期 `Register` / 关路径 `LoadAll`** | 三处守卫（R7-L1 TryAdd、TearingDown 全表扫、IsShuttingDown）均在位——唯一被绕过的是 A4 的**标志位被早退路径复位**，守卫本身没坏 |
| **Unicode 规范化等价类碰撞**：`café` vs `cafe\u0301` 之类身份合并 | 资源/scope 身份全部 Ordinal 精确匹配，无 Normalize() 文字折叠；控制字符 U+0000–001F 在 `ReqStr`/`NonEmptyId` 双侧拒绝。唯一发现的等价类是 **memory 前导零**（`memory:007`≡`memory:7`）——已升级为成功条目 #7 |
| **`default(EffectEvent)`/`default(Budget)` 旁路构造** | `ValidateEvent` 在 `At`/`Audit`/`ComputeSamplePoints` 三入口统一复查（A1-02②）；`Budget.Caps` getter 单点归一（R4-RH-14）——struct default 后门已关 |
| **`NatStar`/`ZStar` 环绕算术假绿**（加/乘溢出、`sum<a` 环绕检测、`ZStar` 溢出检测位运算） | 在**定律覆盖域内**（≤2^63）逐式核验无误；域外行为见成功条目 #5/#6（那是域缺口问题，不是算术错误） |
| **扫换线相位序**（同刻 enter 先于 exit、`Lo=Hi` 零宽事件、exit@MaxValue 的 ⊤ 事件） | 采样点=全部有限端点 ⇒ 每个跳变点必被审计；相位拆分与 `Alive` 语义一致；多条随机/对抗形状等价钉（`Iter26_SweepLine_EqualsBruteForce_*`）与本次手工推导均未找到漏检形 |
| **ImmutableHashSet 枚举顺序不确定性** | `Signature` 相等为内容相等（SetEquals），哈希为顺序无关 XOR 折叠；唯一顺序敏感输出是 `ToJson` 的 claim 排列，而 round-trip 承诺是语义等价非字节等价 |
| **多线程腐蚀 Runtime** | 未攻击——诚实边界 #10 明文冻结「非线程安全、零锁、单线程契约」，属声明范围外（攻击它不构成对声明的反驳） |
| **`RecomputeTopology` no-op 占位** | §8 明文 deferred gap，非冻结声明范围 |
| **测试计数「739」从仓库可证伪** | 仓库内 doc-guard 明文禁止硬编码全量计数、README 声明计数以 CI 为准——该数字本身不构成可攻击的仓库内断言（ROADMAP 历史值 705→708→…漂移，恰证明"计数即契约"不成立，但无单一可判定断言可打） |

### 附：实测环境与可复现性

- .NET SDK 10.0.103；Dafny 4.11.0（`dotnet tool`）+ Z3 4.12.1（与 CI 钉定组合一致）。
- 维度 1/2/3 攻击经独立控制台工程（引用 `src/Cosmos.EffectAlgebra` 与 `src/Cosmos.EffectAlgebra.Runtime` 源工程）真实执行，输出均为程序实际返回值；CI 门攻击以仓库 `ci.yml` L32-38 的**逐字符同款管线**回放；`dafny verify` 基线实测输出 `Dafny program verifier finished with 89 verified, 0 errors`。
- 仓库未做任何修改；临时工程与 Dafny 注入副本已删除。

### 结论一句话

「89 定律 + 739 测试 + 43 快照 + 六层冻结」四件套里，**定律有域、门有洞、快照有界、冻结会自锁**——每一层都能拿出一个具体输入让"全绿"与"正确"分道扬镳；QED 声明应降级为「在定律覆盖域与已钉行为面内、以有缺陷门禁维护的工程纪律」，而非「一次发布永远不用更新」。
