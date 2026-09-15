# 打包消费烟测（ci.ps1 用，逻辑与 run-smoke.sh 逐条对应，见该文件头注）。
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '../..')   # 仓库根
$feed = Join-Path (Get-Location) 'artifacts/feed'
$smokeCache = Join-Path (Get-Location) 'artifacts/nuget-smoke-packages'
$leakyLog = Join-Path (Get-Location) 'artifacts/leaky-build.log'
Remove-Item -Recurse -Force $feed, $smokeCache, $leakyLog -ErrorAction SilentlyContinue
dotnet pack EffectLedger.slnx -c Release --no-build -o $feed
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$env:NUGET_PACKAGES = $smokeCache   # 隔离缓存：全新机器语义（排除同 id+version 全局缓存遮蔽）
dotnet build tests/ConsumerSmoke/Paired/Paired.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host 'ConsumerSmoke: Paired OK（net8 + Generator 单装传递闭包）'
$genFile = Get-ChildItem 'tests/ConsumerSmoke/Paired/obj/Generated' -Recurse -Filter '*.cs' |
  Where-Object { $_.FullName -like '*EffectAlgebraGenerator*' } | Select-Object -First 1
if (-not $genFile -or -not (Select-String -Path $genFile.FullName -Pattern 'ComputeLoad' -Quiet)) {
  Write-Host 'ConsumerSmoke: FAIL——生成器未从包链 emit（Generated/ 无 ComputeLoad）'; exit 1
}
Write-Host "ConsumerSmoke: Generator emit OK（$($genFile.FullName)）"
dotnet build tests/ConsumerSmoke/Leaky/Leaky.csproj -c Release --nologo *> $leakyLog
$leakyExit = $LASTEXITCODE
if ($leakyExit -eq 0) { Write-Host 'ConsumerSmoke: FAIL——Leaky 构建意外成功：分析器未加载（静默假绿）'; exit 1 }
Select-String -Path $leakyLog -Pattern 'EAA0901' -Quiet | ForEach-Object {
  if (-not $_) { Write-Host 'ConsumerSmoke: FAIL——Leaky 构建失败但未见 EAA0901'; Get-Content $leakyLog; exit 1 }
}
Write-Host 'ConsumerSmoke: PASS（配对绿 / 泄漏红 / 真实 NuGet 包链）'
