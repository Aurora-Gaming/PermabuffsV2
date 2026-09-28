#!/usr/bin/env bash
# 查 Permabuffs 数据库里每个玩家常驻了哪些 buff
set -uo pipefail

DB=/app/tshock/Permabuffs.sqlite

echo "=== Permabuffs 表内容 ==="
python3 - "$DB" <<'PY'
import sqlite3, sys
con = sqlite3.connect(sys.argv[1])
cur = con.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
print("tables:", [r[0] for r in cur.fetchall()])
cur.execute("SELECT UserID, ActiveBuffs FROM Permabuffs")
rows = cur.fetchall()
if not rows:
    print("(空)")
for uid, buffs in rows:
    lst = [b for b in (buffs or '').split(',') if b]
    print("  UserID=%s  ActiveBuffs=%r  -> %s" % (uid, buffs, lst))
con.close()
PY

echo
echo "=== TShock 用户表（组）==="
python3 - /app/tshock/tshock.sqlite <<'PY'
import sqlite3, sys
con = sqlite3.connect(sys.argv[1])
cur = con.cursor()
try:
    cur.execute("SELECT username, \"group\" FROM users")
    for u, g in cur.fetchall():
        print("  %-20s group=%s" % (u, g))
except Exception as e:
    print("  (读取失败: %s)" % e)
con.close()
PY
