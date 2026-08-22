# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
为 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs` 的视觉效应剧本 API 补**大量边界 + 性质 + 确定性 + 性能 + 残差**测试，覆盖 30 轮迭代计划里的 Iter3–Iter14 范围（写死在 todo，含 OPEN-4 居民层精细净化、空/单/∞/重叠/嵌套作用域/负陷/多资源/有限ω缩放/§7 形状一致/类型硬化/字符引用/确定性/性能）。已有 16 个测试绿（228 总测试）。

**先读：**
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/EffectScript.cs`（已有 API：`EffectEvent`/`EffectScript`/`Budget`/`AuditResult`/`Violation`，方法 `At(NatStar)`/`Audit(Budget)`）
- `D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/EffectScriptTests.cs`（已有测试模式，照此风格）
- `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/{Objects,Algebra,DerivedMetrics,Numeric,SignedNet}.cs`（L1 真实 API 供断言对照）
- `D:/Godot/Cosmos/EFFECT_SCRIPT.md`（设计，§2/§3/§5 确定性/pure 数据契约）

**测试必须断言具体可证伪**，新增测试类 `EffectScriptEdgeTests.cs`（同 namespace）。覆盖清单（≥40 新测试）：

**Iter3（OPEN-4 居民层净化）**：
- 居民层（ω=⊤, Mode.Use）经长 lifetime ⇒ 不报 Leak；但 Peak 在 Caps 内则 Passed，超 Caps 报 PeakExceeded（已在旧测试，新增：居民层 Mode.Create ω=⊤ 无 release ⇒ 也豁免 Leak，仅受 Peak 约束）
- 居民层与有限 create 共存：有限 create 永不释放 ⇒ 必报 Leak（即 Iter2 的 OPEN-N1 回归，已存在，新增反向：若有限 create 有匹配 release ⇒ 不报 Leak，居民层仍豁免）

**Iter5（Budget ⊤ 交互）**：
- Caps 含资源但 peak 精确等于 cap ⇒ Passed（边界）
- Caps 资源 peak = ⊤（含 ω=⊤ 有限正向？不，ω=⊤ 即 ⊤）与有限 cap 比较 ⇒ PeakExceeded

**Iter6（代数定律）**：
- At(t) 对空脚本恒 Empty
- 任意脚本 At(t) = At(t)（幂等）
- Union 结合/交换：脚本 A∪B（事件拼接）的 At(t) 与分别 At 后...（注意 At 是单点；验证 `new EffectScript(a,b).At(t)` 等价于 `Signature.Union(new EffectScript(a).At(t), new EffectScript(b).At(t))`）
- 端点采样==密集整数扫描（[0,maxT] 每整数 t At 签名相等）——扩展旧测试的更强断言（比较完整 OccupyClaims 集合而非计数）

**Iter7（性质测试）**：
- 随机生成 300 个脚本（随机 lifetime/footprint/loop/scope），断言 Audit 不抛、确定性（同脚本调两次结果相等）、终止（<1s）

**Iter12（确定性）**：
- 同脚本同 t 调用 At 多次结果签名相等（Signature 基于 ImmutableHashSet）
- 事件顺序不同但集合相同 ⇒ Audit 结果相同（含 Violations 集合内容，顺序无关：比较为 set 比较或排序后比较）

**Iter9（溢出→⊤ 守卫）**：
- ω 有限但很大（如 1_000_000）使 size×ω 溢出 ⇒ Peak 返回 ⊤（不崩溃、不负数）；含该事件脚本 Audit 在有限 Cap 下报 PeakExceeded

**Iter13（类型硬化已满足，补测试锚定）**：
- EffectEvent 位置记录构造全字段必填（反射确认无默认构造、4 字段；或确认 `EffectEvent(life,scope,fp)` 3 参重载默认 ω=1 且 Loop==LoopCount.Of(1)）

**Iter14（居民层豁免）**：独立测试类验证 `residentExempt` 仅对「正向贡献只来自 ω=⊤」的资源生效：构造混合（资源 X 有有限 create 释放 + ω=⊤ create 无释放）⇒ X 不豁免（报 Leak）；资源 Y 仅 ω=⊤ Use ⇒ 豁免

**Iter15（空/单/∞）**：
- 空脚本 Audit Passed、All At Empty
- 单事件脚本 At(t) 在 lifetime 内含该事件、外为空
- 事件 hi=⊤（∞ 寿命）⇒ 任意大有限 t 仍存活（At 含之）；Audit 闭包点取 maxFinite（无有限 hi 时取 0）仍正确

**Iter16（重叠/嵌套作用域）**：
- 两事件 lifetime 部分重叠 ⇒ 重叠区间 At 含两者；各自独占区间仅含其一
- 嵌套 ScopeId：Scene("A") 与 Global：IncludedIn(Global) 恒真；验证脚本级 Audit 用 Global 隐含 scope（不传参）不误判嵌套

**Iter17（负陷）**：release 早于 create（已测，新增：跨多个资源分别负陷 ⇒ 各自报 NegativeDip）

**Iter18（多资源/事件）**：单事件 Footprint 含 gpu+commandbuffer+memory 三种 occupy；Audit 三者独立：各自有 create+release ⇒ 不报；删其一 release ⇒ 仅该资源报 Leak

**Iter19（有限 ω 缩放）**：事件 ω=3 占用 size=Exact(2) ⇒ At(t) 该资源 peak = 2×3=6（对照手动 `Combination.Loop`(Footprint, LoopCount.Of(3), scope).OccupyClaims size 求和==6）；累积 net 也 ×3

**Iter20（§ 出处引用，静态检查）**：反射读 EffectScript.cs 公共 API，断言每个 public 类型/方法 XML 注释含 `§` 字符（至少一处）—轻量存在性检查，失败说明注释缺 §

**Iter24（§7 形状一致）**：断言 EffectScript 使用的 ResourceId 构造子种类 ⊆ §7 白名单已用种类（Gpu/CommandBuffer/Memory/Occupancy/SignalBus...），不发明新 kind（即 Footprint 只能含既有 ResourceId 子类；此为非运行时约束，写测试核对 `ResourceId` 枚举的已知子类与 §7 一致即可，或仅文档断言）

**Iter25（性能守卫）**：生成 1000 事件脚本，Audit 在合理时间完成（断言 < 5s）；中途可加少量随机冲突验证仍被检出

**约束**：
- 零 Godot 依赖；只引用 Cosmos.EffectAlgebra 命名空间既有类型
- 不重算代数，只调 `At`/`Audit`/`Combination.Loop`/`Peak`/`NetTable` 等
- 每个测试独立可证伪；用 xUnit `[Fact]`/`[Theory]` + `[InlineData]`
- 保持全解 0e/0w（`TreatWarningsAsErrors` 已开）

**运行（本机先 `$env:MSBUILD_EXE_PATH=$null`）**：`cd D:/Godot/Cosmos; $env:MSBUILD_EXE_PATH=$null; dotnet build Cosmos.EffectAlgebra.slnx` 0e/0w；`dotnet test` 全绿。失败必须修到绿再回复。

完成后回复：新增测试数；build 错误/警告；`dotnet test` 通过/失败数；一行结论。

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