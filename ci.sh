#!/usr/bin/env bash
# 编译期审计 gate 本地等价（迭代28 #113）
set -euo pipefail
cd "$(dirname "$0")"
unset MSBUILD_EXE_PATH   # 本机 VS 坏 MSBuild 特例，CI runner 不需要
dotnet build Cosmos.EffectAlgebra.slnx -c Release -warnaserror
dotnet test Cosmos.EffectAlgebra.slnx -c Release --no-build
echo "CI audit gate: PASS (0 errors, 0 warnings, 0 test failures)"
