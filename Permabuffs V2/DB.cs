using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;

namespace Permabuffs_V2
{
	public static class DB
	{
		private static IDbConnection db;
		public static Dictionary<int, DBInfo> PlayerBuffs = new Dictionary<int, DBInfo>();

		public static void Connect()
		{
			switch (TShock.Config.Settings.StorageType.ToLower())
			{
				case "mysql":
					string[] dbHost = TShock.Config.Settings.MySqlHost.Split(':');
					db = new MySqlConnection()
					{
						ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
							dbHost[0],
							dbHost.Length == 1 ? "3306" : dbHost[1],
							TShock.Config.Settings.MySqlDbName,
							TShock.Config.Settings.MySqlUsername,
							TShock.Config.Settings.MySqlPassword)

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

		public static bool LoadUserBuffs(int userid)
		{
			using (QueryResult result = db.QueryReader("SELECT * FROM Permabuffs WHERE UserID=@0;", userid))
			{
				if (result.Read())
				{
					PlayerBuffs.Add(userid, new DBInfo(result.Get<string>("ActiveBuffs")));
					return true;
				}
				else
					return false;
			}
		}

		public static void AddNewUser(int userid)
		{
			db.Query("INSERT INTO Permabuffs (UserId, ActiveBuffs) VALUES (@0, @1);", userid, String.Empty);
			PlayerBuffs.Add(userid, new DBInfo(""));
		}

		public static void UpdatePlayerBuffs(int userid, List<int> bufflist)
		{
			string buffstring = string.Join(",", bufflist.Select(p => p.ToString()));

			db.Query("UPDATE Permabuffs SET ActiveBuffs=@0 WHERE UserID=@1;", buffstring, userid);
		}

		public static void ClearDB()
		{
			db.Query("DELETE FROM Permabuffs;");
			PlayerBuffs = new Dictionary<int, DBInfo>();
		}

		public static void ClearPlayerBuffs(int userid)
		{
			db.Query("DELETE FROM Permabuffs WHERE UserID=@0;", userid);
			PlayerBuffs[userid] = new DBInfo("");
		}
	}
}
