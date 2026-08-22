#17 file written and header verified (`# Iter17 审计 — ... 独立审计 #17（hy3 单独进程，本轮重跑）`). The `todo` tool is unavailable in this subagent fork, so the live todo update for #17 remains the orchestrator's responsibility — the file evidence is on disk.

Implemented independent audit #17 (MA-004 net/peak confusion convergence truth).
Changed files: `audit/iter17.md` (overwritten, ~9.1KB, header contains "本轮重跑").
Validation: read back line 1 confirms required header; file contains Q1–Q6 sections (proposition / math-property / status / argument / line-numbers), Proof Obligation ledger (PO-I17-a…e), and I17-01…I17-06 gap list; cross-references §3.2.5/§3.3.1/§3.3.2/§3.4 MA-004/§1 DO-8,DO-9/§12.2/Iter15/Iter16 with real line numbers.
Open risks/questions: net/peak each mathematically undefined (net lacks scope per Iter15 I15-06; peak depends on undefined ⊆ per Iter15 I15-01) ⇒ MA-004 "resolved" is naming-layer only, not semantic; three mixed metric objects (Peak count,i / peak Σsize,t / net) have no unified basis; net<0 semantics undefined conflicting with DO-9.
Recommended next step: orchestrator dispatches independent subagent #18 (two ∞ semantics) and marks #17 todo complete.