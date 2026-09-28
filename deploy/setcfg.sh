#!/usr/bin/env bash
# 打开/关闭 Permabuffs 的诊断开关（改完自动 /pbreload）
set -uo pipefail

python3 - "$@" <<'PY'
import json, sys
p = '/app/tshock/PermabuffsConfig.json'
cfg = json.load(open(p, encoding='utf-8'))
for a in sys.argv[1:]:
    k, _, v = a.partition('=')
    cfg[k] = json.loads(v)
json.dump(cfg, open(p, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
for k in ('diagnostics', 'autoGrantOnLogin', 'autoGrantGroups', 'blacklist', 'heartbeatTicks', 'permabuffDuration', 'forceClientResync'):
    if k in cfg:
        print('  %-18s = %s' % (k, json.dumps(cfg[k], ensure_ascii=False)))
PY

/app/send.sh pbreload
