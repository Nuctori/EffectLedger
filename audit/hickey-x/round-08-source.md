

===== THINK 0 =====

Let me start by exploring the project structure to understand what I'm auditing. I have a 25-minute budget, so I need to be efficient.

Plan:
1. Explore the repo structure (src, docs).
2. Read key files: public API surface (types, methods, properties), README, EFFECT_SCRIPT.md, PDR docs.
3. Check previous audit rounds for context (audit/hickey-x/round-01..07?).
4. Do the naming census, concept completeness gaps, terminology alignment, CN/EN mixing.
5. Write report to D:/Godot/Cosmos/audit/hickey-x/round-08.md.
6. End with acceptance report JSON.

Let me start.

===== THINK 3 =====

Interesting — Objects.cs now has `Signature.Equals` structural equality (R4 fix), and `Join` uses `Interval.Merge` (merge_I). Wait, previous rounds said "Join 文档承诺 merge_I 实为 set-union" but the current code at Join uses `cur.Merge(size)` — that looks like merge_I now. So that historical finding may be FIXED. I need to verify: `Join` in Objects.cs line ~226: `merged[key] = merged.TryGetValue(key, out var cur) ? cur.Merge(size) : size;` — yes it's Merge now. So "Join 文档承诺 merge_I 实为 set-union" → 已修. Need to note with line numbers.

Also "CompatibleWith 名实不符" — Claim.CompatibleWith still only compares Mode (line ~168: `public bool CompatibleWith(Claim other) => Compatible.IsCompatible(Mode, other.Mode);`). 仍在.

"Union/Join/Sequence/Parallel 四名一实" — need to check EffectScript / contract for those names. Let me read EffectScript.cs, EffectScriptContract.cs, SignedNet.cs, DerivedMetrics.cs, Deviation.cs, EffectAttributes.cs.

===== THINK 6 =====

Now I have EFFECT_SCRIPT.md. Key terminology observations:

1. EFFECT_SCRIPT.md §2.2 says `EffectScript` is a `readonly record struct` — but code (EffectScript.cs:44) is `sealed partial class`. Doc-code mismatch.
2. §2.1 doc shows EffectEvent with 3 fields (Lifetime, Footprint, Loop) — code has 4 fields (adds Scope). The doc mentions OPEN-1 fix in audit trail but the §2.1 snippet itself still shows 3 fields. Actually wait — the doc says "修正 auditA OPEN-1：Event 自身携带 Scope" in code comment. In the doc §2.1 snippet there's no Scope field. But §1.1 table and elsewhere... Let me check: doc §2.1 shows `public Interval Lifetime`, `public Signature Footprint`, `public LoopCount Loop` — 3 fields. Code has 4 (with Scope). So the design doc's interface listing is stale vs code.
3. Doc §4 JSON example: `"resource": {"gpu": {"bufferId":"mesh1"}}` — but ParseResource expects `{"gpu": "mesh1"}` (string, not object)! Let me verify: `ReqStr(gpu, "gpu")` requires ValueKind String. So the doc's example JSON would FAIL to parse. That's a big terminology/contract mismatch: documentation promises a shape the parser rejects. Let me double check ParseResource:

```csharp
if (el.TryGetProperty("gpu", out var gpu)) return new ResourceId.Gpu(new Rid(ReqStr(gpu, "gpu")));
```
ReqStr requires string non-empty. Doc example: `{"gpu": {"bufferId":"mesh1"}}` → gpu is an object → ReqStr throws FormatException. So yes — the canonical example in EFFECT_SCRIPT.md §4 cannot be parsed by EffectScriptContract.Parse. HUGE finding (doc lies about its own contract). Also `"scope": {"scene":"Battle"}` matches SerializeScope Scene form. OK.

Also budget key format `"commandBuffer:gpu"` matches ResourceKey. Good.

4. Terminology: doc calls `Budget` a "record struct" with only Caps — matches. Doc says `Audit(Budget cap)` — code also has parameterless Audit(). Fine.

5. Doc §3.2 shows AuditResult with Violations described as "首个违例的 (时间 t...)" but property is full list; minor.

6. Doc §8 implementation list: "EffectScript record struct + At(NatStar) + Audit(Budget)" — code is class. Same as above.

7. Doc §10.3: "dotnet test 279 passed" vs README "453 passed：297 单元 + 69 E2E + 87 Runtime". Numbers drift between docs (README newer). Minor consistency note.

Now PDR terms. Let me scan PDR quickly for key names: merge_I, ⊆*, DO-7/DO-9, KIND_MIX, PARA_CONFLICT, release-class, shell_scope/global_scope, occupy, etc. And check LANDING_PLAN for L1/L2/L3 naming.

Also Generator file — read it. Runtime files are out of scope? Task says project D:/Godot/Cosmos（Cosmos.EffectAlgebra）— focus on Cosmos.EffectAlgebra src, but public member census should cover src/Cosmos.EffectAlgebra primarily plus Analyzer diagnostics naming. The Runtime is a separate project ("运行时壳"). The task scope says "项目：D:/Godot/Cosmos（Cosmos.EffectAlgebra）" — ambiguous but previous rounds focused on L1+contract+analyzer. I'll do a quick scan of Generator and Runtime for naming issues too (cheap), but keep the census centered on Cosmos.EffectAlgebra.

Let me now grep for all public members in src/Cosmos.EffectAlgebra to build the census systematically.

===== THINK 7 =====

I now have a good picture of the public API. Let me check a few more things:

1. The `Violation.Kind` property — string type for kind, collides with `Kind` enum name (read/write/occupy). Two concepts named "Kind": `Kind` enum (read/write/occupy) and `Violation.Kind` (Leak | NegativeDip | PeakExceeded | CompatibleConflict). Same name different concept — that's a real finding!

2. `EffectEvent.Loop` — property of type `LoopCount` named `Loop`, while `Combination.Loop` is a static method. Name collision: Loop as noun (property) vs Loop as verb/method. Also ScopeId.Loop nested record.

3. `Peak` class vs `Derived.Peak` method — same name two layers.

4. `Net` — Signature.Net method returns NetTable; Derived.Net also. OK aliasing.

5. `Weight.Of` throws KIND_MIX but comment says "ℝ ∪ {⊥}" with ⊥ while everything else uses ⊤. Inconsistent symbol: Weight uses ⊥ (bottom = undefined), NatStar/ZStar/DeviationVal use ⊤ (top = unknown). Actually mathematically these are opposite conventions! For NatStar, ⊤ means "unknown upper bound" (treated as max). For Weight, ⊥ means "undefined" (throws). Interesting: in one place undefined-ness is modeled by bottom and throws; elsewhere unknown is top and flows. That's a conceptual inconsistency worth noting — though arguably justified. But naming-wise: `Weight.Of` returning double, throwing on cross-kind... fine.

6. `NatStar.Min(NatStar.Top)` returns the other operand — min(x,⊤)=x correct.

7. `CompareToFinite` — the name says "compare to finite" but it compares two NatStars where either can be Top. Misleading name? It treats ⊤ as largest. Actually name suggests comparing against finite values only, but signature takes NatStar which may be Top. Hmm — the doc says "先判 IsTop 再比较数值" — it's really a total order comparison. A better name would be CompareTo or CompareTotal. "CompareToFinite" lies slightly: if o is Top, you're not comparing to a finite value. Medium/low finding.

8. `Budget` — record struct with `Caps` nullable (`IReadOnlyDictionary?` implied). Actually declared as `public IReadOnlyDictionary<ResourceId, NatStar> Caps { get; }` non-nullable, but default(Budget).Caps == null (R10-F1 comment). So the type claims non-null but null happens via default struct. Name/type lie. Already partially known from R7? R7 mentioned budget issues but not this specific naming aspect. This was flagged in EffectScript.cs Audit: "R10-F1：default(Budget).Caps == null（struct 默认值绕过构造函数归一）⇒ 归一为无上限，不 NRE。" The constructor of EffectScript does `budget.Caps != null ? budget : Budget.None`. So public property Caps typed non-nullable CAN be null — the name doesn't lie but the TYPE lies. Worth listing under "名字撒谎/类型撒谎".

9. `EffectScriptContract.Parse` vs `ToJson` — Parse exists, ToJson exists. Symmetric pair present. Good. But