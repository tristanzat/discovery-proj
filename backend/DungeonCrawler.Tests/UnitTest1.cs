using DungeonCrawler.API.Services;

namespace DungeonCrawler.Tests;

public class QuestRewardServiceTests
{
    [Fact]
    public void ResolveLoot_ReturnsGoblinTrophy_ForGoblinItemCode()
    {
        var service = new QuestRewardService();

        var result = service.ResolveLoot("goblin-ear-trophy");

        Assert.Equal("goblin-ear-trophy", result.ItemCode);
        Assert.Equal("Goblin Ear Trophy", result.ItemName);
        Assert.Equal("common", result.Rarity);
        Assert.Equal(1, result.Quantity);
    }

    [Fact]
    public void ResolveLoot_ReturnsFallbackLoot_ForUnknownItemCode()
    {
        var service = new QuestRewardService();

        var result = service.ResolveLoot("some-mystery-item");

        Assert.Equal("unknown-relic-shard", result.ItemCode);
        Assert.Equal("Unknown Relic Shard", result.ItemName);
        Assert.Equal("uncommon", result.Rarity);
        Assert.Equal(1, result.Quantity);
    }

    [Fact]
    public void ResolveLoot_ReturnsFallbackLoot_ForNullItemCode()
    {
        var service = new QuestRewardService();

        var result = service.ResolveLoot(null);

        Assert.Equal("unknown-relic-shard", result.ItemCode);
        Assert.Equal(1, result.Quantity);
    }
}
