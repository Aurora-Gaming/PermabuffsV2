# deploy/

服务器运维与排查脚本。**都不是插件运行所必需的**，删掉不影响插件工作。

约定：所有脚本在容器内的 `/app` 下运行（TShock 服务器根目录），
用 `docker cp deploy/<脚本> debian:/app/ && docker exec debian bash -lc "chmod +x /app/<脚本> && /app/<脚本>"` 部署。

## 启动

| 脚本 | 作用 |
|---|---|
| `start.sh` | 前台启动 TShock.Server（缺 TerrariaServer.dll 时自动跑安装器） |
| `start-bg.sh` | 后台启动，并建一个 `/app/console.in` FIFO 供远程发指令 |
| `newworld.sh` | 交互式向导自动化，创建测试世界 |
| `setport.sh <端口>` | 改 `tshock/config.json` 的 `Settings.ServerPort` 并放开免登录直连 |
| `send.sh <指令...>` | 往后台服务器的 FIFO 写指令，每个间隔 1.2 秒 |

## 配置与分组

| 脚本 | 作用 |
|---|---|
| `setcfg.sh 'key=json值' ...` | 改 `PermabuffsConfig.json` 并自动 `/pbreload`。例：`./setcfg.sh 'permabuffDuration=900' 'diagnostics=true'` |
| `addgroup.sh <组名> [权限名] <ID...>` | 新增/覆盖一个 buff 分组并热重载。例：`./addgroup.sh 111 111 2 3 5 8` |
| `sortgroups.sh` | 把所有分组的 buff ID 去重后按升序排列，并规范黑名单 |
| `smoke.sh` | 命令路径冒烟测试（无玩家在线时的返回值自检） |

## 排查

> 先用 `setcfg.sh 'diagnostics=true'` 打开诊断，输出在 `tshock/logs/*.log`。

| 脚本 | 作用 |
|---|---|
| `overview.sh` | 配置分组概览 + 数据库 + 用户组，一步看全 |
| `dumpdb.sh` | 只打印 `Permabuffs` 表的常驻 buff 和 TShock 用户组 |
| `peek.sh` | 拉取最新日志里的 `PB-DIAG` 诊断行和玩家活动 |
| `timeline.sh` | 把 `PB-DIAG` 压成**状态跃迁时间轴**，命令插进时间线，排查"buff 什么时候出现/消失"最直观 |

## 踩过的坑

改 `tshock/config.json` 时注意：**这些键必须写在嵌套的 `Settings` 对象里**。

```jsonc
{ "Settings": { "DisableLoginBeforeJoin": true },   // ← 生效
  "DisableLoginBeforeJoin": true }                  // ← TShock 根本不读
```

同一个键写两遍不会有任何提示，只是静默不生效。

另一个坑：TShock 的权限通配符**只替换尾部段**。

```
查询 "pb.use" → 依次尝试 "pb.use" → "pb.*" → "*.*"
```

它永远不会去试 `tshook.admin`——那比权限本身更短，不是尾部通配。
所以本插件的 `pb.*` 权限必须**单独授予**，给了 `tshook.admin` 也没用。
