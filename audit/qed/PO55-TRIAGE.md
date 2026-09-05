# PO55-TRIAGE — iter55 账本 PO-55-01..18 三分对账（ROADMAP A0 交付物）

> 日期：2026-09-05。方法：逐项对照 **L1 代码实况**（file:line 证据 + 既有测试钉）裁定，
> 不采信 iter55 的文档层结论作为代码结论。iter55 审的是 `PDR_Effect_Cost_Algebra_v3_FINAL.md`
> 叙事层；本对账裁定「叙事滞后」与「代码缺口」的归属。
> 三分口径（ROADMAP A0）：**已 discharge**（代码已闭合+钉在，残留归文档轨）/
> **并入既有任务** / **显式不修**。

## 0. 总裁定（头条）

**iter55 的三个「阻塞级」（PO-55-01/02/03）均非 L1 代码缺口**：

- **PO-55-01/02（多重性/ω 死变量）**：iter55 的反例「20 次 ∪ 后 Signature 仅含 1 条 ⇒ net=64」
  构造的是 **PDR 叙事里的路径**；代码中该路径在 `Signature.Of` 处 **loud 抛错**（P0-4 重复 Claim 拒，
  `Objects.cs:169-180`，钉 `Round4Hickey2Tests.Parse_DuplicateClaim_ThrowsFormatException`）。
  代码的多重性合法载体是**事件列表**（`EffectScript.Events`，扫换线逐事件累加，
  `EffectScript.cs:201-217`）与 **`Combination.Loop` 的 size×ω**（`DerivedMetrics.cs:85-89` Scale；
  钉 `LoopCombinationTests.Loop_FiniteOmega_ScalesPeakByOmega`：ω=5、size[10,10] ⇒ Peak=50，
  错实现必红）。20 个 Enemy 事件 ⇒ net=1280，AUDIT003 在代码上成立。
- **PO-55-03（ScopeId 矛盾）**：代码 `IncludedIn` 本就是**单向包含**
  （`Objects.cs:102-110`：`other is Global ⇒ true`，无反向分支）⇒ 反对称成立、Global 单向最大元，
  iter55 引的「双向包含」是 PDR 滞后叙事；Shell⊑Shell 由自反 `Equals` 覆盖（Shell 无载荷 record）。
  唯一真开放决策：`Loop(id)⊑enclosing`（跨标签不可比 ⇒ Loop 内 claim 不入外层 scope 过滤，
  `Objects.cs:88` 注释明示）→ 归 A6。

**「数学层阻塞」对代码层为 0；对 PDR 叙事层为 3（归 A5/A6/A8 重写）。**

## 1. 三分台账

| ID | iter55 状态 | A0 裁定 | 去向 | 关键证据 |
| ---- | ---- | ---- | ---- | ---- |
| PO-55-01 | open·阻塞 | 叙事滞后，代码闭合 | **A5**（PDR 推导重写） | `Signature.Of` 重复拒（`Objects.cs:169-180`）+ 扫换线逐事件累加（`EffectScript.cs:201-217`）；钉 Round4Hickey2/BucketIsolation |
| PO-55-02 | open·阻塞 | 叙事滞后，代码闭合 | **A5**（§3.2.5 改写 size×ω） | `Scale`（`DerivedMetrics.cs:85-89`）；钉 `Loop_FiniteOmega_ScalesPeakByOmega` / `Loop_TopOmega_FallsBackToTop` |
| PO-55-03 | open·阻塞 | 三段：(a)(b) 叙事滞后已闭合；(c) 真决策 | **A6** | `IncludedIn` 单向（`Objects.cs:102-110`）+ 自反覆盖 Shell；Loop⊑enclosing 开放 |
| PO-55-04 | open | **代码已实现 iter55 要的修复** | **A8**（补文档） | `Join` 四元组键 (Kind,Resource,Mode,Scope)+Merge（`Objects.cs:217-231`） |
| PO-55-05 | open | 真语义决策 | **A3** | §7 写操作标 use ⇒ CONFLICT 近不可实例化（PDR L618/L566；代码同构） |
| PO-55-06 | open | 代码已显式 fail-open；PDR「⊤ 上界」叙事矛盾 | **A3**（定稿方向） | `Algebra.cs:10-15` Resolve Unknown→Use，注释明示「勿再标 fail-closed」（R4-F8/R10-F6） |
| PO-55-07 | open | **代码已有 ℤ 有符号载体** | **A8**（补文档） | `SignedNet.cs` ZStar/SignedInterval；net 正负贡献构造（`EffectScript.cs:212-216`） |
| PO-55-08 | open | 代码事实成立（真缺口，非叙事） | **A7**（二选一定稿） | 裸名⇒固定 uid：`ApiMapping.cs:47` Callback("cb")；`Objects.cs:27,48` Memory 固定 uid |
| PO-55-09 | asserted | 代码/PDR 同病（7 项含两个疑点实名） | **A9**（源码签名级复核，错则修码） | `ReleaseClass.Names`（`ApiMapping.cs:206-208`）含 cancel_free、free_children_in_group |
| PO-55-10 | open | 纯 PDR 项（L1 无 if/while DSL） | **A8**（定义 Signature(b):=∅） | L1 无控制流记法；仅 PDR L284/L298 |
| PO-55-11 | open | 纯 PDR 项（代码无 copy_i，等价物 Scale 无 scope 标注动作） | **A8**（随 §3.2.5 改写消解） | `DerivedMetrics.cs:85-89` |
| PO-55-12 | open | 纯编辑 | **A8** | 双 ##14 / 断表 / 术语表 / ED-004 / 双围栏（PDR 五处） |
| PO-55-13 | asserted | 程序类前提缺失（真缺口，文档判据面） | **A2**（已吸收） | §14 A1/A2 COMPLETE 依赖直线控制流（PDR L1029-1032） |
| PO-55-14 | discharged | 维持 discharged | —（既有钉） | `HickeyX2FixTests`（⊤ 算术闭合）/ `IntervalArithmeticTests` / `PropertyTests` |
| PO-55-15 | discharged | 维持 discharged | —（既有钉） | `Compatible_Matrix_AllPairs` / `Compatible_Symmetric_AllPairs` / `Compatible_TotalFunction_NoThrow` |
| PO-55-16 | discharged | 维持 discharged | —（既有钉） | `BucketIsolationTests`（三桶隔离/量纲）/ `AttributeBoundaryTests`（KIND_MIX 联动） |
| PO-55-17 | discharged | 维持 discharged | —（既有钉） | `VerificationMatrixTests` / `AttributeBoundaryTests`（Deviation ε=1 链路） |
| PO-55-18 | discharged | 维持 discharged | —（既有钉） | Claim 五元组 record 相等 + `Round4Hickey2Tests.Parse_DuplicateClaim_ThrowsFormatException`（幂等定义基础的重复拒面） |

统计：**已 discharge 7**（04/07 代码闭合归 A8 文档 + 14..18 原判维持）；
**并入既有任务 11**（01/02→A5、03→A6、05/06→A3、08→A7、09→A9、10/11/12→A8、13→A2）；
**显式不修 0**。

## 2. 裁定依据补注

1. **为何 01/02 不是代码缺口**：iter55 的「最小补充」（Multiset<Claim> 或 occurrence 维度）
   在代码里以**更强**的形态存在——重复构造直接非法（P0-4 loud 拒），把多重性收敛到单一合法路径
   （`Combination.Loop` size×ω / 事件列表），集合并的幂等性因此**在合法路径上保持**，
   且静默低估路径被构造性封死。这优于 Multiset 方案（幂等并性质无需重推）。A5 只需把该推导写进 PDR。
2. **PO-55-03(c) 为何是真决策**：`IncludedIn` 跨标签恒 false（`Objects.cs:88`）⇒
   `NetTable`/`Derived.Net(S,Method(m))` 的 ⊆* 过滤**排除**方法体内 `Loop(id)` scope 的 claim。
   注意口径分层：`EffectScript.Audit`（JSON 剧本路径）gate(1) **不做 scope 过滤**（逐事件全累加），
   该缺口只咬代数层入口与 Runtime ⊆* 闸门（README 诚实边界 #11 已声明两层口径差）。
   A6 须在「加单向 Loop⊑宿主链」vs「维持不可比+显式声明降级」间二选一。
3. **PO-55-06 方向预注**：代码现状是显式 fail-open（注释三令五申「勿再标 fail-closed」，
   R4-F8/R10-F6 术语统一收口过）。A3 若裁定改 fail-closed 属**行为变更**，须按 A1 同款
   「二选一 + 否决案记录」处理；若维持，则只修 PDR L320-321 的矛盾叙事（归 A8 顺带）。
4. **行号修正**：iter55 引的 PDR 行号（L166/L252/L293/L312/L282-291…）是磁盘版 v3.0-FINAL-rA6
   的行号，A5/A8 重写 PDR 时以本 TRIAGE §1 的代码证据为准对齐，不再逐条回改 iter55。

## 3. 对 ROADMAP 的回写

- A0 勾销（本文件即交付物）。
- Blockers 段「iter55 PO-55-01/02/03 挂起」**解除**：对代码层无阻塞（§0）；文档层阻塞转入
  A5（01/02）、A6（03a-c）、A8（04/07/10/11/12）正常排期。
- A5/A6/A8 的验收标准不变（其描述已与本次裁定一致：「代码预判零改动」经 A0 证实）。
