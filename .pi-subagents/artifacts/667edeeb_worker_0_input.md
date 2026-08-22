# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
实现 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs`（视觉效应代数剧本，L1 增量，零 Godot 依赖），并加测试。先读 `D:/Godot/Cosmos/EFFECT_SCRIPT.md`（设计，含已审出的 6 项 open）与 `src/Cosmos.EffectAlgebra/{Objects,Algebra,DerivedMetrics,Numeric,SignedNet}.cs`（既有 L1 API）与 `PDR_Effect_Cost_Algebra_v3_FINAL.md` §3.1–§3.3。

**背景**：`audit/effect-script-auditA.md` 已审出 6 项 open（3 HIGH：scope 自由变量 / 瞬时守恒误判临时占用 / Compatible 未分组）。你的实现必须一次性正确落地这 5 项（OPEN-6 LOW 是文档措辞，不在代码）。

**精确类型（位置记录 struct，构造即全必填）：**
```csharp
public readonly record struct EffectEvent
{
    public Interval Lifetime { get; }      // §3.1.5 [lo,hi]；hi=⊤ ⇒ 上界开放
    public ScopeId Scope { get; }          // NEW（修 OPEN-1）：事件的作用域，At/Net/Peak 的 scope 来源
    public Signature Footprint { get; }    // §3.1.4b 该元素资源签名（三桶）
    public LoopCount Loop { get; }         // §3.2.5 ω；默认 LoopCount.Of(1)
    // ctor 全必填；Loop 提供默认重载(LoopCount.Of(1))
}

public readonly record struct EffectScript
{
    public ImmutableArray<EffectEvent> Events { get; }
    public Signature At(NatStar t);        // 所有 Lifetime∋t 的 Event：Combination.Loop(Footprint, Loop, Scope) 后 Union（§3.2.1）
    public AuditResult Audit(Budget cap);  // 端点采样（见下）
}

public readonly record struct Budget
{
    public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }  // 缺省该资源无上限
}

public readonly record struct AuditResult { public bool Passed; public ImmutableArray<Violation> Violations; }
public readonly record struct Violation
{
    public NatStar AtT; public ResourceId Resource; public ScopeId Scope;
    public string Kind;          // "Leak" | "NegativeDip" | "PeakExceeded" | "CompatibleConflict"
    public string Detail;        // 当前值 vs 上限/阈值，供 AI 回修 JSON
}
```

**`At(t)` 语义**：`Lifetime ∋ t` ⇔ `Lifetime.Lo ≤ t && (Lifetime.Hi.IsTop || t ≤ Lifetime.Hi)`。对每个存活 Event 取 `Combination.Loop(e.Footprint, e.Loop, e.Scope)`（§3.2.5，loopScope=e.Scope，修 OPEN-1）后 `Signature.Union` 累积。签名用既有 L1，不重算代数。

**`Audit(cap)` 端点采样定理（§3，精确非近似）**：`At(t)` 是分段常数，仅在各 `Lifetime` 的 distinct `Lo`/`Hi` 端点改变。收集所有 finite 端点（hi=⊤ 视为 +∞，取 max finite lo + 1 作为代表右端），逐点采样：
1. **守恒（修 OPEN-2，累积 net 非瞬时）**：对资源 r 计算累积有符号净效应 `C(t)` = Σ_{e: e.Lo ≤ t} Σ_{c ∈ e.Footprint.OccupyClaims} sign(c.Mode)·scaleSize(c.Size, e.Loop)。sign: create/move=+, release=−；scaleSize 用 §3.2.5（ω=⊤⇒hi=⊤）。要求：对全部采样 t，`C(t).Hi ≥ 0`（无 release-before-create 的负陷，报 `NegativeDip`）；在「全部事件结束后」（t ≥ max finite hi）`C(t)` 区间含 0（生命周期闭合，否则报 `Leak`）。**居民层豁免（修 OPEN-4）**：若某资源的全部正向贡献仅来自 `Loop.Top` 的 Event ⇒ 该资源免守恒检查（合法常驻），但仍参与 Peak/Budget。
2. **峰值预算（修 OPEN-4b/OPEN-5）**：`Peak(At(t), e.Scope)` 对 `cap.Caps` 中含的资源，要求 `Peak ≤ cap`（经 `NatStar.CompareToFinite`）；资源不在 Caps ⇒ 不检查。超限报 `PeakExceeded`（带 current vs cap）。
3. **兼容（修 OPEN-3/OPEN-5）**：在 `At(t)` 内，按 (归一化 ResourceId, ScopeId) 分组 occupy Claims，组内两两 `Compatible.IsCompatible`；冲突（CONFLICT 集 create+create 等）报 `CompatibleConflict`。

**测试**（新建 `tests/Cosmos.EffectAlgebra.Tests/EffectScriptTests.cs`，用 xUnit，断言具体可证伪）：
- 构造 + `At(t)` 基本：两 Event 重叠时刻 Union 正确（对照手动 `Signature.Union`）
- 守恒：create@t1 release@t2（t2>t1）临时占用 ⇒ Audit.Passed（修 OPEN-2 验证，不误报泄漏）
- 泄漏：仅 create 无 release ⇒ Audit 报 `Leak`
- 负陷：release 早于 create ⇒ 报 `NegativeDip`
- 居民层：`Loop.Top` 常驻层 ⇒ 不报 `Leak`，但 Peak 超限仍报 `PeakExceeded`
- Budget：资源在 Caps 且超限 ⇒ `PeakExceeded`；不在 Caps ⇒ 通过
- Compatible：同资源同 scope 两 create ⇒ `CompatibleConflict`；跨资源 ⇒ 通过
- 确定性：`At(t)` 同脚本同 t 同签名（ImmutableHashSet 无序）
- 端点采样 == 全整数密集扫描（[0,maxT] 每整数 t `At` 一致）

**约束**：不引入新代数结构；只复用既有 L1（Interval/Signature.Union/NetTable.Compute/Derived.IsConserved/Peak.Compute/Compatible.IsCompatible/Combination.Loop/LoopCount/ResourceId.Normalize/ScopeId.IncludedIn/SignedInterval）。每个 public 类型/方法带 §x.y 注释。模块头带 `LANDING_PLAN §` 角色注释。文件顶部 `// EFFECT_SCRIPT.md §2/§3` 引用。

**运行（本机需先 `$env:MSBUILD_EXE_PATH=$null`）**：实现后 `cd D:/Godot/Cosmos; $env:MSBUILD_EXE_PATH=$null; dotnet build Cosmos.EffectAlgebra.slnx` 须 0 错误 0 警告；`dotnet test` 全绿。TreatWarningsAsErrors 已在 src 工程开启，确保无警告。

完成后回复：实现完成；`dotnet build` 错误/警告数；`dotnet test` 通过数；一行结论。若 build/test 失败必须修复到全绿再回复。

## Acceptance Contract
Acceptance level: checked
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope
- criterion-2: Return evidence sufficient for an independent acceptance review

Required evidence: changed-files, tests-added, commands-run, residual-risks, no-staged-files

Review gate: required by reviewer.

Finish with a fenced JSON block tagged `acceptance-report` in this shape:
Use empty arrays when no items apply; array fields contain strings unless object entries are shown.
`criteriaSatisfied[].status` must be exactly one of: satisfied, not-satisfied, not-applicable.
`commandsRun[].result` must be exactly one of: passed, failed, not-run.
`manualNotes` and `notes` are optional strings; an empty string means no note and does not satisfy `manual-notes` evidence.
```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "specific proof"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "specific proof"
    }
  ],
  "changedFiles": [
    "src/file.ts"
  ],
  "testsAddedOrUpdated": [
    "test/file.test.ts"
  ],
  "commandsRun": [
    {
      "command": "command",
      "result": "passed",
      "summary": "short result"
    }
  ],
  "validationOutput": [
    "validation output or concise summary"
  ],
  "residualRisks": [
    "none"
  ],
  "noStagedFiles": true,
  "diffSummary": "short description of the diff",
  "reviewFindings": [
    "blocker: file.ts:12 - issue found, or no blockers"
  ],
  "manualNotes": "anything else the parent should know"
}
```