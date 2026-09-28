#!/usr/bin/env bash
# 向运行中的 TShock 控制台下发命令。
# 用法: ./send.sh "命令1" "命令2" ...
FIFO=/app/console.in
LOG=/app/server.log

for c in "$@"; do
  printf '%s\n' "$c" > "$FIFO"
  sleep 1.2
done

# 留一点时间让服务器处理并刷日志
sleep 1.5
