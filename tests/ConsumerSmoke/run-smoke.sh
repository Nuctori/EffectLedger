#!/usr/bin/env bash
# 打包消费烟测（CI 与本地门禁共用的真实 NuGet 管线走查）：
#   1. dotnet pack 五包（L1/Analyzer/Generator/Runtime/Tool）到仓库本地 feed；
#   2. 隔离 NUGET_PACKAGES 全新还原独立消费者工程（排除同 id+version 全局缓存遮蔽，README ⓪ R6-P）；
#   3. Paired（net8，Generator 单装、L1 经依赖组传递）构建必须成功——emit 代码硬引用 L1 类型，
#      还原/编译任一环闭包断裂即红；
#   4. Leaky（net10，Analyzer 真实包链加载）构建必须失败且日志含 EAA0901——
#      分析器未落 analyzers/ 路径或静默不加载时此工程会假绿构建成功，烟测立即抓住。
# 前置：Release 已构建（ci.yml / ci.sh / ci.ps1 均在 build+test 之后调用本脚本）。
set -euo pipefail
cd "$(dirname "$0")/../.."   # 仓库根
FEED="$PWD/artifacts/feed"
SMOKE_CACHE="$PWD/artifacts/nuget-smoke-packages"
LEAKY_LOG="$PWD/artifacts/leaky-build.log"
rm -rf "$FEED" "$SMOKE_CACHE" "$LEAKY_LOG"
dotnet pack EffectLedger.slnx -c Release --no-build -o "$FEED"
export NUGET_PACKAGES="$SMOKE_CACHE"   # 隔离缓存：全新机器语义
dotnet build tests/ConsumerSmoke/Paired/Paired.csproj -c Release --nologo
echo "ConsumerSmoke: Paired OK（net8 + Generator 单装传递闭包）"
GEN_FILE=$(find tests/ConsumerSmoke/Paired/obj/Generated -name '*.cs' -path '*EffectAlgebraGenerator*' 2>/dev/null | head -1)
grep -q "ComputeLoad" "${GEN_FILE:-/dev/null}" || { echo "ConsumerSmoke: FAIL——生成器未从包链 emit（Generated/ 无 ComputeLoad）"; exit 1; }
echo "ConsumerSmoke: Generator emit OK（$GEN_FILE）"
if dotnet build tests/ConsumerSmoke/Leaky/Leaky.csproj -c Release --nologo >"$LEAKY_LOG" 2>&1; then
  echo "ConsumerSmoke: FAIL——Leaky 构建意外成功：分析器未加载（静默假绿）"; exit 1
fi
grep -q "EAA0901" "$LEAKY_LOG" || { echo "ConsumerSmoke: FAIL——Leaky 构建失败但未见 EAA0901"; cat "$LEAKY_LOG"; exit 1; }
echo "ConsumerSmoke: PASS（配对绿 / 泄漏红 / 真实 NuGet 包链）"
