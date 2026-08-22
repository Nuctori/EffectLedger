`audit/iter15.md` is written and the header contains "本轮重跑" as required. Audit complete.

**Summary of findings (Iter15 — ScopeId⊆ 偏序悬空):**

The core gap is that **§3.1.3 defines `ScopeId` as 7 constructors but never defines the `⊆` partial order**, yet both `Peak` (L154) and `peak` (L167) depend on `c.scope ⊆ scope` to filter which Claims fall into a window — so the membership of the summed set is undecidable, and the peak number has **no mathematical definition** (DO-8 hangs).

Six further gaps compound it:
- **I15-02 (high):** Every §7 mapping annotates `shell_scope`, which is **not among the 7 ScopeId constructors** → all §7 Claims are type-illegal vs §3.1.1 `scope ∈ ScopeId`.
- **I15-03:** `global_scope` vs `Global` naming mismatch, no normalization rule.
- **I15-04:** Two *contradictory* Peak definitions — L154 uses count `|·|` over loop index `i∈1..ω`; L167 uses `Σ size` over scope windows `t∈scope`. Different dimensions and traversal domains.
- **I15-05:** `peak`'s `max_{t∈scope}` never lists which ScopeIds are legal windows → max domain undetermined.
- **I15-06:** `net` (L163-165) has **no scope parameter** — global sum hides local leaks (DO-9 fails), compounded by Iter08's QueueFree `mode=move`.
- **I15-07/08:** §7 annotations collapse to a binary `shell_scope`/`global_scope`, so even a defined ⊆ would be trivial; the 5 fine-grained constructors are never used.

Conditional proofs (P1–P3) show that once an explicit ⊆ and normalized §7 annotations are supplied, Peak/peak/net become well-defined.