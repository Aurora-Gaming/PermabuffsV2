#!/usr/bin/env bash
# 打印 buffs 状态跃迁点，并在其后紧跟一行"插件认为的来源"
set -uo pipefail

L=$(ls -t /app/tshock/logs/*.log | head -1)
python3 - "$L" <<'PY'
import re, sys

TS = r'(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)'
DIAG = re.compile(TS + r'.*?\[PB-DIAG\] \[(.*?)\] (buffs=\[[^]]*\])')
SRC = re.compile(r'acct=.*')
CMD = re.compile(TS + r'.*?(?:(\S+) executed:? .*|(\S+) has joined|(\S+) has left)')

prev = None
pending_src = None

for line in open(sys.argv[1], encoding='utf-8', errors='replace'):
    m = DIAG.search(line)
    if m:
        ts, who, state = m.groups()
        s = SRC.search(line)
        pending_src = "      " + s.group(0) if s else ""
        if state != prev:
            print("%s -> %-8s %s" % (ts, who, state))
            if pending_src:
                print(pending_src)
            prev = state
        continue

    if pending_src:
        print(pending_src)
        pending_src = None

    m = CMD.search(line)
    if m:
        ts = m.group(1)
        who = m.group(2) or m.group(3) or m.group(4) or "Server"
        print("%s    %s   %s" % (ts, "»", line.split('INFO: ')[-1].strip()))
PY
