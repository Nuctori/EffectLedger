#!/usr/bin/env bash
# 契约模块打包消费烟测（真实 NuGet 管线，隔离缓存）：
#   1. pack EffectLedger.Contracts + EffectLedger.Contracts.Analyzer 到仓库本地 feed；
#   2. 隔离 NUGET_PACKAGES 全新还原独立消费者（排除同 id+version 全局缓存遮蔽）；
#   3. Paired 构建必须成功（分析器加载但不误报合法代码）；
#   4. Leaky 构建必须失败且日志含 EBC2001（分析器未落 analyzers/ 或静默不加载时此工程会假绿）。
# 前置：无（本脚本自行 pack 与还原）。
set -euo pipefail
cd "$(dirname "$0")/../.."   # 仓库根

FEED="$PWD/artifacts/contracts-feed"
CACHE="$PWD/artifacts/contracts-smoke-packages"
LEAKY_LOG="$PWD/artifacts/contracts-leaky-build.log"
CFG="${1:-Debug}"
AUDIT="-p:NuGetAudit=false"   # 环境无外网时禁用漏洞审计源（不影响包还原本身）

rm -rf "$FEED" "$CACHE" "$LEAKY_LOG"

echo "ConsumerSmoke(contracts): pack 到本地 feed"
dotnet pack src/EffectLedger.Contracts/EffectLedger.Contracts.csproj -c "$CFG" -o "$FEED" $AUDIT --nologo --verbosity quiet
dotnet pack src/EffectLedger.Contracts.Analyzer/EffectLedger.Contracts.Analyzer.csproj -c "$CFG" -o "$FEED" $AUDIT --nologo --verbosity quiet

# 结构门：分析器必须真落 analyzers/dotnet/cs/（未落则消费方静默不加载 = 假绿）。
# 实现说明：不用 python（ubuntu-latest 只有 python3，且既有烟测不依赖 python；
# 引入解释器依赖会让门禁在干净 runner 上直接失败）。改用 unzip -l 解析包清单。
ANALYZER_PKG=$(ls "$FEED"/EffectLedger.Contracts.Analyzer.*.nupkg 2>/dev/null | head -1)
[ -n "$ANALYZER_PKG" ] || { echo "ConsumerSmoke(contracts): FAIL——未生成 Analyzer 包"; exit 1; }
if command -v unzip >/dev/null 2>&1; then
  unzip -l "$ANALYZER_PKG" | grep -q "analyzers/dotnet/cs/EffectLedger.Contracts.Analyzer.dll"     || { echo "ConsumerSmoke(contracts): FAIL——Analyzer DLL 未落 analyzers/dotnet/cs/"; unzip -l "$ANALYZER_PKG"; exit 1; }
  echo "ConsumerSmoke(contracts): analyzers/ 落位 OK -> analyzers/dotnet/cs/EffectLedger.Contracts.Analyzer.dll"
else
  # 无 unzip 时退化为"包内出现该路径"的字节串检查（zip 条目名以明文存储）。
  grep -aq "analyzers/dotnet/cs/EffectLedger.Contracts.Analyzer.dll" "$ANALYZER_PKG"     || { echo "ConsumerSmoke(contracts): FAIL——Analyzer DLL 未落 analyzers/dotnet/cs/"; exit 1; }
  echo "ConsumerSmoke(contracts): analyzers/ 落位 OK（字节串校验）"
fi

export NUGET_PACKAGES="$CACHE"
export NUGET_CONFIG_FILE="$PWD/tests/ContractsConsumerSmoke/nuget.config"

echo "ConsumerSmoke(contracts): 隔离缓存全新还原 Paired"
dotnet build tests/ContractsConsumerSmoke/Paired/Paired.csproj -c "$CFG" $AUDIT --nologo
echo "ConsumerSmoke(contracts): Paired OK（合法消费者零诊断构建）"

echo "ConsumerSmoke(contracts): Leaky 必须失败"
if dotnet build tests/ContractsConsumerSmoke/Leaky/Leaky.csproj -c "$CFG" $AUDIT --nologo >"$LEAKY_LOG" 2>&1; then
  echo "ConsumerSmoke(contracts): FAIL——Leaky 构建意外成功：分析器未加载（静默假绿）"; exit 1
fi
grep -q "EBC2001" "$LEAKY_LOG" || { echo "ConsumerSmoke(contracts): FAIL——Leaky 失败但未见 EBC2001"; cat "$LEAKY_LOG"; exit 1; }
echo "ConsumerSmoke(contracts): PASS（配对绿 / 违规红且含 EBC2001 / 真实 NuGet 包链）"
