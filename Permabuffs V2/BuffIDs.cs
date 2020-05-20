using System.Collections.Generic;

namespace Permabuffs_V2
{
	public static class BuffIDs
	{
		private static List<int> NonPermanentBuffs = new List<int>() { 19, 27, 40, 41, 42, 45, 49, 50, 51, 52, 53, 54, 55, 56, 57, 61, 64, 65, 66, 81, 82, 84, 85, 90, 91, 92, 101, 102, 127, 128, 129, 130, 131, 132, 136, 141, 142, 143, 152, 154, 155, 162, 168, 190, 191, 193, 200, 201, 202, 212, 214, 217, 218, 219, 230, 258, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268, 274, 275, 276, 277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288, 289, 290, 291, 292, 293, 294, 295, 296, 297, 298, 299, 300, 301, 302, 303, 304, 305, 317, 318,  };
		public static bool IsPermanent(this int buffID) => !NonPermanentBuffs.Contains(buffID);
	}
}