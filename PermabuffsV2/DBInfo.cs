namespace Permabuffs_V2;

public class DBInfo
{
	public List<int> bufflist;

	public DBInfo(string activeBuffs)
	{
		bufflist = new List<int>();
		if (activeBuffs != "")
		{
			string[] buffstring = activeBuffs.Split(',');
			foreach (string buff in buffstring)
			{
				if (int.TryParse(buff, out int tempbuff))
					bufflist.Add(tempbuff);
			}
		}
	}
}
