#!/usr/bin/env bash
# 拉取最新 TShock 日志里的诊断行 + 玩家活动
set -uo pipefail

L=$(ls -t /app/tshock/logs/*.log | head -1)
echo "=== $L ==="
echo
echo "--- buffs 非空的诊断行（最多 15 条）---"
grep 'PB-DIAG' "$L" | grep -v 'buffs=\[-\]' | head -15
echo
echo "--- buffs 字段取值分布 ---"
grep -oE 'buffs=\[[^]]*\]' "$L" | sort | uniq -c | sort -rn | head -10
echo
echo "--- 诊断总行数 ---"
grep -c 'PB-DIAG' "$L"
echo
echo "--- 玩家活动（最后 20 条）---"
grep -E 'executed|tried to|has joined|has left' "$L" | tail -20
