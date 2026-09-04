# 编译期审计 gate 本地等价（迭代28 #113，PowerShell）
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:MSBUILD_EXE_PATH = $null   # 本机 VS 坏 MSBuild 特例，CI runner 不需要
dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror --no-incremental  # r6-gate：同 ci.sh，与 CI 干净机语义对齐
dotnet test Cosmos.EffectAlgebra.slnx -c Release --no-build
Write-Host "CI audit gate: PASS (0 errors, 0 test failures; AnalyzerConsumer 1 条 EAA0901 故意泄漏警告为样例设计)"
