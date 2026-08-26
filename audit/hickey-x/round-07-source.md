

===== THINK 4 =====

Key facts confirmed:
- `NatStar.Top = new(true, 0)` — IsTop=true, Value=0.
- `SerializeBudget` writes `kv.Value.Value` — for Top this writes 0 (the flip).
- `ParseBudget`: `dict[r] = NatStar.Of(prop.Value.GetUInt64())` — parses 0 as finite 0. So budget ⊤ → serialized 0 → parsed back finite 0. Confirmed statically; will verify by experiment.

Also interesting: `ParseBudget` calls `prop.Value.GetUInt64()` without checking ValueKind — if value is a string "⊤" it throws InvalidOperationException (not FormatException). And if negative... JSON can't have negative in GetUInt64 — throws. Also `budget` type wrong (array/string/number) → `TryGetProperty("budget") && bud.ValueKind == Object` fails → silently ignored → Budget.None. Confirmed at EffectScriptContract.cs:44-45.

Root-level unknown keys: Parse only reads "events" and "budget"; unknown keys ignored silently.

"Budget" uppercase: TryGetProperty is case-sensitive by default on JsonElement → ignored → silent default.

lifetime [⊤,⊤]: ParseInterval → ParseTop both → NatStar.Top → Interval(Top, Top) — legal per Interval ctor ([⊤,⊤] allowed). Then Audit: endpoints: Lo.IsTop skip, Hi.IsTop → anyOpenEnd=true, maxFinite=0 → sample point t = maxFinite+1 = 1. sweep: Lo.IsTop → continue (event never enters!). So event never enters the active set → gate(2) peak never counts it, gate(3) conflict never fires. But wait — samplePoints includes t=1 and the event is never alive per Alive() since Lo=⊤ > t. So a [⊤,⊤] lifetime event with huge occupy claim + tight budget → Audit passes with zero violations. That's experiment e.

But also note: closure check skips events with Loop ⊤ but not [⊤,⊤] lifetime with finite loop... wait: closure loop: `if (e.Loop.Count.IsTop) continue; if (e.Lifetime.Lo.IsTop) continue;` — Lo=⊤ skipped too. So no Leak either. Fully silent. 

Now R5-C2: At(t) vs Audit() concurrent counting divergence. Let me think about what R5-C2 was. Need to read round-06.md to know what R5-C2 said exactly. The task says: "两事件相同 footprint 重叠窗口 → At(t) vs Audit() 的并发计数分歧实测（R5-C2）".

Hypothesis: At(t) uses Combination.Loop which projects claims into scope and multiplies by loop count ω — producing ω copies of each claim. Audit's gate(2) peakSum adds hi*ω per event. For two events with same footprint overlapping, At(t) would show 2ω copies of the claim (union dedups identical claims! Signature is ImmutableHashSet — Union dedups!). So At(t) shows ONE copy of the identical claim (set semantics), while Audit peakSum shows 2×hi×ω (arithmetic sum). That's the divergence: Signature.Union is set-union (dedup), peak tracking is multiset-sum. Two events with identical footprint at same t: At(t) reports footprint once, Audit reports peak doubled. Which one is "right"? Peak should double (two distinct events). At(t) undercounts due to set dedup — that's arguably the bug: At loses multiplicity across distinct events.

Let me check Signature.Union and Combination.Loop quickly in Objects.cs / Algebra.cs. Also check Compatible.IsCompatible and ResourceId.Normalize, ScopeId equality (record struct — ScopeId.Scene("S") vs Method with name?). Note gate(3) groups by e.Scope; two events with same scope & same resource & same mode → group size 2 → conflict if mode incompatible with itself (create/create, move/move, release/release; use/use compatible).

Let me read Objects.cs and relevant parts of Algebra.cs.

===== THINK 5 =====

Now let me check the previous round reports quickly to understand what was already found (verification matrix), particularly round-06 which is the most recent. Let me read the last two rounds to build the verification matrix. Also I need to check the Combination.Loop for R5-C2 (At vs Audit concurrent counting divergence).

===== THINK 7 =====

Now let me quickly check the previous rounds' claims to build the 核实矩阵. The task says "前轮已确认（勿重复，但可用实验复核）：SerializeBudget ⊤→0 翻转、根级未知键忽略、budget 类型错静默、[⊤,⊤] lifetime 静默消失、scope 序列化只覆盖 4/8 子类、resource 5/15."

I've confirmed statically:
- SerializeBudget: `d[kv.Key] = kv.Value.Value` — for Top, Value=0 (NatStar.Top = new(true, 0)), so serializes as 0. Round-trip flips ⊤→0. ✓ (Contract line ~186)
- Root unknown keys ignored: Parse only TryGetProperty("events")/("budget"). ✓
- budget type error silent: `bud.ValueKind == JsonValueKind.Object` else keep None. ✓
- [⊤,⊤] lifetime silently vanishes: sweep `if (lt.Lo.IsTop) continue` — never alive, never reported. ✓
- Scope serialization 4/8: SerializeScope handles Scene/Method/Type/Global; Shell/Loop/Conditional/Async → FormatException. ✓
- Resource 5/15: ParseResource/SerializeResource handle Gpu/CommandBuffer/Memory/Occupancy/SignalBus out of 15 constructors (Tree/Self/Physics/Memory/Disk/Signal/Gpu/AudioMixer/Occupancy/Callback/Network/Input/Custom + CommandBuffer + SignalBus = 15). ✓

Let me check earlier rounds quickly to see what was already reported so my verification matrix is accurate. Let me skim rounds 1–5 headers quickly. Actually the task gave me the list of previously confirmed items; I'll verify those experimentally plus a few more. I don't need deep reading of all rounds — but a quick grep for their conclusions would help the matrix. Let me do a fast skim of round-05 and round-04 TOP sections.

Actually time budget is 25 min. Priority: build the rt-probe project and run experiments. Let me quickly skim rounds 4 and 5 for contract-related findings to verify.

===== THINK 8 =====

I have enough context. Let me also quickly check the EffectScriptEdgeTests to see what's already tested (to avoid duplicating), and check Compatible.IsCompatible for create×create. Then build the probe project.

Actually, time budget is 25 min. Let me move fast: create rt-probe with csproj + Program.cs covering experiments a–f, run, capture output, delete, write report.

Key details for the probe:
- Namespace Cosmos.EffectAlgebra.
- TFN net10.0 (tests use net10.0; dotnet SDK is 10.0.103).
- ProjectReference to src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj only (generator/analyzer are not needed at runtime — but wait, does EffectAlgebra.csproj reference the generator? Let me check quickly.)

Experiments:
a. Minimal legal JSON → Parse → ToJson → Parse idempotency. Compare JSON strings and/or re-parsed script equality. EffectScript doesn't implement Equals, so compare ToJson outputs of parse1 vs parse2, plus event counts.

b. C# script with NatStar.Top budget → ToJson → Parse → assert budget value (expect flip to 0). Build Dictionary<ResourceId,NatStar>{ {new ResourceId.Memory(7), NatStar.Top} }, Budget, EffectScript(events, budget) → ToJson → Parse → check parsed.Budget.Caps[new Memory(7)] → expect Of(0) i.e. !IsTop && Value==0.

c. C# script with ScopeId.Loop("L") → ToJson expect FormatException. Also Shell.

d. Root unknown key / "Budget" capital / budget:[] — three silent paths: Parse succeeds silently; observe caps count == 0 for Budget/budget:[].

e. [⊤,⊤] lifetime event → Audit output: expect silent no violation (event never alive). Also At(0) empty.

f. Two events same footprint overlapping windows → At(t) vs Audit() concurrency count divergence: At(t).OccupyClaims.Count==1 while Audit reports conflict + peak 2 (with budget cap memory=1 → PeakExceeded).

Also maybe extra probe: lifetime ["⊤","⊤"] via JSON parses fine (ParseTop accepts ⊤ for lo). And SerializeBudget Top→0 in JSON text directly ("memory:7":0).

Let me write Program.cs with a small harness: Run(name, expected, func returning observed string), print PASS/FAIL table.

Careful with C# top-level statements + record types usage. Use fully qualified or using directives.

For building events: new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(10)), new ScopeId.Scene("S"), Signature.Of(new Claim(Kind.Occupy, new ResourceId.Memory(42), Mode.Create, new ScopeId.Scene("S"), Interval.Exact(1))), LoopCount.Of(1)).

Note Claim.Normalize called in ParseClaim; in C# construction Signature.Of normalizes anyway.

For experiment f: two events same footprint occupy gpu mode=create... wait create×create conflict requires group size ≥2 with same (r, scope, mode). Use Mode.Create occupy claims. Audit → CompatibleConflict. Also add budget cap to see PeakExceeded? Keep simple: report violations list kinds + At(t).OccupyClaims.Count.

For e: event lifetime Interval(NatStar.Top, NatStar.Top), occupy create claim size [1,1], loop 1 → Audit: sweep skips Lo=⊤; endpoints empty; anyFinite false; samplePoints=[0]; closureT=0; closure loop skips Lo=⊤. So no violations → Passed=true, silent. At(NatStar.Of(0)) → empty signature (Alive: Lo=⊤ CompareToFinite(0)=1 >0 → not alive). Expect Passed=true, 0 violations — silent disappearance.

For d: JSON strings:
- rootUnknown: {"events":[...],"note":"hi"} → Parse OK.
- "Budget": {...} → caps 0.
- "budget": [] → caps 0.

Also b-check JSON text: after ToJson of Top-budget script, grep "\"memory:7\":0".

Experiment a minimal legal JSON:
{"events":[{"lifetime":[0,5],"scope":{"scene":"S"},"loop":1,"footprint":[{"kind":"occupy","resource":{"gpu":"buf1"},"mode":"use","scope":{"scene":"S"},"size":[1,1]}]}],"budget":{"gpu:buf1":4}}

Parse → ToJson → Parse again → compare second ToJson with first ToJson. Also check round-trip stability (ToJson(Parse(ToJson(p))) == ToJson(p)). Potential idempotency issue: SerializeClaim emits size always (defaulting [1,1]); Parse reads it back. Scope Scene serializes as {"scene":"S"} without type; Parse infers Scene. Fine. But note: footprint order! ReadClaims/WriteClaims/OccupyClaims are ImmutableHashSet iteration order — nondeterministic-ish but stable within process for same content. For a single-claim footprint it's deterministic. Use single claim.

One more potential finding to test: budget key "gpu:" with empty name → ParseResourceKey gives Gpu(Rid("")) — silent empty. And ReqStr prevents empty for events but not budget keys. Could add as extra observation (d2): budget key "gpu:" → accepted, Gpu("") — inconsistent with event-level fail-fast. That's a nice NEW finding candidate if not already reported. R6 mentioned budget GetUInt64 raw errors; empty-key thing may be new. I'll include as experiment g (bonus) — cheap.

Also h: negative number in budget e.g. {"gpu:x":-1} → JsonException from GetUInt64 (raw .NET exception, no field context) — confirms R6 E-series; quick to test.

Now csproj: does EffectAlgebra.csproj reference generator? Check quickly then write files.

===== THINK 9 =====

Now I have everything I need. Let me create the probe project. TFM: net10.0 (matches tests). ProjectReference to src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj.

Program.cs experiments:

**Exp A**: Minimal legal JSON → Parse → ToJson → Parse → idempotence check.
Minimal JSON:
```json
{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[]}]}
```
Note footprint: [] — empty claims array is fine (ParseFootprint iterates, Signature.Of() with empty). SerializeEvent writes lifetime/scope/loop/footprint. Second parse should work. Idempotence: compare json1 == json2 (string equality) and also structural: events count etc.

**Exp B**: C# construct script with Budget{gpu:x: NatStar.Top} → ToJson → Parse → inspect parsed budget. Expect Top flipped to 0.

```csharp
var caps = new Dictionary<ResourceId, NatStar> { [new ResourceId.Gpu(new Rid("x"))] = NatStar.Top };
var script = new EffectScript(ImmutableArray.Create(ev), new Budget(caps));
var json = EffectScriptContract.ToJson(script);
var reparsed = EffectScriptContract.Parse(json);
reparsed.Budget.Caps.TryGetValue(new ResourceId.Gpu(new Rid("x")), out var v) → v.IsTop? v.Value?
```
Note: Budget caps keys — ParseBudget creates ResourceId via ParseResourceKey; keys "gpu:x". The reparsed dict will contain key Gpu(Rid("x")). Lookup by same key works (record equality).

Expected per prior rounds: budget serialized as `kv.Value.Value` = 0 → reparsed cap = Of(0) finite 0, IsTop=false. PASS if flipped (i.e., FAIL of contract).

Also worth checking in exp B: does the flipped 0 budget change Audit? Could add a peak>0 event and show PeakExceeded after round-trip vs none before. That strengthens severity. Let me include it: event occupy gpu [1,1] create... Actually simpler: occupy with mode Use size [5,5], cap ⊤ originally → no violation; after round-trip cap=0 → PeakExceeded. Good demonstration. But keep runtime simple; I'll include it.

**Exp C**: C# script with ScopeId.Loop("L") / Shell → ToJson expect FormatException. Catch and report exception type+message. Expect throw ⇒ confirms non-representable scopes. Note: ScopeId.Loop record has Name field; constructor Loop(string Id)? `public sealed record Loop(string Id) : ScopeId;` positional param name Id.

**Exp D**: three silent paths:
- d1: root unknown key: `{"events":[],"foo":123}` → Parse succeeds? observe.
- d2: `"Budget"` capital: `{"events":[{"lifetime":[0,5],"scope":{"scene":"S"},"footprint":[]}],"Budget":{"gpu:x":9}}` → Parse; then check script.Budget.Caps.Count == 0? And to prove gate disabled: add an occupy claim size [100,100] with Use mode... wait Use contributes to peak. Event occupy use [100,100], budget {"gpu:y":9} under wrong case → Audit passed (no PeakExceeded) despite peak 100 > cap 9. Compare with correct lowercase → PeakExceeded.
- d3: `"budget":[]` type error → Parse succeeds silently, caps None. Also audit passes.

**Exp E**: [⊤,⊤] lifetime event JSON: `{"lifetime":["⊤","⊤"],...}` → Parse OK; Audit → Passed true, zero violations, and At(t) empty forever. Also sweep skips (Lo=⊤ continue at EffectScript.cs:157-159). Observe: violations count 0. Also demonstrate that even with budget cap and huge footprint, no violation — silent disappearance.

Wait — actually for [⊤,⊤]: samplePoints: anyFinite false → endpoints empty; anyOpenEnd true → samplePoints.Add(Of(maxFinite+1)) where maxFinite=0 → t=1. Hmm! anyOpenEnd is set when lt.Hi.IsTop — [⊤,⊤] has Hi=⊤ so anyOpenEnd=true, sample point t=1 added. But sweep skips the event (Lo=⊤), so active set empty at all samples. net/peak/grp all empty. Audit passes. At(1): Alive([⊤,⊤], 1): Lo=⊤ CompareToFinite(1)=1 > 0 → not alive. So event never alive anywhere. Silent. Confirmed statically; experiment verifies.

Interesting extra: what about lifetime ["⊤","⊤"] with loop ⊤ and occupy — completely invisible. Yes.

**Exp F**: R5-C2 divergence: two identical events, overlapping lifetimes:
```
e1: lifetime [0,10], scope Scene("S"), footprint [occupy {gpu:"buf"} use size [1,1]], loop 1
e2: same
```
At(5): OccupyClaims count → 1 (set-dedup). Audit(): expect CompatibleConflict? Wait mode Use — Use×Use is compatible (self-compatible)! gate(3): group (r,S,Use) count 2 → Compatible.IsCompatible(Use,Use)==true → skip. So no conflict for Use. But peakSum: both events contribute → peak 2. With cap {"gpu:buf": 1} → PeakExceeded (peak 2 > 1). So divergence measurable: At(5) shows 1 claim (implying peak 1 ≤ cap 1 → user self-check passes) while Audit reports PeakExceeded. 

R5-C2 said conflict + peak 2 with create mode. Let me do BOTH: (f1) Use-mode two events + cap 1 → At suggests fine, Audit says PeakExceeded; (f2) create-mode two events → Audit says CompatibleConflict too while At(t) union shows single claim. I'll measure At(t).OccupyClaims.Count and each claim's Size, plus Audit violations.

Also note At's claim Size after Combination.Loop: scaled [1,1]×1. Two events dedup → count 1. 

Let me now also think about NEW findings I can hunt during the probe (things previous rounds missed). Candidates visible from code reading:

1. **SerializeClaim always emits explicit `size`** even when original was null→Default — minor, symmetric though since Parse defaults missing size to Default anyway. Not a bug.

2. **`loop` always emitted** — fine.

3. **ToJson emits `"size":[lo,hi]` with `(c.Size ?? Interval.Default)` computed twice — fine.

4. **ParseBudget: `prop.Value.GetUInt64()` on string values throws InvalidOperationException without field context** — known family (R6 E1/R1 residual 3).

5. **NEW: ParseBudget accepts ⊤ neither — but worse: budget value 0 is accepted and means "cap 0"** — combined with N1 flip: after round-trip, ⊤ becomes 0 which is *distinguishable* from "no cap" but semantically inverted (most permissive → most restrictive). Already known.

6. **NEW candidate: `EffectScript.Audit()` parameterless uses Budget — but Parse-built scripts with flipped budgets produce opposite verdicts pre/post serialization.** Part of B.

7. **NEW candidate: root-level `budget: {}` empty object** → caps stays None (fine, same as absent). OK.

8. **NEW candidate: duplicate resource keys inside one claim's resource object**, e.g. `{"gpu":"a","memory":7}` → first-match-wins (gpu checked first) silently. TryGetProperty chain order determines winner — silent precedence. Minor fail-soft: ambiguous shape silently resolved by fixed priority. Worth listing as LOW new finding (D-series adjacent but distinct: multiple valid keys present).

9. **NEW candidate: `ParseInterval` accepts `[⊤, x]`?** ParseTop allows lo="⊤" → Interval(Top, finite) ctor throws ArgumentException (not FormatException!) — uncaught exception type leak: contract promises FormatException for illegal shapes, but `[ "⊤", 5 ]` yields ArgumentException from Interval ctor (Numeric.cs:91). That's an exception-type contract violation — caller catching FormatException gets stabbed by ArgumentException. Verify in experiment (g). Nice new finding!

10. **NEW candidate: negative numbers**: `GetUInt64()` on -1 throws FormatException (System.Text.Json) — actually GetUInt64 on negative number throws FormatException. On "5" string → InvalidOperationException. Type zoo again (known family).

11. **NEW candidate: lifetime hi overflow e.g. 18446744073709551615 (ulong.Max)**: maxFinite + 1 wraps to 0 in sweep line! EffectScript.cs:151 `samplePoints.Add(NatStar.Of(maxFinite + 1))` — if maxFinite == ulong.MaxValue, wraps to 0 → sample at t=0 instead of beyond-end. Also closureT = ulong.MaxValue. Edge case, LOW. Could test: single event [ulongMax, ulongMax] occupy... hmm interesting but obscure. Maybe skip or quick test. Actually this is a genuine wraparound: hi=2^64-1, anyOpenEnd=false, endpoints={Max}, samplePoints=[Max]. closureT=Max. No wrap in this path since anyOpenEnd false. Wrap needs open-end AND maxFinite==ulong.MaxValue: e.g., events [5, ulongMax] and [0,"⊤"]... maxFinite = ulongMax, anyOpenEnd=true → sample point Of(Max+1)=Of(0). Sample at t=0 already exists? endpoints contains 0 and Max. samplePoints would be [0, ..., Max, 0]?? List adds Of(0) at end — out of order! samplePoints sorted assumption broken ("SortedSet 已排序 ⇒ 输出确定性" comment lies). Sweep processing: phase1 applies time < tv... for tv=0 second time: nothing left. Probably harmless-ish but ordering invariant broken. Too obscure; skip experiment, maybe mention as observation only if cheap. Skip — 25min budget.

12. **NEW candidate: `ParseScope` for claim-level scope vs event-level**: SerializeClaim serializes c.Scope (post-Combination? No—SerializeEvent serializes e.Footprint raw claims whose c.Scope is whatever was stored; but At projects c.Scope := e.Scope via Combination.Loop). So JSON round-trip preserves claim.scope, yet Audit IGNORES claim.scope entirely (gate(3) groups by e.Scope, net ignores scope). So the JSON contract makes users believe claim-level scope matters (it's required per claim! Require(c, "scope")), but semantics ignore it. If claim.scope ≠ event.scope, JSON parses happily, audits with e.Scope — silent divergence between declared data and used data. That's a REAL new finding (MED): required-but-ignored field. Test: event scope Scene("A"), claim scope Scene("B") → parse ok; audit behavior identical to claim scope A? Can't easily show difference without deep comparison, but can show parse accepts and audit runs; the ignored-ness is proven by code (EffectScript.cs:175-177 uses e.Scope). Experiment: just confirm accept + cite lines. Also SerializeClaim writes c.Scope back out — round-trip keeps B. So the file faithfully round-trips a field the engine never reads. Hickey: "the schema demands what the machine discards."

13. **NEW: `Require(ev,"lifetime")` etc give no index** — known E-family (zero location info), skip.

14. **NEW: ToJson indented output includes `"loop": 1` even for default** — noise, skip.

15. **Check `Signature.Of()` with empty claims array — footprint [] allowed.** Fine.

16. **`ParseTop` accepts fractional numbers?** GetUInt64 on 1.5 throws FormatException. Fine (fail-fast-ish).

17. **Numbers as strings**: lifetime [0, "10"] → string "10" not "⊤" → FormatException "端点须为数字或 ⊤". Good.

18. What about **JSON number 1e3 (scientific)**? GetUInt64 throws FormatException probably. Skip.

19. **`"events": []` empty + budget only** — fine.

20. **Duplicate events dedup in At but not Audit** — covered by F.

21. **`budget` with unknown-key resource e.g. "vram:1"** → FormatException (good). But **`"memory:"` empty suffix → Memory(0)** silently (Contract ParseResourceKey memory branch allows empty → uid 0). Silent default! `k["memory:".Length..].Length > 0 ? ulong.Parse(...) : 0` — explicit silent fallback to 0. New small finding (LOW-MED): "memory:" → Memory(0) silently, whereas every other resource requires non-empty. Inconsistent within the SAME switch. Test in g.

22. **Case sensitivity of budget resource prefixes**: "GPU:x" → FormatException good.

23. **What about `scope.type` value non-string, e.g. `{"type":3}`** → ty.GetString() on Number returns "3"? JsonElement.GetString() on number returns raw text "3" → switch falls to `_` → FormatException. OK-ish.

24. **`{"scene":42}` → GetString() gives "42"** — known E4 (R6).

So my new-finding shortlist for experiments:
- G1: `["⊤", 5]` lifetime → ArgumentException (not FormatException) — exception-type contract breach. HIGH-ish/MED.
- G2: claim-level `scope` required but semantically ignored by Audit/At (only e.Scope used) — MED. Plus SerializeClaim re-emits it, giving illusion of effect.
- G3: `"memory:"` empty uid → Memory(0) silently (inconsistent with sibling resources' ReqStr) — LOW.
- G4: duplicate keys in resource object → silent priority resolution (first-match in hardcoded order gpu > commandBuffer > memory > occupancy > signalBus) — LOW. Test: `{"resource":{"memory":7,"gpu":"a"}}`.
- G5: budget ⊤ flip changes audit verdict (part of Exp B extension) — reinforces N1.

Also confirm the six assigned experiments a–f.

Time to write the probe. Keep Program.cs compact but complete, print structured PASS/FAIL lines. Use top-level statements. TreatWarningsAsErrors — set false in probe csproj. TFM net10.0.

One concern: build might be slow (analyzers?). The library csproj references Generator? No — library itself doesn't reference generator; tests do. Library builds plain. dotnet run on probe will build library first. Should be OK within budget. Use `dotnet run -c Release` maybe faster build config? Debug fine.

Let me write files now. Directory: D:/Godot/Cosmos/audit/hickey-x/rt-probe/.

Program.cs sketch:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using Cosmos.EffectAlgebra;

int pass = 0, fail = 0;
void Report(string id, bool ok, string detail) { ... Console.WriteLine($"[{(ok?"PASS":"FAIL")}] {id}: {detail}"); if(ok) pass++; else fail++; }

// helpers
static string J(object o) => JsonSerializer.Serialize(o);
EffectEvent Ev(int lo, int hi, string scene, params Claim[] claims) ...
```

Careful with Claim construction: `new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("buf")), Mode.Use, new ScopeId.Scene("S"), Interval.Exact(1))`.

Experiment details:

**A (idempotence)**:
```csharp
const string minimal = """{"events":[{"lifetime":[0,10],"scope":{"scene":"S"},"footprint":[]}]}""";
var s1 = Parse(minimal); var j1 = ToJson(s1); var s2 = Parse(j1); var j2 = ToJson(s2);
ok = j1 == j2 && s1.Events.Length == s2.Events.Length;
```
Print j1. Also compare budgets equal. Note: is j1 == minimal? No (formatting differs); idempotence means ToJson∘Parse stable from first canonicalization. Check j1==j2 (fixed point) AND semantic equality Events equal (record struct Equality => EffectEvent is readonly record struct with Signature member — Signature has custom Equals! EffectEvent record struct auto-generated Equals uses field comparison; Signature is a class field → reference equality unless it overrides... record struct generated equality compares fields via EqualityComparer<Signature>.Default → Signature.Equals(object) override exists → Default uses IEquatable<T>? Signature implements Equals(Signature?) public — that's IEquatable<Signature> implicitly? Public bool Equals(Signature? other) satisfies interface only if declared; as a public method named Equals(T) it IS the pattern; EqualityComparer<T>.Default checks IEquatable<Signature> interface implementation. Without `: IEquatable<Signature>` declaration, the compiler-generated record struct equality uses Comparer via typeof match... Actually for record structs containing non-record class fields, generated Equals uses EqualityComparer<TField>.Default per field. Default<T> prefers IEquatable<T>. Signature defines public bool Equals(Signature?) but doesn't declare IEquatable<Signature>? For classes, implementing the method pattern without declaring interface means it does NOT implement IEquatable. Then Default falls back to object.Equals → virtual override Equals(object?) exists → structural works anyway. So struct equality works structurally. Fine — but simpler: compare j1==j2 strings plus re-parse counts. Good enough.)

**B (⊤ flip)**:
```csharp
var caps = new Dictionary<ResourceId,NatStar>{[new ResourceId.Gpu(new Rid("x"))]=NatStar.Top};
var ev = new EffectEvent(new Interval(NatStar.Of(0),NatStar.Of(10)), new ScopeId.Scene("S"),
    Signature.Of(new Claim(Kind.Occupy,new ResourceId.Gpu(new Rid("x")),Mode.Use,new ScopeId.Scene("S"),Interval.Exact(5))));
var script = new EffectScript(ImmutableArray.Create(ev), new Budget(caps));
var before = script.Audit(); // expect pass
var json = ToJson(script);
var rt = Parse(json);
var after = rt.Audit(); // expect PeakExceeded (peak 5 > cap 0)
caps lookup in rt: rt.Budget.Caps[new Gpu(Rid x)] → IsTop false, Value 0
```
PASS(finding confirmed) criteria: !rtCap.IsTop && rtCap.Value==0 && before.Passed && !after.Passed.

**C (Loop/Shell scope serialize)**:
```csharp
foreach scope in [Loop("L"), Shell(), Async("A"), Conditional("B")]:
  try { ToJson(script with scope) ; ok=false } catch(FormatException ex){ ok=true; }
```
Expect FormatException per Contract:200-205. Confirm actual exception type/message.

**D (three silent paths)**:
- d1 unknown root key: `{"events":[],"wat":1}` → Parse succeeds? Record result (expect success = silent).
- d2 "Budget" uppercase: events with occupy use size Exact(100) gpu buf; JSON handwritten with "Budget":{"gpu:buf":9}. Parse → Budget.Caps.Count==0 → Audit passed=true (gate off). Control: lowercase correct → PeakExceeded present. 
- d3 budget:[] : `{"events":[...],"budget":[]}` → parse ok, caps None, audit passes despite peak>cap-if-it-had-been-parsed... simply record Caps.Count==0 & parse success.

For d2/d3 need full handwritten JSON. Build via string interpolation carefully. Event JSON: {"lifetime":[0,10],"scope":{"scene":"S"},"loop":1,"footprint":[{"kind":"occupy","resource":{"gpu":"buf"},"mode":"use","scope":{"scene":"S"},"size":[100,100]}]}.

**E ([⊤,⊤] silence)**:
JSON: lifetime ["⊤","⊤"], footprint occupy use gpu buf size [999,999], budget {"gpu:buf":1}.
Parse → ok. Audit → expect Passed=true 0 violations (silent). Also At(0)/At(1000) OccupyClaims.Count==0 (never alive). Record.

**F (At vs Audit divergence)**:
two identical events [0,10] occupy Use gpu buf [1,1]; budget {"gpu:buf":1}.
at5 = script.At(Of(5)).OccupyClaims.Count → expect 1.
audit → expect PeakExceeded (sum 2 > 1).
Second variant create mode: audit → expect CompatibleConflict additionally; At still 1.
Record counts + violation kinds.

**G extras**:
- g1: lifetime ["⊤",5] → catch exception type name. Expect ArgumentException proving contract breach (doc says FormatException).
- g2: claim scope mismatch: event scope {"scene":"EVENT"}, claim scope {"scene":"CLAIM"} → parse ok; ToJson round-trip keeps CLAIM; audit unaffected (can't diff directly, but assert parse accepts + cite code lines in report). Show that re-serialized JSON still contains CLAIM (faithful storage, unused semantics).
- g3: budget key "memory:" → Memory(0) silent; also footprint resource {"memory":""}? ReqStr blocks empty string for memory? Look: memory branch: `mem.GetUInt64()` numeric required — {"memory":""} → ValueKind String → throw FormatException good. But budget KEY "memory:" empty suffix → Memory(0). Record.
- g4: resource {"memory":7,"gpu":"a"} both keys → which wins? Expect Gpu("a") (order of TryGetProperty checks). Record observed winner = silent priority.

Output format per experiment: ID, description, expected, actual, verdict PASS/FAIL where PASS = matches expected-from-contract... Careful semantics: task says "每个实验输出 PASS/FAIL + 实际观察值". I'll define PASS = 观察与「契约应有行为」一致；FAIL = 发现契约违背（即历史结论复核成立或新问题）. State clearly in report.

Let me write the program. Watch out: `ScopeId.Loop` — `Loop` conflicts? `new ScopeId.Loop("L")`. Shell: `new ScopeId.Shell()`. Async("A"), Conditional("C").

For C# script construction helper:
```csharp
static EffectEvent MkEv(ScopeId scope, params Claim[] claims)
  => new(new Interval(NatStar.Of(0), NatStar.Of(10)), scope, Signature.Of(claims), LoopCount.Of(1));
static Claim Occ(Mode m, ResourceId r, ulong sz, ScopeId sc)
  => new(Kind.Occupy, r, m, sc, Interval.Exact(sz));
```

ToJson for C: script must contain the exotic scope at EVENT level (SerializeScope(e.Scope)) — yes event-level triggers.

Note: EffectScript ctor takes ImmutableArray<EffectEvent> — fine.

Nullable warnings: enable nullable but TreatWarningsAsErrors false in probe.

One more check for B: `script.Audit()` — parameterless Audit uses Budget property. Before-roundtrip budget ⊤ → p.CompareToFinite(⊤)>0 false → pass. After flip cap=0: peak 5 > 0 → PeakExceeded. 

Also verify ToJson output content for B: prints "gpu:x": 0.

Edge: In Parse, budget dict keys: Gpu(Rid("x")). rt.Budget.Caps[new ResourceId.Gpu(new Rid("x"))] works via record equality hash. Good.

For F: At signature — `public Signature At(NatStar t)`. OccupyClaims is ImmutableHashSet<Claim>. Count property.

Audit control for d2 lowercase: budget {"gpu:buf":9}, event occupy use [100,100] → peak 100 > 9 PeakExceeded. Good.

Write csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../src/Cosmos.EffectAlgebra/Cosmos.EffectAlgebra.csproj" />
  </ItemGroup>
</Project>
```

Path relative: rt-probe under audit/hickey-x/rt-probe → ../../../src/... = D:/Godot/Cosmos/src/... ✓ (audit/hickey-x/rt-probe → up3 = Cosmos root).

Hmm wait: referencing multi-target project (net9.0;net10.0) from net10.0 exe picks net10.0 asset. EffectScriptContract excluded from net9.0 only. Good.

Now Program.cs full text:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using Cosmos.EffectAlgebra;

var pass = 0; var fail = 0;
void Report(string id, bool contractHeld, string expected, string actual)
{
    if (contractHeld) pass++; else fail++;
    Console.WriteLine($"[{(contractHeld ? "PASS" : "FAIL")}] {id}\n  预期: {expected}\n  实际: {actual}");
}
string Try<T>(Func<T> f, out T val) { ... } // maybe simpler inline try/catch per test

Console.WriteLine("=== rt-probe: EffectScriptContract round-trip 实验 ===");

// ── A ──
{
    const string minimal = "{\"events\":[{\"lifetime\":[0,10],\"scope\":{\"scene\":\"S\"},\"footprint\":[]}]}";
    try
    {
        var s1 = EffectScriptContract.Parse(minimal);
        var j1 = EffectScriptContract.ToJson(s1);
        var s2 = EffectScriptContract.Parse(j1);
        var j2 = EffectScriptContract.ToJson(s2);
        var idempotent = j1 == j2;
        var semanticallyEqual = s1.Events.Length == s2.Events.Length && s1.Events[0].Equals(s2.Events[0]) && s1.Budget.Caps.Count == s2.Budget.Caps.Count;
        Report("A 最小剧本 round-trip 幂等", idempotent && semanticallyEqual,
            "ToJson∘Parse 不动点（j1==j2）且事件语义相等",
            $"j1==j2: {idempotent}; 语义相等: {semanticallyEqual}; j1={Compact(j1)}");
    }
    catch (Exception ex) { Report("A", false, "无异常", $"{ex.GetType().Name}: {ex.Message}"); }
}
...
```

Compact(j1): remove newlines/indent spaces for printing. `j1.Replace("\n"," ").Replace("  ","")` rough.

I'll write it all now. Also track total elapsed — keep moving fast.

For D2 control I need handwritten JSON with proper escaping in C#. Use raw string literals (C# 11): `"""..."""` — LangVersion latest, fine.

Let me be careful about one thing in D2: JSON budget key "gpu:buf" — lowercase control. And event footprint claims require scope too (Require(c,"scope")).

Full Program.cs below. I'll aim for clarity and completeness in one shot.

Also for E, additionally demonstrate the "audit blind spot": even budget {"gpu:buf":1} with huge footprint [999,999] passes. Include that in the E JSON.

And for F-create variant, separate script without budget: violations kinds list.

Now write.

===== WRITE (write) =====

// rt-probe — Hickey-X R7 round-trip / fail-soft 边界实验探针。只读项目源码；本目录为临时产物，实验后删除。
using System.Text.Json;
using Cosmos.EffectAlgebra;

int pass = 0, fail = 0;
void Report(string id, bool ok, string detail)
{
    if (ok) pass++; else fail++;
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {id} :: {detail}");
}
string Trunc(string s, int n = 400) => s.Length <= n ? s : s[..n] + "...";

// ── 共用构造 ──
Signature Occupy(string rid, Mode m, ulong lo, ulong hi) =>
    Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid(rid)), m,
        new ScopeId.Scene("S"), Interval.Exact(lo).Merge(Interval.Exact(hi))).Normalize());

EffectEvent Ev(ulong lo, ulong hi) => new(
    new Interval(NatStar.Of(lo), NatStar.Of(hi)),
    new ScopeId.Scene("S"),
    Occupy("gpu:bufA", Mode.Create, 1, 1),
    LoopCount.Of(1));

// ════════ EXP-a 最小合法剧本 JSON → Parse → ToJson → Parse 幂等性 ════════
try
{
    var json = """
    {
      "events": [
        {
          "lifetime": [0, 10],
          "scope": { "type": "scene", "scene": "S" },
          "loop": 1,
          "footprint": [
            { "kind": "occupy", "resource": { "gpu": "bufA" }, "mode": "create",
              "scope": { "scene": "S" }, "size": [1, 1] }
          ]
        }
      ],
      "budget": { "gpu:bufA": 5 }
    }
    """;
    var s1 = EffectScriptContract.Parse(json);
    var j2 = EffectScriptContract.ToJson(s1);
    var s2 = EffectScriptContract.Parse(j2);
    var j3 = EffectScriptContract.ToJson(s2);
    Report("a.idempotent-json", j2 == j3, $"ToJson∘Parse 幂等={(j2 == j3)}");
    // 语义层：事件数 / 预算值
    var capOk = s2.Budget.Caps.TryGetValue(ResourceId.Normalize(new ResourceId.Gpu(new Rid("bufA"))), out var cap) && !cap.IsTop && cap.Value == 5;
    Report("a.budget-value", capOk, $"budget 解析后 = {(capOk ? cap.Value.ToString() : "缺失")}");
    Report("a.event-count", s2.Events.Length == 1 && s1.Events.Length == 1, $"events={s1.Events.Length}->{s2.Events.Length}");
}
catch (Exception ex) { Report("a.minimal-roundtrip", false, $"异常: {ex.GetType().Name}: {Trunc(ex.Message)}"); }

// ════════ EXP-b NatStar.Top 预算 C#→ToJson→Parse 翻转实测（R2-N1 复核）════════
try
{
    var caps = new Dictionary<ResourceId, NatStar> { [new ResourceId.Gpu(new Rid("bufTop"))] = NatStar.Top };
    var script = new EffectScript(ImmutableArray.Create(Ev(0, 5)), new Budget(caps));
    var json = EffectScriptContract.ToJson(script);
    Console.WriteLine($"  b.序列化 JSON budget 段: {Trunc(json[json.IndexOf("budget")..], 120)}");
    var reparsed = EffectScriptContract.Parse(json);
    var key = new ResourceId.Gpu(new Rid("bufTop"));
    var found = reparsed.Budget.Caps.TryGetValue(key, out var v2);
    bool flipped = found && !v2.IsTop && v2.Value == 0;
    Report("b.top-flip-to-0", flipped, $"⊤ 预算 round-trip 后 = {(found ? (v2.IsTop ? "⊤" : v2.Value.ToString()) : "键消失")} ⇒ {(flipped ? "翻转已证实（仍存在）" : "未翻转/已修")}");
    // 后果链：预算 0 + 占用峰值 1 ⇒ PeakExceeded？
    var aud = reparsed.Audit();
    Console.WriteLine($"  b.重解析后 Audit Passed={aud.Passed}, violations={aud.Violations.Length} [{string.Join("; ", aud.Violations.Select(v => $"{v.Kind}@{v.AtT}"))}]");
}
catch (Exception ex) { Report("b.top-flip-to-0", false, $"异常中断: {ex.GetType().Name}: {Trunc(ex.Message)}"); }

// ════════ EXP-c ScopeId.Loop/Shell → ToJson 抛 FormatException（不可表示面实测）════════
foreach (var (nm, sc) in new (string, ScopeId)[] { ("Loop", new ScopeId.Loop("L1")), ("Shell", new ScopeId.Shell()), ("Conditional", new ScopeId.Conditional("B")), ("Async", new ScopeId.Async("A")) })
{
    try
    {
        var script = new EffectScript(ImmutableArray.Create(
            new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(3)), sc,
                Signature.Of(new Claim(Kind.Occupy, new ResourceId.Gpu(new Rid("x")), Mode.Use, sc, Interval.Default).Normalize()), LoopCount.Of(1))));
        var js = EffectScriptContract.ToJson(script);
        Report($"c.scope-{nm}", false, $"预期 FormatException，实际成功序列化: {Trunc(js.Replace('\n', ' '), 150)}");
    }
    catch (FormatException fex) { Report($"c.scope-{nm}", true, $"FormatException ✓ ({Trunc(fex.Message, 90)})"); }
    catch (Exception ex) { Report($"c.scope-{nm}", false, $"异常类型不符: {ex.GetType().Name}: {Trunc(ex.Message, 90)}"); }
}

// ════════ EXP-d 三条静默路径实测：根级未知键 / Budget 大写 / budget:[] ════════
const string evBody = """
      "events": [ { "lifetime": [0, 4], "scope": { "scene": "S" },
        "footprint": [ { "kind": "occupy", "resource": { "gpu": "g1" }, "mode": "use",
          "scope": { "scene": "S" }, "size": [1, 1] } ] } ]
    """;
// d1 根级未知键
try
{
    var s = EffectScriptContract.Parse($"{{ {evBody}, \"budgat\": {{ \"gpu:g1\": 0 }} }}");
    Report("d1.unknown-root-key", false, $"未知键 'budgat' 被静默忽略；budget.Caps.Count={s.Budget.Caps.Count}（预期抛错）");
}
catch (Exception ex) { Report("d1.unknown-root-key", true, $"已拒绝: {ex.GetType().Name}"); }
// d2 Budget 大写
try
{
    var s = EffectScriptContract.Parse($"{{ {evBody}, \"Budget\": {{ \"gpu:g1\": 0 }} }}");
    Report("d2.capitalized-Budget", false, $"'Budget' 静默忽略（区分大小写）⇒ 无上限预算；Caps.Count={s.Budget.Caps.Count}");
}
catch (Exception ex) { Report("d2.capitalized-Budget", true, $"已拒绝: {ex.GetType().Name}"); }
// d3 budget:[] 类型错
try
{
    var s = EffectScriptContract.Parse($"{{ {evBody}, \"budget\": [] }}");
    Report("d3.budget-wrong-type", false, $"'budget':[] 静默跳过 ⇒ Caps.Count={s.Budget.Caps.Count}（gate(2) 整体失效）");
}
catch (Exception ex) { Report("d3.budget-wrong-type", true, $"已拒绝: {ex.GetType().Name}"); }

// ════════ EXP-e [⊤,⊤] lifetime 事件 → Audit 静默无违例（R1-MED-1 复核）════════
try
{
    var tt = new EffectEvent(new Interval(NatStar.Top, NatStar.Top), new ScopeId.Scene("S"),
        Occupy("gpu:leak", Mode.Create, 1, 1), LoopCount.Of(1));
    var script = new EffectScript(ImmutableArray.Create(tt));
    var aud = script.Audit();
    Report("e.top-top-lifetime-silent", !aud.Passed, $"Audit Passed={aud.Passed}, violations={aud.Violations.Length}" +
        (aud.Passed ? " ⇒ [⊤,⊤] 事件静默消失（泄漏不报）" : $" ⇒ 有违例: {aud.Violations[0].Kind}"));
    // At 视角对照
    var at0 = script.At(NatStar.Of(0));
    Console.WriteLine($"  e.At(0) occupy claims={at0.OccupyClaims.Count}（At 也看不见该事件）");
}
catch (Exception ex) { Report("e.top-top-lifetime-silent", false, $"异常: {ex.GetType().Name}: {Trunc(ex.Message)}"); }

// ════════ EXP-f 相同 footprint 重叠窗口：At(t) vs Audit() 并发计数分歧（R5-C2 复测）════════
try
{
    var fp = Occupy("gpu:dup", Mode.Create, 1, 1);
    var script = new EffectScript(ImmutableArray.Create(
        new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(9)), new ScopeId.Scene("S"), fp, LoopCount.Of(1)),
        new EffectEvent(new Interval(NatStar.Of(0), NatStar.Of(9)), new ScopeId.Scene("S"), fp, LoopCount.Of(1))));
    long atCount = script.At(NatStar.Of(5)).OccupyClaims.Count;
    var aud = script.Audit();
    var conflicts = aud.Violations.Where(v => v.Kind == "CompatibleConflict").Count();
    // 峰值口径：cap=1 时是否报 PeakExceeded
    var capped = new EffectScript(script.Events, new Budget(new Dictionary<ResourceId, NatStar> { [ResourceId.Normalize(new ResourceId.Gpu(new Rid("gpu:dup")))] = NatStar.Of(1) }));
    var cappedAud = capped.Audit();
    var peaks = cappedAud.Violations.Where(v => v.Kind == "PeakExceeded").Count();
    Report("f.at-vs-audit-divergence", atCount == 1 && conflicts >= 1 && peaks >= 1,
        $"At(5).OccupyClaims={atCount}（set 口径去重）；Audit: CompatibleConflict×{conflicts}, PeakExceeded(cap=1)×{peaks} ⇒ 同一事实两套计数");
}
catch (Exception ex) { Report("f.at-vs-audit-divergence", false, $"异常: {ex.GetType().Name}: {Trunc(ex.Message)}"); }

// ════════ EXP-g 新增：ParseTop 负数端点异常类型实测（R6 数值解析裸 GetUInt64 复核）════════
try
{
    EffectScriptContract.Parse("""{ "events": [ { "lifetime": [-5, 3], "scope": {"scene":"S"}, "footprint": [] } ] }""");
    Report("g.negative-endpoint", false, "负数 lifetime 端点被接受？!");
}
catch (FormatException fx) { Report("g.negative-endpoint", false, $"抛的是契约层 FormatException（可接受）: {Trunc(fx.Message, 80)}"); }
catch (Exception ex) { Report("g.negative-endpoint", false, $"抛 .NET 原生 {ex.GetType().Name}，无字段名上下文: {Trunc(ex.Message, 100)}"); }

// ════════ EXP-h 新增：budget 值负数 / 超 ulong 实测 ════════
foreach (var (nm, bud) in new[] { ("h.negative-budget", """{ "events": [], "budget": { "gpu:x": -1 } }"""), ("h.overflow-budget", """{ "events": [], "budget": { "gpu:x": 99999999999999999999 } }""") })
{
    try
    {
        var s = EffectScriptContract.Parse(bud);
        Report(nm, false, "被接受？!");
    }
    catch (FormatException fx) { Report(nm, true, $"契约层 FormatException ✓"); }
    catch (Exception ex) { Report(nm, false, $".NET 原生 {ex.GetType().Name}（无字段名）: {Trunc(ex.Message, 110)}"); }
}

// ════════ EXP-i 新增：SerializeEvent 输出 loop 键 + 缺省 scope.type 形态的对称性 ════════
try
{
    var s = EffectScriptContract.Parse($$"""{ {{evBody}} }""");
    var js = EffectScriptContract.ToJson(s);
    bool hasLoop = js.Contains("\"loop\"");
    bool hasType = js.Contains("\"type\": \"scene\"");
    Report("i.serialize-shape-drift", hasLoop && hasType, $"输入缺 loop/type 键，输出补全为显式 loop+type=scene ⇒ 输入输出形态漂移（幂等性靠规范化兜住）: hasLoop={hasLoop}, hasType={hasType}");
}
catch (Exception ex) { Report("i.serialize-shape-drift", false, $"异常: {ex.GetType().Name}: {Trunc(ex.Message)}"); }

Console.WriteLine($"\n==== rt-probe 完成: {pass} PASS / {fail} FAIL ====");
return fail == 0 ? 0 : 1;
