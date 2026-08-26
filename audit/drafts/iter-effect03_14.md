# Iter3–Iter14 批量审计（EffectScript 边界/性质/确定性/性能）

> 审计对象：`tests/Cosmos.EffectAlgebra.Tests/EffectScriptEdgeTests.cs`（新增 ~40 测试）+ `EffectScriptTests.cs`（既有 16）
> 对照：`EFFECT_SCRIPT.md` §2/§3/§5、`src/Cosmos.EffectAlgebra/EffectScript.cs`、`audit/iter-effect01.md`
> 方法：父进程亲读测试源码 + 全解 `dotnet test` 实测（268 通过 / 0 失败 / 0 错误 / 0 警告）
> 立场：逐项核验每轮焦点是否真闭合（含反例对照）

---

## 逐轮闭合核验

### Iter3（OPEN-4 居民层净化）

- **闭合**。`Audit_ResidentCreate_Top_NotLeak_OnlyPeakBound`（:40-50）：ω=⊤ create 无 release ⇒ `Budget.None` 下 `Passed`（豁免 Leak）；cap=1 ⇒ `PeakExceeded`。`Audit_ResidentWithClosedFinite_Create_NoLeak`（:52-60）：居民层(ω=⊤ Use)+有限 create 有 release ⇒ 不报 Leak。✓

### Iter5（Budget ⊤ 交互）

- **闭合**。`Audit_PeakExactlyEqualsCap_Passes`（:63-71）：峰值精确 == cap ⇒ `Passed`（边界）。`Audit_PeakTopVsFiniteCap_Reported`（:73-82）：ω=⊤ ⇒ size [1,⊤] ⇒ 峰值 ⊤ > 有限 cap ⇒ `PeakExceeded`。✓

### Iter6（代数定律）

- **闭合**。`At_EmptyScript_IsEmpty`（:85-91）。`At_Idempotent_SameT`（:93-99）。`At_Union_Commutative_Associative`（:101-121）：脚本拼接 `At(t)` == 分别 `At` 后 `Signature.Union`（完整三桶集合相等，非仅计数）+ 交换 + 结合。`At_EndpointSampling_CapturesPiecewiseConstant`（:123-136）：段内恒定。`At_DenseScan_FullSignatureEquivalence`（:138-157）：[0,40] 每整数 t `At` 与手动 `Union` 全签名相等（强断言）。✓

### Iter7（性质测试）

- **闭合**。`Property_Random300_AuditDeterministicAndTerminates`（:189-203）：300 随机脚本（覆盖 Use/Create/Release/Move/Unknown × 4 资源 × ω=⊤/有限 × 有限/∞ lifetime），两次 `Audit` 以 `HashSet<Violation>` 集合比较一致（确定性），<1s 终止。随机覆盖冲突场景（Release 早于 Create 等），非全绿生成。✓

### Iter9（溢出→⊤ 守卫）

- **闭合**。`At_LargeOmega_OverflowToTop_NoCrash`（:206-222）：ω=1e19 × size 2 ⇒ 溢出 ⇒ 峰值 ⊤（`NatStar.IsTop`），有限 cap ⇒ `PeakExceeded`，无崩溃无负数。✓

### Iter12（确定性）

- **闭合**。`At_MultipleCalls_IdenticalSignature`（:225-236）：多次 `At` 同签名。`Audit_EventOrdering_DoesNotAffectResult`（:238-249）：事件顺序不同集合相同 ⇒ Violations 排序后相等（集合语义顺序无关）。✓

### Iter13（类型硬化锚定）

- **闭合**。`EffectEvent_NoDefaultConstructor_AllFieldsRequired`（:252-258）：无默认构造 ⇒ 全字段必填；含 4 参构造。`EffectEvent_ThreeArgCtor_DefaultsOmegaToOne`（:260-265）：3 参重载 `Loop==LoopCount.Of(1)`。✓

### Iter14（居民层豁免语义）

- **闭合**。`ResidentExempt_MixedFiniteTop_NotExempt_Leak`（:268-278）：X = 有限 create(无 release) + ω=⊤ create ⇒ X 不豁免 ⇒ 报 `Leak`（顺序无关已验证）。`ResidentExempt_OnlyTopUse_Exempt`（:280-286）：Y 仅 ω=⊤ Use ⇒ 豁免。✓

### Iter15（空/单/∞）

- **闭合**。`EmptyScript_AuditPasses_AllAtEmpty`（:289-295）。`SingleEvent_AliveOutsideLifetime`（:297-305）：内/外 lifetime 正确。`InfiniteLifetime_AliveAtLargeT_AuditClosureCorrect`（:307-316）：hi=⊤ 任意大 t（1e9）仍存活；无有限 hi 时闭包点取 0 正确。✓

### Iter16（重叠/嵌套）

- **闭合**。`OverlappingLifetimes_PartialOverlap`（:319-329）：重叠段含两者、独占段仅其一。`NestedScope_GlobalIncludedInImplicitAudit`（:331-340）：`Scene("A").IncludedIn(Global())` 真；脚本级 Audit 用 Global 隐含 scope 在 Scene 作用域仍检出 Leak。✓

### Iter17（负陷跨资源）

- **闭合**。`Audit_ReleaseBeforeCreate_TwoResources_TwoNegativeDips`（:343-354）：两资源各 release 早于 create ⇒ 各报 `NegativeDip`，共 2 条。✓

### Iter18（多资源）

- **闭合**。`MultiResource_AllClosed_Passes`（:357-369）。`MultiResource_MissingOneRelease_OnlyThatLeaks`（:371-386）：删 Mem release ⇒ 仅 Mem 报 Leak，Gpu/CmdBuf 不报（反例对照）。✓

### Iter19（有限 ω 缩放）

- **闭合**。`At_FiniteOmega_ScalesPeakAndNet`（:389-405）：ω=3 size=2 ⇒ 峰值 6（对照 `Combination.Loop` 手动缩放同为 6）；无 release ⇒ Leak。✓

### Iter20（§ 出处）

- **闭合**。`PublicApi_HasSectionCitations`（:408-424）：读 `EffectScript.cs`，每个 `public` 声明前 6 行内须有含 `§` 的 `///` 注释；`missing==0`。✓

### Iter24（§7 形状一致）

- **闭合**。`ResourceKinds_MatchKnownSet`（:427-439）：`ResourceId` 子类集合 == 已知 15 种（含 Custom），未发明新 kind。✓

### Iter25（性能守卫）

- **闭合**。`Performance_1000Events_AuditUnder5s_StillDetectsConflict`（:442-474）：1000 事件 Audit <5s；两次结果集合一致；注入的 `CompatibleConflict` 仍被检出。✓

---

## 对抗质问结论

- **端点采样对 hi=⊤ 是否漏点**：`Alive` 用 `Hi.IsTop || t≤Hi`，开放事件对所有有限 t 存活；闭包点取 `maxFinite`（无有限 hi 取 0）。累积 net 对所有 finite Lo 事件在闭包点已含。与 iter-effect01 焦点结论一致，无遗漏。✓
- **ω=⊤（size=[lo,⊤]）与有限 release 配对**：`CumulativeNet` 对 ⊤ 端 `SignedInterval` 标记 `IsTop`，`IsConserved`/`ContainsZero` 对 ⊤ 端 fail-closed 不报 Leak（交人工）。居民豁免逻辑独立处理，互不影响。✓
- **假绿检查**：所有「Passed」测试均伴随反例对照（如 Iter18 删一 release 才证伪、Iter15 内外 lifetime 对照、Iter25 注入冲突）。无仅断言 Passed 而无反例的测试。✓

---

## 开放项

**无 open。** Iter3–Iter14 全部焦点闭合，且每轮均含可证伪断言 + 反例对照。全解实测 268 测试通过 / 0 失败 / 0 错误 / 0 警告。

## 总评：可关闭

EffectScript API 经本轮 40+ 边界/性质/确定性/性能测试覆盖，所有已知 open（auditA 6 项 + OPEN-N1/N2）已闭合，API 定义良性、可验证、确定性、可符号探索。剩余 Iter11(JSON)/22(fuzz)/23(endpoint proof)/26-28(residual) 为增量覆盖（JSON 契约 + 进一步残差审计），不阻塞本轮关闭。
