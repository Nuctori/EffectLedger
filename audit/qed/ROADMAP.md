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
- [ ] **A7** 归一化实例身份（PO-55-08）：memory/callback 常量 uid 折叠掩盖泄漏（Connect(sigA)+Disconnect(sigB) ⇒ net=0）——参数化 alias 映射 or 显式声明保守合并语义，二选一 + 钉。
- [ ] **A8** 语义文档小项打包（PO-55-04/07/10/11/12 单会话）：⊔ 四元组配对键（代码已对齐，补文档）、ℤ 序与减法（`SignedInterval` 已有，补文档）、Signature(b) 良定义、copy_i scope 标注二选一、纯编辑项（双 ##14/断表/术语表）。
- [ ] **A9** release-class 清单权威性复核（PO-55-09）：对 godotengine 源码给出函数签名级证据——cancel_free 归类方向、free_children_in_group 存在性；错误归类即修 `ApiMapping`。
- [ ] **A2** `At(t)` 集合投影 vs `Audit` 扫换线双计数语义——统一，或钉死差异契约（文档 + 性质测试）；吸收 PO-55-13（A1/A2 完备性的程序类前提：事件单触发/异常路径/有界循环豁免的定义或降级 partial-complete）。
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
