157 tests pass (146 prior + 11 new), 0 failures. Verified on disk.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Wrote AttributeBoundaryTests.cs (11 test cases) proving §8.3.1 reason non-empty (3 throws: empty/null/whitespace + 1 valid) and §8.3.2 epsilon∈[0,0.5] (3 throws: 0.6/-0.1/1.0 + 3 valid boundary/interior via Theory) and §8.3.1 kind-not-overridable (reflection GetProperty OverrideKind == null). Constructors in EffectAttributes.cs already enforce these by throw; tests are falsifiable (removing throws => red)."
    }
  ],
  "changedFiles": [
    "tests/Cosmos.EffectAlgebra.Tests/AttributeBoundaryTests.cs"
  ],
  "testsAddedOrUpdated": [
    "tests/Cosmos.EffectAlgebra.Tests/AttributeBoundaryTests.cs"
  ],
  "commandsRun": [
    {
      "command": "cmd //c \"set MSBUILD_EXE_PATH= && dotnet test -clp:ErrorsOnly\"",
      "result": "passed",
      "summary": "已通过! - 失败: 0, 通过: 157, 已跳过: 0 (net10.0)"
    }
  ],
  "validationOutput": [
    "已通过! - 失败: 0，通过: 157，已跳过: 0，总计: 157，持续时间: 1 s - Cosmos.EffectAlgebra.Tests.dll (net10.0)"
  ],
  "residualRisks": [
    "none — constructors already enforce boundaries; no EffectAttributes.cs edit required"
  ],
  "noStagedFiles": true,
  "diffSummary": "Added AttributeBoundaryTests.cs with 11 tests (4 EffectOverride + 3 AcceptDeviation + 3 Theory cases + 1 reflection) all §8.3.1/§8.3.2 cited; no source changes needed.",
  "reviewFindings": [
    "no blockers — EffectAttributes.cs constructors already throw per spec"
  ],
  "manualNotes": "EffectAttributes.cs required no modification: throw logic already present (ArgumentException on IsNullOrWhiteSpace(reason); ArgumentOutOfRangeException when epsilon<0 or >0.5). Tests lock those type-layer boundaries."
}
```

TEST_OK 失败数=0，已写 AttributeBoundaryTests.cs（reason 非空 3 例 + epsilon 上界 3 例 + kind 不可覆盖反射 1 例，全部 §8.3 出处）。