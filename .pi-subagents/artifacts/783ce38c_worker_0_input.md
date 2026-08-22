# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是写代码 subagent，必须实际 write 文件并构建+自带断言验证。任务：修复 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 中 §3.3 派生度量 3 个数学正确性缺陷（迭代05 OPEN-1/2/3），构建绿且自制断言验证不误报。

环境：`cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly`

**根因**：`NatStar` 是 ℕ*（非负），但 §3.3.1 `net = Σcreate c.size − Σrelease c.size` 是有符号量，可为负。`NegateCore` 当前仅转置不取负，破坏有符号 net，连带 DO-9 误报。

**修复方案（用 write 整体重写涉及文件，避免漂移）：**

1. **新增有符号网值类型（新文件 `SignedNet.cs`）**：
   - `readonly record struct ZStar`：`IsTop`(bool)+`Value`(long)；`Top` 静态只读；`Of(long)`；运算符 `+`/`-(ZStar,ZStar)` 任一 IsTop⇒Top 否则值相加/减；`ContainsZero` 性质由值判断。注释 §3.3.1 有符号网值（ℤ∪{⊤}）。
   - `readonly record struct SignedInterval`：`Lo`/`Hi: ZStar`；构造子校验 `!Lo.IsTop && !Hi.IsTop && Lo.Value > Hi.Value` ⇒ 抛；`Zero` 静态只读 `= new(Of(0),Of(0))`；`ContainsZero` 属性 `=> Lo.Value <= 0 && Hi.Value >= 0`（或任一 IsTop⇒false）；`Merge(SignedInterval)` `new(Lo.Min(o.Lo), Hi.Max(o.Hi))`，Min/Max 内嵌 ZStar ⊤ 律（ZStar 加 `Min`/`Max`）。

2. **改 `NetTable`（Algebra.cs）内部用有符号类型**：
   - `_net` 字段改为 `Dictionary<ResourceId, SignedInterval>`。
   - `Compute`：遍历 `AllClaims()`，仅 occupy 桶（`if (c.Kind != Kind.Occupy) continue;`）；`var signed = c.Mode == Mode.Release ? Negate(c.Size) : ToSigned(c.Size);`；`Negate` 把 `Interval [lo,hi]`（lo,hi∈ℕ*）真取负得 `SignedInterval [-hi, -lo]`：`new SignedInterval(new ZStar.Of(-(long)hiValue), new ZStar.Of(-(long)loValue))`（处理 hi/lo IsTop⇒ZStar.Top）；`ToSigned` 把 `Interval [lo,hi]` → `SignedInterval [lo,hi]`（`Of((long)lo.Value)` 等，IsTop⇒Top）。合并 `cur.Merge(signed)`。
   - `this[ResourceId r]` 索引器返回 `SignedInterval`（缺省 `SignedInterval.Zero` 即 `[0,0]`？注意：缺省语义——PDR 缺省 size `[1,1]` 但 net 缺省应为「无该资源 ⇒ 不纳入」，建议缺省返回 `SignedInterval.Zero` 且 `IsConserved` 对零区间视为守恒？不——缺省无净效应更合理是「无占用」，但为保持 DO-9 不漏报，参照原设计 `Default=[1,1]` 语义：原 `IsConserved` 对缺省返回 false（不守恒）触发报警。为最小改动且保持 fail-closed：索引器缺省返回 `SignedInterval.Zero`（[0,0] 含 0），但 `IsConserved` 对**从未出现在 net 中的资源**（即不在 `_net` 键集）应视为需报警——这是不同语义。简化：保持 `_net` 只含出现过的资源；`IsConserved(r)` 当 `!_net.ContainsKey(Normalize(r))` 时返回 **false**（该资源无任何净效应记录 ⇒ 视为未闭合，fail-closed），否则按 `ContainsZero` 判定。）
   - `IsConserved(ResourceId r)`：`var key=Normalize(r); if(!_net.ContainsKey(key)) return false; var v=_net[key]; if(v.Lo.IsTop||v.Hi.IsTop) return false; return v.ContainsZero;`（区间含 0 ⇒ 可能闭合，不报警）。

3. **`Peak.Compute`（Algebra.cs）加 `mode≠release` 过滤**：循环内 `if (c.Mode == Mode.Release) continue;`（§3.3.2 公式 `c.mode≠release`）。kind 不限。

4. **`Deviation.cs` 的 `Calculate`**：它用 `NetTable` 比较——确认 `Calculate(Signature,Signature)` 内部改用 `NetTable.Compute` 取 `SignedInterval` 后，mid/range 从 `ZStar` 取（ZStar 有 `Value:long`）。若 `Calculate` 现依赖 `Interval` 取 mid/range，需改读 `SignedInterval.Lo/Hi.Value`（long）。**保持 §9.1 公式与 ⊤ 不崩溃语义不变**。

5. **`Derived.cs`/`DerivedMetrics.cs` 的 `IsConserved`/`Net` 便利封装**：确保返回类型/调用对齐新 `SignedInterval`（如 `Derived.IsConserved` 返回 bool 不变）。

**约束（用户铁律）：** 有符号边界由 `ZStar`/`SignedInterval` 类型强制（非运行时 if 漏判）；ℕ* 与 ℤ* 区分清晰；每条带 §3.3.1/§3.3.2 出处。

**自带验证（必须，写个小 xUnit 或临时 Program 断言，跑通后删除临时文件）：**
- `create[1,1] + release[1,1]`（同资源 occupy）⇒ net `[-1,1]`，`IsConserved`==true（含 0，不报警）。
- `create[1,1]` 无 release ⇒ net `[1,1]`，`IsConserved`==false（报警）。
- `create[1,2] + release[1,1]` ⇒ `[-1,2]`，`IsConserved`==true。
- `Peak` 含 release 时不计入（release claim 的 size 不进求和）。
**验证：** `dotnet build -clp:ErrorsOnly` 0 错误 0 警告；上述断言全绿（用临时测试或 Program.cs，验证后清理，保持 Tests 工程干净或留为正式测试均可，但不得留编译错误）。

**完成后最后一行回复：** FIX_OK 错误数=0，已修 OPEN-1(ZStar/SignedInterval 有符号 net)/OPEN-2(含0即守恒)/OPEN-3(Peak 排 release)；3 条断言全绿。

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