#!/usr/bin/env bash
# 调整 TShock 监听端口到容器已映射的 5000-5100 区间内的一个口，
# 并关掉首次运行的交互向导（口令/UUID 登录），方便客户端直连测试。
set -uo pipefail

PORT="${1:-5099}"
CFG=/app/tshock/config.json

python3 - "$CFG" "$PORT" <<'PY'
import json, sys
path, port = sys.argv[1], int(sys.argv[2])
with open(path, encoding="utf-8") as f:
    cfg = json.load(f)
cfg["Port"] = port
cfg["MaxPlayers"] = 16
cfg["ServerPassword"] = ""
cfg["RegistrationEnabled"] = True
# 让客户端能直接进来做 buff 实验，不被口令/UUID 拦住
cfg["DisableUUIDLogin"] = True
cfg["DisableLoginBeforeJoin"] = True
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2, ensure_ascii=False)
print("config.json: Port=%d, 无口令, UUID/Login 拦截已关闭" % port)
PY
