#!/usr/bin/env bash
# 后台启动 TShock，stdin 接 FIFO，方便脚本化下发控制台命令做实验。
set -uo pipefail

export DOTNET_ROOT=/app/dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

FIFO=/app/console.in
LOG=/app/server.log

cd /app

pkill -f 'TShock.Server' 2>/dev/null
sleep 1
rm -f "$FIFO"; mkfifo "$FIFO"
: > "$LOG"

# 常驻一个写端，避免读端在无人写时被 EOF 关掉
sleep infinity > "$FIFO" &
echo $! > /app/fifo-holder.pid

nohup ./TShock.Server \
  -c /app/tshock/config.json \
  -ssc /app/tshock/sscconfig.json \
  -motd /app/tshock/motd.txt \
  -rules /app/tshock/rules.txt \
  -whitelist /app/tshock/whitelist.txt \
  < "$FIFO" > "$LOG" 2>&1 &

echo "started pid=$!  log=$LOG  fifo=$FIFO"
