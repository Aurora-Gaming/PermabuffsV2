using System.Data;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace Permabuffs_V2
{
	[ApiVersion(2, 1)]
	public class Permabuffs : TerrariaPlugin
	{
		public override string Name => "Permabuffs";
		public override string Author => "Zaicon";
		public override string Description => "让 buff 常驻。支持黑名单、真正的撤销、权限固化。";
		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0);

		public static string ConfigPath => Path.Combine(TShock.SavePath, "PermabuffsConfig.json");

		/// <summary>全局配置。在 OnInitialize 里赋值（不再用静态字段初始化器，避免 TShock.SavePath 尚未就绪）。</summary>
		public static Config Config { get; private set; } = new Config();

		private static readonly List<int> GlobalBuffs = new();
		private static readonly Dictionary<int, List<string>> HasAnnounced = new();

		private int _tickCounter;

		public Permabuffs(Main game) : base(game)
		{
			base.Order = 1;
		}

		#region 生命周期

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			// 用 GameUpdate 取代 System.Timers.Timer。
			// 原版用 Timer 会在【线程池线程】上遍历 TShock.Players 和 DB.PlayerBuffs，
			// 而命令在主线程修改同一个字典/列表 -> InvalidOperationException，
			// 而 .NET Core 上线程池线程的未处理异常会直接终止整个进程（崩服）。
			// GameUpdate 跑在主线程，与命令处理天然串行。
			ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			AccountHooks.AccountDelete += OnAccDelete;
			PlayerHooks.PlayerPostLogin += OnPostLogin;
			RegionHooks.RegionEntered += OnRegionEnter;
			GeneralHooks.ReloadEvent += OnReload;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				AccountHooks.AccountDelete -= OnAccDelete;
				RegionHooks.RegionEntered -= OnRegionEnter;
				PlayerHooks.PlayerPostLogin -= OnPostLogin;
				GeneralHooks.ReloadEvent -= OnReload;
			}
			base.Dispose(disposing);
		}

		public void OnInitialize(EventArgs args)
		{
			Config = Config.Read(ConfigPath);
			DB.Connect();
			RegisterCommands();

			TShock.Log.Info($"[Permabuffs] 已载入。blacklist=[{string.Join(",", Config.blacklist)}] " +
				$"heartbeat={Config.heartbeatTicks}t duration={Config.permabuffDuration}t " +
				$"autoGrant={Config.autoGrantOnLogin}");
		}

		private void RegisterCommands()
		{
			Commands.ChatCommands.Add(new Command("pb.use", PBBuffs, "permabuff")
			{ AllowServer = false, HelpText = "给自己常驻指定 buff（再次输入取消）。" });

			Commands.ChatCommands.Add(new Command("pb.check", PBCheck, "buffcheck")
			{ HelpText = "查看指定玩家当前的常驻 buff。" });

			Commands.ChatCommands.Add(new Command("pb.give", PBGive, "gpermabuff")
			{ HelpText = "给指定玩家常驻 buff。-g <组> 可整组授予。" });

			Commands.ChatCommands.Add(new Command("pb.region", PBRegion, "regionbuff")
			{ HelpText = "/regionbuff <add/del> <区域> <buff> [秒]" });

			Commands.ChatCommands.Add(new Command("pb.global", PBGlobal, "globalbuff")
			{ HelpText = "开关全服 buff。" });

			Commands.ChatCommands.Add(new Command("pb.use", PBClear, "clearbuffs")
			{ HelpText = "清除自己的常驻 buff；加 all 则清除所有人。" });

			// Readme 一直宣传但原版从未注册过的命令，这里补上。
			Commands.ChatCommands.Add(new Command("pb.reload", PBReload, "pbreload")
			{ HelpText = "重载 PermabuffsConfig.json。" });

			// 只认数字 ID 的工作流里，需要一个"名字 -> ID"的换算入口
			Commands.ChatCommands.Add(new Command("pb.use", PBId, "pbid", "buffid")
			{ AllowServer = false, HelpText = "把 buff 名字换成数字 ID（本插件只认数字）。" });

			// 撤销通道排查用：逐个策略在游戏里试，定位客户端到底采纳哪条通道
			Commands.ChatCommands.Add(new Command("pb.use", PBDebug, "pbdebug")
			{ AllowServer = false, HelpText = "撤销通道排查：/pbdebug <策略1-5> <buffID>" });
		}

		#endregion

		#region 心跳

		private void OnGameUpdate(EventArgs args)
		{
			_tickCounter++;
			if (_tickCounter < Math.Max(1, Config.heartbeatTicks)) return;
			_tickCounter = 0;

			for (int i = 0; i < TShock.Players.Length; i++)
			{
				TSPlayer p = TShock.Players[i];
				if (p?.Active != true || !p.RealPlayer) continue;

				// ① 全局 buff
				foreach (int buff in GlobalBuffs)
					BuffEngine.Apply(p, buff, Config.permabuffDuration);

				// ② 区域 buff
				if (p.CurrentRegion != null)
				{
					RegionBuff? rb = Config.regionbuffs?.FirstOrDefault(
						r => r != null && r.buffs.Count > 0 && r.regionName == p.CurrentRegion.Name);

					if (rb != null)
						foreach (KeyValuePair<int, int> kvp in rb.buffs)
							BuffEngine.Apply(p, kvp.Key, kvp.Value * 60);
				}

				// ③ 个人常驻 buff
				if (p.IsLoggedIn
					&& DB.PlayerBuffs.TryGetValue(p.Account.ID, out DBInfo? info)
					&& info != null)
				{
					foreach (int buff in info.bufflist)
						BuffEngine.Apply(p, buff, Config.permabuffDuration);
				}

				if (Config.diagnostics && p.Active && p.TPlayer.active)
				{
					// 把"插件认为的来源"和"服务端镜像"并排打出来。
					// 只看 buffs= 会被客户端回声骗到 —— 镜像里有什么，
					// 取决于插件刚下发过什么，不等于来源还在。
					int dbCount = p.IsLoggedIn
						&& DB.PlayerBuffs.TryGetValue(p.Account.ID, out DBInfo? dinfo)
						&& dinfo != null ? dinfo.bufflist.Count : -1;

					string region = p.CurrentRegion?.Name ?? "-";
					int regionBuffs = p.CurrentRegion == null ? 0
						: (Config.regionbuffs?.FirstOrDefault(
							r => r != null && r.buffs.Count > 0 && r.regionName == p.CurrentRegion!.Name)
						  ?.buffs.Count ?? 0);

					TShock.Log.Info("[PB-DIAG] " + BuffEngine.Describe(p)
						+ $" acct={p.Account.ID} logged={p.IsLoggedIn} db={dbCount}"
						+ $" global={GlobalBuffs.Count}[{string.Join(",", GlobalBuffs)}]"
						+ $" region={region}/{regionBuffs}");
				}
			}
		}

		#endregion

		#region 钩子

		public void OnGreet(GreetPlayerEventArgs args)
		{
			TSPlayer? p = TShock.Players[args.Who];
			if (p == null) return;

			if (GlobalBuffs.Count > 0)
				p.SendInfoMessage("本服当前全局常驻 buff：{0}",
					string.Join(", ", GlobalBuffs.Select(BuffEngineName)));

			// 必须在 IsLoggedIn 判断之前建立，否则出生时进区域的提示会被吞掉
			HasAnnounced[args.Who] = new List<string>();
		}

		public void OnPostLogin(PlayerPostLoginEventArgs args)
		{
			int id = args.Player.Account.ID;

			// 原版在 else 分支里 Remove 之后无条件 LoadUserBuffs 再索引，
			// 若 DB 里没有该行（刚被 clearbuffs all 清过）会直接 KeyNotFoundException。
			DB.PlayerBuffs.Remove(id);
			if (DB.LoadUserBuffs(id))
			{
				if (DB.PlayerBuffs[id].bufflist.Count > 0)
					args.Player.SendInfoMessage("你上次的常驻 buff（{0}）已恢复。",
						string.Join(", ", DB.PlayerBuffs[id].bufflist.Select(BuffEngineName)));
			}
			else
			{
				DB.AddNewUser(id);
			}

			TryAutoGrant(args.Player);
		}

		/// <summary>固化：登录时按权限自动授予整组 buff，玩家无需手动 /permabuff。</summary>
		private void TryAutoGrant(TSPlayer player)
		{
			if (!Config.autoGrantOnLogin || player == null || !player.IsLoggedIn) return;

			List<BuffGroup> groups = Config.GetGroupsAllowedTo(player);
			if (Config.autoGrantGroups is { Length: > 0 })
				groups = groups.Where(g => Config.autoGrantGroups.Contains(g.groupName, StringComparer.OrdinalIgnoreCase)).ToList();

			if (groups.Count == 0) return;

			int id = player.Account.ID;
			if (!DB.PlayerBuffs.TryGetValue(id, out DBInfo? info) || info == null) return;

			int added = 0, skipped = 0;
			foreach (BuffGroup g in groups)
			{
				foreach (int buff in g.buffIDs)
				{
					if (BuffEngine.IsBlocked(Config, buff)) { skipped++; continue; }
					if (!buff.BuffHasTimer()) { skipped++; continue; }
					if (info.bufflist.Contains(buff)) continue;

					info.bufflist.Add(buff);
					added++;
				}
			}

			if (added > 0)
			{
				DB.UpdatePlayerBuffs(id, info.bufflist);
				player.SendSuccessMessage($"已根据你的权限自动固化 {added} 个常驻 buff" +
					(skipped > 0 ? $"（跳过 {skipped} 个：黑名单或宠物类）" : "") + "。");
			}
		}

		public void OnAccDelete(AccountDeleteEventArgs args) => DB.ClearPlayerBuffs(args.Account.ID);

		public void OnRegionEnter(RegionHooks.RegionEnteredEventArgs args)
		{
			RegionBuff? rb = Config.regionbuffs?.FirstOrDefault(
				r => r != null && r.buffs.Count > 0 && r.regionName == args.Region?.Name);

			if (rb == null) return;
			if (!HasAnnounced.TryGetValue(args.Player.Index, out List<string>? seen)) return;
			if (seen.Contains(args.Region.Name)) return;

			args.Player.SendSuccessMessage("你进入了带以下 buff 的区域：{0}",
				string.Join(", ", rb.buffs.Keys.Select(BuffEngineName)));
			seen.Add(args.Region.Name);
		}

		public void OnLeave(LeaveEventArgs args)
		{
			HasAnnounced.Remove(args.Who);

			TSPlayer? p = TShock.Players[args.Who];
			if (p == null || !p.IsLoggedIn) return;

			// 掉线时把身上的常驻 buff 真正撤掉，避免残留到下一次会话
			if (DB.PlayerBuffs.TryGetValue(p.Account.ID, out DBInfo? info) && info != null)
				BuffEngine.RevokeAll(p, new List<int>(info.bufflist));

			DB.PlayerBuffs.Remove(p.Account.ID);
		}

		#endregion

		#region buff 解析

		/// <summary>
		/// 把玩家输入解析成唯一的 buff ID。
		/// 默认【只接受纯数字】；Config.allowBuffNames = true 时才回退到按名字模糊查。
		/// 数字优先的原因是 GetBuffByName 经常返回多个匹配，玩家无从判断该选哪个，
		/// 而纯数字零歧义。需要按名字用时先 /pbid &lt;名字&gt; 换成数字。
		/// </summary>
		private bool TryResolveBuff(TSPlayer caller, string input, out int buffType)
		{
			buffType = -1;

			if (!int.TryParse(input.Trim(), out int parsed))
			{
				if (!Config.allowBuffNames)
				{
					caller.SendErrorMessage("只接受数字 buff ID；\"{0}\" 不是数字。", input);
					caller.SendErrorMessage("用 {0}pbid <buff 名字> 可以把名字换成 ID。", Specifier);
					return false;
				}

				List<int> matches;
				if (!BuffEngine.TryLookupByName(input, out matches) || matches.Count < 1)
				{
					caller.SendErrorMessage("没有找到名为 \"{0}\" 的 buff。", input);
					return false;
				}
				if (matches.Count > 1)
				{
					caller.SendMultipleMatchError(matches.Select(BuffEngineName));
					return false;
				}
				parsed = matches[0];
			}

			if (BuffEngine.IsBlocked(Config, parsed, out string reason))
			{
				caller.SendErrorMessage("{0}（ID {1}）。", reason, parsed);
				return false;
			}

			buffType = parsed;
			return true;
		}

		private static string BuffEngineName(int type) => TShock.Utils.GetBuffName(type);

		private static string Specifier => TShock.Config.Settings.CommandSpecifier;

		/// <summary>
		/// 切换一个 buff 的常驻状态。返回 true 表示状态被改变了。
		/// 这是 /permabuff 和 /gpermabuff 共用的核心，保证两条路径的黑名单行为完全一致。
		/// </summary>
		private bool ToggleBuff(TSPlayer target, TSPlayer actor, int buffType, bool silent)
		{
			int id = target.Account.ID;
			if (!DB.PlayerBuffs.TryGetValue(id, out DBInfo? info) || info == null)
			{
				actor.SendErrorMessage("找不到 {0} 的 buff 数据。", target.Name);
				return false;
			}

			if (info.bufflist.Contains(buffType))
			{
				info.bufflist.Remove(buffType);
				DB.UpdatePlayerBuffs(id, info.bufflist);
				// 关键：原版只删数据库，从不撤销已生效的 buff。
				BuffEngine.Revoke(target, buffType);

				actor.SendInfoMessage("已取消 {0} 的「{1}」(ID {2}) 常驻。", target.Name, BuffEngineName(buffType), buffType);
				if (!silent && target.RealPlayer)
					target.SendInfoMessage("你的「{0}」常驻已被 {1} 取消。", BuffEngineName(buffType), actor.Name);

				// 如果同一时刻还有别的来源在提供它，心跳会立刻补回来 ——
				// 不提示的话，玩家看到的现象就是"我明明取消了，buff 却没消失"。
				string? conflict = FindOtherSource(buffType, target);
				if (conflict != null)
					target.SendWarningMessage("注意：{0}仍会补上这个 buff（{1}）。要彻底去掉请先解除该来源。", conflict, BuffEngineName(buffType));

				return true;
			}

			if (!buffType.BuffHasTimer())
			{
				// 宠物/坐骑类：不会自然消失，只发一次即可，不入库存
				BuffEngine.Apply(target, buffType, Config.permabuffDuration);
				actor.SendSuccessMessage("已给予 {0}「{1}」（宠物类，不会自动续期）。",
					target.Name, BuffEngineName(buffType));
				if (!silent && target.RealPlayer)
					target.SendSuccessMessage("{0} 给了你「{1}」。", actor.Name, BuffEngineName(buffType));
				return true;
			}

			info.bufflist.Add(buffType);
			DB.UpdatePlayerBuffs(id, info.bufflist);
			BuffEngine.Apply(target, buffType, Config.permabuffDuration);

			actor.SendSuccessMessage("已为 {0} 固化「{1}」，再次执行同一命令即可取消。",
				target.Name, BuffEngineName(buffType));
			if (!silent && target.RealPlayer)
				target.SendSuccessMessage("{0} 为你固化了「{1}」。", actor.Name, BuffEngineName(buffType));
			return true;
		}

		/// <summary>
		/// 找出"除个人常驻外"还有谁在提供这个 buff。
		/// 取消个人常驻后如果还有全局/区域来源，心跳 1 秒内就会把它补回来，
		/// 玩家看到的现象就是"取消了但没消失" —— 必须告诉他真正的来源是谁。
		/// 返回 null 表示没有其他来源。
		/// </summary>
		private static string? FindOtherSource(int buffType, TSPlayer? except = null)
		{
			List<string> sources = new();

			if (GlobalBuffs.Contains(buffType))
				sources.Add("全局 buff");

			string? region = except?.CurrentRegion?.Name;
			RegionBuff? rb = Config.regionbuffs?.FirstOrDefault(
				r => r != null && r.buffs.ContainsKey(buffType)
					&& (region == null || r.regionName == region));
			if (rb != null)
				sources.Add($"区域「{rb.regionName}」");

			return sources.Count == 0 ? null : string.Join("、", sources);
		}

		#endregion

		#region 命令

		private void PBBuffs(CommandArgs args)
		{			TSPlayer p = args.Player;

			if (args.Parameters.Count == 0)
			{
				p.SendErrorMessage("用法：{0}permabuff <buff 名称或 ID>",
					args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
				return;
			}

			if (!p.IsLoggedIn)
			{
				p.SendErrorMessage("请先登录后再使用常驻 buff。");
				return;
			}

			if (Config.buffgroups.Length == 0)
			{
				p.SendErrorMessage("管理员尚未配置任何 buff 分组。");
				return;
			}

			// 分组权限过滤 + 黑名单过滤
			List<BuffGroup> available = Config.GetGroupsAllowedTo(p)
				.Where(g => g.buffIDs.Any(b => !Config.IsBlacklisted(b)))
				.ToList();

			if (available.Count == 0)
			{
				p.SendErrorMessage("你所在的分组没有任何可用 buff（可能被黑名单全部屏蔽）。");
				return;
			}

			string input = string.Join(" ", args.Parameters);
			if (!TryResolveBuff(p, input, out int buffType)) return;

			// 该 buff 必须落在【玩家有权限的】某个分组内
			if (!available.Any(g => g.buffIDs.Contains(buffType)))
			{
				p.SendErrorMessage("你没有权限使用「{0}」。", BuffEngineName(buffType));
				return;
			}

			ToggleBuff(p, p, buffType, args.Silent);
		}

		private void PBCheck(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("用法：{0}buffcheck <玩家>",
					args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
				return;
			}

			List<TSPlayer> found = TSPlayer.FindByNameOrID(string.Join(" ", args.Parameters));
			if (found.Count < 1) { args.Player.SendErrorMessage("没有找到该玩家。"); return; }
			if (found.Count > 1) { args.Player.SendMultipleMatchError(found.Select(f => f.Name)); return; }

			TSPlayer target = found[0];
			if (!target.IsLoggedIn || !DB.PlayerBuffs.TryGetValue(target.Account.ID, out DBInfo? info) || info == null)
			{
				args.Player.SendInfoMessage("{0} 当前没有常驻 buff。", target.Name);
				return;
			}

			if (info.bufflist.Count == 0)
				args.Player.SendInfoMessage("{0} 当前没有常驻 buff。", target.Name);
			else
				args.Player.SendInfoMessage("{0} 的常驻 buff：{1}", target.Name,
					string.Join(", ", info.bufflist.Select(BuffEngineName)));
		}

		private void PBGive(CommandArgs args)
		{
			TSPlayer actor = args.Player;

			// /gpermabuff -g list
			if (args.Parameters.Count == 2
				&& args.Parameters[0].Equals("-g", StringComparison.OrdinalIgnoreCase)
				&& args.Parameters[1].Equals("list", StringComparison.OrdinalIgnoreCase))
			{
				List<BuffGroup> groups = Config.GetGroupsAllowedTo(actor);
				actor.SendInfoMessage("可用的 buff 分组：{0}",
					groups.Count == 0 ? "（无）" : string.Join(", ", groups.Select(g => g.groupName)));
				return;
			}

			// /gpermabuff -g <group> <player>
			if (args.Parameters.Count == 3)
			{
				if (!args.Parameters[0].Equals("-g", StringComparison.OrdinalIgnoreCase))
				{
					actor.SendErrorMessage("用法：{0}gpermabuff <buff> <玩家> 或 {0}gpermabuff -g <分组> <玩家>",
						TShock.Config.Settings.CommandSpecifier);
					return;
				}

				List<BuffGroup> allowed = Config.GetGroupsAllowedTo(actor);
				BuffGroup? group = allowed.FirstOrDefault(g =>
					g.groupName.Equals(args.Parameters[1], StringComparison.OrdinalIgnoreCase));

				if (group == null) { actor.SendErrorMessage("找不到分组「{0}」，或你没有该分组的权限。", args.Parameters[1]); return; }
				if (!TryResolveTarget(actor, args.Parameters[2], out TSPlayer? gt)) return;

				int gid = gt!.Account.ID;
				if (!DB.PlayerBuffs.TryGetValue(gid, out DBInfo? ginfo) || ginfo == null)
				{ actor.SendErrorMessage("找不到 {0} 的 buff 数据。", gt.Name); return; }

				int added = 0, skipped = 0;
				foreach (int buff in group.buffIDs)
				{
					if (BuffEngine.IsBlocked(Config, buff)) { skipped++; continue; }
					if (!buff.BuffHasTimer()) { skipped++; continue; }
					if (ginfo.bufflist.Contains(buff)) continue;
					ginfo.bufflist.Add(buff);
					added++;
				}

				DB.UpdatePlayerBuffs(gid, ginfo.bufflist);
				actor.SendSuccessMessage("已为 {0} 授予分组「{1}」：新增 {2} 个" +
					(skipped > 0 ? $"，跳过 {skipped} 个（黑名单/宠物类）" : "") + "。",
					gt.Name, group.groupName, added);

				if (!args.Silent && gt.RealPlayer)
					gt.SendSuccessMessage("{0} 授予了你分组「{1}」的所有可固化 buff。", actor.Name, group.groupName);
				return;
			}

			// /gpermabuff <buff> <player>
			if (args.Parameters.Count == 2)
			{
				if (!TryResolveTarget(actor, args.Parameters[1], out TSPlayer? target)) return;
				if (!TryResolveBuff(actor, args.Parameters[0], out int buffType)) return;

				List<BuffGroup> allowed = Config.GetGroupsAllowedTo(actor);
				if (!allowed.Any(g => g.buffIDs.Contains(buffType)))
				{
					actor.SendErrorMessage("你没有权限授予「{0}」。", BuffEngineName(buffType));
					return;
				}

				ToggleBuff(target!, actor, buffType, args.Silent);
				return;
			}

			actor.SendErrorMessage("用法：{0}gpermabuff <buff> <玩家> 或 {0}gpermabuff -g <分组> <玩家>",
				TShock.Config.Settings.CommandSpecifier);
		}

		private bool TryResolveTarget(TSPlayer actor, string name, out TSPlayer? target)
		{
			target = null;
			List<TSPlayer> found = TSPlayer.FindByNameOrID(name);
			if (found.Count < 1) { actor.SendErrorMessage("没有找到该玩家。"); return false; }
			if (found.Count > 1) { actor.SendMultipleMatchError(found.Select(f => f.Name)); return false; }
			if (!found[0].IsLoggedIn) { actor.SendErrorMessage("该玩家未登录，无法授予常驻 buff。"); return false; }

			target = found[0];
			return true;
		}

		private void PBRegion(CommandArgs args)
		{
			TSPlayer p = args.Player;
			if (args.Parameters.Count is < 3 or > 4)
			{
				p.SendErrorMessage("用法：{0}regionbuff <add/del> <区域> <buff> [秒]",
					args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
				return;
			}

			string action = args.Parameters[0].ToLowerInvariant();
			Region? region = TShock.Regions.GetRegionByName(args.Parameters[1]);
			if (region == null) { p.SendErrorMessage("无效区域：{0}", args.Parameters[1]); return; }

			if (!TryResolveBuff(p, args.Parameters[2], out int buffType)) return;

			RegionBuff? entry = Config.regionbuffs!.FirstOrDefault(r => r.regionName == region.Name);

			if (action == "add")
			{
				if (args.Parameters.Count != 4)
				{
					p.SendErrorMessage("添加区域 buff 必须指定持续秒数：{0}regionbuff add <区域> <buff> <秒>",
						TShock.Config.Settings.CommandSpecifier);
					return;
				}

				if (!int.TryParse(args.Parameters[3], out int seconds) || seconds is < 1 or > 540)
				{
					p.SendErrorMessage("持续时间必须是 1~540 之间的秒数。");
					return;
				}

				if (entry == null)
				{
					entry = new RegionBuff { regionName = region.Name, buffs = new Dictionary<int, int>() };
					List<RegionBuff> next = Config.regionbuffs.ToList();
					next.Add(entry);
					Config.regionbuffs = next.ToArray();
				}

				if (entry.buffs.ContainsKey(buffType))
				{
					p.SendErrorMessage("区域 {0} 已经有「{1}」了。", region.Name, BuffEngineName(buffType));
					return;
				}

				entry.buffs[buffType] = seconds;
				Config.Write(ConfigPath);
				p.SendSuccessMessage("已为区域 {0} 添加「{1}」，离开后仍持续 {2} 秒。",
					region.Name, BuffEngineName(buffType), seconds);
				return;
			}

			if (action is "del" or "delete")
			{
				if (entry == null || !entry.buffs.Remove(buffType))
				{
					p.SendInfoMessage("区域 {0} 没有「{1}」。", region.Name, BuffEngineName(buffType));
					return;
				}

				Config.Write(ConfigPath);
				p.SendSuccessMessage("已从区域 {0} 移除「{1}」。", region.Name, BuffEngineName(buffType));
				return;
			}

			p.SendErrorMessage("未知操作 {0}，只支持 add / del。", args.Parameters[0]);
		}

		private void PBGlobal(CommandArgs args)
		{
			TSPlayer p = args.Player;
			if (args.Parameters.Count == 0)
			{
				p.SendErrorMessage("用法：{0}globalbuff <buff 名称或 ID>",
					args.Silent ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier);
				return;
			}

			if (!TryResolveBuff(p, string.Join(" ", args.Parameters), out int buffType)) return;

			if (!buffType.BuffHasTimer())
			{
				p.SendErrorMessage("「{0}」是宠物/坐骑类，不能作为全局 buff。", BuffEngineName(buffType));
				return;
			}

			if (GlobalBuffs.Contains(buffType))
			{
				GlobalBuffs.Remove(buffType);
				// 原版只是停止下发，buff 会残留最多 5 分钟。这里真正撤销。
				List<string> stillPersonal = new();
				foreach (TSPlayer online in TShock.Players.Where(x => x is { Active: true, RealPlayer: true }))
				{
					BuffEngine.Revoke(online, buffType);

					// 玩家个人常驻里有同一个 buff 时，心跳会立刻补回来。
					// 不说的话现象就是"关了全局 buff，结果 buff 没消失"。
					if (online.IsLoggedIn
						&& DB.PlayerBuffs.TryGetValue(online.Account.ID, out DBInfo? oi)
						&& oi != null && oi.bufflist.Contains(buffType))
						stillPersonal.Add(online.Name);
				}

				p.SendSuccessMessage("已关闭全局 buff「{0}」(ID {1})，并从所有在线玩家身上移除。",
					BuffEngineName(buffType), buffType);

				if (stillPersonal.Count > 0)
				{
					p.SendWarningMessage("但 {0} 的个人常驻里还有它，心跳会立刻补回来。",
						string.Join("、", stillPersonal));
					TSPlayer.All.SendWarningMessage("你的个人常驻里仍有 ID {0}「{1}」，它还在生效。用 /permabuff {0} 才会真正取消。",
						buffType, BuffEngineName(buffType));
				}
			}
			else
			{
				GlobalBuffs.Add(buffType);
				foreach (TSPlayer online in TShock.Players.Where(x => x is { Active: true, RealPlayer: true }))
					BuffEngine.Apply(online, buffType, Config.permabuffDuration);

				p.SendSuccessMessage("已开启全局 buff「{0}」。", BuffEngineName(buffType));
			}
		}

		private void PBClear(CommandArgs args)
		{
			TSPlayer p = args.Player;

			if (args.Parameters.Count == 1
				&& (args.Parameters[0] == "*" || args.Parameters[0].Equals("all", StringComparison.OrdinalIgnoreCase)))
			{
				if (!p.HasPermission("pb.clear"))
				{
					p.SendErrorMessage("你没有清除所有人常驻 buff 的权限。");
					return;
				}

				int personalCount = 0, revokedPlayers = 0;

				// 逐个玩家撤销
				foreach (KeyValuePair<int, DBInfo> kvp in DB.PlayerBuffs.ToList())
				{
					// 关键：必须先取快照。原来是先 Clear() 再把同一个 list 传给
					// RevokeAll，遍历的是空集合，等于一个 buff 都没撤销。
					List<int> snapshot = kvp.Value.bufflist.ToList();
					personalCount += snapshot.Count;

					TSPlayer? online = TShock.Players.FirstOrDefault(x => x is { IsLoggedIn: true } && x.Account.ID == kvp.Key);
					if (online != null)
					{
						BuffEngine.RevokeAll(online, snapshot);
						revokedPlayers++;
					}

					kvp.Value.bufflist.Clear();
				}

				// 全局 buff 也要一起清，否则心跳会在 1 秒内把它们全部补回来
				List<int> globalSnapshot = GlobalBuffs.ToList();
				GlobalBuffs.Clear();
				foreach (TSPlayer online in TShock.Players.Where(x => x is { Active: true, RealPlayer: true }))
				{
					BuffEngine.RevokeAll(online, globalSnapshot);
					revokedPlayers++;
				}

				// 原版把 ClearDB() 写在 foreach 体内，会按玩家数重复 DELETE FROM，
				// 并且中途把字典整个换掉。必须提到循环外。
				DB.ClearDB();

				p.SendSuccessMessage("已清除所有玩家的常驻 buff（个人 {0} 个 / 全局 {1} 个），并从 {2} 名在线玩家身上实际移除。",
					personalCount, globalSnapshot.Count, revokedPlayers);
				if (!args.Silent)
					TSPlayer.All.SendInfoMessage("{0} 清除了全服所有常驻 buff。", p.Name);
				return;
			}

			if (!p.RealPlayer) { p.SendErrorMessage("该命令需要在游戏内使用。"); return; }
			if (!p.IsLoggedIn) { p.SendErrorMessage("请先登录。"); return; }

			if (!DB.PlayerBuffs.TryGetValue(p.Account.ID, out DBInfo? info) || info == null)
			{
				p.SendErrorMessage("找不到你的 buff 数据。");
				return;
			}

			List<int> removed = new List<int>(info.bufflist);
			info.bufflist.Clear();
			BuffEngine.RevokeAll(p, removed);
			DB.ClearPlayerBuffs(p.Account.ID);

			p.SendSuccessMessage("已清除你的 {0} 个常驻 buff。", removed.Count);
		}

		/// <summary>
		/// 名字 → 数字 ID 的换算入口。
		/// 本插件只认数字（Config.allowBuffNames 默认 false），所以必须提供这条换算路径，
		/// 否则玩家只能去翻 wiki 的 buff 表。
		/// </summary>
		/// <summary>
		/// 撤销通道排查。
		/// 客户端对自身 buff 有绝对权威，而 MessageBuffer 是客户端专有代码，
		/// 服务端日志无法观测它是否采纳了 50 号包。与其继续猜，不如把每种
		/// 可能的通道都做成可现场验证的策略，一次测出真正生效的那条。
		/// </summary>
		private void PBDebug(CommandArgs args)
		{
			TSPlayer p = args.Player;
			if (!p.RealPlayer) { p.SendErrorMessage("该命令需要在游戏内使用。"); return; }

			if (args.Parameters.Count == 0)
			{
				p.SendInfoMessage("用法：{0}pbdebug <策略> <buffID>", Specifier);
				p.SendInfoMessage("  1 = 50 单播   2 = 50 广播+单播   3 = TShock 完整重同步链");
				p.SendInfoMessage("  4 = 55 包 time=0   5 = 50 连发 5 次");
				p.SendInfoMessage("  0 = 全部依次尝试");
				return;
			}

			if (!int.TryParse(args.Parameters[0], out int m))
			{ p.SendErrorMessage("策略必须是数字 0-5。"); return; }
			if (args.Parameters.Count < 2 || !int.TryParse(args.Parameters[1], out int buff))
			{ p.SendErrorMessage("用法：{0}pbdebug <策略> <buffID>", Specifier); return; }

			if (buff <= 0 || buff > Terraria.ID.BuffID.Count)
			{ p.SendErrorMessage("buff ID 非法（ID {0}）。", buff); return; }

			int[] modes = m == 0 ? new[] { 1, 2, 3, 4, 5 } : new[] { m };

			foreach (int one in modes)
			{
				if (!Enum.IsDefined(typeof(BuffEngine.RevokeMode), one))
				{ p.SendErrorMessage("没有策略 {0}。", one); return; }

				var mode = (BuffEngine.RevokeMode)one;
				bool hit = BuffEngine.Revoke(p, buff, mode);
				p.SendInfoMessage("策略 {0} ({1}) 已下发：服务端槽位命中={2}。看图标是否消失。",
					one, mode, hit);
			}

			if (m == 0)
				p.SendWarningMessage("五种策略已按顺序全部下发。告诉我哪一个让图标消失了（如果都没有，就告诉我图标总共消失了没有）。");
		}

		private void PBId(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("用法：{0}pbid <buff 名字>", Specifier);
				return;
			}

			string input = string.Join(" ", args.Parameters);

			if (int.TryParse(input.Trim(), out int alreadyNumber))
			{
				args.Player.SendInfoMessage("\"{0}\" 已经是数字 ID 了：{1}",
					input, alreadyNumber);
				return;
			}

			if (!BuffEngine.TryLookupByName(input, out List<int> matches) || matches.Count < 1)
			{
				args.Player.SendErrorMessage("没有找到名为 \"{0}\" 的 buff。", input);
				return;
			}

			if (matches.Count == 1)
			{
				int id = matches[0];
				args.Player.SendInfoMessage("{0} = ID {1}{2}",
					BuffEngineName(id), id,
					Config.IsBlacklisted(id) ? "（在黑名单里，不能用）" : "");
				return;
			}

			args.Player.SendMultipleMatchError(matches.Select(BuffEngineName));
			args.Player.SendInfoMessage("以上 {0} 个都匹配，请用数字 ID 直接指定。", matches.Count);
		}

		private void PBReload(CommandArgs args)		{
			Config = Config.Read(ConfigPath);
			args.Player.SendSuccessMessage("PermabuffsConfig.json 已重载。当前黑名单：{0}",
				Config.blacklist.Length == 0 ? "（空）" : string.Join(", ", Config.blacklist.Select(BuffEngineName)));
		}

		private void OnReload(ReloadEventArgs args) => Config = Config.Read(ConfigPath);

		#endregion
	}
}
