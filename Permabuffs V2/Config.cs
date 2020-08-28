using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Permabuffs_V2
{
	public class BuffGroup
	{
		public string groupName;
		public string groupPerm;
		public List<int> buffIDs;
	}

	public class RegionBuff
	{
		public string regionName;
		public Dictionary<int, int> buffs;
	}

	public class Config
	{
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
                206,//Plenty Satisfied  - Well fed tier 2
                207, //Exquisitely Stuffed - Well fed tier 3
                215, //The Bast Defense
                257, //Lucky
                //306 - Titanium Barrier - Client-Activated Only, otherwise no effect
                311, //Harvest Time
                314, //Jungle's Fury
                321 //Cerebral Mindtrick



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
                318 //Winged Slime Mount
                //322 - Terraprimsa - Client-Activated Only


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
				//199 - Creative Shock - Shows up but doesn't actually disable building
				//203 - No effectBetsy's Curse
				//204 - Oiled - Doesn't affect humans
                320 //Sparkle Slime

				}
			}
		};

		public RegionBuff[] regionbuffs = new RegionBuff[]
		{
			new RegionBuff() { regionName = "spawn", buffs = new Dictionary<int,int>() { {11, 10}}}
		};

		public void Write(string path)
		{
			File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
		}

		public static Config Read(string path)
		{
            if (!File.Exists(path))
            {
                Config config = new Config();
                config.Write(path);
                return config;
            }
            else
            {
               return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            }
		}
	}
}
