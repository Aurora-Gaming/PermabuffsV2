#!/usr/bin/env bash
# 向 PermabuffsConfig.json 追加/更新一个 buff 分组，然后 /pbreload。
# 用法: ./addgroup.sh <组名> <权限名> <id1> <id2> ...
# 权限名留空则与组名相同（即 pb.<权限名>）
set -euo pipefail

NAME="$1"; PERM="${2:-$1}"; shift 2
IDS=("$@")

python3 - "$NAME" "$PERM" "${IDS[@]}" <<'PY'
import json, sys, collections

name, perm = sys.argv[1], sys.argv[2]
ids = [int(x) for x in sys.argv[3:]]

path = '/app/tshock/PermabuffsConfig.json'
cfg = json.load(open(path, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
cfg.setdefault('blacklist', [4])

groups = cfg.get('buffgroups') or []
# 同名分组则替换，否则追加
for g in groups:
    if g.get('groupName') == name:
        g['groupPerm'] = perm
        g['buffIDs'] = ids
        break
else:
    groups.append(collections.OrderedDict(
        [('groupName', name), ('groupPerm', perm), ('buffIDs', ids)]))

cfg['buffgroups'] = groups
json.dump(cfg, open(path, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)

print("已写入分组 %s (perm=pb.%s): %s" % (name, perm, ids))
print("当前全部分组: %s" % ", ".join(g['groupName'] for g in groups))
PY

/app/send.sh pbreload
