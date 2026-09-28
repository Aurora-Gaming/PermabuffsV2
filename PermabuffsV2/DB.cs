using System.Data;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace Permabuffs_V2
{
	/// <summary>
	/// 持久化层。表结构极简：一行一个玩家，一列存逗号分隔的 buffID 列表。
	/// 原版在这里有几个会在运行期炸掉的点，已全部加固：
	///   - LoadUserBuffs / AddNewUser 用 Dictionary.Add，键已存在时直接抛 ArgumentException
	///   - ClearPlayerBuffs 用 PlayerBuffs[userid] = ...，键不存在时抛 KeyNotFoundException
	///   - Connect() 在 StorageType 既不是 mysql 也不是 sqlite 时 db 保持 null，后续全部 NRE
	/// </summary>
	public static class DB
	{
		private static IDbConnection? db;

		public static Dictionary<int, DBInfo> PlayerBuffs { get; private set; } = new();

		public static bool Connected => db != null;

		public static void Connect()
		{
			string storage = (TShock.Config.Settings.StorageType ?? "sqlite").ToLowerInvariant();

			switch (storage)
			{
				case "mysql":
				{
					string[] host = (TShock.Config.Settings.MySqlHost ?? "localhost").Split(':');
					db = new MySqlConnection
					{
						ConnectionString = string.Format(
							"Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
							host[0],
							host.Length == 1 ? "3306" : host[1],
							TShock.Config.Settings.MySqlDbName,
							TShock.Config.Settings.MySqlUsername,
							TShock.Config.Settings.MySqlPassword)
					};
					break;
				}

				case "sqlite":
				{
					string path = Path.Combine(TShock.SavePath, "Permabuffs.sqlite");
					db = new SqliteConnection($"Data Source={path};");
					break;
				}

				default:
					TShock.Log.Error($"[Permabuffs] 不支持的 StorageType: {storage}，回退到 sqlite。");
					db = new SqliteConnection($"Data Source={Path.Combine(TShock.SavePath, "Permabuffs.sqlite")};");
					break;
			}

			if (db == null)
			{
				TShock.Log.Error("[Permabuffs] 数据库连接创建失败，插件将以纯内存模式运行。");
				return;
			}

			SqlTableCreator creator = new SqlTableCreator(db, db.GetSqlQueryBuilder());
			creator.EnsureTableStructure(new SqlTable("Permabuffs",
				new SqlColumn("UserID", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 4 },
				new SqlColumn("ActiveBuffs", MySqlDbType.Text) { Length = 100 }));
		}

		public static bool LoadUserBuffs(int userid)
		{
			if (db == null) return false;

			using QueryResult? result = db.QueryReader("SELECT * FROM Permabuffs WHERE UserID=@0;", userid);
			if (result != null && result.Read())
			{
				// 用索引器赋值而不是 Add：键可能已存在（原版这里会抛 ArgumentException）
				PlayerBuffs[userid] = new DBInfo(result.Get<string>("ActiveBuffs"));
				return true;
			}
			return false;
		}

		public static void AddNewUser(int userid)
		{
			if (db != null)
				db.Query("INSERT INTO Permabuffs (UserId, ActiveBuffs) VALUES (@0, @1);", userid, string.Empty);

			PlayerBuffs[userid] = new DBInfo(string.Empty);
		}

		public static void UpdatePlayerBuffs(int userid, List<int> bufflist)
		{
			if (db == null) return;

			string buffString = string.Join(",", bufflist.Select(p => p.ToString()));
			db.Query("UPDATE Permabuffs SET ActiveBuffs=@0 WHERE UserID=@1;", buffString, userid);
		}

		/// <summary>清空整张表并重置内存缓存。必须由调用方保证只调用一次。</summary>
		public static void ClearDB()
		{
			if (db != null)
				db.Query("DELETE FROM Permabuffs;");

			PlayerBuffs = new Dictionary<int, DBInfo>();
		}

		/// <summary>清除单个玩家。键不存在时安全返回，不会抛异常。</summary>
		public static void ClearPlayerBuffs(int userid)
		{
			if (db != null)
				db.Query("DELETE FROM Permabuffs WHERE UserID=@0;", userid);

			PlayerBuffs[userid] = new DBInfo(string.Empty);
		}

		/// <summary>取玩家数据，不存在时自动补一条空记录。原版到处直接 DB.PlayerBuffs[id] 索引，是运行期崩溃的常见来源。</summary>
		public static DBInfo GetOrCreate(int userid)
		{
			if (PlayerBuffs.TryGetValue(userid, out DBInfo? info) && info != null)
				return info;

			if (LoadUserBuffs(userid))
				return PlayerBuffs[userid];

			AddNewUser(userid);
			return PlayerBuffs[userid];
		}
	}
}
