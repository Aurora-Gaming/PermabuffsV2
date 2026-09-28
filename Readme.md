# PermabuffsV2

TShock（Terraria 1.4.5.x / TShock 6.2.x）插件：让增益效果（buff）常驻。
**本分支是基于上游的重写版**，修掉了原版若干会导致崩服、配置写坏、以及"取消后要等 5 分钟"的问题。

---

## 一、先说清楚一个绕不开的前提

**Terraria 多人模式下，客户端对自己的 buff 有绝对权威，服务端移不掉它。**

```csharp
// Terraria/Player.cs — AddBuff()
if (Main.netMode == 1 && Main.myPlayer != whoAmI)
{
    if (Main.pvpBuff[type]) NetMessage.SendData(55, -1, -1, null, whoAmI, type, time);
    return;          // ← 专服上对远程玩家直接返回
}
```

服务端**无法**用 vanilla API 给别的玩家加 buff，只能发包。所以 `TSPlayer.SetBuff` 只做
`SendData(55, ...)` 不是偷懒，是被引擎逼的。**施加**勉强能做，**移除**做不到。

### 撤销为什么做不到：五种通道全部实测无效

`BuffEngine.RevokeMode` 把所有可能的撤销路径都做成了可现场验证的策略
（`/pbdebug <1-5> <buffID>`），逐个在游戏里试，结果是**五种全部无效**：

| 策略 | 做法 | 结果 |
|---|---|---|
| 1 | 50 号包单播（`NetMessage.SendData(50, player.Index, ...)`） | ❌ 图标不变 |
| 2 | 50 号包先广播再单播（照抄 TShock `RestoreCharacter` 前两步） | ❌ |
| 3 | TShock 完整重同步链（4/42/16/50/76/45/39/50） | ❌ |
| 4 | 55 号包 `time=0` 定点撤销 | ❌ |
| 5 | 50 号包连发 5 次（防被并发同步覆盖） | ❌ |

服务端侧一切正常（`[PB-REVOKE] slotHit=True`，服务端槽位确实清掉了），
但客户端图标纹丝不动，直到自然过期。**"已施加、无法撤销"就是 Terraria 1.4.5 的现实。**

> 这五种策略保留在代码里，是为了避免后来者再走一遍死路；
> 它们同时也**不会**让情况变坏（多发几个包而已）。

---

## 二、实际方案：短时长滚动续期

既然移不掉，就不去移。改成：**发一个很短的时长，靠心跳不断续期。**

```
在名单里   → 每 heartbeatTicks(≈1.7s) 续一次 permabuffDuration(5s) → 永不掉
取消名单   → 没人续了                    → 最多 5 秒后自然消失
```

**取消延迟从 5 分钟降到 5 秒。** 这是协议限制下能做到的最好结果。

### 为什么不会影响玩家自己喝的药水

`Player.AddBuff_TryUpdatingExistingBuffTime` 对绝大多数 buff 是**只延长不缩短**：

```csharp
int num = BuffID.Sets.AddBuffTimeAdditivelyToCap[type];
if (num > 0)
{
    buffTime[i] += time;                      // ← 累加型，少数
    if (buffTime[i] > num) buffTime[i] = num;
}
else if (buffTime[i] < time)
{
    buffTime[i] = time;                       // ← 只延长，不缩短
}
```

所以 5 秒的短时长**压不短**一个还剩 5 分钟的药水：药水时长 > 5s，写不进去，什么都不发生。
取消常驻后，药水剩余时间原样继续走完。

**已实测确认**：常驻 buff 生效期间喝回复药水，时长仍是 5 分钟；常驻取消后药水正常倒计时；死亡后正常消失。

### 唯一的例外：累加型 buff

`AddBuffTimeAdditivelyToCap = Factory.CreateIntSet(0, 94, 600, 383, 43200)`
——只有 5 个 ID 走"累加"分支，反复加 5 秒会把玩家正常获得的时长顶到上限，
正好违反上面的约束。`BuffEngine.Apply` 直接挡掉它们：

- `94` ManaSickness（魔力不适）
- `383` Kite（风筝）
- `600` / `43200` 无命名常量

全是负面/状态类，不是会常驻的东西。

---

## 三、鱼腮（Gills / buff 4）为什么会把人淹死

**这不是插件的锅，是 vanilla 机制。**

```csharp
// Terraria/Player.cs — CheckDrowning()
if (gills) { flag = Main.getGoodWorld && !flag; }
```

`gills` **只会在 For the Worthy（受难）世界**下把"是否没入水"的判断反过来。
普通世界里鱼腮完全正常。

而溺水伤害本身是客户端专属逻辑：

```csharp
if (Main.myPlayer == whoAmI)   // ← 专服永远不会进这个分支
{
    statLife -= 2;
    if (breath <= 0) KillMe(...);
}
```

**服务端既不能造成也不能修复它。** 本插件能做的是不让鱼腮进名单：

- 配置默认 `blacklist: [4]`
- 名单里、以及所有分组授予和手动激活，都会跳过黑名单 ID
- 运行时用 `BuffID.Count` 校验 ID 合法性，挡住 `0` 和越界值

> 注意：上游 `probuffs` 分组的原始列表里**本来就含 4**。也就是说黑名单是唯一挡住鱼腮的防线。
> 改动 `blacklist` 配置前请三思。

---

## 四、命令与权限

**所有 buff 一律用纯数字 ID，不接受名字。** 需要按名字用时先 `/pbid <名字>` 换算。
之所以不直接支持名字：`TShock.Utils.GetBuffByName` 对同一个词经常返回多个匹配
（例如 `Regeneration` 会同时命中多条），玩家只看到一串候选、无从判断该选哪个。
纯数字零歧义。`Config.allowBuffNames = true` 可恢复按名字模糊查（默认关闭）。

| 命令 | 权限 | 说明 |
|---|---|---|
| `/pbid <名字>` | `pb.use` | 名字 → 数字 ID 换算 |
| `/permabuff <ID>` | `pb.use` | 给自己常驻；**再次输入同一命令即取消**（≤5 秒生效） |
| `/buffcheck <玩家>` | `pb.check` | 查看某人的常驻 buff |
| `/gpermabuff <ID> <玩家>` | `pb.give` | 给别人常驻 |
| `/gpermabuff -g list` | `pb.give` | 列出可用分组 |
| `/gpermabuff -g <组> <玩家>` | `pb.give` | 整组授予（自动跳过黑名单与宠物类） |
| `/globalbuff <ID>` | `pb.global` | 开关全服 buff |
| `/pbreload` | `pb.reload` | 重载配置（无需重启） |
| `/clearbuffs` | `pb.use` | 清除**自己**的常驻 buff |
| `/clearbuffs all` | `pb.clear` | 清除**全服**：所有玩家的个人常驻 + 全部全局 buff |
| `/pbdebug <1-5> <ID>` | `pb.use` | 撤销通道排查，见第一节 |
| `/regionbuff <add/del> <区域> <ID> [秒]` | `pb.region` | 区域 buff（**未验证**，见第八节） |

分组权限：`pb.<groupPerm>`（默认 `pb.probuffs` / `pb.petbuffs` / `pb.debuffs` / `pb.111`），
`pb.useall` 绕过全部分组限制。批量授权给 `pb.*` 即可覆盖全部分组。

### 「取消了但没消失」不是 bug

常驻 buff 有三个来源：**个人库**、**全局开关**、**区域**。只有个人库会被 `/permabuff` 取消。

| 命令 | 动个人库 | 动全局 | 动区域 |
|---|:---:|:---:|:---:|
| `/permabuff <ID>` | ✅ | ❌ | ❌ |
| `/globalbuff <ID>` | ❌ | ✅ | ❌ |

要真正停掉，必须回到产生它的那个来源去取消。插件会主动检测这种冲突并提示
是谁在补，否则玩家只会看到"我明明取消了，buff 却还在"。

---

## 五、配置

```jsonc
{
  "blacklist": [4],           // 黑名单：所有路径都跳过
  "permabuffDuration": 300,   // 滚动时长 tick。60 tick = 1 秒；300 = 5 秒 = 取消延迟
  "heartbeatTicks": 100,      // 续期间隔 tick。会自动收紧到 ≤ 时长/3，防图标闪烁
  "allowBuffNames": false,    // 是否允许按名字指定 buff
  "autoGrantOnLogin": false,  // 登录时自动授予全部分组
  "autoGrantGroups": [],
  "diagnostics": false,       // 打印心跳/撤销诊断
  "forceClientResync": true,
  "buffgroups": [ /* 见下 */ ],
  "regionbuffs": [ /* 未验证 */ ]
}
```

调整取消延迟就改 `permabuffDuration`：**取消延迟 = 值 ÷ 60 秒**。
想要 15 秒就填 900。改小它但忘了调心跳也没关系，`Normalize()` 会自动收紧并打警告。

### 内置分组

| 分组 | 权限 | 数量 | 说明 |
|---|---|---|---|
| `probuffs` | `pb.probuffs` | 90 | 上游原有增益组（**含 4 号鱼腮，靠黑名单挡**） |
| `petbuffs` | `pb.petbuffs` | 125 | 宠物/坐骑类，`BuffHasTimer` 为假的会被跳过 |
| `debuffs` | `pb.debuffs` | 31 | 减益组 |
| `111` | `pb.111` | 13 | **本版新增**：常用增益精选，ID 升序，全是增益不含减益 |

`111` 组内容：`2 3 5 8 9 11 12 26 111 113 114 115 117`
（Regeneration / Swiftness / Ironskin / Featherfall / Spelunker / Shine /
Night Owl / Well Fed / Dangersense / Lifeforce / Endurance / Rage / Wrath）

### 固化用法

```jsonc
"autoGrantOnLogin": true,
"autoGrantGroups": ["111"]
```

玩家一登录就自动获得该组内其有权限的全部 buff，无需手动 `/permabuff`。

---

## 六、部署

1. 编译 `PermabuffsV2.csproj`（Release，net9.0）
2. 把 `PermabuffsV2.dll` 放进服务器的 `ServerPlugins/`
3. 启动后 `PermabuffsConfig.json` 会自动生成，改完用 `/pbreload` 热重载

容器化 / 远程控制台 / 数据库排查等脚本见 [`deploy/`](deploy/README.md)。

### 开发

```bash
dotnet build PermabuffsV2/PermabuffsV2.csproj -c Release
dotnet test  tests/PermabuffsV2.Tests/PermabuffsV2.Tests.csproj
```

---

## 七、本版改动摘要

**新增**

- `BuffEngine.cs`：施加 / 续期 / 撤销的统一入口
- `BuffEngine.RevokeMode`：五种撤销策略（实测全部无效，留作记录）
- `/pbreload`、`/pbid`、`/pbdebug` 命令（原 Readme 宣传过但从未注册）
- `111` 常用增益分组
- 48 项单元测试

**修复**

- `System.Timers.Timer` 跑在线程池线程上 → 并发访问集合直接崩服。改用 `ServerApi.Hooks.GameUpdate`（主线程）
- `DB.PlayerBuffs` 用 `Dictionary.Add` 索引已存在的键 → `ArgumentException`
- `PlayerBuffs` 改用 `HashSet<int>`（原 `List<int>`）
- `DBInfo` 对 null / 空白不崩，`PlayerBuffs` 属性加私有 setter
- `RegionBuff` / `BuffGroup` 补字段初始化，消除反序列化后的 `NullReferenceException`
- `/clearbuffs all` 原本先 `Clear()` 再把同一个空 list 传给撤销，**实际一个 buff 都没撤销**；且完全没碰 `GlobalBuffs`
- TShock `config.json` 的键必须写在嵌套的 `Settings` 对象里，写在根层级不会被读取
- 纯数字 ID + `/pbid` 换算，取代有歧义的名字匹配
- 取消冲突检测：`/permabuff` 取消后若还有全局/区域来源在提供，立刻提示来源

---

## 八、已知限制

- **撤销有 ≤5 秒延迟**，不是即时的。这是协议限制（第一节），不是实现缺陷。
- **`regionbuff` 与出生/初始化相关逻辑保持原样，既未改动也未验证。**
  当前只对**个人常驻**（`/permabuff`）和**全局 buff**（`/globalbuff`）做过端到端验证。
- 累加型 buff（`0/94/383/600/43200`）无法常驻，会被 `Apply` 跳过。
- `Main.netMode` 在本插件的诊断输出里显示为 `2`（专服应为 `1`），
  成因未查清，暂不影响功能。
- 鱼腮 drowning 属客户端逻辑，服务端插件无从干预。
