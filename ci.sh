#!/usr/bin/env bash
# 编译期审计 gate 本地等价（迭代28 #113）
set -euo pipefail
cd "$(dirname "$0")"
unset MSBUILD_EXE_PATH   # 本机 VS 坏 MSBuild 特例，CI runner 不需要
dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror --no-incremental  # r6-gate：增量构建可跳过 csc⇒分析器不运行，曾本地假绿两天而 CI 红（xUnit2002）；与 CI 干净机语义对齐
dotnet test Cosmos.EffectAlgebra.slnx -c Release --no-build
echo "CI audit gate: PASS (0 errors, 0 test failures; AnalyzerConsumer 1 条 EAA0901 故意泄漏警告为样例设计)"
# qed-gate（2026-09-06）：本地两次偶红 OOM（A5/A2 会话）根因均为 MSBuild 节点 + Roslyn 编译服务器
# 跨多次门禁运行累积内存（~2GB），饿死三门并行测试宿主的进程内 Roslyn 编译；build-server shutdown
# 后同提交确定性全绿（CI 干净机不受影响，此行仅防本机长会话累积）。shutdown 失败不使门禁转红。
dotnet build-server shutdown >/dev/null 2>&1 || true
