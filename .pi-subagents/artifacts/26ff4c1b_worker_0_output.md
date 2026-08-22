88 tests pass, 0 failures. FIX_OK.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Rewrote CrossTableTests.cs: KnownReleaseSemanticsCanon now derives from ReleaseClass.Names.Select(Canonical).Union(new[]{Audio.Stop,Anim.Stop}.Select(Canonical)); deleted the 7 hardcoded literal strings. No other test changed; assertions untouched."
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "dotnet test -clp:ErrorsOnly => 失败:0, 通过:88, 已跳过:0 (matches required 88, 0 failures). File on disk at D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/CrossTableTests.cs."
    }
  ],
  "changedFiles": [
    "D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/CrossTableTests.cs"
  ],
  "testsAddedOrUpdated": [
    "D:/Godot/Cosmos/tests/Cosmos.EffectAlgebra.Tests/CrossTableTests.cs"
  ],
  "commandsRun": [
    {
      "command": "MSBUILD_EXE_PATH= dotnet test -clp:ErrorsOnly",
      "result": "passed",
      "summary": "88 passed, 0 failed, 0 skipped"
    }
  ],
  "validationOutput": [
    "通过:88, 失败:0, 已跳过:0, 总计:88"
  ],
  "residualRisks": [
    "none"
  ],
  "noStagedFiles": true,
  "diffSummary": "KnownReleaseSemanticsCanon field now derives from ReleaseClass.Names (single source of truth) unioned with Audio.Stop/Anim.Stop, eliminating 7 duplicated hardcoded literals; comment updated to reflect OPEN-1 resolution.",
  "reviewFindings": [
    "no blockers"
  ],
  "manualNotes": "Used POSIX shell (bash) with MSBUILD_EXE_PATH= (empty) inline since the harness runs bash not pwsh. Build/test green."
}
```

FIX_OK 失败数=0，已改 KnownReleaseSemanticsCanon 从 ReleaseClass.Names 派生（删 7 串硬编码，OPEN-1）。