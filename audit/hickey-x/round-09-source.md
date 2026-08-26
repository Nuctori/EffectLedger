

===== THINK 0 =====

Let me start by understanding the task. I'm doing round 9 of 10 serial audits on D:/Godot/Cosmos (Cosmos.EffectAlgebra). This is the final adversarial re-review + false-positive cleanup round.

Tasks:
1. Re-verify all HIGH findings (~12 items listed) against current disk state
2. False positive sweep across 8 rounds
3. Fix priority ranking P0/P1/P2
4. Run dotnet test to verify green baseline

Output: audit/hickey-x/round-09.md

25-minute hard budget. Let me be efficient. First, let me read the previous rounds and look at the project structure in parallel.

===== THINK 2 =====

Now I have rounds 1-8 (round-08 read first). Let me check the current state of the source files. The task says the working tree may have evolved. Key files to verify:

1. `src/Cosmos.EffectAlgebra/EffectScriptContract.cs` — N1 SerializeBudget, E1/E3 fail-soft, HIGH-2 budget key dialect, §4 example, root unknown keys
2. `src/Cosmos.EffectAlgebra/EffectScript.cs` — HIGH-3 zero-budget gate(2), V1 Budget aliasing, V2 Violation key normalization
3. `src/Cosmos.EffectAlgebra.Runtime/DependencyGraph.cs` — N2 graph invariants
4. `src/Cosmos.EffectAlgebra/Objects.cs` — default backdoor, Claim record
5. InverseReplay.cs:34 bare catch — E2

Let me read the current sources now. Start with the main contract file and EffectScript.cs.

===== THINK 3 =====

Important observations from the current EffectScriptContract.cs:

1. **ParseInterval now rejects lo="⊤"** — "if (lo.IsTop) throw new FormatException(...)" — so R1-MED-1 / [⊤,⊤] silent disappearance is FIXED! The comment says "[⊤,⊤] 寿命视为非法输入——fail-fast 拒绝". This means R7-e experiment result ([⊤,⊤] silently disappears) is now FIXED on current disk.

2. **SerializeBudget still does `kv.Value.Value`** — line: `d[ResourceKey(kv.Key)] = kv.Value.Value;` — no IsTop handling! So N1 SerializeBudget ⊤→0 is STILL PRESENT. Let me verify NatStar.Top.Value == 0 in Numeric.cs.

3. **ParseScope changes**: `{"type":"scene"}` without scene → name="" → Scene("")? Let's check: hasScene=false → name = "" → if no type → Scene(""). If type=scene and no scene key → also Scene("") via `hasScene ? ... : ""`. Hmm wait: `var name = hasScene ? sc.GetString() ?? throw ... : "";` — so missing scene ⇒ empty string, still E4 partially present. `{"scene":42}` → sc.GetString() on a Number... GetString() throws InvalidOperationException for non-string kinds actually! In System.Text.Json, JsonElement.GetString() throws InvalidOperationException if ValueKind != String. Wait no — GetString() returns the string for String kind, and throws InvalidOperationException for other kinds? Let me recall: JsonElement.GetString() — "Throws InvalidOperationException if the ValueKind is not String." Actually I believe it throws for non-string values (except it may return null for Null kind?). For Number kind it throws InvalidOperationException. Hmm, R6-E4 claimed `{"scene":42}` → GetString() 对 Number 返回原始文本，静默得名字 "42". That was possibly wrong! In .NET, JsonElement.GetString() throws InvalidOperationException when ValueKind is not String. Actually let me check: docs say "Exception: InvalidOperationException - This value's ValueKind is not String." Yes, GetString() throws for non-String kinds. So R6-E4's third case (数字静默强转) was likely a FALSE POSITIVE — it would throw InvalidOperationException, not silently convert. But note null → GetString() returns null for Null kind → then `?? throw` catches it → FormatException ✓. Missing scene → "" → Scene("") silent ✓ (still true). So E4: 2 of 3 subclaims hold (null throws ✓ good behavior actually, missing→empty silent bad, number→throws InvalidOperationException which is fail but wrong exception type). Actually wait — R6 said "null 抛、缺失静默空串、数字静默强转" as THREE inconsistent behaviors where two were silent acceptance. Reality: null throws (fine), missing gives Scene("") silently (bad), number throws InvalidOperationException (not FormatException — inconsistent type but not silent). So E4 was PARTLY exaggerated — the number case is not a silent coercion. Need to verify GetString() behavior though. Let me test with dotnet or just check docs. I can write a quick probe... but discipline says read-only for project files; creating a temp probe outside the repo is fine. Actually round-07 created rt-probe and deleted it after. I could do a quick probe to verify N1 and a few contested claims empirically. That would strengthen the final review. Budget is 25 min though. A quick probe is worth it for N1 (the top finding), and maybe [⊤,⊤] fix verification, and root unknown keys.

4. **Root-level unknown keys**: Parse still only reads events/budget — d1/d2 still present. And budget value `prop.Value.GetUInt64()` raw call still there (line in ParseBudget).

5. **budget memory:"" → Memory(0)**: still present in ParseResourceKey: `.Length > 0 ? ulong.Parse(...) : 0`. Still LOW-2/R1-C2 residual.

6. **SerializeScope still only 4 subclasses** — `_ => throw` — c×4 still present. SerializeResource still 5/15.

Now check EffectScript.cs for HIGH-3 gate(2), V2 violation key normalization, At/Audit dual standard, Join fix status, Sequence/Parallel, default(LoopCount) chain, maxFinite+1, etc.

===== THINK 4 =====

Key observations for EffectScript.cs (current disk):

1. **N3 default backdoor**: Line ~64-67: `Budget = budget.Caps != null ? budget : Budget.None;` — R10-F1 comment: default(Budget).Caps == null normalized to Budget.None. So the Budget part of the default backdoor is FIXED (at the EffectScript constructor level). But what about `default(Claim)`? Need to check Objects.cs Normalize. And `default(LoopCount)` → Count is NatStar, default(NatStar).Value==0, IsTop false? Need Numeric.cs. The sweep divides by w.Value: `hi.Value <= ulong.MaxValue / w.Value` — if w=0 (default LoopCount), division by zero! Let me check: in Step enter path: `var mul = (!hi.IsTop && !w.IsTop && hi.Value <= ulong.MaxValue / w.Value) ? ...` — if w.IsTop false and w.Value==0 → `ulong.MaxValue / 0` DivideByZeroException. So default(LoopCount) crash chain may still exist. Need to check LoopCount definition and whether Of(0) throws but default(LoopCount).Count==0.

2. **V1 Budget alias**: `public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; }` — STILL holds caller's dictionary reference. V1 CONFIRMED still present. Also `Budget.None = new(new Dictionary<...>())` mutable backing.

Wait, also note: gate(2) now normalizes: `var nk = ResourceId.Normalize(kv.Key);` then uses nk for lookup... and Violation still reports `kv.Key`:
```csharp
violations.Add(new Violation(t, kv.Key, scope, "PeakExceeded", $"峰值 {p} > 预算 {kv.Value}"));
```
Yes — line in AuditAtSample: `new Violation(t, kv.Key, scope, "PeakExceeded", ...)`. So V2 (unnormalized key in Violation) STILL PRESENT. Lookup uses nk, report uses kv.Key.

3. **HIGH-3 zero budget = vacuous audit**: gate(2) iterates cap.Caps; comment at line "§2.3 峰值预算…资源不在 Caps ⇒ 不检查" still there. No CapsChecked counter in AuditResult. AuditResult has only Passed + Violations. HIGH-3 STILL PRESENT (though arguably partially mitigated by [⊤,⊤] rejection and E-series fixes? No—HIGH-3 itself remains).

4. **N6 maxFinite+1 wraparound**: `if (anyOpenEnd) samplePoints.Add(NatStar.Of(maxFinite + 1));` — bare ulong add, still present. If maxFinite == ulong.MaxValue wraps to 0. Still present. That was LOW-MED per R3.

5. **E5 CompatibleConflict no event index**: grp HashSet<int> exists, detail is static text. Still present.

Now check other files: Objects.cs (Join fix confirmed R8; CompatibleWith; Kind enum), Numeric.cs (CompareToFinite), Algebra.cs (Net/Peak scope filtering), DerivedMetrics.cs (Sequence/Parallel), DependencyGraph.cs (N2), InverseReplay.cs (E2 bare catch), PluginRuntime.cs, ApiMapping.cs (V4), SignedNet.cs (V3), EFFECT_SCRIPT.md (§4 example, §2.2 struct/class claim), README.md.

Let me read these files now.

===== THINK 5 =====

Now let me check the remaining HIGH items:

1. **N2 Runtime graph invariants** — DependencyGraph.cs, Fiber.cs
2. **V1 Budget alias** — EffectScript.cs:344-348 area. Current: `public Budget(IReadOnlyDictionary<ResourceId, NatStar> caps) { Caps = caps; }` — still holds caller's dictionary reference. CONFIRMED still present (EffectScript.cs around line 345). Let me get exact line numbers.
3. **V2 Violation key** — gate(2): `violations.Add(new Violation(t, kv.Key, scope, "PeakExceeded", ...))` — still uses kv.Key unnormalized. CONFIRMED still present.
4. **E1 silent downgrade** — Parse: no root-level whitelist (root.TryGetProperty("events"), "budget" only), optional fields default silently, TryGetProperty case-sensitive. Still present. But wait — [⊤,⊤] is now rejected at parse level (ParseInterval throws if lo.IsTop). So the "lifetime.lo=⊤ silently vanishing" issue is FIXED for JSON path. But C# path can still construct Interval(Top, Top)? Yes — [⊤,⊤] is legal per Interval constructor ("[⊤,⊤] represents an unknown interval, legal"). And sweep skips Lo.IsTop events silently (EffectScript.cs sweep loop `if (lt.Lo.IsTop) continue;`). And closure also skips `if (e.Lifetime.Lo.IsTop) continue;`. So via C# API, a [⊤,⊤]-lifetime event still silently vanishes from all gates. Partially fixed: JSON contract rejects it, but C# construction path remains.

Wait — actually now ParseInterval rejects lo=⊤ entirely (any lo=⊤, not just [⊤,⊤]). That fixes MED-1/R1-MED-1 at the JSON boundary. Good.

5. **E2 bare catch** — InverseReplay.cs:34. Need to check.
6. **C1 Join** — R8 says fixed (Objects.cs:193-209 real merge_I). Confirmed by reading Objects.cs above — Join now does merge via Dictionary keyed by (Kind, Resource, Mode, Scope) with cur.Merge(size). FIXED. Task says "C1 Join [fixed, do not report]" — so confirm as fixed, do not report.
7. **C2 At/Audit double standard** — At uses Union (set dedup), Audit sweep counts multiplicity. Still present? At → Signature.Union → ImmutableHashSet dedup. Sweep grp counts event indices. Yes, still present.
8. **§4 example unparseable** — EFFECT_SCRIPT.md §4 example with nested gpu object vs ReqStr string. Need to check current EFFECT_SCRIPT.md.
9. **Kind double meaning** — Violation.Kind string vs Kind enum. Still present (Violation.Kind in EffectScript.cs:365+ and Kind enum Objects.cs:111).

Also:
- HIGH-1 onboarding surface: samples dir — check if samples/EffectScript exists now.
- HIGH-3 zero-budget always-true: gate(2) iterates cap.Caps only; no CapsChecked count in AuditResult. AuditResult still has only Passed + Violations. CONFIRMED still present.
- N1 SerializeBudget ⊤→0: SerializeBudget still `d[ResourceKey(kv.Key)] = kv.Value.Value;` — NatStar.Top.Value == 0. STILL PRESENT. Wait — but does anything prevent ⊤ caps? Budget constructor doesn't validate. ParseBudget only produces finite values (GetUInt64). But C# users can construct Budget with Top values, and ToJson serializes them to 0. Round-trip flips semantics. STILL CONFIRMED. Let me check line number: in current EffectScriptContract.cs, SerializeBudget is:

```csharp
    static object SerializeBudget(IReadOnlyDictionary<ResourceId, NatStar> caps)
    {
        var d = new Dictionary<string, object?>();
        foreach (var kv in caps)
            d[ResourceKey(kv.Key)] = kv.Value.Value;
        return d;
    }
```

Need exact line numbers. Let me grep.

Also ParseBudget still `NatStar.Of(prop.Value.GetUInt64())` — no ⊤ expression on parse side. Confirmed.

- N3 default backdoor: Claim readonly record struct with reference-type fields, default(Claim) has null Resource/Scope/Size. Normalize would NRE on null Resource (`ResourceId.Normalize(Resource)` switch on null → actually switch expression on null reference: `r switch { Self s when ... }` — null doesn't match any pattern including `_`? No wait, `_ => r` matches null too. Actually a switch expression with `_` arm matches null. Hmm, but `Self s when s.Component.StartsWith(...)` — for null r, type patterns don't match, `_ => r` returns null. Then Size ?? Interval.Default works. So default(Claim).Normalize() gives Claim with Resource=null. Then Signature.Add puts it in bucket. Later ResourceId.Normalize(c.Resource) in sweep — same, returns null. Dictionary key null → ArgumentNullException? Actually Dictionary<ResourceId,...> with null key throws ArgumentNullException on Add/TryGetValue. So default(Claim) leads to NRE/ANE downstream. Also default(LoopCount).Count == default(NatStar) — IsTop=false, Value=0! LoopCount record struct wrapping NatStar Count. Let me check DerivedMetrics LoopCount definition. If default(LoopCount).Value==0, then sweep `ulong.MaxValue / w.Value` divide-by-zero... wait current code: `hi.Value <= ulong.MaxValue / w.Value` — if w.Value==0 → DivideByZeroException. Need to check LoopCount.Of guard and whether default(LoopCount) bypasses it.

Let me read DerivedMetrics.cs and check.

- V1 Budget alias: confirmed above — constructor stores reference directly. Line ~346.

Let me now read the Runtime files (DependencyGraph.cs, Fiber.cs, InverseReplay.cs, PluginRuntime.cs relevant parts) and DerivedMetrics.cs, then EFFECT_SCRIPT.md §4 example, README, samples listing. Then run tests.

Time budget: 25 minutes. Let me be efficient — batch reads.

===== THINK 6 =====

Key updates found:
- DerivedMetrics: `Parallel` now throws PARA_CONFLICT on cross-branch conflicts — so Sequence/Parallel no longer both = Union! Parallel now has real distinct semantics. R8's claim "Sequence/Parallel=Union (DerivedMetrics.cs:50/:53 unchanged)" is now FIXED (partially — Sequence still = Union, but Parallel has conflict guard).
- InverseReplay.cs:34 still bare `catch {` — E2 confirmed still present.
- DependencyGraph.cs:24 Register silent overwrite + AddHardEdge no validation — N2 confirmed still present.
- Numeric.cs CompareToFinite — name issue still present.
- Objects.cs CompatibleWith(Claim) still mode-only (:168 region) — confirmed.
- SerializeBudget still `kv.Value.Value` → ⊤→0 — N1 STILL PRESENT! Line ~ in current file: `d[ResourceKey(kv.Key)] = kv.Value.Value;` Yes, still there.

Wait — need to check whether budget parse accepts "⊤". ParseBudget: `dict[r] = NatStar.Of(prop.Value.GetUInt64());` — no ⊤ acceptance. So N1 fully confirmed still present.

Also note: EffectScript.Audit gate(2) V2: `violations.Add(new Violation(t, kv.Key, scope, ...))` uses kv.Key unnormalized — confirmed still present at the PeakExceeded violation construction. Let me check exact line numbers later via grep -n.

Now read remaining files: Fiber.cs, PluginRuntime.cs (V5, C5), Algebra.cs (C1 scope split), SignedNet.cs (V3), ApiMapping.cs (V4), EFFECT_SCRIPT.md (§4 example, §2.2 struct/class), README.

===== THINK 7 =====

PluginRuntime.cs has evolved significantly: R7-L1 duplicate FiberId now throws (Register), AddDependency validates same-Scope. But DependencyGraph.Register itself still silently overwrites (`_fibers[f.Id] = f`) — though PluginRuntime guards before calling it. AddHardEdge still doesn't verify Requires⊇Provides or registration.

E2: InverseReplay.cs bare catch still present at :34-40 region — the `catch {` without variable, and PluginRuntime fabricates `new InvalidOperationException($"部分逆释放失败（位置 {diag.FailedIndex}...")` — original exception still discarded. E2 CONFIRMED still present.

V5: PluginRuntime.Graph public property still there (:15 area — `public DependencyGraph Graph => _graph;`), IsShuttingDown `{ get; set; }` public, OnSuspending `{ get; set; }` public. V5 still present.

N2: DependencyGraph.Register `_fibers[f.Id]=f` silent overwrite — still at line ~24. AddHardEdge no invariant check — still. Fiber internal set — still. So N2 confirmed (with refinement that PluginRuntime.Register now throws on dup id, so the graph-level hole is reachable via PluginRuntime.Graph public property → AddHardEdge directly, bypassing AddDependency's scope validation).

Now check EFFECT_SCRIPT.md §4 example, README, samples dir, ApiMapping, SignedNet.