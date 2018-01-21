using System.Collections.Generic;

namespace Permabuffs_V2
{
	public static class BuffIDs
	{
		private static List<int> NonPermanentBuffs = new List<int>() { 19, 27, 40, 41, 42, 45, 49, 50, 51, 52, 53, 54, 55, 56, 57, 61, 64, 65, 66, 81, 82, 84, 85, 90, 91, 92, 101, 102, 127, 128, 129, 130, 131, 132, 136, 141, 142, 143, 152, 154, 155, 162, 168, 190, 191, 193, 200, 201, 202 };
		public static bool IsPermanent(this int buffID) => !NonPermanentBuffs.Contains(buffID);
	}
}