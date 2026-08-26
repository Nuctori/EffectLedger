

===== THINK 2 =====

Now let me look at the previous rounds to understand what's been confirmed already. Let me read round-01 through round-04 quickly, and importantly the confirmed findings list from the task:

前轮已确认（勿重复）：
- FormatException 带字段名但缺 JSON 路径/行号
- budget 键错误只回显键名
- budget 值裸 GetUInt64 抛原生异常
- 根级未知键静默忽略
- [⊤,⊤] lifetime 静默消失
- SerializeBudget ⊤→0
- EAA0901 已带修复动作提示

My focus:
1. Full error message inventory: grep all throw/diagnostics, rate each — does it contain (a) field/location, (b) expected shape, (c) actual value, (d) fix action. Produce an "error message quality matrix" (好/中/差).
2. L3 analyzer diagnostic quality: all EAA diagnostics — messages, actionability, severity defaults reasonable? Any diagnostics reported but users don't know how to fix?
3. Runtime exception paths: LoadValidationException / InvalidOperationException / CrashReport diagnosability — can ops locate which Fiber/which edge?
4. Recoverability flow: how many steps from exception to fix for a user with a wrong script? Error code → doc mapping? Does EFFECT_SCRIPT.md cover every FormatException scenario?
5. Silent path final review: all code points that don't throw but swallow/skip/degrade (continue/default branches) — systematic inventory.

Budget 25 min, prioritize points 1/5.

Let me read the source files now. Priority: EffectScript.cs (parse), EffectScriptContract.cs (serialization), EffectAlgebraAnalyzer.cs, PluginRuntime.cs, LoadValidation.cs, and grep throws everywhere.

Let me read files in parallel.

===== THINK 4 =====

Now let me read the remaining runtime files: Fiber.cs, InverseReplay.cs, DependencyGraph.cs, ProviderCrashCascade.cs, GodotShell.cs, and the L1 files: Algebra.cs, Numeric.cs, Objects.cs, SignedNet.cs, ApiMapping.cs, DerivedMetrics.cs, Deviation.cs, EffectAttributes.cs. Also EFFECT_SCRIPT.md to check error code → doc mapping. Let me batch reads.

===== THINK 5 =====

Now let me look at the remaining files: Algebra.cs (KIND_MIX throw), DerivedMetrics.cs, EffectAttributes.cs, Numeric.cs, Objects.cs, SignedNet.cs — I have the grep results for throws. Let me also check the silent paths: continue/default branches. Let me grep for `continue` and `default:` and catch blocks that swallow.

Also need to check EFFECT_SCRIPT.md for error-code documentation coverage, and check whether there's an "error code → docs" mapping. Let me check the docs folder and EFFECT_SCRIPT.md structure.

Let me also verify line numbers for all the FormatException messages in EffectScriptContract.cs for the matrix. Let me get numbered lines.

===== THINK 6 =====

Key findings so far for silent paths:
1. **GodotShell.cs:33** — `catch { safe = false; }` — swallows exception in IsSafeToInvoke, treats as unsafe silently. No log.
2. **GodotShell.cs:59** — `try { d(); } catch { }` — swallows ALL exceptions from exit drains with NO CrashReport. This is a silent path: if SynchronousExitDrain throws (it's registered via EnqueueExitDrain), the exception is swallowed entirely — no CrashReports entry! Wait, SynchronousExitDrain internally catches per-task exceptions and routes to CrashReports... but exceptions thrown by SynchronousExitDrain *itself* (e.g., from TopoSortLeafFirst, or from the `if (cyclic.Contains...)` — no that doesn't throw) would be swallowed by GodotShell.cs:59. Actually wait: SynchronousExitDrain catches task exceptions and adds CrashReport. But DetectCycles/TopoSortLeafFirst could throw? Unlikely but possible. More importantly: GodotShell.cs:59 comment says "entire task try/catch" — this is the shell flushing exit drains during _ExitTree; any exception is dropped on the floor with zero diagnostics.
3. **PluginRuntime.cs:118 & 229** — OnSuspending hook exception swallowed silently (`catch { /* hook exception isolation */ }`). The hook is the integration seam with Godot shell ProcessMode disabling — if it fails, dependents stay dispatchable but nobody knows. At minimum no diagnostic recorded.
4. **InverseReplay.cs:34** — `catch` without exception variable! The original exception object is DISCARDED. PartialReleaseDiagnosis records FailedIndex and Pending resources but NOT the exception itself. Then PluginRuntime.cs:145 fabricates a NEW InvalidOperationException("partial inverse release failed (position ...)") — the actual root-cause exception message/stack is lost forever. This is a HIGH finding for error-message quality: ops sees "position 2, 1 unreleased item: gpu:x" but has no idea WHY the release action failed (the inner exception is gone). Compare: catch (Exception ex) at :147 preserves ex. So partial-failure path loses root cause while total-failure path preserves it.

Let me verify PluginRuntime line numbers precisely. From my read of the file:
- :55 IsShuttingDown throw
- :58 cascading teardown throw
- :80 LoadValidationException hard cycle
- :139 dynamic hard cycle CrashReport
- :145 partial inverse release failure CrashReport
- :159 RecomputeTopology throw
- :181 exit path partial failure
- :218-224 TickWatchdog (per round-05 C5)
- :118/:229 OnSuspending swallow

InverseReplay.cs:34 bare catch — need exact line number. From read: lines 30-38ish. Let me count: file read shows:
```
29:            try
30:            {
31:                inv.Execute();
32:                completed.Add(inv.Resource);
33:            }
34:            catch
35:            {
```
Actually the read output shows line numbers implicitly. Let me grep -n for precision.

Also LoadValidation messages:
- ValidateScaleClosure: `$"Fiber {fiber.Id}: Scope({inv.Scope}) of inverse {inv.Resource} != coefficient Scope({fiber.Scope}) (§3 step2b scale not closed)"` — has fiber id, resource, both scopes, expected form. Good-ish. Missing: which inverse index / fix hint.
- ValidateReleaseClass empty tags: good message with two options ("must list specific release APIs or leave null for opt-out") — actually decent.
- Non-release tag: lists allowed values via ReleaseClass.All — includes expected set. Decent.
- ValidateDoubleRelease: names fiber, resource, provider id — good.
- VerifyNetClosure: lists violating resources but NOT which fiber! `VerifyNetClosure(IEnumerable<Fiber> fibers)` — CheckAll(fibers) → result.ViolatingResources. If multiple fibers loaded, message says "[gpu:x]" but not which Fiber owns the unclosed net. Need to check NetBenefitClosure.CheckAll to confirm whether fiber info exists. Let me read NetBenefitClosure.cs.

Also PluginRuntime.cs:139 dynamic hard cycle message includes full ring path — good.

Now check Numeric.cs GetUInt64 overflow: ParseTop uses el.GetUInt64() directly — negative number JSON → JsonException (native), not FormatException. Also "-1" as string? Not handled. And lifetime [0, 1e30] → GetUInt64 throws FormatException natively ("The JSON value ... is too large"?). Actually System.Text.Json GetUInt64 on value exceeding ulong throws FormatException with message like "The JSON value is either too large or too small for a UInt64." — native English message, no field name, no path. That's part of prior finding "budget values use bare GetUInt64 throwing native exceptions" — same applies to lifetime endpoints and loop. Prior rounds flagged budget only. I can extend: same class of bug at Contract:81 (ParseTop) and :111 (ParseLoop) — native JsonException without field context. That's a legit extension, not repetition.

Also ParseBudget:184 unknown key echoes key name (prior). prop.Value.GetUInt64 at :183 — bare.

Now check EffectScriptContract Parse: JsonDocument.Parse(json) at :23 — malformed JSON throws JsonException natively WITH line/position info actually! System.Text.Json JsonException does include line and byte position. But FormatExceptions thrown by hand have no position. Interesting contrast: the BCL errors carry location, the custom ones don't.

Silent paths inventory so far:
1. Root-level unknown keys silently ignored (:23-25 — TryGetProperty("events"), TryGetProperty("budget"); anything else ignored). Prior confirmed — still present.
2. Event-level unknown keys silently ignored (ParseEvent :53-60 reads known fields only). Same class — event-level typo like "lifetimes" or "footprinty" → Require throws missing field, ok that's caught. But "Loop" vs "loop"? JSON is case-sensitive here: TryGetProperty("loop") — user writes "Loop" → silently defaults ω=1! That's a NEW silent-path instance beyond root-level: optional-field case sensitivity. Similarly "size"/"Size". And "type": "Global" (capital G) → unknown scope.type throw — ok that throws.
   Actually more subtle: scope {"Type":"method"} → no "type" property → defaults to Scene(name)! Silent misinterpretation. Case-sensitivity turns a typo into a silently different parse. Worth an E-finding: contract parsing is case-sensitive, wrong-case optional keys degrade silently to default values rather than raising errors (loop→ω=1, type→Scene, size→Default).
3. InverseReplay.cs:34 bare catch — discards exception object (HIGH).
4. GodotShell.cs:59 catch {} — exit drain exceptions fully silent (no CrashReport).
5. GodotShell.cs:33 catch { safe=false } — silent degradation.
6. PluginRuntime.cs:118/:229 OnSuspending hook swallow — integration seam failure invisible.
7. EffectScript.Audit Lo=⊤ events skipped silently (:~127 sweep loop `if (lt.Lo.IsTop) continue;`) — event never audited, no violation reported. Known? Round-05 mentioned [⊤,⊤] lifetime silently disappearing (prior confirmed). Lo=⊤+finite Hi is same family. Skip as prior-covered family but note in matrix.
8. Fiber.Load() returns false on non-Inactive — caller PluginRuntime LoadAll ignores return value (`foreach (var f in all) f.Load();`) — double LoadAll would silently no-op second time? Actually LoadAll on already-loaded fibers: Load returns false silently, fibers stay Active, no error. Silent no-op path.
9. NotifyProviderTeardown no-op unless Active — silent by design (idempotent), documented.
10. MarkDead no-op unless TearingDown — ProviderCrashCascade.Handle calls provider.MarkDead() fail-open even when state isn't TearingDown (e.g., watchdog double-enqueue scenario from C5) — silently marks Dead? No: MarkDead checks state==TearingDown, else no-op silently. If fiber was already Dead (double replay), Handle marks nothing but reports crash anyway.
11. Budget default in EffectScript ctor: `budget.Caps != null ? budget : Budget.None` — null Caps silently converted to None. Minor.
12. Parse: `budget` present but not Object → silently ignored (:33-34: `if (... && bud.ValueKind == JsonValueKind.Object)`)! User writes "budget": [] → silently no caps → audit runs unlimited. That's a silent path variant at budget level, distinct from root-level unknown keys. Good E-finding.
13. Analyzer FindWhitelistEntry: unmatched invocations silently skipped (`if (m is null) continue;`) — documented approximation (false negatives acknowledged). It's a documented silence — mention in inventory as "documented silence".

Now analyzer severity review:
- All five diagnostics defaultSeverity Warning, enabled by default.
- EAA0901 message: excellent — includes method, API name, two fix options, authority pointer. Good tier.
- EAA0801: includes method, explains why compile-time, consequence ("method is not exempt"). Actionable? Fix = provide non-empty reason. OK.
- EAA0802: says range, but doesn't echo the actual ε value received! "out-of-range or non-constant is treated as illegal" — user with ε=0.7 gets told range but not what they wrote, and non-constant case gives no hint what "constant" means (const/localization). Medium.
- EAA0303: message says "suggest explicitly annotating intent with [EffectOverride]" — but wait: A3 KIND_MIX suggests [EffectOverride] as remedy, yet per §8.3.1 OverrideKind doesn't exist (kind overrides are type-forbidden per class comment: "L1 fundamentally does not provide an OverrideKind attribute ⇒ type enforcement forbids it"). So EAA0303 tells users to apply a remedy that the type system forbids?! Let me re-read: EAA0303 messageFormat: "...but recommend explicit [EffectOverride] annotation of intent." And the class comment says kind override is NOT provided by EffectOverrideAttribute — writing OverrideKind named arg is CS0117. So what would a user actually write? [EffectOverride("reason")]? That exempts A3/A4 reporting (hasValidOverride skips AnalyzeKindMixAndCompat). Hmm — [EffectOverride] with reason does suppress EAA0303/4 (AnalyzeMethod: `if (!hasValidOverride) AnalyzeKindMixAndCompat(...)`). So the remedy works via plain reason string, not OverrideKind. The message "recommend explicit [EffectOverride] annotation of intent" is actionable ([EffectOverride("reason")]). OK not a lie. But EAA0901 message mentions [EffectOverride("evidence")] with reason required — consistent.
  However EAA0303/EAA0304 don't say HOW to annotate ([EffectOverride("reason")]) nor that reason must be non-empty (else EAA0801 fires). Minor gap.
- EAA0304: message includes conflict pair {a}+{b} e.g. "Create+Create", resource label = first-seen GodotApi name (resourceLabel[key] = m.Value.GodotApi — labeled with the API name not resource!). Wait: `resourceLabel[key] = m.Value.GodotApi` where key is ResourceId. So message "same resource ({1})" prints the API name (e.g., "AddChild"), NOT the resource identity! User sees "conflicting mode pair (Create+Create)" attributed to "AddChild" — which API was first seen touching that resource. The actual normalized ResourceId is never shown. That's misleading: the message template says resource ({1}) but fills in an API name. Check: `resourceLabel[kv.Key]` where kv.Key is ResourceId; label assigned once = first whitelist entry's GodotApi. So EAA0303 message "{1}" = "resource" slot holds "Audio.Play"-style string. Mislabeling — MED finding: the diagnostic claims to report a resource problem but shows an API name; user cannot know WHICH resource collided when several calls share it.
- Location: all diagnostics point at method identifier, not the offending call site. For EAA0901 they do track firstAcquireName (API name in message) but location is method-level. Acceptable but call-site location would be better. LOW-MED.
- EAA0901 counts per-resource acquire/release across whole method including conditionals (flow-insensitive): documented.

Also: EAA0901 message says "no corresponding release-class invocation" — but logic is count-based (acquire>release); partial release (2 acquires, 1 release) also triggers with message saying "no corresponding release-class invocation" which is now inaccurate wording (there IS one, just fewer). Minor wording drift vs R8 improvement. LOW.

Now runtime diagnosability:
- LoadValidationException messages include Fiber Id — good. VerifyNetClosure does NOT include Fiber Ids (only resources). Check NetBenefitClosure.
- CrashReport record: Provider FiberId + Exception + NotifiedDependents — good shape. But fabricated InvalidOperationException at :145/:181 lacks inner exception (root cause lost due to bare catch in InverseReplay). Ops question "which edge/fiber" — CrashReport has Provider id ✓, dependents list ✓. But NOT which inverse index's resource caused hang... diag.Pending lists resources ✓, FailedIndex ✓ (but FailedIndex refers to stack position i — user can't map index→claim easily since Inverses is ImmutableStack; index counting order undocumented).
- DrainTeardownBatch cyclic skip: CrashReport created with new InvalidOperationException — fine.
- SynchronousExitDrain resets IsShuttingDown=false at end — if exception propagates mid-drain... it catches per-task, so fine.

Error-code → doc mapping: Violation.Kind strings ("Leak","NegativeDip","PeakExceeded","CompatibleConflict") vs EFFECT_SCRIPT.md sections? Need to check EFFECT_SCRIPT.md documents these kinds and each FormatException scenario. Let me grep EFFECT_SCRIPT.md.

Also check Algebra.cs:38 KIND_MIX message: "$KIND_MIX: cross-kind weight undefined {a}×{b} (needs L3 error)" — "needs L3 error"?? Message says "(needs L3 error)" — telling the user that L3 should have errored? Odd message: it's telling that this should be an L3-reported condition. As an error message it lacks: expected form, fix action. And it's InvalidOperationException with code prefix KIND_MIX — has code at least. Where is Weight.Of used? ApiMapping/DerivedMetrics probably. Check quickly.

Objects.cs:177 default: $"Unknown Kind: {n.Kind}" — echoes value, no expected set. Low.

Numeric.cs:84/86 ArgumentException: English + §ref, includes lo/hi values. Decent. SignedNet.cs:62 similar.

DerivedMetrics LoopCount.Of(0): ArgumentOutOfRangeException with reason — good.

EffectAttributes: ArgumentException with § ref and requirement — good; ArgumentOutOfRangeException includes actual epsilon value ✓.

Now let me check EFFECT_SCRIPT.md coverage of error scenarios, and NetBenefitClosure.

===== THINK 7 =====

Now let me check the silent paths systematically. Let me grep for `continue`, `default`, and other swallowing constructs in src. Also check GodotShell.cs and DependencyGraph.cs. And check the samples' CrashReport consumption to see if there's an error-code → doc mapping.

Also verify: JsonDocument.Parse at EffectScriptContract.cs:23 — raw JsonException propagates without any wrapping (no JSON path/line info added, though .NET's JsonException actually includes line/position). Actually System.Text.Json.JsonException DOES include line number and byte position in its message ("at Path: $ | LineNumber: 0 | BytePositionInLine: 4."). So malformed raw JSON gives decent diagnostics via JsonException; but semantic errors (FormatException) give no path/index. That nuance matters for fairness: raw syntax errors are diagnosable; contract-level errors are not located.

Key new findings so far:

E1 [HIGH]: FormatException has no event index / field path. All ~20 FormatException throws in EffectScriptContract.cs lack: which event index (i-th in events array), which claim index, which budget key line. The only one with a section prefix is :26 ("EFFECT_SCRIPT §4：根须含 'events' 数组"). Others are bare ("event 须为对象", "缺少字段: kind"). For a 2000-event AI-generated script, "缺少字段: scope" is undiagnosable — you know a claim somewhere lacks scope but not which of thousands. Fix: thread an index/context through ParseEvent/ParseClaim and prefix messages, or wrap in try/catch adding `$"events[{i}]"`.

E2 [HIGH]: Inconsistent exception types for the same failure class — budget value uses bare prop.Value.GetUInt64() (:184 area) which throws native InvalidOperationException/FormatException from Utf8JsonReader... wait actually what does GetUInt64 throw on non-number? JsonElement.GetUInt64() on a string throws InvalidOperationException ("The token type 'String' does not match the expected JsonTokenType..."). Actually it throws InvalidOperationException? Let me recall: JsonElement.GetUInt64() throws InvalidOperationException when TokenKind is not Number. Hmm — docs: GetInt32 etc throw InvalidOperationException if TokenType != Number. Yes InvalidOperationException. So user writes "budget": {"gpu:x": "64"} (string) → gets InvalidOperationException with message about token types but no mention of WHICH budget key. Whereas lifetime endpoint non-number gets FormatException "lifetime 端点须为数字或 \"⊤\"" — same class of error, different exception type + different quality. Also negative numbers: budget "-5"? GetUInt64 on -5 (number) throws FormatException/OverflowException ("The JSON value ... is too large or too small")? GetUInt64 with negative value throws FormatException. And loop: -1? GetUInt64 throws FormatException native, not the friendly one. So negative loop/lifetime endpoints produce raw framework exceptions with no field context. This was partially flagged before (budget 值裸 GetUInt64 抛原生异常) — confirmed still present, but I should extend: same applies to lifetime endpoints (ParseTop:81) and loop (ParseLoop:111) — negative numbers bypass the friendly path. Wait, previous round said "budget 值裸 GetUInt64 抛原生异常" — so budget value specifically. My new angle: ParseTop and ParseLoop have the SAME hole for negative numbers, i.e., the fix pattern was applied nowhere. Let me verify: NatStar.Of(el.GetUInt64()) at line 81 — el = -3 → GetUInt64() throws OverflowException? Let me think about actual behavior: JsonElement.GetUInt64(): if TokenType != Number → InvalidOperationException. If number is -3 → it's a valid number token, but value out of range for ulong → throws FormatException ("The JSON value is not a valid UInt64")? Actually the docs say GetUInt64 throws FormatException if the value doesn't fit. Either way: raw framework exception, no field name. That's a NEW finding distinct from the budget one (they flagged budget only).

E3 [MED]: Violation.Kind strings are free-form strings, not enum — typo-tolerant API invites drift; but more importantly no doc mapping. EFFECT_SCRIPT.md §3.2 documents AuditResult but NOT the four kinds (Leak/NegativeDip/PeakExceeded/CompatibleConflict appear only in code comments). No error-code→doc mapping anywhere: EAA0901 ids reference §3.3.1 but EFFECT_SCRIPT.md has no EAA table; grep showed EAA0401 mentioned once in EFFECT_SCRIPT.md:190 (a planned diagnostic!). So users can't map Violation.Kind or EAA ids to remediation docs. Check README/docs for such mapping.

E4 [HIGH]: InverseReplay.ReplayAndDead swallows inverse exceptions entirely (`catch { }` at InverseReplay.cs:34-39) without capturing the exception object. PartialReleaseDiagnosis records FailedIndex and Pending resources but NOT the exception itself. Then PluginRuntime.cs:145 fabricates a NEW InvalidOperationException("部分逆释放失败（位置 X，未释放 N 项：…）") discarding the original stack trace/root cause. Ops sees "which resource pending" but never WHY it failed — the actual exception from inv.Execute() vanishes into the void. That's the runtime equivalent of "错误消息丢失根因". Fix: add Exception? RootCause to PartialReleaseDiagnosis, chain as InnerException. Verify: yes, catch block at :34-39 has no variable, nothing stored. Confirmed HIGH.

E5 [MED]: CrashReport carries FiberId + NotifiedDependents but the fabricated messages don't include the failing edge/scope/resource beyond Pending list; more importantly ProviderCrashCascade.Handle notifies dependents into Suspending silently — dependents have NO record that they were suspended due to provider crash (CrashReport.NotifiedDependents exists, good) — ok that's covered. But: DrainTeardownBatch catch(Exception ex) at :147-150 — if _fibers.TryGetValue fails (fiber already removed?), the exception is dropped with no CrashReport — silent swallow. When can TryGetValue fail? _fibers never removes fibers (no Unregister visible), so low risk. But SynchronousExitDrain :183-186 same pattern. LOW-MED.

E6 [HIGH/MED]: OnSuspending hook exceptions swallowed twice (PluginRuntime.cs:118, :229) with comment "钩子异常隔离" — no log, no CrashReport, no counter. The host's ProcessMode-disable callback fails silently ⇒ dependent keeps dispatching during teardown while runtime believes it's suspended. Silent path. Also GodotShell.cs:33 `catch { safe = false; }` — IsSafeToInvoke throwing means "unsafe", conservative direction, acceptable-ish but no diagnostics; :59 `try { d(); } catch { }` — exit drain delegate exceptions completely swallowed with comment "整任务 try/catch" — wait, this contradicts PluginRuntime.SynchronousExitDrain which DOES record crash reports. But GodotShell.EnqueueExitDrain wraps... let me read GodotShell fully. If shell flush calls d() = SynchronousExitDrain, then internal try/catch already handles per-task; outer catch{} would only eat exceptions thrown OUTSIDE those inner catches (e.g., TopoSortLeafFirst cycle detection?). Silent.

E7 [MED]: LoadValidation.VerifyNetClosure message lacks fiber attribution: LoadValidation.cs:80-83 lists violating resources globally "[mem:42]" but NOT which Fiber(s) own them. With 50 fibers, ops can't tell whose lifecycle is open. CheckAll aggregates across fibers losing per-fiber attribution (NetBenefitClosure.CheckAll merges violating arrays, dropping the fiber dimension). Fix: NetClosureResult should carry (FiberId, ResourceId) pairs. Good finding — message quality directly blocks diagnosis. MED-HIGH.

E8 [LOW-MED]: DependencyGraph.FindCycle prints ring path with duplicated head/tail node (round-05 noted, cosmetic). Skip or fold into matrix.

E9 [MED]: Analyzer diagnostics all report at method.Identifier.GetLocation() — the whole method, not the offending call site. EAA0901 knows firstAcquireName (the acquire API name) but reports location = method identifier; for a 300-line method, user must hunt for the call themselves. The diagnostic HAS the info (arg {1}) but not the position. Fix: report at the invocation's location. MED.

E10 [MED]: EAA0303 message says "建议显式 [EffectOverride] 标注意图" — advising users to slap [EffectOverride] on A3 hints trains them to use an escape hatch for benign code; severity Warning default for an intent-clarity hint is arguably too high vs Info. And EAA0303's remedy ([EffectOverride] reason must cite evidence + CI approve) is disproportionate: user gets warning whose official cure requires CI human approval. That's a "报了但用户不知道怎么修（或修的代价不成比例）" case. Also note EAA0303/EAA0304 don't include the two conflicting call sites either.

E11 [LOW]: Algebra.cs:38 KIND_MIX message says "(需 L3 报错)" — tells you L3 SHOULD catch it, but EAA0303 fires only when both calls are in the SAME method; cross-method KIND_MIX surfaces as this opaque InvalidOperationException at runtime with kind names but no resource identity! Message: $"KIND_MIX: 跨 kind 权重未定义 {a}×{b}（需 L3 报错）" — no resource, no caller, no scope. Where did it happen? Weight.Of(a,b) called deep inside metrics; stack trace helps but no resource id. MED maybe. It does say which kinds mixed, not which resource. 

E12 [HIGH?]: EffectScript.Audit violations don't carry event indices either: NegativeDip/Leak attribute by (t, resource, scope) — resource+scope is decent attribution, but CompatibleConflict groups hide WHICH events (kv.Value contains event indices ei but the Violation only exposes count-based detail text; group key has res/scope/mode but not event list). User must find "两个 create commandBuffer:gpu 的 event" manually among hundreds. Detail string for PeakExceeded: $"峰值 {p} > 预算 {kv.Value}" — good (actual vs limit). NegativeDip: includes net interval. Leak: includes net. CompatibleConflict: detail is static text like "create×create 冲突（CONFLICT 集，§3.2.3）" — no event ids, no count. Could include the involved event indices (available in kv.Value HashSet<int>). MED.

E13: Budget cap keys: gate(2) iterates cap.Caps and normalizes nk — fine. But violation reports kv.Key (original un-normalized?) — `violations.Add(new Violation(t, kv.Key, ...))` passes kv.Key which is the ORIGINAL dictionary key (unnormalized), while peak lookup used normalized. Minor inconsistency in reported resource id. Actually looking: foreach (var kv in cap.Caps) { var nk = Normalize(kv.Key); ... violations.Add(new Violation(t, kv.Key, scope, ...)) }. So the Violation.Resource may be a non-normalized alias while elsewhere normalized. LOW.

E14: ParseScope:91 `sc.GetString() ?? throw` — if scene property is a NUMBER (e.g., {"scene": 42}), GetString() returns null?? Actually JsonElement.GetString() on a number returns null? GetString() returns string representation only for String/Number? For Number tokens, GetString() returns the original text? Docs: GetString() throws InvalidOperationException for True/False/Object/Array; for Number it returns the raw text. So {"scene":42} → "42" accepted silently as scene name "42". Not null → no exception. Type confusion silently coerced. Similarly scope.name missing → "" default via hasScene=false path only when property absent; {"type":"scene"} without scene → ScopeId.Scene("") — empty name silently accepted! Line 89-93: hasScene=false → name="" ; ty=="scene" → new Scene("") — empty scene name enters the system silently. Two different silent paths: absent scene + explicit scene type = Scene(""); scene:null → FormatException. Inconsistent: null throws, absent+scene-type silently empty. MED. This is a genuine new silent-path: EffectScriptContract.cs:89-101.

Wait — is Scene("") harmful? Scope grouping: two events both Scene("") group together; probably intended as Global-ish default. But SerializeScope(Scene) outputs {"scene":"S"} WITHOUT type — round-trip of Scene("") → {"scene":""} → parse back Scene("") ok. Silent acceptance of {"type":"scene"} without scene name is undocumented. Also {"type":"global","scene":"X"} silently ignores scene. And unknown extra keys in scope/event/claim objects are silently ignored everywhere (root-level unknown keys noted before — extended: EVERY level silently ignores unknown keys, e.g. "Lifetime" typo instead of "lifetime" → Require throws 缺少字段, good; but "LoopCount" vs "loop"... loop optional anyway. Typo'd "footprints" → 缺少字段: footprint ✓ caught. Unknown EXTRA keys pass silently — e.g. "size" misspelled "scale" → default Interval.Default silently! size is OPTIONAL: c.TryGetProperty("size", out sz) ? ParseInterval(sz) : Interval.Default. Typo "szie":[1,2] → silently becomes Interval.Default [1,1]? What IS Interval.Default? Need to check Numeric.cs. If Default=[1,1], user's [1,2] silently degrades to [1,1] → wrong audit verdicts downstream (peak under-counted!) — silent wrong-answer, worse than exception. THIS is the top silent-path finding. Same for "loop" typo → defaults ω=1 silently; "budget" typo → no caps silently (audit passes with no limits!). These were partially known? Previous rounds: "根级未知键静默忽略" — root-level unknown keys. My extension: optionality + typo interaction creates silent data degradation at every level (size/loop/budget). That's a systemic statement, worth HIGH.

Let me verify Interval.Default value in Numeric.cs.

E15: JsonDocument.Parse(json) at :23 — raw syntax errors DO get line/path from JsonException (framework quality), but the FormatException contract layer destroys positional info. Also JsonDocument is IDisposable, never disposed — minor resource issue, not error-message topic. Skip.

Also check: does anything CATCH FormatException anywhere to enrich? grep shows no catch around Parse in src. Samples might show usage. Let me check IntegrationTests quickly for how errors surface to users, plus check README/docs for troubleshooting/error-code sections. And confirm no "error code" registry exists.

Also EAA0901 message includes fix actions (good, previously acknowledged). EAA0801/0802 messages explain why compile-time (good). EAA0304 message: "出现冲突模式对（{2}，如重复 Create 无配对 Release），运行期可能泄漏/竞态" — no fix action! Compare EAA0901 has explicit numbered fixes. EAA0303 suggests [EffectOverride]. EAA0304 gives NO remediation (should say: pair create with release / separate scopes / mark override). MED.

Severity defaults: all five diagnostics Warning, isEnabledByDefault true. EAA0303 as Warning (intent hint) questionable. OK.

Now the quality matrix. Let me enumerate all throw sites:

EffectScriptContract.cs (17 FormatException): :26 root events (中: has doc ref §4, no location), :55 event object (差), :68 lifetime length (中: expected shape stated), :73 lifetime shape (中), :82 endpoint type (中: expected stated), :88 scope object (差), :91 scope.name null (差), :102 unknown scope.type (中: echoes actual, no valid list!), :112 loop=0 (好: states why), :115 loop shape (中), :120 footprint array (差), :129 kind missing (差: "kind 缺失"), :131 mode missing (差), :140 unknown kind (中: echoes actual, no valid list), :147 unknown mode (同), :152 resource object (差), :156 resource union (好: enumerates five options), :163 resource 形状非法 (差: unreachable-ish fallback), :184 unknown budget key (中: echoes key, previous round noted no valid-list), :205/:224/:242 serialize-side (internal errors, 中), :247 缺少字段 (差: no location), :254 resource.X 非空字符串 (中: names field).

L1 ctor validation:
- DerivedMetrics.cs:18 LoopCount.Of(0) — GOOD: names param, explains consequence ("0 会使规模缩放为 [0,0] 致守恒误报"), states constraint ≥1. Best-in-class.
- Numeric.cs:84-86 Interval — good: cites §, states violated invariant with values. English vs Chinese mix (minor).
- Objects.cs:177 ArgumentOutOfRangeException unknown Kind — has value echo.
- EffectAttributes.cs:45/:71 — cite §, state expectation, nameof param. Good.
- SignedNet.cs:62 — good.
- Algebra.cs:38 KIND_MIX — has kinds but no resource/caller; "需 L3 报错" defers blame. 差/中.

Runtime:
- LoadValidation messages: all include Fiber.Id + resource + rule citation (§3 step2b/§7.1/R5-6) — best tier. Except VerifyNetClosure:80 lacks fiber attribution (noted E7).
- PluginRuntime.cs:55/:58/:159 — clear condition + remedy ("须延迟到 provider 真正 Dead 后"). Good.
- :139 dynamic hard cycle CrashReport — includes full ring path + remedy "须宿主/看门狗另行回收". Good.
- :145/:181 partial release messages — include FailedIndex + Pending list but LOSE root-cause exception (E4).
- InverseReplay.cs:21 state precondition — states current state + forbidden states. Good.
- DependencyGraph FindCycle dup-node cosmetic.

Analyzer EAA0901 好 (has fixes + honesty clause); EAA0801/0802 好; EAA0303 中 (remedy disproportionate); EAA0304 差-中 (no remediation, no call-site locations).

Silent-path inventory (第 5 点系统性清点):
S1. Contract: unknown keys ignored at every level (root budget/event/claim/scope) — size/loop/budget typos degrade silently (HIGH, extends prior root-only finding).
S2. ParseScope scene-absent+type=scene → Scene("") (MED).
S3. {"scene":42} number → GetString() coerces "42" (verify behavior; GetString on Number returns raw text — yes per docs "the original string value" hmm need care. JsonElement.GetString(): "For JsonValueKind.Number returns the raw text". Actually docs say GetString gets string for String kind; for Number it returns... let me be careful. From .NET source: GetString() switch: String→copy, Number→RawText (!), True/False→"true"/"false"?? No — True/False THROW InvalidOperationException. Null→null, Object/Array→throw. Number returns _parent.GetRawTextAsString? I believe Number returns raw text. Yes: JsonElement.GetString() for Number returns the underlying raw text. So {"scene": 42} → name="42". Silent coercion confirmed conceptually. I'll phrase carefully.)
S4. ParseTop numeric-string: lifetime ["0","120"] (strings) → FormatException ✓ caught (only "⊤" allowed as string). Fine.
S5. InverseReplay catch{} drops exception object (HIGH E4).
S6. OnSuspending hook catch{} ×2 (PluginRuntime:118/:229) — no observability.
S7. GodotShell.cs:33 catch→safe=false (conservative, but no log); :59 catch{} on exit drain delegates — silent.
S8. PluginRuntime :149/:185 `if (_fibers.TryGetValue(...))` else-drop: exception discarded if fiber missing (defensive dead branch today).
S9. Sweep: Lo=⊤ events skipped silently (EffectScript.cs sweep loop `if (lt.Lo.IsTop) continue;`) — event never lives, audit never mentions it. Known family ([⊤,⊤] lifetime 静默消失 prior) — Lo=⊤ generalization: partially new? Prior said [⊤,⊤]; here ANY Lo=⊤ (e.g. [⊤,50]) silently vanishes from audit AND At. Extension worth noting briefly as 已核实仍在/扩展.
S10. Analyzer: FindWhitelistEntry null → continue silently — non-whitelisted APIs invisible to approximation (documented honestly in class comment, so "documented silence").
S11. Fiber.Load() returns false silently on non-Inactive; PluginRuntime.LoadAll ignores return value — double-load attempt invisible. Check: LoadAll foreach f.Load() — bool discarded. Silent. MED.
S12. NotifyProviderTeardown non-Active no-op — by design (idempotent), documented. OK.
S13. TickWatchdog C5 from round-05 (double-enqueue) — verify still present: PluginRuntime.cs:218-224 region. Looking at current code lines ~218-231: `f.ForceTeardownOnWatchdog(); _teardownQueue.Add((f.Id, () => InverseReplay.ReplayAndDead(f)));` — is there a guard? ForceTeardownOnWatchdog sets TeardownEnqueued=true internally (if !TeardownEnqueued) but TickWatchdog adds to queue UNCONDITIONALLY after checking state Active/Suspending. Window: f.State==Suspending && TeardownEnqueued==true (provider cascaded BeginTeardown already enqueued it) + isTimedOut(f)==true → watchdog re-enqueues → ReplayAndDead second time → state check throws InvalidOperationException inside task → caught at :147 → false CrashReport. Round-05 C5 verdict: STILL PRESENT (lines ~216-231). I verified visually above: no !TeardownEnqueued guard on the Add. Confirm exact line numbers later via grep -n.

Recoverability flow (第 4 点): user writes bad script → Parse throws FormatException with no index → they must binary-search their JSON by hand. Violations have t/resource/kind but no event ids. No error-code registry, no doc mapping (grep found none: EFFECT_SCRIPT.md has no FormatException scenario coverage — §4 shows example only; no "错误处理" section; no table of messages). EAA ids map to §-numbers inside message text itself (self-documenting, good) but no central doc. Steps-to-fix estimate: FormatException without index = O(N) manual search; with index = O(1).

Let me now verify remaining specifics:
1. Interval.Default value (Numeric.cs).
2. Exact line numbers in PluginRuntime TickWatchdog region & LoadAll foreach f.Load().
3. Whether tests/docs contain error-code mapping (quick grep "EAA09\|错误码\|error code" in docs/README).
4. GodotShell full read (84 lines).
5. DependencyGraph FindCycle lines for completeness.
6. Check JsonElement.GetString() on Number behavior — can't run .NET easily? Maybe dotnet available. Quick test possible via dotnet script? Might take time; I'll phrase conservatively ("GetString 对 Number 返回原始文本（框架行为）" — I'm fairly confident: docs state for Number it returns raw text; for True/False it returns "True"/"False"?? Hmm. Let me actually verify with dotnet if available.)

Let me do these checks.

===== THINK 8 =====

Interesting — TickWatchdog at line ~214-224: `f.ForceTeardownOnWatchdog()` sets TeardownEnqueued=true and State=TearingDown. Then `_teardownQueue.Add(...)`. Then for dependents, `BeginTeardown(d)` is called if `!d.TeardownEnqueued`. Inside BeginTeardown: `if (provider.TeardownEnqueued) return;` — so the dependent d has TeardownEnqueued false → proceeds → `d.Unload()` sets TeardownEnqueued=true and TearingDown → enqueue. So the C5 finding from round 5 (TickWatchdog double-enqueue at :221) — wait, in the current code the direct `_teardownQueue.Add((f.Id, ...))` at line 221 has no `if (!f.TeardownEnqueued)` guard, but ForceTeardownOnWatchdog was only called when f.State is Active/Suspending, and the state check excludes Dead/TearingDown. Can a fiber be Active/Suspending AND TeardownEnqueued=true? Per Fiber.Unload: Unload sets TeardownEnqueued=true and TearingDown together. NotifyProviderTeardown only transitions Active→Suspending without setting TeardownEnqueued. So a Suspending fiber could have TeardownEnqueued... hmm: provider begins teardown → NotifyDependents → dependent.NotifyProviderTeardown (Active→Suspending). Then later someone calls dependent.Unload() directly? Unload would set TeardownEnqueued=true + TearingDown. Then state no longer Suspending. So within TickWatchdog, when we see f with State Active/Suspending and isTimedOut(f), can f.TeardownEnqueued already be true? If TeardownEnqueued=true then Unload set state to TearingDown, or ForceTeardownOnWatchdog did. Both leave state=TearingDown. So Active/Suspending ⇒ !TeardownEnqueued invariant holds... unless something else sets TeardownEnqueued directly (internal setter). It's `internal set`. Within the assembly: PluginRuntime and Fiber set it via Unload/ForceTeardownOnWatchdog. OK so round-5's C5 might have been fixed or might be a misreport. Round-5 said "Suspending+TeardownEnqueued 已置位的窗口内（provider 先行 BeginTeardown 级联通知 dependent Suspending、watchdog 同帧触发）:221 无条件 _teardownQueue.Add 再次入队" — but per current code, if provider's BeginTeardown cascaded to dependent d, then d.Unload() inside BeginTeardown sets d.TeardownEnqueued=true and d.State=TearingDown. So when TickWatchdog iterates over d: state==TearingDown excluded by the guard at :220. And the direct f itself: f was Active/Suspending with TeardownEnqueued=false per invariant. So `_teardownQueue.Add` happens exactly once. Actually wait — order matters: TickWatchdog processes f first: ForceTeardownOnWatchdog (sets TeardownEnqueued=true), Add to queue, NotifyDependents, then for each dep: OnSuspending hook, BeginTeardown(d) → d.Unload() → d enqueued once. Fine. But what about the reverse order: provider p began teardown earlier (p.TearingDown, queue contains p). Dependent d got Suspending via notify but its own BeginTeardown