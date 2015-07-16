using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace Permabuffs_V2
{
    [ApiVersion(1,19)]
    public class Permabuffs : TerrariaPlugin
    {
        public override string Name { get { return "Permabuffs"; } }
        public override string Author { get { return "Zaicon"; } }
        public override string Description { get { return "A plugin for permabuffs."; } }
        public override Version Version { get { return new Version(4, 0, 2, 0); } }

        private static IDbConnection db;

        private static Timer update;
        private static Dictionary<int, DBInfo> permas = new Dictionary<int, DBInfo>();
        private static List<int> globalbuffs = new List<int>();
        private static Dictionary<int, List<int>> regionbuffs = new Dictionary<int, List<int>>();
        private static Dictionary<int, List<string>> hasAnnounced = new Dictionary<int, List<string>>();

        public static string configPath = Path.Combine(TShock.SavePath, "PermabuffsConfig.json");
        public static Config config = Config.Read(configPath);// = new Config();

        public Permabuffs(Main game)
            : base(game)
        {
            base.Order = 1;
        }

        #region Initalize/Dispose
        public override void Initialize()
        {
            DBConnect();

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            AccountHooks.AccountDelete += OnAccDelete;
            PlayerHooks.PlayerPostLogin += OnPostLogin;
            RegionHooks.RegionEntered += OnRegionEnter;
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                AccountHooks.AccountDelete -= OnAccDelete;
                RegionHooks.RegionEntered -= OnRegionEnter;
                PlayerHooks.PlayerPostLogin -= OnPostLogin;
            }
            base.Dispose(Disposing);
        }
        #endregion

        #region Hooks
        public void OnInitialize(EventArgs args)
        {
            config.Write(configPath);

            update = new Timer { Interval = 1000, AutoReset = true, Enabled = true };
            update.Elapsed += OnElapsed;

            Commands.ChatCommands.Add(new Command("pb.use", PBuffs, "permabuff") { AllowServer = false, HelpText = "Buffs yourself with a buff permanently." });
            Commands.ChatCommands.Add(new Command("pb.check", PBCheck, "buffcheck") { HelpText = "Lists the active permabuffs of the specified player." });
            Commands.ChatCommands.Add(new Command("pb.give", PBGive, "gpermabuff") { HelpText = "Gives a player the specified permabuff." });
            Commands.ChatCommands.Add(new Command("pb.reload", PBReload, "pbreload"));
            Commands.ChatCommands.Add(new Command("pb.region", PBRegion, "regionbuff"));
            Commands.ChatCommands.Add(new Command("pb.global", PBGlobal, "globalbuff"));
            Commands.ChatCommands.Add(new Command("pb.use", PBClear, "clearbuffs") { HelpText = "Removes all permabuffs." });
        }

        public void OnGreet(GreetPlayerEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
                return;

            if (globalbuffs.Count > 0)
                TShock.Players[args.Who].SendInfoMessage("This server has the following global permabuffs active: {0}", string.Join(", ", globalbuffs.Select(p => Main.buffName[p])));

            if (!hasAnnounced.ContainsKey(args.Who))
                hasAnnounced.Add(args.Who, new List<string>());

            if (!TShock.Players[args.Who].IsLoggedIn)
                return;

            if (!permas.ContainsKey(args.Who))
            {
                if (loadDBInfo(args.Who))
                {
                    if (permas[args.Who].bufflist.Count > 0)
                        TShock.Players[args.Who].SendInfoMessage("Your permabuffs from your previous session ({0}) are still active!", string.Join(", ", permas[args.Who].bufflist.Select(p => TShock.Utils.GetBuffName(p))));
                }
                else
                    createDBInfo(args.Who);
            }
            else
            {
                //loadDBInfo(args.Who);
                if (permas[args.Who].bufflist.Count > 0)
                    TShock.Players[args.Who].SendInfoMessage("Your permabuffs from your previous session ({0}) are still active!", string.Join(", ", permas[args.Who].bufflist.Select(p => TShock.Utils.GetBuffName(p))));
            }            
        }
        
        public void OnPostLogin(PlayerPostLoginEventArgs args)
        {
            if (!permas.ContainsKey(args.Player.Index))
            {
                if (loadDBInfo(args.Player.Index))
                {
                    if (permas[args.Player.Index].bufflist.Count > 0)
                        args.Player.SendInfoMessage("Your permabuffs from your previous session ({0}) are still active!", string.Join(", ", permas[args.Player.Index].bufflist.Select(p => TShock.Utils.GetBuffName(p))));
                }
                else
                    createDBInfo(args.Player.Index);
            }
            else
            {
                permas.Remove(args.Player.Index);
                loadDBInfo(args.Player.Index);
                if (permas[args.Player.Index].bufflist.Count > 0)
                    args.Player.SendInfoMessage("Your permabuffs from your previous session ({0}) are still active!", string.Join(", ", permas[args.Player.Index].bufflist.Select(p => TShock.Utils.GetBuffName(p))));
            }
        }

        public void OnAccDelete(AccountDeleteEventArgs args)
        {
            db.Query("DELETE FROM Permabuffs WHERE UserID=@0;", args.User.ID);
        }

        public void OnRegionEnter(RegionHooks.RegionEnteredEventArgs args)
        {
            KeyValuePair<string, List<int>> regionkvp;

            regionkvp = new KeyValuePair<string, List<int>>("null", new List<int>() { 0 });

            foreach (KeyValuePair<string, List<int>> kvp in config.regionbuffs)
            {
                if (kvp.Key == args.Region.Name && kvp.Value.Count > 0)
                {
                    regionkvp = kvp;
                }
            }

            if (regionkvp.Key != "null" && !hasAnnounced[args.Player.Index].Contains(args.Region.Name))
            {
                args.Player.SendSuccessMessage("You have entered a region with the following buffs enabled: {0}", string.Join(", ", regionkvp.Value.Select(p => Main.buffName[p])));
                hasAnnounced[args.Player.Index].Add(args.Region.Name);
            }
        }

        public void OnLeave(LeaveEventArgs args)
        {
            if (permas.ContainsKey(args.Who))
                permas.Remove(args.Who);

            if (hasAnnounced.Keys.Contains(args.Who))
                hasAnnounced.Remove(args.Who);
        }

        private void OnElapsed(object sender, ElapsedEventArgs args)
        {
            for (int i = 0; i < TShock.Players.Length; i++)
            {
                if (TShock.Players[i] == null)
                    continue;

                if (permas[i].bufflist.Count > 0)
                {
                    for (int k = 0; k < permas[i].bufflist.Count; k++)
                    {
                        TShock.Players[i].SetBuff(permas[i].bufflist[k], 18000);
                    }

                }

                foreach (int buff in globalbuffs)
                {
                    TShock.Players[i].SetBuff(buff, 18000);
                }

                if (TShock.Players[i].CurrentRegion == null)
                    continue;

                foreach (KeyValuePair<string, List<int>> kvp in config.regionbuffs)
                {
                    if (kvp.Key == null || kvp.Key == "null" || kvp.Value.Count == 0)
                        continue;

                    if (TShock.Players[i].CurrentRegion.Name == kvp.Key)
                    {
                        foreach (int buff in kvp.Value)
                        {
                            TShock.Players[i].SetBuff(buff, 1000);
                        }
                    }
                }
            }
        }
        #endregion

        #region Buff Commands
        private void PBuffs(CommandArgs args)
        {
            int bufftype = -1;

            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid syntax: {0}permabuff <buff name or ID>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }
            else
            {
                string buff = string.Join(" ", args.Parameters);

                bool tryparse = Int32.TryParse(args.Parameters[0], out bufftype);

                if (!tryparse)
                {
                    List<int> bufftypelist = new List<int>();
                    bufftypelist = TShock.Utils.GetBuffByName(buff);

                    if (bufftypelist.Count < 1)
                    {
                        args.Player.SendErrorMessage("No buffs by that name were found.");
                        return;
                    }
                    else if (bufftypelist.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, bufftypelist.Select(p => TShock.Utils.GetBuffName(p)));
                        return;
                    }
                    else
                        bufftype = bufftypelist[0];

                }
            }

            int playerid = args.Player.Index;
            
            if (permas[playerid].bufflist.Contains(bufftype))
            {
                permas[playerid].bufflist.Remove(bufftype);
                updateBuffs(args.Player.User.ID, permas[playerid].bufflist);
                args.Player.SendInfoMessage("You have removed the " + Main.buffName[bufftype] + " permabuff.");
                return;
            }
            else
            {
                if (config.buffgroups.Length == 0)
                {
                    args.Player.SendErrorMessage("Your server administrator has not defined any buff groups. Please contact an admin to fix this issue.");
                    return;
                }

                string buffperm = null;
                bool isperma = false;
                bool exists = findBuffInConfig(bufftype, out buffperm, out isperma);

                if (!exists)
                {
                    args.Player.SendErrorMessage("This buff is not available as a permabuff.");
                    return;
                }

                string perm = "pb." + buffperm;

                if (!args.Player.Group.HasPermission(perm) && !args.Player.Group.HasPermission("pb.useall"))
                {
                    args.Player.SendErrorMessage("You do not have permission to buff yourself with this buff!");
                    return;
                }

                if (bufftype > Main.maxBuffTypes || bufftype < 1) // Buff ID is not valid (less than 1 or higher than 190 (1.3.0.5)).
                    args.Player.SendErrorMessage("Invalid buff ID!");

                if (isperma)
                {
                    permas[playerid].bufflist.Add(bufftype);
                    updateBuffs(args.Player.User.ID, permas[playerid].bufflist);
                    args.Player.SendSuccessMessage("You have permabuffed yourself with the {0} buff! Re-type this command to disable the buff.", Main.buffName[bufftype]);
                }
                else
                {
                    args.Player.SetBuff(bufftype);
                    args.Player.SendSuccessMessage("You have given yourself the {0} buff.", Main.buffName[bufftype]);
                }
            }
        }

        private void PBCheck(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid syntax: {0}buffcheck <player>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }

            string playername = string.Join(" ", args.Parameters);

            List<TSPlayer> players = TShock.Utils.FindPlayer(playername);

            if (players.Count < 1)
                args.Player.SendErrorMessage("No players found.");
            else if (players.Count > 1)
                TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
            else
            {
                if (permas[players[0].Index].bufflist.Count == 0)
                    args.Player.SendInfoMessage("{0} has no permabuffs active.", players[0].Name);
                else
                    args.Player.SendInfoMessage("{0} has the following permabuffs active: {1}", players[0].Name, string.Join(", ", permas[players[0].Index].bufflist.Select(p => TShock.Utils.GetBuffName(p))));
            }
        }

        private void PBGive(CommandArgs args)
        {
            int bufftype = -1;

            if (args.Parameters.Count == 2)
            {
                if (args.Parameters[0] == "-g" && args.Parameters[1] == "list")
                {
                    IEnumerable<string> bufflist;

                    if (args.Player.Group.HasPermission("pb.useall"))
                        bufflist = (from buffgroups in config.buffgroups select buffgroups.groupName);
                    else
                        bufflist = (from buffgroups in config.buffgroups where args.Player.Group.HasPermission("pb." + buffgroups.groupPerm) select buffgroups.groupName);

                    args.Player.SendInfoMessage("Available buff groups: {0}", string.Join(", ", bufflist));

                    return;
                }

                string playername = args.Parameters[1];

                List<TSPlayer> players = TShock.Utils.FindPlayer(playername);

                if (players.Count < 1)
                {
                    args.Player.SendErrorMessage("No players found.");
                    return;
                }
                else if (players.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
                    return;
                }

                string buff = args.Parameters[0];

                bufftype = -1;

                bool tryparse = Int32.TryParse(args.Parameters[0], out bufftype);

                if (!tryparse)
                {
                    List<int> bufftypelist = new List<int>();
                    bufftypelist = TShock.Utils.GetBuffByName(buff);

                    if (bufftypelist.Count < 1)
                    {
                        args.Player.SendErrorMessage("No buffs by that name were found.");
                        return;
                    }
                    else if (bufftypelist.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, bufftypelist.Select(p => TShock.Utils.GetBuffName(p)));
                        return;
                    }
                    else
                        bufftype = bufftypelist[0];
                }

                int playerid = players[0].Index;

                if (permas[playerid].bufflist.Contains(bufftype))
                {
                    permas[playerid].bufflist.Remove(bufftype);
                    updateBuffs(args.Player.User.ID, permas[playerid].bufflist);
                    args.Player.SendInfoMessage("You have removed the {0} permabuff for {1}.", Main.buffName[bufftype], players[0].Name);
                    if (!args.Silent)
                        players[0].SendInfoMessage("{0} has removed your {1} permabuff.", args.Player.Name, Main.buffName[bufftype]);
                }
                else
                {
                    if (config.buffgroups.Length == 0)
                    {
                        args.Player.SendErrorMessage("Your server administrator has not defined any buff groups. Please contact an admin to fix this issue.");
                        return;
                    }

                    string buffgroup = null;
                    bool isperma = false;
                    bool exists = findBuffInConfig(bufftype, out buffgroup, out isperma);

                    if (!exists)
                    {
                        args.Player.SendErrorMessage("This buff is not available as a permabuff.");
                        return;
                    }

                    string perm = "pb." + buffgroup;

                    if (bufftype > Main.maxBuffTypes || bufftype < 1) // Buff ID is not valid (less than 1 or higher than 190 (1.3.0.5)).
                        args.Player.SendErrorMessage("Invalid buff ID!");

                    if (isperma)
                    {
                        permas[playerid].bufflist.Add(bufftype);
                        updateBuffs(args.Player.User.ID, permas[playerid].bufflist);
                        args.Player.SendSuccessMessage("You have permabuffed {0} with the {1} buff!", players[0].Name, Main.buffName[bufftype]);
                        if (!args.Silent)
                            players[0].SendInfoMessage("{0} has permabuffed you with the {1} buff!", args.Player.Name, Main.buffName[bufftype]);
                    }
                    else
                    {
                        args.Player.SetBuff(bufftype);
                        args.Player.SendSuccessMessage("You have given {0} the {1} buff!", players[0].Name, Main.buffName[bufftype]);
                        if (!args.Silent)
                            players[0].SendInfoMessage("{0} has given you the {1} buff!", args.Player.Name, Main.buffName[bufftype]);
                    }
                }
            }
            else if (args.Parameters.Count == 3)
            {
                var plist = TShock.Utils.FindPlayer(args.Parameters[2]);

                if (args.Parameters[0] == "-g" && config.buffgroups.Count(p => p.groupName == args.Parameters[1]) > 0 && plist.Count == 1)
                {
                    var buffgroups = (from buffs in config.buffgroups where buffs.groupName == args.Parameters[1] select buffs.buffIDs);

                    foreach (int buff in buffgroups.FirstOrDefault())
                    {
                        TSPlayer player = plist[0];

                        if (!permas[plist[0].Index].bufflist.Contains(buff))
                        {
                            permas[plist[0].Index].bufflist.Add(buff);
                        }
                    }

                    updateBuffs(plist[0].User.ID, permas[plist[0].Index].bufflist);

                    args.Player.SendSuccessMessage("Successfully permabuffed {0} with all of the buffs in the group {1}!", plist[0].Name, args.Parameters[1]);

                    if (!args.Silent)
                        args.Player.SendInfoMessage("{0} has permabuffed you with all of the buffs in the group {1}!", args.Player.Name, args.Parameters[1]);
                }
                else if (args.Parameters[0] != "-g")
                {
                    args.Player.SendErrorMessage("Invalid syntax:");
                    args.Player.SendErrorMessage("{0}gpermabuff <buff name or ID> <player>", TShock.Config.CommandSpecifier);
                    args.Player.SendErrorMessage("{0}gpermabuff -g <buff group> <player>", TShock.Config.CommandSpecifier);
                }
                else if (config.buffgroups.Count(p => p.groupName == args.Parameters[1]) == 0)
                {
                    args.Player.SendErrorMessage("No buffgroups match your query!");
                }
                else if (plist.Count == 0)
                {
                    args.Player.SendErrorMessage("No players matched!");
                }
                else
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, TShock.Utils.FindPlayer(args.Parameters[2]));
                }
            }
            else
            {
                args.Player.SendErrorMessage("Invalid syntax:");
                args.Player.SendErrorMessage("{0}gpermabuff <buff name or ID> <player>", TShock.Config.CommandSpecifier);
                args.Player.SendErrorMessage("{0}gpermabuff -g <buff group> <player>", TShock.Config.CommandSpecifier);
                return;
            }
        }

        private void PBReload(CommandArgs args)
        {
            args.Player.SendSuccessMessage("Permabuff config reloaded!");
            loadConfig();
        }

        private void PBRegion(CommandArgs args)
        {
            //regionbuff <add/del> <region> <buff>

            if (args.Parameters.Count != 3)
            {
                args.Player.SendErrorMessage("Invalid Syntax: {0}regionbuff <add/del> <region name> <buff name/ID>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }

            if (args.Parameters[0] == "add")
            {
                string regionname = args.Parameters[1];
                Region region = TShock.Regions.GetRegionByName(regionname);
                string buffinput = args.Parameters[2];
                int bufftype = -1;

                if (region == null)
                {
                    args.Player.SendErrorMessage("Invalid region: {0}", regionname);
                    return;
                }

                if (!int.TryParse(buffinput, out bufftype))
                {
                    List<int> bufflist = TShock.Utils.GetBuffByName(buffinput);

                    if (bufflist.Count == 0)
                    {
                        args.Player.SendErrorMessage("No buffs found by the name {0}.", buffinput);
                        return;
                    }

                    if (bufflist.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, bufflist.Select(p => Main.buffName[p]));
                        return;
                    }

                    bufftype = bufflist[0];
                }

                if (bufftype < 0 || bufftype > Main.maxBuffTypes)
                {
                    args.Player.SendErrorMessage("Invalid buff ID: {0}", bufftype.ToString());
                    return;
                }

                if (config.regionbuffs.ContainsKey(region.Name))
                {
                    if (config.regionbuffs[region.Name].Contains(bufftype))
                    {
                        args.Player.SendErrorMessage("Region {0} already contains buff {1}!", region.Name, Main.buffName[bufftype]);
                        return;
                    }
                    else
                    {
                        config.regionbuffs[region.Name].Add(bufftype);
                        args.Player.SendSuccessMessage("Added buff {0} to region {1}!", Main.buffName[bufftype], region.Name);
                        config.Write(configPath);
                        return;
                    }
                }
                else
                {
                    config.regionbuffs.Add(region.Name, new List<int>() { bufftype });
                    args.Player.SendSuccessMessage("Added buff {0} to region {1}!", Main.buffName[bufftype], region.Name);
                    config.Write(configPath);
                    return;
                }
            }

            if (args.Parameters[0] == "del")
            {
                string regionname = args.Parameters[1];
                Region region = TShock.Regions.GetRegionByName(regionname);
                string buffinput = args.Parameters[2];
                int bufftype = -1;

                if (region == null)
                {
                    args.Player.SendErrorMessage("Invalid region: {0}", regionname);
                    return;
                }

                if (!int.TryParse(buffinput, out bufftype))
                {
                    List<int> bufflist = TShock.Utils.GetBuffByName(buffinput);

                    if (bufflist.Count == 0)
                    {
                        args.Player.SendErrorMessage("No buffs found by the name {0}.", buffinput);
                        return;
                    }

                    if (bufflist.Count > 1)
                    {
                        TShock.Utils.SendMultipleMatchError(args.Player, bufflist.Select(p => Main.buffName[p]));
                        return;
                    }

                    bufftype = bufflist[0];
                }

                if (bufftype < 0 || bufftype > Main.maxBuffTypes)
                {
                    args.Player.SendErrorMessage("Invalid buff ID: {0}", bufftype.ToString());
                    return;
                }

                if (config.regionbuffs.ContainsKey(region.Name))
                {
                    if (config.regionbuffs[region.Name].Contains(bufftype))
                    {
                        config.regionbuffs[region.Name].Remove(bufftype);
                        args.Player.SendSuccessMessage("Removed buff {0} from region {1}!", Main.buffName[bufftype], region.Name);

                        if (config.regionbuffs[region.Name].Count == 0)
                            config.regionbuffs.Remove(region.Name);

                        config.Write(configPath);
                        return;
                    }
                    else
                    {
                        args.Player.SendSuccessMessage("Buff {0} is not a region buff in region {1}!", Main.buffName[bufftype], region.Name);
                        return;
                    }
                }
                else
                {
                    args.Player.SendSuccessMessage("Buff {0} is not a region buff in region {1}!", Main.buffName[bufftype], region.Name);
                    return;
                }
            }

            args.Player.SendErrorMessage("Invalid syntax: {0}regionbuff <add/del> <region name> <buff name/ID>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
        }

        private void PBGlobal(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid Syntax: {0}globalbuff <buff name>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                return;
            }

            string buff = string.Join(" ", args.Parameters);

            int bufftype = -1;
            bool tryparse = Int32.TryParse(args.Parameters[0], out bufftype);

            if (!tryparse)
            {
                List<int> bufftypelist = new List<int>();
                bufftypelist = TShock.Utils.GetBuffByName(buff);

                if (bufftypelist.Count < 1)
                {
                    args.Player.SendErrorMessage("No buffs by that name were found.");
                    return;
                }
                else if (bufftypelist.Count > 1)
                {
                    TShock.Utils.SendMultipleMatchError(args.Player, bufftypelist.Select(p => TShock.Utils.GetBuffName(p)));
                    return;
                }
                else
                    bufftype = bufftypelist[0];
            }

            if (bufftype > Main.maxBuffTypes || bufftype < 1) // Buff ID is not valid (less than 1 or higher than 190).
                args.Player.SendErrorMessage("Invalid buff ID!");

            string buffgroup = null;
            bool isperma = false;
            bool exists = findBuffInConfig(bufftype, out buffgroup, out isperma);

            if (!exists || !isperma)
                args.Player.SendErrorMessage("This buff is not available as a global buff!");
            else if (globalbuffs.Contains(bufftype))
            {
                globalbuffs.Remove(bufftype);
                args.Player.SendSuccessMessage("{0} has been removed from the global permabuffs.", Main.buffName[bufftype]);
            }
            else
            {
                globalbuffs.Add(bufftype);
                args.Player.SendSuccessMessage("{0} has been activated as a global permabuff!", Main.buffName[bufftype]);
            }
        }

        private void PBClear(CommandArgs args)
        {
            if (args.Parameters.Count == 1 && (args.Parameters[0] == "*" || args.Parameters[0] == "all"))
            {
                if (!args.Player.Group.HasPermission("pb.clear"))
                {
                    args.Player.SendErrorMessage("You do not have permission to clear all permabuffs.");
                    return;
                }
                foreach (KeyValuePair<int, DBInfo> kvp in permas)
                {
                    kvp.Value.bufflist.Clear();
                }
                args.Player.SendSuccessMessage("All permabuffs have been deactivated for all players.");
                if (!args.Silent)
                {
                    TSPlayer.All.SendInfoMessage("{0} has deactivated all permabuffs!", args.Player.User.Name);
                }
            }
            else
            {
                if (!args.Player.RealPlayer)
                {
                    args.Player.SendErrorMessage("You must be in-game to use this command.");
                    return;
                }
                permas[args.Player.Index].bufflist.Clear();

                args.Player.SendSuccessMessage("All of your permabuffs have been deactivated.");
            }
        }
        #endregion

        private void loadConfig()
        {
            config = Config.Read(configPath);
        }

        private bool findBuffInConfig(int bufftype, out string buffgroup, out bool isperma)
        {
            foreach (BuffGroup group in config.buffgroups)
            {
                if (group.buffIDs.Contains(bufftype))
                {
                    buffgroup = group.groupPerm;
                    isperma = group.isperma;
                    return true;
                }
            }

            buffgroup = null;
            isperma = false;
            return false;
        }

        #region Database Functions
        private void DBConnect()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "Permabuffs.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;

            }

            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("Permabuffs",
                new SqlColumn("UserID", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 4 },
                new SqlColumn("ActiveBuffs", MySqlDbType.Text) { Length = 100 }));
        }

        private bool loadDBInfo(int userid)
        {
            using (QueryResult result = db.QueryReader("SELECT * FROM Permabuffs WHERE UserID=@0;", userid))
            {
                if (result.Read())
                {
                    permas.Add(userid, new DBInfo(result.Get<string>("ActiveBuffs")));
                    return true;
                }
                else
                    return false;
            }
        }

        private void createDBInfo(int userid)
        {
            db.Query("INSERT INTO Permabuffs (UserId, ActiveBuffs) VALUES (@0, @1);", userid, String.Empty);
            permas.Add(userid, new DBInfo(""));
        }

        private void updateBuffs(int userid, List<int> bufflist)
        {
            string buffstring = string.Join(",", bufflist.Select(p => p.ToString()));

            db.Query("UPDATE Permabuffs SET ActiveBuffs=@0 WHERE UserID=@1;", userid, buffstring);
        }

        private void clearDB()
        {
            db.Query("DELETE FROM Permabuffs");
        }

        private void clearDB(int userid)
        {
            db.Query("DELETE FROM Permabuffs WHERE UserID=@0;", userid);
        }
        #endregion
    }
}
