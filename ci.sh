#!/usr/bin/env bash
# 编译期审计 gate 本地等价（迭代28 #113）
set -euo pipefail
cd "$(dirname "$0")"
unset MSBUILD_EXE_PATH   # 本机 VS 坏 MSBuild 特例，CI runner 不需要
dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror --no-incremental  # r6-gate：增量构建可跳过 csc⇒分析器不运行，曾本地假绿两天而 CI 红（xUnit2002）；与 CI 干净机语义对齐
# qed-gate2（2026-09-07）：三测试工程顺序跑，替代 slnx 并行——本机高内存负载下并行测试宿主
# 的进程内 Roslyn 编译会 OOM 偶红（52 样本）；顺序执行峰值内存大幅下降，总时长增加有限。
# qed-p3（D4d/D5）：形式规约验证入门禁——0 errors 才放行（证明产物即门禁本体）。
# 依赖：dafny 4.11（dotnet tool install -g Dafny）+ Z3 4.12.1（DAFNY_Z3 可覆盖，缺省见 ROADMAP D1）。
dafny verify --solver-path "${DAFNY_Z3:-$HOME/.dotnet/tools/z3/bin/z3-4.12.1.exe}"   formal/CosmosEffectAlgebra.dfy formal/CosmosSweepLine.dfy
dotnet test tests/Cosmos.EffectAlgebra.Tests/Cosmos.EffectAlgebra.Tests.csproj -c Release --no-build
dotnet test tests/Cosmos.EffectAlgebra.Runtime.Tests/Cosmos.EffectAlgebra.Runtime.Tests.csproj -c Release --no-build
dotnet test samples/GodotIntegration/SampleGame.csproj -c Release --no-build
echo "CI audit gate: PASS (0 errors, 0 test failures; AnalyzerConsumer 1 条 EAA0901 故意泄漏警告为样例设计)"
# qed-gate（2026-09-06）：本地两次偶红 OOM（A5/A2 会话）根因均为 MSBuild 节点 + Roslyn 编译服务器
# 跨多次门禁运行累积内存（~2GB），饿死三门并行测试宿主的进程内 Roslyn 编译；build-server shutdown
# 后同提交确定性全绿（CI 干净机不受影响，此行仅防本机长会话累积）。shutdown 失败不使门禁转红。
dotnet build-server shutdown >/dev/null 2>&1 || true
