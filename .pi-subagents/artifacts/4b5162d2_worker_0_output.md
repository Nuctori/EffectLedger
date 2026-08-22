139 tests pass (88 prior + 51 from this file: 25 matrix + 25 symmetric + 1 total-function), 0 failures. Build green.

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "新文件 CompatibleMatrixTests.cs 仅实现 §3.2.3 全 25 组合期望表穷举锁 + 对称性 + 全函数断言，未触碰源代码（实现已与 §3.2.3 一致，无 bug 需修）；作用域未扩大。"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "期望表逐字引 §3.2.3（CONFLICT/良性配对/use 放行/Unknown 视为 Use），25 组合经 [Theory] MemberData 穷举；对称断言 + 全函数不抛断言；dotnet test 0 失败（139 通过）。"
    }
  ],
  "changedFiles": [
    "tests/Cosmos.EffectAlgebra.Tests/CompatibleMatrixTests.cs"
  ],
  "testsAddedOrUpdated": [
    "tests/Cosmos.EffectAlgebra.Tests/CompatibleMatrixTests.cs"
  ],
  "commandsRun": [
    {
      "command": "dotnet test -clp:ErrorsOnly (in tests/Cosmos.EffectAlgebra.Tests)",
      "result": "passed",
      "summary": "已通过! 失败:0, 通过:139, 跳过:0 — 新文件贡献 51 用例（25 矩阵 + 25 对称 + 1 全函数），与既有 88 合计 139"
    }
  ],
  "validationOutput": [
    "已通过! - 失败: 0，通过: 139，已跳过: 0，总计: 139，持续时间: 677 ms - Cosmos.EffectAlgebra.Tests.dll (net10.0)"
  ],
  "residualRisks": [
    "None — 期望表与 §3.2.3 完全一致，实现 Compatible.IsCompatible 经 Resolve(Unknown→Use) 后行为符合矩阵，无魔法数（全部 Mode 枚举字面量）。"
  ],
  "noStagedFiles": true,
  "diffSummary": "新增 CompatibleMatrixTests.cs：§3.2.3 兼容矩阵 25 组合期望表 (Mode,Mode,bool)[]，3 个测试方法（矩阵逐字断言 / 对称 / 全函数不抛）。",
  "reviewFindings": [
    "no blockers — 实现 Compatible.IsCompatible (Algebra.cs L18) 已正确编码 §3.2.3，无需修改源码。"
  ],
  "manualNotes": "本机 shell 为 bash，env 清理由 `MSBUILD_EXE_PATH= dotnet ...` 前缀完成（等价 PowerShell `$env:MSBUILD_EXE_PATH=$null`）。"
}
```

TEST_OK 失败数=0，已写 CompatibleMatrixTests.cs（§3.2.3 全 25 组合期望表穷举锁 + 对称）。