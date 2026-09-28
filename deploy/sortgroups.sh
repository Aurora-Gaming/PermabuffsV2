#!/usr/bin/env bash
# 把配置里所有分组的 buff ID 统一按升序排列，然后 /pbreload
set -uo pipefail

python3 <<'PY'
import json, collections

path = '/app/tshock/PermabuffsConfig.json'
cfg = json.load(open(path, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)

for g in cfg.get('buffgroups') or []:
    ids = g.get('buffIDs') or []
    was = list(ids)
    g['buffIDs'] = sorted(set(ids))
    tag = "已排序" if was != g['buffIDs'] else "原已升序"
    print("  %-10s %-9s n=%3d  %s" % (g['groupName'], tag, len(g['buffIDs']), g['buffIDs']))

bl = cfg.get('blacklist') or []
cfg['blacklist'] = sorted(set(bl))
print("  blacklist  %s" % cfg['blacklist'])

json.dump(cfg, open(path, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
print("已写回 %s" % path)
PY

/app/send.sh pbreload
