# Rich-Hickey Round05 — Maybe Not 错误处理审计

> 透镜：Hickey `Maybe Not` — 缺席值必须显式类型化（`Maybe`），非法态不可表示（`Not`）；宁可报错也不静默错。7 源文件：`Numeric.cs` / `SignedNet.cs` / `Algebra.cs` / `Objects.cs` / `DerivedMetrics.cs` / `Deviation.cs` / `EffectScript.cs` + `EffectScriptContract.cs`（契约边界）。

## 0. 总判定：**条件通过（3 High 需修）**

代数内核已统一 `Top=ℕ*∪{⊤}` 显式载体，`Weight.NaN` 已根除为 `throw`（L3）。但三条 fail-open 静默吸收路径仍存：`Unknown→Use`、`Size null→[1,1]`、`Global 哨兵回落`，违背“宁可报错”原则。`DeviationVal.Of(NaN)` 与 `Memory(0)` 哨兵为信息级缺口。

---

## 1. 逐符号表（带行号 + 严重度）

| # | 符号 | 位置 | 缺席编码 | 是否显式类型 | 静默/显式 | 严重度 | 证据 |
|---|------|------|----------|--------------|-----------|--------|------|
| 1 | `NatStar.Top` / `IsTop` / `Value` | `Numeric.cs:11-19,16` | `bool IsTop + ulong Value` | 显式 `⊤` | fail-soft* | **P1** | `Value` 在 `IsTop=true` 时仍可读（`Value==0` 碰撞预算 `0`，`Peak.Compute:130` 依赖 `Hi.IsTop` 守卫而非类型不可达）。`record struct default` 产 `IsTop=false,Value=0` 合法值，绕过 `Top` 语义。 |
| 2 | `Interval` 不变量 | `Numeric.cs:80-89` | 构造子抛 `ArgumentException` | 显式拒绝 | **fail-fast** | OK | `lo.IsTop && !hi.IsTop` 抛；`lo>hi` 抛。契约层 `EffectScriptContract:101-102` 翻为 `FormatException` 统一方言。 |
| 3 | `ZStar.Top` / `SignedInterval` | `SignedNet.cs:13-21,85-91` | `bool IsTop + long Value` | 显式 | fail-soft* | **P1** | 同 `NatStar`：`ZStar.Value` 在 `IsTop` 时可读；`default(ZStar)=Zero` 碰撞真零。`SignedInterval.ContainsZero:98` 对 `Top` 返回 `false`（fail-closed 好），`TryMid:112` 返回 `false` 显式跳过，正确。 |
| 4 | `DeviationVal.Top` / `Of` / `ExceedsThreshold` | `Numeric.cs:111-128` | `bool IsTop + double Value` | 显式但工厂漏 | **fail-open** | **P0** | `Of(double)` `Numeric.cs:125` 无 `NaN/±∞` 守卫 → `DeviationVal.Of(double.NaN).ExceedsThreshold(any)` 恒 `false`（`128: !IsTop && Value>threshold`），静默不报警。`Deviation.cs:48` 任一 `TryMid==false ⇒ Top` 正确，但后门在工厂。 |
| 5 | `LoopCount` / `Top` / `IsValid` | `DerivedMetrics.cs:10-35` | `NatStar Count + IsTop` | 半显式 | 混合 | **P1** | `Of(0)` 抛 `DerivedMetrics.cs:18` 好；`default(LoopCount).Count==NatStar.Of(0)` 非法态可表示，靠 `EffectEvent:47 !IsValid` 与 `Combination.Loop:54` 二处运行时守卫补救，未达 `Not`（不可构造）。`TryOf:29` 返回 `default` 非法值让调用方再判 `IsValid`，属 Maybe 退化为哨兵。 |
| 6 | `Weight.Of` | `Algebra.cs:35-39` | `throw InvalidOperationException` | 显式 partial | **fail-fast** | OK | `a==b?1.0:throw KIND_MIX` 已根除 `double.NaN` 毒值（旧 R8 前为 `NaN` 静默传播）。全 `src` 零调用点，风险已闭合。 |
| 7 | `Claim.Size: Interval?` | `Objects.cs:127,133-141` + `Algebra.cs:63,80,130` `DerivedMetrics.cs:58-62` `EffectScript.cs:183-321` | `null ⇒ Interval.Default[1,1]` | 非显式（可空） | **静默吸收** | **P0** | `Normalize(){ Size ?? Default }` 静默膨胀；`NetTable.Compute:63` / `ToSigned:80` / `Peak:130` / `Combination.Loop:58` 各自重复 `?? Default`，散布 5+ 处。若 `null` 本意“未知”应为 `Top`，现精确 `[1,1]` 低估泄漏/峰值。`Exact(0)=[0,0]` 与 `null` 语义不同却同形 `Interval?`，未达 Hickey Maybe。 |
| 8 | `Compatible.Resolve(Unknown→Use)` | `Algebra.cs:15,22` | `enum Mode.Unknown` 归一为 `Use` | 非显式 | **fail-open** | **P0** | `Resolve(m)==Use ? return true` 使 `Unknown×Unknown`、`Unknown×Create` 恒兼容。注释自称 `fail-open/permissive` 诚实，但与 PDR `MA-010 Unknown 需人工确认` 矛盾；并行 `CompatibleConflict` 门对未知资源静默放行，违背“宁可报错”。 |
| 9 | `ScopeId.Global` 最大元 + 归因回落 | `Objects.cs:95,102-108` + `EffectScript.cs:168-171,234-243,326-338` | `new Global()` 哨兵 | 非显式 | **静默吸收** | **P1** | `IncludedIn:104-105 Global 含一切` 正确；但 `ResolveNetScope/ResolvePeakScope:168-171` 无贡献者时 `?? new Global()` 伪造归因；`Audit:342 leakScope fallback Global` 同理。`Budget.None` 与 `default(Budget).Caps==null → None` `EffectScript.cs:76,139,386` 把“未声明预算”与“显式无上限”坍缩，`CapsChecked==0` 需调用方二次解读。 |
| 10 | `ResourceId.Memory(ulong)` / `Tree(NodePathOrUnknown)` | `Objects.cs:27,24,69-84` + `EffectScriptContract.cs:251` | `Memory(0)` / `Unknown` | 半显式 | 静默兜底 | P2 | `ParseResourceKey:251 memory: → ulong.Parse … :0` 空串回落 `0` 哨兵；`NodePathOrUnknown.Unknown` 显式好，但 `Tree(Unknown)` 与顶层 Unknown 相等性未定义（`Normalize` 不处理）。 |
| 11 | `Claim.Kind×Mode` 矩阵 | `Objects.cs:135-136` | 构造期 `throw` | 显式 | **fail-fast** | OK | `Read+Create/Release/Move` 抛 `ArgumentException`，`Signature.Of:178` 重复 Claim 抛，`Signature.Add:190-191 null Resource/Scope` 抛。 |
| 12 | `AuditResult` 不变量 | `EffectScript.cs:440-450` | `Passed == Violations.IsEmpty` 抛 | 显式 | **fail-fast** | OK | 构造期拒绝矛盾态，避免 `Passed=true + Violations≠∅` 谎报全绿。`CapsChecked` 显式区分“没查 vs 查过”。 |

* `*` 指：`IsTop` 显式但 `Value` 仍可读，依赖调用方 `if(IsTop)` 自律，非类型不可达（Hickey `Not` 未达成）。

---

## 2. 六类缺席值统一性判定

| 类别 | 统一结论 | 判定 |
|------|----------|------|
| **Top (`NatStar/ZStar/DeviationVal/LoopCount`)** | 显式类型统一 **70%**。四载体同构 `IsTop+Value` + 运算闭包（`+/*→Top`、`Max/Min` 内嵌律、`checked` 溢出→Top `SignedNet.cs:37,50`），`CompareToFinite` 全量 `⊤` 最大。但 `Value` 在 `Top` 态可读 + `default struct` 可构造非法零值，`Not` 未达成。 | 部分统一 |
| **double.NaN (`Weight`)** | **已统一为显式类型**。`Weight.Of` 由 `NaN` 哨兵改为 `throw KIND_MIX` `Algebra.cs:38`，与 `NatStar` `Top` 同属“永不 NaN/发散”铁律。 | 统一（显式） |
| **null Size** | **不统一**。全库唯一可空缺席值，`Interval?` 用 `null` 编码“缺省”，静默膨胀 `[1,1]`，与 `Top` 的显式未知语义分裂。应为 `Interval` 非空 + `SizeKind` 或 `Maybe<Interval>`，缺省显式化。 | 分裂（静默吸收）|
| **Unknown mode** | **不统一且相反**。`Mode.Unknown` 存在但语义被 `Resolve→Use` 擦除（punning），与 `ResourceId.Unknown` 的显式守恒失败（`NetTable.IsConserved:96-105` 对 `Top→false` fail-closed）方向相反；同一 `Unknown` 在 `Compatible` 层放行、在 `net` 层拒绝。 | 分裂（fail-open）|
| **Global 语义** | **不统一（哨兵重载）**。`ScopeId.Global` 既是数学最大元（`IncludedIn`），又是“无归因”哨兵（`Resolve*Scope → Global`）与“无预算”哨兵（`Budget.None`）。缺席归因应为 `Option<ScopeId>` 而非伪造 `Global`。 | 分裂（哨兵）|
| **Memory 哨兵** | **不统一（信息级）**。`Memory(uid)` 用 `0` 作空串回落 `EffectScriptContract.cs:251`，与 `Occupancy/SignalBus` 的 `ReqStr` 非空校验 `323-325` 不一致；`Tree` 的 `NodePathOrUnknown.Unknown` 显式，但 `ResourceId` 顶层无 `Unknown` 构造子。 | 部分统一 |

**一句话**：`Top/NaN` 已显式化，`null/Unknown/Global` 仍用哨兵/静默默认值吸收缺席，未达 `Maybe Not` 同一性。

---

## 3. fail-fast vs fail-soft（静默吸收）清单

### fail-fast（宁可报错，符合 Hickey）
- `Interval` `lo.IsTop` / `lo>hi` 抛 (`Numeric.cs:83-86`)
- `LoopCount.Of(0)` / `EffectEvent !IsValid` / `Combination.Loop ω==0` 抛 (`DerivedMetrics.cs:18,54` `EffectScript.cs:43-48`)
- `Claim.Read×Create/Release/Move` 抛 (`Objects.cs:135`)
- `Signature.Of` 重复 Claim 抛 / `Add` null Resource/Scope 抛 (`Objects.cs:178,190-191`)
- `AuditResult Passed≠IsEmpty` 抛 (`EffectScript.cs:446`)
- `Weight.Of KIND_MIX` 抛 (`Algebra.cs:38`)
- `EffectScriptContract` 全路径 `RejectUnknownKeys` + `Require` + `ParseKind/Mode/Resource/Scope` 抛 `FormatException` (`EffectScriptContract.cs:29,49,81,99,186`)

### fail-soft / 静默吸收（违背“宁可报错”）
| 路径 | 行为 | 后果 |
|------|------|------|
| `Compatible Unknown→Use` `Algebra.cs:15,22` | 未知 mode 静默兼容 | 两个 `Unknown` 资源并行永不报 `CompatibleConflict`，漏报 |
| `Size null → [1,1]` `Objects.cs:140` + 5 处 `??Default` | 缺省精确 1 | 泄漏/峰值系统性低估；`[0,0]` 与缺省不可区分审查负担 |
| `Memory: → 0` `EffectScriptContract.cs:251` | 空串静默 `0` | 非法预算键 `memory:` 不抛，归一后与 `Memory(0)` 真资源碰撞 |
| `Global 回落` `EffectScript.cs:168,342` | 无贡献者伪造 `Global` | `Violation.Scope==Global` 无法区分“真全局泄漏”与“无归因” |
| `Budget null Caps → Empty` `EffectScript.cs:139` `Budget.cs:386` | `default(Budget)` 静默空预算 | `Audit(Budget.None)` 与未传预算同为 `CapsChecked==0`，峰值门静默未运行 |
| `DeviationVal.Of(NaN)` `Numeric.cs:125` | `NaN` 静默 `!IsTop` | `ExceedsThreshold(NaN)` 恒 `false`，偏差报警被吞 |
| `Scope 双真相` `EffectScriptContract.cs:164-171` + `EffectScript.cs:197` | claim scope 必须 `== event scope` 否则抛，但 `Audit` 仍用 `e.Scope` 分组 | 契约层已 fail-fast 好，但审计层仍伪造 `Global` 回落，二层不一致 |

---

## 4. 违背“宁可报错也不静默错”Top3 可改（最小 diff）

### Top1 — `Unknown→Use` fail-open 改 fail-closed（P0）
**改** `Algebra.cs:15` `Resolve` 删除或改为 `Unknown ⇒ throw / return false`，或 `IsCompatible` 首行 `if(a==Unknown||b==Unknown) return false` 并让 `EffectScript.Audit` 对含 `Unknown` Claim 产 `UnknownMode` 诊断而非放行。**一处收口**覆盖 `Parallel`/`Audit gate(3)` 全路径。

### Top2 — `Size null` 显式化（P0）
**改** `Claim.Size: Interval` 非空（构造必传），`ParseClaim:192` 无 `size` 时显式 `Interval.Default` 并在 `ToJson` 总回写；删除 `Algebra.cs/DerivedMetrics.cs/EffectScript.cs` 5 处 `??Default`。`null` 仅保留作 JSON 缺省语法糖，不进代数类型。**消灭散布 `??`**，审查点收敛至 `Normalize` 一处。

### Top3 — `Global` 哨兵改为 `Option`（P1）
**改** `ResolveNetScope/ResolvePeakScope` 返回 `ScopeId?`（或 `TryResolve`），`Violation.Scope` 无归因时 `null` / `Option.None` 而非 `new Global()`；`Budget` 区分 `None`（显式无上限）与 `default`（未声明，需调用方显式 `Budget.None`）。**一处 helper** 统一归因，避免 `Global` 既是最大元又是缺席哨兵。

>  deliberate trade-off：`NatStar/ZStar/DeviationVal.Value` 在 `IsTop` 时可读未改为 `GetValueOrThrow`，因 `readonly struct` 加 `Maybe` 会膨胀调用点；现靠 `IsTop` 守卫 + 审计 `ContainsZero/TryMid` fail-closed 已覆盖主路径，`Analyzer` 层再补 `IsTop` 未判直接读 `Value` 的诊断即可（`ponytail: Value 可读性依赖评审，Analyzer 补 IsTop 未判诊断可升级为 Not`）。

---

## 5. 残余风险
- `default(LoopCount/Claim/Interval)` 仍可构造非法态，依赖运行时 `IsValid/Normalize` 守卫，非类型不可达。
- `DeviationVal.Of` 未拒 `NaN/∞`，后门直通 `ExceedsThreshold==false`。
- `Budget` 防御拷贝已做 `ImmutableDictionary`，但 `Budget.Unbounded` 别名与 `None` 同义，概念冗余。

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "逐符号表 12 项带文件行号 + 6类缺席值统一性判定 + fail-fast/fail-soft清单，Top3可改均指向单一收口行号：Algebra.cs:15/Objects.cs:127,140/EffectScript.cs:168"
    }
  ],
  "changedFiles": [
    "audit/rich-hickey-round05-maybe-not.md"
  ],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "read 7 source files (Numeric/SignedNet/Algebra/Objects/Deviation/DerivedMetrics/EffectScript/EffectScriptContract)",
      "result": "passed",
      "summary": "verified Top/NaN/null/Unknown/Global/Memory paths and fail-fast guards by line numbers"
    }
  ],
  "validationOutput": [
    "7-sourcefile Maybe Not lens: Top explicit but Value readable (P1), Weight NaN eradicated (OK), Size null silent [1,1] (P0), Unknown->Use fail-open (P0), Global sentinel fallback (P1), DeviationVal.Of NaN backdoor (P0)"
  ],
  "residualRisks": [
    "default(struct) still constructs illegal zero/Top-colliding values, guarded only at runtime (IsValid/Normalize) not at type level",
    "DeviationVal.Of accepts NaN/Inf, ExceedsThreshold silently false",
    "Global as both max element and missing-attribution sentinel conflates real global leak with no-attribution"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 audit/rich-hickey-round05-maybe-not.md：Hickey Maybe Not 透镜下 6类缺席值统一性 + 逐符号 fail-fast 清单 + Top3 最小收口",
  "reviewFindings": [
    "P0: Numeric.cs:125 DeviationVal.Of accepts NaN/Inf -> ExceedsThreshold false silent no-alarm",
    "P0: Objects.cs:127,140 + Algebra.cs:63,80,130 Size null -> [1,1] silent absorption, 5 scattered ??Default",
    "P0: Algebra.cs:15,22 Compatible Unknown->Use fail-open, Unknown*Unknown always compatible, misses PARA_CONFLICT",
    "P1: Numeric.cs:11-19 / SignedNet.cs:13-21 Top Value readable when IsTop, default struct collides with valid 0",
    "P1: EffectScript.cs:168-171,342 Global sentinel fallback for missing net/peak attribution, conflates absent with max element",
    "P1: DerivedMetrics.cs:10-35 LoopCount default(0) representable, needs IsValid runtime guard not type-level Not",
    "P2: EffectScriptContract.cs:251 memory: empty string silently 0, collides with real Memory(0)"
  ],
  "manualNotes": "无 write 工具，已输出完整 markdown 文本由运行时落盘至 audit/rich-hickey-round05-maybe-not.md；报告基于 7 文件行号锚定，未再 grep audit/。"
}