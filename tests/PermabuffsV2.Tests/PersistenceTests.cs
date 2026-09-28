using Permabuffs_V2;
using Xunit;

namespace PermabuffsV2.Tests;

public class DBInfoTests
{
	[Fact]
	public void 空字符串得到空列表()
	{
		Assert.Empty(new DBInfo("").bufflist);
	}

	[Fact]
	public void null列值不再抛NullReference()
	{
		// 原版 `if (activeBuffs != "")` 在列值为 NULL 时直接 NRE
		DBInfo info = new(null);
		Assert.Empty(info.bufflist);
	}

	[Fact]
	public void 正常CSV被解析()
	{
		Assert.Equal(new[] { 1, 2, 3 }, new DBInfo("1,2,3").bufflist);
	}

	[Fact]
	public void 带空格的CSV被正确Trim()
	{
		Assert.Equal(new[] { 1, 2, 3 }, new DBInfo(" 1, 2 , 3 ").bufflist);
	}

	[Fact]
	public void 垃圾数据被跳过而不是让整条记录报废()
	{
		Assert.Equal(new[] { 5 }, new DBInfo("abc,5,,,xyz").bufflist);
	}

	[Fact]
	public void 非法ID被丢弃()
	{
		Assert.Equal(new[] { 7 }, new DBInfo("0,-1,7").bufflist);
	}

	[Fact]
	public void 尾随逗号不会产生空元素()
	{
		Assert.Equal(new[] { 1, 2 }, new DBInfo("1,2,").bufflist);
	}
}

public class BuffIDsTests
{
	[Theory]
	[InlineData(4)]    // Gills —— 有计时器
	[InlineData(2)]    // Regeneration
	[InlineData(63)]   // Panic!
	public void 普通增益有计时器(int id)
	{
		Assert.True(id.BuffHasTimer());
	}

	[Theory]
	[InlineData(19)]   // Shadow Orb
	[InlineData(64)]   // Baby Slime
	[InlineData(388)]  // Chillet Ignis
	public void 宠物坐骑类没有计时器(int id)
	{
		Assert.False(id.BuffHasTimer());
	}

	[Fact]
	public void 未列入的ID默认视为有计时器()
	{
		// Terraria 每次加新坐骑都会让这张表过时，未知 ID 一律按"有计时器"处理才安全
		Assert.True(9999.BuffHasTimer());
	}
}
