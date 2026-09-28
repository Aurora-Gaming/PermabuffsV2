namespace Permabuffs_V2
{
	/// <summary>
	/// 单个玩家的常驻 buff 集合。数据库里存的是 "1,2,3" 这样的 CSV。
	/// 原版 `if (ActiveBuffs != "")` 在列值为 NULL 时会 NRE，这里一并加固。
	/// </summary>
	public class DBInfo
	{
		public List<int> bufflist = new();

		public DBInfo(string? activeBuffs)
		{
			if (string.IsNullOrWhiteSpace(activeBuffs)) return;

			foreach (string part in activeBuffs.Split(','))
			{
				string trimmed = part.Trim();
				if (trimmed.Length > 0 && int.TryParse(trimmed, out int buff) && buff > 0)
					bufflist.Add(buff);
			}
		}
	}
}
