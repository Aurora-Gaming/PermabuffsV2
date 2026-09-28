#!/usr/bin/env bash
# 首次启动的建世界向导自动化：普通 / For the Worthy 两种种子各来一套。
# 之所以要 For the Worthy —— Main.getGoodWorld 为真时，
# Player.CheckDrowning() 里 `if (gills) flag = getGoodWorld && !flag;` 会把"是否淹没"取反，
# 这正是鱼腮在陆地上把人淹死的条件。
set -uo pipefail

WORLD="${1:-testworld}"
SIZE="${2:-1}"
SEED="${3:-}"
EVIL="${4:-}"

/app/send.sh \
  "n" \
  "$WORLD" \
  "$SIZE" \
  "$SEED" \
  "$EVIL" \
  "" "" "" "" "" "" "" ""

echo "[newworld] 已下发，等待生成..."
sleep 25
