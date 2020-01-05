# PermabuffsV2
Updated version of Permabuffs plugin

## Overview
### Features:
- When active, any buff is auto-renewed until the command is turned off.
- This includes player buffs being saved even if the server is shut down.
- Definable buff groups with specific permissions.
- For staff, there is a command to view which permabuffs players have active.
- There are also global buffs, which affect anyone on the server at the time.
- Region buffs can buff everyone in a certain region with a buff that lasts a specified duration once they leave the region.

### Commands:
- /permabuff <buff name or id>: Activates the specified buff.
- /buffcheck <player>: Gives a list of permabuffs that the specified player has active.
- /gpermabuff <buff name or id> <player>: Activates the specified buff for the specified player.
- /gpermabuff -g <"list"/buff group name>
- /regionbuff <add/del> <region name> <buff name or ID> [duration in seconds]
- /globalbuff <buff name or id>: Activates or deactivates a global buff.
- /clearbuffs [all]: Deactivates all active buffs for the player (or all players if "all" is used as a parameter.
- /pbreload: Reloads the config file.

### Config:
- buffgroups:
- groupName: The name of the buff group.
- groupPerm: The permission needed to use permabuffs in this group. (Note that the permission is "pb." + the groupPerm.)
- isperma: If set to false, the buff will still be given, but will not be auto-renewed (useful for "pet" buffs and for use to replace /buff, if players shouldn't have access to every buff).
- buffIDs: The list of buff IDs to include in this group.​
- regionbuffs:
- regionName: The name of the region to apply the buffs in.
- buffs: A list of pairs of buff IDs and duration (in seconds).​

### Future:
- Add offline saving for global buffs, and the ability to disable all global buffs

## Permissions: 
- pb.use: Allows a player to use /permabuff and /clearbuffs (on themselves)
- pb.check: Allows a player to use /buffcheck.
- pb.give: Allows a player to use /gpermabuff.
- pb.clear: Allows a player to deactivate all active buffs.
- pb.region: Allows a player to add/delete region buffs.
- pb.global: Allows a player to set global buffs.
- pb.reload: Allows a player to reload the config file.

Players can only use /permabuff on buffs in groups that they have permission to access. To allow a player to permabuff themselves with buffs in a certain group, use pb.<groupPerm>. To allow a player to permabuff themselves with buffs from any group, use pb.useall.

Ex: pb.probuffs, pb.petbuffs, pb.debuffs are the necessary permissions to use buffs in the default groups. 

## Installation Guide:
1. Place Plugin into ServerPlugins folder.
2. Restart server.
3. Open the PermabuffsConfig.json file and make any desired changes.
4. Use /pbreload. 

Source: [Permabuffs | TShock for Terraria](https://tshock.co/xf/index.php?resources/permabuffs.5/)
