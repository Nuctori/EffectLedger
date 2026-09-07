# QED ROADMAP —「一次发布永远不用更新」收敛路线

> 状态真源：每次迭代会话（含每日 23:00 定时任务）从这里恢复上下文。
> 规则：按阶段顺序 P0→P4，单次会话只做一个最小任务；TDD（可钉发现先红后绿）；
> `bash ci.sh` 全绿才提交/push；文档与代码同一提交；任何「不修」必须记录显式决策；
> 发布动作（nuget push / GitHub Release）永远留给人类，自动化严禁执行。
> 顺序神圣：语义先于 API，API 先于证明，证明先于冻结。

## 验收轴（终态定义）

1. **语义数学级正确（QED）**：L1 纯代数核心形式化验证；语义不对称全部定稿（对称化或 PDR 论证 sound-by-design + 测试钉）。
2. **足够内聚、API 面最小化**：公共面收缩到契约面（6 资源 × 4 scope × 4 Violation Kind）；一名一实；单一本体；公共 API 快照测试钉死不再膨胀。
3. **永续承诺就绪**：真 semver；死特性接线或砍；`PUBLISH-CHECKLIST.md` 就绪即停（发布留给人类）。

## P0 语义定稿（发布即永久的决策最先做）

- [x] **A1** `loop:"⊤"` vs `lifetime:[…,⊤]` 不对称定稿 ✅ 2026-09-05
  - **决策：sound-by-design（不修行为，修正框架）。** 旧 MA-002「同一常驻语义两种相反行为」表述不成立：
    两个 ⊤ 闭合**不同的轴**——`loop:"⊤"` 是 population-⊤（种群基数），事件列表结构性无法枚举 ⊤ 份配对
    release ⇒ 守恒对其无定义，豁免 gate(1)（gate(2) 峰值仍审计）；`lifetime.hi=⊤` + 有限 ω 是 time-⊤
    （时间轴永占），事件完整、release 可表达而缺席 ⇒ Leak（守恒权威判定）。豁免判据 =
    **配对结构性不可枚举**，非「语义常驻」。故意常驻资源以 `loop:"⊤"` 声明。
  - 改 `lifetime:[…,⊤]` 为豁免 = 自造假绿向量（否决）；删 ω=⊤ 豁免 = 常驻剧本永久红（OPEN-4 回归，否决）。
  - 证据：`tests/Cosmos.EffectAlgebra.Tests/QedP0A1SemanticDecisionTests.cs`（2 钉：time-⊤ Leak /
    双 ⊤ 并置对照）；`EFFECT_SCRIPT.md` §2.1 P0-A1 注记；README 锐边改写；`EffectScript.cs`
    Lifetime 注释「常驻层」误导措辞修正。
- [x] **A0**（前置闸门）iter55 PO 账本对账 ✅ 2026-09-05
  - **裁定：PO-55-01..18 三分完成——已 discharge 7 / 并入既有任务 11 / 显式不修 0。
    三个「阻塞级」均非 L1 代码缺口**（01/02：多重性合法载体=事件列表+`Combination.Loop` size×ω，
    集合边界由 P0-4 重复拒 loud 封死；03：`IncludedIn` 本就单向包含，Shell⊑Shell 由自反覆盖，
    仅 Loop⊑enclosing 真决策）。交付物：`audit/qed/PO55-TRIAGE.md`（逐项 file:line 证据 + 既有钉引用）。
  - 后续排序按其裁定执行：A5（01/02 PDR 推导）、A6（03c 决策+文档）、A8（04/07/10/11/12 补文档）、
    A3（05/06 定稿）、A7（08）、A9（09 源码复核）、A2（13 吸收）。
- [x] **A5** 多重性语义定稿（PO-55-01/02 同根）✅ 2026-09-05
  - **决策：sound-by-design（代码零改动，PDR 推导重写）。** 「Set<Claim> 刻意幂等 + 重复构造即拒
    （P0-4）+ 多重性唯一合法路径 = 事件序列逐条累加 / `Combination.Loop` size×ω」已写入 PDR：
    §3.1.4 多重性载体注记、§3.2.1 幂等注记、§3.2.5 弃 Σ-copies/max-over-copies 改 Scale(S,ω)、
    §3.3.1 Σ 作用域注记、§3.3.2 Peak 公式改缩放后逐条求和、AUDIT003 ×20 载体澄清。
  - 证据：`tests/Cosmos.EffectAlgebra.Tests/QedP0A5MultiplicityPins.cs` 8 钉（重复拒直连面 /
    Loop(20)⇒net=1280 / Peak 随 ω 线性 {1,2,5,20}→{64,128,320,1280} / 20 同构事件 cap=19 报·cap=20
    放行的逐事件计数）。对账依据 `audit/qed/PO55-TRIAGE.md` §2.1。
- [x] **A6** ScopeId ⊆* 自洽（PO-55-03）✅ 2026-09-06
  - **决策：sound-by-design（代码零改动，PDR §3.1.3b 修正）。** (a) 删「Global ⊑_any X」反向包含——
    双向包含违反反对称（iter55 F4），商集-预序方案因过滤谓词失去区分力被否决；(b) Shell⊑Shell 补表
    （实现由自反 Equals 覆盖）；(c) ⊆* 第二析取支冗余删除（X⊑Global 恒真已覆盖）；(d) Loop(id)⊑宿主
    **二选一定稿：维持跨标签不可比**——归因点在 `Combination.Loop` 的 loopScope 参数（宿主信息不丢失）、
    剧本层审计无 scope 过滤不受影响、加宿主链字段=公共类型面变更与 P1 收缩反向（否决，记录在案）。
  - 证据：性质钉已由既有 `ScopeOrderTests` 穷举覆盖（自反 8 标签/反对称 500 随机对含前提守卫/传递
    真链/Global 唯一最大元含单向性显式枚举/跨标签 8×8/Shell 不可比），无重复落钉；PDR §3.1.3b 全文
    引用该钉为单一真源。
- [x] **A7** 归一化实例身份（PO-55-08）✅ 2026-09-06
  - **决策：常量实例保守合并 = sound-by-design（已声明盲区，代码零行为改动）。** 折叠仅存在于
    §7 API 白名单层（`Callback("cb")`/`AudioMixer(0)`/裸名哨兵）——该层是静态近似，权威判定=
    运行期 Σnet（仓库既有宪法）；「Connect(sigA)+Disconnect(sigB) ⇒ net=0」的掩蔽属已声明契约。
    JSON 契约面（冻结核心）强制显式资源 id（memory 为非负整数 uid、拒裸名）⇒ 不同 id 即不同资源，
    **无折叠**。参数化 alias（按实参派生身份）归 F 轨候选，与 F1 流敏感化同窗评估——冻结前不实施。
  - 落点：PDR §3.1.4a【QED-A7】注记 + ApiMapping.Cb() 注释 + README 诚实边界 #20；
    钉 `QedP0A7AliasFoldingPins` 3 枚（白名单常量实例 net=0 / Connect↔Disconnect 同实例防静默漂移 /
    契约面显式 id 不折叠 Leak+NegativeDip 并存）。
- [x] **A8** 语义文档小项打包（PO-55-04/07/10/11/12）✅ 2026-09-06
  - **纯 PDR 编辑（代码零改动）。** ①§3.2.4 ⊔ 配对键改四元组投影（size 只参与 merge；修正自吞定义，
    实现真源 Signature.Join）；②§3.3.1 有符号区间值域与序（ZStar/SignedNet 单一真源：含 0⇔守恒、
    负陷按 hi<0、预算按上界）；③§3.2.4 Signature(b):=∅（纯谓词）；④§3.2.5 scope **替换语义**定稿
    （loopScope 参数显式选择——修正 A5 初稿「scope 不变」与代码 rescope 实况不符，PO-55-11 两读法
    歧义消解）；⑤纯编辑：§8.2 断表缝合（§8.3 移至 ED-008 后）、ED-004 ∞→[1,⊤]、术语表补
    Occupancy/Callback/Input/AudioMixer/Shell 并删裸 signal、双 ##14 修复（文档历史改列 §15 + rA7 行）、
    §3.1.2 双围栏。
- [x] **A9** release-class 清单权威性复核（PO-55-09）✅ 2026-09-06
  - **裁定：iter55 三处疑点全部坐实，ApiMapping 修正（本任务授权「错误归类即修」）。**
    godotengine 官方文档签名级证据：①`cancel_free`（4.2+）官方语义「Cancels any queue_free() call」=
    取消释放、节点存活——归 release-class 方向相反（emit release 掩盖其取消的泄漏路径）；
    ②`free_children_in_group` Node 公开 API 不存在（官方文档全文无此项，原「源码实测」不可证）；
    ③`remove_from_group` 纯组织性操作、组员关系非资源占用（emit release=凭空少计）。
  - 修正：`ReleaseClass.Names` 7→4（queue_free/free/remove_child/disconnect）；新增显式白名单条目
    `CancelFree` 按「重新占用」映射（与 QueueFree 逐资源对称，Release↔Create 配对恢复守恒语义）；
    PDR §8.1 块重写 + ApiMapping 注释证据化。
  - 钉同步（修正错误钉非削弱）：VerificationMatrixTests（4 项 + 三处 DoesNotContain 防回归）、
    CrossLayerTests（4 项 + IsRelease=false 三断言 + CancelFree 纯 Create 断言）、CrossTableTests
    （计数 4 + 软约束注释）、Round2AdversarialTests（注释收窄说明）。Runtime 反声明标签零影响
    （测试用 tag 均在保留清单内）。
- [x] **A2** `At(t)` 集合投影 vs `Audit` 扫换线双计数语义 ✅ 2026-09-06
  - **决策：钉死差异契约（sound-by-design，代码零改动）。** At=在场语义（Signature 刻意幂等 ⇒
    同刻同构事件计 1，K 无关）；Audit=计数语义（net/Peak 逐事件累加）。统一化（Signature→Multiset）
    已被 P0-4 构造期重复拒结构性封死——契约即「At 在场 / Audit 计数」，核对脚本禁用 At+Peak 对账。
  - PO-55-13 吸收：PDR §14.3 A1/A2 增「适用程序类」三元前提（单方法体直线控制流 / 事件单触发
    / 循环有界或显式 ⊤），前提不成立 ⇒ 降级 PARTIAL-COMPLETE 须人工确认；A2 保守侧（有界小循环
    也按上界报警）明示不声称不冤枉。A1 判定谓词同步 QED-A8 口径（闭包 net 不含 0 ⇒ Leak）。
  - 证据：`QedP0A2ProjectionContractTests` 5 钉（At 计 1 与 K∈{1,3,7} 无关 / 同剧本 K=3 峰值门
    cap=2 报·cap=3 放行）；README 诚实边界 #5 重写为正式契约。
- [x] **A3** `Unknown` 模式 fail-open 定稿 ✅ 2026-09-06
  - **决策：fail-open 保留（sound-by-design，代码零改动，PDR 四处修正）。** 三维契约——
    net/peak 按占用 +size 保守计入（未映射 API 的泄漏/峰值检测不静默；iter55 PO-55-06 的
    「贡献恒 0」是 PDR 旧公式 mode∈{create,move} 遗漏，修公式而非实现；「Unknown⇒⊤ 上界」
    否决：有限 cap 峰值门必爆=警报洪水）；Compatible 按 Use 最弱兼容放行（fail-closed 否决：
    未映射 API 是常态，逐一报冲突=警报洪水⇒批量 [EffectOverride]⇒工具失效；且 mode 未知时
    断言冲突是对未知命题下结论）；生命周期冲突（CONFLICT 集）为 A4 权威域。
  - PO-55-05 吸收：写写竞争归属显式声明移交 L2 写集分析（F 轨与 F2 同窗）；「修订 §7 写≠use」
    否决（须新增 Exclusive 类 mode=公共枚举/JSON 契约面/Compatible 表三重变更，跨语义域）。
    P4 自相矛盾措辞「fail-closed 为保守兼容」废止；§14.3 A4 域限定；§8.1「3.3.1 上界」失实叙事修正。
  - 证据：`QedP0A3UnknownSemanticsPins` 3 钉（Unknown 占用 Leak 照报 / 峰值计入 / 端到端无冲突
    警报且 Leak 并存）+ 既有 CompatibleMatrixTests 25 组合矩阵；README 诚实边界 #1 重写为三维契约。
- [x] **A4** CLI exit code 契约、四族异常方言表——定稿并声明 frozen ✅ 2026-09-06
  - **决策：冻结（自 2026-09-06，变更=semver major）。** CLI 退出码 0=通过/2=存在违例/1=解析或 IO
    错误（README ⑤ 契约表）；四族异常方言（README 诚实边界 #17）。补齐最后一枚行为钉——CLI 通过
    路径 exit 0 此前无钉（R3 仅钉 1/2 与 --out）；L1 两族方言单点冻结快照，Runtime 两族由
    Runtime.Tests 既有钉承载（跨工程不重复）。
  - 声明落点：README ⑤ 冻结框 + 诚实边界 #17 冻结标记 + EFFECT_SCRIPT §4 异常方言冻结行。
  - 证据：`QedP0A4ContractFreezePins` 2 钉（真实子进程 exit 0 + `"passed": true` 载荷 / L1 两族
    FormatException+ArgumentException 单点表）。**P0 语义定稿阶段（A0–A4、A5–A9）全部完成。**

## P1 API 面收缩

- [x] **B1** 公共 API 快照测试 ✅ 2026-09-06
  - **机制**：反射枚举 L1 与 Runtime 两个公共程序集的全部导出类型（类型头 + 公共成员块，序数排序
    确定性渲染），与仓库内冻结快照逐字节比对——任何公共面新增/变更/删除即红，差异信息直出。
    过滤编译器合成（`<` 名/`CompilerGenerated`/属性访问器行）；枚举值含常量。
  - 快照：`tests/Cosmos.EffectAlgebra.Tests/PublicApiSnapshot.Cosmos.EffectAlgebra.txt`（L1）
    与 `tests/Cosmos.EffectAlgebra.Runtime.Tests/PublicApiSnapshot.Cosmos.EffectAlgebra.Runtime.txt`
    （Runtime，125 行）。重生成：`QED_REGEN_API_SNAPSHOT=1 dotnet test`（写入后须人工审查 diff）。
    B2/B4 的公共面收缩将走「有意变更 + 快照同步重生成」流程——这正是本机制的预期用法。
  - 机制验证：篡改快照注入假成员 ⇒ 红；还原 ⇒ 绿（双向实证，非纸面钉）。
  - Analyzer/Generator/Tool 程序集不在本机制内：其消费面分别是 Roslyn 诊断（钉于 AnalyzerConsumer
    构建门 + AdvE2E）与 CLI 退出码契约（A4 已冻结钉）——面类型不同，机制不适用，记录于此。
- [x] **B2** `FakeHost` 移出公共面 ✅ 2026-09-06
  - **决策：整体迁入测试工程**（优于 internal+InternalsVisibleTo——发布程序集连 internal 足迹都不留）。
    使用面核查：仅 Runtime.Tests 两文件使用，生产代码零引用、样例不用。
    迁移：`src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs` → `tests/Cosmos.EffectAlgebra.Runtime.Tests/FakeHost.cs`
    （保持命名空间 `Cosmos.EffectAlgebra.Runtime`，既有测试代码零改动）。
  - 快照走 B1 流程：重生成后 diff 恰为 FakeHost 块 10 行删除（机制首次真实行使）。
  - 门禁 692 全绿，Runtime 套件零回归。
- [x] **B3** Sequence≡Parallel≡Union 砍到一名 ✅ 2026-09-06
  - **决策：删除 `Combination.Sequence`（纯 Obsolete 别名）与 `Combination.Parallel`（别名 + PARA_CONFLICT
    前置守卫），组合唯一入口 = `Signature.Union`。** 守卫随删的理由：冲突检测权威 = Audit gate(3)
    （25 组合矩阵 + 扫换线端到端钉），直连别名上的冗余守卫无人消费、徒增概念数；时序/并行真语义
    归 F 轨，不得以别名形态复活（DerivedMetrics 墓碑注释锁死）。
  - **「测试数只增不减」的显式例外（-3）**：Sequence_EqualsUnion / Parallel_EqualsUnion /
    Parallel_CreateCreate_Throws 三钉的被钉对象已按本决策删除——删的是死 API 的钉，非活语义的钉；
    gate(3) 的活冲突钉全部保留。快照 diff 恰为两名方法删除（B1 机制第二次行使）。
  - README 诚实边界 #1 标记已解决；PDR §3.2.1 的 QED-A5 幂等注记已先行声明唯一入口。
- [x] **B4a** ScopeId 超集 internal + IVT ✅ 2026-09-06
  - **决策：`ScopeId.Shell/Loop/Conditional/Async` 转 internal**（非 JSON 契约面：契约 4 scope=
    scene/method/type/global）。§7 白名单层（同程序集）不受影响；仓库内测试（L1 14 文件 +
    Runtime.Tests 8 文件）与 SampleGame（5 文件）经 csproj 级 `InternalsVisibleTo` 授权
    （Tests/Runtime.Tests/SampleGame 三友元）——IVT 暴露的是测试便利而非公共契约（公共面以
    QedP1B1 快照为准）。快照 diff 恰为 4 类型 11 行删除（B1 机制第三次行使）。
  - 侦察结论（B4 拆分依据）：①契约面 Gpu(Rid)/SignalBus(StringName) ⇒ Rid/StringName 必须保持
    public；②生成器 emit 仅引用 Signature.Union/Of ⇒ internal 化不破坏消费方编译；③Runtime
    程序集零超集使用；④样例 5 文件 + L1 测试 14 文件 + Runtime.Tests 使用超集 ⇒ 需 IVT。
- [x] **B4b** ResourceId 超集 internal ✅ 2026-09-06
  - **决策：Tree/Self/Physics/Disk/Signal/AudioMixer/Callback/Network/Input/NodePathOrUnknown 转
    internal**（非 JSON 契约面）；契约面 6 资源（Memory/Gpu/Occupancy/Custom/CommandBuffer/SignalBus）
    + Rid/StringName（契约构造子签名可达）保持 public。与 B4a 合并生效后诚实边界 #18 消失
    （外部消费者只能构造契约面，「超集可审计但 ToJson 抛」分叉不复存在）——README #18 标记已解决。
  - 快照 diff 恰为 10 类型 33 行删除（B1 机制第四次行使）；公共面收敛至 43 类型。全解构建零错误
    （IVT 覆盖验证：L1 测试/Runtime.Tests/SampleGame 均正常编译运行）。**B4 全部完成。**
- [x] **B5** TFM 分歧消除 ✅ 2026-09-06
  - **决策：包族收敛 `net8.0;net10.0`（同包全 API，「拆包」方案否决）。** L1 的 net9.0 缩减切片
    （无 EffectScript/DSL）为分析器源内嵌时代（CS8032）而设，A2-06 分析器自包含后成残迹——
    同一包 ID+版本在不同 TFM 暴露不同 API 的分歧类整体消失（诚实边界 #13 解决，README 标记）。
    L1/Generator/Runtime 三包同双目标；net9 消费者按 NuGet 就近原则消费 net8.0 资产（覆盖面不变）；
    分析器包保持 net9.0（编译器宿主对齐，自包含、非消费 TFM）。
  - R3-CG-04 钉更新为新不变量（断言 TargetFrameworks 元素恰为 `net8.0;net10.0`——首版
    `DoesNotContain("net9.0")` 误扫注释字符串被门禁抓回，修正为只认元素本身）。
  - 全 TFM 同一公共面，QedP1B1 快照唯一准绳。**P1 API 面收缩（B1–B5）全部完成。**

## P2 死特性处置

- [x] **C1-决策** `cosmos.effect.json` 二选一定稿：**WIRE（真接线 AdditionalFiles）** ✅ 2026-09-06
  - **理由**：编译期白名单扩展只有 AdditionalFiles 一条路（分析器/生成器在编译期加载，注册表
    API 无代码可运行——死路）；不接线则每个未映射 API 的用户都产生发版压力，与适配层
    「库少发版」承诺直接冲突。解析/校验层已就绪（`CosmosEffectConfig.LoadExtraFromJson`
    → `ImmutableArray<ApiMapping>`，有测试）；缺的只是 L2/L3 消费钩子（「存在但不生效」即 #12）。
  - 侦察结论：①分析器静态字典消费 `GodotApiWhitelist.All`（EffectAlgebraAnalyzer.cs:111-118），
    `RegisterCompilationStartAction` 的 `Options.AdditionalFiles` 可达 ⇒ per-compilation 合并视图
    可行；②生成器目前无 AdditionalTexts 管线 ⇒ 需增量改造（缓存键含配置文本）。
  - **拆分执行（顺序神圣）**：
    - [x] **C1a** L1 增合并视图 helper ✅ 2026-09-06
      `GodotApiWhitelist.MergedWith(extra)`（**internal**——消费者仅 L2/L3 ShareSource 源副本与
      IVT 测试，外部用户只写配置文件不调 API，公共面零增长、QedP1B1 快照不动）：不可变合并视图
      + 碰撞 loud 语义（合并集内任何 Canonical 同键——扩展 vs 基础表/扩展彼此/精确重名——
      ⇒ InvalidOperationException，R3-L1-03 教义）。钉 `QedP2C1aMergedWhitelistPins` 4 枚
      （含基础表不可变验证与三类碰撞）。
    - [x] **C1b** L3 分析器消费 AdditionalFiles ✅ 2026-09-06
      **决策：per-compilation 合并快照 + 配置错误 loud（新诊断 EAA0701，id 属公共契约面，2026-09-06 登记）。**
      静态字典（A2-07 ByFullCanon/ByMethodCanon）退役——Initialize 改走 CompilationStartAction：
      Options.AdditionalFiles 中文件名为 `cosmos.effect.json` 的文件（精确名、大小写不敏感、目录不限、可多份——
      解决方案多消费工程各贡献一份的真实形态）经共享 CosmosEffectConfig.LoadExtraFromJson 严格解析 →
      GodotApiWhitelist.MergedWith 合并 → 本次编译查找表；无文件时回基础表，与旧静态路径行为一致（全量门为证）。
      失败语义：解析/schema 错误 ⇒ EAA0701 定位该文件、该文件扩展整体弃用（基础表不受影响，宁缺勿假）；
      Canonical 碰撞（vs 基础表/跨文件）⇒ 合并集整体回退基础表（无部分生效），诊断定位首个配置文件，
      消息自含碰撞双方 API 名（MergedWith loud 语义透传，QED-C1a）。System.Text.Json 为 net9.0 BCL 内箱
      （宿主前提不变 R2B-02，真实构建门实证解析）；共享副本 LoadExtra/AllWithExtra（进程内文件 IO，
      分析器不调用）按 RS1035 精确范围禁用，解析/校验成员保持守护。
      证据：`QedP2C1bAdditionalFilesPins` 5 钉（扩展 API 报漏与基础表并存 / 无文件不读盘+扩展静默契约 /
      坏 JSON loud+扩展弃用+基础照报 / vs 基础表碰撞回退 / 跨文件碰撞归属首文件）+
      `tests/GateFixture/ExtendedWhitelist` 真实构建门（AdditionalFiles 真接线 mutation 门：接线被拔即红；
      扩展 API EAA0901=error 构建红 + ExtPaired 不误报 + 有效配置零 EAA0701）。门禁 699 全绿（+6），
      pack 五包走查通过。EAA0701 已登记 README ② severity 六行/能力表/③ 扩展说明/诚实边界 #12
      （L3 半边接线）+ templates/README；文档全量收口（③ 真接线章节重写 + #12 关闭）按拆分归 C1d。
    - [x] **C1c** L2 生成器消费 additionalTextsProvider ✅ 2026-09-06
      管线：additionalTextsProvider（文件名 cosmos.effect.json）→ 文本 Collect → 与标注方法 Combine
      （缓存键含配置文本，文本变更⇒重跑）；解析/碰撞失败（FormatException / MergedWith
      InvalidOperationException）⇒ EAA0701（generator 侧同契约 ID 描述符，与 L3 侧 QED-C1b 同语义：
      该文件扩展整体弃用、基础白名单不受影响、绝不静默）且扩展-only 方法回落基础路径。
      扩展-only 方法 emit 字面量 Claims（配置解析限契约面 6 资源 × 4 scope，全 public 类型消费方可
      构造；运行期枚举的 All 不含扩展项，字面量内嵌是唯一正确形态）；基础表命中方法维持运行期
      枚举 body（零变化）。
      实现坑（记录）：①生成器工程经 ProjectReference 可见真实 L1，其命名空间是本文件命名空间的
      父级 ⇒ L1 来源类型必须以 Cosmos.EffectAlgebra.Generator.Shared. 前缀完全限定（父命名空间解析
      优先于 using，裸名会静默绑定真实 L1 类型）；②ShareSource 增量缓存需清 obj 才刷新；
      ③RS2008 随分析器侧同款 NoWarn；④ApiMapping 为 struct ⇒ TryGetValue 后用 bool 标记非空。
      钉 QedP2C1cGeneratorAdditionalFilesPins 3 枚（扩展-only 字面量 emit + 基础不受影响 /
      坏配置 EAA0701 loud + 基础路径保留 / 无配置零诊断原行为）。门禁 702 全绿（+3）。
    - [x] **C1d** 文档收口 ✅ 2026-09-06：README ③ 改全链路真接线说明（L3 诊断 + L2 字面量 emit、
          EAA0701 双侧同契约 ID、模板指引）；诚实边界 #12 关闭（钉 5+4+3 + 真实构建门）；
          templates/README 白名单扩展节同步全链路状态；新增 templates/cosmos.effect.json 可复制样板。
          **C1 全部完成（决策 WIRE + C1a/C1b/C1c/C1d）。**
- [x] **C1** `cosmos.effect.json` 真接线 ✅ 2026-09-06（见 C1-决策 + C1a–C1d）。
- [x] **C2** `ResetDiagnostics` 生产接线或砍 ✅ 2026-09-06
  - **决策：第三条路——结构性有界（两端否决）。** 自动接线（排空完成点自动清诊断）否决：摧毁宿主
    「卸载后轮询 LastCrashReport」观测契约（自动清 = 删除未读证据）；砍除否决：长会话宿主对
    _netAccum 跨批次增长彻底无解。落地：①`CrashReports` 环形上限恒保留最近 64 条（last 语义不变，
    5 个 Add 点收口单点 helper）；②`_netAccum` 在 DrainTeardownBatch/SynchronousExitDrain 完成
    点自动剪除非 Active Fiber 条目（零语义损失：Active 过滤器永久跳过 + 同 FiberId 不可重注册）；
    ③`ResetDiagnostics` 降级为宿主可选显式整体清空出口（XML doc 同步），非内存安全义务。
  - internal 观测口 NetAccumEntries/MaxCrashReports（Runtime.Tests IVT，公共快照不含）。
  - 钉 `QedP2C2DiagnosticsBoundPins` 3 枚（66 崩溃恒 64 条 + last 语义 / 排空剪除死 Fiber 条目 /
    剪除不影响 Active 泄漏判定）。README 诚实边界 #19 关闭。**P2 死特性处置全部完成。**

## P3 形式化验证（QED 主线）

- [x] **D1** 选型 + 最小切片验证 ✅ 2026-09-07
  - **选型：Dafny 4.11**（dotnet tool 安装）。理由：语义贴近 C#（纯函数 + 前置/后置条件），
    ulong 溢出⇒⊤ 的保守闭合作数学模型可直接表达；Lean4 表达力更强但与 C# 无直接通道、建模范式
    成本高一个量级。求解器 Z3 4.12.1（GitHub release，置于 ~/.dotnet/tools/z3/bin/）。
  - **形式规约**：`formal/CosmosEffectAlgebra.dfy`（被验证的可执行规约，D5 的 oracle）。
    NatStar：⊤ 闭合（Add/Mul absorbing）、交换律（含溢出分支）、溢出⇒⊤ 直接形态；
    全序（自反/反对称/传递/total）；Interval：lo≤hi 不变量、Default[1,1] 合法、
    Merge 保持 Valid、Merge 幂等/交换/结合（join-semilattice）。
  - **证据**：`dafny verify --solver-path ~/.dotnet/tools/z3/bin/z3-4.12.1.exe formal/CosmosEffectAlgebra.dfy`
    ⇒ **23 verified, 0 errors**。工具环境：dafny 4.11（dotnet tool）+ z3 4.12.1（非 dotnet tool 捆绑，
    需手动放置，`--solver-path` 显式指定最稳）。
  - CI 接线（dafny verify 入 ci.sh）按拆分归 D5。
- [x] **D2** ScopeId ⊆* 偏序 + Compatible 全函数 + 对称律 ✅ 2026-09-07
  - **形式规约**（`formal/CosmosEffectAlgebra.dfy` D2 段，C# 零改动——既有钉已覆盖的行为升级为全称定理）：
    ①ScopeId 8 构造子（契约面 4 + 超集 4，可见性与偏序律正交不入模）datatype 结构相等 =
    C# record Equals；`Leq` = `this == other || other.Global?`（Objects.cs IncludedIn 逐字对应，
    QED-A6 单向包含定稿的机器形态）。定理：自反 / 反对称 / 传递 / Global 唯一最大元
    （`Global ⊑ X ⇒ X = Global`——iter55 F4 双向包含违反反对称的收口）/ 可比对恰为
    `{(x,x),(x,Global)}`（「机械查表无未定义项、跨标签不可比」的全称形态）。
    ②Mode 五值 datatype + `Resolve(Unknown)=Use`（QED-A3）+ `InConflict` 闭合式
    （解析后同非 Use 对角）+ `IsCompatible` 正枚举（Algebra.cs 逐条对应；Dafny 函数天然全定义
    = P2「25 对全覆盖无未定义项」的构造事实）。定理：闭包式 `Compatible ⟺ ¬CONFLICT`
    （PDR §3.2.3「闭合性可机械验证」的机器证明）/ 对称律 / Unknown ≡ Use /
    create+release 良性配对 / CONFLICT 恰为非 Use/Unknown 对角三对。
  - **证据**：`dafny verify` ⇒ **33 verified, 0 errors**（D1 基线 23 + D2 新增 10）。
    变异检查（防纸面绿）：`InConflict` 去掉 `!= Use` 守卫 ⇒ 31 verified 2 errors
    （ClosedForm/ConflictIsDiagonal 如期红），回滚 ⇒ 33 verified 0 errors。
  - C# 对应钉（不重复落钉）：`ScopeOrderTests`（8 标签穷举）/ `CompatibleMatrixTests`
    （25 组合矩阵）/ `QedP0A3UnknownSemanticsPins`——采样钉守实现漂移，定理守全称律；
    二者合流于 D5 的逐条对照。
- [x] **D3** SignedNet 守恒律（区间含 0 ⇔ 守恒）✅ 2026-09-07
  - **形式规约**（`formal/CosmosEffectAlgebra.dfy` D3 段，C# 零改动）：
    ①ZStar = ℤ ∪ {⊤}（long 值域显式入模 LONG_MIN/MAX）：⊤ 吸收、溢出⇒⊤ 直接形态（R4-F1）、
    ZAddPreservesLegal（加法不产出非法态）、交换律、单位元；
    ②SignedInterval：lo≤hi 不变量、ContainsZero ⇔（两端有限 ∧ lo≤0≤hi）、⊤ 端 fail-closed、
    AddZ 真求和 + 区间级交换；
    ③NetTable.ToSigned/Negate 转换模型（端 ⊤/超 long 域 ⇒ TopZ）+ 转换保合法；
    ④守恒律本体：create/release 精确配对 ⇒ net 含 0（ExactPairingContainsZero）、同 size
    配对坍缩 [0,0]（ExactPairingNetsZero）、「区间含 0 ⇔ 守恒」全 ⇔ 含 Missing/⊤ 双
    fail-closed 闸（ConservationIff）。
  - **发现（形式化即审计）：朴素结合律在溢出保守代数中为假**——全合法输入反例
    `ZAdd(ZAdd(MAX,MAX),−MAX)=⊤` 而 `ZAdd(MAX,ZAdd(MAX,−MAX))=FinZ(MAX)`（一序中途溢出
    得保守 ⊤，另一序得精确有限和）。以可靠形态定理替代：FoldZSound（折叠结果要么 ⊤ 要么
    恰为精确数学和 ⇒ ContainsZero 判定在任意 AllClaims 迭代序下可信）+ FoldZTopOnTotalOverflow
    （真和越 long 域 ⇒ 任何折叠序都得 ⊤ ⇒ fail-closed 恒检出）+ ZAddAssociativeNoOverflow
    （无溢出域内结合律成立）。净效应：NetTable.Compute 的迭代序无关性取「判定可靠性」形态
    而非「同值」形态——行为 sound（有限⇒精确、越域⇒报警、无静默回卷），无需改代码；
    序间分歧仅现于求和超 ±2^63 的非现实规模。工具注记：Dafny 4 对 nat 一元负号仍按子类型
    检查（`FinZ(-v.n)` 编译期红），规约侧以 `0-(v.n as int)` 显式放宽。
  - **证据**：`dafny verify` ⇒ **67 verified, 0 errors**（D2 基线 33 + D3 新增 34 义务），
    连续三次全新进程运行稳定（背靠背连跑曾现驻留进程计数伪影 76，以干净进程数为准）。
    变异检查：ConvNeg 去掉取负（release 记正号）⇒ NegSignedValid / ExactPairingContainsZero /
    ExactPairingNetsZero 等 4 义务如期红，回滚 ⇒ 复绿。
  - C# 对应钉（不重复落钉）：`NetTableSignedTests` / `CrossTableTests`——采样钉守实现漂移，
    定理守全称律；合流于 D5。
- [x] **D4a** 扫换线等价性·阶跃函数核心 ✅ 2026-09-07
  - **形式规约**：`formal/CosmosSweepLine.dfy`（独立模块，与 D1-D3 文件并列）。事件模型 =
    有限寿命 [lo,hi]（含闭存活，与 C# Alive 一致）+ 贡献在自身 lo 处入账（release 负贡献同点入账）
    ⇒ **net(t) = Σ_{lo≤t} contrib 为纯阶跃函数，只在 lo 端点跳变**。
  - 四条核心引理（全部机器验证）：①AliveSetAgree——[u,v] 无端点跨越 ⇒ 存活集恒定；
    ②NetAtAgree——(u,v] 无 lo 端点 ⇒ 累积净额恒定（NegativeDip 段首采样可见性，不漏报）；
    ③AliveSupersetOnSegment——段内存活集 ⊆ 段首样本存活集（峰值/冲突 gate 以超集保守评估，
    不漏报）；④NetAtSampleCoversSegment——段首样本净额精确等于段内净额（精确评估）。
    这四条即「在全部端点采样 ≡ 在所有时刻审计」的数学心脏。
  - 证据：`dafny verify` ⇒ **5 verified, 0 errors**（本文件；全库累计 75 verified）。
  - **拆分余项**：D4b ✅（见下）/ D4c ✅（见下）/ D4d ✅（与 D5 合并完成，见下）。工具环境见 D1 条目。
- [x] **D4d + D5** C# 实现对照 + 形式验证入 CI ✅ 2026-09-07
  - **实现对照**（`tests/Cosmos.EffectAlgebra.Tests/QedP3D5ConformanceTests.cs`，5 测试）：
    oracle = Dafny 模型的**独立 C# 直译**（数学域判定而非环绕检测——两侧实现技巧刻意不同，
    吻合即「实现 == 规约」的证据，分歧即反例直报）。覆盖：①NatStar Add/Mul（⊤ 输入 × 边界
    偏置集 10 值 × 随机 100 + 交叉）；②Interval Merge（随机合法区间对 200 组，含不变量保持
    断言）；③ScopeId IncludedIn（8 构造子全对 64 组合）；④Compatible 25 组合（Unknown→Use
    + CONFLICT 直译）。反例收缩：失败即打印精确输入对（固定种子 23 可复现）。
  - **CI 接线**：`dafny verify`（两个 .dfy，0 errors 才放行）入 ci.sh/ci.ps1（DAFNY_Z3 可覆盖）
    与 ci.yml（新增 Install Dafny + Z3 / Dafny verify 两步骤，ubuntu 用 glibc-2.35 资产）。
  - 证据：`QedP3D5ConformanceTests` 5/5 绿；门禁含 dafny 步骤全绿（**全库 89 verified + 705 测试**）。
  - **P3 形式化验证全部完成**：D1/D2/D3/D4a/D4b/D4c/D4d+D5。
- [x] **D4c** 扫换线增量维护 == 阶跃定义 ✅ 2026-09-07
  - **定理**（`formal/CosmosSweepLine.dfy`）：①增量律 NetAtAddEventEnter/Future——追加事件
    e：lo ≤ t ⇒ 净额恰增 e.contrib；lo > t ⇒ 净额不变（扫换线 enter 处理器的数学内容，
    增量累加无遗漏无重复）；②AliveSetAddEvent——存活集同步并入/忽略（gate2/3 的算法面同构）；
    ③SweepAccMatchesNetAt——前缀增量累加（算法形态）与定义和（语义形态）逐前缀一致；
    ④SweepTotalMatchesNetAt——全量累加 == 定义和（扫换线终态 == 逐点定义任意 t）。
    实现提示：前缀的前缀恒等、全前缀切片==原序列、前缀元素必属全序列（PrefixElementIn）。
  - 证据：`dafny verify` ⇒ CosmosSweepLine.dfy **22 verified, 0 errors**（全库累计 89）。
- [x] **D4b** 三 gates 段覆盖定理 ✅ 2026-09-07
  - **定理**（`formal/CosmosSweepLine.dfy`，段 (a, u] 内无端点的前提下，任意时刻 u 的三类违例
    都在段首样本 a 处可见——「全端点采样不漏报」的 gate 级完整化）：
    ①gate(1) SegmentNetCovered：NetAt(a) == NetAt(u)（**精确**，D4a NetAtAgree 推论）；
    ②gate(2) SegmentPeakCovered：PeakAt(u) ≤ PeakAt(a)（**超集保守**——存活集 ⊆ 样本存活集
    + 权重 ≥ 0 单调；Ev 增补 w/r/m 三字段承载 peak/conflict 维度）；③gate(3)
    SegmentConflictCovered：AliveConflictAt(u) ⇒ AliveConflictAt(a)（**见证迁移**——冲突对
    e1/e2 ∈ AliveSet(u) ⊆ AliveSet(a)，配对条件与时刻无关）；④总纲 SegmentViolationsCovered
    合成三式。
  - 形态要点：gate(3) 用蕴含签名（冲突存在性是保证不是前提）；见证提取用 `var e1, e2 :|`
    多绑定（嵌套 `var :|` 表达式非法）。Mode/Compatible 复用 D2 定义（`import opened` 置于
    模块体内——文件级导入对内嵌模块不可见；冲突谓词名为 InConflict）。
  - 证据：`dafny verify` ⇒ CosmosSweepLine.dfy **14 verified, 0 errors**（全库累计 81）。
- [x] **D5** C# 实现与形式规约逐条对照的性质测试；证明产物纳入 CI 门禁。（✅ 已完成，执行记录见上方「D4d + D5」条目——蓝图区本行仅为任务定义存档）

## P4 冻结与发布就绪（终态）

- [x] **E1** semver 单一真源流水线 ✅ 2026-09-07
  - 五处 csproj 写死的 `<Version>1.0.0</Version>` 全部删除，上收至 `Directory.Build.props`
    单一声明（strict semver 三段数字）。发布流程 = 改这一行 + 走 PUBLISH-CHECKLIST（E4）；
    版本一旦变更即新 NuGet 缓存键——同 id+version 本地重打包缓存陷阱（README ⓪）随发布纪律根除。
  - 钉 `QedP4E1VersioningPins` 3 枚：①BuildProps 恰一 Version 且 strict semver；
    ②src 下任何 csproj 私藏 Version 即红（防漂移）；③五包 PackageId 清单冻结（包族成员不可静默增删）。
  - pack 走查：`dotnet pack` ⇒ `Cosmos.EffectAlgebra.1.0.0.nupkg`（单一真源流出验证）。
    首发版本号决策（仍 1.0.0 或直接 1.x）按 PUBLISH-CHECKLIST 在发布时由人类定夺。
- [x] **E2** JSON schema 版本字段 + 契约面冻结 ✅ 2026-09-07
  - `docs/effect-script.schema.json` 顶层注入 `version: 1.0.0` 与 `x-contract-frozen` 冻结声明
    （6 资源 × 4 scope × kind 3 × mode 5；变更 = semver major + version 同步递增）。
  - 钉 `QedP4E2SchemaFreezePins` 4 枚：版本标记存在 / 资源面恰 6 键 / scope 面恰 4 键+type 枚举 /
    kind+mode 枚举冻结（导航断言精确到 schema 节点）。模板可解析性由既有 DocGuard A1-03 承载。
- [x] **E3** README 诚实边界逐条复核 ✅ 2026-09-07
  - 20 条逐条过检：6 条已解决就地标记（4 条划线：#1/#13/#18/#19；2 条改写扩充：#5→A2 / #12→C1b,c）——原文「划线标记」措辞以此为准（#1 四名一实→B3 / #5 双语义→A2 / #12 白名单
    接线→C1b/c / #13 TFM→B5 / #18 契约面→B4 / #19 诊断有界→C2，各附修复证据）；
    14 条活跃语义各附测试钉/文档引用（#2 DO-1 锁 / #3 D08 等价钉 / #7⑨⑩ 运行期权威宪法 /
    #11 两层口径 / #14 R3-CG-07 / #16 性能曲线钉 / #17 方言冻结→A4 / #20 常量合并→A7 等）。
  - **决策：编号即契约身份永不重排**（`#10`/`#11`/`#20` 等交叉引用遍布代码注释与文档）——
    「已解决的删除」调整为就地划线保留；新增边界只在尾部追加新编号。节头已声明 E3 复核完成。
- [x] **E4** `PUBLISH-CHECKLIST.md` 就绪 ✅ 2026-09-07（发布动作留给人类——使命条款）
  - 八节清单：①版本决策（Directory.Build.props 一行 + semver 规则）②前置三门（ci.sh 含
    dafny 89 定律 / pack 五包 / GitHub CI 绿）③契约冻结核对（快照 43 类型 / schema 面 /
    方言表 / 退出码）④发布物核对（五 nupkg + 依赖闭包）⑤nuget push（人类）⑥GitHub Release
    （人类）⑦发布后文档同步 ⑧维护模式启用（快照红/契约红/dafny 红 = 必须 semver major）。

---

# ═══ QED 路线全部完成（2026-09-07）═══

## P5 产品化与对抗审计（收官加强，2026-09-08）

- [x] **P5.1 文档产品化** ✅ 2026-09-08：README 头部徽章 + 价值主张四点 + 文档地图
  （10 项导航表）；去陈旧计数锚点（"95 Runtime" 已漂移为 120，改为 CI 汇总口径）；
  EFFECT_SCRIPT.md 产品导读（契约定位 + 配套物）。全部 doc-guard 钉（18 测试）保持绿。
- [ ] **P5.2 对抗性审计会议**（双审计员并行：外部新消费者视角 / 冻结承诺红队视角）
  → 会议纪要与处置表落 `audit/p5-adversarial-meeting.md`。

P0 语义定稿（11 项）/ P1 API 收缩（5 项）/ P2 死特性处置（2 项）/ P3 形式化验证（D1–D5，
89 定律 0 errors + 实现对照 5 测试 + CI 门禁）/ P4 冻结清单（E1–E4）——全数收口。
**「一次发布永远不用更新」的承诺自 PUBLISH-CHECKLIST.md 交付起可对人类作出。
后续进入维护模式（见下）；新特性走 F 挂起轨（F1/F2），触发条件 P4 E4 后。**

## 维护模式执行记录

- [x] **M0 维护模式启动 + fuzz 装置上架** ✅ 2026-09-07
  - 常驻装置：`tests/Cosmos.EffectAlgebra.Tests/QedMaintFuzzTests.cs`——每晚自动执行
    （种子=UTC 日期，同日重跑完全可复现；400 剧本/晚 = ~340 合法形状打深层 + ~60 畸形验证方言）。
    四不变量：①合法形状必可 Parse（畸形仅 FormatException）②Audit 永不抛异常
    ③两次运行违例序列确定 ④ToJson→Parse 往返闭合。
  - 首轮战果（装置自证有效）：fuzzer 语料连踩三条契约约束被方言防线逐一拦下——
    memory 字符串值（须非负整数）/ Read+Create（Kind×Mode 铁律）/ size lo>hi（§3.1.5）/
    claim scope 与 event scope 不一致（R3-L1-07 单一真相）——语料修正后 400/400 全绿。
    **四道防线（方言/守恒/确定性/往返）无一被击穿。**
  - 门禁：708 测试全绿（705→708，+fuzz）。CI（ubuntu）行为一致性待推送后观察。

## Feature 挂起轨（QED 主线之外，禁止插入 P0→P4）

> 依据「语义先于 API」：特性在语义冻结（P0 完）+ API 快照（B1）落地前不排期、不实现；挂起即显式决策。

- [ ] **F1** EAA0901 流敏感化：逐值状态机（Unacquired→Acquired→Released；二次 release / 用后即用报错），缩小自认静态盲区（跨方法仍需过程间分析，分两步）。触发：P4 E4 后。
- [ ] **F2** 可选精化类型（检查式·方案 A）：封闭谓词词表（数据非 lambda，保 L1 纯数据可审计）/ ⊤ 不可被标注收窄（非常量 ⇒ Runtime 谓词断言面兜底）/ 禁豁免 EAA0901 禁触 DO-9 / 附着点限 Normalize 后键空间 / fail 方向逐边界显式声明。前置依赖：B4 公共面收缩先行（先收缩再冻结，避免精化标注冻结在超集面上）。触发：P4 E4 后。

## 维护模式（P4 完成后）

每晚对抗模糊测试（随机剧本 fuzz + 变异门 + 性能曲线钉 `ProdAuditR4AuditScaleTests`），回归即修。

## Blockers

（无——PUSH-PENDING 已于 2026-09-06 网络恢复后清空：B3/B4a 及全部积压提交已上远端。）
