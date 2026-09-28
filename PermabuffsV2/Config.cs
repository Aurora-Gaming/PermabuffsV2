using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using TShockAPI;

namespace Permabuffs_V2
{
	public class BuffGroup
	{
		public string groupName = string.Empty;
		public string groupPerm = string.Empty;
		public List<int> buffIDs = new List<int>();
	}

	public class RegionBuff
	{
		public string regionName = string.Empty;
		public Dictionary<int, int> buffs = new Dictionary<int, int>();
	}

	public class Config
	{
		#region 新增：黑名单与运行参数

		/// <summary>
		/// 黑名单：命中的 buffID 在任何路径下都会被跳过
		/// （buffgroup / 手动 /permabuff / gpermabuff / regionbuff / globalbuff 一律生效）。
		/// 默认只屏蔽鱼腮（4 = Gills）。
		/// 原因：在 For the Worthy（Main.getGoodWorld）世界里，
		/// Player.CheckDrowning() 中 `if (gills) flag = Main.getGoodWorld &amp;&amp; !flag;`
		/// 会把"是否淹没"取反，导致玩家在陆地上持续扣氧淹死。常驻鱼腮 = 永远无法解除。
		/// </summary>
		public int[] blacklist = new int[] { 4 };

		/// <summary>
		/// 是否允许用【名字】指定 buff。默认 false —— 只认数字 ID。
		/// 原因：TShock.Utils.GetBuffByName 对同一个词经常返回多个匹配
		/// （例如 "Regeneration" 会同时命中多个条目），玩家只能看到一串候选、
		/// 无法确定到底是哪个。纯数字没有歧义，也省掉一次 LINQ 查表。
		/// 需要按名字查时用 /pbid &lt;名字&gt; 换成数字即可。
		/// </summary>
		public bool allowBuffNames = false;

		/// <summary>
		/// 滚动时长（tick）。300 tick = 5 秒（泰拉瑞亚固定 60 tick/秒）。
		///
		/// 为什么是短时长而不是 5 分钟：客户端对自己的 buff 有绝对权威，服务端
		/// 无法"移除"一个已生效的 buff（5 种撤销通道实测全部无效，见 BuffEngine.RevokeMode）。
		/// 于是改用另一条路：不去移除，而是发一个**很短的**时长，再靠心跳不断续期。
		///   - 在名单里  → 每 heartbeatTicks 续一次，buff 永远不掉
		///   - 取消名单  → 最多 permabuffDuration 后自然消失
		/// 取消延迟 = permabuffDuration ÷ 60 秒。想要 15 秒就填 900。
		///
		/// 安全性：Player.AddBuff_TryUpdatingExistingBuffTime 对绝大多数 buff 是
		/// "只延长不缩短"（else if (buffTime[i] &lt; time)），所以这个短时长
		/// 绝不会截断玩家自己喝的药水 —— 已实测确认（喝药水仍为 5 分钟，死亡正常消失）。
		/// 只有 AddBuffTimeAdditivelyToCap 那 5 个 ID（0/94 ManaSickness/383 Kite/600/43200）
		/// 是累加的，已在 BuffEngine.Apply 里挡掉。
		/// </summary>
		public int permabuffDuration = 300;

		/// <summary>
		/// 心跳间隔（tick），即续期间隔。100 tick ≈ 1.67 秒。
		/// 必须显著小于 permabuffDuration，否则两次续期之间 buff 会掉一下（图标闪烁）。
		/// </summary>
		public int heartbeatTicks = 100;

		/// <summary>
		/// 固化：玩家登录时自动授予其有权限的分组，无需手动 /permabuff。
		/// "一劳永逸"的实现方式。
		/// </summary>
		public bool autoGrantOnLogin = false;

		/// <summary>固化时要自动授予的分组名（留空 = 自动授予玩家有权限的全部分组）。</summary>
		public string[] autoGrantGroups = new string[0];

		/// <summary>诊断日志：每个心跳打印服务端权威 buff 状态，用于排查续期是否真正生效。</summary>
		public bool diagnostics = false;

		/// <summary>
		/// 撤销时是否强制下发 50 号包让客户端重同步。
		/// 这是唯一能让客户端真正丢掉 buff 的手段（SetBuff 只延长不缩短）。
		/// 若你的服务端会自行下发权威 buff 列表，可关掉以避免误清客户端状态。
		/// </summary>
		public bool forceClientResync = true;

		#endregion

		public BuffGroup[] buffgroups = new BuffGroup[]
		{
            //34 - Merfolk - Seems broken when used by command.
            new BuffGroup()
			{
				groupName = "probuffs", groupPerm = "probuffs", buffIDs = new List<int>()
				{
				1, // Obsidian Skin
                2, //Regeneration
                3, //Swiftness
                4, //Gills
                5, //Ironskin
                6, //Mana Regeneration
                7, //Magic Power
                8, //Featherfall
                9, //Spelunker
                10, //Invisibility
                11, //Shine
                12, //Night Owl
                13, //Battle
                14, //Thorns
                15, //Water Walking
                16, //Archery
                17, //Hunter
                18, //Gravitation
                26, //Well Fed
                29, //Clairvoyance
                43, //Paladin's Shield - No time limit, but still probuff
                48, //Honey
                58, //Rapid Healing
                59, //Shadow Dodge
                //60 - Leaf Crystal - Client-Activated Only
                //62 - Ice Barrier - Client-Activated Only
                63, //Panic!
                71, //Weapon Imbue Venom
                73, //Weapon Imbue Cursed Flames
                74, //Weapon Imbue Fire
                75, //Weapon Imbue Gold
                76, //Weapon Imbue Ichor
                77, //Weapon Imbue Nanites
                78, //Weapon Imbue Confetti
                79, //Weapon Imbue Poison
                87, //Cozy Fire - Yes, it works
                89, //Heart Lamp
                93, //Ammo Box
                //95 - 100 - Beetle armor buffs - Won't stay
                104, //Mining
                105, //Heartreach
                106, //Calm
                107, //Builder
                108, //Titan
                109, //Flipper
                110, //Summoning
                111, //Dangersense
                112, //Ammo Reservation
                113, //Lifeforce
                114, //Endurance
                115, //Rage
                116, //Inferno
                117, //Wrath 
                //118 - Minecart - Skipping all minecarts
                119, //Lovestruck
                121, //Fishing
                122, //Sonar
                123, //Crate
                124, //Warmth
                146, //Happy!
                //147 - Banner - Works, but useless
                150, //Bewitched
                151, //Life Drain
                157, //Peace Candle
                158, //Star in a Bottle
                159, //Sharpened
                165, //Dryad's Blessing - Makes slimes fly!
				//170-172 - Solar Blaze - Doesn't Stay
				173, //Life Nebula (1)
				174, //Life Nebula (2)
				175, //Life Nebula (3)
				176, //Mana Nebula (1)
				177, //Mana Nebula (2)
				178, //Mana Nebula (3)
				179, //Damage Nebula (1)
				180, //Damage Nebula (2)
				181, //Damage Nebula (3)
                192, //Sugar Rush
				198, //Striking Moment
				205, //Ballista Panic
                206, //Plenty Satisfied  - Well fed tier 2
                207, //Exquisitely Stuffed - Well fed tier 3
                215, //The Bast Defense
                257, //Lucky
                //306 - Titanium Barrier - Activates but has no effect
                311, //Harvest Time
                314, //Jungle's Fury
                321, //Cerebral Mindtrick
                //326 - Activates but has no effect
                336, // Hearty Meal
                //340 - Activates but has no effect
                343, // Biome Sight
                348, // Strategist

                //357 - 364 - Activates but has no effect
                //367 - 369 - Activates but has no effect
                370, // Instinct of the Raptor
                374, // Nature of the Rat
                376, // Torch Blessing
                377, // Curse of the Bat
                383, // High Spirits
                384, // Form of the Fae
				}
            },
			new BuffGroup() { groupName = "petbuffs", groupPerm = "petbuffs", buffIDs = new List<int>() {
				19, //Shadow Orb
                27, //Fairy
                //28 - Werewolf - Client-Activated Only
                40, //Pet Bunny
                41, //Baby Penguin
                42, //Pet Turtle
                45, //Baby Eater
                49, //Pygmies
                50, //Baby Skeletron Head
                51, //Baby Hornet
                52, //Tiki Spirit
                53, //Pet Lizard
                54, //Pet Parrot
                55, //Baby Truffle
                56, //Pet Sapling
                57, //Wisp
                61, //Baby Dinosaur
                64, //Baby Slime
                65, //Eyeball Spring
                66, //Baby Snowman
                81, //Pet Spider
                82, //Squashling
                //83 - Ravens - Client-Activated Only
                84, //Black Cat
                85, //Cursed Sapling
                90, //Rudolph
                91, //Puppy
                92, //Baby Grinch
                101, //Fairy
                102, //Fairy
                //126 - Imp - Client-Activated Only
                127, //Zephyr Fish
                128, //Bunny Mount
                129, //Pigron Mount
                130, //Slime Mount
                131, //Turtle Mount
                132, //Bee Mount
                //133 - Spider - Client-Activated Only
                //134 - Twins - Client-Activated Only
                //135 - Pirate - Client-Activated Only
                136, //Mini Minotaur
                //139 - Sharknado - Client-Activated Only
                //140 - UFO (Minion) - Client-Activated Only
                141, //UFO (Mount)
                142, //Drill Mount
                143, //Scutlix Mount
                //161 - Deadly Sphere - Client-Activated Only
                152, //Magic Lantern
                154, //Baby Face Monster
                155, //Crimson Heart
                //161 - Deadly Sphere - Client-Activated Only
                162, //Unicorn Mount
                168, //Cute Fishron
                //182 - Stardust Cell - Client-Activated Only
                //187 - Stardust Guardian - Client-Activated Only
                //188 - Stardust Dragon - Client-Activated Only
                190, // Suspicious Looking Eye
                191, //Companion Cube
				193, //Basilisk Mount
				200, //Propeller Gato
				201, //Flickerwick
				202, //Hoardagron
                212, //Golf Cart
                //213 - Sanguine Bat - Client-Activated Only
                //214 - Vampire Frog - Client-Activated Only
                //216 - Baby Finch - Client-Activated Only
                217, //Estee
                218, //Sugar Glider
                219, //Shark Pup
                230, //Witch's Broom
                258, //Lil' Harpy
                259, //Fennec Fox
                260, //Glittery Butterfly
                261, //Baby Imp
                262, //Baby Red Panda
                263, //Desert Tiger
                264, //Plantero
                265, //Flamingo
                266, //Dynamite Kitten
                267, //Baby Werewolf
                268, //Shadow Mimic
                //271 - Enchanted Daggers - Client-Activated Only
                274, //Volt Bunny
                275, //Painted Horse Mount
                276, //Majestic Horse Mount
                277, //Dark Horse Mount
                278, //Pogo Stick Mount
                279, //Pirate Ship Mount
                280, //Tree Mount
                281, //Santank Mount
                282, //Goat Mount
                283, //Book Mount
                284, //Slime Prince
                285, //Suspicious Eye
                286, //Eater of Worms
                287, //Spider Brain
                288, //Skeletron Jr.
                289, //Honey Bee
                290, //Destroyer-Lite
                291, //Rez and Spaz
                292, //Mini Prime
                293, //Plantera Seedling
                294, //Toy Golem
                295, //Tiny Fishron
                296, //Phantasmal Dragon
                297, //Moonling
                298, //Fairy Princess
                299, //Jack 'O Lantern
                300, //Everscream Sapling
                301, //Ice Queen
                302, //Alien Skater
                303, //Baby Ogre
                304, //Itsy Betsy
                305, //Lava Shark Mount
                //312 - A Nice buff - Client-Activated Only, otherwise no effect
                317, //Slime Princess
                318, //Winged Slime Mount
                //322 - Terraprimsa - Client-Activated Only
                //325 - Flinx
                327, // Bernie
                328, // Glommer
                329, // Tiny Deerclops
                330, // Pig
                331, // Chester
                //335 - Abigail
                338, // Fart Kart
                //339 - Fart Kart Legacy Unused - Activates but has no effect
                341, // Slime Royals
                342, // Blessing of the Moon
                345, // Junimo
                346, // Terra Fart Kart
                // 347 - Terra Fart Kart Legacy Unused
                349, // Blue Chicken
                351, // Spiffo
                352, // Caveling Gardener
                354, // The Dirtiest Block
                //355 - Mushroom Boi - Client-Activated Only
                356, // Swarm Biter
                //365 - Whip Spider - Buff activates but has no effect
                366, // Alchemic Enchancement
                371, // Pufferfish
                372, // Cenaxe
                373, // Friendly Boulder
                378, // Blue Roller Skates
                379, // Green Roller Skates
                380, // Classic Roller Skates
                381, // Party Roller Skates
                382, // Friendly Rainbow Boulder
                // 385 - Cattiva - Client-Activated Only
                // 386 - Foxparks - Client-Activated Only
                387, // Chillet
                388, // Chillet Ignis
            }},
			new BuffGroup()
			{
				groupName = "debuffs", groupPerm = "debuffs", buffIDs = new List<int>() {
                //20 - Poisoned - Client-Activated Only
                21, //Potion Sickness
                //22 - Darkness - Client-Activated Only
                //23 - Cursed - Client-Activated Only
                24, //On Fire!
                25, //Tipsy
                //30 - Bleeding - Client-Activated Only
                //31 - Confused - Client-Activated Only
                //32 - Slow - Client-Activated Only
                //33 - Weak - Client-Activated Only
                //35 - Silenced - Client-Activated Only
                //36 - Broken Armor - Client-Activated Only
                //37 - Horrified - Client-Activated Only
                //38 - The Tongue - Client-Activated Only
                39, // Cursed Inferno
                //46 - Chilled - Client-Activated Only
                47, //Frozen
                67, //Burning
                //68 - Suffocation - No Way To Remove Once Active!
                69, //Ichor
                70, //Venoma 
                72, //Midas
                80, //Blackout
                86, //Water Candle - It actually works :o
                88, //Chaos State
                94, //Mana Sickness - Makes magic useless!
                103, //Wet
                137, //Slime
                144, //Electrified
                145, //Moon Bite
                148, //Feral Bite
                149, //Webbed
                //153 - Shadowflame - Doesn't affect humans
                156, //Stoned
                160, //Dazed
                163, //Obstructed - My favorite buff :)
                164, //Distroted - Also a cool buff
                //169 - Penetrated - Doesn't affect humans
                //183 - Celled - Doesn't affect humans
                //186 - Dryad's Bane - Doesn't affect humans
                //189 - Daybroken - Doesn't affect humans
				194, //Wind Pushed
				195, //Withered Armor
				196, //Withered Weapon
				197, //Oozed
				//199 - Creative Shock - Activates but has no effect
				//203 - No effectBetsy's Curse
				//204 - Oiled - Doesn't affect humans
                320, //Sparkle Slime
                323, // Hellfire
                324, // Frostbite
                // 332 - Peckish
                // 333 - Hungry
                // 334 - Starving
                // 337 - TentacleSpike - Activates but has no effect
                // 344 - Blood Butchered - Activates but has no effect
                // 350 - Shadow Candle
                353, // Shimmering

                //375 - Hemorrhage - Activates but has no effect

				}
            },

			// 常用增益精选组。ID 升序排列，方便肉眼比对和二分查找。
			// 全部为服务端可下发的增益（BuffHasTimer 为真），不含任何 debuff。
			new BuffGroup()
			{
				groupName = "111", groupPerm = "111", buffIDs = new List<int>() {
                2,   //Regeneration  - 生命恢复
                3,   //Swiftness     - 移动速度
                5,   //Ironskin      - 防御
                8,   //Featherfall   - 免疫摔落伤害
                9,   //Spelunker     - 照亮 + 标记宝箱
                11,  //Shine         - 照亮
                12,  //Night Owl     - 夜视
                26,  //Well Fed      - 饱食状态
                111, //Dangersense   - 危险感知
                113, //Lifeforce     - 生命链接
                114, //Endurance     - 耐力
                115, //Rage          - 愤怒（攻击+暴击）
                117, //Wrath         - 神怒（攻击+防御）
				}
			}
		};

		public RegionBuff[] regionbuffs = new RegionBuff[]
		{
			new RegionBuff() { regionName = "spawn", buffs = new Dictionary<int,int>() { {11, 10}}}
		};

		public void Write(string path)
		{
			string? dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
		}

		public static Config Read(string path)
		{
			if (!File.Exists(path))
			{
				Config fresh = new Config();
				fresh.Write(path);
				return fresh;
			}

			Config? parsed;
			try
			{
				parsed = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
			}
			catch (Exception e)
			{
				// 配置解析绝不能依赖 TShock 运行时是否就绪（Read 可能被静态初始化调用）
				LogError($"[Permabuffs] 配置文件解析失败，已回退到默认配置: {path}");
				LogError(e.ToString());
				Config fallback = new Config();
				fallback.Write(path);
				return fallback;
			}

			if (parsed == null)
			{
				Config fallback = new Config();
				fallback.Write(path);
				return fallback;
			}

			try
			{
				parsed.Normalize();
			}
			catch (Exception e)
			{
				LogError("[Permabuffs] 配置规整失败，已回退到默认配置。");
				LogError(e.ToString());
				Config fallback = new Config();
				fallback.Write(path);
				return fallback;
			}

			return parsed;
		}

		private static void LogError(string message)
		{
			try { TShock.Log.Error(message); }
			catch { Console.Error.WriteLine(message); }
		}

		/// <summary>该 buff 是否为"累加型时长"（AddBuffTimeAdditivelyToCap）。这类不适合滚动续期，Apply 会跳过。</summary>
		public static bool IsAdditiveTimeBuff(int buffType) => BuffEngine.IsAdditiveTimeBuff(buffType);

		/// <summary>反序列化加固：管理员手写 JSON 时漏字段 / 写 null 都会在这里被补齐，
		/// 避免运行期 NullReferenceException（原版会在 config.buffgroups.Length 处直接崩）。
		/// </summary>
		public void Normalize()
		{
			blacklist ??= new int[] { 4 };
			autoGrantGroups ??= new string[0];
			permabuffDuration = Math.Clamp(permabuffDuration, 60, 3600000);
			heartbeatTicks = Math.Clamp(heartbeatTicks, 1, 3600);

			// 续期间隔必须给滚动时长留余量：至少 3 次心跳落在同一个时长窗口内，
			// 否则网络抖动或 GC 停顿就会让 buff 掉一帧，表现为图标闪烁。
			int minTicks = Math.Max(1, permabuffDuration / 3);
			if (heartbeatTicks > minTicks)
			{
				TShock.Log.Warn($"Permabuffs: permabuffDuration={permabuffDuration} 相对 heartbeatTicks={heartbeatTicks} "
					+ $"过短，已把心跳收紧到 {minTicks}（= 3 次续期/时长窗口）。");
				heartbeatTicks = minTicks;
			}

			buffgroups ??= Array.Empty<BuffGroup>();
			foreach (BuffGroup g in buffgroups)
			{
				if (g == null) continue;
				g.groupName ??= string.Empty;
				g.groupPerm ??= string.Empty;
				g.buffIDs ??= new List<int>();
			}

			regionbuffs ??= Array.Empty<RegionBuff>();
			foreach (RegionBuff r in regionbuffs)
			{
				if (r == null) continue;
				r.regionName ??= string.Empty;
				r.buffs ??= new Dictionary<int, int>();
			}
		}

		/// <summary>黑名单判定。bufType &lt;= 0 一律视为非法。</summary>
		public bool IsBlacklisted(int buffType)
			=> buffType > 0 && blacklist.Contains(buffType);

		/// <summary>把黑名单从配置里剔除，返回可安全授予的分组。</summary>
		public List<BuffGroup> GetGroupsAllowedTo(TSPlayer player)
		{
			List<BuffGroup> groups = buffgroups
				.Where(g => g != null
					&& (string.IsNullOrEmpty(g.groupPerm)
						|| player.HasPermission($"pb.{g.groupPerm}")
						|| player.HasPermission("pb.useall")))
				.ToList();
			return groups;
		}
	}
}
