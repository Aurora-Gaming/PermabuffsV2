using System.Collections.Generic;
using Terraria;
using TShockAPI;

namespace Permabuffs_V2
{
	/// <summary>
	/// buff 施加 / 续期 / 撤销 的唯一入口。
	///
	/// 以下结论全部来自 OTAPI 反编译源码（Terraria.Player / Terraria.NetMessage），
	/// 不是推测，改动前请先读完再动手：
	///
	/// 【1】Terraria 多人模式下 buff 是【客户端权威】的。
	///     Player.AddBuff() 开头：
	///         if (Main.netMode == 1 &amp;&amp; Main.myPlayer != whoAmI) {
	///             if (Main.pvpBuff[type]) NetMessage.SendData(55, -1, -1, null, whoAmI, type, time);
	///             return;      // 专服上对远程玩家直接返回
	///         }
	///     所以服务端【无法】用 vanilla API 直接给别的玩家加 buff，只能发包。
	///     TShock 的 TSPlayer.SetBuff 只做 SendData(55, ...) 并不是偷懒，而是被引擎逼的。
	///     —— 这一点纠正了"直接在服务端调 SetBuff 就能修好"的直觉想法，那样做无效。
	///
	/// 【2】Player.AddBuff_TryUpdatingExistingBuffTime() 对【已存在】的 buff 只会：
	///         else if (buffTime[i] &lt; time) buffTime[i] = time;
	///         return true;      // ← 返回 true 后 AddBuff 不再发任何包
	///     即【只延长、不缩短】，且重复施加是静默的。
	///     => SetBuff(type, 0) 取消不了一个正在跑的 buff。
	///     这就是原版 PermabuffsV2 "取消无效" 的根因。
	///
	/// 【3】唯一真正的撤销路径：清掉服务端权威的 buffType 槽位，再下发 50 号包
	///     （NetMessage 的 case 50）让客户端用服务端的完整列表覆盖自己的数组：
	///         case 50:
	///             packetWriter.Write((byte)number);
	///             Player p = Main.player[number];
	///             for (int i = 0; i &lt; Player.maxBuffs; i++)
	///                 if (p.buffType[i] &gt; 0) packetWriter.Write((ushort)p.buffType[i]);
	///             packetWriter.Write((ushort)0);
	///     TShock 自己在 PlayerData 里清空玩家 buff 时用的也是这个组合。
	/// </summary>
	public static class BuffEngine
	{
		/// <summary>黑名单 / 非法 buff 统一判定。</summary>
		public static bool IsBlocked(Config cfg, int buffType)
			=> buffType <= 0 || buffType > Terraria.ID.BuffID.Count || cfg.IsBlacklisted(buffType);

		/// <summary>黑名单之外的额外拦截钩子（供将来扩展）。</summary>
		public static bool IsBlocked(Config cfg, int buffType, out string reason)
		{
			reason = string.Empty;
			if (buffType <= 0) { reason = "buff ID 非法"; return true; }
			if (buffType > Terraria.ID.BuffID.Count) { reason = "buff ID 超出范围"; return true; }
			if (cfg.IsBlacklisted(buffType)) { reason = "该 buff 已被管理员加入黑名单"; return true; }
			return false;
		}

		/// <summary>
		/// 这些 ID 走 Player.AddBuff 的"累加 + 上限"分支，而不是"只延长不缩短"。
		/// 对它们发短时长会反复把时长往上加、把玩家正常获得的时长顶到上限，
		/// 正好违反"不能影响正常渠道获得的 buff 时长"这条约束，所以直接挡掉。
		/// 来源：Terraria.ID.BuffID.Sets.AddBuffTimeAdditivelyToCap = CreateIntSet(0, 94, 600, 383, 43200)
		/// 其中 94 = ManaSickness、383 = Kite，其余无命名常量。
		/// </summary>
		private static readonly HashSet<int> AdditiveTimeBuffs = new() { 0, 94, 600, 383, 43200 };

		/// <summary>该 buff 是否为累加型（不适合滚动续期）。</summary>
		public static bool IsAdditiveTimeBuff(int buffType) => AdditiveTimeBuffs.Contains(buffType);

		/// <summary>
		/// 施加 / 续期一个 buff。
		/// 走客户端权威通道（TSPlayer.SetBuff → 55 号包）。
		/// 因为引擎的 extend-only 语义，重复调用只会把时间顶回 duration，是幂等的。
		/// </summary>
		public static bool Apply(TSPlayer player, int buffType, int duration)
		{
			if (player == null || !player.RealPlayer) return false;
			if (IsBlocked(Permabuffs.Config, buffType)) return false;
			if (IsAdditiveTimeBuff(buffType)) return false;

			player.SetBuff(buffType, duration);
			return true;
		}

		/// <summary>
		/// 撤销策略。客户端对 buff 有绝对权威，服务端"清数组"能否生效取决于
		/// 哪条通道真的被客户端采纳 —— 这一点源码上说不清（MessageBuffer 是客户端
		/// 专有代码，服务端日志看不到），所以做成可切换的，让人在游戏里逐个试。
		/// </summary>
		public enum RevokeMode
		{
			/// <summary>清服务端槽位 + 50 号包单播（原实现）</summary>
			Msg50Direct = 1,
			/// <summary>清服务端槽位 + 50 号包先广播再单播（与 TShock RestoreCharacter 前两步一致）</summary>
			Msg50Both = 2,
			/// <summary>清服务端槽位 + TShock 完整重同步序列（4/42/16/50/76/45/39/50）</summary>
			FullResync = 3,
			/// <summary>清服务端槽位 + 55 号包 time=0 定点撤销</summary>
			Msg55Zero = 4,
			/// <summary>清服务端槽位 + 50 号包连发 5 次（防被并发同步覆盖）</summary>
			Msg50Spam = 5,
		}

		/// <summary>
		/// 真正撤销一个 buff。
		/// 返回是否在服务端槽位里真的删掉了（不代表客户端一定采纳）。
		/// </summary>
		public static bool Revoke(TSPlayer player, int buffType, RevokeMode mode = RevokeMode.Msg50Direct)
		{
			if (player == null || !player.RealPlayer) return false;

			Player tp = player.TPlayer;
			bool removed = false;

			string before = Describe(player);
			int idx = player.Index;

			// 倒序遍历：DelBuff 会把后续槽位前移压缩，正序会漏删
			for (int i = Terraria.Player.maxBuffs - 1; i >= 0; i--)
			{
				if (tp.buffType[i] == buffType)
				{
					tp.DelBuff(i);
					removed = true;
				}
			}

			SendRevoke(player, buffType, mode);

			if (Permabuffs.Config.diagnostics)
				TShock.Log.Info($"[PB-REVOKE] {player.Name} buff={buffType} idx={idx} mode={(int)mode} "
					+ $"slotHit={removed} "
					+ $"before[{before}] after[{Describe(player)}]");

			return removed;
		}

		/// <summary>按策略向客户端下发撤销指令。各策略都包含清服务端槽位之前的状态。</summary>
		private static void SendRevoke(TSPlayer player, int buffType, RevokeMode mode)
		{
			int idx = player.Index;
			ushort id = (ushort)buffType;

			switch (mode)
			{
				case RevokeMode.Msg50Both:
					// 广播那一次在专服上是空操作（50 不在 orig_SendData 的广播 case 里），
					// 但保持与 TShock 完全一致，便于排除"包形态不对"的可能。
					NetMessage.SendData(50, -1, -1, null, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(50, idx, -1, null, idx, 0f, 0f, 0f, 0, 0, 0);
					break;

				case RevokeMode.FullResync:
					// 抄 TShock RestoreCharacter 的做法：整条重同步链。
					// 代价是会看到一次"角色数据刷新"的闪烁。
					NetMessage.SendData(4, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(42, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(16, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(50, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(76, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(45, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(39, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 400, 0f, 0f, 0, 0, 0);
					NetMessage.SendData(50, idx, -1, Terraria.Localization.NetworkText.Empty, idx, 0f, 0f, 0f, 0, 0, 0);
					break;

				case RevokeMode.Msg55Zero:
					// 与 SetBuff 同一通道，time 传 0。
					NetMessage.SendData(55, idx, -1, null, idx, id, 0f, 0f, 0, 0, 0);
					break;

				case RevokeMode.Msg50Spam:
					for (int k = 0; k < 5; k++)
						NetMessage.SendData(50, idx, -1, null, idx, 0f, 0f, 0f, 0, 0, 0);
					break;

				default:
					NetMessage.SendData(50, idx, -1, null, idx, 0f, 0f, 0f, 0, 0, 0);
					break;
			}
		}

		/// <summary>撤销一组 buff。</summary>
		public static void RevokeAll(TSPlayer player, IEnumerable<int> buffTypes)
		{
			if (player == null) return;
			foreach (int type in buffTypes)
				Revoke(player, type);
		}

		/// <summary>服务端权威视角：玩家当前是否持有该 buff。</summary>
		public static bool IsActiveServerSide(TSPlayer player, int buffType)
		{
			if (player == null) return false;
			Player tp = player.TPlayer;
			for (int i = 0; i < Terraria.Player.maxBuffs; i++)
				if (tp.buffType[i] == buffType && tp.buffTime[i] > 0)
					return true;
			return false;
		}

		/// <summary>
		/// 诊断输出：把服务端权威的【完整】buff 数组打出来。
		/// 早期版本只查单个写死的 buff ID，结果黑名单里的 buff 永远是 slot=-1，
		/// 等于什么都没观测到。这里改成全量转储。
		/// </summary>
		public static string Describe(TSPlayer player)
		{
			Player tp = player.TPlayer;
			List<string> active = new();

			for (int i = 0; i < Terraria.Player.maxBuffs; i++)
			{
				if (tp.buffType[i] <= 0 || tp.buffTime[i] <= 0) continue;
				active.Add($"{tp.buffType[i]}:{tp.buffTime[i]}");
			}

			return string.Format(
				"[{0}] buffs=[{1}] breath={2}/{3} gillsField={4} getGoodWorld={5} netMode={6}",
				player.Name,
				active.Count == 0 ? "-" : string.Join(",", active),
				tp.breath, tp.breathMax,
				tp.gills,
				Main.getGoodWorld,
				Main.netMode);
		}

		/// <summary>按名字反查 buff ID，供"只认数字"的工作流做一次性转换。</summary>
		public static bool TryLookupByName(string name, out List<int> matches)
		{
			matches = TShock.Utils.GetBuffByName(name);
			return matches.Count > 0;
		}
	}
}
