#!/usr/bin/env bash
# TShock 启动脚本 —— 固化环境变量，避免每次手动 export
set -euo pipefail

export DOTNET_ROOT=/app/dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

cd /app

# TShock 需要 Terraria 服务端内容；首次运行 installer 会拉取。
# 这里只在缺失时补一次，避免每次启动都联网。
if [ ! -f /app/bin/TerrariaServer.dll ]; then
  echo "[start] 未检测到 TerrariaServer.dll，先跑一次 installer..."
  ./TShock.Installer
fi

# 配置文件缺失时写入一份可直接启动的最小配置
if [ ! -f /app/tshock/config.json ]; then
  echo "[start] 首次启动：生成默认配置"
fi

echo "[start] 启动 TShock..."
exec ./TShock.Server \
  -c /app/tshock/config.json \
  -ssc /app/tshock/sscconfig.json \
  -motd /app/tshock/motd.txt \
  -rules /app/tshock/rules.txt \
  -whitelist /app/tshock/whitelist.txt \
  "$@"
