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
- [ ] **A2** `At(t)` 集合投影 vs `Audit` 扫换线双计数语义——统一，或钉死差异契约（文档 + 性质测试）。
- [ ] **A3** `Unknown` 模式 fail-open——改 fail-closed 默认 + 显式 opt-in，或 PDR 论证保留。
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

## 维护模式（P4 完成后）

每晚对抗模糊测试（随机剧本 fuzz + 变异门 + 性能曲线钉 `ProdAuditR4AuditScaleTests`），回归即修。

## Blockers

（无）
