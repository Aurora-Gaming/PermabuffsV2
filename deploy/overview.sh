#!/usr/bin/env bash
# 打印 Permabuffs 数据库与配置分组概览
set -uo pipefail

/app/dumpdb.sh

echo
echo "=== PermabuffsConfig 分组概览 ==="
python3 <<'PY'
import json
c = json.load(open('/app/tshock/PermabuffsConfig.json', encoding='utf-8'))
for g in c.get('buffgroups') or []:
    print("  %-10s perm=pb.%-10s n=%2d" % (g['groupName'], g['groupPerm'], len(g['buffIDs'])))
print("  blacklist     =", c.get('blacklist'))
print("  duration      =", c.get('permabuffDuration'), "ticks")
print("  heartbeat     =", c.get('heartbeatTicks'), "ticks")
PY
