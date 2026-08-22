Everything is verified. `git status` shows clean (no staged files — the harness uses a different VCS or none). The generator now emits real per-method Signature code referencing L1 (`GodotApiWhitelist.All`, `Signature.Union`/`Of`), no longer a stub.

Let me produce the acceptance report.

Final verification summary:
- Full solution build: 0 errors, 0 warnings
- `dotnet test`: 141 passed, 0 failed
- Generator upgraded from stub to real per-method Signature emission
- Fixed a genuine generator bug: `HasAttributeName` only matched the long attribute form (`EffectOverrideAttribute`), not the short `EffectOverride` — the comment claimed both but the code didn't. Now uses short-name constants so both forms match.
- Added 2 tests: `Generator_EmitsRealSignatureDelegatingToL1` (asserts generated code references `GodotApiWhitelist.All` + `Signature.Union` + `ComputeM`) and `Generator_EmittedCompute_AddChild_ReturnsWhitelistedClaims` (compiles generated code + reflects `ComputeAddChild` → returns Signature with `Kind.Occupy`).

Let me write the acceptance report.</think:6124c78e>BUILD_OK — 测试 0 失败，已升级 L2 生成器为真实每方法 Signature（引用 GodotApiWhitelist+Signature，非桩）+ 生成消费测试。