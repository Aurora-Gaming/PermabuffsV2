#!/usr/bin/env bash
# 冒烟测试：覆盖所有黑名单入口 + 常规流程。
# 全部从控制台下发，因此玩家级命令（permabuff / clearbuffs）会正确拒绝，
# 这本身就是预期行为。
set -uo pipefail

/app/send.sh \
  "pbreload" \
  "gpermabuff -g list" \
  "globalbuff Gills" \
  "globalbuff 4" \
  "globalbuff 0" \
  "globalbuff 99999" \
  "globalbuff Regeneration" \
  "regionbuff add spawn Gills 10" \
  "regionbuff add spawn 4 10" \
  "regionbuff add spawn Swiftness 10" \
  "regionbuff del spawn Swiftness" \
  "regionbuff add spawn Swiftness 999" \
  "regionbuff add nosuchregion Swiftness 10" \
  "regionbuff frobnicate spawn Swiftness" \
  "globalbuff Regeneration" \
  "clearbuffs all" \
  "clearbuffs" \
  "permabuff" \
  "help regionbuff" \
  "help globalbuff"

sleep 2
echo "=== 冒烟测试输出 ==="
sed -n '/Server started/,$p' /app/server.log
