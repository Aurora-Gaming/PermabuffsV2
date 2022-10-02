using Newtonsoft.Json;

namespace Permabuffs_V2;

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
                    2, // Regeneration
                    3, // Swiftness
                    4, // Gills
                    5, // Ironskin
                    6, // Mana Regeneration
                    7, // Magic Power
                    8, // Featherfall
                    9, // Spelunker
                    10, // Invisibility
                    11, // Shine
                    12, // Night Owl
                    13, // Battle
                    14, // Thorns
                    15, // Water Walking
                    16, // Archery
                    17, // Hunter
                    18, // Gravitation
                    26, // Well Fed
                    29, // Clairvoyance
                    43, // Paladin's Shield - No time limit, but still probuff
                    48, // Honey
                    58, // Rapid Healing
                    59, // Shadow Dodge
                    // 60 - Leaf Crystal - no effect
                    // 62 - Ice Barrier - no effect
                    63, // Panic!
                    71, // Weapon Imbue Venom
                    73, // Weapon Imbue Cursed Flames
                    74, // Weapon Imbue Fire
                    75, // Weapon Imbue Gold
                    76, // Weapon Imbue Ichor
                    77, // Weapon Imbue Nanites
                    78, // Weapon Imbue Confetti
                    79, // Weapon Imbue Poison
                    87, // Cozy Fire - Yes, it works
                    89, // Heart Lamp
                    93, // Ammo Box
                    //95 - 100 - Beetle armor buffs - no effect
                    104, // Mining
                    105, // Heartreach
                    106, // Calm
                    107, // Builder
                    108, // Titan
                    109, // Flipper
                    110, // Summoning
                    111, // Dangersense
                    112, // Ammo Reservation
                    113, // Lifeforce
                    114, // Endurance
                    115, // Rage
                    116, // Inferno
                    117, // Wrath 
                    // 118 - Minecart - Skipping all minecarts
                    119, // Lovestruck
                    121, // Fishing
                    122, // Sonar
                    123, // Crate
                    124, // Warmth
                    146, // Happy!
                    //147 - Banner - Works, but useless
                    150, // Bewitched
                    151, // Life Drain
                    157, // Peace Candle
                    158, // Star in a Bottle
                    159, // Sharpened
                    165, // Dryad's Blessing - Makes slimes fly!
				    // 170-172 - Solar Blaze - no effect
				    173, // Life Nebula (1)
				    174, // Life Nebula (2)
				    175, // Life Nebula (3)
				    176, // Mana Nebula (1)
				    177, // Mana Nebula (2)
				    178, // Mana Nebula (3)
				    179, // Damage Nebula (1)
				    180, // Damage Nebula (2)
				    181, // Damage Nebula (3)
                    192, // Sugar Rush
				    198, // Striking Moment
				    205, // Ballista Panic
                    206,// Plenty Satisfied  - Well fed tier 2
                    207, // Exquisitely Stuffed - Well fed tier 3
                    215, // The Bast Defense
                    257, // Lucky
                    // 306 - Titanium Barrier - no effect, otherwise no effect
                    311, // Harvest Time
                    314, // Jungle's Fury
                    321, // Cerebral Mindtrick
                    336, // Hearty Meal
                    343, // Biome Sight
                    348, // Strategist
                }
            },
            new BuffGroup() 
            { 
                groupName = "petbuffs", groupPerm = "petbuffs", buffIDs = new List<int>()
                {
                    19, // Shadow Orb
                    27, // Fairy
                    // 28 - Werewolf - no effect
                    40, // Pet Bunny
                    41, // Baby Penguin
                    42, // Pet Turtle
                    45, // Baby Eater
                    49, // Pygmies
                    50, // Baby Skeletron Head
                    51, // Baby Hornet
                    52, // Tiki Spirit
                    53, // Pet Lizard
                    54, // Pet Parrot
                    55, // Baby Truffle
                    56, // Pet Sapling
                    57, // Wisp
                    61, // Baby Dinosaur
                    64, // Baby Slime
                    65, // Eyeball Spring
                    66, // Baby Snowman
                    81, // Pet Spider
                    82, // Squashling
                    // 83 - Ravens - no effect
                    84, // Black Cat
                    85, // Cursed Sapling
                    90, // Rudolph
                    91, // Puppy
                    92, // Baby Grinch
                    101, // Fairy
                    102, // Fairy
                    // 126 - Imp - no effect
                    127, // Zephyr Fish
                    128, // Bunny Mount
                    129, // Pigron Mount
                    130, // Slime Mount
                    131, // Turtle Mount
                    132, // Bee Mount
                    // 133 - Spider - no effect
                    // 134 - Twins - no effect
                    // 135 - Pirate - no effect
                    136, // Mini Minotaur
                    // 139 - Sharknado - no effect
                    // 140 - UFO (Minion) - no effect
                    141, // UFO (Mount)
                    142, // Drill Mount
                    143, // Scutlix Mount
                    // 161 - Deadly Sphere - no effect
                    152, // Magic Lantern
                    154, // Baby Face Monster
                    155, // Crimson Heart
                    // 161 - Deadly Sphere - no effect
                    162, // Unicorn Mount
                    168, // Cute Fishron
                    // 182 - Stardust Cell - no effect
                    // 187 - Stardust Guardian - no effect
                    // 188 - Stardust Dragon - no effect
                    190, // Suspicious Looking Eye
                    191, // Companion Cube
				    193, // Basilisk Mount
				    200, // Propeller Gato
				    201, // Flickerwick
				    202, // Hoardagron
                    212, // Golf Cart
                    // 213 - Sanguine Bat - no effect
                    // 214 - Vampire Frog - no effect
                    // 216 - Baby Finch - no effect
                    217, // Estee
                    218, // Sugar Glider
                    219, // Shark Pup
                    230, // Witch's Broom
                    258, // Lil' Harpy
                    259, // Fennec Fox
                    260, // Glittery Butterfly
                    261, // Baby Imp
                    262, // Baby Red Panda
                    263, // Desert Tiger
                    264, // Plantero
                    265, // Flamingo
                    266, // Dynamite Kitten
                    267, // Baby Werewolf
                    268, // Shadow Mimic
                    // 271 - Enchanted Daggers - no effect
                    274, // Volt Bunny
                    275, // Painted Horse Mount
                    276, // Majestic Horse Mount
                    277, // Dark Horse Mount
                    278, // Pogo Stick Mount
                    279, // Pirate Ship Mount
                    280, // Tree Mount
                    281, // Santank Mount
                    282, // Goat Mount
                    283, // Book Mount
                    284, // Slime Prince
                    285, // Suspicious Eye
                    286, // Eater of Worms
                    287, // Spider Brain
                    288, // Skeletron Jr.
                    289, // Honey Bee
                    290, // Destroyer-Lite
                    291, // Rez and Spaz
                    292, // Mini Prime
                    293, // Plantera Seedling
                    294, // Toy Golem
                    295, // Tiny Fishron
                    296, // Phantasmal Dragon
                    297, // Moonling
                    298, // Fairy Princess
                    299, // Jack 'O Lantern
                    300, // Everscream Sapling
                    301, // Ice Queen
                    302, // Alien Skater
                    303, // Baby Ogre
                    304, // Itsy Betsy
                    305, // Lava Shark Mount
                    // 312 - A Nice Buff - Activates but no effect
                    317, // Slime Princess
                    318, // Winged Slime Mount
                    // 322 - Terraprimsa - no effect
                    // 325 - Flinx - no effect
                    327, // Bernie
                    328, // Glommer
                    329, // Tiny Deerclops
                    330, // Pig
                    331, // Chester
                    // 335 - Abigail - no effect
                    // 338, 339, 346, 347 - more minecarts
                    341, // Slime Royals
                    342, // Blessing of the Moon
                    345, // Junimo
                    349, // Blue Chicken
                    351, // Spiffo
                    352, // Caveling Gardener
                    354, // The Dirtiest Block
                }
            },
            new BuffGroup()
            {
                groupName = "debuffs", groupPerm = "debuffs", buffIDs = new List<int>() 
                {
                    20, // Poisoned
                    21, // Potion Sickness
                    22, // Darkness
                    23, // Cursed
                    24, // On Fire!
                    25, // Tipsy
                    // 30 - Bleeding - activates but no effect
                    31, // Confused
                    32, // Slow
                    33, // Weak
                    35, // Silenced
                    36, // Broken Armor
                    // 37 - Horrified - no effect
                    // 38 - The Tongue - no effect
                    39, // Cursed Inferno
                    46, // Chilled
                    47, // Frozen
                    67, // Burning
                    // 68 - Suffocation - No Way To Remove Once Active!
                    69, // Ichor
                    70, // Venoma 
                    72, // Midas
                    80, // Blackout
                    86, // Water Candle - It actually works :o
                    88, // Chaos State
                    94, // Mana Sickness - Makes magic useless!
                    103, // Wet
                    137, // Slime
                    144, // Electrified
                    145, // Moon Bite
                    148, // Feral Bite
                    149, // Webbed
                    // 153 - Shadowflame - activates but no effect
                    156, // Stoned
                    160, // Dazed
                    163, // Obstructed
                    164, // Distroted
                    // 169 - Penetrated - activates but no effect
                    // 183 - Celled - activates but no effect
                    // 186 - Dryad's Bane - activates but no effect
                    // 189 - Daybroken - activates but no effect
				    194, // Wind Pushed
				    195, // Withered Armor
				    196, // Withered Weapon
				    197, // Oozed
				    // 199 - Creative Shock - activates but no effect
				    // 203 - Betsy's Curse - activates but no effect
				    // 204 - Oiled - activates but no effect
                    320, // Sparkle Slime
                    323, // Hellfire
                    324, // Frostbite
                    // 326 - Bone Whip - activates but no effect (npc debuff)
                    // 332 - Peckish - no effect
                    // 333 - Hunger - no effect
                    // 337 - Tentacle Spike - activates but no effect
                    // 340 - Cool Whip - activates but no effect (npc debuff)
                    // 344 - Blood butchered - activates but no effect
                    350, // Shadow Candle
                    // 353 - Shimmering - just no
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
            Config config = new();
            config.Write(path);
            return config;
        }
        else
        {
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
        }
    }
}
