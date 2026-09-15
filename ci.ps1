# 编译期审计 gate 本地等价（迭代28 #113，PowerShell）
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:MSBUILD_EXE_PATH = $null   # 本机 VS 坏 MSBuild 特例，CI runner 不需要
dotnet build EffectLedger.slnx -c Release -warnaserror --no-incremental  # r6-gate：同 ci.sh，与 CI 干净机语义对齐
# qed-gate2（2026-09-07）：同 ci.sh——三测试工程顺序跑，防并行测试宿主 OOM 偶红
# qed-p3（D4d/D5）：形式规约验证入门禁（依赖与环境同 ci.sh 注记）
$z3 = if ($env:DAFNY_Z3) { $env:DAFNY_Z3 } else { "$HOME/.dotnet/tools/z3/bin/z3-4.12.1.exe" } # P5.2-M4：与 ci.sh 默认值对称
dafny verify --solver-path $z3 formal/EffectLedger.dfy formal/EffectLedgerSweepLine.dfy
$dafnyExit = $LASTEXITCODE       # P5.2-N3：dafny 失败不得被后续测试退出码掩盖
# qed-gate2：三测试工程顺序跑（P5.2-N3：逐工程检查退出码——只看最后一个会吞前面的红）
# O-2026-09-14-01 兜底：--blame-hang 把任何宿主停滞转为有界确定性失败（300s 无活动即断言失败），绝不无限挂死
dotnet test tests/EffectLedger.Tests/EffectLedger.Tests.csproj -c Release --no-build --blame-hang --blame-hang-timeout 300s
$t1 = $LASTEXITCODE
dotnet test tests/EffectLedger.Runtime.Tests/EffectLedger.Runtime.Tests.csproj -c Release --no-build --blame-hang --blame-hang-timeout 300s
$t2 = $LASTEXITCODE
dotnet test samples/GodotIntegration/SampleGame.csproj -c Release --no-build --blame-hang --blame-hang-timeout 300s
$t3 = $LASTEXITCODE
# roi-2026-09-14：打包消费烟测——真实 NuGet 管线走查（同 ci.sh；PowerShell 版脚本）
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ConsumerSmoke/run-smoke.ps1
$t4 = $LASTEXITCODE
# qed-gate（2026-09-06）：同 ci.sh——防本机长会话 MSBuild/Roslyn 服务器累积致并行测试宿主 OOM 偶红
dotnet build-server shutdown 2>$null
$gateExit = 0
foreach ($c in @($dafnyExit, $t1, $t2, $t3, $t4)) { if ($c -ne 0) { $gateExit = $c; break } }
if ($gateExit -eq 0) { Write-Host "CI audit gate: PASS (0 errors, 0 test failures; AnalyzerConsumer 1 条 EAA0901 故意泄漏警告为样例设计)" }
exit $gateExit
