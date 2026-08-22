# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
你是一个写代码的 subagent。必须实际调用 write 工具写出文件，不得只输出计划。任务：重写 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/` 下的 `Numeric.cs`、`Objects.cs`、`Algebra.cs` 三个文件，使 `dotnet build` 零错误，并删除 `Class1.cs`。

环境（必读）：构建须在 PowerShell 执行且先 `$env:MSBUILD_EXE_PATH = $null`，否则会调用损坏的 VS MSBuild：
  cd D:/Godot/Cosmos/src/Cosmos.EffectAlgebra; $env:MSBUILD_EXE_PATH = $null; dotnet build -clp:ErrorsOnly

用户铁律：每个符号明确数学边界；类型能约束的用类型，约束不了的写注释（带 §x.y 出处）。

文件内容要求：
=== Numeric.cs ===
- `readonly record struct NatStar`：`IsTop`(bool) + `Value`(ulong)；`Top` 静态只读；`Of(ulong)` 静态；运算符 `+(NatStar,NatStar)` 任一 IsTop⇒Top，否则值相加；`*(NatStar,NatStar)` 任一 IsTop⇒Top，否则相乘；`Max(NatStar)` 任一 IsTop⇒Top；`Min(NatStar)` 非 Top 取 min，含 Top 时取另一个；`CompareToFinite(NatStar)` 先判 IsTop（两 Top⇒0；一 Top⇒±1；否则值比较）。注释引 §3.1.5a、MA-002。
- `readonly record struct Interval`：`Lo`/`Hi: NatStar`；构造子校验 !Lo.IsTop && !Hi.IsTop && Lo.Value>Hi.Value 时抛 ArgumentException（lo≤hi；[x,⊤] 合法因 Hi.IsTop）；`Default=新(Of(1),Of(1))`；`Dynamic=新(Of(1),Top)`；`Exact(ulong)`；`Merge(Interval)` 返回 `新(Lo.Min(o.Lo), Hi.Max(o.Hi))`（min/max 已内嵌 ⊤ 律）。注释引 §3.1.5/§3.1.5b。
- `readonly record struct DeviationVal`：`IsTop`(bool)+`Value`(double)；`Top` 静态只读；`Of(double)` 静态；`ExceedsThreshold(double)` 返回 `!IsTop && Value>threshold`。注释引 §3.1.5c、§9.1。

=== Objects.cs ===
顶部：`using System.Collections.Immutable;` 然后 `namespace Cosmos.EffectAlgebra;` 然后两个内部原语 `public readonly record struct Rid(string Value);` 和 `public readonly record struct StringName(string Value);`（替代 Godot 类型，注释：映射层 §7 负责转换，保持 L1 零 Godot 依赖）。
- `abstract record ResourceId` 与嵌套判别联合构造子：`Tree(NodePathOrUnknown Path)`、`Self(string Component)`、`Physics(Rid BodyId)`、`Memory(ulong Uid)`、`Disk(string Path)`、`Signal(StringName Name)`、`Gpu(Rid BufferId)`、`AudioMixer(int ChannelId)`、`Occupancy(string Channel)`、`Callback(string Id)`、`Network(int PeerId, string Method)`、`Input(string Action)`、`Custom(string Name)`、`CommandBuffer(string Channel)`、`SignalBus(StringName Name)`。
- `static ResourceId Normalize(ResourceId r)`：`Self s when s.Component.StartsWith("signal_") => new SignalBus(new StringName(s.Component["signal_".Length..]))`；`Signal sig when sig.Name.Value.StartsWith("signal_") => new SignalBus(sig.Name)`；其余 `=> r`。注释列 §3.1.4a 全裸名映射表。
- `readonly record struct NodePathOrUnknown`：`IsUnknown`(bool)+`Path`(string)；`Unknown` 静态只读；`Of(string)` 静态。
- `abstract record ScopeId` + 构造子 `Method(string)`/`Type(string)`/`Scene(string)`/`Global()`(用 `public sealed record Global : ScopeId;` 无字段)/`Shell()`/`Loop(string)`/`Conditional(string)`/`Async(string)`。`bool IncludedIn(ScopeId other)`：`Equals(other)||other is Global`（其余跨标签 false）。
- `enum Kind { Read, Write, Occupy }`、`enum Mode { Use, Create, Release, Move, Unknown }`。
- `readonly record struct Claim(Kind Kind, ResourceId Resource, Mode Mode, ScopeId Scope, Interval Size)`：`Normalize()` 返回 `this with { Resource = ResourceId.Normalize(Resource), Size = Size == default ? Interval.Default : Size }`。
- `sealed class Signature`：`private ImmutableHashSet<Claim> _read/_write/_occupy`（非 readonly，初始化 Empty）；`public ImmutableHashSet<Claim> ReadClaims/WriteClaims/OccupyClaims => _x;`；`private Signature(){}`；`public static readonly Signature Empty = new();`；`private Signature Add(Claim c)` 用 `this with`/new + 桶 Add；`public static Signature Union(Signature a, Signature b)`；`public static Signature Join(Signature a, Signature b) => Union(a,b)`；`public NetTable Net(ScopeId scope) => NetTable.Compute(this, scope);`。

=== Algebra.cs ===
顶部 `using System.Collections.Generic;` 和 `using System.Collections.Immutable;`。
- `static class Compatible`：`static bool IsCompatible(Mode a, Mode b)`：resolve Unknown⇒Use；若任一 Use⇒true；`(aa,bb) is (Create,Release) or (Release,Create) or (Create,Move) or (Move,Create) or (Release,Move) or (Move,Release) => true`；否则 false。注释 §3.2.3 P1-P4。
- `static class Weight`：`static double Of(Kind a, Kind b) => a==b ? 1.0 : double.NaN;`（注释 §3.3.2b，NaN 约定 ⊥）。
- `sealed class NetTable`：`private readonly Dictionary<ResourceId, Interval> _net = new();`；`public static NetTable Compute(Signature sig, ScopeId scope)` 遍历 `sig.AllClaims()`，跳过 `!c.Scope.IncludedIn(scope)`，按 `ResourceId.Normalize(c.Resource)` 分组，`c.Mode==Release` 取 `NegateCore(c.Size)` 否则 `c.Size`，合并 `cur.Merge(signed)`；`private static Interval NegateCore(Interval s)` 返回 `new Interval(s.Hi.IsTop?Top:Of(Hi.Value), s.Lo.IsTop?Top:Of(Lo.Value))`（即 [−hi,−lo]）；`public Interval this[ResourceId r]` 索引器 `{ var key=ResourceId.Normalize(r); return _net.TryGetValue(key,out var v)?v:Interval.Default; }`；`public bool IsConserved(ResourceId r)` 取 v，lo≤0≤hi（IsTop 视为未知 fail-closed）⇒ 闭合。
- `static class Peak`：`public static NatStar Compute(Signature sig, ScopeId scope)` 遍历 AllClaims 过滤 ⊆*，遇 `c.Size.Hi.IsTop` 返回 `NatStar.Top`，否则累加 `c.Size.Hi`。
- `static class SignatureExtensions`：`public static IEnumerable<Claim> AllClaims(this Signature sig)` 三个桶 yield return。

完成后必须：删除 `D:/Godot/Cosmos/src/Cosmos.EffectAlgebra/Class1.cs`；运行上述 build 命令确认 0 错误（若错，用 write 修文件直到绿）。最后一行回复：BUILD_OK 错误数=0，已写 Numeric.cs/Objects.cs/Algebra.cs。

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