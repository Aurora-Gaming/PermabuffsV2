using Permabuffs_V2;
using Xunit;

namespace PermabuffsV2.Tests;

/// <summary>
/// 针对本次重写的加固点做回归测试。
/// 重点是"管理员手写 JSON 出错时会不会把服务器搞崩"——
/// 原版在这里会直接 NullReferenceException。
/// </summary>
public class ConfigTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "pbtest_" + Guid.NewGuid().ToString("N"));

	public ConfigTests() => Directory.CreateDirectory(_dir);

	public void Dispose()
	{
		try { Directory.Delete(_dir, true); } catch { /* 测试清理失败不影响结论 */ }
	}

	private string PathFor(string name) => Path.Combine(_dir, name);

	[Fact]
	public void 文件不存在时写出默认配置_且黑名单默认为鱼腮()
	{
		string p = PathFor("new.json");
		Assert.False(File.Exists(p));

		Config c = Config.Read(p);

		Assert.True(File.Exists(p), "首次运行必须落盘配置文件");
		Assert.Equal(new[] { 4 }, c.blacklist);
		Assert.Equal(4, c.blacklist[0]);
	}

	[Fact]
	public void JSON语法错误时回退默认配置_而不是抛异常()
	{
		string p = PathFor("broken.json");
		File.WriteAllText(p, "{ this is not json ][");

		Config c = Config.Read(p);

		Assert.NotNull(c);
		Assert.Equal(new[] { 4 }, c.blacklist);
	}

	[Fact]
	public void 显式null字段被Normalize补齐()
	{
		string p = PathFor("nulls.json");
		File.WriteAllText(p, """
		{
		  "blacklist": null,
		  "autoGrantGroups": null,
		  "buffgroups": null,
		  "regionbuffs": null,
		  "permabuffDuration": 60,
		  "heartbeatTicks": 60
		}
		""");

		Config c = Config.Read(p);

		Assert.NotNull(c.blacklist);
		Assert.NotNull(c.autoGrantGroups);
		Assert.NotNull(c.buffgroups);
		Assert.NotNull(c.regionbuffs);
	}

	[Fact]
	public void 分组缺少buffIDs字段时不再为null()
	{
		string p = PathFor("sparse.json");
		File.WriteAllText(p, """
		{
		  "buffgroups": [ { "groupName": "custom" } ]
		}
		""");

		Config c = Config.Read(p);

		Assert.Single(c.buffgroups);
		Assert.NotNull(c.buffgroups[0].buffIDs);
		Assert.Empty(c.buffgroups[0].buffIDs);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(1, 1)]
	[InlineData(60, 60)]
	[InlineData(3600, 3600)]
	[InlineData(3601, 3600)]
	[InlineData(18000, 3600)]
	public void 心跳间隔被钳制到1到3600(int input, int expected)
	{
		string p = PathFor($"hb_{input}.json");
		// 显式把时长顶到最大，否则"心跳必须远小于时长"这条新规则会先一步收紧心跳，
		// 测的就不是钳制上下限本身了。
		File.WriteAllText(p,
			$$"""{ "heartbeatTicks": {{input}}, "permabuffDuration": 3600000 }""");

		Config c = Config.Read(p);

		Assert.Equal(expected, c.heartbeatTicks);
	}

	[Fact]
	public void 续期时长被钳制到至少60tick()
	{
		string p = PathFor("dur.json");
		// 心跳钉死在最小值，隔离出"时长下限 60"这一条规则
		File.WriteAllText(p, """{ "permabuffDuration": 0, "heartbeatTicks": 1 }""");

		Assert.Equal(60, Config.Read(p).permabuffDuration);
	}

	[Fact]
	public void 黑名单判定只拦黑名单内的ID()
	{
		Config c = new();
		c.blacklist = new[] { 4, 63 };

		Assert.True(c.IsBlacklisted(4));
		Assert.True(c.IsBlacklisted(63));
		Assert.False(c.IsBlacklisted(2));
		Assert.False(c.IsBlacklisted(0), "0 是非法 buff ID，不属于黑名单");
	}

	[Fact]
	public void 黑名单为空时不拦任何东西()
	{
		Config c = new();
		c.blacklist = Array.Empty<int>();

		for (int i = 1; i <= 300; i++)
			Assert.False(c.IsBlacklisted(i));
	}

	[Fact]
	public void 默认只认数字ID_名字解析需显式开启()
	{
		// 纯数字工作流：默认关闭按名字模糊查，避免 GetBuffByName 一次返回多个匹配
		Assert.False(new Config().allowBuffNames);
	}

	[Fact]
	public void 滚动时长必须给心跳留余量()
	{
		string p = PathFor("roll.json");
		// 时长 60 tick 但心跳 3600 tick：每次续期之间 buff 早就掉光了
		File.WriteAllText(p, """{ "permabuffDuration": 60, "heartbeatTicks": 3600 }""");

		Config c = Config.Read(p);

		// Normalize 应收紧心跳，保证一个时长窗口内至少有 3 次续期机会
		Assert.True(c.heartbeatTicks <= c.permabuffDuration / 3,
			$"心跳 {c.heartbeatTicks} 相对时长 {c.permabuffDuration} 过长");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(94)]   // ManaSickness
	[InlineData(383)]  // Kite
	[InlineData(600)]
	[InlineData(43200)]
	public void 累加型buff被识别并挡掉(int id)
	{
		Assert.True(Config.IsAdditiveTimeBuff(id));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(9)]
	[InlineData(111)]
	[InlineData(113)]
	[InlineData(117)]
	public void 普通增益不属于累加型(int id)
	{
		Assert.False(Config.IsAdditiveTimeBuff(id));
	}

	[Fact]
	public void 默认滚动时长为5秒()
	{
		// 泰拉瑞亚固定 60 tick/秒：300 tick = 5 秒，即取消后的最长残留时间。
		Assert.Equal(300, new Config().permabuffDuration);
		Assert.Equal(5, new Config().permabuffDuration / 60);
	}

	[Fact]
	public void allowBuffNames可被配置覆盖()
	{
		string p = PathFor("names.json");
		File.WriteAllText(p, """{ "allowBuffNames": true }""");

		Assert.True(Config.Read(p).allowBuffNames);
	}

	[Fact]
	public void 内置分组111存在且为升序()
	{
		BuffGroup g = new Config().buffgroups.Single(x => x.groupName == "111");

		Assert.Equal(13, g.buffIDs.Count);
		Assert.Equal(g.buffIDs.OrderBy(x => x), g.buffIDs);
		Assert.Equal("111", g.groupPerm);
	}

	[Fact]
	public void 内置分组111不含任何黑名单ID()
	{
		Config c = new();
		BuffGroup g = c.buffgroups.Single(x => x.groupName == "111");

		// 111 组是本版新增的，不该再依赖运行期过滤兜底。
		// 注意 probuffs 原始列表里确实含 4 号鱼腮 —— 那是原作者留下的，
		// 靠 BuffEngine.IsBlocked 在运行期挡掉，不在本测试的约束范围内。
		Assert.All(g.buffIDs, id => Assert.DoesNotContain(id, c.blacklist));
	}

	[Theory]
	[InlineData("probuffs")]
	[InlineData("petbuffs")]
	[InlineData("debuffs")]
	[InlineData("111")]
	public void 所有内置分组均为升序(string groupName)
	{
		BuffGroup g = new Config().buffgroups.Single(x => x.groupName == groupName);

		Assert.Equal(g.buffIDs.OrderBy(x => x), g.buffIDs);
	}
}
