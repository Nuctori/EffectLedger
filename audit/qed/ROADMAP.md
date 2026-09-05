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
- [ ] **A3** `Unknown` 模式 fail-open——改 fail-closed 默认 + 显式 opt-in，或 PDR 论证保留；吸收 PO-55-06（net 的 Unknown ⊤ fail-closed 分支随之定稿）与 PO-55-05（§7 write 多标 use 掏空 CONFLICT 集：修订 mode 赋值或显式声明冲突检测移交 L2 写集分析）。
- [ ] **A4** CLI exit code 2 契约、四族异常方言表——定稿并声明 frozen。

## P1 API 面收缩

- [ ] **B1** 公共 API 快照测试（快照文件入 tests/，任何公共面新增即红）——永续承诺的执行机制。
- [ ] **B2** `FakeHost` 移出公共面（internal 或移入测试工程；现位于 `src/Cosmos.EffectAlgebra.Runtime/GodotShell.cs:83`）。
- [ ] **B3** Sequence≡Parallel≡Union 四名一实——砍到一名（尚未发布，无需 Obsolete 过渡）。
- [ ] **B4** C# 超集本体 vs JSON 契约面——公共类型砍到契约面，超集转 internal（诚实边界 #18 随之消失）。
- [ ] **B5** TFM 分歧消除：net9.0 切片缺 EffectScript（诚实边界 #13）——同包全 API 或拆包，二选一并记录理由。

## P2 死特性处置

- [ ] **C1** `cosmos.effect.json`：L2/L3 经 AdditionalFiles 真消费（外部用户自助扩展白名单、库少发版的
      关键杠杆），或正式砍掉并删 `LoadExtra` API——不许保持「存在但不生效」（诚实边界 #12）。
- [ ] **C2** `ResetDiagnostics` 生产接线（长会话自动调用点）或砍（诚实边界 #19）——二选一。

## P3 形式化验证（QED 主线）

- [ ] **D1** 选型 Dafny 或 Lean4；验证最小切片：NatStar ⊤ 闭包 + 溢出⇒⊤；Interval 不变量
      （lo≤hi / Default[1,1] / Merge join-semilattice）。
- [ ] **D2** ScopeId ⊆* 偏序 + Compatible 全函数 + 对称律。
- [ ] **D3** SignedNet 守恒律（区间含 0 ⇔ 守恒）。
- [ ] **D4** 扫换线 == 暴力扫描等价性——把现有随机等价钉（`Iter26_SweepLine_EqualsBruteForce_*`）升级为定理。
- [ ] **D5** C# 实现与形式规约逐条对照的性质测试（反例 shrink）；证明产物纳入 CI 门禁。

## P4 冻结与发布就绪（终态）

- [ ] **E1** 写死的 1.0.0 改真 semver 流水线（根除 NuGet 同 id+version 本地重打包缓存陷阱）。
- [ ] **E2** JSON schema 加 `$schema` + 版本字段并冻结契约面。
- [ ] **E3** README 诚实边界逐条复核：已解决的删除、保留的附测试证据。
- [ ] **E4** `PUBLISH-CHECKLIST.md` 就绪后【停】——发布动作留给人类。

## Feature 挂起轨（QED 主线之外，禁止插入 P0→P4）

> 依据「语义先于 API」：特性在语义冻结（P0 完）+ API 快照（B1）落地前不排期、不实现；挂起即显式决策。

- [ ] **F1** EAA0901 流敏感化：逐值状态机（Unacquired→Acquired→Released；二次 release / 用后即用报错），缩小自认静态盲区（跨方法仍需过程间分析，分两步）。触发：P4 E4 后。
- [ ] **F2** 可选精化类型（检查式·方案 A）：封闭谓词词表（数据非 lambda，保 L1 纯数据可审计）/ ⊤ 不可被标注收窄（非常量 ⇒ Runtime 谓词断言面兜底）/ 禁豁免 EAA0901 禁触 DO-9 / 附着点限 Normalize 后键空间 / fail 方向逐边界显式声明。前置依赖：B4 公共面收缩先行（先收缩再冻结，避免精化标注冻结在超集面上）。触发：P4 E4 后。

## 维护模式（P4 完成后）

每晚对抗模糊测试（随机剧本 fuzz + 变异门 + 性能曲线钉 `ProdAuditR4AuditScaleTests`），回归即修。

## Blockers

（无——原「iter55 PO-55-01/02/03 挂起」已由 A0 裁定解除：对代码层无阻塞，
文档层归 A5/A6/A8 正常排期，见 `audit/qed/PO55-TRIAGE.md` §0/§3。）
